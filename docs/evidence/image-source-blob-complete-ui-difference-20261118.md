# ImageLoad 与 BlobAnalysis 原厂/自研完整 UI 对照

取证日期：2026-11-18  
原厂版本：VisionMaster 4.4.0 Build20250516  
范围：ImageLoad（原厂 ImageSourceModule）与 BlobAnalysis（原厂 IMVSBlobFindModu）。其他工具不纳入本轮。

## 证据身份

| 证据 | 位置 | 支持范围 |
|---|---|---|
| 原厂 ImageSource 静态 UI 资源 | `docs/07-工具逆向/tools/imagesourcemodule/resources.json`、`ui-preview.json` | 单页 `Tab_Basic Params`，控件别名、三组分区和原厂资源哈希 |
| 原厂 ImageSource 反编译 | `docs/07-工具逆向/decompiled/ModuleControls/ImageSourceModuleControl/ImageSourceModuleControl\\ImageListPanel.xaml`、`LocalImageListView.xaml`、对应 `.cs` | 图像缩略图条、计数、运行模式、自动切换、订阅文件夹和参数回写 |
| 原厂 Blob 静态 UI 资源 | `docs/07-工具逆向/tools/imvsblobfindmodu/resources.json`、`ui-preview.json`、`evidence.json` | 三页、9 个共享组、94 个控件别名、结果显示声明和装配边界 |
| 原厂 Blob ROI 静态源码 | `docs/07-工具逆向/decompiled/Managed/BlobFind/IMVSBlobFindModuCs/BlobFindRoiManager.cs` | 统一 `RoiManager`、输入身份、模式切换与 ROI 写回 |
| 原厂运行观察 | Agent Desktop snapshots `s2evwp4zmlozh`、`s1nfbghm4r19v0`、`s3irk1m39le2f4`，截图 `vm-image-source-dialog-20261118.png`、`vm-blob-dialog-run-tab-20261118.png` | 实际窗口大小、页签、分组、底栏命令和可见层级 |
| 自研入口 | `src/UI/Services/Editing/Nodes/RuntimeToolEditorWindow.cs`、`src/Plugin.Wpf/Controls/Native/Composition/Parameters/NativeToolEditorFrame.xaml` | 宿主窗口、页签承载、底栏命令、关闭排空和释放 |
| 自研工具入口 | `tools/SunEyeVision.ImageSource/Wpf/Views/ImageSourceParameterEditor.xaml(.cs)`、`tools/SunEyeVision.BlobAnalysis/Wpf/Views/BlobParameterEditor.xaml(.cs)`、`BlobUiComposition.cs` | 两个工具的声明页、输入组装、ROI、结果页和生命周期接缝 |

## 差异清单

状态定义：`已对齐` 表示代码与证据一致，`缺口` 表示本轮需要修复，`未确认` 表示证据不足不得自行实现。

| 层 | 原厂证据结论 | 自研现状 | 状态 | 本轮处理 |
|---|---|---|---|---|
| 窗口 | 两个工具均为 420×500；Blob 标题为“2 Blob分析”，ImageLoad 标题为“0 图像源” | `RuntimeToolEditorWindow` 中文宽 420、高 500 | 已对齐 | 保留，并加入自动回归断言 |
| 宿主生命周期 | 原厂窗口关闭时释放页根和连续执行委托；Blob 底栏含连续执行、执行、确定，运行页另有重置参数 | `RuntimeToolEditorWindow` 统一管理命令、关闭排空和 `Dispose` | 已对齐 | 用生命周期测试验证关闭、迟到回调和释放 |
| ImageLoad 页签 | 单页 `Tab_Basic Params` | `DeclaredSettingsView` 单页 | 已对齐 | 保留声明驱动页根 |
| ImageLoad 分组 | 原厂运行观察为 `Tab_Basic Params`、`Tab_Trigger`、`Tab_Subscribe_File` 三个组；静态控件包含 `ImageSourceType`、`CurrentImagePath`、`PixelFormat`、`AutoPlay`、`AutoStop`、订阅文件夹及刷新/触发 | `ImageSourceFields` 已有字段和条件，但图像列表视觉由宿主输入内容接缝提供 | 缺口 | 将输入内容固定映射到三个原厂组，补图像列表/计数/选择态合同测试 |
| ImageLoad 图像列表 | `ImageListPanel` 原件拥有缩略图 `ListBox`、1-based 计数、选中边框、删除/清空/首图/末图/添加按钮；`LocalImageListView` 负责运行全部/选中、自动切换和间隔 | 自研已保留 `ImageSourceDraftContent`，但未形成原厂三组的静态映射清单 | 缺口 | 只补映射和测试，不重新设计图片列表控件 |
| Blob 页签 | Agent Desktop 实测三页：`Tab_Basic Params`、`Tab_Run Params`、`ResultShow` | Blob XML/`DeclaredSettingsView` 已生成三页 | 已对齐 | 保留，增加页签和默认页断言 |
| Blob 基本页 | 原厂基本页包含 `Tab_Image Input` 与 `Tab_ROI Area` 两组 | `BlobUiComposition` 在基本页挂接输入与 `RoiGroupView` | 缺口 | 修复组装映射和 ROI session 归属，禁止占位文本 |
| Blob ROI | 原厂有 `RoiSelectGroup`、`ROISelecter`、`InheritWayGroup`、`ExternROITypeGroup`、`AssistGroup`，并由统一 `RoiManager` 写回输入 | 自研已存在 `RoiGroupModel`、`RoiGroupView`、`BlobLocalRoiOwner`，但仍有输入/组装边界待验证 | 缺口 | 保持数据节点→原件模板→统一 owner 链，补组装和释放测试 |
| Blob 运行页 | 原厂运行观察有滚动参数组，静态 6 种阈值模式、数值/范围、筛选和高级展开控件 | `BlobRunUi` 由 XML 声明生成，条件和范围由 `BlobThresholdCapabilities`/声明驱动 | 已对齐/需回归 | 对照原厂控件别名做静态计数和条件测试 |
| Blob 结果页 | 原厂 `ResultShow` 有检查结果、图像显示、文本显示分组；结果表列包含序号、面积、周长、质心X/Y、角度、长短轴、圆形度、矩形度 | `BlobDisplayUi` 配置图像/文本/可见性，结果 reader 与表格独立 | 缺口 | 验证结果声明、显示组顺序、保存重开和释放；未证动态生产者保持未确认 |
| 公共控件 | 原厂由 `AlgorithmTabRoot → ParamsConfigControlBase → ParamsTabUserControl → ParamListView/ParamUserControl → DataTemplate` 选择模板；Blob 的组是 `GroupLinkItem` | 自研由 `DeclaredSettingsView → DeclaredFieldsView → DeclaredFieldRow → ParameterFieldHost`，工具只保留声明与接缝 | 已对齐边界 | 不新增逐字段视觉控件，修复映射和会话参数传递 |
| 引用/数值 | 原厂数值、引用、范围由模板和组声明决定，参数对象由模块句柄读写 | 自研模板由 `FieldPresentation`/`AllowReference` 选择，生命周期由 host session 管理 | 缺口 | 验证模板加载后引用 session 和数值模型，避免把“加载前 null”误判为缺陷 |
| 底栏 | Blob 运行页原厂显示重置参数，Blob 基本/结果页显示连续执行、执行、确定；ImageLoad 无独立运行参数页 | `NativeToolEditorFrame` 统一承载 reset 和底栏命令 | 已对齐 | 增加按页可见性断言 |
| 关闭与释放 | 原厂关闭退订、停止连续执行、释放 `AlgorithmTabRoot`/ROI 管理器 | 自研窗口关闭排空、取消、工具 view/ROI owner/输入 composition 释放 | 缺口 | 生命周期测试覆盖正常关闭、异常关闭、迟到回调拒写和资源释放 |
| 未确认行为 | 原厂 Blob 动态结果生产者、完整失败码/取消分支、ImageSource 模块 Process 本体未被当前证据覆盖 | 自研不得凭经验扩展 | 未确认 | 只记录，不改算法或动态结果实现 |

## 本轮执行结果

- 原厂运行观察已完成。VisionMaster 4.4.0 Build20250516 的 ImageLoad 为 420×500 单页窗口，Blob 为 420×500 三页窗口。原厂快照和截图路径见证据身份表。
- Release 单进程测试构建成功并部署了 ImageSource 与 Blob WPF/Runtime 程序集。构建时保留 `NU1902 log4net 2.0.17` 警告与既有 nullable/XML 文档警告。
- 宽会话筛选仍有既有结果工作区、持久化和运行会话失败，不能作为本轮 UI 通过依据。Blob 专项回归暴露的具体缺口包括 ROI 页面字段在测试读取时为空、ROI 组装失败后 `ReadyTask`/输入页状态未形成稳定合同，以及引用行/结果树的生产链不稳定。这些缺口继续按原厂证据闭合，不能用自研占位视觉掩盖。
- Agent Desktop 已启动 Release `blob-min` 方案并进入主界面，画布可见 Blob 节点。画布两个节点被暴露为相同的无名 UIA row，Agent Desktop 安全解析拒绝对重复目标双击，窗口级人工打开 Blob/ImageLoad 尚未完成。该阻塞已记录为自动化定位缺口。
- 当前不宣称完整 UI 验收通过。下一步优先修复 Blob 基本页 ROI 组装/字段会话合同，再用稳定节点标识或固定脚本重新采集自研 ImageLoad 与 Blob 窗口快照。
- 已修复首绑阶段页组装异常隔离。`BlobUiComposition.BuildPage` 现在在首次 `Bind` 期间也会记录组合错误并让窗口保持可诊断状态，避免构造函数直接冒泡。该修复使 Blob 三页回归中的异常页生命周期用例恢复通过，但 ROI 字段读取和 Ready 状态仍有 3 个专项失败。
- 最新 Agent Desktop 复采确认原厂当前窗口为 ImageLoad `0 图像源`，窗口 420×500，三组 `Tab_Basic Params`、`Tab_Trigger`、`Tab_Subscribe_File`；随后从原厂节点列表双击 `Blob分析1`，复采 Blob `2 Blob分析`，窗口 420×500，三页 `基本参数`、`运行参数`、`结果显示`，运行页底栏含 `重置参数`、`连续执行`、`执行`、`确定`。截图保存为 `docs/evidence/vm-blob-dialog-current-20261118.png`。自研当前 Agent Desktop 只采到 `blob-min` 主界面截图 `docs/evidence/self-blob-min-main-current-20261118.png`，调试窗口节点级定位仍受重复 UIA row 限制。

## 直接重构边界

1. 固定宿主窗口尺寸、页签、组、底栏和关闭合同，全部由现有 Native 框架承载。
2. ImageLoad 只补原厂三组的声明映射、图像列表输入投影和生命周期测试。
3. Blob 只修复基本页 ROI 组装的输入身份、统一 owner、页切换与释放；运行页和结果页继续由 XML/声明驱动。
4. 禁止新增自研等价控件、占位 `TextBlock`、第二套参数视觉链或未有证据的动态行为。
5. 其他工具保持卸载，不纳入本轮构建验收范围。

## 验收门

- 静态映射：两个工具的页签、分组、控件来源和消费者全部有表中记录。
- 自动测试：ImageLoad 与 Blob 的页面、字段、ROI、结果、保存重开、关闭释放和迟到回调测试通过。
- Release：主解决方案 Release 构建通过，保留构建日志。
- Agent Desktop：自研 `11111` 打开 ImageLoad 与 Blob，逐页核对窗口/页签/分组/底栏，并保存截图。原厂证据截图不作为自研通过的替代。

