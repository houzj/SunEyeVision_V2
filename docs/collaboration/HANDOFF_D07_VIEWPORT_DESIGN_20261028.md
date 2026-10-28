# D07 / E 图像结果视口与工作区整体迁移设计交接（2026-10-28）

- 审计基线：9f197fbe，独立分支快进到46cce5cc42a6b39af92a4f78e73368a599a4ae65（后者只校正宿主测试）。
- 当前分支：codex/d07-viewport-design；执行者：source_editor。
- 本批仅只读源码并新增本交接；不改产品、主方案、CURRENT_TASK，不构建、不执行算法/反向工具。主账与原主方案由root统一汇总。
- 结论：D07不是缺全部基础。原件ImageViewBox/Shell、真实Frame像素lease适配、文字投影守卫、Native ROI绘制/历史会话、现根结果publication与选择epoch均可复用；缺口是唯一结果视口owner、同帧选图/分层投影，以及旧SDK视觉消费者整体接线退出。

## 1. 证据与已有—行为—差异—决策—验收

原件证据先复用HANDOFF_IMAGE_RESULT_NATIVE_20261028.md及Themes/Assets/Results/ResultShell/provenance.json：VMControls.WPF ModuleResultView.xaml的imageUnion/resultGrid/localImageGrid/logUnion，moduleviewstyle及Apps.Style递归资源；不是只取两张截图。ImageViewBox源码/模板已在Controls/Native/Display/VMBaseControls/VMBaseControls，来源SHA见既有O06_NATIVE_*_SOURCE_AUDIT。以下均为本基线已读取的实际调用链，不以不存在同名DeclaredRoiViewport判断缺基础。

|已有实现位置|实际行为|差异/风险|复用/调整/替换决策|验收出口|
|---|---|---|---|---|
|Plugin.Wpf/Controls/Native/Composition/Results/ResultShell/ResultWorkspaceShell.xaml(.cs)|原双ImageViewBox；输入/输出独立selector；单双图列；源条/属性树/当前结果/帮助槽；235/40底栏；缩放Enlarge/Shrink、重置Init；全屏命令收到实际ImageViewBox|尚未成为主界面生产组合；InputImage/OutputImage仅IImageData，自己不证明Frame或拥有lease；内部视口未给正式owner绑定挂点|直接沿同Shell接线，不复制近似XAML。最小只读视口挂点或明确attach入口交唯一owner使用；不在Shell放调度/查询/算法。原资源与左右独立选择保留|同一真实Shell分别独立Designer与运行；单双图独立源、折叠、resize/重载、缺命令禁用、深浅主题|
|Plugin.Wpf/Adapters/Regions/NativeRoiImageFrame.cs|验证IReadOnlyImage；记录完整ImageFrameAddress；AcquirePixels校验Frame/Layout；Gray8/Bgr24/Bgra32逐行去padding私有显示buffer；Dispose释放自己的像素lease|internal且目前由ROI/文字各自拥有；不是可直接拿Bitmap证明Frame；未把EffectiveDomain投影为显示裁剪|复用该真实像素/stride/lease机制，结果专属适配在公共库里用它，不重造SDK图像转换池。每个实际视口持自己的明确frame adapter；双图不共享未计数的可Dispose adapter|三格式/行padding/失败中断；原image释放后视口lease仍有效；换帧与关闭最后释放一次；非矩形Domain不得伪称全域或改物理Frame|
|Plugin.Wpf/Presenters/Display/NativeTextViewport.cs|BindImage带isCurrent；generation/Frame/revision和回调前后检查；原TextShape/transform随resize/zoom刷新；有队列Abort、失败清理与显式RefreshCurrentness|目前自己new ImageViewBox并独占ShapeList；仅文字，不能与ROI/结果各自同时覆盖同一ShapeList；没有通用公开Clear|复用其投影与队列/当前性机制，结果专属viewport统一合成后一次写ShapeList；不能把该wrapper和NativeRoiViewport套到同一原视口当多owner。必要提取内部公共primitive，保既有文字原行为|文字位置/比例、缩放后恒定样式；旧排队render/变换通知不能覆盖新帧；错Frame/revision拒绝|
|Plugin.Wpf/Presenters/Regions/NativeRoiViewport.cs + Adapters/Regions/NativeRoiSession*.cs + NativeRoiScene.cs|真实IReadOnlyImage/Frame；RoiEditStamp/正式accept/history；mouse capture、Esc/cancel、失效和Unloaded退帧；ROIList及部分ShapeList投影|不是结果层owner；已有矩形/完整圆环/扇形/多边形绘制，不含旧generic七类全部手势。NativeRoiSession刷新可写ShapeList，不能与文字各自抢同一列表|复用会话/帧/原控件/历史机制；结果只读overlay和参数ROI编辑租约分职责，统一由结果viewport的唯一层owner安装。参数独立NativeRoiScene不改Blob业务、不当结果工作区替代|同Frame编辑/结果不互清；草稿接受/拒绝/取消/重绑/undo/redo；原已具备四类行为不回归|
|UI/Shell/Controls/Shell/ImageResultControl.xaml(.cs)|SDK ImageControl绑BitmapSource/来源索引；ResultRenderLayer缩放；旁边原PropertyListView和真实ImageSourceStrip，Shutdown会关publication与源session|主图、toolbar/status/zoom仍旧SDK视觉；视图不是已迁Shell；GetImageDisplayControl暴露SDK类型|整片换为ResultWorkspaceShell+唯一viewport owner，复用同sourceStrip与结果树/表槽；ImageControl/ResultRenderLayer入口及旧XAML事件同步退出，不藏一只旧控件供ROI调用|真实源条选择执行不变；模块结果同一pub；切tab/source/节点/方案/关闭不残图；无两套toolbar和隐藏SDK主图|
|UI/Services/Workflow/NodeResultManager.cs|正式结果进WorkflowContext（身份TryPublish）；现UpdateModuleResults已走publication；另把result.Display/Regions写SDK ResultRenderLayer，Debug先取GetDisplayImages.First并转Bitmap|执行缓存与视觉职责混合；DisplayCollector/BuildRegionItems/GetDisplayImages→Bitmap不带中立同帧视口receipt；可绕同一pub/选择epoch|保正确Context缓存/发布行为；视觉写入集中转给新workspace owner。退役SetResultRenderLayer/SetDisplayCollector/SetRegionItems/SetHighlightItems与SDK Clear链，先逐个迁实际调用者；不删除DisplayCollector非视觉生产合同，也不为它补假Frame|正式/调试同样选定pub+snapshot；失败/取消/空结果清理；历史不查询latest；旧视觉入口rg零生产消费者|
|MainWindowViewModel UpdateDisplayImageFromCurrentSource/SetDisplayImageFromSource/TryResolveDisplaySource，ImageResultViewModel/INodeResultSink/IRootCoordinator|选节点与上游来源目录，优先索引后列表回退；CaptureNodeResults短scope内转Bitmap，延后重设-1/index；同步DisplayImage/ImageScale|能显示图，但Bitmap已脱离物理Frame proof；来源属性名/列表索引不是身份；展示失效后回退另一物理帧可能让高亮贴错图|复用来源可用性/用户选择意图业务，改投影为稳定来源地址和实际publication lease，选图后取得真实IReadOnlyImage.Frame；没有proof仅可看图不能绑定跨图高亮；不让延后index刷新覆盖新intent|源失效/回退明确刷新实际来源地址+Frame；显示名/顺序变动不重定向；多图/上游与结果不同pub也不串帧|
|MainWindowViewModel OnDeclaredSelectionHighlightChanged；IResultPublication/ResultSelection/ResultHighlightTarget|已核selection.Publication及Current；当前主动清旧SDK高亮并报告“当前图像视口尚未提供同帧校验，已禁用跨图高亮”；Target含真实ImageFrameAddress和坐标frame|有选择合同但尚无真实viewport可承诺同Frame；不能把缺口说成完整高亮已落地|保当前禁用诊断直到真实receipt接上；同帧证明来自绑定的IReadOnlyImage/像素lease而非当前Bitmap、NodeId、run或图像尺寸|错pub/旧request/同尺寸异帧/多物理帧拒错；有proof才高亮；清选择只清该owner高亮层|
|UI/Services/Editing/Regions/RoiViewportEditScope.cs + Nodes/DeclaredToolConfigurationControl.Regions.cs|原IRoiEditViewport消费、RoiParameterEditingAdapter/RegionGeometryEditor、私有草稿/手势校验/pose与FramePairingVerified；scope仅清自己的子层|具体scope依SDK MainCanvas/InteractionOverlayCanvas/Bitmap provenance Guid与WeakTable<ImageControl>；SDK主图退出后不可原样复用|保参数模型/接纳事务/数值编辑/IRoiEditViewport职责，换原Native视口编辑scope适配；必须保可编辑组、pose禁用、提交前手势完成、关闭取消。不是简单改类名或保隐藏SDK Canvas|Root参数页本地/继承切换、无pose/错帧禁画、取消零写入、旧scope不得清新层、数值与手势同一次正式接纳|
|MainWindow.xaml.cs Loaded SetResultRenderLayer、GetMainImageControl注入；MainWindowViewModel/NodeOpenContext/初始化器/Closed|主窗把SDK结果层交NodeResultManager，调试/旧区域窗口注入主图，关窗将OverlayHitTestVisible=false|把替换XAML当完成会留下类型/回调死链或SDK视觉回退；Group也借此入口|同批同步所有实际消费者，主窗只暴露窄viewport/edit能力，不把原ImageViewBox/Canvas作为业务服务；旧SetMainImageControl链退出主结果域。Group不扩展，见第5节真实占位|无生产GetMainImageControl/SetResultRenderLayer旧依赖；旧窗口关闭回调不重置新窗口/新epoch的编辑状态|
|MainWindowViewModel ExecuteZoomIn/Out/Fit/Reset/ToggleFullScreen；Shell FullscreenCommand|旧VM只写ImageScale，Fit/FullScreen有TODO；Shell已直接调原视口Enlarge/Shrink/Init并给全屏实际view|不可把旧TODO日志或ImageScale=1当真实行为；原Init语义要按原件验收，不擅改名为另套1:1计算|原命令直连当前视口owner，复用原变换而不造第二缩放值。全屏移动同一view，finally恢复原parent/row/column/尺寸/焦点/owner，先取消编辑捕获|原缩放方向与enable、缩放/平移后Pixel坐标、1:1/复位、单双图切换；全屏异常/Esc/关闭/切方案恢复|

## 2. 唯一图层owner与完整调用链

建议职责是一个UI结果工作区协调者持两侧视口session，一个Plugin.Wpf结果专属视口适配持真实NativeRoiImageFrame和原ImageViewBox。名称/方法由root集中冻结后实施，不先造泛化平台合同。现NativeTextViewport/NativeRoiViewport各自ShapeList写者不能并列写同一结果控件。

每侧唯一owner负责：背景buffer/像素lease；结果几何/文字批；结果选择高亮批；只读参数参考区域；当前编辑租约ROI层。层有固定顺序及独立标识，但只有owner把一次冻结的完整投影提交原ShapeList/ROIList；其他消费者提交意图/不可变投影，不直接Clear或覆盖列表。选结果清高亮不清编辑，取消编辑不清结果，换物理帧则必须取消手势并清所有依赖旧帧的层。

生产链：实际正式/调试/历史选择入口→现选定IResultPublication及workspace result epoch→声明来源目录（Node/Port/Scope/真实publication stamp，不用显示名/索引作身份）→明确背景选择或经验证的回退→借来源publication lease→读真实IReadOnlyImage→NativeRoiImageFrame（像素lease/Frame/Layout）→同一原Shell视口→冻结同Frame图层投影→变换后原绘制→选择/缩放/全屏→换源/失败/取消/关闭释放。工作流不读WPF，插件不依UI，视口不调度/执行。

- result epoch固定结果选择意图与pub生命周期；每侧viewport epoch固定背景/物理帧选择；layer revision固定一批样式/文字/几何/高亮。背景切换不能无故退休模块结果publication，不能用一个全局epoch让换背景连表也失效；三者同属唯一协调owner并组合校验。
- 所有UI通知、选择器更改、像素读、帧绑定、投影构造及dispatcher回调前后核：方案session/root/选中节点或debug editor/current publication、对应epoch、完整Frame、revision。异步排队仅执行冻结意图；旧回调不得再查询latest补来源。
- 失败、取消、缺值、无图不会保旧overlay；若业务允许保留背景，必须以仍有效的独立背景receipt标明，不能给失败pub冒充输出图。清理先失效owner epoch并脱回调/队列，再清自己原控件列表及ImageDataSource，再释放frame/lease；宿主最后释放自己publication owner。清理异常仍finally释放余项，重入不得清新owner。
- 原NativeTextViewport无Unloaded自动Dispose，NativeRoiViewport Unloaded会取消/退帧；这两种行为不能误合并。全屏属于同一owner的临时搬移，保同一view/帧身份，取消pointer capture，最终关闭才退workspace。不得通过全屏复制新控件或第二缓存来保画面。

## 3. 多publication、多物理帧的明确规则

图像pub可以来自上游图像源，结果pub来自下游Blob；两个PublicationId不同不代表错帧。必须同时满足结果selection匹配当前结果pub stamp、图像来源pub仍Current、viewport实际绑定Frame等于该Target.ImageFrame、geometry坐标frame与显式已证变换一致。一个run/节点内多个输出也可能是不同物理Frame，不能以Invocation、尺寸、端口显示名或颜色/灰图名称推断相同。

1. Target按完整ImageFrameAddress分组；不是仅按Frame Guid/SourceId/坐标frame分组，更不能取First。单视口只装匹配当前背景的组；双视口分别匹配自己的Frame。其他组明确说明未显示/需选择背景。
2. 若业务要“选择结果同时切背景”，必须通过同一明确用户intent在实际可用候选中找可证明的Frame。唯一可证候选才可采用既定策略；缺失/失效/歧义均禁用诊断，不能任选第一张图。UI恢复选中或分页容器生成不是用户意图。
3. 同Frame别名端口可保用户原背景选择；不同Frame不能叠加。单选含多帧时保各组身份，让用户选择物理帧或双图各显示；不要把一批几何强制改成当前Frame。
4. 当前无proof的禁用日志是正确保护，只有上述真实绑定链验收后才退出；不能删日志后把Bitmap.Frame空值当通过。

## 4. DisplayCollector/Region的接入与SDK视觉整片退出

保留正确非视觉结果生产能力与WorkflowContext；退出的是主结果域SDK ImageControl、ResultRenderLayer及相关Canvas事件链，不是盲删SDK全部算法/结果类型。Main/NodeResultManager/INodeResultSink/ImageResultViewModel的BitmapSource只读展示路径应同步转为正式来源receipt，不在第二条SDK链继续投影。

DisplayCollector包含几何/文字/样式及部分DeclaredElementAddress，但本基线未见它为每一批提供可用的物理ImageFrameAddress。不能靠“选中节点+当前图”给它补proof。已有ResultSelection的显式ImageFrame与声明typed资源/geometry可复用；Region资源必须在合法lease中按需AcquireReader，先核SourceFrame/CoordinateFrame，再做有上限投影与释放，不把资源对象塞入ScalarValue或永久ShapeList。没有可证映射的旧DisplayCollector批次清掉并禁用诊断，依赖保留，不记为迁移完成。

主图每层唯一owner后才集中移除MainWindow_Loaded的SetResultRenderLayer注入、ImageResultControl的SDK事件/获取器、NodeResultManager视觉Set/Clear方法及其实际调用者。不得把旧SDK控件设Collapsed留后台，也不为兼容保GetMainImageControl返回旧对象。尚在其他独立工具窗口使用的SDK UI要另列实际消费者，不能以主结果区退出宣称全产品全部退出。

## 5. ROI编辑适配：基础具备与明确占位

已有IRoiEditViewport、RegionGeometryEditor、RoiParameterEditingAdapter、现正式私有草稿接纳、pose检查、NativeRoiSession/Viewport/Scene足以做窄适配，不重写节点/工作流/Blob业务。

但具体差异必须保留：旧RoiGeometryEditing.Kinds为point/line/circle/rectangle/arc/annulus/polygon；原NativeRoiSession明确只接rectangle/完整annulus/sector/polygon，sector独立身份，不能把部分annulus改名sector。故本阶段已有四类Native能力复用；旧point/line/circle/arc等未接原手势入口，需明确禁用该绘制动作并说明依赖，保已有数值配置与原数据，不造近似自研Canvas回退，也不谎称全形状已迁完。

旧DeclaredToolConfigurationControl.Regions按原IRoiEditViewport改接新的native编辑scope，保本地/继承、editable groups、SetPose/FramePairingVerified、提交前HasPendingGesture及关闭/页面切换取消；scope按实际完整Frame及编辑stamp校验，不再从Bitmap provenance Guid猜配对。旧RoiViewportEditScope的私有模型/事务行为复用，SDK MainCanvas渲染/WeakTable<ImageControl>和MainImageControl注入退出。独立NativeRoiScene用于参数原组合，不能占据主结果owner或擅改Blob工具页面。

Group保持暂停：MainWindowGroupEditing/GroupRegionParameterControl目前也借GetMainImageControl；不得为移除主SDK图而扩Group或保隐藏主图。其主图交互能力本阶段明确不可用占位/诊断，保源码、已有配置与非视觉行为，待Group接续时适配；如果用户验收范围要求该能力，则必须列未完成，不算D07该项通过。

## 6. 集中实施依赖与验收矩阵

一次完整功能批先冻结唯一owner/帧receipt/epoch与图层入口，再同步公共适配+Shell挂点、UI选图/正式及调试结果、主窗注入及ROI窄能力消费者，最后整批退出SDK主结果视觉。不是先换背景按钮、后长期留两套图像路径。实施前root须核所有SetMainImageControl实际派生消费者和全屏reparent父容器，按本表明确可交付/禁用占位。

- 来源/身份：声明无值、成功空、失败/取消/超时/partial；上游图与下游结果不同pub；同尺寸异Frame、同run多图、别名同Frame；错Scope/request/epoch；显式回退与无图清空；恢复选择不执行高亮。
- 所有权：原image/pub owner关闭但本视口lease有效；像素读取失败/缓冲分配失败/投影失败均释放；同实例复用/不同Frame换源；队列和通知重入不会把旧帧或旧Clear覆盖新视图；双图各scope独立，关闭最后lease准确一次。
- 图层：背景、基底结果、文字、选择、参数ROI互不越权清除；多Frame选择拆组；无proof禁用；Region读取严格同SourceFrame且有预算，资源叶不变scalar；Domain与padding不伪造。
- 交互：原pan/zoom/像素读值/缩放方向/Init、单双图、输入输出独立选择、当前/历史结果切换、源条同一session、帮助/底栏；全屏同一view移动与finally恢复、Esc/异常/关闭/切方案；按现API不支持的命令明确禁用。
- 编辑：已支持Native形状完整草稿→手势→接纳/拒绝→undo/redo→保存重开；换帧/pose失效/关闭取消不写正式值；旧scope不清新owner。缺入口形状及Group占位列依赖，不删原总清单。
- 视觉：同一正式Shell和原ImageViewBox独立设计数据/实际VS两态、运行深浅主题/高DPI/resize；自动功能测试或HTML均不能代原WPF外观及人工验收。
- 规定UI和全量Release同源构建、相关结果/ROI/源条回归与人工程序验收由主线唯一窗口集中执行。本交接未构建、未运行、未VS、未实际设备/人工验收；只给设计与现有实现证据，D07/E未宣称完成。