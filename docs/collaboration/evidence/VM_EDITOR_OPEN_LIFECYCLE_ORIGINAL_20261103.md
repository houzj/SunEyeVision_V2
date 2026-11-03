# VM 4.4 普通节点 / Blob 参数窗口：原版静态调用链

范围：既有 VM 4.4 原程序集、反编译和 XAML；不启动 VM/SDK，不以耗时、截图或自研窗口推断原实现。源码路径相对 `analysis/decompiled`。`ProcedureConfigControl` 普通参数窗与独立 `ParamsConfigWithResultViewControl` 是不同宿主，不能混为同一窗口。

## 双击/命令到窗口

1. `VMControls.WPF/base/diagramitemtemplate.xaml:1798-1799` 通过附加路由事件把节点 `MouseDoubleClick` 接到 `DiagramArea.ConfigParamsCommand`；同模板 `:1381,1659` 有配置按钮绑定。`VMControls.WPF.Base/DiagramArea.xaml.cs:346-370` 禁止项不打开，加载失败显示警告，否则将装饰器作为 `ItemDoubleClick` 参数。`ProcedureConfigControl.xaml:538,552` 接 `DiagramArea_ItemDoubleClick`；其代码 `:613-657` 将事件或自己的配置命令转为 `OpenParams(data)`。
2. `ProcedureConfigControl.xaml.cs:649-705` 先 `CanOpenConfigurationInterface`：读取登录角色、`ConfigAvailable`、`ConfigCheck`；准入通过且 `curTabControlBase!=null` 才先 `Close()`，再 `OpenParamsTabControl`。此路径没有按同节点 ID 返回当前窗口的分支：即同节点重复打开在此方法中也走关闭后重新创建。关闭被阻止/异常及其他入口不应在未核证时泛化。
3. `OpenParamsTabControl:708-780` 在仍有 `curTabControlBase` 时返回；检查模块目录及 `模块.xml`、`AlgorithmTab.xml`、`Display.xml`，为非全局模块登记 ModifyConfig 撤销根；处理结果视图 Tag 的旧 TabWin；调用 `GetExternTabWin(vmModule) ?? new ParamsTabControlBase()`；依次设置 `ROIPartUI`、编辑状态、`ParamsConfig=vmModule.Params`、Owner/AlongParent，`EnableModelessKeyboardInterop`，然后 `Show()`；之后标记 IsOpenParams、保存当前窗、注册 Closed、设置结果视图 Tag/ROI 和选中节点。异常记录日志。`Show` 不等于已经呈现首帧，静态源不能给出耗时。
4. `GetExternTabWin:804-829` 先在当前 AppDomain 按 `ModuleTypeName+Control` 找程序集，未加载时在参数 XML 所在目录找同名 Control.dll 并加载；从程序集类型中选首个 `TabWinBase` 子类创建实例，否则使用普通 `ParamsTabControlBase`。**程序集可复用不等于窗口实例复用**。本机 Blob 安装目录有 `IMVSBlobFindModu.dll`、`IMVSBlobFindModuCs.dll`、XML，未见 `IMVSBlobFindModuControl.dll`；已加载同名程序集仍是另一条件，本次没有运行时绑定证据。

## 参数根、视图与冷/热边界

`ParamsTabControlBase.xaml:40` 持有 `ParamsConfigControlBase`；`ParamsTabControlBase.xaml.cs:22-31` 的 ParamsConfig setter 直接转给该控件。`ParamsConfigControlBase.xaml.cs:206-265` 在 ParamsConfig 属性改变时，旧 DataContext 是参数根则 Dispose；新值先尝试 `LoadModuleControlDLL`，XML 不存在则返回；若为 `ModuleParam` 调 `param.GetParamRoot()`，否则创建 `AlgorithmTabRoot`；随后 `DoIniter()`、`GetItemsValueUnit()`、赋 DataContext 并绑定订阅窗状态。模板 `ParamsConfigControlBase.xaml:5` 将 `ParamsTabUserControl` 与 DataContext 展示相连。根的 XML 加载不是由截图推断：`AlgorithmTabRoot.cs:645-667` 构造时设置 UserStringData/Owner，符合条件调 owner `InitLoad`，有路径时 `loadXml(ignoreUnknown:true)` 后 `DynamicSetProperty`；异常记日志。

`VmModuleExtern.GetParamRoot`（`Apps.XmlParser.ParamTab/VmModuleExtern.cs:28-35`）仅在 `ModuleParam.Data==null` 时创建根并保存，否则返回缓存对象。`AlgorithmTabRoot.Dispose:1558-1561` 所读方法仅设置 `IsSubscribeInWindow=false`，没有清 `ModuleParam.Data`；`DoFinish:854-865` 遍历 tab 完成处理。故所核路径能证明**以 ModuleParam.Data 为宿主的根缓存**，不能证明所有内部资源持续可用、所有重开都不重新初始化，也不能将新 `ParamsTabControlBase`、WPF 模板或布局说成缓存。`DoIniter` 和 `GetItemsValueUnit` 在 ParamsConfig 变更路径中依然调用；具体项初始化是否重复、各视图实现的加载/虚拟化和首帧时点须另证。

普通参数读取以已有 `B08-C05运行回读调用者与单值合同补证-20261103.md` 为准：逐项可达 `GetParamUnit → IUserStringData → ModuleParam.GetParamValue → IMVS_GetParamValue`。是否一次读取、分页、并发/同步耗时及 native 内部缓存，不能由链路数目推定。

## 当前实现的窗口组装与本轮修正

2026-11-03 已核到主仓 Runtime Blob 打开路径：`NodeOpenDecisionChain.CreateDebugControl` 创建轻量空 `RuntimeNodeConfigurationControl`，`MainWindowRuntimeEditing.InitializeRuntimeEditor` 建立正式 `ProductionEditorContext`、先挂接 draft context，再只调用一次 `control.BindDraft(context.DraftNode)` 组装完整声明页，随后绑定结果预览并 `Show()`。原构造函数曾先以正式节点副本 `BindDraft(node.Clone())`，正式会话随后又绑定一次；现已删除临时参数页组装，避免双份控件和订阅创建。单项正式 Release 回归 `O06_WINDOW_CASE_FILTER=正式Runtime三页` 已自然通过，核对三页、声明展示字段全集及基础/组合输入；旧的55/4计数已更新为基于正式声明的集合核对。

本轮在 `MainWindowViewModel.ExecuteOpenDebugWindow` 与 `MainWindowRuntimeEditing.InitializeRuntimeEditor` 增加分段耗时日志：`[Perf] 双击节点窗 NodeOpenDecisionChain`、`查询服务与宿主准备`、`BindDraft完整参数页组装`、`结果预览绑定`、`窗口Show同步耗时`、`双击节点窗至Show完成`。它们能指出当前慢在命令前置、完整UI组装、结果预览还是窗口Show；尚未取得用户机器上的实际数值，也未与原厂同机动态测时对比。因此本次只能确认重复组装已去除，不能声称慢速已彻底解决或已经达到VM速度。`window.Show()`计时只是同步调用，不代表首帧呈现；人工比较应分别记录点击到首帧和关闭重开。

## 切换、关闭与结果视图

`ProcedureConfigControl.CurTabControlBase_Closed:830-846` 在 Dispatcher 中清当前窗口、结果视图 Tag、IsOpenParams，更新 ROI 可见性，之后移除空撤销操作。`ParamsConfigControlBase` Loaded 时挂弱 Closed 监听；`:400-422` 窗关闭时按条件停止连续自运行、注销列举的相机/灯光/通信函数并对根 `Dispose`、`DoFinish`。这不能推断各设备、native 句柄和所有迟到回调都已释放。

主界面已有 `MainViewControl.xaml:33` 的 `ModuleResultView`，其 `ModuleSource` 绑定选择项；`ModuleResultView.ModuleSourceChanged:416-464` 第一次通过 Dispatcher 延后并校验新值仍等于当前来源，之后同步处理；旧 decorator Dispose、清缓存，新来源创建并 Init `ModuleDisplayDecorator`。这是**结果视图的重绑定**，不是参数窗口本身的复用。独立 `ParamsConfigWithResultViewControl.xaml.cs:149-177` 则自身含结果视图并在源变更清旧根；按实际宿主使用，不混用。

## 实施建议与最小验收

- 依据已证路径，在新窗重活之前完成权限/资源准入和旧窗关闭；当前重复 Bind 已移除，后续依据上述分段日志定位仍慢的真实阶段，再按VM证据检查对应完整路径；**不是 VM 恰好只发生一次绑定或原版性能原因的证明**。
- 明确区分可缓存的模块参数根/解析元数据、每窗 WPF 实例、结果视图 decorator 与真实模块 session 的所有权；复用须以节点/方案身份和生命周期安全为前提，避免把旧节点 UI 或订阅带到新节点。后台加载、首帧骨架、单飞/代次、额外释放检查均是自研优化，不能标为原机制。
- 本方真实可交互桌面分段计时：入口→准入/关闭→窗口构造→根获取/解析→DoIniter→普通参数读回次数与耗时→模板首次构建/Loaded→首帧；各分冷进程、同节点重复、不同节点切换、关闭重开，并统计工厂次数、DataContext/模型绑定次数、读写次数、UI 响应。对拒绝关闭、加载失败、旧回调和方案切换保留测试；原机差分不在本轮已执行范围。

**尚未闭合**：Window.Close 是否可取消及对应重入、构造到第一帧 WPF 具体模板实例化时序、`GetParamRoot` 缓存失效的全部入口、Blob 实际加载的 Control/程序集、同机各段耗时和原厂后台线程栈。下一步可定向查 `TabWinBase`/BaseWindow Closing、参数页 `ParamsTabUserControl`、GetParamRoot 清空 Data 的调用者及静态程序集装配；所列可追入口未完成前不称静态方法穷尽。

## 身份

`Applications/myLibs/VMControls.WPF.dll` SHA-256 `99FFA401C8784472913AD43813D68A8A5577890C00E149506E3E5664FA61C97D`，MVID `d144317f-3992-4275-9b0f-13fb6163e07d`；`Apps.XmlParser.dll` SHA-256 `78EC7AE1E5ADEFFF29197099C7109B153A3F75D092B7C3E9DC253D8C1B25A8D1`，MVID `d0604401-ab61-48a4-938b-faad16d74e7c`；`Apps.XmlUI.dll` SHA-256 `639D0FB9DFCA81E38DF690E201B73874CE006D1361B689DDC6D5F41F7B0B04FD`，MVID `a00f8b40-7a51-4c6d-8aeb-cad134724f1f`。反编译文本：`ProcedureConfigControl.xaml.cs` SHA-256 `36BE1620196C60A72EFB03DAB8AFB4FA302BA1BA8EC4ACF7EB1F5F61883678BD`；`VmModuleExtern.cs` `4D4B726DF4EAC492C955502CB90118E793679EE74718B900E62625CFE2DE2C9C`；`ParamsConfigControlBase.xaml.cs` `6E625D0A505D37E17E59D6A357353AAFAFFB3D9A4BF4E4F8F87EE31087436F58`。身份不证明运行时实际装载。
