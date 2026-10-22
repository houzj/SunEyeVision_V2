# VM-PERSIST-14/F — VM-READ-01 与 VM-OPEN-02 有限原版取证

2026-10-22；原版侧只读证据，基线 `2db7540b0f803bb6f80fd4557d3098d9529ce559`。本报告只回答 B1 尚未定案的异常读取和重新加载问题。复用现有反编译源及先前 `analysis/sol-re-01/SOL-RE-01.md`；没有运行 VM、SDK、设备或制造损坏方案，也没有修改产品/共享模型。结构化逐项证据见同目录 `evidence.json`。

## VM-READ-01：异常读取

1. **格式/密码的前置门**（R01、R02）。菜单加载在 `ProjectManager.LoadSolution` 先调用 SDK `CheckSolution`；抛 `VmException` 时显示异常并返回 `false`，`nValidFormat != 1` 时调用 `LoadSolutionFailed` 并返回 `false`，有密码时需解密窗口或 `CheckPassword`。这些分支位于关闭全局通信和 `VmSolution.Load` 之前。因此只有**前置门拒绝**可确认不会从此路径启动切换任务；无法由此推广到所有损坏情形。
2. **坏格式与最近记录**（R03）。`LoadSolutionFailed → RemoveSolution` 仅当路径存在于最近项时询问是否移除；确认后 `RemoveRecentSolution` 从 `ApplicationName.cfg` 的最近列表删除路径。源码中这个动作**不是删除方案磁盘文件**。不在最近项时显示错误框。是否对坏内容创建备份、默认恢复或修改原文件，托管链未见证据，不能写成“会”或“不会”。
3. **已进入原生加载后的失败**（R04、R05）。`VmSolution.Load → CheckSol → Import → SolutionManager.SyncLoadSolution → IMVS_LoadSolution`。原生加载开始回调设置 `isLoading=true` 并触发主窗口关闭通信/重置 UI 资源。普通非零结束状态使 `VmSolution.Instance = new VmSolution()`，旧实例模块去激活；`VmSolution_Import_OnEndCallBack` 清空新实例方案路径/密码。特殊码 `-536870093` 保留失败刷新流程，并调用 `GetMultiObjectLoadState` 给已存在模块标 `IsLoadFailed` 的 `true/false/null`。不能由这段代码把该特殊码归因于“未知模块”或“坏变量”。
4. **错误报告与返回值**（R06）。菜单前置格式失败有错误框；原生加载路径的 `ProjectManager.LoadSolution` 把任务异步启动后就可返回 `true`，`true` 不是成功终态。`SolutionManager.SyncLoadSolution` 在 SDK 模式对非零结束码抛异常；UI 模式在这一层不据该码抛，依赖回调的状态/UI 后续处理。单一 `bool` 不足以判定工作区最终有效。
5. **四类内容损坏的边界**（R07）。本轮托管调用链没有把“未知模块”“坏变量类型/值”“坏连接/孤立边”分别映射到确定的 SDK 状态码、跳过/默认替换/删除规则或文件写回规则。现有 `GetMultiObjectLoadState` 只说明**某些回调状态下**有逐模块失败标记。细分类策略的第一断点为原生 `IMVS_LoadSolution` 及其解析/回调路径；需针对真实只读样本或原生函数继续取证。不得用当前恢复项目的容错行为冒充 VM。

## VM-OPEN-02：同路径打开与 Reload

1. **菜单同路径请求**（O01）。菜单打开、最近项打开都进入 `ProjectManager.LoadSolution`；可见的这条入口没有以 `filename == VmSolution.Instance.SolutionPath` 直接返回的分支。通过忙态、用户保存询问、格式/密码和版本门后，任务会调用 `VmSolution.Load(filename,passWord)`，再经 `Import` 到 `IMVS_LoadSolution`。所以可确认同路径**再次发出原生加载调用**；原生内部是否重新读取磁盘字节、命中缓存、增量重建，托管源码无法证明。
2. **显式 Reload**（O02）。本轮对 `ProjectManager`、`TitleMenueItemData`、`VmSolution`、托管 SDK wrapper 做了限定检索，未发现独立的整方案 `ReloadSolution` 命令。`VmModule.ReloadModule` 用于模块数据/全局模块，不是整方案 Reload；`Program.cs` 中 `Lang_DevelopmentReloadSolution` 只是退出提示字符串。限定检索不足以宣称整个产品没有其他重新加载入口。
3. **候选失败与旧工作区**（O03）。格式校验等前置失败在资源重置前返回，旧托管实例不因这条入口替换。加载启动后，`ProjectManager` 先关闭全局通信，原生 LoadBegin 又 `ResetResource`；普通非零 LoadEnd 明确替换 `VmSolution.Instance`，因此“候选失败仍保留旧工作区”不是 VM 通用策略。原生图/设备的先卸后载顺序、异常中断时是否留半载入状态仍未知。
4. **提交时机与覆盖风险**（O04）。原版可见路径是**前置检查 → 可选旧方案保存/撤销快照 → 原生加载 → 回调更新活动实例/路径**，并非可证的“先完整构建候选、成功后原子提交”。如果用户在切换时选择保存，`SaveSolution(task)` 可能先保存当前活动方案再启动加载任务；同路径重开会先写入当前内容的可能性必须作为使用场景说明，不能把它等同于“无写入的重新读取”。是否对原文件做自动备份/覆盖保护、原生写入事务仍未知。

## 横向比较所需的事实与取舍边界

`actualPolicy` 仅能写成：**前置格式/密码拒绝；条件满足后重复派发原生加载；加载开始会清 UI/通信资源；普通非零回调重建托管当前实例；特殊码可保留逐模块失败状态；最近项错误可提示移除最近记录。** 它不等于原版对四种坏内容的完整策略，也不证明原生磁盘重读或方案文件覆盖保护。

供自研 B1 讨论的“隔离候选、校验后提交、失败保留旧会话、同路径明确重读与缓存失效、坏条目隔离/备份”均是**候选设计**，不是原版事实。它们的潜在优点是减少失败破坏和状态歧义；代价可能是双实例内存、设备独占转移、引用重绑定、保存与重开事务协调，均属设计推断，需以自研当前代码和测试量化。已完成自研优化无需重分析；本轮未审自研实现，主控应只把本证据用于 B1 未定案比较。

## 第一断点与可补证条件

- 原生 `IMVS_CheckSolution` 只暴露格式/密码输出；它对具体坏模块、变量、边的容忍度未知。
- `IMVS_LoadSolution` 的解析、读取/缓存、旧图释放、加载事务、逐项容错和文件写回不在托管方法体中。下一步须取原生实现或使用**独立受控只读样本**做差分；本任务未获准制造损坏原始方案或运行 VM。
- `-536870093` 的触发条件与 `GetMultiObjectLoadState` 状态码对四种坏内容的映射未证，不能从后处理推倒原生前因。
- 本地 SDK 指南仅发现两份 CHM（.NET/C++），本轮未取得可定位正文页；不引用其标题作行为证据。

本批交付到此停止。既有 `SOL-RE-01` 的方案生命周期结论供交叉核对；没有扩展产品实施、整套加载器恢复或跨对话消息。
