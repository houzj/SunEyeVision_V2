# AI 协作交接单：ROI 形状绘制按钮不可点（Blob 调试窗口）

- 基线提交：7339632e0be48f958d99ba03981106badcca325c（工作区在其上有**未提交**阶段性修改，见"已修改文件"）
- 当前分支：feature/param-window-datatree-codebuddy
- 当前执行者：CodeBuddy → 交由 Codex 接手修复
- 工作区：E:\SunEyeVision_V2（主仓，勿在其它 worktree 修此问题）
- 任务目标：Blob 调试窗口参数页 ROI 形状绘制按钮（矩形/圆/圆弧/方/六边形/屏蔽区）在正式程序中**全部点不动**；按原厂交互基线修复，并补"不可绘制必须给用户反馈"
- 允许修改目录：src/UI、src/Plugin.Wpf、tools/SunEyeVision.BlobAnalysis、tests/、docs/collaboration
- 临时提交要求：接手前先把当前工作区按用途做成中文临时提交（见"已修改文件"清单），禁止 git reset --hard、禁止丢弃/覆盖他人改动

## 缺陷事实（用户人工确认，2026-11-13）

1. ROI 页所有形状按钮点击无任何反应（截图显示绘制模式已选中、矩形按钮呈高亮态，屏蔽区 ⊘）。
2. **关键证据：`Release\_validation\roibtn.log` 在用户点击后不存在**。已埋的三处探针（`BlobLocalRoiOwner.Activate` 入口/after-acquire、`ResultWorkspacePresenter.RoiLogging BorrowRoiFrame` 拒绝分支与 borrow-check、`MainWindowRuntimeEditing` 宿主借帧门槛）**一行都没写**。
3. 结论：点击**从未到达** `BlobLocalRoiOwner.Activate` 及更深的借帧链——失效点在 WPF 命令/IsEnabled 层，即 `NativeRoiEditor` 的命令门控或 `NativeRoiUiDraftControls` 按钮绑定层，先于业务借帧。

## 命令门控分析（Codex 排查入口）

`NativeRoiEditor.cs`（src/Plugin.Wpf/Presenters/Regions/）：

- 绑定会话时（Context 构造，约 :370-379）：`ValueChangedCommand.CanExecute = !_disposed && !_projecting && !_committing && !_suspended && IsLoaded && uiGeneration==_uiGeneration && generation==_generation && ReferenceEquals(_session,session) && session.IsCurrent`。
- 未绑定会话时（UnboundContext，约 :336-343）：CanExecute = `!_disposed && !_projecting && !_suspended && IsLoaded && generation==_generation`，执行体调用 `PrepareInteraction?.Invoke()`（→ `BlobLocalRoiOwner.Activate`）。
- `NativeView.IsEnabled = session?.IsCurrent==true || session is null && PrepareInteraction is not null`（Project，约 :145）。
- `_suspended` 只在 `OnLoaded` 的 ContextIdle `_resume` 回调里清 false（约 :155-179）；`_generation` 在 Bind/Dispose/RetireFrame 时自增。

**最可能根因（按概率）**：
1. `_suspended` 卡 true（`OnLoaded` 的 ContextIdle `_resume` 未执行/被 Abort/编辑器重建后 Loaded 未再触发）；
2. 会话已绑定但 `session.IsCurrent=false`（借帧租约失效后未重借），Context.CanExecute 恒 false 且**无任何用户反馈**；
3. `_generation`/`_uiGeneration` 闭包失配（BindDraft 重建行/组合器后 editor 实例被换，旧闭包命令失效）。

注意：探针三处埋点分别在 `BlobLocalRoiOwner.cs`（Trace helper + Activate 两行）、`ResultWorkspacePresenter.RoiEditing.cs`（borrow reject + borrow-check）、`MainWindowRuntimeEditing.cs`（hostborrow，注意已改名 ownerProbe 避免 CS0136）。这些是**临时诊断代码，修复后必须删除**。

## 原厂交互基线（必须对齐，锚点见差异矩阵）

- 交互全量差异矩阵：`docs/09-现行文档/图像结果与ROI绘制交互原厂对照-20261113.html`（A1-A8 图像结果工作区 8 条 + B1-B5 ROI 绘制 5 条，每条带 *.pending 行号锚点）。
- 证据 JSON：`docs/collaboration/evidence/VM编辑器相关输入单图联动原件对照-20261113.json`（含用户人工反馈两条）。
- 关键原件语义：
  - 绘制门槛 = `IsCanDrawROI`（ModuleDisplayDecorator.cs.pending:446-480，当前显示图 ∈ 模块 IMAGE 输入关联 IO）→ `SetRoiVisible`（ModuleResultView:2252-2275）；
  - **不可绘制时点击图像弹 `RoiDrawTips` 警告框**（ModuleResultView:2514-2521）——本方目前静默 no-op，必须补用户反馈；
  - 刷新后对既有 ROI `UpdateROI` 重投影（:604-608），不销毁会话；拖拽中（IsDragROI）跳过；
  - 区域/参数编辑只重绘结果，从不终止模块会话（P_ValueChanged → UpdateModuleViewResultShow）。

## 已修改文件（本批未提交，Codex 接手前先临时提交）

- src/UI/Services/Images/ResultWorkspacePresenter.cs（单一调试显示态 + ResolveOutputChoice + DisplayUnchanged 共用解析 + 帧身份比较变量图像 + 空/无输入中间计划不清视口 + RetireRoiPresentation 移入重建分支）
- src/UI/Services/Images/ResultWorkspacePresenter.RoiEditing.cs（变量租约谓词按帧锚定 + 临时借帧拒绝探针）
- src/UI/Shell/Windows/MainWindowEditorDebug.cs（EditorRelatedImage 单态替代 ActiveEditorInputKey/PortId）
- src/UI/Shell/Windows/MainWindowRuntimeEditing.cs（窗口关闭清临时态 + 宿主借帧探针）
- src/UI/Shell/Windows/MainWindowViewModel.NativeResults.cs（删除 ResultWorkspaceTransientInputChanged；删除正式像素压制 retainFormalImage→恒 true）
- src/UI/Services/Editing/Nodes/RuntimeNodeConfigurationControl.cs（输入页写回通知：IMAGE 输入触发 _debugResultChanged，不再失效调试会话）
- tests/SunEyeVision.UI.Tests/Session/EditorDebugSessionTests.cs（P0 断言翻转为原件基线 + 新增 Explicit_display_choice... 用例）
- docs/collaboration/CURRENT_TASK.md、evidence/VM编辑器相关输入单图联动原件对照-20261113.json、docs/09-现行文档/图像结果与ROI绘制交互原厂对照-20261113.html

## 已完成（有当次 Release 证据）

- P0 输入下拉→单图联动：`Blob_primary_image_edit_selects_the_new_roi_frame_without_replacing_the_saved_output_choice` 通过（原失败）。
- 显式改选空白：`Explicit_display_choice_shows_the_selected_modules_formal_pixels_while_the_debug_window_is_open` 新用例通过。
- 变量 ROI 同帧：`Stored_image_variable_uses_the_same_frame...` 通过（租约谓词按帧锚定修复）。
- 定向合计 9/9（含 Blob 三页 5/5）；`Confirmed_blob_roi_threshold_and_display_reopen...` 通过；`ShutdownLifecycleProbe wait` 通过；全量 Release 0 错误。
- 全量差异矩阵归档（见上）。

## 未完成 / 本交接要修的

1. ROI 形状按钮不可点（本单核心）：按"命令门控分析"逐项埋点定位（建议在 `NativeRoiEditor` 的两个 CanExecute 与 `_suspended`/`_resume` 生命周期加一次性日志，写 `Release\_validation\roibtn.log`），修复后删除全部探针。
2. 补 A8/B4：不可绘制时给用户明确反馈（对齐 RoiDrawTips 弹窗语义，形式可用状态条/弹窗），禁止静默 no-op。
3. 借帧全路径帧锚定复核（publication 非临时路径租约仍锚定 `ActiveOwner()`，重建后应能立即重借同一帧）。
4. 既有失败甄别：`ImageSourceSessionTests.Camera_acquisition_resolves_exposure_and_gain_from_the_formal_upstream_result` 报"普通参数来源必须属于当前流程的控制前序"（CandidateInputValidation.cs:58，不经过本批改动文件，疑似存量）。
5. A4 双图入口差异（原件强制 [0] vs 本方保留当前）待用户确认后统一。

## 已执行构建与测试

- `dotnet build SunEyeVision.slnx -c Release --no-restore -m:1` → 0 错误。
- 定向：`dotnet test tests/SunEyeVision.UI.Tests/... -c Release --no-build --filter ...` → 9/9、ROI 相关 10/10。
- 共享 bin 规矩：全量构建后立即 `--no-build` 跑测试；编译器锁用 `-p:UseSharedCompilation=false`，不要批量杀 dotnet/VBCSCompiler；用户程序占用 Release 时等用户退出，不擅杀进程。

## 人工验收结果（2026-11-13，用户）

- 调试窗口内切换 input.image 下拉 → 右侧联动正常 ✅（用户确认"感觉正常了"）。
- 主窗口下拉显式选"Blob图像数据" → 修复后待用户复验（合同用例已过）。
- ROI 形状绘制按钮 → ❌ 全部不可点（本单核心，探针证明点击未进入业务层）。

## 已知风险或禁止改动

- 禁止删除 src/Plugin.Wpf/Controls/Native/Input/WorkspaceControls/ 下 *.pending 原厂证据。
- 禁止恢复旧 SDK 兼容桥、禁止双轨实现、禁止以"编译通过/局部通过"宣称闭口。
- 修 ROI 时不得破坏已对齐的 A2/A3/A5/A6/B3/B5 六条（定向 9 条为回归红线）。
- C 盘旧项目、用户运行中的进程：不动、不杀。
- 目标未闭口前不得 update_goal complete；Git 提交标题正文用中文。

## 追加（2026-11-13 晚）：字号红框实锤根因（与拼接高度同族，float 路径）

探针证据（`Release\_validation\numeric-dps.txt`，测试 `ZzNumericProbeTests.PROBE_numeric_range_dps`，Blob 调试窗口切到结果显示页）：

- 字号行（t.size，声明 Number 默认 10、范围 6-72，BlobResultDisplay.cs:64）：页面加载后 `ValueText.Text == "100.00"`，RangeRule 报 `[Please enter an age in the range: 6 - 72]` → 红框。**字段文本被滑条路径写成了 100.00**，红框本身是越界文本的正确反馈，错在值被写坏。
- 机理：float 行滑条为单值模式，`SliderArrangeBase.EndValueF` setter 在 `!isBetween` 分支直接 `SetValue(EndValueFProperty, Maximum)`（SliderArrangeBase.cs:425-441，Maximum 默认 100）；`NativeNumericCommit` 绑定 `EndValueProperty → OnEndValueChange → OnOwnerValue`（NativeNumericCommit.cs:71）把该值推进文本/值链路。
- 这与已修的拼接高度（提交 090211fa8，Integer 行 `MaxChanged` 未同步 `EndValueF`）**同族**：int 行修了，float 行（SliderArrangeFloat）没修。
- 用户实机另一处红框（查找个数=100，范围 1-20000，值本身合法）在测试夹具中未复现（该行无校验错误），疑似为字号 100 写坏后的连锁残留或用户会话中的残留态；修好 float 推值后请用户复验，若仍红再单独取证。
- 修复方向：float 单值模式与 int 行同规则——`EndValueF` 必须同步当前 `EndValue`（字段当前值），任何情况下不得把滑条 `Maximum` 默认值推进值链路；修复后补一个"结果显示页加载后字号文本保持默认 10、无校验错误"的合同用例（可仿 `ParameterTemplateInteractionTests.Integer_row_default_above_hundred_keeps_no_validation_error_after_range_binding`）。
- 注意 `EndValueF` 的 `!isBetween→Maximum` 写法源自原厂反编译原件（AppsXmlUi 原件行为），修复应在本方组合层（commit 绑定/协调器）拦截，不改原件语义原件文件。

## 下一步（建议顺序）

1. 临时提交当前工作区（中文 message，按用途拆分）。
2. 在 `NativeRoiEditor` 命令层加一次性日志定位 `_suspended/IsCurrent/generation` 哪个门卡住（用户点一次即可判定）。
3. 按根因修复 → 删全部探针 → 补"不可绘制反馈"（RoiDrawTips 对应物）→ 定向 9 条 + ROI 回归 + 全量 Release → 交用户人工验证绘制。
4. 完成后更新 CURRENT_TASK.md 顶部摘要与本单结论，历史只追加不改写。
