## 2026-10-31 S3 当前批最终验证及人工验收

结果树表头采用已有原件语言资源；单变量按正式ID直接解绑，多变量选择指定目标；复制正式路径与原件同样对重复内容不重复提示，并复用原件非模态成功框，宿主卸载/关闭退出计时器和提示。剪贴板测试使用实例内替身避免再次干扰用户系统剪贴板；先前真实剪贴板恢复失败保留日志，不掩盖。

最终独立worktree UI Release 0错误/6388警告，全量0错误/6349警告；s3-copy2-regression.log 370通过0失败，真实结果绑定生产用例含单/多目标、撤销、保存重开、复制反馈及清理；表头独立宿主资源用例已执行通过。测试替身不证明Windows系统剪贴板在所有占用情况下成功。用户人工验收、实际VS设计器、主线集成、原件行可见性与成员级显示合同及动态回调闭包仍未完成；不得将整阶段记作完成。
## 2026-10-31 S3 结果树本批实现及验证

本批复用原件中英文资源和现有主题/结果树，表头接入共享字典；唯一绑定变量直接按正式ID解绑，多个目标继续选择，事务与身份校验保留。独立UI Release 0错误/6488警告，全量 0错误/6464警告。隔离真实宿主联合回归370通过0失败，包含表头和单目标绑定/解绑/撤销/保存重开。随后补充多目标选择解绑的单例诊断，先因测试事务外添加变量失败，调整为建方案前准备后通过；单例诊断预期退出2，不当完整回归。日志 s3-global-binding.log、s3-combined-regression.log、s3-combined-regression-2.log、s3-multibinding-diagnostic*.log 保留全部失败及修复证据。

本批尚未人工验收；S3原件行可见性、成员级显示合同、完整动态回调映射、复制反馈、实际VS设计器及主线集成仍待完成或核实。缺成员级声明的范围如实保留，不计完成；Group暂停。
# S3 模块结果树预核对（2026-10-31）

本记录仅为 S2 人工复验期间的只读准备，S3 未启动实施，不能据此认定原件对照或阶段完成。

- 主结果区：`src/UI/Shell/Controls/Shell/ImageResultControl.xaml.cs` 使用 `PropertyListView`，从 `MainWindowViewModel.ResultTreeRoots` 接入数据；绑定/解绑命令受编辑权限、行可绑定性及当前结果身份约束；关闭时清理来源、结果展示及主题资源。
- 编辑链入口：`src/UI/Shell/Windows/MainWindowResultBindingEditing.cs` 使用 `ProducerBinding` 进入变量编辑；其完整执行、保存重开、撤销与异常行为需下一批逐项核对。
- 独立只读分析视图：`src/Plugin.Wpf/Controls/Native/Composition/Results/PropertyList/DeclaredPropertyList.cs` 从 publication 借用 lease，按 128 项懒加载，变量编辑明确不可用。它不是主结果区绑定能力是否存在的判据。
- 原件入口：`D:/MyWork/REA逆向测试/analysis/decompiled/VMControls.WPF/VMControls.WPF/ModuleResultView.xaml` 及其 code-behind；XAML 包含变量管理列。仍需追查结果数据模型、模板、分页、命令、资源及宿主完整依赖闭包，并核对运行行为。
- 未核实：原件与当前列、层级、分页语义、状态、绑定生命周期及结果消费者的逐项一致性；真实 WPF 与人工验收。不得将已有同名入口当作完成证据。

当前等待 S2 来源选择器人工复验，S2 封版后按阶段顺序集中设计和实施 S3。
## 原件 XAML 与宿主新增定位

原件 `ModuleResultView.xaml` 的模块结果页明确有三列表头（结果名称、当前值、变量管理）、可折叠的错误信息行、双向滚动树、选中路径和复制按钮；层级模板分别将模块的 Outputs 和 IO 的 Children 绑定为下级。此为静态 XAML 证据，尚未证明具体分页、绑定或运行时一致性。当前宿主 `MainWindowResultPublication.cs` 调用 `DeclaredResultPresenter.Present(publication, bindings)`，其 Roots 经 `MainWindowViewModel.ResultTreeRoots` 提供给主结果控件；仍需追踪构建、切换、退休、绑定与原件的完整差异。
## 模板初步差异

`PropertyListView.xaml` 已复用相同的 40 像素表头与错误行、200/2*/1* 列结构、树和路径区域；当前三列表头及路径提示是中文常量，原件使用动态语言资源。原件路径文本格式化为 `VmSolution.Instance[...]`，当前显示正式引用路径；是否需按现行身份合同展示对应人类可读格式，应结合绑定及消费者完整链判断，不能将原件路径当作可执行接口直接复制。当前使用统一 `Children` 模板替代原件模块/IO 两种类型模板，层级行为是否等价尚待检查。上述均为源码差异，未经过运行外观及语言切换验证。
## 变量编辑与分页入口补证

`MainWindowResultBindingEditing.cs` 在提交前核验方案会话、编辑权限、变量归属和 `GlobalProducerBindingRules`；解绑还校验来源引用相同，通过 `CommitBatch` 提交并刷新展示。这里证明入口及保护存在，尚不能推断撤销、保存重开或原件语义一致。主结果 `ResultPresentationPresenter.cs` 自身调用 `GetChildrenPage` 并有退休路径；与独立只读分析视图的 128 项分页是不同实例，应分别验证，不混称同一生产链。
## S3 原件数据装饰器与当前展示器定位

原件数据入口补充定位为 `VMControls.WPF.DataForUI/ModuleDisplayDecorator.cs`：其 `ResultData`、`SelectedItemPath`、选择/复制、关联/解除全局变量命令及 `Dispose` 均为后续逐项闭包核查对象；关联命令以 IO 类型限定变量并调用原件绑定接口。现行主结果 `ResultPresentationPresenter` 从 publication 借 lease 并按 stamp/generation 守护、按页加载并在切换时退休；绑定由宿主正式编辑链负责。以上为入口与资源所有权定位，并未证明变量类型、错误、分页、回调和 UI 行为等价。下一步须逐方法核查 1024–1196、1485–1798 及其依赖，映射对应消费者与验证，形成完整批设计；禁止凭相同命令名直接认定已迁移。
## 原件选择与变量命令处理体对照

原件 `ModuleDisplayDecorator.cs:1709` 从选中实体 FullName 去除 `%` 后设置路径；复制遇相同剪贴板内容直接返回，否则复制并显示约1.5秒成功提示。当前路径来自正式结果身份，复制入口见 `ImageResultControl.xaml.cs`，提示行为需进一步核对。

原件运行中拒绝变量关联及解绑；关联弹窗按 IO 类型过滤、支持重复选择，同名不重复绑定，异常显示失败；解绑直接调用该 IO 的解绑方法。现行入口使用当前发布身份、编辑许可和兼容性检查，在弹窗返回后再次确认会话、publication及行身份；当前解绑先选择绑定到该结果的变量再提交。两者交互差异（尤其多变量与直接解绑语义）尚需沿原件结果行模板、绑定关系及正式变量合同整体判断，不推断单一动作等价。
## 绑定关系刷新证据

原件 `ModuleDisplayDecorator.RefreshGlobalBindingRelation` 会分别扫描全局变量模块及当前模块输入局部变量模块；`RefreshVarBindingRelation` 根据模块ID、参数名和组合根/类型匹配，把 `VarItem` 写入对应 IO 的 `Tag`。该字段为单值且循环中可覆盖，不能据此认定后端只允许一个变量，也不能由此直接决定批量解绑。需要继续追原件 `UnBindingGlobalVariable` 的实现与结果行模板，再结合当前 `ProducerBinding` 的一对多关系确定行为。
## 原件解绑目标已定位

`VM.Core/VM.Core/VmIO.cs:1506` 无参 `UnBindingGlobalVariable()` 读取当前 IO 的 `Tag`（变量名及所属模块），按该变量查找输出并计算组合字段解绑列表，提交后清空 Tag；Tag 为空时不操作。原件树行 `styles/moduleviewstyle.xaml:407` 和 `:428` 将当前行传给关联/解绑命令，Tag 为空时显示关联并隐藏删除；有 Tag 时显示变量名和删除。故原件从行直接解除**当前Tag所代表的关系及其组合字段**，不能据此称删除该输出的全部变量关系。现行多变量显示和编辑如何映射，仍须结合正式合同设计和验证。
## 生命周期与分页范围

原件 `ModuleDisplayDecorator.Init/DeInit` 除结果树外还统管图像、图形、输入映射、模块/流程回调、记录定时器；退出逐项退订、清树/选择/错误及停止定时器。当前 `ResultPresentationPresenter.Retire` 退订行和分页回调，清除结果树、publication/stamp并释放借用reader；其余宿主订阅在其他组件，需沿宿主分别核对，不能要求同一个类复刻全部职责。

原件 `CurResultScrollCommandExcute` 和每次500条的逻辑属于 `ModuleCurrentResultList`，不能直接当作模块结果树IO层级分页的证据。当前树的128项分页属于现行结果模型；原件树的子项构造和大集合行为仍需追 `VmIO.Children` 与原件视图实现后判断。
## 原件 IO 子项证据

`VM.Core/VmIO.cs:448` 的 `Children` 直接返回 `Childs`；`VmIOCollection` 是 `ObservableCollection<VmIO>`，增删时维护全部及叶子 IO 索引。原件 XAML 以该集合绑定层级，此入口未显示“更多”分页行。不能据此断言底层所有数据都立即加载，也不能将当前结果序列的128项分页直接认作原件交互；需核对序列值与IO定义的边界后再决定性能适配和展示。
## 集中对照及验证出口（尚未实施）

| 功能链 | 原件证据 | 现行入口与初判 | 验收缺口 |
|---|---|---|---|
| 三列与主题 | ModuleResultView.xaml及moduleviewstyle.xaml | PropertyListView同列比例，文本当前为中文常量 | 语言切换、深浅主题与WPF外观 |
| 树层级与分页 | 模块Outputs、IO Children；VmIOCollection可观察集合 | ResultPresentationPresenter声明层级与128项分页 | 对照IO与序列语义、展开/大集合真实交互 |
| 选择与路径 | SelectedCommand、去除百分号、复制成功提示 | 选择正式引用路径、CopyPathCommand | 身份映射、提示、键鼠及切换清理 |
| 变量关联与解除 | IO类型过滤，Tag关联，直接解除当前Tag及组合字段 | 编辑会话/兼容性验证，解绑先选择变量 | 多变量映射、撤销、保存重开、运行禁用与异常 |
| 更新与生命周期 | Init/DeInit订阅模块、流程、参数、映射及计时器 | publication stamp/generation、lease和宿主分工清理 | 原件事件与现行消费者逐项映射、重入与卸载 |
| 已有回归 | 不适用 | ResultPresentationPresenterTests、NativeResultCompositionTests覆盖层级、选择、分页与租约重入 | 对照真实宿主、原件行为和人工验收，不能以测试名代替证明 |

本表是实施设计的输入，仍需完成原件模板/资源身份、回调和现行绑定存储闭包，不能据此宣布S3完成。先复用正确链路，按完整批集中调整，缺公共基础的明确禁用及记录，不迁Group。
## 语言资源与设计时可复用证据

原件表头资源 `Lang_ResultName`、`Lang_CurValue`、`Lang_VariableManager` 的中英文值已在 `src/Plugin.Wpf/Themes/Native/Localization/zh-cn.xaml` 与 `en-us.xaml` 存在；现有 PropertyListView 表头却写死中文。整批方案可复用同一语言字典及动态资源，不另造翻译权威；应验证主题/语言切换和独立设计宿主资源解析。

`Design/Results/PropertyList/PropertyListDesignView.xaml.cs` 已用同一个真实 PropertyListView 和示例行构造树；需在真实VS设计器及运行时检查，而不是把样例视为业务或外观通过。关联/解绑模板分别对应原件按钮和现行命令，但资源身份及状态触发仍待逐项核对。
## 资源作用域约束

`ResultViewThemeScope` 在控件加载时订阅宿主 ThemeManager 并按实例替换深浅调色板，卸载时退订；语言字典 `LocalizationResourceDictionary.Instance` 在首次创建时由 CurrentUICulture 固定中英文，当前代码未提供运行时切换语言的接口。因此整批接入现有语言资源可以支持初始化时语言选择，不能未经证据宣称运行时切换语言；如产品确需热切换，应先核实正式语言服务而不是改共享单例。独立结果树示例已存在但其真实VS可用性仍需验收。
## 当前多变量身份与解绑决策约束

`MainWindowViewModel.cs:4392` 将当前方案全局变量按 `ProducerBinding.ReferenceExpression` 分组，多个变量名以顿号合并显示；`ResultBindingInfo` 保留正式 OutputReference，但显示字典仅保存合并后的名称字符串。原件按钮解除当前 Tag 指定的单个变量及其组合字段，当前行若显示多个变量则无法从合并文本唯一确定目标；故不能直接将删除按钮改为无选择地删除首项或全部。整批设计应保留变量唯一身份并提供明确、不可误删的目标选择；仅一个绑定时是否直接解绑须结合既有弹窗及原件完整行为验证，不修改公共持久合同。
## 结果行可见性规则

原件 `TreeItemVisibilityConverter` 对 `VisibleInResultTree` 或 `Visible` 为 false 的 IO 隐藏整行；变量管理区仅对支持类型且 owner 不是流程的 IO 可见，其他行为 Hidden。现行模板对无正式绑定能力的行保留变量区并禁用按钮，属于明确视觉差异；现行 publication 的 DisplayedPorts 只证明根端口过滤，成员可见性合同须再核实。整批设计应以正式声明与原件规则为依据映射，不能用类型名称猜测或把无服务的入口变为可操作。
## 成员可见性合同核对

现行 `IResultPublication.DisplayedPorts` 是端口ID集合，`ResultPresentationPresenter.Install` 按其过滤根输出；`ResultFieldPath` 虽能定位记录成员，用途是结果投影路径，并非成员显示开关。当前已核入口没有发现原件 `VisibleInResultTree` 的成员级对等声明，因此不能按字段名称推断隐藏状态，也不能将根过滤当成员过滤。此项作为合同依赖记录：保留现有声明真实展示，不新增公共合同或虚构原件可见性；后续合同稳定时补齐映射。
## 正式回归承接与剩余覆盖

`ResultVariableBindingProductionTests.Real_runtime_result_binding_unbinding_cancel_history_and_save_reopen_share_one_formal_endpoint` 使用正式宿主、真实模态变量选择、会话及保存路径；应先读取其断言并复用，重点补原件单Tag/当前多变量选择差异、实际运行锁定和异常。`ResultPresentationPresenterTests` 已覆盖大序列按页、嵌套展开、不同结果状态、旧publication退休及重入；`NativeResultCompositionTests` 覆盖真实树选择及只读禁用。测试证明的是各自受控场景，不替代原件资源、完整宿主或用户验收；新增测试只针对本批差异和真实未覆盖链。
## 根行名称与结果状态

`ResultPresentationPresenter.Install` 创建默认“结果”根行并将正式执行状态写入当前值；`MainWindowResultPublication.PresentResultPublication` 在接受发布后将同一个根行名称替换为选中节点名。因此不能仅看展示器构造代码就误判生产界面总是显示“结果”。原件根是模块对象、子项为Outputs；当前根/输出映射应复用，并验证节点切换、空声明、未运行、失败、历史和重入时不残留旧名称与状态。
## S3 整批实施设计草案（原件资源身份及回调闭包待核，尚未批准实施状态）

1. **视图与主题**：保留 PropertyListView 的原件三列、树、选中与路径组合；以现有共享语言字典接入原件三列表头，保留实例主题作用域和真实设计视图。不得把启动时语言选择称为热切换。原件资源及状态触发须补身份核对。
2. **声明与行状态**：复用 publication 的根端口过滤、根节点命名、正式状态和懒加载结果；按正式可绑定能力映射原件变量区可见性，明确区分类型不支持与服务暂缺/只读/执行中。成员级原件可见性缺正式声明，记录后续依赖，禁止猜测或扩公共合同。
3. **变量与历史**：复用 OutputReference、ProducerBinding、兼容性验证、编辑事务、撤销与保存链。单目标直达原件动作必须通过正式变量ID与重入检查；多目标必须明确选择，不按展示文字或首项猜测。继续使用原件视觉模板和正式对话入口；组合字段语义需与现行合同完成映射后决定，未证明的不可声称完整。
4. **树、路径与生命期**：复用现有选择/分页/lease/generation及宿主关闭清理；逐条对照原件展开、双滚动、选中路径、复制及提示，保持当前正式身份，不生成VM伪路径。原件IO子项集合不等于当前结果序列分页，先核行为再改。
5. **验证**：利用已有正式绑定/解绑/取消/撤销/保存重开测试及结果树重入/分页测试；补原件差异涉及的单/多目标、不可绑定/只读/运行态、两主题、独立资源、实际点击/键盘、清理与重入。最终同版Release和完整宿主回归后人工验收；VS真实设计器单列，用户此前同意暂不阻塞运行时。

该草案保留原完整范围，未完成原件资源身份/回调闭包及单目标细节的证据核实，不代表S3已实施。S2主线集成、叠层和设计器未完成事实继续保留。
## 结果树图标来源复核

根据 `Themes/Assets/Results/PropertyList/provenance.json` 对10项图标逐一计算SHA256，产品文件均与清单哈希及清单记录的原件提取文件一致：关联正常/悬停、展开/折叠、解除正常/悬停/禁用、告警、复制正常/悬停。此为文件身份证据，不证明原程序集嵌入资源身份、模板触发状态或运行外观一致；清单原状态为静态候选，尚须单列实际使用验证。
## 回调与撤销重载核对

原件 Init 将当前模块加入 ResultData，注册模块结果、参数重载、前驱及输入映射事件；参数重载时先去旧显示对象的过滤器和 ValueChanged，再装新对象；DeInit 对称退订并清空。`ModuleResultView` 的相关回调还涵盖图像、图形和流程，并非均属于S3树。现行树走不可变publication和generation/lease而不是订阅原厂对象；绑定编辑经正式会话提交。应在S3验证结果替换、Undo/Redo、对象重建和卸载时的真实消费者；无需复制原厂全局服务或把图像叠层、Group扩入S3。原件回调分支仍须与现行宿主调用逐项比对，当前只记录入口和职责。
## 现行回调与消费者映射补充

- 原件模块回调 → 当前 `PresentSnapshot` 验证会话、流程、节点及执行身份后发布；选中/来源变更通过 epoch 退役旧publication。
- 原件变量关系刷新 → 当前 `MainWindowProductionEditing.RefreshCurrentResultBindings` 重建正式引用/绑定显示并重新呈现当前publication；重入时检查所有者。
- 原件图像变更与宿主卸载 → 当前 `ResultWorkspaceChanged` 通知 presenter，`ResultWorkspacePresenter.Dispose` 退订；结果树及图像共用被接纳的publication但分别负责reader。
- 原件参数重载、动态输入和流程回调覆盖更大范围；目前仅确认上述入口存在，不以方法名推断全部覆盖。需依据 `ResultVariableBindingProductionTests`、实际切换和撤销回归核对消费者，保留尚缺的映射与证据。
## 行可见性实施约束修正

当前主结果 `FlatResultRow.CanBindVariable` 来自正式引用类型与存储可承载性；独立 `DeclaredPropertyList.Row.CanBindVariable` 恒为 false，原因是只读分析无编辑服务。二者共用模板，因此直接以该布尔值隐藏变量区会把“原件支持但此宿主只读/无服务”误判成“不支持的类型”。现有 `NativeResultCompositionTests.Read_only_tree_and_absent_variable_services_leave_real_link_buttons_disabled` 实际验证只读控件禁用和图标资源存在。整批设计必须先明确独立于编辑权限与服务可用性的行能力来源，再映射原件隐藏；当前合同不能区分者保留安全禁用，不制造可操作入口或盲改测试。
## 复制成功反馈依赖

已定位控件库现有原件 `Native.Display.VMControls.HikMessageBox.CreateMessageBox`，支持 `MessageIcons.Ok` 和 `MessageButtons.NoButton`；原件在复制成功且文本与剪贴板不同时非模态显示约1.5秒。其静态自动关闭 Show 使用 ShowDialogDelay，不能不核语义直接替代非模态。要迁移时需确保拥有窗口、UI dispatcher、关闭/切换时定时器退出及剪贴板异常行为，新增真实WPF回归；目前未接入，不计完成。
## 路径生产链待核风险

`ImageResultControl.OnResultTreeSelectionChanged` 将可绑定项的 `BindingInfo.ResultPath` 或行Key赋给路径栏；无有效选择赋空字符串，而模板仅在 null 时折叠。当前新增复制测试直接设置 SelectedItem 并手动传入测试字符串，验证复制/提示生命周期但没有证明真实鼠标或键盘选择 → 路径栏 → 复制按钮参数这一整链。需在用户验收版本不变的前提下，后续通过实际路由选择验证路径是否符合当前正式引用语义、空选中是否隐藏并防止复制旧项；任何修正整批回归，不能把现有370项当此链通过。

补充：以真实行的鼠标路由选择，再由 WPF ButtonAutomationPeer 调用复制按钮，使用隔离剪贴板替身的单例诊断通过，验证了路径栏、按钮参数及复制反馈链；日志 `Release/_validation/s3-routed-copy-diagnostic-3.log`。直接 RaiseEvent(Click) 不触发按钮的命令调用，前两次失败不能据此认定产品按钮失效。此为单例诊断，不替代整批回归，也未覆盖空选择或 Windows 系统剪贴板。

## 路径身份澄清（2026-10-31）

重命名链补证：`Workflow` 和 `WorkflowNodeBase` 均继承 `ObservableObject`；节点改名的 `RenameCanvasNodeCommand` 在执行和撤销时修改 `DispName`、`Name` 并刷新，根流程改名由 `RootWorkflowNameCommand` 进入历史并同步页签和执行快照。结果路径若需实时反映名称，须以选中结果的 ScopeId/NodeId 确定实体，并处理名称变化、选中项/结果更换、重初始化和关闭的订阅清理；还需覆盖 Undo/Redo。`ProducerBinding.DisplayPath` 使用持久化名称快照，不能直接认定自动跟随改名。当前证据不证明结果栏已订阅这些变化。

当前已有完整展示格式：`src/Workflow/GlobalVariable.cs` 的 `ProducerBinding.DisplayPath` 是 `WorkflowName.NodeName.ResultDisplayName`，`InputSourceDisplayText` 消费它；这些名称字段是显示快照。与其并存的 `ReferenceExpression` 是版本化序列化的 `OutputReference`，是唯一生产者持久身份；不能将点分名称作为可解析身份。`ImageResultControl.EditResultVariable` 已在正式作用域和节点 ID 唯一匹配后构造这三项展示字段。针对 S3 可复用相同显示约定与身份校验生成当前显示内容，但必须另外覆盖名称更改、名称含分隔符、重复名称、结果撤销/退场与宿主切换；尚未证实名称快照自动随重命名更新，不能声称复制文本可以作为重新绑定输入。当前人工验收版本未变。

原件 `VM.Core/VmFrameTreeBase.cs:43` 的 FullName 向父级遍历，以各实体 UniqueName 和点分隔构造，IO 有父级时以括号包围其 UniqueName 并跳到 Root；用户变量模块有特殊标记。`VmIO.cs:452`、`VmModule.cs:720`、`VmProcedure.cs:324` 均以 Name 作为 UniqueName；原件选择后去掉百分号再显示复制。现行 `ImageResultControl.EditResultVariable` 已从正式 ScopeId/NodeId 查到工作流 Name 与节点 DispName，并将其与正式 ReferenceExpression 分开保存；工具参数页 `RuntimeNodeConfigurationControl.FormatReference` 使用节点显示名、声明输出显示名及成员路径。上述能力说明可形成用户可读名称，但本项目名称层级、嵌套成员及可复制路径解析合同不能凭 VM 拼接语法推断，尤其不能把展示名称当持久身份。需集中核对重命名、同名及无声明项后再实施。

此前“复制正式路径”表述不准确。`ResultPresentationPresenter` 用端口 ID 和成员路径构造 `ResultBindingInfo.ResultPath`，例如 `output.width`；界面复制的是此结果内局部路径，不含流程和节点身份。正式持久化绑定身份另由 `ResultBindingInfo.ReferenceExpression` 序列化 `OutputReference`，包含来源节点、流程等身份；两者不是同一字符串。原件选中时取 IO 的 `FullName` 并去除 `%`，不是当前这个局部路径。现有自动回归只证明局部路径经选择、复制按钮与剪贴板替身传递一致，不能证明复制值等价于完整 VM 路径或可直接作为当前正式绑定表达式；当前工具提示“正式引用路径”可能误导。完整用户可读路径格式与复制用途尚未取得充分合同证据，列为 S3 待确认差异，不借自动测试通过认定原件等价，也不在人工验收期间更换程序。
