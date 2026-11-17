Import-Module (Join-Path $PSScriptRoot '../LicensedMenuCI.psm1') -Force -DisableNameChecking


Describe 'Unity package manifest UTF-8 protocol' {
 It 'writes strict UTF-8 without a BOM while preserving non-ASCII dependency paths and parsed data' {
  $directory=Join-Path $TestDrive ('manifest-'+[char]0x00e9)
  New-Item -ItemType Directory -Path $directory|Out-Null
  $path=Join-Path $directory 'manifest.json'
  $unicode='file:E:/Donn'+[char]0x00e9+'es/Menu-'+[char]0x4e16+[char]0x754c
  $manifest=[pscustomobject]@{dependencies=[ordered]@{'com.michael-tiller.mtoolkit'=$unicode;'com.unity.inputsystem'='1.14.2'};testables=@('com.michael-tiller.mtoolkit')}
  Write-CiPackageManifest $manifest $path
  $bytes=[IO.File]::ReadAllBytes($path)
  $text=[Text.UTF8Encoding]::new($false,$true).GetString($bytes)
  [int]$text[0]|Should Not Be 65279
  $parsed=$text|ConvertFrom-Json
  $parsed.dependencies.'com.michael-tiller.mtoolkit'|Should Be $unicode
  $parsed.dependencies.'com.unity.inputsystem'|Should Be '1.14.2'
  @($parsed.testables).Count|Should Be 1
  $parsed.testables[0]|Should Be 'com.michael-tiller.mtoolkit'
 }
}

Describe 'Runner hook file-command handoff' {
 It 'passes values from a hidden child through GITHUB_ENV without changing its parent environment' {
  $handoff=Join-Path $TestDrive 'github-env';[IO.File]::WriteAllText($handoff,'')
  $child=Join-Path $TestDrive 'hook-child.ps1'
  $module=(Resolve-Path (Join-Path $PSScriptRoot '../LicensedMenuCI.psm1')).Path.Replace("'","''")
  $code="Import-Module '"+$module+"' -Force -DisableNameChecking"+[Environment]::NewLine+"Write-CiRunnerEnvironment -EnvironmentFile '"+$handoff.Replace("'","''")+"' -ToolsPath 'E:\Trusted Tools' -ApprovalPath 'E:\Trusted Tools\approval.json'"
  [IO.File]::WriteAllText($child,$code)
  $originalTools=$env:LICENSED_MENU_TOOLS;$originalApproval=$env:LICENSED_MENU_APPROVAL
  $start=[Diagnostics.ProcessStartInfo]::new()
  $start.FileName=Join-Path $PSHOME 'powershell.exe';$start.Arguments='-NoProfile -ExecutionPolicy Bypass -File "'+$child+'"'
  $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
  $process=[Diagnostics.Process]::Start($start)
  $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
  $process.WaitForExit(10000)|Should Be $true
  $exitCode=$process.ExitCode;$process.Dispose()
  $exitCode|Should Be 0
  $entries=@{}
  foreach($line in [IO.File]::ReadAllLines($handoff)){ $separator=$line.IndexOf('=');$entries[$line.Substring(0,$separator)]=$line.Substring($separator+1) }
  $entries['LICENSED_MENU_TOOLS']|Should Be 'E:\Trusted Tools'
  $entries['LICENSED_MENU_APPROVAL']|Should Be 'E:\Trusted Tools\approval.json'
  $step=Join-Path $TestDrive 'job-step.ps1'
  [IO.File]::WriteAllText($step,'if($env:LICENSED_MENU_TOOLS -ceq ''E:\Trusted Tools'' -and $env:LICENSED_MENU_APPROVAL -ceq ''E:\Trusted Tools\approval.json''){exit 0}else{exit 19}')
  $start.Arguments='-NoProfile -ExecutionPolicy Bypass -File "'+$step+'"'
  foreach($key in $entries.Keys){$start.EnvironmentVariables[$key]=$entries[$key]}
  $next=[Diagnostics.Process]::Start($start)
  $next.WaitForExit(10000)|Should Be $true
  $next.ExitCode|Should Be 0
  $next.Dispose()
  $env:LICENSED_MENU_TOOLS|Should Be $originalTools
  $env:LICENSED_MENU_APPROVAL|Should Be $originalApproval
 }
 It 'rejects file-command injection in a control path' {
  $file=Join-Path $TestDrive 'injected-env';[IO.File]::WriteAllText($file,'')
  {Write-CiRunnerEnvironment $file ('E:\Tools'+[char]10+'OTHER=bad') 'E:\approval.json'}|Should Throw
 }
}

Describe 'One candidate menu sample per isolated Unity project' {
 BeforeEach {
  $owned=Join-Path $TestDrive 'sample-run';$project=Join-Path $owned 'project';$private=Join-Path $owned 'private'
  $package=Join-Path $TestDrive 'candidate-package';$sample=Join-Path $package 'Samples~/HierarchicalActionMenus'
  New-Item -ItemType Directory -Force -Path (Join-Path $project 'Assets/Samples/MToolKit/1.21.0/HierarchicalActionMenus'),$private,(Join-Path $sample 'Runtime')|Out-Null
  [IO.File]::WriteAllText((Join-Path $package 'package.json'),'{"name":"com.michael-tiller.mtoolkit","version":"1.22.0"}')
  [IO.File]::WriteAllText((Join-Path $sample 'ActionMenuGallery.unity'),'candidate saved scene')
  [IO.File]::WriteAllText((Join-Path $sample 'ActionMenuGallery.unity.meta'),'candidate scene guid')
  [IO.File]::WriteAllText((Join-Path $sample 'Runtime/Menu.asmdef'),'candidate assembly')
  [IO.File]::WriteAllText((Join-Path $sample 'Runtime/Menu.asmdef.meta'),'candidate assembly guid')
  [IO.File]::WriteAllText(($sample+'.meta'),'candidate folder guid')
  $old=Join-Path $project 'Assets/Samples/MToolKit/1.21.0/HierarchicalActionMenus'
  [IO.File]::WriteAllText((Join-Path $old 'Old.asmdef'),'preserve imported assembly')
  [IO.File]::WriteAllText(($old+'.meta'),'preserve imported folder guid')
 }
 It 'parks the complete old import with metadata then stages the candidate once at its package version' {
  $destination=Set-CiMenuSampleSnapshot $project $package $private $owned
  $destination|Should Be ([IO.Path]::GetFullPath((Join-Path $project 'Assets/Samples/MToolKit/1.22.0/HierarchicalActionMenus')))
  [IO.File]::ReadAllText((Join-Path $destination 'Runtime/Menu.asmdef.meta'))|Should Be 'candidate assembly guid'
  [IO.File]::ReadAllText(($destination+'.meta'))|Should Be 'candidate folder guid'
  @(Get-ChildItem -LiteralPath (Join-Path $project 'Assets') -Recurse -File -Filter '*.asmdef').Count|Should Be 1
  Test-Path -LiteralPath $old|Should Be $false
  Test-Path -LiteralPath ($old+'.meta')|Should Be $false
  $parked=@(Get-ChildItem -LiteralPath $private -Recurse -File -Filter Old.asmdef)
  $parked.Count|Should Be 1
  [IO.File]::ReadAllText($parked[0].FullName)|Should Be 'preserve imported assembly'
  @(Get-ChildItem -LiteralPath $private -Recurse -File -Filter 'HierarchicalActionMenus.meta').Count|Should Be 1
  [IO.File]::ReadAllText((Join-Path $sample 'Runtime/Menu.asmdef'))|Should Be 'candidate assembly'
 }
 It 'refuses a candidate missing its saved gallery before parking existing assets' {
  Remove-Item -LiteralPath (Join-Path $sample 'ActionMenuGallery.unity')
  {Set-CiMenuSampleSnapshot $project $package $private $owned}|Should Throw
  Test-Path -LiteralPath (Join-Path $old 'Old.asmdef')|Should Be $true
 }
 It 'refuses parking inside Assets or moving from a foreign checkout' {
  {Set-CiMenuSampleSnapshot $project $package (Join-Path $project 'Assets/private') $owned}|Should Throw
  {Set-CiMenuSampleSnapshot (Join-Path $TestDrive 'foreign-project') $package $private $owned}|Should Throw
  Test-Path -LiteralPath (Join-Path $old 'Old.asmdef')|Should Be $true
 }
}
Describe 'Whole dependency snapshots' {
 It 'clones the exact committed snapshot without adopting dirty files or a linked git file' {
  $source=Join-Path $TestDrive 'committed-source';$owned=Join-Path $TestDrive 'clone-owner'
  New-Item -ItemType Directory -Force -Path $source,$owned|Out-Null
  $null=& git init --quiet $source
  $file=Join-Path $source 'package.txt';[IO.File]::WriteAllText($file,'committed')
  $null=& git -C $source add package.txt
  $null=& git -C $source -c user.name=Fixture -c user.email=fixture@example.invalid commit --quiet -m fixture
  $revision=(& git -C $source rev-parse HEAD).Trim()
  [IO.File]::WriteAllText($file,'uncommitted')
  $target=Copy-CiGitSnapshot $source $revision (Join-Path $owned 'snapshot') $owned
  [IO.File]::ReadAllText((Join-Path $target 'package.txt'))|Should Be 'committed'
  (& git -C $target rev-parse HEAD).Trim()|Should Be $revision
  (Get-Item -LiteralPath (Join-Path $target '.git') -Force).PSIsContainer|Should Be $true
 }
 It 'copies missing nested data and accepts identical existing dependency files without overwriting' {
  $source=Join-Path $TestDrive 'vendor'
  $owned=Join-Path $TestDrive 'run'
  New-Item -ItemType Directory -Force -Path (Join-Path $source 'nested'),$owned|Out-Null
  [IO.File]::WriteAllText((Join-Path $source 'nested/file.dll'),'licensed fixture')
  [IO.File]::WriteAllText((Join-Path $source 'nested/file.dll.meta'),'fixture guid')
  $target=Join-Path $owned 'project/Assets/ThirdParty'
  Copy-CiDependencyRoot $source $target $owned
  [IO.File]::ReadAllText((Join-Path $target 'nested/file.dll.meta'))|Should Be 'fixture guid'
  Copy-CiDependencyRoot $source $target $owned
  [IO.File]::ReadAllText((Join-Path $target 'nested/file.dll.meta'))|Should Be 'fixture guid'
  {Copy-CiDependencyRoot $source (Join-Path $TestDrive 'foreign') $owned}|Should Throw
 }
 It 'records a deterministic digest that changes when a nested dependency or metadata changes' {
  $source=Join-Path $TestDrive 'digest-vendor';New-Item -ItemType Directory -Force -Path $source|Out-Null
  $meta=Join-Path $source 'asset.meta';[IO.File]::WriteAllText($meta,'original')
  $before=Get-CiTreeDigest $source
  $same=Get-CiTreeDigest $source
  $before.Hash|Should Be $same.Hash
  $before.FileCount|Should Be 1
  [IO.File]::WriteAllText($meta,'changed')
  (Get-CiTreeDigest $source).Hash|Should Not Be $before.Hash
 }
 It 'rejects an unavailable pinned Git commit before creating a destination' {
  $source=Join-Path $TestDrive 'source-git';$owned=Join-Path $TestDrive 'snapshot-owner'
  New-Item -ItemType Directory -Force -Path $source,$owned|Out-Null
  $null=& git init --quiet $source
  $target=Join-Path $owned 'snapshot'
  {Copy-CiGitSnapshot $source ('f'*40) $target $owned}|Should Throw
  Test-Path -LiteralPath $target|Should Be $false
 }
}

Describe 'Required ignored dependency roots with tracked candidate subsets' {
 BeforeEach {
  $owned=Join-Path $TestDrive ('hydrate-'+[Guid]::NewGuid().ToString('N'))
  $source=Join-Path $owned 'source';$project=Join-Path $owned 'project'
  New-Item -ItemType Directory -Force -Path $source,$project|Out-Null
  $null=& git init --quiet $project
  $null=& git init --quiet $source
  [IO.File]::WriteAllText((Join-Path $project '.gitattributes'),"* text=auto eol=lf`n*.bin -text`n")
  [IO.File]::WriteAllText((Join-Path $source '.gitattributes'),"* text=auto eol=lf`n*.bin -text`n")
  $required=@('Assets/ThirdParty','Assets/Plugins','Assets/Packages','Assets/ExternalDependencyManager','Assets/TextMesh Pro','Packages/com.ivanmurzak.unity.mcp')
  $ignore=@()
  foreach($relative in $required){
   $root=Join-Path $source $relative
   New-Item -ItemType Directory -Force -Path (Join-Path $root 'nested')|Out-Null
   [IO.File]::WriteAllText((Join-Path $root 'nested/payload.bin'),$relative)
   [IO.File]::WriteAllText((Join-Path $root 'nested/payload.bin.meta'),'nested fixture metadata')
   $ignore+=($relative+'/')
   if($relative.StartsWith('Assets/')){
    [IO.File]::WriteAllText(($root+'.meta'),'root fixture metadata')
    $ignore+=($relative+'.meta')
   }
  }
  [IO.File]::WriteAllLines((Join-Path $project '.gitignore'),[string[]]$ignore)
 }
 It 'recognizes directory-only ignore rules before missing dependency directories exist' {
  Test-Path -LiteralPath (Join-Path $project 'Assets/ThirdParty')|Should Be $false
  Copy-CiIgnoredDependencies $source $project $owned
  foreach($relative in $required){
   [IO.File]::ReadAllText((Join-Path $project ($relative+'/nested/payload.bin')))|Should Be $relative
  }
  [IO.File]::ReadAllText((Join-Path $project 'Assets/ThirdParty.meta'))|Should Be 'root fixture metadata'
 }
 It 'hydrates whole ignored roots around identical tracked files while retaining candidate-only source and metadata' {
  $tracked='Assets/Plugins/nested/payload.bin';$meta='Assets/Plugins.meta';$candidate='Assets/Plugins/nested/candidate.cs'
  New-Item -ItemType Directory -Force -Path (Join-Path $project 'Assets/Plugins/nested')|Out-Null
  [IO.File]::WriteAllBytes((Join-Path $project $tracked),[IO.File]::ReadAllBytes((Join-Path $source $tracked)))
  [IO.File]::WriteAllBytes((Join-Path $project $meta),[IO.File]::ReadAllBytes((Join-Path $source $meta)))
  [IO.File]::WriteAllText((Join-Path $project $candidate),'candidate-owned source')
  $null=& git -C $project add -f -- $tracked $meta $candidate
  if($LASTEXITCODE -ne 0){throw 'Fixture failed to track the candidate subset.'}
  $before=(Get-FileHash -LiteralPath (Join-Path $project $tracked) -Algorithm SHA256).Hash
  Copy-CiIgnoredDependencies $source $project $owned
  (Get-FileHash -LiteralPath (Join-Path $project $tracked) -Algorithm SHA256).Hash|Should Be $before
  [IO.File]::ReadAllText((Join-Path $project $candidate))|Should Be 'candidate-owned source'
  [IO.File]::ReadAllText((Join-Path $project $meta))|Should Be 'root fixture metadata'
  [IO.File]::ReadAllText((Join-Path $project 'Assets/Plugins/nested/payload.bin.meta'))|Should Be 'nested fixture metadata'
 }
 It 'rejects conflicting existing files before copying any missing sibling or overwriting candidate bytes' {
  $sourceRoot=Join-Path $source 'Assets/Plugins';$target=Join-Path $project 'Assets/Plugins'
  New-Item -ItemType Directory -Force -Path (Join-Path $target 'nested')|Out-Null
  [IO.File]::WriteAllText((Join-Path $target 'nested/payload.bin'),'different candidate bytes')
  [IO.File]::WriteAllText((Join-Path $sourceRoot 'brand-new.bin'),'must not be copied after conflict')
  {Copy-CiDependencyRoot $sourceRoot $target $owned}|Should Throw
  [IO.File]::ReadAllText((Join-Path $target 'nested/payload.bin'))|Should Be 'different candidate bytes'
  Test-Path -LiteralPath (Join-Path $target 'brand-new.bin')|Should Be $false
 }
 It 'preserves tracked Git text checkout bytes and approves only their canonical EOL-equivalent digest' {
  $relative='Assets/Plugins/nested/tracked.txt';$sourceFile=Join-Path $source $relative;$targetFile=Join-Path $project $relative
  New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($targetFile))|Out-Null
  [IO.File]::WriteAllText($sourceFile,"same text`nsecond line`n")
  [IO.File]::WriteAllText($targetFile,"same text`r`nsecond line`r`n")
  $null=& git -C $source add -f -- $relative
  $null=& git -C $project add -f -- $relative
  $before=(Get-FileHash -LiteralPath $targetFile -Algorithm SHA256).Hash
  $receipt=Join-Path $owned 'private/hydration.json'
  Copy-CiIgnoredDependencies $source $project $owned -ReceiptPath $receipt
  (Get-FileHash -LiteralPath $targetFile -Algorithm SHA256).Hash|Should Be $before
  $expected=@(Get-CiIgnoredDependencyManifest $source|Where-Object Path -eq 'Assets/Plugins')[0]
  $actual=@(Get-CiIgnoredDependencyManifest $project|Where-Object Path -eq 'Assets/Plugins')[0]
  $actual.Hash|Should Be $expected.Hash
  $actual.RawHash|Should Not Be $expected.RawHash
  $record=Get-Content -LiteralPath $receipt -Raw|ConvertFrom-Json
  $equivalent=@($record.existingFiles|Where-Object mode -eq 'existing-git-text-eol')
  $equivalent.Count|Should Be 1
  $equivalent[0].sourceSha256|Should Not Be $equivalent[0].destinationSha256
 }
 It 'rejects EOL differences for ignored files and binary data and rejects changed tracked text' {
  foreach($case in @('ignored.txt','tracked.bin','changed.txt')){
   $from=Join-Path $owned ('case-source/'+$case);$to=Join-Path $project ('Assets/Plugins/'+$case)
   New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($from)),([IO.Path]::GetDirectoryName($to))|Out-Null
   $a=if($case -eq 'tracked.bin'){"binary`0data`n"}else{"source text`n"}
   $b=if($case -eq 'tracked.bin'){"binary`0data`r`n"}elseif($case -eq 'changed.txt'){"different text`r`n"}else{"source text`r`n"}
   [IO.File]::WriteAllText($from,$a);[IO.File]::WriteAllText($to,$b)
   if($case -ne 'ignored.txt'){$null=& git -C $project add -f -- ('Assets/Plugins/'+$case)}
   $before=(Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash
   {Copy-CiDependencyRoot $from $to $owned -GitProject $project}|Should Throw
   (Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash|Should Be $before
  }
 }
}

Describe 'Licensed menu CI trust and launcher boundaries' {
  BeforeEach {
    $root = Join-Path $TestDrive 'owned'
    New-Item -ItemType Directory -Force -Path $root | Out-Null
  }
  It 'accepts an actual descendant without creating it' {
    Assert-CiChildPath $root (Join-Path $root 'integration/project') | Should Be ([IO.Path]::GetFullPath((Join-Path $root 'integration/project')))
  }
  It 'rejects root itself' { { Assert-CiChildPath $root $root } | Should Throw }
  It 'rejects sibling-prefix paths' { { Assert-CiChildPath $root ($root+'-foreign/data') } | Should Throw }
  It 'rejects traversal outside owned root' { { Assert-CiChildPath $root (Join-Path $root '../foreign') } | Should Throw }
  It 'quotes a space-containing Windows argument' { Quote-CiCmdArgument 'E:\CI Run\project' | Should Be '"E:\CI Run\project"' }
  It 'rejects cmd expansion and command separators even within quotes' {
    foreach ($bad in @('E:\%USERPROFILE%','E:\a&b','E:\a|b','E:\a^b','E:\a!b',('E:\a'+[char]34+'b'))) {
      { Quote-CiCmdArgument $bad } | Should Throw
    }
  }
  It 'uses hidden cmd start wait without quit or no graphics for rendered tests' {
    $start=New-CiUnityStartInfo 'E:\CI Run\project' 'E:\Unity\Unity.exe' 'E:\CI Run\result.xml' 'E:\CI Run\unity.log' 'PlayMode' 'MToolKit.ContextMenus.Samples.Tests'
    $start.FileName | Should Be 'cmd.exe'
    $start.Arguments | Should Match '/c start "" /wait'
    $start.Arguments | Should Match '"-runTests"'
    $start.Arguments | Should Not Match '\-quit|\-nographics'
    $start.UseShellExecute | Should Be $false
    $start.CreateNoWindow | Should Be $true
  }
  It 'repairs missing ALLUSERSPROFILE in the child only' {
    $start=New-CiUnityStartInfo 'E:\project' 'E:\Unity.exe' 'E:\result.xml' 'E:\unity.log' 'EditMode' 'MToolKit.Tests.Editor.ContextMenus' @{ALLUSERSPROFILE=' '}
    $start.EnvironmentVariables['ALLUSERSPROFILE'] | Should Be ([Environment]::GetFolderPath([Environment+SpecialFolder]::CommonApplicationData))
  }
  It 'preserves explicit ALLUSERSPROFILE' {
    $start=New-CiUnityStartInfo 'E:\project' 'E:\Unity.exe' 'E:\result.xml' 'E:\unity.log' 'EditMode' 'MToolKit.Tests.Editor.ContextMenus' @{ALLUSERSPROFILE='E:\Custom ProgramData'}
    $start.EnvironmentVariables['ALLUSERSPROFILE'] | Should Be 'E:\Custom ProgramData'
  }
  It 'requires the approved canonical run and same-repository pull request' {
    $a=[pscustomobject]@{Repository='michael-tiller/MToolKit';RunId='42';RunAttempt='1';Actor='michael-tiller';WorkflowSha=('a'*40);MergeSha=('b'*40);HeadSha=('c'*40);WorkflowPath='.github/workflows/licensed-menu.yml';PullRequest=7}
    $e=@{GITHUB_REPOSITORY=$a.Repository;GITHUB_RUN_ID='42';GITHUB_RUN_ATTEMPT='1';GITHUB_ACTOR=$a.Actor;GITHUB_TRIGGERING_ACTOR=$a.Actor;GITHUB_EVENT_NAME='pull_request';GITHUB_WORKFLOW_SHA=$a.WorkflowSha;GITHUB_SHA=$a.MergeSha;GITHUB_REF='refs/pull/7/merge';GITHUB_JOB='menu-ci';GITHUB_WORKFLOW_REF=($a.Repository+'/'+$a.WorkflowPath+'@refs/pull/7/merge')}
    $event=[pscustomobject]@{number=7;pull_request=[pscustomobject]@{head=[pscustomobject]@{sha=$a.HeadSha;repo=[pscustomobject]@{full_name=$a.Repository}};base=[pscustomobject]@{repo=[pscustomobject]@{full_name=$a.Repository}}}}
    Assert-CiJobApproval $a $e $event | Should Be $true
    $event.pull_request.head.repo.full_name='attacker/fork'
    { Assert-CiJobApproval $a $e $event } | Should Throw
  }
  It 'rejects a different run or changed workflow despite a matching actor' {
    $a=[pscustomobject]@{Repository='michael-tiller/MToolKit';RunId='42';RunAttempt='1';Actor='michael-tiller';WorkflowSha=('a'*40);MergeSha=('b'*40);HeadSha=('c'*40);WorkflowPath='.github/workflows/licensed-menu.yml';PullRequest=7}
    $e=@{GITHUB_REPOSITORY=$a.Repository;GITHUB_RUN_ID='43';GITHUB_RUN_ATTEMPT='1';GITHUB_ACTOR=$a.Actor;GITHUB_TRIGGERING_ACTOR=$a.Actor;GITHUB_EVENT_NAME='pull_request';GITHUB_WORKFLOW_SHA=$a.WorkflowSha;GITHUB_SHA=$a.MergeSha;GITHUB_REF='refs/pull/7/merge';GITHUB_JOB='menu-ci';GITHUB_WORKFLOW_REF=($a.Repository+'/'+$a.WorkflowPath+'@refs/pull/7/merge')}
    $event=[pscustomobject]@{number=7;pull_request=[pscustomobject]@{head=[pscustomobject]@{sha=$a.HeadSha;repo=[pscustomobject]@{full_name=$a.Repository}};base=[pscustomobject]@{repo=[pscustomobject]@{full_name=$a.Repository}}}}
    { Assert-CiJobApproval $a $e $event } | Should Throw
    $e.GITHUB_RUN_ID='42';$e.GITHUB_WORKFLOW_SHA=('d'*40)
    { Assert-CiJobApproval $a $e $event } | Should Throw
  }
}
Describe 'Fresh Unity XML is the CI authority' {
  BeforeEach {
    $path=Join-Path $TestDrive 'result.xml'
    $started=[DateTime]::UtcNow.AddSeconds(-5)
    $green='<test-run total="1" passed="1" failed="0" result="Passed"><test-suite><test-case fullname="MToolKit.Tests.Editor.ContextMenus.MenuSessionTests.Command" result="Passed"/></test-suite></test-run>'
    [IO.File]::WriteAllText($path,$green)
  }
  It 'accepts fresh all-passed matching tests and zero exit' {
    $r=Read-CiTestResult $path $started 0 @('MToolKit.Tests.Editor.ContextMenus')
    $r.Total | Should Be 1
    $r.Outcome | Should Be 'Passed'
  }
  It 'rejects exit and XML disagreement' { { Read-CiTestResult $path $started 2 @('MToolKit.Tests.Editor.ContextMenus') } | Should Throw }
  It 'rejects stale XML' { { Read-CiTestResult $path ([DateTime]::UtcNow.AddMinutes(1)) 0 @('MToolKit.Tests.Editor.ContextMenus') } | Should Throw }
  It 'rejects empty, skipped, and mismatched selections' {
    { Read-CiTestResult $path $started 0 @('Other.Namespace') } | Should Throw
    [IO.File]::WriteAllText($path,$green.Replace('result="Passed"','result="Skipped"'))
    { Read-CiTestResult $path $started 0 @('MToolKit.Tests.Editor.ContextMenus') } | Should Throw
    [IO.File]::WriteAllText($path,'<test-run total="0" passed="0" failed="0" result="Passed"/>')
    { Read-CiTestResult $path $started 0 @('MToolKit.Tests.Editor.ContextMenus') } | Should Throw
  }
  It 'rejects XML external entities' {
    [IO.File]::WriteAllText($path,'<!DOCTYPE x [<!ENTITY secret SYSTEM "file:///C:/private.txt">]><test-run total="1" passed="1" failed="0" result="Passed">&secret;</test-run>')
    { Read-CiTestResult $path $started 0 @('MToolKit.Tests.Editor.ContextMenus') } | Should Throw
  }
  It 'publishes only fixed-schema numeric evidence and public commit identities' {
    $r=[pscustomobject]@{Total=1;Passed=1;Failed=0;ExitCode=0;Outcome='Passed';PrivatePath='E:\Private\dirigible';Message='license secret';Xml='private xml'}
    $safe=ConvertTo-CiSafeSummary $r 'michael-tiller/MToolKit' ('a'*40) ('b'*40) 'framework'
    $text=$safe|ConvertTo-Json -Compress
    $text | Should Not Match 'Private|secret|Xml'
    $safe.total | Should Be 1
    $safe.revision | Should Be ('a'*40)
  }
}

Describe 'Focused framework regression selection' {
 It 'selects the authoring namespace and both affected prompt fixtures and requires their results' {
  $tokens=$null;$errors=$null
  $scriptPath=Join-Path $PSScriptRoot '../Invoke-LicensedMenuCI.ps1'
  $ast=[Management.Automation.Language.Parser]::ParseFile($scriptPath,[ref]$tokens,[ref]$errors)
  @($errors).Count|Should Be 0
  $framework=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Run-UnityStage' -and $node.CommandElements.Count -gt 1 -and $node.CommandElements[1].Value -eq 'framework'},$true))
  $framework.Count|Should Be 1
  $selected=@($framework[0].CommandElements[3].SafeGetValue().Split(';'))
  $required=@($framework[0].CommandElements[4].SafeGetValue())
  foreach($scope in @('MToolKit.Tests.Editor.ContextMenus','MToolKit.ContextMenus.Samples.Tests.Editor','MToolKit.Editor.ContextMenus.Tests','MToolKit.Tests.Editor.Runtime.Input.InputPromptServiceTests','MToolKit.Tests.Editor.Runtime.Input.InputPromptForeignObservationReviewTests')) {
   ($selected -contains $scope)|Should Be $true
   ($required -contains $scope)|Should Be $true
  }
 }
}

Describe 'Focused action presentation integration selection' {
 It 'selects the affected authored and runtime projection fixtures and requires their results' {
  $tokens=$null;$errors=$null
  $scriptPath=Join-Path $PSScriptRoot '../Invoke-LicensedMenuCI.ps1'
  $ast=[Management.Automation.Language.Parser]::ParseFile($scriptPath,[ref]$tokens,[ref]$errors)
  @($errors).Count|Should Be 0
  $assignment=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left -is [Management.Automation.Language.VariableExpressionAst] -and $node.Left.VariablePath.UserPath -ceq 'integrationClasses'},$true))
  $assignment.Count|Should Be 1
  $selected=@($assignment[0].Right.FindAll({param($node) $node -is [Management.Automation.Language.StringConstantExpressionAst]},$true)|ForEach-Object{$_.Value})
  foreach($fixture in @('Dirigible.Tests.Editor.Actions.ActionPresentationProjectionTests','Dirigible.Tests.Editor.Actions.AuthoredWorldContextMenuTests')) {
   @($selected|Where-Object{$_ -ceq $fixture}).Count|Should Be 1
  }
  $integration=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Run-UnityStage' -and $node.CommandElements.Count -gt 1 -and $node.CommandElements[1].Value -eq 'integration'},$true))
  $integration.Count|Should Be 1
  $integration[0].CommandElements[4].VariablePath.UserPath|Should Be 'integrationClasses'
 }
}
