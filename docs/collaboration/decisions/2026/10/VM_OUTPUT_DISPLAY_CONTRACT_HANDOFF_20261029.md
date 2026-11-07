# 输出目录、来源树与结果视图：统一展示合同草案（2026-10-29）

用户要求来源树、结果树与结果表依照 VM 的行为共享真实输出信息。本文基于本仓源码与已核逆向资料描述合同缺口及建议；不是已经实施，也不把 VM 安装文件组织推断为其源码工程结构。

## 当前确定的问题

- `src/Workflow/OutputBindingChangeService.cs` 的 Runtime 输出目录将 `DisplayName` 设为端口 ID。`RuntimeNodeConfigurationControl` 和 `DeclaredToolConfigurationControl` 用该目录建立来源候选，因此会出现 `output.image` 等内部 ID。
- `src/Plugin.Wpf/Adapters/References/ReferenceFieldSession.cs` 用 `NodeId` 显示模块根，候选完整标题显示在扁平叶上；`NativeTextSession.cs` 有另一条同样按节点 ID 平铺的树。截图中的模块名、输出名和多层成员没有完整对应。
- 当前 `DeclaredOutputSnapshot.Fields`、`IResultPublication.DisplayNames` 主要携带顶层端口元数据；`ResultPresentationPresenter` 只更新顶层标题，嵌套节点依赖成员 ID。仅替换一个下拉显示文本无法解决结果树或成员路径。
- `ResultProjectionDescriptor.Columns` 是某个视图的列投影，可包含对同一字段的不同列和标题，且可能不列出全部输出；不能从投影反向猜唯一字段名称，也不能只暴露投影中的字段。

## 建议的唯一职责

1. **输出类型声明**仍负责端口及成员身份、类型、集合、可空、数值存储、资源种类与坐标框架；展示名不进入类型或绑定身份。
2. **输出展示声明**使用完整正式端口及逐段成员路径，明确可读名称、顺序、分组和结果树可见性；在纯 Contracts/Runtime 内可读取，不依赖 WPF 或执行算法。未声明名称的输出不能因此消失，应在实施中明确诊断与回退政策。
3. **结果投影**选择表/属性视图的字段、列顺序、格式、有效性及几何映射；列标题可按视图单独覆盖，不能替代输出目录。
4. **发布快照**冻结本次运行的声明、值、展示元数据及资源租约。后续重命名、改参数或换节点不能把新声明混入旧轮结果；资源元信息是否可绑定需正式声明，不能凭界面凭空制造子字段。
5. **引用身份**始终保留 `ScopeId / NodeId / PortId / MemberPath / Selection / Type / Storage`。模块、路径显示名只用于展示，改名、同名或文本裁剪不改变引用。

## 需要同批贯通的消费者

- 正式输出目录读取同一展示声明，同时保留未列于结果表的所有可绑定输出；未知路径及冲突定义明确拒绝，不能取重名列的第一项。
- 参数来源树和文本来源树共用模块→端口→成员的构造逻辑；仅真实可绑定节点可选择，纯分组只展开。模块按作用域和节点身份区分，不能按标题分组。
- 索引来源、来源刷新、展开窗口和选择恢复支持任意深度；旧候选、已失效会话和刷新失败不能写入当前草稿。完整引用文本与树上的短标题分别提供，避免影响图像下拉。
- 结果树从同次快照读取分组和成员名称；数组按声明及实际结果展示，不能把显示字符串变成值或从轴对齐边框猜未声明几何。Blob 表仍按实际结果字段和本视图列定义读取。

## 验收

1. 不运行算法、无 WPF 时可查询正式输出声明；运行后结果树和表读取同次快照，旧快照不随新声明变动。
2. 跨作用域同 ID、同名模块、重命名、复制、保存重开、深层成员、集合及索引引用均按身份恢复。
3. 同一字段多列、缺少展示元数据、未知/重复路径及冲突标题分别测试；不遗漏输出、不静默挑第一项。
4. 图像输入下拉、普通来源树、文本树、索引树及结果树同时核名称和真实绑定；过期弹窗不能写新会话。
5. 大量输出按路径索引或树形结构构建，验证分页、关闭释放和重复打开不积累；名称变化不触发算法执行或改变资源所有权。

VM 多层结果截图已归档于原方案 `references/result-panel-20261029/`；具体控件样式及 VM 原输出组仍以逆向负责人的分批交付为准。这里不补造目前 Blob 尚未声明的最大内接矩形、长短轴、角度、灰度统计及图像元信息。

## 原来源树补证与实施顺序

原 `GetFrontFilterTreeOperation.cs:24–65` 从模块正式输出目录筛选候选；原 `SubTreeView.xaml:135–145` 用递归 `EnumEntrysA` 及本地化 `Name` 展示；`VmIO.cs:661–680` 组合模块编号、模块名与输出显示名。本方仍以稳定引用身份为准，不能把原显示字符串用作绑定身份。上述为反编译静态证据，尚未验证原件所有筛选例外。

本方 `Plugin.Contracts/Values.cs` 的端口现无完整成员显示声明，`OutputBindingChangeService.cs:172–175` 将输出名写成端口ID，`RuntimeNodeConfigurationControl.cs` 与 `RootWorkflowReferencePicker.cs` 分别拼接候选，两个引用及文本session各自构造平铺树。须集中完成：在无WPF的正式输出声明中按端口和成员路径提供展示元数据并校验；节点实例负责模块名；输出目录与publication冻结同一声明；来源及文本树共用递归结构并递归恢复选择；结果树读取同轮声明。表格列投影仍独立，不能反向作为完整输出目录。显示名及翻译不进入绑定身份或类型指纹。

依次覆盖无表投影的真实输出、三层以上成员、集合、跨作用域同名、重命名、旧候选失效、刷新失败、节点复制删除和撤销重做、保存重开，以及同轮结果树和值。此方案尚未实施；共享Contracts和工作流接口须在独立工作流分支集成前协调，不能覆盖两边已有修改。

## 现有生产链复核：实施时不能遗漏的断点

- 三个入口 `RuntimeNodeConfigurationControl.ReadReferenceCandidates`、`DeclaredToolConfigurationControl.ReadReferenceCandidates` 和 `RootWorkflowReferencePicker.Candidates` 已递归枚举 record 成员（穿过 sequence 后保留成员路径）；它们各自拼接 `member.Id`，输出一个平铺候选表。应复用已声明的类型和 `DeclaredReferenceCandidate` 的路径投影与存储类型规则，统一来源目录和显示树，不能误判为完全没有成员支持而重写绑定机制。候选树的显示根须由节点实例提供名称，并仍以 `(ScopeId, NodeId)` 分组。
- `ReferenceFieldSession.Build` 将候选平铺到节点下，`RestoreSelection` 仅查直接子项；但 `Contains` 已递归按对象身份检查有效节点。迁入层级树时必须同时改建树、递归恢复源与索引选择，并保留现有刷新代次和节点身份门禁；组节点不可因为有子项就自动丧失它自身的合法端口候选，叶与可选父项均由真实绑定资格决定。文本树和展开窗须使用同一结构规则，不能只改 Popup 外观。
- `DeclaredOutputSnapshot.Capture` 使用 `new ToolSchema(Array.Empty<Port>(), schema.Outputs)` 创建输出专用声明，默认 `Fields` 是顶层 `port.Id`；当前 `Fields` 仅能按顶层端口索引。无论最终选择把展示元数据放入 schema 还是独立声明，都必须在正常执行、失败/未执行、调试执行及声明视图路径显式传递并冻结相同输出展示声明，禁止依赖原 schema 的其他展示属性恰好存活。
- `ToolResultViews.FromSnapshot` 将顶层 `Fields` 转成 `IResultPublication.DisplayNames` 与 `DisplayedPorts`；`ResultPublication` 只接受属于顶层端口 ID 的键并复制字典。成员展示需要连同快照、publication、结果树消费者一起调整；只更新输出目录或字段名称不能完成同轮显示。`ResultPublication` 和 `DeclaredResultTable` 构造临时输出 schema 的用途是校验类型指纹，展示名称不得进入该指纹或改变绑定兼容判断。
- 以上为源码盘点和实施约束，并非新合同已实现；不同入口筛选规则、集合索引投影及 VM 可选父节点行为须结合原件和现行消费者确定后成批实施。当前试用期不构建，未进行运行或人工验收。
