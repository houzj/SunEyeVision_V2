"""Evidence-based documentation review only. Does not run or modify the product."""
from pathlib import Path
from urllib.parse import quote, unquote
from html import escape
from html.parser import HTMLParser
import hashlib, json, os, re, subprocess
import runpy
from datetime import datetime, timezone, timedelta

ROOT=Path(__file__).resolve().parents[2]
DOC=ROOT/'docs/04-工具设计/架构对照/2026/10/21/自研架构文档维护'
PAGE=DOC/'优化方案/optimization-o06.html'
OUT=ROOT/'artifacts/analysis'
E=escape
sources=[
 ('S01','节点创建','src/UI/Services/Interaction/DragDropHandler.cs','Canvas_Drop'),
 ('S02','Blob窗口持有主视口、查询与清理','tools/SunEyeVision.BlobAnalysis/Views/BlobAnalysisToolDebugControl.xaml.cs','public void Cleanup'),
 ('S03','公共ROI管理器持有视口及renderer','src/Plugin.SDK/UI/Controls/Roi/Views/RoiEditorManagerControl.xaml.cs','public void SetMainImageControl'),
 ('S04','UI类型筛选规则','src/Plugin.SDK/UI/Controls/ParamBindingBase.cs','public static List<AvailableDataSource> FilterDataSourcesByType('),
 ('S05','UI持有生产草稿适配','src/UI/Services/Editing/ProductionEditorContext.cs','public sealed class ProductionEditorContext'),
 ('S06','唯一节点编辑发布宿主','src/Workflow/NodeConfigurationEditHost.cs','public sealed class NodeConfigurationEditHost'),
 ('S07','实际Blob算法与输入异常边界','tools/SunEyeVision.BlobAnalysis/BlobAnalysisTool.cs','public BlobAnalysisResults Run'),
 ('S08','稳定声明、记录投影及WPF点桥','tools/SunEyeVision.BlobAnalysis/BlobOutputContract.cs','public static class BlobOutputContract'),
 ('S09','结果持有者引用计数','src/Plugin.SDK/Execution/Results/ResultResourceOwner.cs','public sealed class ResultResourceOwner'),
 ('S10','结果树显式Run与reader','src/UI/Services/Monitoring/ResultPresentationPresenter.cs','public void ShowRun'),
 ('S11','UI-free合同已有实现','src/Plugin.Contracts/ToolContracts.cs','public interface IToolDefinition'),
 ('S12','图像合同实际能力边界','src/Plugin.Contracts/Resources.cs','public interface IReadOnlyImage'),
 ('S13','绑定编译器与数量合同','src/Plugin.Contracts/BindingPlans.cs','public enum BindingCompatibility'),
 ('S14','单字段编辑会话并非整节点CAS','src/Plugin.Contracts/Editing.cs','public sealed class ParameterEditSession'),
 ('S15','旧SDK真实WPF依赖','src/Plugin.SDK/SunEyeVision.Plugin.SDK.csproj','<UseWPF>true'),
 ('S16','Blob算法和View同一WPF插件','tools/SunEyeVision.BlobAnalysis/SunEyeVision.Tool.BlobAnalysis.csproj','<UseWPF>true'),
 ('S17','公共控件和模板装配','src/Plugin.SDK/Themes/Generic.xaml','ResourceDictionary'),
 ('S18','运行前准备仍在控件基类','src/Plugin.SDK/UI/Controls/ToolDebugControlBase.cs','var request = new ToolExecutionRequest'),
 ('S19','工具定义注册与按类型创建','src/Plugin.Infrastructure/Managers/Tool/ToolRegistry.cs','public static IToolPlugin? CreateToolInstance'),
 ('S20','节点移除连接与退役入口','src/Workflow/Workflow.cs','public bool RemoveNode'),
 ('S21','工作流执行会话与统一结束','src/Workflow/WorkflowExecutionEngine.cs','public async Task<ExecutionResult> ExecuteWorkflowAsync'),
 ('S22','执行器实际请求入口','src/Plugin.SDK/Core/ToolExecutor.cs','public ToolResults Run(IToolPlugin tool, ToolExecutionRequest request)'),
 ('S23','持久化写入快照','src/Workflow/Persistence/SolutionSnapshot.cs','public static SolutionSnapshot Prepare'),
 ('S24','节点配置与实例创建入口','src/Workflow/WorkflowNode.cs','public virtual IToolPlugin? CreateInstance'),
 ('S25','工具窗口另创建实例','src/UI/Services/Editing/Nodes/NodeOpenDecisionChain.cs','private void CreateDebugControl'),
 ('S26','无UI绑定工程实际包含内容','src/Plugin.Runtime/BindingScopeSnapshot.cs','public sealed class BindingRunSnapshot'),
 ('S27','可选WPF呈现工程已有边界','src/Plugin.Wpf/SunEyeVision.Plugin.Wpf.csproj','<ProjectReference'),
 ('S28','有限字段消费者而非生产编辑器','src/Plugin.Wpf/ParameterEditorControl.cs','public sealed class ParameterEditorControl'),
 ('S29','中央三层图像控件','src/Plugin.SDK/UI/Controls/ImageControl/ImageControl.xaml.cs','class ImageControl'),
 ('S30','结果层批量/普通路径、缓存和异步','src/Plugin.SDK/UI/Controls/ImageControl/ResultRenderLayer.cs','class ResultRenderLayer'),
 ('S31','交互ROI轻量Visual与失效标志','src/Plugin.SDK/UI/Controls/Roi/Rendering/WpfShapeRenderer.cs','class WpfShapeRenderer'),
 ('S32','图形收集分组键','src/Plugin.SDK/Models/Visualization/DisplayCollector.cs','private GraphicsDisplayItem GetOrCreateGraphicsItem'),
 ('S33','节点分析控件创建、缓存读取和高亮桥','src/UI/Shell/Windows/MainWindowViewModel.cs','private System.Threading.Tasks.Task UpdateDataAnalysisControl'),
 ('S34','数据分析协议实际边界','src/Plugin.SDK/UI/Controls/IToolControlDataAnalysis.cs','public interface IToolControlDataAnalysis'),
 ('S35','Blob分析表格、聚合及高亮','tools/SunEyeVision.BlobAnalysis/Views/BlobDataAnalysisControl.xaml.cs','class BlobDataAnalysisControl'),
 ('S36','节点图像预览与版本/取消','src/UI/Shell/Controls/Panels/ImagePreviewControl.xaml.cs','public class ImageInfo'),
 ('S37','预览内容版本缓存与并发预算','src/UI/Services/Thumbnail/PreviewBitmapCache.cs','public sealed class PreviewBitmapCache'),
 ('S38','已存在生产视口/模态/帧/格式/计量实际入口图','artifacts/analysis/a11-host-identity-review-20261024/viewport-production-entry-map.json','"routes"'),
 ('T01','已有几何/reader/Presenter有限独验','artifacts/independent-testing/tool-framework-first-dependency/runs/geometry-3ab-20261024/acceptance.json','{'),
 ('T02','已有真实Blob/native/输出有限独验','artifacts/independent-testing/tool-framework-first-dependency/runs/declared-blob-host01-20261024/acceptance.json','{'),
 ('V01','VM原模块身份与managed调用库存','D:/MyWork/REA逆向测试/docs/plugins/BLOB-ANALYSIS-IDENTITY-SOURCE-INVENTORY.md','## 身份'),
 ('V02','VM公共组与控件能力声明','D:/MyWork/REA逆向测试/docs/architecture-recovery/tool-rendering-entry-audit.resources/IMVSBlobFindModuAlgorithmTab.xml','<GroupLinkItem Name="ImageSourceGroup">'),
 ('V03','VM配置根创建、初始化和Dispose','D:/MyWork/REA逆向测试/analysis/decompiled/VMControls.WPF/VMControls.WPF/ParamsConfigControlBase.xaml.cs','private static void OnParamsConfigChanged'),
 ('V04','VM本体片段与未贯通native边界','D:/MyWork/REA逆向测试/analysis/selfdev-support/VM-BLOB-NATIVE-MINIMAL/native-minimal-evidence.json','"taskId"'),
]
evidence=[]
for sid,title,path,needle in sources:
    p=Path(path)
    if not p.is_absolute():p=ROOT/p
    text=p.read_text(encoding='utf-8-sig')
    index=text.find(needle)
    evidence.append(dict(id=sid,title=title,path=path,line=text[:index].count('\n')+1 if index>=0 else 1,
                         located=index>=0,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
def ref(ids):return ' '.join(f'<a href="#br-evidence-{i}">{i}</a>' for i in ids.split())
def table(headers,rows):
    return '<div class="br-scroll"><table><thead><tr>'+''.join('<th>'+E(h)+'</th>' for h in headers)+'</tr></thead><tbody>'+''.join('<tr>'+''.join('<td>'+str(c)+'</td>' for c in row)+'</tr>' for row in rows)+'</tbody></table></div>'
def flow(items):return '<div class="br-flow">'+''.join('<div><b>'+E(a)+'</b><small>'+E(b)+'</small></div>' for a,b in items)+'</div>'

parts=['''<section id="blob-five-layer-full-chain-review" class="br" aria-labelledby="br-title">
<style>
.br{padding:22px;border:1px solid #568399;border-radius:14px;background:#102632;color:#deedf2;line-height:1.65;margin:20px 0;scroll-margin-top:45px}.br *{box-sizing:border-box}.br h2{font-size:27px}.br h3{color:#9bdff8;font-size:21px;margin-top:27px}.br a{color:#9bdefa}.br small{display:block;color:#b3cbd6}.br .br-note{padding:12px 15px;border-left:4px solid #efb666;background:#263641;margin:12px 0}.br .br-flow{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:10px;margin:12px 0;counter-reset:stage}.br .br-flow>div{padding:12px;border:1px solid #47778c;border-radius:8px;background:#173c4c}.br .br-flow b:before{counter-increment:stage;content:counter(stage)' · ';color:#ffca81}.br .br-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}.br .br-card{padding:14px;border:1px solid #456b7d;border-radius:9px;background:#173441}.br .br-scroll{overflow:auto}.br table{border-collapse:collapse;width:100%;min-width:860px;font-size:14px}.br th,.br td{padding:11px;border:1px solid #3d6375;vertical-align:top;text-align:left;overflow-wrap:anywhere}.br th{background:#204656}.br .br-nav{display:flex;gap:13px;flex-wrap:wrap;margin:15px 0}.br code{overflow-wrap:anywhere;font-size:12px}.br .br-ok{color:#9ee4ba}.br .br-warn{color:#ffcd8d}.br details{padding:12px;border:1px solid #446c80;border-radius:8px;margin:12px 0}.br summary{cursor:pointer;color:#ccebf7;font-weight:bold}@media(max-width:850px){.br .br-grid{grid-template-columns:1fr}.br .br-flow{grid-template-columns:repeat(2,minmax(0,1fr))}.br{padding:14px}.br h2{font-size:22px}}
</style>
<small>V8.1证据驱动架构复审 · 2026-10-24 · 设计待整体审核 · 未改产品、未运行新测试</small>
<h2 id="br-title">框架—工具—节点完整闭环：Blob架构复审与优化漏斗</h2>
<div class="br-note"><b>明确推荐：</b>采用“业务语义合同 + 宿主应用服务 + 可选UI组合”的模块化方向。保留经证据支持的类型、运行、事务和资源能力；<b>替换工具页面/管理器直接拥有运行、共享视口和类型规则的架构路径</b>。常见字段可声明组合，专业工具允许专用视图，但都通过同一受限能力入口工作。不是给现有管理器继续加功能，也不是整体复制VM的XML框架。</div>
<p>推荐来自场景约束、双方证据与候选比较，不以代码已实现或改动小作为理由。目标方向可审核；Blob完整生产接线、图像桥、七类手势和正式性能仍未验证，不能据本页宣布可实施或已验收。</p>
<nav class="br-nav" aria-label="架构复审目录"><a href="#br-funnel">完整优化漏斗</a><a href="#br-lifecycle">生命周期覆盖</a><a href="#br-basis">场景与证据</a><a href="#br-options">候选取舍</a><a href="#br-layers">五层结论</a><a href="#br-target">职责与调用图</a><a href="#br-controls">41控件分级</a><a href="#br-contracts">合同与工程</a><a href="#br-acceptance">范围与验收</a><a href="#br-gaps">未确认与审核</a><a href="#br-evidence">证据索引</a></nav>
<nav class="br-nav" aria-label="节点周边专项复核"><a href="#br-node-peripherals">节点周边总图</a><a href="#br-control-library-governance">框架与控件库管理</a><a href="#br-blob-controls">Blob实际控件</a><a href="#br-region-result-rendering">区域编辑与结果渲染</a><a href="#br-preview-analysis-recheck">中央显示／预览／数据分析复核</a></nav>
<h3 id="br-basis">① 先从使用要求和实际证据判断</h3>''']
parts.append(flow([
 ('创建并配置节点','拖入画布、绑定图像、检测/屏蔽ROI、参数与草稿'),
 ('提交与执行','确定/取消、同代输入和配置、失败与停止'),
 ('消费同一次结果','树、轮廓、文字与下游绑定指向同一数据'),
 ('保存与退役','重开可恢复；关窗、删节点、最后读者释放各有边界')]))
parts.append(table(['必须满足的架构条件','当前证据与判断','性质/边界'],[
 ('工具不协调全局UI和运行','Blob控制代码持DataSourceQueryService、主ImageControl，Cleanup清结果层；基类构造运行请求。'+ref('S02 S18'),'设计边界失配；不是加一个Dispose即可解决。'),
 ('公共绘图不拥有正式参数/全局层','ROI管理器把来源集合、两组编辑器、共享Canvas、renderer、窗口事件合到一个UserControl。'+ref('S03'),'局部交互协调合理；跨会话权限和生命周期应由宿主控制。需拆职责，不因名为Manager而全删。'),
 ('类型/绑定规则是无UI业务语义','ParamBindingBase含标量/数组词汇归并、数值互通和容器放行；现有BindingPlan已具兼容/选择表达。'+ref('S04 S13'),'UI候选过滤可以是投影，最终语义必须由权威编译器决定；不能据该片段断言运行已发生错误转换。'),
 ('声明与执行可无界面使用','Plugin.Contracts/Runtime是net9.0；旧SDK与Blob插件仍UseWPF，输出桥使用System.Windows.Point。'+ref('S08 S11 S15 S16'),'已有方向正确但迁移不完整；不能称Blob已经无UI解耦，也不能因新合同存在就默认它足够。'),
 ('区分局部成功与全链可用','节点草稿发布、资源引用计数、结果树显式reader已有实现；Blob消费者接入并未由这些源码自动证明。'+ref('S05 S06 S09 S10'),'可复用机制是候选，经消费者接线验收后才计整链。'),
 ('VM作为对照，不作为真值','Blob XML引用公共Group并声明ROI能力；配置根统一初始化/切换清理。native算法和全部回收未贯通。'+ref('V01 V02 V03 V04'),'可借鉴声明组合与集中装配；事务、native算法及回收不得补作VM事实。')]))

parts.append('''<h3 id="br-options">② 同一标准比较，再确定目标方向</h3><p>标准一致：完整使用链正确性、职责与无UI边界、类型/事务/资源一致性、专业工具扩展、性能可验证性及演进成本。以下是定性架构判断，没有虚构评分或耗时。</p>''')
parts.append(table(['候选','收益','结构性代价与证据','推荐'],[
 ('A · 在现有控件/管理器上继续增强','迁移量小，可保留现有页面操作','工具与WPF/共享视口/查询和执行准备继续相互牵连；对多窗、取消、复用形成持续约束。S02–04/S18。','不作为最终架构；只允许明确期限的迁移适配。'),
 ('B · 完整移植VM式XML/UI配置运行框架','常见工具声明集中、公共组装配一致','VM的元数据系统、操作节点、native桥和事务语义仍有未知；复制全部框架会增加解释器、调试和兼容成本。V01–04。','借鉴声明与装配模式，不复制其完整机制。'),
 ('C · 语义合同 + 宿主应用服务 + 可选UI组合','同一运行与数据语义可服务窗口、下游及无UI执行；公共交互受控、专业能力可扩展','需把现有耦合真正迁出，明确合同适配和版本；现有IReadOnlyImage仅宽高/CopyPixels，不能假称已覆盖HALCON需求。S11–16。','推荐。保留符合职责的机制；替换错误的消费路径，迁移成本显式承担。'),
 ('D · 所有工具都自动生成通用页面','简单字段一致、减少重复布局','Blob复合ROI、模板建模、脚本编辑的交互不能仅由通用标量表单表达；易形成新的万能管理器。','不采用强制全自动；只自动组合适合的字段，复合编辑器按能力扩展。')]))
parts.append('''<div class="br-grid"><div class="br-card"><b>借鉴VM</b><p>工具声明语义字段、公共组和可用交互能力；框架统一装配、初始化和解绑。依据V02/V03。</p></div><div class="br-card"><b>我们独立补齐</b><p>稳定类型和字段地址、版本化草稿提交、同Run/Frame的结果消费、受限绘图会话与最后读者释放；这些是场景要求，不冒充VM已证实现。</p></div><div class="br-card"><b>明确不照搬</b><p>不以VM XML名称、控件外观或本地native片段替代语义合同；不复制未证的提交/回滚/释放行为；不把16个ROI等原版配置上限当我们的业务需求。</p></div></div>''')
layers=[
 ('L1 · 场景/节点','VM已证拖放入口，模块创建/Undo全桥仍有缺口。我们有Canvas_Drop→CreateNode→AddNode链。','实例身份、配置寿命与每次运行寿命应分开；拖放只表达创建意图，自动连线/选中/开窗属产品策略。','保留可追踪创建命令与身份机制；宿主应用服务统一创建、撤销和释放，不让工具页自创节点。','BV01/BV11/BV12；创建一次、取消无残留、复制不共享可变配置。',ref('S01')),
 ('L2 · UI/共享能力','VM公共组和配置根有证据；我们页面直接组合控件，Manager与页面掌共享视口。','标准字段呈现、复合几何交互、编辑事务、共享视口不是同一个所有者。','替换越界的管理与页面初始化路径；纯可视部件按能力留用。绘图只得到所属字段/图层的作用域，不能调用全局Clear。','BV02/BV04/BV05；A窗关闭不得清B结果；新消费者无须拿主窗口/全局服务定位器。',ref('S02 S03 V02 V03')),
 ('L3 · 数据/绑定/执行','现有生产EditorContext/NodeHost及BindingPlan提供部分基础；旧控件仍有自己的筛选词汇。','业务参数、绑定地址、UI草稿、运行输入和输出各有角色，不能多处拥有正式状态。','保留一处权威发布与显式绑定计划；统一候选过滤和运行编译口径。ParameterEditSession是字段编辑，不替代整节点提交。','BV03/BV05/BV08/BV10/BV11；非法文本不部分提交，类型/数量不静默丢失，同代运行输入固定。',ref('S04 S05 S06 S13 S14')),
 ('L4 · 算法/引擎','我们HALCON真实调用已读；VM实际ProcessAlg/Output、连通谓词及性能未知。','业务算法选择、引擎适配和UI/工作流调度分开；正确性与资源成本先有可测边界。','当前保留HALCON算法作为基线候选，不批准引擎替换；拆除对WPF运行路径的必要依赖。输入解包在try前属已证实现边界问题，按一致失败合同修复。','BV06/BV07/BV08/BV12；独立真值、同能力样本，再比较分段/端到端成本。',ref('S07 S12 S16 V04')),
 ('L5 · 结果/下游/释放','稳定字段、记录、ResultOwner和reader已具实现；VM显示对象声明与回收片段不等于全链已闭合。','树和图形是投影，不能成为下游数据源；结果寿命独立于窗口。','保留记录/资源租约机制；替换旧字符串/可变对象绕过的消费路径。ElementId当前来自运行内区域序号，必须与Run/Frame联合，不承诺跨运行跟踪。','BV09/BV10/BV12；排序、0结果、旧行、跨窗、最后reader、失败清理均验证。',ref('S08 S09 S10 V01 V04')),
]
parts.append('<h3 id="br-layers">③ 五层统一结论：区分设计替换与实现修复</h3>')
parts.append(table(['层面','实际证据','架构判断','处置与范围','可观察验收','出处'],layers))

parts.append('''<h3 id="br-target">④ 推荐职责图：工具如何使用框架，而不拥有框架</h3>''')
parts.append(flow([
 ('工具定义/算法','声明输入、参数、ROI能力、输出语义；实现校验与算法'),
 ('宿主应用服务','拥有节点编辑事务、绑定编译、运行准入与快照、结果发布'),
 ('可选呈现适配','标准字段或专用视图；只发编辑/绘图/执行意图'),
 ('视口与结果消费者','作用域图层 + 同Run/Frame reader；独立释放')]))
parts.append('''<div class="br-note"><b>依赖方向：</b>工具业务/算法 → 无UI语义合同及所需引擎适配；引擎适配 → 无UI图像/几何合同与引擎库；宿主调度 → 无UI调用合同；可选工具视图 → 公共呈现适配与语义合同。<b>合同层不得反向依赖UI、引擎或宿主。</b>宿主装配实现并提供受限能力，工作流不引用控件。工具仍负责业务算法、业务校验与结果语义，并非“只写参数声明就足够”。</div>
<div class="br-grid"><div class="br-card"><b>配置链</b><p>字段/能力声明 → 私有草稿 → 语义校验与绑定计划 → 一次正式提交 → 明确版本的运行输入。</p><small>数值输入、拖图形、继承切换回到同一字段；不产生第二正式仓库。</small></div><div class="br-card"><b>绘图链</b><p>字段身份+允许形状+坐标框架 → 宿主授予编辑作用域 → 工具栏/手势修改草稿 → 校验/撤销/确认或取消。</p><small>ROI编辑器管理手势状态；宿主管理跨窗互斥和权限，renderer只画，不提交业务参数。</small></div><div class="br-card"><b>结果链</b><p>算法输出 → 同次声明快照与资源owner → 树/图形/下游各持reader → 旧owner退休 → 最后reader释放。</p><small>视口缓存可淘汰显示几何，不能销毁仍被其他消费者持有的算法结果。</small></div></div>
<h3 id="br-controls">⑤ 共享控件按职责管理：不按已有类逐一默认保留</h3>
<p>已有41项是源码盘点范围，包含内部部件和专门能力，不是41个全部应公开的框架接口，也不冒称整个仓库所有公共控件。另核Plugin.Wpf.ParameterEditorControl为有限合同消费者，尚非生产编辑替代。下面按能力分级，原逐件清单继续作为证据附录；任何“复用”都不等于现有行为已验收。</p>''')
control_groups=[
 ('C1 · 纯视觉基础','ToggleSwitch、LabeledControl、ParamGroup、ToolIcon、ColorPicker/ColorSelector、ParamTextBox、ParamComboBox、NumericUpDown、RangeSlider','可继续使用WPF主题/模板。基础输入只拥有文本/局部交互状态；参数解析规则和正式提交由会话提供。','保留合格可视实现；精度、文化、键盘、DPI、设计时/运行时逐类验。不是因为已实现而保留，而是局部职责适当且替换无已证收益。'),
 ('C2 · 语义字段适配','ConfigSetting、ParamBindingBase/ParamBinding、ImageSourceSelector、RangeInputControl、RoiSubscribePanel、NodeSelectorPopup','替换控件自有的权威类型、绑定兼容和正式参数写入职责；消费统一字段/来源投影/诊断。','外观可复用，控制路径需重构；只靠增加分支或字符串路径恢复不予通过。'),
 ('C3 · 复合编辑能力','RoiEditorControl、MeasureEditorControl、MeasureElementsEditorControl、DrawingParameterPanel、MeasureGuideParameterPanel、ShapeToolBar/MeasureToolBar/ToolBarControl','工具栏是命令呈现；手势状态机/命中测试/坐标变换属于公共交互能力；业务字段经编辑会话提交。','复用已证几何计算和手势部件；按7类真实行为核适配，不能只更换VM样式。'),
 ('C4 · 宿主与所有权协调','RoiEditorManagerControl、MeasurementEditorManagerControl、ToolDebugControlBase、DefaultDebugWindow/CustomDebugWindow、SdkDialogWindow','公共控件可保留组内路由；节点会话、共享视口权限、运行入口和关闭提交语义回归宿主。','替换越界职责及相应调用链，旧类可短期薄适配，不允许长期双权威。壳的主题/布局可复用。'),
 ('C5 · 结果投影与渲染','ImageControl、ResultRenderLayer、RoiVisualHost、JudgmentEditorControl、GraphicsDisplayEditorControl、TextDisplayEditorControl、DispItemColorControl/DispTextColorControl','图层内部隐藏在视口能力之后；显示配置草稿与运行reader分离。图形批次具Run/Frame/Element地址。','保留批量渲染/分页原理；替换全局清层和无身份选择路径。裁剪、命中测试与缓存收益需真实负载测量。'),
 ('C6 · 专门能力','ScriptEditorCore、CommunicationDataAnalysisControl','按专门消费者管理，不进入Blob运行依赖，不抽象成万能编辑器。','复用原已测成果和合同，未有当前场景需求的不纳入Blob改造。')]
parts.append(table(['能力层','已有类映射','目标职责','复用/调整/替换依据'],control_groups))
parts.append('''<p><b>管理方式：</b>业务字段与能力定义归工具语义声明；呈现适配和模板归可选UI层；跨窗会话/图层作用域归宿主；资源归运行owner/reader。由宿主按稳定能力键装配已注册适配，核不支持能力并给出明确诊断；不新增万能全局Manager。先用现有声明及WPF模板承载，只有确有无法表达的元数据才扩展现有合同，扩展前核四类消费者。</p>
<h3 id="br-contracts">⑥ 接口与工程：复用机制，替换错误依赖</h3>''')
parts.append(table(['合同/工程','当前可保留的依据','必须调整或替换','适用边界'],[
    ('Plugin.Contracts / Plugin.Runtime','TypeRef、ToolSchema、OutputReference、BindingPlan、IToolDefinition/Session/Invocation、RunIdentity已存在且工程无WPF；Plugin.Runtime当前主要承载绑定服务和快照。','以这些语义合同为统一入口；确认业务数量/几何/图像能力的覆盖，消除UI自建权威兼容规则。实际生产执行仍核WorkflowExecutionEngine/ToolExecutor，不能由工程名推断已迁移。','名称存在不等于Blob已经接入；禁止再平行定义第二套同义类型/执行入口。'),
 ('Workflow NodeConfigurationEditHost / BindingConfigurationStore；UI ProductionEditorContext','有私有候选、身份/基线校验、一次发布和草稿适配。','补Blob参数/ROI/绑定全字段接入；校验、撤销和运行快照由同一应用边界串起。','ParameterEditSession.TryCommit只是字段局部提交，不冒充整节点发布。旧文“CaptureForRun”是行为要求，本次src检索未找到同名方法，不再写作已接通API。'),
    ('旧Plugin.SDK / 已有Plugin.Wpf / Blob WPF插件','Plugin.Wpf已只引用Contracts；ParameterEditorControl是最小字段合同消费者，不能当生产编辑器。SDK视觉实现可按职责保留。','复用既有工程边界承载可选UI；替换Blob/旧SDK必须把运行和视图捆绑的路径；WPF Point仅在显示边界转换，不再另起同义UI工程。','这是实质接入适配，需审核批准及全量依赖/发布验证；不是本次已实施，也不借本轮去重写全插件加载器或全工具。S27/S28。'),
 ('图像与HALCON桥','IVisionImage/HalAdapter现有实际图像能力可作桥接证据。','IReadOnlyImage只有Width/Height/CopyPixels；IOutputResources仅Gray8创建与RetainInput。需要明确格式/通道/步长、借用寿命与native适配，不能以全图复制默认解决。','不假称已有零拷贝；先做接口覆盖审查和成本验证，必要扩展现有合同，不扩大到全新全局图像仓库。'),
 ('Blob算法 / 输出合同 / ResultResourceOwner / Presenter','现有HALCON区域流水线、稳定FieldId、Sequence<Record>、按需轮廓、引用计数与分页适合业务。','输入解包纳入统一失败边界；结果身份与排序分离；树/轮廓/下游接同一快照；按需特征和图像复制根据测量优化。','HALCON与具体渲染后端均不宣称性能最优；记录和原平行数组必须来源于同一最终集合，不能形成两份权威。')]))
parts.append('''<h3 id="br-acceptance">⑦ 统一改造范围与验收：不再按控件零散追加</h3>''')
work=[
 ('R1 · 节点到编辑会话','宿主创建/打开/关闭身份、草稿与Undo；Blob参数/ROI/绑定消费统一服务','复用创建、事务、Repository；替换页面直接协调运行/共享层的路径','BV01–05/BV11；真实拖入→配置→确定/取消→保存重开；跨窗/非法pending不污染。'),
 ('R2 · 公共控件与绘图','按C1–C6分工适配；7类图形的样式、手势、坐标和回填；绑定来源消费','保留合格基础部件；替换Manager越界权限/独立语义；不做Blob工具页面美化','BV02–05；绘制与继承、数值/拖动双向、Esc/Undo、缩放和A/B节点隔离；卡尺作第二消费者合同检查。'),
 ('R3 · 数据与运行边界','类型/数量/失效来源诊断、同代输入和参数、无UI调用及图像桥覆盖','沿已有Contracts/Runtime/应用发布机制适配；不造第二Store/执行器','BV03/BV05/BV08/BV10；下游整集/元素/字段、missing/false/0分明；无活动窗口仍能执行。'),
 ('R4 · 算法正确性和成本','独立真值、ROI/屏蔽/修正、阈值、形态学、筛选/排序；统一失败边界','先保当前引擎为比较基线；只按数据确认改动，不预设换引擎','BV06–08；0/1/多目标、触边/孔洞/噪声/相连、灰度/彩色；同能力结果容差冻结后比较。'),
 ('R5 · 结果与资源','同Run/Frame/Field/Element、分页/选择/渲染/下游、owner退休与reader释放','复用输出投影/Owner/Presenter；替换全局Clear和裸UI对象数据路径','BV09–12；旧行不选新运行、0结果不沿旧值、最后读者释放；连续多轮后资源回基线或已解释的有界缓存。'),
 ('R6 · 关联兼容及统一交付','图像源/预览、绑定树/结果树、保存/复制、插件版本/部署、UI设计时','映射原FT覆盖与已有成果；工程拆分经批准后按真实依赖部署','代表Blob整链后核卡尺/模板/脚本必要差异；固定同一Release、独验与手验，局部PASS不代整项。')]
parts.append(table(['整链工作','包含内容','处置','验收要求'],work))
parts.append('''<p>这些是原范围内的能力映射，<b>不是六个新批次或并行开工指令</b>。经理在既有FT总账逐项核映射、缺口、负责人和估算，保留已验子证据；当前在途发布/编辑交付继续。本轮不改产品、不新增独立进程强停、全局资源系统、VM克隆框架或全工具页面重做。</p>
<div class="br-note"><b>性能验收沿原批准档，不重新降门：</b>2048×2048 Gray8/Bgr24/Bgra32；交互图形1/100/1000、轮廓1000/10000且每条64点；反馈p95≤50ms、帧p95≤33ms；重复5次，每次至少1000事件及60秒稳态；每视口render缓存256MiB、交互索引64MiB。10万轮廓/4K只记录容量边界，不承诺33ms。冻结同机、同样本、固定Release与输出需求，分别测首次/再次打开、算法分段、首帧和稳态、分配与资源峰值。这些是原批准验收要求，未在本轮运行；缓存预算不等于进程总内存上限。</div>
<h3 id="br-gaps">⑧ 推荐明确，尚未确认的边界也明确</h3>''')
parts.append(table(['未确认项','对决策的影响','处置/停止边界'],[
 ('VM native算法、完整输出与析构尾链','不能选择VM算法替换，不能复制其长期资源策略，也不能断言VM全链性能更优。','保留V04静态边界；只有会改变引擎选择/释放合同的问题才继续定向取证，不把完整逆向VM当本轮前置。'),
 ('UI-free图像桥对HALCON和多通道是否充分','影响推荐方向的落地方式、复制成本和工程适配，不改变UI不拥有运行的职责判断。','当前不承诺零拷贝；先核现有合同与适配证据，必要的独立验证另经批准，不动产品。'),
 ('7类图形真实手势/VM细节及大量图形成本','影响可复用部件范围；旧代表图和41类存在不能证明风格/手势/性能符合。','原实机截图优先；未实证部分明确待核，不自行补画。保留既有已证几何，不重复整库分析。'),
 ('Blob整链、跨窗、取消/失败/最后reader与关联消费者','决定R1–R6能否验收；现有Threshold/资源有限PASS不能替代Blob。','复用原BV01–12及已验样本，先代表链早期试用再扩消费者，不将本次静态评审当独验。'),
 ('默认拖入/确认/关闭/试运行的产品语义','会影响自动连线、取消回滚及试运行是否发布正式配置。','推荐拖入只建节点并明确选中反馈，自动连线/开窗保持显式策略；试运行用草稿隔离结果、确定后正式发布、取消不发布。未经用户确认不能静默改变当前行为。')]))
parts.append('''<div class="br-note"><b>本次请整体审核：</b>① 是否认可C方向及“替换越界职责、保留合格机制”的范围；② 是否接受必要的运行/UI工程适配纳入原范围映射后再提实施；③ 编辑确定/取消/试运行语义是否采用上述推荐。VM内部缺体、图像桥和性能等技术核实由总监组织收敛，不要求用户代替技术判断。<br>本页状态：<b>架构推荐已形成，待用户整体审核；技术落地条件与未验项目仍开放。</b> 不能据此自动启动Blob新实施，也不影响原批准在途交付。</div>
<div class="br-note"><b>用户指定最终人工验收入口：</b><code>D:\\MyWork\\SunEyeVision_V2\\Release\\SunEyeVision_v1.0.0\\SunEyeVision</code>，该目录当前程序文件名为<code>SunEyeVision.UI.exe</code>。开发/独测隔离路径仅用于固定身份验证；最终由交付经理组织完整Release交付到此目录，核部署与独验身份一致，再提供一份手验清单及已闭环/未闭环结论。目录中现有程序不代表本轮已统一交付；保留用户配置、方案和数据，覆盖前检查占用。</div>
<h3 id="br-evidence">⑨ 实际证据索引（本次只读取证）</h3>''')
erows=[]
for e in evidence:
    p=Path(e['path']);p=p if p.is_absolute() else ROOT/p
    url=quote(os.path.relpath(p,PAGE.parent).replace('\\','/'),safe='/:')
    erows.append([f'<span id="br-evidence-{e["id"]}">{e["id"]}</span>',E(e['title']),f'<a href="{url}">{E(p.name)}:{e["line"]}</a>',f'<code>{e["sha256"]}</code>'])
parts.append(f'<small>自研观察HEAD：{E(head)}。下列每份源码/报告记录实际SHA；在途变化按文件身份解释，不将不同提交的产品结果混为本轮验收。</small>')
parts.append(table(['证据','内容','来源（行号为阅读定位）','SHA256'],erows))
parts.append('<p><a href="#blob-five-layer-evidence-inventory">展开原完整链、41项清单与BV验证附录</a> · <a href="#br-title">返回本次推荐</a></p></section>')

# The original 48 IDs remain authoritative. The funnel is a read-only projection,
# never a replacement progress ledger or a new queue/batch.
scopefile=ROOT/'artifacts/validation/tool-framework-contract/final-review-scope.json'
ledgerfile=ROOT/'docs/collaboration/DELIVERY_LEDGER.json'
scope=json.loads(scopefile.read_text(encoding='utf-8-sig'))
ledger=json.loads(ledgerfile.read_text(encoding='utf-8-sig'))
ledgeritems={r['id']:r for r in ledger['items']}
# Architecture decomposition is derived from scenarios and ownership, NOT the
# historical FT categories. Existing IDs are reverse references only.
arch_domains=[
 ('工具定义、发现与接入边界',[
  ('定义与能力','运行、参数、输出及可选视图是否由清晰合同表达？','S11 S15 S16 S19 S27 S28','保留语义声明；替换运行必须依赖WPF视图的装配关系。','已证耦合；适配未验','A01 A02 B01 B02 B03'),
  ('装配及兼容','缺依赖、契约错配、缺UI时是否仍有确定行为？','S19','类型发现、运行工厂和视图工厂分别承担失败；版本/发布核已有独验，不从文件名猜能力。','部分证据；完整装配沿在途验','B01 B02 B03'),
  ('专业扩展','Blob、卡尺、模板、脚本能否用同一职责边界接入？','S11 V02 V03','公共字段可组合，复合能力和专用视图可扩展；不强迫所有工具套一张自动表单。','架构推荐；其他消费者需核','A03 E01 E03 O01 P01')]),
 ('方案、节点与设计期状态',[
  ('类型/节点/运行实例','工具类型、持久化节点、窗口实例、算法会话是否被混为一物？','S01 S19 S24 S25','四种身份/寿命分别定义；开窗建对象不等于运行缓存命中。','创建路径已证；全寿命待验','C01 C03 D03'),
  ('图操作与引用完整性','创建、复制、撤销、删节点或断连后，绑定和运行是否仍指向正确对象？','S01 S20','图变更与配置引用一起校验；复制重建实例身份，删除先封准入再退役，旧结果只按明确历史语义消费。','入口已证；并发删除待验','C02 H03 G03 O03'),
  ('方案域/作用域','切方案、子程序或全局来源时，身份、权限和数据目录如何隔离？','S06 S13 S26','按文档/工作流/节点/运行标识限定来源与回写；禁止当前UI选择隐式改变执行语义。','合同基础已证；生产全链待验','G02 O01 O03')]),
 ('参数、编辑事务与业务校验',[
  ('参数模型','配置常量、引用、运行值、显示偏好和临时文本是否各有所有者？','S05 S06 S14','以业务语义区分状态；正式配置唯一发布，运行值不能被旧草稿覆盖，偏好不混入算法事务。','结构已见；变量联合提交待专审','H01 D02 E02'),
  ('全量准备与提交','多个Tab、ROI、节点和变量修改怎样一起校验、失败保留、一次发布？','S05 S06 S14','先构造合法候选并验证写集/冲突，再发布；不得部分生效或用无条件rebase掩盖冲突。','跨节点/变量缺口已升级；本页未批准修复','H01 H02 E02 F01'),
  ('字段类型和输入语义','整数精度、数组数量、单位、范围、0/false/missing怎样被一致解释？','S04 S13 S14','控件投影权威类型规则；类型/数量/单位显式，所有输入路径采用同一语义校验。','UI自有规则已证；实际转换逐入口核','A02 F01 F02 M03')]),
 ('来源、绑定与数据准备',[
  ('声明目录和选择树','未运行时能否选合法来源？候选来自声明还是最后结果？','S04 S08 S13','声明与来源作用域生成目录，结果值只提供本次状态；树不成为数据权威。','声明存在；完整来源消费者待验','G01 G02 J03'),
  ('引用编译和失效','标量/集合/记录子字段引用能否稳定表达、编译和诊断？','S13 S26','用稳定地址与选择计划；容器可展开不等于整容器可绑定标量，禁静默截首项。','合同已证；Blob生产接入待验','F02 G03 M03'),
  ('运行输入冻结','图像、ROI/修正、变量、绑定及配置版本是否一致？','S21 S22 S26','运行准入生成自洽请求与输入reader；后续编辑不改当前请求，缺源明确失败。','生产请求入口已证；Blob整链待验','K01 G02 H01')]),
 ('工具UI、共享控件与公共交互',[
  ('窗口/页面/服务','壳、视图、编辑会话与运行入口是否职责互侵？','S02 S05 S18 S25 V03','壳发意图、宿主管会话/运行，工具视图消费受限上下文；替换全局查询/清层路径。','源码已证边界问题','D01 D02 D03 E01 E03'),
  ('控件分层及管理','基础输入、语义字段、复合编辑器和宿主协调是否被一个Manager兼任？','S03 S04 S17 S27 S28','按基础视觉/字段适配/交互能力/宿主协调分层；现有41类逐件映射，实例和事件由所属会话释放。','源码已证职责聚合；行为待验','F01 F03 I01 L02'),
  ('绘图、坐标与回填','形状、继承、命中、捕获、取消、缩放、数值回填如何保持一致？','S03 S08 V02','公共交互状态机作用于私有字段草稿；工具栏发命令，renderer只画；形状与单位/坐标规则统一。','已有能力；7类完整操作尚未验','I01 I02 I03 F03 M03')]),
 ('调度、算法及运行状态',[
  ('准入与并发','单次/连续/试运行的排队、重入、复用和互斥规则是什么？','S21 S22','统一运行语义而非万能执行类；按工具实际线程安全与资源键准入，避免UI发起旁路。','入口已证；不同模式仍需整链验','K01 C03'),
  ('业务算法与引擎','算法步骤、ROI/修正/筛选/判断和引擎适配分别归谁？','S07 S12 S16 V04','业务规则归工具，引擎封在适配边界；当前HALCON作基线，未知VM算法不能作真值或直接替换。','自研函数体已证；VM核心未知','M01 M02 M03'),
  ('失败/停止/恢复','失败、NG、空结果、取消、超时和迟到完成是否各有确定语义？','S07 S21 S22','单轮结果状态明确；停止封新准入并处理在途，不把取消令牌当native强停；输入解包纳入失败边界。','输入try边界已证；完整恢复待验','K02 K03')]),
 ('结果模型、显示及下游消费',[
  ('结果真源与身份','多种结果投影能否证明来自同一最终集合和同一次运行？','S08 S10','声明+不可变输出快照为真源；Run/Frame/Field/Element联合寻址，Label是显示排序而非跨运行身份。','投影已证；整链身份待验','J03 A02 M01'),
  ('显示场景和只读交互','底图、轮廓、文字、高亮、来源切换如何同帧一致？','S02 S08 S10','一次发布一致场景、只读选择与编辑分离；清理只影响所属作用域，不能关工具窗清全局结果。','全局Clear已证；显示联动待验','J01 J02 I03'),
  ('下游消费与数量变化','0/1/多目标、排序变化、旧行和缺字段时能否正确取值？','S08 S13 S26','下游按类型计划读输出，不读UI树；数组与单值显式适配，旧运行选择不暗指新结果。','合同具备；真实消费者未全验','J03 G03 F02')]),
 ('持久化、版本与恢复',[
  ('保存边界','保存的是已提交配置、未失焦文本、显示偏好还是裸运行句柄？','S05 S06 S23','明确写入投影与合法候选，运行值/句柄不混作配置；用户取消不写盘，失败不部分覆盖。','写入快照存在；完整联合保存待验','H01 H02'),
  ('恢复/复制/重置','重开、复制、撤销、重置后如何重建声明、引用、视图和运行状态？','S19 S23 S24','配置恢复后重新编译有效来源；参数和运行会话分别初始化，禁止复活过期native对象。','入口基础已证；全链待验','C02 H03 O03'),
  ('合同演进与部署','接口、数据格式、插件构建和可选UI版本如何一致且可诊断？','S11 S15 S16 S27','按真实适配核契约及发布身份；仅讨论必要版本边界，不借此启动旧格式迁移或全loader重写。','部分基础；原批准边界约束实施','B01 B03 H03')]),
 ('资源所有权、缓存与退出',[
  ('图像/区域/模型/轮廓','借用输入、持久模型、单轮输出和显示几何是否被误用一种寿命？','S09 S12 V04','按资源类别分清owner/reader，作用域可不同；现有资源桥不足要明确补合同，不能默认全图复制。','部分owner已证；模型不作Blob前置','L01 L03 M01'),
  ('退役与迟到任务','换结果、关窗、删节点、切方案、应用退出时谁先停止、谁后释放？','S02 S03 S09 S20 S21','封准入→取消/等待→断开回写→解绑→owner退役→最后reader释放；窗口事件只负责所属会话。','已有退役入口；完整顺序未验','L01 L02 K02 O03'),
  ('缓存及预热','缓存身份、容量、失效和预热触发能否解释真实收益？','S09 S10','按版本/帧/样式及能力分缓存，预算含旧/新/在途；先测消费链再选预热，不默认预热或替换全部组件。','需正式冷/热与容量测量','N01 N02 N03')]),
 ('质量、风险验证与交付闭合',[
  ('可测正确性及性能','算法真值、交互延迟、显示完整性、资源上界如何独立验证？','S07 S09 S10','独立真值和同能力样本；沿已批准基线档测端到端及分段，未知不写通过。','BV及性能门已有；未运行本轮测试','P02 M02 I03'),
  ('信任与可观测性','外部输入/插件/脚本权限、错误定位、运行和资源诊断是否有边界？','S11 S19 S22','以真实信任边界审必要限制，Run/Node/版本关联诊断；没有证据不发明安全缺陷或另开治理项目。','架构检查点；风险按证据核实','A03 K03 O01'),
  ('代表链及跨消费者验收','Blob通过以后，公共能力、其他工具及最终发布如何验收？','S27 S28','先完整Blob早期人工试用，再核卡尺/模板/脚本必要差异；局部/节点/原项/总交付四层结论分开。','已有局部成果；整体验收未闭','P01 P02 P03')]),
]
old_design_audit={
 'A':['保留依赖方向约束，但用真实运行依赖而非工程名字证明。','改写为统一语义与身份，不是统一万能类型/仓库。','需具体化能力提供与权限边界，不能只是公共服务大全。'],
 'B':['保留失败显式诊断目标；需分发现、兼容和装配失败。','保留可选UI原则；不能据此断言现有SDK拆分已完成。','拆分发布身份、运行退出和资源退役的责任，不能合用一个Dispose判完成。'],
 'C':['由定义/实例二分细化为定义、持久节点、运行会话和窗口会话。','创建、复制、删除分别有图事务和资源条件，按完整状态细分。','并发与复用须按工具实际状态和线程安全选策略，不能默认单例或每次new。'],
 'D':['重审壳/视图/应用服务边界；现有基类不是默认架构。','明确用户意图与发布规则，不能由按钮名称推导全局事务。','复用是可测优化选项，事件/生命周期正确性先成立。'],
 'E':['标准组合与专业扩展并存；不强制复制VM XML或保留当前页面。','跨Tab不构成业务事务边界，必须核整候选与关联配置。','扩展点受控且能替代当前直接服务访问，避免第二接口体系。'],
 'F':['字段合同与回填应统一语义，控件不拥有正式业务状态。','标量、集合、常量与引用分别表达，不能沿旧词汇直接等同。','局部UI状态与共享业务状态分开，交互一致性需同一会话证明。'],
 'G':['保留来源目录问题；声明驱动可用性而非依赖最后运行值。','作用域、身份与权限先建模，再审现有筛选器。','失效不回退旧值；配置变更与运行值缺失须分类诊断。'],
 'H':['区分正式配置/私有草稿/运行值/显示偏好，原二分不足以覆盖所有状态。','保存不是单节点Apply；核全局变量、文本pending、快照和写盘原子边界。','复制、重置、格式三类问题应拆开论证；不自动授权历史兼容迁移。'],
 'I':['绘图是受限公共能力，不预设沿Manager增加入口。','七类是既有业务范围；应逐类验证语义，样式不替代交互。','多视口隔离与大量图形性能分别验，不能拿一个缓存补两者。'],
 'J':['显示选图与运行输入绑定不同；底图来源身份和帧要明确。','结果层只读但可选择；信息完整性不能用静默抽样换速度。','结果树与下游是独立投影，不经UI树传递业务数据。'],
 'K':['统一语义与准入不是强造一个大类；核所有真实入口。','取消、超时、停止完成分别定边界，native不强停作为现范围约束。','失败转换、业务NG、部分结果与恢复按状态分析，不只增加catch。'],
 'L':['图像/模型/轮廓寿命不同；Owner可复用不等于资源合同完整。','事件、任务、视图reader依各自所有者退出，不能靠GC兜底。','程序集/后端对象须分机制审；载体选型不因这条旧项自动获准重启。'],
 'M':['明确数据与渲染边界，但允许受控适配；不是把所有转换都禁掉。','算法负责业务语义，公共层只管理可复用机制，避免万能流水线。','类型/数量/单位/精度都审，不由CLR类型或视觉样式代替。'],
 'N':['首次成本从实际消费者端到端测，不能只计控件构造。','缓存是有边界候选；版本失效和资源持有必须共同成立。','预热需收益/成本实证，不默认保留或默认去掉。'],
 'O':['动态声明是专项差异，需版本和生命周期，不当Blob实施前置。','排除的是新增设备通信实施；原有必要输入和服务消费仍要分析，不留架构盲区。','批量操作与切方案分开建模；保原专项回归，不新增批编辑功能。'],
 'P':['代表节点验证公共性，还需其他消费者必要回归，不以单例成功代全库。','沿原批准档验证；新发现缺口不通过降低标准消除。','文档/测试/实际交付分别闭合；原ID只是追溯工具，不是架构正确性证明。']}
independent=['''<h3 id="br-funnel">完整分析漏斗：由场景与职责推导，不由旧48项划定架构</h3>
<div class="br-note"><b>范围主线：</b>工具定义与装配 → 节点/方案设计 → 配置/绑定/公共交互 → 运行/算法 → 结果/显示/下游 → 持久化恢复 → 资源退役及整体质量。参数、算法、结果、显示是例子，不是范围上限。<b>五层和旧48项都作为辅助索引，本身同样接受审查。</b></div>''']
independent.append('''<svg viewBox="0 0 1000 330" role="img" aria-label="由完整业务闭环逐级收敛到具体问题和可验证方案的分析漏斗" style="width:100%;max-height:340px;display:block;margin:15px auto">
<polygon points="30,10 970,10 880,80 120,80" fill="#245469" stroke="#6aacc4"/><text x="500" y="52" text-anchor="middle" fill="#e0f4ff" font-size="22">完整业务与生命周期：框架 ↔ 工具 ↔ 节点 ↔ 使用者</text>
<polygon points="120,90 880,90 800,160 200,160" fill="#204657" stroke="#6aacc4"/><text x="500" y="133" text-anchor="middle" fill="#e0f4ff" font-size="21">职责与能力域 → 细分能力与正常 / 失败路径</text>
<polygon points="200,170 800,170 730,240 270,240" fill="#193d50" stroke="#6aacc4"/><text x="500" y="213" text-anchor="middle" fill="#e0f4ff" font-size="20">实际调用、类型、状态、数据与资源证据</text>
<polygon points="270,250 730,250 675,320 325,320" fill="#173444" stroke="#efbd77"/><text x="500" y="293" text-anchor="middle" fill="#ffdb9f" font-size="20">具体问题 → 候选取舍 → 可观察验收</text></svg>''')
independent.append(flow([('完整业务与生命周期','识别角色、状态、数据和资源流，覆盖正常与失败路径'),('职责/能力逐级分解','按系统问题域细分子能力，不以当前类名或旧FT号切架构'),('问题与方案叶子','证据、根因、候选、取舍、消费者、验证及风险'),('反向核旧清单','保留/改写/合并/拆分/废弃/补缺；保成果与原验收记录')]))
for domain,leaves in arch_domains:
    independent.append(f'<details><summary>{E(domain)} · {len(leaves)}个细分审查点</summary>')
    for name,question,refs,recommendation,state,legacy in leaves:
        independent.append('<article><h4>'+E(name)+'</h4><p><b>架构问题：</b>'+E(question)+'</p><p><b>证据/可信边界：</b>'+ref(refs)+' · '+E(state)+'</p><p><b>当前推荐/验证要求：</b>'+E(recommendation)+'</p><small>旧清单反查：'+E('、'.join('FT-'+x for x in legacy.split()))+'；旧项不能反向限制该能力的设计。</small></article>')
    independent.append('</details>')
independent.append('''<div class="br-note"><b>完整性声明：</b>以上是本轮按场景推导的10个问题域和30个细分审查点，不是“总共只有30项问题”，也不是全部已取证完成。原48项下的已知子缺口、41项控件源码及BV01–12继续引用。发现新问题可超出旧清单表达能力；先判架构必要性和授权影响，再由用户决定实施范围。分析完整性不能靠“不新增批次”删掉必要问题，实施也不能因分析发现而自动扩围。</div>
<details id="br-legacy-scope"><summary>旧48项逐项反审与交付映射（历史依据，不是目标架构）</summary>''')
groups=[
 ('基础定义与部署','先能发现、理解和装配工具', [('A','架构与公共合同','L1/L3/L4'),('B','工具发现、加载与可选UI','L1/L2')]),
 ('节点及方案定义','节点从创建到复制、编辑、持久化与恢复', [('C','定义、节点实例及复用','L1/L3'),('H','正式配置、草稿与方案恢复','L1/L3')]),
 ('工具编辑与公共能力','窗口、页面、字段、来源与公共绘图', [('D','窗口壳及会话','L2/L3'),('E','页面组合和扩展','L2'),('F','共享字段控件和回填','L2/L3'),('G','绑定来源与作用域','L3'),('I','公共绘图与多视口','L2/L5')]),
 ('运行及结果消费','准入、算法、结果树、渲染和下游', [('K','统一执行、停止与恢复','L3/L4'),('M','类型、算法与显示边界','L3/L4/L5'),('J','图像、结果图形与结果树','L5')]),
 ('资源、性能及关联适配','跨阶段的寿命、缓存、预热与专门消费者', [('L','资源与退出','L1/L4/L5'),('N','打开成本、缓存和预热','跨层'),('O','原专项与关联边界','跨层')]),
 ('独验与最终交付','代表链验证、原项验收、统一Release和人工试用', [('P','接入验证与最终收口','跨层')]),
]
mapped=[]
funnel=['''<h4>旧清单只读对照：47项在途覆盖、1项排除，各项设计重新接受判断</h4>
<p>下列分组仅保留历史追溯；不是独立架构问题域。逐项反审是架构判断和验证要求，不冒称每项所有调用均已重新取证或全部已判定缺陷。对旧设计的质疑与对原交付事实的记录分开：历史通过不能证明设计最优，架构复审也不能抹去实际成果或自动推翻已批准在途。</p>''']
funnel.append(flow([('历史交付分组','原记录分成6个阅读分组，不用来决定目标架构'),('历史A–P分类','16个原分类；每项都重新审其合理性'),('原组件证据','41可视类及已有源码/VM材料按适用范围引用'),('原项与子缺口','48项保留身份及事实，设计允许改写和补缺')]))
ledger_time=datetime.fromisoformat(ledger['updatedAt'].replace('Z','+00:00')).astimezone(timezone(timedelta(hours=8))).strftime('%Y-%m-%d %H:%M:%S 北京时间')
funnel.append(f'<p><b>状态读取快照：</b>{E(ledger_time)}。状态与未闭环来自交付经理总账，不由本次架构评审重判。实施中不等于整项完成或正在同时编码；最新事实以交付总账为准。点击主域 → 能力域 → 原项展开。该漏斗覆盖当前已登记范围；未发现的问题不冒称已穷尽，新增发现先按原范围归属审核。</p>')
for stage,desc,domains in groups:
    funnel.append(f'<details data-br-domain><summary>{E(stage)} · {E(desc)}</summary>')
    for letter,title,layerset in domains:
        domainrows=[r for r in scope['items'] if r['id'].startswith('FT-'+letter)]
        funnel.append(f'<details data-br-domain><summary>{letter} · {E(title)} <small>分析视角 {E(layerset)} · {len(domainrows)}个原项</small></summary>')
        for item in domainrows:
            oid=item['id'];mapped.append(oid);r=ledgeritems.get(oid,{})
            status='排除新增能力' if item['scope']=='排除新增能力' else r.get('status') or '待核'
            remained=r.get('remainingAcceptance') or [r.get('acceptanceCriterion') or '未记录完整验收标准，须核原合同']
            sourceurl=quote(os.path.relpath(ROOT/'artifacts/validation/tool-framework-contract/approved-final-plan-20261024.html',PAGE.parent).replace('\\','/'),safe='/:')+'#'+oid
            evidencepaths=[x.get('path','') if isinstance(x,dict) else str(x) for x in r.get('evidence',[])]
            evidencepaths=[x for x in evidencepaths if x]
            evidencepaths=evidencepaths[:3]
            audit=old_design_audit[letter][int(oid[-2:])-1]
            leaf=(f'<article id="br-ft-{oid.lower()}" data-br-item><h4>{E(oid)} · {E(item["item"])}</h4>'
                  f'<p><b>本次设计反审：</b>{E(audit)}</p>'
                  f'<p><b>{E(status)}</b> · {E(item["scope"])} · {E(r.get("ownerRole") or "沿原责任记录")}</p>'
                  f'<p><b>范围：</b>{E(item.get("boundary",""))}</p>'
                  f'<p><b>实际证据与缺口：</b>{E(r.get("assessment") or "原账未给出结论，保持待核。")}</p>'
                  '<b>整项验收/剩余要求：</b><ul>'+''.join('<li>'+E(str(x))+'</li>' for x in remained)+'</ul>'
                  f'<p><a href="{sourceurl}">原合同与批准边界</a> · <small>不把有限子证据改判为整项通过。</small></p>'
                  '<small>证据路径：'+E('；'.join(evidencepaths) or '待补明确来源')+'</small></article>')
            funnel.append(leaf)
        funnel.append('</details>')
    funnel.append('</details>')
if len(mapped)!=48 or len(set(mapped))!=48 or set(mapped)!={r['id'] for r in scope['items']}:
    raise RuntimeError('Original scope funnel is incomplete or duplicated')
funnel.append('''</details><div class="br-note"><b>叶子记录规范：</b>每个具体问题写明业务场景 → 源码/VM证据 → 设计/实现/待核分类 → 备选与取舍 → 复用/调整/替换 → 关联消费者 → 验证步骤 → 实际结果/剩余项。能映射原FT/缺陷的保留引用；不能合理容纳的单列架构缺口，不强塞旧项。旧设计可合并、拆分、改写或废弃，但改变在途批准合同须明确决策与影响，不自行新开批次或抹去旧验收。</div>
<h3 id="br-lifecycle">生命周期覆盖：用横向场景检查纵向漏斗有没有断点</h3>''')
life=[
 ('定义/发现/装配','框架目录与发布装配；工具提供类型/版本/能力','FT-A01/A02/A03、FT-B01/B02/B03','S11/S15/S16/S19；VM V01/V02','加载失败、缺UI、重名/版本冲突明确拒绝；发布验收沿在途A11，不由本页重跑。'),
 ('拖入/创建/复制/撤销','节点定义归方案；运行实例和窗口实例分别建域','FT-C01/C02/C03、FT-H03','S01/S19/S24；VM拖放入口未贯通至全部创建','复制重建实例身份、不共享可变草稿；撤销不复活已退役native对象。'),
 ('连线/绑定/全局/作用域','正式图及绑定配置归应用事务；运行值有独立所有者','FT-F01/F02、FT-G01/G02/G03、FT-H01','S04/S06/S13/S14/S26；VM原来源树证据保留边界','失效来源、跨作用域和数量转换明确；变量/节点联合保存存在在途缺口，不能静态称已闭合。'),
 ('开窗/重开/多窗切换','宿主拥有编辑会话；视图消费受限能力','FT-D01/D02/D03、FT-E01/E02/E03','S02/S05/S25；VM V03','窗口与运行实例不是同一寿命；开窗创建工具实例不等于运行缓存复用。'),
 ('参数/ROI编辑/继承/回填','草稿私有；绘图会话仅作用所属字段与视口层','FT-F03、FT-I01/I02/I03','S02/S03/S04；VM V02/V03','拖动、数值、继承与取消同一语义；多个控件不各自发布正式值。'),
 ('校验/提交/取消/主保存','准备全部合法候选后才发布；失败保草稿','FT-H01/H02、FT-D02、FT-E02','S05/S06/S14/S23','跨Tab、节点和变量写集边界须一致；当前变量事务升级尚待专门架构审，不把本页方向当修复批准。'),
 ('运行准入/输入冻结','工作流应用层准入；执行器消费同代请求','FT-K01、FT-C03、FT-G02','S21/S22/S26','Plugin.Runtime当前是绑定基础，不冒已接管全调度；GUI执行和正式运行核同一语义。'),
 ('算法执行/业务判断','工具业务与引擎适配；取消服从实际能力','FT-M01/M02/M03、FT-K02/K03','S07/S21/S22；VM V04仍局部','业务NG与运行失败分开；停止不等于强停native算法，不能由CancellationToken存在推断可中断。'),
 ('结果发布/树/图形/下游','运行结果归owner；树/视口/下游各持reader','FT-J01/J02/J03、FT-L01、FT-M01','S08/S09/S10；VM Display声明而非全桥','同Run/Frame/Field/Element；0结果/失败不沿用旧成功值。'),
 ('保存/关闭/退出/恢复','持久化保存配置；运行对象按合同退休/重建','FT-H02/H03、FT-O03、FT-B03','S23及Repository；VM恢复与全部释放未知','只读输出、native句柄不混作正式配置；恢复后类型和来源重新核验。'),
 ('删除节点/断连/退役','先撤销新准入，处理在途，解绑消费者，最终释放','FT-C02、FT-L01/L02/L03、FT-K02','S20已有RetireNode，S21有CompleteRoot/finally；VM V04','调用了RetireNode不是完整闭环证明；在途结果不可回写被删节点，最后reader释放须独验。'),
 ('性能/缓存/演进/最终交付','跨层质量门；经理统排独验与Release','FT-N01/N02/N03、FT-O01/O03、FT-P01/P02/P03','原批准性能档与交付总账；S09/S10','正式功能/性能、设计时/运行时和人工试用分别记录；FT-O02排除保留，不扩设备通信新能力。'),
]
funnel.append(table(['生命周期阶段','责任/所有权','关联原项（引用，不重复计数）','实际证据与未贯通','闭环判据'],life))
funnel.append('''<div class="br-grid"><div class="br-card"><b>节点定义状态</b><p>创建 → 可编辑/已配置 → 修改/保存 → 删除/撤销恢复。</p><small>持久化身份与配置版本；不是每次运行对象。</small></div><div class="br-card"><b>编辑/运行状态分开</b><p>编辑：打开草稿 → 校验 → 确认发布/取消。<br>运行：准入 → 在途 → 成功/失败/取消/超时 → 完成。</p><small>推荐状态模型；并非声称当前所有生产入口已实现。</small></div><div class="br-card"><b>资源状态</b><p>创建/借用 → 被读者持有 → owner退役 → 最后reader释放。</p><small>删节点、关窗、换方案和停止分别触发不同动作；避免用一个Closed事件代替全部生命周期。</small></div></div>''')
new='\n'.join(parts).replace('Sequence<Record>','Sequence&lt;Record&gt;')
new=new.replace('<h3 id="br-basis">','\n'.join(independent+funnel)+'<h3 id="br-basis">',1)
peripherals=runpy.run_path(str(OUT/'render_blob_peripherals_review_20261024.py'))
new=new.replace('<h3 id="br-controls">',peripherals['render'](ROOT,PAGE,ref,table,flow)+'<h3 id="br-controls">',1)
start='<!-- BLOB-FIVE-LAYER-REVIEW:BEGIN -->';end='<!-- BLOB-FIVE-LAYER-REVIEW:END -->'
backend_review=runpy.run_path(str(ROOT/'artifacts/analysis/review_blob_backend_boundary_20261024.py'))
new=new.replace('当前保留HALCON算法作为基线候选，不批准引擎替换','本轮固定使用HALCON，设计保留未来后端迁移能力，不实施引擎替换')
new=new.replace('<h3 id="br-basis">','<p><a href="#br-backend-migration-review">本轮HALCON与未来OpenCV迁移边界复审 →</a></p><h3 id="br-basis">',1)
new=new+backend_review['render']()
page=PAGE.read_text(encoding='utf-8')
match=re.search(re.escape(start)+r'(.*?)'+re.escape(end),page,re.S)
if not match:raise RuntimeError('Original chapter marker missing')
old=match.group(1)
if '<!-- BR-ORIGINAL-EVIDENCE:BEGIN -->' in old:
    old=old.split('<!-- BR-ORIGINAL-EVIDENCE:BEGIN -->',1)[1].split('<!-- BR-ORIGINAL-EVIDENCE:END -->',1)[0]
else:
    old=old.replace('id="blob-five-layer-full-chain-review"','id="blob-five-layer-evidence-inventory"',1).replace('id="bfr-title"','id="bfr-evidence-title"',1).replace('aria-labelledby="bfr-title"','aria-labelledby="bfr-evidence-title"',1)
    old=old.replace('<b>推荐：</b>用Blob真实消费者收敛公共合同；','<b>初版分析附录：</b>以下逐项建议不是本次最终架构结论；处置以本页V8.1复审为准。用Blob真实消费者核查公共合同；',1)
old=old.replace('id="shared-control-design-inventory"','id="shared-control-design-inventory-history"',1)
replacement=start+'\n'+new+'\n<details class="br" id="br-original-evidence"><summary>证据附录：初版五层盘点、41类源码及BV01–12（处置建议以本次复审为准）</summary><!-- BR-ORIGINAL-EVIDENCE:BEGIN -->'+old+'<!-- BR-ORIGINAL-EVIDENCE:END --></details>\n'+end
page=page[:match.start()]+replacement+page[match.end():]
PAGE.write_text(page,encoding='utf-8')

report=dict(kind='V8.1-evidence-based-Blob-architecture-review',observedHead=head,productChanges=False,newRuntimeOrPerformanceTests=False,
    recommendation='C: semantic contracts + host application services + optional UI composition',
    status='recommendation-for-whole-review-not-implementation-approval',evidence=evidence,
    existingAPINameCorrection='CaptureForRun is a behavioral requirement, not a located src method; use actual existing publication/snapshot interfaces after consumer verification.',
    inheritedCoverage='Original BV01–12/41 visual-class inventory, finite tests retained at original scope; no new batch or acceptance denominator.',
    funnel=dict(originalIds=mapped,total=48,included=47,excluded=['FT-O02'],architectureDomains=len(arch_domains),architectureReviewLeaves=sum(len(x[1]) for x in arch_domains),legacyMappingDomains=16,lifecycleStages=len(life),
        scopeSha256=hashlib.sha256(scopefile.read_bytes()).hexdigest(),ledgerSha256=hashlib.sha256(ledgerfile.read_bytes()).hexdigest(),
        ledgerUpdatedAt=ledger.get('updatedAt'),statusAuthority='Read-only snapshot of delivery manager ledger; no status reclassification'))
(OUT/'blob-architecture-v81-review-20261024.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

class Parse(HTMLParser):
    def __init__(self):super().__init__();self.ids=[];self.links=[]
    def handle_starttag(self,tag,attrs):
        d=dict(attrs)
        if 'id'in d:self.ids.append(d['id'])
        if tag=='a' and 'href'in d:self.links.append(d['href'])
p=Parse();p.feed(page)
new_ids=[x for x in p.ids if x.startswith('br-') or x=='blob-five-layer-full-chain-review']
duplicates=[x for x in set(new_ids) if p.ids.count(x)!=1]
if p.ids.count('shared-control-design-inventory')!=1:duplicates.append('shared-control-design-inventory')
bad=[]
for link in re.findall(r'href="([^"]+)"',new):
    if link.startswith(('https://','http://')):continue
    path,_,anchor=unquote(link).partition('#')
    if path and not (PAGE.parent/path).resolve().exists():bad.append(link)
    if not path and anchor and anchor not in p.ids:bad.append(link)
if duplicates or bad:raise RuntimeError(dict(duplicates=duplicates,badLinks=bad))
inventory=json.loads((OUT/'blob-shared-controls-inventory-20261024.json').read_text(encoding='utf-8'))
missing_controls=[c['name'] for c in inventory['controls'] if c['name'] not in new]
if missing_controls:raise RuntimeError(dict(unmappedControls=missing_controls))
check=dict(passed=True,page=str(PAGE),reviewAnchors=len(new_ids),evidenceFiles=len(evidence),architectureDomains=len(arch_domains),architectureReviewLeaves=sum(len(x[1]) for x in arch_domains),originalScopeItems=len(mapped),mappedVisualClasses=41,lifecycleStages=len(life),brokenReviewLinks=bad,duplicateReviewIds=duplicates,
    pageSha256=hashlib.sha256(PAGE.read_bytes()).hexdigest(),scope='New review anchors and local links only; no product/runtime acceptance.')
(OUT/'blob-architecture-v81-static-verification-20261024.json').write_text(json.dumps(check,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(check,ensure_ascii=False))
