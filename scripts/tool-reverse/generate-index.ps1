#requires -Version 5.1
<#
.SYNOPSIS
    生成工具逆向证据库的机器可读索引 index.json。

.DESCRIPTION
    扫描 docs/07-工具逆向/tools/ 下每张卡片的 evidence.json 与 ui-preview.json，
    汇总统计并输出 index.json。手工改动 index.json 会被本脚本覆盖。

    聚合口径：
      - statusCounts：按 evidence.json.status 计数
      - originCounts：有 origin 字段按值计，缺省计为 codex
      - uiKindCounts：按 ui-preview.json 的 previewKind 判定四种形态
      - priorityCounts：按 evidence.json.priority 计数

.PARAMETER Root
    证据库根目录，默认按脚本位置推导。

.EXAMPLE
    pwsh -File scripts/tool-reverse/generate-index.ps1
#>
[CmdletBinding()]
param(
    [string]$Root
)

$ErrorActionPreference = 'Stop'

if (-not $Root) {
    $Root = Join-Path $PSScriptRoot '..\..\docs\07-工具逆向'
}
if (-not (Test-Path $Root)) {
    throw "证据库根目录不存在: $Root"
}
$root = (Resolve-Path $Root).Path
$toolsDir = Join-Path $root 'tools'
$pagesDir = Join-Path $root 'pages'
$outPath = Join-Path $root 'index.json'

if (-not (Test-Path $toolsDir)) {
    throw "工具卡目录不存在: $toolsDir"
}

$pageCount = 0
$pageNames = @{}
if (Test-Path $pagesDir) {
    $pageFiles = @(Get-ChildItem -Path $pagesDir -Filter '*.html' -File)
    $pageCount = $pageFiles.Count
    foreach ($pf in $pageFiles) { $pageNames[$pf.BaseName] = $true }
}

# 我方页面命名规则：<uiCategory 的 PascalCase>-<originalPlugin>.html，uiCategory 缺省时用 Unknown
function Get-PageBaseName([string]$uiCategory, [string]$originalPlugin) {
    $prefix = 'Unknown'
    if (-not [string]::IsNullOrWhiteSpace($uiCategory)) {
        $prefix = ''
        foreach ($seg in ($uiCategory -split '[-_]')) {
            if ($seg.Length -gt 0) { $prefix += $seg.Substring(0, 1).ToUpper() + $seg.Substring(1) }
        }
    }
    return ($prefix + '-' + $originalPlugin)
}

# 参数面板预览页（docs/08-工具预览/ui）单独统计，供 targetPages 对齐
$targetPagesRoot = Join-Path (Split-Path $root -Parent) '08-工具预览\ui'
$targetPageCount = 0
if (Test-Path $targetPagesRoot) {
    $targetPageCount = @(Get-ChildItem -Path $targetPagesRoot -Filter '*.html' -File).Count
}
$pageMiss = New-Object 'System.Collections.Generic.List[string]'

$statusCounts = @{}
$originCounts = @{}
$uiKindCounts = @{}
$priorityCounts = @{}
$profileCounts = @{}
$tools = New-Object 'System.Collections.Generic.List[object]'

foreach ($dir in (Get-ChildItem -Path $toolsDir -Directory | Sort-Object Name)) {
    $toolId = $dir.Name
    if ($toolId.StartsWith('_')) { continue }

    $evPath = Join-Path $dir.FullName 'evidence.json'
    if (-not (Test-Path $evPath)) { continue }
    $ev = Get-Content $evPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $evProps = @($ev.PSObject.Properties.Name)

    $status = $ev.status
    if (-not $status) { $status = 'unknown' }
    if ($statusCounts.ContainsKey($status)) { $statusCounts[$status]++ } else { $statusCounts[$status] = 1 }

    $origin = 'codex'
    if (($evProps -contains 'origin') -and $ev.origin) { $origin = $ev.origin }
    if ($originCounts.ContainsKey($origin)) { $originCounts[$origin]++ } else { $originCounts[$origin] = 1 }

    $priority = $ev.priority
    if (-not $priority) { $priority = 'unknown' }
    if ($priorityCounts.ContainsKey($priority)) { $priorityCounts[$priority]++ } else { $priorityCounts[$priority] = 1 }

    $profile = 'standard'
    if ($evProps -contains 'authority') { $profile = 'deep' }
    if ($profileCounts.ContainsKey($profile)) { $profileCounts[$profile]++ } else { $profileCounts[$profile] = 1 }

    $uiKind = 'empty'
    $uiPath = Join-Path $dir.FullName 'ui-preview.json'
    if (Test-Path $uiPath) {
        $ui = Get-Content $uiPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $uiProps = @($ui.PSObject.Properties.Name)
        if (($uiProps -contains 'previewKind') -and $ui.previewKind) { $uiKind = $ui.previewKind }
        elseif ($uiProps -contains 'kind') { $uiKind = 'schema-preview' }
    }
    if ($uiKindCounts.ContainsKey($uiKind)) { $uiKindCounts[$uiKind]++ } else { $uiKindCounts[$uiKind] = 1 }

    $unknowns = @()
    if ($evProps -contains 'unknowns') { $unknowns = @($ev.unknowns | Where-Object { $_ -ne $null }) }

    $pageBase = Get-PageBaseName $ev.uiCategory $ev.originalPlugin
    $pagePath = $null
    if ($pageNames.ContainsKey($pageBase)) { $pagePath = "pages/$pageBase.html" }
    else { $pageMiss.Add($toolId) }

    $item = [ordered]@{
        toolId          = $toolId
        displayName     = $ev.displayName
        originalPlugin  = $ev.originalPlugin
        category        = $ev.originalCategory
        uiCategory      = $ev.uiCategory
        priority        = $ev.priority
        status          = $ev.status
        evidenceState   = $ev.evidenceState
        origin          = $origin
        profile         = $profile
        uiPreviewKind   = $uiKind
        cardPath        = "tools/$toolId/"
        pagePath        = $pagePath
        counts          = [ordered]@{
            resources   = $ev.resourceCount
            parameters  = $ev.parameterCount
            inputs      = $ev.inputCount
            outputs     = $ev.outputCount
            uiControls  = $ev.uiControlCount
        }
        unknownCount    = $unknowns.Count
        unknowns        = $unknowns
    }
    $tools.Add([pscustomobject]$item)
}

$index = [ordered]@{
    schemaVersion = '1.1'
    generated     = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
    generator     = 'scripts/tool-reverse/generate-index.ps1（本文件由脚本生成，手改会被覆盖）'
    source        = [ordered]@{
        inherited     = 'codex-visionreplica'
        inheritedPath = 'D:\MyWork\REA逆向测试'
        inheritedRule = '缺 origin 字段视为继承自 Codex，我方未逐条复验'
        own           = 'suneyevision'
    }
    counts        = [ordered]@{
        cards       = $tools.Count
        pages       = $pageCount
        targetCards = $tools.Count
        targetPages = $targetPageCount
        pageLinked  = $tools.Count - $pageMiss.Count
        pageMissing = $pageMiss.Count
    }
    statusCounts   = $statusCounts
    originCounts   = $originCounts
    priorityCounts = $priorityCounts
    profileCounts  = $profileCounts
    uiKindCounts   = $uiKindCounts
    tools          = $tools
}

$json = $index | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($outPath, $json, [System.Text.UTF8Encoding]::new($false))

Write-Host ''
Write-Host '================ 索引生成 ================' -ForegroundColor Cyan
Write-Host ("工具卡: {0}" -f $tools.Count)
Write-Host ("状态分布: {0}" -f (($statusCounts.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join '  '))
Write-Host ("档案分布: {0}" -f (($profileCounts.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join '  '))
Write-Host ("UI 形态: {0}" -f (($uiKindCounts.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join '  '))
Write-Host ("页面对齐: {0}/{1}（缺 {2}）" -f ($tools.Count - $pageMiss.Count), $tools.Count, $pageMiss.Count)
if ($pageMiss.Count -gt 0) {
    Write-Host ("  未对上页面的卡: {0}" -f (($pageMiss | Select-Object -First 10) -join ', ')) -ForegroundColor Yellow
}
Write-Host ("输出: {0}" -f $outPath)
Write-Host ''
