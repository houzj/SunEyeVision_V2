# Blob图文显示完整迁移设计（2026-11-01）

状态：可审核实施设计，产品未实施、未构建或验收。产品核查基线主仓6d36e7fdd763e833f34dd53cc315a3be0d30b137。本文件只写指定集成区文档；不批准逐行解闸，不新增无人消费的接口，不把九行均可编辑作为阶段完成。

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

## 3. 当前源码与可复用闭包

- tools/SunEyeVision.BlobAnalysis/Wpf/Views/BlobDisplayUi.cs:15–24创建NativeDisplayUiDraftRows；现九行与七文本字段均UI-only，不能保留为第二状态源。
- src/Plugin.Wpf/Fields/NativeDisplayOptions.cs提供NativeGraphicDisplayOptions与NativeTextDisplayOptions；原眼睛、样式popup、颜色及文本组装直接复用。替换临时实例，不重绘图标或模板。
- BlobDisplayDefinition.cs静态Graphics/Texts并无正式运行消费者；BlobDisplay.xml当前外接矩形/质心/轮廓等非原九对象清单，必须按上述矩阵整理，不能让两套声明同时生效。
- NativeResultViewport.Shapes.cs:45–52、229–239的GeometryLayer/TextLayer已具备独立层revision、receipt和isCurrent；NativeResultViewport.cs:239–249有RegionLayer。本批不写新渲染器，也不改变严格物理帧/坐标proof。
- BlobResultView.xaml.cs:47目前仅交表；ResultWorkspacePresenter.cs:299明确显示缺口，只另有选择高亮。ResultSelection只表达选择，不能借它运输常驻图形。
- IResultPublication当前Borrow只含输出，无本次显示配置；IToolResultView无常驻图层描述；ResultProjectionDescriptor的Geometry属于表选择，不能错用。

## 4. 单一正式owner及配置保存

唯一配置owner为节点RuntimeConfiguration.Fields和现DocumentEditSession/Host，新增Blob正式字段不另建配置服务。建议稳定字段角色：display.blob.{visible,ok,ng,opacity,width}；display.text.{visible,content,ok,ng,size,opacity,x,y}。最终fieldId按现唯一注册规则集中确定，不能以显示标签作身份。字段仅接真正启用的对象；其他原行无可写假字段。

内容字段保留原StringConfig与原长度声明0x20；字符计数/格式串转义未核时必须在实现前定清，不擅自扩大。size6..72、opacity0..1 step.01、width1..10 step1。颜色统一正式Rgba。Text默认Blob Area:{0}、逐Blob Area数组及状态同索引；允许字面量和受控占位规则须用同一解析器校验/格式化，未知语法明确拒绝，不能静默回退空串。

X/Y的原来源为前驱及自身int/float、SetResultIOToModuleOperation显示输出位置，与算法输入订阅不同。最小范围支持明确常量坐标；动态位置只有在整批引入本次输出读取阶段及引用资格后才开放。不能把自身结果作为普通输入参数注入，制造自依赖；不能在WPF查询运行值。动态位置未实现时保留原位置行及清晰原因，本阶段不得宣称七行全部原语义完成。

现编辑Host校验、复制、撤销重做、保存重开复用。Style popup确认/取消同一字段草稿；任一值非法不得半写，关闭前退役回调，禁用/隐藏/换节点/换文档均不写新代。真实已实现显示字段应可捕获；ROI/判定等其他no-op保护不因本批解除。

## 5. 同次冻结和publication的最小接线

必须先冻结正式配置，再执行；publication必须携带本次实际采用的不可变显示快照。不得宿主在呈现时读取当前节点设置，不得把样式伪造成算法结果/可订阅端口。

实施批需要一个最小、工具无关、立即有生产者和消费者的显示接入合同；这是现合同缺口，不是新渲染基础。最终签名由共享合同唯一负责人冻结，本设计不先写接口。建议职责分成两部分：

1. 运行元数据：在现正式执行结果/DeclaredOutputSnapshot中附带不可变已接纳显示配置快照（按本次RunIdentity和SchemaRevision），再由ToolResultViews.FromSnapshot投到publication。传输位置属于结果元数据，不在Schema输出树创建假数值端口，不给普通订阅资格。元数据只有冻结标量/表达式描述，不能持有WPF、回调或新资源owner。
2. 显示声明：工具注册的结果呈现声明以正式路径描述对象、状态、内容、几何和配置角色；宿主统一读取，不依赖Blob CLR类、不反射私有XML、不按中文名字特判。只加入本批Blob外接矩形与文本两个真正可消费描述；未知对象保持原页面禁用。

生产者是Blob正式设置捕获和Blob现结果声明；传递者是现执行snapshot→ToolResultViews；消费者是现ResultWorkspacePresenter。三者同一提交接通，禁止只交付“以后用”的公共接口。实现前需确认调试、工作流、失败/取消及历史publication均用同一传输点；缺配置元数据的旧/声明publication不得读取当前设置补齐，只显示图像和表，不画常驻层并给明确诊断。

如果用户禁止任何公共合同扩展，本方案到此阻塞，不能只在Blob内完成：现接口没有冻结显示配置/常驻声明路径。不得通过SelectionHighlightChanged、静态字典或插件查宿主控件逃避该缺口。

## 6. 现有宿主图层与租约

在ResultWorkspacePresenter.Pane内复用GeometryLayer/TextLayer，各Pane自身层与选择层、十字线分开。宿主按同次publication借一次独立IResultValueLease，解析完整候选场景后才原子安装；每层拥有自己的revision，有共同的当前publication/Pane generation检查。分层更新不能因兄弟层revision改变使已安装层proof失效。

Blob rotated-bounds为四个PointGeometry，转闭合PolygonGeometry；几何本身来自本次Blobs，判定按相同element-id/项索引读取judgment-passed，不按屏幕行号重配。文本同一项Area格式化并选同项OK/NG色；原Status0→NG、其他→OK，但本方bool必须显式映射false→NG/true→OK，不能利用数值隐式转换。

物理来源优先使用同一Blobs项region的SourceFrame/Frame证明，保持Blobs几何image:input坐标与实际显示资源Domain一致。灰/二值输出已有Blob槽域；上游原图Domain若不同不画、不按同尺寸重标。UI显示其他工具图像时不把当前Blob图形强贴上去。配置有值但资源未请求/未产生时保持空场景，不凭最近一帧补齐。

RegionLayer当前不用于伪装原ROI矩形/ExternROI多边形；将来正式几何具备再按相同宿主接入。绘制只读结果，不开放编辑手势。

换publication/来源/文档先撤权威、退役旧isCurrent，再归还旧lease并安装新场景。旧像素保留策略只能保留像素，不保留可借ROI/高亮/常驻图文权威。失败、超时、取消、清图、节点删除、窗口关闭、重入与清理异常均对称归还；清理异常不能掩盖原异常，也不能释放重入新代层。图层Dispose必须先退订再解除所有持有。

## 7. 允许文件与跨模块边界

- Blob Runtime：BlobParameters.xml、BlobSettings、BlobParameterDefinition绑定、BlobDisplayDefinition/BlobDisplay.xml；明确冻结配置元数据，不增加未实现算法。
- Blob Wpf：BlobDisplayUi、真实参数编辑器及设计样例走同一组装；移除对应UI-only行，不改共享原模板。
- 共享合同/Runtime：仅现执行snapshot和结果呈现声明的最小冻结显示接入，由唯一负责人统一实施；不能由Wpf引用Runtime/Workflow执行服务。
- UI：ToolResultViews及ResultWorkspacePresenter/Pane消费上述接入，不新增viewport或双图owner。
- 测试：公共控件既有回归+Blob真实宿主/结果工作区整链。其他ROI、位置、判定、轴算法、输出图像、OpenCV导出不纳入隐式扩建。

## 8. 一次实施与阶段验收

阶段交付必须同时具备字段→真实页面→确认取消/历史保存→冻结→同次publication→实际Pane层→退役。先定字段/合同/原默认和两对象语义，再按这条链集中实施；不是先加字段、下次再找消费者。

必需自动验收：

1. 真实Blob页面眼睛、样式popup、颜色、opacity、width；文本字面/默认模板、颜色、字号、常量XY；合法/非法整组、取消、禁用、隐藏、重绑和迟到回调。原七个未实现图形对象的眼/笔均不可误用，动态XY单列未实现。
2. 正式Host确认/取消、复制、撤销重做、保存重开；运行冻结后改草稿不改变已有场景，重新运行才使用新配置；不改变算法数值/输出请求语义。
3. 同次双Blob不同面积/判定/排序/限数，矩形与文本各对应element-id；零结果零假图形/文字，NG色有真实依据；缺边界和空资源不借旧值。
4. 实际ResultWorkspace生产入口：表/图同publication，原图/灰图/二值切换，严格Frame/Domain拒绝，双Pane只更新匹配一侧；选择高亮与常驻层独立，清高亮不删常驻层，清图按产品语义统一退役。
5. 重跑、失败、预取消/运行中取消、窗口隐藏关闭、换节点/文档/同ID重开、观察者重入异常；记录最后reader释放，独立读者不被提前释放，新代不能被旧清理覆盖。
6. 使用已有FieldEditing/WindowPresentation/BlobParameterProduction/BlobResultWorkspace/RuntimeWorkflow/ResultChain，并增加精确新场景，不以已有轨道数量冒充新断言。UI及全量同版正式Release和加载SHA由主线统一验收。

设计时按用户最新决定暂不安排自动或VS验收，由用户手动检查；实现仍走同一真实视图与正式资源，不另造预览控件。其他DPI、真实指针、VM原机差分及人工分别标未验，不阻断已明确当前范围自测，但不能声称外观/算法同态。

阶段可交付：原Blob矩形与Show Text在明确受限语义下的正式生产接入、九项布局和不可用边界；不是九对象全实现，不是Blob整工具完成。原最大内接矩形、ROI矩形、outlinepoint、基准/运行点、多边形屏蔽、扇环及动态显示位置均保留具体依赖。只有补齐对应生产值/证据后才能接入；不得偷用近似值或扩其他基础凑完成。

## 9. 当前执行决定

渲染基础够用；仅Blob文件修改不能完成。本方案需最小共享显示元数据及描述接入；按用户当前“缺其他基础不扩展”的决定，暂不实施该公共合同或移除整窗保护，保留完整后续范围及依赖，不将其计为产品完成。原报告可追的FeatureBase/显示位置/数组索引与native资源边界继续集中补证；未知未被证明穷尽，不一概当原机缺失。
