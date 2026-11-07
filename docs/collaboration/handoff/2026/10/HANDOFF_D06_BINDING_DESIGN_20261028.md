> 当前实施状态：2026-10-28已获用户完整批授权，全部生产消费者候选已集成，未构建运行。原只读设计与证据保留如下；当前实现、限制和正式验证入口以HANDOFF_D06_ATOMIC_IMPLEMENTATION_20261028.md为准。

# D06 结果变量绑定整体设计交接（2026-10-28）

## 状态、基线及范围

本单仅为只读源码取证与实现设计，不代表产品实施、构建或人工通过。独立核验目录 workflow-review，分支 codex/image-source-audit，文档基线 e81a6e84；现行产品差异另以 root 87f4 的 158b55c6 及在途结果接线源码只读核对。root 统一原主方案和 CURRENT_TASK，本路仅交本文件。

**明确撤销先前“只实现结果根 PortId 字符串绑定即可”的建议。** 不能因当前新 Presenter 子行未生成 BindingInfo，就缩减原件具有的子字段/组合绑定。推荐复用现有 OutputReference + BindingReferenceSerializer + ParameterBindingService，所有生产者消费者同批迁移，ResultName 不再保留另一套点分寻址解释权。Group 不动。

## 原件范围证据（复用本地源码，未重新反编译）

原件根目录 `D:/MyWork/REA逆向测试/analysis/decompiled/`。

|文件及行|直接事实|结论与限制|
|---|---|---|
|VMControls.WPF/VMControls.WPF/ModuleResultView.xaml:234–236|VmModule.Outputs 与 VmIO.Children 的递归 HierarchicalDataTemplate|树并非只显示或操作输出根|
|VMControls.WPF/styles/moduleviewstyle.xaml:400–407、428|每个行对象作为 RelatedGlobalVarCommand/UnRelatedGlobalVarCommand 参数|子 IO 行具有相同绑定入口，不限根深度|
|VMControls.WPF/VMControls.WPF/TreeItemVisibilityConverter.cs:27–32|变量按钮按支持 TypeName 且 owner 非 Procedure 决定可见|不是“只有根可绑定”，但不是所有展示行都可绑定|
|VMControls.WPF/VMControls.WPF.DataForUI/ModuleDisplayDecorator.cs:1753–1776|停止状态、IVmIO、TypeName 筛目标变量，调用所选 IO 的 BindingGlobalVariable|绑定应依真实端点声明/类型，不依显示文字|
|Apps.XmlParser.CapabilityAudit/Apps.XmlParser.ParamTab/VmModuleExtern.cs:1747–1768|原生订阅参数取 subIO.Owner.ID 和 subIO.Name；索引固定 SUBSCRIBE_INDEX_ALL / All|已证子字段 IO/集合 All，未证单个 [n] 的全局变量绑定|
|同文件:1799–1814、1818–1831|叶直接绑定，组合按对应 Childs 递归成多条叶绑定|组合不是一段 FullName 反射；必须核目标可承载的组合结构|
|VM.Core/VM.Core/VmFrameTreeBase.cs:43–66；VmIO.cs:452|FullName 对 VmIO 使用 UniqueName（Name）后跳 Root，而非按 UI 树父层逐级拼成员|FullName 是原厂定位/脚本路径，不能推断为本方 record/member 点分表达式|
|ModuleDisplayDecorator.cs:1709–1717|FullName 写入 SelectedItemPath；ModuleResultView.xaml:240–242 显示脚本索引|显示/复制路径与生产绑定身份有区别|

本方旧 MainWindowViewModel 中 CreateShapeBindingInfo / CreateFieldBindingInfo（独立基线约 5207/5223）确实生成类型筛选/Geometry 子字段 BindingInfo，亦不能宣称历史产品只有根绑定。新 typed 路径不继续字符串 `[Type=...]` 解释；没有正式筛选声明/映射的旧专有几何端点明确拒绝并记依赖，不造猜测适配。

单 [n]：现有 OutputReference/ParameterBindingService 本来具备 AtConstant 能力，但原件本次全局绑定证据只有 All。保留索引端点追踪和需要确认项，本批不凭能力存在就自行开放；分页 More、状态、容器标题、运行样本展示行不能冒充声明端点。

## 方案比较与决策

|方案|职责及改动|问题|决策|
|---|---|---|---|
|A 仅完整 PortId 字符串|ResultName 原样保存 output.width 等；校验/读值先完整匹配|只覆盖独立 IO 根；成员、组合、集合投影仍无结构；日后再拼 . 或 [] 会复发歧义，且通信旧 OutputKey 继续第二解释链|撤销推荐，不作为 D06 完整交付|
|B 统一 typed OutputReference|ScopeId/NodeId/PortId/Type/Storage/MemberFieldPath/BindingSelection 唯一身份；序列化走现有严格 serializer；编译与解析走现有服务|必须成批调整 UI、生产者、历史复制、通信、依赖图及运行资源所有权|推荐整体实施，不造兼容 bridge|

OutputReference.Type 是原输出根 TypeRef；成员选择后的目标类型由 ParameterBindingService 编译投影得出，不能拿投影类型冒充原根类型。Storage 亦沿正式声明核对。ScopeId 必须是源流程实际作用域，不以当前 tab 补猜。组合 All 可从已声明 record/sequence 的 member path 投影；是否能绑定由目标变量的真实存储/类型合同决定，不能自动序列化 record 或 resource 到设计初值。

## 已有实现—差异—决策—验收出口

|链路/消费者|已有实现与差异|复用/调整决策|验收出口|
|---|---|---|---|
|RuntimeToolResults.Describe；MainWindow DeclaredBindings；ResultBindingInfo/FlatResultRow；ResultPresentationPresenter.GetEntry/UpdateRootMetadata|Runtime 具有完整 Port.Id/Type；旧 VariableType 对非 image 常为空，新树仅根有绑定元数据|从声明生成不可变 OutputReference；成员叶携完整成员 ID 路径，All 投影保形状；不从显示名/当轮数值猜类型|带点/连字符 PortId、record 叶、集合 All 可正确生成来源；无声明/不支持目标类型不显示可误用入口|
|ImageResultControl 变量选择；MainWindowResultBindingEditing|现 UI 创建 ProducerBinding，TryBind 强制 Guid；开始变量事务后 CommitBatch 已存在|保持正式变量事务，重验 owner/publication/当前行以及目标变量，按 typed reference 编译核准后接纳；Guid 不再作新 Runtime 门槛|接纳一次一历史；换节点/换流程/换发布/类型不符拒绝且无修改|
|GlobalVariable.ProducerBinding（约 50）、Capture/Clone（约 226/457）|SourceFieldId + ResultName；多处独立复制|生产者唯一来源存严格 typed reference 表达式（采用现 serializer，具体属性名由 root 定案）；名称字段只显示；全部 clone 同步深冻结，不保 ResultName 寻址回退|捕获后反改不影响快照；保存重开引用与声明一致|
|BindingConfigurationStore.Signature/Copy；NodeConfigurationEditHost.VariableSignature/CommitBatch；GraphBindingUndoSnapshot|已有 producer 纳签名、clone、历史、回滚|复用，核新唯一属性覆盖全部 clone/copy；取消/验证失败恢复 producer 及消费者关系，结构历史不丢变量绑定|Undo/Redo、结构 rebase、无关节点增删、拒绝接纳回滚|
|WorkflowFragmentCopy、节点删除及变量失效；SolutionExecutionDependencyGraph:118 起|依赖使用 producer WorkflowId/NodeId，删除由协调失效；当前来源身份分散|依赖图/复制删除按同一 reference scope/node；有现行复制重映射规则则只重写相关源身份，外部引用按明确规则保留或拒绝，不跨容器猜测|删除生产者使绑定明确失效；Undo 恢复；复制不指向错误新节点；跨流程依赖/成环拒绝|
|OutputBindingChangeService.Reconcile:204 与 Capture:402|Split('.')、按旧 Name/Guid 找字段；BindingContractValidator 仅 image PortId 特判|删除该生产者旧解释，统一 Deserialize + 正式 source schema + ParameterBindingService.CompileBinding；改显示名不换身份，删除成员/类型改变标明重新绑定|显示重命名保留；根/成员删除、Storage/类型改变失效；不得自动改绑同名其他字段|
|WorkflowExecutionManager.PreparePublishedRun/全局定义冻结、ApplyProducedGlobalValues:1186–1210|正式配置整体冻结可复用；当前 ResultName 经 OutputVariableResolver 解析，甚至未传 SourceFieldId|冻结 producer reference 与目标变量声明/编译计划，结果由原 admitted publication/reader 构造真实 run snapshot，使用 ParameterBindingService；不再走旧 Name 反射|冻结后改配置不改本轮；失败/缺值/取消不回设计初值；后续流程拿当前批来源|
|OutputVariableResolver 与 BindingContractValidator|现 Runtime 只有精确 image 特判，其余按点分旧路径|D06 producer 不通过旧字符串解析；不要新增 typed overload 后仍旧 fallback。若其他当前消费者仍用 resolver，保其实际职责，不能误删无关正式业务；新生产者统一一条 typed 链|output.width 不拆成 output/width；数值转换/集合数量按现服务规则|
|WorkflowCommunicationSendService:74–106；CommunicationApplicationService.GetCurrentVariables:260 起|发送会写/比较 ResultName=OutputKey；接收信息也透出 ResultName|通信生产者须从其正式输出声明生成同一 typed reference；校验同源身份，不继续 OutputKey 作为独立寻址协议。ReceiveVariableSource 的展示/诊断消费者需核真实用途同步，不扩通信平台|现发送生产者仍能建立/确认/执行；改配置拒过期；接收侧不泄露第二解释链|

## 冻结编译与资源闭包（不可省略）

现成能力：`src/Plugin.Contracts/Values.cs:143–163` OutputReference；`src/Plugin.Runtime/BindingReferenceSerializer.cs` 严格版本/重复成员/结构校验；`src/Plugin.Runtime/ParameterBindingService.cs:10` CompileBinding、:37 起 CompileReference、:76 起 ProjectType、:97 ResolvePublishedBinding。后者检查 consumer、DocumentRevision、DocumentSessionId，不能随意 new RunIdentity/revision0 过校验。

全局变量生产赋值是方案运行协调层责任，不是 UI 或 Runtime 工具责任。协调层应在现有方案批次中明确一个真实的 global-consumer scope/identity，使用该批真实 admission（原执行 owner/document session、编译配置 revision）；source `BindingSource/BindingNodeResult` 引用实际成功 publication 的 RunIdentity、SchemaRevision、Fingerprint，expected invocation 来自该批实际生产节点完成记录。不把历史 publication 或调试 DebugOwner 伪装成当前批生产输出，也不为计划通过创造新假 request。

**当前 `runtimeGlobals` 裸 object 不拥有资源。** 若 typed resolve 输出 image/region/含资源组合，把 resource 塞入该字典后退出 using reader 会悬空。整批必须二选一，按真实目标变量合同决定：

1. 标量和已支持的纯值集合：从受保 reader 解析到不可变/独立值，按实际变量存储合同交付，可先真实全闭环。
2. 图像/区域/含资源组合：只有现有目标变量与全局运行快照已有可保资源合同才开放。每个生产变量在方案批次 owner 中持 reader/lease 至后续消费者排空、整批完成/取消/失败；替换值先安装新 owner，旧 owner 只退休自身；每条提前退出都统一清理。不能借同一个全局 clear 误关新一代资源。
3. 若资源全局存储/租约基础不具备：入口明确说明不支持，不接纳 producer，不伪造序列化、不写 GlobalVariable.Value。record/组合若目标类型词汇/载体无法表示，也明确拒绝并保依赖项；不能把数值显示通过记为组合绑定完成。

相应资源 owner 必须属于方案运行生命周期；不要在 `ApplyProducedGlobalValues` 单方法 using 内关闭后续流程尚在读的值，不把资源复制到持久初值。重新运行/切方案、失败/取消、consumer 未启动/中途异常均需排空。保持主线已有结果 reader 所有权约定，不抢改公共 publication/图像合同。

## 集中实施批与共享边界

一次冻结完整 producer 合同和字段端点规则，随后连续实施：声明端点/结果行 → 正式变量接纳/clone/JSON/history → 协调/删除复制/依赖图/通信生产者 → 冻结编译/真实结果解析/运行值 owner → 相称集中回归。不能先只去 Guid 门禁发布，不能先根字符串后再添另一套 typed 消费。原件 All/组合需求完整保留，缺目标存储时逐项明确占位。

root 工作流线负责 UI/Workflow/现通信消费者和测试；OutputReference/ParameterBindingService 现合同优先复用，若确需共享 API 修改先与主线冻结完整签名及所有消费者，再由唯一写者成片实施。Plugin.Wpf 公共结果/资源/Catalog/主题仍主线所有。不得为此修改 Blob/Group/循环或新造显示插件合同。正式构建由主仓同源窗口统一，不在该独立 worktree 生成私有 Release。

## 验收矩阵

- 声明端点：根 scalar、record 实际可绑定叶、组合/序列 All；UI 容器/状态/More 行不能绑定。单 [n] 未有原全局绑定证据，不当既成范围；需要后续确认保持追踪。
- 身份：PortId 含点/连字符；成员名称/显示标签不同；改显示名保持；删除根/成员、改变 Type/Storage、错 scope、复制/删除再撤销均无错绑。
- 接纳：一次操作一次变量事务；失效 row/publication、运行中编辑、目标变量删除/换类型、编译拒绝无历史和模型污染。
- 生命周期：保存重开、Undo/Redo、结构 rebase、跨流程依赖/循环拒绝、运行冻结后改来源不影响本轮。
- 读值：零/一/多项、缺值/null、可证明数值转换与数量约束；失败/部分成功策略/取消/旧 invocation 不用初值掩盖不可用。
- 资源：目标合同允许才验真实 Frame/Domain/reader；替换、后续 consumer、完成/取消/异常排空，释放一次、无裸资源悬空。基础不具备时明确拒绝和依赖，不记完成。
- 通信：现有发送生产变量的创建/确认/执行和接收来源诊断同一规范引用，不保旧 ResultName/OutputKey 双寻址。
- 构建/自动/真实 VS/人工分别记录，本单不提供任何已通过结论。

## 生产准入与出口补定（root复核）

可信expected来源为WorkflowBindingContext构造阶段owner.Begin后形成的_expected（约202–211）。现RunSolutionAsync约534等待引擎返回才ApplyProducedGlobalValues，但Engine在返回前已ReleaseExecutionResources/释放context，并在外层CompleteRoot（约1343）；不能在返回后从ExecutionResult.DeclaredOutputs候选反抄expected，也不能生产使用PublishedBindingSnapshot（明确preview且限同scope）。

解析应接源workflow完成、ReleaseExecutionResources及CompleteRoot之前：request携同次冻结的producer工作项，BindingContext复用自己的声明与_expected，为实际global-producer:<variableId>操作在当前scope/root经owner.Begin/EnterRun正式准入，使用BindingRunSnapshot及ResolveBinding。操作结束排空run lease/回调；后续流程只消费已解析本批值，不跨root复用源解析计划。错误和取消同样形成变量本轮不可用，不回设计初值。

实际配置版本出口已存在NodeConfigurationEditHost.Capture().Version；当前Manager.CapturePublishedConfiguration只是Action，MainWindow约857及WorkflowDocumentPreparationTests有实际消费者。实施批应将该入口整体改为返回同次BindingConfigurationSnapshot的冻结捕获，PublishedRunInputs、WorkflowRunRequest与BindingContext贯通实际Version和配置；不得再在全局新链填revision0。不能从UI当前节点或另次Capture拼版本与定义。现BindingContext硬编码0也应同批统一实际请求版本，所有正式/调试/测试入口明确传本次版本。

producer唯一持久来源采用现严格serializer的引用表达式，显示名称只显示；旧ResultName/SourceFieldId寻址与重复WorkflowId/NodeId持久身份应退出消费，不留读取两种来源的fallback。端点行由真实声明根PortId和成员ID生成，不解析UI Key/FullName。需要同步全部consumer清单后成片替换，不先单改模型。

若目标全局值合同已支持资源，在RunSolutionAsync的while外创建本批变量槽owner：源workflow尚活时AcquireReader，解析成功后value+lease原子安装；替换只退旧槽；后续consumer/取消/失败排空后finally统一释放。裸runtimeGlobals字典本身无资源权。目标尚不支持标准IReadOnlyImage/region/record的项接纳时显式拒绝并记录依赖，不能仅因为能持reader就把旧SDK图像变量当新合同已通。

公共Contracts/Runtime签名无须为此扩展；新增限Workflow实际请求工作项、版本和受控运行值出口，执行层不访问WPF或UI回调。此补定仍为实施设计，未写产品/未验收。
