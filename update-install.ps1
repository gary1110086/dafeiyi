param(
    [Parameter(Mandatory=$true)][string]$PackageDirectory,
    [Parameter(Mandatory=$true)][string]$InstallDirectory,
    [int]$WaitProcessId=0,
    [switch]$NoLaunch
)
$ErrorActionPreference='Stop'
$taskPackage=[IO.Path]::GetFullPath($PackageDirectory).TrimEnd('\')
$taskTarget=[IO.Path]::GetFullPath($InstallDirectory).TrimEnd('\')
$taskWork=[IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($PSCommandPath))
$taskBackup=Join-Path $taskWork ('backup-'+[Guid]::NewGuid().ToString('N'))
$taskProgram=Join-Path $taskTarget '轻译.exe'
$taskChanged=New-Object 'System.Collections.Generic.List[object]'
$taskResult=Join-Path $taskWork 'update-result.json'
function Get-UpdateHash([string]$path) {
    $taskHasher=[Security.Cryptography.SHA256]::Create()
    $taskStream=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try { return [BitConverter]::ToString($taskHasher.ComputeHash($taskStream)).Replace('-','') } finally { $taskStream.Dispose(); $taskHasher.Dispose() }
}
function Allowed-UpdateFile([string]$relative) {
    if (-not $relative -or $relative.Contains('\') -or $relative.Contains(':') -or $relative.StartsWith('/') -or ($relative.Split('/') | Where-Object { $_ -eq '..' -or $_ -eq '.' -or $_ -eq '' })) { return $false }
    if ([IO.Path]::GetFileName($relative) -in @('settings.json','history.dat','terms.dat')) { return $false }
    return ($relative -in @('轻译.exe','轻译.exe.config','setup.ps1','update-install.ps1','README.md','README.en.md','LICENSE','THIRD_PARTY_NOTICES.md','SECURITY.md','安装到桌面.cmd','安装并开机启动.cmd','distribution-manifest.json')) -or (-not $relative.Contains('/') -and $relative.EndsWith('.dll')) -or $relative.StartsWith('Assets/') -or $relative.StartsWith('Libraries/')
}
function Safe-UpdatePath([string]$root,[string]$relative) {
    $taskResolved=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if (-not $taskResolved.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Update path escapes its root.' }
    $taskProbe=$taskResolved
    while ($taskProbe.Length -ge $root.Length) {
        if ((Test-Path -LiteralPath $taskProbe) -and ((Get-Item -LiteralPath $taskProbe -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Update paths cannot pass through a link.' }
        if ($taskProbe -eq $root) { break }
        $taskProbe=[IO.Path]::GetDirectoryName($taskProbe)
    }
    return $taskResolved
}
try {
    if ($taskPackage -eq $taskTarget -or -not (Test-Path -LiteralPath $taskProgram -PathType Leaf)) { throw 'Existing program directory is invalid.' }
    $taskProfile=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'LightTranslate')).TrimEnd('\')
    if ($taskTarget -eq $taskProfile -or $taskTarget.StartsWith($taskProfile+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'User data cannot be used as an installation target.' }
    $taskManifest=Get-Content -LiteralPath (Join-Path $taskPackage 'distribution-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $taskListed=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($taskEntry in $taskManifest.files) {
        if (-not (Allowed-UpdateFile $taskEntry.path) -or $taskEntry.path -eq 'distribution-manifest.json' -or -not $taskListed.Add($taskEntry.path)) { throw 'Update manifest contains an invalid file.' }
        $taskSource=Safe-UpdatePath $taskPackage $taskEntry.path
        if (-not (Test-Path -LiteralPath $taskSource -PathType Leaf) -or (Get-UpdateHash $taskSource) -ne $taskEntry.sha256) { throw 'Update file checksum mismatch.' }
        $null=Safe-UpdatePath $taskTarget $taskEntry.path
    }
    if (-not $taskListed.Contains('轻译.exe') -or -not $taskListed.Contains('轻译.exe.config') -or -not $taskListed.Contains('update-install.ps1')) { throw 'Required program files are absent.' }
    if ($WaitProcessId -gt 0) {
        $taskRunning=Get-Process -Id $WaitProcessId -ErrorAction SilentlyContinue
        if ($taskRunning) {
            if ($taskRunning.Path -ne $taskProgram) { throw 'Update process does not match the installed program.' }
            if (-not $taskRunning.WaitForExit(120000)) { throw 'The application did not close. No program files were changed.' }
        }
    }
    New-Item -ItemType Directory -Path $taskBackup -Force | Out-Null
    $taskNames=@($taskManifest.files | ForEach-Object {$_.path})+@('distribution-manifest.json')
    foreach ($taskRelative in $taskNames) {
        $taskSource=Safe-UpdatePath $taskPackage $taskRelative
        $taskDestination=Safe-UpdatePath $taskTarget $taskRelative
        $taskOld=Safe-UpdatePath $taskBackup $taskRelative
        $taskExisted=Test-Path -LiteralPath $taskDestination -PathType Leaf
        $taskOldHash=$null
        if ($taskExisted) { New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($taskOld)) -Force | Out-Null; Copy-Item -LiteralPath $taskDestination -Destination $taskOld -Force; $taskOldHash=Get-UpdateHash $taskOld }
        $taskChanged.Add([pscustomobject]@{path=$taskRelative;existed=$taskExisted;hash=$taskOldHash})
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($taskDestination)) -Force | Out-Null
        Copy-Item -LiteralPath $taskSource -Destination $taskDestination -Force
    }
    @{status='updated';version=$taskManifest.version;message='Program updated. User data was not modified.'} | ConvertTo-Json | Set-Content -LiteralPath $taskResult -Encoding UTF8
} catch {
    $taskFailure=$_.Exception.Message; $taskRollbackFailed=$false
    for ($taskIndex=$taskChanged.Count-1; $taskIndex -ge 0; $taskIndex--) {
        $taskItem=$taskChanged[$taskIndex]
        try {
            $taskDestination=Safe-UpdatePath $taskTarget $taskItem.path
            if ($taskItem.existed) {
                if (-not (Test-Path -LiteralPath $taskDestination) -or (Get-UpdateHash $taskDestination) -ne $taskItem.hash) { Copy-Item -LiteralPath (Safe-UpdatePath $taskBackup $taskItem.path) -Destination $taskDestination -Force }
            } elseif (Test-Path -LiteralPath $taskDestination -PathType Leaf) { Remove-Item -LiteralPath $taskDestination -Force }
        } catch { $taskRollbackFailed=$true }
    }
    @{status= $(if($taskRollbackFailed){'recovery-needed'}else{'restored'});message=$taskFailure;backup=$taskBackup} | ConvertTo-Json | Set-Content -LiteralPath $taskResult -Encoding UTF8
    if (-not $NoLaunch) {
        try { Add-Type -AssemblyName PresentationFramework; $taskMessage=if($taskRollbackFailed){'更新未完成，需要手动恢复程序。你的用户数据未被修改。程序备份：'+$taskBackup}else{'更新未完成，旧版已恢复。你的配置、登录、历史和收藏保留，可稍后重试。'}; [Windows.MessageBox]::Show($taskMessage,'大肥译更新') | Out-Null } catch { }
        if (-not $taskRollbackFailed -and (Test-Path -LiteralPath $taskProgram) -and (-not $taskRunning -or $taskRunning.HasExited)) { Start-Process -FilePath $taskProgram -WorkingDirectory $taskTarget -WindowStyle Hidden | Out-Null }
    }
    exit 1
}
if (-not $NoLaunch) { Start-Process -FilePath $taskProgram -WorkingDirectory $taskTarget -WindowStyle Hidden | Out-Null }
