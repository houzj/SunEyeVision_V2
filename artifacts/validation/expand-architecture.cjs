const fs=require('fs'),path=require('path');
const root=process.cwd(),base='D:/MyWork/REA逆向测试/analysis/decompiled';
const target=path.join(root,'docs/04-工具设计/标定工具/2026/10/19/suneye-vm-design-comparison-plan.html');
const esc=s=>s.replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const link=(p,label)=>`<a href="${new URL('file:///'+p.replaceAll('\\','/')).href}">${esc(label||p)}</a>`;
const vm=p=>base+'/'+p, own=p=>root.replaceAll('\\','/')+'/'+p;
const domains=[
{id:'workflow',name:'工作流与方案',
 v:'VmSolution 组织方案，VmProcedure 组织流程，VmModuleSet / VmModule 组织模块。已核实 VmProcedure.Run 调用 ServerSDKManager.mProcessManager.SyncExecuteOnce，SyncRun 调用 SyncExecuteOnceGetEx；查询结果经模块管理器预处理再进入方案回调。托管对象承接身份、状态和结果，实际执行继续进入平台管理层。',
 vf:['VM.Core/VM.Core/VmSolution.cs','VM.Core/VM.Core/VmProcedure.cs','VM.Core/VM.Core/VmModuleSet.cs','VM.PlatformSDKCS.Audit/VM.PlatformSDKCS/ProcessManager.cs'],
 vb:'输入是方案/流程身份、命令及运行策略，输出涉及执行计数、模块结果和错误信息。存在 Load / SaveAs 和流程复制入口；文件内部格式、分支/循环/Group 的完整调度、停止与原生资源清理尚未闭合。Run 的界面响应选项也说明不能把托管层笼统称为完全无 UI。',
 o:'Solution / Workflow 保存设计对象，WorkflowExecutionEngine 组织执行。当前入口进入就绪队列执行路径，利用依赖图和资源租约调度节点；根运行会话限制同一执行器并发根请求。节点转换阶段创建参数快照，子程序经委托进入子工作流执行，算法节点继续调用工具执行边界。',
 of:['src/Workflow/WorkflowExecutionEngine.cs','src/Workflow/WorkflowExecutionDependencyGraph.cs','src/Workflow/WorkflowResourceLeaseManager.cs','src/Workflow/WorkflowRunSession.cs','src/Workflow/ToolParameterSnapshot.cs','src/Workflow/SolutionRepository.cs'],
 ob:'输入包含本轮图像、变量、参数及取消；结果经节点/工作流上下文汇集。Workflow.cs 仍直接使用 ToolRegistry；项目启用 WPF 并引用 Plugin.Infrastructure，说明物理独立不等于无界面运行边界已经成立。注释中的旧并行组描述不能覆盖实际就绪队列分支。',
 diff:'VM 的流程执行委托平台管理层；我们自行掌握调度、快照和工具调用。比较核心是执行语义与控制责任，不能仅按原生/托管区分。',
 gain:'VM：统一流程与模块身份，执行接口集中；代价是扩展与诊断依赖平台合同。我们：调度和设备语义可按产品演进，源码可追踪；代价是取消、分支、并发和恢复均由自己长期维护。',
 rec:'保留自有运行时作为候选，收敛为统一运行请求、结果与会话状态；运行层消费可注入工具目录，编辑器不能决定调度。',
 why:'已有快照和会话可作为试点基础，开放硬件需要可演进边界；但现有投入不是免审依据。只有同输入结果、取消响应、分支/子流程、尾延迟和资源增长通过验证，才冻结路线。'},
{id:'device',name:'设备与通信',
 v:'GlobalCameraManager 枚举 Global 模块目录，读取 Cs / Control 与 Librarys.Config，反射创建模块并关联 VmSolution；InitItems 从方案模块恢复设备列表。GlobalCommunicationMgr 分别提供读取、关闭与清界面委托入口。',
 vf:['VMControls.WPF/VMControls.WPF.GlobalUI/GlobalCameraManager.cs','VMControls.WPF/VMControls.WPF.GlobalUI/GlobalCommunicationMgr.cs'],
 vb:'配置经模块参数树进入编辑界面。已见相机全局模块组织，未恢复所有通信协议和驱动内部；共享连接、重连回调与帧所有权不能由此推断。原厂相机部署模式见本章原有目录树。',
 o:'相机与通信由独立运行时管理，默认装配持有工厂、发现器、轮询和连接会话。相机有采集租约；通信连接关闭会停止对应轮询。工具通过 SDK 设备服务消费能力，厂商句柄留在适配侧。',
 of:['src/DeviceDriver/Cameras/CameraRuntimeComposition.cs','src/DeviceDriver/Cameras/Contracts/CameraRuntimeManager.cs','src/DeviceDriver/Cameras/Contracts/ICameraAdapterFactory.cs','src/DeviceDriver/Communications/CommunicationRuntimeComposition.cs','src/DeviceDriver/Communications/CommunicationProtocolFactories.cs','src/Plugin.SDK/Devices/Camera/ICameraCaptureService.cs'],
 ob:'相机默认显式注册 MVS，通信工厂为 internal；独立设备扩展尚未完整实现。应用级连接、方案设备配置与本轮操作必须通过稳定身份和代次协调。',
 diff:'VM 纳入平台全局模块；我们把物理连接作为独立服务，方案与工具引用设备能力。两种方式都可以支持共享或独占，具体需合同约束。',
 gain:'VM：方案配置与模块管理入口统一；跨方案独立服务需额外规则。我们：窗口、工具与连接周期可分开，适配层便于隔离厂商；代价是引用解析、重配和退出一致性更复杂。',
 rec:'独立设备会话 + 方案内稳定引用 + 能力化提供器；标准协议复用、专有 SDK 独立适配，特殊设备按需独占或进程桥。',
 why:'对应多厂商、冷门设备和预览/工作流复用的明确需求。用至少两种实现验证同一工具，测试切方案、取消和重连；设备不支持共享时允许独占。'},
{id:'plugin',name:'工具插件与算法后端',
 v:'VmModule 承接模块身份与参数/结果，ModuleManager 提供模块创建、单模块执行、状态和关闭等管理入口。相机模块已见 Cs/Control 加载模式；其他工具的配置模型、界面控件与平台执行可分别出现，不能假设全部遵循同一设备目录。',
 vf:['VM.Core/VM.Core/VmModule.cs','VM.Core/VM.Core/ModuleParam.cs','VM.PlatformSDKCS.Audit/VM.PlatformSDKCS/ModuleManager.cs','VMControls.Data/VMControls.Data/ToolBoxData.cs'],
 vb:'创建与执行是不同入口。CloseOneModule 只说明模块关闭，不能证明程序集热卸载；原厂第三方工具打包协议和所有模块 ABI 尚未完整恢复。',
 o:'PluginLoader / PluginManager 发现插件，PluginLoadContext 解析依赖；ToolRegistry 登记元数据、创建工具和可选调试窗口。IToolPlugin 表达参数、结果、输出变量和 Run；ToolExecutor 承接运行。各工具源码位于 tools，HALCON / CV 通过适配程序集接入。',
 of:['src/Plugin.Infrastructure/Managers/Plugin/PluginLoader.cs','src/Plugin.Infrastructure/Managers/Plugin/PluginManager.cs','src/Plugin.Infrastructure/Managers/Plugin/PluginLoadContext.cs','src/Plugin.Infrastructure/Managers/Tool/ToolRegistry.cs','src/Plugin.SDK/Core/IToolPlugin.cs','src/Plugin.SDK/Core/ToolExecutor.cs'],
 ob:'加载上下文不可回收；宿主合同和宿主可解析依赖优先共享，插件私有依赖另行解析，并有 WPF BAML 解析桥。类型身份隔离有规则，但进程内插件不提供崩溃或安全隔离。',
 diff:'VM 以平台模块管理入口统一后端；我们以可执行工具合同和加载基础设施组合算法与编辑能力。两者都可能调用原生算法。',
 gain:'VM：统一模块身份与运行状态，平台负责更多管理；代价是接口开放范围及部署约束。我们：工具可独立开发并使用不同后端；代价是目录、编辑创建、依赖版本和实例作用域需要治理。',
 rec:'保留一个正式工具目录，分开发现装载、运行创建和可选编辑能力；默认重启升级，个别不稳定后端按需隔离。',
 why:'已有目录和加载规则有价值，优先消除真实职责交叉而非重建发现器。验收无 UI 执行、缺编辑器仍可运行、冲突依赖诊断和异常实例释放。'},
{id:'sdk',name:'SDK、参数与结果合同',
 v:'VM.Core 提供方案/流程/模块对象，VM.PlatformSDKCS 承接平台管理与回调；Apps.XmlParser 中 AlgorithmTabRoot 组织参数树，Apps.XmlUI 提供参数窗口。AlgorithmTabRoot 已引用 WPF、命令、VM.Core 和平台接口，不能把 XmlParser 简称为无 UI 纯解析库。',
 vf:['VM.Core/VM.Core/ModuleParam.cs','VM.PlatformSDKCS.Audit/VM.PlatformSDKCS/ServerSDKManager.cs','Apps.XmlParser/Apps.XmlParser.ParamTab.AlgorithmTabRoot.decompiled.cs','Apps.XmlUI/VisionMaster.ParamTab/ParamsTabControl.xaml.cs'],
 vb:'平台接口、对象模型、参数组织、界面承载分布于多个程序集；这种物理划分不证明完全单向依赖，也不等于已获得可公开部署的第三方 SDK 合同。',
 o:'Plugin.SDK 同时包含 Core 执行、Execution 参数/结果、Metadata、Devices、Vision、Logging、Scripting、Models，以及 UI 控件/主题和 ViewModels。工具元数据、类型词汇和输出合同已有基础，旧参数运行字段与新 Request / Context 并存。',
 of:['src/Plugin.SDK/SunEyeVision.Plugin.SDK.csproj','src/Plugin.SDK/Core/IToolPlugin.cs','src/Plugin.SDK/Core/ToolExecutionRequest.cs','src/Plugin.SDK/Metadata/ToolMetadata.cs','src/Plugin.SDK/Models/SdkTypeVocabulary.cs','src/Plugin.SDK/UI/Controls/ToolDebugControlBase.cs'],
 ob:'SDK 项目 UseWPF=true。配置快照不能自动保证图像和数组深层不可变；配置、运行服务、结果和编辑状态仍需分清所有权。',
 diff:'VM 按平台对象、桥接、参数和界面形成程序集群；我们用较集中的 SDK 同时交付合同与编辑支持。重点应比较消费者依赖和变更传播。',
 gain:'VM：专业分域便于复用，程序集间协同和部署也更复杂。我们：插件开发引用直接、资源统一；代价是无 UI 消费者承受 WPF 和较大更新面。',
 rec:'先逻辑分开稳定数据合同、运行支持、WPF 编辑支持；无 UI 消费者和独立驱动试点证明需要后，再物理拆包，避免复制类型与元数据。',
 why:'以实际消费者约束依赖比照搬程序集数更可靠。验证序列化不带运行服务，调试与正式运行使用相同快照合同，合同版本能明确诊断。'},
{id:'ui',name:'UI 宿主、画布与编辑调试',
 v:'VisionMasterMain 中已见欢迎页、方案管理和方案页面；VMControls.Data 有工具箱数据，VMControls.WPF 承载全局设备管理及配置界面相关入口。Apps.XmlUI 的 ParamsTabControl 继承 BaseWindow，窗口 Owner 指向应用主窗口，说明参数编辑还有统一窗口宿主管理。',
 vf:['VisionMasterMain/VisionMasterMain/WelcomeDialog.xaml.cs','VisionMasterMain/VisionMasterMain/SolutionManagerDialog.xaml.cs','VisionMasterMain/VisionMasterMain/SolPage.xaml.cs','VMControls.Data/VMControls.Data/ToolBoxData.cs','Apps.XmlUI/VisionMaster.ParamTab/ParamsTabControl.xaml.cs'],
 vb:'这里只确认已读界面及数据入口。完整主程序启动、画布编辑事务、调试提交/取消和运行态订阅尚未贯通，不据窗口名称认定其满足 MVVM 或无调度耦合。',
 o:'App.xaml 合并应用、节点、连接线、画布、菜单及提示资源；App.xaml.cs 承接启动和服务准备。Shell/Windows 提供主窗口与主窗口模型，Features/WorkflowUI 分出画布和共享调试，Features/Camera 等承接领域编辑。UI 引用工作流、驱动、插件、SDK 和算法适配。',
 of:['src/UI/App.xaml','src/UI/App.xaml.cs','src/UI/Shell/Windows/MainWindow.xaml.cs','src/UI/Shell/Windows/MainWindowViewModel.cs','src/UI/Features/WorkflowUI/Canvas/WorkflowCanvasControl.xaml.cs','src/UI/Features/WorkflowUI/Diagnostics/SharedDebugControl.xaml.cs'],
 ob:'目录按 Shell / Features / Services 分组，并不自动消除主窗口协调负担。需要按实际入口检查调试和正式执行是否同一请求、关窗撤订阅和迟到结果是否被拒绝。',
 diff:'VM 可见统一模块配置宿主及平台对象；我们采用 WPF 宿主、功能目录与 SDK 调试控件组合。具体画布事务和会话隔离仍需成对追踪。',
 gain:'VM：模块配置和窗口规范有集中入口；代价是宿主/参数框架协同依赖。我们：业务交互和专业编辑更灵活；代价是主窗口、服务定位和共享调试易形成隐式状态。',
 rec:'UI 只发命令和展示状态；编辑会话管理草稿、预览、提交和关闭，执行会话由运行层持有，专业编辑器复用公共宿主。',
 why:'避免窗口周期影响连接和运行，保留专业 ROI/点表能力。验证双编辑会话、关窗、取消和过期结果；必须另做 VS 设计时及真实程序人工验收。'},
{id:'controls',name:'公共控件、主题与显示',
 v:'VM.Controls 提供通用控件、窗口样式和转换器；VMBaseControls 中有图像 Shape / ROI 基类。Apps.ColorFun 的 ColorThemesResourceDictionary 从 ColorCFG/ColorStyleSet.cfg 读取主题名并定位 ColorStyle 下的 XAML。参数控件另由 Apps.XmlUI 等承接。',
 vf:['VM.Controls/styles/basewindowstyle.xaml','VM.Controls/styles/tabcontrol.xaml','VMBaseControls/VMBaseControls.Base.ImageView.Shape/ShapeControlBase.cs','VMBaseControls/VMBaseControls.Base.ImageView.ROI/ROIControlBase.cs','Apps.ColorFun/Apps.ColorFun/ColorThemesResourceDictionary.cs'],
 vb:'这是已有控件与资源入口，未系统验证所有 Popup、设计器、动态主题切换和 DPI；图像显示继承树也不说明原生缓存所有权已恢复。',
 o:'可复用控件主要位于 Plugin.SDK/UI/Controls，包括参数绑定、图像、ROI、通信编辑和 ToolDebugControlBase；SDK 有默认主题和设计时主题加载。UI/Themes 提供深浅主题并合并宿主资源。CommonControl 当前主要是 ScriptEditorCore、AvalonEdit 文本容器和高亮/错误标记，不能把它等同全产品公共控件库。',
 of:['src/CommonControl/ScriptEditorCore.xaml','src/CommonControl/SemanticHighlighter.cs','src/Plugin.SDK/UI/Controls/ParamBinding.cs','src/Plugin.SDK/UI/Controls/ImageControl/ImageControl.xaml','src/Plugin.SDK/UI/Themes/DesignTimeThemeLoader.cs','src/Plugin.SDK/UI/Themes/DefaultTheme.xaml','src/UI/Themes/Dark/DarkTheme.xaml','src/UI/Themes/Light/LightTheme.xaml'],
 ob:'控件实际分布跨 CommonControl、SDK 和宿主。参数绑定属于编辑支持，图像渲染属于显示支持，不能承担工作流调度。资源回退、宿主覆盖及显示结果代次需要明确。',
 diff:'双方均有多层控件与主题体系；VM 程序集分工与我们项目名称不一一对应，应按职责映射，不能把所有公共内容机械搬进 CommonControl。',
 gain:'VM：公共窗口/控件/图像/参数体系有复用入口；代价是框架耦合及配置维护。我们：SDK 控件直接供插件复用，宿主主题可覆盖；代价是资源键和职责跨项目，设计时与运行时可能产生不同表现。',
 rec:'先建立控件职责与资源键清单：基础视觉、参数编辑、图像/ROI、专业编辑分域；运行合同不引用控件，专业能力不为统一模板而退化。',
 why:'复用真实已有控件可降低重复实现，同时约束控件不调度。验证深浅主题、Popup、禁用/错误态、DPI、设计器、关闭释放及改样式不重算。'},
{id:'persistence',name:'保存、部署与跨域支撑',
 v:'VmSolution / VmProcedure 已见方案及流程加载、保存和复制入口；模块目录和 Librarys.Config 表达部分部署依赖。日志、权限、版本和全升级路径只有零散材料，不作完整机制结论。',
 vf:['VM.Core/VM.Core/VmSolution.cs','VM.Core/VM.Core/VmProcedure.cs'],
 vb:'原厂完整安装目录、许可证与外部分发规则、方案升级兼容及崩溃恢复未闭合；分析目录中的 csproj 不是原厂构建规则。',
 o:'SolutionRepository、WorkflowJsonConverter、AtomicJsonFile 和版本对象组织持久化相关逻辑；Directory.Build.targets 为工具输出指定 plugins/<项目名>/，宿主提供共享依赖，并复制 HALCON 等运行依赖。PluginLoadContext 与构建规则共同影响实际类型身份。',
 of:['src/Workflow/SolutionRepository.cs','src/Workflow/WorkflowJsonConverter.cs','src/Workflow/Persistence/AtomicJsonFile.cs','src/Workflow/SolutionVersion.cs','Directory.Build.targets','src/Plugin.Infrastructure/Managers/Plugin/PluginDeploymentInspector.cs'],
 ob:'默认 Release 工具路径在 Release/SunEyeVision_v1.0.0/SunEyeVision/plugins/<项目名>/；PackagingRoot 可覆盖到 app/plugins。此处核对规则，未重建或核验整份发布目录。日志关联、权限、副作用、图像生命周期和性能长稳仍按覆盖矩阵保留缺口。',
 diff:'VM 的方案与平台模块关系更紧；我们需要同时治理方案格式、工具类型登记、合同共享与构建部署。两边完整升级恢复尚未成对验证。',
 gain:'集中平台可能减少格式协调点，也受平台兼容约束；自有格式和部署更可控，但缺工具、旧类型、依赖漂移和迁移责任均需自担。',
 rec:'稳定身份与文件版本先行，依赖由构建规则交付，加载前诊断缺包/冲突；发布验收覆盖干净目录和旧方案，升级默认重启。',
 why:'直接关系方案可恢复和现场可部署。验收保存重开、复制重映射、缺插件、错误版本、异常写入恢复和干净机器启动；不把现有构建通过当成全部通过。'}
];
function block(d,side){const v=side==='v',files=v?d.vf:d.of;for(const f of files)if(!fs.existsSync(v?vm(f):own(f)))throw Error(f);return `<article id="${v?'vm':'suneye'}-${d.id}" class="architecture-domain"><h3>${d.name}</h3><h4>职责、输入输出与调用链</h4><p>${v?d.v:d.o}</p><h4>文件组织与源码入口</h4><pre>${esc(files.join('\n'))}</pre><p>${files.map(f=>link(v?vm(f):own(f),path.basename(f))).join(' · ')}</p><h4>边界、生命周期与待核实项</h4><p>${v?d.vb:d.ob}</p></article>`;}
let s=fs.readFileSync(target,'utf8');
// Extend the existing two independent descriptions before all comparisons.
for(const [id,side] of [['vm-architecture','v'],['suneye-architecture','o']]){
 const re=new RegExp(`(<section id="${id}" data-section>)([\\s\\S]*?)(</section>)`);
 s=s.replace(re,(_,a,b,c)=>a+b+`<h3>全系统分域说明：7 个领域</h3><p>下面按工作流、设备、插件、SDK、UI、公共控件和跨域支撑分别说明。每项列职责、实际文件、调用证据和未知边界；领域说明齐全不等于底层实现全部逆向闭合。</p>`+domains.map(d=>block(d,side)).join('\n')+c);
}
const comparison=`<section id="system-comparison" data-section><h2>三、全系统逐项比较</h2><p class="lead">双方说明在前，本章按同一职责逐项比较。优势与代价为结构分析，不表示经过对等性能和长稳验证。后续原比较卡保留更细证据。</p><div class="table"><table><thead><tr><th>领域 / 双方说明</th><th>设计差异</th><th>双方优势与代价</th><th>优化方向与推荐理由</th></tr></thead><tbody>${domains.map(d=>`<tr><td><strong>${d.name}</strong><p><a href="#vm-${d.id}">VM 说明</a> / <a href="#suneye-${d.id}">我们的说明</a></p></td><td>${d.diff}</td><td>${d.gain}</td><td>${d.rec}<p><strong>为什么：</strong>${d.why}</p></td></tr>`).join('')}</tbody></table></div><p>本章覆盖所有要求的主域；图像缓冲、完整调度、复制撤销、权限、日志及性能等横向问题继续按16项矩阵登记，未获得证据的部分不得标记闭合。</p></section>`;
s=s.replace('<section id="devices" data-section>',comparison+'\n<section id="devices" data-section>');
s=s.replace('<h2>三、架构比较：设备接入</h2>','<h2>设备架构：进一步比较与取舍</h2>');
const plan=`<h3>全系统目标职责与文件组织（建议，尚未实施）</h3><pre>产品装配根 / UI 宿主
  → Workflow.Runtime：运行会话、执行计划、取消、资源协调
  → ToolCatalog / PluginLoader：目录消费与装载分工
  → DeviceRuntime：设备身份、连接、操作租约与提供器
工具与设备提供器
  → Contracts：配置、类型、输入、结果、能力与错误
编辑器 / 参数控件 / 图像与 ROI
  → WpfSupport：编辑会话、资源键、显示投影
算法适配层 → HALCON / CV / 厂商 SDK
Persistence / Deployment → 版本、引用恢复、构建与依赖诊断</pre><p>以上是目标职责图，名称用于表达分域，不是批准创建一批新项目。第一阶段在现有目录清理依赖；只有无 UI 消费、独立发布或版本治理证明需要时，才拆程序集。设备提供器和工具可共包，但生命周期分别约定。</p><div class="table"><table><thead><tr><th>阶段</th><th>具体工作</th><th>推荐原因与代价</th><th>验收及改变建议的条件</th></tr></thead><tbody>
<tr><td>1 冻结合同与身份</td><td>梳理项目引用、设备/工具/运行/编辑会话身份；配置、输入、服务和结果分域。</td><td>先阻止隐式耦合继续扩散；需短期兼容旧参数入口。</td><td>同工具调试/正式请求等价，保存不带运行服务；若合同无法承接真实设备则先修订合同。</td></tr>
<tr><td>2 两个实际试点</td><td>一个简单工具与一个专业工具复用编辑会话；两个设备实现消费同一能力合同。</td><td>用真实消费者检验抽象，避免按目录重构全部插件；成本是适配和回归。</td><td>无编辑器能运行，专业 ROI/报告不退化，特殊设备不靠裸 object 逃逸；失败则收窄抽象。</td></tr>
<tr><td>3 运行与设备协调</td><td>目录注入、取消、资源租约、共享/独占、重连代次和失败恢复。</td><td>保留已有运行基础，补集中模块身份价值；并发和恢复责任仍由我们承担。</td><td>竞争/超时/取消后可再次运行，旧回调被拒绝；若尾延迟或恢复不达标则评估其他运行后端。</td></tr>
<tr><td>4 控件与分发</td><td>统一资源合同、设计时支持、提供器部署和依赖版本诊断，按需求决定拆包。</td><td>降低新增厂商和工具对宿主修改量；需兼容列表和发布维护。</td><td>VS设计器及运行时人工验收、深浅主题/DPI、全量Release与干净部署；本轮仅文档，未执行这些产品验收。</td></tr>
</tbody></table></div><p><strong>整体推荐：</strong>保留自有运行时和独立设备会话，吸收集中身份、统一状态和参数宿主的价值；先修职责和依赖，再由试点决定物理拆分。若设备必须独占、SDK需进程隔离或运行器达不到目标，应改变相应实现，不能为维护统一外观强行适配。</p>`;
s=s.replace('<h2>四、优化方案：根据比较收敛边界</h2>','<h2>四、全系统优化方案与推荐理由</h2>'+plan);
s=s.replace('<a href="#devices">三、比较：设备接入</a>','<a href="#system-comparison">三、全系统逐项比较</a><a href="#devices">设备详细比较</a>');
// Full navigable inventory of existing first-party source and reverse-analysis files.
function walk(dir){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>['bin','obj','.git','build_temp','build_validation'].includes(e.name)?[]:e.isDirectory()?walk(path.join(dir,e.name)):/\.(cs|xaml|csproj|props|targets|config)$/i.test(e.name)?[path.join(dir,e.name)]:[]);}
for(const [id,dirs,label] of [['vm-architecture',['VM.Core','VM.PlatformSDKCS.Audit','VMControls.WPF','VMControls.Data','VisionMasterMain','Apps.XmlParser','Apps.XmlUI','VM.Controls','VMBaseControls','Apps.ColorFun'],'VM 已有分析材料'],['suneye-architecture',['src','tools'],'本项目源码']]){
 const v=id==='vm-architecture',r=v?base:root,groups=dirs.map(d=>{const files=walk(path.join(r,d));return `<details><summary>${d} · ${files.length} 个代码/资源/项目文件</summary><pre>${esc(files.map(f=>path.relative(r,f).replaceAll('\\','/')).sort().join('\n'))}</pre></details>`;}).join('');
 const inventory=`<details class="file-inventory"><summary>${label}完整文件索引（限定下列目录，排除 bin/obj 和构建临时目录）</summary><p>这是实际存在文件的自动枚举，便于检索职责落点；文件列入索引不表示逐个完成语义审计。VM 索引属于分析输出，不是原厂源工程；源码索引不是发布文件清单。</p>${groups}</details>`;
 const re=new RegExp(`(<section id="${id}" data-section>[\\s\\S]*?)(</section>)`);s=s.replace(re,(_,a,b)=>a+inventory+b);
}
fs.writeFileSync(target,s);
const check=path.join(root,'artifacts/validation/check-vm-design-plan.cjs');let t=fs.readFileSync(check,'utf8').replaceAll('count(),25','count(),26').replaceAll('count()<25','count()<26').replace('count(),9','count(),10').replace('sections:25','sections:26');
t=t.replace("assert.equal(await page.locator('#coverage-matrix tbody tr').count(),16);",`assert.deepEqual(await page.locator('main > section').evaluateAll(es=>es.map(e=>e.id).filter(x=>['vm-architecture','suneye-architecture','system-comparison','architecture-summary'].includes(x))),['vm-architecture','suneye-architecture','system-comparison','architecture-summary']);
  assert.equal(await page.locator('.architecture-domain').count(),14);
  assert.equal(await page.locator('#system-comparison tbody tr').count(),7);
  assert.equal(await page.locator('.file-inventory').count(),2);
  assert.equal(await page.locator('#coverage-matrix tbody tr').count(),16);`);
fs.writeFileSync(check,t);
console.log('Expanded 7 domains per side, 7 comparisons, optimization roadmap and scoped full file inventories.');
