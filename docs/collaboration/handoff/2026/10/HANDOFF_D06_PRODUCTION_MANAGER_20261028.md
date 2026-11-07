# D06 正式运行准备与 Manager 生产接线候选交接

- 基线：0acde8c4；分支 codex/result-binding-ui-endpoints；独立 workflow-overview worktree。
- 本文件补充 HANDOFF_D06_RESULT_BINDING_UI_20261028.md；两份为同一完整候选，不拆半成品提交。
- 状态：未提交、未集成、未构建或运行；用户已授权完整批实施，待 root 同模型/Engine/SDK/通信批一起原子切换。

## 本路新增生产文件范围

1. 新 src/Workflow/WorkflowPublishedConfiguration.cs：正式发布配置+实际owner/resourcepaths载体。
2. src/UI/Services/Workflow/WorkflowExecutionManager.cs：完整请求准备、同流/跨流/连续/通信批生命周期及单模块接线。
3. src/UI/Shell/Windows/MainWindowViewModel.cs：CapturePublishedConfiguration 从同一次 Host.Capture 创建载体，返回前复核相同session与Host snapshot；退出两处旧SetGlobalVariables调用。
4. tests/SunEyeVision.UI.Tests/WorkflowDocumentPreparationTests.cs：原4项保留并迁返回值入口；新增同/跨流成功、运行失败、配置失效6态，独立节点继续/无初值回退、冻结revision/owner/base/live普通变量、input等待取消实际batch退休与下一轮可运行。

## 完整生产调用链

- Main：当前session.Host.Capture → WorkflowPublishedConfiguration.Capture。同一版本提供workflow/globaldefs；配置copy附实际各workflow owner，request固定实际document ExecutionOwner与资源基目录。未绑定普通变量单独经CaptureValues采样live运行值；不把配置历史中的旧Value当运行权威。producer/失效/无目标存储的Value在副本清空，不保资源/设计初值后备。
- Manager：Func<WorkflowPublishedConfiguration>唯一正式准备入口，禁止先Action再分别读live engine/globaldefs/solutionOwner。运行参数另作瞬态拷贝。仅UI游标所有权/展示一致性仍看实际engine对象，不用其第二份内容替换run配置。
- 按冻结variable经唯一GlobalProducerBindingRules.Validate/TryDescribeTarget创建WorkflowGlobalProducerOperation；无效producer在再次克隆的defs标BindingValidationPending并跳过ops，初值仍排除，独立节点不因此整批阻塞。
- 单次/连续每轮新WorkflowGlobalRunValues；方案运行/同communication operation各workflow共享一个batch。每request携Configuration/actual ExecutionOwner/resourcepaths/GlobalProducers/ProducedGlobals。变量consumer通过root提供的WorkflowGlobalVariableReceiveService读取同一values，Manager不另造adapter。
- 删除ApplyProducedGlobalValues和OutputVariableResolver释放后回填链；跨流程值由Engine在source scope有效时内部Publish纯值后供后继读取。删除Manager旧_globalVariables/SetGlobalVariables，不留静默无效入口。
- Prepared inputs退出后按共享引用最后一次Dispose values；单/连续Release、solution finally、communication finally统一退休；Manager.Dispose先取消，仍活跃的批不提前退休，待运行finally释放。
- 单模块通信收敛Engine单节点冻结configuration，不执行上游、不读上次结果缓存；保assignment/nodeBindingOverrides、输入、取消事件、ExecutionResult.Dispose与完成事件。只接当前root直接节点；Group内部目标明确尚未接入正式scope并拒绝，不搬到错误root。

## 需 root 集中接缝

- 主线已承接模型/Engine/SDK/ReceiveData/通信消费者与新API。
- MainWindowCommunicationAutomation.cs 中旧Manager.SetGlobalVariables调用由root同步退出；本路不修改该文件。
- WorkflowRunConfiguration需新DocumentRevision及Capture的revision参数。
- WorkflowRunRequest需GlobalProducers/ProducedGlobals；WorkflowGlobalProduction.cs与WorkflowGlobalVariableReceiveService.cs由root提供，不复制到本支。
- GlobalProducerBindingRules由audit/root提供，State仅Compatible/Conditional准入，没有IsValid属性。
- 真正相对A/B保存的源回归走Main新callback；测试显式AllowUnhosted路径从传入solution或engine fixture走Host.Capture，不借live CurrentSolution补owner/base目录。
- 单模块同communication batch分次producer→同root consumer：root已核图编译只对knownNodes中的producer加依赖，源不在单节点图不会伪造缺失节点；普通global参数走冻结pure值。新增实际单模块先producer再consumer成功与新batch仅consumer拒绝/不回退999初值的回归源码。Manager保留来源身份，不改Graph；尚未运行。

## 核验

完整UI+Manager候选14个C#以进程已加载Roslyn纯语法解析0错，git diff --check通过；未加载SDK编译程序集、未构建私有Release或复制DLL。未语义编译、未执行新旧回归、未VS/人工验收。不能套用此前正式构建成绩。当前完整清单包含此前UI11个C#及本次Manager/准备测试/新Workflow载体，共14个C#，另两份专用交接。
