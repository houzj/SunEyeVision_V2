# C06完整原件迁移合同

基线：C05正式产品de6b7a3ba，阶段闭口文档c7d5c3a43。C05同版172组正式通过；本C06尚未实施或验收。既有两个候选工作树复用，不新增工作树；公开合同与正式产物保持到本批集中集成。

## 交付单位与迁移方式

以原IntegerBettween/FloatBettween及其Integer/Float子项、原single/between模板/SliderArrange/SliderArrangeFloat/NumericControl、引用槽和宿主完整消费链为一个控件单位整块迁移。数据模型、比较、事件时序沿原件直接移植/复用，不按本方习惯另设计范围系统；确需非视觉宿主、身份和生命周期适配在已有边界集中处理。现有正确的三真实范围、组草稿、索引/变量、运行快照、历史与保存重开复用，不重建引用或执行系统。工具层同样沿完整原件链迁移；S3算法未提前开启。

原依据：C05_C06_ORIGINAL_NUMERIC_MODEL_20261103.md及回执、C05_ORIGINAL_READ_CALLER_EVIDENCE_20261103.md、BLOB_RANGE_PRODUCTION_DESIGN_20261101.md、原O06 C05/C06单值/between身份与模板记录。主线已实际重读原Integer/Float/IntegerBettween/FloatBettween/ValueBettween和现NativeRangePresenter/NativeNumericCommit/字段根/SliderArrangeBase。原件在D:/MyWork/REA逆向测试/analysis/decompiled/Apps.XmlParser.CapabilityAudit/Apps.XmlParser.ParamTab/，原native完整模板身份继续沿Catalog，不以当前本方代码当原件证据。材料足够，不再启动原SDK/VM或重复全域取证。

## 固定的原件语义与宿主接缝

1. **原数据模型。** Int32 Min相对Inc格点与double/Math.Round ToEven沿原Integer/Float；两端各自CurValue及原isInitValue语义保留，不能以HasValue代替原初始化标志。原setter比较的是未规范化输入与已有CurValue，舍入后相同仍可能触发原初始化/通知；初始0、已知0、未读引用须区分。沿C05既有字段owner模型作原件状态适配，不建立第二套可持久化范围/参数模型。原件暂存/恢复不触发setter的分支不能被无意改成正式编辑事件。字段身份、原文、模型、普通参数读值和执行快照仍分别管理。
2. **原Check比较直接迁入。** 原Integer/Float.Check：右端以左CurValue为下界（仅当它不高于右原Max）；左端在右isInitValue后以右CurValue比较，否则直接用UserStringData/parent.InnerGetStringParamValue取右字符串作临时比较。它不是调用右GetParamUnit，也不安装右模型。原TryParse失败为局部result=0、异常为局部MaxValue，且边界条件保留；这些是临时比较分支，不得成为正式/执行或已知模型的默认值。不另造“未知peer一律禁用”或“未知peer等于实时来源”的规则。
3. **普通读取适配。** 复用现ReferenceFieldOptions.ReadCurrentValue/host.CurrentValues，不声明它等价native IMVS_GetParamValue；常量由正式Fields提供，引用无当前值仍Unavailable。原Check所需临时字符串读取经该既有普通参数回调作最小非视觉适配，类型按原Int32/double及InvariantCulture；宿主null/失效必须明确为不可用/读取失败，而不是伪造成功字符串。有效owner中的临时读取失败按原Check异常分支处理，回调中换根/Dispose/参数接纳则退役并停止，不以原fallback继续写旧owner。临时读取不安装对端模型、不写CurrentValues/Dirty/Undo；原普通回读失败双通知/回写不复制为新宿主副作用，沿C05显式状态结果。
4. **读范围与初始化。** 不增公开接口：已有ReadNumericModels(Field/CurrentPage/All)及三个真实视图转发直接覆盖paired端点；同一DeclaredSettingsView成功Rebuild末尾All一次，不新增Loaded/宿主重复或运行后自动回读。成对全量/当前页读取遵原ValueBettween.GetParamUnit先左后右；Field只处理请求字段，左Check临时读右是比较依赖，不冒充右Field更新结果。非数值/原文本XY仍NotApplicable。C05测试中“C06排除”是旧阶段断言，本阶段更新为实际注册，保历史回执，不删其他强断言。
5. **原控件输入和绑定。** 双端使用同一原between，保原T1/T2、模板、订阅槽、箭头/滚轮/拖动/失焦/Popup关闭事件及双向binding，不换成两个自制single。不将原文先舍入而掩盖原RangeRule的绝对/对端越界。原合法正式值、UI模型投影与新候选严格存储分离同C05；显示规范化不回写。引用态也保原UI模型，未知槽物理DP仅是原绑定目标，不安装为值、不漏清隐藏槽自身转换错误或误清用户坏raw。
6. **完整组接纳与回滚。** 原编辑模型/比较先迁入，再接既有SubmitRange/BeginRange/TrySetGroup一次完整宿主提案。单端真实编辑不改另一端正式引用；整组捕获同时候选先完整检验再提交，拒绝还原owned投影与pending，不能先写live再逐项回滚。原ValueBettween两端顺序读写不等于本方文档事务原子性；原子文档边界为明确宿主适配，不复制原业务全局服务。当前性守卫同时核两端AcceptanceVersion、模型Version/原初始化变化、模式、leaf/绑定、EditVersion及owner代次；模型回读重入不得被误认为未变。
7. **模式切换、历史与执行。** 两端分别常量/模块/变量/索引、解绑当前普通值、无值受保护常量输入、坏raw取消、隐藏/换根、确认取消/历史复制保存重开都走同一既有链。原模型不是执行结果、不能让窗口UI值直接替执行快照；原单值/三真实范围实际执行和既有ALL/索引资格不扩改。缺基础的其他原判定范围继续S1真实预览和明确保护，不用控件迁移宣称业务producer/算法实现。
8. **生命周期。** 复用原native异步事件，不另造调度。宿主程序投影、模板换根、隐藏/卸载/Dispose需使旧原Dispatcher回调失效；现Base仅IsLoaded/数值相同不足以证明同一owner，可在原控件内加最小内部代次/borrowed生命周期钩子，保持正常用户事件顺序和模板不变，非原安全适配记录明确。Popup实际打开/关闭、引用两端未知槽、构造失败逐项清理及借用所有权同批核；不为减少测试把真实原事件换手工Raise或全局缓存控件。

## 两线边界与交付

- 公共线：既有blob-reference-chain-20261101，新分支codex/c06-range-model-public-20261103。唯一写Plugin.Wpf所需原数据模型/状态/NativeRangePresenter/NativeNumericCommit/字段聚合及必要原控件最小生命周期钩子；新增C06完整公共测试与runner，更新C05公共阶段断言。原模板/事件/模型逻辑直接迁移，私有实现可按现边界适配；不扩SDK/Workflow/Blob/UI/算法，不改主方案/Catalog/CURRENT，不重复创建新公开模型API。尽早固定完整公共源提交交消费者，不逐功能反复编全量。
- 消费者线：既有blob-display-neutral-20261103，新分支codex/c06-range-model-consumers-20261103。唯一写真实C06模型/范围生产测试、S4C05NumberModelCases阶段断言和Run-S2ReleaseValidation接入；必须的Blob/UI非视觉接入仅限C06，现三视图转发正确则不重写。不写公共源/SDK/Workflow/算法，不复制公共实现。消费公共固定源后统一UI/full并整轨验证；其自身产品源码与公共线应同规范Git blob。
- 主线：唯一共享合同、Catalog、原O06、集成与正式验收。双方固定候选及源码/loaded/载荷回执后集中主仓构建；候选不签正式，正式试用时产物冻结。原28目录/17组、B08 A00–A23全范围保留；本控件实施自测通过可闭口，VS/自然键鼠和统一人工另记。

## 整体验收矩阵

- 公共完整原件：两端常量/引用四交叉；Int32非零Min/格点/跨符号安全差异、double中点/精度/合法存储投影；原初始化0/非0及舍入同值通知；左右Check不对称、右未init临时读/parse/exception、不安装右模型；读Field/Page/All有序且不Dirty、不改正式/执行；当前值无值/异常、同一read callback里换根/接纳/关闭；原raw/绝对及对端越界、单端与成对候选、一次原子拒绝/成功；原Popup两端源更新、未知隐藏binding、Dispatcher旧回调和retire、构造失败/借用释放。
- 真实消费：继承既有Area/Circularity/Rectangularity All各16生产场景（Core14+Sensitivity1+Publication1），完整保每组自己的结果集合敏感oracle、两端身份/索引/变量、确认/取消/历史/片段复制/新对象保存重开执行、图像/结果publication替换及旧捕获冻结。另增加真实窗口显式模型读取/局部模型与执行值分离/原文与组接纳/换根退役场景；真实三范围非单个Threshold替代。
- 回归随共享改动选择：C05公共与真实6、Loaded数值/范围、B08混合窗和完整图文作为直接共享消费者；无需重复无关导出/几何或全SDK计数。只有新变更、失败或未解风险补测，不能把原范围拆成每个参数轮流改/测/提交。
- 各树真实UI/full Release构建，HALCON用主仓正式libs来源规则，不复制运行库；实际loaded路径/SHA、测试快照、封包和原件来源身份核对。未完成/失败和非原适配明确保存，不借测试数量宣布整阶段完成。