# J03 公共结果 Presenter 有限交接

- 基线提交：`28b98ffd758e9a1eb0db03f424fe07ad1533e3b6`。
- 当前分支：`codex/j03-result-presenter-20261024`。
- 当前执行者：Codex 独占子片。
- 独立工作树：`D:/MyWork/SunEyeVision_V2/artifacts/worktrees/j03-result-presenter-20261024`。
- 任务目标：为已提交 ResultPresentationModel 增加现有 DataGrid 的按需 Presenter，目录与指定 run 分开，Presenter 管理原 owner 的 reader lease。
- 允许修改及本提交文件仅三项：`src/UI/Services/Monitoring/ResultPresentationPresenter.cs`、`tests/SunEyeVision.UI.Tests/ResultPresentationPresenterTests.cs`、本交接。主树、旧 worktree 和固定包均未写。CURRENT 仅更新本树，不提交覆盖主线记录。

## 已完成及主线接线入口

构造 `ResultPresentationPresenter(ObservableCollection<FlatResultRow> rows, Action<bool> setHasResults, int pageSize = 128)`。调用线程由宿主负责，所有入口和行变更在同一 UI 线程执行；Presenter 不创建 Dispatcher、Manager 或执行调度。

- `ShowSchema(ToolSchema schema, IReadOnlyDictionary<string,string>? displayNames = null, IReadOnlyDictionary<string,ResultBindingInfo>? bindings = null)`：独立声明目录，不取运行值。
- `ShowRun(DeclaredOutputSnapshot snapshot, 同上两可选映射)`：来自冻结 `ToolResults.DeclaredOutput` 或明确 run 的 snapshot；准确保存其 schema/revision/完整 identity/status，获取并拥有 `DeclaredOutputReader`。不读 latest，不用 `IsCurrent` 拒绝历史。
- `RefreshRows()`：只重建当前可见且已请求的行，不额外取页。
- `Clear()` / 幂等 `Dispose()`：使旧回调 generation 失效、退订行回调、撤下旧行，再释放旧 reader。切 schema/run 和取得新 reader 失败也遵循此顺序。

保持现有 `FlatResultRow`/`ModuleResultNode` 定义、共享 ObservableCollection、DataGrid 样式/列及点击 handler 不变。Presenter 行不使用 SourceNode 反查值；现 handler 翻转 IsExpanded 后由 Presenter 回调加载。根行标题为“结果”。首次只生成声明输出行；记录、序列都折叠；每次展开仅取一页。尾部只有一个折叠的“加载下一页”行，点击才追加下一页。分页行只是 UI 游标，无 BindingInfo，不充当 canonical 模型节点。可见行递归只访问已经展开且已物化的孩子；不会预建全部子项/全部分页，也不 string.Join 全序列。

canonical 节点及缓存仍用原模型 Port.Id / field.Id / index 的稳定键。已访问行和展开状态在折叠、RefreshRows、相同 schema fingerprint 的 ShowSchema、相同完整 run identity + schema fingerprint/revision/status 的 ShowRun 中保留；切换则新建隔离模型。仅对已访问节点持有行及回调，替换时退订。

displayNames 与 bindings 仅按根 Port.Id 查询，立即写入行，不保留调用方字典。BindingInfo 完全来自上游显式映射；成员和分页行均不猜名称/路径/数组对应关系，不把模型 Key 作为 ResultPath。主线后续需提供 canonical BindingReference 的明确映射。

成功空序列、missing、null、空文本、未运行及失败明确区分。数值为 InvariantCulture；1 元素序列保持容器，不解包。失败显示首条诊断；未读取的成功字段显示“值不可用”。资源和含资源聚合根仍由纯模型标为 ResourcePending，不创建其 DeclaredValueReader 或读取 payload；Presenter 保持整个 snapshot 的 DeclaredOutputReader lease 至视图撤下，仅 Dispose reader，不 Dispose borrowed IReadOnlyImage。几何值显示待接入，不新增任意对象读取。

## 实际构建与测试

所有设施、obj/bin/PackagingRoot/locks/logs 都在本工作树 `artifacts/validation/j03-result-presenter`，简称 `$owned`。19 个实际 UI.Tests 项目依赖均经 MSBuild 求值检查 TargetPath/OutDir/BaseIntermediateOutputPath/MSBuildProjectExtensionsPath/DirectoryBuildTargetsPath/PackagingRoot 为 owned；`logs/*-evaluation.json`、`logs/evaluated-project-closure.txt` 保留结果。

实际 props 导入本工作树 Directory.Build.props；targets 导入本工作树 Directory.Build.targets，文档输出改到 owned，覆盖 EnsureHalconNetAssembly 以免写 dev/Debug。HALCON vendor 只从已固定 binding-b2-regression/build/app 复制五个依赖到 owned/vendor 并逐 SHA 核同，见 vendor-receipt；未从主树 live SDK/Contracts 取 DLL。native 部署定向复制该五件，不宣称完整 HALCON 发布验证。离线 config 仅映射本机 cache，pattern `*`，无网络、无降包。

实际命令（先在本 worktree 设置 `$owned = Join-Path (Get-Location) 'artifacts/validation/j03-result-presenter'`）：

```powershell
dotnet msbuild <19 个实际 csproj> -p:Configuration=Release -p:DirectoryBuildPropsPath=$owned/validation-build.props -getProperty:TargetPath,OutDir,BaseIntermediateOutputPath,MSBuildProjectExtensionsPath,DirectoryBuildTargetsPath,PackagingRoot,HostDebugRoot -getItem:ProjectReference -nologo
dotnet restore tests/SunEyeVision.UI.Tests/SunEyeVision.UI.Tests.csproj -p:Configuration=Release -p:DirectoryBuildPropsPath=$owned/validation-build.props --configfile $owned/offline.NuGet.Config --locked-mode --disable-parallel
dotnet restore tests/SunEyeVision.UI.Tests/SunEyeVision.UI.Tests.csproj -p:Configuration=Release -p:DirectoryBuildPropsPath=$owned/validation-build.props -p:RestoreLockedMode=false --configfile $owned/offline.NuGet.Config --force-evaluate --disable-parallel
dotnet build tests/SunEyeVision.UI.Tests/SunEyeVision.UI.Tests.csproj -c Release --no-restore -p:DirectoryBuildPropsPath=$owned/validation-build.props -m:1
& $owned/run-targeted-tests.ps1 -RunName presenter02
```

- 首恢复 restore01 因基线锁还描述旧项目图报 NU1004；具体为 Runtime/Kernels 新项目引用与旧锁不一致。只重算 owned locks 后 restore02 成功。原日志/receipt 和 locks-baseline 均保留。`lock-version-comparison.json` 记录包版本变化 0 项。没有更改任何项目/仓库锁。
- build01：实际完整 UI.Tests 项目闭包，0 错误、1184 警告、75.64 秒。presenter01：12 PASS / 0 FAIL / 0 SKIP。随后静态复核将资源聚合中未读取的普通字段从运行状态文字改为“值不可用”，补同一测试断言。
- build02：最终实际 UI.Tests 增量编译，0 错误、767 警告、19.79 秒。新 Presenter/测试没有编译警告。没有产品/测试失败，未重跑旧模型 17 项或无关全套。
- presenter02 最终：12 PASS / 0 FAIL / 0 SKIP，测试耗时 624 ms；PID 56912，UTC 2026-10-24 04:43:36.1043622 至 04:43:39.0959691，自然 exit 0，timedOut=false。
- 精确 filter：`FullyQualifiedName~SunEyeVision.UI.Tests.ResultPresentationPresenterTests`。runner 先 ParseFile 通过、检查实际 runtime 文件与 SHA，再用实际 dotnet vstest；进程上限 120 秒。日志、TRX 和完整实参见 presenter02-runtime-receipt.json、logs/presenter02-*。
- 实际 PE 读取 UI.Tests AssemblyMetadata.HostDebugRoot 为本 owned/build/app；app/testbin 内 UI、SDK、Contracts 三程序集 SHA 相同。构建后及运行前核对 1047 条源输入无漂移。最终 testbin 188 文件完整 manifest；owned app 197 文件 manifest 均保存。

覆盖实际 UI.dll 里的 Presenter、原 FlatResultRow、WPF DataGrid/STA Dispatcher，以及真实 ToolExecutor -> DeclaredOutputSnapshot.Publish。覆盖声明顺序与显式绑定、嵌套逐层分页、重复展示/折叠保持、missing/null/empty、culture、历史 admission 退役后仍可呈现、队列旧行在 Clear/Dispose 后失效、资源 getter/borrowed Dispose 为 0、reader 取得失败及重入清理。200 万 double 先完成冻结后，首屏+32 项页的线程分配 <1 MiB；用于防止全数组复制，不是正式性能 SLA。

关键证据 SHA256：

- source-input-manifest-build02.json（1047 条）：`83593AC9BA878E3F942D25F730B2955115C55F3CB6886BBA1BAC063400BFF10D`
- presenter02-binary-input-manifest.json（188 条）：`2C33000F48D8E31C25A10668DD9802B1FEAFDF72C41D637C47D1A73F95B89723`
- results/presenter02/presenter02.trx：`AB8A35DBFE9FE02E25EA6FFA6D091B21B749D618F0255DBFF20929C1989BD557`

## 未完成、人工验收及后继边界

本片没有修改 MainWindowViewModel、ImageResultControl、ImageResultViewModel、Manager、Contracts、SDK 或 Workflow。未把 Presenter 接到主窗真实选择/声明变化/普通相机刷新/关闭；这些调用由主线接入，并继续保持 selected/document/window 等有效性门。原 `ApplySubroutineOutputChange` 的旧树名字增量路径也由主线处理，不能修改规范模型或历史 run。

没有启动完整 MainWindow，没有实际鼠标点击 ImageResultControl，没有做 VS 设计时或人工验收。测试中的 DataGrid 未创建 Window；实际展开由与旧 handler 相同的行属性翻转触发，旧队列验证使用真实 Dispatcher。未运行完整 solution/ALC/HALCON 全套，未关闭公共树全机制、资源桥展示、几何展示、schema/run 正式集成、性能窗或最终统一 Release/人工/VS事项。未 push、未清理未知文件。

构建时 HEAD 为冻结 28b98ffd 加本片两个新源文件，不能声称程序集内构建版本已是后续提交号。提交后另生成 owned commit-binding.json，核本提交两个源的归一文本与实际编译源一致，绑定以上 source/bin/TRX。最终提交号由交回消息提供。
