param([switch]$Test,[string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$taskFramework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$taskTarget = if ($OutputDirectory) { [System.IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $PSScriptRoot '..\轻译' }
New-Item -ItemType Directory -Force -Path $taskTarget | Out-Null
$taskReferences = @('System.dll','System.Core.dll','System.Net.Http.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Web.Extensions.dll','System.Security.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll') | ForEach-Object { '/reference:' + (Join-Path $taskFramework $_) }
$taskReferences += @('System.Runtime.dll','System.Runtime.WindowsRuntime.dll') | ForEach-Object { '/reference:' + (Join-Path $taskFramework $_) }
$taskReferences += '/reference:' + (Join-Path $taskFramework 'netstandard.dll')
$taskReferences += @('Windows.Foundation','Windows.Media','Windows.Graphics','Windows.Globalization','Windows.Storage') | ForEach-Object { '/reference:' + (Join-Path $env:WINDIR ('System32\WinMetadata\' + $_ + '.winmd')) }
$taskLibraries=Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Libraries') -Filter '*.dll'
$taskReferences += $taskLibraries | ForEach-Object { '/reference:' + $_.FullName }
$taskSources = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Select-Object -ExpandProperty FullName
$taskExe = Join-Path $taskTarget '轻译.exe'
$taskManifest = Join-Path $PSScriptRoot 'app.manifest'
$taskIcon = Join-Path $PSScriptRoot 'app.ico'
if (-not (Test-Path -LiteralPath $taskIcon)) { & (Join-Path $PSScriptRoot 'make-icon.ps1') }
& (Join-Path $taskFramework 'csc.exe') /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /win32manifest:$taskManifest /win32icon:$taskIcon /out:$taskExe @taskReferences @taskSources
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'app.config') -Destination ($taskExe + '.config') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'update-install.ps1') -Destination (Join-Path $taskTarget 'update-install.ps1') -Force
$taskLibraries | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $taskTarget $_.Name) -Force }
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Libraries\Native') -Filter '*.dll' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $taskTarget $_.Name) -Force }
$taskLicenses=Join-Path $taskTarget 'Libraries'
New-Item -ItemType Directory -Path $taskLicenses -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Libraries') -File | Where-Object Extension -ne '.dll' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $taskLicenses $_.Name) -Force }
$taskAssetSource=Join-Path $PSScriptRoot 'Assets\Whale'
if(Test-Path -LiteralPath $taskAssetSource) {
    $taskAssetTarget=Join-Path $taskTarget 'Assets\Whale'
    New-Item -ItemType Directory -Path $taskAssetTarget -Force | Out-Null
    Get-ChildItem -LiteralPath $taskAssetSource -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $taskAssetTarget $_.Name) -Force }
}
Write-Output "Built: $taskExe"
$taskReadingTarget=Join-Path $taskTarget 'Assets\Reading'
New-Item -ItemType Directory -Path $taskReadingTarget -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Assets\Reading') -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $taskReadingTarget $_.Name) -Force }
if ($Test) {
    $taskReport = Join-Path $taskTarget 'self-test.txt'
    $taskProcess = Start-Process -FilePath $taskExe -ArgumentList @('--self-test', ('"' + $taskReport + '"')) -PassThru -Wait -WindowStyle Hidden
    Get-Content -LiteralPath $taskReport -Encoding utf8
    if ($taskProcess.ExitCode -ne 0) { throw '测试失败' }
}
