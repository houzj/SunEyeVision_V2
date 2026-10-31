# Runtime 设置三态整链设计交接（只读审计）

- 主线只读基线：`e9e9ca07934cfd05c145b82735164584ed84bafb`，目录 `D:/MyWork/SunEyeVision_V2`。
- 当前独立分支：`codex/d07-native-roi-host`；本次仅新增本文，不修改生产、共享合同或测试，不构建。
- 主线脏项仅 `tools/SunEyeVision.BlobAnalysis/Wpf/Views/BlobParameterEditor.xaml`，SHA256 `118128FB08EA3163B353620A91359B08B4D5CB1D36A58E37EAA9BF5C83DA67ED`；用户修改不暂存、不覆盖。读取期间基线不意味着干净构建产物。

## 结论与不可绕过的边界

Unset 的公共值语义已经存在，缺的是 Runtime 设置从编辑到持久化的一致映射。不能只给 WriteGroup 加一个分支：现强类型对象仍保留旧值，重绑或重开会重新读成 Constant；也不能把 null、空字符串、空集合、零当清除。建议集中建立 Runtime 设置字段的唯一权威 ParameterSetting 映射：Constant/Reference 是当前存在项，Unset 是事务中的删除操作，接纳映射缺键表示 Unset。类型化设置只作为插件业务消费的派生快照，不与映射形成两份可写、可持久的真值。运行输入绑定仍独立属于输入端口，不挪入设置字段。

这需要同批调整合同及消费者，不是本轮已批准实施；尤其不能先改 JSON 格式再遗漏资源路径、根参数、执行和设计数据消费者。

## 已有位置—行为—差异—决策

| 位置 | 当前行为与差异 | 最小集中调整 |
|---|---|---|
| Plugin.Contracts/Editing.cs：ParameterSetting、ParameterEditSession | 已有 UnsetSetting；可选字段 Clear，required 拒绝；接纳后删除键；有代次、prepare/ack、Cancel 与值冻结。常量备份是会话状态 | 复用三态和原门禁；备份不得变成持久真值，Unset 后默认不自动 RestoreConstant |
| Plugin.Contracts/Values.cs：InputValues 57–87 | Contains/Read 可区分缺失；Get 对缺键抛；构造拒 required 缺失 | 初值和备份读取必须先 Contains；不可用 Get 无条件补默认。编辑缺失与运行必填门禁要明确分层 |
| Plugin.Contracts/ToolSettingsFields.cs；Plugin.Runtime/ToolSettingsEditSession.cs | ApplySettings/ApplyFields 仅 object 值变化，无法表达删除或引用；Prepare 冻结 typed 对象并立即 Validate | 草稿统一接收完整三态原子提案并返回不可变捕获；对字段类型/模式/引用先验证，真正删除可选字段；拒绝和重入不安装半份 typed 值 |
| Plugin.Wpf/Presenters/Parameters/DeclaredSettingsView.cs：70、99–106、143–190 | 初始/backup 一律 Get；WriteGroup 只 Constant/Reference；TryCapture 只对象+References；_references 与 typed backup 组合恢复旧值 | 初始、条件、备份、组合候选和捕获同读权威映射；整组先验证再一次 accept；显式 Unset 保持为空，不从旧 typed 值复活 |
| Plugin.Wpf/Presenters/Parameters/IToolSettingsView.cs | 当前没有 ToolSettingsCapture 类型（本次 rg 无定义）；Options.AcceptCandidate 和 TryCapture 双参数拆分 | 若引入 ToolSettingsCapture，应作为同一不可变捕获合同：完整字段状态+派生 typed 快照，快照不单独可写/持久；AcceptCandidate/TryCapture/工具 wrapper 同批切换，不保旧重载 |
| UI/Services/Editing/Nodes/RuntimeNodeConfigurationControl.cs：BindDraft、TryCapture、ValidateSettingsCandidate | 只收 references；无 reference 就从 ParamSettings Remove，但 RuntimeConfiguration.WithSettings 保旧 typed 值 | 用一次完整候选替换私有 Runtime 字段映射；不再从缺 reference 推断 Constant。输入端口 ParamSettings 不受设置字段清除误伤；保 generation/context 双重核验和临时验证 finally 恢复 |
| Workflow/RuntimeToolConfiguration.cs：Create/WithSettings/WithFieldValues/Copy/Restore/converter | 当前唯一持久 payload 是实际 typed Settings；converter 严格只 ToolId+Settings；没有显式缺失映射 | 新建时只一次从 CreateSettings 导入默认常量；恢复不调用默认补齐。映射成为设置字段持久来源，typed 由定义执行物化，不另外存备用 typed payload；严格 JSON 类型、重复键、未知字段和引用反序列化 |
| Workflow/RuntimeInputBindings.cs；AlgorithmNode.Runtime.cs；RuntimeDebugInvocation.cs；WorkflowExecutionEngine.cs | 当前 SettingsSchema 常量不得重复存 ParamSettings；引用解析后 WithFieldValues 覆盖 typed 值；删 reference 后 typed 备份可回退 | 设置引用改从同一权威映射编译；Constant直接使用，Reference只用冻结编译结果，Unset不产生值。失败引用不能回落常量或默认；正式/调试/单模块/全局引用同改 |
| Workflow/NodeConfigurationEditHost.cs 441、541–602、726–760；BindingConfigurationStore；WorkflowFragmentCopy | 已有 Runtime.Copy、完整节点 fingerprint、一次 CommitBatch/历史和结构 rebase | 复用事务；新映射纳入 Copy/fingerprint/冻结，三态整组变化只增一次历史，Undo/Redo 恢复真实存在性；运行/导航不入编辑历史 |
| Workflow/WorkflowJsonConverter、WorkflowSerializationOptions、Persistence/SolutionResourceResolver、SolutionLegacyBindingMigration | 严格 Runtime 存在性、强模型序列化、图像源路径目前直接读 RuntimeConfiguration.Settings 属性 | 同批换到稳定字段映射；路径仅转换确实存在的本地路径 Constant；Reference/Unset 不能解释成路径或默认。没有旧格式兼容要求，不保旧 payload fallback |
| ImageSourcePolicy/根参数 Catalog/RootResourceProxyBinding/WorkflowBindingContext、图像源宿主与设计 draft | 均消费 ReadFieldValues/BindingTargets/字段稳定ID | 必须逐消费者核缺失；必需业务字段不改成optional来迁就清除；设计样例同真实草稿，不复制默认；根代理/变量编译不得漏 Runtime 设置引用 |

## 映射及类型化边界的最终设计要求

1. 一个字段 ID 只允许一个当前状态。已接纳映射仅 Constant 或 Reference；缺键=Unset。提案允许 UnsetSetting，序列化不落 `_unset` 标记，也不把缺失写成 null 常量。
2. Constant(null) 仅 Nullable=true 时合法；Constant(empty sequence) 与 Unset 不等价。Reference 必须正式编译并保 root port/member path/All 语义；未知 ID、资源常量、禁止模式立即拒绝。
3. 所有已持久业务设置须由声明覆盖，或先明确其非编辑字段的权威归属；不能把未声明 typed 属性藏成第二 payload。这是切换前清单门禁。现 BlobSettings 有 JsonRequired 非空数值和构造默认，不能用反射删 property，不能让可选 Assist 值落进这些必填数值属性。正式可选表示须由工具定义提供缺失物化/读取能力，不能由 Wpf 猜测。
4. 插件定义负责权威映射→实际业务类型的确定性物化和设置验证。可编辑草稿允许可选空值；required 字段不得 Unset。运行时再核条件必需、引用是否已解析和业务就绪；不要把所有编辑期 Validate 都延后，也不要把缺值自动补成默认。required 且绑定 Reference 的字段在未执行前是已配置，不能因为 typed 投影尚无解引用值而被误报缺失。
5. 配置冻结持有纯值/语义引用，不持图像 reader/lease/WPF 控件。派生 typed 快照不能写回清空字段；常量备份仅当前会话显式恢复动作可用，关闭/取消/重绑释放，不改变最终状态。

## Assist 关闭的完整事务

复用既有证据 `O06_ROI_CLOSURE_QUEUE_20261027.json`（AssistGroup243–980、十 float/CorrectInfo/两 Point），以及主线已核 Group.xml 关闭行为：一次组提案将 Assist=false，清十个 float、CorrectInfo、RunPoint，保 OriginPoint。旧证据明确 FixtureInitPoint 清除名字与实际 OriginPoint 不一致，不能把无效字符串查找移植成“清 OriginPoint”。本次没有重新逆向，具体字段稳定ID由后续原件组映射核齐，不能按UI标签推断。

完整组提案先验证所有目标确为可清的 optional 声明，再一次私有接纳；任何一项拒绝/失效/重入，全部保持旧态。Changed 只在安装后发布，隐藏并不自动清字段，只有原件明确关闭动作执行该原子清除。取消窗口不改正式节点；Apply 走原 Host 一次事务，随后 Undo/Redo/保存重开保相同存在性。Assist 再开启不能从 typed 默认或 backup 悄悄恢复已清来源；OriginPoint 必须保持原常量或引用及其身份。位姿算法/Blob 新业务不在本次设计实施授权内。

## 成片实施顺序与验收出口

先冻结当前 Runtime 全设置字段及读写消费者清单、JSON 单映射格式和插件缺失物化合同；然后同一批改 Contracts/Runtime 草稿/公共视图/工具 wrapper/UI 接纳/Workflow 编译与调试/持久化路径/根参数消费者。最后集中改设计样例和真实回归，整个批次接好才构建，不逐文件交付假闭包。原三态 ParameterEditSession、Host、历史与 BindingCompiler 行为应复用，不再造一套 history 或 compatibility adapter。

必须覆盖：

- 可选 Constant→Reference→Unset→重绑始终缺失；required Unset拒绝，null/empty/0分别保值，显式恢复才恢复常量。
- 整组十float+CorrectInfo+RunPoint清空且 OriginPoint不变；其中一项无效或回调重入则整组无变化。
- Apply前正式文档不变；Cancel/关闭/rebind拒旧回调；一次Apply一次历史；Undo/Redo、复制节点/流程、结构rebase保新节点与映射独立性。
- 保存A/另存B/重开严格相同三态，不调用创建默认复活；重复键、未知ID、错误模式、非法null拒绝；图像源相对路径及资源resolver完整回归。
- 正式/调试/连续/单模块从同一冻结设置运行；缺required或失效Reference明确失败，不回退默认/旧typed备份；取消和owner释放不改持久状态。
- 实际WPF公共组合、工具页独立设计同链；条件隐藏/再显示不丢未清字段；VS实际设计器和用户人工验收单列。

## 交付状态

本次完成只读消费者设计与最小整链决策；没有实施新合同、JSON格式、Assist业务或测试。未构建、未运行、未真实VS/人工验证。旧源码证据及历史失败全部保留；不能把本文当作 Unset 已完成或批准了格式切换。由root汇总主方案后集中实施并正式验收。
