# 当前任务：落实O06 Blob与原31类控件方案，交付真实Release

## 写入与版本

- 当前主对话是主工作树唯一产品写者；不恢复旧管理角色、批次或副本，不原样恢复旧变量收口。
- 本轮本地检查点以HEAD为准，父基线c8de30c4；包含图像、框架、颜色事件及执行入口上下文修复。远端历史检查点b87fb685的成绩不套作新候选；当前未推送。
- 用户要求构建通过前不推送；现已全量通过。用户质疑算法等待UI后，已修工作流入口上下文捕获，正在补强无UI消息泵的执行验证；固定源码与发布身份后按原授权中文提交/推送，不重复询问。

## 已落地的当前代码

- DisplayObjectField使用就地四行主区与更多项，同一DeclaredFieldEditor写外层私有草稿；外层ProductionEditorContext/窗口确认或Discard负责持久化。嵌套弹层归属、逐层Escape、卸载/失活关闭及监听解除已有真实验证。
- ColorField→VmColorPicker组合原70色色板、默认/更多模板及高级色谱。修复实际运行发现的颜色事件异常：两个颜色控件均注册合法RoutedEvent；选色写外层草稿，未增加子层保存按钮。
- NumericUpDown有效键入同步滑条；有效Enter失焦收起，非法Enter留焦。初始显示与用户改值分开，仅聚焦滚轮改值。数值检查改发实际Preview阶段事件，保留首轮失败。
- 原Byte2ImageSource、Windows内存辅助、精确指针复制方法和WriteableBitmapEx六份源码迁入；来源SHA/静态REA依据保留，无厂商DLL部署。139份依赖源码、9个原件不是控件数量或整库完成数。
- ImageViewBox用显式可选ExtensionProvider/有限视觉会话，卸载和更换释放；不恢复厂商Ioc/模块/IO模型。完整纯WPF的IImageViewBoxWAddition按原件迁入；3D模块/IO实际消费者仍未适配，不以生命周期夹具冒充3D绘制通过。
- 同步WPF导出复制借入缓冲；RGB/Gray16原图及原ShapeItemsPage图形渲染已实测。栅格/JPEG为本方适配，不宣称VM/OpenCV逐像素等价。
- Behavior基类调用、圆角x:Name、已知Text依赖属性TwoWay绑定/旧模板解绑、消息框自有按钮解绑、泛型XAML属性所有者已修。半像素偏移改实例选项，不读取或猜测厂商全局配置。
- 50份组合源码按C#/XAML/依赖闭包保留为.pending，仍是迁移义务，不计功能完成。16份宿主适配有原件及输出保护；整合器只允许校验后更新C#头部using哈希，正文变化需显式复核。

## 实际验证

- 全量033：0错误/6125警告77.35秒；颜色事件修复后的全量034：0错误/6619警告52.47秒。正式目录D:/MyWork/SunEyeVision_V2/Release/SunEyeVision_v1.0.0/SunEyeVision。
- 单UI命令033：0错误/6621警告87.63秒（颜色路由修复前）；最终固定源码后补最终UI/全量身份核对。
- WindowPresentation011：8组通过，含样式草稿/逐层Escape/数值同步与Enter/真实Blob三页、原图与原图形页导出、扩展会话释放。FullChain031：8组真实MainWindow工具箱→HALCON→下游→私有调试→保存重开通过。Resources007：22组输入/输出所有权、取消、异常清理通过。均从正式Release装载，不代物理操作、VS或用户人工。
- 首轮Window008发现颜色RoutedEvent异常；已修产品。数值检查事件阶段、Gray16解码PreservePixelFormat和provider夹具模板已修；失败日志均保留。
- 首轮与补充UI构建曾重叠且清单SHA不符，不计最终发布验证。随后串行封存现有产物，窗口/整链/资源准入已通过。
- ComponentDeployment021的11项全部通过：缺少可选Wpf仍可配置并执行，非法值拦截和草稿隔离成立；缺Runtime及错误组件明确拒绝，3文件和清单均按原SHA还原。实际Loaded后InputElement为NumericUpDown模板TextBox，宿主Content为组合Grid，用例已按真实树查找；普通Application加载真实主题避免触发产品App交互启动。保留此前失败日志。
- ROI编辑019九组、ROI组合002六组、ROI绑定006十二组通过（035执行入口修复前的034产物成绩，不能套用于新产物）。
- 用户指出算法不应等待UI，核查发现Task.Run内HALCON不依赖Dispatcher，但公共异步入口默认await捕获调用方上下文，返回及finally清理可能等UI。产品根入口已ConfigureAwait(false)，两个兼容入口直接返回Task。影响仅执行任务收尾，不改变快照、调度、取消、结果所有权；UI已有Post更新边界。全量035：0错误/3560警告81.30秒。ComponentDeployment023十一项通过，有/无Wpf均在WPF上下文且不泵消息完成三个正式入口HALCON和会话清理；Components065五组另验已取消请求返回Cancelled、清理及下一次真实运行成功，不用泵消息掩盖执行层捕获。
- 035产物FullChain032八组、Resources008二十二组、Consumption023七组通过。Consumption021旧用例裸输入没有物理帧来源，实际被同帧门拒绝；用两个真实ImageLoad建立可区分来源后，同源成功、异源拒绝，未放宽产品校验。原失败及首次用例SourceKind枚举赋值错误日志保留。最后补UI035构建并核对程序集身份。
- UI035最终构建0错误/6610警告140.91秒；同源码重建使UI/Workflow/Plugin.Wpf程序集SHA变化，已按新产物补跑Components066五组与FullChain033八组，均通过。Resources008/Consumption023/ComponentDeployment023仍明确归属UI重建前产物；不伪称旧哈希覆盖新产物。原方案已同步。
- 原方案optimization-o06.html与S4_IMPLEMENTATION_PLAN_20261026.json、原31行证据已同步当前通过项、失败和未完范围。实际运行页截图仍有标签截断；不称整体VM外观达标。

## 下一步与未完成

- 本轮按用户要求直接从VM原件取样式：REA核Apps.XmlUI.dll SHA639d0fb9…/MVID a00f8b40-7a51-4c6d-8aeb-cad134724f1f及g.resources，复用并逐哈希验证同原件ILSpy/BAML导出。新增extract_vm_blob_ui_evidence.py及VM_BLOB_UI_SOURCE_BASELINE_20261026.json，三页/18相关模板/16显示对象；不是控件总数或VM实机模板命中。REA成员检查超过64MiB表示预算，记录unavailable，不计成功。
- 原ImageViewConfig浮点透明度0..1步长.01、粗细1..10步长1已查到原模板。FieldLayout增加有限正Increment（尾部默认1，纯中立显示合同），数值字段/范围组合消费该值；透明度/圆度/矩形度使用.01，查找数量上限由错误100万改为VM XML及现行参数校验一致20000。未改算法调度/快照/旧存储默认值；原件面积允许范围与截图当前值分开，不直接覆盖存量。
- UI036：0错误/6610警告195.20秒；全量036：0错误/6626警告86.79秒；最终串行封存后WindowPresentation013九组、FullChain034八组通过。真实原PCUI SliderArrangeFloat已加载并验证.01步进/数值联动/订阅隐藏及恢复；实际Blob透明度箭头.01只写私有草稿。用例012误用LegacyPath而不是StoragePath的编译失败保留。
- 当前主数值字段仍使用提取SDK NumericUpDown，下一步用已实测的原SliderArrange/Float接完整消费链，不把源码导入或单控件加载当替换完成。原件16显示对象与当前五组显示并未全部对应；继续补齐映射与必要实现。原31项及VM视觉/人工目标保持。

- 根完成035产物的无UI消息泵执行门、发布扰动及相关回归，固定中文源码检查点及同源Release；继续VM控件真实消费和同态布局迁移。旁聊只读沟通已同步；SunEyeVision.App.slnx入口清理建议排在当前验证后，不打断主线。
- 图形透明度/粗细的范围、步长与存量值兼容、VM同状态外观、typed调试新run、原31项重组及旧SDK消费者退出、VS设计时、用户真实人工反馈均未闭合。原目标与退出门不缩减，不据局部PASS删除旧类。
- 人工记录保留用户此前“不符合要求”的真实反馈；新候选未由用户验收，不代签或标整体目标完成。
- HALCON沿用加密狗，不单独检查许可。浏览器file预览此前被策略拒绝，不绕过；本轮图像为正式WPF程序实际输出，未称HTML渲染通过。

## 2026-10-26 补充VM控件库逆向参考

- 用户指定REA逆向测试的VM控件库逆向目标页，已按原字节归档HTML、两份JSON及静态扫描器到原优化方案references/vm-control-library-20261026；references/index.html提供来源、VM版本、口径和原方案映射，清单记录逐文件SHA-256。O06生成源与页面补持久入口。194项是工具层静态目标，不是迁移完成数；源Blob旧预览差异与当前修复分版本。
- 当前未提交数值消费者增量：DeclaredFieldEditor已接原PCUI数值控件；UI040通过，WindowPresentation最新10项通过/1项失败，失败为面积范围七位数字可视宽度。该增量尚未做全量Release，继续修布局后回归，不套用036成绩。HEAD本地基线cd8c6402，未推送。

## 2026-10-26 最新架构约束与检查点

- 用户明确：完全不考虑旧项目兼容，今后都不需要；已强化AGENTS.md，撤销“现行宿主集成可保留旧适配”的设计例外。目标删除旧入口/双模型/桥接，公共宿主与消费者直接接新合同。当前先交付文件结构方案，未执行产品删除或工程改造。
- 原方案新增Blob两工程结构：Runtime唯一Settings/Output/Schema/Session/Algorithm，Wpf只保留视图工厂/布局/显示投影；复用现有公共合同和控件，不新增Blob Settings/Contracts工程。明确Runtime直接发现、设置编辑持久化、结果投影发现入口及视图生命周期四项共同改造边界。
- 实际数值接入最新验证：UI045 0错误/6118警告58.37秒，全量038 0错误/6518警告66.16秒；正式窗口017 11组、字段012 8组、整链035 8组、组件078 5组、发布扰动027 11项通过，3文件和发布清单已原SHA恢复。窗口015宽度失败、字段010溢出、部署024-026隐藏页检查失败均保留；最后者修用例先切对应可见页，不放宽实际类型/可见/草稿门。
- 当前仍未提交，HEAD cd8c6402；正式产物sourceCommit/Core编译身份仍为该基线加已核源码增量，不能只改发布清单为未来提交号。下一正式同源交付需固定源码检查点后构建，并按新哈希回归；旧哈希成绩保历史。

## 2026-10-26 先推送基线再调整结构

- 用户明确先提交并推送远程，再改文件结构。已开始的26项XML/目录/入口重命名变更已保存到Release/_validation/O06-RoiCore-20261026/blob-structure-pending-before-checkpoint.zip，逐项before/after SHA齐全，ZIP校验通过；当前工作树已移除这些未验证改动。不是新仓库或程序副本。
- 恢复后5个产品关键源码SHA与正式Release验证证据一致，4轨最终测试源码SHA也一致；本次检查点只收已验证控件消费者及文档。新结构尚未编译、未实施完整宿主替换，不计完成。
- 用户批准的最新目标为Runtime/Parameters、Results、Algorithms、Resources和Wpf/Factories/Views；BlobParameters.xml是默认值/编辑元数据唯一来源，BlobDisplay.xml声明结果显示，属性存当前值、方案存用户值。两份XML不是用户参数保存文件。
- 核src工程实际职责：Contracts/Runtime/Wpf具真实公共职责；SDK仍混合旧参数/结果/控件与辅助，须随新消费链退出；Cv/Hal Adapter按引擎边界评估重写，不为旧SDK兼容保留；Tool.Kernels当前仅PointPairMath一个类，仅SDK生产调用，独立工程收益不足。CommonControl主要脚本编辑，命名需澄清。

## 2026-10-26 更新S4目标清单方案

- 已核结构改造前基线f1d42c80提交并推送origin/main完成；原先“正在提交/未推送”段落为历史。新结构改动仍仅保存在既有ZIP，尚未恢复或验证。
- 用户要求重新整理目标清单；原O06生成源新增s4GoalPlan，G0—G8覆盖基线、公共工程/新合同、两工程唯一模型、XML唯一默认值/显示定义、Runtime完整链、Blob三页交互、原31与整套VM库、正式Release、真实人工验收。保留完整总目标及既有成果，不把范围缩到目录调整。
- 三个交付检查点：M1新Blob完整运行链；M2原VM控件/交互与整库/旧类退出；M3正式版本及真实验收收口。当前为清单方案更新，不将原人工未通过或未知项改为完成。

## 2026-10-27 正式制定并启动S4完整交付目标

- 用户明确“好的制定目标”，G0—G8由方案转正式执行目标；基线f1d42c80不重置，原完整S4/原31及整套VM库范围保留。
- G2目标结构是硬性交付标准，唯一文件树在S4_IMPLEMENTATION_PLAN_20261026.json的blobFileStructure.tree；s4GoalPlan.G2Acceptance引用其SHA，并列出必需路径、两工程依赖、唯一模型、XML默认定义/用户值持久化、旧入口桥接删除、宿主接入及正式Release整链验收。
- M1新Blob完整运行链，M2控件/交互与整库/旧类退出，M3正式版本及真实验收收口；单工作树单产品写者，不恢复旧角色/副本，不为旧项目兼容保留双轨。
- 新目标已正式启动不代表G1—G8已完成；本轮只更新目标文件和原O06展示，未恢复或宣称通过已保存的结构重构增量。

## 2026-10-27 Blob全VM控件与公共层管理约束

- 用户明确Blob全部控件都用VM迁移原件，当前先迁移Blob所需的控件及依赖，缺资料继续VM逆向；194项参考资料供查源，不按原31类设计/保留本方控件。已写入AGENTS和原方案vmCommonControls。
- 公共可视控件统一Plugin.Wpf/Controls/Native，原模板/资源留Themes/Native与Assets；目标Adapters只做非视觉数据绑定/生命周期，Presenters选原模板组装。Blob.Wpf不复制公共控件，Runtime不依赖WPF。
- 现有Fields/Regions组合及SDK壳/RangeSlider/输入源等并未全部替换；不得改目录/改名就称原VM消费完成。原31清单保历史功能追踪，整库既有目标仍记录。
- 实施状态纠正：上一继续执行轮已按before/after SHA恢复26项结构/XML增量，目前为未编译增量；Runtime有BlobTool和Parameters/Results/Algorithms，参数XML及读取已初步接入，尚缺BlobInput/显示XML、完整公共合同消费和新构建验证。

## 2026-10-27 用户删除旧目标后重新创建

- 用户明确旧目标已删除并要求重新创建；已核工具返回goal=null。新目标以最后确认的Blob完整方案为准，范围为Blob及受影响公共链，原31只作历史追踪，整套VM库全量移植不再作为本轮完成条件。此段取代此前“整库既有范围仍属当前目标”的说法；旧目标清单已保存在supersededGoalPlans。
- 保留已推送基线f1d42c80和实际成果；新目标仍要求两工程硬性结构、两份XML、无旧兼容/双模型/桥接、Blob全部VM原控件及完整交互、正式Release同源回归和真实人工验收。M1完整运行链→M2所需VM控件/交互→M3正式验收。
- 当前产品源码是已恢复26项未验证结构/XML增量，并未宣称G2/G3完成；后续直接继续实施，不重新申请阶段授权。

## 2026-10-27 Blob输入与XML基础检查点（当前状态）

- 执行者Codex，单产品写者，main基线f1d42c807fa6e5848b3640cd797d172fc50534d6。上一轮只确认目标；本轮已实际修改源码并完成本增量的正式Release定向验证。
- 已接BlobInput借用宿主冻结输入，不新增资源所有者；BlobTool/Settings/Output/算法位于目标职责目录。参数XML是20运行字段和12判定的新建默认来源，严格拒绝未知/重复/缺失/类型/非法默认值/范围/步进/判定及DTD。属性保持强类型当前值。
- BlobDisplay.xml与严格解析器已嵌入Runtime：区分输入ROI与输出，校验稳定字段路径、颜色/样式、表格身份。当前生产参数初始化读取5图形/1文本样式，结果页按XML生成8列并直接读算法发布摘要，不自行Sum/Average/Min/Max。
- UI构建blob-xml-ui-build-001：0错误/6610警告，175.34秒；全量blob-xml-release-build-001：0错误/6626警告，109.73秒。正式目录RuntimeKernel018十一组、FullChain037八组、WindowPresentation018十一组、FieldEditing013八组、Components079五组、ComponentDeployment028十一项通过。部署检查实际启动正式EXE，22工具准入；临时移动3文件及清单均原SHA还原。全768项发布文件及144条加载记录核对一致。
- FullChain036失败是新增测试误把旧SDK单元素数组作标量；修为正式声明NormalizeValue后037通过。失败日志保留，不放宽产品门。
- 证据docs/collaboration/evidence/O06_BLOB_XML_CHECKPOINT_20261027.json，记录30份Blob源码SHA、构建、测试源码、加载文件与发布SHA。Core/清单编译身份为f1d42c80，加明确记录的未提交增量；不得把后续提交号直接改入清单冒充本次构建。
- 未完成：旧SDK根入口、双套模型和转换桥接仍存在；工程最终命名、Wpf参数/结果Views与工厂、宿主直接Runtime发现/编辑/持久化/结果链均需改造。显示XML已供默认样式与表格列，几何/文本生成尚有旧入口硬编码，不能称完整映射消费。当前样式XML沿用产品值，不冒充全部VM默认或VM外观达标。
- 下一步优先G1公共新合同与生产消费链，连同受影响宿主与测试直接替换，删除旧入口/双模型/桥接；然后完成两工程与全部所需VM原件。原31只历史核对，本轮不要求全194清单迁完。
- VS设计时未验证；保留用户此前视觉未通过，新候选未人工验收。目标继续active，不把本检查点计M1或整体完成。

## 2026-10-27 中立结果视图与可选Wpf生产链（最新）

- 当前执行者Codex/main，基线dc5112e41526b32089e69ddf92bf0c8f3923d5a7，单产品写者，无新增工作树/副本/智能体。本轮先完成G1结果视图边界，未把整个G1降为仅视图。
- Runtime BlobTool实现现有Contracts内IToolResultPresentationProvider；结果Schema/Projection从Runtime和BlobDisplay XML取得。现有Plugin.Wpf/Presenters/Results新增中立IToolResultView/IToolPresentationFactory与DeclaredResultTable，未新增工程。
- Blob.Wpf移除SDK工程引用，旧Views/BlobDataAnalysisControl.xaml/.cs删除，实际用BlobResultView.xaml/.cs及BlobResultViewFactory；BlobEditorFactory为中立Wpf工厂入口，视图只读InputValues并投影不可变几何。宿主ToolRegistry选择专属或无Wpf通用表，MainWindow三处真实结果刷新及缓存装配都消费新接口；选择切换/结果区退出退订释放并清高亮。
- 跨模块影响：调整Runtime结果声明/Editor准入/Wpf依赖、UI发布及生命周期；未改工作流调度、输入/全局快照或算法。原其他工具保现有消费路径，Blob结果页不回退旧SDK接口。根SDK工具发现/参数/序列化/执行仍待整体替换，尚不是M1。
- 实际修复：FullChain038/039/041发现新UI入口转换全部SDK输出、误把不需要的图像作新资源捕获，被正式帧/登记门拒绝。新增视图Sources并只读所需已有值；UI不捕获图像、不生成帧、不登记算法资源。有/无Wpf整链042/044均通过9组，实际面积单元格、选中高亮、失败清空、确认取消、保存重开、关闭释放成立。
- RuntimeKernel019十一组通过，实际无SDK/WPF；Components093六组与无Wpf091七组通过，读取真实工作流发布值后释放执行scope仍可选择几何，空更新清空，Dispose后拒绝。ComponentDeployment030十二项通过，新增缺Wpf真实MainWindow完整结果表链；缺Runtime/错误元数据明确拒绝，正式EXE准入22工具，临时3文件与清单原SHA恢复。
- 最终UI005构建0错误/6612警告84.28秒，全量003构建0错误/6525警告53.71秒，日志前缀blob-neutral-result。刷新依赖时还原001映射失败/002在线SSL失败，已恢复受影响锁文件并以现有原包缓存完成003还原；原第三方版本不变，未改nuget.config。相关依赖锁文件因Infrastructure显式引用公共Wpf而更新。
- 保留Components084未Loaded读取行列失败（改真实加载布局）；FullChain040诊断启动90秒超时且无首条输出，原因未证实，移除动态日志写入器后正常；Deployment029缺Runtime负向检查因新测试编译引用Runtime失败（改读真实工作流发布值后030通过）。不覆盖失败。
- 证据docs/collaboration/evidence/O06_NEUTRAL_RESULT_VIEW_20261027.json，含源码/锁文件、构建/测试源码、147实际加载记录和768发布项SHA。有/无Wpf最终产物同源。Core/清单仍为dc5112e4加记录增量，不可只改清单冒充后续提交构建。
- 已查看FullChain042真实截图：数据、摘要和高亮可见；表头紧凑，未按VM原模板核收，不计VM视觉通过。VS设计时未验证；用户此前未通过反馈保留，新候选无人工验收。
- 下一步直接替换公共Runtime发现、配置编辑/持久化和执行消费，删除Blob根SDK工程/双模型/转换桥接；之后完成最终两工程命名、参数Views/Factory和全部所需VM原件及交互。G1/G2/M1及整体目标均保持未完成/active。

## 2026-10-27 宿主Runtime入口与重复结果删除（最新）

- Codex/main，基线df06496cbd8efc11e525441ae4eba1f26b001f7e，单产品写者。上轮为真实进展；本轮已继续改实际注册/执行和删除重复模型，不重置目标。
- 缺口已核：IToolDefinition原来只有泛型入口，宿主用反射核ToolId且无法直接创建/调用设置；WorkflowNodeBase.Parameters仍为SDK ToolParameters，AlgorithmNode.Tool为IToolPlugin，方案/编辑事务也绑定SDK多态参数。后几项尚未替换。
- 现有Contracts新增非泛型宿主入口与调用信封，泛型接口默认实现仅保留/转接真实强类型实例，不做新旧模型转换；Plugin.Runtime的ToolRuntimeExecutor负责一次调用冻结/输入及请求校验/取消准入/session清理，不做调度、不拥有借入输入资源。执行与异步清理均ConfigureAwait(false)，双异常保留。
- ToolRegistry直接创建已注册Runtime，准入通过接口核类型/ToolId/XML默认值/Schema，不再反射读取Runtime ToolId。当前Blob生产调用已经走公共执行器；但外层仍SDK输入/参数/资源边界，未称工作流节点或持久化直连完成。
- 删除BlobResult.cs、逐Blob DTO复制、SDK另算统计及阈值/直方图/判定信息副本。BlobAnalysisResults的FilteredBlobs与Runtime.Measurements为同一引用，现行外壳只转发数据及准备宿主资源；渲染时才把中立点投影成WPF点。BlobAnalysisResults/Parameters和根SDK工程仍需最终删除。
- Runtime/Wpf工程文件改为批准名称SunEyeVision.BlobAnalysis.Runtime.csproj、SunEyeVision.BlobAnalysis.Wpf.csproj；Slnx、UI及工具引用已同步并用原包缓存还原。程序集身份未重命名，当前仍3工程，不把文件改名算G2完成。
- 最终UI002构建0错误/6612警告107.86秒，全量001为0错误/6589警告99.08秒（日志blob-runtime-entry前缀）。RuntimeKernel020十三组通过含假同步上下文无消息泵、同步/异步会话/异步Dispose、参数冻结/同一结果、错误准入/取消/双异常；真实进程无SDK/WPF。
- Components098七组、无Wpf096八组通过：注册表直接创建Runtime默认强类型参数并真实HALCON执行（该调用不创建SDK参数/结果），且旧BlobResult类型不存在、测量集合为同一引用。现行工作流三个入口无UI消息泵、取消后恢复、资源释放后视图选择仍通过。
- FullChain045/046有无Wpf各九组、Algorithm010九组通过（包含滤波/形态学→Blob真实链）；ComponentDeployment031十二项通过，正式EXE22工具准入、缺Runtime及坏元数据拒绝，3文件及清单原SHA恢复。768发布项与182加载记录匹配，证据docs/collaboration/evidence/O06_RUNTIME_ENTRY_CHECKPOINT_20261027.json。
- Core/清单编译身份df06496c加证据记录的源码增量，不可把后续提交号只改进清单；旧成绩按旧身份保留。测试已直接改用Runtime测量，不为旧BlobResult保留兼容类型或旧接口。
- 下一步优先替换WorkflowNodeBase.Parameters/AlgorithmNode.Tool和执行参数来源、SolutionSession/NodeConfigurationEditHost的SDK多态持久化、DeclaredToolConfigurationControl的SDK ToolDebugControlBase边界。直接使用Runtime设置与现有中立绑定/编辑合同，不再新增Blob旧/新转换桥；随后删根SDK工程/参数及资源边界，并完成参数Views/工厂、全部所需VM原控件/模板/交互。
- 仍未验证VS设计时，新候选无真实用户验收；原视觉未通过反馈保留。M1/M2/M3、G1/G2及整体目标均未完成，继续active。

## 2026-10-27 Runtime枚举单一来源与XML运行准入（最新）

- Codex/main，基线14eb5f5e819f94a1bb3e12427629ee0fdd10e83f，单工作树单写者。本轮实际删除重复模型类型并补齐Runtime准入，不增加SDK参数包装层或新转换桥。
- 删除根工具的ThresholdType、BlobSortMode、BlobSegmentationMode。现行参数属性、运行元数据、适配调用和相关消费者/测试直接使用Runtime枚举，删除枚举数值转换；不为旧类型保留别名或兼容模型。
- 发现原Runtime数量仅校验正数、面积仅校验有限/有序、判定未核对XML允许范围及既定类型。现在启用数值字段按BlobParameters.xml边界校验，判定按XML核对键/类型/作用域和启用范围；空集合/空条目冻结明确拒绝，Validate给出字段诊断。XML原值未改，不能据此宣称VM默认或外观一致。
- 串行正式UI构建0错误/6612警告238.28秒，全量0错误/6633警告81.95秒；日志blob-settings-authority-ui/release-001。正式目录RuntimeKernel021十四组、Components103七组/无Wpf101八组、Algorithm011九组、FullChain047/048各九组通过；ComponentDeployment032十二项通过，3文件及清单原SHA恢复，实际EXE准入正常。
- 新增直接中立调用的越界/伪造判定负向检查；组件实际比较参数与Runtime属性的枚举类型身份且核对旧类型已不存在。真实HALCON、确认取消、下游、保存重开、资源释放、缺Wpf整链仍通过。
- 证据docs/collaboration/evidence/O06_SETTINGS_AUTHORITY_CHECKPOINT_20261027.json：10份源码增量、768发布项和182加载记录SHA核对一致。Core/清单仍14eb5f5e加记录增量，不可用随后的提交号改写编译身份。原O06同步保留全部未完成条件。
- 尚未删除重复参数存储/根SDK工程/设置及输入转换；节点配置、编辑事务、快照与方案持久化仍SDK绑定。下一步应直接改公共宿主的中立配置入口及这些消费者，不再给旧Blob参数加一层Runtime包装；最终删除旧入口/模型。参数Wpf视图、VM原件替换/交互和图形文本XML完整消费仍待实施。
- M1/M2/M3均未完成，目标保持active。无新用户人工验收，VS设计时未验证，原视觉未通过反馈保留。本批功能PASS不等于VM视觉或整体交付完成。
