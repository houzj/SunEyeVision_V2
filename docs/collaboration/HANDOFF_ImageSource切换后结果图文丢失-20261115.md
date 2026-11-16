# AI 协作交接单

- 基线提交：`6de0ea3cff760827293f79ff76a4e5d4b2faf2d5`（工作区存在未提交修改，交接内容对应当前工作树）
- 当前分支：`feature/param-window-datatree-codebuddy`
- 当前执行者：Codex → CodeBuddy
- 任务目标：修复 ImageSource 图像源切换后“图像和当前结果表仍在，但 Blob 图形与文本显示被清空”的问题，并保持 VM 原厂状态边界：图像源游标/自动切换/运行模式/缩略图切换不重新执行、不清除上一轮正式结果；正式执行、节点/方案身份变化才替换或清理结果。
- 允许修改目录：`src/UI/Services/Images/`、`src/UI/Shell/Windows/`，必要时同步 `src/Plugin.Wpf/Presenters/Display/`；不要删除或重写其他未相关改动。所有修改需先核对 VM 证据和完整生命周期。
- 已修改文件（本问题相关）：
  - `src/UI/Services/Images/ImageSourceProjectionBinding.cs`
  - `src/UI/Services/Images/ResultWorkspacePresenter.cs`
  - `src/UI/Services/Images/ResultWorkspacePresenter.Display.cs`
  - `src/UI/Shell/Windows/MainWindowDocumentHistory.cs`
  - `src/UI/Shell/Windows/MainWindowEditorDebug.cs`
  - `src/UI/Shell/Windows/MainWindowResultDisplay.cs`
  - `src/UI/Shell/Windows/MainWindowResultPublication.cs`
  - `src/UI/Shell/Windows/MainWindowViewModel.NativeResults.cs`
- 已完成：
  - 依据 VM 证据确认图像源策略编辑不应触发结果执行或结果清空。
  - 图像源接受入口统一设置结果显示保留标志，策略编辑前续租当前完整结果快照。
  - 保留编辑前的 `ResultDisplayDescriptor`、`ResultDisplayAppearance` 和几何快照，避免节点配置对象替换后旧结果图文被判失效。
  - 修复输出视图来源切换前无条件 `pane.ClearDisplay()` 的直接清理点：输出背景帧切换保留 DisplayScene，等待新帧回执后再原子替换。
  - 对来源重绑期间的短暂显示描述不可用增加保留路径；正式执行/发布退役/节点或方案切换仍允许清理。
  - 相关探针已删除；当前正式源码无本问题探针。
- 未完成：
  - 尚未取得本次最终载荷在真实 UI 中的人工复验截图；上一版用户截图仍显示图像源切换后图形/文本消失，不能把历史构建通过当作行为验收。
  - 需要确认“图像源1·输出图像”和 Blob 输出之间来回切换时，Blob 图形、文本、当前结果表始终保留；还要覆盖自动切换、运行模式、运行全部、缩略图切换。
  - 需要确认正式重新执行后结果整体替换，切换节点/方案或关闭重开后按身份正确清理。
- 已执行构建与测试：
  - 全量 Release 命令：`dotnet build-server shutdown; dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GenerateDocumentationFile=false`
  - 最近一次构建：成功，0 错误，1 个既有 `log4net NU1902` 警告。
  - 定向测试：`ResultPresentationPresenterTests` 38/38 通过（在当前场景直接清理修复前后均通过，不能替代 UI 验收）。
  - 最近一次正式载荷目录：`Release/SunEyeVision_v1.0.0/SunEyeVision`
  - 最近一次载荷 SHA-256：`DFCE3CED92BE513DF30A8CEBD31091B97E5D6032ADB7C979443B9D13E2846037`
  - `SunEyeVision.UI.dll`：`061E7EF89F950C9E4314A45556CCF3F0C6149D421F96EEC792C53A3D90427BCC`
  - `SunEyeVision.Tool.ImageSource.Runtime.dll`：`B65D767274BB0397CCF3A66BB30F28851F24F3EAE90D80F2D5CF96F6D2CA6DDD`
  - `SunEyeVision.Tool.BlobAnalysis.Runtime.dll`：`73F0C7980E24BD57F3549D947251935FADF46C1646C6FC32829676FF0F1C55E3`
  - `SunEyeVision.UI.exe`：`D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`
- 人工验收结果：上一版本用户已复现失败；本轮直接清理点修复后尚未人工验证。当前不要宣称完成。
- 已知风险或禁止改动：
  - 不要通过“永不清理”掩盖问题：正式执行结果发布、节点/方案身份切换、文档关闭仍必须释放旧 publication、DisplayScene 和 snapshot lease。
  - 不要把图像源游标切换当成执行；参数/ROI 改动不执行时应保持上一轮结果。
  - 不要删除现有 VM 证据或把结果表、图像背景、结果图文层混成一个生命周期。
  - 不要新增长期探针进入正式载荷；需要诊断时使用临时日志并在构建前移除。
  - 当前工作区有大量历史未提交修改和未跟踪证据/构建目录，禁止使用全量 reset/clean 覆盖他人工作。
- 下一步：
  1. 先读取 `docs/collaboration/CURRENT_TASK.md` 顶部摘要、`docs/09-现行文档/图像结果与ROI绘制交互原厂对照-20261113.html` 和相关 VM 证据。
  2. 用最新 Release 复现：执行 ImageSource→Blob，记录图形/文本/表格；切换“图像源1·输出图像”、Blob 输出、自动切换、运行模式、运行全部、缩略图。
  3. 若仍清空，沿 `ResultWorkspacePresenter.Present → ResultImagePane.TryPresent → NativeResultViewport.BindImage/Clear → QueueResultDisplay → CaptureCurrentResultDisplay` 记录哪一步清理/返回 null；重点检查旧 DisplayScene 的 `Owns()` 是否因旧 receipt 或 publication currentness 被判失效。
  4. 修复必须覆盖完整生产链：创建/执行、图像源策略编辑、结果图文重绘、当前结果表、正式执行替换、节点/方案切换、关闭释放。
  5. 重新做全量 Release，更新 `CURRENT_TASK.md` 和本交接单，最后提供新的载荷哈希与人工验收结果。

