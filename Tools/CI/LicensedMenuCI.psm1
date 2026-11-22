Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
function Assert-CiNoReparse {
 param([string]$Path)
 $current=[IO.Path]::GetFullPath($Path)
 while($current) {
  if((Test-Path -LiteralPath $current) -and ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'CI paths cannot cross links or junctions.' }
  $parent=[IO.Path]::GetDirectoryName($current)
  if($parent -eq $current){break};$current=$parent
 }
}
function Assert-CiChildPath {
 param([string]$Root,[string]$Path)
 $r=[IO.Path]::GetFullPath($Root).TrimEnd([char[]]'\/');$p=[IO.Path]::GetFullPath($Path).TrimEnd([char[]]'\/')
 if(-not $p.StartsWith($r+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'CI target must be strictly inside its assigned root.'}
 Assert-CiNoReparse $r;Assert-CiNoReparse $p
 return $p
}
function Quote-CiCmdArgument {
 param([string]$Value)
 if([string]::IsNullOrWhiteSpace($Value) -or $Value -match '[%&|<>^!"\x00-\x1f]'){throw 'Unsafe Windows command argument.'}
 return '"'+$Value+'"'
}
function New-CiUnityStartInfo {
 param([string]$ProjectPath,[string]$UnityPath,[string]$ResultPath,[string]$LogPath,[ValidateSet('EditMode','PlayMode')][string]$Platform,[string]$Filter,[hashtable]$Environment=@{})
 if($Filter -notmatch '^[A-Za-z_][A-Za-z0-9_.;]+$'){throw 'Invalid focused test filter.'}
 $args=@($UnityPath,'-batchmode','-runTests','-projectPath',$ProjectPath,'-testPlatform',$Platform,'-testFilter',$Filter,'-testResults',$ResultPath,'-logFile',$LogPath,'-UNITY_MCP_KEEP_CONNECTED=false')
 $start=[Diagnostics.ProcessStartInfo]::new()
 $start.FileName='cmd.exe';$start.Arguments='/d /s /c start "" /wait '+(($args|ForEach-Object{Quote-CiCmdArgument $_}) -join ' ')
 $start.WorkingDirectory=$ProjectPath;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
 foreach($key in $Environment.Keys){$start.EnvironmentVariables[$key]=[string]$Environment[$key]}
 if([string]::IsNullOrWhiteSpace($start.EnvironmentVariables['ALLUSERSPROFILE'])){$start.EnvironmentVariables['ALLUSERSPROFILE']=[Environment]::GetFolderPath([Environment+SpecialFolder]::CommonApplicationData)}
 return $start
}
function Start-CiQueuedUnity {
 [CmdletBinding()]
 param(
  [Parameter(Mandatory)][string]$ProjectPath,
  [Parameter(Mandatory)][Diagnostics.ProcessStartInfo]$StartInfo,
  [Parameter(Mandatory)][string]$TaskId,
  [Parameter(Mandatory)][string]$Owner,
  [Parameter(Mandatory)][string]$Summary,
  [Parameter(Mandatory)][ValidateRange(1,86400)][int]$ExpectedSeconds,
  [Parameter(Mandatory)][ValidateRange(1,86400)][int]$QueueTimeoutSeconds
 )
 # The consumer snapshot is pinned by the reviewed job approval. Its adapter
 # selects Captain's configured queue or the consumer's checked-in fallback.
 $project=(Resolve-Path -LiteralPath $ProjectPath -ErrorAction Stop).Path
 $adapter=Join-Path $project 'Tools/Captain/UnityEditorLaunch.psm1'
 if(-not(Test-Path -LiteralPath $adapter -PathType Leaf)){throw 'Pinned consumer is missing the Captain Unity queue adapter.'}
 $module=Import-Module -Name $adapter -Force -PassThru -DisableNameChecking -ErrorAction Stop
 $launch=$module.ExportedCommands['Start-DirigibleUnityEditor']
 if($null -eq $launch){throw 'Pinned consumer adapter does not export the Captain Unity queue launcher.'}
 $process=& $launch -StartInfo $StartInfo -ProjectPath $project -TaskId $TaskId -Owner $Owner `
  -Summary $Summary -ExpectedSeconds $ExpectedSeconds -QueueTimeoutSeconds $QueueTimeoutSeconds
 if($process -isnot [Diagnostics.Process]){throw 'Captain Unity queue did not return the owned launcher process.'}
 return $process
}
function Assert-CiJobApproval {
 param($Approval,[hashtable]$Environment,$Event)
 foreach($field in @('WorkflowSha','MergeSha','HeadSha')){if($Approval.$field -notmatch '^[a-f0-9]{40}$'){throw 'Approval requires exact Git identities.'}}
 $expected=@{GITHUB_REPOSITORY=[string]$Approval.Repository;GITHUB_RUN_ID=[string]$Approval.RunId;GITHUB_RUN_ATTEMPT=[string]$Approval.RunAttempt;GITHUB_ACTOR=[string]$Approval.Actor;GITHUB_TRIGGERING_ACTOR=[string]$Approval.Actor;GITHUB_EVENT_NAME='pull_request';GITHUB_WORKFLOW_SHA=[string]$Approval.WorkflowSha;GITHUB_SHA=[string]$Approval.MergeSha;GITHUB_REF=('refs/pull/'+$Approval.PullRequest+'/merge');GITHUB_JOB='menu-ci'}
 $expected.GITHUB_WORKFLOW_REF=$Approval.Repository+'/'+$Approval.WorkflowPath+'@'+$expected.GITHUB_REF
 foreach($key in $expected.Keys){if(-not $Environment.ContainsKey($key) -or $Environment[$key] -cne $expected[$key]){throw 'Job does not match the externally approved run.'}}
 if([int]$Event.number -ne [int]$Approval.PullRequest -or $Event.pull_request.head.sha -cne $Approval.HeadSha -or $Event.pull_request.head.repo.full_name -cne $Approval.Repository -or $Event.pull_request.base.repo.full_name -cne $Approval.Repository){throw 'Forks and changed PR heads cannot use the licensed host.'}
 return $true
}
function Read-CiTestResult {
 param([string]$XmlPath,[DateTime]$StartedUtc,[int]$ExitCode,[string[]]$AllowedPrefixes)
 $file=Get-Item -LiteralPath $XmlPath -ErrorAction Stop
 if($file.Length -le 0 -or $file.Length -gt 8388608 -or $file.LastWriteTimeUtc -lt $StartedUtc){throw 'Missing, stale or oversized Unity result.'}
 $settings=[Xml.XmlReaderSettings]::new();$settings.DtdProcessing=[Xml.DtdProcessing]::Prohibit;$settings.XmlResolver=$null
 $reader=[Xml.XmlReader]::Create($file.FullName,$settings)
 try{$document=[Xml.XmlDocument]::new();$document.XmlResolver=$null;$document.Load($reader)}finally{$reader.Dispose()}
 $run=$document.SelectSingleNode('/test-run');$cases=@($document.SelectNodes('//test-case'))
 if($null -eq $run -or $cases.Count -le 0 -or [int]$run.total -ne $cases.Count){throw 'Unity result is incomplete or empty.'}
 foreach($case in $cases){
  $accepted=$false
  foreach($prefix in $AllowedPrefixes){if(([string]$case.fullname).StartsWith($prefix+'.',[StringComparison]::Ordinal)){$accepted=$true;break}}
  if(-not $accepted -or $case.result -ne 'Passed'){throw 'Focused Unity tests failed, skipped, or exceeded their declared selection.'}
 }
 if($ExitCode -ne 0 -or $run.result -ne 'Passed' -or [int]$run.failed -ne 0 -or [int]$run.passed -ne $cases.Count){throw 'Unity exit and XML do not establish a passing run.'}
 return [pscustomobject]@{Total=$cases.Count;Passed=$cases.Count;Failed=0;ExitCode=$ExitCode;Outcome='Passed'}
}
function ConvertTo-CiSafeSummary {
 param($Result,[string]$Repository,[string]$Revision,[string]$DependencyRevision,[string]$Stage)
 if($Repository -notin @('michael-tiller/MToolKit','michael-tiller/dirigible') -or $Revision -notmatch '^[a-f0-9]{40}$' -or $DependencyRevision -notmatch '^[a-f0-9]{40}$' -or $Stage -notin @('framework','integration','gallery')){throw 'Invalid public evidence identity.'}
 return [pscustomobject][ordered]@{schemaVersion=1;repository=$Repository;revision=$Revision;mtoolkitRevision=$DependencyRevision;stage=$Stage;total=[int]$Result.Total;passed=[int]$Result.Passed;failed=[int]$Result.Failed;exitCode=[int]$Result.ExitCode;outcome=[string]$Result.Outcome}
}
function Assert-CiInteractiveIdentity {
 param([string]$ExpectedIdentity)
 if([Security.Principal.WindowsIdentity]::GetCurrent().Name -cne $ExpectedIdentity -or $ExpectedIdentity -notmatch '\\metag$' -or [Diagnostics.Process]::GetCurrentProcess().SessionId -eq 0){throw 'CI requires the approved licensed interactive metag session.'}
}
function Invoke-CiGit {
 param([string[]]$Arguments)
 $output=@(& git @Arguments 2>&1)
 if($LASTEXITCODE -ne 0){throw 'Pinned local Git snapshot operation failed.'}
 return ($output -join [Environment]::NewLine).Trim()
}
function Copy-CiGitSnapshot {
 param([string]$Source,[string]$Revision,[string]$Destination,[string]$OwnedRoot)
 $target=Assert-CiChildPath $OwnedRoot $Destination;Assert-CiNoReparse $Source
 if($Revision -notmatch '^[a-f0-9]{40}$' -or (Test-Path -LiteralPath $target)){throw 'Snapshot requires an exact revision and fresh destination.'}
 if((Invoke-CiGit @('-C',$Source,'rev-parse',($Revision+'^{commit}'))) -cne $Revision){throw 'Unavailable pinned commit.'}
 $null=Invoke-CiGit @('clone','--quiet','--no-hardlinks','--no-checkout','--local',$Source,$target)
 $null=Invoke-CiGit @('-C',$target,'checkout','--quiet','--detach',$Revision)
 if((Invoke-CiGit @('-C',$target,'rev-parse','HEAD')) -cne $Revision){throw 'Snapshot identity mismatch.'}
 return $target
}
function Copy-CiDependencyRoot {
 param([string]$Source,[string]$Destination,[string]$OwnedRoot,[string]$GitProject,[Collections.Generic.List[object]]$ExistingFiles)
 $target=Assert-CiChildPath $OwnedRoot $Destination;Assert-CiNoReparse $Source
 $root=Get-Item -LiteralPath $Source -Force -ErrorAction Stop
 $entries=@($root);if($root.PSIsContainer){$entries+=@(Get-ChildItem -LiteralPath $root.FullName -Force -Recurse -ErrorAction Stop|Sort-Object FullName)}
 $tracked=Get-CiTrackedTextPaths $GitProject $target
 $directories=[Collections.Generic.List[string]]::new();$copies=[Collections.Generic.List[object]]::new()
 # Preflight the complete root before copying: a candidate collision never gets overwritten.
 foreach($entry in $entries){
  if($entry.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Dependency roots cannot contain links.'}
  $relative=$entry.FullName.Substring($root.FullName.Length).TrimStart([char[]]'\/')
  $to=if($relative){Join-Path $target $relative}else{$target}
  Assert-CiNoReparse $to
  if(Test-Path -LiteralPath $to){
   $existing=Get-Item -LiteralPath $to -Force
   if($existing.PSIsContainer -ne $entry.PSIsContainer){throw 'Dependency destination has a conflicting file or directory.'}
   if($entry.PSIsContainer){continue}
   $a=(Get-FileHash -LiteralPath $entry.FullName -Algorithm SHA256).Hash;$b=(Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash
   $mode='existing-identical';$canonical=$null
   if($a -cne $b){
    if(-not $tracked.Contains($to)){throw 'Dependency file differs from the candidate snapshot.'}
    $canonical=Get-CiUtf8LfHash $entry.FullName;$other=Get-CiUtf8LfHash $to
    if(-not $canonical -or $canonical -cne $other){throw 'Tracked dependency differs beyond Git text line endings.'}
    $mode='existing-git-text-eol'
   }
   if($null -ne $ExistingFiles){$ExistingFiles.Add([pscustomobject]@{source=$entry.FullName;destination=$to;sourceSha256=$a;destinationSha256=$b;canonicalSha256=$canonical;mode=$mode})}
  }elseif($entry.PSIsContainer){$directories.Add($to)}else{$copies.Add([pscustomobject]@{source=$entry.FullName;destination=$to})}
 }
 foreach($directory in $directories){$null=[IO.Directory]::CreateDirectory($directory)}
 foreach($copy in $copies){$null=[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($copy.destination));[IO.File]::Copy($copy.source,$copy.destination,$false)}
}
function Copy-CiIgnoredDependencies {
 param([string]$SourceProject,[string]$DestinationProject,[string]$OwnedRoot,[string]$ReceiptPath)
 $receipt=$null
 if($ReceiptPath){
  $receipt=Assert-CiChildPath $OwnedRoot $ReceiptPath
  foreach($project in @($SourceProject,$DestinationProject)){
   $base=[IO.Path]::GetFullPath($project).TrimEnd([char[]]'\/')
   if($receipt.StartsWith($base+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Hydration receipts must stay outside source and destination projects.'}
  }
  if(Test-Path -LiteralPath $receipt){throw 'Hydration receipt must be fresh.'}
 }
 $existing=[Collections.Generic.List[object]]::new();$complete=$false
 try{
  foreach($relative in @('Assets/ThirdParty','Assets/Plugins','Assets/Packages','Assets/ExternalDependencyManager','Assets/TextMesh Pro','Packages/com.ivanmurzak.unity.mcp')){
   $ignored=Invoke-CiGit @('-C',$DestinationProject,'check-ignore','--no-index','--',($relative+'/'))
   if([string]::IsNullOrWhiteSpace($ignored)){throw 'Required root is not ignored by the tested repository.'}
   Copy-CiDependencyRoot (Join-Path $SourceProject $relative) (Join-Path $DestinationProject $relative) $OwnedRoot -GitProject $DestinationProject -ExistingFiles $existing
   if($relative.StartsWith('Assets/')){Copy-CiDependencyRoot (Join-Path $SourceProject ($relative+'.meta')) (Join-Path $DestinationProject ($relative+'.meta')) $OwnedRoot -GitProject $DestinationProject -ExistingFiles $existing}
  }
  $complete=$true
 }finally{
  if($receipt){
   $null=[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($receipt))
   [pscustomobject]@{schemaVersion=1;complete=$complete;sourceProject=$SourceProject;destinationProject=$DestinationProject;existingFiles=@($existing.ToArray())}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $receipt -Encoding utf8
  }
 }
}
function Get-CiTrackedTextPaths {
 param([string]$Project,[string]$Path)
 $result=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
 if(-not $Project){return ,$result}
 $projectPath=[IO.Path]::GetFullPath($Project).TrimEnd([char[]]'\/');$full=Assert-CiChildPath $projectPath $Path
 $relative=$full.Substring($projectPath.Length+1).Replace('\','/')
 $files=(Invoke-CiGit @('-C',$projectPath,'ls-files','-z','--',$relative)).Split([char[]]@([char]0),[StringSplitOptions]::RemoveEmptyEntries)
 foreach($file in $files){
  $attrs=(Invoke-CiGit @('-C',$projectPath,'check-attr','-z','text','eol','--',$file)).Split([char]0)
  if($attrs.Count -ge 6 -and $attrs[2] -ne 'unset' -and ($attrs[2] -in @('set','auto') -or $attrs[5] -in @('lf','crlf'))){$null=$result.Add([IO.Path]::GetFullPath((Join-Path $projectPath $file)))}
 }
 return ,$result
}
function Get-CiUtf8LfHash {
 param([string]$Path)
 try{$text=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($Path))}catch [Text.DecoderFallbackException]{return $null}
 if($text.IndexOf([char]0) -ge 0){return $null}
 $hash=[Security.Cryptography.SHA256]::Create()
 try{return [BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($text.Replace("`r`n","`n")))).Replace('-','')}finally{$hash.Dispose()}
}
function Get-CiTreeDigest {
 param([string]$Path,[string]$GitProject)
 Assert-CiNoReparse $Path
 $entry=Get-Item -LiteralPath $Path -Force
 $files=@(if($entry.PSIsContainer){Get-ChildItem -LiteralPath $entry.FullName -File -Force -Recurse|Sort-Object FullName}else{$entry})
 $lines=[Text.StringBuilder]::new();$rawLines=[Text.StringBuilder]::new();$tracked=Get-CiTrackedTextPaths $GitProject $entry.FullName
 foreach($file in $files){
  Assert-CiNoReparse $file.FullName
  $relative=if($entry.PSIsContainer){$file.FullName.Substring($entry.FullName.TrimEnd('\').Length+1).Replace('\','/')}else{$file.Name}
  $raw=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash;$canonical=$null
  if($tracked.Contains($file.FullName)){$canonical=Get-CiUtf8LfHash $file.FullName}
  if(-not $canonical){$canonical=$raw}
  $null=$lines.Append($relative).Append([char]0).Append($canonical).Append([char]10)
  $null=$rawLines.Append($relative).Append([char]0).Append($raw).Append([char]10)
 }
 $hash=[Security.Cryptography.SHA256]::Create()
 try{$digest=[BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($lines.ToString()))).Replace('-','');$rawDigest=[BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($rawLines.ToString()))).Replace('-','')}finally{$hash.Dispose()}
 return [pscustomobject]@{Hash=$digest;RawHash=$rawDigest;FileCount=$files.Count}
}
function Get-CiIgnoredDependencyManifest {
 param([string]$Project)
 $result=@()
 foreach($relative in @('Assets/ThirdParty','Assets/Plugins','Assets/Packages','Assets/ExternalDependencyManager','Assets/TextMesh Pro','Packages/com.ivanmurzak.unity.mcp')){
  $paths=@($relative);if($relative.StartsWith('Assets/')){$paths+=($relative+'.meta')}
  foreach($path in $paths){$digest=Get-CiTreeDigest (Join-Path $Project $path) -GitProject $Project;$result+=@([pscustomobject]@{Path=$path;Hash=$digest.Hash;RawHash=$digest.RawHash;FileCount=$digest.FileCount})}
 }
 return $result
}
function Write-CiPackageManifest {
 param($Manifest,[string]$Path)
 $json=$Manifest|ConvertTo-Json -Depth 30
 [IO.File]::WriteAllText($Path,$json,[Text.UTF8Encoding]::new($false))
}
function Write-CiRunnerEnvironment {
 param([string]$EnvironmentFile,[string]$ToolsPath,[string]$ApprovalPath)
 foreach($path in @($EnvironmentFile,$ToolsPath,$ApprovalPath)){
  if([string]::IsNullOrWhiteSpace($path) -or $path -match '[\x00\r\n]'){throw 'Invalid runner file-command path.'}
 }
 if(-not(Test-Path -LiteralPath $EnvironmentFile -PathType Leaf)){throw 'Runner environment file is missing.'}
 [IO.File]::AppendAllText($EnvironmentFile,'LICENSED_MENU_TOOLS='+$ToolsPath+[Environment]::NewLine+'LICENSED_MENU_APPROVAL='+$ApprovalPath+[Environment]::NewLine)
}
function Set-CiMenuSampleSnapshot {
 param([string]$Project,[string]$Package,[string]$PrivateRoot,[string]$OwnedRoot)
 $projectPath=Assert-CiChildPath $OwnedRoot $Project
 $privatePath=Assert-CiChildPath $OwnedRoot $PrivateRoot
 if($privatePath -eq $projectPath -or $privatePath.StartsWith($projectPath+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
  $projectPath.StartsWith($privatePath+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Parked sample evidence must be outside the integration project.'}
 $source=Join-Path $Package 'Samples~/HierarchicalActionMenus'
 Assert-CiNoReparse $source
 $packageInfo=Get-Content -LiteralPath (Join-Path $Package 'package.json') -Raw|ConvertFrom-Json
 if($packageInfo.name -cne 'com.michael-tiller.mtoolkit' -or $packageInfo.version -notmatch '^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?(?:\+[A-Za-z0-9.-]+)?$'){throw 'Invalid candidate sample package identity.'}
 $scenes=@(Get-ChildItem -LiteralPath $source -File -Recurse -Filter 'ActionMenuGallery.unity')
 if($scenes.Count -ne 1 -or -not(Test-Path -LiteralPath ($source+'.meta') -PathType Leaf)){throw 'Candidate sample must include exactly one saved ActionMenuGallery scene and folder metadata.'}
 $samplesRoot=Join-Path $projectPath 'Assets/Samples/MToolKit'
 $destination=Assert-CiChildPath $OwnedRoot (Join-Path $samplesRoot ($packageInfo.version+'/HierarchicalActionMenus'))
 $parkRoot=Assert-CiChildPath $OwnedRoot (Join-Path $privatePath 'parked-menu-samples')
 if(Test-Path -LiteralPath $parkRoot){throw 'Parked sample destination must be fresh.'}
 $moves=@()
 if(Test-Path -LiteralPath $samplesRoot){
  Assert-CiNoReparse $samplesRoot
  foreach($version in @(Get-ChildItem -LiteralPath $samplesRoot -Directory -Force)){
   $import=Join-Path $version.FullName 'HierarchicalActionMenus'
   if(-not(Test-Path -LiteralPath $import)){continue}
   $from=Assert-CiChildPath $OwnedRoot $import
   foreach($item in @(Get-ChildItem -LiteralPath $from -Recurse -Force)){
    if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Imported samples cannot contain links.'}
   }
   $to=Assert-CiChildPath $OwnedRoot (Join-Path $parkRoot ($version.Name+'/HierarchicalActionMenus'))
   $digest=Get-CiTreeDigest $from
   $moves+=@([pscustomobject]@{Source=$from;Destination=$to;Hash=$digest.Hash;FileCount=$digest.FileCount})
   if(Test-Path -LiteralPath ($from+'.meta')){
    $metaFrom=Assert-CiChildPath $OwnedRoot ($from+'.meta');$metaTo=Assert-CiChildPath $OwnedRoot ($to+'.meta')
    $meta=Get-CiTreeDigest $metaFrom
    $moves+=@([pscustomobject]@{Source=$metaFrom;Destination=$metaTo;Hash=$meta.Hash;FileCount=$meta.FileCount})
   }
  }
 }
 # Resolve every mutation target before the first move, retaining an original-path/hash manifest.
 $null=New-Item -ItemType Directory -Path $parkRoot
 $moves|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $parkRoot 'original-inputs.json') -Encoding utf8
 foreach($move in $moves){
  $from=Assert-CiChildPath $OwnedRoot $move.Source;$to=Assert-CiChildPath $OwnedRoot $move.Destination
  if(Test-Path -LiteralPath $to){throw 'A parked destination already exists.'}
  $null=New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($to))
  Move-Item -LiteralPath $from -Destination $to
 }
 Copy-CiDependencyRoot $source $destination $OwnedRoot
 Copy-CiDependencyRoot ($source+'.meta') ($destination+'.meta') $OwnedRoot
 return $destination
}
Export-ModuleMember -Function *
