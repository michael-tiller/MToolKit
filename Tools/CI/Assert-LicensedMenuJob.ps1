Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
try {
 Import-Module (Join-Path $PSScriptRoot 'LicensedMenuCI.psm1') -Force -DisableNameChecking
 $approvalPath=Join-Path $PSScriptRoot 'approval.json'
 $approval=Get-Content -LiteralPath $approvalPath -Raw|ConvertFrom-Json
 $config=Get-Content -LiteralPath $approval.HostConfigPath -Raw|ConvertFrom-Json
 if((Get-FileHash -LiteralPath $approval.HostConfigPath -Algorithm SHA256).Hash -cne $approval.HostConfigHash){throw 'Approved host configuration changed.'}
 Assert-CiInteractiveIdentity $config.ExpectedIdentity
 $environment=@{};Get-ChildItem Env:|ForEach-Object{$environment[$_.Name]=$_.Value}
 $event=Get-Content -LiteralPath $env:GITHUB_EVENT_PATH -Raw|ConvertFrom-Json
 $null=Assert-CiJobApproval $approval $environment $event
 foreach($input in $approval.ToolHashes){
  $file=Assert-CiChildPath $PSScriptRoot (Join-Path $PSScriptRoot $input.Path)
  if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -cne $input.Hash){throw 'Reviewed tools changed.'}
 }
 Write-CiRunnerEnvironment $env:GITHUB_ENV $PSScriptRoot $approvalPath
 Write-Output 'Approved licensed menu job.'
 exit 0
}catch{
 [Console]::Error.WriteLine('This job is not approved for the licensed host.')
 exit 1
}
