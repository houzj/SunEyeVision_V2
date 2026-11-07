# 结果发布统一消费者与公共惰性模型交接（2026-10-28）

- 基线提交：a104ef93。
- 当前分支：codex/result-publication-presenter。
- 当前执行者：source_editor；workflow-overview独立worktree。
- 任务目标：Presenter和公共模型沿单一IResultPublication/IResultValueLease合同呈现声明及实际结果；保旧真实Reader及惰性分页范围。
- 允许修改：宿主ResultPresentationPresenter、原ResultPresentationModel迁移到已授权Plugin.Wpf/Controls/Native/Composition/Results/PropertyList、原两个Tests及本交接。未改共享发布API/ToolResultViews/Main/源工厂/StandardTable。

## 已集中实施

1. 原UI ResultPresentationModel整体移至Plugin.Wpf/Controls/Native/Composition/Results/PropertyList/ResultPresentationModel.cs，namespace同目录，旧UI文件删除，无兼容桥。只有Contracts及同公共发布合同依赖，无SDK/UI宿主依赖。
2. 公共模型唯一工厂ForPublication(IResultPublication publication,IResultValueLease lease,int pageSize=128,Func<bool>?isCurrent=null)。校验发布/lease同Stamp及SchemaFingerprint，前后核IsCurrent/Stamp。只抽取Schema/Stamp/State和无资源声明Reader，不保存publication/lease，不延寿资源或含资源aggregate。Model公开Schema/Stamp/Identity(RunIdentity?)/State(统一ResultViewState)/PageSize/Outputs；旧ForSchema/ForRun与第二状态enum退出。
3. ResultPresentationNode既有Key/Declaration/DeclaredPort/ElementIndex/IsExpanded/ValueState/ScalarValue/GeometryValue/ChildCount/GetChildrenPage保留。可选当前守卫向各子节点传播，失效后不再暴露值或新页。ResourcePending语义保留，几何与巨数组按页读取，不复制全集。
4. Presenter唯一数据入口Present(IResultPublication,bindings=null)，旧ShowSchema/ShowRun/SDK Snapshot依赖退出。仅同实例同Stamp并仍Current复用一份lease及已访页。Presenter只Dispose自己的Borrow lease，绝不Dispose宿主publication owner。
5. Borrow前后、读Model/metadata前后、通知与分页、旧行事件均校验代次、同实例、Stamp和IsCurrent。清空退订先发生，最后释放旧lease；清理通知或lease释放重入安装新发布时，旧清理不再清新行/改HasResults。摘要读取不因失效后null计数/geometry抛错。声明(nullInvocation)/未执行/运行/成功/部分成功/失败/超时/取消分别按统一State呈现，成功空输出保成功行，声明空Schema保持空。

## 回归范围

原两个类33个Fact/Theory方法名和实质范围全部保留，并新增5方法，总38方法（不是实际执行行数）：

- 原声明顺序/Storage/未知序列计数/缺值null空值、状态隔离、文化与标量、嵌套记录和序列、巨double数组无全集拷贝、不可变geometry/大Polygon分页、键及已访页复用、ResourcePending且不读payload/不延寿图像、旧排队展开无害等保持。
- 原真实SDK ToolExecutor→DeclaredOutputSnapshot→真实资源Owner改经root ToolResultViews.FromSnapshot正式工厂创建publication；Fixture分开ReleaseResult/ReleasePublication，保原结果owner关闭后Borrow lease延寿及rows清空后最后释放断言。Presenter不能替宿主Dispose owner；测试显式关宿主publication后由Presenter退役放最后lease。
- 新增shared owner不越权Dispose、同实例同Stamp一借用复用、Current失效清空/旧分页无害、leaseStamp失配拒绝；Borrow重入让新发布胜出且仅释放旧待接lease；声明nullInvocation和同Invocation不同PublicationId更新诊断；懒页守卫重入替换不追加旧子页；模型失效不读旧页。

## 依赖与验证状态

- root须同批提供Plugin.Wpf.Presenters.Results的ResultPublicationStamp/IResultPublication/IResultValueLease/ResultViewState/ResultPublication具体实现，冻结构造(schema,stamp,state,values,owner,isCurrent,diagnostics?,displayNames?,displayedPorts?)。
- root须同批提供ToolResultViews.FromSnapshot(DeclaredOutputSnapshot,Func<bool>)与Declaration(ToolSchema,Func<bool>,displayNames?,displayedPorts?)。弱表仅固定同Snapshot PublicationId；owner用真实AcquireReader，测试依赖失效资源的新FromSnapshot确实拒绝。历史结果是否当前由UI传入回调，不重新绑已退休算法Admission。
- root同步Main原ShowSchema/ShowRun消费者；native已获新公共模型API并实现同Publication的DeclaredPropertyList消费，源工厂归root。不要保旧重载。
- 四个改动/迁移C#已用当前已加载Roslyn纯语法解析，0错误；git diff --check通过。共享API在本支尚未落，未语义编译、未构建、未运行任何测试、未VS/人工验收。测试源码存在不等于38项通过。
- 下一步：root整合统一API、宿主工厂/Main/源结果视图及native消费者，唯一正式Release集中UI/全量构建、上述原/新增定向回归与实际结果窗口切换分页/关闭验收。CURRENT_TASK与主方案由root集中维护。