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
