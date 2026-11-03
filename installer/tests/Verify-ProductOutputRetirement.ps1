$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspace 'installer/Product.Components.ps1')
$staging = [IO.Path]::GetFullPath((Join-Path $workspace 'artifacts/staging')) + [IO.Path]::DirectorySeparatorChar
$caseDirectory = [IO.Path]::GetFullPath((Join-Path $staging ('retired-output-' + [guid]::NewGuid().ToString('N'))))
if (-not $caseDirectory.StartsWith($staging, [StringComparison]::OrdinalIgnoreCase)) { throw '验证目录越界' }
$app = Join-Path $caseDirectory 'app'
try {
    New-Item -ItemType Directory -Path $app -Force | Out-Null
    foreach ($name in 'SunEyeVision.Workflow.dll','ControlNamingProbe.dll','OutcomeAdmissionProbe.exe','SunEyeVision.OutcomeOwnership.Probe.dll','SunEyeVision.UI.dll','user-data.json') {
        Set-Content -LiteralPath (Join-Path $app $name) -Value 'fixture'
    }
    Remove-RetiredProductToolOutputs -AppPath $app -EntryAssemblyName 'OutcomeAdmissionProbe'
    foreach ($name in 'SunEyeVision.Workflow.dll','ControlNamingProbe.dll') {
        if (Test-Path -LiteralPath (Join-Path $app $name)) { throw ('退役产物未退出：' + $name) }
    }
    foreach ($name in 'OutcomeAdmissionProbe.exe','SunEyeVision.OutcomeOwnership.Probe.dll','SunEyeVision.UI.dll','user-data.json') {
        if (-not (Test-Path -LiteralPath (Join-Path $app $name))) { throw ('合法入口/组件/用户文件被误删：' + $name) }
    }
    $rejected = $false
    try { Remove-RetiredProductToolOutputs -AppPath $workspace } catch { $rejected = $true }
    if (-not $rejected) { throw '未拒绝源码根清理' }
    Write-Output 'PASS：退役框架及误部署探针退出，显式入口/声明组件/产品/用户文件保留，源码根拒绝。'
}
finally {
    $verified = [IO.Path]::GetFullPath($caseDirectory)
    if (-not $verified.StartsWith($staging,[StringComparison]::OrdinalIgnoreCase)) { throw '验证清理目录越界' }
    if (Test-Path -LiteralPath $verified) { Remove-Item -LiteralPath $verified -Recurse -Force }
}
