param(
    [Parameter(Mandatory=$true)][string]$App,
    [Parameter(Mandatory=$true)][string]$SolutionText,
    [string]$OutputRoot = 'C:\Users\Administrator\.codex\visualizations\2026\11\09\01a1c08e-2257-7a32-8813-4fdf253c33e3\AgentDesktopToolReview',
    [string]$AgentDesktopHome = 'C:\Users\Administrator\.codex\visualizations\2026\11\09\01a1c08e-2257-7a32-8813-4fdf253c33e3\agent-desktop-state'
)
$ErrorActionPreference = 'Stop'
$agent = 'E:\SunEyeVision_V2\.tools\agent-desktop\agent-desktop.cmd'
$env:AGENT_DESKTOP_HOME = $AgentDesktopHome
New-Item -ItemType Directory -Force $OutputRoot | Out-Null
$run = Join-Path $OutputRoot (Get-Date -Format 'yyyyMMdd-HHmmss')
New-Item -ItemType Directory -Force $run | Out-Null
function Invoke-Agent([string[]]$Arguments) {
    $commandArgs = @()
    if ($Arguments[0] -eq 'mouse-click') { $commandArgs += '--headed' }
    $commandArgs += $Arguments
    $text = (& $agent @commandArgs 2>&1 | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($text)) { throw "Agent Desktop 无输出" }
    $file = Join-Path $run ('command-' + [Guid]::NewGuid().ToString('N') + '.json')
    $text | Set-Content -Encoding UTF8 $file
    $json = $text | ConvertFrom-Json
    if ($json.ok -ne $true) { throw "Agent Desktop 命令失败，详见 $file" }
    return $json
}
function Invoke-FreshSnapshot([string]$appName,[string]$label) {
    try { return Invoke-Agent @('snapshot','--app',$appName,'--include-bounds','--timeout-ms','15000') }
    catch {
        # Loading a solution can replace the WPF window/process identity. Refresh the
        # authoritative app list before retrying the same read-only snapshot.
        Invoke-Agent @('list-apps') | ConvertTo-Json -Depth 20 | Set-Content -Encoding UTF8 (Join-Path $run "$label-list-apps.json")
        Start-Sleep -Milliseconds 500
        return Invoke-Agent @('snapshot','--app',$appName,'--include-bounds','--timeout-ms','20000')
    }
}
function Find-Node($node) {
    if ($null -eq $node) { return $null }
    $text = @($node.name,$node.description,$node.value) -join ' '
    $bounds = $node.bounds
    if ($text -like "*$SolutionText*" -and $null -ne $bounds -and $bounds.width -gt 0 -and $bounds.height -gt 0) { return $node }
    foreach ($child in @($node.children)) {
        $found = Find-Node $child
        if ($null -ne $found) { return $found }
    }
    return $null
}
function Find-NodeFromMatches($matches) {
    foreach ($match in @($matches)) {
        $bounds = $match.bounds
        if ($null -eq $bounds -or $bounds.width -le 0 -or $bounds.height -le 0) { continue }
        $text = @($match.name,$match.description,$match.value) -join ' '
        if ($text -like "*$SolutionText*") { return $match }
    }
    return $null
}
$appName = Split-Path -Leaf $App
$launch = Invoke-Agent @('launch',$App,'--activate','--timeout','30000')
$launch | ConvertTo-Json -Depth 20 | Set-Content -Encoding UTF8 (Join-Path $run 'launch.json')
try {
    $wait = Invoke-Agent @('wait','--app',$appName,'--text',$SolutionText,'--timeout','30000')
    $wait | ConvertTo-Json -Depth 20 | Set-Content -Encoding UTF8 (Join-Path $run 'wait-solution.json')
} catch {
    # Agent Desktop may reject the wait when the WPF process instance changes during startup.
    # The following fresh snapshot is the authoritative gate; keep the wait failure in the run log.
    @{ ok = $false; message = $_.Exception.Message; phase = 'wait-solution' } |
        ConvertTo-Json -Depth 10 | Set-Content -Encoding UTF8 (Join-Path $run 'wait-solution.json')
}
$before = Invoke-FreshSnapshot $appName 'welcome-before'
$before | ConvertTo-Json -Depth 40 | Set-Content -Encoding UTF8 (Join-Path $run 'welcome-before.json')
$node = Find-Node $before.data.tree
if ($null -eq $node) {
    $found = Invoke-Agent @('find','--app',$appName,'--text',$SolutionText)
    $found | ConvertTo-Json -Depth 40 | Set-Content -Encoding UTF8 (Join-Path $run 'find-solution.json')
    $node = Find-NodeFromMatches $found.data.matches
}
if ($null -eq $node) { throw "欢迎页中没有找到方案 $SolutionText" }
$x = [int]($node.bounds.x + ($node.bounds.width / 2))
$y = [int]($node.bounds.y + ($node.bounds.height / 2))
Invoke-Agent @('mouse-click','--xy',"$x,$y",'--count','2') | ConvertTo-Json -Depth 20 | Set-Content -Encoding UTF8 (Join-Path $run 'open-solution.json')
try {
    Invoke-Agent @('wait','--app',$appName,'--text','V4.4.0','--timeout','30000') |
        ConvertTo-Json -Depth 20 | Set-Content -Encoding UTF8 (Join-Path $run 'wait-main.json')
} catch {
    @{ ok = $false; message = $_.Exception.Message; phase = 'wait-main' } |
        ConvertTo-Json -Depth 10 | Set-Content -Encoding UTF8 (Join-Path $run 'wait-main.json')
}
$after = Invoke-FreshSnapshot $appName 'main-after'
$after | ConvertTo-Json -Depth 40 | Set-Content -Encoding UTF8 (Join-Path $run 'main-after.json')
@{ app=$App; solution=$SolutionText; click_xy="$x,$y"; snapshot_id=$after.data.snapshot_id; created_utc=(Get-Date).ToUniversalTime().ToString('o') } | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $run 'run.json')
Write-Output $run

