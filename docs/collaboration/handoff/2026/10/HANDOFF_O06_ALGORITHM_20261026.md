# O06 算法实施交接

- 产品基线提交：`25d3b1a4f0b123586c805c0008334cad08b6a2e5`，main。该中文检查点已保存窗口重组及持久回归入口；正式开始前以主对话派工给出的最新提交核对。
- 当前执行者：从主对话交给 `blob_algorithm_scope`；收到主对话明确放权消息后才开始。主对话转只读审查和文档，不同时改产品。
- 目标：把已批准的 Blob 分割职责和上游承接真正落地，不只隐藏旧参数或改文案。依据为原O06方案及 `evidence/O06_ALGORITHM_TRANSFER_20261026.md`；不声称未知VM原生公式等价。

允许修改：

- `tools/SunEyeVision.BlobAnalysis/` 中算法、参数、输入/输出合同、适配及声明；不改只读结果WPF样式以外的无关UI。
- `tools/SunEyeVision.Tool.ImageFilter/`、`tools/SunEyeVision.Tool.ImageThreshold/`、`tools/SunEyeVision.Tool.Morphology/` 必要的参数、算法、选项/视图和发布元数据。
- `src/SunEyeVision.HalAdapter/` 必要的严格二值准入、灰度转换等可复用算子；保持实际Domain和来源身份。
- `src/Workflow/Persistence/` 必要的旧字段明确拒绝，不恢复旧变量收口；以及对应现有测试、正式Release验证用例。改到其他目录前先报具体必要性。
- 本交接单可补结果；分析原报告SHA已冻结，不覆写历史报告。主对话持有CURRENT_TASK、ACTIVE_GOAL、协议、原方案与文档生成器写权。

必须完成：

1. Blob单阈值/双阈值/不二值化与极性分开；模式、活动字段、默认、schema/稳定ID、保存、执行和实际阈值输出一致。不二值化直接检查原始Gray8及输入Domain内0/255，不能先转灰度或隐式阈值化。无阈值时输出明确nullable，声明与消费者一起适配。
2. 从Blob删除通用预处理/形态学的实际执行与设置，不留双轨。ImageFilter承接均值/高斯/中值/均衡化；Morphology严格消费二值、支持现有操作并明确Circle与Ellipse的尺寸差异，不能枚举强转或继续固定128分割。真实链明确选择ImageThreshold.BinaryMask。
3. 保存遇到旧已启用功能、旧参数或绑定时明确拒绝/提示重新配置，不能删除CLR属性后静默吞掉。软件未发布，不为旧开发方案妥协保留重复实现。
4. 保持有效Domain∩检测区−屏蔽区、反向不越域、最终筛选排序/限数/NG统计、完整边界、资源reader生命周期及按需输出。算法流程变化造成参数/统计差异明示，不能伪装VM数值等价。
5. 全量Release编译与有意义的正式目录回归；不为旧测试保留错误算法，不削弱错帧拒绝、严格输入或资源约束。新增二值边界/nullable输出/上游链/保存拒绝断言。

已完成和现存状态：

- 最新正式全量 `o06-canonical-frame-full-build.log`：0错误/786警告/31.90秒。窗口4组和现行实际宿主整链8组独测通过，总回执 `Release/_validation/O06-Window-20261026/receipt-final-window-fullchain-independent.json`；不是新算法完成或整体UI达标。
- attempt018曾在ROI确认后二次生产错帧；已恢复HEAD既有canonical图像override，attempt019根和020独测通过。必须保留该override；不能把GetOutputVariables的SDK图像RuntimeValue改为中立图像，现行SDK输入仍消费IVisionImage。
- 原外部改动仍未提交：`src/UI/packages.lock.json`、`tests/SunEyeVision.UI.Tests/packages.lock.json`、`BlobAnalysisResults.cs`中min/max属性与映射删除及白像素标签变化。不得擅自重置或混作本次成果。Results须先保存原diff并核对新输出合同；如新合同确需整合，明确逐项依据，不静默覆盖。
- ROI四种继承与整体视觉仍由后续阶段承接，本次不要用像素Region改名充数。真实VS设计器/用户未验。

运行与交付：

- 不新建副本/Worktree/代理；不提交或推送，完成后由主对话审查并按中文有意义提交保存。不得清理其他测试或归档。
- 测试仅基于正式 `Release/SunEyeVision_v1.0.0/SunEyeVision`，日志置于Release下，不复制payload。HALCON沿用加密狗，不单独查许可。
- 产品写者可以构建；独测开始前明确冻结产物并交独测，冻结期间不覆盖。每个实际检查点向主对话报告已改文件、实际构建/测试、问题与预计下一检查点，检查点不停工。
- 交回时列出改动、失败及修复、证据位置、未完成、源与产物身份，并明确释放产品写权。不能代用户通过，不将局部PASS称全交付。

## 算法候选检查点（2026-10-26，尚未释放产品写权）

执行者 blob_algorithm_scope 已按根消息核对 HEAD `4cd3b61512ad9adf06fb605e3d626e4922d468c8` 接手。原三项外部差异先保存到 `Release/_validation/O06-Algorithm-20261026/original-external.diff`，SHA-256 `71C004F19716413B0505452C25484D4D772722D06B6072BCF5EFC70F10B57E3B`；未还原锁文件、Results 的 min/max 删除和白像素标签变化，canonical 图像 override 保留，SDK 图像 RuntimeValue 仍为 IVisionImage。

已实施：

- Blob `SegmentationMode=SingleThreshold/DoubleThreshold/None`，默认单阈值，极性独立 `Type=Binary/BinaryInv`。单阈值闭区间 `[Threshold,255]`，双阈值闭区间 `[Threshold,UpperThreshold]`；反向取有效域补集。不二值化忽略极性和阈值，先验证原始 Gray8 及原始输入整个 Domain 内0/255，再提取255。
- Blob 内置预处理/形态学的设置和执行已删除；ImageFilter 增加有效 ROI-minus-mask 直方图均衡化，原均值/高斯/中值保留；Morphology 改严格二值准入并新增独立 Circle，所有原操作保留。ImageFilter/Threshold 共用明确 BGRA 忽略Alpha的灰度转换。
- 实际阈值输出8/9为 nullable：None 为 null，单阈值实际上限255，双阈值实际配置上限。102端口为 SegmentationModeUsed；Facade 显式声明 nullable 标量，保持现有 SDK 类型体系。
- 分割方式只允许常量：Port.Constant、移除 RuntimeParameterDescriptor、布局禁订阅，以及存储校验拒绝模式槽绑定；Workflow 现有编译器检查配置端口引用能力。有限 FieldCondition.Negate 默认false，Single/Double显示阈值与极性、Upper只Double、None隐藏；条件值/字段仍经原声明校验。未改仅UI布局不应影响的执行指纹。
- 保存前按实际 JSON 字段和 ParamSettings.Settings.ParameterName 拒绝退出字段/旧绑定；不做整串 substring，不误拒绝上游同名输出；拒绝时保源文件不变。

候选实际验证：`build-final-candidate.log` 全量Release 0错误/853警告/32.65秒。正式runner `Algorithm/attempt-006` 9组、`RuntimeKernel/attempt-017` 8组、`Consumption/attempt-020` 7组、`InputBoundaries/attempt-016` 4组、`FullChain/attempt-021` 8组、`O06-AlgorithmFields-20261026/Fields/attempt-001`通过。Algorithm含真实四工具Workflow、nullable下游不能默认0、同source不同run旧Region拒绝、ROI均衡化域、OutputImage亮值200拒绝/BinaryMask接受、真实仓库保存重开及旧已启用设置拒绝。不是独立测试、设计器或人工验收。

失败保留：首次/第二次全量构建为新增 AreaCenter 重载二义性（第二次编辑脚本编码失败未真正落修复），显式HTuple后通过；Algorithm早期fixture缺using、多态注册与实际插件隔离上下文不符、C#语法错误均保留，改用正式注册参数类型后通过；脚本第一次powershell启动被执行策略拒绝，S1脚本在Windows PowerShell按无BOM误解码后用pwsh正常运行，均不是产品算法失败。各轮日志在 `Release/_validation/O06-Algorithm-20261026` 及对应正式runner attempts。

候选已冻结给独评 AlgorithmStrict6/AlgorithmWindowState4，未覆盖产物。身份 `frozen-candidate.json` SHA-256 `63CF89A370232462F3703FDD0AFCEA471B3C363F71B73D45257E2EE41796467D`，含源码SHA和完整payload列表。Blob facade `5DA681140232C9D0ED0744EB4A6CE637A4DE24843395D89368263CE88C46888C`，Blob Runtime `2A6B81AC83449BFC479B9C6D5A2CC1A650ED5F459880E9BA66C2ED740D2DCED6`。

冻结期间发现尚需修复的真实承接缺口：旧Blob形态学尺寸为1..101并允许偶数，Morphology原限制仍为1..51奇数；已通知根和独评，待独评明确释放冻结后修参数/界面范围及断言，再重建并验证受影响范围。当前候选不称算法整体闭合。本路没有提交或推送；根仍持有CURRENT_TASK/原O06文档写权。

## 独测发现与三项修复后的新候选（2026-10-26）

第一候选独评 Strict6 + Window4 实跑10组通过，回执 `Release/_validation/O06-Window-20261026/receipt-algorithm-independent.json` SHA-256 `4EE53E472F33CEEA7330F299463CAE999DA8721F1C0BFB028AEA35F32DB96104`，666项payload前后不变、72条已加载产品记录来自正式目录且SHA一致；18张PNG已由独评查看。另补 hiddenUpper probe实际确认：Double下Upper切订阅未选源，切Single/None虽隐藏却仍阻塞View.TryValidate；回Double pending仍在。首probe因收尾提示停住，后续恢复常量自然退出；不描述为“窗口永久不能关闭”。独评已正式释放第一候选冻结。

按根明确授权一次合并修复：

- Morphology参数上限101，允许偶数且不改成最近奇数；XAML Max101/Step1及提示同步。实际Rect尺寸2/100/101和双轴组合的膨胀面积精确等于宽×高，全部原操作保留。Circle保原较大尺寸/2整数半径合同；测试先猜半径1圆为5像素而真实失败，保留失败后改与原迁出native圆实际基准对照，HALCON实测为4像素，未改产品公式凑预期。
- Blob None显示标签按字段定向，SegmentationMode.None为“不二值化”，SortMode.None仍为“无”。
- DeclaredFieldsView用FieldPresentation.Layout.VisibleWhen建立逻辑活动表，显隐、TryValidate和HasInvalidActiveFields使用同一有限规则；宿主HasPendingEdits读取新活动校验属性。不依据IsVisible、选中Tab或Expander状态过滤。隐藏Upper的pending不清空、不写默认值；Single/None不阻塞，回Double恢复阻塞；未解析Reference保持显式活动，其他Tab及收起Advanced里的活动错误仍阻塞。额外修改宿主单文件scope已由根授权。

最后全量 `build-revised-candidate.log`：0错误/894警告/28.60秒。新候选正式runner `Algorithm/attempt-008` 9组通过（含以上实际事件与边界断言），`FullChain/attempt-022` 8组通过。未对无新变化的Kernel/Consumption/Input/Fields重复扩大测试；第一候选对应成绩保留其原身份。字段新结构109输入、25组、79展示叶、32门面输出，字段用例已同步；原外部min/max删除仍保留且未冒称本轮新增删除。

新候选冻结身份 `Release/_validation/O06-Algorithm-20261026/frozen-revised-candidate.json` SHA-256 `ABDE8D55699F1D1BEC57E7819F5E3107177F5735CF89683913BCD9AE54C38C9A`；包含本轮源码SHA、diff SHA和完整payload列表。Blob facade `D04CB847F5951D29AF95EE40B85C4F8D8844DB877B3AF6AFC4583D69D26CCA3D`，Morphology `666BF12FE4C63A871037324541B5C5CC52E6EBB7736976AA9187B0737DA47B1D`，UI `56A38265D2AB40268891E20C3D04DA98FD9BC59C043FB3FB2FFCE253CB94779D`，Plugin.Wpf `BB26E31AB631B19BB92218472ED259BE5957104BBBBA522486BA003A3BDAB907`。旧候选清单和所有失败记录未覆盖。

新候选已再冻结给独评原10组、hiddenUpper实际宿主和范围承接复验；尚待该回执。本路当前不改产品/构建覆盖，暂保唯一产品写权等待必要修复，未提交/推送。ROI完整四继承、整体视觉与VM未知公式、实际VS设计器和用户人工验收仍未完成，由根按目标继续推进。

冻结中的关联释放审查：根要求只读核查上游原生输出所有权，确认三个上游Results尚无自有ResultResourceOwner；ToolResults.RegisterOutputResource仅释放注册typed snapshot及previous owner，previous为null时不会释放原OutputImage/GrayImage/BinaryMask。AlgorithmNode.StoreLastToolResult/ReleaseLastResult、WorkflowExecutionEngine.ReleaseExecutionResources、ExecutionResult.Dispose→NodeResultSnapshot均只释放Resources及其租约，没有另一条GetDisplayImages原图Dispose出口；AlgorithmResult也不承担释放。因此现有run.Dispose/Blob reader测试不能代替上游原图关闭验证。已向根定位回报；当前冻结未修改。建议后续在三Results明确拥有自己创建的图像，原生输出临时HObject构造也用using闭合，并实际验证各上游DeclaredOutputReader跨run.Dispose保活、最后reader释放原图关闭、独立pixel lease仍可读/最终关闭，以及失败部分输出释放。全局旧native临时算子审计不在本结论覆盖范围内。

## 资源释放实证与当前冻结候选（2026-10-26，独评进行中）

上一修订候选独评已完成 Strict6、Window4、hiddenUpper 实际宿主活动性与 Morph 尺寸承接，回执 `Release/_validation/O06-Window-20261026/receipt-algorithm-revised-independent.json` SHA-256 `3BB274B0E0903C7DD803DC82627AFC53EF956DE6929D395AE65493E02F87EA82`；104条加载记录来自正式目录且SHA一致，独评明确释放冻结后才开展以下修复。

资源先有真实失败基线：`Release/_validation/O06-Algorithm-20261026/resources-baseline.log`、正式 `Resources/attempt-001` 同12组全部失败。四工具真实run释放及最后declared reader关闭后，8张上游原HalImage仍可访问；三工具在创建gray后失败仍留原生图；同步/异步取消或身份失效的迟到owned结果仅执行legacy IDisposable，owner释放计数为0。不是只根据静态源码推断，也未用Blob自己的reader代替上游生命周期证明。

根授权后限定修复：ImageFilterResults、ImageThresholdResults、MorphologyResults显式持有新建图像的ResultResourceOwner，按引用去重且各图清理异常不阻止后图；ImageFilter、Threshold、Morphology本链输出HObject临时包装using闭合。ToolExecutor.Admission/ToolExecutor的同步和异步迟到拒绝分支同时清理Resources及原IDisposable，尝试全部清理后聚合真实失败；不动调度/UI执行边界，不扩成全部旧tuple/native算子审计。新增持久 `O06ResourceCases.cs` 与正式runner `-Stage S3 -Track Resources`，覆盖真实原生图、declared reader、pixel lease三级寿命、调用者输入不被释放、失败部分输出及8种同步/异步取消/失效/清理异常组合。

原外部差异整合依据已由根逐项审核：两lock仅增加Plugin.Wpf项目及Contracts依赖，可由根最终归并；BlobResults原删除MinimumArea/MaximumArea与现行Runtime稳定输出30/31矛盾，本轮经明确授权恢复派生属性及GetPropertyMap映射，并以排序限数后的同一最终集合核Runtime/Facade/formal三方min/max一致。白像素短标签保留，canonical override和SDK IVisionImage RuntimeValue继续保留。原外部diff归档不删，不将恢复属性描述为悄悄回退未知修改。当前字段结构为109输入、25组、79展示叶、34门面输出。

最后全量 `build-resource-final.log`：0错误/786警告/27.87秒。正式作者回归 `Resources/attempt-002` 12组、`Algorithm/attempt-009` 9组、`FullChain/attempt-023` 8组及Fields（exit0、34输出）全部通过。Resources实际原8图可访问计数由8降至0、失败gray已关闭、独立pixel lease跨owner仍保活至最后释放、输入仍可用；迟到结果owner与legacy各释放1次，正常拒绝为Cancelled、真实清理异常为Failed。Algorithm补强nullable下游判因：前4节点成功，失败明确来自required-threshold的Threshold/null绑定，实际诊断为“参数 Threshold 解析失败…disallows null”，不以任意失败算通过。未重复无变化的其他轨道，不将历史候选通过数混作当前完整重跑。

所有作者构建/回归进程均结束后明确冻结产品/验证源码及正式Release，交独评资源必要复验。当前清单 `Release/_validation/O06-Algorithm-20261026/frozen-resource-candidate.json` SHA-256 `7481E3D2BF70528D86868DED79C32D1F31ABF5B56DB6E5D4F79371186548B5B2`，含完整源码/产物SHA及diff身份。当前仍是HEAD `4cd3b61512ad9adf06fb605e3d626e4922d468c8` 加未提交增量；未提交或推送，未释放产品写权。冻结中仅补本文档，等待独评实测及明确释放；父级已提醒聚合异常终端诊断可能未完整保留inner链，须以独评真实双throw结果判定，当前没有提前改源码。

未做范围仍为ROI完整四继承、整体VM视觉差距、真实VS设计器及用户人工验收；本轮不承诺未知VM原生公式数值等价，不以资源/功能PASS代替整体外观验收。父级继续统一维护原方案、CURRENT_TASK和阶段中文提交。

### 资源独评诊断缺口及定点修复候选

资源独评首轮22项18PASS/4FAIL，回执 `Release/_validation/O06-Window-20261026/receipt-resource-independent.json` SHA-256 `AE276BDD0B1CF540EFD3867C47C35A3CDC81D8BD4B3DC8F1AA4101C67BEE3C61`。真实native/三级租约/输入保留、部分失败、引用去重、不同引用及清理异常继续后图均通过；4项失败是同步/异步×取消/失效在owner与legacy同时throw时，终端ErrorStackTrace只保外层堆栈，遗漏完整Aggregate类型/inner链。666payload及41source前后一致，33条加载记录为正式产物；没有把此诊断失败说成原生释放仍失败。

独评明确释放冻结后，按根限定授权仅将 `ToolExecutor.cs/CreateTerminalResult` 的 `ErrorStackTrace=exception?.StackTrace` 改为 `exception?.ToString()`，短ErrorMessage、状态与清理语义不变，不修改断言。全量 `build-resource-diagnostics.log` 0错误/1087警告/47.52秒；正式 `Resources/attempt-003` 12组及 `FullChain/attempt-024` 8组均exit0。所有作者进程结束后再次冻结，清单 `frozen-resource-diagnostics-candidate.json` SHA-256 `F28E361C85EB7BF15130E3CE1ACF995A832AE066F315757E541786B1A9F4E0A2`（41source/666payload），已交独评同22项复验。尚待新回执，不将原18PASS/4FAIL改写；当前仍未提交推送或释放产品写权。

## 本轮最终回执与写权释放（2026-10-26）

资源诊断候选同22项独评全部通过/exit0，回执 `Release/_validation/O06-Window-20261026/receipt-resource-diagnostics-independent.json` SHA-256 `2980A7D220F8330D103197F041680017DE1ECA479524670D989EABDCE4451787`。独评fixture SHA `178B2300ADB48D78819D3270975CDB641781F4C53A2CAE0686027765BC158DBA` 与原18PASS/4FAIL完全相同，未削弱断言；4种双清理失败均保留Aggregate与两种inner类型、细节和栈。41source与666payload前后全匹配，33条已加载产品记录全部来自正式Release且SHA一致。独评扩到真实四工具第4个Blob节点的declared/pixel reader、三Results同引用去重/不同引用分别释放、首图异常仍清后图，以及owner与legacy同时throw；本轮无新资源阻断。独评明确释放冻结，旧失败回执和日志完整保留。

blob_algorithm_scope 现明确释放唯一产品写权及本交接单写权交还 `/root`，不再修改产品/验证源码或构建覆盖，不提交/推送。最后受测身份仍为 `4cd3b61512ad9adf06fb605e3d626e4922d468c8` 加清单 `frozen-resource-diagnostics-candidate.json` 所列未提交增量；这不是新提交身份。父级接续统一原方案和CURRENT_TASK、按审核决定中文阶段提交。算法迁移及关联资源缺口已在本轮范围实做并验证；ROI完整四继承、整体VM视觉差距、VS设计器及用户人工验收继续由父级推进，不能将本回执称整体O06完成。
