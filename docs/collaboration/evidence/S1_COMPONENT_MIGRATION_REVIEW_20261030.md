## 2026-10-30 S1普通启动修复完成，版本冻结人工验收

初次PID14356普通启动在App.ShowStartPageAsync:425发生DateTime.MinValue转DateTimeOffset越界，359项组件通过不代表实际启动成功。现由StartPageSettings内部适配先排除无路径/无访问时间的目录推荐，再沿原偏好优先、去重、隐藏和排序规则聚合；不伪造现在时间、不修改公共起始页或持久化合同、不改用户方案和配置。失败堆栈保留Release/_validation/s1-startup-recent-time-failure.txt。

最终UI构建0错误/6484警告，全量0错误/6446警告；attempt-037为360通过/0失败，回归前后最终产物身份一致，包含空时间/正常日期、历史聚合及实际起始页构造。最终日志s1-startup-time-ui-build.log、s1-startup-time-full-build.log、s1-startup-fixed-regression.log均在Release/_validation，S1_FINAL_ACCEPTANCE_CANDIDATE_20261030.json已更新。

实际普通启动已确认：PID116840，主窗口标题为太阳眼视觉 - 机器视觉平台，窗口非零且响应，新日志D:/SunEyeVisionData/Logs/2026/10/30/SunEyeVision_225103.txt无App级未处理异常。用户要求启动后截图，现在保持此版本，不构建、不重启、不关闭。用户及实际VS验收仍未完成，S1不封版，不提前推进S2，不合并主仓。

以下为此前按基线保留的实施/验证历史，当前运行状态以本节为准。
## S1当前交付候选：原件开关与完整生产接入（2026-10-30）

本批按S1_FINAL_COMPONENT_MIGRATION_PLAN_20261030.md集中实施，复用既有成果；总监正式提交e83b066d的PCUI UserControl家族已接入，中立类型为ContentToggleSwitch，未混用Apps.XmlUI参数开关，也未保留旧PCUI别名或本地AutoSwitchStyle。原dark两图及每实例独立内容、原事件sender语义、TwoWay纯UI投影、回填抑制、拒绝恢复与Attach/Detach退役同批贯通。模式、标题、提示和菜单沿现有语言资源，不新造全局语言服务。

最终验证是当前独立工作区正式Release，不是主仓已合并产物：全量Release 0错误/6487警告，规定UI构建0错误/6484警告。UI增量构建改变了前一已测Wpf DLL，故未套用前一attempt-035成绩；冻结最终DLL后重新执行attempt-036，359通过/0失败，回归前后产物身份一致。最终证据见S1_FINAL_ACCEPTANCE_CANDIDATE_20261030.json、Release/_validation/s1-final-acceptance-product.json及s1-final-acceptance-regression.log。单例诊断现在要求恰好执行一个用例；此前零用例attempt-033不算通过，真正刷新焦点诊断及最终整批均已执行，原记录保留。

新增真实主结果宿主回归覆盖：内部TO两向Click、正式接纳/拒绝恢复、一次历史、序列化、Undo/Redo、旧处理图像只显示且无结果权限、零隐式执行、只读恢复、卸载/重载与一次事件；另核实当前来源重入不会落到新源、语言重建不改模式、独立Image实例。缩略刷新实际容器和焦点保持、下一方向键正常，因此未重写正确的不可变投影。实际WPF off/on两态已查看attempt-036/source-strip-original-off.png、source-strip-original-on.png；不冒充原机运行差分、VS或用户人工通过。

| 范围 | 当前决定与证据 |
|---|---|
| 原件完整职责盘点 | 五文件143声明全部归到功能/必要适配/未实现或无行为声明，未分配0；S1_MEMBER_DISPOSITION_20261030.json。内部字段、空方法和生成代理不是新增开发任务，分类不是运行等价证明。 |
| 22属性、33名称域、十套模板及21资产 | 原逐项证据更新至本批源码；21图中15张直接提取路径一致，6张折叠图归一化候选一致，原嵌入资源名映射仍未核，不据哈希宣称完整性。原ImageView子控件全文已核，两个鼠标处理体为空，Uniform视觉保留，像素转换沿现有冻结ImageSource适配。 |
| 既有本地功能链 | 导入不自动激活/运行、拖放拒绝规则、普通/快键选择、勾选与Ctrl/Shift、首图四组合、删除游标及下一真实读取、取消、保存重开、历史和资源释放继续复用相称生产证据，不拆成零散重写。 |
| 明确未实现分支 | 无正式基础的远程、RP/3D和订阅目录不计完成；不暴露可误用的远程菜单，RP禁用折叠；普通添加目录不冒充订阅。Group和主界面整体重组未扩入。 |
| 保存语义 | 仅持久化图像引用与设置，不打包原图或缩略图，重开按原文件按需重建；用户询问不扩成打包实施任务。 |
| 人工及设计器 | 源码与构建/自动/独立WPF外观核对已分别记录；当前无实际VS设计器通过证据，用户人工验收未完成。S1候选可启动测试，但S1不封版、不提前启动S2，不合并或推送主仓。 |

后续先在冻结版本人工验收：文件/目录导入不运行，左右方向键快切与旧运行完成不回弹，开关和全部/选中双向切换，首图/勾选/删除/撤销与保存重开；另在VS打开Plugin.Wpf/Design/ImageSources/ImageSourceStripDesignView.xaml验证同一真实组合。发现差异按整条功能链归并，不恢复逐点试错。

以下保留历史阶段摘要和原始证据；其中旧“当前”只指当时基线，以本节为现行结论。
## S1当前核收与交付状态（2026-10-30，模板批验证后）

当前产品包含结构输入批5431c9fe及本批运行勾选模板/主题恢复。独立工作区正式Release全量构建0错误/6487警告；ImageSourceRecovery attempt-032为356通过/0失败（receipt exit=0）。此前attempt-031在300秒总时限终止，198通过/0失败但未跑全；随后仅显式延长同一套用例时限至900秒，默认时限不变，旧超时记录保留。新增实际源条回归核实原件两套焦点模板、空/有内容切换、勾选图像、禁用系统文字色、深浅主题与卸载重载；产品没有新增公共控件或执行接口。独立运行回归不等同VS设计器、原机运行差分或人工验收。

| 核收范围 | 当前结论 | 证据/后续 |
|---|---|---|
| 33命名声明 | 已逐次记录当前映射；纠正外层list与内层lst误映射，不能按同名判等价 | S1_NAMED_ELEMENT_REVIEW_20261030.json |
| 21张资产 | 15张直接提取路径字节一致；6张折叠图为归一化提取候选一致，嵌入资源名映射仍未核 | S1_ASSET_IDENTITY_REVIEW_20261030.json及复验脚本 |
| 十套局部模板及焦点依赖 | 已保存逐节点、逐属性差异；六套列表/滚动无结构删减；焦点两模板除中立键改名外参数相同 | S1_LOCAL_TEMPLATE_REVIEW_20261030.json；资源/主题适配不冒充运行等价 |
| 22属性 | 已逐项记录产品承载、必要适配与未证边界，不等于全部143声明完成 | S1_PROPERTY_IMPLEMENTATION_REVIEW_20261030.json；其余121声明仍待完整审核 |
| 自动切换 | 本地ToggleButton替代尚未退出，所需PCUI布尔家族无中立正式接口及独立验收基线 | 总监WORKFLOW_BOOLEAN_CONTROL_HANDOFF_20261030.json；不可混用B01或旧SDK开关 |
| 语言及部署分支 | 已有启动语言查询基础，但源条模式文字硬编码；无正式动态语言事件。远程/RP/订阅目录分支未实现，不计完成 | 不新造全局语言或远程架构；保留差异，下一批须先完成剩余声明集中设计 |
| 阶段出口 | S1未封版；未启动新用户验收；不推进S2、不合并主仓 | VS实际设计器、WPF外观和用户人工验收仍待完成；Group边界不变 |

日志：Release/_validation/s1-template-focus-build.log、s1-template-focus-regression-extended.log及S4-20261025/ImageSourceRecovery/attempt-032/receipt.json。

保存行为核查：当前方案持久化local.paths/local.selected、配置起点、运行方式及自动切换等正式字段；原图内容和缩略图缓存均不进入方案。SolutionResourceResolver.RebaseCopy与SolutionManager.CopyOrSaveAsSolution只重定位引用并写方案JSON，不复制图像；缩略图重开后由可视范围按需读取原文件重建。原文件移动/缺失会影响加载，不能把仅保存引用描述成图片已随方案打包。此次用户询问未被擅自扩成图像打包新实施批。

以下保留此前取证与实施历史；旧产品状态和旧测试结果以其当时基线为准，不覆盖本节当前结论。
# S1原件到宿主整链核收决定（2026-10-30）

基线：产品49b33e70；原件身份见S1_SOURCE_COMPONENT_INVENTORY_20261030.json。本文是已审行为链的实施决定，不将未审143声明、资源闭包或动态调度自动记作完成。用户不再承担逐点发现原件机制的任务。

| 行为链 | 原件证据 | 当前实现位置与实证 | 决定及出口 |
|---|---|---|---|
| 父级参数、全部/选中、自动开关 | LocalImageListView.xaml/.cs的OnRootChanged、cbxMode_SelectionChanged；参数变化不直接调用Run | ImageSourceStrip/Session的SetMode/SetAutoSwitch；BindLocalImageSourceEditor统一提交并退休结果，attempt-014已复现AutoSwitch清图 | 复用字段、持久化和门禁；核对配置失效与实际渲染消费者，禁止无条件清图或无条件保图。不得把过期结果标为当前；保留处理后显示不能偷换源图。显示所有权决定未完成前不补留图回退 |
| 文件/目录导入 | DoAddImages/AddImageLocal/AddImageByDir→UpdateImageListSource；原入口不直接Run；GetModuleSel抑制程序性选中再次运行 | ImportCoreAsync原子提交→AddImages(selectFirstAdded:true)→ExplicitSelectionAccepted→本地预览；attempt-014两类真实新增均执行开始0、视口换local-preview | 复用枚举、正式提交、取消、一次历史和释放；把导入配置更新与用户激活/运行分开审核。当前证据说明自动预览，不外推物理文件框全路径绝无误激活 |
| 运行遮罩 | ImageListPanel.xaml:372末尾windowShadow；派生ChangeShadowDataSource将根流程作为DataContext，绑定IVMRun.IsRunning | 当前源条无对应元素；原先禁用整条导致运行后焦点丢失，已移除 | 恢复原件独立Grid及同版WindowShadow资源，按所属根流程/连续状态适配；不能改成禁用ListBox或抢焦点。必须验证根切换、停止、失败、卸载清理，不把遮鼠标当键盘保护 |
| 用户鼠标与物理键盘 | Grid_MouseDown仅Ctrl/Shift拦截；ListBox选择变化→lst_SelectionChanged；图形左右按钮另有IsRunning检查 | 当前普通输入已走条目焦点/选择链；真实运行后焦点回归已通过；快键运行中RunWorkflowWithImage早退丢新意图 | 保留已正确默认导航，不再自写左右算法；整批核忙态请求、程序恢复抑制和选择身份。原native忙态/特殊分支未全证，禁止假称latest-only。执行按钮正常与快键回弹分开 |
| 完成回调和列表回写 | OnPreduceResultCallBackArrived→DoRefreshLocalImage→TargetValueChanged；50ms/100ms单槽为回写节流，GetModuleSel设置needSelectionTrigger | LocalImages完成回执→TryPresentRuntimeCursor校验root/source/编辑版本；未接纳导航不推进版本，旧结果可改回选择 | 复用执行快照和实例检查，补清导航意图与执行显示的关系；不能用编辑版本充当未接纳导航版本。执行调度策略和UI回写节流必须分开，不加延时掩盖 |
| 缩略图、滚动及图像所有权 | Loaded后台批次、lst_ScrollChanged可见需求、LoadThumbnailImage与GetImageEntitybyPath | Session有界需求、缩略生命周期、稳定集合和generation/owner；1051真实宿主滚动及释放已有同版证据 | 正确非视觉读取/所有权复用，不复制VM全局服务或旧线程销毁缺陷；待逐字段映射原件位置、异常和图片格式，不靠测试数量推定所有资源已核 |
| 根/源切换、加载卸载 | OnRootChanged清缓存；ImagePanel_DataContextChanged撤旧流程事件、设置遮罩；Unloaded撤订阅/线程退出 | 宿主RefreshSourceSession/ReleaseSourceSession，Session.Dispose及控件Detach已覆盖owner/取消；运行遮罩订阅尚缺 | 保留更严格资源清理；父级状态与遮罩纳入同一来源所有权。停止、异常、切换不能留下旧蒙版或迟到回调。不能只移植IsRunning布尔外观 |
| 本地/远程/订阅/RP入口与部署 | Pop1/Pop2按原条件选择本地或远程；Subscribe与RP依外部SDK/目录 | 本机部署明确不提供远程/订阅/RP运行基础，已有入口差异与用户不显示虚假下拉要求 | 保留原完整需求追踪，按既有授权明确占位/禁用；缺基础不伪造实现，不以删除Pop名称当全行为完成 |
| 样式、主题、语言、设计态 | 两级XAML、动态资源、OnLanguageChanged、IsInDesignMode及BAML | 原资产已迁，源条和工具版背景不同；宿主曾误覆盖橙色；真实VS设计器未核 | 每项资源按所属原件核SHA/消费，不混共享版与工具版；组合改名需证据；语言/设计态未核不能记全迁 |

## 已确认的复用与必须解决的边界

非视觉正式能力已存在：ImageInputSource原子接纳、执行/历史同一lease、读取接纳、根/节点/来源实例身份、取消释放及两独立原件视口。无须重写调度引擎或将VM全局业务搬入控件。当前不能复用的是“配置提交必清当前视口”“忙时无记录地吞导航”“导入程序性选择与主动激活混在同一条发布语义”这些差异。

原件143声明仍须逐项归入上述链或生成/空方法，不因本文按链评审而全部改成通过。本文与清点JSON一同交逆向负责人核对完整原件闭包；最终实施批应包含对应的父级状态、遮罩、配置/导航/结果分流、全部消费者及取消关闭，不拆成单独蒙版或单独按钮交付。
## 完整组件交付复核（2026-10-30，原件新证据）

已读取逆向负责人的《S1图像源组件闭包总台账》《S1原厂旧图清理条件矩阵》《S1共享图像源事件实施对照》。23个直接事件声明已逐项找到原入口及候选落点，但不包含动态订阅、主窗16事件和WPF类处理器，不能据此封版。原件运行遮罩是独立Grid，不能用禁用列表代替；本方现有SessionPropertyChanged、Detach和ReleaseSourceSession提供状态通知及退出清理落点，但尚无运行状态投影，须与所属根流程和连续状态同批贯通。

重要纠正：原件AutoPlay参数变化可在ParamModifyTriggerRun及父节点条件满足时清Display临时缓存并触发执行；最终画面由RenderControlBase的owner、执行计数、临时值、回查及具体回调决定。安装配置False只是静态快照。故attempt-014能证明本方当前动作清图，不能单独证明与原件不符。新审计测试的automaticPreserved无条件断言目前不是有效的VM一致性验收标准；保留红色记录供追溯，不将其改绿冒称问题解决，正式断言须按条件矩阵和当前宿主合同集中替换。

本批设计出口固定为：程序恢复/导入与主动选择分流；源选择、请求接纳、执行采样及完成回写身份明确；原件遮罩及根切换/停止/失败/卸载状态闭合；配置变化与显示有效性/像素所有权分别处理。缺失native忙态证据不能用自拟latest-only顶替。产品版本49b33e70保持不变，未启动新验收轮次。
## 运行状态消费链在制实施

已集中接入共享原件末层遮罩：ImageSourceStrip新增IsRunning视觉依赖属性与RunningShadow，沿原件Margin和共享WindowShadow色值#44F0F4F9；不是工具版白色资源，不禁用ListBox、不抢键盘焦点。ImageResultControl按所挂来源根Tab单向绑定，ReleaseSourceSession先清绑定再释放Session，卸载/切源沿同一退出链。

第一次实际工作流方向键回归明确失败：Tab.IsRunning原有状态只覆盖部分入口，单次运行未显示遮罩。已核RunSingleAsync并仅补既有根Tab视觉投影：接纳后的try内在UI线程置true，原执行租约释放后finally在UI线程置false；不改变采样、执行门禁或请求调度。此改动影响Tab运行图标、State和现有CanEdit消费者，须沿真实单次及取消/异常出口验证，已告知技术总监共享hunk范围。全量Release构建0错误/6385警告；遮罩显示/结束隐藏、列表保留焦点、卸载解绑定/旧根通知无效/重新加载恢复纳入同一实际运行回归，同一真实运行单例已通过，见Release/_validation/s1-running-shadow-production-recheck.log；单例返回非完整回归状态，不计整批通过。

这只是S1在制批的一部分，不独立交付、不宣称解决物理快键回弹，也不推进S2。运行产物已由本地构建更新，但未自动启动新验收。
## 快切整链适配设计：配置、选择与读取分离

原件已证顺序为先写模块路径、再检查就绪并发起运行；未证原生所有忙态/排队策略。本方不能放开执行中的配置编辑门禁，也不能把用户新选择丢掉或声称自动执行。适配复用现有每根每源运行游标及ResetVersions：列表Activate只登记瞬态选择并更新源显示，不写配置历史；配置起点/首图、导入、删除、勾选、模式仍走正式编辑。此处属于冻结快照宿主的独立适配，不标作VM内部latest-only算法。

| 状态/入口 | 生产处理及消费者 | 验收出口 |
|---|---|---|
| 空闲Activate | MainWindowImageSourceEditing验证方案/根/节点/源/条目/版本，经理从正式policy生成同一LocalPlan定位entry，登记运行游标与递增reset；源显示选择；ImageResultControl再请求单次 | 先选择后运行；实际读图就是所选项；导航不改变配置/历史 |
| 运行中Activate | 同一瞬态选择接纳，reset使旧轮游标/显示回执失效；保留原配置锁与当前轮输入快照，不另排队/启动执行 | 真实路由连续键能到末项；旧完成不覆盖；实际执行次数不伪增 |
| 下一次明确运行 | 经理在BeginLocalImages读取已登记选择；为未读取的显式选择记录pending标志，只抑制该源本次自动推进；读接纳完成后清pending | 普通运行/执行所选均读最新选择；其他源保持既有推进；失败未读不消耗选择 |
| 旧完成/取消/失败 | 原reset/version/owner屏障继续工作；只有匹配当前reset的真实读取回执才能更新运行游标和显示，未读取不消费pending | 迟到/异常不清新选择；取消不回滚已接纳读取 |
| 根/源替换和卸载 | 复用root对象身份、来源对象/节点、条目reference/address、Session取消与解绑；不得让旧来源选择写入新来源 | 切换、同ID替换、卸载后回调失效；不残留执行请求 |
| 配置编辑/撤销/保存 | 继续正式policy与history；配置定义变化不复用不同定义的运行游标；保存只存配置、瞬态选择不新增持久合同 | 保存重开还原配置；无导航撤销噪声；首图及模式语义保留 |

完整修改落点限ImageInputSource的瞬态显示/通知，MainWindowImageSourceEditing的选择及运行校验，ImageResultControl的先选择后忙门，WorkflowExecutionManager.LocalImages现有游标/Begin/读取/完成消费者，以及对应真实宿主与已有失效/重入测试。不新增SDK/插件公共合同、不修改算法、不拷贝VM全局服务。先复现正式执行开始边界内的真实按键，原红色日志保留；完成批再全量构建并作相称回归，不单独交付一个防回跳补丁。

### 2026-10-30 S1运行遮罩与快切生产链集中修正（未封版）

已复现真实执行开始边界内连续右键从第2张到第6张却恢复第2张的失败，红色日志s1-fast-keys-production-diagnostic.log保留。本批复用现有每根/每源运行游标与reset身份，将用户瞬态选择和配置起点分开；导航不修改配置历史，忙时仍登记选择，旧轮输入快照不变、不新增执行队列。待明确读取的选择仅抑制对应源下一次推进，真实读取接纳后消费；其他源保持原推进。完成回写仍受root/source/reset/条目/reference/address约束，显示通知增加重入代次屏障。首图、导入、删除、模式和勾选继续正式编辑合同。此为冻结快照宿主的必要适配，不宣称VM原生latest-only或完整忙态结论。

原件运行遮罩、根绑定及退出清理一起纳入本批；RunSingleAsync补齐所属根Tab视觉状态try/finally投影。全量Release构建0错误/3493警告；ImageSourceRecovery attempt-019为350通过/0失败，覆盖原21失败承接。快键补证同时确认：旧运行结果像素仍为第2张，不把旧结果伪称第6张；选择维持第6张，下一次明确运行才实际读第6张。新增像素身份断言在s1-fast-keys-frame-identity-recheck.log单例通过，不重复宣称整批数量。

补充WorkflowHost旧轨首次因旧测试程序集引用已移除ProtocolParse失败；尝试当前源码编译又发现旧目录API和SDK测试合同未同步。未为其恢复旧插件或扩大架构重写，验证宿主试验已撤回，失败证据保留，旧RootWorkspaceHost整体不计通过；该文件的导航消费者已按新合同同步，待当前基线测试链具备后核实。350项真实结果宿主、源保存/历史及运行链的已通过证据仍独立成立。

未完成范围：配置变化与结果显示的原件条件化对应（包括AutoSwitch清图观察）、完整资源/字段/宿主事件核收、VS实际设计器及本批人工验收。无条件保图断言已按原件条件矩阵移除，继续记录观察，不冒称显示差异已解决。S1未封版、不启动新验收、不推进S2—S4或Group、不合并主仓。
## 配置显示消费链设计（下一集中实施批）

已有基础：NativeResultImageFrame已持有独立像素读者，ResultPublication借阅与该像素读者拥有不同退出点。无需新增公共显示合同、复制完整位图或把源文件冒充处理输出。原件矩阵已证缓存清理不等于主图清理；本宿主没有启用参数修改自动执行功能，本批只处理不改变文件目录/配置起点的AutoSwitch、运行模式和勾选修改。导入/删除/首图及原参数自动执行分支继续独立核收，不能以本批保留像素宣称全部配置行为一致。

在正式提交前准备已有图像/来源/根/节点/代次的留存凭证，提交接纳后、结果失效前才激活；失败不激活，更新输出或重入切换优先。激活后立即释放结果借阅，ResultImagePane.CurrentFrame和BorrowCurrentImage均不再证明当前结果；视口只借其既有独立像素读者继续显示，旧ROI、图形、高亮按原证明退休。当前结果表、绑定和工具消费者保持正式失效，不延长算法结果有效期。

刷新空计划时仅在同方案、同根、同源、同选中节点及当前配置代次范围内保留像素；新的有效输出立即替换，明确清图、根/节点/来源切换、配置历史重放、卸载/关闭释放；一次新运行结束而未产生图像时清掉保留画面。主窗仅发内部显示事务和根运行结束通知，工作流执行层不访问控件，不增加SDK/插件接口。所有订阅和准备凭证均沿现有Presenter.Dispose及源配置writer的try/finally退出。

验证出口：正式模式/AutoSwitch/勾选修改后旧输出确实退休、结果借阅拒绝，但旧真实像素不丢且资源所有者释放；连续配置修改不累积读者；提交拒绝/通知重入不覆盖新图；清图/切换/运行无新图/卸载均释放。只使用正式Release和现有真实宿主，不将预期写成原机实测，也不以该批通过封S1。

### 2026-10-30 S1非结构配置与最后显示图像整链（未封版）

本批复用NativeResultImageFrame已有独立像素读者，无复制整图、无新增Plugin.Wpf/SDK公共显示合同。文件目录和配置起点不变的自动切换、运行方式和运行勾选修改，经主窗内部SourceDisplayPreservationScope在正式接纳后、结果失效前转为仅显示：释放结果借阅，ResultImagePane.CurrentFrame/BorrowCurrentImage不再授权当前结果，旧ROI/高亮按原证明退休，但真实旧像素仍由原视口持有。新的有效输出替换；明确清图、结构修改、上下文变更、根结束无新图及卸载关闭均退出。准备凭证包含方案/根/节点/来源/配置代次，提交拒绝不生效，选中节点重入不跨上下文留图。

真实顺序取证定位了原共用状态问题：RetireResultPublication和用户清图都调用ClearNativeImagePresentation。本批以独立显式清图代次区分，不因结果退休误杀像素；源投影安装后的ConfigurationEdited再消费显示有效性，防止结构清空跨代次残留。仅显示状态不延长算法结果有效性，不把源文件冒充处理输出。

全量Release最终0错误/3391警告。ImageSourceRecovery attempt-026为352通过/0失败，仍承接原21失败；覆盖连续配置、实际像素不变、旧借阅已关闭、新图替换、明确清图、真实失败运行、卸载重载、结构性清空、拒绝及选中节点重入。首轮保图失败及顺序JSON日志保留，测试夹具自持根快照已显式释放后再验证生产读者替换释放一次，不把夹具所有权误算成产品泄漏。VS实际设计器和本批人工验收未做，不以自动通过代替原件外观验收。

范围未缩小：本批只收口上述非结构配置显示链。导入/删除/首图的程序性选择、拖放与原件剩余字段/资源/宿主事件仍需逐项核收；原参数修改自动执行与native忙态完整分支没有伪造为已实现。S1仍未封版，不自动启动验收、不推进S2—S4/Group、不合并主仓。
## 结构操作核收设计：导入、删除、首图与拖放

复用已有正式原子提交、一次历史、取消/generation、路径身份及缩略图退出，不重写已通过的首图四组合。原FirstImage_Click按运行全部/选中及AutoPlay四组合写首/末候选，方法体没有Run；本方FirstResetIndex与运行读取对应已有正式回归，保留。原DoAddImages和AddImageToList只写入及追加列表，不像当前ImportCoreAsync那样强制选择新添首项；原生空表首次、追加和删除游标需沿AddImage2CircleList的FUN_18000b820、DeleteImage的FUN_18000b680/FUN_18000b920与CurImagePath发布闭合，已集中请求统筹补证，不先猜第一项策略。

本批已证须替换的入口是拖放：JudgeDrage逐项要求File.Exists及.bmp/.jpeg/.jpg/.png/.tif/.tiff，目录/丢失文件/混合非法类型均拒绝。控件DragEnter/DragOver和Drop使用同一检查，Drop再次验证后才冻结路径并进入现有ImportFiles正式链。ImportFiles只消费文件，文件夹枚举保留在ImportDirectory按钮；不保留被误加的目录拖入扩展。验收以真实命名列表的路由DragEnter/Drop、非法集合不改配置/历史、合法文件一次正式导入且不发起工作流为出口；物理桌面拖放仍须最终人工核验，不把构造事件当成OS操作。
原生补证已核收：S1图像源新增列表与当前路径原生静态证据（F8D17BE0DADE384C3FB4B5DB376897D47BF36F257E2A4BA8A9FD16CADF6228FD）与删除游标证据（178302A8D561A7DC0FACC63A6EEC9EA55D21042467BD01FCFE94EE6E1587CCA4）。普通追加不主动改游标；清空后保持-1；CurrentImagePath无效游标的首项返回不写回索引，CurImagePath已核发布在Process；唯一正常路径下删当前置-1、删前减一、删后保持。重复路径/异常别名和未提取发布点仍不作全域结论。

实施决定：ImportCore不再selectFirstAdded；程序性追加与主动激活分开，不触发RawPreview或工作流。Source.RemoveImage删除当前显示项时显示置-1，配置起点被删也置-1，其余按原记录身份移动。源正式投影安装后的ConfigurationEdited沿现有主窗入口同步每根每源运行游标的新定义：保留追加/删前/删后的实际记录，清当前置无选择；保留尚未读取的显式导航标志，首图正式reset已清旧标志；仍用reset/根/源/引用身份阻断旧回写，失败记录已接纳但运行不可用，不能让配置已变而游标静默失配。追加可复用上一批只显示像素机制，仍退休旧结果权限；不自动解码新添文件冒充新输出。清空及其他结构退出按现有显示清理处理，原机渲染间接边界仍保留。

首图高亮复用原IsReturnFirstForce语义：独立于配置起点，按本次正式选择接纳置位，实际游标回写（含同索引）与卸载/来源切换复原；缩略图刷新不能误清。仅调整现有会话的DisplayIndex通知区分真实源游标事件与普通缩略图刷新，不新增公共接口或第二套参数。已有四组合回归增加初始/激活/缩略刷新/同索引回写状态出口。整批还需验证追加不选新项且零运行、删除前/当前/后游标及下一次实际读图、一次历史/撤销重开与取消，不能只测列表数。

## 2026-10-30 S1结构输入与运行游标整链核收结果

本批沿原件托管入口及原生追加/删除游标证据，集中修正导入、拖放、删除、首图状态和下一次真实读取：导入只追加、不主动选中新项或执行；已有显示记录按身份保留，删前减一、删后保持、删当前无选择；无有效游标的首次实际读图使用首项而不提前自动跨过；原件首图强制状态由实际回写复原，缩略图刷新不误清。正常文件拖放逐项验证存在性与原件扩展名，目录及混合非法集合整批拒绝。复用现有正式编辑、历史、执行快照及结果释放链，无新增公共合同、兼容入口或Group扩展。

同版正式Release全量构建：0错误/3391警告，日志Release/_validation/s1-structural-final-build.log。ImageSourceRecovery整批355通过/0失败，日志Release/_validation/s1-structural-final-regression.log；新增生产回归包含真实宿主的导入零运行、追加保留像素但退休旧结果权限、删除前/后/当前后的下一次实际输出、清空与撤销重做，另含原件首图四组合状态和路由拖放。此前352通过/3失败的结果保留：两项旧测试假定导入主动激活，与已核原件冲突，改为验证不激活；一项真实提示覆盖统一为运行游标不可用，仍验证正式接纳、执行阻断及修复恢复，未删减故障检查。

边界：普通唯一路径的原生静态证据不推广为重复路径或未提取外部回调的动态结论；构造WPF路由拖放不是物理OS拖放。143项声明及33项命名元素的完整对应仍待核，VS实际设计器、WPF同态及用户人工验收未完成。S1保持未封版，S2—S4顺序与Group暂停不变；本批未启动用户程序，不以355项通过宣称完整组件已迁完。
## 2026-10-30 S1名称域、资产与正式布尔依赖核收

本轮不改产品、不重复构建已验证5431c9fe。新增S1_NAMED_ELEMENT_REVIEW_20261030.json逐条记录全部33命名声明，六个ImageBackground按出现次序保留，不能一条名称匹配替代六个模板验证。纠正旧清单将外层ImageListPanel list误指目标lst ListBox的问题：原外层组合实际折入imageListGrid及外围Grid；MainGrid消失也是组合适配，需布局验证，不能仅按名称判缺失。RunningShadow已核到实际最终子级、根状态绑定与释放链；其余同名声明仍不证明等价。Pop1/Pop2属远程部署分支，现未实现；RP入口禁用折叠，均不算功能完成。

新增可复验脚本scripts/verification/New-ImageSourceAssetIdentityReview.ps1及S1_ASSET_IDENTITY_REVIEW_20261030.json，逐文件校验逆向资源图中13份XAML的固定SHA，再核对产品21张S1图片。15张与原URI直接提取路径字节一致；折叠/展开6张与空格转连字符的提取候选字节一致，原始嵌入资源名映射仍未核，不假报为直接身份已证。整字典扫描的一个未定位1:1图片属于旁支，不可据此认定S1正在消费该图。资产一致不能代替模板/主题/运行正确。

本轮发现两项明确模板缺口，必须纳入完整视觉批而非再按截图补丁：自动切换仍为本地ToggleButton+AutoSwitchStyle；运行勾选SmallCheckBoxStyle省略原EmptyCheckBoxFocusVisual及HasContent时CheckRadioFocusVisual，并用Transparent替换原动态填充/边框。原件两份焦点样式已定位checkboxstyle.xaml:6–24；需整组恢复及核对系统动态画刷作用域，不用默认主题焦点冒充原件。

技术总监已正式交接D:/MyWork/SunEyeVision_V2/docs/collaboration/evidence/WORKFLOW_BOOLEAN_CONTROL_HANDOFF_20261030.json：所需HikPcUI.ListView.BooleanControl为VM.PcUI UserControl家族，IsChecked(bool DP)、OffContent/OnContent及OnCheckedChange，内部TO/ToggleButtonStyle1；原LocalImageListView使用dark两图、Stretch=None。目前没有中立正式入口或独立功能验收基线。已验B01是Apps.XmlUI ToggleButton家族，旧Controls.ToggleSwitch来自SDK，均不可最终替代。主线55507bce与所报Wpf SHA仅作交接身份，不是所需原件验收。公共开关由总监负责，本线不重复开发；保留真实缺口，继续其余核收，不宣布S1封版。

尚待：143声明按行为与生产消费者逐项审核、模板/主题完整依赖、实际VS设计器及最终人工验收。本轮新增证据不提高功能通过数，既有355通过/0失败仍仅对应5431c9fe生产批。
### S1运行勾选模板整链恢复设计（实施前）

原件SmallCheckBoxStyle的EmptyCheckBoxFocusVisual、HasContent→CheckRadioFocusVisual及CheckBoxFillNormal/Stroke必须作为同一依赖闭包恢复。只在S1公共组合资源中添加中立ImageSources键，按原Rectangle模板和系统动态画刷保留两个焦点状态；不新增控件类型、不改变勾选请求或工作流执行。独立设计面使用原件色值；现有ImageSourceThemeScope按InputBackgroundBrush/InputBorderBrush提供正式宿主主题映射，并沿已有卸载/重载生命周期退出。生产触发器、勾选图标、取消/提交和只读逻辑复用现有实现；不借此修改尚待总监的PCUI开关。验收从实际ImageSourceStrip的真实Checkbox取模板，检查空/有内容焦点、已选图像、禁用系统文字色和动态画刷更新及卸载重载；随后同版Release全量构建与受影响生产回归。独立WPF渲染不等同于VS设计器或最终人工通过。
## 2026-10-30 人工验收启动阻塞：起始页目录时间适配

bc1e50e1候选的真实普通启动在App.ShowStartPageAsync:425失败；D:/SunEyeVisionData/Logs/2026/10/30/SunEyeVision_215025.txt记录DateTime.MinValue隐式转DateTimeOffset在东八区越界。主题初始化已成功。该异常发生在起始页构造前，不是S1控件通过即可覆盖的启动链；之前359项不作为真实启动成功证据。

修复设计：将App原有最近记录聚合完整移到StartPageSettings内部适配函数；目录元数据只有具备文件路径和实际访问时间时才转成RecentSolutionFile。无路径或未访问的目录项不是最近打开记录，进入DateTimeOffset前排除；不伪造现在时间、不清理或迁移用户配置、不修改公共起始页类型/持久格式。既有偏好优先、路径去重、隐藏和日期排序原样保留。新增生产适配回归同时覆盖缺时/空路径、有效Local/Utc/Unspecified时间、偏好优先/隐藏/排序，并构造实际起始页。然后按UI→全量构建顺序固定最终产物、回归和普通启动，仍不计用户或VS验收完成。
### 2026-10-30 S1用户人工证据更新（35d33872冻结版）

用户提供三张源条截图：运行全部、切运行选中、多个可见条目橙色勾选，三图均显示22/1051，当前选中边框保持。用户明确反馈“左右不会弹”，据此记该项人工确认；静态截图只证明模式/复选框/勾选显示，不推定只运行勾选项、零隐式执行、保存或所有快切边界均已通过。原图按字节保存在Release/_validation/S1-manual-20261030，SHA及观察范围见S1_MANUAL_ACCEPTANCE_20261030.json，不复制进源码资产。

自动切换不清已有结果图、删除当前图撤销恢复等待用户反馈；原完整范围和VS设计器门禁仍保留。当前PID129160版本冻结，不改源码、不构建、不重启，不把部分人工确认称作S1封版。
### 2026-10-30 自动切换人工反馈补记

用户明确确认“开关不会清图”，此项记为人工通过；删除图片后的撤销尚未操作，不记通过。当前版本保持冻结，不构建或重启。撤销入口已核对为主窗口顶栏撤销按钮（ShellTopBarControl），Ctrl+Z当前要求焦点位于流程画布内。

### 2026-10-30 删除撤销人工通过，剩余验收范围

用户明确确认“删除后撤销可以恢复，也没有自动执行”。结合此前左右快切不回弹、开关不清图及模式/勾选截图，这些项不再重复要求测试。剩余人工范围为导入不隐式执行，全部/选中与自动切换开/关四组合的实际运行及首图入口，滚动折叠与窗口缩放，保存关闭重开配置和引用恢复；实际VS设计器另列工程验证，不要求用户在运行程序中验证。当前验收版保持不动，未将部分人工通过记作阶段封版。

### 2026-10-30 S1运行时人工验收通过，设计器待核

用户明确反馈“目前4项测试没有什么问题”，对应添加文件/文件夹不自动执行、全部/选中与自动切换开关四组合及首图、滚动折叠和窗口布局、保存关闭重开四组。结合此前快切不回弹、开关不清图和删除撤销恢复且不执行，S1当前运行时人工验收记为通过；用户同版重启进程为128724。未改源码、未重新构建。实际VS设计器尚未验证，因此S1仍不封版，S2—S4不计完成；下一步只补真实设计器证据，不重复要求这些人工操作。完整未做范围保持原记录。

### 2026-10-30 实际设计器验证受工具访问限制

本机已确认安装Visual Studio 18 Community；通过Computer Use启动实际VS时返回“Computer Use was not approved to use Visual Studio”。未改用其他自动化绕过访问限制，未构建、未替换正式Release。用户已表示愿意手动测试，已提供当前工作区方案及真实ImageSourceStrip.xaml的设计/拆分视图步骤；当前等待其加载结果或截图。运行时人工验收仍通过，设计器验证仍未通过，S1未封版。无须重复运行时四组测试，不提前推进S2。

## 2026-10-30 用户调整阶段门禁，启动S2完整核收

用户明确要求：“VS设计器先记为待验证，不要卡在这里，继续推进S2图像结果视口，先完整对照逆向原件，再集中迁移。”本条覆盖此前等待设计器而不推进S2的限制。S1运行时人工验收通过，实际VS设计器继续待验证，不冒称通过；S2正式进入原件与已有生产链整体核收。保留当前Release程序供使用，源码设计/实施可继续，覆盖DLL前按实际进程情况协调关闭，不重复构建S1。

S2已定位已有ResultWorkspaceShell双ImageViewBox、ResultWorkspacePresenter、ResultImagePane和NativeResultViewport、全屏作用域及原件ModuleResultView/ImageViewBox；复用D07既有整体设计及资源来源记录，不能以找到类名或历史测试就宣称本阶段已完成。下一步核完整原件入口、模板资源依赖、来源及单双图、缩放平移/像素、全屏和生命周期差异，形成统一处置与验收矩阵后集中修改。Group、S3/S4提前实施、主界面重组不扩入S2。

### 2026-10-31 VS截图版本已确认：主仓，不是当前独立分支

用户明确给出D:/MyWork/SunEyeVision_V2/Src/Plugin.Wpf/Controls/Native/Composition/ImageSources/ImageSourceStrip.xaml。截图ControlBrush/Control.Template解析失败及重叠记录为主仓设计时证据，文件与当前87f4来源内容/行号不同，不能作为本分支实际设计器通过或相同失败。原图、两份源SHA及用户路径已保存S1_VS_MAIN_WORKSPACE_EVIDENCE_20261031.json，主仓未修改。当前验收程序和S2在87f4，尚未合并主仓；本分支VS仍待验证，按用户决定不阻塞S2。不要为修另一版本而编辑主仓或替换当前正式Release。
