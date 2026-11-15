[CmdletBinding()]
param([Parameter(Mandatory)][string]$ApprovalPath)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
Import-Module (Join-Path $PSScriptRoot 'LicensedMenuCI.psm1') -Force -DisableNameChecking
$privateRoot=$null
try {
 $approval=Get-Content -LiteralPath $ApprovalPath -Raw|ConvertFrom-Json
 $config=Get-Content -LiteralPath $approval.HostConfigPath -Raw|ConvertFrom-Json
 Assert-CiInteractiveIdentity $config.ExpectedIdentity
 $envMap=@{};Get-ChildItem Env:|ForEach-Object{$envMap[$_.Name]=$_.Value}
 $event=Get-Content -LiteralPath $env:GITHUB_EVENT_PATH -Raw|ConvertFrom-Json
 $null=Assert-CiJobApproval $approval $envMap $event
 if($config.UnityVersion -cne '6000.3.10f1' -or -not(Test-Path -LiteralPath $config.UnityExecutable -PathType Leaf)){throw 'Installed Unity version is not configured.'}
 if([Diagnostics.FileVersionInfo]::GetVersionInfo($config.UnityExecutable).ProductVersion -notlike '6000.3.10f1*'){throw 'Installed Unity executable version mismatch.'}
 $ownedRoot=[IO.Path]::GetFullPath($config.RunsRoot)
 Assert-CiNoReparse $ownedRoot
 $runRoot=Assert-CiChildPath $ownedRoot (Join-Path $ownedRoot ($approval.Repository.Split('/')[1]+'-'+$approval.RunId+'-'+$approval.RunAttempt))
 if(Test-Path -LiteralPath $runRoot){throw 'A CI run directory already exists. Preserve it and use a new run attempt.'}
 $null=New-Item -ItemType Directory -Path $runRoot
 $privateRoot=Join-Path $runRoot 'private';$null=New-Item -ItemType Directory -Path $privateRoot
 $publicRoot=Join-Path $env:GITHUB_WORKSPACE 'menu-ci-public'
 if(Test-Path -LiteralPath $publicRoot){throw 'Public evidence destination must be fresh.'}
 $null=New-Item -ItemType Directory -Path $publicRoot
 # The installed tools and approval are copied outside the runner application/workspace.
 # Only aggregate evidence is ever returned to Actions; all child output stays private.
 $isFramework=$approval.Repository -ceq 'michael-tiller/MToolKit'
 $consumerRevision=if($isFramework){[string]$config.ConsumerRevision}else{[string]$approval.MergeSha}
 $consumerSource=if($isFramework){[string]$config.DependencyProjectRoot}else{[string]$approval.SourceRoot}
 $project=Copy-CiGitSnapshot $consumerSource $consumerRevision (Join-Path $runRoot 'project') $runRoot
 Copy-CiIgnoredDependencies $config.DependencyProjectRoot $project $runRoot -ReceiptPath (Join-Path $privateRoot 'hydrated-inputs.json')
 $copiedRoots=@(Get-CiIgnoredDependencyManifest $project)
 if($copiedRoots.Count -ne $approval.IgnoredDependencyManifest.Count){throw 'Dependency root manifest is incomplete.'}
 foreach($input in $approval.IgnoredDependencyManifest){
  $actual=@($copiedRoots|Where-Object Path -ceq $input.Path)
  if($actual.Count -ne 1 -or $actual[0].Hash -cne $input.Hash -or $actual[0].FileCount -ne $input.FileCount){throw 'Ignored dependency contents differ from the approved host snapshot.'}
 }
 $copiedRoots|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $privateRoot 'ignored-inputs.json') -Encoding utf8
 $version=Get-Content -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -Raw
 if($version -notmatch '(?m)^m_EditorVersion: 6000\.3\.10f1\s*$'){throw 'Consumer project Unity version mismatch.'}
 $manifestPath=Join-Path $project 'Packages/manifest.json'
 $manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
 $lock=Get-Content -LiteralPath (Join-Path $project '.captain-sdlc/dependency-lock.json') -Raw|ConvertFrom-Json
 $dependencies=Join-Path $runRoot 'dependencies';$null=New-Item -ItemType Directory -Path $dependencies
 $resolved=@()
 $mtkRoot=$null;$mtkRevision=$null
 foreach($entry in @($manifest.dependencies.PSObject.Properties)){
  if($entry.Value -notlike 'file:*'){continue}
  $name=$entry.Name
  if($isFramework -and $name -ceq 'com.michael-tiller.mtoolkit'){
   $mtkRevision=[string]$approval.MergeSha
   $target=Copy-CiGitSnapshot $approval.SourceRoot $mtkRevision (Join-Path $dependencies $name) $runRoot
   $resolved+=@([ordered]@{name=$name;commit=$mtkRevision;override='explicit public package candidate';consumerLockedCommit=@($lock.local_packages|Where-Object name -ceq 'MToolKit'|Select-Object -ExpandProperty source_commit)})
  }else{
   $spec=@($config.LocalPackages|Where-Object Name -ceq $name)
   if($spec.Count -eq 1){
    $spec=$spec[0]
    if($spec.PSObject.Properties.Name -contains 'Commit' -and $spec.Commit){
     $locked=@($lock.local_packages|Where-Object name -ceq $spec.LockName)
     if($locked.Count -ne 1 -or $locked[0].source_commit -cne $spec.Commit){throw 'Local package pin differs from the consumer dependency lock.'}
     $target=Copy-CiGitSnapshot $spec.Source $spec.Commit (Join-Path $dependencies $name) $runRoot
     $resolved+=@([ordered]@{name=$name;commit=$spec.Commit})
     if($name -ceq 'com.michael-tiller.mtoolkit'){$mtkRevision=[string]$spec.Commit}
    }elseif($spec.PSObject.Properties.Name -contains 'Sha256' -and $spec.Sha256 -match '^[a-fA-F0-9]{64}$'){
     if((Get-FileHash -LiteralPath $spec.Source -Algorithm SHA256).Hash -cne $spec.Sha256.ToUpperInvariant()){throw 'Local package archive hash mismatch.'}
     $target=Join-Path $dependencies ($name+'.tgz')
     Copy-CiDependencyRoot $spec.Source $target $runRoot
     $resolved+=@([ordered]@{name=$name;sha256=$spec.Sha256})
    }else{throw 'Local package needs a Git commit or archive digest.'}
   }elseif($spec.Count -eq 0){
    $relative=([string]$entry.Value).Substring(5)
    if([IO.Path]::IsPathRooted($relative)){throw 'Unmapped external file dependency.'}
    $target=Assert-CiChildPath $project (Join-Path (Join-Path $project 'Packages') $relative)
    if(-not(Test-Path -LiteralPath $target)){throw 'Missing embedded file dependency.'}
    $resolved+=@([ordered]@{name=$name;source='tracked or hydrated embedded package'})
   }else{throw 'Duplicate local package mapping.'}
  }
  if(Test-Path -LiteralPath $target -PathType Container){
   $package=Get-Content -LiteralPath (Join-Path $target 'package.json') -Raw|ConvertFrom-Json
   if($package.name -cne $name){throw 'Resolved package name mismatch.'}
  }
  $entry.Value='file:'+([IO.Path]::GetFullPath($target).Replace('\','/'))
  if($name -ceq 'com.michael-tiller.mtoolkit'){$mtkRoot=$target}
 }
 if(-not $mtkRoot -or $mtkRevision -notmatch '^[a-f0-9]{40}$'){throw 'No pinned MToolKit integration source.'}
 # This rewrites only the generated integration manifest. The committed lock is retained unchanged.
 $manifest|ConvertTo-Json -Depth 30|Set-Content -LiteralPath $manifestPath -Encoding utf8
 $resolved|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $privateRoot 'resolved-inputs.json') -Encoding utf8
 $sampleDestination=Set-CiMenuSampleSnapshot $project $mtkRoot $privateRoot $runRoot
 $bridge=Join-Path $project 'Assets/Editor/LicensedMenuCiEntry.cs'
 $null=New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($bridge))
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Unity/MenuCiSetup.cs.txt') -Destination $bridge
 $openEditors=@(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'"|Where-Object{$_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($project.Replace('/','\'))})
 if($openEditors.Count){throw 'Integration checkout already has an Editor.'}
 function Run-UnityStage([string]$Stage,[string]$Platform,[string]$Filter,[string[]]$Prefixes,[switch]$Setup){
  $stageRoot=Join-Path $privateRoot $Stage;$null=New-Item -ItemType Directory -Path $stageRoot
  $xml=Join-Path $stageRoot 'result.xml';$log=Join-Path $stageRoot 'unity.log'
  $info=New-CiUnityStartInfo $project $config.UnityExecutable $xml $log $Platform $Filter
  if($Setup){
   $args=@($config.UnityExecutable,'-batchmode','-projectPath',$project,'-executeMethod','LicensedMenuCiEntry.ValidateSavedGallery','-logFile',$log,'-UNITY_MCP_KEEP_CONNECTED=false')
   $info.Arguments='/d /s /c start "" /wait '+(($args|ForEach-Object{Quote-CiCmdArgument $_}) -join ' ')
  }
  $info.Arguments+=' "-screen-width" "1600" "-screen-height" "1000"'
  $started=[DateTime]::UtcNow
  $process=[Diagnostics.Process]::Start($info)
  if($null -eq $process){throw 'Unity launcher did not start.'}
  try{
   if(-not $process.WaitForExit(1800000)){
    # Exact process tree only; no foreign Editor or desktop process is stopped.
    $stop=[Diagnostics.ProcessStartInfo]::new();$stop.FileName='taskkill.exe';$stop.Arguments='/PID '+$process.Id+' /T /F';$stop.UseShellExecute=$false;$stop.CreateNoWindow=$true
    $kill=[Diagnostics.Process]::Start($stop);if($kill){$null=$kill.WaitForExit(10000);$kill.Dispose()}
    throw 'Owned Unity stage timed out.'
   }
   $exitCode=$process.ExitCode
  }finally{$process.Dispose()}
  $logFile=Get-Item -LiteralPath $log
  if($logFile.Length -le 0 -or $logFile.Length -gt 134217728 -or $logFile.LastWriteTimeUtc -lt $started){throw 'Missing, stale or oversized Unity log.'}
  $logText=Get-Content -LiteralPath $log -Raw
  if($logText -match 'No valid Unity Editor license found|error CS\d+|Scripts have compiler errors|project:resolve-packages --> 500'){throw 'Unity environment or compilation failed.'}
  if($Setup){
   if($exitCode -ne 0 -or $logText -notmatch 'LICENSED_MENU_CI_SETUP_PASS'){throw 'Gallery setup failed.'}
  }else{
   $result=Read-CiTestResult $xml $started $exitCode $Prefixes
   ConvertTo-CiSafeSummary $result $approval.Repository $approval.MergeSha $mtkRevision $Stage|ConvertTo-Json -Compress|Set-Content -LiteralPath (Join-Path $publicRoot ($Stage+'.json')) -Encoding utf8
  }
 }
 Run-UnityStage 'setup' 'EditMode' 'MToolKit.Tests.Editor.ContextMenus' @() -Setup
 Run-UnityStage 'framework' 'EditMode' 'MToolKit.Tests.Editor.ContextMenus;MToolKit.ContextMenus.Samples.Tests.Editor' @('MToolKit.Tests.Editor.ContextMenus','MToolKit.ContextMenus.Samples.Tests.Editor')
 if(-not $isFramework){
  $integrationClasses=@('Dirigible.Tests.Editor.UI.ContextMenu','Dirigible.Tests.Editor.UI.ContextPriorityFanTests','Dirigible.Tests.Editor.UI.ContextMenuAccessibleInputTests','Dirigible.Tests.Editor.Actions.ControllerContextActivationTests','Dirigible.Tests.Editor.Actions.ActionMenuOwnershipTests','Dirigible.Tests.Editor.Actions.ActionInteractionOwnershipTests','Dirigible.Tests.Editor.Actions.ActionInputConsumerTests','Dirigible.Tests.Editor.Actions.WorldContextDispatcherTests')
  Run-UnityStage 'integration' 'EditMode' ($integrationClasses -join ';') $integrationClasses
 }
 Run-UnityStage 'gallery' 'PlayMode' 'MToolKit.ContextMenus.Samples.Tests.MenuGalleryPlayModeTests;MToolKit.Tests.Runtime.ContextMenus.MenuHostCanvasLifetimeTests;MToolKit.Tests.Runtime.ContextMenus.MenuHostActivePrefabReentrancyTests' @('MToolKit.ContextMenus.Samples.Tests.MenuGalleryPlayModeTests','MToolKit.Tests.Runtime.ContextMenus.MenuHostCanvasLifetimeTests','MToolKit.Tests.Runtime.ContextMenus.MenuHostActivePrefabReentrancyTests')
 Write-Output 'Licensed menu CI passed. Public aggregate evidence is ready.'
 exit 0
}catch{
 # Never stream private Unity output, paths, exceptions or XML into public MTK Actions logs.
 if($privateRoot -and (Test-Path -LiteralPath $privateRoot)){$_|Out-String|Set-Content -LiteralPath (Join-Path $privateRoot 'failure.txt') -Encoding utf8}
 [Console]::Error.WriteLine('Licensed menu CI failed. Inspect retained private host evidence.')
 exit 1
}
