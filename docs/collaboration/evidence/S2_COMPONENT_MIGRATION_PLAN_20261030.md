## 2026-10-31 S2 来源交互人工反馈

用户确认新版的来源切换、双图输入和名称悬停提示“已测试，正常”。此前截图确认运行中中文名称和当前选择显示；未执行状态名称有自动用例通过，用户未单独确认该状态。S2既有五组运行交互此前用户反馈正常。本独立worktree构建和368项自动回归通过；主线同版集成验收及VS实际设计器红叉仍未完成。叠层、背景交换及保存恢复因多图基础缺失保持禁用和未完成，不以本次反馈宣称全范围完成。
## 2026-10-31 当前阶段：S2来源选择器整批验证通过，待本组人工复验

本批集中贯通工具既有展示声明、实际绑定图像输入目录、独立输入显示项、输出当前勾选、完整名称提示和生命周期清理；保持端口及持久引用身份不变。修复WPF可变显示项的引用身份和只读标题OneWay绑定，不引入双轨模型。S1设计资源改用明确ResourceDictionary入口，设计样例沿真实控件及同一组装链，运行时不注入示例。

正式独立Release最终UI构建0错误/6488警告，全量0错误/6475警告；冻结18个DLL后attempt-042整批367通过/0失败，回归前后产物哈希不变。实际选择器及空宿主资源的真实设计组合PNG已查看。attempt-039、040失败历史与041单例诊断均保留；041有意退出2，不算整批成绩。证据位于Release/_validation/s2-selector-binding-*.log、s2-selector-binding-frozen-product.json、s2-selector-accepted-regression.log及S4-20261025/ImageSourceRecovery/attempt-042/receipt.json。

S2此前五组一般运行交互人工反馈通过；本批中文名称、实际输入目录、当前勾选和完整提示仍待人工复验。叠层、背景交换与相关保存恢复缺正式多图owner基础，保留明确禁用及完整依赖待办，不记完成、不阻塞当前交付。Group和公共显示合同扩展继续暂停，S3/S4未启动。

VS实际设计器仍未加载成功：用户最新截图为错误0但等待IntelliSense初始化；正确87f4方案已确认，Release五类型存在，Debug目标未有DLL。空宿主独立WPF设计渲染通过不能替代实际VS验证，不将待初始化报为成功，不改主仓、不删除VS缓存。

以下历史记录保留，当前状态以本节为准。
# S2图像结果视口：完整原件核收与集中实施方案

## 基线与阶段边界

源基线35d3387286cc20ef7b57aa64b158b3f7a60807ac。用户2026-10-30明确允许S1实际VS设计器记待验证、不阻塞S2；S1运行时人工通过，不将设计器冒称通过。当前Release不在核收期间重建。S2沿原件完整链核收后集中修改，不重造旧SDK主图、不扩Group、S3/S4或整体主界面。

## 证据及完整功能清单

原件根目录D:/MyWork/REA逆向测试/analysis/decompiled。核心是VMControls.WPF/VMControls.WPF/ModuleResultView.xaml及.cs、VMBaseControls/VMBaseControls/ImageViewBox.xaml及.cs、VMControls.WPF/styles/moduleviewstyle.xaml、VMControls.WPF.DataForUI/ModuleDisplayDecorator.cs与DisplayImageListDecorator.cs，以及Apps.PcUI.ImageSourceAudit/HikPcUI.Controls/FullScreen.xaml及.cs。复用HANDOFF_IMAGE_RESULT_NATIVE、HANDOFF_D07_VIEWPORT_DESIGN、PUBLIC_VIEWPORT_API、RESULT_FULLSCREEN及D07_INTERACTION_MANAGED_SOURCE_AUDIT已有来源和设计；历史候选测试不算当前新增行为通过。

|完整功能链|已有位置/行为|当前核收结论与决策|验收出口|
|---|---|---|---|
|真实原视口、模板、样式、图标、深浅主题|Plugin.Wpf原ImageViewBox及ResultShell资源；ResultViewThemeScope|保留原件，不另画工具栏；继续核原XAML依赖与当前映射，不凭文件数判完整|正式WPF单双态、两主题/禁用/悬停；设计器单列|
|来源目录/选择|ResultWorkspaceImagePlan、Presenter.InputSelected/OutputSelected|复用真实publication/Port/Scope/Frame；完整核原输入/输出列表及空源策略|上下游来源、别名/异帧、空/失败/旧来源，不串图、不执行|
|单双图及输入侧重置|Shell.IsDoubleImage目前仅ApplyLayout列宽|已确认差异：原OnIsShowDoubleImageChanged清输入选择，开启取首个输入，并调整ROI可见性；宿主需集中适配，保留输出选择及同帧约束|反复切换、输入无/多图、编辑手势取消、输入恢复，不靠刷新碰巧实现|
|原缩放/平移/复位|原ImageViewBox.Enlarge/Shrink/Init、鼠标与尺寸事件|复用原算法；Init原义是比例/偏移/裁剪复位，不另外实现伪1:1；核按钮enable和单双图显隐|工具栏、滚轮、拖动、resize后像素及叠层坐标一致|
|像素/尺寸/文件名状态栏|NativeResultImageFrame像素lease与原CallChangePixel；当前无ImageName赋值|复用格式/stride/Domain链；已发现本地ImageName未接，沿当前明确源路径给原视口设置，不用输出端口名冒充文件名|Gray/Bgr/Bgra、边界/缩放后坐标、换源清旧文件名/像素|
|全屏完整组装|ResultViewportFullscreenScope移动同视口、所有权/父槽恢复|已确认差异：原ModuleResultView同时移动左右按钮；FullScreen还支持非捕获双击退出/Topmost。集中适配实际原按钮、上下文与关闭链，保单一视口/lease|双侧全屏、左右导航、Esc/非捕获双击、重复开关、父槽和焦点恢复|
|同帧选中高亮/辅助线|NativeResultViewport层owner、Presenter.Highlight/Crosshair|复用当前同Frame校验及独立层释放，不以Bitmap或同尺寸猜身份；核原CrossConfig和当前正式命令|背景换帧不残层；错pub/Frame拒绝；调整不执行工作流|
|图像导出/右键|NativeResultViewport.Export及原ImageViewBox保存入口|沿原入口核原图/渲染图快照与取消/释放，不扩新文件格式|原图/渲染图区别、取消不写、失效拒绝|
|本地预览与S1衔接|ResultWorkspacePresenter预览及SourceDisplayPreservationScope|保S1已人工通过的显示保留与键盘意图；不能新全屏/来源处理导致旧完成回弹|快切、开关不清图、导入不执行、撤销不执行最小相关回归|
|参数ROI消费者|现Native ROI lease/适配和真实冻结输入|保正确已有会话，不扩绘制种类；单双图原件ROI侧变化要核实际消费者，不能只改可见性造成错帧编辑|取消/应用/关闭/重载，旧scope不清新scope|
|保存/重开与编辑历史边界|正式方案模型、导航和视口独立状态|复用当前合同；运行/导航/变换不进入编辑重放；不能为迁移造第二套身份|切流程/节点/方案、保存重开、Undo不重放执行|
|异步/异常/取消/关闭及资源|ResultImagePane、NativeResultViewport、Presenter.Dispose|复用已有generation/current闭包和独立lease；所有新事件/名称/全屏附属元素一并退订恢复|旧回调不覆盖、双图各释放一次、全屏异常及主窗关闭清理|
|设计时与人工|ResultWorkspaceDesignView同真实Shell/owner|已有独立设计面，实际VS结果未知；用户已允许待验证不阻塞实施，仍必须如实记录|同一正式Release启动，完整S2人工矩阵，不套用S1反馈|

## 集中实现原则与生产调用链

主窗ImageResultControl → ResultWorkspaceShell真实两个原件 → ResultWorkspacePresenter冻结来源计划 → ResultImagePane借明确publication → NativeResultViewport/NativeResultImageFrame持真实Frame与像素 → 原件变换/像素/显示层 → 换源、全屏、取消、关闭按epoch退出。执行层不访问WPF。一个实际原视口仅一个owner；全屏搬移原视口及原导航按钮，不复制图片/控件或借第二个像素reader。跨源图像所有权与结果表publication分开，不为换背景退休模块结果。

本批先完成上述13链源到消费者核对，形成资产身份和差异处置记录后，统一实施单双图/来源、状态栏和全屏组合等已确认差异。完整批包含事件绑定、拒绝和重入、卸载重载、关闭恢复及相称回归，不逐按钮提交。缺正式基础的叠层背景、图形/文字显示配置等沿当前记录禁用，不能因S2核收擅自扩公共显示合同；若核收发现现有正式基础已具备，则按真实能力更新决策，不把未找同名类当缺基础。

## 当前状态

已完成关键入口定位和三组确定差异；完整原件/模板/资源核收仍进行中，尚未修改产品代码、未构建、未称S2完成。下一步读取尚未核完的原件行为与现有完整消费者，固定批次设计及受影响验收，再实施。此前超长输出截断的文件不计完整读过。

## 本批实现冻结与补充核查

保留原ModuleResultView/Page模板/资源、ImageViewBox Enlarge/Shrink/Init及鼠标/像素链，保NativeResultViewport像素、导出、图层和ROI lease。集中修改：Shell原导航按钮采用显式绑定源，跨全屏namescope仍接同命令；FullscreenScope整体搬移视口/左右原按钮并保祖先主题资源、双击退出及Topmost，按每个原父槽恢复，外部新parent优先；Presenter订阅单双图DP，退出清输入，开启取首输入，Refresh时单图不加载隐藏输入；文件名来自同publication借阅的正式output.file（仅图像源output.image/gray-image），本地预览用本次被接纳的path，绑定新帧与清空同时清旧ImageName，不读当前光标冒充发布路径。

辅助线核查发现公共CreateCrosshairLayer/OpenSettings已具备，但实际Shell.CrossLinesCommand未接；本批复用其完整原窗口和退出链接到输出侧，按首次操作惰性创建，不重造配置窗口或无条件给每个视口增加图层。源图叠层/背景替换仍缺正式多图层选择合同，不以辅助线能力冒称已具备。

新增回归沿真实生产夹具覆盖：单双图输入清退/首项恢复且输出回执不变；全屏原按钮/命令/主题/同帧及双击恢复；实际Shell辅助线命令到原配置窗口及主控件关闭；原预览用例补文件名与清空断言。当前UI首次构建0错误，辅助线接线及完整新批仍须最终UI/full和同版回归；不得用首次构建覆盖后续修改。

## 2026-10-31 S2整批视口候选验证通过，准备人工验收

原件对照及处置见S2_COMPONENT_MIGRATION_PLAN_20261030.md，原入口身份与29图标/9资源来源复核见S2_ORIGINAL_ENTRY_IDENTITIES_20261030.json、S2_RESULT_SHELL_ASSET_REVIEW_20261030.json，全部SHA一致。复用既有ImageViewBox变换/像素/导出/lease和同Frame层，不重造主视口。集中修正原全屏视口与左右按钮整体搬移、命令namescope及祖先主题资源、Topmost和非捕获双击退出、父槽恢复；单双图清输入/取首项；文件名来自同publication正式output.file或本次被接纳预览路径，换帧/清图清旧名；原辅助线完整配置窗口能力已接真实主结果按钮，惰性创建、宿主关闭退场。

最终UI构建0错误/3392警告，全量Release0错误/6374警告。正式18个SunEyeVision DLL固定后执行attempt-038，363通过/0失败，覆盖原21失败及新增单双图/全屏导航/辅助线生产用例；预览文件名和清空断言亦通过。回归后18DLL均未变化。日志Release/_validation/s2-viewport-ui-build.log、s2-viewport-full-build.log、s2-viewport-regression.log，身份s2-viewport-frozen-product.json。阶段号沿现有runner使用S4，不表示S4功能阶段已验收。

运行时人工S2尚未开始，实际VS设计器待验证（用户已允许不阻塞推进）；不以363项或资源SHA称WPF同态/整阶段封版。原件全局单例ShowDialog未照搬厂商服务，宿主用独立owned Window作用域管理同一视口，保新owner/重入/异常/主窗关闭的恢复责任；全屏导航和退出行为仍须用户验收。叠层背景、正式图形文字显示声明、3D/点云、历史完整上下文、Group等仍按原完整清单保留未做/禁用，不记完成。S1此前人工通过继续保留，VS待验证。

本批人工验收按完整操作链：选择不同输入/输出来源及单双图切换，核对输入清空/首项、输出不误切；缩放/滚轮/拖动/复位/像素读数；两侧全屏左右切图、Esc和双击退出并还原；辅助线打开/配置/关闭及换图；文件名换源清空、切流程/重开/关闭不残留。确认后再记录S2通过，不提前推进S3。

## 2026-10-31 S2人工交互反馈通过，来源名称显示问题仍待修正

用户反馈“目前测试没有问题”，随后明确补充来源选择器显示有问题并提供截图。已记录五组运行交互无问题，不能忽略后续纠正而将整个S2封版。4张原截图保存在Release/_validation/S2-manual-20261031，哈希及反馈见S2_MANUAL_ACCEPTANCE_20261031.json。

集中显示修正设计：来源目录优先使用已有IToolResultPresentationProvider的ResultProjection中文列名，严格按同Schema的Port/MemberFieldPath匹配；通过现有RuntimeToolConfiguration持有的definition只读公开现成展示声明，不新增持久模型、协议或工具实例，不修改输出端口/运行/绑定身份。没有声明时保原明确显示字段，不硬编码image→中文、不将显示名当Key。原ImageListComboBox模板和142宽度保持；在真实选择器补完整SelectedItem.DisplayName悬停提示。新回归验证同身份中文标签、来源切换不变，以及双侧选择器提示；独立UI/full同版集中验证后只请用户复验显示，不要求重测已通过的全部交互。双图像素条窄宽裁剪与原模板相同，先记原件窄宽行为，未擅自另行设计状态栏。

## 来源选择器整链核收与本批收口决定（2026-10-31）

用户最新要求：能修复先修，因关联/逆向缺口不能完整实现的记录保留、不阻塞，后续可能统一调整架构与工具层。本条覆盖此前只修名称的候选设计，未构建候选一并审查、不单独交付。

原件已读取：DisplayImageListDecorator完整1–649行、ImageViewItem完整、ModuleResultView的2146–2463选择/叠层/刷新/清理及775–867加载退出、moduleviewstyle的ImageListComboBox完整状态模板、VM.PcUI/HikPcUI.ImageView/Byte2ImageSource.BuiltBgraImageSource、TransparencyData。以下为源码静态结论，不冒称实机取证。

|原件完整行为与位置|现有差异/已有基础|本批决策及验收出口|
|---|---|---|
|RefreshImageList：有效前置模块+自身显示图像，模块名排序，OutputMask末置|已有真实依赖图/输出声明目录、冻结publication，不移植厂商全局对象|保当前可证明依赖范围，名称改正式展示声明；不凭端口字符串猜mask角色；重名不改变引用身份|
|输入列表来自Inputs的RelateIO，克隆显示项|目前把全部IsUpstream项当输入，控制前驱也误入；BindingRelationCatalog.References和Runtime.BindingTargets已具备|按正式图像输入引用匹配输出Port/Member/Selection/Scope，独立克隆输入显示项；无绑定不造默认；历史缺整运行上下文不混用当前上游|
|普通输出选择：当前项IsCheck=true、IsEnable=false；其他项取消|当前IsCheck恒false，菜单全未勾选|显示状态按已接纳输出选择同步；单图当前项仍不可取消；输入项不共享输出勾选；空选择/换节点/重建不留旧状态|
|显示名Owner.Name+本地化NameA，收起选择器与下拉同源|工具已有ResultProjection中文元数据，快照默认显示名为ID|只读取得同Schema的既有声明；匹配Port/Member，歧义不任选；名称不是持久/绑定Key；两侧完整悬停提示，保原142宽和模板|
|选图订阅/移除OutputFilter及ValueChanged|现publication lease/current/generation替代厂商对象回调已成立|复用所有权，输入克隆不重复拥有publication；计划退出统一释放，旧通知不得覆盖新选择|
|勾选总数最多4、运行中拒绝、无底图/无叠层值拒绝|当前无正式多图像owner，不能靠复选框造功能|本批不可误用禁用，保原勾选图标状态；提示具体未实现原因，不冒称完整叠层|
|背景标记/取消背景重选/点击交换及确认|同上缺各图lease、布局/导出全链|保留禁用入口，完整后续范围记录，不用普通选图冒充背景交换|
|3个叠层槽：alpha191/127/63、z100/101/102|RegionLayer是区域RLE，不是图像层；原AddImageLayer只是底层入口|不套RegionLayer，不改同Frame高亮保护，不扩公共合同|
|BuiltBgraImageSource：Gray零值透明、RGB转BGRA、最近邻缩放到背景视图|已有单图/区域像素链但没有受管多图像转换owner；类别色元数据另缺|完整记录这些行为与资源限额依赖，不能只拿WPF.Opacity模拟原alpha规则|
|选非背景图暂藏叠层，选回背景恢复；尺寸/回调重绘|原状态机依赖多图owner|本批不可用，和背景交换一起留待实现；不改已验普通来源切换|
|Record/RecoveryCheckedImages保存选中、背景、勾选项；无保存选自身非mask后输入后首项|现只持久SelectedDisplaySource，不持久多图状态；正式身份是引用不是原FullName字符串|普通选择保存/重开继续复用；多图保存、复制、删除、Undo完整待办，不造第二持久模型|
|ClearImageLayer/DeInit/Dispose全部退图层、透明度槽、过滤回调|已具备计划/Pane/Viewport退出链|本批输入目录与勾选状态随同计划退出、拒绝与重入；未实现层不伪造清理完成|

技术总监已按主仓确认：NativeResultViewport只有单_frame/Region/Geometry/Selection/Text，无正式多IReadOnlyImage图层，也无待构建交付；原队列背景+三叠层是后续范围不是实现。当前公共显示/插件合同扩展暂停；因此本批不等待B08、不扩平台。需要后续用户确认接续的完整依赖：受管多图像owner、每图租约/Frame/Domain/布局、透明度/次序/重绘、导出、跨选择与运行刷新、保存复制删除撤销恢复。原完整范围保留、不计完成。

本批集中验收：同Schema中文名称+无/重复名称；实际输入绑定与控制前驱区分、同端口别名/序列固定索引；输入/输出状态分离；选中勾选刷新/重入/清空/方案切换；禁用叠层提示；保存重开与旧生产链。更新原主方案后统一UI/full、正式同版回归，再启动本组复验。VS继续待验证不阻塞，不提前推进Group/主界面整体重组。

### 2026-10-31 当前worktree实际VS错误及同批修正设计

用户补充87f4/Src/Plugin.Wpf/Controls/Native/Composition/ImageSources/ImageSourceStrip.xaml的实际截图：XLS0503，MergedDictionaries第4行无法加入StaticExtension，IntelliSense仍在初始化。与此前主仓ControlBrush两错误分开，不能混用或宣称当前VS已通过。截图标签有未保存星号，修改后需VS重新加载磁盘版本再核。

设计：现LocalizationResourceDictionary为private构造的共享语言资源实例，不能直接new或为设计器复制一套语言字典。新增明确继承ResourceDictionary的中立LocalizationResources，构造只合并同一Instance，S1根XAML用明确类型元素替代x:Static对象，保资源顺序和生产同一实例；只处理当前S1完整依赖，不批量改其他工具资源。原控件补d:DesignWidth/Height及mc忽略标记，仅设计时提供760宽，不改变运行时尺寸。沿现ImageSourceStripDesignView.SampleSession提取同一个工厂，控件仅DesignMode=true时使用；正常运行仍由宿主注入Session，不读文件/设备、不执行工具、不复制业务模型。独立设计视图同用工厂并给合理设计宽度。资源/示例/完整控件同批验证后仍需实际VS复验，不能以WPF离屏渲染代替。

程序已确认退出；与S2来源选择器整批一起UI/full、同版生产回归，资源修改的影响包括本地化、模板加载、源条设计与运行。已有S1用户人工结论保留，新设计时修正单列。不会修改主仓或绕过未获准的VS自动操作。

### 本批首次完整回归与选择一致性修正

attempt-039：366通过/1失败，原21项均承接。失败Source_selectors_use_tool_declared_names_and_full_tooltips_without_changing_port_identity：中文名称断言已过，但真实ComboBox.SelectedItem仍为受控节点，ToolTip已为灰度来源，暴露真实选择缓存不同步；不修改断言放行。可变IsCheck/事件放入record会参与生成的值相等/哈希，不适合作为WPF可变显示项。改为内部引用身份class，端口引用/Key不变，输入项通过明确工厂独立克隆；补哈希稳定和真实SelectedItem断言，保重入/清空回归。用户可见禁用提示仅说明图像叠加/背景切换未实现，不暴露内部租约术语。失败日志保留，不把366/1称通过。

独立S1同正式控件设计图s1-typed-resources-design.png已生成并目视核查：正常/只读/折叠三态加载，首项与勾选可见。本证据仍非VS实际成功。VS当前截图错误0但等待IntelliSense；进程启动路径为正确87f4方案，Debug目标dev/Debug/SunEyeVision.Plugin.Wpf.dll不存在，Release元数据中五个类型均存在，已请求切Release复核。已有ServiceHub日志仅见旧断连，不足以认定当前原因；不删缓存、不改安全/全局设置，不阻塞S2。

### attempt-040同源模板问题及整批复验安排

最终整批首次：309通过/58失败，58项经逐个异常归组全部为同一个原因：只读DisplayName被原选择器内部TextBox.Text默认TwoWay写回。不是58个独立功能缺口。记录类改引用身份后揭露此前init属性掩盖的模板写回；正式显示项不应为只读标题增加setter或回退record。保稳定引用模型，明确SelectedItem.DisplayName展示绑定Mode=OneWay；继续保真实控件SelectedItem/ToolTip同值断言，不放宽。

attempt-040的空宿主资源设计测试已过并生成新图，证明正式独立设计视图不借主窗资源可加载，但实际VS仍待验证。失败日志完整保留。本次先UI/full，诊断白名单只增加这一个实际失败场景，执行前后单例数断言保留；诊断有意退出2不当完整通过。确认修正后仅再执行一次完整批，不能反复扩测。
