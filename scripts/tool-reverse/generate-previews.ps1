#requires -Version 5.1
<#
.SYNOPSIS
    从工具卡数据生成控件级调试窗预览页。

.DESCRIPTION
    数据来源只有卡片七件套，不引入额外推断：
      resources.json.uiTabs      -> 页签栏
      resources.json.uiControls  -> 左列参数控件（类型 -> 控件形态，名称 -> 标签）
      resources.json.sharedUiGroups -> 工具栏下的共享组标记
      parameters.json.parameters -> 参数默认值（填进控件）
      parameters.json.inputs     -> 左列下方输入区（Combination / Filter）
      parameters.json.outputs    -> 右列输出区
      evidence.json              -> 标题、模块名、分类、状态

    还原边界（必须保留，不许删）：
      - 控件形态与顺序来自资源声明，不含像素级样式、颜色、字体的逐控件取证
      - 控件到页签的归属未取证：多页签时全部控件列在第一个页签，其余页签显式标注「归属未取证」
      - 显隐条件、可用状态、值联动未取证，不做假装
      - 不执行算法，画布为占位

.PARAMETER Root
    工具卡根目录，默认 docs/07-工具逆向。

.PARAMETER OutDir
    预览输出目录，默认 docs/08-工具预览。

.PARAMETER OnlyTool
    只生成指定工具，可传多个。

.PARAMETER All
    全量生成并重建索引。不带此开关且未传 OnlyTool 时按全量处理，但会给出提示。

.EXAMPLE
    powershell -File scripts/tool-reverse/generate-previews.ps1 -OnlyTool imagesourcemodule,imvsblobfindmodu,ifbranchmodule
#>
[CmdletBinding()]
param(
    [string]$Root,
    [string]$OutDir,
    [string[]]$OnlyTool,
    [switch]$All,
    [switch]$RebuildIndex
)

$ErrorActionPreference = 'Stop'

# powershell -File 传 -OnlyTool a,b,c 时不会自动拆成数组，这里统一按逗号/分号拆分
if ($OnlyTool) {
    $OnlyTool = @($OnlyTool | ForEach-Object { $_ -split '[,;]' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

if (-not $Root) { $Root = Join-Path $PSScriptRoot '..\..\docs\07-工具逆向' }
if (-not $OutDir) { $OutDir = Join-Path $PSScriptRoot '..\..\docs\08-工具预览' }
if (-not (Test-Path $Root)) { throw "工具卡根目录不存在: $Root" }
$root = (Resolve-Path $Root).Path
$toolsDir = Join-Path $root 'tools'
$outRoot = [System.IO.Path]::GetFullPath($OutDir)
$previewDir = Join-Path $outRoot 'preview'
if (-not (Test-Path $previewDir)) { New-Item -ItemType Directory -Path $previewDir -Force | Out-Null }

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

function Get-SafeName {
    param([string]$Name)
    if (-not $Name) { return 'unknown' }
    $safe = $Name -replace '[^\w\-]', ''
    if (-not $safe) { return 'unknown' }
    return $safe
}

# 控件类型 -> 形态。按关键字归类，未归类的一律 text 并保留原始类型标签，
# 不做「看起来像下拉就当下拉」的猜测。
function Get-ControlKind {
    param([string]$Type)
    $t = [string]$Type
    if (-not $t) { return 'text' }
    if ($t -match 'PointSet|PointPair|LinePair|Combination') { return 'composite' }
    if ($t -match 'Bettween|Beetween|Between') { return 'range' }
    if ($t -match '^RadioSelecter') { return 'radio' }
    if ($t -match 'Enumeration|Enumeraion|^Combo|^Enumera') { return 'select' }
    if ($t -match '^Boolean') { return 'toggle' }
    if ($t -match '^ImageViewConfig') { return 'imageview' }
    if ($t -match '^GroupLinkItem') { return 'group' }
    if ($t -match '^Expand') { return 'fold' }
    if ($t -match '^HeadBar|^TextNormal') { return 'header' }
    if ($t -match '^ROISelecter|^ROI') { return 'roi' }
    if ($t -match 'OpenFile|OpenFolder|SaveFile|SaveTextItem|^Command|ButtonSelecter|GlobalVarItemIn|QueueItemIn') { return 'button' }
    if ($t -match '^Integer|^Float|Numeric') { return 'number' }
    if ($t -match '^String|Text') { return 'text' }
    return 'text'
}

# parameters.json 的参数树 -> 名称到默认值的表
function Get-ParamDefaults {
    param([object]$ParametersJson)
    $map = @{}
    if (-not $ParametersJson) { return $map }
    foreach ($p in @($ParametersJson.parameters)) {
        if (-not $p) { continue }
        $name = $null
        $value = $null
        foreach ($c in @($p.children)) {
            if (-not $c) { continue }
            if ($c.type -eq 'Name') { $name = [string]$c.text }
            elseif ($c.type -eq 'DefaultValue') { $value = [string]$c.text }
        }
        if ($name -and -not $map.ContainsKey($name)) { $map[$name] = $value }
    }
    return $map
}

# ---- 原厂图标：从已入库的反编译资源里取，复制到预览站点共享一份 ----
# 来源固定为 decompiled 下的 WpfControls / PcUi / Controls 资源目录，不做二次绘制。
$iconSrcRoot = Join-Path $root 'decompiled'
$iconOutDir = Join-Path $outRoot 'assets\icons'
$iconMap = [ordered]@{
    'run'      = 'WpfControls\resources\btnrunnormal.png'
    'stop'     = 'WpfControls\resources\btnstopnormal.png'
    'open'     = 'WpfControls\resources\btnopennormal.png'
    'save'     = 'WpfControls\resources\btnsavenormal.png'
    'add'      = 'WpfControls\resources\add_normal.png'
    'runbar'   = 'WpfControls\resources\bottombar_run.png'
    'zoomin'   = 'PcUi\resources\zoomin.png'
    'zoomout'  = 'PcUi\resources\zoomout.png'
    'original' = 'PcUi\resources\view-original-image-_normal.png'
    'delete'   = 'Controls\controls\collectioncontrol\images\delete16.png'
}
$script:iconsReady = $false
$script:iconAvailable = @{}
if (Test-Path $iconSrcRoot) {
    foreach ($k in $iconMap.Keys) {
        if (Test-Path (Join-Path $iconSrcRoot $iconMap[$k])) { $script:iconAvailable[$k] = $true }
    }
    if ($script:iconAvailable.Count -gt 0) {
        if (-not (Test-Path $iconOutDir)) { New-Item -ItemType Directory -Path $iconOutDir -Force | Out-Null }
        foreach ($k in $script:iconAvailable.Keys) {
            Copy-Item -Path (Join-Path $iconSrcRoot $iconMap[$k]) -Destination (Join-Path $iconOutDir ($k + '.png')) -Force
        }
        $script:iconsReady = $true
    }
}

function Get-IconHtml {
    param([string]$Key)
    if ($script:iconsReady -and $script:iconAvailable.ContainsKey($Key)) {
        return '<img class="ico" src="../assets/icons/' + $Key + '.png" alt="">'
    }
    return ''
}

$css = @'
:root{--win:#fff;--bar:#f2f3f5;--line:#dcdfe4;--text:#1d2129;--muted:#6b7280;--accent:#ff7900;--field:#fff;--bg:#e9ecf0}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--text);font:12px/1.5 "Microsoft YaHei","Segoe UI",sans-serif}
.pagewrap{max-width:1220px;margin:0 auto;padding:18px 16px 40px}
.pagehead{font-size:12px;color:var(--muted);margin-bottom:10px}
.pagehead a{color:#2563eb;text-decoration:none}
.pagehead a:hover{text-decoration:underline}
.win{background:var(--win);border:1px solid var(--line);border-radius:4px;box-shadow:0 4px 16px rgba(0,0,0,.10);display:flex;flex-direction:column;height:780px;overflow:hidden}
.titlebar{height:32px;background:var(--bar);border-bottom:1px solid var(--line);display:flex;align-items:center;gap:8px;padding:0 10px;flex:0 0 32px}
.titlebar .nm{font-weight:700;font-size:13px}
.titlebar .mod{color:var(--muted);font-size:11px}
.titlebar .sp{flex:1}
.titlebar .btns span{display:inline-block;width:24px;text-align:center;color:#555}
.toolbar{height:42px;border-bottom:1px solid var(--line);display:flex;align-items:center;gap:6px;padding:0 10px;flex:0 0 42px}
.toolbar button{height:26px;padding:0 12px;font-size:12px;border:1px solid #cfd3d8;background:#fbfbfc;border-radius:2px;cursor:default;font-family:inherit}
.toolbar button .ico{width:15px;height:15px;vertical-align:-3px;margin-right:5px}
.roibar .b .ico{width:14px;height:14px;vertical-align:-2px;margin-right:3px}
.toolbar button.primary{background:var(--accent);border-color:var(--accent);color:#fff}
.toolbar .sep{width:1px;height:20px;background:var(--line);margin:0 6px}
.toolbar .tag{font-size:11px;color:#8a5a00;background:#fff4e0;border:1px solid #ffd9a0;border-radius:10px;padding:1px 9px}
.toolbar .hint{font-size:11px;color:var(--muted);margin-left:8px}
.tabs{height:34px;background:#fafafc;border-bottom:1px solid var(--line);display:flex;align-items:stretch;flex:0 0 34px;padding-left:6px;overflow:hidden}
.tabs .tab{display:flex;align-items:center;padding:0 16px;font-size:12px;color:#374151;border-bottom:2px solid transparent;white-space:nowrap}
.tabs .tab.on{background:#fff;border-bottom-color:var(--accent);font-weight:600}
.tabs .tab.na{color:#b0b4bb}
.body{flex:1;display:flex;min-height:0}
.col{display:flex;flex-direction:column;min-height:0}
.col-left{width:330px;border-right:1px solid var(--line);background:#fff}
.col-mid{flex:1;background:#f7f8fa;border-right:1px solid var(--line)}
.col-right{width:330px;background:#fff}
.sect{font-size:11px;font-weight:700;color:#4b5563;background:#f5f6f8;border-bottom:1px solid var(--line);padding:6px 10px;letter-spacing:.02em;display:flex;justify-content:space-between}
.sect .cnt{font-weight:400;color:var(--muted)}
.scroll{overflow:auto;flex:1;padding:6px 10px 12px}
.grp{margin:10px 0 4px;font-size:11px;font-weight:700;color:#6b7280;border-bottom:1px dashed var(--line);padding-bottom:3px;display:flex;align-items:center;gap:6px}
.grp .ic{color:var(--accent)}
.grp .ty{font-weight:400;color:#9aa0a6;font-size:10px}
.row{display:flex;align-items:center;gap:8px;min-height:26px;padding:1px 0}
.row>label{flex:0 0 52%;text-align:right;color:#4b5563;font-size:12px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.row .ctl{flex:1;display:flex;align-items:center;gap:4px;min-width:0}
.row select,.row input[type=text],.row input[type=number]{height:24px;width:100%;border:1px solid #cfd3d8;border-radius:2px;background:var(--field);font:12px/1 "Microsoft YaHei",sans-serif;padding:0 6px;color:var(--text)}
.row select{appearance:none;background-image:linear-gradient(45deg,transparent 50%,#777 50%),linear-gradient(135deg,#777 50%,transparent 50%);background-position:calc(100% - 12px) 10px,calc(100% - 8px) 10px;background-size:4px 4px,4px 4px;background-repeat:no-repeat;padding-right:22px}
.row input[type=checkbox]{width:14px;height:14px;accent-color:var(--accent)}
.row .btn{height:24px;flex:1;border:1px solid #cfd3d8;background:#fbfbfc;border-radius:2px;font:12px "Microsoft YaHei",sans-serif;color:#374151;padding:0 6px;text-align:left;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.row .btn.ell:after{content:"…";float:right;color:#888}
.row .tild{color:#9aa0a6}
.row .mini{flex:0 0 24px;height:24px;border:1px solid #cfd3d8;background:#fbfbfc;border-radius:2px;font:12px "Microsoft YaHei";color:#555;padding:0}
.row .mini:hover{background:#f0f1f3}
.row .swatch{width:14px;height:14px;border:1px solid #c9ccd1;background:linear-gradient(135deg,#ff7900,#3b82f6 60%,#22c55e);flex:0 0 14px}
.row.disabled>label{color:#b0b4bb}
.typechip{font-size:10px;color:#9aa0a6;border:1px solid #e3e6ea;border-radius:3px;padding:0 4px;flex:0 0 auto}
.canvas{flex:1;display:flex;align-items:center;justify-content:center;padding:16px;background-image:linear-gradient(#eef0f3 1px,transparent 1px),linear-gradient(90deg,#eef0f3 1px,transparent 1px);background-size:20px 20px}
.ph{max-width:420px;text-align:center;color:#8a9199;line-height:1.9;border:1px dashed #ccd2d9;border-radius:6px;padding:26px 22px;background:#fff}
.ph b{color:#5f6771}
.roibar{height:30px;border-top:1px solid var(--line);display:flex;align-items:center;gap:6px;padding:0 8px;background:#fbfbfc}
.roibar .b{height:22px;padding:0 9px;border:1px solid #d5d9de;background:#fff;border-radius:2px;font-size:11px;color:#555;display:inline-flex;align-items:center}
.statusbar{height:28px;border-top:1px solid var(--line);background:#f5f6f8;display:flex;align-items:center;gap:12px;padding:0 10px;font-size:11px;color:var(--muted);flex:0 0 28px}
.statusbar .sp{flex:1}
.statusbar a{color:#2563eb;text-decoration:none}
.showrow{display:flex;align-items:center;gap:6px;min-height:22px;font-size:12px}
.showrow .nm{flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.showrow .t2{font-size:10px;color:#9aa0a6;flex:0 0 auto}
.showrow input{width:13px;height:13px;accent-color:var(--accent)}
.showrow .swatch{width:13px;height:13px;border:1px solid #c9ccd1;background:linear-gradient(135deg,#ff7900,#3b82f6 60%,#22c55e);flex:0 0 13px}
.outrow{display:flex;align-items:center;gap:6px;min-height:22px;font-size:12px}
.outrow .nm{flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.outrow .t2{font-size:10px;color:#9aa0a6;flex:0 0 auto}
.outrow input{width:13px;height:13px;accent-color:var(--accent)}
.note{margin-top:14px;border-left:3px solid #f59e0b;background:#fffbeb;color:#78350f;font-size:12px;padding:10px 14px;border-radius:0 6px 6px 0}
.note b{color:#92400e}
.note ul{margin:6px 0 0;padding-left:20px}
.note li{margin:2px 0}
'@

function New-ControlRow {
    param([object]$Ctrl, [hashtable]$Defaults)

    $type = [string]$Ctrl.type
    $name = [string]$Ctrl.name
    $kind = Get-ControlKind $type
    $label = Encode-Html $name
    if (-not $name) { $label = '<span style="color:#b0b4bb">（未命名）</span>' }

    $val = ''
    if ($name -and $Defaults.ContainsKey($name) -and $null -ne $Defaults[$name]) { $val = $Defaults[$name] }
    $valHtml = Encode-Html $val
    # 类型标签只在「未归类、按文本框兜底」时显示：已归类类型的形态本身已表达类型，
    # 每行都挂类型标签会让还原图变成表格，原件界面里没有这种东西。
    $chip = ''
    if ($kind -eq 'text') { $chip = '<span class="typechip">' + (Encode-Html $type) + '</span>' }

    switch ($kind) {
        'group' {
            return '<div class="grp"><span class="ic">&#9662;</span>' + $label + '<span class="ty">' + (Encode-Html $type) + '</span></div>'
        }
        'fold' {
            return '<div class="grp"><span class="ic">&#9656;</span>' + $label + '<span class="ty">' + (Encode-Html $type) + '（折叠）</span></div>'
        }
        'header' {
            return '<div class="grp"><span class="ic">&#9642;</span>' + $label + '</div>'
        }
        'select' {
            return '<div class="row"><label title="' + $label + '">' + $label + '</label><div class="ctl"><select><option>' + $valHtml + '</option></select>' + $chip + '</div></div>'
        }
        'toggle' {
            $checked = ''
            if ($val -eq 'True' -or $val -eq '1' -or $val -eq 'true') { $checked = ' checked' }
            return '<div class="row"><label title="' + $label + '">' + $label + '</label><div class="ctl"><input type="checkbox"' + $checked + '>' + $chip + '</div></div>'
        }
        'imageview' {
            return '<div class="row"><label title="' + $label + '">' + $label + '</label><div class="ctl"><span class="swatch"></span><input type="checkbox" checked>' + $chip + '</div></div>'
        }
        'number' {
            return '<div class="row"><label title="' + $label + '">' + $label + '</label><div class="ctl"><input type="text" value="' + $valHtml + '">' + $chip + '</div></div>'
        }
        'range' {
            return '<div class="row"><label title="' + $label + '">' + $label + '</label><div class="ctl"><input type="text" value="' + $valHtml + '" style="max-width:72px"><span class="tild">~</span><input type="text" value="" style="max-width:72px">' + $chip + '</div></div>'
        }
        'radio' {
            $ck = ''
            if ($val -eq 'True' -or $val -eq '1') { $ck = ' checked' }
            return '<div class="row"><label title="' + $label + '">' + $label + '</label><div class="ctl"><input type="radio" name="' + (Encode-Html $name) + '"' + $ck + '><input type="radio" name="' + (Encode-Html $name) + '">' + $chip + '</div></div>'
        }
        'roi' {
            return '<div class="row"><label title="' + $label + '">' + $label + '</label><div class="ctl"><select><option>' + $valHtml + '</option></select><button class="btn" style="flex:0 0 34px;text-align:center">&#9633;</button></div></div>'
        }
        'button' {
            # ButtonSelecter 在原件里是「值 + 绑定选择器」两段，不是纯按钮；
            # 文件/文件夹对话框才是整行按钮；命令类保持按钮。
            if ($type -match 'ButtonSelecter') {
                return '<div class="row"><label title="' + $label + '">' + $label + '</label><div class="ctl"><input type="text" value="' + $valHtml + '"><button class="mini" title="选择绑定">&#8943;</button>' + $chip + '</div></div>'
            }
            $cls = 'btn'
            if ($type -match 'OpenFile|OpenFolder|SaveFile|SaveTextItem') { $cls = 'btn ell' }
            return '<div class="row"><label title="' + $label + '">' + $label + '</label><div class="ctl"><button class="' + $cls + '">' + $valHtml + '</button>' + $chip + '</div></div>'
        }
        'composite' {
            return '<div class="row"><label title="' + $label + '">' + $label + '</label><div class="ctl"><button class="btn ell">' + $valHtml + '</button>' + $chip + '</div></div>'
        }
        default {
            return '<div class="row"><label title="' + $label + '">' + $label + '</label><div class="ctl"><input type="text" value="' + $valHtml + '">' + $chip + '</div></div>'
        }
    }
}

function New-PreviewPage {
    param(
        [string]$ToolId,
        [object]$Evidence,
        [object]$Resources,
        [object]$Parameters,
        [string]$OutFileName
    )

    $displayName = $Evidence.displayName
    if (-not $displayName) { $displayName = $ToolId }
    $plugin = $Evidence.originalPlugin
    if (-not $plugin) { $plugin = $ToolId }
    $status = $Evidence.status
    if (-not $status) { $status = 'unknown' }

    $defaults = Get-ParamDefaults $Parameters
    $tabs = @()
    $controls = @()
    $sharedGroups = @()
    if ($Resources) {
        $tabs = @($Resources.uiTabs | Where-Object { $_ -ne $null })
        $controls = @($Resources.uiControls | Where-Object { $_ -ne $null })
        $sharedGroups = @($Resources.sharedUiGroups | Where-Object { $_ -ne $null })
    }
    $inputs = @()
    $outputs = @()
    if ($Parameters) {
        $inputs = @($Parameters.inputs | Where-Object { $_ -ne $null })
        $outputs = @($Parameters.outputs | Where-Object { $_ -ne $null })
    }

    # ImageViewConfig 是结果视图的显示项开关（对应原件 Display.xml 的视图项），
    # 不属于参数区。按形态分列，避免把显示开关画成参数。
    $showItems = @($controls | Where-Object { (Get-ControlKind ([string]$_.type)) -eq 'imageview' })
    $paramControls = @($controls | Where-Object { (Get-ControlKind ([string]$_.type)) -ne 'imageview' })

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('<!DOCTYPE html>')
    [void]$sb.AppendLine('<html lang="zh-CN"><head><meta charset="utf-8">')
    [void]$sb.AppendLine('<meta name="viewport" content="width=device-width,initial-scale=1">')
    [void]$sb.AppendLine('<title>' + (Encode-Html $displayName) + ' · 调试窗预览</title>')
    # 供逆向对照页识别「这张预览已升级到控件级」，不要删
    [void]$sb.AppendLine('<!-- suneye:preview-level=control tool=' + (Encode-Html $ToolId) + ' -->')
    [void]$sb.AppendLine('<style>' + $css + '</style></head><body>')
    [void]$sb.AppendLine('<div class="pagewrap">')
    [void]$sb.AppendLine('<div class="pagehead"><a href="../index.html">&larr; 工具预览索引</a> &nbsp;·&nbsp; <a href="../../07-工具逆向/index.html">逆向证据库</a> &nbsp;·&nbsp; 卡片 <code>' + (Encode-Html $ToolId) + '</code></div>')

    [void]$sb.AppendLine('<div class="win">')
    # 标题栏
    [void]$sb.AppendLine('<div class="titlebar"><span class="nm">' + (Encode-Html $displayName) + '</span><span class="mod">' + (Encode-Html $plugin) + '</span><span class="sp"></span><span class="btns"><span>&minus;</span><span>&#9633;</span><span>&times;</span></span></div>')
    # 工具栏
    [void]$sb.AppendLine('<div class="toolbar"><button class="primary">' + (Get-IconHtml 'run') + '运行</button><button>' + (Get-IconHtml 'runbar') + '连续运行</button><button>' + (Get-IconHtml 'stop') + '停止</button><span class="sep"></span><button>确认</button><button>取消</button><span class="sep"></span><span class="tag">' + (Encode-Html $Evidence.originalCategory) + '</span><span class="hint">静态还原：控件形态与顺序按原件资源声明，不执行算法</span></div>')

    # 页签
    if ($tabs.Count -gt 0) {
        [void]$sb.AppendLine('<div class="tabs">')
        $i = 0
        foreach ($t in $tabs) {
            $cls = 'tab'
            $label = [string]$t.Name
            if ($i -eq 0) { $cls = 'tab on' }
            elseif ($tabs.Count -gt 1) { $cls = 'tab na'; $label = $label + '（归属未取证）' }
            [void]$sb.AppendLine('<span class="' + $cls + '">' + (Encode-Html $label) + '</span>')
            $i++
        }
        [void]$sb.AppendLine('</div>')
    }

    [void]$sb.AppendLine('<div class="body">')

    # 左列：参数控件 + 输入
    [void]$sb.AppendLine('<div class="col col-left">')
    [void]$sb.AppendLine('<div class="sect">参数<span class="cnt">控件 ' + $paramControls.Count + ' · 默认值 ' + $defaults.Count + '</span></div>')
    [void]$sb.AppendLine('<div class="scroll">')
    if ($paramControls.Count -eq 0) {
        [void]$sb.AppendLine('<div class="ph" style="margin:12px 0;padding:16px">原件未声明 UI 控件。<br>本工具只能按参数树还原，不能渲染控件形态。</div>')
    }
    else {
        foreach ($c in $paramControls) {
            [void]$sb.AppendLine((New-ControlRow -Ctrl $c -Defaults $defaults))
        }
        # 声明稀少必须说明原因，否则页面会让人误以为界面只有这两三个控件。
        if ($paramControls.Count -lt 6 -and $defaults.Count -eq 0) {
            [void]$sb.AppendLine('<div class="ph" style="margin:14px 0;padding:14px 12px;text-align:left;line-height:1.8">原件资源只声明 ' + $paramControls.Count + ' 个控件，且参数树为空。<br>该类工具的界面主体多由内嵌复合控件或代码构建，不在 <code>AlgorithmTabRoot</code> 声明里，因此本页无法还原其完整布局。</div>')
        }
    }
    [void]$sb.AppendLine('</div>')

    [void]$sb.AppendLine('<div class="sect">输入<span class="cnt">' + $inputs.Count + ' 项</span></div>')
    [void]$sb.AppendLine('<div class="scroll" style="flex:0 0 auto;max-height:190px">')
    if ($inputs.Count -eq 0) {
        [void]$sb.AppendLine('<div class="row"><label>—</label><div class="ctl"><span class="typechip">无输入声明</span></div></div>')
    }
    else {
        foreach ($in in $inputs) {
            $attrs = $in.attributes
            $inName = [string]$attrs.Name
            if (-not $inName) { $inName = [string]$in.type }
            $inType = [string]$in.type
            $style = [string]$attrs.Style
            $vt = [string]$attrs.ValueType
            $tail = ''
            if ($style) { $tail = $style }
            elseif ($vt) { $tail = $vt }
            [void]$sb.AppendLine('<div class="row"><label title="' + (Encode-Html $inName) + '">' + (Encode-Html $inName) + '</label><div class="ctl"><span class="typechip">' + (Encode-Html $inType) + '</span>' + (Encode-Html $tail) + '</div></div>')
        }
    }
    [void]$sb.AppendLine('</div>')
    [void]$sb.AppendLine('</div>')

    # 中列：画布
    [void]$sb.AppendLine('<div class="col col-mid">')
    [void]$sb.AppendLine('<div class="sect">图像<span class="cnt">画布占位</span></div>')
    [void]$sb.AppendLine('<div class="canvas"><div class="ph"><b>' + (Encode-Html $displayName) + '</b> 的调试窗<br>左侧控件按原件 <code>AlgorithmTabRoot</code> 声明还原<br><span style="color:#a0a6ad">图像、ROI 交互与运行结果需实机执行，本页不含</span></div></div>')
    [void]$sb.AppendLine('<div class="roibar"><span class="b">' + (Get-IconHtml 'original') + '适应</span><span class="b">1:1</span><span class="b">ROI</span><span class="b">屏蔽</span><span class="b">十字</span><span style="flex:1"></span><span class="b">' + (Get-IconHtml 'zoomin') + '</span><span class="b">' + (Get-IconHtml 'zoomout') + '</span></div>')
    [void]$sb.AppendLine('</div>')

    # 右列：显示项 + 输出
    [void]$sb.AppendLine('<div class="col col-right">')
    if ($showItems.Count -gt 0) {
        [void]$sb.AppendLine('<div class="sect">显示项<span class="cnt">' + $showItems.Count + ' 项 · 结果视图开关</span></div>')
        [void]$sb.AppendLine('<div class="scroll" style="flex:0 0 132px">')
        foreach ($c in $showItems) {
            $nm = Encode-Html ([string]$c.name)
            [void]$sb.AppendLine('<div class="showrow"><input type="checkbox" checked><span class="swatch"></span><span class="nm" title="' + $nm + '">' + $nm + '</span><span class="t2">' + (Encode-Html ([string]$c.type)) + '</span></div>')
        }
        [void]$sb.AppendLine('</div>')
    }
    [void]$sb.AppendLine('<div class="sect">结果 / 输出<span class="cnt">' + $outputs.Count + ' 项</span></div>')
    [void]$sb.AppendLine('<div class="scroll">')
    if ($outputs.Count -eq 0) {
        [void]$sb.AppendLine('<div class="outrow"><span class="nm">无输出声明</span></div>')
    }
    else {
        foreach ($o in $outputs) {
            $oa = $o.attributes
            $on = [string]$oa.Name
            if (-not $on) { $on = [string]$o.type }
            $ot = [string]$oa.ValueType
            $arr = ''
            if ([string]$oa.IsArray -eq 'true') { $arr = '[]' }
            $checked = ' checked'
            [void]$sb.AppendLine('<div class="outrow"><input type="checkbox"' + $checked + '><span class="nm" title="' + (Encode-Html $on) + '">' + (Encode-Html $on) + '</span><span class="t2">' + (Encode-Html $ot) + $arr + '</span></div>')
        }
    }
    [void]$sb.AppendLine('</div>')

    if ($sharedGroups.Count -gt 0) {
        [void]$sb.AppendLine('<div class="sect">共享组<span class="cnt">' + $sharedGroups.Count + '</span></div>')
        [void]$sb.AppendLine('<div class="scroll" style="max-height:120px">')
        foreach ($g in $sharedGroups) {
            [void]$sb.AppendLine('<div class="outrow"><span class="nm">' + (Encode-Html $g) + '</span></div>')
        }
        [void]$sb.AppendLine('</div>')
    }
    [void]$sb.AppendLine('</div>')

    [void]$sb.AppendLine('</div>')  # body

    # 状态栏
    [void]$sb.AppendLine('<div class="statusbar"><span>未运行</span><span>模块 ' + (Encode-Html $plugin) + '</span><span>参数 ' + $defaults.Count + ' / 输入 ' + $inputs.Count + ' / 输出 ' + $outputs.Count + '</span><span class="sp"></span><span>状态 ' + (Encode-Html $status) + '</span><a href="../index.html">工具预览索引</a></div>')
    [void]$sb.AppendLine('</div>')  # win

    # 还原边界
    [void]$sb.AppendLine('<div class="note"><b>还原边界</b><ul>')
    [void]$sb.AppendLine('<li><b>已还原</b>：页签、参数控件形态与声明顺序、参数默认值、输入输出声明、共享组、分类与状态徽章</li>')
    [void]$sb.AppendLine('<li><b>未还原</b>：像素级样式（颜色/字号/间距）、控件到页签的归属、显隐与可用条件、值联动、ROI 绘制交互、运行结果</li>')
    [void]$sb.AppendLine('<li>控件形态由 <code>resources.json.uiControls[].type</code> 推断，未归类类型按文本框显示并保留原始类型标签；不做外形臆测</li>')
    [void]$sb.AppendLine('<li>本页不证明该工具在原厂已注册、加载、执行或完成</li>')
    [void]$sb.AppendLine('</ul></div>')

    [void]$sb.AppendLine('</div></body></html>')

    $outPath = Join-Path $previewDir $OutFileName
    [System.IO.File]::WriteAllText($outPath, $sb.ToString(), $utf8)
    return $OutFileName
}

# ---- 主流程 ----
$toolDirs = Get-ChildItem -Path $toolsDir -Directory | Sort-Object Name
if ($OnlyTool -and $OnlyTool.Count -gt 0) {
    $wanted = @{}
    foreach ($t in $OnlyTool) { $wanted[$t] = $true }
    $toolDirs = @($toolDirs | Where-Object { $wanted.ContainsKey($_.Name) })
    if ($toolDirs.Count -eq 0) { throw "未找到工具卡: $($OnlyTool -join ',')" }
}
elseif (-not $All) {
    Write-Host '未指定 -OnlyTool 且未加 -All：按全量生成。' -ForegroundColor Yellow
}

$generated = New-Object 'System.Collections.Generic.List[object]'
$usedNames = @{}

# 部分卡片的 evidence.json 缺 originalCategory，直接生成会得到 Unknown-<模块>.html，
# 与既有的 <真实分类>-<模块>.html 并存，反而让对照页匹配到两个文件而判定无预览。
# 这里按「规范化模块名 -> 既有文件名里的分类前缀」回填，来源是既有文件名本身。
$existingCatByModule = @{}
if (Test-Path $previewDir) {
    foreach ($f in (Get-ChildItem -Path $previewDir -Filter *.html -File)) {
        $b = $f.BaseName
        $i = $b.IndexOf('-')
        if ($i -lt 1) { continue }
        $modKey = ($b.Substring($i + 1)) -replace '[-_]', ''
        $existingCatByModule[$modKey.ToLower()] = $b.Substring(0, $i)
    }
}

foreach ($dir in $toolDirs) {
    $toolId = $dir.Name
    if ($toolId.StartsWith('_')) { continue }

    $evPath = Join-Path $dir.FullName 'evidence.json'
    if (-not (Test-Path $evPath)) { continue }
    $ev = Get-Content $evPath -Raw -Encoding UTF8 | ConvertFrom-Json

    $res = $null
    $resPath = Join-Path $dir.FullName 'resources.json'
    if (Test-Path $resPath) { $res = Get-Content $resPath -Raw -Encoding UTF8 | ConvertFrom-Json }

    $par = $null
    $parPath = Join-Path $dir.FullName 'parameters.json'
    if (Test-Path $parPath) { $par = Get-Content $parPath -Raw -Encoding UTF8 | ConvertFrom-Json }

    # 模块名里的下划线统一成连字符，与既有预览命名一致，避免同一工具出现两份文件
    $plugin = (Get-SafeName $ev.originalPlugin) -replace '_', '-'
    $cat = Get-SafeName $ev.originalCategory
    if (-not $ev.originalCategory) {
        $modKey = ($plugin -replace '[-_]', '').ToLower()
        if ($existingCatByModule.ContainsKey($modKey)) { $cat = $existingCatByModule[$modKey] }
    }
    $fileName = $cat + '-' + $plugin + '.html'
    if ($usedNames.ContainsKey($fileName.ToLower())) {
        $fileName = $cat + '-' + $plugin + '-' + (Get-SafeName $toolId) + '.html'
    }
    $usedNames[$fileName.ToLower()] = $true

    $ctrlCount = 0
    if ($res) { $ctrlCount = @($res.uiControls | Where-Object { $_ -ne $null }).Count }

    $null = New-PreviewPage -ToolId $toolId -Evidence $ev -Resources $res -Parameters $par -OutFileName $fileName
    $generated.Add([pscustomobject]@{
        toolId        = $toolId
        displayName   = $ev.displayName
        category      = $ev.originalCategory
        status        = $ev.status
        page          = $fileName
        controlCount  = $ctrlCount
        hasControls   = ($ctrlCount -gt 0)
    })
}

# 索引：仅在非单工具模式或显式 -All 时重建，避免把索引缩成一张卡
$rebuildIndex = ($generated.Count -gt 3) -or $All -or $RebuildIndex
if ($rebuildIndex) {
    # 重建时用全量卡片，保证索引始终完整
    $allCards = New-Object 'System.Collections.Generic.List[object]'
    $nameMap = @{}
    foreach ($g in $generated) { $nameMap[$g.toolId] = $g }
    foreach ($dir in (Get-ChildItem -Path $toolsDir -Directory | Sort-Object Name)) {
        $toolId = $dir.Name
        if ($toolId.StartsWith('_')) { continue }
        $evPath = Join-Path $dir.FullName 'evidence.json'
        if (-not (Test-Path $evPath)) { continue }
        $ev = Get-Content $evPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $plugin = (Get-SafeName $ev.originalPlugin) -replace '_', '-'
        $cat = Get-SafeName $ev.originalCategory
        $catLabel = $ev.originalCategory
        if (-not $ev.originalCategory) {
            $modKey2 = ($plugin -replace '[-_]', '').ToLower()
            if ($existingCatByModule.ContainsKey($modKey2)) { $cat = $existingCatByModule[$modKey2]; $catLabel = $cat }
            else { $catLabel = '未分类' }
        }
        $fileName = $cat + '-' + $plugin + '.html'
        $pvPath = Join-Path $previewDir $fileName
        if (-not (Test-Path $pvPath)) {
            # 分类缺失的卡片按模块名唯一匹配既有预览，避免索引里出现假「无预览」
            $key = ($plugin -replace '[-_]', '').ToLower()
            $hits = @(Get-ChildItem -Path $previewDir -Filter *.html -File | Where-Object {
                $b = $_.BaseName
                $i = $b.IndexOf('-')
                if ($i -lt 0) { return $false }
                return ((($b.Substring($i + 1)) -replace '[-_]', '').ToLower() -eq $key)
            })
            if ($hits.Count -eq 1) { $fileName = $hits[0].Name; $pvPath = $hits[0].FullName }
        }
        $exists = Test-Path $pvPath
        $level = 'none'
        if ($exists) {
            $level = 'shell'
            $pvText = [System.IO.File]::ReadAllText($pvPath, $utf8)
            if ($pvText.Contains('suneye:preview-level=control')) { $level = 'control' }
        }
        $ctrlCount = 0
        $resPath = Join-Path $dir.FullName 'resources.json'
        if (Test-Path $resPath) {
            $res = Get-Content $resPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $ctrlCount = @($res.uiControls | Where-Object { $_ -ne $null }).Count
        }
        $allCards.Add([pscustomobject]@{
            toolId       = $toolId
            displayName  = $ev.displayName
            category     = $catLabel
            status       = $ev.status
            page         = $fileName
            controlCount = $ctrlCount
            hasControls  = ($ctrlCount -gt 0)
            exists       = $exists
            level        = $level
        })
    }

    $ix = New-Object System.Text.StringBuilder
    [void]$ix.AppendLine('<!DOCTYPE html>')
    [void]$ix.AppendLine('<html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">')
    [void]$ix.AppendLine('<title>工具调试窗预览 · 索引</title>')
    [void]$ix.AppendLine('<style>' + $css + '</style></head><body><div class="pagewrap">')
    $withCtrl = @($allCards | Where-Object { $_.hasControls }).Count
    $ctrlLevel = @($allCards | Where-Object { $_.level -eq 'control' }).Count
    [void]$ix.AppendLine('<h1 style="font-size:20px;margin:0 0 8px">工具调试窗预览</h1>')
    $ctrlTail = '，其余仍是外壳页'
    if ($ctrlLevel -eq $allCards.Count) { $ctrlTail = '（全部）' }
    [void]$ix.AppendLine('<p class="pagehead">按原件资源声明还原控件形态与顺序；不执行算法，不含运行结果。共 ' + $allCards.Count + ' 个工具，' + $withCtrl + ' 个有控件声明，其中 <b>' + $ctrlLevel + '</b> 个已升到控件级' + $ctrlTail + '。</p>')
    [void]$ix.AppendLine('<div style="margin:12px 0 4px"><input id="q" type="search" placeholder="搜索工具名 / 模块 ID…" style="width:320px;height:32px;border:1px solid #cfd3d8;border-radius:6px;padding:0 10px;font:13px &quot;Microsoft YaHei&quot;,sans-serif"><span id="qhint" style="margin-left:10px;font-size:12px;color:#6b7280"></span></div>')
    [void]$ix.AppendLine('<div class="note" style="margin:0 0 16px"><b>边界</b>：控件形态由 <code>uiControls[].type</code> 推断，页签归属与像素级样式未取证。与 <code>docs/07-工具逆向/</code> 的逆向证据卡口径不同，不互相替代。<br><b>控件显示 0</b> 表示原件 <code>AlgorithmTabRoot</code> 里没有控件别名声明（如全局变量、计算器脚本编辑器、协议组装），界面由内嵌复合控件或代码构建，要单独读反编译 XAML 才能还原，不能靠资源声明批量出。</div>')
    foreach ($grp in ($allCards | Group-Object category | Sort-Object Name)) {
        [void]$ix.AppendLine('<div class="grpbox">')
        [void]$ix.AppendLine('<h2 style="font-size:15px;margin:18px 0 8px;border-bottom:1px solid var(--line);padding-bottom:6px">' + (Encode-Html $grp.Name) + '<span style="font-weight:400;color:var(--muted);font-size:12px"> · ' + $grp.Count + '</span></h2>')
        [void]$ix.AppendLine('<table style="width:100%;border-collapse:collapse;font-size:12px"><thead><tr><th style="text-align:left;padding:6px 8px;background:#f5f6f8">工具</th><th style="text-align:left;padding:6px 8px;background:#f5f6f8">模块</th><th style="text-align:left;padding:6px 8px;background:#f5f6f8">控件</th><th style="text-align:left;padding:6px 8px;background:#f5f6f8">预览</th></tr></thead><tbody>')
        foreach ($c in ($grp.Group | Sort-Object displayName)) {
            if ($c.level -eq 'control') {
                $link = '<a href="preview/' + (Encode-Html $c.page) + '">控件级预览</a>'
            }
            elseif ($c.exists) {
                $link = '<a href="preview/' + (Encode-Html $c.page) + '" style="color:#8a8f98">外壳预览（待升级）</a>'
            }
            else {
                # 没有预览文件就不给入口，避免点了 404
                $link = '<span style="color:#b0b4bb">无预览</span>'
            }
            [void]$ix.AppendLine('<tr><td style="padding:5px 8px;border-bottom:1px solid #eef0f2">' + (Encode-Html $c.displayName) + '</td><td style="padding:5px 8px;border-bottom:1px solid #eef0f2;color:#6b7280">' + (Encode-Html $c.toolId) + '</td><td style="padding:5px 8px;border-bottom:1px solid #eef0f2">' + $c.controlCount + '</td><td style="padding:5px 8px;border-bottom:1px solid #eef0f2">' + $link + '</td></tr>')
        }
        [void]$ix.AppendLine('</tbody></table>')
        [void]$ix.AppendLine('</div>')
    }
    [void]$ix.AppendLine('<p class="pagehead" style="margin-top:20px"><a href="../07-工具逆向/index.html">逆向证据库索引</a></p>')
    # 搜索：纯前端过滤，file:// 直接打开可用，不需要本地服务
    [void]$ix.AppendLine('<script>')
    [void]$ix.AppendLine('(function(){var q=document.getElementById("q"),h=document.getElementById("qhint");if(!q)return;')
    [void]$ix.AppendLine('q.addEventListener("input",function(){var v=this.value.trim().toLowerCase(),shown=0,total=0;')
    [void]$ix.AppendLine('document.querySelectorAll(".grpbox").forEach(function(box){var any=false;')
    [void]$ix.AppendLine('box.querySelectorAll("tbody tr").forEach(function(tr){total++;var hit=!v||tr.textContent.toLowerCase().indexOf(v)>=0;tr.style.display=hit?"":"none";if(hit){any=true;shown++}});')
    [void]$ix.AppendLine('box.style.display=(!v||any)?"":"none"});')
    [void]$ix.AppendLine('if(v){h.textContent="命中 "+shown+" / "+(total||0)}else{h.textContent=""}});')
    [void]$ix.AppendLine('})();')
    [void]$ix.AppendLine('</script>')
    [void]$ix.AppendLine('</div></body></html>')
    [System.IO.File]::WriteAllText((Join-Path $outRoot 'index.html'), $ix.ToString(), $utf8)
    Write-Host ("索引已重建: {0}" -f (Join-Path $outRoot 'index.html'))
}
else {
    Write-Host '单工具/少量模式：跳过索引重建，避免索引只列部分工具。' -ForegroundColor Yellow
}

Write-Host ''
Write-Host '================ 调试窗预览生成 ================' -ForegroundColor Cyan
Write-Host ("生成页面: {0}" -f $generated.Count)
foreach ($g in $generated) {
    Write-Host ("  {0,-42} 控件 {1,3}  -> {2}" -f $g.toolId, $g.controlCount, $g.page)
}
Write-Host ''
