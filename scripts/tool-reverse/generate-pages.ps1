#requires -Version 5.1
<#
.SYNOPSIS
    从工具卡数据生成我方工具对照页与总览索引页。

.DESCRIPTION
    读取 docs/07-工具逆向/tools/<toolid>/ 的七件套，按 contracts/page.sections.json
    定义的 8 个固定章节渲染 HTML 到 docs/07-工具逆向/pages/，并生成 index.html。

    原则：
      - 「原件事实」来自卡片证据，保留路径与 SHA-256 溯源
      - 「当前实现」只读 evidence.json.currentImplementation；缺失即显示「我方未核对」
      - 分析环节与算法对照在卡片未记录时如实显示未建立，不编造
      - 中文内容写入 UTF-8 无 BOM

.PARAMETER Root
    证据库根目录，默认按脚本位置推导。

.PARAMETER OnlyTool
    只生成指定工具页，便于调试。

.EXAMPLE
    pwsh -File scripts/tool-reverse/generate-pages.ps1
    pwsh -File scripts/tool-reverse/generate-pages.ps1 -OnlyTool imagesourcemodule
#>
[CmdletBinding()]
param(
    [string]$Root,
    [string]$OnlyTool
)

$ErrorActionPreference = 'Stop'

if (-not $Root) {
    $Root = Join-Path $PSScriptRoot '..\..\docs\07-工具逆向'
}
if (-not (Test-Path $Root)) { throw "证据库根目录不存在: $Root" }
$root = (Resolve-Path $Root).Path
$toolsDir = Join-Path $root 'tools'
$pagesDir = Join-Path $root 'pages'
$contractPath = Join-Path $root 'contracts\page.sections.json'

if (-not (Test-Path $contractPath)) { throw "页面契约不存在: $contractPath" }
if (-not (Test-Path $pagesDir)) { New-Item -ItemType Directory -Path $pagesDir -Force | Out-Null }

$contract = Get-Content $contractPath -Raw -Encoding UTF8 | ConvertFrom-Json
$stageSpecs = @($contract.analysisStages)
$dimSpecs = @($contract.algorithmDimensions)
$stepSpecs = @($contract.semanticSteps)
$badges = $contract.statusBadges
$fixedNotice = ($contract.sections | Where-Object { $_.id -eq 'notice' }).fixedText
$categoryExt = $contract.categoryExtensions

$utf8 = [System.Text.UTF8Encoding]::new($false)

function Encode-Html {
    param([object]$Value)
    if ($null -eq $Value) { return '' }
    $s = [string]$Value
    $s = $s -replace '&', '&amp;'
    $s = $s -replace '<', '&lt;'
    $s = $s -replace '>', '&gt;'
    $s = $s -replace '"', '&quot;'
    return $s
}

# 调试窗预览由 scripts/tool-reverse/generate-previews.ps1 产出，
# 文件名规则 <原件分类>-<原件模块>.html，与本页命名（uiCategory-模块）不同，必须按模块名查。
$previewDir = Join-Path (Split-Path $root -Parent) '08-工具预览\preview'

# 预览文件名是 <原件分类>-<原件模块>.html，但部分卡片的 originalCategory 为空，
# 且模块名里 _ 与 - 两种写法并存。按「规范化模块名 -> 文件」建一次缓存，避免逐卡扫目录。
$script:previewByModule = $null
function Get-PreviewByModule {
    if ($null -ne $script:previewByModule) { return $script:previewByModule }
    $script:previewByModule = @{}
    if (-not (Test-Path $previewDir)) { return $script:previewByModule }
    foreach ($f in (Get-ChildItem -Path $previewDir -Filter *.html -File)) {
        $base = $f.BaseName
        $idx = $base.IndexOf('-')
        if ($idx -lt 0) { continue }
        $mod = $base.Substring($idx + 1)
        $key = ($mod -replace '[-_]', '').ToLower()
        if (-not $script:previewByModule.ContainsKey($key)) { $script:previewByModule[$key] = @() }
        $script:previewByModule[$key] += $f.Name
    }
    return $script:previewByModule
}

function Get-PreviewInfo {
    param([string]$ToolId, [object]$Evidence)
    $result = [pscustomobject]@{ level = 'none'; file = ''; rel = '' }
    if (-not $Evidence) { return $result }
    $props = @($Evidence.PSObject.Properties.Name)
    if (-not ($props -contains 'originalPlugin')) { return $result }
    $plugin = [string]$Evidence.originalPlugin
    if (-not $plugin) { return $result }
    $cat = 'Unknown'
    if (($props -contains 'originalCategory') -and $Evidence.originalCategory) { $cat = [string]$Evidence.originalCategory }
    $safePlugin = $plugin -replace '[^\w\-]', ''
    $safeCat = $cat -replace '[^\w\-]', ''
    if (-not $safePlugin -or -not $safeCat) { return $result }

    $file = $safeCat + '-' + $safePlugin + '.html'
    $path = Join-Path $previewDir $file
    if (-not (Test-Path $path)) {
        # 分类缺失或下划线写法不同时，按规范化模块名唯一匹配；匹配到多个就不猜，按无预览处理
        $key = ($safePlugin -replace '[-_]', '').ToLower()
        $index = Get-PreviewByModule
        if (-not $index.ContainsKey($key)) { return $result }
        $hits = @($index[$key])
        if ($hits.Count -ne 1) { return $result }
        $file = $hits[0]
        $path = Join-Path $previewDir $file
    }
    if (-not (Test-Path $path)) { return $result }

    $result.level = 'shell'
    $result.file = $file
    $result.rel = '../../08-工具预览/preview/' + $file
    if ([System.IO.File]::ReadAllText($path, $utf8).Contains('suneye:preview-level=control')) {
        $result.level = 'control'
    }
    return $result
}

function Get-BadgeText {
    param([string]$Status)
    if ($Status -and $badges.PSObject.Properties.Name -contains $Status) { return $badges.$Status }
    return $Status
}

function Get-CategoryPascal {
    param([string]$Category)
    if (-not $Category) { return 'Unknown' }
    $parts = $Category -split '[-_]'
    $out = ''
    foreach ($p in $parts) {
        if ($p.Length -gt 0) { $out += $p.Substring(0, 1).ToUpper() + $p.Substring(1) }
    }
    return $out
}

function Get-SafeName {
    param([string]$Name)
    if (-not $Name) { return 'unknown' }
    $safe = $Name -replace '[^\w\-]', ''
    if (-not $safe) { return 'unknown' }
    return $safe
}

$css = @'
:root{--bg:#f6f7f9;--card:#ffffff;--line:#e4e7ec;--text:#1d2129;--muted:#646a73;--accent:#2563eb;--warn:#b45309;--ok:#15803d;--bad:#b91c1c}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--text);font:14px/1.65 -apple-system,"Segoe UI","Microsoft YaHei",sans-serif}
.wrap{max-width:1120px;margin:0 auto;padding:26px 20px 60px}
nav.crumbs{font-size:13px;color:var(--muted);margin-bottom:16px}
nav.crumbs a{color:var(--accent);text-decoration:none;margin-right:16px}
nav.crumbs a:hover{text-decoration:underline}
header.top{border-bottom:1px solid var(--line);padding-bottom:18px;margin-bottom:26px}
h1{font-size:25px;margin:0 0 10px;display:flex;align-items:center;gap:10px;flex-wrap:wrap;line-height:1.3}
.badge{font-size:12px;padding:3px 11px;border-radius:999px;font-weight:600;white-space:nowrap}
.badge.inventory-only{background:#eef1f5;color:#475569}
.badge.source-indexed{background:#e0f2fe;color:#075985}
.badge.source-partial{background:#fef3c7;color:#92400e}
.badge.source-confirmed{background:#dcfce7;color:#166534}
.badge.unknown,.badge.not-established,.badge.deferred{background:#f1f2f4;color:#6b7280}
.badge.partial{background:#fef3c7;color:#92400e}
.badge.done,.badge.established,.badge.confirmed{background:#dcfce7;color:#166534}
.badge.inferred{background:#e0f2fe;color:#075985}
.meta{font-size:13px;color:var(--muted);margin:0}
.meta code{background:#eef1f5;padding:1px 6px;border-radius:4px;font-size:12px}
.notice{margin:14px 0 0;padding:10px 14px;background:#fffbeb;border-left:3px solid #f59e0b;color:#78350f;font-size:13px;border-radius:0 6px 6px 0}
section.card{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:18px 20px;margin-bottom:18px}
section.card>h2{font-size:16px;margin:0 0 14px;padding-bottom:10px;border-bottom:1px solid var(--line);display:flex;align-items:center;gap:8px}
section.card>h2 .tag{font-size:11px;font-weight:500;color:var(--muted);background:#f1f2f4;padding:2px 8px;border-radius:4px}
table{width:100%;border-collapse:collapse;font-size:13px}
th,td{text-align:left;padding:8px 10px;border-bottom:1px solid var(--line);vertical-align:top}
th{background:#fafbfc;font-weight:600;color:#374151;font-size:12px;letter-spacing:.02em}
tr:last-child td{border-bottom:none}
code.mono{font-family:Consolas,Monaco,monospace;font-size:12px;background:#f5f6f8;padding:1px 5px;border-radius:4px;word-break:break-all}
.sha{color:#6b7280;font-size:11px}
.empty{color:var(--muted);font-size:13px;padding:4px 0}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(300px,1fr));gap:12px}
.item{border:1px solid var(--line);border-radius:8px;padding:12px 14px;background:#fcfcfd}
.item .hd{display:flex;justify-content:space-between;align-items:center;gap:8px;margin-bottom:6px}
.item .nm{font-weight:600;font-size:13px}
.item .dt{font-size:12px;color:var(--muted)}
.item .nd{font-size:12px;color:var(--muted);margin-top:6px;padding-top:6px;border-top:1px dashed var(--line)}
ul.plain{margin:6px 0 0;padding-left:20px;font-size:13px;color:#374151}
ul.plain li{margin:3px 0}
footer{margin-top:30px;padding-top:16px;border-top:1px solid var(--line);font-size:12px;color:var(--muted)}
a{color:var(--accent)}
.links{font-size:12px;line-height:1.9;white-space:nowrap}
.links a{text-decoration:none}
.links a:hover{text-decoration:underline}
.links .sep{color:#d1d5db;margin:0 3px}
.cta{display:inline-block;padding:8px 18px;background:var(--accent);color:#fff!important;border-radius:6px;text-decoration:none;font-size:13px;font-weight:600}
.cta:hover{filter:brightness(.94);text-decoration:none!important}
.thumb{display:block;max-width:100%;height:auto;margin:12px 0;border:1px solid var(--line);border-radius:8px;background:#101827}
.offline{margin:14px 0 0;padding:10px 14px;background:#ecfdf5;border-left:3px solid #10b981;color:#065f46;font-size:13px;border-radius:0 6px 6px 0}
'@

function New-Page {
    param(
        [string]$ToolId,
        [object]$Evidence,
        [object]$UiPreview,
        [object]$Resources,
        [string]$SemanticsText,
        [string]$PageFileName
    )

    $evProps = @($Evidence.PSObject.Properties.Name)
    $displayName = $Evidence.displayName
    if (-not $displayName) { $displayName = $ToolId }
    $status = $Evidence.status
    if (-not $status) { $status = 'unknown' }
    $origin = 'codex'
    if (($evProps -contains 'origin') -and $Evidence.origin) { $origin = $Evidence.origin }
    $profile = 'standard'
    if ($evProps -contains 'authority') { $profile = 'deep' }

    $sb = New-Object System.Text.StringBuilder

    [void]$sb.AppendLine('<!DOCTYPE html>')
    [void]$sb.AppendLine('<html lang="zh-CN"><head><meta charset="utf-8">')
    [void]$sb.AppendLine('<meta name="viewport" content="width=device-width,initial-scale=1">')
    [void]$sb.AppendLine('<title>' + (Encode-Html $displayName) + ' · 工具对照</title>')
    [void]$sb.AppendLine('<style>' + $css + '</style>')
    [void]$sb.AppendLine('</head><body><div class="wrap">')

    # header
    $cardDir = Join-Path $toolsDir $ToolId
    $evidenceFiles = [ordered]@{
        '逆向记录' = $ToolId + '-reverse.md'
        '证据索引' = 'evidence.json'
        '参数全量' = 'parameters.json'
        '资源清单' = 'resources.json'
        'UI 描述'  = 'ui-preview.json'
        '布局图'   = 'ui-preview.svg'
        '算法语义' = 'algorithm-semantics.md'
    }
    $evidenceLinks = @()
    foreach ($k in $evidenceFiles.Keys) {
        $fn = $evidenceFiles[$k]
        if (Test-Path (Join-Path $cardDir $fn)) {
            $evidenceLinks += '<a href="../tools/' + (Encode-Html $ToolId) + '/' + (Encode-Html $fn) + '">' + (Encode-Html $k) + '</a>'
        }
    }
    $hasUiSvg = Test-Path (Join-Path $cardDir 'ui-preview.svg')

    [void]$sb.AppendLine('<nav class="crumbs"><a href="../index.html">&larr; 工具索引</a><a href="../_reference-codex/site/index.html">Codex 原站（只读）</a><a href="../../collaboration/evidence/">证据目录</a></nav>')
    if ($evidenceLinks.Count -gt 0) {
        [void]$sb.AppendLine('<nav class="crumbs links">证据直达' + '<span class="sep">｜</span>' + ($evidenceLinks -join '<span class="sep">｜</span>') + '</nav>')
    }
    [void]$sb.AppendLine('<header class="top">')
    [void]$sb.AppendLine('<h1>' + (Encode-Html $displayName) + '<span class="badge ' + (Encode-Html $status) + '">' + (Encode-Html (Get-BadgeText $status)) + '</span></h1>')
    $metaParts = @()
    $metaParts += 'toolId <code>' + (Encode-Html $ToolId) + '</code>'
    if ($Evidence.originalPlugin) { $metaParts += '原件 <code>' + (Encode-Html $Evidence.originalPlugin) + '</code>' }
    if ($Evidence.originalCategory) { $metaParts += '分类 ' + (Encode-Html $Evidence.originalCategory) }
    if ($Evidence.uiCategory) { $metaParts += 'uiCategory <code>' + (Encode-Html $Evidence.uiCategory) + '</code>' }
    if ($Evidence.priority) { $metaParts += '优先级 ' + (Encode-Html $Evidence.priority) }
    $metaParts += '档案 ' + (Encode-Html $profile)
    $metaParts += '来源 ' + (Encode-Html $origin)
    [void]$sb.AppendLine('<p class="meta">' + ($metaParts -join ' &nbsp;·&nbsp; ') + '</p>')
    [void]$sb.AppendLine('<p class="notice">本页不证明该工具在原厂已注册、加载、执行或完成。</p>')
    [void]$sb.AppendLine('</header><main>')

    # 0 调试窗预览：主入口指向控件级 HTML；SVG 只作控件树证据，不再当预览图
    $previewInfo = Get-PreviewInfo -ToolId $ToolId -Evidence $Evidence
    [void]$sb.AppendLine('<section class="card" id="uiPreview"><h2>调试窗预览<span class="tag">控件级还原</span></h2>')
    if ($previewInfo.level -eq 'control') {
        [void]$sb.AppendLine('<p class="meta">按原件 <code class="mono">AlgorithmTabRoot</code> 的控件声明还原：页签、控件形态、声明顺序、参数默认值与输入输出声明。不执行算法，不含运行结果。</p>')
        [void]$sb.AppendLine('<p class="links"><a class="cta" href="' + (Encode-Html $previewInfo.rel) + '">打开调试窗预览</a></p>')
    }
    elseif ($previewInfo.level -eq 'shell') {
        [void]$sb.AppendLine('<p class="meta">该工具目前只有三栏外壳预览（参数表 + 空画布 + 结果表），控件级还原未做。</p>')
        [void]$sb.AppendLine('<p class="links"><a href="' + (Encode-Html $previewInfo.rel) + '">打开外壳预览（待升级）</a></p>')
    }
    else {
        [void]$sb.AppendLine('<p class="empty">无预览文件，不提供入口。</p>')
    }
    if ($hasUiSvg) {
        [void]$sb.AppendLine('<p class="meta">控件树证据（非预览图）：<code class="mono">tools/' + (Encode-Html $ToolId) + '/ui-preview.svg</code>，描述控件树与分区，属证据性布局。</p>')
        if ($UiPreview) {
            $uiProps = @($UiPreview.PSObject.Properties.Name)
            $ctrlJson = @($UiPreview.controls | Where-Object { $_ -ne $null }).Count
            $uiParts = @()
            if (($uiProps -contains 'previewKind') -and $UiPreview.previewKind) { $uiParts += 'previewKind <code>' + (Encode-Html $UiPreview.previewKind) + '</code>' }
            $uiParts += '控件 ' + $ctrlJson + ' 项'
            $uiParts += '缺失项 ' + @($UiPreview.missing | Where-Object { $_ -ne $null }).Count + ' 条'
            [void]$sb.AppendLine('<p class="meta">' + ($uiParts -join ' &nbsp;·&nbsp; ') + '</p>')
            $svgRaw = Get-Content (Join-Path $cardDir 'ui-preview.svg') -Raw -Encoding UTF8
            $svgM = [regex]::Match($svgRaw, 'controls: (\d+)')
            if ($svgM.Success -and [int]$svgM.Groups[1].Value -ne $ctrlJson) {
                [void]$sb.AppendLine('<p class="notice">图上标注 controls ' + $svgM.Groups[1].Value + ' 与控件树 JSON 实际 ' + $ctrlJson + ' 条不一致。本页统计以 JSON 为准，差异待逆向负责人核实口径，不在此处改图。</p>')
            }
        }
        [void]$sb.AppendLine('<p class="links"><a href="../tools/' + (Encode-Html $ToolId) + '/ui-preview.html">控件树 JSON</a><span class="sep">｜</span><a href="../tools/' + (Encode-Html $ToolId) + '/ui-preview.svg">SVG 布局证据</a></p>')
    }
    [void]$sb.AppendLine('</section>')

    # 1 原版入口与证据
    [void]$sb.AppendLine('<section class="card" id="originalEvidence"><h2>原版入口与证据<span class="tag">originalEvidence</span></h2>')
    if ($Evidence.originalSourcePath) {
        [void]$sb.AppendLine('<p class="meta" style="margin-bottom:12px">原件路径 <code class="mono">' + (Encode-Html $Evidence.originalSourcePath) + '</code></p>')
    }
    $evList = @($Evidence.sourceEvidence | Where-Object { $_ -ne $null })
    if ($evList.Count -gt 0) {
        [void]$sb.AppendLine('<table><thead><tr><th style="width:36%">符号 / 文件</th><th style="width:14%">行号</th><th style="width:16%">SHA-256</th><th>可确认范围</th></tr></thead><tbody>')
        foreach ($e in $evList) {
            $sha = ''
            if ($e.sha256 -and $e.sha256.Length -ge 12) { $sha = $e.sha256.Substring(0, 12).ToUpper() }
            [void]$sb.AppendLine('<tr><td><code class="mono">' + (Encode-Html $e.symbol) + '</code><br><span class="sha">' + (Encode-Html $e.path) + '</span></td><td>' + (Encode-Html $e.lines) + '</td><td><span class="sha">' + $sha + '</span></td><td>' + (Encode-Html $e.confirmable) + '</td></tr>')
        }
        [void]$sb.AppendLine('</tbody></table>')
    }
    else {
        [void]$sb.AppendLine('<p class="empty">卡片未登记 sourceEvidence 条目。</p>')
    }
    $xmlList = @()
    if ($Resources) { $xmlList = @($Resources.xmlEvidence | Where-Object { $_ -ne $null }) }
    if ($xmlList.Count -gt 0) {
        [void]$sb.AppendLine('<h2 style="margin-top:20px">资源与 XML 证据</h2>')
        [void]$sb.AppendLine('<table><thead><tr><th style="width:60%">文件</th><th style="width:14%">根节点</th><th>SHA-256</th></tr></thead><tbody>')
        foreach ($x in $xmlList) {
            $sha = ''
            if ($x.sha256 -and $x.sha256.Length -ge 12) { $sha = $x.sha256.Substring(0, 12).ToUpper() }
            [void]$sb.AppendLine('<tr><td><span class="sha">' + (Encode-Html $x.path) + '</span></td><td><code class="mono">' + (Encode-Html $x.root) + '</code></td><td><span class="sha">' + $sha + '</span></td></tr>')
        }
        [void]$sb.AppendLine('</tbody></table>')
    }
    [void]$sb.AppendLine('</section>')

    # 2 当前实现
    [void]$sb.AppendLine('<section class="card" id="currentImplementation"><h2>当前实现与差异<span class="tag">currentImplementation</span></h2>')
    if ($evProps -contains 'currentImplementation' -and $Evidence.currentImplementation) {
        $ci = $Evidence.currentImplementation
        [void]$sb.AppendLine('<table><tbody>')
        [void]$sb.AppendLine('<tr><th style="width:24%">ModuleId</th><td>' + (Encode-Html $ci.moduleId) + '</td></tr>')
        [void]$sb.AppendLine('<tr><th>实现路径</th><td><code class="mono">' + (Encode-Html $ci.implPath) + '</code></td></tr>')
        [void]$sb.AppendLine('<tr><th>核对时间</th><td>' + (Encode-Html $ci.verifiedAt) + '</td></tr>')
        $diffs = @($ci.differences | Where-Object { $_ -ne $null })
        if ($diffs.Count -gt 0) {
            [void]$sb.AppendLine('<tr><th>差异</th><td><ul class="plain">')
            foreach ($d in $diffs) { [void]$sb.AppendLine('<li>' + (Encode-Html $d) + '</li>') }
            [void]$sb.AppendLine('</ul></td></tr>')
        }
        [void]$sb.AppendLine('</tbody></table>')
    }
    else {
        [void]$sb.AppendLine('<p class="empty">我方未核对。此卡继承自 Codex，其「当前实现」描述的是 VisionReplica 项目状态，与我方无关，故不在此展示。</p>')
    }
    [void]$sb.AppendLine('</section>')

    # 3 分析环节
    # 状态优先取卡片 evidence.json.analysisStages；卡片未记录时如实 unknown。
    # 不按「有 UI 预览文件」「参数计数大于 0」推断为部分，那会把「有清单」说成「已核对」，属于状态冒称。
    $stageMap = @{}
    if ($evProps -contains 'analysisStages') {
        foreach ($s in @($Evidence.analysisStages)) {
            if ($s -and $s.id) { $stageMap[$s.id] = $s }
        }
    }
    [void]$sb.AppendLine('<section class="card" id="analysisStages"><h2>分析环节<span class="tag">7 环节</span></h2><div class="grid">')
    foreach ($st in $stageSpecs) {
        $stStatus = 'unknown'
        $stDetail = $st.covers
        $stLinks = @()
        if ($stageMap.ContainsKey($st.id)) {
            $rec = $stageMap[$st.id]
            if ($rec.status) { $stStatus = $rec.status }
            if ($rec.detail) { $stDetail = $rec.detail }
            if ($rec.links) { $stLinks = @($rec.links | Where-Object { $_ -ne $null }) }
        }
        $linkHtml = ''
        if ($stLinks.Count -gt 0) {
            $linkHtml = '<div class="nd" style="color:#2563eb">' + (Encode-Html ($stLinks -join ' ｜ ')) + '</div>'
        }
        [void]$sb.AppendLine('<div class="item"><div class="hd"><span class="nm">' + (Encode-Html $st.name) + '</span><span class="badge ' + (Encode-Html $stStatus) + '">' + (Encode-Html (Get-BadgeText $stStatus)) + '</span></div><div class="dt">' + (Encode-Html $stDetail) + '</div>' + $linkHtml + '</div>')
    }
    [void]$sb.AppendLine('</div><p class="empty" style="margin-top:12px">环节状态默认 unknown；卡片未记录逐环节证据时不得标 done。状态来源：<code class="mono">tools/' + (Encode-Html $ToolId) + '/evidence.json</code> 的 analysisStages。</p></section>')

    # 4 算法语义链
    # conclusion 优先取卡片 semanticChain；卡片未记录时退化为「语义文件提到该步骤」= inferred。
    # 单字关键词匹配会让全部步骤都标成推断，所以只用完整中文步骤名精确匹配。
    $chainMap = @{}
    if ($evProps -contains 'semanticChain') {
        foreach ($c in @($Evidence.semanticChain)) {
            if ($c -and $c.step) { $chainMap[$c.step] = $c }
        }
    }
    [void]$sb.AppendLine('<section class="card" id="algorithmSemantics"><h2>算法语义链<span class="tag">9 步</span></h2><table><thead><tr><th style="width:22%">步骤</th><th style="width:12%">状态</th><th>原版证据 / 说明</th><th style="width:24%">仍需补什么</th></tr></thead><tbody>')
    foreach ($step in $stepSpecs) {
        $stepStatus = 'unknown'
        $stepNote = $step.needs
        $stepUnknown = '卡片未记录该步骤证据'
        if ($chainMap.ContainsKey($step.step)) {
            $rec = $chainMap[$step.step]
            if ($rec.conclusion) { $stepStatus = $rec.conclusion }
            if ($rec.originalEvidence) { $stepNote = $rec.originalEvidence }
            if ($rec.unknown) { $stepUnknown = $rec.unknown }
        }
        else {
            $hit = $false
            if ($SemanticsText -and ($SemanticsText -match [regex]::Escape($step.name))) { $hit = $true }
            if ($hit) {
                $stepStatus = 'inferred'
                $stepNote = '语义文件提到该步骤，未达源码级确认'
            }
        }
        $unknownHtml = ''
        if ($stepStatus -ne 'confirmed') { $unknownHtml = (Encode-Html $stepUnknown) }
        [void]$sb.AppendLine('<tr><td>' + (Encode-Html $step.name) + '</td><td><span class="badge ' + (Encode-Html $stepStatus) + '">' + (Encode-Html (Get-BadgeText $stepStatus)) + '</span></td><td>' + (Encode-Html $stepNote) + '</td><td class="empty">' + $unknownHtml + '</td></tr>')
    }
    [void]$sb.AppendLine('</tbody></table><p class="empty" style="margin-top:10px">「推断」表示语义文件提到了该步骤，但未达到源码级确认。原始语义文件：<code class="mono">tools/' + (Encode-Html $ToolId) + '/algorithm-semantics.md</code></p></section>')

    # 5 算法对照
    $dimMap = @{}
    if ($evProps -contains 'algorithmComparison') {
        foreach ($d in @($Evidence.algorithmComparison)) {
            if ($d -and $d.id) { $dimMap[$d.id] = $d }
        }
    }
    [void]$sb.AppendLine('<section class="card" id="algorithmComparison"><h2>算法对照<span class="tag">7 维</span></h2><table><thead><tr><th style="width:22%">维度</th><th style="width:12%">状态</th><th>已取得的对比证据</th><th style="width:24%">缺什么</th></tr></thead><tbody>')
    foreach ($dim in $dimSpecs) {
        $dimStatus = 'not-established'
        $dimEvidence = '卡片未记录该维度对照，需补'
        $dimUnknown = '未建立对比'
        if ($dimMap.ContainsKey($dim.id)) {
            $rec = $dimMap[$dim.id]
            if ($rec.status) { $dimStatus = $rec.status }
            $evList2 = @($rec.evidence | Where-Object { $_ -ne $null })
            if ($evList2.Count -gt 0) { $dimEvidence = ($evList2 -join ' ｜ ') }
            if ($rec.unknown) { $dimUnknown = $rec.unknown }
        }
        $dimUnknownHtml = ''
        if ($dimStatus -ne 'established') { $dimUnknownHtml = (Encode-Html $dimUnknown) }
        [void]$sb.AppendLine('<tr><td>' + (Encode-Html $dim.name) + '</td><td><span class="badge ' + (Encode-Html $dimStatus) + '">' + (Encode-Html (Get-BadgeText $dimStatus)) + '</span></td><td>' + (Encode-Html $dimEvidence) + '</td><td class="empty">' + $dimUnknownHtml + '</td></tr>')
    }
    [void]$sb.AppendLine('</tbody></table><p class="empty" style="margin-top:10px">UI 例证、XML 参数数量、目录映射或编译通过均不替代数值基线。</p></section>')

    # 5b 同类特有章节（按 uiCategory 扩展）
    $cat = $Evidence.uiCategory
    if ($cat -and ($categoryExt.PSObject.Properties.Name -contains $cat)) {
        $extItems = @($categoryExt.$cat | Where-Object { $_ -ne $null })
        if ($extItems.Count -gt 0) {
            [void]$sb.AppendLine('<section class="card" id="categoryExtension"><h2>同类特有条目<span class="tag">' + (Encode-Html $cat) + '</span></h2><ul class="plain">')
            foreach ($it in $extItems) { [void]$sb.AppendLine('<li>' + (Encode-Html $it) + '</li>') }
            [void]$sb.AppendLine('</ul><p class="empty" style="margin-top:10px">本清单由 <code class="mono">contracts/page.sections.json</code> 的 categoryExtensions 按 uiCategory 决定，同类工具共用同一组条目；具体取值仍需在该工具的 7 环节与 9 步中逐项取证。</p></section>')
        }
    }

    # 6 未知
    [void]$sb.AppendLine('<section class="card" id="unknowns"><h2>未知与待补<span class="tag">unknowns</span></h2>')
    $unknowns = @()
    if ($evProps -contains 'unknowns') { $unknowns = @($Evidence.unknowns | Where-Object { $_ -ne $null }) }
    $missing = @()
    if ($UiPreview -and ($UiPreview.PSObject.Properties.Name -contains 'missing')) { $missing = @($UiPreview.missing | Where-Object { $_ -ne $null }) }
    if ($unknowns.Count -gt 0) {
        [void]$sb.AppendLine('<h2 style="margin:0 0 8px;font-size:13px;border:none;padding:0">证据未知项</h2><ul class="plain">')
        foreach ($u in $unknowns) { [void]$sb.AppendLine('<li>' + (Encode-Html $u) + '</li>') }
        [void]$sb.AppendLine('</ul>')
    }
    if ($missing.Count -gt 0) {
        [void]$sb.AppendLine('<h2 style="margin:14px 0 8px;font-size:13px;border:none;padding:0">UI 缺失项</h2><ul class="plain">')
        foreach ($m in $missing) { [void]$sb.AppendLine('<li>' + (Encode-Html $m) + '</li>') }
        [void]$sb.AppendLine('</ul>')
    }
    if ($unknowns.Count -eq 0 -and $missing.Count -eq 0) {
        [void]$sb.AppendLine('<p class="empty">卡片未登记未知项。这通常意味着尚未真正取证，而非无未知。</p>')
    }
    [void]$sb.AppendLine('</section>')

    # 7 固定声明
    [void]$sb.AppendLine('<section class="card" id="notice"><h2>对比范围与未知<span class="tag">notice</span></h2><p class="empty">' + (Encode-Html $fixedNotice) + '</p></section>')

    [void]$sb.AppendLine('</main>')
    [void]$sb.AppendLine('<footer>由 <code class="mono">scripts/tool-reverse/generate-pages.ps1</code> 从卡片数据生成。修改请改卡片，不要直接改本页。生成时间 ' + (Get-Date -Format 'yyyy-MM-dd HH:mm') + '</footer>')
    [void]$sb.AppendLine('</div></body></html>')

    $outPath = Join-Path $pagesDir $PageFileName
    [System.IO.File]::WriteAllText($outPath, $sb.ToString(), $utf8)
    return $PageFileName
}

$toolDirs = Get-ChildItem -Path $toolsDir -Directory | Sort-Object Name
if ($OnlyTool) { $toolDirs = @($toolDirs | Where-Object { $_.Name -eq $OnlyTool }) }

$generated = New-Object 'System.Collections.Generic.List[object]'
$usedNames = @{}

foreach ($dir in $toolDirs) {
    $toolId = $dir.Name
    if ($toolId.StartsWith('_')) { continue }

    $evPath = Join-Path $dir.FullName 'evidence.json'
    if (-not (Test-Path $evPath)) { continue }
    $ev = Get-Content $evPath -Raw -Encoding UTF8 | ConvertFrom-Json

    $ui = $null
    $uiPath = Join-Path $dir.FullName 'ui-preview.json'
    if (Test-Path $uiPath) { $ui = Get-Content $uiPath -Raw -Encoding UTF8 | ConvertFrom-Json }

    $res = $null
    $resPath = Join-Path $dir.FullName 'resources.json'
    if (Test-Path $resPath) { $res = Get-Content $resPath -Raw -Encoding UTF8 | ConvertFrom-Json }

    $semText = ''
    $semPath = Join-Path $dir.FullName 'algorithm-semantics.md'
    if (Test-Path $semPath) { $semText = Get-Content $semPath -Raw -Encoding UTF8 }

    $catPascal = Get-CategoryPascal $ev.uiCategory
    $plugin = Get-SafeName $ev.originalPlugin
    $fileName = $catPascal + '-' + $plugin + '.html'
    if ($usedNames.ContainsKey($fileName.ToLower())) {
        $fileName = $catPascal + '-' + $plugin + '-' + (Get-SafeName $toolId) + '.html'
    }
    $usedNames[$fileName.ToLower()] = $true

    $null = New-Page -ToolId $toolId -Evidence $ev -UiPreview $ui -Resources $res -SemanticsText $semText -PageFileName $fileName

    $cardFiles = [ordered]@{
        '逆向记录' = $toolId + '-reverse.md'
        '证据索引' = 'evidence.json'
        '参数全量' = 'parameters.json'
        '资源清单' = 'resources.json'
        'UI 描述'  = 'ui-preview.json'
        '布局图'   = 'ui-preview.svg'
        '算法语义' = 'algorithm-semantics.md'
    }
    $present = @()
    foreach ($k in $cardFiles.Keys) {
        if (Test-Path (Join-Path $dir.FullName $cardFiles[$k])) { $present += $k }
    }

    $pvInfo = Get-PreviewInfo -ToolId $toolId -Evidence $ev
    $generated.Add([pscustomobject]@{
        toolId = $toolId
        displayName = $ev.displayName
        page = $fileName
        status = $ev.status
        uiCategory = $ev.uiCategory
        plugin = $plugin
        hasSvg = ($present -contains '布局图')
        previewLevel = $pvInfo.level
        previewFile = $pvInfo.file
        cardFiles = $cardFiles
        present = $present
    })
}

# 总览索引页
$indexHtml = New-Object System.Text.StringBuilder
$statusBuckets = $generated | Group-Object status | Sort-Object Name
[void]$indexHtml.AppendLine('<!DOCTYPE html>')
[void]$indexHtml.AppendLine('<html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">')
[void]$indexHtml.AppendLine('<title>工具逆向证据库 · 索引</title><style>' + $css + '</style></head><body><div class="wrap">')
[void]$indexHtml.AppendLine('<header class="top"><h1>工具逆向证据库<span class="badge source-partial">' + $generated.Count + ' 张卡</span></h1>')
[void]$indexHtml.AppendLine('<p class="meta">每个工具一卡七件套：逆向记录、证据索引、参数全量表、资源清单、UI 预览描述、布局图、9 步算法语义链</p>')
[void]$indexHtml.AppendLine('<p class="notice">本库只记录「看到了什么、在哪看到的、还有什么没看到」，不证明任何工具在原厂已注册、加载、执行或完成。</p>')
[void]$indexHtml.AppendLine('<p class="offline">纯本地静态页：直接双击本文件用 <code>file://</code> 打开即可，不需要启动任何本地服务。</p>')
[void]$indexHtml.AppendLine('</header><main>')
[void]$indexHtml.AppendLine('<section class="card"><h2>两列怎么用</h2><table><thead><tr><th style="width:12%">列</th><th style="width:44%">指向什么</th><th>说明</th></tr></thead><tbody>')
[void]$indexHtml.AppendLine('<tr><td><b>预览</b></td><td><code class="mono">docs/08-工具预览/preview/&lt;分类&gt;-&lt;模块&gt;.html</code></td><td>按原件 <code class="mono">AlgorithmTabRoot</code> 的控件声明还原的调试窗：页签、控件形态、声明顺序、参数默认值。标「控件级」= 已升级；标「外壳待升级」= 还是三栏壳；显示「无预览」表示没有预览文件，不给可点击入口。</td></tr>')
[void]$indexHtml.AppendLine('<tr><td><b>证据</b></td><td>该工具七件套（逆向记录 / 证据索引 / 参数全量 / 资源清单 / UI 描述 / 布局图 / 算法语义）</td><td>每项为卡片目录下的真实文件，缺哪项就不显示哪项。</td></tr>')
[void]$indexHtml.AppendLine('</tbody></table>')
[void]$indexHtml.AppendLine('<p class="meta" style="margin-top:12px">分工：本库出证据与对照（七件套 + 7 环节 / 9 步 / 7 维），<code class="mono">docs/08-工具预览/</code> 出控件级界面还原，两边页面互相链接。界面还原不能替代源码证据，反之亦然。</p>')
[void]$indexHtml.AppendLine('</section>')
[void]$indexHtml.AppendLine('<section class="card"><h2>按状态筛选</h2><table><thead><tr><th>状态</th><th>数量</th><th>含义</th></tr></thead><tbody>')
$statusMeaning = @{
    'inventory-only' = '仅有清单，无源码证据'
    'source-indexed' = '源码已定位，未逐项解读'
    'source-partial' = '源码部分恢复，部分环节有证据'
    'source-confirmed' = '环节级有反编译源码直接支撑'
}
foreach ($b in $statusBuckets) {
    $meaning = ''
    if ($statusMeaning.ContainsKey($b.Name)) { $meaning = $statusMeaning[$b.Name] }
    [void]$indexHtml.AppendLine('<tr><td><span class="badge ' + (Encode-Html $b.Name) + '">' + (Encode-Html (Get-BadgeText $b.Name)) + '</span></td><td>' + $b.Count + '</td><td class="empty">' + (Encode-Html $meaning) + '</td></tr>')
}
[void]$indexHtml.AppendLine('</tbody></table></section>')
[void]$indexHtml.AppendLine('<section class="card"><h2>全部工具（' + $generated.Count + '）</h2><div style="margin:0 0 10px"><input id="q" type="search" placeholder="搜索工具名 / 模块 ID / 分类…" style="width:320px;height:32px;border:1px solid #cfd3d8;border-radius:6px;padding:0 10px;font:13px &quot;Microsoft YaHei&quot;,sans-serif"><span id="qhint" style="margin-left:10px;font-size:12px;color:#6b7280"></span></div><table id="tooltable"><thead><tr><th style="width:18%">工具</th><th style="width:15%">原件模块</th><th style="width:9%">分类</th><th style="width:12%">状态</th><th style="width:7%">对照页</th><th style="width:8%">预览</th><th>证据</th></tr></thead><tbody>')
# 原件参数面板还原页映射：08-工具预览/ui/<分类>-<模块>.html -> 模块名(小写) -> 文件名
$uiMap = @{}
$uiDir = Join-Path (Split-Path $root -Parent) '08-工具预览\ui'
if (Test-Path -LiteralPath $uiDir) {
    foreach ($uf in (Get-ChildItem -LiteralPath $uiDir -File -Filter '*.html')) {
        if ($uf.BaseName -eq 'index') { continue }
        $dash = $uf.BaseName.IndexOf('-')
        if ($dash -gt 0) {
            $modKey = $uf.BaseName.Substring($dash + 1).ToLower()
            if (-not $uiMap.ContainsKey($modKey)) { $uiMap[$modKey] = $uf.Name }
        }
    }
}

foreach ($g in ($generated | Sort-Object uiCategory, displayName)) {
    # 预览列优先级：原件参数面板还原（08-工具预览/ui）> 调试窗控件级（preview）> 外壳待升级 > 无预览
    $previewCell = '<span class="empty">无预览</span>'
    $uiFile = ''
    if ($g.plugin) {
        $pkey = ([string]$g.plugin).ToLower()
        if ($uiMap.ContainsKey($pkey)) { $uiFile = $uiMap[$pkey] }
    }
    if ($uiFile) {
        $previewCell = '<a href="../08-工具预览/ui/' + (Encode-Html $uiFile) + '" style="font-weight:600">原件还原</a>'
    }
    elseif ($g.previewLevel -eq 'control') {
        $previewCell = '<a href="../08-工具预览/preview/' + (Encode-Html $g.previewFile) + '" style="color:#8a8f98">结构草图</a>'
    }
    elseif ($g.previewLevel -eq 'shell') {
        $previewCell = '<a href="../08-工具预览/preview/' + (Encode-Html $g.previewFile) + '" style="color:#8a8f98">外壳待升级</a>'
    }
    $linkParts = @()
    foreach ($k in $g.cardFiles.Keys) {
        if ($g.present -contains $k) {
            # 证据卡统一链 .html（generate-file-cards.ps1 生成），file:// 下可直读；
            # .svg 布局图与 .json/.md 保持原文件但优先用已生成的 .html 包装页
            $cardFile = [string]$g.cardFiles[$k]
            $cardStem = [System.IO.Path]::GetFileNameWithoutExtension($cardFile)
            $cardHtml = $cardStem + '.html'
            $cardHref = 'tools/' + (Encode-Html $g.toolId) + '/' + (Encode-Html $cardFile)
            if ($cardFile -ne 'ui-preview.svg' -and (Test-Path (Join-Path $dir.FullName $cardHtml))) {
                $cardHref = 'tools/' + (Encode-Html $g.toolId) + '/' + (Encode-Html $cardHtml)
            }
            $linkParts += '<a href="' + $cardHref + '">' + (Encode-Html $k) + '</a>'
        }
    }
    $evidenceCell = '<span class="empty">无</span>'
    if ($linkParts.Count -gt 0) { $evidenceCell = ($linkParts -join '<span class="sep">｜</span>') }
    [void]$indexHtml.AppendLine('<tr><td>' + (Encode-Html $g.displayName) + '</td><td><code class="mono">' + (Encode-Html $g.toolId) + '</code></td><td>' + (Encode-Html $g.uiCategory) + '</td><td><span class="badge ' + (Encode-Html $g.status) + '">' + (Encode-Html (Get-BadgeText $g.status)) + '</span></td><td><a href="pages/' + (Encode-Html $g.page) + '">打开</a></td><td>' + $previewCell + '</td><td class="links">' + $evidenceCell + '</td></tr>')
}
[void]$indexHtml.AppendLine('</tbody></table></section>')
[void]$indexHtml.AppendLine('<section class="card"><h2>附属入口</h2><ul class="plain">')
[void]$indexHtml.AppendLine('<li><a href="_reference-codex/site/index.html">Codex 原站（只读参考，描述 VisionReplica 状态，非我方）</a></li>')
[void]$indexHtml.AppendLine('<li><a href="../collaboration/evidence/">证据目录</a></li>')
[void]$indexHtml.AppendLine('<li><a href="../08-工具预览/index.html">工具调试窗预览（按原件参数声明还原，与预览列口径不同）</a></li>')
[void]$indexHtml.AppendLine('<li><a href="../04-工具设计/架构对照/2026/10/21/archive/架构站点-20261021/自研架构文档维护/优化方案/references/vm-blob-ui-target-20261027.html">Blob 分析像素级手工还原（内嵌原厂图标与截图，6.3 MB；样式基准）</a></li>')
[void]$indexHtml.AppendLine('<li><code class="mono">contracts/card.schema.json</code> 卡片字段契约 ｜ <code class="mono">contracts/page.sections.json</code> 页面章节契约</li>')
[void]$indexHtml.AppendLine('</ul></section>')
[void]$indexHtml.AppendLine('</main><footer>索引由 <code class="mono">generate-pages.ps1</code> 生成，生成时间 ' + (Get-Date -Format 'yyyy-MM-dd HH:mm') + '。修改请改卡片。</footer>')
# 搜索：纯前端过滤，file:// 直接打开可用，不需要本地服务
[void]$indexHtml.AppendLine('<script>')
[void]$indexHtml.AppendLine('(function(){var q=document.getElementById("q"),h=document.getElementById("qhint"),t=document.getElementById("tooltable");if(!q||!t)return;')
[void]$indexHtml.AppendLine('var rows=t.querySelectorAll("tbody tr");')
[void]$indexHtml.AppendLine('q.addEventListener("input",function(){var v=this.value.trim().toLowerCase(),shown=0;')
[void]$indexHtml.AppendLine('rows.forEach(function(tr){var hit=!v||tr.textContent.toLowerCase().indexOf(v)>=0;tr.style.display=hit?"":"none";if(hit)shown++});')
[void]$indexHtml.AppendLine('h.textContent=v?("命中 "+shown+" / "+rows.length):""});')
[void]$indexHtml.AppendLine('})();')
[void]$indexHtml.AppendLine('</script>')
[void]$indexHtml.AppendLine('</div></body></html>')
if ($OnlyTool) {
    # 总览页必须由全量卡片生成。单卡模式只写单页，否则会把索引缩成一张卡。
    Write-Host '-OnlyTool 模式：跳过总览页重写，索引须用全量生成' -ForegroundColor Yellow
}
else {
    [System.IO.File]::WriteAllText((Join-Path $root 'index.html'), $indexHtml.ToString(), $utf8)
}

# ---- 证据目录索引（file:// 下目录链接需要落地页）----
$evidenceDir = Join-Path (Split-Path $root -Parent) 'collaboration\evidence'
if (Test-Path -LiteralPath $evidenceDir) {
    $evSb = New-Object System.Text.StringBuilder
    [void]$evSb.AppendLine('<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>证据目录</title><style>*{box-sizing:border-box}body{margin:0;background:#f6f7f9;color:#1d2129;font:14px/1.7 -apple-system,"Segoe UI","Microsoft YaHei",sans-serif}.wrap{max-width:960px;margin:0 auto;padding:24px 20px 60px}.card{background:#fff;border:1px solid #e4e7ec;border-radius:10px;padding:20px 24px}h1{font-size:19px;margin:0 0 12px}li{padding:6px 0;border-bottom:1px solid #f0f2f5;list-style:none}ul{padding:0;margin:0}a{color:#2563eb;text-decoration:none}.n{color:#8a8f98;font-size:12px;margin-left:8px}</style></head><body><div class="wrap"><div class="card"><h1>证据目录</h1><ul>')
    foreach ($d in (Get-ChildItem -LiteralPath $evidenceDir -Directory | Sort-Object Name)) {
        $cnt = (Get-ChildItem -LiteralPath $d.FullName -File -ErrorAction SilentlyContinue | Measure-Object).Count
        [void]$evSb.AppendLine('<li><a href="' + (Encode-Html $d.Name) + '/">' + (Encode-Html $d.Name) + '</a><span class="n">' + $cnt + ' 个文件</span></li>')
    }
    [void]$evSb.AppendLine('</ul></div></div></body></html>')
    [System.IO.File]::WriteAllText((Join-Path $evidenceDir 'index.html'), $evSb.ToString(), $utf8)
    Write-Host ("证据目录索引: {0}" -f (Join-Path $evidenceDir 'index.html'))
}

Write-Host ''
Write-Host '================ 页面生成 ================' -ForegroundColor Cyan
Write-Host ("工具页: {0}" -f $generated.Count)
Write-Host ("总览页: {0}" -f (Join-Path $root 'index.html'))
foreach ($b in $statusBuckets) { Write-Host ("  {0,-18} {1}" -f $b.Name, $b.Count) }
Write-Host ''
