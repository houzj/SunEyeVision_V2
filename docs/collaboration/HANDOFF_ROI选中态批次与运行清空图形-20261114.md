# AI 协作交接单：ROI 绘制生态链（选中态批次收口 + 运行后交互图形清空）

- 基线提交：`6de0ea3cf`（文档）/ 最后代码提交 `1971f83a4`；未提交改动仅 1 个文件（见下），接手后**先按用途提交该改动固定基线再开工**。
- 当前分支：`feature/param-window-datatree-codebuddy`
- 当前执行者：Codex（CodeBuddy 移交，用户已确认 Codex 恢复接手）
- 任务目标：ROI 绘制生态链按原厂（VM）行为基线逐条对齐。工作流 = 修一批 → 全量 Release 部署 → 给用户一条验证清单 → 用户实机回 ✅/❌（附截图）→ 新差异实锤后成片修复。**以用户实机截图为语义最终裁决，不做无原证的自行推导。**
- 允许修改目录：`src/UI/`、`src/Plugin.Wpf/`、`tools/SunEyeVision.BlobAnalysis/`、`tests/SunEyeVision.UI.Tests/`、`docs/collaboration/`。禁区见"已知风险或禁止改动"。
- 已修改文件（未提交，构建 0 错误、已部署、**用户人工验证通过**）：
  - `src/Plugin.Wpf/Presenters/Regions/NativeRoiEditor.cs`
    1. owner 新增 `internal string? UnboundShape {get;set;}`（约 :74-76）：阻断态（无 session）形状按钮选中态的单一事实源。根因是借帧失败 → `Bind(null)` 每次重建 `UnboundContext`，旧实现形状属性全空（`BoxA{get=>false;set{}}`），选中态即丢。
    2. `UnboundContext` 改为实现 `INotifyPropertyChanged`（约 :357-395）：四个形状属性（BoxA/SectorA/SectorRingA/PolygonA）以 `owner.UnboundShape` 为单一事实源、radio 互斥（kind 取值 "rectangle"/"sector"/"annulus"/"polygon" 与 session 侧 `ActiveDraw.Kind` 一致），setter 变化时通知全部四个属性。`FullScreenA` 维持原样（绑定 `UnboundFullDomain` 覆盖状态，不属于形状组）。
    3. `Bind()` 中 `if(session is not null)UnboundShape=null;`（约 :93）：真实 session 绑定后由 session 的 `ActiveDraw` 接管形状视觉权威。
- 已完成（近三批，均已提交、部署、用户验证）：
  1. `8c9fbacbb` 阻断语义终定案（用户 VM 实机截图：显示图≠输入图时点形状按钮，VM 弹"无效的绘制操作！请检查绘制图像是否为模块的输入图像"，用户手动切换）+ 显示来源下拉按模块序号（GlobalIndex）排列、条目带模块号前缀、掩膜回组内。
  2. `1971f83a4` 两段式交互：按钮点击只选中不弹框；提示延迟到图像落笔。通道链 = presenter 视口 `PreviewMouseLeftButtonDown`（仅 `RoiDrawBlockReason` 非空）→ `NotifyRoiInputViewportClicked`（主窗口）→ 调试控件 → `NativeRoiFrameSource.InputViewportClicked` → `BlobLocalRoiOwner.OnInputViewportClicked` 重试激活，仍阻断才弹 `ShowInteractionFeedback`（已开放 public——**Plugin.Wpf 无对工具/UI 程序集的 InternalsVisibleTo，跨程序集成员必须 public**）。
  3. 未提交批：阻断态按钮选中样式保持（见上）。
- 未完成（**Codex 主任务**）：
  1. **点击"运行"后可交互图形被清空（用户新实锤，附截图：Blob 调试窗口显示"0 1 图像源1 · 图像"，输入图上圆 ROI 可见，点击运行后交互图形清空）**。未定位、未修。初步排查线索（未验证，勿当结论）：
     - 运行 → 新帧发布 → 视口 epoch+1 → 租约按回执全等锚定（`ResultWorkspacePresenter.RoiEditing.cs`，f5a9bf36b 定案）死亡 → `BlobLocalRoiOwner.RefreshFrame`（:134-150）退役；重建条件 `_controls.IsVisible&&_controls.IsLoaded&&_sessions.Count==0&&IsLocalDrawing`，重建走 `AcquireFrame(interactive:false)` → `_source.Borrow(BlobTool.ImageInput,false)`。
     - 需核对：①运行后 Borrow 是否成功（显示页签已是图像源 1，理论上应命中输入图）；②已被用户绘制的形状是否已提交进参数值（`Accept` 链写 `input.roi.*`）——提交过的形状退役后应经 `Read()` 参数值随重建恢复（4364645ea 已修"切图丢形状"，本缺陷疑似同族新路径）；③是否存在"显示页签=图像源1 但借帧按模块输入图判定不一致"的来源 Key 错位；④`RetireFrame` 后 `_editors.Bind(null)` 进 Unbound 态，若重建借帧失败则画布无形状且无提示——与 P3 反馈语义是否冲突。
  2. 遗留登记项：下拉"显示X"旧本地化命名链（`zh-cn.xaml:5250-5251` Display Blob Image / Display Binary Image 键）待定位具体下拉面对齐原厂命名；EditorDebugSession 16 条存量失败甄别；变量 ROI 显示位（P4 订阅链）。
- 已执行构建与测试：
  - `dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore` → 0 错误，产物已部署 `E:\SunEyeVision_V2\Release\SunEyeVision_v1.0.0\SunEyeVision\`。
  - EditorDebugSession 52 条基线 = **16 失败 / 36 通过（存量基线即如此，零回归判据）**。本批未跑测试（用户指示跳过，改人工验证）。构建/测试前确认用户程序已退出（Release DLL 被占用会 MSB3027，测试跑旧产物不算数）。
- 人工验收结果：
  - ✅ 用户确认：阻断态形状按钮选中样式保持（本批未提交改动）。
  - ✅ 此前确认：两段式交互（按钮只选中、落笔才弹框）。
  - ❌ 用户新报：点击运行后可交互图形清空（主任务）。
- 已知风险或禁止改动：
  1. **禁区：显示刷新→ROI 的跨路径通知**（5242612b7 实锤回滚）。曾试"呈现完成通知"形成反馈死循环 UI 冻结；呈现等待只能在调用栈内有界进行（800ms 上限）。
  2. **不得恢复"一击命中自动切换显示"**（5242612b7 已按用户实机截图定案撤销，现行为 = 阻断 + 原厂文案提示 + 用户手动切换）。
  3. VM原件优先门禁（CURRENT_TASK.md:89，持续有效）：任何代码修改先登记同版本 VM 原件证据（仓内 `*.pending` 反编译保留源）+ 当前消费者 + 差异 + 决定 + 验收出口；无原证行为只能标"运行未确认"。原厂基线 6 条见 `docs/09-现行文档/ROI完整生态链逆向对照与优化方案-20261113.html`。
  4. rule-015：不可绘/借帧失败必须给用户可见反馈（RoiDrawTips 等价语义），禁止静默 no-op 或静默回退。
  5. 不留兼容层/双轨实现（用户明令不考虑旧方案兼容）；删除即删干净，无消费者不留死代码。
  6. 工作区有未跟踪文件 `anti-flicker-wip.patch`——旧首版参考，**不得套用**。
  7. 提交规范：中文 message（Conventional Commits），经 UTF-8 文件 `git commit -F` 提交，禁止命令行参数传中文；每阶段独立提交可回退。
  8. 修改工作流执行、连续运行、取消、共享图像空间、Plugin.SDK、插件加载或发布规则前，先评估跨模块影响（AGENTS.md 架构边界）。
- 下一步：
  1. 提交 `NativeRoiEditor.cs` 未提交改动（用途：阻断态形状按钮选中态跨 Bind(null) 保持，用户已验证），更新 `docs/collaboration/CURRENT_TASK.md` 顶部账本与交接入口。
  2. 按"未完成 1"定位运行清空图形根因：建议先在实机复现时区分"会话退役未重建"与"重建成功但投影丢失"两种情形（可用临时日志或断点，采证后删除），再按原件 UpdateROI 语义（ModuleResultView.xaml.cs.pending:604-608）成片修复，不打局部补丁。
  3. 修完 → 全量 Release 0 错误 → EditorDebugSession 52 条对照 16F/36P 基线 → 部署 → 给用户验证清单（建议单条：绘制形状 → 点运行 → 图形保持可交互；补充：运行后切页/切图/关窗不丢形状回归）。\n\n## 2026-11-14 后续接续：ROI 命中换帧竞态\n\n正式运行日志曾记录 `BlobLocalRoiOwner.Activate → NativeRoiFrameLease.BeginEditing → NativeResultViewport.BeginRoiEditing` 因借帧后视口换代而抛出物理帧不一致异常。当前实现已在 `ActivateCore` 识别帧/会话失效时退役旧会话，并由 `Activate` 重新借当前视口帧后限次重试。真正参数错误继续保留原异常路径。全量 Release 构建 payload 为 `2B2209C30B6A499E0B57924246D0920888D6ED9F56387EA510D6D906723218C4`，未运行自动化测试，待人工验证。
