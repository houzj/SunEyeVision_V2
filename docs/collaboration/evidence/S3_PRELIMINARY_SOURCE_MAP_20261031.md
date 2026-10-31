# S3 模块结果树预核对（2026-10-31）

本记录仅为 S2 人工复验期间的只读准备，S3 未启动实施，不能据此认定原件对照或阶段完成。

- 主结果区：`src/UI/Shell/Controls/Shell/ImageResultControl.xaml.cs` 使用 `PropertyListView`，从 `MainWindowViewModel.ResultTreeRoots` 接入数据；绑定/解绑命令受编辑权限、行可绑定性及当前结果身份约束；关闭时清理来源、结果展示及主题资源。
- 编辑链入口：`src/UI/Shell/Windows/MainWindowResultBindingEditing.cs` 使用 `ProducerBinding` 进入变量编辑；其完整执行、保存重开、撤销与异常行为需下一批逐项核对。
- 独立只读分析视图：`src/Plugin.Wpf/Controls/Native/Composition/Results/PropertyList/DeclaredPropertyList.cs` 从 publication 借用 lease，按 128 项懒加载，变量编辑明确不可用。它不是主结果区绑定能力是否存在的判据。
- 原件入口：`D:/MyWork/REA逆向测试/analysis/decompiled/VMControls.WPF/VMControls.WPF/ModuleResultView.xaml` 及其 code-behind；XAML 包含变量管理列。仍需追查结果数据模型、模板、分页、命令、资源及宿主完整依赖闭包，并核对运行行为。
- 未核实：原件与当前列、层级、分页语义、状态、绑定生命周期及结果消费者的逐项一致性；真实 WPF 与人工验收。不得将已有同名入口当作完成证据。

当前等待 S2 来源选择器人工复验，S2 封版后按阶段顺序集中设计和实施 S3。
## 原件 XAML 与宿主新增定位

原件 `ModuleResultView.xaml` 的模块结果页明确有三列表头（结果名称、当前值、变量管理）、可折叠的错误信息行、双向滚动树、选中路径和复制按钮；层级模板分别将模块的 Outputs 和 IO 的 Children 绑定为下级。此为静态 XAML 证据，尚未证明具体分页、绑定或运行时一致性。当前宿主 `MainWindowResultPublication.cs` 调用 `DeclaredResultPresenter.Present(publication, bindings)`，其 Roots 经 `MainWindowViewModel.ResultTreeRoots` 提供给主结果控件；仍需追踪构建、切换、退休、绑定与原件的完整差异。
## 模板初步差异

`PropertyListView.xaml` 已复用相同的 40 像素表头与错误行、200/2*/1* 列结构、树和路径区域；当前三列表头及路径提示是中文常量，原件使用动态语言资源。原件路径文本格式化为 `VmSolution.Instance[...]`，当前显示正式引用路径；是否需按现行身份合同展示对应人类可读格式，应结合绑定及消费者完整链判断，不能将原件路径当作可执行接口直接复制。当前使用统一 `Children` 模板替代原件模块/IO 两种类型模板，层级行为是否等价尚待检查。上述均为源码差异，未经过运行外观及语言切换验证。
## 变量编辑与分页入口补证

`MainWindowResultBindingEditing.cs` 在提交前核验方案会话、编辑权限、变量归属和 `GlobalProducerBindingRules`；解绑还校验来源引用相同，通过 `CommitBatch` 提交并刷新展示。这里证明入口及保护存在，尚不能推断撤销、保存重开或原件语义一致。主结果 `ResultPresentationPresenter.cs` 自身调用 `GetChildrenPage` 并有退休路径；与独立只读分析视图的 128 项分页是不同实例，应分别验证，不混称同一生产链。