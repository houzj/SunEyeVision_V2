# Blob图文显示完整迁移设计（2026-11-01）

当前状态（2026-11-02）：首批真实外接矩形/Area文本已集成正式c8311cff；当前独立候选codex/blob-roi-result-display-20261102在该基线上完成检测ROI/屏蔽/扇环同次名义几何与正式样式接线。产品冻结002的UI/全量0错，Core5/Roi10/Window15/RuntimeDebug7/FieldEditing38/ROI预览24自然exit0，Loaded DLL均匹配候选；尚待集中正式集成，不称九项、S2/S3或VM等价完成。剩余最大内接矩形/轮廓点/基准点/运行点及动态文本位置继续保护。见 [当前实施设计](BLOB_RESULT_DISPLAY_IMPLEMENTATION_DESIGN_20261101.md)、[本阶段完整设计](BLOB_ROI_RESULT_DISPLAY_DESIGN_20261102.md) 及 [候选回执](BLOB_ROI_RESULT_DISPLAY_CANDIDATE_20261102.json)。本次几何非裁剪后像素区，四形/多屏蔽与总BusinessJudgment颜色均为明确适配；原历史证据/失败保留。
## 1. 原证据与纠正

第一依据：D:/MyWork/REA逆向测试/docs/joint-audit/B08-结果显示配置到视口静态证据-20261101.md。

原AlgorithmTab SHA256 34A0CC5423A3D66B92DC272E0F50B013310E5180E801C648005383C3ADDDFDEC；Display XML B3BC63E6CF7C8CA120BAF5B632BBE1359843138CF77CB5A64FC698EF22465112；Group.xml 9129B82A0F9906DDE591347498A80E2E8C019C0FB959A721ECFD24FBBB5207DC。

GetDisplayObjectOperation.cs:25–61按模块FullPath.[Display]和NameA查唯一对象，BridgeItemBase.cs:26–48缓存BridgeObj。九个ImageViewConfig不是九个输出端口，也不是九份算法结果。原眼睛控制IsDisplay；笔仅打开样式，PanVisible=False不能解释成眼睛或对象整体不可见。原模板paramusercontroldatatemplate.xaml:2503–2555的颜色、opacity(0..1、步进.01)、linewidth(1..10、步进1且按StrokeThicknessConfigEnable显示)为复用标准。

ObjectBase.cs:131–246、382–424、454–493已证模块UI参数UserData键的写读；不据此宣称所有native方案文件跨实例保存成功。本方正式保存/历史为平台要求，应自测，不冒称原厂落盘等价。FeatureBase.cs:309–394、RenderDataCenter.cs:71–137/167–247证明所核分支按executeCount读feature及交换缓存；本方以RunIdentity/publication/帧替代其全局服务，不复制缓存实现。

## 2. 原九项与阶段决定

|原配置/对象|已证真实映射|本方数据及处理决定|阶段状态|
|---|---|---|---|
|BlobFiled / Blob|rect；CenterX/Y、BoxWidth/Height、Angle、BlobStatus数组，BlobNum|Blobs逐项rotated-bounds四角与judgment-passed已有同次真实值；用四角闭合多边形显示本方外接矩形，按逐项判定选色。不得拿像素Region或质心冒充该对象；与原native矩形数值一致仍未证|可实现明确本方适配，不宣称VM数值同态|
|BlobMaxAreaInnerRect / Blob Max Area Inner Rect|rect；MaxAreaInnerRect五分量及BlobStatus|没有真实最大内接矩形，不能复用rotated-bounds|明确禁用；依赖真实算法输出|
|CheckZone / ROI|rect；DetectCenterX/Y/Width/Height/Angle、ModuStatus|现input.inspection.region是像素Region，不是原矩形组合；不能画Region充当该矩形|明确禁用；依赖正式ROI几何与本次有效参数|
|MatchLocation / Contour Point|outlinepoint；ContourInfo、ContourNum；PanVisible=False|有完整boundary/可选contour，但原outlinepoint双颜色分支及点排列尚未定；本方单外环contour为有损投影|明确禁用原行；不可用质心/整条线替代，也不扩渲染类型|
|FixtureInitPoint / Fixtured Point|point；OutputFixtureInitPointX/Y；Status NULL默认1|缺正式基准点生产值|禁用，并保留原位置条件|
|FixtureRunPoint / Unfixtured Point|point；OutputFixtureRunPointX/Y；Status NULL默认1|缺正式运行点生产值|禁用，并保留原位置条件|
|ResutlText / Show Text|text；Content默认Blob Area:{0}映射Area；BlobStatus；PanVisible=False|真实逐Blob area与judgment-passed可消费；首先支持原默认模板和显式有证的标量模板格式。不能用现静态“面积列表”定义冒充原功能；完整动态文本feature持久/位置绑定未知分列|可实现受限内容模板+七行正式配置的明确范围，未证表达式保持禁用|
|BlindZone / ExternROI|polygon；BlindPolygonPointNum、PointsX/Y、ModuStatus；填色#FF5400，初始opacity.5|现mask Region没有原polygon顶点；RegionLayer能力不等于数据语义|禁用原行；依赖正式屏蔽几何，不能借Region轮廓假造原顶点|
|CheckZoneAnnular / Annular|annularsector；DetectAnnulus中心/半径/StartAngle/AngleExtend、ModuStatus|缺本次正式环几何/继承物化；原角度与扫角单位转换不能猜|禁用；依赖ROI完整组合|

九行原清单不缩减。Blob默认#66ff00/#ff0000/opacity1/linewidth2/IsDisplay=true；其他对象初值逐项从原Display XML导入不可全局套用绿色。未实现行显示原因并不可编辑，不能保存no-op状态。颜色/透明度/线宽原字段依赖须全部一致；Point/Outline特殊分派不能套普通Status规则。原九配置之外还有Input/Blob/Binary/OutputMask图像对象和Data Record/Result List；本批不扩双图叠加/背景owner或图像导出基础。

## 3. 当前源码与实际复用闭包

- BlobResultDisplay.cs提供独立13项显示声明；算法BlobSettings/SettingsSchema仍55项。RuntimeToolConfiguration.Fields为唯一正式owner，EditorSettingsSchema合并68项，注册准入、真实Host、JSON与设计示例均走完整编辑快照；算法/current/绑定顺序仅算法子集。
- BlobDisplayUi复用NativeGraphicDisplayOptions、NativeVisibilityDisplayOptions与NativeTextDisplayOptions；九行原序、原三页与判定→图像→文本组序保持，七项缺生产值禁用，不保留第二可写临时矩形/文本状态。
- BlobDisplayDefinition/BlobDisplay.xml仅保留表声明；旧Graphics/Texts已移除，避免静态样式与正式字段双轨。
- NativeResultViewport.DisplayScene薄适配在一次原ShapeLayer事务中消费既有Geometry/Text投影与预算，没有新renderer或双图owner。ResultWorkspacePresenter.Display每pane持独立publication reader，逐行RegionReader核实际物理frame/coordinates/domain；文字-only也不按尺寸猜同域。
- MainWindowResultDisplay管理实际document/node/window scope与成功BindDraft token代次；当前正式样式与有效草稿即时刷新同一publication，非法pending保最后完整有效外观。旧token/关闭/回调不能清新owner。ProductionEditorContext复用现安全无改重绑，有改草稿保值并沿原Host并发冲突。
## 4. 单一正式owner及配置保存

唯一配置owner为节点RuntimeConfiguration.Fields和现DocumentEditSession/Host，新增Blob正式字段不另建配置服务。建议稳定字段角色：display.blob.{visible,ok,ng,opacity,width}；display.text.{visible,content,ok,ng,size,opacity,x,y}。最终fieldId按现唯一注册规则集中确定，不能以显示标签作身份。字段仅接真正启用的对象；其他原行无可写假字段。

内容复用原StringConfig；LengthA=0x20不证明业务截断，未实施无证32硬上限。场景总UTF-16字符预算1000000为本方资源保护、无截断。size6..72、图形opacity0..1 step.01、文本opacity0..1 step.1、width1..10 step1。颜色统一正式Rgba。Text默认Blob Area:{0}、逐Blob Area数组及状态同索引；允许字面量和受控占位规则须用同一解析器校验/格式化，未知语法明确拒绝，不能静默回退空串。

X/Y的原来源为前驱及自身int/float、SetResultIOToModuleOperation显示输出位置，与算法输入订阅不同。最小范围支持明确常量坐标；动态位置只有在整批引入本次输出读取阶段及引用资格后才开放。不能把自身结果作为普通输入参数注入，制造自依赖；不能在WPF查询运行值。动态位置未实现时保留原位置行及清晰原因，本阶段不得宣称七行全部原语义完成。

现编辑Host校验、复制、撤销重做、保存重开复用。Style popup确认/取消同一字段草稿；任一值非法不得半写，关闭前退役回调，禁用/隐藏/换节点/换文档均不写新代。真实已实现显示字段应可捕获；ROI/判定等其他no-op保护不因本批解除。

## 5. 同次publication与当前显示修订

本次算法结果仍按正式执行快照发布，显示样式采用“同一已发布publication＋当前正式显示配置/有效草稿”的独立修订策略；这是本方宿主时序，不称原厂全部竞态已证，不再要求样式冻结进算法publication。算法、输入、休眠引用、工具/schema等任一真实执行配置变化仍原失效，只有已验证纯常量显示字段不失效现结果。

IToolResultDisplayProvider只交声明路径、独立显示schema、默认及纯值外观；ResultDisplayDescriptor复用ResultFieldPath逐项校验。字段不进入算法SettingsSchema、执行/Debug解析或current；复制/删除/历史/JSON管理保全量。RunIdentity与样式revision独立。

MainWindowResultDisplay在实际文档会话/流程/节点实例/publication身份匹配后捕获不可变外观；ResultWorkspacePresenter准备同publication全部图文后原子安装，每pane单独lease。provider/proof抛错或同步重入按request/epoch/scene引用保护只清自己的旧场景。prepare失败清旧整景、不声称保留；配置非法在入口拒绝，不发布半套。
## 6. 现有宿主图层与租约

在ResultWorkspacePresenter.Pane内复用GeometryLayer/TextLayer，各Pane自身层与选择层、十字线分开。宿主按同次publication借一次独立IResultValueLease，解析完整候选场景后才原子安装；每层拥有自己的revision，有共同的当前publication/Pane generation检查。分层更新不能因兄弟层revision改变使已安装层proof失效。

Blob rotated-bounds为四个PointGeometry，转闭合PolygonGeometry；几何本身来自本次Blobs，判定按相同element-id/项索引读取judgment-passed，不按屏幕行号重配。文本同一项Area格式化并选同项OK/NG色；原Status0→NG、其他→OK，但本方bool必须显式映射false→NG/true→OK，不能利用数值隐式转换。

物理来源优先使用同一Blobs项region的SourceFrame/Frame证明，保持Blobs几何image:input坐标与实际显示资源Domain一致。灰/二值输出已有Blob槽域；上游原图Domain若不同不画、不按同尺寸重标。UI显示其他工具图像时不把当前Blob图形强贴上去。配置有值但资源未请求/未产生时保持空场景，不凭最近一帧补齐。

RegionLayer当前不用于伪装原ROI矩形/ExternROI多边形；将来正式几何具备再按相同宿主接入。绘制只读结果，不开放编辑手势。

换publication/来源/文档先撤权威、退役旧isCurrent，再归还旧lease并安装新场景。旧像素保留策略只能保留像素，不保留可借ROI/高亮/常驻图文权威。失败、超时、取消、清图、节点删除、窗口关闭、重入与清理异常均对称归还；清理异常不能掩盖原异常，也不能释放重入新代层。图层Dispose必须先退订再解除所有持有。

## 7. 允许文件与跨模块边界

- Blob Runtime：BlobParameters.xml、BlobSettings、BlobParameterDefinition绑定、BlobDisplayDefinition/BlobDisplay.xml；明确冻结配置元数据，不增加未实现算法。
- Blob Wpf：BlobDisplayUi、真实参数编辑器及设计样例走同一组装；移除对应UI-only行，不改共享原模板。
- 共享合同/Runtime：独立Editor显示schema、纯显示描述与外观捕获同批接通，不扩算法snapshot元数据；Wpf不引用Runtime/Workflow执行服务。
- UI：MainWindowResultDisplay及ResultWorkspacePresenter/Pane消费上述接入，不新增viewport或双图owner。
- 测试：公共控件既有回归+Blob真实宿主/结果工作区整链。其他ROI、位置、判定、轴算法、输出图像、OpenCV导出不纳入隐式扩建。

## 8. 一次实施与阶段验收

阶段交付必须同时具备字段→真实页面→确认取消/历史保存→冻结→同次publication→实际Pane层→退役。先定字段/合同/原默认和两对象语义，再按这条链集中实施；不是先加字段、下次再找消费者。

必需自动验收：

1. 真实Blob页面眼睛、样式popup、颜色、opacity、width；文本字面/默认模板、颜色、字号、常量XY；合法/非法整组、取消、禁用、隐藏、重绑和迟到回调。原七个未实现图形对象的眼/笔均不可误用，动态XY单列未实现。
2. 正式Host确认/取消、复制、撤销重做、保存重开；有效显示草稿即时预览同一结果且不执行算法，取消读最新正式值，确认/Undo/Redo刷新样式不失效publication；不改变算法数值/输出请求语义。
3. 同次双Blob不同面积/判定/排序/限数，矩形与文本各对应element-id；零结果零假图形/文字，NG色有真实依据；缺边界和空资源不借旧值。
4. 实际ResultWorkspace生产入口：表/图同publication，原图/灰图/二值切换，严格Frame/Domain拒绝，双Pane只更新匹配一侧；选择高亮与常驻层独立，清高亮不删常驻层，清图按产品语义统一退役。
5. 重跑、失败、预取消/运行中取消、窗口隐藏关闭、换节点/文档/同ID重开、观察者重入异常；记录最后reader释放，独立读者不被提前释放，新代不能被旧清理覆盖。
6. 使用已有FieldEditing/WindowPresentation/BlobParameterProduction/BlobResultWorkspace/RuntimeWorkflow/ResultChain，并增加精确新场景，不以已有轨道数量冒充新断言。UI及全量同版正式Release和加载SHA由主线统一验收。

设计时按用户最新决定暂不安排自动或VS验收，由用户手动检查；实现仍走同一真实视图与正式资源，不另造预览控件。其他DPI、真实指针、VM原机差分及人工分别标未验，不阻断已明确当前范围自测，但不能声称外观/算法同态。

阶段可交付：原Blob矩形与Show Text在明确受限语义下的正式生产接入、九项布局和不可用边界；不是九对象全实现，不是Blob整工具完成。原最大内接矩形、ROI矩形、outlinepoint、基准/运行点、多边形屏蔽、扇环及动态显示位置均保留具体依赖。只有补齐对应生产值/证据后才能接入；不得偷用近似值或扩其他基础凑完成。

## 9. 当前交付边界

渲染基础已沿原图层复用，必要共享编辑阶段/纯显示描述与真实消费者已集中接通；不再以缺新渲染基础为由保留全部显示占位。原九项中的七个无真实本轮输出对象与动态位置继续禁用；本地/继承ROI物化已有，但缺实际本轮publication几何传递，不能读当前草稿拼旧结果。

中文{面积}token映射、多对象文本原厂数量/排版、VM像素/样式时序等价未证。当前受控{0}逐Blob Area、常量XY为明确适配，默认两样本沿原MultiText renderer实测分为两行，排布与VM等价未证；不自行加行偏移或用质心冒充。当前只验LoadCandidate值与复制，不冒称重开宿主渲染/执行已验；双pane共存、异常注入/跨文档同ID等未新增直接实测项以候选回执单列，不能从代码推作通过。VS/自然鼠标/DPI/人工仍未验，由主线正式集成后统一安排。
