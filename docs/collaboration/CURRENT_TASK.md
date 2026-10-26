# 当前任务：落实O06 Blob新架构与所需VM控件，交付真实Release

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

## 2026-10-27 中立Runtime节点配置、事务及直接执行（最新）

- Codex/main，基线d749dc815d7f06377a840e64bba5edd1d0a2601d，单产品写者。本轮推进真实公共宿主配置，未建立Blob参数包装或新旧Blob转换模型。
- Workflow内RuntimeToolConfiguration只持有实际Runtime设置类型；Create按已注册Runtime读XML默认，WithSettings/Copy先冻结校验，不持有图像或ROI资源。持久化只存稳定ToolId与真实设置，不从JSON CLR类型名加载类型；拒绝未知/重复/缺失/错型字段和错误Runtime。BlobSettings/BlobJudgment字段标记JsonRequired，保存重开不因缺字段悄悄回填XML默认。
- WorkflowNodeBase新增RuntimeConfiguration；相应节点的方案JSON不写SDK Parameters，读取拒绝双模型、显式null、重复Runtime配置及ToolId与节点不符。BindingConfigurationStore复制/结构历史与NodeConfigurationEditHost签名、冻结、提交、撤销重做、接受回滚均携带此状态。ValueGate内仍只赋值准备好的引用，无新增序列化、插件调用或UI回调。
- 节点新增ExecuteRuntimeAsync，校验ToolId/NodeId，使用配置快照经公共ToolRuntimeExecutor执行。正式组件检查实际保存阈值73→重开节点→灰度100图像HALCON执行面积64，证明未忽略用户值回退XML默认128；该调用不创建SDK Blob参数/结果。
- 重要未完成：工具箱仍默认旧SDK Blob；完整工作流调度器、绑定/ROI输入、参数页尚未改用新配置。为防忽略Runtime设置后误执行SDK工具，CreateInstance/当前调度器对RuntimeConfiguration节点明确拒绝；后续必须接通新消费并移除此缺口。本轮FullChain分数为原SDK链回归，不证明新调度器入口已完成。
- 最终UI002构建0错误/879警告41.86秒，全量001为0错误/6527警告99.97秒（blob-runtime-configuration前缀）。UI001为添加节点直接入口前构建，虽0错误但不作本批最终源码证据。
- 正式RuntimeKernel022十四组、Components110八组/无Wpf108九组、S2 DocumentEditing030二十八项、FullChain049/050各九组通过；ComponentDeployment033十二项通过，真实EXE准入正常，3文件及清单原SHA恢复。768发布项和160加载记录同源；证据docs/collaboration/evidence/O06_RUNTIME_CONFIGURATION_CHECKPOINT_20261027.json。
- 新配置测试含私有草稿、冻结、并发冲突拒绝、Undo/Redo、原始接受失败回滚/草稿保留、正式保存重开、节点直接HALCON以及缺/重/错字段、双模型和身份错误负向检查。104/105失败保留：新增保存检查宿主未按App启动流程发布已完成状态；105明确为插件元数据未加载完成。按App.xaml.cs真实顺序在完整PluginManager准入和清单检查后MarkAsLoaded，106及后续通过；未放宽产品保存门。
- 样式核对纠正：现有Themes/Combo.xaml与Native/Input/VisionUi/SourceComboBoxes.xaml仅资源URI重定位不同（逐字比较为true），不能把前者称自绘模板。本轮未改视觉控件，也不据此声称VM交互或视觉通过。
- 下一步从WorkflowExecutionEngine的RuntimeConfiguration拒绝位置接通实际节点执行与结果发布，并同步绑定/输入/帧与资源准入、工具箱默认创建及中立参数编辑；不要再把旧Blob参数包进新设置或新增Blob兼容转换。最终删除根SDK工程/旧参数和结果后才能满足两工程目标。
- Core/清单编译身份d749dc81加证据所列9份源码增量，不可用随后提交号改写该产物。目标树未改，M1/M2/M3均未完成/目标active。VS设计时未验证，新候选无人工验收，原视觉未通过反馈保留。

## 2026-10-27 中立Runtime实际工作流调度与结果发布（最新）

- Codex/main，基线3ef07588ad4f9a9c902965a180ceee0bfc2c5348，单工作树单写者。本轮已移除完整调度器对RuntimeConfiguration的临时拒绝，以下状态取代上一条“调度器尚未接通”；不改写旧证据。
- WorkflowExecutionEngine从RuntimeConfiguration直接创建AlgorithmNode，不实例化SDK Blob工具/参数。冻结设置与本次覆盖、复制输入设置，再用既有Owner分配原执行身份、编译依赖/资源需求。Runtime能力声明提供HALCON资源键。
- BlobParameters.xml新增必需workflowImageInputId，声明工作流默认图像输入；ToolSchema校验必须为图像输入并纳入指纹。计划开始捕获像素/实际Domain并拥有默认图像，Runtime节点只借用。显式输入按稳定端口/语义引用编译和解析，输入读者租约贯穿调用，跨物理帧ROI不重标记、不降级。
- 宿主SDK现有结果管线增加通用RuntimeToolResults，保留实际IToolOutput实例及正式值，统一资源租约；不是Blob DTO或业务结果复制。WorkflowBindingContext直接发布已中立值，不重新捕获图像。Runtime插件本身仍无SDK/WPF依赖。执行、结果捕获与多处清理失败均保留原始/清理异常并执行全部清理。
- 保存门修复Runtime输入仍要求SDK可写属性的缺口，直接按Runtime声明端口校验；Runtime消费者拒绝旧绑定信封，Runtime来源拒绝旧别名。不新增Blob新旧转换桥。
- 新S3 RuntimeWorkflow轨道经真实WorkflowExecutionEngine运行：双Runtime Blob引用生成依赖/隐藏灰图需求、同帧ROI；设计修改只影响下次及本次覆盖不回写；未知源只阻断消费节点/修正恢复；真实SDK ImageLoad→Runtime并拒绝另一物理帧ROI；保存重开再执行；运行所有者关闭后读者租约保留输出/最后释放关闭；节点开始后取消排空/重跑；捕获与输出/工厂清理同时失败保留三个错误。全程禁止UI SynchronizationContext.Post。
- 正式RuntimeWorkflow002/003有无Wpf各八组；RuntimeKernel023十五组；Components115八组/无Wpf113九组；DocumentEditing031二十八项；Algorithm012九组；原UI FullChain051/052各九组；ComponentDeployment034十三项通过，新增无Wpf时新Runtime调度8组。实际EXE准入正常，临时3文件及清单均原SHA恢复。
- 最终构建UI004为0错误/6426警告75.52秒，全量002为0错误/6628警告71.60秒，blob-runtime-workflow前缀。早期UI001—003及全量001仅属前序源码，不冒充最终产物。本批768发布项、262加载记录及22项源码增量已核对；证据docs/collaboration/evidence/O06_RUNTIME_WORKFLOW_CHECKPOINT_20261027.json，Core/清单仍3ef07588加列出的源码增量，不能用随后提交号只改清单。
- 失败保留：RuntimeWorkflow001先通过新执行/快照/引用恢复，在保存时因框架仍只查SDK可写属性失败；修复实际保存校验后002/003通过。跨帧和未知端口用例日志中的节点错误属于明确预期负向检查，收据exit=0才计PASS。
- 工具箱/参数页默认入口仍为旧SDK Blob，RuntimeConfiguration节点当前须由公共模型/API创建。WorkflowNodeBase.CreateInstance的SDK接口拒绝Runtime配置是防止走错接口，不能让UI用此接口忽略新设置。设置字段订阅/全局变量、本地ROI编辑及参数Wpf视图仍待接入；根SDK工程/BlobAnalysisParameters/Results/适配尚未删除，最终两工程未完成。
- 下一步优先中立设置编辑元数据和实际Wpf参数编辑/工具箱默认创建；同步输入订阅/本地ROI与调试生命周期，再删除根SDK Blob入口与旧模型。不要为继续保留旧Blob参数新增包装/兼容转换；宿主通用输出信封不是另一套Blob结果。之后继续全部Blob所需VM原件及完整交互与显示XML图形文本消费。
- 新Runtime调度证据与旧UI FullChain回归分开记录；本轮未改控件视觉，不计VM视觉、物理鼠标操作或人工验收。原视觉未通过反馈保留，VS设计时未验证，目标/M1/M2/M3继续未完成/active。

## 2026-10-27 XML强类型设置编辑与可选Wpf入口（最新）

- Codex/main，基线5674ff75bb6d0130f06cce949a0f768b3f0e19c6，单产品写者。新目标继续active；没有另建目标、缩减硬性目标树或代签人工验收。
- BlobParameters.xml现在声明54个稳定设置字段：20运行值、12判定开关、22判定端点，包含选项标签、显隐条件、范围配对、边界/步进和页签分组；这是字段数，不是VM控件数量。现有字段身份保留为稳定身份，生产路径不读取旧BlobInputContract或转换旧参数。
- Contracts增加IToolSettingsFields/IToolSettingsDraft；Runtime的ToolSettingsEditSession只保留一份实际强类型设置，先冻结并检查整批提案，再替换私有状态。Blob直接投影/应用BlobSettings及其判定记录，没有第二套业务DTO。未知字段/越界/非法枚举/交叉范围错误明确拒绝，不污染原设置或已接受草稿。
- Wpf新增目标Views/BlobParameterEditor.xaml及工厂设置入口，公共DeclaredSettingsView复用字段组合；ToolRegistry准入时实际检查设置声明，并按有无可选Wpf创建专属入口或通用声明视图。视图保持私有模型与订阅提案，不提交文档、不执行算法、不持有图像资源。
- 正式组件检查通过设置字段合同改真实模型，沿既有事务验证冻结/冲突/撤销重做/回滚、保存重开及HALCON。设置视图54字段的有效修改/非法提交阻断及原配置隔离有有无Wpf定向证据，但真实节点打开/文档参与者和默认工具箱还未消费该编辑入口，不能称用户窗口已全面使用新链。
- 最终串行UI003：0错误/6471警告122.58秒；全量002：0错误/6632警告69.62秒，blob-settings前缀。UI001误用不存在的DeclaredValueReader.Value导致编译失败，改为InputValues.Get；UI002/全量001曾重叠，停止后串行重跑，不计最终产物。一次封存校验参数名错误已按函数真实签名纠正，未伪造校验成功。
- 正式RuntimeKernel024十六组、RuntimeWorkflow004/006有无Wpf各八组、Components123九组/121无Wpf十组、DocumentEditing032二十八组、FullChain055九组通过；ComponentDeployment036十三项通过，实际EXE准入，3文件和清单原SHA还原。原SDK窗口整链回归与新设置API证据分开，不混算新参数窗口完成。
- 部署035失败保留：新增测试静态引用Runtime类型，在移除Runtime的负向场景导致检查宿主编译失败（Components119）；产品缺件探针已正确拒绝。测试改为公共合同/实际类型反射检查后036全通过，没有放宽产品准入。
- 证据docs/collaboration/evidence/O06_RUNTIME_SETTINGS_CHECKPOINT_20261027.json：15份源码增量、768发布项、158加载记录SHA核对一致；清单/Core仍5674ff75加所列增量，不用随后提交号改写已编译身份。原S4 JSON/O06已同步；目标树未改，生成器697锚点/幂等成立。
- 下一步直接把节点打开/文档编辑参与者与默认工具箱接入中立编辑视图，同步设置订阅/全局变量、本地ROI、调试生命周期，随后删除根SDK Blob工程/旧参数/转换。不要加SDK参数包装桥。全部所需VM原件和显示XML完整消费仍待完成。
- M1/M2/M3均未完成。VS设计时未验证，新候选无真实人工验收；原视觉未通过反馈保留。新设置页定向API检查不冒充完整焦点/物理鼠标/VM视觉通过。

## 2026-10-27 Runtime设置字段实际订阅与持久化（最新）

- Codex/main，基线082454c7，单工作树单产品写者。上一目标轮已有XML字段和可选Wpf编辑创建API，本轮把这些字段接到真实工作流编译、执行和保存链；不是重述状态或只写新接口。
- 跨模块影响已核：共同改动Workflow配置、绑定编译/校验、调度快照、执行及持久化。沿用原Owner准入、原结果发布/输入读者租约和ConfigureAwait(false)，不向UI反调、不把设置回写设计节点；未改变公共VM视觉控件和发布依赖图。
- RuntimeToolConfiguration公开输入/设置的共同绑定目标，拒绝两类稳定身份冲突；WithFieldValues直接构造冻结校验后的真实强类型模型。引用仍只存ParamSettings，常量只存BlobSettings。执行准入、文档事务及保存重开均拒绝设置常量的第二份绑定存储；没有增加Blob参数DTO或SDK包装桥。
- WorkflowBindingContext与候选持久化直接按同一声明编译设置引用。AlgorithmNode持有生产者读者租约，解析输入/设置到各自集合，整批应用本次设置并校验；改变已准入端口声明或资源键时拒绝。Runtime输入值仍只按算法输入端口构造，不把54个设置字段混入图像/ROI资源输入。
- NodeBindingOverrides对设置字段直接修改本次强类型快照，屏蔽本次引用，不把临时常量写入持久化绑定。未改变公共NodeParameterOverrides按强类型属性覆盖的现有行为。
- 新RuntimeWorkflow用例证明：正式值阈值200、订阅上游数量/面积后实际HALCON得到面积16；整数及显式Float64ToInt64Checked转换生效；运行期间删除设计引用不污染当前快照，下一次恢复正式值；保存重开保留引用/转换及正式200；临时200覆盖屏蔽本次订阅且不污染正式引用。越界/禁止订阅模式/重复常量拒绝消费，修正后可重跑。负向节点错误日志是预期检查，不隐去或冒充用例失败。
- 串行UI blob-setting-binding-ui-001：0错误/6613警告166.02秒；全量blob-setting-binding-release-001：0错误/6527警告85.33秒。正式RuntimeWorkflow007/008有无Wpf各十组，RuntimeKernel025十六组，Components127九组/125无Wpf十组，DocumentEditing033二十八组，原窗口FullChain057/056有无Wpf各九组，ComponentDeployment037十三项通过。实际EXE缺件/恢复准入正常，3文件及清单均原SHA还原。
- 证据docs/collaboration/evidence/O06_RUNTIME_SETTING_BINDINGS_CHECKPOINT_20261027.json：8份源码增量、768发布项、192加载记录SHA核对一致。清单/Core编译身份082454c7加所列增量，不用随后提交号改写本次产物。原O06/目标JSON同步，硬性目标树未改，生成器697锚点且幂等。
- 默认工具箱/参数窗口仍SDK入口，公共新编辑视图尚未进入真实窗口文档/调试生命周期；UI来源选择、本地ROI和新Runtime全局变量绑定仍未实现。ToolRegistry当前工具箱元数据只从SDK工具候选登记、Runtime登记也在其循环中，是移除根SDK工程前必须改掉的明确缺口；后续同时接中立工具目录及窗口/文档入口，不能只删除根工程或复制另一套Blob参数。
- 根SDK Blob工程、旧模型与转换尚未全部退出；全部所需VM原件、显示XML完整图形文本消费、VS设计时和真实用户验收仍待完成。原视觉未通过反馈保留；本批功能/旧窗口回归不等于新窗口或VM视觉验收，M1/M2/M3均未完成，目标保持active。

## 2026-10-27 Runtime独立工具目录与公共节点创建（最新）

- Codex/main，基线b249e7f0ca8c2d4a6187e10c5aad869884ba0544，单工作树单产品写者。本轮实际解开Runtime登记必须位于SDK候选循环、发布声明必须有SDK工具消费方的缺口；不是只加一个未消费的描述接口。
- 跨模块影响覆盖Contracts、SDK宿主元数据、Infrastructure目录事务/实际PluginManager/发布准入、公共Workflow节点工厂及构建清单生成规则。Runtime仍不引用SDK/WPF；目录声明只有名称、描述、语义图标键、分类和排序，不持有视觉资源或算法状态。未新增工程。
- BlobTool通过IToolCatalogProvider/ToolCatalogDescriptor成为工具箱目录来源。ToolRegistry在SDK扫描前准备、校验并原子发布Runtime/Editor；公开存在/数量按完整目录计算，失败提案不污染已发布状态，重复登记幂等。现有SDK前端类型仍供尚未切换的旧UI，已不再是Runtime目录来源；它们仍须删除，不作为兼容设计保留。
- ReleasePayloadAuthority分别校验SDK主程序集声明与Runtime生产者，将二者的实际ToolId集合供完整准入核对；Runtime无需旧SDK消费声明。存在同ToolId SDK前端时必须明确依赖同一Runtime，独立生产者冲突拒绝。清单生成也拒绝冲突；PE/文件SHA/组件角色/所需Runtime及可选Wpf门保留。
- 公共src/Workflow/WorkflowNodeFactory无参数创建时默认已登记Runtime并初始化真实BlobSettings；当前UI自己的src/UI/Services/Workflow/WorkflowNodeFactory仍调用SDK CreateParameters，实际参数窗口/文档和调试入口尚未切换，不混称已完成用户窗口。
- 新RuntimeCatalog轨道：正式发现/目录信息及来源、公共工厂创建→HALCON、修改实际设置→保存重开→HALCON、冲突拒绝/幂等共四组。部署额外同时移除旧SDK Blob DLL及Wpf，并显式删除候选清单中的SDK主程序集声明；正式EXE仍发现22工具，该四组通过，实际加载列表无SunEyeVision.Tool.BlobAnalysis及Blob.Wpf。证明生产目录/后端独立能力，不是最终源码仅两工程或清洁发布完成。
- 串行UI blob-runtime-catalog-ui-001：0错误/6613警告124.19秒；全量blob-runtime-catalog-release-001：0错误/6491警告88.49秒。正式RuntimeCatalog001/002有SDK及无SDK/Wpf各四组，RuntimeKernel026十六组，RuntimeWorkflow010/009有无Wpf各十组，Components131九组/129无Wpf十组，Algorithm013九组，DocumentEditing034二十八组，原窗口FullChain059/058有无Wpf各九组通过。
- ComponentDeployment038扩为十五项全通过；实际EXE准入/缺件拒绝/恢复正常，临时四文件及清单按原SHA还原。本批没有非预期验证失败；负向缺件/元数据拒绝仍有真实日志，不改写先前失败记录。
- 证据docs/collaboration/evidence/O06_RUNTIME_CATALOG_CHECKPOINT_20261027.json：12份源码增量（包含installer/Release.Common.ps1）、768发布项、293加载记录SHA核对一致。Core/清单编译身份仍b249e7f0加列明增量，不用随后提交号改写产物。原O06/目标JSON同步、硬性树未改，697锚点与幂等成立。
- 下一步直接接UI自己的节点创建、NodeOpenDecisionChain与真实窗口的文档/调试生命周期。现有ProductionEditorContext已经基于NodeConfigurationEditDraft/DocumentEditSession并携带Runtime配置，可评估直接复用事务能力；不要将BlobSettings转成SDK ToolParameters。同步来源选择/本地ROI/全局变量后删除根SDK工程与旧模型/转换，不能把独立目录作为M1完成。
- 全部所需VM原件、显示XML完整图形文本消费、VS设计时和真实人工验收仍未完成；本轮没有视觉控件修改。保留原视觉未通过反馈，不将旧窗口回归或目录语义图标键等同于VM图标/外观验收。M1/M2/M3均未完成，目标active。

## 2026-10-27 Runtime节点实际参数窗口与文档事务（最新）

- Codex/main，基线2c58251b，单工作树单产品写者。本轮继续既有active新目标，硬性两工程目标树未改；未另建目标或缩减范围。原31类仅历史功能核对，不能恢复为当前实施范围。
- 跨模块影响已核：UI节点投影、真实节点打开链、生产文档草稿参与者、公共设置视图和输入元数据；不改算法调度/执行，不新增项目。WorkflowNode.FromBase现在复制实际RuntimeConfiguration，避免UI投影丢失配置。
- NodeOpenDecisionChain对Runtime配置创建RuntimeNodeConfigurationControl，MainWindow实际打开命令接ProductionEditorContext/文档编辑范围；不创建SDK Blob工具/参数，不转BlobSettings为旧参数。复用现有通用窗口及宿主事务，Runtime/Wpf插件仍无UI宿主依赖。
- XML新增4个输入的稳定标识/标签/页签分组，严格拒绝缺重错声明；与既有54设置字段组成实际58字段视图。来源弹窗从正式声明候选选择图像/区域/位置引用，仅写私有节点；设置常量只存真实强类型设置，保留原引用转换。位置常量编辑和本地ROI仍待接通。
- 真实主窗口保存命令接受未失焦有效编辑，保存重开保留用户值；隐藏非法输入阻断保存/确定，关闭取消保留正式值，重新打开读取正式值，确定发布并退出编辑范围。保存重绑后旧来源控件迟到事件按代次拒绝；卸载/取消退订并释放视图。
- 来源按钮实际渲染曾为空白：先修正误用的纯色资源，再改为绝对pack URI、即时加载并冻结原迁入位图。最终实际WPF图中显示，4个来源按钮可视树均含有效图像和布局尺寸；此项局部显示核对不等于全部VM外观验收。基础页仍有输入组合/重复分组等未完成布局。
- 最终串行UI006为0错误/6613警告97.16秒，全量004为0错误/6621警告86.06秒（blob-runtime-editor前缀）。早期构建/候选成绩不套本产物；清单buildId=o06-runtime-editor-20261027-004，编译身份仍2c58251b加列明16份源码增量，不用随后提交号改写产物。
- 正式RuntimeEditor011/012有SDK与无SDK/Wpf各六组，RuntimeCatalog007四组、RuntimeKernel028十六组、RuntimeWorkflow015十组、Components143九组/141无Wpf十组、Algorithm014九组、DocumentEditing036二十八组、FieldEditing015八组、原SDK窗口FullChain064九组通过。新Runtime窗口与原窗口回归分开记录。
- ComponentDeployment041十六项通过，实际EXE缺件/恢复准入正常，临时4文件及清单按原SHA恢复；768发布项/330加载记录同源校验。证据docs/collaboration/evidence/O06_RUNTIME_EDITOR_CHECKPOINT_20261027.json；原O06/目标JSON已同步，697锚点且幂等。
- 失败保留：UI001同名ParameterSetting冲突、UI002属性lambda的field关键字误用；RuntimeEditor001测试调用错层CreateNode，002测试绕过真实保存命令而误判。改用实际SelectedTab.CreateNode和SaveCurrentSolutionCommand后通过；不把002当产品保存故障。009来源图标空白图保留，旧六组功能成绩不作视觉通过。
- 下一步接新Runtime窗口单次/连续调试的真实冻结输入与结果生命周期，再切UI自身默认节点工厂和输入自动绑定；不得伪造来源执行身份、隐式重跑有副作用上游或增加SDK Blob参数包装。当前新窗口运行按钮未显示，不能称调试链已完成；默认UI工具箱仍SDK。
- 本地ROI/位置常量、新Runtime全局变量、根SDK Blob工程/旧模型/转换删除、全部所需VM原件、显示XML完整图形文本消费、VS设计时和真实人工验收仍待完成。原视觉未通过反馈保留，M1/M2/M3均未完成，目标active。

## 2026-10-27 Runtime窗口真实调试及已发布输入快照（最新）

- Codex/main，基线caf26581，单工作树单产品写者。上一轮为实际源码/验证进展；本轮继续同一active目标，未重建或缩减目标。此条取代“新Runtime窗口调试尚未接通”的当前状态，不改写原六组检查点。
- 跨模块影响已核：Plugin.Runtime绑定解析、Workflow单次调试宿主、SDK通用窗口路由及UI文档私有结果。Runtime Blob插件仍无SDK/WPF/UI依赖；不新增工程、不创建SDK Blob参数/工具/结果DTO，不隐式重跑上游。
- PublishedBindingSnapshot/ResolvePublishedBinding明确表达编辑器选定的已发布输入，保留来源原始Owner/运行身份和物理帧。普通ResolveBinding仍要求同一次运行，不把旧来源身份改成当前调试身份；新测试同时证明普通入口拒绝、明确发布快照入口通过。
- RuntimeDebugInvocation在文档Owner边界冻结实际设置及语义引用；核对当前正式声明、真实生产Owner/工作流/原准入身份/成功状态，双读拒绝捕获中替换，持有正式输出读者至执行清理结束。画布与语义绑定共同编译循环检查；缺/失败/其他文档来源、缺图像在算法前拒绝。
- 设置订阅只应用到本次强类型配置，不回写设计值。捕获后修改/移除草稿引用、释放原生产运行仍不影响本次调用；输出图像在输入读者释放后仍可读取。取消/退役前置拒绝也会排空资源，下一次可运行；同一输入快照只能执行一次。
- 新Runtime参数控件接标准执行命令，通用壳仅为实际存在执行处理的控件显示运行按钮。UI先冻结调用，再Task.Run；算法及收尾ConfigureAwait(false)，完成后才由UI发布私有DebugResults/实际结果表。单次与连续停止有真实窗口检查，正式配置及正式结果保持不变。
- 补齐输入捕获失败的显示分支：不伪造失败结果身份，清空上一份私有预览、结果表和显示；保留正式结果。真实窗口先成功显示一行、清除来源后执行失败，私有结果与实际表均为空。关闭/确认仍走既有文档作用域及取消/Owner退役；保存重绑后的旧调试结果按代次丢弃。
- 最终串行UI006：0错误/6574警告99.95秒；全量003：0错误/6607警告60.45秒（blob-runtime-debug前缀）。早期UI001/002误把DisplayImage及ImageControl.SourceImage的BitmapSource传作IVisionImage，编译拒绝；已移除这条伪输入接法，保留失败日志。UI003—005/全量001—002为前序候选，不套最终产物。
- 正式RuntimeDebug005/004有无Wpf各七组；RuntimeEditor016/017有SDK与无SDK/Wpf各八组（含实际工作流→已发布来源→单次/连续调试→私有表、失败清空）；RuntimeKernel030十六组、RuntimeWorkflow019十组、RuntimeCatalog011四组、Components152九组/149无Wpf十组、Algorithm016九组、DocumentEditing038二十八组、FieldEditing017八组、原SDK窗口FullChain068九组通过。
- ComponentDeployment043十七项通过，真实EXE缺件及恢复准入正常，临时4文件及清单原SHA恢复。证据汇总曾拒绝沿用前候选中与最终程序集SHA不一致的旧成绩；已按最终产物补跑关联轨道。11份源码增量、768发布项及399加载记录同源；证据docs/collaboration/evidence/O06_RUNTIME_DEBUG_CHECKPOINT_20261027.json。
- 当前清单buildId=o06-runtime-debug-20261027-003；Core/清单编译身份仍caf26581加列明增量，不用随后提交号改写已编译身份。原O06/目标JSON同步，目标树和完整s4GoalPlan保持一致，697锚点且幂等。真实WPF运行图已核运行按钮及当前状态，不作全部VM视觉通过。
- 下一步直接切UI自身默认节点工厂及语义图像自动绑定，再移除根SDK Blob工程/旧模型/转换并改消费者/测试，不保留兼容包装。新UI目前只调试已发布来源；缺省工作流图像仍需非推进的真实输入快照合同，未绑定来源会明确提示选择图像，不能拿显示位图伪装像素/Domain。
- 本地ROI/位置常量、新Runtime全局变量、全部所需VM原件/完整交互、显示XML完整图形文本、VS设计时及真实人工验收仍未完成。原视觉未通过反馈保留，M1/M2/M3未完成，目标active。
