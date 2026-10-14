# B3宿主节点编辑网关交接

- 基线提交：a108da498ae21bda155bbf05806b9368d9fa3fe6。
- 当前分支：feature/comm。
- 当前执行者：Codex单写入，无并行写入、代理提交、推送或跨AI写入交接。
- 任务目标：继续B3，为窗口草稿和正式提交建立可调用的宿主网关。
- 允许修改目录：D:/MyWork/SunEyeVision_V2-comm；保留用户与前批全部变更，不操作设备或另一项目。

## 已修改文件

- src/Workflow/NodeConfigurationEditHost.cs：节点草稿、冲突检查、宿主接受投影、完整参数／绑定恢复及通知异常回滚。
- src/Workflow/BindingConfigurationStore.cs：宿主接受边界、无实际变化提交去重、重入保护；正式契约造成失效时仍发布，不将其当无变更。
- tests/SunEyeVision.UI.Tests/NodeConfigurationEditHostTests.cs：7项宿主网关测试。
- tests/SunEyeVision.UI.Tests/BindingConfigurationStoreTests.cs：新增无变更提交回归，并调整原测试使用实际配置变化；核心共14项。
- 既有主方案、通信实施计划、CURRENT_TASK及本交接。

## 已完成

宿主通过BeginEdit获得与正式节点隔离的参数和绑定草稿。Commit检查当前正式方案及节点基线，相关配置变化时拒绝覆盖并保留用户草稿；无关节点的编辑可在最新正式代上提交。验证成功后，宿主先接受完整投影，正式版本和历史随后才发布。成功草稿不能重复提交，未改配置的提交不产生历史或新版本。

活节点和变量对象保持原身份，参数与被解绑的下游引用共同变化；撤销／重做恢复完整参数及变量生产／消费关系，不替换画布、设备／通讯配置，不恢复变量的运行值，也不清理正在运行的结果空间。投影通知异常时逐个尝试恢复已变字段、原参数和绑定对象，正式代与历史保持不变；同一草稿可在通知故障消除后重试。接受和投影禁止重入，未变化的变量绑定不重复通知。

## 未完成

窗口尚未调用该网关，现有实际行为仍沿B2原链路。MainWindowViewModel.InitializeToolControlAsync目前直接SetCurrentNode(node)和SetParameters(node.Parameters)，不能只替换提交回调就宣称隔离。网关尚未与CommunicationApplicationService形成统一配置权威，也未代替执行计划或CommandManager。

DefaultDebugWindow执行与关闭已有TryCommit路径；CustomDebugWindow仍有反射PersistParameterBindings的关闭路径。接入前必须统一关闭失败的阻止行为、成功后草稿参数重绑和受影响编辑器的提示，避免窗口关闭丢草稿或覆盖其他打开窗口的未提交内容。当前SDK通用注册与查询服务按节点／工作流组织，宿主应保持稳定作用域，不能用裸节点ID跨流程合并草稿。

外部拓扑／变量绑定修改不会被网关默默吞并，当前会拒绝覆盖，需由统一事务入口建立新基线。完整拓扑、保存／加载／导入、所有执行入口、运行结果配置版本和命令历史仍待接入；B4增量索引、B5人工验收及完整U1至U3保持后置。

## 已执行构建与测试

- 最终135项专项通过，0失败；含14项核心和7项宿主网关测试，以及前批UI／通信／依赖回归。证据artifacts/validation/binding-adaptation-b3-host-20261014/b3-host.trx。
- 覆盖通知异常完整回滚及可重试、同节点旧草稿拒绝、外部设计变化拒绝、无关编辑器顺序提交、无变更去重、源参数与消费者共同恢复、变量当前值保持和失败草稿保留。
- 标准全量Release：0错误1152警告。
- 最终标准UI Release：0错误949警告；中间构建曾报告1064警告，以最终命令结果为本轮记录。
- 常用Release已构建更新，最终隐藏启动工具注册探针退出0、24工具；四通信工具、Script及Subroutine正常。证据artifacts/validation/binding-adaptation-b3-host-20261014/release-toolbox.json，未初始化硬件通信。
- git diff --check通过；保留既有改动，没有无关警告清理。

## 人工验收结果

尚未启用窗口入口，不提前要求用户以B3全量行为进行统一测试。真实Visual Studio设计器、高DPI及全入口人工验收仍待接入后记录。

## 下一步

先把Default／Custom窗口改为持有草稿并通过此网关正式提交，补成功／失败／关闭／连续执行的生命周期测试及草稿重绑。随后接入同一CommandManager的事务历史与各执行入口；保留设备、通讯和运行数据空间，不将绑定配置投影作为完整Solution替换。文档维护技能已同步方案和计划的真实阶段。
