# 文档索引生成器 v2
# 用法：powershell -ExecutionPolicy Bypass -File "docs\00-索引\更新索引.ps1"
# 特性：$PSScriptRoot 定位（不依赖当前工作目录）；分区输出带状态列；覆盖 md/html/json/pptx/zip 等格式；
#       大文件只登记不解析；证据区按子目录汇总；生成后自检条目数，失败即报错。

$ErrorActionPreference = 'Stop'

$IndexDir = $PSScriptRoot
$DocsRoot = Split-Path -Path $IndexDir -Parent
$OutFile = Join-Path $IndexDir '文档索引.html'
$BigBytes = 1MB

$LayerOrder = @('现行文档', '方案设计', '实施总结', '测试报告', '工具设计', '框架架构', '指南参考', '协作记录', '证据', '其他')

function Get-LayerInfo {
    param([string]$RelPath)
    $p = $RelPath.Replace('\', '/')
    $layer = '其他'
    $status = '历史'
    if ($p -like '09-现行文档/*') { $layer = '现行文档'; $status = '现行' }
    elseif ($p -like '01-方案设计/*') { $layer = '方案设计' }
    elseif ($p -like '02-实施总结/*') { $layer = '实施总结' }
    elseif ($p -like '03-测试报告/*') { $layer = '测试报告' }
    elseif ($p -like '04-工具设计/*') { $layer = '工具设计' }
    elseif ($p -like '05-框架架构/*') { $layer = '框架架构' }
    elseif ($p -like '06-指南参考/*') { $layer = '指南参考' }
    elseif ($p -like 'collaboration/evidence/*') { $layer = '证据'; $status = '证据' }
    elseif ($p -like 'collaboration/*') { $layer = '协作记录' }
    if ($p -match '/archive/' -and $layer -ne '证据') { $status = '归档' }
    return @{ Layer = $layer; Status = $status }
}

function Get-Summary {
    param([System.IO.FileInfo]$File)
    if ($File.Length -gt $BigBytes) { return '大文件，仅登记路径' }
    $ext = $File.Extension.ToLowerInvariant()
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    try {
        if ($ext -eq '.md' -or $ext -eq '.txt') {
            $lines = [System.IO.File]::ReadAllLines($File.FullName, $utf8)
            foreach ($line in $lines) {
                if ($line -match '^\s{0,3}#\s+(.+)$') { return ($line -replace '^\s*#+\s*', '').Trim() }
            }
            return '-'
        }
        if ($ext -eq '.html' -or $ext -eq '.htm') {
            $text = [System.IO.File]::ReadAllText($File.FullName, $utf8)
            if ($text -match '<title>\s*([^<]+?)\s*</title>') { return $Matches[1].Trim() }
            return '-'
        }
    }
    catch {
        return '读取失败'
    }
    return '-'
}

function Get-CreatedDate {
    param([System.IO.FileInfo]$File)
    if ($File.Name -match '(\d{4})[-_]?(\d{2})[-_]?(\d{2})') {
        return ($Matches[1] + '-' + $Matches[2] + '-' + $Matches[3])
    }
    return $File.LastWriteTime.ToString('yyyy-MM-dd')
}

$evidenceRoot = Join-Path $DocsRoot 'collaboration\evidence'
$evidenceNames = @()
if (Test-Path $evidenceRoot) {
    $evidenceNames = @(Get-ChildItem -Path $evidenceRoot -Directory | Select-Object -ExpandProperty Name)
}

$allFiles = @(Get-ChildItem -Path $DocsRoot -Recurse -File | Where-Object { $_.FullName -notlike ($IndexDir + '\*') })

$rows = New-Object System.Collections.ArrayList
$evidenceStat = @{}
$skippedEvidence = 0
$fileRows = 0

foreach ($f in $allFiles) {
    $rel = $f.FullName.Substring($DocsRoot.Length + 1)
    $relU = $rel.Replace('\', '/')

    if ($relU.StartsWith('collaboration/evidence/')) {
        $skippedEvidence++
        $parts = $relU.Split('/')
        $key = '(根目录散文件)'
        if ($parts.Count -gt 3) { $key = $parts[2] }
        if (-not $evidenceStat.ContainsKey($key)) { $evidenceStat[$key] = @{ Count = 0; Bytes = 0; Last = $f.LastWriteTime } }
        $evidenceStat[$key].Count++
        $evidenceStat[$key].Bytes += $f.Length
        if ($f.LastWriteTime -gt $evidenceStat[$key].Last) { $evidenceStat[$key].Last = $f.LastWriteTime }
        continue
    }

    $info = Get-LayerInfo -RelPath $relU
    $ext = $f.Extension.ToLowerInvariant()
    if ($ext.StartsWith('.')) { $ext = $ext.Substring(1) }
    if ($ext -eq '') { $ext = '无扩展名' }

    [void]$rows.Add([pscustomobject]@{
        Rel     = $relU
        Layer   = $info.Layer
        Status  = $info.Status
        Format  = $ext
        Created = Get-CreatedDate -File $f
        Updated = $f.LastWriteTime.ToString('yyyy-MM-dd')
        SizeKB  = [int][math]::Round($f.Length / 1KB, 0)
        Summary = Get-Summary -File $f
    })
    $fileRows++
}

foreach ($name in ($evidenceStat.Keys | Sort-Object)) {
    $stat = $evidenceStat[$name]
    if ($null -eq $stat) { continue }
    $relEvidence = 'collaboration/evidence/' + $name + '/'
    if ($name -eq '(根目录散文件)') { $relEvidence = 'collaboration/evidence/' }
    [void]$rows.Add([pscustomobject]@{
        Rel     = $relEvidence
        Layer   = '证据'
        Status  = '证据'
        Format  = '目录'
        Created = '-'
        Updated = $stat.Last.ToString('yyyy-MM-dd')
        SizeKB  = [int][math]::Round($stat.Bytes / 1KB, 0)
        Summary = ('证据目录，' + $stat.Count + ' 个文件，明细不入表')
    })
}

$expected = $allFiles.Count - $skippedEvidence
if ($fileRows -ne $expected) {
    throw ('索引条目数与文件数不一致：已登记 ' + $fileRows + '，应为 ' + $expected + '（证据区目录汇总 ' + $evidenceNames.Count + ' 行）')
}

function Encode-Html {
    param([string]$Text)
    if ($null -eq $Text) { return '' }
    return $Text.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;').Replace('"', '&quot;')
}

$countCurrent = @($rows | Where-Object { $_.Status -eq '现行' }).Count
$countArchive = @($rows | Where-Object { $_.Status -eq '归档' }).Count
$countEvidence = @($rows | Where-Object { $_.Status -eq '证据' }).Count

$h = New-Object System.Collections.ArrayList
[void]$h.Add('<!DOCTYPE html>')
[void]$h.Add('<html lang="zh-CN">')
[void]$h.Add('<head>')
[void]$h.Add('<meta charset="utf-8">')
[void]$h.Add('<meta name="viewport" content="width=device-width,initial-scale=1">')
[void]$h.Add('<title>文档索引</title>')
[void]$h.Add('<link rel="stylesheet" href="assets/doc.css">')
[void]$h.Add('</head>')
[void]$h.Add('<body>')
[void]$h.Add('<div class="wrap">')
[void]$h.Add('')
[void]$h.Add('<nav class="crumbs">')
[void]$h.Add('    <a href="主题台账.html">主题台账</a>')
[void]$h.Add('    <a href="../09-现行文档/README.html">现行文档说明</a>')
[void]$h.Add('</nav>')
[void]$h.Add('')
[void]$h.Add('<header class="doc-header">')
[void]$h.Add('    <h1>文档索引</h1>')
[void]$h.Add('    <p class="doc-meta">由 <code>docs/00-索引/更新索引.ps1</code> 自动生成，禁止手改<span class="sep">·</span>重建：<code>powershell -ExecutionPolicy Bypass -File "docs\00-索引\更新索引.ps1"</code></p>')
[void]$h.Add('</header>')
[void]$h.Add('')
[void]$h.Add('<div class="stat-row">')
[void]$h.Add('    <div class="stat-box"><span class="num">' + $rows.Count + '</span><span class="label">索引条目</span></div>')
[void]$h.Add('    <div class="stat-box"><span class="num">' + $countCurrent + '</span><span class="label">现行（唯一真相）</span></div>')
[void]$h.Add('    <div class="stat-box"><span class="num">' + $countArchive + '</span><span class="label">归档批次</span></div>')
[void]$h.Add('    <div class="stat-box"><span class="num">' + $countEvidence + '</span><span class="label">证据目录（汇总）</span></div>')
[void]$h.Add('</div>')
[void]$h.Add('')
[void]$h.Add('<div class="search-bar">')
[void]$h.Add('    <input id="q" type="search" placeholder="输入路径、日期或摘要关键词过滤（纯本地，不联网）">')
[void]$h.Add('    <span class="result-count">显示 <b id="n">' + $rows.Count + '</b> / ' + $rows.Count + '</span>')
[void]$h.Add('</div>')
[void]$h.Add('')
[void]$h.Add('<section class="card lead">')
[void]$h.Add('    <h2>怎么用这张表</h2>')
[void]$h.Add('    <p>找文档的顺序：先看 <code>主题台账.html</code> 定位主题与现行文档，再用本表查具体文件。</p>')
[void]$h.Add('    <p>状态含义：<span class="t-ok">现行</span> = 唯一真相<span class="sep">·</span>历史 = 演进记录<span class="sep">·</span><span class="t-muted">证据</span> = 取证材料<span class="sep">·</span>归档 = 已归档批次。列含义：创建日期取自文件名，缺失时用最后更新；摘要对 md 取正文首标题、对 html 取 <code>title</code>。</p>')
[void]$h.Add('    <p>扫描到 <strong>' + $allFiles.Count + '</strong> 个文件，其中证据区 <strong>' + $skippedEvidence + '</strong> 个按目录汇总为 <strong>' + $evidenceNames.Count + '</strong> 行，不逐个入表。</p>')
[void]$h.Add('</section>')
[void]$h.Add('')

$layerCount = 0
foreach ($layer in $LayerOrder) {
    $group = @($rows | Where-Object { $_.Layer -eq $layer } | Sort-Object -Property Rel)
    if ($group.Count -eq 0) { continue }
    $layerCount++
    [void]$h.Add('<section class="card" data-block>')
    [void]$h.Add('    <h2>' + (Encode-Html $layer) + '（' + $group.Count + '）</h2>')
    [void]$h.Add('    <table class="idx">')
    [void]$h.Add('        <thead>')
    [void]$h.Add('            <tr><th style="width:34%">文档路径</th><th>状态</th><th>格式</th><th>创建日期</th><th>最后更新</th><th>体积</th><th style="width:24%">摘要</th></tr>')
    [void]$h.Add('        </thead>')
    [void]$h.Add('        <tbody>')
    foreach ($row in $group) {
        $summary = $row.Summary
        if ($null -eq $summary) { $summary = '-' }
        $summary = $summary.Replace("`r", ' ').Replace("`n", ' ')
        $statusText = (Encode-Html $row.Status)
        if ($row.Status -eq '现行') { $statusText = '<span class="t-ok">现行</span>' }
        elseif ($row.Status -eq '证据') { $statusText = '<span class="t-muted">证据</span>' }
        elseif ($row.Status -eq '归档') { $statusText = '<span class="t-muted">归档</span>' }
        $q = ($row.Rel + ' ' + $row.Status + ' ' + $row.Format + ' ' + $row.Created + ' ' + $row.Updated + ' ' + $summary + ' ' + $layer).ToLowerInvariant()
        # 每条记录压成一行：产物可读、git diff 以行为单位，不因逐格换行放大体积
        $cells = '<td class="path">' + (Encode-Html $row.Rel) + '</td>' +
                 '<td>' + $statusText + '</td>' +
                 '<td>' + (Encode-Html $row.Format) + '</td>' +
                 '<td class="date">' + $row.Created + '</td>' +
                 '<td class="date">' + $row.Updated + '</td>' +
                 '<td class="date">' + $row.SizeKB + ' KB</td>' +
                 '<td class="summary">' + (Encode-Html $summary) + '</td>'
        [void]$h.Add('            <tr data-q="' + (Encode-Html $q) + '">' + $cells + '</tr>')
    }
    [void]$h.Add('        </tbody>')
    [void]$h.Add('    </table>')
    [void]$h.Add('</section>')
    [void]$h.Add('')
}

[void]$h.Add('<nav class="doc-nav">')
[void]$h.Add('    <a class="primary" href="主题台账.html">主题台账</a>')
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

$html = ($h -join "`r`n") + "`r`n"

# 自检：数据行数必须与登记条目数一致，标题行数必须等于有内容的分区数
$dataRows = ([regex]::Matches($html, '<tr data-q=')).Count
if ($dataRows -ne $rows.Count) {
    throw ('索引产物行数不一致：HTML 数据行 ' + $dataRows + '，登记条目 ' + $rows.Count)
}
$headRows = ([regex]::Matches($html, '<tr>')).Count
if ($headRows -ne $layerCount) {
    throw ('索引产物表头行数不一致：表头 <tr> 共 ' + $headRows + '，应为 ' + $layerCount + '（每分区一行）')
}

[System.IO.File]::WriteAllText($OutFile, $html, (New-Object System.Text.UTF8Encoding($false)))

Write-Output ('索引已生成：' + $OutFile)
Write-Output ('条目 ' + $rows.Count + ' 条，扫描文件 ' + $allFiles.Count + ' 个，证据汇总 ' + $skippedEvidence + ' 个文件 / ' + $evidenceNames.Count + ' 个目录。')
Write-Output ('自检通过：数据行 ' + $dataRows + ' 条，分区 ' + $layerCount + ' 个。')
