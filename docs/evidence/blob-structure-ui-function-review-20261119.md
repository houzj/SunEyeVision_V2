# Blob 分析工具结构、UI 组装和功能复核

复核基线为 VisionMaster 4.4.0 Build20250516 原厂 XML、程序集清单和当前本方源码。底层算法库差异按既定规则允许，其他职责必须逐项对应。

## 当前证据结果

| 复核项 | 结果 | 证据 |
|---|---|---|
| 工具入口 | 已映射 | `BlobTool.cs` 提供 ToolId、输入输出、参数声明、结果声明和会话创建 |
| 参数声明 | 静态通过 | `BlobParameters.xml` 与 `BlobParameterDefinition.cs` 的字段、类型、默认值和范围校验一致 |
| 原厂类别 | 静态通过 | `Tab_ROI Area`、`Tab_Run Params`、`Tab_Check Result` 已写入字段布局 |
| 基本参数页顺序 | 静态通过 | `Verify-BlobBasicTabStatic.ps1` |
| 原厂 GroupLinkItem 消费者 | 静态通过 | `BlobGroupLinkMapping.json` 覆盖全部原厂链接，`Compare-BlobArchitectureStatic.ps1` 无未映射链接 |
| 参数页组装 | 部分对应 | `BlobUiComposition` 使用 `DeclaredSettingsView`、`DeclaredFieldsView` 和公共 Native 控件，但仍由 Blob WPF 入口编排页面行为 |
| ROI 组装 | 部分对应 | `BlobRoiUi` 使用 `NativeRoiUiDraftControls`、`NativeRoiInheritanceBinding` 和公共输入编辑器，原厂模板与事件顺序仍未确认 |
| 运行参数 | 部分对应 | `BlobRunUi` 只添加字段条件和原厂 XML 可证实的隐藏规则，完整控件模板仍待运行证据 |
| 结果显示 | 部分对应 | `BlobDisplayUi` 使用公共图形、文本和可见性声明，原厂结果树和显示事件顺序仍待运行证据 |
| 底栏命令 | 未确认 | 已有公共命令入口映射，原厂命令生命周期和关闭排空顺序缺少同版本运行证据 |
| 算法实现 | 部分对应 | `BlobAlgorithm` 在工具算法目录中使用 HALCON 完成算法实现，宿主仍通过 `ExecuteAsync` 接入；底层实现差异允许，参数语义和结果结构仍需生产链复核 |
| 生命周期 | 部分对应 | `BlobParameterEditor.Dispose`、`BlobUiComposition.Dispose` 和公共窗口释放链存在，原厂迟到回调与资源释放顺序未确认 |

## 当前工具源码边界

`SunEyeVision.Tool.BlobAnalysis` 负责工具入口、参数、输入输出、算法接缝、结果声明和工具资源。HALCON 通过 `SunEyeVision.HalAdapter.Runtime` 接入。

Blob 编辑器入口已并入 `SunEyeVision.Tool.BlobAnalysis` 单项目，程序集内保留 Runtime/Editor 两个组件声明。编辑器仍调用公共 `DeclaredSettingsView`、`DeclaredFieldsView`、`NativeRoiUiDraftControls` 和结果呈现器，专属组装职责还需要继续按原厂运行证据收回公共框架。

## 待修复和待确认

1. 用原厂运行证据确认 ROI、结果显示和底栏命令的模板与事件顺序。
2. 将已确认的公共组装职责从 Blob WPF 入口收回统一框架。
3. 保留 Blob 的工具差异声明和算法调用接缝，不改变 HALCON 适配层边界。
4. 对参数编辑、ROI 继承、结果显示、关闭释放和迟到回调执行生产链验证。

没有同版本原厂证据的行为继续标记为“未确认”。

## Agent Desktop 对照状态

2026-11-19 的自动化重试已成功采集原厂 Blob 工具窗口快照，路径为 `Release/_validation/AgentDesktopToolReview/blob-comparison-refresh/Blob分析1/vm-tool-window.json`。自研侧未在欢迎页找到方案 `11111`，本次没有形成有效的双侧对照。外层脚本已修正为检查子脚本退出码，避免将自研侧失败误记为成功。

随后通过自研程序启动参数直接加载 `.testdata/agent-desktop/blob-connected/blob-connected.solution`，已形成有效双侧快照。汇总文件为 `Release/_validation/AgentDesktopToolReview/blob-comparison-direct2/summary.json`，原厂窗口为 `Blob分析1/vm-tool-window.json`，自研窗口为 `product/20261119-042823-Blob分析1/tool-window.json`。

快照对照发现一项 UIA 结构差异。原厂窗口暴露 `Tab_Basic Params`、`Tab_Image Input`、`Tab_ROI Area`、`Tab_Run Params` 和 `ResultShow` 等原件身份，自研窗口泄露 `NativeParameterGroup`、`NativeParameterRow` 和 `NativeToolCommandItem` 的框架类型全名。两侧窗口尺寸均为 420×500，中文页签和底栏命令文本已出现，但自研公共控件的自动化名称和原厂原件身份尚未对齐。机器可读差异见 `.testdata/blob-agent-desktop-ui-diff-20261119.json`。

最终 Release 快照 `Release/_validation/AgentDesktopToolReview/blob-comparison-uia-final/` 已验证公共模板修复结果。自研窗口现在只暴露 `基本参数`、`图像输入`、`ROI区域`、`运行参数`、`结果显示`、`连续执行`、`停止执行`、`执行` 和 `确定` 等业务名称，类型全名和资源键名不再泄露。剩余窗口标题编号后缀差异不影响工具组装职责。

功能入口自动化已完成。`Invoke-SingleToolAudit.ps1` 使用 `blob-connected.solution` 成功打开 Blob 窗口、定位并点击 `执行`、生成执行结果快照、定位 `确定` 并关闭应用。证据目录为 `Release/_validation/AgentDesktopToolReview/blob-functional-final/`。结果状态和释放日志仍需从快照与运行日志中补充到最终生命周期清单。

生命周期回执已补齐。`close-app.json` 返回 `process_instance_exited` 且 `verified=true`，说明自研程序进程已确认退出。执行结果快照包含 `结果显示`、`执行` 和 `确定` 等窗口元素，但未提供可判定的算法成功/失败文本，因此算法结果状态继续标记为未确认。

## 第三方算法边界复核

当前门禁 `Verify-ToolThirdPartyBoundary.ps1` 已通过。Blob 与 ImageSource 的 HALCON 类型只出现在各自工具的 `Algorithms/` 算法实现目录，工具参数、结果、WPF 和公共框架不直接持有第三方类型。`HalconBlobAlgorithmContext` 只负责 Blob 算法调用期间的 HALCON 临时上下文和资源恢复，不能被解释为独立的宿主执行层。

## 2026-11-19 算法上下文改名后的 Release 复核

- `dotnet build SunEyeVision.slnx --configuration Release --no-restore --verbosity quiet -m:1` 通过，0 个错误，3328 个既有警告。
- Agent Desktop 双侧采集成功，汇总为 `captured-both-sides`。原厂快照位于 `Release/_validation/AgentDesktopToolReview/blob-comparison-post-algorithm-context/Blob分析1/vm-tool-window.json`，自研有效连线方案快照位于 `Release/_validation/AgentDesktopToolReview/blob-post-context-direct/20261119-050054-Blob分析1/tool-window.json`。
- 自研调试窗口已能读取 `基本参数`、`图像输入`、`ROI区域`、`运行参数`、`结果显示`、`连续执行`、`停止执行`、`执行`、`确定` 等业务名称，未再出现 `NativeParameterGroup`、`NativeParameterRow`、`NativeToolCommandItem` 或资源键名。
- 该复核只证明改名和 Release 产物没有破坏窗口组装。算法结果成功/失败文本、原厂动态参数回写和图像结果数据仍按原记录标记为运行未确认。

## 同一时点静态门禁

- 发布单元结构门禁通过，ImageSource 与 Blob 的原厂 Algorithm、AlgorithmTab、Display、ToolItemInfo、发布清单和目标清单均存在并被工程链接。
- Blob 基本页静态门禁通过，原厂首组 `ImageSourceGroup`、ROI 分组顺序、输入掩膜 `ROIAreaType/MaskImage` 声明和自研待处理锚点重放均通过。
- Blob 组装映射门禁通过，四个原厂类别均有自研映射，`unmappedVmGroupLinks=[]`，所有映射有消费者。原厂动态组装行为仍逐项标记未确认。
- ImageSource 字段映射门禁通过，25 项顺序和三类页签完全匹配。原厂保存件有一个 `SelecetedTips` 标签拼写错误，门禁只在内存解析视图中修正，原始证据文件保持不变，并记录 `sourceXmlRepairApplied=true`。

## 单项目启动门禁

- 共享程序集加载器已按完整 PE 声明集校验，再按 Runtime/Editor 过滤返回，修复 Runtime-only 过滤导致的“Component PE declarations differ”启动失败。
- Runtime 准入对同一程序集已声明 Editor 的桌面共享组件保留 WPF 依赖，纯 Runtime 程序集仍执行原有中性依赖检查。
- Release 由 Agent Desktop 启动后进入 `SunEyeVision · 起始页`。优雅关闭一次超时，强制关闭确认进程退出，未留下运行进程。

算法层可以直接调用 HALCON 算子，`SunEyeVision.HalAdapter.Runtime` 负责类型转换、所有权、初始化和释放。当前边界门禁已通过，Release 可编译仍不代表算法结果语义和生命周期已完成原厂运行对照。
