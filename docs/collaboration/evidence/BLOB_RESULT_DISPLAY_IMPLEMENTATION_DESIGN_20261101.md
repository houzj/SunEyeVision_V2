# C11–C13 Blob图像显示与文本正式组合实施设计

基线a2ff34e4fd2bd3357bd76a88b0bc66eef3368bf4；分支codex/blob-result-display-20261101，复用唯一候选工作区。旧预览98ebbc21分支保留。本批不改主仓/正式Release、不启VM、不扩其他工具/Script/双图owner。

## 原厂事实及本方时序

原依据：REA docs/joint-audit/B08-结果显示配置到视口静态证据-20261101.md；AlgorithmTab SHA34A0CC5423A3D66B92DC272E0F50B013310E5180E801C648005383C3ADDDFDEC，Display XML SHA B3BC63E6CF7C8CA120BAF5B632BBE1359843138CF77CB5A64FC698EF22465112，Group XML SHA9129B82A0F9906DDE591347498A80E2E8C019C0FB959A721ECFD24FBBB5207DC。

原九行按所属Root获取同名显示对象；眼睛双向IsDisplay，笔仅打开样式。颜色/透明度/线宽写模块UserData，已证读回；主动刷新依executeCount获取结果。不能因此宣称原所有方案落盘/取消及竞态已证。本方明确采用“同次publication真实结果＋当前正式显示配置修订”；样式revision独立RunIdentity，不冒充本次算法冻结配置。有效编辑草稿作为临时预览覆盖；无效pending不发布半套，维持最后完整有效场景。取消撤覆盖后读最新正式值，非开窗旧快照；确认先提交正式Fields，再撤token。无隐式算法执行。

## 九行完整清单

|原项|真实数据及本批处理|
|---|---|
|BlobFiled/Blob|原rect中心/宽高/角度及BlobStatus；本方同次Blobs.rotated-bounds/area/judgment-passed具备。开通矩形样式及真实边框，以本方四角投影适配，不称VM数值等价|
|最大内接矩形|无真实生产值，原位置保持不可用|
|CheckZone/ROI|本地/继承几何已有但publication无本轮实际采用值；保护，不读当前编辑器几何补旧结果|
|MatchLocation/Contour Point|outlinepoint双颜色/排列未闭合；保护，不用单外环线冒充|
|FixtureInitPoint|无正式生产值，保持原位置条件并保护|
|FixtureRunPoint|同上|
|ResutlText/Show Text|同次Area及逐项状态具备；开通眼睛、七行内容/颜色/字号/透明度/常量位置；默认Blob Area:{0}。动态结果位置需独立结果阶段解析，本批保护，不制造算法自环|
|BlindZone/ExternROI|本轮polygon结果几何传递缺，用户屏蔽多形不等价原polygon；保护|
|CheckZoneAnnular/Annular|原renderer/继承物化具备，本轮结果几何传递缺；保护|

全部九行保留原顺序/标签/模板/笔显隐。未实现项不能保存no-op状态；短Tooltip沿用户要求，不加常驻大段说明。原Blob初色#66ff00、NG#ff0000、opacity1、width2、visibletrue；不能沿用旧本方XML#00ff00。文本实际资源默认/范围逐项核后固化，原Content长度0x20，格式仅开放有实际消费者的受控{0}，未知表达式/动态位置不假实现。

## 唯一owner与独立显示阶段合同

正式配置唯一owner为RuntimeToolConfiguration.Fields，不新增旁路字典或第二可写模型。算法SettingsSchema仍保持原定义；显示字段另由provider声明DisplaySettingsSchema，BindingTargets合并以便编辑/保存/历史/复制，执行投影明确排除显示字段；无参数解析/current回写/算法校验。新建节点通过CreateDisplaySettings只设置一次默认，重开必须读取文件并严格校验，不补默认覆盖。

Contracts结果呈现最小接口：IToolResultDisplayProvider提供DisplaySettingsSchema、CreateDisplaySettings():InputValues、ResultDisplay:ResultDisplayDescriptor、CaptureResultDisplay(FieldSettings已声明显示子集):ResultDisplayAppearance。描述复用ResultFieldPath，明确rows、四角、Area、状态与element-id，不用中文/Blob CLR反射。外观为冻结纯值，无FieldSettings/回调/资源逃逸；provider不访问WPF。

RuntimeToolConfiguration新增只读显示捕获与EditorSettingsSchema；ToolSettingsEditSession沿同一草稿合并编辑schema，算法子集和显示子集分别校验。现Host传入完整编辑子集。生产者Blob、编辑Host和结果Presenter同批真实消费，不造未来接口。其他无provider工具保持原行为。

## 生产链、预览token与图层

Blob正式显示Fields→原NativeGraphicDisplayOptions/NativeTextDisplayOptions同一state组装→Host提案/确认/取消/History→RuntimeToolConfiguration捕获不可变外观。旧BlobDisplayDefinition Graphics/Texts不得作为并行权威；其Table继续负责真实结果表。

主窗ResultWorkspacePresenter按当前publication获取一份独立reader租约，读取同次Blobs行、四角/Area/逐项状态；按element-id而非屏幕行号关联。每pane独立Geometry/Text层，整体候选场景准备完成且proof复核后原子替换。无publication、失败/取消/空结果/错frame/domain先撤旧权威；不凭尺寸补坐标或读旧近帧。常驻层与选择高亮、十字、ROI附件分离；清高亮不能删除常驻层，旧Dispose不得清新代。

临时预览token归实际DocumentEditSession、formal node实例、ProductionEditorContext及该次绑定代次；只允许一个有效owner。切文档/节点替换/关闭/解绑先退役；多窗口新owner替换旧owner，旧关闭不能撤新覆盖。实际呈现同时核publication、pane receipt与外观修订；getter/通知/Dispose均防同步重入。取消后重新读取最新正式Fields；确认由正式Host先提交成功再撤覆盖，失败不伪确认。无效pending不采用新外观，关闭色板/样式保持原文及确认门禁。

本组位置指Text七行X/Y；ROI位姿仍属C08，未开放。双向指字段↔原眼睛/颜色/样式/文本控件与当前显示修订，不开放结果图形拖动回写算法。

## 文件边界

Contracts/Results/Presentation最小显示声明/外观接口及ToolSettingsEditSession阶段消费；Workflow/RuntimeToolConfiguration及BindingOrder仅配置/执行阶段区分。Infrastructure.CreateSettingsView同步编辑声明。

Blob Runtime新增BlobResultDisplay声明/默认/纯捕获，BlobTool实现provider、BlobDisplayDefinition移除非消费Graphics/Texts权威；不把显示塞BlobSettings算法模型。Blob Wpf/BlobDisplayUi/BlobParameterEditor及同链设计数据组装原九行/七文本。

Plugin.Wpf仅显示原组合必要组装：文本眼睛与七行共用真实字段state，无影子颜色/值；原模板/资源不替换。UI RuntimeNodeConfigurationControl、ProductionEditorContext必要适配、MainWindowRuntimeEditing及ResultWorkspacePresenter新增partial消费token/原图层。专属验证脚本与必须同步的字段数断言同批维护。

## 整批验收矩阵

1. 默认仅新建；保存/重开/复制/删除/Undo/Redo保正式身份与全部样式，不覆盖文件或用默认洗旧值。
2. 九行顺序与笔/眼显隐，Blob和文本真实字段可用；七项缺生产值明确保护、不可伪保存。文本动态位置无候选/程序提案也拒绝。
3. 颜色/透明度/线宽/字号/内容/位置双向同步；非法pending不半发布；拒绝/关闭重开/取消保原文，恢复后可确认。
4. 两个实际不同Blob、Area文本和OK/NG一一对应，零结果无旧图文；计数与来源均来自真实publication。
5. 同publication改样式不运行、不改RunIdentity/current；改变算法草稿不能伪造新结果；确认/取消/Undo显示按正式修订变化。
6. 临时token多窗口接管、取消读最新正式、确认先提交；旧window/session/proof/Dispose不影响新owner。
7. 原图/灰/二值，错frame/domain、同尺寸新帧/新publication、单双图各pane及无关pane；图文与高亮/十字/ROI共存。
8. 部分准备/通知/清理异常、重入、隐藏/关闭/换文档，reader和独立图层逐项释放。
9. 隐藏桌面专轨真实Host、标准候选UI/全量、自审实际截图及Loaded DLL；必要正常Debug/字段/结果工作区回归。自然键鼠、任意DPI、VS、VM像素与时序及主仓正式验收单列。

当前状态：设计冻结前集中核合同，不把九行可见或仅两对象开通称整九项功能完成。原图像ROI像素语义、屏蔽继承字符串仍待主线补证，本组不猜测。

## 实现演进与证据边界（历史冻结004）

算法 SettingsSchema 保持55项，显示独立13项，EditorSettingsSchema 为完整68项；WithSettingsFields只接收完整编辑集合，WithSettings仅替换算法并保留显示。BindingTargets/JSON/复制/删除管理保留全量，执行图、快照、Debug、current、参数顺序排除显示。Root参数目录不将显示字段误纳入。ConfigurationIdentity/NodeSignature保持全量；仅ChangedNodes采用另立的执行签名，仍保留休眠字段、工具/schema/绑定元数据，显示schema变化保守失效。无provider工具沿原行为。

七个缺真实生产值的原行运行时禁用，保持原顺序及条件显隐；设计示例仍为原真实视图组装，不将示例可编辑解释为生产已接通。文本只支持受控{0}面积格式；中文截图的{面积}token映射未闭合，明确拒绝。LengthA不能证明业务长度32，未实施该硬上限；场景总UTF-16字符预算1000000是本方资源保护，无截断。

003 UI与全量日志均0错，原构建句柄在上下文恢复中不可用，仅记录日志完整摘要与无候选活动构建进程，不虚报exit码。Core001真实插件准入失败；Core002诊断确认四个颜色Record误声明Scalar，严格ToolPresentation校验拒绝。冻结004集中按Type.Shape修正，UI与全量串行重建；此时Core/Window尚无通过回执。独立测试工程001/002未包含测试源码导致入口CS1555，003显式包含源码后exit0，保留所有原日志。

### EditorSettings调用闭包审计
生产ToolSettingsEditSession仅ToolRegistry.Components两处：CreateSettingsView接真实窗口完整字段；注册准入自检为新定义合并算法与显示默认后严格验证。RuntimeNodeConfigurationControl.Bind传EditorSettingsSchema子集，唯一两处WithSettingsFields均为该视图完整快照。BlobDesignData同链默认68项，ImageSource无provider仍原算法schema。算法Materialize/current/参数绑定顺序继续55项。Core003已实际暴露并修准入遗漏：旧自检仅传55项而报缺display.blob.visible；不为加载或用户旧配置补默认。


### 原页面与组序复核
原AlgorithmTab XML的ResultShow直接子项依次为Tab_Check Result、Tab_Image Display、TextDisplayGroup；本批必须保持结果判定→图像显示→文本显示，页签为基本参数→运行参数→结果显示。Window006诊断实际页签为基本→结果→运行，XY未加载；这是显示字段从0开始全局Order导致的真实候选组装缺陷，非模板缺失。006构建启动后复核组序，待其自然完成后以算法最大Order之后唯一递增的显示Order集中修正，禁止凭固定页索引掩盖错误；测试按Header选页并另断言完整页序/组序。

### 历史实测与修正记录（冻结008）
005 Core004自然exit0，5组通过；Window真实首组已证明2个实际Blob面积25/64、2条四角PolygonProjection及2条Area文字，原MultiText自有StrokeThickness=0不能混入边框线宽oracle。Window页序诊断发现并修原三页排序；007按算法最大Order之后唯一递增显示Order保结果判定→图像→文本。008又修Blob组装的重复Reverse，使屏蔽行在扇环前；专轨按真实row.View身份断言全部九行，不只比标签。此历史时点完整Window尚未通过，失败均保留；不以首组或Core通过称整批完成。

### 历史与开窗草稿实际缺陷及修正范围
Window009/010确认关窗Undo正式width=2、publication仍有效、scene仍4；009新增当前session/history/revision保护的显示刷新通知。Window011/012进一步确认开窗无改时formal3、scene5、draft5、Context有效、pendingfalse：仅增加通知不足，旧token仍覆盖。010复用ProductionEditorContext.Prepare已有无改安全重绑逻辑为真实显示消费者：HasPendingChanges含正式候选变化及HasPendingViewEdits，前后核draft/Context；有改草稿不重绑，旧覆盖退役、原文保留，提交仍原Host并发冲突。无改草稿应同时更新UI与显示。Apply后新DraftNode与未确认旧DraftNode需按实际代次区分，测试不能以关闭窗口规避同窗生命周期。

## 最终候选011与阶段边界

候选UI与全量构建自然exit0，产品源码与冻结011逐项零漂移。最终Core5、真实Window15、RuntimeDebug7、RuntimeKernel19、FieldEditing实际38条通过日志均exit0；实际加载15/15/13/4/12个程序集SHA逐一匹配本候选，测试SHA与提交源码一致。FieldEditing旧固定总结仍写31，准确计数及普通桌面失败→隐藏桌面最终通过的区别见机器回执。原001–010段是演进失败记录，不能当当前正式成绩。

稳定窗口scope拥有配对事件订退；显示Token只在Control成功绑定Context实际DraftNode及稳定generation后创建。Apply后新代可再次预览；无改Undo安全重绑新正式值，有未确认草稿不重绑/不丢原文、旧覆盖不复活，最终Host冲突拒绝。算法正式确认经Context回调已证明缓存结果/旧publication/场景退役，Undo不复活。直接Host.Commit只证明失效集合，不代UI消费，相关诊断保留。

原主设计已同步到BLOB_GRAPHIC_MIGRATION_DESIGN_20261101.md；完整候选回执BLOB_RESULT_DISPLAY_CANDIDATE_20261102.json、交接BLOB_RESULT_DISPLAY_HANDOFF_20261102.md。七个未实现图像对象及动态位置仍禁用；保存仅LoadCandidate值，不冒称重开宿主/执行；双pane、跨文档同ID、provider/proof异常重入直接注入、真实NG像素等本控件剩余直接验收项明确保留。默认截图原MultiText将两个Area分行，但VM排版/时序/像素等价未证。非自然键鼠、非VS/人工、非主仓正式验收，不称九项/Blob/S2/S3完成。


