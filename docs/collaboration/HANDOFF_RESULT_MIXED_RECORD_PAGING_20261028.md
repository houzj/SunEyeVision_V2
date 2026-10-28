# D05混合记录惰性分页交接（2026-10-28）

- 基线提交：158b55c6。
- 当前分支：codex/result-mixed-record-paging。
- 当前执行者：source_editor，独立workflow-overview工作树。
- 任务目标：修正Blob式sequence<Record含region>整支被判资源待接入，保标量/几何/记录与资源所有权完整链。
- 允许修改：公共ResultPresentationModel.cs、ResultPresentationModelTests.cs及本交接。未改Presenter/主宿主/共享API/Blob业务/StandardTable/源工厂。

## 已集中修正

1. 仅ValueShape.Resource叶为ResourcePending；含资源record/sequence使用实际Missing/Null/Available状态及观察计数，按既有128页展开。非资源标量/几何保原类型与不可变reader，资源叶不调用GetField/GetElement取其资源reader，更不读pixels/Domain/RLE/资源元数据。
2. 混合容器不存父DeclaredValueReader，也不存InputValues或强publication/lease。新增仅模型内部ResultValueAccess（弱publication、弱lease、Stamp）及ResultValuePath（稳定输出ID、字段ID/索引、父路径）。每次按需解析同lease的短路径，局部临时reader在调用返回后释放；不复制整条records、不扫描region。
3. Scalar/Geometry/ChildCount/新页均通过消费者guard及弱lease/owner当前性；值读前后复核Stamp/IsCurrent，关闭lease或publication后旧节点返回不可读/无新页。已有缓存无资源叶可以持不可变scalar/geometry reader，但不能绕过关闭守卫。当前公共API保持ForPublication及节点签名不变，无新共享合同。

## 回归与测试夹具

- 原15个Fact/Theory方法保语义范围，资源容器用例按正确新语义更新名称/预期，新增3个方法：8193条Blob式element-id/area/region/bounds/parts混合记录，128首分页/尾页/键复用/嵌套标量和Point类型；毒化region在呈现期任何Frame/SourceFrame/AcquireReader均抛错，期望访问0；lease关闭后旧页不访问。另验publication关闭但lease仍保owner时立即失效、最终owner只释放一次；缓存混合根与已读area子项保持时WeakReference region/publication/lease均不延寿。
- 原模型测试helper此前创建后using立即关闭lease再返回model；现按新真实生命周期由xUnit IDisposable fixture保lease/pub至测试结束，ImageSourceCases现已finally IDisposable.Dispose，可正确清理。原独立资源GC helper仍局部关闭owner/lease后返回model，不加入夹具持有清单，以实测不延寿。
- 巨double数组和嵌套数组仍保原分配上限；把ResultPublication构造/冻结提前到测量外，仅测模型和请求页，避免把现共享发布边界冻结计入模型全集拷贝。Success null的公共ResultPublication实际拒绝是ArgumentException，断言改为准确基类，不缩小必需值校验。

## 已验证与未验

- 两C#用当前已加载Roslyn纯语法解析错误0；git diff --check通过。
- 未语义编译、未构建/执行、未真实VS/人工验收。没有复用此前回归成绩。
- 集成依赖：root同步ResultPresentationPresenterTests原Resource_roots_and_resource_aggregates_stay_pending_without_reading_borrowed_payloads断言；现在正确为image叶pending、record显示“记录”、label显示secret，不能再断言整record pending/标量不可见。该文件本任务无写权未修改，已消息交root。
- native DeclaredPropertyList也应按节点ValueState/ChildCount展开混合记录，不再以ContainsResource整体禁用；已消息同步native，不动其分支文件。
- 下一步：root整合后唯一正式Release集中构建/原及新增模型回归、Presenter正确新语义和实际Blob模块结果分页验收；主账及原主方案由root集中维护。