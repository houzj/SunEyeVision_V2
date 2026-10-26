"""Update the original HTML preview from migrated VM resources, without WPF claims."""
from pathlib import Path
import hashlib
import html
import json
import os
import re
import base64
import xml.etree.ElementTree as ET
from PIL import Image
from urllib.parse import quote

ROOT = Path(__file__).resolve().parents[2]
PAGE = next((ROOT / 'docs').rglob('optimization-o06-ui-preview.html'))
PUBLIC = PAGE.parent.parent / '公共资源'
ASSETS = ROOT / 'src/Plugin.Wpf/Themes/Assets'
CSS = PUBLIC / 'blob-migrated-controls.css'

def url(path, parent=PUBLIC):
    return quote(os.path.relpath(path, parent).replace('\\', '/'), safe='/.-_')

assets = {}
def image(name):
    p = ASSETS / name
    assert p.is_file(), p
    assets[name] = {'path': p.relative_to(ROOT).as_posix(), 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()}
    return 'url("' + url(p) + '")'

css = '''/* Generated from migrated originals; browser presentation, not WPF rendering. */
.window{--paper:#f2f3f4;--ink:#666;--muted:#666;--border:#ccc7c6;--accent:#ff7900;--tint:#fcecdc;font-family:"Microsoft YaHei","Segoe UI",sans-serif}
.window .label,.window .radios{font-size:14px}.window .row{grid-template-columns:108px minmax(0,1fr);min-height:36px;margin:2px 0}
.window .group h3{font-size:15px;color:#32383b}.window .group{padding:18px 0 14px}.window .page{padding:0 24px 20px}
.window .tabs{height:38px}.window .tabs button{font-size:14px;padding:0 14px}.window .tabs button[aria-selected=true]{background:#fceddf;border-bottom:2px solid #ff7900;color:#242424}
.window .row input,.window .row select{font-size:14px;color:#808080;border-color:#ccc7c6;background:#fcfcfc}
.window .switch{width:40px;height:27px}.window .switch i{border:0;border-radius:0;box-shadow:none;background:SWITCH_OFF center/contain no-repeat}.window .switch i:after{display:none}.window .switch input:checked+i{background:SWITCH_ON center/contain no-repeat;border:0}
.window .switch input:focus-visible+i{outline:1px dotted #444;outline-offset:-2px}
.window .radios input{appearance:none;border:0;width:20px;height:20px;display:inline-block;background:RADIO_OFF center/contain no-repeat}.window .radios input:checked{border:0;background-image:RADIO_ON}.window .radios input:checked:after{display:none}
.window .bind,.window .pen,.window .eye,.window .mode-toggle{background-color:transparent;background-position:center;background-repeat:no-repeat;background-size:contain}
.window .bind svg,.window .pen svg,.window .eye svg,.window .mode-toggle svg{display:none}
.window .bind{width:28px;height:28px;background-image:LINK_NORMAL;border:0}.window .bind:hover{background-image:LINK_HOVER}.window .bind:active{background-image:LINK_CLICK}
.window .mode-toggle{width:28px;height:28px;padding:0;background-image:MODE_LINK;border-color:#ccc7c6}.window .mode-toggle[aria-pressed=false]:hover{background-image:MODE_LINK_CLICK;background-color:transparent}
.window .mode-toggle[aria-pressed=true]{background-image:MODE_EDIT;background-color:transparent}.window .mode-toggle[aria-pressed=true]:hover{background-image:MODE_EDIT_CLICK;background-color:transparent}
.window .mode-toggle:disabled{background-image:MODE_LINK_DISABLED}.window .mode-toggle[aria-pressed=true]:disabled{background-image:MODE_EDIT_DISABLED}
.window .pen{width:26px;height:26px;background-image:EDIT_NORMAL;border:0}.window .pen:hover{background-image:EDIT_CLICK}
.window .eye{width:28px;height:28px;background-image:EYE_OPEN;border:0;opacity:1}.window .eye:hover{background-image:EYE_OPEN_HOVER}.window .eye.off{background-image:EYE_CLOSED;opacity:1}.window .eye.off:hover{background-image:EYE_CLOSED_HOVER}
.window .sourcecombo>button{height:30px;font-size:14px;background:#fcfcfc;border-color:#ccc7c6}.window .sourcecombo>button img{content:TRIANGLE_NORMAL;width:16px;height:16px}.window .sourcecombo>button:hover img,.window .sourcecombo>button[aria-expanded=true] img{content:TRIANGLE_CLICK}
.window select{appearance:none;background-image:TRIANGLE_NORMAL!important;background-repeat:no-repeat!important;background-position:right bottom!important;background-size:16px!important;padding-right:19px!important;height:30px}
.window .steppers button{font-size:0;position:relative}.window .steppers button:after{content:"";position:absolute;left:5px;top:4px;border-left:4px solid transparent;border-right:4px solid transparent;border-bottom:5px solid #cfb8a7}.window .steppers button:last-child:after{border-bottom:0;border-top:5px solid #cfb8a7}
.window .value-slider{height:40px;background:#fff;padding:0 8px;border:1px solid #999;box-shadow:1px 2px 3px #0005}
.window .value-slider input[type=range]{appearance:none;background:transparent;height:26px;outline:0}.window .value-slider input[type=range]::-webkit-slider-runnable-track{height:4px;background:#555;border-radius:0}.window .value-slider input[type=range]::-webkit-slider-thumb{appearance:none;width:10px;height:22px;margin-top:-9px;border:0;border-radius:0;background:THUMB_NORMAL center/contain no-repeat}.window .value-slider input[type=range]:active::-webkit-slider-thumb{background-image:THUMB_CLICK}
.window .range-slider input[type=range]::-webkit-slider-runnable-track{background:transparent}.window .range-slider:before{height:4px;background:#555}.window .range-slider input[type=range]::-webkit-slider-thumb{pointer-events:auto}
.window .value-slider:not(.range-slider) input[type=range]::-webkit-slider-runnable-track,.window .range-slider:before{background:linear-gradient(to right,#d7d9d8 0 var(--vm-low,0%),#4c4c4c var(--vm-low,0%) var(--vm-high,100%),#d7d9d8 var(--vm-high,100%) 100%)}
.window .footer{background:#e5e5e7}.window .footer button{background:#e1dede;border:1px solid #c5bfb9;border-radius:2px;color:#666;height:28px;min-width:66px}.window .footer button:hover{background:#fce3ce;border-color:#ff7900}.window .footer .primary{background:#e1dede;color:#666}
.window .feature-range .control{width:100%}.migration-summary{margin:14px 25px;padding:12px 16px;background:#fff;border:1px solid #d6dce0;border-left:3px solid #ff7900;font-size:12px;line-height:1.8}.migration-summary strong{color:#744315}.migration-summary a{text-decoration:underline}.native-assets{display:flex;gap:15px;flex-wrap:wrap;margin:12px 0}.native-assets figure{margin:0;background:#f2f3f4;padding:12px;min-width:105px}.native-assets img{width:auto;height:26px;object-fit:contain;margin-right:6px}.native-assets figcaption{font-size:12px;margin-top:8px;color:#666}.migration-table td{vertical-align:top}.migration-table code{font-size:11px;overflow-wrap:anywhere}
.source-size .window{height:750px}.window .shapes svg{opacity:.7}
.window .numeric input[aria-invalid=true]{border-color:#c64132!important;outline-color:#c64132}
.window .colorbox{position:relative;cursor:pointer;background:#f8f8f8;color:#111;gap:12px;height:30px}.window .colorbox:hover{border-color:#ff7900}.window .colorbox input[type=color]{position:absolute;opacity:0;width:1px;height:1px;pointer-events:none}.vm-text-color-chip{display:inline-block;width:24px;height:18px;flex:none}.window .colorbox span{font-size:14px}
.vm-style-popup,.vm-color-popup{position:fixed;box-sizing:border-box;color:#808080;font:14px "Microsoft YaHei","Segoe UI",sans-serif}.vm-style-popup{z-index:1100;width:260px;padding:6px 6px 6px 0;border:1px solid #cccccd;border-radius:4px;background:linear-gradient(#fcfcfc 15%,#f7f7f7)}
.vm-style-row{display:grid;grid-template-columns:102px minmax(0,1fr);height:32px;align-items:center;margin:0}.vm-style-row>span:first-child{padding-left:10px}.vm-color-value{display:flex;align-items:center;gap:10px;width:100%;height:28px;border:1px solid #cccccd;padding:0 4px;background:transparent;color:#000;text-align:left;white-space:nowrap}.vm-color-value i{display:block;width:24px;min-height:18px;flex:none}.vm-color-value span{font:14px Arial,sans-serif}.vm-color-value:hover,.vm-color-value[aria-expanded=true]{border-color:#ff7900}.vm-color-value:focus-visible{outline:1px dotted #111;outline-offset:-3px}
.vm-style-popup .scalar-setting{position:relative;display:flex;min-width:0;width:100%;padding:0!important}.vm-style-popup .numeric{height:28px}.vm-style-popup .numeric input{appearance:textfield;width:100%;min-width:0;height:28px;border:1px solid #ccc7c6;border-radius:2px;background:#fcfcfc;color:#808080;padding:2px 21px 2px 8px;font-size:14px}.vm-style-popup .numeric input::-webkit-inner-spin-button{appearance:none}.vm-style-popup .numeric input:focus{outline:1px solid #ff7900}.vm-style-popup .steppers button{font-size:11px;color:#cbb5a4}.vm-style-popup .value-slider{z-index:1101;top:30px;height:36px;padding:0 7px}.vm-style-popup .value-slider input{width:100%;height:24px;accent-color:#b7b9b8}
.vm-color-popup{z-index:1102;width:206px;background:#fff;border:1px solid #aaa}.vm-color-tabs{display:flex;gap:0;height:31px;padding:3px 8px 0;background:#fff;border-bottom:1px solid #ccc}.vm-color-tabs button{width:76px;border:0;border-bottom:2px solid transparent;background:#fafafa;color:#666;font-size:14px}.vm-color-tabs button[aria-selected=true]{background:#fceddf;border-bottom-color:#ff7900;font-weight:bold;color:#505050}
.vm-default-colors{padding:4px}.vm-palette-heading{height:23px;background:#dde7ee;border-bottom:2px solid #bfbfbf;color:#00008b;font:bold 11px Arial,"Microsoft YaHei",sans-serif;padding:5px 5px 0}.vm-palette-grid{display:grid;grid-template-columns:repeat(10,1fr);padding:0 2px;gap:0 4px}.vm-palette-grid.vm-palette-top{margin:4px 0 5px}.vm-palette-grid button{width:13px;height:13px;padding:0;border:solid #d0d0d0;border-width:0 1px}.vm-palette-top button{border-width:1px}.vm-palette-grid button:hover,.vm-palette-grid button:focus-visible,.vm-palette-grid button[aria-pressed=true]{outline:2px solid #feab38;outline-offset:-2px}
.vm-more-colors{display:flex;align-items:flex-start;gap:8px;padding:10px;background:#fff;min-height:160px}.vm-more-colors[hidden],.vm-default-colors[hidden]{display:none}.vm-color-chart{position:relative;width:138px;height:138px;overflow:hidden;cursor:crosshair;flex:none;touch-action:none}.vm-color-chart img{position:absolute;left:0;top:0;width:174px;height:138px;max-width:none;pointer-events:none}.vm-color-marker{position:absolute;width:12px;height:12px;border:2px solid #a52a2a;border-radius:50%;transform:translate(-50%,-50%);pointer-events:none}
.vm-color-brightness{appearance:none;writing-mode:vertical-lr;direction:rtl;width:15px;height:138px;margin:0;border:0;border-radius:0;background:linear-gradient(to top,#000,var(--vm-chart-base,#808080),#fff);cursor:pointer}.vm-color-brightness::-webkit-slider-runnable-track{width:15px;background:transparent}.vm-color-brightness::-webkit-slider-thumb{appearance:none;width:0;height:0;border-top:4px solid transparent;border-bottom:4px solid transparent;border-right:8px solid #a83f20;transform:translateX(12px)}
/* Focus uses the existing border; no second outer orange ring or added size. */
.window :is(input,select,button):focus,.window .colorbox:focus,.vm-style-popup .numeric input:focus{outline:none;box-shadow:none}
.window :is(input[type=number],input[type=text],select):focus,.window .sourcecombo>button:focus,.window .colorbox:focus,.vm-style-popup .numeric input:focus{border-color:#ff7900}
.window :is(button,select,input[type=number],input[type=text],input[type=radio],input[type=checkbox]):focus-visible,.window .colorbox:focus-visible,.vm-style-popup .numeric input:focus-visible{outline:1px dotted #333;outline-offset:-2px}
'''
mapping = {
    'SWITCH_OFF':'swith_off.png','SWITCH_ON':'swith_on.png',
    'RADIO_OFF':'check-box_off.png','RADIO_ON':'check-box_on.png',
    'LINK_NORMAL':'combo_ic_关联_normal.png','LINK_HOVER':'combo_ic_关联_hover.png','LINK_CLICK':'combo_ic_关联_click.png',
    'MODE_LINK':'ic_btn_link切换_normal.png','MODE_LINK_CLICK':'ic_btn_link切换_click.png','MODE_LINK_DISABLED':'ic_btn_link切换_disable.png',
    'MODE_EDIT':'ic_btn_edit切换_normal.png','MODE_EDIT_CLICK':'ic_btn_edit切换_click.png','MODE_EDIT_DISABLED':'ic_btn_edit切换_disable.png',
    'EDIT_NORMAL':'edit_normal.png','EDIT_CLICK':'edit_click.png',
    'EYE_OPEN':'see_normal.png','EYE_OPEN_HOVER':'see_click.png','EYE_CLOSED':'not-see_normal.png','EYE_CLOSED_HOVER':'not-_see_click.png',
    'TRIANGLE_NORMAL':'combo_ic_triangle_normal.png','TRIANGLE_CLICK':'combo_ic_triangle_click.png',
    'THUMB_NORMAL':'滑块_normal.png','THUMB_CLICK':'滑块_click.png',
}
for token in sorted(mapping, key=len, reverse=True):
    css = css.replace(token, image(mapping[token]))
CSS.write_text(css, encoding='utf-8')
palette_source = ROOT / 'src/Plugin.Wpf/Controls/Native/Display/VisionUi/SourceColorPalette.xaml'
palette_tree = ET.parse(palette_source)
ns = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'
palette_rows = [[button.attrib['Background'].lower() for button in grid.findall(ns+'Button')] for grid in palette_tree.iter(ns+'UniformGrid')]
assert len(palette_rows)==7 and all(len(row)==10 for row in palette_rows), 'Preserve all original palette rows.'
chart = ROOT / 'src/Plugin.Wpf/Themes/Assets/Native/Display/VisionUi/SourceAssets/colorchart.png'
chart_image = Image.open(chart).convert('RGB')
color_data = {'rows':palette_rows,'width':chart_image.width,'height':chart_image.height,'rgbBase64':base64.b64encode(chart_image.tobytes()).decode('ascii'),
              'chartUrl':url(chart,PAGE.parent),'source':palette_source.relative_to(ROOT).as_posix(),'chartSha256':hashlib.sha256(chart.read_bytes()).hexdigest()}
(PUBLIC / 'blob-vm-color-data.js').write_text('window.VMColorEvidence='+json.dumps(color_data,ensure_ascii=False,separators=(',',':'))+';\n',encoding='utf-8')

rows = [
    ('输入源下拉','SourceComboBoxes.xaml / ComboBox样式','src/Plugin.Wpf/Themes/Native/Input/VisionUi/SourceComboBoxes.xaml','原三角图、选项行、鼠标/选中态；候选仍是演示数据'),
    ('开关与单选','SourceBooleanToggle / NormalRadioButton','src/Plugin.Wpf/Themes/VmFieldStyles.xaml','原开关及单选图片；开关一行、参数下一行'),
    ('常量 / 订阅','EditLinkButton / NativeLinkButton','src/Plugin.Wpf/Themes/VmFieldStyles.xaml','原关联/编辑切换图片；本地值与来源两态、取消保留草稿'),
    ('数值与范围','NumericControl / SliderArrange / SliderArrangeFloat','src/Plugin.Wpf/Controls/Native/Numeric/numeric-migration-manifest.json','步进、聚焦弹滑条、失焦或Esc收起、双端不交叉'),
    ('眼睛与编辑','eyeToggleButton / NativeDisplayPopupButton','src/Plugin.Wpf/Themes/VmFieldStyles.xaml','原显隐/编辑图标，控制示意叠加层及样式弹窗'),
    ('页签与底部按钮','TopTabItemStyle / Orange_ButtonStyle','src/Plugin.Wpf/Themes/VmFieldStyles.xaml','按原模板对应规则呈现选中、悬停与三按钮布局'),
    ('ROI及颜色组合','RoiSelectControl / ROIStack / VmColorPicker / ImageViewConfig_subContent','src/Plugin.Wpf/Controls/VmColorPicker.cs','预览含就地样式面板及默认/更多色板；真实ColorField已接原色板、页签及高级算法，尚未运行验收。真实图形样式父层仍待从modal重组为就地四行面板；复杂ROI图标仍为示意'),
]
table = '<table class="migration-table"><thead><tr><th>部件</th><th>对应迁入对象</th><th>本次呈现和边界</th></tr></thead><tbody>'
for label, kind, path, state in rows:
    table += f'<tr><td>{label}</td><td>{kind}<br><a href="{url(ROOT / path, PAGE.parent)}">查看迁入文件</a></td><td>{state}</td></tr>'
table += '</tbody></table>'
gallery = '<div class="native-assets">'
for label,names in [('开关',['swith_off.png','swith_on.png']),('单选',['check-box_off.png','check-box_on.png']),('常量 / 订阅',['ic_btn_link切换_normal.png','ic_btn_edit切换_normal.png']),('关联',['combo_ic_关联_normal.png','combo_ic_关联_hover.png']),('显示 / 隐藏',['see_normal.png','not-see_normal.png']),('滑块',['滑块_normal.png','滑块_click.png'])]:
    gallery += '<figure>' + ''.join(f'<img alt="{html.escape(name)}" src="{url(ASSETS/name, PAGE.parent)}">' for name in names) + '<figcaption>'+label+' · 迁入原图</figcaption></figure>'
gallery += '</div>'
panel = '<div class="note" id="migrated"><h2>本次预览用了哪些已迁入控件</h2><p>原图直接读取本项目控件资源，不另画替代图。WPF模板规则转成浏览器CSS，交互由HTML演示；它不是WPF程序截图，不代表迁移验收通过。</p>'+gallery+table+'<p><b>建议审核：</b>依次检查输入源下拉、ROI绘制/继承、运行参数常量/订阅、数值聚焦与双端滑条、判断开关展开、眼睛显隐、图形样式的OK/NG颜色及文本默认/更多色板。颜色选择修改私有草稿，窗口取消回退。VM实际截图仍在原导航保留。</p><p><b>产品现状：</b>新候选仍有跨组引用和依赖构建错误；本页不能代替Release运行、HALCON或人工验收。复杂ROI工具栏仍为示意，未称整体外观验收通过。</p></div>'
s = PAGE.read_text(encoding='utf-8')
presentation = ROOT / 'tools/SunEyeVision.BlobAnalysis/Runtime/BlobPresentation.cs'
count_range = re.search(r'if\(key=="setting.BlobCountLimit"\)\{[^\n]*minimum=(\d+);maximum=(\d+)', presentation.read_text(encoding='utf-8'))
assert count_range, 'Use the actual declared Blob count domain, never invent a slider limit.'
s=s.replace('subNumber("limit",1000,1)',f'subNumber("limit",1000,{count_range[1]},{count_range[2]})')
s=s.replace('(y<=1?.001:1)','(y<=1?.01:1)')
old_start = 'document.querySelectorAll("[data-scalar] .numeric input").forEach(input=>input.addEventListener("click"'
if old_start in s:
    start = s.index(old_start)
    end = s.index('function sourceOpen(open)', start)
    s = s[:start] + 'subscriptionPaint();\nfunction closeValueSliders(){window.vmNumericInteraction?.closeAll();}\n' + s[end:]
interaction_hash = hashlib.sha256((PUBLIC / 'blob-vm-numeric-interaction.js').read_bytes()).hexdigest()[:12]
interaction_script = '<script src="../公共资源/blob-vm-numeric-interaction.js?v=' + interaction_hash + '"></script>'
interaction_pattern = r'<script src="\.\./公共资源/blob-vm-numeric-interaction\.js(?:\?v=[a-f0-9]+)?"></script>'
if re.search(interaction_pattern,s):
    s=re.sub(interaction_pattern,lambda _:interaction_script,s,count=1)
else:
    s = s.replace('</body>', interaction_script + '</body>')
for name in ('blob-vm-color-data.js','blob-vm-color-interaction.js'):
    digest=hashlib.sha256((PUBLIC/name).read_bytes()).hexdigest()[:12]
    tag='<script src="../公共资源/'+name+'?v='+digest+'"></script>'
    pattern=r'<script src="\.\./公共资源/'+re.escape(name)+r'(?:\?v=[a-f0-9]+)?"></script>'
    if re.search(pattern,s):s=re.sub(pattern,lambda _:tag,s,count=1)
    else:s=s.replace('</body>',tag+'</body>')
# Remove the old page-wide exterior halo too: anchored popovers live outside .window.
# Outline is drawn inward and does not alter the border width, padding or layout.
s=s.replace('button:focus-visible,a:focus-visible,input:focus-visible,select:focus-visible{outline:2px solid #ed903f;outline-offset:2px}',
            'button:focus-visible,a:focus-visible,input:focus-visible,select:focus-visible{outline:1px dotted #333;outline-offset:-2px}')
s=s.replace('.switch input:focus-visible+i{outline:2px solid #e99b55;outline-offset:2px}',
            '.switch input:focus-visible+i{outline:1px dotted #333;outline-offset:-2px}')
css_digest=hashlib.sha256(CSS.read_bytes()).hexdigest()[:12]
link = '<link id="migrated-controls-css" rel="stylesheet" href="../公共资源/blob-migrated-controls.css?v='+css_digest+'">'
if 'id="migrated-controls-css"' not in s:s=s.replace('</head>',link+'</head>')
else:s=re.sub(r'<link id="migrated-controls-css"[^>]+>',lambda _:link,s,count=1)
if 'id="migrated"' in s:s=re.sub(r'<div class="note(?: active)?" id="migrated">[\s\S]*?</div>(?=<div class="note|</section>)',lambda _:panel,s,count=1)
else:s=s.replace('<div class="note active" id="differences">',panel+'<div class="note active" id="differences">')
if 'data-note="migrated"' not in s:s=s.replace('<section class="notes"><nav>','<section class="notes"><nav><button data-note="migrated">迁入控件与本次变化</button>')
s=re.sub(r'<div class="notice">[\s\S]*?</div>', '<div class="notice"><span class="badge">迁入资源版 · 待审核</span><span>本次使用迁入的原开关、单选、订阅/编辑、眼睛、下拉和滑块图片。HTML演示不执行HALCON，WPF新候选尚未编译通过。</span></div>',s,count=1)
s=s.replace('非WPF产品 · 未实施 · 未验收','迁入资源的HTML预览 · 非WPF运行结果')
s=s.replace('本轮仅方案，未迁移或删除产品文件。','部分源码、模板与原图已迁入；31类旧消费者的完整退出与本次构建尚未通过，不按清单文字宣称完成。')
s=s.replace('模式尚未在产品实现。','产品模式已实施；此处仅演示界面，不执行真实图像计算。')
s=s.replace('单阈值与不二值化是重构推荐，双阈值承接现行区间能力；不宣称与VM公式逐值相同。','产品已支持单阈值、双阈值及不二值化；本页仅做示例交互，不宣称与VM native公式逐值相同。')
s=s.replace('该调整尚未实施，详见下方算法差异。','产品已将两项移交上游工具；本页保留演示边界，详见原方案。')
s=s.replace('本方几何参数组装与成员绑定仍需设计和实施。','本方四类几何继承已接入；本页不模拟真实工作流来源解析。')
s=s.replace('移出预处理/形态学为本方调整推荐，未实施；','预处理/形态学已移到上游专用工具；')
s=s.replace('当前Group直接生成五页','声明已按三页分组')
s=s.replace('当前以代码创建WPF元素','当前按声明创建WPF元素，部分原模板已接入')
s=s.replace('步进、聚焦弹滑条、失焦或Esc收起、双端不交叉','按原源码：聚焦弹出、文本失焦提交、拖动松手通知、轨道按步长增减；双端按中点分区且不交叉')
s=s.replace('<div class="note" id="migrated">','<div class="note active" id="migrated">').replace('<div class="note active" id="differences">','<div class="note" id="differences">')
s=s.replace('<button data-note="migrated">','<button class="active" data-note="migrated">').replace('<button class="active" data-note="differences">','<button data-note="differences">')
PAGE.write_text(s,encoding='utf-8')
receipt = {'date':'2026-10-26','preview':PAGE.relative_to(ROOT).as_posix(),
           'kind':'browser translation of migrated WPF resources, not WPF rendering',
           'assets':assets,'components':[{'label':a,'type':b,'source':c,'scope':d} for a,b,c,d in rows],
           'remaining':['complex ROI toolbar remains illustrative','WPF unified build still failing; latest run 003 has 40 errors','no production or manual acceptance claimed']}
receipt['color_popup_evidence'] = {'style':'ImageViewConfig_subContent: width260; four rows32; OK/NG, opacity0..1 step0.01, width1..10 step1',
    'palette':'ColorPickerControlView: Default/More tabs; SourceColorPalette: exact70 colors; SourceAdvancedColorPicker: actual colorchart pixels and linear-scRGB brightness',
    'draft':'palette selection updates private browser draft; outside click closes; window cancel restores capture; no generic HEX modal',
    'files':[{ 'path':p.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in [palette_source,chart,
       ROOT/'src/Plugin.Wpf/Controls/Native/Display/VisionUi/SourceAdvancedColorPicker.xaml',
       ROOT/'src/Plugin.Wpf/Controls/Native/Display/VisionUi/SourceAdvancedColorPicker.xaml.cs',
       ROOT/'src/Plugin.Wpf/Themes/Native/Composition/Apps.XmlUI/Apps.XmlUI.ParamTabControl/ColorPickerControlView.xaml.pending',
       ROOT/'src/Plugin.Wpf/Themes/Native/Composition/Apps.XmlUI/styles/paramusercontroldatatemplate.xaml.pending']]}
receipt['numeric_interaction_evidence'] = {
    'NumericControl.xaml':'ValueText Text binding UpdateSourceTrigger=LostFocus',
    'NumericControl.xaml.cs':'TextBox_GotFocus; ForceUpdateSource; ValueText_MouseWheel; UpButton_Click/DownButton_Click',
    'SliderArrange.xaml':'PART_Popup/CBox.IsDropDownOpen; LargeChange=SliderTickFrequency; RepeatButton Slider.IncreaseLarge/DecreaseLarge',
    'SliderArrange.xaml.cs':'PART_Popup_Closed forces both Text bindings before notifications',
    'SliderArrangeBase.cs':'ClipSilder midpoint hit regions; SL_Bat1/2_ValueChanged bounds; Thumb_MouseLeftButtonUp notifications',
    'preview_translation':'blob-vm-numeric-interaction.js; not WPF runtime equivalence'}
receipt['numeric_source_files'] = []
receipt['quantity_slider_range']={'minimum':int(count_range[1]),'maximum':int(count_range[2]),'source':presentation.relative_to(ROOT).as_posix()}
receipt['user_requested_keyboard_behavior']='Valid Enter validates numeric draft, closes its popup and releases focus; invalid Enter retains focus and reports error. Never executes or confirms whole window. Arrow keys use declared step.'
for name in ('NumericControl.xaml','NumericControl.xaml.cs','SliderArrange.xaml','SliderArrange.xaml.cs','SliderArrangeBase.cs','RangeRule.cs'):
    source = ROOT / 'src/Plugin.Wpf/Controls/Native/Numeric/AppsXmlUi' / name
    receipt['numeric_source_files'].append({'path':source.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(source.read_bytes()).hexdigest()})
(PUBLIC / 'blob-migrated-controls.provenance.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'assets':len(assets),'component_groups':len(rows),'preview':PAGE.relative_to(ROOT).as_posix()},ensure_ascii=True))
