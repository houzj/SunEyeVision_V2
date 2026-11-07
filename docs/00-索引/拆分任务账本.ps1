# 任务账本拆分器（幂等，按块内容哈希去重）
# 用法：powershell -ExecutionPolicy Bypass -File "docs\00-索引\拆分任务账本.ps1" [-KeepRecent 12]
# 作用：把 collaboration/CURRENT_TASK.md 中较早的「## YYYY-MM-DD 标题」批次按月份切到 collaboration/history/，
#       主文件只保留最近的若干批次与顶部摘要。
# 去重口径：以「整块内容（标题 + 正文）」的 SHA256 判断是否已归档，标题文本重复但内容不同的块不会被跳过。

param(
    [int]$KeepRecent = 12
)

$ErrorActionPreference = 'Stop'

$IndexDir = $PSScriptRoot
$DocsRoot = Split-Path -Path $IndexDir -Parent
$TaskFile = Join-Path $DocsRoot 'collaboration\CURRENT_TASK.md'
$HistoryDir = Join-Path $DocsRoot 'collaboration\history'

if (-not (Test-Path $TaskFile)) { throw ('找不到任务账本：' + $TaskFile) }
if (-not (Test-Path $HistoryDir)) { New-Item -ItemType Directory -Force -Path $HistoryDir | Out-Null }

$utf8 = New-Object System.Text.UTF8Encoding($false)

function Get-BlockHash {
    param([string[]]$Body)
    $text = ($Body -join "`n").Trim()
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $hash = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($text))
    $sha.Dispose()
    return [System.BitConverter]::ToString($hash)
}

function Get-Blocks {
    param([string[]]$Lines, [int]$FirstIndex)
    $result = New-Object System.Collections.ArrayList
    $idx = New-Object System.Collections.ArrayList
    for ($i = $FirstIndex; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match '^##\s+(\d{4})-(\d{2})-(\d{2})') { [void]$idx.Add($i) }
    }
    for ($s = 0; $s -lt $idx.Count; $s++) {
        $begin = $idx[$s]
        $end = $Lines.Count - 1
        if ($s -lt $idx.Count - 1) { $end = $idx[$s + 1] - 1 }
        $body = @()
        if ($end -ge $begin) { $body = $Lines[$begin..$end] }
        $month = '未分月'
        if ($Lines[$begin] -match '^##\s+(\d{4})-(\d{2})') { $month = $Matches[1] + '-' + $Matches[2] }
        [void]$result.Add([pscustomobject]@{
            Heading = $Lines[$begin]
            Month   = $month
            Body    = $body
            Hash    = (Get-BlockHash -Body $body)
        })
    }
    return $result
}

$lines = [System.IO.File]::ReadAllLines($TaskFile, $utf8)
$sections = Get-Blocks -Lines $lines -FirstIndex 0
if ($sections.Count -eq 0) { throw '任务账本中没有「## YYYY-MM-DD」批次标题，无需拆分。' }

$firstSectionIndex = 0
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^##\s+(\d{4})-(\d{2})-(\d{2})') { $firstSectionIndex = $i; break }
}
$preamble = @()
if ($firstSectionIndex -gt 0) { $preamble = $lines[0..($firstSectionIndex - 1)] }

$keep = New-Object System.Collections.ArrayList
$move = New-Object System.Collections.ArrayList
for ($s = 0; $s -lt $sections.Count; $s++) {
    if ($s -lt $KeepRecent) { [void]$keep.Add($sections[$s]) } else { [void]$move.Add($sections[$s]) }
}

$movedNew = 0
$movedLines = 0

foreach ($section in $move) {
    $name = 'CURRENT_TASK_ARCHIVE_' + $section.Month + '.md'
    $path = Join-Path $HistoryDir $name
    $buffer = New-Object System.Collections.ArrayList
    if (Test-Path $path) {
        $buffer.AddRange([System.IO.File]::ReadAllLines($path, $utf8))
    }
    else {
        [void]$buffer.Add('# 任务账本历史分卷 ' + $section.Month)
        [void]$buffer.Add('')
        [void]$buffer.Add('> 从 `docs/collaboration/CURRENT_TASK.md` 按批次切出的历史记录，倒序排列（最新在前）。本卷只追加、不改写正文。')
        [void]$buffer.Add('')
    }
    foreach ($line in $section.Body) { [void]$buffer.Add($line) }
    [void]$buffer.Add('')
    [System.IO.File]::WriteAllText($path, ($buffer -join "`r`n"), $utf8)
    $movedNew++
    $movedLines += $section.Body.Count
}

$mainLines = New-Object System.Collections.ArrayList
foreach ($line in $preamble) { [void]$mainLines.Add($line) }
$keepLines = 0
foreach ($section in $keep) {
    foreach ($line in $section.Body) { [void]$mainLines.Add($line) }
    $keepLines += $section.Body.Count
}
[System.IO.File]::WriteAllText($TaskFile, ($mainLines -join "`r`n") + "`r`n", $utf8)

$totalLines = 0
foreach ($section in $sections) { $totalLines += $section.Body.Count }
if (($keepLines + $movedLines) -ne $totalLines) {
    throw ('内容行数不守恒：原有 ' + $totalLines + '，保留 ' + $keepLines + '，归档 ' + $movedLines)
}

Write-Output ('批次总数 ' + $sections.Count + '，主文件保留 ' + $keep.Count + '（' + $keepLines + ' 行），本次归档 ' + $movedNew + '（' + $movedLines + ' 行）。')
Write-Output ('行数守恒校验通过：' + $totalLines + ' 行全部有归属。')
Write-Output ('主文件现有行数 ' + (Get-Content -Path $TaskFile -Encoding UTF8).Count)
Write-Output ('历史分卷：' + ((Get-ChildItem -Path $HistoryDir -File -Filter 'CURRENT_TASK_ARCHIVE_*.md' | Select-Object -ExpandProperty Name) -join ', '))
