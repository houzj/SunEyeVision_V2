# 主工作区 VM 对照与 ImageSource-Blob 完整链路任务

- [ ] Task 1: 建立 VM 主工作区差异矩阵
- [ ] Task 2: 固定 ImageSource → Blob 字段、ROI、结果和持久化合同
- [ ] Task 3: 建立 ROI 不可用、参数红框误报、结果区空白的最小复现
- [ ] Task 4: 接通 ImageSource 正式输入与 Blob 输入准入
- [ ] Task 5: 修复 Blob 参数、ROI 创建/继承/屏蔽区和正式提交
- [ ] Task 6: 接通执行、取消、结果发布和图形文字显示
- [ ] Checkpoint: ImageSource → Blob 运行闭环
- [ ] Task 7: 完善方案保存、命名、最近列表和重开
- [ ] Task 8: 保存恢复节点、连线、参数、ROI、显示和历史结果
- [ ] Task 9: 完善切换、关闭、迟到回调和资源退役
- [ ] Task 10: 对照主工作区画布、结果视口、小地图和窗口组装
- [ ] Checkpoint: 持久化与工作区闭环
- [ ] Task 11: Release 构建与相称回归
- [ ] Task 12: ImageSource → Blob 完整人工验收
- [ ] Task 13: 更新主文档、实施总结、证据索引和未实现范围
- [ ] Checkpoint: 完整闭口

## ExecutionServer 产品级重构前置

- [ ] 完成 VM 启动服务、Server、ModuleProxy 和关闭闭包取证
- [ ] 完成 VM Solution/Procedure/Module/Port/Connection 生命周期取证
- [ ] 完成 VM 拓扑编译、依赖调度、分支并发和取消异常取证
- [ ] 完成 VM 输入传播、结果提交、订阅和资源所有权取证
- [ ] 完成 VM 方案切换、关闭和最终释放顺序取证
- [ ] 取证矩阵收口并定稿 ExecutionServer/CompiledWorkflowPlan/RunRequest/ResultSubscription 合同
- [ ] 设计合同审查通过后再开始 ExecutionServer 实施

## 当前批次：ROI 完整生产链

- [x] 原厂证据：完成 ROI 按钮、SelectedROI、ROIList、ImageViewBox、ValueChanged、UpdateROI、RoiDrawTips 链路对照
- [x] 失败证据：新增回归证明五个 ROI 形状按钮初始全部 IsEnabled=false
- [x] 命令入口修正：按钮恢复可用并且矩形命令实际进入绘制状态
- [x] 定向验证：ROI 相关 4/4，Blob 三页 5/5
- [ ] Task 14：命令加载恢复、帧借用和会话创建时序
- [ ] Task 15：无图像、失效帧、不可绘制反馈和同帧重借
- [ ] Task 16：各形状创建、提交、撤销重做和屏蔽区约束
- [ ] Task 17：绘制期间刷新、输入切换、结果重投影和正式选择分离
- [ ] Task 18：保存重开、取消关闭、方案切换、迟到回调和租约释放
- [ ] Checkpoint：ROI 链完整通过后再做全量生态回归

## 当前验证记录（2026-11-13）

- Release 全量构建已在 `-m:1` 下通过；并行构建对共享 `build_temp\obj` 的竞争已确认不是权限阻断。
- ImageSource Runtime：45/45 通过（设置 `S2_CASE_ROOT=E:\\SunEyeVision_V2\\Release\\_validation`）。
- Blob Runtime：修复停用引用默认值的 `int`/`long` 语义错误后，定向持久化绑定测试 2/2 通过；结果字段 21/22/25/26 与灰度统计序列合同已补齐，相关 14 条测试通过。
- 当前剩余 Blob Runtime 失败集中在屏蔽区几何语义 1 条、轮廓测试发现阶段 1 条；其余原始失败已收敛。UI 三页回归仍有 ROI 投影、对话框观察和停用修正来源清理问题，尚未计作完成。
- 位置修正关闭链已确认：`BlobTool.ExpandChanges` 和正式草稿都能生成并提交来源清理，当前缺口位于可视编辑行的接受状态刷新，不能用“草稿已清理”代替 UI 闭环。
- 持久化回归已收口：`EmbeddedFileCatalog.Parse` 对 JSON 整数节点的 `int`/`long` 表示不兼容，已改为按 JSON 数字令牌读取并统一抛出 `JsonException`；清理旧 `dotnet/testhost` 进程及测试输出副本后，设置 `S2_CASE_ROOT=E:\\SunEyeVision_V2\\Release\\_validation` 重跑持久化套件 **56/56 通过**。
- UI 持久化三条回归仍未闭口：ImageSource 保存重开在直接切换方案时被“通讯服务及驱动”租约阻断；Blob 主图选择在重开后仍带有图像值；Blob 屏蔽区保存重开断言失败。它们已与程序集装载问题分离，下一批按“方案切换前服务租约排空、结果来源 HasImageValue 语义、屏蔽区继承投影”三条链分别修复。
- 关闭资源阻断已修复：`MainWindow.CompletePreparedCloseAsync` 在插件租约超时后现在等待同一个 drain 任务完成并自动重新执行关闭链；`ShutdownLifecycleProbe wait` 已通过，返回“Actual MainWindow shutdown scenario passed: wait”。原先“等待资源释放”只弹窗但不重试的缺口已闭合。
- Blob 位置修正 UI 状态投影已修复：关闭修正开关时通过真实 `input.pose` 参数行的宿主写回清理组合来源，避免直接向不含投影字段的编辑草稿写入；`Disabling_position_correction_clears_its_source_in_the_same_undoable_edit` **1/1 通过**。
- Blob 三页回归复测为 **2/5 通过**（此前 1/5）：位置修正清理已通过；剩余为 Reset 对话框观测、真实三页数值模型查找、继承 ROI 预览颜色/层路由三项，仍未计作 UI 闭口。
- ROI 预览实现已补两处结构缺口：继承/区域模式变更现在触发 `BlobLocalRoiOwner` 预览刷新；只读 ROI 层把预览形状投影到与结果视口相同的分组 `PresentationItems`。本次回归复测被测试夹具直接 `SolutionManager.Open` 的通讯租约阻断，尚未计为通过，需在切换前排空服务后复测。
- 参数页惰性首屏阻断已修复：`DeclaredFieldsView` 显式物化初始选中页，避免初始页索引为 0 时不触发 `SelectionChanged`；Blob Assist 控件改为由正式修正开关同步可见性，并在关闭状态首次打开时先完成一次 VM 控件物化。`Real_three_pages_read_numeric_models_and_retire_with_the_actual_editor` **1/1 通过**。
- 当前 `BlobThreePageSessionRegressionTests` **3/5 通过**；剩余两条均为测试基础设施/夹具入口问题：Reset 测试的自动消息框应答钩子使观察列表为空；ROI 回归在测试中直接 `SolutionManager.Open`，未沿正式方案切换链排空通讯服务租约。两项不计作产品控件链通过，待分别修正测试入口后复测。
- 测试入口阻断已收口：Reset 钩子仅对该确认/成功消息保留真实 owned dialog，Blob 三页套件 **5/5 通过**；直接重开前补齐通讯租约排空。持久化根验证器现允许当前 `EmbeddedFileCapture` 写入的根级 `EmbeddedFiles` 信封字段，ImageSource 保存重开两条回归 **2/2 通过**。
- 仍有一条独立 ROI 调试窗口回归未闭口：当前 ROI 借帧要求结果工作区选中上游输入图像，调试窗口初始仍选 Blob 输出，`PrepareInteraction` 返回 false；本轮未保留试探性工作区自动切换，待按 VM 原证补齐“编辑器打开时输入图像选择”完整链后再修。

## 2026-11-15 状态边界重构批次
- [x] 原厂证据核对：配置、运行策略、显示对象、结果发布、编辑层生命周期分离
- [x] ImageSource 所有编辑入口统一结果快照续租
- [x] 接纳窗口跳过结果发布退役和显示清空
- [x] 无探针 Release 构建
- [ ] 人工验收：ImageSource/Blob 全入口不执行保持上次结果，重新执行替换，身份切换清理

## 2026-11-15 公共执行链常驻化

- [x] 建立方案级 SolutionRuntime 与 ExecutionPlan 合同
- [x] 拆分计划构建和每轮输入/变量捕获
- [x] 复用方案级 WorkflowExecutionEngine 与工具 Session，保留取消/连续运行/关闭排空
- [x] 接入配置、节点、方案和目录身份失效释放
- [x] 增加执行计划命中/构建分段探针并核对首次/后续差异
- [x] 复用配置未变更时的工作流执行快照，按配置实例变化失效
- [x] 全量 Release 构建并记录载荷哈希
- [ ] Release 人工验收首次/后续、连续运行、取消、编辑失效和关闭释放
