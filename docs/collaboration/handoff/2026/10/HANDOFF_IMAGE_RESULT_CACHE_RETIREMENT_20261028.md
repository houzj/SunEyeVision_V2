# D02旧结果缓存死链集中退役交接（2026-10-28）

## 独立基线与职责

- 分支 `codex/image-result-cache-retirement`，独立工作树workflow-overview，基线 `02872a5c`。root临时冻结MainWindowViewModel与NodeResultManager，本线唯一写者。
- 复用原方案 `RESULT_CACHE_CONSUMER_AUDIT_20261028.json`，在当前基线重新沿src/tools/tests/scripts核引用，不因旧证据直接删；不改正式执行/结果合同/图像源缓存/视觉/Blob/Group。
- 这是D02源码退役候选，不代表整个图像与结果目标完成；没有构建、运行、VS或人工通过成绩。

## 删除前引用与决定

| 闭包 | 当前引用证据 | 决定 |
|---|---|---|
| WorkflowExecutionOrchestrator | ExecuteAndDisplayAsync仅本类定义；PreExecute无外调。Main只字段/getter/构造/统计/清空 | 删除协调器及所有Main专用入口，不留兼容getter/空统计 |
| PreExecutionCache / CachedExecutionResult | 仅Orchestrator、缓存本身及NodeResultManager回填消费 | 删除，不为nodeId_imageSourceId旧键补Run/Schema/Frame协议 |
| WorkflowEnginePool | 仅Orchestrator构造与租还；构造Prewarm(1)预建一个引擎 | 删除无业务入口的启动预建，不是解码器预热 |
| ImageConverterPool | 仅Orchestrator和NodeResultManager无调用的ConvertMatToBitmapSource | 删除按尺寸共享可写Bitmap的旧池，不重写补救 |
| BatchUIUpdater | 仅Orchestrator实例化/入队/Flush/Dispose | 删除该专用类型，其他BatchUpdateManager/EnhancedBatchUpdateManager不动 |
| NodeResultManager.UpdateNodeImageFromCache | 唯一外调是旧Orchestrator；从Bitmap重建HalImage并伪造GenericToolResults Success写WorkflowContext | 完整退出无准入身份的回填方法，不影响真实UpdateNodeResult |
| NodeResultManager.ConvertMatToBitmapSource | 无消费者 | 删除该方法、池字段、构造初始化与专用using |
| Main.ResolveFirstImagePropertyName | 唯一用途是传入NodeResultManager的回填专用第三构造参数 | 删除私有resolver、callback参数与字段，Main改用现两参构造；PositionCorrectionDisplayTests原两参不变 |

核验查询覆盖C#/XAML/项目/PowerShell/Python消费者；另有ui_verify.txt仅历史编译日志，保持原证据。产品/测试/脚本对这些类型与方法的精确查询删除后为0。未发现需保留的实际执行消费者。

旧构造虽不执行工具，仍会预建引擎、启动PreExecutionCache每60秒清理timer并持有转换池/DispatcherTimer；本次连同初始化一起退出，不能把“没有执行入口”误说成“原来完全没有运行副作用”。

## 删除前源码SHA256

- WorkflowExecutionOrchestrator.cs：72197bd22d284d4b4352ca6cf0562e4458ced14637ebbc0931c89373bd2ea40e
- PreExecutionCache.cs：9003c1bc64e32c4c7ff19b9188b29ec3a0e5d2256f4d3c61812f44c258fe7ac8
- CachedExecutionResult.cs：a696d48ce6952fa00fe3f205a36b71b3eac85c1e7c383e7f64b75464881041f7
- WorkflowEnginePool.cs：62c6cd61b61baf96802ace7fc8b0cc40e7f5920cd22e7e1737a6e45c9a6b113d
- ImageConverterPool.cs：d23dc9e8cc609a9f5bbb1f00d5e62b7812d8b55816d25a171732514640002707
- BatchUIUpdater.cs：d7cf4bd0d460cd4ad767c436295ae0c78cba6190c1f7eb8ef11d6fdc17a1f5bc
- NodeResultManager.cs：315acb47cc14517272555116c966f9e65beb3db0aa442b26b14f45dc2dd86707
- MainWindowViewModel.cs：d43579512bb00d8804db80ae18aa0bf52697ebf4a35b46260c7fe0e42ff83047

前六项与原审计一致；Main已含本轮新源迁移，按当前基线核其消费者，不照搬旧Main SHA。

## 保留链及验证出口

- NodeResultManager真实UpdateNodeResult的ExecutionIdentity.TryPublish、上下文结果与显示刷新、GetNodeOutputImage/GetDisplayImageFromSource及VisionImageToBitmapSourceConverter原样保留；区域/位置修正/高亮视觉原样保留。
- 正式WorkflowExecutionManager/WorkflowExecutionEngine/DeclaredOutputSnapshot/Reader/FrozenOutputResources、RuntimeToolResults资源所有权未改；图像源ImageCache/LocalImageAssetCache/SourceStripSession/运行输入快照未改。
- 非本死链的性能工具未删，不顺手扩展全局清理。
- 纯Roslyn语法两文件0错。保留方法体比对：NodeResultManager16个保留方法body全部原样；Main168个方法body原样，唯OnCurrentSolutionChanged移除旧ClearCache调用。两处构造仅退出旧初始化与callback。初次只读比对脚本未处理表达式方法，已纠正后完整复核；不是产品错误。
- git diff --check通过；没有dotnet build/test、未生成私有Release、未复制DLL。root集成后在主仓唯一同源Release做规定UI/全量构建、源执行与结果显示/上下文切换关联回归，不套旧测试成绩。
- 原审计、旧失败和历史代码由Git与原文档保留；本单交root归入原主方案阶段状态，不能仅凭删除量声明D01–D09全部完成。
