# J03 显式轮廓元素地址有限交接

- 基线提交：`07b0413fc95cc88ae76c263e7437da054fc442be`。
- 当前分支：`codex/j03-contour-address-20261024`。
- 当前执行者：Codex，独占工作树 `D:/MyWork/SunEyeVision_V2/artifacts/worktrees/j03-contour-address-20261024`。
- 任务目标：生产者显式给出每条 batch 轮廓的输出端口/元素/成员地址，按原 batch 保留至 collector 终态，不由名称或数量推断对应。
- 允许修改及本提交仅五文件：`src/Plugin.SDK/Models/Visualization/DeclaredElementAddress.cs`、同目录 `VisualElement.cs`、`DisplayCollector.cs`、`tests/SunEyeVision.Plugin.SDK.Tests/DeclaredContourAddressTests.cs`、本交接。主树产品、旧树及旧 fixed 包均未写。

## 已完成与准确 API

```csharp
public sealed class DeclaredElementAddress
{
    public DeclaredElementAddress(string fieldId, long elementId,
        IReadOnlyList<string> memberFieldPath);
    public string FieldId { get; }
    public long ElementId { get; }
    public IReadOnlyList<string> MemberFieldPath { get; }
}

public BatchedContourGeometry(IReadOnlyList<Point[]> contours, bool isClosed = true,
    IReadOnlyList<DeclaredElementAddress>? sourceElements = null);
public IReadOnlyList<DeclaredElementAddress>? SourceElements { get; }

public void DisplayCollector.AddContourBatch(GraphicsStyle style,
    IReadOnlyList<Point[]> contours, bool isPass = true, bool isClosed = true,
    IReadOnlyList<DeclaredElementAddress>? sourceElements = null);
```

FieldId 是声明输出端口 ID，MemberFieldPath 是同声明的稳定字段 ID 序列。构造拒绝空白端口/成员 ID、负 ElementId、null path；允许空 path 表示根元素，允许 long.MaxValue，不作位置转换。路径立即复制为私有只读列表，class/get-only 无可写属性。地址只在同一次 ToolResults 内寻址，不包含或产生 RunIdentity/Owner/注册表，不检索 schema 或 latest。

batch 复制 Contours 外层和地址列表并只读暴露；提供地址时必须逐轮廓同 count 且不能含 null 地址。旧调用缺省 SourceElements=null，明确未映射；不会从相同样式、名称或两个同长数组生成地址。Point[] 本体继续既有借用语义，不克隆点 payload，不承诺深不可变。地址本身不可变，因此复制其列表即可。

AddContourBatch 先完成 geometry 构造与校验，再 GetOrCreate/Append，错误 count 不留下 style/item/部分几何。原 null+未映射和空批次调用保留 no-op；显式错误映射即使轮廓为空也拒绝。原源码调用方式仍适用；添加末尾可选参数改变程序集方法签名，主线应随新 SDK 重编依赖，本片没有改合同版本常量、发布规则或序列化。

实核现有同 Style 累计、Merge 和 Freeze：都保留原 BatchedContourGeometry 对象，Freeze 仅把 GeometryList 转数组，无需拆分或重组地址。另发现并经父协调明确批准：首次 Items 后 _isFinalized 缓存不失效会隐藏后续 batch/merge。现仅在 AddContourBatch 成功追加与非 null Merge 入口两处置 false，不扩其他 Collector 生命周期、分组、所有权或渲染行为。相同 Style 的每个 batch 保持独立 SourceElements；IsPassArray 仍按 batch 对齐，IsClosed 保留。

## 实际验证、设施与命令

全部输出位于本工作树 `artifacts/validation/j03-contour-address`，简称 `$owned`。实际 SDK.Tests 及 8 项依赖共 9 个 csproj 经 MSBuild 求值核 TargetPath/OutDir/BaseIntermediateOutputPath/MSBuildProjectExtensionsPath/DirectoryBuildTargetsPath/PackagingRoot 均 owned。日志与闭包列表在 `logs/*-evaluation.json`、`logs/evaluated-project-closure.txt`。

本 owned props/targets 仅导入本工作树原构建文件，文档输出重定向 owned，覆盖 EnsureHalconNetAssembly 避免写 dev/Debug。固定 binding-b2-regression/build/app 五个 HALCON 运行依赖只读复制到 owned/vendor，逐 SHA 核同；托管 halcondotnetxl 为本冻结树 libs 输入。没有读取主树 live SDK/Contracts DLL。native 定向部署只支持本有限纯显示测试，不冒完整发布/native 算子验证。

offline.NuGet.Config 仅映射本机包 cache，pattern `*`。已知基线锁图落后，不再盲试 locked-mode；只复制至 owned/locks 后离线 force-evaluate，包版本变化为 0（lock-version-comparison.json），仓库锁不写。无联网、降包或依赖版本修改。

实际命令（在本 worktree 设 `$owned = Join-Path (Get-Location) 'artifacts/validation/j03-contour-address'`）：

```powershell
dotnet msbuild <实际 9 项 csproj> -p:Configuration=Release -p:DirectoryBuildPropsPath=$owned/validation-build.props -getProperty:TargetPath,OutDir,BaseIntermediateOutputPath,MSBuildProjectExtensionsPath,DirectoryBuildTargetsPath,PackagingRoot,HostDebugRoot -getItem:ProjectReference -nologo
dotnet restore tests/SunEyeVision.Plugin.SDK.Tests/SunEyeVision.Plugin.SDK.Tests.csproj -p:Configuration=Release -p:DirectoryBuildPropsPath=$owned/validation-build.props -p:RestoreLockedMode=false --configfile $owned/offline.NuGet.Config --force-evaluate --disable-parallel
dotnet build tests/SunEyeVision.Plugin.SDK.Tests/SunEyeVision.Plugin.SDK.Tests.csproj -c Release --no-restore -p:DirectoryBuildPropsPath=$owned/validation-build.props -m:1
& $owned/run-targeted-tests.ps1 -RunName contour01
```

- 恢复首次成功，无设施、产品或测试失败。build01 0 错/366 警告/21.07 秒；仅修测试自己的 nullable 警告后，build02 最终 0 错/1 个既有 ToolExecutorContextTests 警告/3.47 秒。新产品 API 在 build01 与 build02 间未改。
- runner 先 ParseFile 通过，再检查实际 runtime 文件、完整 SHA；真实 dotnet vstest 进程上限 120 秒。实际 PE 读取 AssemblyMetadata.HostDebugRoot 为本 owned/build/app，app/testbin 的 SDK/Contracts/Workflow SHA 相等；运行前核 562 源条目无漂移。
- contour01：实际 SDK.Tests 首轮 21 PASS / 0 FAIL / 0 SKIP，新增地址 18 项 + 既有 DisplayCollectorGeometryContractTests 3 项，测试耗时 80 ms。PID 15296，UTC 2026-10-24 05:03:03.7866600 至 05:03:06.5417766，自然 exit 0，timedOut=false。
- 精确 filter：`FullyQualifiedName~SunEyeVision.Plugin.SDK.Tests.DeclaredContourAddressTests|FullyQualifiedName~SunEyeVision.Plugin.SDK.Tests.DisplayCollectorGeometryContractTests`。
- 完整终态覆盖路径复制、外层列表不可变/点 payload 仍借用、非空和范围校验、错 count 原子拒绝（含空轮廓错映射）、生产源对象排序前后 ID/轮廓一致、同 Style OK/NG/未映射同长批次不串、Items 后追加与两种 Merge、Clear/旧未映射调用。没有只测试镜像 if 或模拟 collector。
- 最终 source 562 条、testbin 65 文件、app 28 文件 manifest；实际实参/运行 receipt、原 build 日志、props/targets/config/vendor 证据均 owned 留存。提交后另生成 commit-binding.json，核提交源归一文本与实际 tested 输入一致；构建 HEAD 为 07b0413f 加本片修改，不能冒后续提交号已写入程序集。

关键 SHA256：source-input-manifest-build02.json `EB5BEFE18E260B01265208B30AB9C8DC7EC08CD7FE5220E1C9B28334BF14CE3E`；contour01-binary-input-manifest.json `F43C81FFEF0DD62084FF43382B48F28415C5673B7804B580D050001560BFDC74`；results/contour01/contour01.trx `6C7F8AB740B1B24D8676D426645E1F21A6CC4C3C7D66A9ED32F1CBC5794BA05D`。

## 未完成与交接边界

不改 renderer、VM、工具/Blob、Contracts、Workflow、Owner、资源载体、版本或发布规则。未重跑 Presenter12/旧模型17、SDK全套、HALCON/ALC 窗口或完整 Release；没有主窗/鼠标/VS人工验证。这里只交付显式地址载体与 collector 保存路径，不冒真实 Blob、树↔图形交互、schema/run/资源/性能整项完成。

父主线后继从同一个实际 Blob 对象的 ElementId（排序前 HALCON region 序号）及 BlobsPortId、contour/rotated-bounds 稳定成员 ID 产地址，再传新 API；本片不提供按两个数组长度配对的 helper。地址是否属于实际 schema 和指定 run 仍由该生产/消费接线核验。主树唯一 writer 不变，待主线整合后做实际 Blob 定向控。

CURRENT 只本树更新不纳本提交；不 push、不删除或清理未知漂移。最终精确 commit 与自有 receipt SHA 由交回消息提供。
