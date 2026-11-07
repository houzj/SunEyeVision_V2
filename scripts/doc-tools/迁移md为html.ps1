# 现行文档 md -> html 迁移器
# 用法：powershell -ExecutionPolicy Bypass -File "scripts\doc-tools\迁移md为html.ps1"
#       只转指定文件：-Targets 'docs\09-现行文档\相机与采集.md'
# 作用：把 09-现行文档 与 00-索引 的说明页转成与 assets/doc.css 配套的静态 HTML。
#        标题/表格/列表/引用/代码块/链接按 md 语义逐块转换，mermaid 块只留
#        <!--DIAGRAM:篇名-序号--> 占位，由图稿手工注入（4 种零依赖 CSS 模式）。
# 注意：mermaid 占位被手工替换后重跑本脚本会覆盖手写图稿；如要重跑，先另存图稿。

param(
    [string[]]$Targets
)

$ErrorActionPreference = 'Stop'

$ScriptDir = $PSScriptRoot
$RepoRoot = Split-Path -Path (Split-Path -Path $ScriptDir -Parent) -Parent
$DocsRoot = Join-Path $RepoRoot 'docs'
$CssFull = Join-Path $DocsRoot '00-索引\assets\doc.css'

# 10 篇现行文档：正文里出现「篇名.md」时要同步改成 .html
$TopicNames = @('框架架构总览', '相机与采集', '通讯工具链', '测量与交互', '订阅绑定与ROI几何', '结果渲染与性能', '图像预览器', '脚本工具', '主题与公共控件', '方案与版本管理')
$IndexNames = @('主题台账', '文档索引')

$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Esc {
    param([string]$Text)
    if ($null -eq $Text) { return '' }
    return $Text.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;')
}

# 行内语义：`代码` / **粗体** / [文本](链接)
function Convert-Inline {
    param([string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return '' }
    $mark = [string][char]1
    $t = [regex]::Replace($Text, '<br\s*/?>', $mark)
    $parts = $t -split '`'
    $sb = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $parts.Count; $i++) {
        if (($i % 2) -eq 1) {
            [void]$sb.Append('<code>' + (Esc $parts[$i]) + '</code>')
        }
        else {
            [void]$sb.Append((Esc $parts[$i]))
        }
    }
    # 链接与粗体都可能跨行内代码（[`主题台账.html`](…) / **不用 `lock` 串行**），拼装后统一处理
    $s = [regex]::Replace($sb.ToString(), '\[([^\]]+)\]\(([^)]+)\)', '<a href="$2">$1</a>')
    $s = [regex]::Replace($s, '\*\*([^*]+)\*\*', '<strong>$1</strong>')
    return $s.Replace([string]$mark, '<br>')
}

# 表格行切分：只按反引号之外的 | 切，避免 codeHash|宿主指纹|引用指纹 这类单元格被切坏
function Split-Row {
    param([string]$Line)
    $cells = New-Object System.Collections.ArrayList
    $sb = New-Object System.Text.StringBuilder
    $inCode = $false
    foreach ($ch in $Line.ToCharArray()) {
        if ($ch -eq '`') { $inCode = -not $inCode; [void]$sb.Append($ch); continue }
        if (($ch -eq '|') -and (-not $inCode)) { [void]$cells.Add($sb.ToString()); [void]$sb.Clear(); continue }
        [void]$sb.Append($ch)
    }
    [void]$cells.Add($sb.ToString())
    if ($cells.Count -gt 0 -and $cells[0].Trim() -eq '') { $cells.RemoveAt(0) }
    if ($cells.Count -gt 0 -and $cells[$cells.Count - 1].Trim() -eq '') { $cells.RemoveAt($cells.Count - 1) }
    return , $cells
}

function Convert-Table {
    param([System.Collections.ArrayList]$Rows)
    $out = New-Object System.Collections.ArrayList
    $grid = New-Object System.Collections.ArrayList
    foreach ($r in $Rows) { [void]$grid.Add((Split-Row $r)) }

    $start = 0
    $hasHead = $false
    if ($grid.Count -ge 2) {
        $joined = ($grid[1] -join '')
        if (($joined -match '^[\s:\-]+$') -and $joined.Contains('-')) { $hasHead = $true; $start = 2 }
    }

    [void]$out.Add('        <table>')
    if ($hasHead) {
        [void]$out.Add('            <thead>')
        [void]$out.Add('                <tr>')
        foreach ($c in $grid[0]) { [void]$out.Add('                    <th>' + (Convert-Inline $c.Trim()) + '</th>') }
        [void]$out.Add('                </tr>')
        [void]$out.Add('            </thead>')
    }
    [void]$out.Add('            <tbody>')
    for ($k = $start; $k -lt $grid.Count; $k++) {
        [void]$out.Add('                <tr>')
        foreach ($c in $grid[$k]) { [void]$out.Add('                    <td>' + (Convert-Inline $c.Trim()) + '</td>') }
        [void]$out.Add('                </tr>')
    }
    [void]$out.Add('            </tbody>')
    [void]$out.Add('        </table>')
    return $out
}

function Convert-Blocks {
    param(
        [string[]]$Lines,
        [string]$Slug,
        [ref]$DiagNo
    )
    $out = New-Object System.Collections.ArrayList
    $i = 0
    $n = $Lines.Count

    while ($i -lt $n) {
        $line = $Lines[$i]
        $trim = $line.Trim()

        if ($trim -eq '') { $i++; continue }

        # 二级以下标题
        if ($line -match '^(#{3,6})\s+(.+)$') {
            $level = $Matches[1].Length
            if ($level -gt 4) { $level = 4 }
            [void]$out.Add('        <h' + $level + '>' + (Convert-Inline $Matches[2].Trim()) + '</h' + $level + '>')
            $i++
            continue
        }

        # 代码块 / mermaid 占位
        if ($trim -match '^```') {
            $lang = $trim.Substring(3).Trim()
            $i++
            $buf = New-Object System.Collections.ArrayList
            while ($i -lt $n -and -not ($Lines[$i].Trim() -match '^```')) {
                [void]$buf.Add($Lines[$i])
                $i++
            }
            $i++
            if ($lang -eq 'mermaid') {
                $DiagNo.Value = $DiagNo.Value + 1
                [void]$out.Add('        <!--DIAGRAM:' + $Slug + '-' + $DiagNo.Value + '-->')
            }
            else {
                [void]$out.Add('        <pre><code>' + (Esc ($buf -join "`n")) + '</code></pre>')
            }
            continue
        }

        # 表格
        if ($line -match '^\s*\|') {
            $buf = New-Object System.Collections.ArrayList
            while ($i -lt $n -and $Lines[$i] -match '^\s*\|') {
                [void]$buf.Add($Lines[$i])
                $i++
            }
            foreach ($l in (Convert-Table -Rows $buf)) { [void]$out.Add($l) }
            continue
        }

        # 引用块
        if ($line -match '^\s*>\s?(.*)$') {
            $buf = New-Object System.Collections.ArrayList
            while ($i -lt $n -and $Lines[$i] -match '^\s*>\s?(.*)$') {
                [void]$buf.Add($Matches[1])
                $i++
            }
            [void]$out.Add('        <blockquote>')
            foreach ($p in $buf) {
                if ($p.Trim() -eq '') { continue }
                [void]$out.Add('            <p>' + (Convert-Inline $p.Trim()) + '</p>')
            }
            [void]$out.Add('        </blockquote>')
            continue
        }

        # 分隔线
        if ($trim -match '^-{3,}$') {
            [void]$out.Add('        <hr>')
            $i++
            continue
        }

        # 无序列表（支持二级缩进）
        if ($line -match '^(\s*)-\s+(.*)$') {
            $items = New-Object System.Collections.ArrayList
            while ($i -lt $n -and $Lines[$i] -match '^(\s*)-\s+(.*)$') {
                [void]$items.Add([pscustomobject]@{ Indent = $Matches[1].Length; Text = (Convert-Inline $Matches[2].Trim()) })
                $i++
            }
            [void]$out.Add('        <ul>')
            $k = 0
            while ($k -lt $items.Count) {
                if ($items[$k].Indent -ge 2) {
                    [void]$out.Add('                <li>' + $items[$k].Text + '</li>')
                    $k++
                    continue
                }
                $hasChild = (($k + 1) -lt $items.Count) -and ($items[$k + 1].Indent -ge 2)
                if (-not $hasChild) {
                    [void]$out.Add('            <li>' + $items[$k].Text + '</li>')
                    $k++
                    continue
                }
                [void]$out.Add('            <li>' + $items[$k].Text)
                [void]$out.Add('                <ul>')
                $m = $k + 1
                while (($m -lt $items.Count) -and ($items[$m].Indent -ge 2)) {
                    [void]$out.Add('                    <li>' + $items[$m].Text + '</li>')
                    $m++
                }
                [void]$out.Add('                </ul>')
                [void]$out.Add('            </li>')
                $k = $m
            }
            [void]$out.Add('        </ul>')
            continue
        }

        # 有序列表
        if ($line -match '^\s*\d+\.\s+(.*)$') {
            [void]$out.Add('        <ol>')
            while ($i -lt $n -and $Lines[$i] -match '^\s*\d+\.\s+(.*)$') {
                [void]$out.Add('            <li>' + (Convert-Inline $Matches[1].Trim()) + '</li>')
                $i++
            }
            [void]$out.Add('        </ol>')
            continue
        }

        # 普通段落
        [void]$out.Add('        <p>' + (Convert-Inline $trim) + '</p>')
        $i++
    }
    return $out
}

function Get-RelPath {
    param([string]$FromDir, [string]$ToPath)
    $f = @($FromDir.Replace('/', '\').TrimEnd('\') -split '\\')
    $t = @($ToPath.Replace('/', '\').TrimEnd('\') -split '\\')
    $i = 0
    while (($i -lt $f.Count) -and ($i -lt $t.Count) -and ($f[$i].ToLower() -eq $t[$i].ToLower())) { $i++ }
    $parts = @()
    for ($j = $i; $j -lt $f.Count; $j++) { $parts += '..' }
    for ($j = $i; $j -lt $t.Count; $j++) { $parts += $t[$j] }
    return ($parts -join '/')
}

function Get-BadgeClass {
    param([string]$Status)
    switch ($Status) {
        '现行' { return 'current' }
        '部分未实施' { return 'partial' }
        '部分未验' { return 'unverified' }
        '已废弃' { return 'deprecated' }
        default { return 'partial' }
    }
}

function Convert-OneFile {
    param([string]$MdPath)

    $dir = Split-Path -Path $MdPath -Parent
    $slug = [System.IO.Path]::GetFileNameWithoutExtension($MdPath)
    $outPath = [System.IO.Path]::ChangeExtension($MdPath, '.html')
    $lines = [System.IO.File]::ReadAllLines($MdPath, $Utf8NoBom)
    $n = $lines.Count

    # 一级标题
    $title = $slug
    $i = 0
    while ($i -lt $n) {
        if ($lines[$i] -match '^#\s+(.+)$') { $title = $Matches[1].Trim(); $i++; break }
        $i++
    }

    # 紧随其后的引用块：既可能是页头元信息，也可能是导语（中间允许空行）
    $intro = New-Object System.Collections.ArrayList
    while ($i -lt $n -and $lines[$i].Trim() -eq '') { $i++ }
    while ($i -lt $n -and $lines[$i] -match '^\s*>\s?(.*)$') {
        [void]$intro.Add($Matches[1])
        $i++
    }

    $metaDate = ''
    $metaStatus = ''
    $metaBase = ''
    $metaSource = ''
    $introParas = New-Object System.Collections.ArrayList
    foreach ($l in $intro) {
        if ($l -match '最后更新') {
            foreach ($seg in ($l -split '[｜|]')) {
                if ($seg -match '^\s*最后更新[：:]\s*(\S+)') { $metaDate = $Matches[1].Trim() }
                elseif ($seg -match '^\s*状态[：:]\s*(.+)$') { $metaStatus = $Matches[1].Trim() }
                elseif ($seg -match '^\s*代码基线[：:]\s*(.+)$') { $metaBase = $Matches[1].Trim().Trim('`') }
            }
            continue
        }
        if ($l -match '^\s*合并来源[：:]\s*(.+)$') { $metaSource = $Matches[1].Trim(); continue }
        [void]$introParas.Add($l.Trim())
    }
    if ([string]::IsNullOrEmpty($metaDate)) { $metaDate = (Get-Item $MdPath).LastWriteTime.ToString('yyyy-MM-dd') }

    # 正文：从首个二级标题开始按节切分（代码块里的 ## 是示例文本，不是小节）
    $bodyStart = $n
    $inFence = $false
    for ($k = $i; $k -lt $n; $k++) {
        if ($lines[$k].Trim() -match '^```') { $inFence = -not $inFence; continue }
        if ((-not $inFence) -and ($lines[$k] -match '^##\s')) { $bodyStart = $k; break }
    }

    $sections = New-Object System.Collections.ArrayList
    $curTitle = ''
    $curLines = New-Object System.Collections.ArrayList
    $inFence = $false
    for ($k = $bodyStart; $k -lt $n; $k++) {
        $line = $lines[$k]
        if ($line.Trim() -match '^```') { $inFence = -not $inFence }
        if ((-not $inFence) -and ($line -match '^##\s+(.+)$')) {
            if ($curTitle -ne '') { [void]$sections.Add([pscustomobject]@{ Title = $curTitle; Lines = @($curLines) }) }
            $curTitle = $Matches[1].Trim()
            $curLines = New-Object System.Collections.ArrayList
            continue
        }
        [void]$curLines.Add($line)
    }
    if ($curTitle -ne '') { [void]$sections.Add([pscustomobject]@{ Title = $curTitle; Lines = @($curLines) }) }

    $diagNo = 0
    $body = New-Object System.Collections.ArrayList
    $leadUsed = ($introParas.Count -gt 0)

    foreach ($sec in $sections) {
        $cls = 'card'
        if ((-not $leadUsed) -and ($body.Count -eq 0)) { $cls = 'card lead'; $leadUsed = $true }
        [void]$body.Add('<section class="' + $cls + '">')
        [void]$body.Add('    <h2>' + (Convert-Inline $sec.Title) + '</h2>')
        $blocks = Convert-Blocks -Lines $sec.Lines -Slug $slug -DiagNo ([ref]$diagNo)
        foreach ($b in $blocks) { [void]$body.Add($b) }
        [void]$body.Add('</section>')
        [void]$body.Add('')
    }

    $cssRel = Get-RelPath -FromDir $dir -ToPath $CssFull
    $ledgerRel = Get-RelPath -FromDir $dir -ToPath (Join-Path $DocsRoot '00-索引\主题台账.html')
    $indexRel = Get-RelPath -FromDir $dir -ToPath (Join-Path $DocsRoot '00-索引\文档索引.html')
    $readmeRel = Get-RelPath -FromDir $dir -ToPath (Join-Path $DocsRoot '09-现行文档\README.html')
    $isCurrentDir = ($dir -eq (Join-Path $DocsRoot '09-现行文档'))
    $showReadmeLink = (-not ($isCurrentDir -and $slug -eq 'README'))

    $html = New-Object System.Collections.ArrayList
    [void]$html.Add('<!DOCTYPE html>')
    [void]$html.Add('<html lang="zh-CN">')
    [void]$html.Add('<head>')
    [void]$html.Add('<meta charset="utf-8">')
    [void]$html.Add('<meta name="viewport" content="width=device-width,initial-scale=1">')
    if ($isCurrentDir -and $slug -ne 'README') {
        [void]$html.Add('<title>' + (Esc $title) + ' · 现行文档</title>')
    }
    else {
        [void]$html.Add('<title>' + (Esc $title) + '</title>')
    }
    [void]$html.Add('<link rel="stylesheet" href="' + $cssRel + '">')
    [void]$html.Add('</head>')
    [void]$html.Add('<body>')
    [void]$html.Add('<div class="wrap">')
    [void]$html.Add('')
    [void]$html.Add('<nav class="crumbs">')
    [void]$html.Add('    <a href="' + $ledgerRel + '">主题台账</a>')
    [void]$html.Add('    <a href="' + $indexRel + '">文档索引</a>')
    if ($showReadmeLink) { [void]$html.Add('    <a href="' + $readmeRel + '">现行文档说明</a>') }
    [void]$html.Add('</nav>')
    [void]$html.Add('')
    [void]$html.Add('<header class="doc-header">')
    if ([string]::IsNullOrEmpty($metaStatus)) {
        [void]$html.Add('    <h1>' + (Convert-Inline $title) + '</h1>')
    }
    else {
        [void]$html.Add('    <h1>' + (Convert-Inline $title) + '<span class="badge ' + (Get-BadgeClass $metaStatus) + '">' + (Esc $metaStatus) + '</span></h1>')
    }
    $metaParts = New-Object System.Collections.ArrayList
    [void]$metaParts.Add('最后更新：' + $metaDate)
    if (-not [string]::IsNullOrEmpty($metaStatus)) { [void]$metaParts.Add('状态：' + $metaStatus) }
    if (-not [string]::IsNullOrEmpty($metaBase)) { [void]$metaParts.Add('代码基线：<code>' + (Esc $metaBase) + '</code>') }
    if (-not [string]::IsNullOrEmpty($metaSource)) { [void]$metaParts.Add('合并来源：' + (Esc $metaSource)) }
    [void]$html.Add('    <p class="doc-meta">' + ($metaParts -join '<span class="sep">·</span>') + '</p>')
    [void]$html.Add('</header>')
    [void]$html.Add('')

    if ($introParas.Count -gt 0) {
        [void]$html.Add('<section class="card lead">')
        [void]$html.Add('    <blockquote>')
        foreach ($p in $introParas) { [void]$html.Add('        <p>' + (Convert-Inline $p) + '</p>') }
        [void]$html.Add('    </blockquote>')
        [void]$html.Add('</section>')
        [void]$html.Add('')
    }

    foreach ($b in $body) { [void]$html.Add($b) }

    [void]$html.Add('<nav class="doc-nav">')
    [void]$html.Add('    <a class="primary" href="' + $ledgerRel + '">主题台账</a>')
    [void]$html.Add('    <a href="' + $indexRel + '">文档索引</a>')
    if ($showReadmeLink) { [void]$html.Add('    <a href="' + $readmeRel + '">现行文档说明</a>') }
    [void]$html.Add('</nav>')
    [void]$html.Add('')
    [void]$html.Add('</div>')
    [void]$html.Add('</body>')
    [void]$html.Add('</html>')

    $text = ($html -join "`r`n") + "`r`n"

    # 正文里指向本层的 md 引用同步改 html（历史层 01-06 的 md 链接保持不变）
    foreach ($name in $TopicNames) { $text = $text.Replace($name + '.md', $name + '.html') }
    foreach ($name in $IndexNames) { $text = $text.Replace($name + '.md', $name + '.html') }

    [System.IO.File]::WriteAllText($outPath, $text, $Utf8NoBom)

    $tableCount = ([regex]::Matches($text, '<table>')).Count
    $linkCount = ([regex]::Matches($text, '<a href="../0')).Count
    Write-Output ('转换完成：' + (Split-Path $outPath -Leaf) + ' 节 ' + $sections.Count + ' / 表 ' + $tableCount + ' / 图占位 ' + $diagNo + ' / 历史链接 ' + $linkCount)
}

if (-not $Targets -or $Targets.Count -eq 0) {
    $Targets = @()
    $Targets += (Get-ChildItem -Path (Join-Path $DocsRoot '09-现行文档') -Filter *.md -File | Select-Object -ExpandProperty FullName)
    $Targets += (Join-Path $DocsRoot '00-索引\README.md')
}

foreach ($t in $Targets) {
    $full = $t
    if (-not [System.IO.Path]::IsPathRooted($full)) { $full = Join-Path $RepoRoot $t }
    if (-not (Test-Path $full)) { throw ('找不到源文件：' + $full) }
    Convert-OneFile -MdPath $full
}

Write-Output ('全部完成，共 ' + $Targets.Count + ' 篇。')
