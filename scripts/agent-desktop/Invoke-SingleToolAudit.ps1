param(
    [Parameter(Mandatory = $true)][string]$App,
    [Parameter(Mandatory = $true)][string]$SolutionText,
    [Parameter(Mandatory = $true)][string]$NodeText,
    [string]$OutputRoot = 'D:\MyWork\SunEyeVision_V2\Release\_validation\AgentDesktopToolReview\single-tool',
    [string]$AgentDesktopHome = 'C:\Users\Administrator\.codex\visualizations\2026\11\09\01a1c08e-2257-7a32-8813-4fdf253c33e3\agent-desktop-state',
    [switch]$ProbeExecute,
    [switch]$KeepOpen
)
$ErrorActionPreference = 'Stop'
$agent = 'E:\SunEyeVision_V2\.tools\agent-desktop\agent-desktop.cmd'
$env:AGENT_DESKTOP_HOME = $AgentDesktopHome
if (-not (Test-Path -LiteralPath $agent)) { throw "Agent Desktop 未找到: $agent" }
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$run = Join-Path $OutputRoot ((Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss') + '-' + ($NodeText -replace '[^\p{L}\p{Nd}_-]', '_'))
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

function Save-Json($value, [string]$name) {
    $value | ConvertTo-Json -Depth 50 | Set-Content -Encoding UTF8 (Join-Path $run $name)
    return $value
}

function Snapshot([string]$appName, [string]$label) {
    try { return Save-Json (Invoke-Agent @('snapshot','--app',$appName,'--include-bounds','--timeout-ms','20000')) "$label.json" }
    catch {
        Save-Json (Invoke-Agent @('list-apps')) "$label-list-apps.json" | Out-Null
        Start-Sleep -Milliseconds 500
        return Save-Json (Invoke-Agent @('snapshot','--app',$appName,'--include-bounds','--timeout-ms','25000')) "$label.json"
    }
}

function Find-Node($node, [string]$needle) {
    if ($null -eq $node) { return $null }
    $bounds = $node.bounds
    $text = @($node.name, $node.description, $node.value) -join ' '
    if ($text -like "*$needle*" -and $null -ne $bounds -and $bounds.width -gt 0 -and $bounds.height -gt 0) { return $node }
    foreach ($child in @($node.children)) {
        $found = Find-Node $child $needle
        if ($null -ne $found) { return $found }
    }
    return $null
}

function Find-Match($matches, [string]$needle) {
    foreach ($match in @($matches)) {
        $bounds = $match.bounds
        $text = @($match.name, $match.description, $match.value) -join ' '
        if ($null -ne $bounds -and $bounds.width -gt 0 -and $bounds.height -gt 0 -and $text -like "*$needle*") { return $match }
    }
    return $null
}

$appName = Split-Path -Leaf $App
$closed = $false
try {
    Save-Json (Invoke-Agent @('launch',$App,'--activate','--timeout','30000')) 'launch.json' | Out-Null
    try { Save-Json (Invoke-Agent @('wait','--app',$appName,'--text',$SolutionText,'--timeout','30000')) 'wait-solution.json' | Out-Null }
    catch { @{ ok = $false; phase = 'wait-solution'; message = $_.Exception.Message } | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $run 'wait-solution.json') }
    $welcome = Snapshot $appName 'welcome-before'
    $solution = Find-Node $welcome.data.tree $SolutionText
    if ($null -eq $solution) {
        $found = Save-Json (Invoke-Agent @('find','--app',$appName,'--text',$SolutionText)) 'find-solution.json'
        $solution = Find-Match $found.data.matches $SolutionText
    }
    if ($null -eq $solution) { throw "欢迎页没有找到方案: $SolutionText" }
    $x = [int]($solution.bounds.x + $solution.bounds.width / 2)
    $y = [int]($solution.bounds.y + $solution.bounds.height / 2)
    Save-Json (Invoke-Agent @('mouse-click','--headed','--xy',"$x,$y",'--count','2')) 'open-solution.json' | Out-Null
    try { Save-Json (Invoke-Agent @('wait','--app',$appName,'--text','V4.4.0','--timeout','30000')) 'wait-main.json' | Out-Null }
    catch { @{ ok = $false; phase = 'wait-main'; message = $_.Exception.Message } | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $run 'wait-main.json') }
    $main = Snapshot $appName 'main-before-tool'
    $node = Find-Node $main.data.tree $NodeText
    if ($null -eq $node) {
        $foundNode = Save-Json (Invoke-Agent @('find','--app',$appName,'--text',$NodeText)) 'find-tool.json'
        $node = Find-Match $foundNode.data.matches $NodeText
    }
    if ($null -eq $node) { throw "主界面没有找到节点: $NodeText" }
    $nx = [int]($node.bounds.x + $node.bounds.width / 2)
    $ny = [int]($node.bounds.y + $node.bounds.height / 2)
    Save-Json (Invoke-Agent @('mouse-click','--headed','--xy',"$nx,$ny",'--count','2')) 'open-tool.json' | Out-Null
    Start-Sleep -Milliseconds 1000
    $dialog = Snapshot $appName 'tool-window'
    @{ app = $App; solution = $SolutionText; node = $NodeText; solution_xy = "$x,$y"; node_xy = "$nx,$ny"; snapshot_id = $dialog.data.snapshot_id; created_utc = (Get-Date).ToUniversalTime().ToString('o') } |
        ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $run 'run.json')
    if ($ProbeExecute) {
        $execute = Save-Json (Invoke-Agent @('find','--app',$appName,'--text','执行')) 'find-execute.json'
        $executeMatch = @($execute.data.matches |
            Where-Object {
                $_.interactive -eq $true -and
                -not [string]::IsNullOrWhiteSpace([string]$_.ref_id) -and
                ((@($_.name, $_.description, $_.value) -join ' ') -like '*执行*')
            } | Select-Object -First 1)
        if ($executeMatch.Count -eq 1) { $executeMatch = $executeMatch[0] } else { $executeMatch = $null }
        if ($null -eq $executeMatch) { throw "工具窗口没有找到执行命令: $NodeText" }
        Save-Json (Invoke-Agent @('click',$executeMatch.ref_id)) 'execute.json' | Out-Null
        Start-Sleep -Milliseconds 700
        $executeResult = Snapshot $appName 'execute-result'
        $executeResult.data | ConvertTo-Json -Depth 50 | Set-Content -Encoding UTF8 (Join-Path $run 'execute-result-tree.json')
        $confirmMatches = (Invoke-Agent @('find','--app',$appName,'--text','确定')).data.matches
        $confirm = @($confirmMatches |
            Where-Object {
                $_.interactive -eq $true -and
                -not [string]::IsNullOrWhiteSpace([string]$_.ref_id) -and
                ((@($_.name, $_.description, $_.value) -join ' ') -like '*确定*')
            } | Select-Object -First 1)
        if ($confirm.Count -eq 1) { $confirm = $confirm[0] } else { $confirm = $null }
        if ($null -ne $confirm) {
            Save-Json (Invoke-Agent @('click',$confirm.ref_id)) 'execute-confirm.json' | Out-Null
        }
    }
}
finally {
    if (-not $KeepOpen) {
        try {
            Save-Json (Invoke-Agent @('close-app',$appName)) 'close-app.json' | Out-Null
            $closed = $true
        } catch {
            @{ ok = $false; message = $_.Exception.Message } | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $run 'close-app.json')
        }
    } else {
        @{ ok = $true; kept_open = $true; note = '调用方负责后续交互和关闭' } |
            ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $run 'close-app.json')
    }
}
Write-Output $run
