# PropertyList 正式结果发布适配交接

- 基线提交：a104ef93。
- 当前分支：codex/declared-property-list。
- 执行者：source_native，独立 workflow-timing-footer worktree。
- 允许修改：新 DeclaredPropertyList.cs、ImageSourceEditorFactory.cs 的 PropertyList 路由、本交接；未改现 PropertyListView、共享合同、主 UI、ToolResultViews、模型或 StandardTable。

## 完整接线

ImageSourceEditorFactory.CreateResultView(schema, projection) 在 projection.Kind==PropertyList 时创建 DeclaredPropertyList；其余维持 DeclaredResultTable，未保旧 Update 兼容壳。

DeclaredPropertyList 实现新的 IToolResultView：Present(IResultPublication)、Clear、Dispose、SelectionHighlightChanged(EventHandler<ResultSelection>)。直接组合原 PropertyListView，IsReadOnly=true，不提供变量链接/解绑业务命令。Sources 使用构造 schema.Outputs，并按 publication.DisplayedPorts 过滤实际顶层显示。

生产数据仅使用同一次正式 publication.Borrow 得到的本视图租约，并直接调用另一独立路迁入的 ResultPresentationModel.ForPublication(publication, lease,128,isCurrent)。无第二次 SDK 缓存查询、无反射取值、未复制模型。模型节点稳定 Key 对应同代 Entry/Row，重复 Present 同一当前 publication 不重建、保留展开/选择与已读页；新 publication 即使 schema 相同也换代。

展开时才 GetChildrenPage，每页 128，保同模型/节点和已加载行对象。下一页是纯视觉导航行，不作为业务身份；退出后旧 row 属性/旧命令/旧分页回调均不能读取新发布。三列模板绑定 DisplayName/DisplayValue/Level/Children/IsGroup/IsExpanded，变量能力明确 false。

状态覆盖 Declaration/NotExecuted/Running/Success/PartialSuccess/Failed/Timeout/Cancelled；诊断显示到原 ErrorInfo 区。Missing/null/空字符串/空序列明确区分，数字使用 InvariantCulture，几何保原坐标文本；资源或含资源 aggregate 直接服从共用模型 ResourcePending，不推造 bitmap/frame/domain 或几何证明。

## 命令与资源生命周期

每代命令核当前 generation、publication 引用、stamp、IsCurrent、租约 stamp 与实际 Row 引用；IsCurrent 调用前后重核，防宿主回调切代。原 SelectedCommand 仅选择路径。选择时最多发同 stamp、node.Key 和空 Targets 的 ResultSelection 安全清选，不根据当前显示帧构造高亮。CopyPathCommand 仅当前正式行路径，写 WPF Clipboard；没有业务变量修改。

Clear/新发布/Dispose 先换代失效、解除所有 IsExpanded 与下一页订阅，摘自身 publication/model/lease，再清原视图和行；finally 释放自己的 lease。清选事件带退役 stamp，不会用新发布身份清除旧覆盖层。Dispose 最后解除外部事件引用。绝不 Dispose 共享 publication。可重入通知后的代次检查避免旧清理覆写新安装。

## 依赖汇合

- 新公共 API 实际命名空间 SunEyeVision.Plugin.Wpf.Presenters.Results，取证读取主仓 ResultPublication.cs；ResultSelection(stamp,object? identity,IEnumerable<ResultHighlightTarget>)。
- ResultPresentationModel / ResultPresentationNode 由 source_editor 专路迁入同 PropertyList namespace。ForPublication 和 IsCurrent 守卫按其已冻结实际实现消费。
- 本分支基线仍是旧接口、尚无新模型，必须与主线共享 API、editor 模型提交同批汇合后正式语义编译。不加占位接口或兼容 overload 消除当前分支预期未解析类型。
- 原 PropertyListView 的行 Grid 左键适配漏项由 root 集中修复，本适配不改模板或用 Focusable=True 代替原输入行为。

## 验证与未完成

已执行：DeclaredPropertyList.cs 与 Factory 的 Roslyn 5 纯语法检查零错误，diff 检查。未执行语义编译、正式 Release 构建、WPF 运行、VS 设计器或人工验收；未构建任何私有 Release。

下一步 root 同批集成共享 ResultPublication/IToolResultView 与迁移模型，纳正式测试轨验证 PropertyList 工厂可构造、同 pub Borrow 不重复查询、128/256 分页与同对象保留、旧代页/选中禁用、失败/取消/空值/诊断、Clear/Dispose 释放借用且共享 pub 仍归宿主，及真实用户行左键和变量禁用。现源码候选不代表完整生产链已验收。
