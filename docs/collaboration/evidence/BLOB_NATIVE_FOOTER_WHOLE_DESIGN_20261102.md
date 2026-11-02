# Blob 编辑窗公共底栏与宿主命令整体设计

状态：候选工作区已按本设计实施，候选构建与部分自测通过，结果显示整轨仍在验证；尚未合入主仓或完成正式验收。主仓产品基线 `ce341300e`，后续验证与文档提交截至 `4431cfa02`。本批仅 Blob Runtime 编辑窗，工作流及图像源独立线不等待、不改其窗口；保留其他工具既有入口，后续按同一公共库逐工具迁移，不构造Blob新旧双底栏。

## 原件链与差异

VM 4.4 原 Blob XML `IMVSBlobFindModuAlgorithmTab.xml:2723–2731` 左侧 `Button_ResetAlgoParams`、右侧 `BottomCommandGroup`；原 `Group.xml:2938–2981` 顺序为连续、停止、单次执行、确定。`ParamsTabUserControl.xaml:95–140` 使用左右两组 ItemsControl，重置仅运行参数页显示；原 `paramusercontroldatatemplate.xaml:159–230` 随连续状态、根流程运行状态和运行许可控制可见/可用。原XAML/模板SHA及原组合见 `O06_TEXT_LIST_FOOTER_STATIC_20261028.json`，`RESULT_AND_FOOTER_BASELINE_FORMAL_20261031.json` 仅证明公共底栏专项，不证明Blob已消费。

现生产入口 `NodeOpenDecisionChain.DefaultOpenWindow` 对 Runtime 使用 SDK `DefaultDebugWindow`，`MainWindowRuntimeEditing.InitializeRuntimeEditor` 硬绑定该窗口及ContentHost；原公共 `NativeToolEditorFrame`、`INativeToolCommands` 和模板已存在但Blob未使用。现SDK窗的500ms连续循环不是原厂行为，不能搬入新控件。`RuntimeNodeConfigurationControl` 本身是普通 UserControl，但当前用SDK路由事件向旧窗转发结果；拆旧窗须同步替换或保留经明确宿主接缝的完整结果传播，不能让执行结果和lease断链。

已有托管反编译 `D:/MyWork/REA逆向测试/analysis/decompiled/Apps.XmlParser.CapabilityAudit/Apps.XmlParser.ParamTab/BottomCommand.cs` 显示单次先核根流程、模块就绪和方案加载，再调用当前模块；连续注册模块结果回调、按回调与根流程状态门禁，循环含300ms等待、退出解绑。原重置先确认、解绑运行参数订阅、重置模块并刷新；具体原机线程时序和异常覆盖尚未证明。原关闭清连续标志和资源/完成回调。上述是参考证据，不在UI控件复制VM业务全局服务。

## 完整生产链与保护

1. 新中立 Runtime 编辑窗由 UI 宿主创建，主体接入 `Plugin.Wpf` 原 `NativeToolEditorFrame` 与原样式/资源；只对 Blob 分派，设置真实 Owner、标题、尺寸、主题与内容，设计器仍使用同一真实视图资源。底栏不可同时保留旧 SDK 可视按钮。
2. 宿主为唯一 `ProductionEditorContext` 提供帧和 `INativeToolCommands` 生命周期。执行使用同一草稿完整捕获、`RuntimeDebugInvocation`、DebugOwner/取消与资源；单次执行锁定有效输入、记录状态，完成结果透过明确事件接缝进入现有 `SubscribeToolExecutionCompleted` 和正式图像/结果，路由/CLR兼容仅限同一当前生产链，不能保留重复业务执行器。
3. 确认先捕获活动字段和原子验证，失败保留原文/草稿，成功写正式历史并按原关闭；关闭或取消弃草稿并撤销订阅，保留正式配置；窗口关闭、重绑、方案切换和执行完成迟到时只允许当前上下文发布。`Retain`/Dispose、异常及执行中关闭按现有身份合同排空，不阻塞Dispatcher或释放活跃资源。
4. 本方独立单节点连续会话与原等价回调门禁需先沿完整非视觉执行层验证；若基础未就绪，连续/停止保留原位置和状态并显式不可误用，不复用整个工作流连续运行或旧窗定时器冒充。重置事务若尚无订阅解绑+模块重置等价能力，运行页呈现原图标但禁用并明确未实现，其他页隐藏；不以静态默认重建伪造。按用户的依赖保护口径，这些单列未实现，不得描述成功能已完成。
5. 准确状态映射要区分 Root运行、私有调试运行、有效context和确认能力；刷新/旧请求、命令不可执行与隐藏语义基于原模板及当前宿主安全能力，差异记录。绑定租约随窗口退役；不得把工作流调度放进 WPF 控件。

## 涉及及验收

先核 `NodeOpenDecisionChain`、`MainWindowRuntimeEditing`、`MainWindowProductionEditing`/结果发布订阅、`RuntimeNodeConfigurationControl`、现有 `NativeToolEditorFrame`/`NativeToolCommandPresenter` 和正式窗口关闭调用者；必要时新增 UI 中立 Runtime 窗口/宿主命令适配器，仅对Blob启用。必须同步更新受影响的真实生产消费者与测试，不能为旧SDK窗维护兼容桥或重复运行路径。共享合同行为确定后成片实施，不以只改XAML算完成。

验收同一真实 Blob 窗口：原底栏与运行页重置显隐、主题与状态；单次真实执行及输出/同次图文；非法/合法确认与取消、未失焦、Undo/Redo/保存重开；执行中关闭、重绑、旧回调与资源租约排空；根流程运行时禁用、结果刷新不串窗；设计器独立显示和实际VS/自然输入分别记录。连续与重置未实现或未证实项按上述受保护清单明确标记，不能借本批通过声称A22六项或Blob/S2全部完成。测试候选与正式Release分开，按实际加载DLL、测试SHA及版本回执核对。
