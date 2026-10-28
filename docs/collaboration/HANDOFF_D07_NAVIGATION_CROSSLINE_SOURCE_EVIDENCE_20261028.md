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


## 原DLL导航补证核收（2026-10-28）

已读取统筹回件的probe/provenance并重新核对安装目录原DLL SHA256，提取记录及三文件hash见 evidence/D07_ORIGINAL_IMAGE_NAVIGATION_20261028.json。十个成功导航阶段逐项核名称和错误码；缺B阶段确认错误且SN=0，恢复后输出C。确认Next/Last首尾环绕、请求one-shot抑制AutoPlay额外advance、普通AutoPlay读前推进及失败消费游标。静态SetParam/Process证据由统筹交付native-3/native-0，未在本线重新反编译。

探针保留partial-solution-load=-536870093与originalRuntimeEquivalence=false，不扩大为整个宿主运行等价。AutoStop真实procedure分支、输入背景跨列表仍未知；SaveAs重载涉及附件重写，不据此推断参数保存。现行设计应以本层全部源一次瞬态导航+一次执行实现，不改正式配置起点或把缩略图选择路径代替导航。此证据不是本产品功能已实现。


## 执行游标整链差异与集中适配设计

已有实现核对：Services/LocalImageSequenceService.Begin以正式选中项初始化；Complete只在success且advance时后移，StopAtLast在后移前判定。WorkflowExecutionManager.LocalImages.BeginLocalImagesAsync先File.Exists过滤，CompleteLocalImagesAsync失败直接返回；RunSingleAsync成功才提交、失败明确不推进，连续链也以execution.Success门控。当前无Last/Next瞬态请求生产入口。这与原DLL已验证读前推进/失败消费不一致，不属于按钮外观问题。

整批边界与决策：

1. 保留现有Root实例弱引用、冻结配置/资源地址、ResetVersions、代号和输入override链；正式配置起点、UI显示、执行游标各自唯一职责。普通取图和导航同一序列服务，不另造导航缓存或逐源编辑事务。
2. 在完整运行准入时冻结本层全部参与源的导航意图（Previous/Next/普通），运行中拒绝；导航只作用本层，已有递归Group取图捕获行为不借此扩展或修改Group语义。只有真实源节点执行到取图边界才消费游标，不能因开始整个流程就提前消费未到达源。
3. 普通AutoPlay在实际取图前确定并接纳本轮游标；导航请求先确定相邻项并在本次抑制额外自动推进。端点环绕已证；勾选运行集合仍由原冻结plan提供，空集合明确失败。不得先过滤丢失文件改变序列：保留原冻结项，交实际加载器诊断，失败消费该项。
4. 需要把当前统一Complete(success)拆分职责为取图接纳与执行完成：取图接纳只推进该来源且具执行代号/迭代/节点身份，实际成功发布才增加SN；下游算法失败不能倒退已取图游标。取消在取图接纳前不消费，接纳后取消不能回滚已观察游标；重复/旧回调不得二次消费。精确取图接纳回执需核现有图像源执行链后冻结，不提前造公共合同或WPF回调。
5. AutoStop原owner运行态尚未实测，现StopAtLast与连续ShouldStopAfter职责先保留为待核差异，不宣称已同原件；导航与正式参数编辑/Undo历史独立，不以保存当前显示游标覆盖配置起点。
6. 同批贯通RunSingle、连续、单模块调试、通信触发、切源/换根/删除/取消/异常/关闭、UI真实来源选择及结果帧发布。验收至少覆盖A/B/C手动保持、前后环绕、AutoPlay首次/连续、导航one-shot、缺B失败后C、上游源未执行/多源部分执行/下游失败、重复回执与取消分界、保存重开起点不变和运行不入Undo。硬件与AutoStop未知分列，不改Group。

本节为集中设计与差异，未修改执行产品代码；原180/0不得作为这些新语义已通过。下一冻结真实取图接纳位置和全部调用消费者后，作为一次运行链批次协调批准与正式构建。


### 真实取图接纳位置核实

正式链为WorkflowExecutionEngine安装SourceRunInput→AlgorithmNode.Runtime.ExecuteRuntimeNodeAsync取得真实AdmittedIdentity/EnterRun并解析绑定→ImageSourceInvocationInputs.CaptureAsync→ImageSourceSession.Execute。调试链RuntimeDebugInvocation同样调用CaptureAsync及同一工具Session。CaptureAsync本地分支只检查取消、复制冻结地址，无IO；Session先校验设置、SN和来源一致性，再File.Exists和HImage读取。因此“节点开始通知”太早，整体完成太晚；缺文件也是一次已进入源的尝试，须保原失败语义。现SourceRunInput仅地址/SN，没有接纳回执或纯值尝试记录；不能伪装已有。

Manager完成后SN循环已按各source NodeResults.Success递增，而非所有SN随整流程成功递增，应保留，不因为旧注释误重写。当前Source游标却由整体CompleteLocalImagesAsync(success)门控，二者需在设计中区分。不能简单删除success条件：未执行源、绑定前失败源、取消前未进入的源都不应消费。接纳必须带真实Run/Scope/Node及代号，在共有CaptureAsync本地冻结输入校验后、实际读图前定义一次性纯值回执，或等价的运行内尝试记录；主流程只消费与本轮匹配的事实，不借WPF事件推断，不在插件里引用UI。真正接口须先核合同唯一写边界与准入失败语义，再整批实施。当前仅设计，不新增回调服务或公开类型。

现有ImageSourceDebugInvocationTests已覆盖缺调度输入、持久常量绕过及相机错设备/取消释放，应继续使用。LocalImageSequence相关成功后推进断言属于待替换行为证据，不能为了旧测试保错逻辑；新增源未执行/下游失败/缺文件消费/取消接纳前后回归必须同批落地。


## AutoStop真实owner补证核收及设计修正

最新结论以 evidence/D07_ORIGINAL_AUTOSTOP_OWNER_20261028.json 为准，替代上文“AutoStop运行态未知”的当前状态（旧缺口记录保留）。亲读两次probe/provenance，逐核每次四场景及清理状态，原DLL SHA相同。真实VmProcedure Flow1(ID10000)经ContinuousRunEnable调用原厂ContinousRunStart，非module.Run。AutoPlay开启，AutoStop=true时B→C末项进入处理请求停，C结果仍成功输出；C缺失时错误2148532230/SN0也停且不回A。AutoStop=false成功C后回A，缺C错误后也继续至A。Continuous=false早于IsRunning=false，排空必须等待两者均false。

静态由统筹回件定位VmModule.Root、VmProcedure连续setter与native-0:1223–1236/1345–1358：先确定CurImagePath，满足owner运行态和末项predicate请求StopOwnerProcedure，之后继续当前路径结果处理。因此集中实现应在真实来源取图接纳时记录“本轮后停止”决策，禁止立即取消本轮令牌截断末帧，禁止依success才停止或在失败时继续环绕。停止调度仍属Workflow层，插件不引用UI。未执行来源不触发停止。实际停止/取消/关闭沿既有排空和资源释放链。

第一次补测场景间IsRunning尚未归零被保护拒绝的日志保留，后补排空等待后两次复测一致。partial方案加载错误仍保；模板originalRuntimeEquivalence=false既不当本次失败，也不当全方案等价。四场景均AutoPlay，不擅自扩为所有手动/勾选模式。足以冻结本轮自动连续停止设计，不再重复取证；输入背景跨列表仍独立未知。本产品尚未按此修改或验证。
