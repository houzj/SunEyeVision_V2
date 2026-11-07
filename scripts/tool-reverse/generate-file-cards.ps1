#requires -Version 5.1
<#
.SYNOPSIS
    Convert tool-card .md and .json evidence files to standalone .html pages
    so the whole library works under file:// without any local server.

.DESCRIPTION
    For every tool directory under docs/07-*/tools/<id>/:
      *.md                  -> same-name .html (rendered markdown)
      evidence.json         -> evidence.html
      parameters.json       -> parameters.html
      resources.json        -> resources.html
      ui-preview.json       -> ui-preview.html

    Source files are kept untouched; .html is generated next to them.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/tool-reverse/generate-file-cards.ps1
#>
[CmdletBinding()]
param(
    [string]$Root,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$utf8 = [System.Text.UTF8Encoding]::new($false)

function Escape-Html {
    param([string]$s)
    if ($null -eq $s) { return '' }
    $s = $s -replace '&', '&amp;'
    $s = $s -replace '<', '&lt;'
    $s = $s -replace '>', '&gt;'
    $s = $s -replace '"', '&quot;'
    return $s
}

function Convert-Inline {
    param([string]$s)
    $s = Escape-Html $s
    $s = [regex]::Replace($s, '`([^`]+)`', '<code>$1</code>')
    $s = [regex]::Replace($s, '\*\*([^*]+)\*\*', '<b>$1</b>')
    $s = [regex]::Replace($s, '\[([^\]]+)\]\(([^)]+)\)', '<a href="$2">$1</a>')
    return $s
}

function Convert-MdBody {
    param([string]$md)
    $lines = $md -split "`r?`n"
    $out = New-Object System.Text.StringBuilder
    $inCode = $false
    $codeLines = New-Object System.Collections.Generic.List[string]
    $inUl = $false
    $inTable = $false

    function Close-List {
        if ($script:inUl) { [void]$script:out.Append("</ul>`n"); $script:inUl = $false }
    }
    function Close-Table {
        if ($script:inTable) { [void]$script:out.Append("</tbody></table>`n"); $script:inTable = $false }
    }

    $i = 0
    while ($i -lt $lines.Count) {
        $line = $lines[$i]

        if ($line -match '^\s*```') {
            if ($inCode) {
                Close-List; Close-Table
                [void]$out.Append('<pre>' + (Escape-Html ($codeLines -join "`n")) + '</pre>')
                $inCode = $false; $codeLines.Clear()
            } else {
                Close-List; Close-Table
                $inCode = $true
            }
            $i++; continue
        }
        if ($inCode) { $codeLines.Add($line); $i++; continue }

        if ($line -match '^(#{1,6})\s+(.*)$') {
            Close-List; Close-Table
            $lvl = $matches[1].Length
            $text = Convert-Inline $matches[2]
            [void]$out.Append("<h$lvl>$text</h$lvl>")
            $i++; continue
        }

        if ($line -match '^\s*\|(.+)\|\s*$') {
            Close-List
            $cells = $matches[1] -split '\|'
            $isSep = $true
            foreach ($c in $cells) { if ($c.Trim() -notmatch '^:?-{2,}:?$') { $isSep = $false; break } }
            if ($isSep) { $i++; continue }
            if (-not $inTable) {
                [void]$out.Append('<table><tbody>')
                $inTable = $true
            }
            [void]$out.Append('<tr>')
            foreach ($c in $cells) { [void]$out.Append('<td>' + (Convert-Inline ($c.Trim())) + '</td>') }
            [void]$out.Append('</tr>')
            $i++; continue
        }
        Close-Table

        if ($line -match '^\s*[-*]\s+(.*)$') {
            Close-Table
            if (-not $inUl) { [void]$out.Append('<ul>'); $inUl = $true }
            [void]$out.Append('<li>' + (Convert-Inline $matches[1]) + '</li>')
            $i++; continue
        }
        if ($line -match '^\s*\d+\.\s+(.*)$') {
            Close-Table
            if (-not $inUl) { [void]$out.Append('<ul>'); $inUl = $true }
            [void]$out.Append('<li>' + (Convert-Inline $matches[1]) + '</li>')
            $i++; continue
        }
        Close-List

        if ($line.Trim() -eq '') { $i++; continue }

        if ($line -match '^>\s?(.*)$') {
            [void]$out.Append('<blockquote>' + (Convert-Inline $matches[1]) + '</blockquote>')
            $i++; continue
        }

        if ($line -match '^---+\s*$') { [void]$out.Append('<hr>'); $i++; continue }

        # paragraph: merge until blank line
        $para = New-Object System.Collections.Generic.List[string]
        while ($i -lt $lines.Count -and $lines[$i].Trim() -ne '') {
            if ($lines[$i] -match '^\s*```' -or $lines[$i] -match '^#{1,6}\s' -or $lines[$i] -match '^\s*\|' -or $lines[$i] -match '^\s*[-*]\s' -or $lines[$i] -match '^>\s') { break }
            $para.Add($lines[$i]); $i++
        }
        if ($para.Count -gt 0) {
            [void]$out.Append('<p>' + ((($para | ForEach-Object { Convert-Inline $_ }) -join '<br>')) + '</p>')
        } else {
            $i++
        }
    }
    Close-List; Close-Table
    if ($inCode) { [void]$out.Append('<pre>' + (Escape-Html ($codeLines -join "`n")) + '</pre>') }
    return $out.ToString()
}

function New-CardHtml {
    param([string]$Title, [string]$BodyHtml, [string]$CrumbsHtml)
    $style = @'
*{box-sizing:border-box}
body{margin:0;background:#f6f7f9;color:#1d2129;font:14px/1.7 -apple-system,"Segoe UI","Microsoft YaHei",sans-serif}
.wrap{max-width:960px;margin:0 auto;padding:24px 20px 60px}
.crumbs{font-size:13px;margin-bottom:14px}
.crumbs a{color:#2563eb;text-decoration:none}
.card{background:#fff;border:1px solid #e4e7ec;border-radius:10px;padding:26px 30px}
h1,h2,h3,h4{line-height:1.4}
h1{font-size:20px}h2{font-size:17px;margin-top:26px}h3{font-size:15px}
table{border-collapse:collapse;margin:12px 0;width:100%;font-size:13px}
td,th{border:1px solid #e4e7ec;padding:6px 10px;vertical-align:top}
tr:nth-child(even) td{background:#fafbfc}
code{background:#f1f3f5;border-radius:3px;padding:1px 5px;font-size:12.5px;font-family:Consolas,Menlo,monospace;color:#c7254e}
pre{background:#282c34;color:#abb2bf;border-radius:6px;padding:14px 16px;overflow:auto;font-size:12.5px;line-height:1.6;font-family:Consolas,Menlo,monospace}
pre code{background:none;color:inherit;padding:0}
ul{padding-left:22px}
blockquote{border-left:3px solid #d0d5da;margin:10px 0;padding:4px 14px;color:#5a636b;background:#fafbfc}
hr{border:0;border-top:1px solid #e4e7ec;margin:18px 0}
p{margin:8px 0}
'@
    return "<!doctype html>`n<html lang=`"zh-CN`"><head><meta charset=`"utf-8`"><meta name=`"viewport`" content=`"width=device-width,initial-scale=1`"><title>$Title</title><style>$style</style></head><body><div class=`"wrap`"><nav class=`"crumbs`">$CrumbsHtml</nav><div class=`"card`">$BodyHtml</div></div></body></html>"
}

# locate the reverse directory (named 07-*)
$reverse = ''
if ($Root) {
    $reverse = [System.IO.Path]::GetFullPath($Root)
} else {
    $docsDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\docs'))
    foreach ($d in (Get-ChildItem -LiteralPath $docsDir -Directory)) {
        if ($d.Name.StartsWith('07-')) { $reverse = $d.FullName; break }
    }
}
if (-not $reverse -or -not (Test-Path -LiteralPath $reverse)) { throw 'reverse directory (07-*) not found' }

$toolsDir = Join-Path $reverse 'tools'
$jsonTargets = @('evidence.json', 'parameters.json', 'resources.json', 'ui-preview.json')

$mdDone = 0; $jsonDone = 0; $svgFixed = 0; $skipped = 0
foreach ($toolDir in (Get-ChildItem -LiteralPath $toolsDir -Directory | Sort-Object Name)) {
    $toolId = $toolDir.Name
    $crumbs = '<a href="../../index.html">&#8592; 工具索引</a> &middot; <code>' + (Escape-Html $toolId) + '</code>'

    # ---- markdown files ----
    foreach ($md in (Get-ChildItem -LiteralPath $toolDir.FullName -File -Filter '*.md')) {
        $htmlPath = Join-Path $toolDir.FullName ($md.BaseName + '.html')
        if (-not $Force -and (Test-Path -LiteralPath $htmlPath) -and (Get-Item -LiteralPath $htmlPath).LastWriteTime -ge $md.LastWriteTime) { $skipped++; continue }
        $mdText = [System.IO.File]::ReadAllText($md.FullName, $utf8)
        $title = $toolId + ' · ' + $md.BaseName
        $m = [regex]::Match($mdText, '(?m)^#\s+(.+)$')
        if ($m.Success) { $title = $m.Groups[1].Value.Trim() }
        $body = Convert-MdBody $mdText
        $html = New-CardHtml -Title (Escape-Html $title) -BodyHtml $body -CrumbsHtml $crumbs
        [System.IO.File]::WriteAllText($htmlPath, $html, $utf8)
        $mdDone++
    }

    # ---- json files ----
    foreach ($jn in $jsonTargets) {
        $jp = Join-Path $toolDir.FullName $jn
        if (-not (Test-Path -LiteralPath $jp)) { continue }
        $htmlPath = Join-Path $toolDir.FullName ([System.IO.Path]::GetFileNameWithoutExtension($jn) + '.html')
        if (-not $Force -and (Test-Path -LiteralPath $htmlPath) -and (Get-Item -LiteralPath $htmlPath).LastWriteTime -ge (Get-Item -LiteralPath $jp).LastWriteTime) { $skipped++; continue }
        $raw = [System.IO.File]::ReadAllText($jp, $utf8)
        $body = '<h2>' + (Escape-Html $jn) + '</h2><pre>' + (Escape-Html $raw.TrimEnd()) + '</pre>'
        $html = New-CardHtml -Title (Escape-Html ($toolId + ' · ' + $jn)) -BodyHtml $body -CrumbsHtml $crumbs
        [System.IO.File]::WriteAllText($htmlPath, $html, $utf8)
        $jsonDone++
    }

    # ---- svg：补 XML 编码声明，保证 file:// 下中文不乱码 ----
    foreach ($sv in (Get-ChildItem -LiteralPath $toolDir.FullName -File -Filter '*.svg')) {
        $raw = [System.IO.File]::ReadAllText($sv.FullName, $utf8)
        if ($raw.TrimStart().StartsWith('<?xml')) { continue }
        $fixed = '<?xml version="1.0" encoding="utf-8"?>' + "`r`n" + $raw
        [System.IO.File]::WriteAllText($sv.FullName, $fixed, $utf8)
        $svgFixed++
    }
}

Write-Output ('markdown -> html : {0}' -f $mdDone)
Write-Output ('json     -> html : {0}' -f $jsonDone)
Write-Output ('svg encoding fix : {0}' -f $svgFixed)
Write-Output ('up-to-date skip   : {0}' -f $skipped)
