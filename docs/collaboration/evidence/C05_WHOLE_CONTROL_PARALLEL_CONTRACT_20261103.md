# C05 单数值整链迁移与并行写入合同（2026-11-03）

基线 daaf7c80a；S1正式产品3480dfc94保持。当前仅C05完整单元，C06双端比较/夹值/整组提案下一单元，不先扩算法或工作流。

## 原证据与复用

复用C05_C06_ORIGINAL_NUMERIC_MODEL_20261103.md及本次调用者补证。原Integer为Int32，范围内按Min相对Inc格点截整；原Float为double、Math.Round默认ToEven。现NativeNumericCommit的double/ToEven、原SliderArrange/SliderArrangeFloat/NumericControl、引用会话、参数提案、CurrentValues身份、raw/pending与退役代次全部复用。缺口是独立CurValue UI模型、显式普通回读、整数格点；不重建引用系统。

原六程序集离线CIL未证明Blob运行完成自动回读；本批不添加该行为。宿主CurrentValues回调是SunEyeVision普通参数适配，不声称与IMVS_GetParamValue或实时订阅执行值等价。原读取失败可能先失败再成功通知，原解绑标志缺finally；本方保持安全接纳/清理，差异显式记录，不复制不安全机制。

## 固定接口与所有权

Plugin.Wpf.Presenters.Parameters新增NumericModelReadScope枚举Field/CurrentPage/All，NumericModelReadStatus枚举Updated/Unchanged/Unavailable/Failed/Retired/NotApplicable，NumericModelReadResult(string FieldId, NumericModelReadStatus Status, string? Error=null)。IToolSettingsView新增IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope,string? fieldId=null)。三真实实现（DeclaredSettingsView、BlobParameterEditor、ImageSourceParameterEditor）完整提供，不加默认空实现。公共DeclaredSettingsView实现；BlobParameterEditor和ImageSourceParameterEditor真实转发。宿主RuntimeNodeConfigurationControl提供显式转发与普通读取身份保护。初始化唯一归DeclaredSettingsView：每次Rebuild成功安装字段根并核Current后的末尾All一次，非Loaded；宿主安装后不再重复All，不接运行完成自动回读。这样同外层窗口内Editor.Bind重建（含图像源OnSourceChanged、Blob设计同context重绑）也有独立新根初始化。不新增UI按钮、SDK接口或可保存业务模型。

每个C05字段所有者持有窗口内数值UI模型：已知性/值/投影版本，跨TextBox重建，随字段退役。正式Accepted身份、模型、原raw/pending、宿主普通回读、执行值分别管理；模型变化事件不冒充字段接纳。引用状态也保留并投影模型，不能读来源结果冒充模型。明确单项/当前页/全部范围：只处理本批C05字段，当前页依声明归属，不以控件尚未Loaded当不存在；C06不被隐式注册。重复初始化、换根、异常/无值与旧根调用有明确结果。

C05停止通过ConstantBackup getter偶然回读；复用既有ReadCurrentValue显式取值。未取到不补0、XML默认或旧常量；已有已知UI值可保留但不能当本次回读成功或解绑有效值。解绑读取有值后走原常量提案；无值/异常保持Accepted正式引用与宿主Fields，但沿既有TryUseConstant/BeginConstantEntry允许进入未接纳常量输入态（IsReferenceMode=false），以pending/invalid门禁保护直到新有效常量提案接纳；取消或重绑回正式身份。不能因保护而把UI永久卡在引用态，也不能把旧模型当本次解绑有效值。读取/投影不写正式Fields或CurrentValues，不产生Dirty/Undo/运行，pending/raw不被回读覆盖。接受常量与UI数值模型投影分开，不静默写回舍入值。

整数在原Int32中间计算不溢出的范围沿原格点语义实现；本方采用Int64中间差/格点后按实际Int32收窄，非法Inc/最终越界拒绝并保留原值。极端跨符号中间溢出域属于明确安全适配，不宣称与原可能溢出的逐值结果等价。其他存储宽度沿现有类型合同，不能冒称原Int32全类型等价。浮点复用double/ToEven，有限值与精度验证保留。共享数值准备代码影响现Inc=1范围时跑回归，不顺便改变C06夹值。

## 两条实际开发线

公共线：既有blob-reference-chain-20261101，分支codex/c05-number-model-public-20261103。唯一写Plugin.Wpf所需字段状态/模型/commit/coordinator/视图接口和实现，以及新增独立C05公共测试/runner；不写Blob/UI、现有综合runner、Catalog/O06/CURRENT_TASK。

消费者线：既有blob-display-neutral-20261103，分支codex/c05-number-model-consumers-20261103。唯一写BlobParameterEditor与ImageSourceParameterEditor接口转发（后者仅本API编译闭包，不改图像源业务）、RuntimeNodeConfigurationControl显式读取/身份适配（根初始化唯一归公共视图）、真实窗口C05测试及既有综合验证runner和受影响测试。不写Plugin.Wpf/SDK/Workflow。先消费公共线固定API提交，再独立构建测试，不自造同名模型。

主线唯一负责共享合同、集成、Catalog、原O06和阶段记录。两个现有worktree复用，不新增副本。公共线优先固定API/WIP提交交消费者；完整单元两线最终固定源码和候选回执后集中集成正式同版Release，不借候选成绩签正式。

## 整体验收出口

真实创建→原件装配→初始化/单项/页/全部回读→文本Enter/失焦/箭头/滚轮/滑块/弹层→候选接纳/拒绝→宿主草稿→真实调试→确认取消/UndoRedo/复制/保存重开→隐藏换根及退出。核整数非零Min/负值/非整步/边界/非法Inc/溢出，浮点正负中点/舍入同值/精度0–9/有限性；核引用身份与模型独立、无值/异常不补默认、回读不写业务、raw保留、旧TextBox/回调拒绝及关闭重入。

公共独立测试验证模型与原输入；真实窗口专轨验证实际字段/CurrentValues/文档身份及生产出口。复用B08、Blob单值和必要范围回归，不重复无关导出几何全轨。UI/全量Release实际构建并核加载路径SHA，候选/正式/设计器人工分别记。未证native普通值等价、C06、算法和缺基础组合不称实现；完整28目录/17组及B08 A00–A23原范围保留。
## 已固定首交付及边界复核

公共API/model首提交093c6f76c；消费者Blob/UI两源616fdb3df，均为未验完整单元的中间源码，不是闭口。实际ROI继承11个单元素浮点序列由Options.InputContent的DeclaredInputFieldsView同owner持有，纳入C05 Field/All与可证元数据页查询，不通过任意视觉树发现；模型Value标量，正式值保原Sequence形状，零/多元素拒绝。图文opacity/width未开弹层亦登记；文本X/Y原ButtonSelecter属于另一原件，明确NotApplicable。实际页键须包括附加页插入影响，不能仅假定声明数组索引相同。模型回读失败保已知UI值不等于本次成功，失败初始常量不得保留错误对应关系。

## 固定源自审后的集中校验裁决

原Integer/Float.Check经RangeChecker核解析与上下限，CurValue setter另做格点/ToEven；因此原合法正式值与UI投影不同不代表非法。C05单端将原值校验/ReadProjection与新候选Prepare分离：原值先核真实CLR类型、存储、finite、原上下限及合法metadata，再用double模型投影，不回写、不Dirty、不用显示精度差阻未改动捕获。例min1/inc4的正式8显示5；合法Float32原1.25显示1.2，不因显示值1.2不能精确存Float32而拒绝读取。只有用户新提案才按格点/ToEven后严格Freeze，不放宽存储或将string自动变正规double。C06旧校验不变；reference-only模型应在普通回读及改变模式前拒绝切常量。集中同批测试初始化/回读/原文/未触碰捕获/首次真实编辑、Float32两种出口及无效原值；不把中间b179构建或9组成绩移签修正后版本。
