# B08 订阅窗口偏好：持久化与宿主只读核查

本次只读既有反编译和 2026-10-30 保存方案的 ZIP 条目，没有运行原 SDK、载入方案或打开窗口。区别方案默认偏好、单个配置根的窗口状态、原生持久化与 Undo；不把接口名称或静态调用等同运行结果。

## 偏好读写和失败边界

`VmSolution` 的显式 `IVmSolution.IsSubscribeInWindow` getter 首次尝试从 `Instance.UserData["IsSubscribeInWindow"]` 获取字节并 `bool.TryParse`，正常空值/解析不成功得到 false 并缓存；读取抛异常被 catch，此时缓存仍可能为 null，返回 false 且下次可重试。setter **先更新 nullable 缓存和通知**，之后把值字符串转字节写 UserData，写异常被吞掉；因此界面当次显示新值不能证明 SDK 写成功。方案对象 ID 为 70000；`ResetInstance()` 将该缓存置 null，但不能单靠缓存重置推断下一次从磁盘恢复什么。

`VM.Core.UserData` 非动态路径读取 `GetUIParamValueByKey(Parent.ID,key)`，写 `SetUIParamValue`。对既非 GlobalModu、也非 DynamicModu 且非特殊 key 的分支：先读旧字节、写新字节；引用不相等且满足特殊检查时调用 `RecordSingleData`，再由可选的 `Ioc.Get<IUnRedoService>()?.Register(...)` 注册。此处是**可能进入撤销登记**的静态分支，不证明服务实际存在、操作真正入栈、UI Undo/Redo 成功或 Dirty 状态改变。实际运行时父对象标志、写失败、撤销聚合及服务实现均须另核。

## 窗口与宿主边界

`ParamsConfigControlBase` 和 `ComprehensiveConfigControlBase` 在加载参数配置时调用根 `DoIniter()`；仅 `Window.GetWindow(...) is BaseWindow || !CVmObjectBase.GetIsSDKMode()` 时，将方案偏好读入该 `AlgorithmTabRoot.IsSubscribeInWindow`，然后与该宿主 `IsOpenSubWindow` 双向绑定。SDK 模式及非 BaseWindow 条件下，不能推导必然按全局偏好打开。

两个宿主各自持有 `curSubWindow`，调用 `OpenSubWindow` 时检查**所属 AlgorithmTabRoot 实例**的 `HasSubscribeInWindow=false`；未 Loaded 时仅设置延迟打开标志。成功 Show 后置根标志 true。窗口 Closed 处理器清 DataContext、置该宿主 `IsOpenSubWindow=false`、清窗口引用；绑定在生效时再更新根状态，根 setter 将 HasSubscribeInWindow 清 false。所读关闭处理器没有直接设置 `IVmSolution.IsSubscribeInWindow` 或写 UserData；这不排除窗口内部、其他事件或宿主调用有间接影响。

`AlgorithmTabRoot.Dispose()` 只将该根 IsSubscribeInWindow 置 false；`ButtonSelecter` 的显式弹窗命令设置根的 SelectedSubscribeParam 和根开窗状态。`SubAlgorithmTabRoot` 将根状态委托给 Root。相同根实例的两宿主有共享 HasSubscribeInWindow 门槛，但不同根各自有状态；并发创建、各自生命周期、窗口关闭重开和是否全局互斥不能仅由此得出运行结论。

## 保存/另存与已有文件

静态源码可见 `VmSolution.SaveAs → instance.BaseSaveAs → base.SaveAs`，但本次未闭合 `SetUIParamValue(70000,key,bytes)` 在原生方案保存、另存及新进程加载中的序列化路径。故 true/false/缺省的保存后值、Undo/Redo 对实际窗口及 Dirty 的影响均**未证**。

只读 ZIP 扫描以下 attempt03 文件的全部解压条目字节，未命中 ASCII `IsSubscribeInWindow`：`fixture.sol`（8 条目，SHA256 `B91A7736F467757E15A55AF7836E22364AE153478E17C69AAEB69E0C30674D0B`）、`b08-bound.sol`（10 条目，`60CF6DDF06ED50AC44B503F9ED7901BDB3A693B139446CBD7FF14F6317610962`）和 `b08-after-unbind.sol`（10 条目，`45A0339239D8295CE445BCEDC77858EC4AA28141140B16FE9CF6B94DC24D8DB1`）。这些样本没有记录偏好设置且可能使用其他编码、结构或原生二进制格式；**未命中字节不证明未保存、不证明新实例读 false**。

## 最小后续验证（未执行）

在确认自有服务与历史清理边界、另获独占窗口授权后：对缺省/显式 false/true，记录 ID 70000 的 SDK 原字节、属性缓存、Dirty 与 Undo 栈；分别保存、另存、关闭并全新实例重开同一文件，重读原字节和属性。分别打开两个同根宿主与两个不同根宿主，记录真实窗口数、Loaded 顺序、关闭后根状态和方案偏好；覆盖 SDK 模式、非 SDK 模式及 BaseWindow 条件。保存输入/输出和日志哈希，失败与缺席值分开。此测试与当前环境并发用户进程、共享清理未隔离的阻塞相独立，不得自动运行。

## 源码身份

反编译文本 SHA256：`analysis/decompiled/VM.Core/VM.Core/UserData.cs` `8B5099C298B336A6576F9C6FF0C02090EC48C5E4E06B3127D8129DC4C1A79394`；`VmSolution.cs` `C141E2C53913B233B768782D6757FAE5997ECAF638B35DB2DDF98F5EF951A13E`；`analysis/decompiled/VMControls.WPF/VMControls.WPF/ParamsConfigControlBase.xaml.cs` `6E625D0A505D37E17E59D6A357353AAFAFFB3D9A4BF4E4F8F87EE31087436F58`；`ComprehensiveConfigControlBase.xaml.cs` `204827AEEA0E08151ED80235A2670301D32F0C8BE0FFE09686E02D39B44A2504`；`analysis/decompiled/Apps.XmlParser.CapabilityAudit/Apps.XmlParser.ParamTab/AlgorithmTabRoot.cs` `31C8AAFDBCCD0E034316A54E2959F451E135298FAD9F618E8234BC6CBC1BC72E`；`ButtonSelecter.cs` `A04C04DC557328269A06D67C194F6C3A64B68659ACC1F76E4DE778F5DA858931`。
