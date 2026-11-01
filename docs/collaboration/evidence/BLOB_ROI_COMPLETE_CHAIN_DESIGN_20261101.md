# Blob ROI整控件生产接入设计（候选，2026-11-01）

基线 `300b924db00844fb7149f7990ff833d65c15063b`；唯一写入工作区 `D:/MyWork/line-aiproject/blob-reference-chain-20261101`，分支 `codex/blob-roi-chain-20261101`。本文件记录冻结设计、已实施本地两组四形链、候选构建与专项边界。不是原ROI全部分支、主仓正式验收或Blob整体完成声明。

## 1. 原件入口与证据等级

原 `Applications/Module(sp)/x64/Location/IMVSBlobFindModu/IMVSBlobFindModuAlgorithmTab.xml` 的基本页ROI分类按顺序链接 OutputMaskImageGroup、InputMaskImageGroup、RoiSelectGroup、RoiType、InheritWayGroup2、ExternROITypeGroup、AssistGroup。RoiType 原 SelectType=Mul、FullScreenEnable=True、RoiMaxNum=16，形状 Box/SectorRing/Polygon/Sector。原 Group.xml SHA256 `9129B82A0F9906DDE591347498A80E2E8C019C0FB959A721ECFD24FBBB5207DC`；Blob XML SHA256 `F1B2F279717DCE3EC61C11C41F858BD100359095BCF13672422A3C73EFE47F6C`。

- Group.xml:1308起四继承；:1709 ROIBOX/InputROI，:1740–1895五个float；:1896 ROIANNULUS/InputROIAnnulus，:1927–2112六个float。组合不是像素Region。原显示名“圆形”不能丢弃内/外半径及角度跨度。
- Group.xml:2489起屏蔽绘制/继承，:2560原绘制仅Polygon，SelectType=Avoid；继承按钮写InputBlindPolygonString（string），不是ROIBOX/ROIANNULUS。**用户明确扩展屏蔽为多形状**，四种公共原形状可复用，差异须单列，不能说原厂屏蔽本来四形。
- Group.xml:2605输出掩膜默认false、AlgorithmIndex=0；:2621图形/图像类型默认0，图像类型显示MaskImage并隐藏检测、屏蔽、Assist；MaskImage的EnumerationG读取IMAGE候选，写InputMask。资源证明入口和显隐，不证明mask像素含义/最终输出。
- Group.xml:243、363–1000按信息/点/坐标是FIXTURE、POINT及十个float来源选择，不能等同现Affine2D或底层FixMode。
- 已存集中证据 `D:/MyWork/REA逆向测试/docs/joint-audit/B08-ROI继承与位置修正静态证据-20261101.md`、`B08-位置修正原生定点补证-20261101.md`，以及仓内 `BLOB_ROI_IMAGE_SOURCE_AUDIT_20261101.md`。原生预处理已证部分修正和GenerateMaskImage调用，角度、公式、外部mask及完整布尔语义仍未证。

## 2. 现有能力、差异与复用决定

| 完整分支 | 已有生产/公共能力 | 本批判断与出口 |
|---|---|---|
| 检测多形绘制/编辑 | Plugin.Wpf NativeRoiSession、NativeRoiEditor、NativeRoiAttachment/Drawing、NativeResultViewport.BeginRoiEditing；rectangle/annulus/sector/polygon与稳定组ID、版本、覆盖、拖动及参数互写 | 可直接复用，需Blob私有正式配置owner、JSON、执行栅格化及实际主窗租约接线；不是缺渲染/绘制基础。四形同批，原检测最多16的资源约束进入接纳。 |
| 屏蔽多形 | 同一Session允许第二组及多对象，独立ID、选择、删除/覆盖、手势。现Blob仅临时ShapeContext | 可同批接入四形；用户扩展，原厂仅polygon。不能把mask完整环与部分annulus混kind，负扫角及整环转换原公共实现直接复用。 |
| 全图/无屏蔽 | RoiGroupState/RoiCoverage的组级FullDomain/None；BlobSession用input实际Domain和inspection−mask | 直接复用。空组语义明确，不能造全图矩形，也不能把无几何一概当空检测域。 |
| 矩形/圆环组合继承 | RoiDeclarations.Shape/Sequence，BindingClrAdapter/RoiShapeValueAdapter已有正式SDK输出→中立Record；公共引用树/索引及只读ROI投影已有；NativeRoiInheritanceSample展示完整组合 | 底层可用，**样例不等于生产**。需顶层正式引用字段+宿主同次解析/帧来源证明；运行时严格校验kind和完整性，不按名称假定ROIBOX等价。不能复用资源Region输入冒充。 |
| 5/6参数继承 | 正式float候选、完整OutputReference/索引/变量、ReferenceBindingBatch、公共组合样例已有 | 可复用解析与控件；仍须每叶独立正式字段、整组原子解析和角度约定。现样例明确矩形degrees、环radians属于本方库合同，不能据此推断VM裸float单位。没有原证时角度模式不得伪开放。 |
| 屏蔽继承 | 原输入是多边形字符串；本方有Shape类型与像素Region但无该字符串格式消费者 | 原字符串编码/错误语义未证，保留交互与拒绝，不能用Shape或Region树偷偷替换原语义。 |
| 图像ROI | 图像EnumerationG与正式IReadOnlyImage/Domain和同帧reader已有 | InputMask具体二值/灰度/通道、前景极性、与几何组合/异常策略未证；不是缺图像控件。未证前保持UI-only修改拒绝，不新增未消费输入。 |
| 输出掩膜 | 已有HALCON域、CreateRegion/CreateImage、同次publication及资源所有权 | 原OutputMask开关的准确输出身份、像素值/极性/组合及资源声明未核，不可把现BinaryImage或灰度图重命名。保留原行和拒绝，缺语义不扩显示基础。 |
| 三位置方式 | 正式Affine2D、Blob现pose开关、同帧Region变换、原Assist公共组合存在 | 不能将三方式映射Affine2D；FIXTURE/点/坐标到实际HVAS公式及外部mask顺序未闭合，保值拒绝。已存Affine清除入口继续可修复，不自动清值。 |

## 3. 单一正式数据与保存/历史

本批把**完整本地绘制检测+屏蔽四形**贯通，不逐形交付；其余原分支完整保留上述不可误用边界。私有owner只组织既有合同，不引RoiParameters/旧SDK可视或第二几何存储。

正式配置采用Blob私有Scalar/Record/Sequence声明的可选纯值输入 `input.roi.local`，不进入原页面额外行。BlobLocalRoi.Declaration根含inspection/mask两个组，组含Coverage和有序对象列表；对象含稳定ID/name/enabled/visible和私有纯值形状。坐标是x/y Scalar Record，顶点是其Sequence，矩形degrees、环/扇start/end radians为现中立库合同；无PointGeometry、图像、HRegion或WPF对象持久化。缺省检测FullDomain/屏蔽None。

BlobLocalRoiProjection在UI边界无状态转换既有RoiDeclarations.Shape，唯一可写值仍是RuntimeConfiguration.Fields的该Record。不会扩Workflow RuntimeFieldJson。DeclaredInputFieldsView为未呈现Constant输入提供窄Read/Write回调，明确拒绝已呈现字段和非Constant编辑模式；复用Host既有WriteInput，不开放通用EditModes。BlobInput.Read及BlobSession立即消费该输入，不增加未消费字段。

owner以真实ProductionEditorContext/同一私有RuntimeConfiguration身份拥有；新scope GUID、单调revision，组名inspection/mask，对象ID只在该owner内唯一。新建/替换/删除/覆盖统一一次Apply字段提案，前置比较预期RoiEditStamp并读取正式草稿确认；先安装接纳快照再通知，通知异常不撤回已接纳结果。UI的pending/gesture单独阻止捕获；取消/重绑先退役旧callback，再释放编辑lease，再释放图像借用；清理异常不掩盖原异常。

编辑只改变私有正式草稿，确认/保存沿现Host提交历史；取消不影响正式模型或文件。窗口内Undo/Redo采用owner针对该纯值字段的私有历史；确认后整个节点沿现文档历史撤销/重做。Copy复用RuntimeConfiguration.Fields深冻结，局部对象ID可保留，scope重新分配，禁止把物理Frame/lease存进文件。重开按声明读取纯值。切换输入图不自动丢几何：几何属于image:input槽位坐标，新帧重新借用与绑定；没有尺寸/坐标变换推断。

继承若同批获准，需要单独顶层正式字段（两个组合引用和5/6叶分别存在），沿现FieldSettings.MapReferences参与复制/历史；不能把ReferenceSetting塞进Constant Record。非活动分支旧来源保值，但执行冻结目前CaptureExecutionFields会解析所有配置来源，必须同时设计活动分支捕获/拒绝，不能仅隐藏控件后留下暗中依赖。此点未解决前不开放继承保存。部分配置无来源时只读/错误，不回落本地几何。

## 4. 执行、坐标及资源

BlobInput.Read接收同次冻结的local纯值定义；BlobSession使用原HALCON环境作用域，将所有enabled检测形状按组union、屏蔽形状union，再与现input.EffectiveDomain相交并差集。不经旧ToolRegionHelper/RoiParameters/业务SDK模型，复用中立记录及HALCON现运算；四形一次实现。矩形遵循现中立X右Y下/angle-degrees，HALCON角度符号显式转换；环/扇形用现start/end radians，支持正负扫角、跨±π与完整环，完整圆用GenCircle，不拿0→2π GenCircleSector冒充；多边形保持闭合顶点与孔洞/裁剪测试。精确像素边界按本方HALCON现合同自测，不声称VM像素等价。

本地逻辑坐标RoiDeclarations.ImageFrame=image:input不是上游资源Domain ID。NativeResultViewport.BeginRoiEditing已明确支持该输入槽绑定，但严格比较Session.Coordinates、实际Domain、Frame和尺寸。运行物化绑定到**本次input.Frame**，保持原CopyRegion等跨帧检查；不以同尺寸、同文件名或同引用的旧run当同帧。已有显式像素Region与本地纯值定义不得同时暗中生效：本批保留旧Region，存在非Unset时继续明确拒绝窗口捕获，不静默清除。若以后混合，另有原语义依据后明确组合，不擅加优先级。

所有临时HRegion置于using，生成/union/clip/算法失败/取消均释放；仅需要正式输出的对象由IOutputResources接纳。local定义不持资源。draw与数值修改不触发算法；调试仍从冻结草稿启动RuntimeDebugInvocation，正式执行仍标准输入快照。

位置开关和非空pose仍拒绝；不能对已在当前帧的继承形状重复套变换。图像mask/OutputMask未证分支不进入上述新栅格化，不把它们计完成。

## 5. 实际帧接线：必要且立即有消费者

现ResultImagePane.BorrowCurrentImage已有publication独立Borrow+receipt+IsCurrent；ResultViewportImageLease带实际IReadOnlyImage、NativeResultViewport、NativeResultFrameReceipt，Dispose只释放借用。基线RuntimeNodeConfigurationControl/ToolSettingsViewOptions **没有**这条入口，本批已按下述路径接入；DeclaredToolConfigurationControl.SetRegionInputProvider仍针对旧参数链且未发现真实调用者，不照搬。

主线已确认本线为最小共享文件唯一写者。补核原 `ModuleDisplayDecorator.cs:445–477`：单图SelectedImage、双图SelectedInputImage，IsCanDrawROI核当前显示IO属于ImageInputs；没有证据支持仅打开或点击ROI就自动切其他图。因此最终接线**完全不选择/恢复任何图源**：

1. NativeRoiFrameLease借入Image/Viewport/Receipt/IsCurrent/release，只能BeginEditing，直接复用BeginRoiEditing；NativeRoiFrameSource传递现publication改变。ToolSettingsViewOptions.RoiFrames立即由Blob私有owner消费。打开参数窗不Borrow；原工具箱无Session时只路由用户请求，不造图像/可写几何。
2. RuntimeNodeConfigurationControl将真实输入当前Reference与document/node/generation验证闭包交MainWindowRuntimeEditing，ImageResultControl的现ResultWorkspacePresenter提供实际入口。Wpf不访问Runtime/Workflow。
3. **单图只看当前可见OutputPane，双图只看当前可见InputPane**。其真实selector Source完整Key必须等于Blob的ImageInput，已呈现内部Source必须同一对象，Source属于该计划的正式输入，publication Stamp与当前独立借用一致，且现receipt仍有效。不同Key、无图、未运行、旧publication/异Frame、保留展示态均拒绝。用户须在原图像selector选择实际输入图；相同尺寸/同物理Frame的另一个成员图也不能替代来源身份。
4. 请求代次仲裁仅作用于未完成借用；已授予lease独立检查自身presentation/plan/epoch/选中项及proof，失败的不匹配请求不抢占它。current/source.Current前后复核；安装后早退或异常按presentation引用恢复此前marker，不能清新owner；首错与借用清理错聚合。成功接管后旧Dispose只释放自己的reader，不清图、不恢复选择、不退役新owner。
5. 运行/用户换图/模式单双切换/Root或窗口退役只使旧编辑scope失效，不主动抢回输入；后续用户再操作ROI才重借。没有可借帧时保留原工具箱及正式纯值，不在错误图像上画旧几何；几何参数需重新选择正确输入后进入原编辑器，未匹配raw继续保留并阻止捕获。
6. 直接复用ResultImagePane.BorrowCurrentImage的publication lease，不保存原VM指针、不延用跨轮内存池。原缓冲所有权补证与本方HalImageSnapshotSource逐通道复制是不同平台所有权，后者是明确适配。

不新增双图owner、不改工作流独立工作区、不要求主界面改造。接线只沿现ImageResultControl→ResultWorkspacePresenter→ResultImagePane真实owner。

## 6. 文件与跨模块边界

产品候选最小批：Blob Runtime新BlobLocalRoi/BlobLocalRoiRasterizer、BlobInput/BlobTool纯值输入定义及BlobSession调用；Blob.Wpf新增私有owner与现BlobRoiUi/BlobParameterEditor接线（BlobSupportedWindowState原保护复用）；Plugin.Wpf原ROI组合需接正式状态而不另造模板，NativeRoiSession/Editor/Viewport正确部分不重写；IToolSettingsView选项与新借帧封装；UI RuntimeNodeConfigurationControl、MainWindowRuntimeEditing、ImageResultControl/ResultWorkspacePresenter的最小provider；不修改Workflow RuntimeFieldJson。所有新增接口必须此批被Blob及真实Host消费，无空接口。

继承整组若纳入，另需现正式绑定快照到ReferenceBindingBatch适配（RuntimeDebugInvocation已有完整编译/变量快照，但不提供输入预览读取出口），不是Wpf自行查询运行值；跨模块API须主线另冻结，不复制第二解析器。位置/掩膜语义未证不新增字段。共享修改已由主线指定本线唯一写者；未涉及未冻结合同。

## 7. 一次性验收矩阵

- 原件外观/显隐：图形绘制、四继承、屏蔽绘制/继承、图像模式；原顺序、原模板与中文420/英文680保持，设计样例走同一组装；无常驻说明，原短Tooltip与操作时拒绝。
- 真实Host：四形在两个组各多对象创建/选择/参数双向/移动缩放旋转/删除/覆盖；检测16/17边界；正负扫角跨±π、部分环到完整环保kind；mask多形为用户扩展单列。
- pending与时序：非法原文不擦、未完成手势阻止确认；切组/模式/页、隐藏/禁用/owner关闭；Create/Replace接纳与通知异常/重入；旧request迟到不能改新scope。
- 正式数据：取消不改模型/文件；确认只一次History；Undo/Redo/复制/保存重开保持全部组状态/对象ID/纯值；未知JSON、非有限值、错误Frame/重复ID拒绝，不保留资源。
- 实际帧：同引用新run、同尺寸异Frame、相同Frame异Domain、record成员错、跨文档/同ID重开、视口替换时取消重入；最后读者释放、只释放自有lease，旧清理不清新帧。没有匹配真实输入时不以样例图代替。
- 执行：四形两组union/difference与非矩形inputDomain；全图/无屏蔽、全遮挡、相交/越界/空结果；直接/工作流/调试同定义结果一致；取消/算子故障资源清理；确认后保存重开执行不变。
- 未实现原分支：UI-only变更拒绝且恢复可用；旧像素Region/pose/隐藏非默认输出与判断保值拒绝不回归；所有不支持旧配置禁止静默改值。
- 回归：独立UI+全量Release标准构建，现RoiComposition/RoiBindings、WindowPresentation原ROI各组、BlobSupportedWindow及RuntimeWorkflow/RuntimeAlgorithm/BlobParameterProduction；新增BlobRoiProduction独立真实Host/帧专项。只跑相称受影响范围，不用样例通过冒称Blob接通；VS由用户人工安排。

## 8. 已冻结批次与退役原文

主线已授权：本批完整贯通检测+屏蔽两组全部四形的本地定义、原控件、实际帧、保存历史与执行；继承、图像mask、输出掩膜、三位置仍保留完整清单与保护。共享接线唯一写者已确认，不把设计问题转成逐项用户权限门槛。

单owner下两Session仅是投影：每份都含两组全部Visible几何，另组Enabled=false只读，同视口只改变当前组编辑权，不删另一组；两份原Editor各只注册到自己的Session，避免改变公共Session单编辑器规则。激活新组先提交旧组raw，未完成手势明确拒绝切换。原输入和屏蔽绘制顺序保持页面顺序，mask四形/未设数量上限为用户扩展。原ModuleDisplayDecorator.cs:512聚合AlgorithmItems.ROISelecters的全部ROIList；ModuleResultView.xaml.cs:2250–2269将双图ROI放输入view、单图放主view。资源/静态链不是原机运行证明。

切图/新帧不能当取消。NativeRoiEditor/NativePolygonRoiInputs导出opaque原文快照：原Editor身份、组、对象稳定ID、旧scope/frame/revision、原已接纳几何与全部数字/顶点raw。不持旧图像资源，不入JSON。新帧仍同owner时只在同scope、ID与已接纳几何一致才恢复原文；匹配不了保持明确不可捕获状态，用户可取消/关闭主动放弃；新窗口不会继承旧raw。未完成绘制失帧不能恢复未提交鼠标路径时明确拒绝，不把它当已接纳几何。

物化边界：有限矩形角度先按360周期安全归一化再转radians，避免有限double乘PI溢出；宽/高半值下溢0提前拒绝。UI周期等价投影且未编辑角度保留原标量。均为本方合同，不声明原厂单位/栅格等价。集中点集核正负45°矩形、非对称多边形、正负环扫角/跨±π、±2π全环及孔、多mask并集和非矩形Domain。

构建记录：UI001候选obj ACL拒绝；UI002唯一CS0103命名空间错误，均保留；003 UI/全量0错；最终当前004 UI0错/6535警告、全量0错/6537警告，23产品文件哈希与冻结一致。独立Model001五组、Model003七组通过；窗口004/005因夹具误用selector包装实例恒等失败，005确认逻辑Key/model/file及实际Frame/Domain未变，关闭仅receipt Epoch21→27及包装重建；下一轮改为更强稳定身份+实际raster像素/Domain+文件断言，不把包装对象作为持久身份。

候选窗口008四组、Reentry010五组、Frame011七组均已自然exit0；Frame包含真实窗口、两组四形、组内删除复制粘贴历史、真实确认/方案撤销重做复制、保存重开对象重新注册正式引擎执行，以及错图/新publication的非法原文恢复与旧回调退役。Model003七组通过。RuntimeWorkflow、RuntimeAlgorithm及BlobParameterProduction相称回归均exit0。CompleteDraw仅验证真实原命令到会话完成入口，不是自然鼠标绘制；新增重开执行使用LoadCandidate所得对象重新注册正式引擎，不以原会话执行代称重开执行。测试源码编译002的WorkflowEngine夹具错误也保留，不计产品失败。
## 9. 本批可交付与未完成边界

可交付是检测/屏蔽两组本地四形的正式纯值owner、原控件组装、当前匹配输入图上的借帧与两组同屏、运行物化及保存历史整链。借用失败不抢旧lease、proof同步重入/异常、旧Dispose、新publication及用户改选Key均有实际宿主专项。所有截图/载荷是本候选自行构建，原始日志在Release/_validation保留；不是主仓正式验收。

继承四方式、图像mask、输出掩膜、三位置及外部像素Region与本地ROI同时使用仍未接入，保值并拒绝不支持配置；不称整Blob或S2/S3完成。原厂角度单位/矩阵/栅格边界未证，本方degrees/radians与HALCON物化明确为平台合同；屏蔽四形及无数量上限为用户扩展。未测自然鼠标绘制、真实OS剪贴板、VS设计器/其他DPI；未完成鼠标路径失帧仅保留明确拒绝状态，用户取消/关闭主动放弃，不能恢复路径。未直接穷尽16/17数量边界与所有算子故障注入，原有共享控件回归不等同这些私有场景。

### 受影响原控件回归与失败边界

BlobSupportedWindow attempt-002原14组通过：四种模式显隐/非法未接入状态/隐藏正式值保护保留；形状入口从已移除的临时RoiSelectControl适配为两组真实NativeRoiEditor，并验证无匹配viewport不能生成正式几何。首次改断言误写类型命名空间导致宿主编译失败，保留attempt-001，不计产品失败。WindowPresentation attempt-001筛选“原Box”3组通过；RoiComposition attempt-001六组通过。

WindowPresentation attempt-002筛选Polygon为6通过1失败：失败由原绘制画布Mouse.Capture在独立隐藏桌面返回false，原强断言保留；不能据此宣称自然鼠标绘制已验，也未据环境失败改产品。其余有序顶点/非法双槽/恢复/绑定损坏/卸载原文检查通过。attempt-003圆环9通过1失败：O06:2522仍要求部分annulus抛NotSupportedException，与同文件已集成的六项部分圆环适配及NativeRoiProjection分派矛盾；仅更新该旧断言为真实NativeSectorRoiProjection、annulus kind/ID/原端点不改、6个原字段，保留原四项半径退化拒绝和原文恢复，之后重跑同版。

RoiBindings attempt-001失败：11组依赖ToolRegistry.CreateParameters(ImageThreshold/CircleDetect)而当前正式release-payload只准入ImageLoad与BlobAnalysis的Runtime/Editor组件；对应旧源码仍存，不属当前正式发布工具，未拷旧插件或放宽准入。唯一纯值非法record拒绝组通过，不将整轨标通过。现RuntimeWorkflow/BlobParameterProduction以及本地ROI专项覆盖当前Blob正式运行/资源和保存链，不能替代未迁移的几何继承生产者与该旧轨全部断言；后者列保留缺口。

验证命令：候选在本工作区使用 Run-S2ReleaseValidation.ps1 -ValidationScope Candidate；BlobRoiProduction 的 S4_BLOB_ROI_SCOPE 为 Model/Window/Frame/Reentry；共享旧窗口用 S2_ISOLATED_DESKTOP=1 与 O06_WINDOW_CASE_FILTER，Stage S4，Track WindowPresentation。正式集成后由主仓换为 -ValidationScope Formal。隔离选项只改变自有测试线程桌面，默认入口不变，不调用SwitchDesktop，不操作用户试用窗口。

最终原圆环重跑：WindowPresentation attempt-004自然exit0，10组通过。全部产品源码仍等于004构建前快照，未因测试修订改变产品。测试/加载DLL/回执SHA集中见BLOB_ROI_CANDIDATE_RECEIPTS_20261101.json。所有自有测试进程均已终态，GUI已释放。
