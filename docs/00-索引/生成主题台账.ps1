# 主题台账生成器
# 用法：powershell -ExecutionPolicy Bypass -File "docs\00-索引\生成主题台账.ps1"
# 作用：扫描 01-06 历史层与 07 现行层，按主题规则聚簇，输出「主题 -> 现行文档 / 历史版本数 / 整合状态」。
# 说明：只读文件名与正文首个标题，不读全文；规则的先后顺序即匹配优先级。新增主题时在此追加规则并同步创建现行文档。

$ErrorActionPreference = 'Stop'

$IndexDir = $PSScriptRoot
$DocsRoot = Split-Path -Path $IndexDir -Parent
$OutFile = Join-Path $IndexDir '主题台账.html'
$CurrentDir = Join-Path $DocsRoot '09-现行文档'

$Topics = @(
    @{ Name = '图像源与Blob'; Pattern = '图像源|Blob' },
    @{ Name = '通讯工具链'; Pattern = '通讯|通信|Modbus|协议|报文|OPC|收发|心跳|发送数据|接收数据' },
    @{ Name = '相机与采集'; Pattern = '相机|采集' },
    @{ Name = '脚本工具'; Pattern = '脚本|编辑器|高亮|补全|沙箱|预编译' },
    @{ Name = '订阅绑定与ROI几何'; Pattern = '订阅|绑定|全局变量|快照|参数契约|数据源|解绑' },
    @{ Name = '测量与交互'; Pattern = '测量|圆弧|卡尺|引导|工具栏|几何|Region|ROI|区域|命中|光标|交互' },
    @{ Name = '结果渲染与性能'; Pattern = '渲染|GPU|绘制|视口|缩略图|性能|批处理' },
    @{ Name = '图像预览器'; Pattern = '预览|图像显示|ImageGlass' },
    @{ Name = '主题与公共控件'; Pattern = '主题|控件|弹窗|MessageBox|图标|样式|界面' },
    @{ Name = '路径画布与节点'; Pattern = '画布|连线|节点|路径避让|拖动|集合替换|复制粘贴' },
    @{ Name = '方案与版本管理'; Pattern = '解决方案|版本|发布|保存|加载|持久化|序列化' },
    @{ Name = '框架架构总览'; Pattern = '架构|框架|引擎|执行|分层|插件|SDK|契约|解耦|模块' },
    @{ Name = '测试与验收'; Pattern = '^$' },
    @{ Name = '指南与参考'; Pattern = '^$' },
    @{ Name = '其他实施总结'; Pattern = '^$' },
    @{ Name = '其他方案'; Pattern = '^$' },
    @{ Name = '工具专项'; Pattern = '^$' }
)

$DirFallback = @{
    '01-方案设计' = '其他方案'
    '02-实施总结' = '其他实施总结'
    '03-测试报告' = '测试与验收'
    '04-工具设计' = '工具专项'
    '05-框架架构' = '框架架构总览'
    '06-指南参考' = '指南与参考'
}

$ScanRoots = @('01-方案设计', '02-实施总结', '03-测试报告', '04-工具设计', '05-框架架构', '06-指南参考')

function Get-FirstHeading {
    param([string]$Path)
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    try {
        $lines = [System.IO.File]::ReadAllLines($Path, $utf8)
        foreach ($line in $lines) {
            if ($line -match '^\s{0,3}#\s+(.+)$') { return ($line -replace '^\s*#+\s*', '').Trim() }
        }
    }
    catch {
        return ''
    }
    return ''
}

function Get-TopicName {
    param([string]$Text)
    foreach ($topic in $Topics) {
        if ($Text -match $topic.Pattern) { return $topic.Name }
    }
    return ''
}

$items = New-Object System.Collections.ArrayList

foreach ($scanRoot in $ScanRoots) {
    $rootPath = Join-Path $DocsRoot $scanRoot
    if (-not (Test-Path $rootPath)) { continue }
    $files = Get-ChildItem -Path $rootPath -Recurse -File -Filter *.md
    foreach ($f in $files) {
        $rel = $f.FullName.Substring($DocsRoot.Length + 1).Replace('\', '/')
        if ($rel -match '/archive/') { continue }
        # 目录说明文件不是历史方案，不参与主题聚簇（否则会被算进某个主题并拉高篇数）
        if ($f.BaseName -ieq 'README') { continue }
        $heading = Get-FirstHeading -Path $f.FullName
        $topic = Get-TopicName -Text ($f.BaseName + ' ' + $heading)
        if ($topic -eq '') { $topic = $DirFallback[$scanRoot] }
        # 日期取值优先级：文件名日期 > 路径年月日 > 文件修改时间。
        # 不直接用修改时间兜底，否则搬运/编辑文档会把「编辑时间」当成本文日期。
        $date = ''
        if ($f.Name -match '(\d{4})[-_]?(\d{2})[-_]?(\d{2})') {
            $month = [int]$Matches[2]
            $day = [int]$Matches[3]
            if ($month -ge 1 -and $month -le 12 -and $day -ge 1 -and $day -le 31) {
                $date = $Matches[1] + '-' + $Matches[2] + '-' + $Matches[3]
            }
        }
        if (-not $date -and $rel -match '/(\d{4})/(\d{1,2})/(\d{1,2})/') {
            $date = ('{0}-{1:D2}-{2:D2}' -f $Matches[1], [int]$Matches[2], [int]$Matches[3])
        }
        if (-not $date) { $date = $f.LastWriteTime.ToString('yyyy-MM-dd') }
        [void]$items.Add([pscustomobject]@{ Topic = $topic; Rel = $rel; Date = $date })
    }
}

# 台账产物是 HTML：现行文档层已改为 09-现行文档/*.html，页面由 assets/doc.css 统一样式。
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Encode-Html {
    param([string]$Text)
    if ($null -eq $Text) { return '' }
    return $Text.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;').Replace('"', '&quot;')
}

$integrated = 0
$summary = New-Object System.Collections.ArrayList

foreach ($topic in $Topics) {
    $group = @($items | Where-Object { $_.Topic -eq $topic.Name })
    if ($group.Count -eq 0) { continue }
    $currentRel = '09-现行文档/' + $topic.Name + '.html'
    $hasCurrent = Test-Path (Join-Path $DocsRoot ('09-现行文档\' + $topic.Name + '.html'))
    if ($hasCurrent) { $integrated++ }
    $latest = ($group | Sort-Object -Property Date -Descending | Select-Object -First 1).Date
    $state = '未整合'
    if ($hasCurrent) { $state = '已整合' }
    [void]$summary.Add([pscustomobject]@{
        Name       = $topic.Name
        Current    = $currentRel
        HasCurrent = $hasCurrent
        Count      = $group.Count
        Latest     = $latest
        State      = $state
    })
}

$totalRows = $summary.Count + $items.Count

$h = New-Object System.Collections.ArrayList
[void]$h.Add('<!DOCTYPE html>')
[void]$h.Add('<html lang="zh-CN">')
[void]$h.Add('<head>')
[void]$h.Add('<meta charset="utf-8">')
[void]$h.Add('<meta name="viewport" content="width=device-width,initial-scale=1">')
[void]$h.Add('<title>主题台账</title>')
[void]$h.Add('<link rel="stylesheet" href="assets/doc.css">')
[void]$h.Add('</head>')
[void]$h.Add('<body>')
[void]$h.Add('<div class="wrap">')
[void]$h.Add('')
[void]$h.Add('<nav class="crumbs">')
[void]$h.Add('    <a href="文档索引.html">文档索引</a>')
[void]$h.Add('    <a href="../09-现行文档/README.html">现行文档说明</a>')
[void]$h.Add('</nav>')
[void]$h.Add('')
[void]$h.Add('<header class="doc-header">')
[void]$h.Add('    <h1>主题台账</h1>')
[void]$h.Add('    <p class="doc-meta">由 <code>docs/00-索引/生成主题台账.ps1</code> 自动生成，禁止手改<span class="sep">·</span>重建：<code>powershell -ExecutionPolicy Bypass -File "docs\00-索引\生成主题台账.ps1"</code></p>')
[void]$h.Add('</header>')
[void]$h.Add('')
[void]$h.Add('<div class="stat-row">')
[void]$h.Add('    <div class="stat-box"><span class="num">' + $summary.Count + '</span><span class="label">主题</span></div>')
[void]$h.Add('    <div class="stat-box"><span class="num">' + $integrated + '</span><span class="label">已整合（有现行文档）</span></div>')
[void]$h.Add('    <div class="stat-box"><span class="num">' + $items.Count + '</span><span class="label">历史文档</span></div>')
[void]$h.Add('</div>')
[void]$h.Add('')
[void]$h.Add('<div class="search-bar">')
[void]$h.Add('    <input id="q" type="search" placeholder="输入主题名、日期或文档名过滤（纯本地，不联网）">')
[void]$h.Add('    <span class="result-count">显示 <b id="n">' + $totalRows + '</b> / ' + $totalRows + '</span>')
[void]$h.Add('</div>')
[void]$h.Add('')
[void]$h.Add('<section class="card lead">')
[void]$h.Add('    <h2>怎么用这张表</h2>')
[void]$h.Add('    <p>查一件事「现在是什么」先在本表定位主题与现行文档；历史版本只是演进记录，不等于当前结论。</p>')
[void]$h.Add('    <p><span class="t-ok">已整合</span> = 已有 <code>09-现行文档</code> 单篇权威说明；<span class="t-warn">未整合</span> = 仍只有按日期叠加的历史文档，需要顺着清单自己读。</p>')
[void]$h.Add('</section>')
[void]$h.Add('')
[void]$h.Add('<section class="card">')
[void]$h.Add('    <h2>主题总览（' + $summary.Count + '）</h2>')
[void]$h.Add('    <table class="idx">')
[void]$h.Add('        <thead>')
[void]$h.Add('            <tr><th style="width:20%">主题</th><th style="width:30%">现行文档</th><th>历史版本</th><th>最近日期</th><th>整合状态</th></tr>')
[void]$h.Add('        </thead>')
[void]$h.Add('        <tbody>')
foreach ($row in $summary) {
    if ($row.HasCurrent) {
        $docCell = '<a href="../' + (Encode-Html $row.Current) + '"><code>' + (Encode-Html $row.Current) + '</code></a>'
    }
    else {
        $docCell = '<span class="t-muted">（待整合）</span>'
    }
    $stateCell = '<span class="t-warn">未整合</span>'
    if ($row.HasCurrent) { $stateCell = '<span class="t-ok">已整合</span>' }
    $q = ($row.Name + ' ' + $row.Current + ' ' + $row.Latest + ' ' + $row.State).ToLowerInvariant()
    [void]$h.Add('            <tr data-q="' + (Encode-Html $q) + '">')
    [void]$h.Add('                <td><strong>' + (Encode-Html $row.Name) + '</strong></td>')
    [void]$h.Add('                <td>' + $docCell + '</td>')
    [void]$h.Add('                <td>' + $row.Count + '</td>')
    [void]$h.Add('                <td class="date">' + $row.Latest + '</td>')
    [void]$h.Add('                <td>' + $stateCell + '</td>')
    [void]$h.Add('            </tr>')
}
[void]$h.Add('        </tbody>')
[void]$h.Add('    </table>')
[void]$h.Add('</section>')
[void]$h.Add('')
[void]$h.Add('<section class="card">')
[void]$h.Add('    <h2>各主题历史版本</h2>')
foreach ($row in $summary) {
    $group = @($items | Where-Object { $_.Topic -eq $row.Name } | Sort-Object -Property Date -Descending)
    $stateText = '<span class="t-warn">未整合</span>'
    if ($row.HasCurrent) { $stateText = '<span class="t-ok">已整合</span>' }
    [void]$h.Add('    <div data-block>')
    [void]$h.Add('        <h3>' + (Encode-Html $row.Name) + '（' + $group.Count + ' 篇 · ' + $stateText + '）</h3>')
    [void]$h.Add('        <table class="idx compact">')
    [void]$h.Add('            <thead>')
    [void]$h.Add('                <tr><th style="width:120px">日期</th><th>文档</th></tr>')
    [void]$h.Add('            </thead>')
    [void]$h.Add('            <tbody>')
    foreach ($item in $group) {
        $q = ($item.Date + ' ' + $item.Rel).ToLowerInvariant()
        [void]$h.Add('                <tr data-q="' + (Encode-Html $q) + '">')
        [void]$h.Add('                    <td class="date">' + $item.Date + '</td>')
        [void]$h.Add('                    <td class="path">' + (Encode-Html $item.Rel) + '</td>')
        [void]$h.Add('                </tr>')
    }
    [void]$h.Add('            </tbody>')
    [void]$h.Add('        </table>')
    [void]$h.Add('    </div>')
}

if (-not (Test-Path $CurrentDir)) {
    [void]$h.Add('    <div class="notice">提示：<code>09-现行文档</code> 目录尚未创建，所有主题均处于未整合状态。</div>')
}

[void]$h.Add('</section>')
[void]$h.Add('')
[void]$h.Add('<nav class="doc-nav">')
[void]$h.Add('    <a class="primary" href="文档索引.html">文档索引</a>')
[void]$h.Add('    <a href="../09-现行文档/README.html">现行文档说明</a>')
[void]$h.Add('</nav>')
[void]$h.Add('')
[void]$h.Add('</div>')
[void]$h.Add('<script>')
[void]$h.Add('(function () {')
[void]$h.Add('    var input = document.getElementById(''q'');')
[void]$h.Add('    var counter = document.getElementById(''n'');')
[void]$h.Add('    var rows = Array.prototype.slice.call(document.querySelectorAll(''tr[data-q]''));')
[void]$h.Add('    var blocks = Array.prototype.slice.call(document.querySelectorAll(''[data-block]''));')
[void]$h.Add('    function apply() {')
[void]$h.Add('        var key = input.value.trim().toLowerCase();')
[void]$h.Add('        var shown = 0;')
[void]$h.Add('        for (var i = 0; i < rows.length; i++) {')
[void]$h.Add('            var hit = !key || rows[i].getAttribute(''data-q'').indexOf(key) >= 0;')
[void]$h.Add('            rows[i].style.display = hit ? '''' : ''none'';')
[void]$h.Add('            if (hit) { shown++; }')
[void]$h.Add('        }')
[void]$h.Add('        for (var j = 0; j < blocks.length; j++) {')
[void]$h.Add('            var inside = blocks[j].querySelectorAll(''tr[data-q]'');')
[void]$h.Add('            var visible = 0;')
[void]$h.Add('            for (var k = 0; k < inside.length; k++) {')
[void]$h.Add('                if (inside[k].style.display !== ''none'') { visible++; }')
[void]$h.Add('            }')
[void]$h.Add('            blocks[j].hidden = (visible === 0);')
[void]$h.Add('        }')
[void]$h.Add('        counter.textContent = shown;')
[void]$h.Add('    }')
[void]$h.Add('    input.addEventListener(''input'', apply);')
[void]$h.Add('    apply();')
[void]$h.Add('})();')
[void]$h.Add('</script>')
[void]$h.Add('</body>')
[void]$h.Add('</html>')

[System.IO.File]::WriteAllText($OutFile, ($h -join "`r`n") + "`r`n", $Utf8NoBom)

Write-Output ('主题台账已生成：' + $OutFile)
Write-Output ('主题 ' + $summary.Count + ' 个，已整合 ' + $integrated + ' 个，历史文档 ' + $items.Count + ' 篇。')
