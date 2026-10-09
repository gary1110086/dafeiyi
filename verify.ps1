param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$taskOutput=if($OutputDirectory){[System.IO.Path]::GetFullPath($OutputDirectory)}else{[System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\轻译'))}
& (Join-Path $PSScriptRoot 'build.ps1') -Test -OutputDirectory $taskOutput
$taskExe = Join-Path $taskOutput '轻译.exe'
$taskReportRoot = Join-Path $taskOutput '验证报告'
New-Item -ItemType Directory -Force -Path $taskReportRoot | Out-Null
foreach ($taskMode in @('ui','runtime','pet','render','companion','reading','window','product','web-bridge','website-ui','iteration')) {
    $taskFolder = Join-Path $taskReportRoot $taskMode
    $taskProcess = Start-Process -FilePath $taskExe -ArgumentList @(('--' + $taskMode + '-test'), ('"' + $taskFolder + '"')) -PassThru -Wait -WindowStyle Hidden
    Get-Content -LiteralPath (Join-Path $taskFolder ($taskMode + '-test.txt')) -Encoding utf8
    if ($taskProcess.ExitCode -ne 0) { throw ($taskMode + ' verification failed') }
}
Write-Output 'All local checks passed. API regression uses a local mock; no paid requests.'
