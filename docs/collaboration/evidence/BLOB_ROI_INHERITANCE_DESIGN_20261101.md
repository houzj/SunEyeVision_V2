# Blob ROI 矩形/圆环继承完整组合实施设计

基线 `1bfb7536`；工作区 `D:/MyWork/line-aiproject/blob-reference-chain-20261101`，分支 `codex/blob-roi-inheritance-20261101`，本线唯一写入。先整体设计，再按已证范围集中实施；候选与主仓正式产物分开记录。原VM不启动，未知不按名称补齐。

## 原件、已有能力与裁判边界

主证据为 `D:/MyWork/REA逆向测试/docs/joint-audit/B08-ROI继承与位置修正静态证据-20261101.md`，其中原Group.xml SHA256 `9129B82A0F9906DDE591347498A80E2E8C019C0FB959A721ECFD24FBBB5207DC`，Blob声明XML SHA256 `F1B2F279717DCE3EC61C11C41F858BD100359095BCF13672422A3C73EFE47F6C`。Group.xml:1308/1709–2112、Blob XML:44–65确认下表；不是像素Region。原CurValue=-1只是资源声明，不证明实例最终状态。

| 原模式 | 原源/目标 | 本批完整消费者 | 已证/未知 |
| --- | --- | --- | --- |
| 按矩形区域 | ROIBOX组合 → InputROI | 一个结构化组合来源、完整5叶验证后物化矩形 | 五float叶与组合订阅已证；角度单位、native坐标/栅格及失效最终值待补证 |
| 按矩形参数 | 5个独立float来源 → 同一InputROI五叶 | X/Y/宽/高/角度独立订阅或变量/索引，批次完成才物化 | 各叶可来自不同源已证；不能强制同模块，也不能猜五叶天然同帧 |
| 按圆形区域 | ROIANNULUS组合 → InputROIAnnulus | 中心X/Y、内外半径、起始角及跨度完整6叶 | 名称“圆形”不能缩成圆；角度/跨度及native入参仍待证 |
| 按圆形参数 | 6个独立float来源 → 上述六叶 | 六叶独立来源，单批原子验证/物化 | 不借单一圆形、像素Region或完整圆环丢失角字段 |

原SetCombinationSourceOperation.cs:25–88已证按来源路径/索引设置RelateIO/RelateIndex，找不到来源提示返回；不能据此证明所有持久模型清除。主线补证初报的先订阅、设索引重订阅及失败清空内部状态暂不扩大为保存语义。几何角度和帧身份补证由主线集中接收。

已有本方基础直接复用：BlobLocalRoi纯Scalar/Record/Sequence唯一持久本地形状；BlobLocalRoiProjection到RoiDeclarations.Shape无状态投影；BlobLocalRoiRasterizer的四形并集/Domain裁剪；NativeRoiSession/Editor/Viewport、原继承radio与5/6行布局、DeclaredFieldEditor/ReferenceFieldOptions及来源/变量/索引整链；RuntimeFieldJson已有Scalar/Record/Sequence及引用身份保存；正式InputValues与publication reader lease、私有调试及文档草稿/历史/复制/保存重开。不得引入RoiParameters、SDK可视或第二几何owner。

现有具体差异：四继承目前均是ui-only并由总捕获拒绝；Blob正式输入只有图像/像素Region/仿射与本地纯值；ROIBOX/ROIANNULUS并非这些Region。现本地矩形角度degrees、环start/end radians是中立平台合同，不能冒称VM数值等价；Blob当前没有正式ROIBOX/ROIANNULUS输出生产者，现有bounds/rotated-bounds也不能按名字当作同一组合。

## 唯一正式配置与实现范围

继承模式和来源加入Blob私有输入声明，不增加全平台ROI模型。模式只允许常量，决定活动输入集；组合输入用具有精确成员声明的Sequence<Record>，叶输入使用现有NumberArray声明及独立ParameterSetting（Reference/Variable/Constant按原证据和明确适配分别声明）。持久存储仍仅RuntimeConfiguration.Fields；本地几何与继承配置各有明确职责，模式切换不清空另一模式配置。UI临时投影不能成为第二可写owner。

整个继承分支完成后，以已解析5/6叶构造一次临时BlobLocalRoi形状，经现有验证和栅格器消费，不将本轮解析值写回正式常量或本地几何。原本地检测/屏蔽草稿保留，绘制模式继续原链；非活动继承引用保留但本轮不编译、不取资源、不形成依赖、不消费。活动缺值/非法/断链必须整批拒绝且无新结果，不可回退旧解析值。组合须精确验证类型及全成员，不自动从任意Geometry/Region或同名bounds猜转换。

角度/坐标/帧证据未闭合前，相关模式不得默认开放为原厂等价。可独立实现正式声明、保值管理和活动执行规则、完整本方合同测试；真实生产继承入口只有在准确转换与实际来源身份能被证明或明确获准为平台适配时开放。无当前真实组合生产者时允许空候选并明确不可误用，不造假生产者或无消费者平台接口。图像mask、输出mask、屏蔽继承和三位置模式保留原S1交互及现捕获保护，不在本批伪接通。

## 最小工具无关活动规则（主线已授权本线唯一写入）

需要一个可选Contracts纯规则：工具根据不可变FieldSettings快照返回本次活动**输入**ID集合。无此规则的工具默认全部输入活动；设置字段始终维持现有顺序/消费，不让工具隐藏任意正式参数。返回值必须非空集合对象、无重复、仅含已声明输入ID，必需输入不得省略；模式不能来自本轮待解析引用，须在配置快照中是合法常量，否则fail-closed。

RuntimeToolConfiguration在构造时从其不可变Fields冻结并验证活动集合；同一配置实例的依赖编译、变量声明捕获、取用计划和执行读取同一集合，不能每一阶段重新调用可能变化的外部getter。CaptureExecutionFields依原ParameterBindingOrder再按活动输入过滤，保持参数写入顺序；不能改变其全量持久Fields/绑定顺序/复制序列化。UI不引用Workflow判断活动。

执行视图与管理视图分离：BindingRelationCatalog原References/VariableReferences/Describe保留全引用管理，增加明确用于执行的过滤出口，不能在原管理出口静默删非活动项。BindingInputReference.Active可投影执行可用性，但删除/复制/变更政策仍必须能枚举到所有存储引用。

完整调用链必须同批接入：

1. RuntimeInputBindings.Validate的执行准备校验仅检查活动引用（结构化配置合法性继续全量）；Describe保持全项，活动标记准确。
2. WorkflowExecutionDependencyGraph、SolutionExecutionDependencyGraph的引用、变量及动态索引递归仅消费活动视图，不能让非活动失效引用/变量生成循环或阻塞。
3. WorkflowBindingContext执行编译（当前:215直接Fields变量、:227 References）改用同一投影；CompileBindings、输出需求、snapshot dependencies和reader lease同样限活动。
4. RuntimeDebugInvocation在:52收集references/variables、前置source publications、:176编译plans及:196 Prepare均使用同一冻结活动快照。不能只修Prepare造成提前失败。
5. AlgorithmNode.Runtime.CaptureExecutionFields及CompiledBindings依赖reader使用上述同一个配置，先取得所有reader再捕获本轮snapshot，解析后原子构造InputValues；BlobInput/Session只消费活动分支。
6. 管理路径：BindingConfigurationStore、OutputBindingChangeService、文档关闭/保存/历史/复制/跨方案remap继续完整存储引用，包括非活动项；删除失效理由不得被活动过滤漏掉。恢复模式后重新校验，不能用旧plan或旧current值。

其他工具不实现纯规则则行为完全不变；不得ToolId特判，不把Blob面积的FirstItem政策推广到这11个float叶。新叶采用现有序列目标合同：All完整序列、显式固定/动态索引后单项序列；不得静默取首或广播。原生条件与默认见下文最终报告。

## 同批冻结、帧、预览与退出

解析使用执行已冻结publication/variable snapshot，源身份包含scope/node/port/member/storage/selection、变量solution/type及动态索引完整身份。所有11叶的活动子集在一次invocation中解析；模式/非活动引用不在执行中切换。解析值只在本轮局部物化，失败不发布、不半写current或正式配置。

几何静态CoordinateFrame不是物理ImageFrameAddress。绝不由同尺寸推断同帧，也不由同一NodeId或同一logical frame推断同一publication。纯数值叶自身没有物理帧元数据，不能声称已证明其来自当前图像；需原件补证决定其语义，或明确声明它们是在当前输入图坐标中解释的参数。组合若要约束与源图同物理帧，必须依已有实际publication/resource receipt证明；现无此证据则保护，不扩造无消费者帧平台。

预览复用现NativeRoiFrameSource借当前可见、真实source/key/receipt匹配的输入图；不自动改用户选图，不借未显示InputPane、不拿VM缓冲指针。继承投影只读，不能被本地手势改写继承来源；参数原文pending和打开弹层未确认必须保留/拒绝捕获。换帧/切图先退役旧手势和lease、保留pending，旧回调不能写新目标。关闭/取消只退役投影，不改正式Fields或文件；confirm由现正式Host原子接纳入历史，保存/复制/Undo用同一纯值配置。

## 集中验收矩阵

| 轴 | 必需相称自动验收 | 未证/禁止冒称 |
| --- | --- | --- |
| 声明/模式 | 四种继承、5/6叶精确字段及原条件显隐；绘制↔继承切换保留另一配置；空/错误模式fail-closed | 不以资源CurValue=-1当实例默认 |
| 活动合同 | 其他工具默认全量；重复/未知/漏required拒绝；同配置规则仅冻结一次；活动输入全链一致 | 不把active标记当管理引用不存在 |
| 引用 | 每叶独立来源/变量/固定和动态索引、整身份；非法/断链/越界原子拒绝，切非活动不阻塞，重新激活仍拒绝 | ALL/native多值消费未证不放宽 |
| 图与快照 | 正式与debug活动依赖一致；非活动缺源/变量/跨scope/自环不取用；运行开始后源/变量变化仅影响下轮 | 同一次未筛选算法测量不证明VM等价 |
| 物化 | 矩形5叶和圆环6叶独立敏感输入，点集/最终Blob对象集合证明实际裁剪；正负扫角/整环及非法尺寸 | 未得角度证据前仅称本方合同或保护 |
| 生产UI | 原真实selector、两个popup/展开窗、切模式/目标、pending拒绝恢复；现真实BlobHost调试/confirm/cancel | RaiseEvent是原WPF事件链，不是物理键鼠 |
| 生命周期 | 同帧/异帧/换publication、只读预览/旧回调、取消关闭/切图、lease最终释放 | 无receipt不宣称实帧预览闭环 |
| 管理 | 非活动引用仍参与删除/复制/remap/历史；撤销重做、保存真实重开后实际执行及结果oracle | 不能只比LoadCandidate而执行旧对象 |

先冻结产品/测试源码，再标准独立UI/全量候选构建，加载DLL逐项核本候选；不复制主仓Release。新专项按Model/Binding/Window/Frame有严格筛选，完整清单逐项记录，不以组数代完成。复用现BlobRoiProduction、RuntimeWorkflow、FieldEditing/VariableFields与受影响原窗口回归；准确保留环境/夹具/产品失败。自然键鼠/VS/其他DPI无证据则列未验。

## 本批文件边界与交接

Blob.Runtime私有继承声明/验证/临时物化、Blob.Wpf实际组装/owner、Plugin.Wpf原继承组合最小绑定适配及专属测试；唯一共享扩展只为当前消费者的Contracts活动规则与RuntimeToolConfiguration/BindingRelationCatalog/RuntimeInputBindings/两依赖图/WorkflowBindingContext/RuntimeDebugInvocation/AlgorithmNode必要接线。主线已授权本线唯一写入这些接线；遇超出范围或新增资源/帧合同先报具体缺口。工作流独立工作区及主仓均不写。

当前已实施四态声明、执行活动投影、11叶原数值/来源/索引控件及多项物化；冻结004候选UI和全量构建0错。真实窗口、提案拒绝、保存重开和运行回归仍在验证，未将已有局部通过称作整组完成。

## 原生补证及本批明确适配（实施前冻结补充）

已核最终报告 `Blob-ROI-inheritance.md` SHA256 E53EE5EAF853530065E6DBCE18B8B517D0E4E4FE9840C8D2A4E744A338DC5E09，主线归档为BLOB_ROI_INHERITANCE_ORIGINAL_20261101.md。矩形helper18001c790五叶初查全为0xe000031b才状态1返回；中心/宽/高需成功，计数取五叶最小值上限64，param_5为0仅首项；角度特定非成功可置0，0xe000031c/状态2另拒。扇环18001bf70初查六叶全0xe000031b才状态1；中心X/Y和外半径必须成功，其他分支内半径/起始角/跨度可置0/0/360；最小计数/64及param_5、param_6各有条件。不能概括所有缺叶都有默认或始终多值。RelateIO/RelateIndex重订阅失败清内部关系，不证明完整事务/持久回滚。

原±180/360强支持degree但仍属推断；坐标、帧及Domain当前无法确认。实际本批显式适配：继承参数在本次冻结输入图像像素空间解释，角度degree，内外值按半径，使用平台double/序列目标；本地已有栅格化转换不另重写。Group.xml:1989等Description是CircleInnerRadius、目标ROIAnnulusInnerRadius，未发现选择器除2；中文内径/外径不能证明要除2。原VM float32、缺叶回退及来源跨图语义不宣称等价；min-count/64依据后续多项报告实施。本批不是VM像素算法等价，替换位置为BlobRoiInheritance.Materialize，修正条件为后续取得对应native/原机坐标与单位证据。

所有互斥继承顶层Port均Required:false；组合内部5/6成员仍Required:true，通用FieldSettings/InputValues/JSON必填合同不改。活动模式缺整来源/任一叶由Blob ValidateExecutionInputs纯配置校验在确认/执行时拒绝，编辑模式切换本身允许，不能塞默认伪值。配置模式仅constant输入，不进入SettingsSchema；原构造ValidateIndexSelections仅纯身份结构约束，CaptureSettings只读原设置，无预先解析休眠继承来源。

管理Describe.Active不修改；新增DescribeExecution及显式executionOnly引用视图。方案ConsumerBindings执行过滤按现行InputKey或ParameterName对key/name匹配，只有唯一匹配的非活动字段才省略依赖，未知/歧义保留保守依赖；登记不删。Host RuntimeNodeConfigurationControl只提交/核活动input pending，休眠编辑器原文仍在原实例且HasPending仍报告，切回模式重新校验。额外最小UI适配ComposeInputEditor复用同一Host read/write/reference回调并保留声明EditModes，限工具组装的未呈现输入；不改变既有四个Blob输入或其他工具的编辑模式。

真实组合来源无当前正式生产者，两组合候选空时必须拒绝，不能用测试Record称真实生产可选；逐叶用真实ImageLoad/Blob标量或数组选择验证。继承预览现尚无本轮已解析输入传给参数页的既有通道，本批不伪造帧/投影；整体原组合Tooltip清楚标记，既有本地绘制与同帧借用继续原链。执行裁剪与只读预览分别记验收，不能用执行成功称预览已完成。

## 多项正式合同修订（以此节覆盖前述单项阶段方案）

已读原Blob-multiple-inherit-flag.md：CreateModule构造→Blob ResetMember把+0x2f覆盖为1，若实例后续未改写，四态可达各叶min-count/64多项；一形无屏蔽直接SetROI，其余列表经一次PreproMaskTool再执行一次Blob。原mask逐像素合成仍未证；本方复用既有检测并集、屏蔽并集相减再与真实Domain相交，明确为当前平台假设，不能以Union2称VM像素等价。Blob-annulus-radius.md（SHA8704FD69BEEBC5B56331A578BB2CF778845E73BA7AFF5B2264555B6FC2AB2274）确认已追输入→24字节记录→对象参数无除2，按半径原值有静态依据；上游及后续全部缩放未覆盖。

单项方案不作为最终提交。唯一正式声明改为：11叶均Sequence<Number>、两组合均Sequence<Record5/6>，仍为可选顶层/必需组合成员。ParameterBindingService现有scalar→singleton、All→全序列、固定/动态索引后的scalar→singleton直接复用；不得使用面积FirstItem策略。本轮DeclaredValueReader统一读double[]及SequenceValue，逐叶取最小数量、最多64，常量由本方UI提交单项序列，不广播；该常量计数是本方明确输入合同，不另宣称VM常量IO全部路径计数已证。空/缺叶仍明确拒绝，不按原helper部分缺省条件猜默认；矩形角及扇环内/起始/跨度各自条件保留前节，不称空叶策略原厂一致。

UI正式state/Port全程保留Sequence，未新增ReadReferenceTarget、假scalar Port或第二正式字段。ComposeInputEditor(...,numericSequence:true)只让原NativeNumberPresenter/NativeNumericCommit在数值显示/提交边界对单项序列与double做无状态投影；来源树、变量、索引、ValidateProposal与宿主验证均用实际Sequence Port。多项/空常量不能取首显示，原文和值保持并给明确原控件错误状态，用户可显式输入新单常量或选择订阅；打开/取消不写。其他Scalar/Sequence消费者默认行为不改变，受影响普通数值/范围回归须执行。

继承执行临时BlobLocalRoi.Group可有64项，但本地绘制持久owner仍16项；两者通过BlobInput.IsInheritedRoi和BlobSession实际输入重载区分，栅格器按本轮明确上限验证，不把本地上限全局改64。所有继承几何只是本轮局部物化，不写本地纯值owner，实际mask仍来自同一原配置快照。min-count/64、全部五/六叶、相交/离散图形、单常量限min、空/错误、固定/动态单项、首64/第65项排除、保存重开和快照变化均加入专轨；正向组合的模型Record仅验证类型/物化，无正式生产者仍列不可用，不冒充实际可选源。

首次model-001真实退出1：矩形中心改30、宽12的点25仍在[24,36]，是测试oracle选点错，修为原中心20；保留日志，未据此改产品。首轮产品UI/Full001及002均0错，但多项修订以后须再冻结重建，不将旧DLL回执算最终。

## 实测缺陷与提案事务修订

候选021真实模态每个目标候选均存在，但六次确认后Editor/Draft仍Unset，错误BLOB_ROI_SOURCE_REQUIRED：UI.ValidateProposal原先调用Host.Validate完整提交校验，活动组第一叶被其余缺叶挡住。此为本批确证的产品接纳缺陷，不是点击成功即已写入。

修复边界：RuntimeNodeConfigurationControl引用提案复用Host.ValidateRuntimeFieldProposals；该方法在Compose的独立候选上应用真实Fields（输入+设置），校验全部已配置活动来源/变量、已知设置跨字段约束，再用SolutionExecutionDependencyGraph(checkReadiness:false)检查全部活动引用、动态索引及变量生产者图环。引用值的运行期跨字段关系无法在此提前推断，仍由运行物化验证。只允许尚未填写的条件组继续作为草稿，未削弱实际坏来源、类型/存储、基线/版本及间接环保护。RuntimeToolConfiguration.ValidateSettings为Host内部已知设置检查，公开Validate仍先检查完整活动输入；最终TryCapture/Confirm/Host.Commit和正式/Debug执行继续完整拒绝。NodeConfigurationEditHost.cs已获主线唯一写入授权。

实际失败保留：001模型矩形oracle点错；002/003圆环内半径9对(26,26)受HALCON离散边界影响，独立GenCircle/GenCircleSector差集与实际Region一致，改为远离边界(25,25)后004模型7组通过。009/010几何9像素被原默认面积下限10合法过滤，012独立冻结探针证实叶长度1/1/9/1/1、宽2高2和实际Count0；几何夹具随后通过正式Host关闭过滤，另留默认过滤/关闭恢复实际对照。019模态只看前10项，改驱动原Next按钮翻页，020/021进一步暴露上述真实提案缺陷。所有夹具编译/事务前置失败不算产品失败，不删记录、不放宽原强断言。

## 候选阶段交接（004冻结产品）

- 基线/分支：1bfb7536 / codex/blob-roi-inheritance-20261101；本线独立目录唯一写入，未修改主仓、正式Release或工作流工作区。
- 已落地：14个Blob私有输入（模式、两组合、11叶）、实际原ROI四态组装、数值序列单项编辑边界、真实来源树整数到浮点提升、两级执行依赖图及绑定/Debug/快照活动投影；原全量引用管理保留。NodeConfigurationEditHost只在独立Compose候选进行提案验证，已确证并修复的六叶首项死锁不再发生。
- 最终构建：UI004 0错/6539警告、全量004 0错/6541警告。27个产品源码与004冻结记录0差异。后续只修测试夹具及文档，未改产品。
- 候选终态：Binding029九组、Window030十一组、Publication027一组、Model028七组；FieldEditing001实际31组、VariableFields001十三组，全部exit0。继承四轨各15个已加载DLL，字段12个、变量2个，与独立候选Release逐个哈希一致。脚本本基线为31组，不冒称历史38组。精确PID、测试源码SHA、原始失败、载荷哈希和命令边界统一见 BLOB_ROI_INHERITANCE_CANDIDATE_20261101.json。
- 关键实测：真实圆环六次变量确认后Editor/Draft均保留正确All身份；两环实际Count2/面积12、两个环正点与内孔/外域负点、单常量min1、固定/动态单项、确认/历史/保存重开真实引擎执行。64上限和第65项排除为模型+实际HALCON执行验证，不称真实窗口64项全覆盖。
- 提案拒绝证据：不完整组合法首叶可接纳，最终Validate/Commit仍拒绝，正式值不改；坏来源和自环拒绝。A→B控制前序与真实参数边建立后，反向ROI输入提案到WorkflowExecutionDependencyGraph.GetTopologicalLayers真实循环诊断；变量生产者到SolutionExecutionDependencyGraph.AddDependency真实自环诊断。均经过Host.ValidateRuntimeFieldProposals:308，不将候选类型/前序拒绝冒充完整图环覆盖。运行期引用数值跨字段约束仍由物化校验负责。
- 失败保持：022/024小圆环正点在HALCON离散域外，独立runs证实后改用(2,1)/(6,5)正点；025间接环夹具缺控制前序，026把已确认圆环误当空组；均留原失败，不改产品迎合oracle。007及前序夹具/产品失败记录继续保留。
- 未完成：两组合无正式ROIBOX/ROIANNULUS生产者，空候选及缺源拒绝；继承预览未接；原生坐标/帧/float32/像素合成等价未证，使用本方冻结输入像素、degree、半径及已有HALCON并集/差集合同；空叶严格拒绝，未模拟原native状态默认；原自然键鼠/剪贴板、VS/人工未验。图像ROI/mask、输出mask、屏蔽继承和位置三方式继续保护。休眠坏来源完整正常执行不取reader尚无独立行为探针，本批有图编译/捕获与静态投影证据，不声称全域生命周期已验。
- 下一步：主线按文件集成后重建正式Release并执行正式整合回归，补真实组合生产者/继承预览及原始未证语义时再闭合对应入口；本候选不称完整ROI、Blob或S2/S3完成。
