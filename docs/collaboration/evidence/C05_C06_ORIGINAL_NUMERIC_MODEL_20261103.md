# B08 C05/C06 数值控件：模型、订阅和回读集中证据

本报告以 VM 4.4.0 原资源及本机反编译为范围；C05 单数值先于 C06 双端实施。原 VM/SDK 未启动；实施方报告的当前实现行为是待对照对象，不视为原版证据。复用 `B08双端订阅夹值回读时机补证-20261101.md`、`B08双端切回常量与读值静态链-20261030.md`。

## 主要结论：模型值与订阅值分离

1. `Integer.cs:8-54,121-130` 的参数模型字段是 `int`；`Value` 经当前区域性 `int.Parse`/`ToString` 转换到 `CurValueA`。赋值在 Min/Max 内时做整数 `(value-Min)/Inc*Inc+Min`，超范围时先原样存入；这与随后 `Check/RangeChecker` 的参数提交校验不同。不能把 setter 当成总是先夹到范围，也不能丢掉 Inc=0/整数溢出的独立分支风险。
2. `Float.cs:8-58,124-133` 模型字段是 **double**，`ComType="float"` 为订阅类型名，不证明托管模型为 single。`CurValueA` 在原值不同的分支用 `Math.Round(value,DecimalDigits)`；比较在舍入之前，原值不同但舍入后相同仍可能发属性通知。默认 DecimalDigits=2、Inc=1；`Value` 用 `double.Parse`/`ToString` 的当前区域性。Math.Round 此重载默认中点到偶数；具体文本绑定、区域性及原生参数转换须分别核对。
3. `ValueBase.cs:84-155` 的 `SubData` 是 `ButtonSelecter` 来源路径，候选操作按 `ComType` 过滤；取得现有 `ParamSubManager.GetModuleParamBinding` 是绑定**身份**，并非在该 getter 把上游执行数值赋给 `CurValueA`。因此引用端仍有独立的模型数值/隐藏控件属性；不能以 `null` 代替对端模型数值参加原控件比较，也不能反向断言该数值总是最新订阅执行值。

## 入口、更新及失败

| 事件 | 已核直接调用与条件 | 尚未证明 |
|---|---|---|
| 打开/更换参数根 | `ParamsConfigControlBase.OnParamsConfigChanged:211-265`：旧 root Dispose；新根 DoIniter → GetItemsValueUnit → 设 DataContext；`AlgorithmTabRoot:785-818 → Tab:300-320 → ValueBettween:53-74 → GetParamUnit` 遍历，受读权限与父节点条件约束。 | 读到的普通参数必是引用源当前执行值。`ModuleResultView.InitSpecialParams:1115-1152` 也在有专属 ISpecialParam 类型时回读，是初始化分支，非运行完成回调。 |
| 选来源 | `ValueBase.SubData` 的 `SetModuleParamBindOperation` 由来源选择触发，`GetModuleParamBindOperation` 恢复已有关系；当前已读此链没有直接把来源实时数值赋回模型。 | 其他动态通知或 native 后端是否在某次运行后做额外更新。 |
| 提交常量与按需运行 | `ValueBase.ValueChangedCmdExecute:266-278`：非断连且值变化才 `SetParamUnit`；返回 >=0 更新 OldValue 并 `PreExecuteRun`。`NameValueItem:1330-1388` 有 FilterPreRun、全局参数修改运行开关、动态分组、CanTriggerExecute 和正在运行门禁，最后清显示值缓存并 `vmModule.Run()`。 | 一次/私有运行完成后必定刷新参数模型。 |
| 单项/全量/页回读 | `ParamsConfigControlBase:274-287` 分别 `FindBaseNodeByName(key).GetParamUnit`、root `GetItemsValueUnit`、选中 Tab 的同方法；`VMControls.WPF.Release/VmParamsConfigControl.xaml.cs:62-74` 只转发。已对本地 `analysis/decompiled` 的同名直接调用做文本搜索，只找到 Release 包装；未定位 Blob 真实运行回调调用这三个接口的证据。 | 反射、未解码程序集/native 回调或别的通知路径；不应据局部未命中断言原程序不存在。可定向查 Blob Control 与宿主程序集对接口 token/CIL 的调用者。 |
| 改引用为常量 | `ValueBase.IsSubA:27-72`：仅模块存在、`_SubData` 已创建且来源非空时先清 `CurValueA`，再置 `IsGetFromModule=true`、本端 `GetParamUnit`，恢复标志；随后写 UserData 的 `@ComName`。 | 回读值是否为历史常量/最后执行值，以及异常时 `IsGetFromModule` 是否经其他路径恢复。 |
| 隐藏/再显示 | `ValueBase.CustomVisibleA:169-199` 在隐藏且 category/item 允许删除订阅、来源对象已创建且非空时清绑定并回读本端；重显 setter 本身未见重新绑定原来源。 | 其它触发器、容器或恢复机制；不能一概抹掉所有隐藏控件的订阅。 |
| 读写失败 | `NameValueItem.SetParamUnit:903-1020` 的校验失败会尝试 `Value=OldValue` 并返回 -2；写入异常返回 -1；`GetParamUnitByZmq:1044-1085` 普通参数读取后 `Check`，内层异常通知失败并尝试 `SetParamUnit()`，后面仍可能报完成成功并返回0。 | 这不是全链事务回滚证明；普通读取与订阅执行值的语义不能合并。 |

## 控件双端、镜像与输入事件

`Apps.XmlUI/styles/paramusercontroldatatemplate.xaml:540-598,约790-843` 单端/双端分别绑定 Start/End 到相应 `CurValueA`；引用开关到 `IsSubA`，订阅展示另绑定 `SubData`。`VM.PcUI/HikPcUI.ListView/SliderArrangeBase.cs:462-558` 的 Start/End getter 同时设置 E/F 镜像；between 模式四个镜像直接使用输入，非 between 则 E/F 某些端点替换为 Minimum、Maximum 或 Start/End。不要把 E/F 当另一份上游结果。

`SliderArrangeBase:878-909` 的整数 `SliderArrange` DP 改变时先 `Math.Round`，同步镜像；仅 Start 在 `IsDynamicValue` 分支额外立即触发 Start 事件，End callback 未显示对应调用。`SL_Bat1/2_ValueChanged:1032-1097` 通过 Dispatcher 排队，比较执行时的对端控件当前值并按端点范围处理；早先 `e.NewValue` 和执行时对端可能不是同一瞬间，所读代码无在关闭/重绑时取消该排队回调的证明。

`SliderArrangeBase:1140-1209` 的重复箭头在 Dispatcher 后按按钮 Tag 对应触发端点事件；T1/T2 失焦或 NumericClick 以当前 Start/End 比较，必要时向另一端夹并触发相应提交事件。`NumericControl.xaml.cs:330-410` 箭头按 Increment 及 XML/控件极值调整，文本框聚焦时滚轮也修改值，空格被拦截；弹层关闭 `SliderArrange.xaml.cs:25-31`、`SliderArrangeFloat.xaml.cs:115-121` 强制 T1/T2 更新文本绑定再触发两端事件。事件存在不等于每次都会写入：仍受连接、值是否变化、检查和 SetParamUnit 结果约束。

## 可实施边界与自审矩阵

建议 C05 将 `订阅路径`、`本地 CurValueA`、`普通参数回读`、`执行时来源值` 分为独立状态；参数回读时投影模型不能仅因当前端是引用就自动跳过，但仅在调用了有证据的回读入口时进行，不虚构每次运行后的自动回读。C06 延续 C05 独立模型：比较双方控件当前 Start/End（包括隐藏/引用端保留值），并按事件实际时序夹值；异步关闭后的防陈旧处理如采用代次或取消队列，应明确标记自研保护而非原机制。

| 自审场景 | 静态合同 | 不得冒称 |
|---|---|---|
| int 步长、负值、超范围；float 中点舍入、不同区域性 | setter/Check/字符串转换分层 | 仅控件视觉变化就代表 native 参数写成功 |
| 选择来源、初开、显式单项/页/全部回读 | 关系身份和模型值分别断言；模拟成功/失败 | 引用必然实时投影 |
| 左常量右引用、左右互换、初始未赋值 | 用隐藏对端控件当前值和 `isInitValue` 分支；分别覆盖 Slider 与模型校验 | 以前次取到的运行值就是原版此时实时值 |
| 文本失焦、左右箭头、拖动、弹层关闭 | 验证每端事件、写入条件及排队顺序 | 一次事件必触发一次运行 |
| 隐藏/重显、切常量、失败、关闭后迟到任务 | 条件解绑、本端回读、失败/代次保护分别记录 | 本方代次保护是 VM 原机制或原版所有关闭分支已证 |

当前**尚未穷尽**：Blob 专属 Control 及宿主依接口 token 的运行完成通知调用者、参数读写进入 SDK/native 后与订阅执行值的关系、关闭/重绑与排队任务竞态。具体取证条件为取得并静态解码相关程序集调用与可定位 native 方法；若这些可用入口最终都不可用，才将具体行为标为“当前取证条件下无法确认”，在参数回读/订阅投影适配器放置明确自研假设。

## 原件身份

安装目录 `Applications/myLibs`：`Apps.XmlParser.dll` SHA-256 `78EC7AE1E5ADEFFF29197099C7109B153A3F75D092B7C3E9DC253D8C1B25A8D1`，MVID `d0604401-ab61-48a4-938b-faad16d74e7c`；`Apps.XmlUI.dll` SHA-256 `639D0FB9DFCA81E38DF690E201B73874CE006D1361B689DDC6D5F41F7B0B04FD`，MVID `a00f8b40-7a51-4c6d-8aeb-cad134724f1f`；`VM.PcUI.dll` SHA-256 `87D21004FD3807213247623212F93C5F15A784DA78C4BC2C890A7906C3C92B42`，MVID `1393e856-5b85-4d5e-b730-304cf95a7766`；`VMControls.WPF.dll` SHA-256 `99FFA401C8784472913AD43813D68A8A5577890C00E149506E3E5664FA61C97D`，MVID `d144317f-3992-4275-9b0f-13fb6163e07d`。静态 PEReader 与 SHA 检查不证明运行时绑定了这些文件。Blob 参数 XML 和 XAML 文本哈希见前述既有报告。
