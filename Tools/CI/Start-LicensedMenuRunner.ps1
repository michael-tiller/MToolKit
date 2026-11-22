[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$ReviewedSourceRoot,[Parameter(Mandatory)][string]$HostConfigPath,
 [Parameter(Mandatory)][string]$ControlDirectory,[Parameter(Mandatory)][string]$RunnerDirectory,
 [Parameter(Mandatory)][ValidateSet('michael-tiller/MToolKit','michael-tiller/dirigible')][string]$Repository,
 [Parameter(Mandatory)][ValidatePattern('^[0-9]+$')][string]$RunId,[Parameter(Mandatory)][ValidatePattern('^[1-9][0-9]*$')][string]$RunAttempt,
 [Parameter(Mandatory)][ValidateRange(1,2147483647)][int]$PullRequest,
 [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$MergeSha,
 [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$WorkflowSha,
 [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$HeadSha,
 [ValidatePattern('^[a-zA-Z0-9-]+$')][string]$Actor='michael-tiller',[switch]$StartRunner
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'LicensedMenuCI.psm1') -Force -DisableNameChecking
$config=Get-Content -LiteralPath $HostConfigPath -Raw|ConvertFrom-Json
Assert-CiInteractiveIdentity $config.ExpectedIdentity
$control=Assert-CiChildPath $config.ControlRoot $ControlDirectory
$runner=Assert-CiChildPath $config.RunnersRoot $RunnerDirectory
if(Test-Path -LiteralPath $control){throw 'Use a fresh control directory for this exact run attempt.'}
if(-not(Test-Path -LiteralPath (Join-Path $runner 'bin/Runner.Listener.exe')) -or (Test-Path -LiteralPath (Join-Path $runner '.runner')) -or (Test-Path -LiteralPath (Join-Path $runner '.credentials'))){throw 'Provide a fresh, independently verified official Windows runner extraction.'}
# Preparation never fetches, registers, installs a service, or changes global configuration.
if((Invoke-CiGit @('-C',$ReviewedSourceRoot,'rev-parse',($MergeSha+'^{commit}'))) -cne $MergeSha){throw 'Reviewed merge commit is not available locally.'}
if($WorkflowSha -cne $MergeSha){throw 'Use the workflow from the exact reviewed PR merge commit.'}
$workflowPath='.github/workflows/licensed-menu.yml'
$null=Invoke-CiGit @('-C',$ReviewedSourceRoot,'cat-file','-e',($MergeSha+':'+$workflowPath))
$null=New-Item -ItemType Directory -Path $control
$archive=Join-Path $control 'reviewed-tools.zip'
$null=Invoke-CiGit @('-C',$ReviewedSourceRoot,'archive','--format=zip',('--output='+$archive),$MergeSha,'Tools/CI',$workflowPath)
$extracted=Join-Path $control 'reviewed';Expand-Archive -LiteralPath $archive -DestinationPath $extracted
$tools=Join-Path $extracted 'Tools/CI'
$copiedConfig=Join-Path $control 'host-config.json';Copy-Item -LiteralPath $HostConfigPath -Destination $copiedConfig
$hashes=@(Get-ChildItem -LiteralPath $tools -File -Recurse|ForEach-Object{[ordered]@{Path=$_.FullName.Substring($tools.Length+1).Replace('\','/');Hash=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
$approval=[ordered]@{schemaVersion=1;Repository=$Repository;RunId=$RunId;RunAttempt=$RunAttempt;PullRequest=$PullRequest;Actor=$Actor;MergeSha=$MergeSha;WorkflowSha=$WorkflowSha;HeadSha=$HeadSha;WorkflowPath=$workflowPath;SourceRoot=(Resolve-Path -LiteralPath $ReviewedSourceRoot).Path;HostConfigPath=$copiedConfig;WorkflowHash=(Get-FileHash -LiteralPath (Join-Path $extracted $workflowPath) -Algorithm SHA256).Hash;ToolHashes=$hashes}
$approvalPath=Join-Path $tools 'approval.json';$approval|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $approvalPath -Encoding utf8
$approval.IgnoredDependencyManifest=@(Get-CiIgnoredDependencyManifest $config.DependencyProjectRoot)
$approval.HostConfigHash=(Get-FileHash -LiteralPath $copiedConfig -Algorithm SHA256).Hash
$approval|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $approvalPath -Encoding utf8
$label='licensed-menu-'+$RunId+'-'+$RunAttempt
$plan=[ordered]@{label=$label;repository=$Repository;runId=$RunId;attempt=$RunAttempt;started=$false;runner=$runner;tools=$tools;approval=$approvalPath}
$plan|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $control 'runner-plan.json') -Encoding utf8
if(-not $StartRunner){Write-Output 'Reviewed plan prepared. No runner registered or started.';exit 0}
if([string]::IsNullOrWhiteSpace($env:ACTIONS_RUNNER_INPUT_TOKEN)){throw 'Supply the short-lived repository registration token in ACTIONS_RUNNER_INPUT_TOKEN.'}
$info=[Diagnostics.ProcessStartInfo]::new()
$info.FileName=Join-Path $runner 'bin/Runner.Listener.exe'
$info.Arguments='configure --unattended --ephemeral --no-default-labels --url https://github.com/'+$Repository+' --name '+$label+' --labels '+$label+' --work _work'
$info.WorkingDirectory=$runner;$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
$configure=[Diagnostics.Process]::Start($info)
$stdout=$configure.StandardOutput.ReadToEndAsync();$stderr=$configure.StandardError.ReadToEndAsync()
if(-not $configure.WaitForExit(120000)){throw 'Registration timed out; inspect the owned process before retrying.'}
$stdout.Result|Set-Content -LiteralPath (Join-Path $control 'configure.log');$stderr.Result|Set-Content -LiteralPath (Join-Path $control 'configure-error.log')
$code=$configure.ExitCode;$configure.Dispose()
if($code -ne 0){throw 'Registration failed; inspect private logs.'}
# Only this process's child environment is changed, and it is restored after hidden launch.
$savedHook=$env:ACTIONS_RUNNER_HOOK_JOB_STARTED;$savedToken=$env:ACTIONS_RUNNER_INPUT_TOKEN;$savedPolicy=$env:PSExecutionPolicyPreference
try{
 $env:ACTIONS_RUNNER_HOOK_JOB_STARTED=Join-Path $tools 'Assert-LicensedMenuJob.ps1';$env:ACTIONS_RUNNER_INPUT_TOKEN=$null
 $env:PSExecutionPolicyPreference='Bypass'
 $runArguments='/d /s /c "'+(Quote-CiCmdArgument (Join-Path $runner 'run.cmd'))+'"'
 $process=Start-Process -FilePath 'cmd.exe' -ArgumentList $runArguments -WorkingDirectory $runner -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $control 'runner.log') -RedirectStandardError (Join-Path $control 'runner-error.log')
}finally{$env:ACTIONS_RUNNER_HOOK_JOB_STARTED=$savedHook;$env:ACTIONS_RUNNER_INPUT_TOKEN=$savedToken;$env:PSExecutionPolicyPreference=$savedPolicy}
$plan.started=$true;$plan.pid=$process.Id
$plan|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $control 'runner-plan.json') -Encoding utf8
Write-Output ('One-job ephemeral runner started with PID '+$process.Id+'.')
