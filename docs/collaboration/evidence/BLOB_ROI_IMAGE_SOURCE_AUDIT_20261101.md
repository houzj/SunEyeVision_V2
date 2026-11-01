# Blob ROI 与图像显示来源核查（2026-11-01）

基线：主仓 `6d36e7fd`；本候选源码 `963b6159` 与之产品代码相同，后者未含主仓后续正式回执文档提交。此项为静态核查，不是控件实施、构建或验收。

## ROI 生产链

| 环节 | 已有实现及判断 | 未完成 |
|---|---|---|
| 页面 | `BlobRoiUi.Add` 仍创建 `NativeRoiUiDraftControls`，正式检测及屏蔽 Region 输入已嵌入。 | 临时绘制、继承方式及参数尚非正式数据。 |
| 公共组合 | `NativeRoiSession`、`NativeRoiEditor`、`NativeRoiViewport` 具备组接纳、双向几何编辑和退役能力；原公共验证见 `ROI_CONTROLS_FORMAL_20261031.json`。 | 公共样例及回执不证明 Blob 生产接入。 |
| 执行 | `BlobInput` 与 `BlobSession` 已支持同帧检测 Region、屏蔽 Region、位姿、区域差集和图像 Domain。 | Blob 本地几何到 Region 的正式物化链尚无。 |
| 状态与资源 | `RoiDeclarations.Shape`、公共几何编辑与区域算子素材存在。 | Blob 私有 owner、持久几何字段、几何值序列化、真实帧租约和 `IGeometryEditService` 正式实现缺失；旧 SDK 模型不作为新合同。 |

原件范围与控件区别见 `ROI_CONTROLS_SCOPE_DESIGN_20261031.json`；用户要求屏蔽区多形状，部分环使用 Sector 的投影是本方适配而非已证原厂行为。图像 ROI、四种继承、三种位置修正及输出掩膜仍须逐链核实。本次不扩缺失的其他公共基础，未就绪入口和 `BlobRunUi` 保护保留；不将 ROI 生产链标为完成。

条件具备后的完整生产链：检测/屏蔽组身份与草稿 → 原控件编辑和参数双向 → 确认/取消、历史及保存重开 → 同次输入帧与区域物化 → 冻结、检测减屏蔽、执行及回显 → 取消、异常和最后读者释放。需验证全形状、多屏蔽、引用失效、跨帧、复制与重开；不得使用设计样例、仅按钮可点击或局部算法测试代替。

坐标边界需纳入统一设计：`BlobSession.Execute` 在位置修正启用时对非空检测区和屏蔽区使用同一个 pose；若将已经处在当前帧的订阅 Region 当作参考坐标区域再次变换，可能发生重复修正。现有代码不能单独证明此情形的实际输入语义或 VM 对应行为，须区分本地参考几何与上游 Region 的坐标身份、单次变换责任和原厂证据；保持现有帧校验，不以图像尺寸代替身份。配置不能把 HRegion 当作常量持久化：`FieldSettings` 禁止配置持有资源，需纯值定义与运行物化明确分层。

进一步核查：`RuntimeFieldJson` 支持 Scalar/Record/Sequence，但目前不支持 `GeometryValue`；现行 Shape 的中心和顶点包含 PointGeometry，不能直接存入常量后宣称支持保存重开。`RoiViewportEditScope` 已具备实际图像 lease、手势及失效清理，但其参数适配仍接旧 SDK RoiParameters，不能照搬旧链。可评估 Blob 私有纯 Scalar/Record/Sequence 单一存储及到现有原控件几何的投影，避免扩张公共 Geometry JSON；此为设计候选，尚未实施或验证。现有像素 Region 引用的同次解析和执行具备基础，仍须核真实 Blob 页面接纳、位置修正语义与撤销保存，不能用底层能力冒称整窗可用。

### VM几何组合继承核实

原版 `D:/Program Files/VisionMaster4.4.0/Applications/Module(sp)/Group.xml:1709` 的 Region 使用 `GetFrontCombinationTreeOperation(ROIBOX)`、`SetCombinationSourceOperation(InputROI)`；`:1896` 的 Region2 对应 `ROIANNULUS` 与 `InputROIAnnulus`。原版 `x64/Location/IMVSBlobFindModu/IMVSBlobFindModu.xml:39` 起分别声明矩形中心/宽/高/角度及圆环中心/内外半径/角度组合。由此可证这里的原厂入口是几何组合继承，不能将本方 `IReadOnlyRegion` 像素区域订阅直接称为原厂等价。位姿原生变换顺序和坐标语义仍在集中补证。真实 Blob 编辑页总保护不能仅因像素 Region 后端已能执行而移除：临时绘制、切换和结果配置仍可变但未持久或消费，会误导用户。
逆向负责人已交 `D:/MyWork/REA逆向测试/docs/joint-audit/B08-ROI继承与位置修正静态证据-20261101.md`：矩形参数为五个可分别订阅的浮点叶，圆环参数为六个浮点叶；组合订阅可带索引并写入 RelateIO/RelateIndex。三种位置修正 UI 方式不得直接等同六种底层 FixMode；原输入装配、坐标系、角度、变换顺序及对屏蔽作用仍未闭合。原报告包含原厂版本和哈希，本次未运行原 VM/SDK。

### 原显示对象与位置修正补证

逆向负责人提供原 4.4 原件身份及九个活动显示配置映射：`D:/MyWork/REA逆向测试/docs/joint-audit/B08-结果显示配置到视口静态证据-20261101.md`。原 Blob 显示对象是以 CenterX/Y、BoxWidth/Height、Angle 和 BlobStatus 映射的 rect，并非可以直接用像素 Region 或质心代替；Contour Point 映射 ContourInfo，文本内容映射 Area。原对象 IsDisplay、颜色、透明度和线宽通过模块 UserData 写/读，FeatureBase 按执行次数取值；完整落盘、跨实例和原生缓冲释放仍未证。最大内接矩形、基准/运行点及扇环缺本方真实生产链，不得伪造。

原指令静态结果 `D:/MyWork/REA逆向测试/docs/joint-audit/B08-位置修正原生定点补证-20261101.md` 显示某初始化分支 SetFixMode(6)、两组姿态和修正形状返回；检测形状及绘制屏蔽多边形各有修正路径。外部像素 mask 是否变换、变换公式、坐标及角度约定尚未证，不能据接口名直接替换现有实现。

现有 `NativeResultViewport` 有独立 Geometry/Text/Region 图层、物理帧 receipt 和退役能力，不需要新建渲染基础；缺口是工具无关的同次正式显示描述与宿主接入，目前 `IResultPublication` 和 `IToolResultView` 无该常驻层合同。本方 BlobDisplayDefinition 的静态配置不能当成原厂映射或当次冻结配置。按当前不扩其他基础的边界保留保护；先形成整组配置、原对象映射、同次数据及资源所有权设计，再决定接入，不提交只有字段而没有消费的半链。
## VM 图像显示来源与本方适配

原件 `D:/MyWork/REA逆向测试/analysis/decompiled/VMControls.WPF/VMControls.WPF.DataForUI/ModuleDisplayDecorator.cs:1341` 从前序和自身 Display 候选查询映射 IO；`:1552` 和 `:1655` 分别恢复和记录主图、背景及其他勾选项。输入图、输出主图及多图不是一个值。原 UserData 与读取行为为静态证据；方案最终序列化和复制、删除/改名语义仍待证。集中报告：`D:/MyWork/REA逆向测试/docs/joint-audit/VM图像显示来源与选择状态整链-20261031.md`。

本方 `ResultWorkspacePresenter` 现只保存单个 `SelectedDisplayReference`；其 Record.data 及类型化表达式是宿主适配，不宣称 VM 内部等价。背景和叠加仍未实现。`ImageSourceResolver` 以同一引用选择图像条来源，但只核作用域/节点；结果面板按完整 Key 核端口、成员及类型，失效口径不同。后续统一设计主图、输入、背景及勾选状态，并核实显示与图像条的联动边界、身份失效和保存恢复；不可为旧字段保留兼容双轨，也不可在证据不足时把两个职责简单视为互不关联。

格式变更导致用户方案 4 不能打开；一次性副本 `D:/MyWork/line-aiproject/recover-solution4-20261031/4.recovered.solution` 经正式 `SolutionRepository.LoadCandidate` 零诊断并核1051路径、5勾选、索引、变量及显示引用；原文件哈希未变。该恢复不证明其他方案均可转换，程序界面打开及人工反馈另记。
