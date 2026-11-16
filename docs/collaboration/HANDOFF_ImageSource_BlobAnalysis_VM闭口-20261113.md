# AI 协作交接单：ImageSource → BlobAnalysis VM 对照闭口

- 基线提交：7339632e0be48f958d99ba03981106badcca325c
- 当前分支：feature/param-window-datatree-codebuddy
- 当前执行者：Codex。目标已按用户要求暂停，后续由 CodeBuddy 接手
- 工作区：E:\SunEyeVision_V2
- 目标：完成 VM 对照闭口，优先贯通 ImageSource → BlobAnalysis，并覆盖 ROI、执行结果、持久化、保存重开、取消关闭、资源释放和结果工作区生态

## 接手前必须阅读

1. docs/collaboration/CURRENT_TASK.md 顶部摘要
2. docs/collaboration/evidence/VM调试窗口结果视口更新路径-20261112.json
3. docs/collaboration/VM-ImageSource-Blob全链路差异矩阵-20261111.html
4. 本交接单和 AGENTS.md

## 当前工作区

工作区有大量未提交的阶段性修改，涉及持久化、Blob 三页、ROI、结果工作区、编辑器调试窗口、关闭释放、画布和测试夹具。不要执行 git reset --hard、批量丢弃或覆盖其他改动。先审阅 git diff 和文件边界，再决定是否建立临时提交。

## 已完成且有记录的范围

- EmbeddedFileCatalog 严格 Int64 JSON 校验。
- SolutionDocumentValidator 接受根级 EmbeddedFiles。
- Persistence 56/56，ImageSource 嵌入持久化 2/2。
- 主窗口关闭 drain 超时后自动重试关闭，ShutdownLifecycleProbe wait 曾通过。
- Blob 三页回归 5/5。
- ROI 保存重开回归通过。
- 上游失败退役下游发布并支持新帧恢复。
- Blob 结果树按 BlobDisplay.xml 显示声明和成员转置方向重构。
- 双击调试窗口闪烁已有 VM 证据和结果图层保留改动。
- 画布、小地图、欢迎页实名保存等阶段项已有 CURRENT_TASK 记录。

## VM 源码级证据

仓内原厂反编译保留源：

- src/Plugin.Wpf/Controls/Native/Input/WorkspaceControls/WorkspaceControls/ModuleResultView.xaml.cs.pending
- src/Plugin.Wpf/Controls/Native/Input/WorkspaceControls/WorkspaceControls.DataForUI/ModuleDisplayDecorator.cs.pending
- src/Plugin.Wpf/Controls/Native/Input/WorkspaceControls/WorkspaceControls.DataForUI/DisplayImageListDecorator.cs.pending
- src/Plugin.Wpf/Controls/Native/Input/WorkspaceControls/WorkspaceControls/RenderControlBase.xaml.cs.pending
- src/Plugin.Wpf/Controls/Native/Input/WorkspaceControls/WorkspaceControls/RenderControlBase.xaml.pending

原厂语义已经确认：

1. 单图调试时关联输入变化进入 ModuleResultView.OnRelatedIOChanged。
2. 该方法直接把 SelectedImage 切换到关联输入图像。
3. OnModuleCallBackArrived 从 RenderDataCenter.SelectedImage 刷新右侧结果视口。
4. 该调试切换不改正式保存的显示选择。
5. RenderControlBase 的单图下拉绑定 SelectedImage，双图输入绑定 SelectedInputImage。

## P0 未完成问题

失败用例：

SunEyeVision.UI.Tests.EditorDebugSessionTests.Blob_primary_image_edit_selects_the_new_roi_frame_without_replacing_the_saved_output_choice

测试文件：

tests/SunEyeVision.UI.Tests/Session/EditorDebugSessionTests.cs，约 755 至 820 行。

复现：

1. ImageSource 连接 BlobAnalysis 并运行。
2. 正式输出选择二值字段 field:8ca10000000040008000000000000006。
3. 打开 Blob 调试窗口。
4. input.image 下拉切换到 output.gray-image。
5. 预期右侧结果立即显示 output.gray-image，正式 SelectedDisplayReference 仍保持二值字段。
6. 当前实际最终仍显示二值字段。

最近定向结果：

- Opening_the_debug_window_keeps_the_presented_result_workspace_untouched：通过
- Stored_image_variable_uses_the_same_frame：通过
- Blob_primary_image_edit_selects_the_new_roi_frame_without_replacing_the_saved_output_choice：失败
- 合计 2/3

相关实现文件：

- src/UI/Shell/Windows/MainWindowEditorDebug.cs
- src/UI/Shell/Windows/MainWindowRuntimeEditing.cs
- src/UI/Shell/Windows/MainWindowResultDisplay.cs
- src/UI/Shell/Windows/MainWindowViewModel.NativeResults.cs
- src/UI/Services/Images/ResultWorkspacePresenter.cs
- src/UI/Services/Editing/Nodes/RuntimeNodeConfigurationControl.cs

本轮已尝试但尚未证明完成的接缝：

- 编辑器图像字段变化接回 RefreshEditorDebugResult。
- ActiveEditorInputKey 和 ActiveEditorInputPortId 临时身份。
- ResultWorkspaceTransientInputChanged 直接通知结果工作区。
- pending 输入、ContextIdle 最终选择同步。
- Context.Deactivated 与窗口 Closed 分离清理。

## P1 待办清单

- 变量图像源完整 ROI 链：变量切换、单图帧、双图切换、PrepareInteraction、绘制提交、正式执行、保存重开和租约释放。
- ImageSource → BlobAnalysis 完整生产链：输入引用、灰度/彩色输出、ROI、执行结果、图像/二值图/轮廓/文本叠加、单图/双图、持久化和关闭释放。
- 正式 SelectedDisplayReference 与调试 SelectedImage 必须始终分离。
- 重新验证 Blob 三页、ROI 保存重开、上游失败恢复和 ShutdownLifecycleProbe。

## P0 推荐接续步骤

1. 先执行 git diff --check，确认没有临时日志。
2. 只加一次性事件日志，记录 ResultWorkspaceShell.SelectedOutputItem、ActiveEditorInputPortId、_plan、_refreshing、WPF ComboBox SelectedItem 回写顺序，日志写入 Release\_validation 后删除。
3. 核对 ResultWorkspaceShell.xaml 的 ComboBox 双向绑定、ItemsSource 替换默认选中行为、DependencyPropertyDescriptor 回调顺序和 ResultWorkspacePresenter.Refresh 的 finally 路径。
4. 用一个明确的调试临时 SelectedImage 状态替代多个恢复字段。正式 SelectedDisplayReference 只能在正式输出选择或提交边界写入。
5. P0 通过后再跑 P1，不要用局部通过宣称生态闭口。

## 构建与验证命令

    dotnet build tests/SunEyeVision.UI.Tests/SunEyeVision.UI.Tests.csproj -c Release --no-restore -m:1 -p:GenerateDocumentationFile=false -p:UseSharedCompilation=false
    dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore -m:1
    dotnet build SunEyeVision.slnx -c Release --no-restore -m:1

构建后使用同一 Release 目录执行 --no-build 测试，不使用旧 bin 或旧封装程序集。共享编译器锁定时使用 UseSharedCompilation=false，不要批量终止所有 dotnet 或 VBCSCompiler 进程。

## 人工验收和禁止事项

用户曾确认画布、小地图、双击调试窗口闪烁和拼接高度红框阶段项。P0 输入下拉到灰度图后的右侧联动仍未通过，修复后必须重新人工核对。

不要删除 C 盘旧项目、不要删除原厂 .pending 证据、不要恢复旧 SDK 兼容桥、不要把截图或单个编译通过写成完整闭口、不要在目标暂停期间调用 update_goal complete。

## 结束判定

只有 P0、变量 ROI、执行结果、持久化/保存重开、取消关闭、资源释放和相称回归全部有当前 Release 证据，并明确记录未实现和人工验收范围后，才能结束目标。
