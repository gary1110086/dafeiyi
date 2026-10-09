param(
    [switch]$StartWithWindows,
    [switch]$NoLaunch,
    [switch]$NoShortcuts,
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'Programs\LightTranslate')
)
$ErrorActionPreference = 'Stop'
$packageRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$targetRoot = [IO.Path]::GetFullPath($InstallDirectory)
$programSource = Join-Path $packageRoot '轻译.exe'
$programTarget = Join-Path $targetRoot '轻译.exe'
if (-not (Test-Path -LiteralPath $programSource)) { throw '请从 Windows 发行包运行安装脚本，先完整解压所有文件。' }
if ($packageRoot.TrimEnd('\') -eq $targetRoot.TrimEnd('\')) { throw '安装目标不能与解压目录相同。' }

$manifestPath = Join-Path $packageRoot 'distribution-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) { throw '发行文件清单缺失，请重新下载并完整解压。' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($entry in $manifest.files) {
    $entryPath = [IO.Path]::GetFullPath((Join-Path $packageRoot $entry.path))
    if (-not $entryPath.StartsWith($packageRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw '发行文件路径无效。' }
    if (-not (Test-Path -LiteralPath $entryPath) -or (Get-FileHash -LiteralPath $entryPath -Algorithm SHA256).Hash -ne $entry.sha256) { throw ('发行文件损坏：' + $entry.path) }
}
foreach ($process in (Get-Process -Name '轻译' -ErrorAction SilentlyContinue)) {
    if ($process.Path -eq $programTarget) { Stop-Process -Id $process.Id; if (-not $process.WaitForExit(5000)) { throw '旧版程序未退出，请关闭后重试。' } }
}
New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null
foreach ($name in @('轻译.exe','轻译.exe.config','update-install.ps1','README.md','README.en.md','LICENSE','THIRD_PARTY_NOTICES.md','SECURITY.md','distribution-manifest.json')) {
    Copy-Item -LiteralPath (Join-Path $packageRoot $name) -Destination (Join-Path $targetRoot $name) -Force
}
Get-ChildItem -LiteralPath $packageRoot -Filter '*.dll' -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $targetRoot $_.Name) -Force }
foreach ($folder in @('Assets','Libraries')) { Copy-Item -LiteralPath (Join-Path $packageRoot $folder) -Destination $targetRoot -Recurse -Force }

if (-not $NoShortcuts) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcutFolders = @([Environment]::GetFolderPath('Desktop'))
    if ($StartWithWindows) { $shortcutFolders += [Environment]::GetFolderPath('Startup') }
    foreach ($folder in $shortcutFolders) {
        $link = $shell.CreateShortcut((Join-Path $folder '大肥译.lnk'))
        $link.TargetPath = $programTarget; $link.WorkingDirectory = $targetRoot
        $link.IconLocation = (Join-Path $targetRoot 'Assets\Reading\whale-app.ico') + ',0'
        $link.Description = '大肥译 · 划词翻译与肥鱼陪伴'; $link.Save()
    }
}
Write-Output '安装完成。你的 API Key、登录、历史和收藏只会保存在自己的 Windows 用户账户中。'
if (-not $NoLaunch) { Start-Process -FilePath $programTarget -WorkingDirectory $targetRoot -WindowStyle Hidden | Out-Null }
