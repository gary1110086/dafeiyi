$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -Test
$taskExe = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\轻译\轻译.exe'))
$taskReportRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\轻译\验证报告'))
New-Item -ItemType Directory -Force -Path $taskReportRoot | Out-Null
foreach ($taskMode in @('ui','runtime','pet','render','companion','reading','window','product','web-bridge')) {
    $taskFolder = Join-Path $taskReportRoot $taskMode
    $taskProcess = Start-Process -FilePath $taskExe -ArgumentList @(('--' + $taskMode + '-test'), ('"' + $taskFolder + '"')) -PassThru -Wait -WindowStyle Hidden
    Get-Content -LiteralPath (Join-Path $taskFolder ($taskMode + '-test.txt')) -Encoding utf8
    if ($taskProcess.ExitCode -ne 0) { throw ($taskMode + ' 验证失败') }
}
Write-Output '全部本地验证通过；API 测试使用本机模拟服务，没有发起付费请求。'
