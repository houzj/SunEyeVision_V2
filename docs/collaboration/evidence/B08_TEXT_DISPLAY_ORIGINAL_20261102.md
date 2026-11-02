# Blob 文本显示：配置到取值的静态证据

取证范围：本机 VisionMaster 4.4.0 安装资源及 `analysis/decompiled/Apps.XmlParser.CapabilityAudit`；仅静态分析，未在原机执行，也未检查全部原生行为。文件为证据记录，不代表插件已采用或与 VM 完全等价。

## 资源与候选来源

- 安装目录 `Applications/Module(sp)/Group.xml:2767-2894` 的 `TextDisplayGroup` 将文本配置关联到 `Show Text` 的 `Content`，X/Y 使用 `GetFrontAndSelfFilterTreeOperation(int;float)`；分别写入 `DisplayoutputPositionX;float`、`DisplayoutputPositionY;float`。
- `Applications/Module(sp)/x64/Location/IMVSBlobFindModu/IMVSBlobFindModuDisplay.xml:87-96` 中的文本默认值包括 `Content="Blob Area:{0}"`、`Mapping="Area"`、X/Y 的 `Mapping="NULL"` 和 `Value="0"`，显示状态关联 `BlobStatus`。这不能推出默认位置是 Blob 质心。
- `Apps.XmlParser.ParamTab.GetFrontAndSelfFilterTreeOperation.Operate` 调用 `VmModuleExtern.GetFrontFilterTree`，包含上游及自身、包含模块集合内部、排除空项和全局变量；后者将匹配节点交给 `AddOutputTreeNode`，并对自身候选去除 `ModuRunTime`。候选资格仍须遵守下层类型和作用域过滤，不能简单认为所有节点都可选。

## 选择、索引和取值

- `Apps.XmlParser.ParamTab.SetResultIOToModuleOperation` 使用隐藏的动态输出保存位置绑定，解析来源与索引，设置 `RelateIO`、`RelateIndex`，并设置显示 feature 的 Mapping；空选择走 `NULL`。旧动态 IO 在部分来源解析之前被移除，解析失败可能提前返回，因此不能承诺失败时原绑定总是原子保留。
- `Apps.XmlParser.FeatureBase.GetValue` 与 `Apps.XmlParser.Object.FillValueList` 负责映射及按结果索引生成显示数据；直接取值和回调缓存是不同路径。可见代码中，执行计数检查、数组短于索引时回退索引 0、空值的类型默认值均有条件，不能概括成所有失败都显示 0 或每次都读取最新结果。
- `Apps.XmlParser.Feature` 的文本解析/本地化路径处理 `{ID}`、`{ModuleDisplayName}` 及映射占位；`FeatureBase.GetValue` 对 `{i}` 进行对应值替换。不可因此断言任意用户文本都会翻译，也不可推断它支持完整 .NET 格式字符串语法。

## 持久化与复制边界

- `FeatureBase.SaveValue`/`SaveMapping` 写入以参数根、显示对象和 feature 命名的用户数据；`ParamRoot`、`ObjectBase.UpdateResultShowParam` 有重载路径。`Feature.PasteFeature` 对符合条件的 Value 字符串进行模块身份替换；尚不能由此推断所有动态 IO、绑定和索引均在复制、保存、重开后完整恢复。
- 通用 `ModuleSetShapesConfig` 的确认/取消代码不足以证明它就是此文本属性的实际事务边界；确认、取消后的回滚及原生持久化效果尚未在对应调用链闭合。

## 可供设计评审的自研假设占位

下列条目是建议的隔离边界，**不是 VM 已确认行为，也不是本次实施结果**：

| 未确认点 | 临时假设 | 影响和替换位置 | 恢复追证的条件 |
|---|---|---|---|
| 来源解析失败后 UI、旧订阅和动态输出最终状态 | 配置变更先在独立事务中校验，失败保持旧配置并提示 | 显示位置绑定适配器的提交/回滚边界；可能与原版失败行为不同 | 找到具体失败分支后继、原始回调/订阅源码，或可控原版测试环境 |
| 文本对话框取消时的实际回滚合同 | 编辑副本，仅确认时提交 | 文本编辑会话的确认/取消方法；不改变已核实的 Value/Mapping 格式 | 追到实际编辑入口、命令以及其调用的状态写入/回滚方法 |
| 复制、保存重开后动态 IO 的完整恢复 | 显式保存来源身份和索引；无法恢复时标记失效，不猜测新来源 | 显示绑定序列化与重连适配器；可能不兼容原版持久化细节 | 找到相应序列化、重连源码/机器码，或可用隔离环境 |

上述三点本次尚有明确可追的调用点，因此**不声称静态手段已穷尽**。评审可使用占位，但继续取证优先沿表中具体入口推进；只有经记录的针对性搜索与可用工具均无结果，才把对应条目标为“当前取证条件下无法确认”。

关键证据路径：`analysis/decompiled/Apps.XmlParser.CapabilityAudit/Apps.XmlParser.ParamTab/{GetFrontAndSelfFilterTreeOperation,VmModuleExtern,SetResultIOToModuleOperation}.cs`；`analysis/decompiled/Apps.XmlParser.CapabilityAudit/Apps.XmlParser/{Feature,FeatureBase,Object}.cs`。

关键文件 SHA-256：`Group.xml` = `9129B82A0F9906DDE591347498A80E2E8C019C0FB959A721ECFD24FBBB5207DC`；`SetResultIOToModuleOperation.cs` = `128C8E2A13CCA393A27F3DF1E4AAA7001A78649E787F311E895C7C33A5BDD027`；`FeatureBase.cs` = `672D0B57519FA4E750E38E627910B9F11F1B28DA86E9E1980C607004954E5EAD`。反编译文本哈希用于定位本次分析输入，不能替代原程序集身份。

## 后续定点追踪：复制、重载、取消与失败

1. **复制**：`ParamRoot.PasteNode` 接收映射字典，对所有显示对象的 `FeaturesB` 调 `Feature.PasteFeature`。该方法只遍历当前 feature 的 `vmIOs`，当 IO 的 Root 在字典中且新值为模块时，将 `ValueA` 里的旧 `ID 名称` 字符串替换成新 `ID 名称`；发生变化才重新赋 `ValueA`。父方法捕获并记录异常。此方法体没有直接改写 `Mapping`、动态 IO 的 `RelateIO/RelateIndex`；不能据此断言其他复制流程未改写它们。
2. **初始化/重载**：`ParamRoot.InitNode` 的顺序是载入模块 Display.xml → 设置 owner → `InitDynamicObject` → `ReloadProperty` → `InitVmIOs`。`ReloadProperty` 用模块 ID 读取 `GetUIParamValueByObject`，按参数名包含对象名分组；`ObjectBase.UpdateResultShowParam` 按精确 key 恢复 feature 的 `.Value` 与 `.Mapping`，随后调用 `Initializer`。它们证明展示参数加载路径，不证明方案文件序列化或动态 IO 源端恢复已完整闭合；异常被记录或捕获的分支也不能算恢复成功。
3. **文本编辑入口**：`Group.xml` 的 `StringConfig` 通过 `GetDisplayObjectFeatureOperation` 取得 `Show Text.Content` feature；`BridgeItemBase.BindingPathA` 形成 `BridgeObjA.ValueA`，`analysis/decompiled/Apps.XmlUI/styles/paramusercontroldatatemplate.xaml:2557` 的文本框经附加绑定使用此路径。`StringConfig.IndexClickCommandExecute` 将选择的 IO 路径插入已定位的花括号片段，失焦关闭候选弹层。该证据表明此处为内嵌文本编辑及候选弹层，尚不能由其他窗口的取消按钮推断它存在独立事务回滚；文本绑定更新时机仍需检查附加绑定实现。
4. **位置来源解析失败**：`SetResultIOToModuleOperation.Operate` 先将旧 IO 从 `Outputs` 移除，非空输入将 IO 加回，再调用 `GetOutputInfo`。其返回 false 时若参数为 `ButtonSelecter`，执行 `CurValueA = OldCurValue` 后立即返回，未到本次 `RelateIO/RelateIndex` 赋值和 feature `SetMapping`/`InitVmIOs` 代码。`ButtonSelecter.CurValueA` setter 自身更新 `OldCurValue`、通知变化；回退是否重入其他绑定处理、以及订阅最终状态，还需要追 `OnPropertyChanged` 的监听链，不能直接断言最终完整回滚或损坏。
5. **图形配置窗口的有限结论**：`VMControls.WPF.ModuleResultView.ShapesConfig_Click` 创建 `ModuleSetShapesConfig`，将命令绑定到 `ShapesConfigCommand` 后 `ShowDialog`；其取消只设置 `DialogResult=false`，确认执行 `ConfigCommand(ItemsSource)` 后返回 true。该入口是结果视图图形配置窗口，尚无证据说明它控制上面 `StringConfig` 内容编辑；不能把该取消行为套到文本 feature 上。

这些是反编译源码的静态结论，尚未执行原版验证；占位仍是独立设计而非 VM 合同。下一步定点入口：`AttachBinding.BindingString` 的绑定创建与更新触发、`ButtonSelecter.OnPropertyChanged`/setter 监听及 `DynamicVmIO` 的解绑和序列化调用者。

## 文本长度与默认值的证据边界

`Group.xml` 的 `TextDisplayGroup` 声明 `StringConfig Content` 长度 `0x20`（Group.xml SHA-256 见上）；已检查的 `StringConfig.cs` 中 `Length/LengthA` 仅见字段及存取，尚未发现它约束 `TextBox.MaxLength`、字节数或 feature 值的调用。不能把另一类普通字符串参数的长度政策移植到此控件。原 `IMVSBlobFindModuDisplay.xml` 的 Show Text 默认内容为 `Blob Area:{0}`、映射 `Area`，位置默认 X/Y 为 0；同文件其他显示对象的较长默认内容也不能单独证明手工输入完全不限长。

因此候选不据此自行截断；手工输入限制、按字节或字符计数、候选来源插入是否受限仍未证。候选显示场景的累计字符资源预算是本方适配，不能称作原厂长度合同。后续有明确模板消费者或原机差分时再定点核实。