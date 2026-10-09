[CmdletBinding()]
param([Parameter(Mandatory)][string]$SetupPath, [Parameter(Mandatory)][string]$EvidencePath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($env:SUNEYEVISION_ACCEPTANCE_VM -ne '1') { throw '仅可在隔离验收 VM 使用；在 VM 设置 SUNEYEVISION_ACCEPTANCE_VM=1。' }
$target = Join-Path ([IO.Path]::GetTempPath()) ('SunEyeVision-acceptance-' + [guid]::NewGuid().ToString('N'))
$uninstaller = Join-Path $target 'unins000.exe'
$evidence = [ordered]@{install=$false;launch=$false;association=$false;uninstall=$false}
function Run-InstallerProcess([string]$Executable, [string[]]$Arguments) {
    $process = Start-Process -FilePath $Executable -ArgumentList $Arguments -PassThru -WindowStyle Hidden
    if (!$process.WaitForExit(180000)) { $process.Kill(); throw "进程超时：$Executable" }
    if ($process.ExitCode -ne 0) { throw "进程退出码 $($process.ExitCode)：$Executable" }
}
try {
    Run-InstallerProcess $SetupPath @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',"/DIR=`"$target`"")
    $exe = Join-Path $target 'SunEyeVision.UI.exe'
    $evidence.install = Test-Path $exe
    if (!$evidence.install) { throw '安装后未找到主程序。' }
    $probePath = Join-Path $target 'probe.json'
    Run-InstallerProcess $exe @('--release-probe',"`"$probePath`"")
    $probe = Get-Content $probePath -Raw | ConvertFrom-Json
    $evidence.launch = ($probe.releaseVersion -ne '')
    $progId = if($probe.channel -eq 'local'){'SunEyeVision.Local.SolutionFile'}else{'SunEyeVision.SolutionFile'}
    $registry = Get-Item "HKCU:/Software/Classes/$progId/shell/open/command"
    $evidence.association = ($registry.GetValue('') -eq "`"$exe`" `"%1`"")
    if (!$evidence.association) { throw '安装器文件关联未指向本次安装目录。' }
} finally {
    if (Test-Path $uninstaller) {
        Run-InstallerProcess $uninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')
        $evidence.uninstall = !(Test-Path (Join-Path $target 'SunEyeVision.UI.exe'))
    }
    $evidence | ConvertTo-Json | Set-Content -LiteralPath $EvidencePath -Encoding utf8
}
if ($evidence.Values -contains $false) { throw '安装验收存在失败项。' }
