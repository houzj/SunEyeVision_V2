# Presenter回归混合记录及helper owner收尾（2026-10-28）

- 基线提交：e171e81f。
- 当前分支：codex/result-presenter-test-owners。
- 当前执行者：source_editor，独立workflow-overview工作树。
- 任务目标：同步D05正确的混合资源记录预期，修复测试helper创建publication owner未释放。
- 允许修改：ResultPresentationPresenterTests.cs及本交接；未改生产、共享API或项目入口。

## 已集中完成

1. 原资源呈现用例改为只有image资源叶显示“资源待接入”；record显示“记录”且展开后label=secret可见，保poison图像元数据/payload访问0、Dispose0及真实SDK最后reader释放断言。用例名相应改为Resource_leaves_stay_pending_while_mixed_record_scalars_remain_visible_without_reading_payloads。
2. 正式FromSnapshot发布/冻结先完成，再开启PoisonImage呈现禁读阶段，不把发布边界的资源准入元数据校验误算为Presenter读payload。之后仍经真实Presenter公共Present/展开链验证，无私有handler或复制UI。
3. SchemaPresent/SnapshotPresent改用本测试实例的HoldAndPresent，在调用Present前登记publication owner；整例期间不退役，允许当前性/分页及同发布复用正常验证。测试类IDisposable在例尾逐一Dispose全部helper publication，清空清单；异常释放仍尝试其余owner后聚合报告。原RunFixture和显式using owner的真实释放断言不变。
4. 新增Helper_publication_owners_remain_current_until_the_fixture_finishes_and_then_retire：Presenter.Clear后声明owner仍Current；后续实际Snapshot失败发布两owner都保持Current；fixture Dispose后均退休，Presenter自己的lease再清理，二次fixture Dispose安全。

## 验证与未验

- 本C#用当前已加载Roslyn纯语法解析错误0；git diff --check通过。
- 已只读确认ImageSourceCases反射执行器finally调用测试实例IDisposable.Dispose；xUnit同样支持例尾fixture Dispose，无需改共享清单或项目。
- 未语义编译、未构建或执行测试，未VS/人工验收。生产结果所有权及实际效果仍由主线同源正式Release集中验证。
- 下一步：root合入后定向运行Presenter与D05模型回归，保原当前性、排队分页、重入、真实Reader释放的全部断言。主账/方案由root集中更新。