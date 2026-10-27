# 根流程工作区并行只读核验交接

- 基线提交：adc727ba1eabf1d3af3bbbf2e5d22494fb402597（固定版本）
- 当前分支：codex/workflow-review
- 当前执行者：D 只读核验
- 任务目标：对照恢复项目和R01—R18，核清可实施链与真正基础缺口；不把恢复件等同原厂行为。
- 允许修改目录：仅本文件。产品、测试、共享任务账本和原主方案只读。
- 已修改文件：docs/collaboration/subtasks/workflow-review.md

## 结论与阻断项

1. R08不得声称原厂等待语义已迁移。恢复件 `VisionFramework/SolutionExecutor.cs:20—22,46—54` 创建 CancellationTokenSource/CancelAfter，将取消转为TimedOut，并明确排除用户取消优先。它证明恢复宿主有协作超时实现，不证明原厂 Get/SetProcedureWaitTime 等同整流程执行时限。固定基线 RootWorkspaceSettings 仅有间隔、NG和总览位置，没有有效等待超时字段。当前保持明确禁用占位是诚实范围；若启用须完整设计迟到完成、资源释放、用户取消、通信/相机等待边界。
2. R12确有可实施基础，不能整页判缺基础。当前已有正式运行参数描述符、BindingInputCatalog和多节点候选发布。恢复件 `WorkflowConfigurationWindow.xaml.cs:61—101` 不仅曝光参数，还曝光输入槽，支持订阅选择；`WorkflowConfiguration.cs:4—12` 还保存CategoryId、Relations和稳定目标身份。A批如先接标量参数代理，需保留输入槽/分类/多关联的逐项剩余，不用“运行参数页完成”覆盖差异。此项是完整性检查，不要求恢复被冻结的Group行为。
3. R11根输入/输出与R14局部变量是宿主正式身份链缺口，不是逆向材料缺失。恢复件 `WorkflowConfiguration`、`WorkflowSubscriptions.WorkflowSourceKind`、`WorkflowRunContext` 有独立WorkflowInput/LocalVariable身份、初始化、订阅、当次执行值和结果发布。当前 `DataSourceQueryService` 的GlobalVariables是方案字典，`__SubroutineInputs__`由引擎子调用注入，并非根输入。直接改标题、名称前缀、假节点或借Group绕过都不成立。SDK来源/作用域与根快照未具备且本轮禁止公共扩展，准确禁用占位合理。
4. R13恢复件配置预览消费根Outputs/Displays声明并转换实际图像；当前节点结果/图像缓存有可复用能力，但不能命名为完整根输出/显示映射。节点预览须明确作用域、实际本代结果与图像身份匹配，关闭清引用；共享图像不可由窗口擅自释放。完整根显示声明、报告映射与租约合同暂占位。

## R01—R18固定基线验收矩阵

|项|adc727ba源审状态|集成批必须保留的验收出口|
|---|---|---|
|R01|40px根Tab、四态资源及正式配置红点已接；配置/局部变量仍禁用|暗亮长名、选中运行禁用、整窗物理点击与设计时；R10/R14另列|
|R02|30px选中可见和固定新增/溢出已有|多Tab窄宽Resize、首尾键鼠切换、多DPI|
|R03|根与普通节点目录沿正式身份导航|重名、过期对象、零根、关闭弹层，不扩展Group|
|R04|根复制复用片段和方案级历史|新根/节点/边/参数/引用独立身份、撤销重做、保存重开真实运行；新增曝光ID同步重映射|
|R05|根增改删与零根恢复已有|当前根删除切选、引用清理、UndoRedo、保存；不能仅删Tab|
|R06|不可变根设置和执行代捕获已有|间隔0/非0、取消等待、单次不等待、旧代不被编辑污染|
|R07|真实JudgmentStatus NG聚合已有|成功NG/OK、执行失败、取消分离，重启无旧代资源|
|R08|原厂语义未证；基线无有效实现|明确禁用，不把恢复协作超时称VM等价|
|R09|正式定义/插件/参数/待修绑定判断已有|加载变化、修正刷新、缺插件、空图；不取LastResult作为就绪|
|R10|完整根配置窗尚无|单窗、会话/根对象身份、确认取消、运行门禁、切方案关闭、暗亮实际WPF|
|R11|宿主根输入身份全链未有|占位解释来源、值快照、查询/编译、复制删除所缺合同|
|R12|正式目录/多节点事务可复用，根代理尚无|参数值与根代理同次提交回滚、历史、失效修复、复制映射、保存重开、真实执行；输入槽/关系/分类差异不隐去|
|R13|节点真实结果可用；完整根显示合同缺|真实结果不等同计时；图像代匹配、切根/关闭清理|
|R14|只有方案全局；独立根局部全链缺|准确占位，不假隔离；未来同名隔离/生产消费/执行初快照/删除Undo|
|R15|总览坐标持久化已有，无总览UI|140×120卡、真实计数/耗时来源、移动取消/复制/Undo/保存重开、独立视口|
|R16|搜索/画布/小地图既有|总览往返关闭搜索/详情/拖动，视口独立、订阅清理、多DPI|
|R17|现有根快照只读详情及单窗生命周期|原样式逐态、根计时数据、重复开关/切根关闭；Group选择保持冻结|
|R18|恢复件本身禁用项与未证标题注释|导出/通信/变量导入导出不得造假可用；未证不等于确认不存在|

## 可定位来源与文件身份

恢复项目路径均为 `D:/MyWork/REA逆向测试/src/`，本次只读源码，没有运行原厂或恢复程序：

|文件|SHA256|
|---|---|
|VisionFramework/SolutionExecutor.cs|A49BA2AC2B924D8C5FA16941F138A78F92A6D81E47180753FD95B2D54349FC3B|
|VisionFramework/WorkflowConfiguration.cs|AD59DB622C095AC22C3550C3B3CA4B6491AC31D68108A83E0CAA9C56928E7B1F|
|VisionFramework/WorkflowRunContext.cs|D00F29701F640028040E0F2C3D1AC11246FFB500945246C3424E435741241247|
|VisionEditor/WorkflowConfigurationWindow.xaml.cs|C4550293546A4B0D691A3FE133A16E1E916B479B8B0E243321D804F43C878A50|

本产品来源固定在上述基线：`src/Workflow/RootWorkspaceSettings.cs`、`WorkflowRunConfiguration.cs`、`BindingConfigurationStore.cs`、`NodeConfigurationEditHost.cs`、`DocumentEditSession.cs`，以及 `src/Plugin.SDK/Execution/Parameters/BindingInputCatalog.cs`、`DataSourceQueryService.cs`、`src/Workflow/WorkflowExecutionEngine.cs`。原R表沿用主方案，不另缩范围。

## 验证、剩余与交接

- 已完成：固定基线源审和能力边界复核；R08/R11/R12/R13/R14重点差异已发集成负责人。
- 已执行构建与测试：无。本路线按授权不部署、不复制程序或库、不改测试。基线既有测试记录只是引用，不是本路线重跑结果。
- 人工验收结果：未执行；不声称原厂动态、VS设计时或产品物理交互通过。
- 未完成：等待A/B/C固定提交后，沿新合同、生产消费者和退出清理链做增量评审；最终集成构建和同版Release验收由A承担。
- 已知风险或禁止改动：Group、循环、SDK公共来源/显示合同冻结；不主仓/main同步；不读取浮动源码后宣称固定版验证通过。
- 下一步：A提供固定提交SHA后，仅核对应差异与上述完整功能出口；真实缺基础项在原主方案保留禁用占位和后续依赖。