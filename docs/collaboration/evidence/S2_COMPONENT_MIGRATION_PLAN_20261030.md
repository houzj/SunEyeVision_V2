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
