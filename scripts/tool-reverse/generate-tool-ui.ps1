#requires -Version 5.1
<#
.SYNOPSIS
    按原件 AlgorithmTab.xml + 真实控件 XAML 生成「工具参数面板」的 HTML 预览。

.DESCRIPTION
    还原方式（与旧版的关键差别）：
      不再是「XML 节点名 → 自造一个输入框」，而是按原件控件体系的真实构成逐层复刻：

      面板骨架   Apps.XmlUI/ParamsTabUserControl.xaml + ParamListView.xaml + ParamCategoryUI.xaml
      行容器     Apps.XmlUI.ParamTab/LeftRightControl.xaml   左列 ChildLeftWidth=90(中文) + 右列 *
      类型分发表 styles/paramusercontroldatatemplate.xaml    类型 → 真实 DataTemplate
      控件样式   Apps.Style/{buttonstyle,radiobuttonstyle,comboboxstyle,textboxstyle,...}
      主题色     Apps.ColorFun/colorstyle1.xaml              例如 Col_Theme=#E76900、标签#666666
      图标       Apps.XmlUI/resources 与 Apps.Style/resources 的原件 PNG，逐张复制到预览站

    即：Boolean 用 swith_on/off.png 真图，单选是 24px 的 check-box_off/on.png，
    ROI 形状是 36x34 的图标按钮（每种形状对应原件 ROI_*.png），
    数值是 NumericControl（1px 边框 + 右侧 20px 上下箭头），
    ImageViewConfig 是 32x32 眼睛图标 + 颜色弹层按钮，
    共用组 GroupLinkItem 按 Group.xml 就地展开。

    显隐：CustomVisible 静态值 + 当前取值命中的 Triggers（VisibleOperation/HiddenOperation）求初始状态。

    边界：Triggers 里依赖运行期外部状态的显隐、弹层内容、滑杆展开态不还原；不执行算法。

.PARAMETER Root
    证据库根目录，默认 docs/07-工具逆向。

.PARAMETER Tool
    只生成指定工具卡（可多个或逗号分隔）。

.PARAMETER OutDir
    输出目录，默认 docs/08-工具预览/ui。

.PARAMETER NoIndex
    不重建 ui/index.html。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/tool-reverse/generate-tool-ui.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/tool-reverse/generate-tool-ui.ps1 -Tool imvsblobfindmodu
#>
[CmdletBinding()]
param(
    [string]$Root,
    [string[]]$Tool,
    [string]$OutDir,
    [switch]$NoIndex
)

$ErrorActionPreference = 'Stop'

if ($Tool) {
    $Tool = @($Tool | ForEach-Object { $_ -split '[,;]' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}
if (-not $Root) { $Root = Join-Path $PSScriptRoot '..\..\docs\07-工具逆向' }
if (-not (Test-Path -LiteralPath $Root)) { throw "证据库根目录不存在: $Root" }
$root = (Resolve-Path -LiteralPath $Root).Path
$toolsDir = Join-Path $root 'tools'
if (-not (Test-Path -LiteralPath $toolsDir)) { throw "工具卡目录不存在: $toolsDir" }
if (-not $OutDir) { $OutDir = Join-Path $root '..\08-工具预览\ui' }
$outRoot = [System.IO.Path]::GetFullPath($OutDir)
if (-not (Test-Path -LiteralPath $outRoot)) { New-Item -ItemType Directory -Path $outRoot -Force | Out-Null }
$iconOut = Join-Path $outRoot 'assets\icons'
if (-not (Test-Path -LiteralPath $iconOut)) { New-Item -ItemType Directory -Path $iconOut -Force | Out-Null }
$utf8 = New-Object System.Text.UTF8Encoding($false)

# ---------------------------------------------------------------- 原件度量（取自 XAML）
$script:M = [ordered]@{
    LabelWidth      = 90      # LeftWidthConverter：中文 90，英文 170
    RowHeight       = 32      # LeftRightControl Height / RadioButton Height
    FontSize        = 14      # LeftRightControl TextBlock.FontSize / NormalRadioButton
    ColorLabel      = '#666666'   # Col_ModuleConfig_List_Foreground
    ColorTitle      = '#3D3D3D'   # Col_ModuleConfig_Title_Foreground
    ColorDivider    = '#E0DFDE'   # ParamGroupItemStyle Rectangle Fill
    ColorPanel      = '#F2F3F5'   # Col_SubForm_Background
    ColorTheme      = '#E76900'   # Col_Theme
    ColorComboBorder = '#CCC7C6'  # Col_ModuleConfig_ComboBox_BorderBrush
    ColorComboBg    = '#FFFFFF'   # Col_ModuleConfig_ComboBox_Background
    ComboHeight     = 30      # ListViewComboBoxStyle Height
    RadioIcon       = 24      # NormalRadioButton 图标列
    ExpandHeight    = 22      # ExpandButtonStyle Border Height
    RoiBtnW         = 36      # fullscreenRoiBtnStyle Width
    RoiBtnH         = 34
    RoiBtnGap       = 27      # Margin 0,0,27,0
    EyeSize         = 32      # eyeToggleButton
}

# ---------------------------------------------------------------- 语言字典
$script:lang = @{}
$langPaths = @(
    (Join-Path $root 'decompiled\Lang\zh-cn.xaml'),
    (Join-Path $root 'decompiled\Lang\gateway-zh-cn.xaml'),
    (Join-Path $root 'decompiled\HostShell\ExeEntry\resources\zh-cn.xaml')
)
$langLoaded = @()
foreach ($p in $langPaths) {
    if (-not (Test-Path -LiteralPath $p)) { continue }
    $raw = [System.IO.File]::ReadAllText($p, $utf8)
    $added = 0
    foreach ($m in [regex]::Matches($raw, '<sys:String\s+x:Key="([^"]+)"\s*>([^<]*)</sys:String>')) {
        $k = $m.Groups[1].Value
        if (-not $script:lang.ContainsKey($k)) { $script:lang[$k] = $m.Groups[2].Value; $added++ }
    }
    $langLoaded += [pscustomobject]@{ File = (Split-Path $p -Leaf); Added = $added }
}
if (@($script:lang.Keys).Count -eq 0) { throw '未加载到任何语言字典，无法生成中文标签' }

function Get-Text {
    param([string]$Key)
    if (-not $Key) { return '' }
    $k = $Key.Trim()
    if ($script:lang.ContainsKey($k)) { return $script:lang[$k] }
    return $k
}

function Test-RawLabel {
    param([string]$Key)
    if (-not $Key) { return $false }
    return (-not $script:lang.ContainsKey($Key.Trim()))
}

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

function Get-NodeText {
    param([object]$Node, [string]$Path)
    if (-not $Node) { return '' }
    $n = $Node.SelectSingleNode($Path)
    if (-not $n) { return '' }
    return ([string]$n.InnerText).Trim()
}

function Get-NumberText {
    param([string]$Value)
    if (-not $Value) { return '' }
    $v = $Value.Trim()
    if ($v -match '^0[xX][0-9a-fA-F]+$') {
        try { return ([Convert]::ToInt64($v, 16)).ToString() } catch { return $v }
    }
    return $v
}

function Test-ValueEqual {
    param([string]$A, [string]$B)
    if ($A -eq $B) { return $true }
    $da = 0.0; $db = 0.0
    $okA = [double]::TryParse((Get-NumberText $A), [ref]$da)
    $okB = [double]::TryParse((Get-NumberText $B), [ref]$db)
    if ($okA -and $okB) { return ([math]::Abs($da - $db) -lt 1e-9) }
    return $false
}

# ---------------------------------------------------------------- 图标（原件 PNG）
$script:iconDirs = @(
    (Join-Path $root 'decompiled\XmlUi\resources'),
    (Join-Path $root 'decompiled\AppsStyle\resources')
)
$script:iconCache = @{}
$script:iconMissing = @{}
$script:iconCopied = 0

function Get-IconUrl {
    param([string]$Name)
    if (-not $Name) { return '' }
    if ($script:iconCache.ContainsKey($Name)) { return $script:iconCache[$Name] }
    foreach ($d in $script:iconDirs) {
        if (-not (Test-Path -LiteralPath $d)) { continue }
        $p = Join-Path $d $Name
        if (Test-Path -LiteralPath $p) {
            Copy-Item -LiteralPath $p -Destination (Join-Path $iconOut $Name) -Force
            $script:iconCopied++
            $url = 'assets/icons/' + $Name
            $script:iconCache[$Name] = $url
            return $url
        }
    }
    $script:iconMissing[$Name] = $true
    $script:iconCache[$Name] = ''
    return ''
}

function New-IconImg {
    param([string]$Name, [int]$W, [int]$H, [string]$Alt, [string]$Class)
    $url = Get-IconUrl $Name
    if (-not $url) { return '<span class="iconmiss" title="原件图标缺失: ' + (Encode-Html $Name) + '">?</span>' }
    $cls = ''
    if ($Class) { $cls = ' class="' + $Class + '"' }
    return '<img' + $cls + ' src="' + $url + '" width="' + $W + '" height="' + $H + '" alt="' + (Encode-Html $Alt) + '">'
}

# ---------------------------------------------------------------- 共用组
$script:groups = @{}
$groupPath = Join-Path $root 'decompiled\Groups\Group.xml'
$groupCount = 0
if (Test-Path -LiteralPath $groupPath) {
    $gx = [xml]([System.IO.File]::ReadAllText($groupPath, $utf8))
    foreach ($g in $gx.SelectNodes('//GroupInfoItem')) {
        $gn = [string]$g.GetAttribute('Name')
        if ($gn -and -not $script:groups.ContainsKey($gn)) { $script:groups[$gn] = $g; $groupCount++ }
    }
}

# ---------------------------------------------------------------- ROI 形状 → 原件图标
# 图标名取自 Apps.Style/buttonstyle/togglebuttonstyle.xaml 的各 *RoiBtnStyle
$script:roiShape = [ordered]@{
    FullScreen     = @{ Text = '全屏'; Icon = 'ROI_full-screen_normal.png' }
    SectorRing     = @{ Text = '环形扇'; Icon = 'ROI_circle_normal.png' }
    Sector         = @{ Text = '扇形'; Icon = 'ROI_circle_normal.png' }
    OverSector     = @{ Text = '超扇'; Icon = 'ROI_circle_normal.png' }
    Box            = @{ Text = '矩形'; Icon = 'ROI_retangle_normal.png' }
    UroBox         = @{ Text = '旋转矩形'; Icon = 'ROI_retangle_normal.png' }
    LineBox        = @{ Text = '线阵矩形'; Icon = 'ROI_retangle_normal.png' }
    SeqBox         = @{ Text = '序列矩形'; Icon = 'ROI_retangle_normal.png' }
    DoubleBox      = @{ Text = '双矩形'; Icon = 'ROI_DoubleBox_normal.png' }
    QuadraBox      = @{ Text = '四边形'; Icon = 'ROI_QuadraBox_click.png' }
    Polygon        = @{ Text = '多边形'; Icon = 'ROI_polygon_normal.png' }
    LineCaliper    = @{ Text = '直线卡尺'; Icon = 'ROI_retangle_normal.png' }
    CircleCaliper  = @{ Text = '圆卡尺'; Icon = 'ROI_circle_normal.png' }
    SectorCaliper  = @{ Text = '扇环卡尺'; Icon = 'ROI_circle_normal.png' }
    PolygonCaliper = @{ Text = '多边形卡尺'; Icon = 'ROI_polygon_normal.png' }
    Point          = @{ Text = '点'; Icon = 'Geometry_point_clickoff.png' }
    Line           = @{ Text = '直线'; Icon = 'Geometry_line_clickoff.png' }
    Ellipse        = @{ Text = '椭圆'; Icon = 'Geometry_circle_clickoff.png' }
    CircleMes      = @{ Text = '圆测量'; Icon = 'Geometry_circle_clickoff.png' }
    MatrixCircle   = @{ Text = '矩阵圆'; Icon = 'ROI_circle_normal.png' }
    Coordinate     = @{ Text = '坐标系'; Icon = 'Geometry_point_clickoff.png' }
    Box3D          = @{ Text = '3D矩形'; Icon = 'ROI_retangle_normal.png' }
}

# ---------------------------------------------------------------- 空模板类型
$script:emptyTpl = @{}
$tplPath = Join-Path $root 'decompiled\XmlUi\styles\paramusercontroldatatemplate.xaml'
if (Test-Path -LiteralPath $tplPath) {
    $tplRaw = [System.IO.File]::ReadAllText($tplPath, $utf8)
    foreach ($m in [regex]::Matches($tplRaw, '<DataTemplate\s+x:Key="([^"]+)"\s*/>')) { $script:emptyTpl[$m.Groups[1].Value] = $true }
    foreach ($m in [regex]::Matches($tplRaw, '<DataTemplate\s+x:Key="([^"]+)"\s*>\s*</DataTemplate>')) { $script:emptyTpl[$m.Groups[1].Value] = $true }
}

# ---------------------------------------------------------------- 显隐状态求值
$script:vis = @{}
$script:enabled = @{}

function Initialize-ParamState {
    param([object]$RootNode)

    $script:vis = @{}
    $script:enabled = @{}
    $named = @($RootNode.SelectNodes('//*[@Name]'))

    foreach ($n in $named) {
        $nm = [string]$n.GetAttribute('Name')
        if (-not $nm) { continue }
        $cv = Get-NodeText $n 'CustomVisible'
        $v = $true
        if ($cv -and $cv -ieq 'False') { $v = $false }
        $en = Get-NodeText $n 'Enable'
        $e = $true
        if ($en -and $en -ieq 'False') { $e = $false }
        if (-not $script:vis.ContainsKey($nm)) { $script:vis[$nm] = $v } else { $script:vis[$nm] = ($script:vis[$nm] -and $v) }
        $script:enabled[$nm] = $e
    }

    $owners = New-Object 'System.Collections.Generic.List[object]'
    foreach ($n in $named) { if ($n.SelectSingleNode('Triggers/Trigger')) { $owners.Add($n) } }
    foreach ($g in $script:groups.Values) {
        foreach ($n in $g.SelectNodes('.//*[@Name]')) { if ($n.SelectSingleNode('Triggers/Trigger')) { $owners.Add($n) } }
    }

    for ($pass = 0; $pass -lt 3; $pass++) {
        $changed = $false
        foreach ($n in $owners) {
            $triggers = $n.SelectNodes('Triggers/Trigger')
            if (-not $triggers -or $triggers.Count -eq 0) { continue }
            $cur = Get-NodeText $n 'CurValue'
            if (-not $cur) { $cur = Get-NodeText $n 'DefaultValue' }
            if (-not $cur) {
                if ($n.LocalName -match '^ROISelecter') {
                    $selTokens = Get-NodeText $n 'ROISelection'
                    if ($selTokens) { $cur = @($selTokens -split '\s+' | Where-Object { $_ })[0] }
                }
                elseif ($n.LocalName -match '^(RadioSelecter|Enumeration|Enumeraion)') {
                    $firstEntry = $n.SelectSingleNode('EnumEntrys/EnumEntry')
                    if ($firstEntry) { $cur = Get-NodeText $firstEntry 'Value' }
                }
            }
            foreach ($tr in $triggers) {
                $prop = Get-NodeText $tr 'Property'
                if ($prop -ne 'CurValue') { continue }
                $val = Get-NodeText $tr 'Value'
                if (-not $val -or -not $cur) { continue }
                if (-not (Test-ValueEqual $val $cur)) { continue }
                foreach ($st in $tr.SelectNodes('Setters/Setter')) {
                    $target = Get-NodeText $st 'TargetName'
                    $op = Get-NodeText $st 'OperationName'
                    if (-not $target -or -not $op) { continue }
                    if (-not $script:vis.ContainsKey($target)) { continue }
                    if ($op -match 'HiddenOperation') {
                        if ($script:vis[$target]) { $script:vis[$target] = $false; $changed = $true }
                    }
                    elseif ($op -match 'VisibleOperation') {
                        if (-not $script:vis[$target]) { $script:vis[$target] = $true; $changed = $true }
                    }
                }
            }
        }
        if (-not $changed) { break }
    }
}

# ---------------------------------------------------------------- 类型 → 真实控件族
function Get-ControlFamily {
    param([string]$Type)

    if (-not $Type) { return 'unknown' }
    if ($script:emptyTpl.ContainsKey($Type)) { return 'empty' }
    if ($Type -eq 'GroupLinkItem') { return 'group' }
    if ($Type -eq 'Expand') { return 'expand' }
    if ($Type -eq 'HeadBar') { return 'headbar' }
    if ($Type -eq 'TextNormal') { return 'text' }
    if ($Type -match '^ROISelecter') { return 'roi' }
    if ($Type -match '^RadioSelecter') { return 'radio' }
    if ($Type -match 'Bettween') { return 'range' }
    if ($Type -match '^ImageViewConfig') { return 'display' }
    if ($Type -match '^BooleanCheck') { return 'check' }
    if ($Type -match '^Boolean') { return 'switch' }
    if ($Type -match '^(Enumeration|Enumeraion|ComboxEdit|CommandSelecter|EnumerationBx|EnumerationG|EnumerationLx)') { return 'combo' }
    if ($Type -match '^(Integer|Float|Double|NumberMultiply|ImgStitchInteger|EditButton_Calib)') { return 'numeric' }
    if ($Type -match '^(String|PasswordString|IPString|StringName|StringBx|StringImgName)') { return 'string' }
    if ($Type -match '^(OpenFile|OpenFolder|OpenDialog|SaveFileDialog|OpenFileFor)') { return 'path' }
    if ($Type -match '^ButtonSelecter') { return 'select' }
    if ($Type -match '^(PointPair|PointSet|LinePair)$') { return 'pointpair' }
    if ($Type -match '^(GlobalVarItem|QueueItem|SaveTextItem|AddButton|AddList|ListParam|ProtocolOutputParam|ModbusItem|PLCItem|CommunicationItem|GateWaySendEventItem|IOSendData|BasicItemsSend|Combination)') { return 'list' }
    if ($Type -match '^Command') { return 'command' }

    # 工具自带复合控件：界面在工具控件程序集里，不在公共模板字典中。
    # 结构与绑定依据见 decompiled/ModuleControls/<工具>/*Panel.xaml，逐条取自原件 XAML。
    if ($Type -eq 'ThresholdAreaList') { return 'c-threshold' }
    if ($Type -eq 'ImageSourceList') { return 'c-imgsrc' }
    if ($Type -eq 'PixelModuRangeConfigList') { return 'c-rangecfg' }
    if ($Type -eq 'TimeConfig') { return 'c-timecfg' }
    if ($Type -match '^(Panel_EdgepatMatch|Panel_EdgePairpatMatch|PathDrawingGuide)$') { return 'c-model' }
    if ($Type -eq 'ReadFileConfigData') { return 'c-filecfg' }
    if ($Type -eq 'ColorConfigEx') { return 'c-color' }
    return 'unknown'
}

# ---------------------------------------------------------------- 样式
$css = @'
:root{
  --label:#666666;--title:#3D3D3D;--divider:#E0DFDE;--panel:#F2F3F5;--theme:#E76900;
  --combo-bd:#CCC7C6;--combo-bg:#FFFFFF;--bd:#CCC7C6;--muted:#9AA0A6;
  --row:32px;--labelw:90px;--fs:14px;
}
*{box-sizing:border-box}
body{margin:0;background:#DEE1E6;color:#333;font:14px/1.4 "Microsoft YaHei","Segoe UI",sans-serif}
.page{max-width:1000px;margin:0 auto;padding:18px 16px 40px}
.cap{font-size:12px;color:#5B6472;margin:0 0 10px}
.cap a{color:#2563eb;text-decoration:none}
.cap code{background:#EEF0F3;border:1px solid #E1E4E9;border-radius:3px;padding:0 4px;font-size:11px}
.cap .sp{color:#98A0AA}

.win{width:420px;background:#fff;border:1px solid #B9BEC5;border-radius:3px;box-shadow:0 8px 26px rgba(0,0,0,.18);overflow:hidden}
.win .tb{height:30px;background:#3D3D3D;display:flex;align-items:center;gap:7px;padding:0 8px}
.win .tb i{font-style:normal;color:#FF8C1A;font-weight:700;font-size:13px}
.win .tb b{color:#fff;font-size:13px;font-weight:700}
.win .tb .q{width:14px;height:14px;border-radius:50%;border:1px solid #FF8C1A;color:#FF8C1A;font-size:10px;line-height:12px;text-align:center}
.win .tb .x{margin-left:auto;color:#D5D8DD;font-size:13px;line-height:1}

.tabs{display:flex;height:28px;background:#fff;border-bottom:1px solid var(--divider);padding-left:6px;overflow:hidden}
.tabs .tab{position:relative;display:flex;align-items:center;padding:0 14px;font-size:14px;color:#666;cursor:pointer;white-space:nowrap}
.tabs .tab.on{color:#1F2733;font-weight:700}
.tabs .tab.on:after{content:"";position:absolute;left:6px;right:6px;bottom:0;height:2px;background:var(--theme)}

.pane{display:none;background:var(--panel);padding:2px 8px 8px;overflow:auto}
.pane.on{display:block}

.cat{margin:0 0 5px}
.cat .ln{height:1px;background:var(--divider);margin-right:18px}
.cat h3{margin:20px 0 0;font-size:var(--fs);font-weight:700;color:var(--title);line-height:20px}
.cat.nohd .ln{display:none}
.cat.nohd h3{display:none}

.row{display:flex;align-items:center;min-height:var(--row)}
.row>.lb{flex:0 0 var(--labelw);width:var(--labelw);font-size:var(--fs);color:var(--label);overflow:hidden;text-overflow:ellipsis;white-space:nowrap;padding-right:2px}
.row>.lb.raw{border-bottom:1px dotted #E5484D}
.row>.ct{flex:1 1 auto;min-width:0;display:flex;align-items:center;gap:0}
.row.hid{display:none}
body.showhid .row.hid{display:flex;opacity:.55}
.row.dis>.lb{color:#B4B8BF}
.ct.full{margin-left:2px}

/* Boolean：原件 swith_on/off.png */
.sw{width:32px;height:16px}
/* BooleanCheck：原件 check2-box_on/off.png 置于左列 30px */
.row.chkrow>.lb{flex:0 0 auto;width:auto;padding-left:2px}
.row.chkrow>.ct{flex:0 0 30px;width:30px;justify-content:flex-start}

/* RadioSelecter：24px check-box_off/on.png + 3px 间距 + 文字 */
.radio{display:flex;align-items:center;height:var(--row);margin-right:2px;color:var(--label);font-size:var(--fs);white-space:nowrap}
.radio img{margin-right:3px}

/* 数值：NumericControl */
.num{display:flex;align-items:center;flex:1 1 auto;min-width:0;height:28px;border:1px solid var(--bd);border-radius:2px;background:#fff}
.num .v{flex:1 1 auto;min-width:0;border:0;background:transparent;font:var(--fs) "Microsoft YaHei";color:var(--label);padding:0 4px 0 7px;height:26px;outline:none}
.num .sp{flex:0 0 20px;width:20px;height:26px;border-left:1px solid #E9E9E9;display:flex;flex-direction:column}
.num .sp span{flex:1;display:flex;align-items:center;justify-content:center}
.num .sp b{width:0;height:0;border-left:4px solid transparent;border-right:4px solid transparent;display:block}
.num .sp .up b{border-bottom:5px solid #5A5A5A}
.num .sp .dn b{border-top:5px solid #5A5A5A}

/* 关联（ButtonSelecter_sub 右列 32px） */
.link{flex:0 0 32px;width:32px;height:32px;margin-left:2px;border:1px solid var(--bd);border-radius:2px;background:#FBFBFC;display:flex;align-items:center;justify-content:center;padding:0}

/* ComboBox */
.combo{display:flex;align-items:center;flex:1 1 auto;min-width:0;height:30px;border:1px solid var(--combo-bd);border-radius:2px;background:var(--combo-bg);position:relative}
.combo .v{flex:1 1 auto;min-width:0;padding:0 22px 0 6px;font-size:var(--fs);color:var(--label);overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.combo img{position:absolute;right:2px;bottom:1px}

/* 文本 */
.txt{display:flex;align-items:center;flex:1 1 auto;min-width:0;height:28px;border:1px solid var(--bd);border-radius:2px;background:#fff}
.txt .v{flex:1;border:0;background:transparent;font:var(--fs) "Microsoft YaHei";color:var(--label);padding:0 6px;height:26px;outline:none;min-width:0}

/* 颜色弹层按钮 (ImageViewConfig 第二列) */
.colorbtn{flex:0 0 24px;width:24px;height:24px;border:1px solid var(--bd);border-radius:2px;background:linear-gradient(135deg,#ff7900 0 33%,#3b82f6 33% 66%,#22c55e 66% 100%)}

/* ROI 形状按钮：按钮 36x34；间距按原件面板截图实测约 6px（样式文件里的 27px 是 ROI 工具栏变体） */
.roiwrap{display:flex;flex-wrap:nowrap;align-items:center;gap:0;overflow:hidden}
.roibtn{flex:0 0 36px;width:36px;height:34px;margin-right:6px;border:0;background:transparent;padding:0;display:flex;align-items:center;justify-content:center}
.roibtn.on{background:#fff3e6;outline:1px solid var(--theme)}
.roimax{font-size:12px;color:var(--muted)}

/* 高级参数 / ROI参数 折叠按钮：ExpandButtonStyle 高 22 */
.expander{display:flex;justify-content:flex-end;padding-right:40px;height:30px;align-items:center}
.expander button{display:flex;align-items:center;gap:0;height:22px;border:1px solid transparent;border-radius:1px;background:transparent;font:12px "Microsoft YaHei";color:#808080;padding:0 6px 0 6px}

/* HeadBar：30 高列头 */
.headbar{display:flex;align-items:center;height:30px;padding-left:12px;font-size:var(--fs);font-weight:700;color:#666}

/* PointPair 三行网格 */
.pp{margin:2px 0}
.pp .h{display:flex;align-items:center;gap:4px;height:32px;padding-left:18px;font-size:var(--fs);color:#666}
.pp .grid{display:grid;grid-template-columns:140px 18px 140px;align-items:center;row-gap:5px}
.pp .grid .lbx{font-size:14px;color:#666;text-align:right}
.pp .grid .sep{height:1px;background:#CECCCB;margin:0 3px}

/* ---------------------------------------------------------------------------
   工具自带复合控件：尺寸/列宽/底色取自原件 ModuleControls/*Panel.xaml
   （原有 9 种在 XML 里只有一行声明，界面在工具自己的控件程序集里）
   --------------------------------------------------------------------------- */
.cmp{width:100%;margin:2px 0 6px;border:1px solid var(--divider);border-radius:2px;background:#fff;overflow:hidden}
.cmp>.src{display:flex;align-items:center;gap:8px;flex-wrap:wrap;background:#F7F8FA;border-bottom:1px solid var(--divider);padding:4px 8px;font-size:11px;color:#5B6472}
.cmp>.src code{background:#EEF0F3;border:1px solid #E1E4E9;border-radius:3px;padding:0 4px;font-size:11px}
.cmp>.src .nt{color:#98A0AA}
.cmp .ft{display:flex;align-items:center;gap:6px;padding:6px 8px}
.cmp .ft .ib{display:inline-flex;align-items:center;justify-content:center;width:24px;height:24px}
.cmp .sep{flex:0 0 12px;text-align:center;color:#808080}
.cmp .hint{padding:12px;font-size:12px;color:#98A0AA;line-height:1.7}

/* ThresholdAreaList：表头 1.2* / 1.5* / 4.8* / 1.6*，行高 32，末列删除 */
.cth{padding:2px 8px 4px}
.cth .cr{display:grid;grid-template-columns:1.2fr 1.5fr 4.8fr 1.6fr 28px;align-items:center;min-height:34px;column-gap:6px}
.cth .cr.hd{min-height:30px;font-size:14px;color:#666}
.cth .cr>.c0{font-size:14px;color:#666}
.cth .cr>.c1,.cth .cr>.c2{display:flex;align-items:center;min-width:0}
.cth .cr>.c3{display:flex;align-items:center;justify-content:center}

/* ImageSourceList：固定高 133 的滚动区，行 = 90px 标签 + 下拉（原件 Margin 0,0,-12,0） */
.cimg{height:133px;overflow:auto;padding:4px 8px}
.cimg .rowline{display:flex;align-items:center;height:32px}
.cimg .rowline .lbx{flex:0 0 90px;width:90px;font-size:14px;color:#666}
.cimg .rowline .combo{flex:1 1 auto;margin-right:-12px}

/* PixelModuRangeConfigList：灰底 #f2f3f5，标题 32 高，列表 MaxHeight 233，底部 add/delete */
.crg{background:#f2f3f5;padding:10px}
.crg .ttl{height:32px;line-height:32px;padding-left:5px;font-size:14px;font-weight:700;color:#3D3D3D}
.crg .lst{max-height:233px;overflow:auto;background:#fff;border:1px solid var(--divider);border-radius:2px;min-height:64px}
.crg .ft{padding:4px 0 0}

/* TimeConfig：80px 名称 + 两个 200x32 下拉 + 50px 删除，底部新增 */
.ctm{padding:4px 8px}
.ctm .r{display:grid;grid-template-columns:80px 1fr 50px;align-items:center;min-height:72px;column-gap:4px}
.ctm .r .nm{font-size:14px;color:#4C4C4C}
.ctm .r .sel{display:flex;flex-direction:column;gap:4px;align-items:center}
.ctm .r .sel .combo{width:200px;height:32px}
.ctm .r .del{display:flex;align-items:center;justify-content:center}

/* 模型类面板（PathDrawingGuide / Panel_EdgepatMatch / Panel_EdgePairpatMatch）：工具条 + 1px 边框示意区 */
.cmd .tb{display:flex;align-items:center;gap:6px;min-height:44px}
.cmd .tb .btn{display:inline-flex;align-items:center;gap:4px;height:34px;padding:0 10px;border:1px solid var(--bd);border-radius:2px;background:#fff;font-size:14px;color:#666}
.cmd .tb .btn.r{margin-left:auto;height:26px;font-size:13px}
.cmd .tb .ic{display:inline-flex;align-items:center;gap:2px}
.cmd .cv{display:flex;flex-direction:column;align-items:center;justify-content:center;gap:6px;min-height:120px;background:#FCFCFC;border:1px solid #E0DFDE;border-radius:1px;font-size:12px;color:#98A0AA}

/* ReadFileConfigData：单 ListBox，行形态按 DisplayType 动态选模板 */
.cfl{padding:6px 8px}
.cfl .lst{background:#fff;border:1px solid var(--divider);border-radius:2px;min-height:72px}

/* 文本型提示 */
.note-line{margin:6px 0;color:#999;font-size:13px}
.unknown{margin:2px 0;padding:2px 6px;border-left:3px solid #E5484D;background:#FDF0F0;color:#8A1F1F;font-size:12px}
.emptyctl{margin:2px 0;padding:2px 6px;border-left:3px solid #B4B8BF;background:#EDEFF2;color:#5B6472;font-size:12px}
/* 我方主动不采用的原件控件（厂商专属） */
.notadopted{margin:2px 0;padding:2px 6px;border-left:3px solid #93C5FD;background:#EFF6FF;color:#1E3A8A;font-size:12px;line-height:1.6}
.notadopted b{color:#1D4ED8}
.notadopted .rp{color:#3B82F6}

/* 底部命令条 */
.foot{display:flex;align-items:center;justify-content:space-between;gap:6px;padding:5px 8px;background:#F2F3F5;border-top:1px solid var(--divider)}
.foot .l,.foot .r{display:flex;gap:5px}
.foot button{height:26px;min-width:64px;padding:0 12px;border:1px solid #CCC7C6;border-radius:2px;background:#fff;font:13px "Microsoft YaHei";color:#666}
.foot .r button:last-child{border-color:var(--theme);color:var(--theme)}
.nofoot{font-size:12px;color:var(--muted);padding:4px 10px;background:#F2F3F5;border-top:1px solid var(--divider)}

/* 图例与说明 */
.meta{font-size:12px;color:#5B6472;margin:10px 0 0}
.meta b{color:#333}
/* 与原件形态不同的自研设计（相机等品类） */
.diff{margin:0 0 10px;max-width:820px;border-left:3px solid #2563EB;background:#EFF6FF;color:#1E3A8A;font-size:12px;padding:9px 12px;border-radius:0 4px 4px 0;line-height:1.75}
.diff b{color:#1E40AF}
.diff ul{margin:6px 0 0;padding-left:18px}
.diff li{margin:2px 0}
details{margin-top:10px;background:#fff;border:1px solid #D6DAE0;border-radius:4px;padding:8px 12px}
details summary{cursor:pointer;font-size:13px;color:#374151;font-weight:600}
details ul{margin:8px 0 0;padding-left:18px}
details li{font-size:12px;color:#4B5563;line-height:1.8}
details code{background:#F1F3F5;border:1px solid #E3E6EA;border-radius:3px;padding:0 3px;font-size:11px}
.iconmiss{display:inline-block;width:16px;height:16px;line-height:16px;text-align:center;border:1px dashed #E5484D;color:#E5484D;font-size:11px}
'@

$js = @'
(function(){
  var tabs=document.querySelectorAll('.tab');
  Array.prototype.forEach.call(tabs,function(t){
    t.onclick=function(){
      var idx=t.getAttribute('data-tab');
      Array.prototype.forEach.call(document.querySelectorAll('.tab'),function(x){x.classList.remove('on');});
      Array.prototype.forEach.call(document.querySelectorAll('.pane'),function(p){p.classList.toggle('on',p.getAttribute('data-tab')===idx);});
      t.classList.add('on');
    };
  });
  var cb=document.getElementById('showhid');
  if(cb){cb.onchange=function(){document.body.classList.toggle('showhid',cb.checked);};}
})();
'@

$script:stat = $null
# 当前工具卡片里登记为「我方主动不采用」的原件控件类型：type → { reason, replacement }
$script:notAdopted = @{}

function New-Row {
    param(
        [string]$Label,
        [string]$LabelKey,
        [string]$ControlHtml,
        [bool]$Hidden,
        [bool]$Disabled,
        [string]$ExtraClass
    )
    $cls = 'row'
    if ($ExtraClass) { $cls += ' ' + $ExtraClass }
    if ($Hidden) { $cls += ' hid'; $script:stat.hidden++ }
    if ($Disabled) { $cls += ' dis' }
    $lbCls = 'lb'
    if (Test-RawLabel $LabelKey) { $lbCls += ' raw'; $script:stat.raw++ }
    $title = ''
    if ($LabelKey) { $title = ' title="' + (Encode-Html $LabelKey) + '"' }
    $h = '<div class="' + $cls + '">'
    if ($Label) { $h += '<div class="' + $lbCls + '"' + $title + '>' + (Encode-Html $Label) + '</div>' }
    else { $h += '<div class="lb"></div>' }
    $h += '<div class="ct">' + $ControlHtml + '</div></div>'
    return $h
}

function Get-CurrentValue {
    param([object]$Node)
    $v = Get-NodeText $Node 'CurValue'
    if (-not $v) { $v = Get-NodeText $Node 'DefaultValue' }
    return $v
}

function Get-EnumCurrentText {
    param([object]$Node)
    $cur = Get-CurrentValue $Node
    $entries = $Node.SelectNodes('EnumEntrys/EnumEntry')
    if ($entries -and $entries.Count -gt 0 -and $cur) {
        foreach ($e in $entries) {
            if (Test-ValueEqual (Get-NodeText $e 'Value') $cur) { return (Get-Text (Get-NodeText $e 'DisplayName')) }
        }
    }
    if ($cur) { return (Get-NumberText $cur) }
    return ''
}

function New-NumericControl {
    param([string]$Value, [switch]$WithLink)
    $h = '<span class="num"><input class="v" value="' + (Encode-Html (Get-NumberText $Value)) + '" readonly tabindex="-1"><span class="sp"><span class="up"><b></b></span><span class="dn"><b></b></span></span></span>'
    if ($WithLink) {
        # combo_ic_关联_normal.png 实际 32x32，ButtonSelecter_sub 里是 32px 列
        $h += '<button class="link" title="关联/下拉">' + (New-IconImg -Name 'combo_ic_关联_normal.png' -W 32 -H 32 -Alt '关联') + '</button>'
    }
    return $h
}

# 复合控件外壳：明确写出结构来源，避免被当成凭印象画的占位
function New-CmpBlock {
    param([string]$Source, [string]$Inner, [string]$Note)
    $h = '<div class="cmp"><div class="src">结构取自原件 <code>' + (Encode-Html $Source) + '</code>'
    if ($Note) { $h += '<span class="nt">' + (Encode-Html $Note) + '</span>' }
    $h += '</div>' + $Inner + '</div>'
    return $h
}

function New-CmpCombo {
    param([string]$Text)
    return '<span class="combo"><span class="v">' + (Encode-Html $Text) + '</span>' + (New-IconImg -Name 'combo_ic_triangle_normal.png' -W 16 -H 16 -Alt '下拉') + '</span>'
}

function New-CmpRangePair {
    return (New-NumericControl -Value '') + '<span class="sep">~</span>' + (New-NumericControl -Value '')
}

function Render-Node {
    param([object]$Node, [int]$Depth)

    $type = $Node.LocalName
    $name = [string]$Node.GetAttribute('Name')
    $disp = Get-NodeText $Node 'DisplayName'
    if (-not $disp) { $disp = Get-NodeText $Node 'Description' }
    $labelKey = $disp
    if (-not $labelKey) { $labelKey = $name }
    $label = Get-Text $labelKey

    $hidden = $false
    $cv = Get-NodeText $Node 'CustomVisible'
    if ($cv -and $cv -ieq 'False') { $hidden = $true }
    if ($name -and $script:vis.ContainsKey($name)) { $hidden = -not $script:vis[$name] }
    $disabled = $false
    $en = Get-NodeText $Node 'Enable'
    if ($en -and $en -ieq 'False') { $disabled = $true }
    if ($name -and $script:enabled.ContainsKey($name)) { $disabled = -not $script:enabled[$name] }

    $family = Get-ControlFamily $type

    switch ($family) {
        'group' {
            $linkName = Get-NodeText $Node 'LinkName'
            if (-not $linkName) { $linkName = $name }
            $script:stat.groups++
            $h = '<div class="glink" data-g="' + (Encode-Html $linkName) + '">'
            if ($script:groups.ContainsKey($linkName) -and $Depth -lt 2) {
                $h += (Render-Container $script:groups[$linkName] ($Depth + 1))
            }
            else {
                $h += '<div class="emptyctl">共用组「' + (Encode-Html $linkName) + '」在 Group.xml 中无定义，内容由宿主构建</div>'
            }
            $h += '</div>'
            return $h
        }
        'expand' {
            $script:stat.rows++
            $arrow = New-IconImg -Name 'btn_高级参数_downarrow.png' -W 12 -H 12 -Alt '展开'
            return '<div class="row exp"><div class="lb"></div><div class="ct"><div class="expander"><button>' + (Encode-Html (Get-Text '高级参数')) + $arrow + '</button></div></div></div>'
        }
        'headbar' {
            $script:stat.rows++
            $items = $Node.SelectNodes('EnumEntrys/HeadItem')
            $h = '<div class="headbar">'
            foreach ($it in $items) {
                $w = Get-NodeText $it 'Width'
                $style = ''
                if ($w -match '^\d+$') { $style = ' style="width:' + $w + 'px;flex:0 0 ' + $w + 'px"' }
                $h += '<span' + $style + '>' + (Encode-Html (Get-Text (Get-NodeText $it 'DisplayName'))) + '</span>'
            }
            $h += '</div>'
            return $h
        }
        'text' {
            $script:stat.rows++
            $fs = Get-NodeText $Node 'FontSize'
            $style = ''
            if ($fs -match '^\d+$') { $style = ' style="font-size:' + $fs + 'px"' }
            return '<div class="note-line"' + $style + '>' + (Encode-Html $label) + '</div>'
        }
        'switch' {
            $script:stat.rows++
            $v = Get-CurrentValue $Node
            $icon = 'swith_off.png'
            if ($v -ieq 'True') { $icon = 'swith_on.png' }
            # 原件 swith_on/off.png 实际 48x32
            $ctl = New-IconImg -Name $icon -W 48 -H 32 -Alt '开关'
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $ctl -Hidden $hidden -Disabled $disabled)
        }
        'check' {
            $script:stat.rows++
            $v = Get-CurrentValue $Node
            $icon = 'check2-box_off.png'
            if ($v -ieq 'True') { $icon = 'check2-box_on.png' }
            $ctl = New-IconImg -Name $icon -W 16 -H 16 -Alt '勾选'
            # BooleanCheck：勾选在左列 30px，标签在右
            return (New-Row -Label '' -LabelKey '' -ControlHtml $ctl -Hidden $hidden -Disabled $disabled -ExtraClass 'chkrow') 
        }
        'numeric' {
            $script:stat.rows++
            $v = Get-CurrentValue $Node
            $ctl = New-NumericControl -Value $v -WithLink
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $ctl -Hidden $hidden -Disabled $disabled)
        }
        'range' {
            $script:stat.rows++
            $pair = $Node.SelectNodes('Integers/Integer')
            if (-not $pair -or $pair.Count -eq 0) { $pair = $Node.SelectNodes('Floats/Float') }
            $v1 = ''; $v2 = ''
            if ($pair -and $pair.Count -ge 2) {
                $v1 = Get-CurrentValue $pair[0]
                $v2 = Get-CurrentValue $pair[1]
            }
            $ctl = (New-NumericControl -Value $v1 -WithLink) + '<span style="flex:0 0 12px;text-align:center;color:#808080">~</span>' + (New-NumericControl -Value $v2 -WithLink)
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $ctl -Hidden $hidden -Disabled $disabled)
        }
        'combo' {
            $script:stat.rows++
            $cur = Get-EnumCurrentText $Node
            $arrow = New-IconImg -Name 'combo_ic_triangle_normal.png' -W 16 -H 16 -Alt '下拉'
            $ctl = '<span class="combo"><span class="v">' + (Encode-Html $cur) + '</span>' + $arrow + '</span>'
            # 没有 EnumEntrys 说明候选由运行期提供（例如相机设备列表），标注清楚而不是只显示一个数字
            $entries = $Node.SelectNodes('EnumEntrys/EnumEntry')
            if (-not $entries -or $entries.Count -eq 0) {
                $ctl += '<span class="roimax" style="margin-left:4px">候选由运行期提供</span>'
            }
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $ctl -Hidden $hidden -Disabled $disabled)
        }
        'radio' {
            $script:stat.rows++
            $cur = Get-CurrentValue $Node
            $entries = $Node.SelectNodes('EnumEntrys/EnumEntry')
            $ctl = ''
            if ($entries -and $entries.Count -gt 0) {
                $idx = 0
                foreach ($e in $entries) {
                    $on = ($idx -eq 0 -and -not $cur)
                    if ($cur -and (Test-ValueEqual (Get-NodeText $e 'Value') $cur)) { $on = $true }
                    $icon = 'check-box_off.png'
                    if ($on) { $icon = 'check-box_on.png' }
                    $ctl += '<span class="radio">' + (New-IconImg -Name $icon -W 24 -H 24 -Alt '单选项') + (Encode-Html (Get-Text (Get-NodeText $e 'DisplayName'))) + '</span>'
                    $idx++
                }
            }
            else {
                $ctl = '<span class="emptyctl">该单选在 XML 中未声明选项</span>'
            }
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $ctl -Hidden $hidden -Disabled $disabled)
        }
        'roi' {
            $script:stat.rows++
            $sel = Get-NodeText $Node 'ROISelection'
            $tokens = @()
            if ($sel) { $tokens = @($sel -split '\s+' | Where-Object { $_ }) }
            $full = Get-NodeText $Node 'FullScreenEnable'
            if ($full -and $full -ieq 'True') { $tokens = @('FullScreen') + $tokens }
            $maxNum = Get-NodeText $Node 'RoiMaxNum'
            $ctl = '<span class="roiwrap">'
            $first = $true
            foreach ($t in $tokens) {
                $txt = $t
                $icon = ''
                if ($script:roiShape.Contains($t)) { $txt = $script:roiShape[$t].Text; $icon = $script:roiShape[$t].Icon }
                $cls = 'roibtn'
                if ($first) { $cls = 'roibtn on'; $first = $false }
                $img = New-IconImg -Name $icon -W 32 -H 32 -Alt $txt
                $ctl += '<span class="' + $cls + '" title="' + (Encode-Html $txt) + '">' + $img + '</span>'
            }
            if ($maxNum -match '^\d+$' -and [int]$maxNum -gt 0) {
                $ctl += '<span class="roimax">最多 ' + $maxNum + ' 个</span>'
            }
            $ctl += '</span>'
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $ctl -Hidden $hidden -Disabled $disabled)
        }
        'display' {
            $script:stat.rows++
            $eyes = 'see_normal.png'
            $ctl = '<span style="display:flex;align-items:center;gap:6px;min-width:0">'
            # eyeToggleButton 32x32，see_normal.png 实际 28x28
            $ctl += '<span style="flex:0 0 32px;width:32px;height:32px;display:flex;align-items:center;justify-content:center">' + (New-IconImg -Name $eyes -W 28 -H 28 -Alt '显示') + '</span>'
            $ctl += '<span class="colorbtn" title="线宽/颜色"></span>'
            $ctl += '</span>'
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $ctl -Hidden $hidden -Disabled $disabled)
        }
        'select' {
            $script:stat.rows++
            $v = Get-CurrentValue $Node
            $ctl = '<span class="txt"><input class="v" value="' + (Encode-Html $v) + '" readonly tabindex="-1"></span>'
            $ctl += '<button class="link" title="关联/下拉">' + (New-IconImg -Name 'combo_ic_关联_normal.png' -W 32 -H 32 -Alt '关联') + '</button>'
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $ctl -Hidden $hidden -Disabled $disabled)
        }
        'string' {
            $script:stat.rows++
            $v = Get-CurrentValue $Node
            $ctl = '<span class="txt"><input class="v" value="' + (Encode-Html $v) + '" readonly tabindex="-1"></span>'
            $ctl += '<button class="link" title="订阅/关联">' + (New-IconImg -Name 'combo_ic_关联_normal.png' -W 32 -H 32 -Alt '关联') + '</button>'
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $ctl -Hidden $hidden -Disabled $disabled)
        }
        'path' {
            $script:stat.rows++
            $v = Get-CurrentValue $Node
            $ctl = '<span class="txt"><input class="v" value="' + (Encode-Html $v) + '" readonly tabindex="-1"></span>'
            $ctl += '<button class="link" title="浏览">...</button>'
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $ctl -Hidden $hidden -Disabled $disabled)
        }
        'pointpair' {
            $script:stat.rows++
            $idx = Get-NodeText $Node 'DisplayIndex'
            $h = '<div class="pp"><div class="h">' + (Encode-Html (Get-Text '对位示教点')) + (Encode-Html $idx) + ' — ' + (Encode-Html (Get-Text 'Unfixtured Point')) + (Encode-Html $idx) + '</div>'
            $h += '<div class="grid">'
            foreach ($lblKey in @('Point', 'PointX', 'PointY', 'Angle')) {
                $h += '<div class="lbx">' + (Encode-Html (Get-Text $lblKey)) + '</div><div class="sep"></div>'
                $h += '<div>' + (New-NumericControl -Value '' -WithLink) + '</div>'
            }
            $h += '</div></div>'
            return $h
        }
        'list' {
            $script:stat.rows++
            $ctl = '<span class="txt"><input class="v" value="" readonly tabindex="-1"></span>'
            $ctl += '<button class="link" title="添加">' + (New-IconImg -Name 'add_normal.png' -W 20 -H 20 -Alt '添加') + '</button>'
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $ctl -Hidden $hidden -Disabled $disabled)
        }
        'command' {
            $script:stat.rows++
            $ctl = '<button class="link" style="flex:0 0 auto;width:auto;padding:0 10px">' + (Encode-Html $label) + '</button>'
            return (New-Row -Label '' -LabelKey '' -ControlHtml $ctl -Hidden $hidden -Disabled $disabled)
        }
        'c-threshold' {
            # ThresholdAreaPanel.xaml：表头 1.2* / 1.5* / 4.8* / 1.6*，行 5 列，底部新增按钮，上限 255 条
            $script:stat.rows++
            $h = '<div class="cth">'
            $h += '<div class="cr hd"><span class="c0">' + (Encode-Html (Get-Text 'Number')) + '</span><span class="c1">' + (Encode-Html (Get-Text 'GrayValue')) + '</span><span class="c2">' + (Encode-Html (Get-Text 'RunParam_Area Range')) + '</span><span class="c3">' + (Encode-Html (Get-Text 'Lang_Inverse')) + '</span><span></span></div>'
            $h += '<div class="cr"><span class="c0">1</span><span class="c1">' + (New-NumericControl -Value '') + '</span><span class="c2">' + (New-CmpRangePair) + '</span><span class="c3">' + (New-IconImg -Name 'check2-box_off.png' -W 16 -H 16 -Alt '反向') + '</span><span>' + (New-IconImg -Name 'delete_normal.png' -W 20 -H 20 -Alt '删除') + '</span></div>'
            $h += '<div class="ft"><span class="ib">' + (New-IconImg -Name 'add_normal.png' -W 20 -H 20 -Alt '新增') + '</span></div></div>'
            $inner = New-CmpBlock -Source 'ModuleControls/IMVSBlobFindLabelsModuControl/ThresholdAreaPanel.xaml' -Inner $h -Note '示例行为占位；上限 255 条，存参格式 序号#新增#灰度#下限#上限#反向'
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $inner -Hidden $hidden -Disabled $disabled)
        }
        'c-imgsrc' {
            # ImageSourceListView.xaml：固定高 133 滚动，行 = 90px 标签 + 下拉，无增删按钮
            $script:stat.rows++
            $h = '<div class="cimg">'
            foreach ($i in @(1, 2)) {
                $h += '<div class="rowline"><span class="lbx">' + (Encode-Html (Get-Text 'Input Source')) + ' ' + $i + '</span>' + (New-CmpCombo -Text '') + '</div>'
            }
            $h += '</div>'
            $inner = New-CmpBlock -Source 'ModuleControls/IMVSImgStitchCalibModuControl/ImageSourceListView.xaml' -Inner $h -Note '行数由输入图数量决定，行内下拉选项来自输入图清单（CombinationSelecter）'
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $inner -Hidden $hidden -Disabled $disabled)
        }
        'c-rangecfg' {
            # RangeConfigListPanel.xaml：灰底 #f2f3f5 + 标题 + 列表 MaxHeight 233 + 底部 add/delete 图标
            $script:stat.rows++
            $h = '<div class="crg">'
            $h += '<div class="ttl">' + (Encode-Html (Get-Text 'Threshold Range')) + '</div>'
            $h += '<div class="lst"><div class="hint">行模板由代码注入（ItemTemplate），原件 XAML 未固定行结构；每行是一个阈值区间项</div></div>'
            $h += '<div class="ft"><span class="ib">' + (New-IconImg -Name 'add_normal.png' -W 20 -H 20 -Alt '新增') + '</span><span class="ib">' + (New-IconImg -Name 'delete_normal.png' -W 20 -H 20 -Alt '删除') + '</span></div></div>'
            $inner = New-CmpBlock -Source 'ModuleControls/IMVSPixelCountModuVAControl/RangeConfigListPanel.xaml' -Inner $h -Note '删除按钮按 DelEnable 显隐，列表超 233 高滚动'
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $inner -Hidden $hidden -Disabled $disabled)
        }
        'c-timecfg' {
            # TimeConfig.xaml：80px 名称 + 两个 200x32 模块下拉 + 50px 删除，底部新增，上限 64 条
            $script:stat.rows++
            $h = '<div class="ctm">'
            $h += '<div class="r"><span class="nm">' + (Encode-Html (Get-Text 'TimeName')) + '</span>'
            $h += '<span class="sel">' + (New-CmpCombo -Text '') + (New-CmpCombo -Text '') + '</span>'
            $h += '<span class="del">' + (New-IconImg -Name 'delete_normal.png' -W 20 -H 20 -Alt '删除') + '</span></div>'
            $h += '<div class="ft"><span class="ib">' + (New-IconImg -Name 'add_normal.png' -W 20 -H 20 -Alt '新增') + '</span></div></div>'
            $inner = New-CmpBlock -Source 'ModuleControls/TimeStatisticModuleControl/TimeConfig.xaml' -Inner $h -Note '两个下拉分别是起始/结束模块；上限 64 条（TimeConfigCountError）'
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $inner -Hidden $hidden -Disabled $disabled)
        }
        'c-model' {
            # PathDrawPannel.xaml（46 高工具栏）/ EdgepatMatchConfigPanel.xaml（41 高工具栏）：下方为 1px 边框的模型预览区
            $script:stat.rows++
            $isPath = ($type -eq 'PathDrawingGuide')
            $h = '<div class="cmd"><div class="tb">'
            if ($isPath) {
                $h += '<span class="btn">' + (New-IconImg -Name 'btn_新建模板_normal.png' -W 20 -H 20 -Alt '新建模型') + (Encode-Html (Get-Text '新建模型')) + '</span>'
                $h += '<span class="btn">' + (New-IconImg -Name 'ic_btn_edit_normal.png' -W 20 -H 20 -Alt '修改模型') + (Encode-Html (Get-Text 'Lang_ColorDistinguish_ModifyModel')) + '</span>'
                $h += '<span class="ic" style="margin-left:auto">' + (New-IconImg -Name 'ic_导入_normal.png' -W 34 -H 34 -Alt '导入') + (New-IconImg -Name 'ic_导出2_normal.png' -W 34 -H 34 -Alt '导出') + '</span>'
            }
            else {
                $h += '<span class="ic">' + (New-IconImg -Name 'ic_创建_normal.png' -W 34 -H 34 -Alt '创建模板') + (New-IconImg -Name 'ic_导入_normal.png' -W 34 -H 34 -Alt '导入模板') + '</span>'
                $h += '<span class="btn r">' + (Encode-Html (Get-Text 'Lang_ColorDistinguish_ModifyModel')) + '</span>'
            }
            $h += '</div><div class="cv">'
            if ($isPath) { $previewName = '图像运行_图片.png' } else { $previewName = 'pic_边缘缺陷检测.png' }
            $previewUrl = Get-IconUrl $previewName
            if ($previewUrl) { $h += '<img src="' + $previewUrl + '" alt="模型预览区" style="max-width:100%;max-height:112px">' }
            $h += '<span>原件此处为模型预览区（' + (Encode-Html $previewName) + '），模型未建立时为空</span>'
            $h += '</div></div>'
            if ($isPath) { $srcXaml = 'ModuleControls/IMVSGluePathConductModuControl/PathDrawPannel.xaml' } else { $srcXaml = 'ModuleControls/' + $(if ($type -eq 'Panel_EdgepatMatch') { 'IMVSEdgeFlawInspModuControl' } else { 'IMVSEdgePairFlawInspModuControl' }) + '/EdgepatMatchConfigPanel.xaml' }
            $inner = New-CmpBlock -Source $srcXaml -Inner $h -Note '模板创建/编辑走弹窗，弹层内容未还原'
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $inner -Hidden $hidden -Disabled $disabled)
        }
        'c-filecfg' {
            # ReadFilePanel.xaml：单个 ListBox，条目按 DisplayType 经 UserTempConverter 选模板
            $script:stat.rows++
            $h = '<div class="cfl"><div class="lst"><div class="hint">每行形态由子参数类型决定（Float / Int / String / Point / 标定点列表），原件按 DisplayType 动态选模板；行数由读到的文件内容决定</div></div></div>'
            $inner = New-CmpBlock -Source 'ModuleControls/ReadCalibFileModuControl/ReadFilePanel.xaml' -Inner $h -Note '无增删按钮，条目全部由读入文件生成'
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $inner -Hidden $hidden -Disabled $disabled)
        }
        'c-color' {
            # 全局模板 ColorConfig（XmlUi/styles/paramusercontroldatatemplate.xaml 的 x:Key="ColorConfig"）
            $script:stat.rows++
            $h = '<div style="display:flex;align-items:center;gap:6px;padding:6px 8px">'
            $h += '<span class="colorbtn" title="颜色"></span>'
            $h += '<span class="hint" style="padding:0">颜色弹层未展开</span>'
            $h += '</div>'
            $inner = New-CmpBlock -Source 'XmlUi/styles/paramusercontroldatatemplate.xaml (x:Key="ColorConfig")' -Inner $h -Note '颜色候选与取色弹层内容未还原'
            $ctl = $inner + '<button class="link" title="关联">' + (New-IconImg -Name 'combo_ic_关联_normal.png' -W 32 -H 32 -Alt '关联') + '</button>'
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $ctl -Hidden $hidden -Disabled $disabled)
        }
        'empty' {
            $script:stat.rows++
            $script:stat.empty++
            $ctl = '<span class="emptyctl">原件模板为空（<code>' + (Encode-Html $type) + '</code>），界面由代码构建</span>'
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $ctl -Hidden $hidden -Disabled $disabled)
        }
        default {
            $script:stat.rows++
            if ($script:notAdopted.ContainsKey($type)) {
                # 卡片登记为「我方主动不采用」：不是欠账，写清原因与替代
                $na = $script:notAdopted[$type]
                $script:stat.notAdopted++
                $ctl = '<span class="notadopted"><b>我方不采用</b> <code>' + (Encode-Html $type) + '</code>：' + (Encode-Html ([string]$na.reason)) +
                       ' <span class="rp">替代：' + (Encode-Html ([string]$na.replacement)) + '</span></span>'
                return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $ctl -Hidden $hidden -Disabled $disabled)
            }
            $script:stat.unknown++
            $ctl = '<span class="unknown">未还原：原件类型 <code>' + (Encode-Html $type) + '</code> 尚无对应控件模板</span>'
            return (New-Row -Label $label -LabelKey $labelKey -ControlHtml $ctl -Hidden $hidden -Disabled $disabled)
        }
    }
}

function Render-Container {
    param([object]$Node, [int]$Depth)

    $sb = New-Object System.Text.StringBuilder
    foreach ($c in $Node.SelectNodes('Categorys/Category')) {
        $cn = Get-NodeText $c 'DisplayName'
        if (-not $cn) { $cn = [string]$c.GetAttribute('Name') }
        $dispType = Get-NodeText $c 'DisplayType'
        $cls = 'cat'
        if ($dispType -eq 'CategoryNoHead') { $cls = 'cat nohd' }
        [void]$sb.Append('<div class="' + $cls + '"><div class="ln"></div><h3>' + (Encode-Html (Get-Text $cn)) + '</h3>')
        [void]$sb.Append((Render-Container $c $Depth))
        [void]$sb.Append('</div>')
        $script:stat.categories++
    }
    foreach ($it in $Node.SelectNodes('Items/*')) {
        if ($it.LocalName -eq 'Category') {
            $cn = Get-NodeText $it 'DisplayName'
            if (-not $cn) { $cn = [string]$it.GetAttribute('Name') }
            $dispType = Get-NodeText $it 'DisplayType'
            $cls = 'cat'
            if ($dispType -eq 'CategoryNoHead') { $cls = 'cat nohd' }
            [void]$sb.Append('<div class="' + $cls + '"><div class="ln"></div><h3>' + (Encode-Html (Get-Text $cn)) + '</h3>')
            [void]$sb.Append((Render-Container $it $Depth))
            [void]$sb.Append('</div>')
            $script:stat.categories++
        }
        else {
            [void]$sb.Append((Render-Node $it $Depth))
        }
    }
    return $sb.ToString()
}

# ---------------------------------------------------------------- 单工具页面
function New-ToolUi {
    param([string]$ToolId, [object]$Evidence, [string]$OutFileName)

    $srcDir = [string]$Evidence.originalSourcePath
    $plugin = [string]$Evidence.originalPlugin
    if (-not $plugin) { $plugin = $ToolId }
    $displayName = [string]$Evidence.displayName
    if (-not $displayName) { $displayName = $ToolId }
    $category = [string]$Evidence.originalCategory
    if (-not $category) { $category = 'Unknown' }
    $status = [string]$Evidence.evidenceState
    if (-not $status) { $status = [string]$Evidence.status }

    $script:stat = [pscustomobject]@{ rows = 0; categories = 0; groups = 0; hidden = 0; raw = 0; tabs = 0; unknown = 0; empty = 0; notAdopted = 0 }

    $script:notAdopted = @{}
    if ($Evidence.currentImplementation -and $Evidence.currentImplementation.notAdopted) {
        foreach ($na in $Evidence.currentImplementation.notAdopted) {
            $t = [string]$na.type
            if ($t) { $script:notAdopted[$t] = $na }
        }
    }

    $xmlPath = ''
    if ($srcDir) { $xmlPath = Join-Path $srcDir ($plugin + 'AlgorithmTab.xml') }

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('<!DOCTYPE html>')
    [void]$sb.AppendLine('<html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">')
    [void]$sb.AppendLine('<title>' + (Encode-Html $displayName) + ' · 参数面板</title>')
    [void]$sb.AppendLine('<!-- suneye:tool-ui-v2 algorithm-tab tool=' + (Encode-Html $ToolId) + ' plugin=' + (Encode-Html $plugin) + ' -->')
    [void]$sb.AppendLine('<style>' + $css + '</style></head><body>')
    [void]$sb.AppendLine('<div class="page">')
    [void]$sb.AppendLine('<div class="cap"><a href="index.html">工具参数面板 · 索引</a> <span class="sp">/</span> 卡片 <a href="../../07-工具逆向/tools/' + (Encode-Html $ToolId) + '/evidence.html"><code>' + (Encode-Html $ToolId) + '</code></a> <span class="sp">/</span> 分类 ' + (Encode-Html (Get-Text $category)))

    if (-not $xmlPath -or -not (Test-Path -LiteralPath $xmlPath)) {
        [void]$sb.AppendLine('</div>')
        [void]$sb.AppendLine('<div class="win"><div class="tb"><i>1</i><b>' + (Encode-Html $displayName) + '</b><span class="x">&#10005;</span></div>')
        [void]$sb.AppendLine('<div class="pane on"><div class="note-line">原件目录未记录或未提供 <code>' + (Encode-Html ($plugin + 'AlgorithmTab.xml')) + '</code>，本页不还原参数面板。</div></div></div>')
        [void]$sb.AppendLine('<div class="meta">分类 <b>' + (Encode-Html (Get-Text $category)) + '</b> · 证据状态 <b>' + (Encode-Html $status) + '</b></div>')
        [void]$sb.AppendLine('</div></body></html>')
        [System.IO.File]::WriteAllText((Join-Path $outRoot $OutFileName), $sb.ToString(), $utf8)
        return [pscustomobject]@{ ToolId = $ToolId; DisplayName = $displayName; Plugin = $plugin; Category = $category; Status = $status; File = $OutFileName; Tabs = 0; Rows = 0; Hidden = 0; Raw = 0; Unknown = 0; Missing = $true }
    }

    $x = [xml]([System.IO.File]::ReadAllText($xmlPath, $utf8))
    Initialize-ParamState -RootNode $x.DocumentElement
    $tabs = @($x.SelectNodes('AlgorithmTabRoot/Tabs/Tab'))
    $winW = Get-NodeText $x.DocumentElement 'ZHCustomTabWidth'
    $winH = Get-NodeText $x.DocumentElement 'ZHCustomTabHeight'
    if (-not $winW -or $winW -notmatch '^\d+$') { $winW = '420' }
    if (-not $winH -or $winH -notmatch '^\d+$') { $winH = '560' }
    $paneH = [int]$winH - 96
    if ($paneH -lt 240) { $paneH = 240 }

    $body = New-Object System.Text.StringBuilder
    for ($ti = 0; $ti -lt $tabs.Count; $ti++) {
        $tab = $tabs[$ti]
        $cls = 'pane'
        if ($ti -eq 0) { $cls = 'pane on' }
        [void]$body.Append('<div class="' + $cls + '" data-tab="' + $ti + '" style="max-height:' + $paneH + 'px">')
        [void]$body.Append((Render-Container $tab 0))
        [void]$body.Append('</div>')
        $script:stat.tabs++
    }

    $footLeft = New-Object System.Text.StringBuilder
    foreach ($n in $x.SelectNodes('AlgorithmTabRoot/BottomExcuteButtonsLeft/*')) {
        $lt = [string]$n.GetAttribute('Name')
        $dn = Get-NodeText $n 'DisplayName'
        if ($dn) { $lt = Get-Text $dn }
        elseif ($n.LocalName -eq 'Button_ResetAlgoParams') { $lt = Get-Text 'Lang_Reset parameters' }
        else { $lt = Get-Text $lt }
        [void]$footLeft.Append('<button title="' + (Encode-Html $n.LocalName) + '">' + (Encode-Html $lt) + '</button>')
    }
    $footRight = New-Object System.Text.StringBuilder
    foreach ($n in $x.SelectNodes('AlgorithmTabRoot/BottomExcuteButtons/*')) {
        if ($n.LocalName -eq 'GroupLinkItem') {
            $ln = Get-NodeText $n 'LinkName'
            if ($script:groups.ContainsKey($ln)) {
                foreach ($c in $script:groups[$ln].SelectNodes('Items/*')) {
                    $vis = Get-NodeText $c 'CustomVisible'
                    if ($vis -and $vis -ieq 'False') { continue }
                    $dn2 = Get-NodeText $c 'DisplayName'
                    $t = ''
                    if ($dn2) { $t = Get-Text $dn2 } else { $t = [string]$c.GetAttribute('Name') }
                    [void]$footRight.Append('<button>' + (Encode-Html $t) + '</button>')
                }
            }
            else { [void]$footRight.Append('<button>' + (Encode-Html (Get-Text $ln)) + '</button>') }
        }
        else {
            $dn3 = Get-NodeText $n 'DisplayName'
            $t = ''
            if ($dn3) { $t = Get-Text $dn3 } else { $t = [string]$n.GetAttribute('Name') }
            [void]$footRight.Append('<button>' + (Encode-Html $t) + '</button>')
        }
    }

    [void]$sb.AppendLine('</div>')
    $naMeta = ''
    if ($script:stat.notAdopted -gt 0) { $naMeta = ' · 我方不采用 <b>' + $script:stat.notAdopted + '</b>' }
    [void]$sb.AppendLine('<div class="meta">窗口 <b>' + $winW + 'x' + $winH + '</b> · 页签 <b>' + $script:stat.tabs + '</b> · 分类 <b>' + $script:stat.categories + '</b> · 参数行 <b>' + $script:stat.rows + '</b> · 初始隐藏 <b>' + $script:stat.hidden + '</b> · 共用组 <b>' + $script:stat.groups + '</b> · 无中文 <b>' + $script:stat.raw + '</b> · 未还原类型 <b>' + $script:stat.unknown + '</b>' + $naMeta + ' <label style="margin-left:12px;cursor:pointer"><input type="checkbox" id="showhid"> 显示初始隐藏项</label></div>')

    # 我方与原件形态不同的品类（相机：多品牌多接口），差异写在卡片 currentImplementation.differences
    $diffs = @()
    if ($Evidence.currentImplementation -and $Evidence.currentImplementation.differences) {
        $diffs = @($Evidence.currentImplementation.differences | Where-Object { $_ })
    }
    if ($diffs.Count -gt 0) {
        [void]$sb.AppendLine('<div class="diff"><b>我方设计与原件形态不同</b>：下面这页是原件的面板形态，只作字段与分组的参考，不是我方目标界面。<ul>')
        foreach ($d in $diffs) { [void]$sb.AppendLine('<li>' + (Encode-Html $d) + '</li>') }
        [void]$sb.AppendLine('</ul></div>')
    }

    [void]$sb.AppendLine('<div class="win" style="margin-top:10px;width:' + $winW + 'px">')
    [void]$sb.AppendLine('<div class="tb"><i>1</i><b>' + (Encode-Html $displayName) + '</b><span class="q">?</span><span class="x">&#10005;</span></div>')
    [void]$sb.AppendLine('<div class="tabs">')
    for ($ti = 0; $ti -lt $tabs.Count; $ti++) {
        $cls = 'tab'
        if ($ti -eq 0) { $cls = 'tab on' }
        [void]$sb.AppendLine('<span class="' + $cls + '" data-tab="' + $ti + '">' + (Encode-Html (Get-Text ([string]$tabs[$ti].GetAttribute('Name')))) + '</span>')
    }
    [void]$sb.AppendLine('</div>')
    [void]$sb.Append($body.ToString())
    $fl = $footLeft.ToString()
    $fr = $footRight.ToString()
    if ($fl -or $fr) {
        [void]$sb.AppendLine('<div class="foot"><div class="l">' + $fl + '</div><div class="r">' + $fr + '</div></div>')
    }
    else {
        [void]$sb.AppendLine('<div class="nofoot">原件未声明底部命令按钮</div>')
    }
    [void]$sb.AppendLine('</div>')

    [void]$sb.AppendLine('<details><summary>这页按哪些原件材料还原（点开看）</summary><ul>')
    [void]$sb.AppendLine('<li>结构：<code>' + (Encode-Html ($plugin + 'AlgorithmTab.xml')) + '</code> 的 <code>Tabs/Tab/Categorys/Category/Items</code>，顺序与原件一致。</li>')
    [void]$sb.AppendLine('<li>行容器：<code>LeftRightControl.xaml</code>，左列 <code>ChildLeftWidth=90</code>（<code>LeftWidthConverter</code> 中文值），行高 32，标签 14px <code>#666666</code>。</li>')
    [void]$sb.AppendLine('<li>控件：<code>styles/ParamUserControlDataTemplate.xaml</code> 的类型分发表 + <code>Apps.Style</code> 各 Style（单选 24px、下拉 30px、折叠 22px、ROI 36x34、眼睛 32x32）。</li>')
    [void]$sb.AppendLine('<li>图标：<code>Apps.XmlUI/resources</code>、<code>Apps.Style/resources</code> 原件 PNG，逐张复制到 <code>ui/assets/icons/</code>。</li>')
    [void]$sb.AppendLine('<li>主题色：<code>Apps.ColorFun/colorstyle1.xaml</code>（主题色 <code>#E76900</code>、面板底 <code>#F2F3F5</code>、分隔线 <code>#E0DFDE</code>）。</li>')
    [void]$sb.AppendLine('<li>共用组：<code>Group.xml</code> 的 <code>GroupInfoItem</code> 就地展开。</li>')
    [void]$sb.AppendLine('<li>中文：<code>Lang/zh-cn.xaml</code> 等三份语言字典；查不到的中文按原件英文键显示，标签带红色虚线下划线。</li>')
    [void]$sb.AppendLine('</ul></details>')

    [void]$sb.AppendLine('<details><summary>没有还原的部分（不要当成原件等价物）</summary><ul>')
    [void]$sb.AppendLine('<li>弹层内容（下拉候选、颜色选择、滑杆展开态、订阅树）未还原，只画收起态。</li>')
    [void]$sb.AppendLine('<li>显隐只按当前取值的 Triggers 求初始状态，运行期由代码改的显隐不在此列。</li>')
    [void]$sb.AppendLine('<li>像素级误差仍在：字体渲染、控件内边距、部分样式的 hover/按下态未逐项对齐。</li>')
    [void]$sb.AppendLine('<li>不执行算法，不含运行结果与 ROI 交互。</li>')
    [void]$sb.AppendLine('</ul></details>')

    [void]$sb.AppendLine('</div>')
    [void]$sb.AppendLine('<script>' + $js + '</script>')
    [void]$sb.AppendLine('</body></html>')

    [System.IO.File]::WriteAllText((Join-Path $outRoot $OutFileName), $sb.ToString(), $utf8)
    return [pscustomobject]@{
        ToolId = $ToolId; DisplayName = $displayName; Plugin = $plugin; Category = $category; Status = $status
        File = $OutFileName; Tabs = $script:stat.tabs; Rows = $script:stat.rows; Hidden = $script:stat.hidden
        Raw = $script:stat.raw; Unknown = $script:stat.unknown; Missing = $false
        NotAdopted = $script:stat.notAdopted
        HasDiff = ($diffs.Count -gt 0)
    }
}

# ---------------------------------------------------------------- 索引页
function New-IndexPage {
    param([object[]]$Rows)

    $total = @($Rows).Count
    $missing = @($Rows | Where-Object { $_.Missing }).Count
    $unknownTotal = (($Rows | Measure-Object -Property Unknown -Sum).Sum)

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('<!DOCTYPE html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">')
    [void]$sb.AppendLine('<title>工具参数面板 · 索引</title>')
    [void]$sb.AppendLine('<style>')
    [void]$sb.AppendLine('*{box-sizing:border-box}body{margin:0;background:#DEE1E6;color:#1F2733;font:13px/1.6 "Microsoft YaHei",sans-serif}')
    [void]$sb.AppendLine('.page{max-width:1180px;margin:0 auto;padding:18px 16px 48px}h1{font-size:20px;margin:0 0 6px}')
    [void]$sb.AppendLine('.sub{color:#5B6472;font-size:12px;margin:0 0 12px}.sub code{background:#EEF0F3;border:1px solid #E1E4E9;border-radius:3px;padding:0 4px}')
    [void]$sb.AppendLine('.bar{display:flex;gap:10px;align-items:center;margin:0 0 14px;flex-wrap:wrap}')
    [void]$sb.AppendLine('.bar input[type=search]{width:300px;height:30px;border:1px solid #CCC7C6;border-radius:4px;padding:0 10px;font:13px "Microsoft YaHei"}')
    [void]$sb.AppendLine('.bar select{height:30px;border:1px solid #CCC7C6;border-radius:4px;font:12px "Microsoft YaHei";padding:0 6px;background:#fff}')
    [void]$sb.AppendLine('table{width:100%;border-collapse:collapse;background:#fff;border:1px solid #D6DAE0}')
    [void]$sb.AppendLine('th,td{padding:6px 10px;border-bottom:1px solid #EEF0F3;text-align:left;font-size:12px}')
    [void]$sb.AppendLine('th{background:#F2F3F5;color:#3D3D3D;font-weight:700}tr:hover td{background:#FAFBFC}td.num{text-align:right;color:#4B5563}')
    [void]$sb.AppendLine('td a{color:#2563eb;text-decoration:none}td a:hover{text-decoration:underline}')
    [void]$sb.AppendLine('.mk{font-size:11px;border:1px solid #D5D9DE;border-radius:3px;padding:0 4px;color:#6B7280}.mk.miss{border-color:#FCA5A5;color:#B91C1C;background:#FEF2F2}.mk.diff{border-color:#93C5FD;color:#1D4ED8;background:#EFF6FF}')
    [void]$sb.AppendLine('.note{margin-top:14px;border-left:3px solid #E76900;background:#FFF7ED;color:#7C2D12;font-size:12px;padding:10px 14px;line-height:1.8}')
    [void]$sb.AppendLine('</style></head><body><div class="page">')
    [void]$sb.AppendLine('<h1>工具参数面板 · 索引</h1>')
    [void]$sb.AppendLine('<p class="sub">按原件 <code>AlgorithmTab.xml</code> + 真实控件 XAML/样式/图标逐层还原参数面板。共 <b>' + $total + '</b> 个工具，<b>' + ($total - $missing) + '</b> 个有 XML 声明；仍有未还原类型的参数行共 <b>' + $unknownTotal + '</b> 行（页内标红）。</p>')
    [void]$sb.AppendLine('<div class="bar"><input id="q" type="search" placeholder="搜索工具名 / 模块名 / 分类"><select id="cf"><option value="">全部分类</option>')
    foreach ($c in @($Rows | Group-Object Category | Sort-Object Name)) {
        [void]$sb.AppendLine('<option value="' + (Encode-Html $c.Name) + '">' + (Encode-Html (Get-Text $c.Name)) + '（' + $c.Count + '）</option>')
    }
    [void]$sb.AppendLine('</select><span id="cnt" style="color:#5B6472"></span></div>')
    [void]$sb.AppendLine('<table id="tb"><thead><tr><th style="width:150px">工具</th><th style="width:190px">原件模块</th><th style="width:110px">分类</th><th style="width:56px">页签</th><th style="width:70px">参数行</th><th style="width:76px">初始隐藏</th><th style="width:90px">未还原</th><th>说明</th></tr></thead><tbody>')
    foreach ($r in ($Rows | Sort-Object Category, DisplayName)) {
        $hay = Encode-Html ($r.DisplayName + ' ' + $r.Plugin + ' ' + $r.ToolId + ' ' + (Get-Text $r.Category))
        [void]$sb.AppendLine('<tr data-c="' + (Encode-Html $r.Category) + '" data-h="' + $hay + '">')
        [void]$sb.AppendLine('<td><a href="' + (Encode-Html $r.File) + '">' + (Encode-Html $r.DisplayName) + '</a>' + $(if ($r.HasDiff) { ' <span class="mk diff">与原件有差异</span>' } else { '' }) + '</td>')
        [void]$sb.AppendLine('<td>' + (Encode-Html $r.Plugin) + '</td><td>' + (Encode-Html (Get-Text $r.Category)) + '</td>')
        if ($r.Missing) {
            [void]$sb.AppendLine('<td class="num">-</td><td class="num">-</td><td class="num">-</td><td><span class="mk miss">缺原件 XML</span></td><td>' + (Encode-Html $r.ToolId) + '</td>')
        }
        else {
            $um = '-'
            if ($r.Unknown -gt 0) { $um = '<span class="mk">' + $r.Unknown + '</span>' }
            $naCell = Encode-Html $r.ToolId
            if ($r.NotAdopted -gt 0) { $naCell += ' <span class="mk diff">我方不采用 ' + $r.NotAdopted + '</span>' }
            [void]$sb.AppendLine('<td class="num">' + $r.Tabs + '</td><td class="num">' + $r.Rows + '</td><td class="num">' + $r.Hidden + '</td><td>' + $um + '</td><td>' + $naCell + '</td>')
        }
        [void]$sb.AppendLine('</tr>')
    }
    [void]$sb.AppendLine('</tbody></table>')
    [void]$sb.AppendLine('<div class="note"><b>口径</b>：参数行按 XML 叶子节点统计（含共用组展开）；「初始隐藏」= <code>CustomVisible=False</code> 且当前取值未命中显示触发器；「未还原」= 该类型尚未有对应控件模板，页内红框标出，不做假控件替代；「我方不采用」= 卡片 <code>currentImplementation.notAdopted</code> 登记的原件厂商专属控件，页内蓝框写明原因与替代方案，不计入未还原欠账。</div>')
    [void]$sb.AppendLine('</div><script>')
    [void]$sb.AppendLine('var q=document.getElementById("q"),cf=document.getElementById("cf"),cnt=document.getElementById("cnt"),rows=[].slice.call(document.querySelectorAll("#tb tbody tr"));')
    [void]$sb.AppendLine('function f(){var s=(q.value||"").toLowerCase(),c=cf.value,n=0;rows.forEach(function(r){var ok=(!s||r.getAttribute("data-h").toLowerCase().indexOf(s)>=0)&&(!c||r.getAttribute("data-c")===c);r.style.display=ok?"":"none";if(ok)n++;});cnt.textContent="显示 "+n+" / "+rows.length;}')
    [void]$sb.AppendLine('q.addEventListener("input",f);cf.addEventListener("change",f);f();')
    [void]$sb.AppendLine('</script></body></html>')
    [System.IO.File]::WriteAllText((Join-Path $outRoot 'index.html'), $sb.ToString(), $utf8)
    return $total
}

# ---------------------------------------------------------------- 主流程
$dirs = Get-ChildItem -Path $toolsDir -Directory | Sort-Object Name
if ($Tool -and $Tool.Count -gt 0) {
    $want = @{}
    foreach ($t in $Tool) { $want[$t] = $true }
    $dirs = @($dirs | Where-Object { $want.ContainsKey($_.Name) })
}

$results = New-Object 'System.Collections.Generic.List[object]'
$usedNames = @{}
$ok = 0; $fail = 0

foreach ($dir in $dirs) {
    $toolId = $dir.Name
    if ($toolId.StartsWith('_')) { continue }
    $evPath = Join-Path $dir.FullName 'evidence.json'
    if (-not (Test-Path -LiteralPath $evPath)) { continue }
    $ev = [System.IO.File]::ReadAllText($evPath, $utf8) | ConvertFrom-Json

    $cat = [string]$ev.originalCategory
    if (-not $cat) { $cat = 'Unknown' }
    $plugin = [string]$ev.originalPlugin
    if (-not $plugin) { $plugin = $toolId }
    $file = ($cat -replace '[^\w\-]', '') + '-' + ($plugin -replace '[^\w\-]', '') + '.html'
    if ($usedNames.ContainsKey($file)) { $file = ($cat -replace '[^\w\-]', '') + '-' + ($plugin -replace '[^\w\-]', '') + '-' + $toolId + '.html' }
    $usedNames[$file] = $true

    $r = New-ToolUi -ToolId $toolId -Evidence $ev -OutFileName $file
    $results.Add($r)
    if ($r.Missing) { $fail++ } else { $ok++ }
}

$indexCount = 0
if (-not $NoIndex) { $indexCount = New-IndexPage -Rows $results.ToArray() }

Write-Host ''
Write-Host '========== 工具参数面板还原 (v2 真实控件) ==========' -ForegroundColor Cyan
Write-Host ('语言字典: ' + (($langLoaded | ForEach-Object { "$($_.File)+$($_.Added)" }) -join '  '))
Write-Host ('共用组: ' + $groupCount + '   图标目录: ' + $script:iconDirs.Count)
Write-Host ("有 XML: {0}   缺原件 XML: {1}   参数行: {2}   初始隐藏: {3}   未还原类型行: {4}   我方不采用行: {5}" -f `
        $ok, $fail,
        (($results | Measure-Object -Property Rows -Sum).Sum),
        (($results | Measure-Object -Property Hidden -Sum).Sum),
        (($results | Measure-Object -Property Unknown -Sum).Sum),
        (($results | Measure-Object -Property NotAdopted -Sum).Sum))
Write-Host ('复制图标: ' + $script:iconCopied + '  缺失图标种类: ' + @($script:iconMissing.Keys).Count)
if (@($script:iconMissing.Keys).Count -gt 0) {
    Write-Host ('  缺失: ' + ((@($script:iconMissing.Keys) | Sort-Object) -join ', '))
}
Write-Host ('输出目录: ' + $outRoot)
if (-not $NoIndex) { Write-Host ('索引: ' + (Join-Path $outRoot 'index.html') + '  条目 ' + $indexCount) }
Write-Host ''
