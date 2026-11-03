# C07/B08 来源显示、索引编辑与刷新：定点静态证据

范围：VM 4.4 原反编译文本；未启动 VM/SDK，不修改另一项目。以下行号均指 `analysis/decompiled`。结论仅覆盖所列入口，不将显示文本等同原生绑定结构。

## 1. 数值来源与索引

- `Apps.XmlParser.CapabilityAudit/Apps.XmlParser.ParamTab/ValueBase.cs:92–137`：`SubData` 首次创建 `ButtonSelecter` 时直接 `ReadOnlyA=true`，并装配 `GetFrontFilterTreeOperation(ComType)`、`CurValue` 变更的 `SetModuleParamBindOperation(ComName)`、初始化 `GetModuleParamBindOperation` 和一次绑定回读；这是普通数值来源路径，未见按单值/序列长度改变该初始只读值。
- `Apps.XmlUI/styles/paramusercontroldatatemplate.xaml:255–301,550–551,802–803`：`Integer_Sub`、`Float_sub` 使用 `ButtonSelecter_sub`；其中 `DeleteTextBox.IsReadOnly` 绑定 `ReadOnlyA`，焦点事件接 `FocusedCommand`。`ButtonSelecter.cs:517–577` 的焦点处理在 `ReadOnly` 为真时直接返回；否则还须光标位于一对方括号内才调用索引来源准备操作并打开索引选择。这个证据说明上述 `ValueBase` 初始化的控件默认只读、焦点索引选择受阻；**不证明所有不同模板/操作都只读，也不推断单值索引不可消费**。`GetSelectedCombinationOperation:68–79` 和 `SetCombinationSourceOperation:30–33` 改变只读状态属于组合 IO 路径，不应套在普通参数绑定上。
- `ButtonSelecter.cs:347–363`：选 `VmIO` 时，若旧 `CurValue` 含 `[`，把第一个 `[` 起整个后缀原样接到新 `ModulePathName`；否则追加 `[]`。所读分支不检查源的值数量。因此从 `旧源[1]` 切到单值源时，该入口形成 `新源[1]`，不自动改为 `[0]`；这只证字符串路径及后续提交，不证明后端消费成功。
- `ButtonSelecter.CurValueA:90–122` 更新旧/新字符串及通知；`SetModuleParamBindOperation.cs:30–119` 解析源和索引：数字索引提交 `SUBSCRIBE_INDEX_CONSTANT` 与数字字符串，空/非数字非路径进入 ALL，包含点的索引解析后提交 VARIABLE；源无法解析提前返回，提交失败提示并无触发清空。`VmParamSubManager.cs:76–141` 按返回的模块 ID、IO 名称和 index 类型重建路径及 `[]`/`[n]`/`[索引源]`。既有样本只证单值 `[0]` 消费、`[99]` 的指定失败，单值 `[1]` 的 SDK 消费结果未知，不能凭 UI 拼接断言成功。

## 2. 图像来源是组合父节点

`GetFrontParamItemsOperation.cs:23–52` 将 `VmModule.GetFrontIOWithLeafItemsByName("IMAGE")` 的结果赋予候选列表；尽管方法名含 Leaf，`VM.Core/VmModule.cs:3702–3721,4049–4075` 实际递归只遍历 `!IsLeafNode`，收集 `TypeName=="IMAGE"` 的**组合节点**，再按 `CanSubscribeA && Visible` 筛选，投影成 `ModulePathName` 字符串。前驱、特定全局及局部变量模块各按该方法添加，不可替换为所有图像叶节点。

`VM.Core/VmIO.cs:452,661–675`：普通有 Root 的路径为 `Root.ID + 空格 + Root.UniqueName + '.' + 本地化(该 IO 的 UniqueName)`；该类 `UniqueName => Name`。因此组合的候选与所选显示取 IMAGE 父节点名，不把 `Width`/`InImage` 等叶名拼上；普通数值引用如果选择的是图像宽度等叶，`ButtonSelecter` 使用**该叶自身**的 `ModulePathName`，不能从图像父路径自行推断叶名或额外拼接层级。

`paramusercontroldatatemplate.xaml:846–850` 的 `EnumerationG` ComboBox 绑定 `EnumEntrysA` 和 `CurValueA`；`EnumerationG.cs:11–32` 设置值时移除第一个非首位 `[` 起的后缀；`:35–48` 更换列表对象时直接清内部 `CurValue`，再通知列表变化。`GetSelectedCombinationOperation.cs:55–81` 取目标 IO 的 `SubscriptionInfo`，为空则 `GetSubscriptionInfo()`，写入 `CurValueA`；`SetCombinationSourceOperation.cs:53–85` 按路径解析来源，将其赋给目标 `RelateIO` 和 `RelateIndex`。候选项是字符串，最终关系是解析得到的 IO，二者不可混成同一种持久化身份。

## 3. 改名与编辑中的刷新

`VmIO.cs:97–108` 的 `Name` setter 修改名称并调用 `Root.InvokeOnModuleIONameChanged(oldName,name)`；`IMVSGroup.cs:426–430` 在 Group 覆写中发相应事件。`ModulePathName` 是基于当前 Root 和 Name 计算的 getter，但 `ButtonSelecter` 持有自己的字符串，不因此自动证明正在编辑的文本实时改写。

已定位重要的另一条具体路径：`ValueBase.cs:140–157` 在**后续读取现存 SubData getter** 时，若 `IsSubscribeStr` 成立，先去掉方括号后缀，再以现有文本找来源；解析不到时设置 `CurValueA=null`。该 setter 的变更通知与装配的 `CurValue` trigger 存在后续解绑调用链，具体是否触发及改名通知先后取决于入口和条件，不能宣称每次改名都会清除或保留绑定。与此相对，显式 `GetModuleParamBinding` 可按已保存的模块 ID/IO 名称解析对象并重新生成当前路径；IO 名称是否随原生绑定同步另需证据。

对整个现有反编译树按 `OnModuleIONameChanged` 文本检索，看到事件定义/触发但未定位对 `ButtonSelecter` 直接订阅；反射、未解码程序集或其他通知通道不在该检索覆盖内。**编辑中改名/刷新是否保留未提交后缀、何时刷新原生身份仍未闭合；尚有具体事件订阅和绑定同步链可追，不能标为取证穷尽。** 迁移方如需并发编辑保护，应显式标为自研设计。

## 4. 可复用实现核对点

1. 分开来源候选、编辑字符串、绑定身份、回读路径；普通数值控件初始只读条件按 `ValueBase` 适配，不能按源数量猜测。
2. 按原入口保留切源后的索引后缀；提交常量/变量/ALL 三类分别覆盖成功、解析失败与提交失败，不把 `[1]` 对单值视为已验证。
3. IMAGE 列表从可订阅、可见的组合节点产生，名称从该节点的 `ModulePathName` 生成；数值树和图像组合候选分开。
4. 覆盖列表对象更换、显式绑定回读、来源不再按旧文本可解析及编辑中改名；后者仅验证自研策略，不宣称原版等价。

原程序集身份及既有单值实验链见 [索引证据](B08数值引用索引语义与补证方案-20261031.md) 和 [数值回读](B08-C05-C06数值模型订阅回读集中证据-20261103.md)。
