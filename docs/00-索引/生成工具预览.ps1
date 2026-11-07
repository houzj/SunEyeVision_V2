# 工具调试窗预览生成器
# 用法：powershell -ExecutionPolicy Bypass -File "docs\00-索引\生成工具预览.ps1"
# 数据源：docs/04-工具设计/架构对照/2026/10/21/archive/架构站点-20261021/自研架构文档维护/公共资源/数据/page-data-7690579E71.js
# 产物：docs/08-工具预览/index.html（工具表，含「预览」列）+ docs/08-工具预览/preview/<id>.html（逐工具调试窗还原）+ assets/window.css
# 说明：预览页是按原件 ToolItemInfo 声明（参数/输入/输出）静态还原的调试窗，不执行算法、不产生真实结果。

$ErrorActionPreference = 'Stop'

$IndexDir = $PSScriptRoot
$DocsRoot = Split-Path -Path $IndexDir -Parent
$DataFile = Join-Path $DocsRoot '04-工具设计\架构对照\2026\10\21\archive\架构站点-20261021\自研架构文档维护\公共资源\数据\page-data-7690579E71.js'
$OutDir = Join-Path $DocsRoot '08-工具预览'
$PreviewDir = Join-Path $OutDir 'preview'
$AssetDir = Join-Path $PreviewDir 'assets'

if (-not (Test-Path $DataFile)) { throw ('找不到工具数据源：' + $DataFile) }
if (-not (Test-Path $PreviewDir)) { New-Item -ItemType Directory -Force -Path $PreviewDir | Out-Null }
if (-not (Test-Path $AssetDir)) { New-Item -ItemType Directory -Force -Path $AssetDir | Out-Null }

$utf8 = New-Object System.Text.UTF8Encoding($false)

function ConvertTo-HtmlText {
    param([string]$Text)
    if ($null -eq $Text) { return '' }
    return $Text.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;').Replace('"', '&quot;')
}

function Get-SafeId {
    param([string]$Key, [string]$Name)
    $base = $Key
    if ([string]::IsNullOrWhiteSpace($base)) { $base = $Name }
    $id = $base -replace '[^0-9A-Za-z\u4e00-\u9fa5]+', '-'
    return $id.Trim('-')
}

# 读取数据
$raw = [System.IO.File]::ReadAllText($DataFile, $utf8)
$marker = 'window.__PAGE_DATA__ = '
$startIndex = $raw.IndexOf($marker)
if ($startIndex -lt 0) { throw '数据文件格式变化：找不到 window.__PAGE_DATA__ 标记' }
$jsonText = $raw.Substring($startIndex + $marker.Length)
if ($jsonText.EndsWith(';')) { $jsonText = $jsonText.Substring(0, $jsonText.Length - 1) }
$data = $jsonText | ConvertFrom-Json
$tools = @($data.vmBaseline.tools)
if ($tools.Count -eq 0) { throw '数据里没有 vmBaseline.tools' }

function Get-ParamRows {
    param($Items)
    $rows = New-Object System.Collections.ArrayList
    if ($null -eq $Items) { return $rows }
    foreach ($item in $Items) {
        $name = ''
        $type = ''
        $default = ''
        $desc = ''
        foreach ($child in $item.children) {
            $label = [string]$child.type
            $text = [string]$child.text
            switch ($label) {
                'Name' { $name = $text }
                'DefaultValue' { $default = $text }
                'ValueType' { $type = $text }
                'Type' { if ($type -eq '') { $type = $text } }
                'Description' { $desc = $text }
                'Unit' { if ($text -ne '') { $desc = ($desc + ' / 单位 ' + $text).Trim(' ', '/') } }
            }
        }
        if ($name -eq '' -and $default -eq '' -and $type -eq '') { continue }
        [void]$rows.Add([pscustomobject]@{ Name = $name; Type = $type; Default = $default; Desc = $desc })
    }
    return $rows
}

function Get-VarRows {
    param($Filters)
    $rows = New-Object System.Collections.ArrayList
    if ($null -eq $Filters) { return $rows }
    foreach ($f in $Filters) {
        $attr = $f.attributes
        $name = ''
        $type = ''
        $isArray = ''
        if ($attr) {
            $name = [string]$attr.Name
            $type = [string]$attr.ValueType
            $isArray = [string]$attr.IsArray
        }
        if ($name -eq '') { continue }
        [void]$rows.Add([pscustomobject]@{ Name = $name; Type = $type; IsArray = $isArray })
    }
    return $rows
}

# 公共样式
$css = @'
:root{
  --bg:#0d1b28;--panel:#132535;--panel2:#172c3e;--line:#22394a;--text:#dde9f0;--dim:#8ba3b5;--faint:#5f7889;
  --accent:#7fd4e8;--ok:#7fd3a8;--warn:#e0a06a;--bad:#c96b6b;
}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--text);font:13px/1.6 "Microsoft YaHei","PingFang SC",-apple-system,sans-serif}
a{color:var(--accent);text-decoration:none}
a:hover{text-decoration:underline}
.win{max-width:1280px;margin:22px auto;border:1px solid var(--line);border-radius:8px;overflow:hidden;background:var(--panel)}
.titlebar{display:flex;align-items:center;gap:10px;padding:10px 14px;background:linear-gradient(180deg,#1b3348,#132535);border-bottom:1px solid var(--line)}
.titlebar .name{font-weight:600;font-size:14px}
.titlebar .mod{color:var(--faint);font-size:12px}
.titlebar .spacer{flex:1}
.titlebar .btn{width:22px;height:22px;border:1px solid var(--line);border-radius:4px;color:var(--dim);text-align:center;line-height:20px;font-size:12px}
.toolbar{display:flex;align-items:center;gap:8px;padding:8px 12px;border-bottom:1px solid var(--line);background:var(--panel2)}
.toolbar button{background:#1d384d;color:var(--text);border:1px solid var(--line);border-radius:4px;padding:4px 12px;font-size:12px;cursor:default}
.toolbar .sep{width:1px;height:18px;background:var(--line);margin:0 4px}
.toolbar .hint{margin-left:auto;color:var(--faint);font-size:12px}
.body{display:flex;min-height:520px}
.col-left{width:300px;border-right:1px solid var(--line);display:flex;flex-direction:column}
.col-mid{flex:1;display:flex;flex-direction:column;background:#0a1520}
.col-right{width:330px;border-left:1px solid var(--line);display:flex;flex-direction:column}
.sect-title{padding:7px 12px;font-size:12px;color:var(--dim);background:var(--panel2);border-bottom:1px solid var(--line)}
.scroll{overflow:auto;flex:1}
table{width:100%;border-collapse:collapse;font-size:12px}
th,td{text-align:left;padding:5px 10px;border-bottom:1px solid rgba(34,57,74,.7);vertical-align:top}
th{color:var(--dim);font-weight:500;position:sticky;top:0;background:var(--panel2)}
td.k{color:var(--text)}
td.t{color:var(--accent);white-space:nowrap}
td.v{color:var(--dim)}
.canvas{flex:1;display:flex;align-items:center;justify-content:center;color:var(--faint);text-align:center;padding:24px;font-size:12px;line-height:2}
.placeholder{border:1px dashed var(--line);border-radius:8px;padding:22px 26px;max-width:520px}
.statusbar{display:flex;gap:16px;align-items:center;padding:7px 12px;border-top:1px solid var(--line);background:var(--panel2);color:var(--faint);font-size:12px}
.badge{display:inline-block;padding:1px 7px;border-radius:10px;border:1px solid var(--line);color:var(--dim)}
.badge.ok{color:var(--ok);border-color:rgba(127,211,168,.4)}
.badge.warn{color:var(--warn);border-color:rgba(224,160,106,.4)}
.hub{max-width:1180px;margin:26px auto;padding:0 18px}
.hub h1{font-size:20px;margin:0 0 6px}
.hub .sub{color:var(--dim);font-size:12px;margin-bottom:18px}
.hub .filters{display:flex;gap:10px;margin-bottom:12px;flex-wrap:wrap}
.hub input,.hub select{background:var(--panel);color:var(--text);border:1px solid var(--line);border-radius:6px;padding:6px 10px;font-size:12px}
.hub table{background:var(--panel);border:1px solid var(--line);border-radius:8px;overflow:hidden}
.hub th{background:#16293a}
.hub tr:hover td{background:rgba(127,212,232,.05)}
.mono{font-family:Consolas,"Courier New",monospace}
'@
[System.IO.File]::WriteAllText((Join-Path $AssetDir 'window.css'), $css, $utf8)

$rows = New-Object System.Collections.ArrayList
$stateCount = @{}

foreach ($tool in $tools) {
    $displayName = [string]$tool.displayName
    $key = [string]$tool.key
    $category = [string]$tool.categoryLabel
    $plugin = [string]$tool.plugin
    if ($displayName -eq '') { $displayName = $plugin }
    if ($displayName -eq '') { $displayName = '未命名工具' }
    $id = Get-SafeId -Key $key -Name $displayName
    if ($id -eq '') { continue }

    $params = Get-ParamRows -Items $tool.declarations.algorithmParameters
    $inputs = Get-VarRows -Filters $tool.declarations.inputs
    $outputs = Get-VarRows -Filters $tool.declarations.outputs

    $state = '有原件声明'
    if ($params.Count + $inputs.Count + $outputs.Count -eq 0) { $state = '无声明' }
    if (-not $stateCount.ContainsKey($state)) { $stateCount[$state] = 0 }
    $stateCount[$state]++

    $paramRows = New-Object System.Text.StringBuilder
    foreach ($p in $params) {
        [void]$paramRows.Append('<tr><td class="k">' + (ConvertTo-HtmlText $p.Name) + '</td><td class="t">' + (ConvertTo-HtmlText $p.Type) + '</td><td class="v">' + (ConvertTo-HtmlText $p.Default) + '</td></tr>')
    }
    if ($params.Count -eq 0) { [void]$paramRows.Append('<tr><td colspan="3" class="v">原件未提供参数声明</td></tr>') }

    $inputRows = New-Object System.Text.StringBuilder
    foreach ($v in $inputs) {
        [void]$inputRows.Append('<tr><td class="k">' + (ConvertTo-HtmlText $v.Name) + '</td><td class="t">' + (ConvertTo-HtmlText $v.Type) + '</td></tr>')
    }
    if ($inputs.Count -eq 0) { [void]$inputRows.Append('<tr><td colspan="2" class="v">无输入变量声明</td></tr>') }

    $outputRows = New-Object System.Text.StringBuilder
    foreach ($v in $outputs) {
        $arrayMark = ''
        if ($v.IsArray -eq 'true') { $arrayMark = ' []' }
        [void]$outputRows.Append('<tr><td class="k">' + (ConvertTo-HtmlText $v.Name) + '</td><td class="t">' + (ConvertTo-HtmlText ($v.Type + $arrayMark)) + '</td></tr>')
    }
    if ($outputs.Count -eq 0) { [void]$outputRows.Append('<tr><td colspan="2" class="v">无输出变量声明</td></tr>') }

    $helpUrl = ''
    if ($tool.originalToolItem) { $helpUrl = [string]$tool.originalToolItem.helpUrl }

    $html = @'
<!doctype html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>__TITLE__ 调试窗（原件声明还原）</title>
<link rel="stylesheet" href="assets/window.css">
</head>
<body>
<div class="win">
  <div class="titlebar">
    <span class="name">__TITLE__</span>
    <span class="mod">__MODULE__</span>
    <span class="spacer"></span>
    <span class="btn">−</span><span class="btn">□</span><span class="btn">×</span>
  </div>
  <div class="toolbar">
    <button>运行</button><button>连续运行</button><button>停止</button>
    <span class="sep"></span>
    <button>确认</button><button>取消</button>
    <span class="sep"></span>
    <span class="badge">__CATEGORY__</span>
    <span class="hint">静态还原：按原件声明还原布局与参数，不执行算法</span>
  </div>
  <div class="body">
    <div class="col-left">
      <div class="sect-title">参数（__PCOUNT__ 项）</div>
      <div class="scroll">
        <table><thead><tr><th>参数</th><th>类型</th><th>默认值</th></tr></thead><tbody>__PARAMS__</tbody></table>
      </div>
      <div class="sect-title">输入（__ICOUNT__ 项）</div>
      <div class="scroll" style="max-height:170px">
        <table><thead><tr><th>名称</th><th>类型</th></tr></thead><tbody>__INPUTS__</tbody></table>
      </div>
    </div>
    <div class="col-mid">
      <div class="sect-title">图像</div>
      <div class="canvas">
        <div class="placeholder">
          这是 __TITLE__ 的调试窗外壳还原<br>
          布局、参数树、输入输出均来自原件声明<br>
          <span style="color:#5f7889">运行结果、ROI 交互与算法输出需实机执行，本页不含</span>
        </div>
      </div>
    </div>
    <div class="col-right">
      <div class="sect-title">结果 / 输出（__OCOUNT__ 项）</div>
      <div class="scroll">
        <table><thead><tr><th>名称</th><th>类型</th></tr></thead><tbody>__OUTPUTS__</tbody></table>
      </div>
    </div>
  </div>
  <div class="statusbar">
    <span>未运行</span>
    <span>原件模块 __MODULE__</span>
    <span>参数 __PCOUNT__ / 输入 __ICOUNT__ / 输出 __OCOUNT__</span>
    <span class="spacer" style="flex:1"></span>
    <a href="../index.html">← 返回工具表</a>
  </div>
</div>
</body>
</html>
'@

    $html = $html.Replace('__TITLE__', (ConvertTo-HtmlText $displayName))
    $html = $html.Replace('__MODULE__', (ConvertTo-HtmlText $plugin))
    $html = $html.Replace('__CATEGORY__', (ConvertTo-HtmlText $category))
    $html = $html.Replace('__PARAMS__', $paramRows.ToString())
    $html = $html.Replace('__INPUTS__', $inputRows.ToString())
    $html = $html.Replace('__OUTPUTS__', $outputRows.ToString())
    $html = $html.Replace('__PCOUNT__', [string]$params.Count)
    $html = $html.Replace('__ICOUNT__', [string]$inputs.Count)
    $html = $html.Replace('__OCOUNT__', [string]$outputs.Count)

    [System.IO.File]::WriteAllText((Join-Path $PreviewDir ($id + '.html')), $html, $utf8)

    [void]$rows.Add([pscustomobject]@{
        Id = $id; Name = $displayName; Key = $key; Plugin = $plugin; Category = $category; State = $state
        Params = $params.Count; Inputs = $inputs.Count; Outputs = $outputs.Count
    })
}

# 总表
$tableRows = New-Object System.Text.StringBuilder
foreach ($r in ($rows | Sort-Object -Property Category, Name)) {
    [void]$tableRows.Append('<tr data-cat="' + (ConvertTo-HtmlText $r.Category) + '"><td class="k">' + (ConvertTo-HtmlText $r.Name) + '</td><td class="mono v">' + (ConvertTo-HtmlText $r.Plugin) + '</td><td class="v">' + (ConvertTo-HtmlText $r.Category) + '</td><td class="v">' + $r.Params + ' / ' + $r.Inputs + ' / ' + $r.Outputs + '</td><td><a href="preview/' + $r.Id + '.html">打开</a></td></tr>')
}

$categories = ($rows | Select-Object -ExpandProperty Category -Unique | Sort-Object)
$options = '<option value="">全部分类</option>'
foreach ($c in $categories) { $options += '<option value="' + (ConvertTo-HtmlText $c) + '">' + (ConvertTo-HtmlText $c) + '</option>' }

$hub = @'
<!doctype html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>工具调试窗预览（__COUNT__ 个）</title>
<link rel="stylesheet" href="preview/assets/window.css">
<style>
.hub table{width:100%}
.hub td.state{white-space:nowrap}
</style>
</head>
<body>
<div class="hub">
  <h1>工具调试窗预览</h1>
  <div class="sub">共 __COUNT__ 个原件工具。点击「打开」按原件声明还原该工具的调试窗（参数 / 输入 / 输出）。数据来源：VM 原件 ToolItemInfo 声明，非运行截图。<br>
  说明：原工具表里的「状态」列由另一份数据源维护，本页未迁入以免口径不一致；本页只保证工具集合（162）与原件模块名一致。</div>
  <div class="filters">
    <input id="q" placeholder="搜索工具名或模块名…" style="min-width:260px">
    <select id="cat">__OPTIONS__</select>
    <span class="badge" id="count"></span>
  </div>
  <table id="tbl">
    <thead><tr><th>工具</th><th>原件模块</th><th>分类</th><th>参数 / 输入 / 输出</th><th>预览</th></tr></thead>
    <tbody>__ROWS__</tbody>
  </table>
</div>
<script>
(function(){
  var q=document.getElementById('q'), cat=document.getElementById('cat'), rows=[].slice.call(document.querySelectorAll('#tbl tbody tr')), chip=document.getElementById('count');
  function apply(){
    var kw=q.value.trim().toLowerCase(), c=cat.value, n=0;
    rows.forEach(function(tr){
      var t=tr.innerText.toLowerCase(), okKw=!kw||t.indexOf(kw)>=0, okCat=!c||tr.getAttribute('data-cat')===c;
      var show=okKw&&okCat; tr.style.display=show?'':'none'; if(show)n++;
    });
    chip.textContent='显示 '+n+' / '+rows.length;
  }
  q.addEventListener('input',apply); cat.addEventListener('change',apply); apply();
})();
</script>
</body>
</html>
'@

$hub = $hub.Replace('__COUNT__', [string]$rows.Count)
$hub = $hub.Replace('__OPTIONS__', $options)
$hub = $hub.Replace('__ROWS__', $tableRows.ToString())
[System.IO.File]::WriteAllText((Join-Path $OutDir 'index.html'), $hub, $utf8)

Write-Output ('工具数 ' + $rows.Count + '，预览页已生成到 ' + $PreviewDir)
Write-Output ('状态分布：')
foreach ($k in $stateCount.Keys) { Write-Output ('  ' + $k + ' = ' + $stateCount[$k]) }
