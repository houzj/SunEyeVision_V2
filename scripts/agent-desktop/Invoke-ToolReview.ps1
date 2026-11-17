param(
    [string]$OriginalApp = 'C:\Program Files\VisionMaster4.4.0\Applications\VisionMaster.exe',
    [string]$ProductApp = 'D:\MyWork\SunEyeVision_V2\Release\SunEyeVision_v1.0.0\SunEyeVision\SunEyeVision.UI.exe',
    [string]$OutputRoot = 'D:\MyWork\SunEyeVision_V2\Release\_validation\AgentDesktopToolReview',
    [string[]]$ProductArg = @(),
    [string]$AgentDesktopHome = 'C:\Users\Administrator\.codex\visualizations\2026\11\09\01a1c08e-2257-7a32-8813-4fdf253c33e3\agent-desktop-state'
)
$ErrorActionPreference = 'Stop'
$agent = 'E:\SunEyeVision_V2\.tools\agent-desktop\agent-desktop.cmd'
$env:AGENT_DESKTOP_HOME = $AgentDesktopHome
if (-not (Test-Path -LiteralPath $agent)) { throw "Agent Desktop 未找到: $agent" }
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$run = Join-Path $OutputRoot (Get-Date -Format 'yyyyMMdd-HHmmss')
New-Item -ItemType Directory -Force -Path $run | Out-Null
function Invoke-Agent([string[]]$Arguments) {
    $text = (& $agent @Arguments 2>&1 | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($text)) { throw "Agent Desktop 无输出: $($Arguments -join ' ')" }
    $file = Join-Path $run ('command-' + [Guid]::NewGuid().ToString('N') + '.json')
    $text | Set-Content -Encoding UTF8 $file
    try { $json = $text | ConvertFrom-Json } catch { throw "Agent Desktop 返回非 JSON，详见 $file" }
    if ($json.ok -ne $true) { throw "Agent Desktop 命令失败，详见 $file" }
    return $json
}
function LaunchAndSnapshot([string]$Label,[string]$App,[string[]]$Args = @()) {
    $appName = Split-Path -Leaf $App
    $launch = @('launch', $App, '--activate', '--timeout', '30000')
    foreach ($arg in $Args) { $launch += '--arg'; $launch += $arg }
    try {
        Invoke-Agent $launch | ConvertTo-Json -Depth 20 | Set-Content -Encoding UTF8 (Join-Path $run "$Label-launch.json")
    } catch {
        $apps = Invoke-Agent @('list-apps')
        $running = @($apps.data.apps | Where-Object { $_.name -eq $appName })
        if ($running.Count -eq 0) { throw }
        @{ reused = $true; app = $appName; pid = $running[0].pid; reason = $_.Exception.Message } | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $run "$Label-launch.json")
    }
    Invoke-Agent @('snapshot','--app',$appName,'--include-bounds','--timeout-ms','10000') | ConvertTo-Json -Depth 40 | Set-Content -Encoding UTF8 (Join-Path $run "$Label-welcome.json")
}
LaunchAndSnapshot 'vm' $OriginalApp
LaunchAndSnapshot 'product' $ProductApp $ProductArg
@{
    created_utc = (Get-Date).ToUniversalTime().ToString('o')
    original_app = $OriginalApp
    product_app = $ProductApp
    product_args = $ProductArg
    agent_desktop_home = $AgentDesktopHome
    next_manual_step = '从最新快照重新解析唯一控件引用，再按场景 JSON 操作，不复用旧 ref'
} | ConvertTo-Json -Depth 10 | Set-Content -Encoding UTF8 (Join-Path $run 'run.json')
Write-Output $run
