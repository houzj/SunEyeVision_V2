const fs=require('fs'),path=require('path'),{pathToFileURL}=require('url');
const target='docs/04-工具设计/标定工具/2026/10/19/suneye-vm-design-comparison-plan.html';
const base='D:/MyWork/REA逆向测试/analysis/decompiled/';let s=fs.readFileSync(target,'utf8');
const E=x=>String(x).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
function evidence(file,needle,vm=false){const f=vm?base+file:path.resolve(file),lines=fs.readFileSync(f,'utf8').split(/\r?\n/),i=lines.findIndex(l=>l.includes(needle));if(i<0)throw Error(file+': '+needle);return `<details class="chain-evidence"><summary>${E(path.basename(file))} · 第${i+1}行附近 · ${E(needle)}</summary><p><a href="${pathToFileURL(f).href}">${E(file)}</a>（本轮读取时行号，后续源码变更可能移动）</p><pre>${E(lines.slice(Math.max(0,i-2),Math.min(lines.length,i+7)).map((l,n)=>`${Math.max(0,i-2)+n+1}  ${l}`).join('\n'))}</pre></details>`;}
const audits=[
{id:'binding-chain',name:'绑定编译、动态端口与跨流程依赖',gap:'此前主要描述参数和全局变量对象，缺少“绑定怎样变成执行顺序”的链路。',
v:'GlobalVariableDialog → VariableManager.GetGlobalBindingRelation / ModifyLinkModuleForSub；参数窗口 → AlgorithmTabRoot；模块执行 → 平台管理器。已见绑定编辑和平台入口，中间依赖图如何构建、是否区分显示引用与执行依赖尚未恢复。',
o:'BindingRelationCatalog.Describe → BindingInputCatalog 描述活动输入 → SolutionExecutionDependencyGraph.Compile（内部编译各流程图并处理生产/消费变量）→ WorkflowExecutionManager.BuildWorkflowPredecessors → RunSolutionAsync 选取就绪流程 → WorkflowExecutionEngine 就绪队列 → AlgorithmNode.ExecuteAsync → ToolExecutionPreparation / ParameterResolver → ToolExecutor。',
scenario:'流程A产出位置，流程B通过全局变量消费；如果只按界面Tab顺序执行，B可能读旧值。改名、删除输出或动态脚本端口变化时，光更新显示文字不能修复绑定。',
vg:'VM 把绑定读写集中在变量/参数框架，调用方不用各自解析平台结构；但原生依赖策略不可由界面接口推断。',
og:'我们从活动输入和变量生产消费推导依赖，可在运行前指出失效引用；代价是普通绑定、专用输入、动态端口和旧方案兼容必须描述同一事实，漏一类会漏排顺序。',
rec:'保留统一绑定目录和编译校验，运行前生成带作用域的计划；显示引用明确标成仅展示。动态端口变化时使旧计划失效，不能仅刷新控件。',why:'已有编译链可复用，优先修统一描述的覆盖范围，比再造第二套绑定器更直接。实际业务新增能力时允许合同升级；不能为了接口稳定跳过新输入。',cost:'需要保存稳定节点/字段身份，诊断循环及缺生产者，维护旧方案迁移。',check:'跨流程逆序Tab、同名节点、生产者失败、动态字段删除、只显示自身结果不能产生算法自环。',ev:[['src/Workflow/SolutionExecutionDependencyGraph.cs','public static SolutionExecutionDependencyGraph Compile'],['src/Workflow/BindingRelationCatalog.cs','Describe(WorkflowNodeBase'],['src/UI/Services/Workflow/WorkflowExecutionManager.cs','BuildWorkflowPredecessors'],['VMControls.WPF/VMControls.WPF.GlobalUI/GlobalVariableDialog.xaml.cs','GetGlobalBindingRelation',true]]},
{id:'control-chain',name:'条件分支、子程序与循环',gap:'原“工作流”一节没有独立解释控制节点如何改变后续执行，以及Group与子程序不能直接画等号。',
v:'IfModuleTool / IMVSGroupTool 继承 VmModule / IMVSGroup → ModuParams → 平台模块执行；ModuResult → QueryModuleOneResult → SetModuleResult。这里只确认包装与结果查询，不据类型名重构完整条件跳转或循环语义。',
o:'ConditionTool.ExecuteCore 评估规则 → ConditionResults.NextNodeIds → WorkflowExecutionEngine 读取目标并裁剪分支；SubroutineTool.Run → ExecuteWithLoop 或 ExecuteSubWorkflowOnce → 注入的 ExecuteSubWorkflowAsync → 引擎递归执行 → 累积输出及显示。循环间隔使用同步等待 Task.Delay。',
scenario:'条件全部不命中时，我们返回空目标并终止该分支；循环子程序要求区分最后一轮输出与全部轮次累积，不能只按普通节点连线执行。',
vg:'VM 用统一模块身份包装条件和Group，宿主能复用参数结果机制；代价是可见包装不足以解释控制语义与错误传播。',
og:'我们控制结果显式携带后续节点，子程序复用同一引擎；但同步子程序桥接与循环等待使取消及线程占用更难，不能把工具声明异步当作全链可取消。',
rec:'先冻结分支命中、多目标、无命中、循环边界和失败传播语义；逐步让子程序执行和间隔等待原生异步并传取消，不为外观对齐把Group强制映射为子程序。',why:'这些节点会改变调度正确性，应先验证业务语义再改实现。同步桥只有在同条件测试证明无需改造时才可继续保留。',cost:'迁移接口要同时验证输出累积、递归作用域和显示代次，不能只改 await。',check:'无命中/多命中、嵌套子程序、空循环、循环中停止、迭代失败、累积类型与取消后的下一次运行。',ev:[['tools/SunEyeVision.Tool.Condition/ConditionTool.cs','result.NextNodeIds.Add'],['tools/SunEyeVision.Tool.Subroutine/SubroutineTool.cs','Task.Delay(config.LoopInterval)'],['src/Workflow/WorkflowExecutionEngine.cs','var targets = cfr.NextNodeIds'],['IMVSGroup.WorkflowAudit/IMVSGroupCs/IMVSGroupTool.cs','QueryModuleOneResult',true]]},
{id:'image-source-chain',name:'文件图像源、相机采集与连续帧入口',gap:'相机管理已独立，但从输入源选择到一轮工具拿到哪一帧的路径尚缺。',
v:'GlobalCameraManager 组织全局相机模块；ImageSourceModuleControl 与图像列表分析材料存在。设备模块到图像源的完整帧传输及队列策略本轮未恢复，不能推断它与我们相同。',
o:'WorkflowExecutionManager.RunSingleAsync → CameraWorkflowFrameFeed.StartAsync → 读取采集配置/获取采集租约 → 软件/硬件/连续模式取帧 → WorkflowRunRequest.CameraFrames → AlgorithmNode.CameraFrame → ImageLoadTool.ExecuteAsync。文件模式另经 CreateImageLoadParameterOverrides 冻结本轮路径 → ImageLoadTool.Run → HalImage.CreateFromFile。',
scenario:'连续模式算法比相机慢，预览通常偏好新帧；逐件检测可能要求每帧必处理，两者不能默认共用“丢旧保新”。文件列表切换也不应改变已启动一轮的输入路径。',
vg:'VM 设备与图像源模块可能统一平台引用，但具体排队和触发规则尚无足够证据。',
og:'我们已有统一运行帧入口和路径覆盖快照，工具无需持有WPF控件；代价是采集策略仍由UI项目里的管理器装配，触发、预览与正式采集必须协调租约。',
rec:'将输入源解析和帧策略移为可测试运行服务；提供“最新帧”与“逐触发保序”等明确能力，不能把丢帧策略静默用于所有工位。',why:'输入帧身份直接决定结果是否对应当前工件。先定义工艺要求，再选缓存策略，比统一一个无限队列或强制全部最新帧更可靠。',cost:'保序会增加背压/缓存与超时成本；最新帧必须显式统计被替换数量。',check:'慢算法、连续帧、硬触发超时、文件序列切换、预览竞争、切方案与帧归属。',ev:[['src/UI/Services/Workflow/CameraWorkflowFrameFeed.cs','public static async Task<CameraWorkflowFrameFeed> StartAsync'],['src/UI/Services/Workflow/WorkflowExecutionManager.cs','CreateImageLoadParameterOverrides'],['tools/SunEyeVision.Tool.ImageLoad/ImageLoadTool.cs','request.CameraFrame ??'],['VMControls.WPF/VMControls.WPF.GlobalUI/GlobalCameraManager.cs','cameraInfo.Type + "Cs.dll"',true]]},
{id:'image-lifetime-chain',name:'图像、缓冲与结果缓存所有权',gap:'原文列了图像接口和结果视图，没有追谁生产、替换和释放帧。',
v:'IMVSGroupTool.ModuResult → QueryModuleOneResult → mModuleResultManager.SetModuleResult；ModuleResultView 有当前显示信息释放路径。平台结果转换、原生指针借用及显示缓存的完整所有权仍未知。',
o:'CameraWorkflowFrameFeed.OnFrameAsync → _pending 替换并 Dispose 旧帧 → 消费者取走本轮帧 → 运行请求借用；DisposeAsync 清剩余帧并释放采集租约。CameraFrame.Dispose 会调用 Image.Dispose；WorkflowRunRequest 注释要求宿主在执行完成后释放本轮帧。结果显示是否仍持有该图像要继续沿渲染与缓存验证。',
scenario:'算法输出直接引用输入图像，运行结束释放原帧后，结果视图若继续借用可能访问已释放对象；反过来永久保留输入又可能使长连运行内存增长。这里是待核实风险，不是已复现缺陷。',
vg:'VM 平台结果管理集中，有统一转换入口；底层所有权不可见会增加独立重建难度。',
og:'我们的帧包装和替换释放可直接审查；但 IReadOnlyDictionary 与变量浅拷贝不能保护内部图像，结果、显示和脚本都可能延长引用。',
rec:'对帧、工具输出和显示缓存逐类规定借用、复制或引用计数；显示缓存有容量及代次，不能仅靠每层都实现 IDisposable。',why:'只有明确跨层转移规则才能同时避免过早释放和泄漏；是否复制由性能与对象语义决定，不统一做深拷贝。',cost:'需要审计各工具返回输入原对象的情况，计量原生内存与句柄。',check:'共享输入多消费者、失败/取消、结果切换、脚本保留引用、长时间连续运行的内存/句柄增长。',ev:[['src/UI/Services/Workflow/CameraWorkflowFrameFeed.cs','replaced?.Dispose();'],['src/Plugin.SDK/Devices/Camera/CameraFrame.cs','Image.Dispose();'],['src/Workflow/WorkflowRunRequest.cs','宿主在执行完成后释放帧'],['IMVSGroup.WorkflowAudit/IMVSGroupCs/IMVSGroupTool.cs','SetModuleResult',true]]},
{id:'script-chain',name:'脚本编译、生命周期和扩展程序集',gap:'脚本此前只归为一个工具项目，未解释编译加载、缓存与信任边界。',
v:'现有平台分析有 GlobalScriptReportCallBack / GlobalScriptCrashMotice 等回调材料，未恢复脚本编译器、权限与隔离链。不能根据名称宣称VM脚本是独立进程或安全沙箱。',
o:'ScriptTool.ExecuteCore → ScriptLifecycleManager.Execute → ScriptCompiler.Compile → ScriptSandboxPool.GetOrCreate（按代码/引用键）→ ScriptSandbox.LoadAssembly → CreateScriptInstance → 输入字段注入 → Init → Process → 输出读取。ScriptSandbox.Unload 调用可回收 ALC.Unload，脚本池和编译缓存需分别治理。',
scenario:'用户反复修改脚本或增加DLL引用，编译缓存、脚本实例和ALC可能有不同生命周期；脚本死循环不会因为传入取消令牌就必然停止。',
vg:'VM 暴露报告/崩溃通知，说明有平台反馈入口；其实现和保障范围未知。',
og:'我们用Roslyn编译与类型化输入输出，插件能复用脚本；代价是缓存键、静态引用、后台线程、原生调用和卸载失败均由我们负责。可回收ALC与工具插件不可回收ALC不是同一策略。',
rec:'明确当前脚本是可信进程内扩展；编译缓存、实例周期和停止行为独立定义。需要执行不可信代码或可靠强制终止时，再以独立进程实现隔离。',why:'ALC只解决装载和类型作用域，不是权限沙箱；但把所有可信脚本移到IPC也会引入图像传输和调试成本。',cost:'进程隔离需设计输入输出序列化、超时杀进程和重启；进程内方案需接受合作取消及宿主故障边界。',check:'修改代码/引用失效缓存、编译错误、Init/Process异常、重复执行、无限循环终止策略、卸载资源增长。',ev:[['tools/SunEyeVision.Tool.Script/ScriptEngine/ScriptLifecycleManager.cs','var sandbox = ScriptSandboxPool.GetOrCreate'],['tools/SunEyeVision.Tool.Script/ScriptEngine/ScriptLifecycleManager.cs','scriptInstance.Process();'],['tools/SunEyeVision.Tool.Script/ScriptEngine/ScriptSandbox.cs','_alc.Unload();']]},
{id:'communication-chain',name:'通信事件、触发执行与协议收发',gap:'通信管理窗口与协议驱动之外，遗漏了外部命令如何进入运行器和如何验证目标。',
v:'GlobalCommunicationMgr 区分读取/关闭/清界面；TriggerProcedurePanel 有流程触发配置入口。协议解帧→命令验证→流程调度→完成响应的原厂整链尚未恢复。',
o:'MainWindowViewModel.InitializeCommunicationAutomation 将 automation.Dispatcher 赋为 DispatchCommunicationAsync → RunCommunicationOperationAsync 取得操作作用域 → UI Dispatcher 读取方案和参数快照 → ValidateTarget 校验赋值范围 → RunSolutionAsync / RunSingleAsync / RunCommunicationNodeAsync；开始/完成/停止/错误事件回送 WorkflowStateChanged。切方案命令走单独切换路径。',
scenario:'外部命令只执行模块A，却携带模块B参数；不校验范围就可能修改本轮外对象。通信完成灯和连接灯也不能代替目标流程实际执行结果。',
vg:'VM 有集中通信与触发配置，可在平台对象下关联流程；内部协议和时序仍未知。',
og:'我们已有目标范围验证与通信操作互斥，限制错误赋值；但分发器实际依赖主窗口模型、Dispatcher和工作流Tab，执行控制仍穿过UI。',
rec:'把命令验证、业务分发与执行状态响应下沉应用服务；主窗口只提供用户视图和必要编辑提交。保留类型化收发工具，按协议处理帧边界与质量。',why:'这条实际链证明UI调度边界尚未落实；先迁移真实触发路径，比只把接口挪到SDK更有收益。',cost:'需迁移状态订阅、切方案互斥和错误响应，不能丢现有目标验证。',check:'目标越界、重复触发、忙时策略、停止/超时、切方案、半包/粘包、响应关联到正确运行。',ev:[['src/UI/Shell/Windows/MainWindowCommunicationAutomation.cs','automation.Dispatcher = DispatchCommunicationAsync'],['src/UI/Shell/Windows/MainWindowCommunicationAutomation.cs','CommunicationTriggerEvaluation.ValidateTarget'],['src/UI/Shell/Windows/MainWindowCommunicationAutomation.cs','var dispatcher = Application.Current'],['VMControls.WPF/VMControls.WPF.GlobalUI/GlobalCommunicationMgr.cs','class GlobalCommunicationMgr',true]]},
{id:'lifecycle-chain',name:'启动装配、单实例、方案切换与退出',gap:'UI主窗口和设备生命周期已列，但缺启动顺序和资源回收的应用级链路。',
v:'已见 VmSolution、Load_Solution、GlobalCameraManager.InitItems 及 MainViewControl.Solution 等入口；不能把它们拼成已证实的完整原厂启动退出时序。',
o:'App 启动先加载插件（参数类型注册依赖加载结果）→ ServiceInitializer.InitializeServices / InitializeSolutionManager → 设置当前方案 → 初始化主题与监控 → 恢复相机连接。退出链可见先 DisposeCommunicationRuntimeAsync 再 DisposeCameraRuntimeAsync；实际停止/等待窗口和切方案事务需结合执行管理器继续验证。',
scenario:'旧方案运行未结束就恢复新方案设备，会使旧回调写入新视图；插件尚未登记类型时反序列化方案会无法解析参数类型。',
vg:'VM 有方案和模块恢复入口，平台可集中恢复身份；完整事务与回滚未知。',
og:'我们装配顺序可追踪，部署诊断与恢复可主动设计；但 App、ServiceInitializer和主窗口承担的职责分散，异常中途启动和同步等待退出可能复杂。',
rec:'建立可取消的应用状态转换：准备服务→装载类型→恢复方案→准备设备→接受运行；切换先拒绝新请求、停止等待旧运行，再换方案/设备，失败可诊断。',why:'装配顺序直接影响可恢复性。先复用现有服务并集中转换协调，不把所有初始化塞入新的万能管理器。',cost:'每一步必须有失败出口与补偿，设备自动连接失败不能吞掉也不应无条件阻止离线编辑。',check:'第二实例激活、缺插件、半启动失败、设备不在线、运行中切方案、退出超时与未完成保存。',ev:[['src/UI/App.xaml.cs','ServiceInitializer.InitializeServices();'],['src/UI/App.xaml.cs','ServiceInitializer.InitializeSolutionManager'],['src/UI/App.xaml.cs','DisposeCommunicationRuntimeAsync'],['src/UI/Services/Workflow/WorkflowExecutionManager.cs','public async Task StopAndWaitAsync']]},
{id:'persistence-chain',name:'保存恢复、复制撤销与版本',gap:'保存与部署曾同列，缺“怎样写文件”和“编辑撤销不是持久化回滚”的区别。',
v:'VmProcedure.CopyProcedure 调用 Export 再 ImportProFile；VmProcedure.Load / SaveAs 提供平台流程文件入口，底层格式和原子写入策略未恢复。',
o:'SolutionRepository.Save → JsonSerializer.Serialize → 同目录临时文件 → 保存旧内容恢复副本 → File.Replace 或首次 File.Move。画布另由 CommandManager 和可撤销命令管理编辑；GlobalVariable/Solution克隆不代表全部深拷贝，身份重映射需独立检查。',
scenario:'保存到一半异常不能损坏已有方案；复制节点时如果稳定ID未重映射，绑定可能仍指向原节点。撤销一次UI删除也不等于撤销设备实际写入。',
vg:'VM 平台导入导出可封装模块身份与附属数据，降低宿主格式处理；兼容与迁移受平台规则约束。',
og:'我们的临时写入和恢复副本提供可审查保护；但参数类型注册、缺插件恢复、版本迁移和跨对象ID同步需要自己维护。',
rec:'分别定义文件事务、编辑命令事务与外部副作用；稳定身份/版本由合同管理，缺插件保留可恢复信息，复制重映射通过统一服务。',why:'这三类事务的回滚能力不同，混成一个“撤销”会产生误导；保留原子写入基础比重写格式更合适。',cost:'需要旧方案样本、异常注入及恢复策略；不能用文件扩展名相同宣称兼容。',check:'磁盘写失败、旧文件恢复、缺工具/版本、复制含绑定节点、批删撤销与保存重开一致性。',ev:[['src/Workflow/SolutionRepository.cs','File.Replace(temporaryPath'],['src/UI/Features/WorkflowUI/Commands/CommandManager.cs','class CommandManager'],['VM.Core/VM.Core/VmProcedure.cs','isFromCopy: true',true]]},
{id:'algorithm-chain',name:'算法后端、标定与正式结果发布',gap:'算法适配此前仅列项目；求解、验证、发布是三种不同责任，不能当成普通工具返回值。',
v:'已见模块参数/平台结果与显示框架；没有本轮对等证据证明原厂标定模型、验证门槛及正式文件发布和我们一致。原厂算法内部不从界面或模块名称推断。',
o:'NPointCalibrationTool.Execute → 复制有效样本、验证输入/契约/取消 → HalAdapter 原生算子路线求解 → 候选结果；NPointVerificationTool 读取候选及摘要 → 独立验证 → 接受时重查候选摘要/配置 → NPointCalibrationFile.Publish 正式文件。参数修订与指纹用于拒绝旧结果。',
scenario:'求解通过后用户改变点表，迟到结果不能写成新配置的正式标定；候选文件被替换后也不能复用旧验证结论。',
vg:'VM 平台结果合同与显示入口统一；没有证据可比较其算法精度或发布保护优劣。',
og:'我们显式区分候选、独立验证和正式发布，可保护工艺配置；代价是文件版本、采样证据、指纹和发布状态更复杂。自动现场采样上游可信证据仍有接入缺口。',
rec:'保留 HALCON 生产路线与候选/正式分离，不借本次架构整理改算法；发布作为有条件提交动作，工具结果与UI显示不得绕过验证。',why:'正式标定会影响下游坐标和现场动作，防止陈旧结果比减少一个对象更重要。其他纯计算工具无需强制套完整发布流程。',cost:'继续维护兼容/拒绝旧格式策略、真实工位门槛与独立验证数据。',check:'改样本、取消、候选摘要变化、重复发布、错误相机/平面身份；人工精度与自动采样缺口单列。',ev:[['tools/SunEyeVision.Tool.NPointCalibration/NPointCalibrationTool.cs','InputFingerprint = parameters.Fingerprint()'],['tools/SunEyeVision.Tool.NPointCalibration/NPointVerificationTool.cs','旧验证结论失效'],['tools/SunEyeVision.Tool.NPointCalibration/NPointVerificationTool.cs','NPointCalibrationFile.Publish']]},
{id:'diagnostics-chain',name:'事件总线、日志与性能候选实现',gap:'目录里存在 EventBus、引擎池和性能类，但不能把存在的类都画进主执行链。',
v:'MainViewControl / LogView 有展示入口；未贯通平台事件报告、限频与追踪关联，不宣称其全链优于或弱于我们。',
o:'Core.EventBus 提供 Subscribe/Publish/Unsubscribe；App 对重命名处理器有实际订阅。WorkflowEventPublisher 将执行事件转换为总线事件；此类存在不等于所有运行入口都调用。UI/Services/Performance 中的池、缓存和基准类须先查引用才能标为活动后端。主引擎已见入口直接进入就绪队列，旧并行分组代码在return之后。',
scenario:'仅根据性能类名推荐引擎池，可能在根会话互斥和设备独占前提下制造并发；逐帧日志推送UI也可能让可观测性本身成为瓶颈。',
vg:'集中平台可统一结果与状态上报，但目前缺吞吐/长稳和限频证据。',
og:'自有事件与日志便于加入运行/设备身份；代价是事件解绑、线程切换、重复通道及缓存容量都需治理。',
rec:'架构图区分活动调用、兼容入口、未接入候选；日志记录关键状态和关联ID，按故障定位需求决定采样与限频。先测代表性负载再启用池化并发。',why:'真实调用关系比类数量更能解释系统；没有测量不以“Performance”命名推导性能收益。',cost:'需建立可复现负载和原生内存/句柄指标，统计本身应有低开销边界。',check:'重复订阅、关窗解绑、日志爆量、队列积压、取消延迟、P95/P99及持续运行资源曲线。',ev:[['src/Core/Events/EventBus.cs','public void Publish'],['src/UI/App.xaml.cs','eventBus.Subscribe(renameHandler)'],['src/Workflow/WorkflowExecutionEngine.cs','return await ExecuteWorkflowWithReadyQueue']]},
{id:'access-chain',name:'角色权限、语言资源与外部副作用',gap:'此前没有明确哪些只是按钮可见性、哪些需要运行服务真正校验，也缺语言资源对应现状。',
v:'GlobalCameraManager 根据 CodeManager.GetLoginUser、AccessUser 及运行状态拒绝部分操作；提示通过 AppLocalizationService.GetLang 获取。AccessManagerHelper 读取并恢复访问配置信息，完整鉴权/授权链未恢复。',
o:'本项目UI、SDK和工具中存在大量直接中文提示；本轮未确认与VM等价的完整角色/语言服务。通信调度已有目标范围校验，但范围校验不等于用户权限。脚本和进程内插件也不能作为权限隔离。',
scenario:'隐藏“断开相机”按钮并不能阻止通信命令或脚本调用同一操作；切语言时拼接提示与资源字符串也可能混杂。',
vg:'VM 已见角色与语言入口有集中复用价值；仅凭界面判断不能证明底层所有入口受控。',
og:'我们可先按实际部署需求做最小授权与消息合同，避免复制不必要账户体系；代价是当前缺口要如实列为待设计，不能宣称全权限已实现。',
rec:'先确认部署角色和语言需求；若要求限制发布/写设备/换方案，授权检查放在业务动作入口，UI消费同一结果。消息码与展示文案分离，按需求逐步资源化。',why:'基于真实操作边界比全局屏蔽按钮有效；不自动引入复杂用户体系或承诺安全沙箱。',cost:'需要定义可信插件前提、审计与拒绝原因，评估既有脚本/外部触发能否绕过边界。',check:'同动作由UI/通信/脚本触发的一致授权、运行中限制、语言回退、格式化参数与操作审计。',ev:[['VMControls.WPF/VMControls.WPF.GlobalUI/GlobalCameraManager.cs','AccessUser loginUser',true],['VMControls.WPF/VMControls.WPF.GlobalUI/GlobalCameraManager.cs','Lang_Rename',true],['src/UI/Shell/Windows/MainWindowCommunicationAutomation.cs','ValidateTarget']]}
];
function card(d){return `<article class="chain-audit" id="${d.id}"><h3>${d.name}</h3><p class="note"><strong>本轮补的缺口：</strong>${d.gap}</p><h4>VM：已证实的调用边界</h4><p>${d.v}</p><h4>我们：实际入口与数据/控制流</h4><p>${d.o}</p><h4>具体场景</h4><p>${d.scenario}</p><div class="tradeoff-grid"><div class="tradeoff-side"><h4>VM的收益与代价</h4><p>${d.vg}</p></div><div class="tradeoff-side"><h4>我们的收益与代价</h4><p>${d.og}</p></div></div><div class="recommendation"><h4>推荐与具体理由</h4><p>${d.rec}</p><p><strong>理由：</strong>${d.why}</p></div><p><strong>选择后的成本：</strong>${d.cost}</p><p><strong>验证门槛：</strong>${d.check}</p><h4>实际调用片段与文件职责</h4>${d.ev.map(e=>evidence(...e)).join('')}<p>证据为当前静态源码观察；未执行原厂、现场设备或长稳实验，未知链路不以设计分析补成事实。</p></article>`;}
const section=`<section id="chain-audit" data-section><h2>补漏审查：11条实际调用链与具体取舍</h2><p class="lead">按入口、数据、执行、结果和释放复查，补充此前按项目分域容易漏掉的横向链路。每条附定位到方法和行号附近的实际代码片段；不是只列文件名，也不是声称整个领域完成逆向。</p><div class="overview-links">${audits.map(d=>`<a href="#${d.id}">${d.name}</a>`).join('')}</div><div class="note warning"><strong>本轮修正：</strong>我们的跨流程排序、变量生产值提交、外部通信分发仍有业务协调在 UI 项目；“UI 不调度”是目标要求，尚不能当作现状。全局变量生产值已有运行快照保护，不再笼统登记为完全未知。</div>${audits.map(card).join('')}<h3>补漏后仍未闭合的范围</h3><p>原生调度/设备内部、ROI坐标与共享结果全资源链、全量类型兼容迁移、权限全部入口、完整日志长稳和真实设备支持列表仍需专项审查。16领域矩阵的保守状态本轮不升级为闭合；部分链路新证据不代表整域验证。</p></section>`;
s=s.replace('<section id="system-comparison" data-section>',()=>section+'\n<section id="system-comparison" data-section>');
s=s.replace('<a href="#system-comparison">三、全系统逐项比较</a>','<a href="#chain-audit">补漏 · 11条实际调用链</a><a href="#system-comparison">三、全系统逐项比较</a>');
// Correct earlier descriptions with new evidence instead of silently leaving inconsistent unknowns.
s=s.replace('我们的生产者写回时机、跨流程可见性、失败值/上一轮值及图像释放还需贯通，不能因有唯一生产者字段就认定并发问题已解决。','本轮已追到 RunSolutionAsync：根据生产消费关系排序流程，ApplyProducedGlobalValues 先移除旧值、仅提交成功生产者到 runtimeGlobals，不改方案初值；共享图像释放和全部并发场景仍未闭合。详见补漏调用链。');
s=s.replace('已有快照和会话可作为试点基础，开放硬件需要可演进边界；但现有投入不是免审依据。','已有快照和会话可作为试点基础，开放硬件需要可演进边界；跨流程排序和通信分发仍在UI项目，必须迁移这些实际职责，不能仅保留现状。');
// A scoped module inventory ties source families to reviewed or pending behavior.
const tools=fs.readdirSync('tools',{withFileTypes:true}).filter(e=>e.isDirectory()).map(e=>e.name);
const classify=n=>/Script/.test(n)?['脚本编译执行','script-chain']:/Condition|Subroutine/.test(n)?['控制流','control-chain']:/ImageLoad/.test(n)?['文件与采集输入','image-source-chain']:/Protocol|ReceiveData|SendData/.test(n)?['协议与收发','communication-chain']:/Calibration/.test(n)?['标定与验证发布','algorithm-chain']:n==='FixEncoding'?['维护工具（不据目录认定为运行插件）','']:['图像/几何算法工具（仅分类，未逐个审计算法）','algorithm-chain'];
const inventory=`<h3>工具目录漏项复核：全部当前一级目录</h3><div class="table"><table><thead><tr><th>实际目录</th><th>职责分类与审查入口</th><th>覆盖边界</th></tr></thead><tbody>${tools.map(n=>{const [label,id]=classify(n);return `<tr><td>${E('tools/'+n)}</td><td>${id?`<a href="#${id}">${label}</a>`:label}</td><td>${/Calibration|Script|Condition|Subroutine|ImageLoad/.test(n)?'本轮追了代表性执行路径，不表示每个参数和异常分支全部通过。':'当前是目录覆盖清单；普通算法的算子/几何/输出逐项验证仍待专项。'}</td></tr>`;}).join('')}</tbody></table></div><p> Core、CvEngine、适配层、事件服务与构建规则已纳入依赖/调用审查；Tests 和性能基准用于验证，不能作为产品活动模块。按目录枚举的完整性与按运行链验证的完整性分别记录。</p>`;
s=s.replace('<h3>补漏后仍未闭合的范围</h3>',inventory+'<h3>补漏后仍未闭合的范围</h3>');
// Add concrete chain references to every existing architecture comparison.
const refs={workflow:['control-chain','绑定、分支和子程序会改变真正执行顺序。场景：B消费A生产值时，即使Tab在前也应等待A；迁移UI内跨流程排序时必须保留此行为。'],device:['image-source-chain','同相机预览与工作流争用时，租约只能解决部分互斥；先声明触发模式和保序要求，再决定共享策略。'],plugin:['script-chain','新增带私有库的工具时，加载隔间仍须共享SDK类型；脚本可回收ALC不能反推全部工具支持热卸载。'],sdk:['binding-chain','动态脚本输出改名会同时影响参数选择、依赖图和序列化，稳定合同应覆盖这三处，不能复制三份元数据。'],ui:['communication-chain','通信执行目前实际经过主窗口和UI Dispatcher；推荐下沉的是这条业务分发与状态协调链，不只是拆窗口文件。'],controls:['image-lifetime-chain','图像控件换帧时，既要正确显示ROI坐标，也要处理旧图像引用；统一主题不能代替所有权规则。'],persistence:['persistence-chain','同目录临时写入和恢复副本已有价值，迁移方案不应丢失；画布撤销不能承诺撤回一次设备发送。'],globals:['binding-chain','生产者失败会先清除本轮变量旧值，消费者应得到不可用诊断；不得退回上一轮值或把运行值保存成初值。'],'camera-management':['image-source-chain','预览借用已有会话关闭后保持连接，自建会话关闭走释放路径；统一服务不能抹去这两个明确分支。']};
for(const [id,[ref,txt]] of Object.entries(refs)){
 s=s.replace(new RegExp(`(<article[^>]*id="suneye-${id}"[\\s\\S]*?)(</article>)`),(_,a,b)=>a+`<h4>本轮补充：具体调用与代价</h4><p>${txt} <a href="#${ref}">查看方法链、双方取舍和验证条件</a>。</p>`+b);
}
const uiDetails={shell:['lifecycle-chain','先载插件再反序列化参数是实际顺序约束；启动页不应独自决定设备重连和方案切换。'],commands:['communication-chain','同一个开始动作从按钮或通信进入，应共用忙状态与目标校验；不能仅依赖按钮禁用。'],canvas:['binding-chain','批量删除与撤销要同时恢复节点、连线及绑定身份；一个UI集合回滚不足以保证运行计划有效。'],toolbox:['script-chain','工具目录与缓存失效分开；脚本编译预热属于工具生命周期，不应阻塞工具箱展示或被重复触发。'],editor:['algorithm-chain','标定点表改动后旧验证结果不能提交；通用编辑宿主需调用领域提交保护而非直接把旧结果写回。'],image:['image-lifetime-chain','运行请求借用帧而帧释放会释放图像；结果或预览继续展示前必须明确复制/保留约定。'],results:['image-lifetime-chain','结果视图显示输入原图时，运行结束释放帧可能影响缓存；此风险需顺显示引用验证，不冒充已复现故障。'],globals:['binding-chain','ApplyProducedGlobalValues 写运行快照而非方案初值；UI应区分初值与本轮值，避免用户误判保存内容。'],camera:['image-source-chain','预览借用/自建会话、连续保新帧与逐触发保序是不同策略；优先保证工件与帧身份正确。'],communication:['communication-chain','Dispatcher内验证赋值目标范围后才执行；下沉业务服务时必须保留这一保护与工作流忙响应。'],solution:['persistence-chain','已有写临时文件、保留恢复副本和原子替换；方案窗口应反馈保存是否成功，不能点击后先清未保存标记。'],logs:['diagnostics-chain','事件订阅与窗口寿命不同；同一运行既发总线又发UI事件时要避免重复日志与泄漏。'],theme:['access-chain','按钮可见性、角色授权与语言文案是三个职责；主题统一不意味着操作权限或多语言完成。']};
for(const [id,[ref,txt]] of Object.entries(uiDetails))s=s.replace(new RegExp(`(<article class="ui-review" id="ui-${id}">[\\s\\S]*?)(</article>)`),(_,a,b)=>a+`<h4>具体机制、代价与推荐依据</h4><p>${txt} <a href="#${ref}">沿实际链查看证据、承担的成本与验收</a>。</p>`+b);
s=s.replace('<h2>四、全系统优化方案与推荐理由</h2>','<h2>四、全系统优化方案与推荐理由</h2><div class="note warning"><strong>本轮优先级修正：</strong>先迁移 UI 项目中的跨流程依赖排序、运行变量提交和通信业务分发；保留已存在的范围校验、旧值移除和方案初值保护。其次梳理输入帧与显示所有权、子程序取消、脚本缓存/信任边界。最后再决定物理拆包与目录调整。只移动文件而不改变消费者和作用域，不算完成边界治理。</div>');
fs.writeFileSync(target,s);
const cp='artifacts/validation/check-vm-design-plan.cjs';let c=fs.readFileSync(cp,'utf8').replaceAll('count(),27','count(),28').replaceAll('count()<27','count()<28').replace("locator('section[data-section]:visible').count(),11","locator('section[data-section]:visible').count(),12").replace('sections:27','sections:28');c=c.replace("assert.equal(await page.locator('.ui-review').count(),13);","assert.equal(await page.locator('.ui-review').count(),13);\n  assert.equal(await page.locator('.chain-audit').count(),11);\n  assert.ok(await page.locator('.chain-evidence').count()>=30);\n  for(const item of await page.locator('.chain-audit').all()){assert.ok((await item.textContent()).includes('验证门槛'));assert.ok((await item.textContent()).includes('选择后的成本'));}\n");fs.writeFileSync(cp,c);
console.log(JSON.stringify({auditChains:audits.length,toolDirectories:tools.length,baseline:'3a5dc6dd54f372699a450ad7862b52c961cd2797'}));
