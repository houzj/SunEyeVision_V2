# 变量与节点批量配置事务：隔离核心源码交接

- 基线提交：f7fc6f18586ed2eae03840125ed23f2e52821549。
- 当前分支：codex/variable-batch-host。
- 当前执行者：Codex，/root/f01_structured_projection。
- 独占工作树：C:/Users/houzhongjie/.codex/worktrees/variable-batch-host/SunEyeVision_V2。
- 决策：docs/collaboration/VARIABLE_SAVE_TRANSACTION_DECISION_20261024.json，父已正式授权核心实施；此前5f只读树没有旧patch可恢复。
- 允许修改：Workflow的NodeConfigurationEditHost.cs / BindingConfigurationStore.cs / GlobalVariable.cs，新专用UI.Tests测试及本交接；主树UI/SDK由父负责。

## 已完成源码

1. GlobalVariableConfigurationEditDraft位于现Host文件，Configuration为独立Solution，BaseVersion绑定配置代，SetValue(id,value)同时更新私有显示值并保存独立显式写集。重设相同值仍是命令；新增变量不CAS不存在旧对象。未通过SetValue的既有文本值变化会被拒绝，不能推断成用户命令。
2. Host.BeginVariableEdit / ValidateBatch / CommitBatch。原单节点Validate/Commit委托批量路径；变量draft严格Owner/版本，节点维持原逐节点签名兼容。绑定目标对Parameters或ParamSettings的修改与节点draft按稳定scope合并；同一节点两个参数/设置集合都变化且不同则保守拒绝，相同变化可合并，变量private节点未改变时绝不覆盖node pending。
3. Store.Validate与Publish共享Prepare：私有复制、Reconcile、Graph/inputIssues验证后接受。显式写集forcePublication使相等JSON也执行CAS/接受并至多推进一代；纯无编辑仍no-op。history容器所需空间在接受前准备。
4. [JsonIgnore]ValueRevision；普通Value setter实际变值递增，原子CaptureValue/CaptureValues/CaptureDefinitions。后者在同ValueGate内固定成员、复制稳定Id/metadata/bindings/值/revision，无public setters、JSON、UI或native操作；primitive数组复制，复杂值沿原borrowed只读语义，不产生owner/Dispose。为防任意caller枚举器在锁内执行，两批量capture仅接受文档的实际List<GlobalVariable>或固定GlobalVariable[]。
5. Host原子接受：Host._gate→Store._gate→ValueGate；先统一检验实例及opening ValueRevision（含ABA），再raw投影节点引用/变量metadata与写集/集合。ValueGate持续到Store代与历史赋值完成，随后释放全部锁才Emit通知。通知失败记LastRefreshError，已经发布仍成功、draft已消耗，不能重复发布。
6. 完整checkpoint：节点Parameters/ParamSettings原引用、变量配置/绑定原引用、值原引用与revision、原集合对象/顺序、history backing新增项。默认禁用的私有整数_acceptanceFailureAfterRawMutation仅供确定性测试反射设置，不执行任意锁内Action。异常时恢复后不推进Store代、不消费draft，可同草稿重试。
7. 存活节点/变量和GlobalVariables集合身份保持；Host局部配置history backing保留被移除变量实例，使Undo/Redo恢复对象而不回灌其历史Value。任何历史恢复都不重放显式值命令。类型恢复若与最新live值不相容会拒绝，保持原状态，不隐式改运行值。
8. 真实外写guard保留Name/Type/bindings并补Group/Index/Description/AdditionalData及实例/集合身份；未排除真实配置、未无条件rebase。独立SelectedDisplaySource语义沿前继保留。

## 新专用测试源码

tests/SunEyeVision.UI.Tests/GlobalVariableConfigurationEditHostTests.cs：15 Fact＋6 Theory（22 inline rows），预期37个用例。纯实际Host/Store/ProtocolParse配置路径，不Show、不创建Application。覆盖一次batch、打开live值/未编辑保最新、显式同值/ABA、Store0/live10/显式0正负控、第二变量CAS冲突、raw第1/5/7笔失败后的全checkpoint恢复和同draft重试、变量metadata/增删/顺序/对象与配置历史、稳定变量Id重命名、incompatible type/delete消费者拒绝、参数和ParamSettings重叠/相同/独立delta、坏Id/名字/type/value/拓扑、外写/stale/重复参与者、发布后锁外通知失败、值数组和定义绑定独立复制、并发definition的name/value一致。

## 未完成与实际验证事实

- 按父明确要求，本隔离树没有restore/build/test/Show/GUI/性能进程；没有产物、TRX或PASS结论。仅git diff --check通过，LF/CRLF转换提示保留。
- 未修改原NodeConfigurationEditHostTests.cs。旧Throwing_binding_notifications_restore_all_fields...期望通知抛即回滚，与正式新决策冲突；父负责改为已发布+LastRefreshError oracle。
- 父负责UI/SDK Prepare与legacy前置拒绝、统一Host生命周期/renew、Session/Document检查、磁盘dirty/save/run快照接线及所有实际构建/定向验证/独验。当前生产draft adapter只ProtocolParse/Threshold，不冒全部legacy或完整B02通过。
- 原主窗变量Save FAIL与所有旧fixed/运行证据不变。本源码不替代真实pending181＋三变量同保存/非法拒绝/Run/关闭重开验收。
- 原固定验收输入threshold-run.solution只读核得Count/Ratio/Label三个变量无消费者/生产者，保守params/settings冲突规则不会阻挡这三个未绑定变量与节点pending参数并批；其他同节点真实重叠明确拒绝并保draft。
- 可文本编辑值只Integer/Double/String/Boolean及既有primitive数组形态；不新增业务InitialValue、Native owner、全局registry或保存manager。原地修改既有Value数组不经过setter无法增加revision，正式写者须按既有替换Value路径；不声称新框架解决任意mutable native值。

## 后继核验入口

- 父合并本commit的四源码/测试文件，并保自身UI/SDK与旧oracle修正；先owned前检再build，精准过滤FullyQualifiedName~GlobalVariableConfigurationEditHostTests（预计37，不冒实际计数），再按影响补原Host/编辑scope测试。
- 两个Capture批量API要求配置成员使用同文档受控List，父保存应重定向GlobalVariables getter到CaptureDefinitions独立集合，Run从同definitions读Name/Type/Value，禁止退出gate后回读livemetadata拼字典。
- 固定包、实际Main/变量Save、磁盘失败dirty、非法输入、实际Run与重开由父/独测统一执行；本子线无设施写入旧包或共享Release。
