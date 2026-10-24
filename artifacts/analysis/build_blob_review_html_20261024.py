"""Documentation-only integration. No product build, probe, or code change."""
from pathlib import Path
import hashlib, html, json, os, re
from urllib.parse import quote

ROOT = Path(__file__).resolve().parents[2]
DOCROOT = ROOT / 'docs/04-工具设计/架构对照/2026/10/21/自研架构文档维护'
PAGE = DOCROOT / '优化方案/optimization-o06.html'
DATA = json.loads((ROOT / 'artifacts/analysis/blob-five-layer-review-20261024.json').read_text(encoding='utf-8'))
esc = lambda x: html.escape(str(x), quote=True)

def href(path, origin=PAGE.parent):
    p = Path(path)
    if not p.is_absolute(): p = ROOT / p
    return quote(os.path.relpath(p, origin).replace('\\', '/'), safe='/:#')

def source_link(path, line=None):
    return f'<a href="{href(path)}">{esc(Path(path).name)}{":"+str(line) if line else ""}</a>'

# Public reusable visual classes, not every helper/model/event args under a Controls directory.
families = {
 'ToolDebugControlBase': ('窗口与会话','工具调试内容基类、初始化/执行与绑定问题呈现','调整','收敛为宿主编辑上下文适配器；运行、保存与取消只走一个正式事务入口。'),
 'DefaultDebugWindow': ('窗口与会话','默认工具窗口壳','调整','只负责布局、命令转发和关闭意图，不拥有第二份正式参数或调度器。'),
 'CustomDebugWindow': ('窗口与会话','专业窗口壳','调整','与默认壳共享初始化/编辑会话/关闭合同；工具专有内容保留，不再各自持久化。'),
 'SdkDialogWindow': ('窗口与会话','通用对话窗口壳','复用并验','保持主题/窗口语义；对话结果与业务提交分离，核设计时和运行时。'),
 'ParamBindingBase': ('参数与绑定','来源树、类型过滤、绑定问题基类','调整','来源结构与兼容判断由统一类型/绑定合同提供，控件不自建另一份类型系统。'),
 'ConfigSetting': ('参数与绑定','常量/绑定模式、多类型参数编辑','调整','复用外观及模式切换；pending文本、CLR精度、0/false/missing和校验交给同一会话合同。'),
 'ParamBinding': ('参数与绑定','只绑定的来源选择','调整','统一稳定来源地址、兼容类型和失效提示，不凭显示字符串解析正式引用。'),
 'ImageSourceSelector': ('参数与绑定','图像输入来源选择','调整','复用选择器；来源归查询服务，预览reader与正式输入引用分离，换源清理订阅。'),
 'NumericUpDown': ('参数与绑定','数值文本与步进','复用并补验','复用已验证pending/整数改动；全UInt64、文化与各宿主入口尚需真实控件验证，不冒全范围通过。'),
 'ParamTextBox': ('参数与绑定','参数文本输入','复用并补验','统一UpdateSource/校验时点，空字符串与无值分开；不能与数值控件各自提交正式状态。'),
 'ParamComboBox': ('参数与绑定','枚举/候选项选择','复用并补验','候选项身份与显示名分离；换候选、空选和绑定来源失效按同一合同处理。'),
 'RangeInputControl': ('参数与绑定','上下界输入','调整','上下界原子验证和提交，保留非法草稿而不写半个范围；明确开闭区间与无界值。'),
 'RangeSlider': ('参数与绑定','范围拖动交互','复用并补验','只产生草稿编辑意图；拖动反馈节流与最终提交分开，避免每像素运行算法。'),
 'ToggleSwitch': ('基础呈现','布尔开关','复用并验','保持纯值控件；false不是missing，开关不直接调度工作流。'),
 'LabeledControl': ('基础呈现','标签与内容布局','复用并验','仅管对齐/可访问性/主题，不夹带参数解析或业务状态。'),
 'ParamGroup': ('基础呈现','参数分组与折叠','复用并验','折叠不卸载正式数据；可见性只影响呈现，不隐式清空参数。'),
 'ToolIcon': ('基础呈现','工具图标绘制','复用并验','复用统一图标与主题/DPI规则，避免工具私有重复资源。'),
 'ColorPicker': ('颜色与样式','颜色选择组合控件','复用并验','明确草稿颜色/确认取消、Alpha与颜色编码；复用主题资源。'),
 'ColorSelector': ('颜色与样式','颜色选择器内部可视部件','复用并验','作为ColorPicker实现部件管理，不升成第二套业务颜色配置服务。'),
 'DispItemColorControl': ('颜色与样式','图形显示颜色设置','调整','绑定显示配置草稿，不直接修改共享renderer；线宽/DPI规则由显示合同约束。'),
 'DispTextColorControl': ('颜色与样式','文字显示颜色设置','调整','文字坐标、缩放与样式合同独立明确；样式变化只失效所属显示批次。'),
 'ToolBarControl': ('公共交互','通用工具栏呈现','复用并调整','只呈现可用命令与选中态；工具栏不管理正式ROI、视口资源或业务运行。'),
 'RoiEditorManagerControl': ('ROI与绘图','检测/屏蔽两编辑器的事件路由与渲染协调','替换越界职责','保留可复用外观和renderer适配；正式状态/会话/视口归属迁回宿主合同，不在管理器继续叠加仓库和调度。'),
 'RoiEditorControl': ('ROI与绘图','单组ROI编辑内容','调整','绘制/继承/选择编辑统一作用于字段草稿；Apply/Cancel/Undo与外层参数同事务。'),
 'RoiSubscribePanel': ('ROI与绘图','ROI继承/来源面板','调整','继承地址及模式由合同声明；不能复制上游运行对象当正式配置或允许越界编辑。'),
 'ShapeToolBar': ('ROI与绘图','ROI形状命令工具栏','复用并调整','复用命令呈现；可绘形态由字段声明，不硬编码为全工具一套能力。'),
 'DrawingParameterPanel': ('ROI与绘图','ROI几何参数面板','调整','数值和拖动双向一致；共享坐标/单位/角度及校验，不自成一套参数回填。'),
 'NodeSelectorPopup': ('ROI与绘图','节点/输出选择弹层','调整','复用弹层，来源树来自统一查询；关闭时解除外部订阅，不持全方案强引用。'),
 'RoiVisualHost': ('渲染实现部件','ROI底层可视宿主','复用并验','内部渲染部件，不作为工具直接操作的公共业务入口；显式视口作用域和生命周期。'),
 'ImageControl': ('图像与结果显示','底图/交互图形/结果图形的视口组合','调整','保留三层能力；Frame/坐标/DPI/图像寿命由同一呈现批次约束，不让每工具清全局层。'),
 'ResultRenderLayer': ('图像与结果显示','批量结果图形绘制层','复用并调整','复用批量绘制；补同run/frame/SourceElements选择消费、裁剪/失效与正式性能实测。'),
 'MeasureEditorControl': ('测量与卡尺','测量引导图形编辑','调整','复用线/圆/弧既有手势；参数草稿/字段与坐标合同统一，不重造ROI式第二管理框架。'),
 'MeasurementEditorManagerControl': ('测量与卡尺','测量编辑器协调','替换越界职责','保留可复用视图；会话与正式参数所有权回归host，避免与ROI管理器重复状态。'),
 'MeasureElementsEditorControl': ('测量与卡尺','测量元素集合编辑','调整','集合元素身份与显示序号分开；批量编辑/取消/删除经同一事务。'),
 'MeasureGuideParameterPanel': ('测量与卡尺','引导几何参数','调整','复用字段编辑，坐标/角度与图形方向一致，数值回填不旁路校验。'),
 'MeasureToolBar': ('测量与卡尺','测量工具命令栏','复用并调整','可用命令由字段能力声明；命令与编辑会话分离，不能直接修改运行对象。'),
 'JudgmentEditorControl': ('结果配置与分析','判断条件编辑','复用并调整','编辑结果判断配置草稿，条件组合和非法范围在提交前统一验证。'),
 'GraphicsDisplayEditorControl': ('结果配置与分析','图形显示配置编辑','复用并调整','编辑声明字段及显示样式，不拥有运行结果或renderer实例。'),
 'TextDisplayEditorControl': ('结果配置与分析','结果文字配置编辑','复用并调整','字段选择/格式/坐标单位有类型约束，长文字与不同缩放按显示合同验。'),
 'CommunicationDataAnalysisControl': ('专门公共能力','通信结果分析视图','条件复用','只对通信消费者复用，不为Blob抽象成万能结果面板；沿正式输出快照只读显示。'),
 'ScriptEditorCore': ('专门公共能力','AvalonEdit/Roslyn脚本编辑核心','条件复用','保留已测基础预热/会话缓存，脚本UI、语义服务和运行版本分离；不成为Blob依赖。')
}

scanroots = [ROOT/'src/Plugin.SDK/UI/Controls', ROOT/'src/Plugin.SDK/UI/Windows', ROOT/'src/CommonControl']
all_sources = []
for root in [ROOT/'src', ROOT/'tools']:
    for current, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d not in {'bin','obj','.git','.vs'}]
        for f in files:
            if f.endswith(('.cs','.xaml')):
                p = Path(current)/f
                all_sources.append((p, p.read_text(encoding='utf-8-sig', errors='replace')))
classes = {}
for folder in scanroots:
    for p in folder.rglob('*.cs'):
        if any(part in {'bin','obj'} for part in p.parts): continue
        text = p.read_text(encoding='utf-8-sig', errors='replace')
        for match in re.finditer(r'^\s*public\s+(?:(?:abstract|sealed|partial)\s+)*class\s+(\w+)\s*:\s*([^\n{]+)', text, re.M):
            name, base = match.groups()
            if name not in families: continue
            consumers = [str(q.relative_to(ROOT)).replace('\\','/') for q,t in all_sources
                         if q != p and (re.search(r'<(?:\w+:)?'+re.escape(name)+r'(?:\s|/|>)', t)
                                        or re.search(r'\bnew\s+'+re.escape(name)+r'\s*\(', t))]
            lines = text[:match.start()].count('\n')+1
            dp = re.findall(r'DependencyProperty\.Register(?:Attached|ReadOnly)?\s*\(\s*(?:nameof\((\w+)\)|"([^"]+)")', text)
            events = [x for x in ['Loaded','Unloaded','DataContextChanged','Closed','Closing','OnApplyTemplate','Dispose'] if re.search(r'\b'+x+r'\b', text)]
            classes[name] = dict(name=name, base=base.strip(), source=str(p.relative_to(ROOT)).replace('\\','/'),line=lines,
                sha256=hashlib.sha256(p.read_bytes()).hexdigest(),directConsumerFiles=consumers,
                dependencyProperties=[a or b for a,b in dp],lifecycleMarkers=events,
                category=families[name][0],role=families[name][1],recommendation=families[name][2],design=families[name][3])
missing = sorted(set(families)-set(classes))
if missing: raise RuntimeError('Unlocated named controls: '+str(missing))
inventory = dict(kind='source-based-public-control-design-review',scope='Plugin.SDK UI controls/windows and CommonControl public visual classes; render parts and dedicated capabilities explicitly separated',
    count=len(classes),behaviorAcceptance=False,rule='Consumer search counts only XAML instantiation or new expressions; reflection/template-only consumers can be missing. Lifecycle markers are navigation evidence, not proof of correct disposal.',controls=list(classes.values()))
(ROOT/'artifacts/analysis/blob-shared-controls-inventory-20261024.json').write_text(json.dumps(inventory,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

parts = []
parts.append('''<section id="blob-five-layer-full-chain-review" class="bfr" aria-labelledby="bfr-title">
<style>
.bfr{margin:22px 0;padding:22px;border:1px solid #568399;border-radius:14px;background:#102632;color:#dcebf0;line-height:1.65;scroll-margin-top:60px}.bfr *{box-sizing:border-box}.bfr h2{font-size:26px;margin:0 0 8px}.bfr h3{font-size:20px;margin:24px 0 10px;color:#90d9f5}.bfr h4{font-size:16px;margin:6px 0}.bfr p{margin:7px 0}.bfr a{color:#9bdcff}.bfr .muted{color:#b2c7d1}.bfr .lead{font-size:17px}.bfr .notice{padding:12px 15px;border-left:4px solid #f4b45a;background:#24313a;margin:12px 0}.bfr .cards{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}.bfr .card{padding:14px;background:#173544;border:1px solid #41697e;border-radius:9px;min-width:0}.bfr .tags{display:flex;gap:8px;flex-wrap:wrap}.bfr .tag{display:inline-block;padding:2px 8px;border-radius:12px;border:1px solid #547488;font-size:12px}.bfr .verified{color:#97e3bb;border-color:#548571}.bfr .pending{color:#ffd08d;border-color:#947846}.bfr .flow{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px;margin:12px 0;counter-reset:bfstep}.bfr .flow>.arrow{display:none}.bfr .step:before{counter-increment:bfstep;content:counter(bfstep);display:inline-block;margin-right:8px;color:#9edcf0;font-weight:bold}.bfr .step{background:#193c4c;border:1px solid #4f849a;border-radius:8px;padding:9px 12px;flex:1;min-width:120px}.bfr .arrow{align-self:center;color:#9bc8da;font-size:23px}.bfr .scroll{overflow:auto;margin:12px 0}.bfr table{width:100%;border-collapse:collapse;margin:0;background:#102a38;font-size:14px}.bfr th,.bfr td{border:1px solid #3c6073;padding:10px;vertical-align:top;text-align:left;overflow-wrap:anywhere}.bfr th{background:#1d4051;color:#d8eff7}.bfr table.compare{min-width:940px}.bfr .lane{display:grid;grid-template-columns:145px 1fr;gap:10px;margin:8px 0}.bfr .lane strong{padding:12px;background:#214859;border-radius:7px}.bfr .lane>div{padding:12px;border:1px solid #436b7e;border-radius:7px}.bfr .verify-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:10px}.bfr details{background:#153442;border:1px solid #40697e;border-radius:8px;padding:10px 13px;margin:8px 0}.bfr summary{cursor:pointer;font-weight:650;color:#d4edfa}.bfr .small{font-size:13px}.bfr input{width:100%;padding:11px 14px;border:1px solid #7193a5;border-radius:7px;background:#0c2634;color:#eef8fc;font-size:15px}.bfr .controls{min-width:960px}.bfr .controls td:nth-child(1){width:19%}.bfr .controls td:nth-child(2){width:27%}.bfr .controls td:nth-child(3){width:31%}.bfr code{font-size:12px;overflow-wrap:anywhere}.bfr .two{display:grid;grid-template-columns:1fr 1fr;gap:12px}.bfr .state{border-left:4px solid #7cacca;padding:12px;background:#183744}.bfr .nav{display:flex;gap:12px;flex-wrap:wrap;margin:14px 0}.bfr [hidden]{display:none!important}@media(max-width:900px){.bfr .cards,.bfr .two{grid-template-columns:1fr}.bfr .verify-grid{grid-template-columns:1fr}.bfr .lane{grid-template-columns:1fr}.bfr .flow{grid-template-columns:repeat(2,minmax(0,1fr))}.bfr{padding:14px}.bfr h2{font-size:22px}}
</style>
<div class="tags"><span class="tag">方案审核 · 2026-10-24</span><span class="tag pending">未实施 · 未整链验收</span><span class="tag verified">复用已有源码与有限证据</span></div>
<h2 id="bfr-title">Blob完整业务链 × 五层架构 × 共享控件设计</h2>
<p class="lead">从工具箱拖入画布开始，验证用户能完成什么、UI如何实现、数据如何执行、算法是否正确、结果怎样被消费和释放。</p>
<div class="notice"><b>推荐：</b>用Blob真实消费者收敛公共合同；现有代码和VM均无优先权。保留已证能力，调整越界职责，补真实接线。不增加批次、不改产品代码、不以HTML示意代替实机验收。模板专有模型资产不是Blob前置条件。</div>
<nav class="nav" aria-label="Blob方案目录"><a href="#blob-five-layer-comparison">五层对照</a><a href="#blob-parameter-result-flow">数据与职责</a><a href="#blob-algorithm-review">算法链</a><a href="#shared-control-design-inventory">共享控件逐项设计</a><a href="#blob-verification-steps">12步验证</a><a href="#blob-optimization-list">优化清单</a><a href="#blob-evidence-gaps">待确认与证据</a></nav>
''')

def flow(items):
    return '<div class="flow">'+'<span class="arrow" aria-hidden="true">→</span>'.join('<div class="step">'+esc(x)+'</div>' for x in items)+'</div>'
parts.append(flow(['工具箱拖放 / 节点身份','连接与输入绑定','窗口 / 参数 / ROI','提交或取消','算法执行','树 / 图形 / 下游','保存重开','关闭 / 退休 / 释放']))
parts.append('<h3 id="blob-five-layer-comparison">① 五层对照：VM、当前、目标逐项对齐</h3><p class="muted">当前源码观察基线 '+esc(DATA['observedHead'][:12])+'；在途编辑修复与历史有限测试分开。VM XML和导出名不能证明原生算法体。</p><div class="scroll"><table class="compare"><thead><tr><th>层面</th><th>VM已证 / 边界</th><th>我们当前</th><th>推荐与取舍</th></tr></thead><tbody>')
for row in DATA['layers']:
    parts.append('<tr>'+''.join('<td>'+esc(row[k])+'</td>' for k in ['name','vm','current','recommendation'])+'</tr>')
parts.append('</tbody></table></div>')
parts.append('''<h3 id="blob-parameter-result-flow">② 框架与工具职责：配置链和结果链分开</h3><div class="cards">
<div class="card"><h4>Blob工具声明 / 实现</h4><p>图像输入、业务参数、检测/屏蔽字段、算法与输出类型。描述可绘能力和结果含义。</p><b>不承担：</b>全局选中、宿主窗口生命周期、跨节点图层清理、工作流调度。</div>
<div class="card"><h4>框架宿主负责</h4><p>节点身份、来源查询、编辑会话、全字段校验、唯一提交、执行快照、Undo/Redo、方案持久化及结果分发。</p><b>复用：</b>既有Store-CAS、CaptureForRun、Snapshot/Repository。</div>
<div class="card"><h4>公共控件负责</h4><p>把声明适配为输入、来源选择、绘图命令和只读展示；通过会话回填草稿。</p><b>不承担：</b>第二套正式参数库、独立类型系统或运行缓存权威。</div></div>
<div class="lane"><strong>配置写入链</strong><div>工具声明 → 控件/绘图编辑草稿 → 全量pending与跨字段校验 → 唯一CAS提交 → 明确配置版本 → CaptureForRun</div></div>
<div class="lane"><strong>运行只读链</strong><div>同代参数 + 输入reader + ROI/修正快照 → Blob算法 → 本次输出快照；不读取活动WPF控件，也不反写用户草稿。</div></div>
<div class="lane"><strong>结果消费链</strong><div>输出声明 + Run/Frame/Field/Element身份 → 结果树分页 / 批量renderer / 下游类型绑定；排序序号不是元素身份。</div></div>
<div class="lane"><strong>资源寿命链</strong><div>新结果替换 → 旧Owner退休 → 树/视口/下游逐个释放reader → 最后读者离开才释放native资源。关闭一个工具窗不能清空别人的结果。</div></div>
<p class="small">这里是目标职责图，不表示Blob已接入全部生产入口。<a href="#framework-tool-final-review">既有最终合同</a>、<a href="#public-control-capability-audit">旧公共能力盘点</a>继续保留，避免另起接口。</p>
''')
parts.append('<h3 id="blob-algorithm-review">③ 核心算法：按实际函数体拆分，不按界面标签猜实现</h3>')
parts.append(flow(['输入解包与校验','灰度转换','检测 / 屏蔽 / 位置修正','按域预处理（可选）','阈值 / 域内反相','形态学并限制有效域','Connection连通域','SelectShape / 特征 / 判断','排序及数量截断','按需轮廓与输出发布']))
parts.append('''<div class="two"><div class="card"><h4>我们已证</h4><p>HALCON函数调用、筛选及排序后的统一集合、按需轮廓和批量显示有源码；Blob输出投影、资源Owner和几何显示有有限测试。源码注释“按需计算”仍须与实际特征调用和耗时分别核对。</p><p>算法段计时 ≠ 按钮到可见帧延迟；本轮无新增性能数值。</p></div><div class="card"><h4>VM证据边界</h4><p>原模块是IMVSBlobFindModu，非BlobFindLabels。托管Run/结果查询、XML和native导入已证。补充静态片段：Process入口分配80000字节至+0x15D98；DeInit释放该字段并调用DestroyBlobFindToolInstance后清零；尾跳及实际析构目标未贯通。</p><p><b>仍未知：</b>ProcessAlg/ProcessOutput内部顺序、阈值/连通谓词、输出排序及完整释放。不得称已还原VM算法或证明其整体更快。</p></div></div>
<div class="notice"><b>同样标准：</b>同一像素输入、ROI/屏蔽、坐标修正、分割及筛选需求，分别检验独立真值、VM输出与当前输出；比较正确性、算法分段时间和用户可见延迟。现有HALCON实现不自动保留，VM也不是默认真值。</div>
''')

parts.append(f'<h3 id="shared-control-design-inventory">④ 共享控件怎样管理：{len(classes)}项实际类逐件审查</h3><p>范围：Plugin.SDK的工具公共控件/窗口、CommonControl脚本编辑核心；底层渲染部件和专门能力单列，不把ViewModel、事件参数、转换器或helper算作控件。表中是声明与接线的源码审查，<b>不是全库行为验收通过</b>。</p>')
parts.append('''<div class="cards"><div class="card"><h4>当前并非一个统一控件注册中心</h4><p>原生C#控件以DependencyProperty、DefaultStyleKey、XAML模板/资源字典组合；工具XAML或factory直接创建。基窗口和初始化接口注入节点、参数、查询服务、图像视口。ROI/Measure管理器及工具代码仍分担事件与渲染状态。</p></div><div class="card"><h4>借鉴VM</h4><p>公共组/控件目录 + 参数声明 + 类型模板 + 统一页面宿主。VM ParamsConfig更换时Dispose旧AlgorithmTabRoot，再创建/复用、初始化并绑定新root。原Blob通过XML复用公共组，不需要专属Control DLL。</p></div><div class="card"><h4>我们的取舍</h4><p>保留WPF控件和主题资源，不照搬VM整套XML语言；统一“控件消费字段声明与会话”的合同。控件库管理关注依赖方向、可用能力、实例归属、版本和验收，不为了统一名称再造万能管理器。</p></div></div>
<label for="bfr-control-search">按控件、职责、建议或消费者文件筛选</label><input id="bfr-control-search" type="search" placeholder="例如：ROI、ConfigSetting、结果、Script…" autocomplete="off"><p id="bfr-control-count" class="small" aria-live="polite"></p>
<div class="scroll"><table class="controls"><thead><tr><th>控件 / 所属层</th><th>当前机制与状态归属</th><th>逐项设计推荐</th><th>实际消费者 / 验证</th></tr></thead><tbody>
''')
for name, row in classes.items():
    family, role, decision, design = families[name]
    dps = '、'.join(row['dependencyProperties'][:12]) or '无直接DP声明（可能继承或使用XAML绑定）'
    life = '、'.join(row['lifecycleMarkers']) or '本文件无上述生命周期标记；不能据此断言泄漏'
    consumers = row['directConsumerFiles']
    links = '<br>'.join(source_link(p) for p in consumers[:4]) or '未检出直接XAML/new消费；模板/反射路径需补核'
    search = esc(' '.join([name,family,role,decision,design,*consumers]).lower())
    parts.append(f'<tr data-control-row data-search="{search}"><td><b>{esc(name)}</b><br><span class="tag">{esc(family)}</span><br>{source_link(row["source"],row["line"])}</td><td>{esc(role)}<details><summary>源码合同与生命周期</summary><p>继承：{esc(row["base"])}</p><p>DP：{esc(dps)}</p><p>标记：{esc(life)}</p><p class="small">标记只用于定位，外部订阅与释放正确性需沿调用链验证。</p></details></td><td><span class="tag pending">{esc(decision)}</span><p>{esc(design)}</p></td><td>{links}<p class="small">直接消费文件数 {len(consumers)}；仅为静态引用，不计功能通过。</p></td></tr>')
parts.append('''</tbody></table></div><p class="small">逐件共同验收：声明/类型一致 → 常量/绑定/空值 → pending与合法性 → 只改草稿 → 提交/取消/重开 → 外部订阅和reader释放 → 设计时/运行时/DPI/主题。测量和脚本专门控件不强塞Blob依赖；实现helper不扩张为新公共API。</p>
<script>(()=>{const root=document.getElementById('blob-five-layer-full-chain-review');const input=root.querySelector('#bfr-control-search');const rows=[...root.querySelectorAll('[data-control-row]')];const count=root.querySelector('#bfr-control-count');function update(){const q=input.value.trim().toLowerCase();let n=0;for(const r of rows){r.hidden=!!q&&!r.dataset.search.includes(q);if(!r.hidden)n++;}count.textContent='显示 '+n+' / '+rows.length+' 项；筛选只改变文档显示。';}input.addEventListener('input',update);update();})();</script>
''')
parts.append('<h3 id="blob-verification-steps">⑤ 验证步骤：按用户操作贯通五层</h3><p>先确认场景和可观察结果，再执行。建议基础样本：3个独立目标，检测区包含全部、屏蔽1个得到2个；另备0/1/大集合、触边/孔洞/相连目标。此处定义验证要求，尚未执行本轮整链。</p><div class="verify-grid">')
for case in DATA['verification']:
    parts.append(f'<details><summary>{esc(case["id"])} · {esc(case["action"])}</summary><p><b>判定：</b>{esc(case["oracle"])}</p><p class="small">记录：样本与配置版本、节点/Run/Frame身份、UI实际操作、数值/图像/下游结果、关闭及资源证据。失败先区分算法、集成、控件和测试设施。</p></details>')
parts.append('</div><div class="notice"><b>四个验收层级分开：</b>组件/局部回归 → Blob节点完整链 → 原FT全部要求 → 最终同一Release。前一级PASS不自动升级后一级；人工试用尽早发生，但不能顶替独立测试。</div>')
parts.append('<h3 id="blob-optimization-list">⑥ 优化清单：复用、调整、替换有明确边界</h3><div class="scroll"><table><thead><tr><th>项 / 决策</th><th>范围</th><th>证据与验收</th><th>对应原清单</th></tr></thead><tbody>')
mapping = {'BO01':['B02','C02','J02','J03','M01'],'BO02':['D01','D02','E02','E03','H01','H02'],'BO03':['B02','N02','O03'],'BO04':['F01','G01','I01','J02','J03','L01'],'BO05':['H01','P01'],'BO06':['I03','M03','P01','P02'],'BO07':['B02','C02','D03','H02']}
for row in DATA['optimizationList']:
    ids = ' / '.join(f'<a href="#FT-{i}">FT-{i}</a>' for i in mapping[row['id']])
    parts.append(f'<tr><td><b>{esc(row["id"])}</b><br>{esc(row["kind"])}</td><td>{esc(row["scope"])}</td><td>{esc(row["gate"])}</td><td>{ids}<p class="small">覆盖映射，非新增批次或整项状态。</p></td></tr>')
parts.append('</tbody></table></div><p><b>建议顺序：</b>确认Blob场景和职责 → 复用已有编辑合同接真实参数/ROI → 打通同run的结果树/renderer/下游 → 验正确性和生命周期 → 正式性能与同Release验收。只做本场景必要能力；模板专有模型与未验证的七形态工作量不阻挡Blob首链。</p>')
parts.append('<h3 id="blob-evidence-gaps">⑦ 真正待确认的问题与证据入口</h3><div class="two"><div class="state"><h4>需用户确认的使用行为</h4><ul><li>拖放后是否自动连接、选中、开窗，以及对应场景。</li><li>直接关闭、取消、确定、单次/连续试运行对草稿和正式配置的语义；推荐沿唯一编辑事务统一，不能由各按钮自定。</li></ul></div><div class="state"><h4>由技术成员补证，不推给用户猜</h4><ul><li>VM ProcessAlg/Output及尾跳释放体；本轮工具可达边界已记录。</li><li>共享控件的模板/反射消费者与真实生命周期；静态出现标记不是行为证明。</li><li>Blob真实整链和正式性能；未给出虚构耗时、通过率或采用结论。</li></ul></div></div><details><summary>可回查源码 / VM证据 / 已有分析</summary><ul>')
for raw in DATA['sources']:
    match = re.match(r'^(.*):(\d+)$',raw)
    path,line = (match.group(1),int(match.group(2))) if match else (raw,None)
    if Path(path).suffix == '.md' and 'REA逆向测试' in path:
        path='D:/MyWork/REA逆向测试/docs/架构逆向与实现对照文档/专题/documents/plugins/BLOB-ANALYSIS-IDENTITY-SOURCE-INVENTORY.html'
    parts.append('<li>'+source_link(path,line)+'</li>')
native='D:/MyWork/REA逆向测试/analysis/selfdev-support/VM-BLOB-NATIVE-MINIMAL/native-minimal-evidence.json'
parts.append('<li>'+source_link(native)+' · 同原件哈希下的有限静态片段，不是完整native反编译。</li>')
parts.append('<li>'+source_link('artifacts/analysis/blob-shared-controls-inventory-20261024.json')+' · 逐控件源码SHA/DP/直接消费者与生命周期定位记录。</li>')
parts.append('</ul></details><p class="small">本章只更新方案与验证设计，不改变在途交付状态，不运行产品、算法或诊断探针。已有分析归入现有O06，主文档首页提供入口。</p></section>')
fragment='\n'.join(parts)
start='<!-- BLOB-FIVE-LAYER-REVIEW:BEGIN -->'
end='<!-- BLOB-FIVE-LAYER-REVIEW:END -->'
page=PAGE.read_text(encoding='utf-8')
block=start+'\n'+fragment+'\n'+end
if start in page:
    page=re.sub(re.escape(start)+r'.*?'+re.escape(end),lambda _:block,page,count=1,flags=re.S)
else:
    page=page.replace('<main>','<main>\n'+block+'\n',1)
PAGE.write_text(page,encoding='utf-8')

home=DOCROOT/'index.html'
home_start='<!-- BLOB-REVIEW-ENTRY:BEGIN -->';home_end='<!-- BLOB-REVIEW-ENTRY:END -->'
entry=home_start+'''<nav aria-label="Blob方案审核入口" style="display:flex;gap:16px;flex-wrap:wrap;padding:12px 20px;border-bottom:1px solid #527b91;background:#153442"><a href="优化方案/optimization-o06.html#blob-five-layer-full-chain-review">Blob完整链 · 五层方案审核</a><a href="优化方案/optimization-o06.html#shared-control-design-inventory">共享控件逐项设计</a><a href="优化方案/optimization-o06.html#blob-verification-steps">验证步骤与优化清单</a></nav>'''+home_end
hs=home.read_text(encoding='utf-8')
if home_start in hs: hs=re.sub(re.escape(home_start)+r'.*?'+re.escape(home_end),lambda _:entry,hs,count=1,flags=re.S)
else: hs=re.sub(r'(<body[^>]*>)',lambda m:m.group(1)+'\n'+entry,hs,count=1)
home.write_text(hs,encoding='utf-8')
print(json.dumps(dict(controls=len(classes),verificationSteps=len(DATA['verification']),optimizations=len(DATA['optimizationList']),htmlCharacters=len(fragment),page=str(PAGE),home=str(home)),ensure_ascii=False))
