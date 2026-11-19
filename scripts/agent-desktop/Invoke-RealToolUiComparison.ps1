param(
    [Parameter(Mandatory = $true)][string]$VmApp,
    [Parameter(Mandatory = $true)][string]$ProductApp,
    [string]$SolutionText,
    [string]$VmSolutionText = 'test',
    [string]$ProductSolutionText = '11111',
    [string]$ProductSolutionPath = 'D:\MyWork\SunEyeVision_V2\.testdata\agent-desktop\blob-connected\blob-connected.solution',
    [Parameter(Mandatory = $true)][string[]]$NodeText,
    [string]$OutputRoot = 'D:\MyWork\SunEyeVision_V2\Release\_validation\AgentDesktopToolReview\real-ui-comparison',
    [string]$Agent = 'E:\SunEyeVision_V2\.tools\agent-desktop\agent-desktop.cmd'
)

$ErrorActionPreference = 'Stop'
$env:AGENT_DESKTOP_HOME = 'C:\Users\Administrator\.codex\visualizations\2026\11\09\01a1c08e-2257-7a32-8813-4fdf253c33e3\agent-desktop-state'
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

function Invoke-Agent([string[]]$Arguments) {
    $raw = (& $Agent @Arguments 2>&1 | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($raw)) { throw "Agent Desktop 无输出: $($Arguments -join ' ')" }
    $json = $raw | ConvertFrom-Json
    if ($json.ok -ne $true) { throw "Agent Desktop 命令失败: $raw" }
    return $json
}

function Save-Agent($Value, [string]$Path) {
    $Value | ConvertTo-Json -Depth 60 | Set-Content -Encoding UTF8 $Path
}

function Snapshot([string]$App, [string]$WindowId, [string]$Path) {
    $args = @('snapshot', '--app', $App, '--include-bounds', '--timeout-ms', '30000')
    if ($WindowId) { $args += @('--window-id', $WindowId) }
    try { Save-Agent (Invoke-Agent $args) $Path }
    catch {
        if ($_.Exception.Message -notmatch 'WINDOW_NOT_FOUND|identity no longer matches') { throw }
        Save-Agent (Invoke-Agent @('list-windows', '--app', $App)) ($Path + '.refreshed-windows.json') | Out-Null
        Save-Agent (Invoke-Agent @('snapshot', '--app', $App, '--include-bounds', '--timeout-ms', '30000')) $Path
    }
}

function Close-ToolWindows([string]$AppName) {
    $windows = (Invoke-Agent @('list-windows', '--app', $AppName)).data
    foreach ($window in @($windows | Where-Object { $_.bounds.width -eq 420 -and $_.bounds.height -eq 500 })) {
        try {
            $close = Invoke-Agent @('find', '--app', $AppName, '--window-id', $window.id, '--native-id', 'CloseWinButton', '--limit', '5')
            $ref = @($close.data.matches | Where-Object { $_.interactive -eq $true } | Select-Object -First 1)
            if ($ref.Count -eq 1) { Invoke-Agent @('click', $ref[0].ref_id) | Out-Null }
        } catch { }
    }
    Start-Sleep -Milliseconds 300
}

function Find-Node($Tree, [string]$Text) {
    $matches = @((Invoke-Agent @('find', '--app', $Tree, '--text', $Text, '--limit', '50')).data.matches)
    return $matches | Where-Object { $_.bounds.width -gt 0 -and $_.bounds.height -gt 0 -and $_.name -like "*$Text*" } | Select-Object -First 1
}

function Compare-Node([string]$Text) {
    $slug = $Text -replace '[^\p{L}\p{Nd}_-]', '_'
    $run = Join-Path $OutputRoot $slug
    New-Item -ItemType Directory -Force -Path $run | Out-Null
    $record = [ordered]@{ tool = $Text; created_utc = (Get-Date).ToUniversalTime().ToString('o'); original = [ordered]@{}; product = [ordered]@{}; status = 'pending' }
    try {
        Close-ToolWindows (Split-Path -Leaf $VmApp)
        $vm = Invoke-Agent @('launch', $VmApp, '--activate', '--timeout', '30000')
        Save-Agent $vm (Join-Path $run 'vm-launch.json')
        Start-Sleep -Milliseconds 800
        Invoke-Agent @('focus-window', '--app', (Split-Path -Leaf $VmApp)) | Out-Null
        $windows = Invoke-Agent @('list-windows', '--app', (Split-Path -Leaf $VmApp))
        $mainWindow = @($windows.data | Where-Object { $_.bounds.width -ne 420 -or $_.bounds.height -ne 500 } | Select-Object -First 1)
        if ($mainWindow.Count -ne 1) { throw '原厂主窗口未找到。' }
        $welcomeSnapshot = Join-Path $run 'vm-welcome.json'
        Snapshot (Split-Path -Leaf $VmApp) $mainWindow[0].id $welcomeSnapshot
        $solution = Invoke-Agent @('find', '--app', (Split-Path -Leaf $VmApp), '--text', $VmSolutionText, '--limit', '20')
        Save-Agent $solution (Join-Path $run 'vm-find-solution.json')
        $solutionMatch = @($solution.data.matches | Where-Object { $_.bounds.width -gt 0 -and $_.bounds.height -gt 0 -and $_.name -like "*$VmSolutionText*" } | Select-Object -First 1)
        if ($solutionMatch.Count -ne 1) { throw "原厂欢迎页没有唯一方案: $VmSolutionText" }
        $sx = [int]($solutionMatch[0].bounds.x + $solutionMatch[0].bounds.width / 2)
        $sy = [int]($solutionMatch[0].bounds.y + $solutionMatch[0].bounds.height / 2)
        Save-Agent (Invoke-Agent @('mouse-click', '--headed', '--xy', "$sx,$sy", '--count', '2')) (Join-Path $run 'vm-open-solution.json')
        Start-Sleep -Seconds 4
        Invoke-Agent @('focus-window', '--app', (Split-Path -Leaf $VmApp)) | Out-Null
        $main = Invoke-Agent @('snapshot', '--app', (Split-Path -Leaf $VmApp), '--include-bounds', '--timeout-ms', '30000')
        Save-Agent $main (Join-Path $run 'vm-main.json')
        $nodeMatches = @((Invoke-Agent @('find', '--app', (Split-Path -Leaf $VmApp), '--text', $Text, '--limit', '50')).data.matches)
        $node = $nodeMatches | Where-Object { $_.bounds.width -gt 0 -and $_.bounds.height -gt 0 -and $_.name -eq $Text -and $_.role -eq 'statictext' } | Select-Object -First 1
        if ($null -eq $node) {
            $node = $nodeMatches | Where-Object { $_.bounds.width -gt 0 -and $_.bounds.height -gt 0 -and $_.name -eq $Text } | Select-Object -First 1
        }
        if ($null -eq $node) { throw "原厂主界面没有找到节点: $Text" }
        $nx = [int]($node.bounds.x + $node.bounds.width / 2)
        $ny = [int]($node.bounds.y + $node.bounds.height / 2)
        Save-Agent (Invoke-Agent @('mouse-click', '--headed', '--xy', "$nx,$ny", '--count', '2')) (Join-Path $run 'vm-open-tool.json')
        Start-Sleep -Seconds 2
        $vmWindows = (Invoke-Agent @('list-windows', '--app', (Split-Path -Leaf $VmApp))).data
        $dialog = @($vmWindows | Where-Object { $_.bounds.width -eq 420 -and $_.bounds.height -eq 500 } | Select-Object -First 1)
        if ($dialog.Count -ne 1) {
            $option = $nodeMatches | Where-Object { $_.bounds.width -gt 0 -and $_.bounds.height -gt 0 -and $_.role -eq 'option' -and $_.name -eq $Text } | Select-Object -First 1
            if ($null -ne $option) {
                $ox = [int]($option.bounds.x + $option.bounds.width / 2)
                $oy = [int]($option.bounds.y + $option.bounds.height / 2)
                Save-Agent (Invoke-Agent @('mouse-click', '--headed', '--xy', "$ox,$oy", '--count', '2')) (Join-Path $run 'vm-open-tool-retry.json')
                Start-Sleep -Seconds 2
                $vmWindows = (Invoke-Agent @('list-windows', '--app', (Split-Path -Leaf $VmApp))).data
                $dialog = @($vmWindows | Where-Object { $_.bounds.width -eq 420 -and $_.bounds.height -eq 500 } | Select-Object -First 1)
            }
        }
        if ($dialog.Count -ne 1) { throw "原厂节点未打开 420x500 工具窗口: $Text" }
        $dialogPath = Join-Path $run 'vm-tool-window.json'
        Snapshot (Split-Path -Leaf $VmApp) $dialog[0].id $dialogPath
        $record.original = [ordered]@{ window_id = $dialog[0].id; snapshot = $dialogPath; node_xy = "$nx,$ny"; status = 'captured' }
        try {
            $close = Invoke-Agent @('find', '--app', (Split-Path -Leaf $VmApp), '--window-id', $dialog[0].id, '--native-id', 'CloseWinButton', '--limit', '5')
            $closeRef = @($close.data.matches | Where-Object { $_.interactive -eq $true } | Select-Object -First 1)
            if ($closeRef.Count -eq 1) { Save-Agent (Invoke-Agent @('click', $closeRef[0].ref_id)) (Join-Path $run 'vm-close-tool.json') }
        } catch { $_ | Out-String | Set-Content -Encoding UTF8 (Join-Path $run 'vm-close-tool-error.txt') }
    } catch {
        $record.original = [ordered]@{ status = 'blocked'; error = $_.Exception.Message }
    }
    try {
        $productScript = Join-Path $PSScriptRoot 'Invoke-SingleToolAudit.ps1'
        $productSolution = if([string]::IsNullOrWhiteSpace($SolutionText)){$ProductSolutionText}else{$SolutionText}
        $productArgs=@('-NoProfile','-ExecutionPolicy','Bypass','-File',$productScript,'-App',$ProductApp,'-SolutionText',$productSolution,'-NodeText',$Text,'-OutputRoot',(Join-Path $OutputRoot 'product'))
        if(Test-Path -LiteralPath $ProductSolutionPath){$productArgs += @('-SolutionPath',$ProductSolutionPath)}
        $productRun = & powershell @productArgs
        if ($LASTEXITCODE -ne 0) { throw "自研工具对照脚本退出码: $LASTEXITCODE" }
        $record.product = [ordered]@{ status = 'captured'; run = ($productRun | Select-Object -Last 1) }
    } catch {
        $record.product = [ordered]@{ status = 'blocked'; error = $_.Exception.Message }
    }
    $record.status = if ($record.original.status -eq 'captured' -and $record.product.status -eq 'captured') { 'captured-both-sides' } else { 'blocked' }
    $record | ConvertTo-Json -Depth 20 | Set-Content -Encoding UTF8 (Join-Path $run 'comparison.json')
    return $record
}

$results = foreach ($node in $NodeText) { Compare-Node $node }
$results | ConvertTo-Json -Depth 20 | Set-Content -Encoding UTF8 (Join-Path $OutputRoot 'summary.json')
$results | ConvertTo-Json -Depth 20
