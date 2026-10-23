const fs=require('fs'),crypto=require('crypto');
module.exports=function(){
const root='artifacts/validation/vm-official-sdk-442/dotnet/';
const docDefs=[
['sdk-doc-solution','_xE6_x96_xB9_xE6_xA1_x88_xE7_x9B_xB8_xE5_x85_xB3_xE6_x93_x8D_xE4_xBD_x9C.html','VmSolution.Load','方案相关操作','官方示例加载/枚举/执行/保存/关闭，并要求退出前释放；结果回调需按读取需要启用。'],
['sdk-doc-module','_xE6_xA8_xA1_xE5_x9D_x97_xE7_x9B_xB8_xE5_x85_xB3_xE6_x93_x8D_xE4_xBD_x9C.html','SetImageData','模块相关操作','参数与临时输入分开；图像/输入/ROI说明当次有效；每次运行后重新取得结果对象。'],
['sdk-doc-procedure','_xE6_xB5_x81_xE7_xA8_x8B_xE7_x9B_xB8_xE5_x85_xB3_xE6_x93_x8D_xE4_xBD_x9C.html','SetInputImage_V2','流程相关操作','流程输入/结果、连续运行、流程导入导出及释放；导入/保存标注非线程安全。'],
['sdk-doc-globals','_xE5_x85_xA8_xE5_xB1_x80_xE6_xA8_xA1_xE5_x9D_x97_xE7_x9B_xB8_xE5_x85_xB3_xE6_x93_x8D_xE4_xBD_x9C.html','GlobalVariableModuleTool','全局模块相关操作','全局变量、通信、相机、光源和队列有独立对象入口；不将其协议或授权迁入自研。'],
['sdk-doc-events','_xE4_xBA_x8B_xE4_xBB_xB6_xE8_xAE_xBE_xE7_xBD_xAE_xE6_x96_xB9_xE6_xB3_x95.html','OnWorkStatusEvent','事件设置方法','方案/流程/模块分级订阅；流程ID与工作状态用于区分事件，示例末尾释放方案。'],
['sdk-doc-output','_xE9_x80_x9A_xE8_xBF_x87_xE6_xB5_x81_xE7_xA8_x8B_xE8_xBE_x93_xE5_x87_xBA_xE6_x88_x96_xE8_x80_x85f73babc98d494d79cf1981c0a0321c6d.html','GetOutputInt','通过流程输出或模块输出取数据','官方推荐流程输出以降低应用对具体模块的耦合；标量、数组、字符串、图像分别读取。'],
['sdk-doc-view','_xE9_x80_x9A_xE8_xBF_x87_xE7_xBB_x91_xE5_xAE_x9A_xE6_xB5_x81_xE7_xA8_x8B_xE6_x88_x96_xE6_xA8_xA1a402d64484deba2f1921afb46f0d4f2e.html','ModuleSource','通过绑定流程或模块取渲染结果','官方推荐控件绑定流程显示多个模块的配置渲染；单控件同一时刻绑定一个流程。']
];
const ownDefs=[
['sdk-request','src/Plugin.SDK/Core/ToolExecutionRequest.cs','public sealed class ToolExecutionRequest'],
['sdk-reader','src/Plugin.SDK/Core/IToolInputReader.cs','public interface IToolInputReader'],
['sdk-image-contract','src/Plugin.SDK/Vision/Contracts/IVisionImage.cs','public interface IVisionImage'],
['sdk-parameters','src/Plugin.SDK/Execution/Parameters/ToolParameters.cs','public abstract class ToolParameters'],
['sdk-binding','src/Plugin.SDK/Execution/Parameters/ParamSetting.cs','public class ParamSetting']
];
const additionalSources=[...docDefs.map(([id,p,token])=>[id,root+p,token]),...ownDefs];
const sources=additionalSources.map(([id,path,token])=>{const bytes=fs.readFileSync(path),text=id.startsWith('sdk-doc-')?new TextDecoder('gb18030').decode(bytes):bytes.toString('utf8');if(!text.includes(token))throw Error('sdk source '+id);return{id,path,token,line:text.slice(0,text.indexOf(token)).split('\n').length,sha256:crypto.createHash('sha256').update(bytes).digest('hex'),kind:id.startsWith('sdk-doc-')?'官方V4.4.2开发指南（静态解包）':'自研当前源码'};});
const packages=[
['共享合同（基础包，不是第4条产品线）','SunEyeVision.Contracts（拟议）','稳定ID、Field/Flow schema、像素格式/几何、结果质量、资源句柄与错误DTO；仅系统基础类型。','无WPF/宿主/算法引擎引用；建议net9.0，旧.NET Framework兼容不自动承诺。','当前SDK公共类型逐个审依赖；不能按文件夹整搬。System.Windows.Point/Rect等出现在公共签名时转自有值类型或边界适配。'],
['运行时SDK（客户应用）','SunEyeVision.Runtime.SDK（拟议）','RuntimeHost/ProjectSession/FlowHandle、加载保存、数据schema、运行/停止、结果句柄与订阅。','只依赖Contracts；公开表面不暴露MainWindow、WorkflowEngine或ToolResults派生类。首期net9.0-windows x64，UseWPF=false。','门面新增；适配现有SolutionRepository/Manager、WorkflowExecutionEngine与设备服务，不复制执行器。'],
['工具插件SDK（算法作者）','SunEyeVision.Tool.SDK（拟议）','IToolPlugin/IAsyncToolPlugin、参数/结果声明、只读运行输入、执行profile与能力服务接口。','依赖Contracts；不得反依赖Runtime.SDK、UI宿主或WPF控件库。纯合同可net9.0，算法插件平台能力另声明。','保留已有强类型工具与ToolExecutionRequest/Inputs；把ToolExecutor装配、解析及缓存实现放内部运行时。测试入口通过独立开发测试宿主复用同一执行管线；测试宿主可引用Runtime.SDK，不是Tool.SDK运行时依赖。'],
['UI控件库（嵌入界面）','SunEyeVision.Controls.Wpf（拟议，可分Viewer/Editors组件）','结果树/图像Viewer/ROI、可选参数和流程编辑器；只通过公共会话及只读投影工作。','net9.0-windows + WPF；Viewer依赖Contracts结果源，编辑器可依赖Runtime.SDK/Tool.SDK。WPF编辑器工厂归可选UI扩展。','现有IToolEditorFactory独立于IToolPlugin接口的思路保留，但从运行合同程序集分离；控件不得拥有全局设备连接。'],
['运行实现（内部发布依赖）','Runtime.Engine / Plugin.Infrastructure / DeviceDriver（职责名，非已建工程）','装配旧执行层、插件发现、参数解析、资源协调、设备/许可服务；由运行SDK发行包带入。','依赖Contracts和Tool.SDK；通过注入实现SDK内部provider接口，避免SDK门面与Engine双向ProjectReference。','同进程先复用；native依赖按RID/版本清单发布，公共类型由宿主唯一加载。'],
['迁移兼容层（过渡）','现有SunEyeVision.Plugin.SDK（保留兼容，非新的纯合同）','旧类型/资源URI/插件ABI的兼容与适配；旧UI资源单独标记。','旧插件按原依赖运行；新纯API样例不得靠装入旧WPF兼容层偷偷满足依赖。','TypeForward仅用于全名和结构兼容的类型；改签名/改命名空间须显式适配及序列化迁移，不能万能转发。']
];
const operations=[
['建立宿主','RuntimeHost.Create(options) → Capabilities','配置插件目录、日志、native搜索和自有许可provider；Create只校验配置/建立对象图，耗时初始化在Open等异步阶段完成。不弹自研窗口、不默认抢占设备。','Runtime.SDK提供，内部composition实现；一进程首期一个运行宿主。','VM官方API/控件依赖分开；自研engine构造目前需内部对象。','doc-packages engine sdk'],
['加载方案','host.OpenProjectAsync(path, options, ct)','绝对路径/版本/插件/权限/资源先检查候选；失败不发布半就绪项目，候选资源退役。加载期间多数操作拒绝ProjectNotReady。Open选项默认RestoreDevices=None，客户显式启用设备恢复才连接。','ProjectSession唯一Owner；首期MaxProjects=1显式能力，不把静态注册表包装成已支持多项目。','VM Load、加载事件与方案操作；内部实现不能只从API文档推断。','sdk-doc-solution doc-events'],
['定位流程与schema','project.Flows.Resolve(idOrName); flow.GetSchema()','名称只用于初次解析；后续用稳定FlowId/FieldId及schema版本；缺少/重名/失配返回诊断。','外部应用面向流程的公开输入输出，模块路径只保留专家接口。','VM推荐流程输出降低模块耦合。','sdk-doc-output sdk-binding'],
['修改持久参数','BeginEditAsync(expectedRevision) → CommitAsync','按项目修订号提交候选，校验全部绑定/类型/设备前置；默认运行中拒绝结构性编辑，成功只改设计态，保存另行调用。','Runtime.SDK负责事务；不会让客户直接修改活跃ToolParameters对象。','VM ModuParams有配置和临时输入；自研需明确分合同。','sdk-doc-module sdk-parameters'],
['更新全局变量','project.Globals.UpdateAsync(expectedRevision, values)','更新全局设计值/修订，下一根运行冻结；当前Run及子Scope继续读旧快照。即时控制指令另走设备API，不伪装成变量。','所有值校验类型与可变资源；不得提供未定义写回的共享可变字典。','VM有全局模块入口；原SDK未据此证明自研所需快照语义。','sdk-doc-globals sdk-reader'],
['准备当次输入','flow.CreateRequest() → Inputs / ParameterOverrides','只对本次Run有效，输入/ROI/覆盖参数不写入方案；数组/图像按明确copy/retain政策冻结，缺少必填输入拒绝。','输入Owner在接纳前归调用者；Start接纳成功后运行持有必要lease。','VM示例明确临时输入当次有效。','sdk-doc-procedure sdk-doc-module sdk-request'],
['开始执行','flow.StartAsync(request, admissionToken) → RunHandle','返回仅证明接纳，不证明执行结束；schema/定义/全局修订及身份冻结，忙则Busy，未获资源前可取消接纳。','admissionToken只控制接纳；Run返回后取消运行用StopAsync，不把取消一次等待当停机。','VM Run有bIsWait/执行计数；自研对外优先异步显式handle。','doc-flow session'],
['等待运行','run.Completion → RunOutcome','包含约定的执行、在途观察和本轮收尾；Completed/Failed/Cancelled/Quarantined分开。等待者取消只结束等待。','run.Completion.WaitAsync(waitToken)不自动停止硬件；业务取消后仍须StopAsync并观察结局。','VM事件与等待参数不能代替自研终态保证。','doc-flow doc-events engine'],
['读取结果','run.AcquireResultAsync() → ResultLease','按Run/Scope/Frame和schema取得本轮只读快照；Read<T>/TryRead检查Quality及形状，失败不能回退旧成功。','结果lease独立于显示；不让客户读取CurrentContext最后值猜轮次。','VM每轮重新取得ModuResult，推荐流程输出。','sdk-doc-output sdk-doc-procedure result'],
['订阅结果','project.SubscribeResults(filter, handler, options) → Subscription','通知只带结果地址/版本/状态，不自动转移像素Owner；按Run/Flow/Scope过滤，业务记录与UIlatest-only分通道。','回调不在执行/全局锁内运行；异常记录并隔离。Subscribe状态不决定运行是否保存结果。','VM文档警示回调中耗时渲染阻塞下次运行；自研应隔离该负担。','doc-events sdk-doc-events vm-queue'],
['停止单次/连续','run.StopAsync(deadline)；controller.StopAsync(deadline)','请求停止新派发，取消并等待在途；连续控制器停止覆盖其所有iteration。超时返回隔离而非成功完成。','不支持取消的native调用不得强制释放正在使用的资源；状态保留故障及恢复条件。','VMContinuousRunEnable=false是官方调用方式，内部排空仍需逆向证据。','sdk-doc-solution sdk-doc-procedure engine feed'],
['保存方案','project.SaveAsync(expectedRevision, path?, ct)','只保存已提交设计态，临时输入、当前结果、设备句柄不入文件。文件成功而索引失败分阶段反馈；另存资源复制政策明确。','仅Ready/允许保存状态；未决编辑或过期revision拒绝，native副作用不当文件事务一部分。','VM Save/SaveAs与配置操作分开。','sdk-doc-solution'],
['关闭项目/宿主','project.CloseAsync(deadline) → CloseOutcome；DisposeAsync','停准入→停连续/单次→等在途和回调→退订→等外部结果lease→释放项目实例；最后host释放应用设备/许可。','重复调用共享同一退役任务；未排空保留隔离，Close/Dispose失败可观察，不悄悄回到Idle。','VM例程CloseSolution/Dispose及退出前释放，避免析构函数调用。','sdk-doc-solution doc-wpf feed']
];
const dataRules=[
['公开流程输入/输出','FieldId + 名称别名 + ValueKind/Shape/Unit/CoordinateDomain + Required + schemaRevision','默认客户只依赖流程公开schema，内部算法替换时保持schema即可；诊断/专家模块入口明确不稳定性。','借鉴VM流程输出推荐，保留自研ParamSetting已有SourceFieldId，不能称完全无字段ID。'],
['持久参数','项目编辑事务提交并可保存；直接关系到方案修订','编辑草稿不能被运行读取；执行参数覆盖只改当次请求，不修改持久化ToolParameters。','保留ToolParameters及clone/metadata基础，切分UI通知与运行值。'],
['当次输入','每次Run单独请求，连续每轮由InputProvider提供或明确重复固定快照','缺输入拒绝；不能沿用上一轮临时输入；坐标/图像格式与字节长度、stride、深度和色序校验。','借鉴VM临时输入约定；自研给copy/retain明确选择，不暴露无寿命裸指针。'],
['全局变量','全局修订+根运行快照；子调用继承同一版本','写入影响下一根Run，局部遮蔽明示；设备立即控制不走Globals.Update，结果回写须独立事务。','自研新增保证，VM全局入口存在不等其快照政策相同。'],
['结果','ResultAddress含Run/Scope/Node/Iteration/Frame/schema；Quality区分Valid/Missing/Failed/Cancelled/Stale','Read<T>不默认强制转换，数组不能自动截断/广播；TryRead返回结构化原因。结果只读，无WriteOutput绕过算法。','VM typed读取可借鉴；不复制名哈希作为持久ID或缺失数据默认化。'],
['资源','图像/模型等返回显式lease，不把只读接口当不可变像素','AcquireImage在父结果有效时获得独立子lease；子lease可按合同比父结果活得更久，但必须释放后项目才能完全关闭。','现有IVisionImage只有Clone/Dispose，尚不等retain/refcount实现；原合同需适配。']
];
const life=[
['RuntimeHost','客户应用','创建composition与许可/设备服务','所有ProjectSession/在途/外部资源退役后DisposeAsync；不强停别的宿主（首期不支持多宿主）。'],
['ProjectSession','打开项目的客户或应用服务','持方案、插件实例及资源索引','关闭只退役自己，不因控件关窗关闭共享项目；失败隔离并报告剩余Owner。'],
['输入ImageLease','创建输入的调用者；接纳后Run另持使用lease','默认CopyFromPixels给自有缓冲；Retain只接受可证支持的载体','调用者可在Start成功后释放自己持有，Run仍持有效使用权；拒绝接纳则无转移。'],
['RunHandle','发起Run的代码','持本轮请求和在途观察','DisposeAsync等同按政策停止/等待并释放handle；超时抛或返回失败，不能伪装资源已归还。'],
['ResultLease / ImageLease','每个获取者','外部读取不得无lease抓裸像素跨线程','先释放子图像再父结果；支持独立retain时按各自使用权释放；Close发现未归还须定位。'],
['Subscription','订阅方','拥有handler及过滤/队列配额','Dispose停止新投递；DrainAsync等待已投递handler；不在自身回调内同步Drain自身，避免死锁。'],
['Viewer / EditorSession','嵌入控件的客户UI','控件只拥有自己的订阅/显示lease/编辑草稿','DetachAsync等显示在途退出后归还；清屏不等销毁项目或输入图像，Dispose在正确Dispatcher。']
];
const failures=[
['加载中取消或缺插件','候选退役，当前项目不替换，返回缺少工具/版本/资源清单；不发布Ready。'],
['schema或定义修订过期','拒绝接纳或提交；客户重新GetSchema，不按旧名称猜绑定。'],
['同项目并发Start','首期MaxConcurrentRoots=1，明确Busy；能力接口保留未来扩展，不冒称已支持多租户。'],
['等待者取消','只取消WaitAsync等待；调用者若要停运行必须StopAsync并观察Completion。'],
['工具失败或结果缺失','Outcome失败+字段Quality，输出不冒充上次成功；读失败结构化返回。'],
['结果handler慢/抛错','不影响调度终态；队列超容量按通道拒绝/丢弃并记录计数，ACK和业务记录不可无声合并。'],
['停止超时/外部lease未还','Quarantined/ClosePending，保留资源占用及Owner诊断；不能继续重连或卸载native。'],
['许可失效/拔自有狗','各入口统一准入，自有策略决定拒绝新Run并安全退役；狗型号/宽限未定，不能承诺发行。'],
['主异常与收尾异常并发','保留主异常，单独聚合清理错误；示例中的await using不代表Dispose失败可以覆盖原错误而丢失诊断。']
];
const migration=[
['S0（F0）','冻结公共表面与依赖账本','把ToolParameters/ToolResults/ParamSetting/Image合同的跨包类型、JSON判别符、URI、插件ABI逐个登记；冻结Flow/Field稳定身份。','架构评审；不只是画3个新csproj。'],
['S1（F0/F1）','新增门面和独立调用样例','Open→GetSchema→Run→读结果→Stop→Close全部经同一现有执行器适配，默认单Host/项目/根Run能力明确。','Console不创建MainWindow/ServiceInitializer；旧引擎过渡实现可能仍需WindowsDesktop，这步不能冒称最终无WPF部署。native/许可/设备依赖显式。'],
['S2（F2）','提取Contracts与工具合同','先迁纯值/接口，再拆WPF签名、资源及EditorFactory；兼容旧包，原全名类型可转发，其余适配。','新工具仅依赖Tool.SDK+Contracts可构建与调用；旧插件/方案不失配。'],
['S3（F1-F3）','接入快照、结果lease与订阅','补输入冻结、Quality、批次、外部lease、订阅排空；从图像源/阈值/子程序2调用做贯穿样例。','失败/取消/迟到/未还lease不误完成；UI和SDK看到同版结果。'],
['S4（F4）','分发可选Viewer/Editor与开发工具','Viewer先面向流程结果；Editor草稿事务。工具调试/预编译/测试走同一执行管线，不另造Run。','独立WPF壳可嵌入，关控件不关客户项目；设计时和运行时分别验。'],
['S5（F4/F5）','发布与兼容验证','发行清单含managed/native/RID、SDK与工具合同版本、自有许可服务、资源模板、文档/样例。','干净机、旧方案/插件、非UI运行、慢消费者/长时循环、正常异常退出；本轮不构建运行。']
];
const acceptance=[
['A1','包引用方向','新Runtime.SDK不引WPF/宿主；Tool.SDK不反依赖Runtime或UI；UI可选；公开合同无System.Windows类型；最终纯API运行实现和新插件的传递依赖也不得隐含WPF。'],
['A2','纯API应用','新Console样例不创建主窗，完成项目加载、schema查询和一次运行；最终纯API发布须核传递依赖无WPF/Editor，兼容旧引擎期间仍需WindowsDesktop时单列状态。缺native/许可错误可诊断。'],
['A3','数据与修改','持久参数提交/保存与当次输入分开；Globals更新不改变当前Run；旧schema明确失败。'],
['A4','流程输出稳定','替换内部工具但保持公开Flow schema，客户程序不改模块路径；改字段则走版本迁移。'],
['A5','取消及终态','接纳取消/等待取消/请求停止各自可测，停止包含子Scope和在途；超时保留隔离。'],
['A6','输入及结果寿命','调用者释放输入后运行仍正确；多消费者各持lease；未还lease关闭有诊断，最后使用后恰好释放。'],
['A7','事件与UI','慢/异常handler不破坏调度；订阅取消排空可等待；两个Viewer、切流程、关控件不释放客户项目。'],
['A8','工具插件','同步旧工具经适配、新异步工具用只读Inputs；调试与外部运行参数/结果同语义，部分Init失败有清理合同。'],
['A9','兼容与发布','旧插件/JSON/资源URI不静默坏；同名不同版本拒载；共享与私有native正确部署。'],
['A10','可重复退出','加载失败、运行异常、handler失败、Stop超时和重复Close/Dispose均有唯一可观察结局。']
];
const code=`// 设计示例：以下RuntimeHost等API均未实现，非可直接编译的产品代码。
await using var host = RuntimeHost.Create(new HostOptions {
    PluginDirectories = new[] { pluginRoot }, LicenseProvider = ownLicenseProvider
}); // 不启动自研主窗；不默认连接全部设备
await using var project = await host.OpenProjectAsync(solutionPath, openOptions, ct);
var flow = project.Flows.Resolve(flowId); // 名称可先解析一次，保存稳定ID
var schema = flow.GetSchema();
using var subscription = project.SubscribeResults(
    new ResultFilter(flow.Id), n => uiQueue.Post(n.Address), LatestOnly);
using var input = host.Images.CopyFromPixels(bytes, width, height, stride, pixelFormat);
var request = flow.CreateRequest(schema.Revision)
    .WithInput(inputFieldId, input)
    .WithParameterOverride(thresholdFieldId, 128); // 只对当次Run有效，不写方案
await using var run = await flow.StartAsync(request, admissionToken);
// Start返回仅代表接纳；admissionToken不再用作运行停止信号。
var outcome = await run.Completion; // 等约定终态，不只等“命令已发送”
if (outcome.State != Completed) { ShowFailure(outcome); return; }
using var result = await run.AcquireResultAsync();
double score = result.Read<double>(scoreOutputId); // 检查Quality/type/schema
using var image = result.AcquireImage(imageOutputId); // 显式图像使用权
var pixelsForLater = image.CopyPixels(); // 需要脱离lease长期保存时复制
// 反向释放 image/result/run/input/subscription/project/host。
// 生产应用另用关闭协调器保留主异常并聚合收尾异常，参见故障与寿命合同。`;
const editCode=`// 拟议：持久修改；同“当次参数覆盖”分开。
await using var edit = await project.BeginEditAsync(expectedRevision, ct);
edit.SetParameter(nodeId, parameterFieldId, newValue);
var commit = await edit.CommitAsync(ct); // 校验并采用候选，默认运行中拒绝结构修改
await project.SaveAsync(commit.Revision, destinationPath, ct);
// 拟议：取消等待不自动停运行。
try { await run.Completion.WaitAsync(waitToken); }
catch (OperationCanceledException) {
    var stop = await run.StopAsync(stopDeadline); // 独立控制操作
    if (!stop.Drained) ShowQuarantine(stop); // 不继续释放被native使用的资源
}`;
const detail={sources,officialTopics:docDefs.map(([id,,token,title,fact])=>({id,token,title,fact})),packages,operations,dataRules,life,failures,migration,acceptance,code,editCode,productModified:false,implementationStatus:'全部新增包、门面、示例及合同为待实施设计；现有能力按来源列出',checkpointUpgrades:0};
fs.writeFileSync('artifacts/validation/sdk-development-design.json',JSON.stringify(detail,null,2));
function responsiveTables(html){return html.replace(/<table>([\s\S]*?)<\/table>/g,(_,body)=>{const headers=[...body.matchAll(/<th>([\s\S]*?)<\/th>/g)].map(m=>m[1].replace(/<[^>]+>/g,''));return '<table>'+body.replace(/<tr>([\s\S]*?)<\/tr>/g,(row,content)=>{let i=0;return '<tr>'+content.replace(/<td>/g,()=>'<td data-label="'+(headers[i++]||'内容').replaceAll('"','&quot;')+'">')+'</tr>';})+'</table>';});}
function render({E,table,flow,card,sdkPoint,official}){
const ref=ids=>ids.split(' ').map(id=>'<a href="#source-'+id+'">'+E(id)+'</a>').join(' · ');
let apiTable=table(['操作','拟议API（未实现）','语义/失败边界','责任与限制','VM对照与现状','证据'],operations);for(const row of operations)apiTable=apiTable.replace('<td>'+E(row[5])+'</td>','<td>'+ref(row[5])+'</td>');
const panels=[
['sdk-overview','结论与官方对照','<h2>二开具体设计：三类SDK，统一公共数据与寿命合同</h2><p class="notice">以下新包、RuntimeHost/ProjectSession和API均为拟议，尚未开发；不改产品。当前工具插件接口与公开执行器存在，但不能等同完整应用SDK。</p>'+card(sdkPoint)+table(['VM官方可借鉴的设计','自研目标','直接影响'],[['纯API与WPF/WinForm组件分开','Runtime.SDK、Tool.SDK、Controls.Wpf三条消费线，共用Contracts','客户Console不必带自研主窗或控件包'],['流程输出优先于模块输出','流程级稳定Field schema为默认外部合同','内部工具换实现不强迫客户改模块路径'],['当次输入与每轮重新取结果','单次RunRequest＋按Run的ResultLease','不会默用上一轮输入或把CurrentContext当历史库'],['公开事件及退出前释放','订阅/排空与CloseAsync可等待合同','回调负担与资源Owner公开，不让外部客户猜Dispose']])+official.html],
['sdk-packages','三类SDK怎么拆','<h2>拆包：先按消费边界，再迁具体类型</h2><p>三条产品线之外需要共享合同和内部实现。包名是建议命名，工程尚未创建；不能用移动文件夹或“去掉UseWPF”代替依赖审计。</p>'+table(['层次','建议交付包（未实现）','对外职责','依赖/平台边界','现有代码如何处理'],packages)+flow('目标依赖方向（箭头表示依赖，不是执行调用）',['客户Console → Runtime.SDK → Contracts','算法工具 → Tool.SDK → Contracts','Viewer → Contracts；Editor → Runtime.SDK / Tool.SDK + 可选WPF扩展','Runtime内部实现 → Tool.SDK / Contracts + 现有Workflow/Infrastructure/设备适配','UI宿主组合三条产品线；Contracts不反依赖任何一条'])+'<p><b>默认实现怎么接入：</b>后端Factory/Session最小接口放Contracts的运行平台子域；Runtime.SDK从配置或发行清单取得Factory，默认发行包带内部实现。Engine只实现该合同，不反向依赖Runtime.SDK门面；公开门面也不静态引用Engine，避免形成循环ProjectReference。后端配置是宿主装配，不暴露给算法工具自行替换。</p><p><b>不能原样搬的类型：</b>ToolResults含System.Windows引用，参数/结果的每个公共成员都要追踪类型；ObservableObject本身是通知模型不等WPF，按真实引用判断。ParamSetting已有SourceFieldId要保留；ToolExecutionRequest/Inputs已有运行数据分离，可作为适配起点。来源：'+ref('sdk workflow-package result sdk-parameters sdk-binding sdk-request sdk-reader editor-contract')+'</p><p><b>可选UI编辑器：</b>运行插件只发布工具和schema；另一个Editor程序集发布IToolEditorFactory及资源，manifest按ToolId关联。参数DTO由同一个Tool.SDK类型定义，避免编辑器复制DTO导致类型身份分裂。最终纯API部署不安装Editor。</p><p class="notice"><b>过渡与最终状态分开：</b>无主窗不等于无需WPF运行时。S1适配旧Workflow/Plugin.SDK时，其WindowsDesktop/WPF依赖仍可能保留；Capabilities/发布说明必须标兼容模式。只有S2-S4迁掉传递UI依赖，并用新工具完成A1/A2，才能标纯API发布已达标。旧WPF插件支持走单列兼容发行，不强迫所有纯API客户安装它。</p>'],
['sdk-external','外部程序完整调用','<h2>从加载到退出：每个API必须说明返回意味着什么</h2><p>首期建议.NET 9 Windows x64同进程，公开Capabilities明确MaxHosts/MaxProjects/MaxConcurrentRoots初期限制，不声称多租户、跨平台或.NET Framework兼容。应用级数据接口保持可扩展，现有静态注册表隔离未解决前拒绝第二项目。</p>'+apiTable+'<h3>正常单次的外部程序示例</h3><pre><code>'+E(code)+'</code></pre><h3>修改方案与取消等待的独立示例</h3><pre><code>'+E(editCode)+'</code></pre><p><b>连续运行：</b>StartContinuousAsync返回Controller；每轮具有独立Iteration/Frame身份和输入请求。InputProvider每轮提供数据，重复固定图像必须显式选择；StopAsync停止新轮并等待所有在途。结果通知不可代替controller.Completion；事件未订阅也要有运行结局与可查询结果。</p><p><b>避免完成与取结果循环等待：</b>Completion等待执行任务、回调和本轮临时资源收尾，不等待未来才会获取的ResultLease。RunHandle活着时固定本轮结果版本；handle释放后禁止新Acquire，已取得的ResultLease继续持有自身资源引用。最终像素释放发生在结果Owner及所有使用权退出之后，不因Completion立即销毁。</p>'],
['sdk-data','读写数据合同','<h2>读写数据：配置、输入、全局和结果分四种合同</h2>'+table(['数据类别','公开结构/生效范围','约束','对应取舍'],dataRules)+'<p>稳定字段ID不是把所有业务都变成无类型object。工具内部保持强类型；外部读取用Field schema与Read&lt;T&gt;/TryRead，返回类型、形状、质量、版本错误。输入位置、输出点集、图像都带坐标域/单位，不按double[]长度猜类型。流程GetSchema同时列公开Inputs、Outputs及可覆盖的Parameters；WithParameterOverride只接受已公开参数FieldId，不能任意写内部工具字段。BeginEdit.SetParameter(nodeId,...)属于显式专家编辑接口，客户选择它就承担内部结构版本耦合。</p><p><b>别名与兼容：</b>旧名称/属性路径由迁移适配映射到FieldId，找不到或歧义必须报错；只改显示名不变ID。定义修订和运行结果版本不同，GetLatest仅是显式便利接口，不能代替指定Run读取。</p><p>官方依据：'+ref('sdk-doc-output sdk-doc-module sdk-doc-procedure doc-input')+'；自研现状：'+ref('sdk-binding sdk-request sdk-reader sdk-image-contract')+'</p>'],
['sdk-events','事件及错误','<h2>事件、并发与失败：不把内部回调责任推给客户</h2><p>订阅句柄持有过滤器、有限队列及handler。实时UI允许LatestOnly，业务记录/ACK按有界可靠投递或拒绝准入；缓冲满、过期、失败和丢弃都有计数。通知只给地址/状态，读取像素必须另获取lease。handler异常不改变已提交的RunOutcome。通知地址不承诺无限保留结果：单次由RunHandle固定版本；连续按有界保留窗口，过期返回ResultExpired。可靠记录订阅可由分发队列内部保留至交付，handler需另Acquire才能跨回调持有；UI只转发地址时必须处理过期并跳过旧帧。</p>'+table(['故障/竞争场景','拟议处理与诊断'],failures)+'<p><b>错误模型：</b>SdkError包含Code、Operation、Project/Run/Scope/Field地址、可读信息、原始内部异常链和阶段。参数/版本/缺依赖是接纳失败；工具业务失败是RunOutcome；关闭失败是CloseOutcome，不统一吞成false。</p><p><b>回调线程：</b>默认SDK分发worker，不保证UI线程；UI控件自行Dispatcher投影。禁止客户在回调内同步等待自身Drain/Stop造成互锁，提供异步退出路径及重入错误诊断。官方同样提示避免同步渲染阻塞，见'+ref('doc-events sdk-doc-events')+'。</p>'],
['sdk-resources','释放与所有权','<h2>资源释放：拥有者与使用者都写进SDK合同</h2>'+table(['资源','Owner','持有什么','何时退役/释放'],life)+flow('项目关闭时序（目标合同，尚未实施）',['拒绝新编辑、Start和新订阅','停连续派发 → 请求取消单次/子Scope','等全部工具/native/回调结局；超时隔离','停止新事件投递 → 等handler和控件在途','定位仍存活Result/Image lease；未归还则ClosePending','所有使用者退出后释放工具、结果和设备用途','关闭项目；最后宿主释放应用连接和自有许可服务'])+'<p><b>为什么不能只写await using：</b>语言自动调用DisposeAsync并不证明native可中断。Close未排空时应保留隔离/诊断，明确重试和宿主保留策略；不能清注册表后报“释放成功”。终结器不是设备和异步会话清理入口。Result/Image lease的同步Dispose仅归还使用权，不能凭它断言native异步销毁完成；资源Owner跟踪最终退役任务，由Close/Drain等待，防止把重清理塞进UI线程。</p><p>官方示例退出前Dispose、避免析构函数调用，是使用规范；原生最后读者与回调排空未因文档存在而闭合。自研IVisionImage目前只有Clone/Dispose，不能把拟议Retain实现当现有。来源：'+ref('sdk-doc-solution doc-wpf sdk-image-contract feed vm-register')+'</p>'],
['sdk-tools','工具作者怎么开发','<h2>工具插件SDK：保留强类型基础，收紧运行与UI边界</h2>'+table(['工具作者需要什么','保留/调整','合同'],[['ToolId、参数/结果类型与schema','保留[Tool]、IToolPlugin泛型及输出声明','manifest记录合同版本、RID、依赖及能力；ToolId不同于显示名称'],['异步执行和输入','保留IAsyncToolPlugin/ToolExecutionRequest.Inputs','输入是本轮快照/借用视图，不持久保存外部Context或图像；不绕过宿主直接控制共享相机'],['并发和状态','默认PerExecution，允许显式PerSession但需资源/并发profile','实例状态属于谁、能否重入、停止是否支持必须声明；Clear缓存不等在用实例已退役'],['输出与图像','强类型ToolResults经公共字段适配，明确OutputOwned/Borrowed/Retained','移交Owner在框架接纳输出时确认，失败候选由创建方清理；不双方都Dispose同一图像'],['清理与异常','定义Init/Execute/Release在部分失败时义务','主异常保留、清理异常汇总；未知native强制Dispose不是取消机制'],['调试与测试','开发测试宿主适配同一ToolExecutor与参数准备','不能为了Console样例直接跳过绑定、许可和资源门禁；UI调试不另写算法执行逻辑'],['编辑器与分析控件','可选Editor程序集实现WPF工厂','运行SDK不加载编辑器；关闭编辑器只退役自己的会话，不卸载仍在执行的工具']])+'<p>自研已有这些接口的基础，不整体推倒重做。需调整的主要是同程序集WPF耦合、实例/输出Owner和客户可消费的稳定边界。源码：'+ref('tool-contract async-contract editor-contract sdk-request sdk-reader result sdk')+'</p>'],
['sdk-ui','客户如何嵌入UI','<h2>UI控件库：可组合，不接管客户程序</h2>'+table(['组件','绑定对象与职责','不能做的事情/寿命规则'],[['ImageViewer / ResultTree','绑定IResultSource+FlowId/Run选择，默认流程公开显示合同','不访问MainWindowViewModel/静态全局Context；不自行关闭ProjectSession'],['ParameterEditor','绑定编辑事务+工具schema，草稿→校验→Commit/Cancel','不把活跃参数对象直接给UI改；取消只丢草稿，模型训练/文件副作用需候选事务'],['ProcedureEditor','可选绑定公开方案编辑接口/快照','运行入口仍走Runtime.SDK，不在控件中调度；无此组件也能纯API运行'],['专业ROI/图形文字','几何/坐标域/Frame与样式分离；输入结果只读','坐标转换、DPI与帧失配拒收；保存渲染图不是保存原始像素'],['生命周期','Attach → 订阅 → Dispatcher采用 → DetachAsync/Dispose','迟到结果拒收；多Viewer各自lease，关闭一个不破坏另一个；切源归还旧lease']])+'<p>VM官方有WPF/WinForm及参数/流程/渲染等独立控件，并推荐绑定流程聚合显示。自研首期优先WPF，WinForms可通过单独宿主适配按需求增加，不自动承诺Qt或C++控件；C ABI/RPC属于未来独立边界。</p><p><b>兼容限制：</b>本版官方VM WPF文档要求.NET Framework≥4.6.1且不支持.NET Core；自研继续.NET 9，借鉴职责不直接迁VM控件二进制。来源：'+ref('doc-packages doc-wpf doc-render sdk-doc-view editor-contract')+'</p>'],
['sdk-migration','迁移、发布与验收','<h2>迁移和发布：每一步都有可验证出口</h2>'+table(['步骤','阶段交付','改法（未实施）','验收出口'],migration)+'<h3>SDK发行物</h3><p>Runtime发行包带门面和内部实现、RID/native清单及自有许可provider；Tool.SDK开发包带合同、模板、schema规范与测试宿主；UI包带样式/资源与可选Editor。共享Contracts同一版本/类型身份，私有依赖隔离需遵守加载策略。客户文档给出独立Console、WPF嵌入、工具插件三个样例及能力/错误/释放规范。</p><p>自有狗必须是自有体系；签发、设备能力与方案文件保密分别设计。默认不复制VM目录中的DLL/驱动/狗协议。旧Plugin.SDK兼容支持期限、包版本、JSON判别符、资源URI及回滚版本逐项登记。</p>'+table(['验收项','范围','通过条件（本轮均未运行）'],acceptance)+'<p>这些是本专项的设计验收清单，不自动增加旧41整改或原42证据。VM文档说明与内部实现、产品可用和性能验证仍分别标记。</p>']
];
panels.push(...require("./sdk-engineering-migration.cjs")().render({E,table}));
detail.panelIds=panels.map(p=>p[0]);fs.writeFileSync('artifacts/validation/sdk-development-design.json',JSON.stringify(detail,null,2));
const html= '<h2>二次开发：从SDK拆分到外部程序完整调用</h2><p class="notice">三条SDK消费线及以下所有新包/API是待实施设计。保留现有工具接口与参数/结果基础，借鉴VM的应用集成边界；官方文档版本V4.4.2，未把文档当原生实现闭合。</p><nav class="sdk-subnav" aria-label="二开设计子专题">'+panels.map(([id,title])=>'<a href="#'+id+'" data-sdk-link="'+id+'">'+title+'</a>').join('')+'</nav>'+panels.map(([id,title,body],i)=>'<section class="sdk-panel" id="'+id+'" '+(i?'hidden':'')+'><div class="chapter-label">二开 / '+title+'</div>'+body+'</section>').join('');
return responsiveTables(html);
}
return{additionalSources,sources,render};
};
