# ImageLoad / BlobAnalysis 当前同构差异审计

日期 2026-11-19

本表先于后续代码修复使用。任何未有 VM 4.4.0 静态程序集、模板/资源或运行截图证据的行为，保持“未确认”，不以自研测试通过代替原厂事实。

证据基线

- VM 原厂窗口截图：`docs/evidence/vm-image-source-dialog-20261118.png`、`docs/evidence/vm-blob-dialog-run-tab-20261118.png`、`docs/evidence/vm-blob-dialog-current-20261118.png`
- VM 逆向结论：`docs/evidence/VM同构UI完整链路差异与优化方案-20261118.md`
- Blob 参数声明和原厂映射：`docs/collaboration/acceptance/D2-声明对照-BlobAnalysis.md`
- 当前自研入口：`src/UI/Services/Editing/Nodes/RuntimeToolEditorWindow.cs`、`RuntimeNodeConfigurationControl.cs`、`src/Plugin.Wpf/Presenters/Parameters/DeclaredSettingsView.cs`、`tools/SunEyeVision.BlobAnalysis/Wpf/Views/BlobUiComposition.cs`

| 链路层 | VM 原厂证据 | 当前自研实现 | 已确认差异 | 处理结论 |
|---|---|---|---|---|
| 窗口根 | `BaseWindow`、`AlgorithmTabRoot`、底栏命令和释放由同一窗口事务持有 | `RuntimeToolEditorWindow`、`NativeToolEditorFrame`、`RuntimeNodeConfigurationControl` 分担 | 窗口关闭完成信号与调试资源排空不是同一根事务 | 先建立窗口根契约，再改实现 |
| 参数根 | `ParamListView`/原厂页签按 declaration 和页面映射组装 | `DeclaredSettingsView` 动态生成页签和字段行 | FlatNative 迁移后工具又重复挂载正式字段，曾触发重复 Visual | 公共层保留唯一字段 owner，工具只声明原厂接管集合 |
| 图像来源 | VM ImageSource 窗口为独立原厂 Tab/Group/模板链 | ImageLoad 由宿主接缝提供 `ImageSourceDraftContent` | 当前尚未完成每个模板、资源键和交互事件逐项映射 | 未闭合，不继续凭外观补丁 |
| Blob 基本参数 | VM `Tab_Basic Params` 下有图像输入、ROI 区域、位置修正 | Blob `BlobUiComposition` 通过 ROI/辅助控件组装 | 之前存在输入页与工具 ROI 两套所有权链 | 已改为工具声明接管字段，仍需按原厂页签截图复核 |
| ROI | 原厂 `BlobFindRoiManager` 管理模式、区域、来源和写回 | `BlobRoiUi`、`NativeRoiUiDraftControls`、`BlobLocalRoiOwner` 分担 | 组装所有权和 ROI session 生命周期尚未完全收拢 | 需先做差异矩阵，再合并为单一 session |
| 运行参数 | VM `Tab_Run Params` 使用原厂声明模板和底栏命令 | `BlobRunUi` 复用声明字段并挂条件 | 静态字段映射部分已有证据，模板状态触发器尚未逐项闭合 | 以原厂 XML/资源键核对后批量修复 |
| 结果显示 | VM `ResultShow` 与结果树/图像叠加由原厂显示根拥有 | 自研 `IToolResultView`、ResultWorkspace、结果 reader 分担 | 结果 reader 与关闭释放的所有权边界仍有差异 | 先确认原厂 reader/关闭动态证据，再改 |
| 执行会话 | VM 模块会话、输入管理器、结果管理器串联 | `ProductionEditorDebug`、`DocumentDebugSession`、`DeclaredOutputSnapshot` 串联 | 关闭调用方可观察到窗口已关闭但结果资源仍在释放 | 关闭完成必须绑定 session drain 和资源释放完成 |
| 关闭/释放 | 原厂窗口事务统一退订、排空、释放 | 多个对象分别退订和 Dispose | 历史审计时生命周期用例曾失败；2026-11-19 当前 targeted regression 已通过，原厂动态顺序仍未确认 | 暂停局部测试修复，先对照原厂生命周期证据 |

当前代码改动中，`ToolOwnedInputFields`、`ContainsView` 和 Blob 输入接管属于已确认的字段所有权差异修复。`RuntimeToolEditorWindow` 的关闭 drain 加固属于实现修复，尚未有同版本 VM 动态证据，暂标“待原厂确认”，不能据此宣称完全同构。

后续顺序

1. 用 REA 固定 VM 4.4.0 的窗口根、参数根、Blob ROI manager、结果显示和关闭释放证据。
2. 将 ImageLoad 与 Blob 的自研调用链逐层映射到证据，形成可审查的类型、模板、资源键、事件和 owner 表。
3. 先落地公共窗口根和唯一字段 owner，再批量迁移两个工具的声明和组装。
4. 只有差异表闭合后，才跑完整回归、Release 和 Agent Desktop 验证。

