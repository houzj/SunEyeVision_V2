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
