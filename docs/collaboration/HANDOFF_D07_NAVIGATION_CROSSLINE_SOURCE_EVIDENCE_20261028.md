# D07 导航、辅助线与背景菜单源码核验

基线：ecd4ea90。仅只读核验现有恢复源码，无二进制重新逆向、构建、运行或人工验收。原主方案与 CURRENT_TASK 由集成方统一维护。本文件的“确认”仅指静态源码行为；恢复项目实现不能代替原件行为证据。

## 证据位置

原件根目录 A=`D:/MyWork/REA逆向测试/analysis/decompiled/VMControls.WPF/`；B=`D:/MyWork/REA逆向测试/analysis/decompiled/VMBaseControls/`。下列路径均相对该根，行号来自现存原件反编译文件。

|链|已有实现与原始行为|差异、决策与验收出口|
|---|---|---|
|左右导航|A `VMControls.WPF/ModuleResultView.xaml.cs:1940–2020`，RunCheck→拒运行中→GetAllModuleByType(ImageSourceModule,isCurrentLevel:true)→逐源写LastImage/NextImage空字符串控制命令→一次procedure.Run(null,false)。每源命令异常吞掉，运行异常呈现。XAML:168–169按钮属于整个结果区。|当前流程本层全部图像源，不只选中源；不递归Group。不把原件逐项吞异常复制成半配置提交。应在现行Manager执行准入中一次冻结所有参与源、瞬态步进并运行一次。不能逐源调用正式SelectImage。|
|持久配置与运行游标|同一导航入口未写CurrentImagePath/start/配置Host。A `VMControls.WPF/ImageListPanelBase.cs:1313`缩略图选择才写CurrentImagePath；AutoPlay时写选中项的前驱来补偿运行自增；勾选模式使用勾选集合。`DoRefreshLocalImage:1377`从运行CurImagePath.Value回填显示。|这证明两入口不同，不能证明LastImage/NextImage内部是否也改持久参数。现有材料未定位SetModuleCurrIndex；不可凭该名称补结论。导航具体步幅、两端wrap、自动停止及非本地源响应仍是native命令缺口；不得把缩略图前驱规则移给左右按钮。未补证前不能声称完整VM导航闭环。|
|辅助线入口|A `VMControls.WPF/ModuleResultView.xaml.cs:323`创建两CrossData；`:1859 AddCrossLine`向输出imageView的ShapeList加可见线；`:1883 Line_Click`首次开启第一线，初始化两模型，创建CrossConfig非模态窗口，Owner为主窗。`:1909`显示命令重加。|不是简单十字toggle。保持同一viewport/owner，工具栏打开真实配置组合；源切换后恢复模型可见项。未迁配置窗不能记完成。|
|辅助线模型与交互|A `VMControls.WPF/CrossData.cs:298–330`居中、隐藏、锁/解锁、首次Init；第一线50%中心橙色，第二线60%中心黄色；Init只一次。X/Y范围0..100000，宽1..10。A CrossConfig.xaml双行含显示、X/Y、宽、颜色、居中、锁。B `VMBaseControls.Base.ImageView.Shape/CrossControl.xaml:17–20`右键隐藏/锁定/解锁/居中；对应cs:358–398只允许未锁时捕鼠标拖动。|关闭配置窗口无回滚，不应造Apply/Cancel事务。隐藏只置IsVisible=false；锁禁止拖动。现有CrossShape/CrossControl模板已在Plugin.Wpf，但不能直接插ShapeList绕开唯一owner；需要公共库正式受管ICross层与原配置组合依赖，或明确禁用占位。不能用CrossROI或两条Geometry近似冒充。|
|输入/输出来源|A `VMControls.WPF/ModuleResultView.xaml:54,75`两侧共用ImageListComboBox；A `VMControls.WPF.DataForUI/ModuleDisplayDecorator.cs:230,262,281,318,1431`两集合与选择独立；ImageViewItem.cs:169 Clone不复制IsCheck/IsBackgroundImage。|输入源按实际输入关联筛选、独立选中，不能与输出下拉合并或名字匹配。原模板两侧均有checkbox/background按钮，不能宣称输入没有按钮；但独立overlay能力不能由外观推出。|
|背景角色|A `styles/moduleviewstyle.xaml:42,50`勾选执行SelectImageCommand，背景按钮执行ClickImageCommand；两者绑定实际ImageViewItem。A ModuleDisplayDecorator.cs:2024–2078背景切换须已有背景、目标已勾选且非当前背景，运行中拒绝并确认；旧背景转叠层，新背景从叠层移除并成为SelectedImage。ModuleResultView.xaml.cs:2169–2200选择背景显示叠层，选择非背景隐藏；`:2280`最多3叠层。|不是普通选中项装饰，也不是把任一来源设为图像底纹。命令实际统计输出ImageViewList、操作输出imageView，即使输入模板复用也不能自行扩展双侧背景管理。需实机补输入按钮行为；本方输出背景/叠层保存应同一有界状态与实际typed身份，不使用DisplayName寻源。|

## 本方已有链及集中实施设计

1. `src/UI/Shared/ImageInputSource.cs:51–97`的SelectImage走ConfigurationWriter；`src/UI/Shell/Windows/MainWindowImageSourceEditing.cs:41`将它接NodeConfigurationEditHost正式编辑和ResetLocalImageCursor。此链正确用于源配置/明确缩略图选择，不应用于全源导航逐项提交；否则产生多历史、半接纳并错误污染start。
2. `WorkflowExecutionManager.LocalImages.cs:12–29`已有root对象身份与源定义隔离的瞬态游标；`:42`冻结全部本地输入，`:186–235`完成后用TryPresentRuntimeCursor回投显示并复核root、reset版本和源实例。应复用这里的owner及执行锁，整批加入明确导航请求；不另开第二cursor服务。当前捕获会递归子流程，左右导航必须另明确当前层参与集合，不改变Group。
3. `LocalImageSequenceService`已有generation/iteration、消费快照、成功后推进、失败保留、StopAtEnd及取消排空。现有是完成后向前自动推进，不是已证明VM LastImage/NextImage语义。导航整批准备时须确定前后移动与自动推进互斥，执行取消/失败是否保留新游标需补原件证据后冻结；不能偷套当前forward completion。
4. 现有 `RunPreviewImageAsync`以advanceLocalImage:false运行，可复用一次运行准入与防二次推进原则；不能对每个源调用它。正式配置仍只由Host写入；运行/导航不进入撤销历史，不重放运行。
5. `ResultWorkspaceShell`已有PreviousImageCommand/NextImageCommand/CrossLinesCommand及实际item参数的背景命令。集中接Manager导航、typed输出源身份、同一ResultImagePane/NativeResultViewport owner；跨scope、退休publication、关闭、换源后命令不可命中旧对象。
6. 辅助线完整批依赖：原CrossData等价非视觉会话模型、原CrossConfig组合/资源、受管ICross渲染层、drag/contextmenu生命周期。公共库由主线单写；宿主不直接访问原ShapeList，不为配置新建第二viewport。源切换尺寸改变保首次初始化与手工坐标，隐藏/锁定状态按会话管理，关闭主窗清理dialog与订阅。

## 验收矩阵与当前出口

- 多个本层本地源/相机源、选中另一结果源、Group内源：确认实际参与集合、仅一次运行、无正式配置或Undo记录变化。
- 全部/勾选模式，首/末/单图/空列表、AutoPlay开关及StopAtLast，缺文件与某源错误：先补native命令证据，再按冻结语义断言，不能用测试自证猜测。
- 导航准备中切方案/删除源/编辑列表/取消/关闭：无旧游标提交、无半正式编辑、资源与执行slot排空。
- 两条辅助线完整配置、第一次位置/颜色、拖动坐标、居中、锁定、隐藏与右键、关闭配置不回滚、换图保状态、主窗关闭排空。
- 输入输出独立选择；输出1背景+至多3叠层、替换背景与旧背景转层、选择非背景时隐藏叠层；使用typed端点和当前publication，禁止名称回退。
- 真实设计器、主题/布局与原件截图、正式同源Release与人工验收均未执行。本次只交静态设计，不把缺口或已有模板当作完整迁移。
