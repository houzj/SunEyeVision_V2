# Blob 标签分析单项目迁移与算法复核记录

日期：2026-11-20
工具：`BlobLabels`（原厂 `IMVSBlobFindLabelsModu`）

## 结论

BlobLabels 已完成 Runtime 与 Wpf 源码合并到单项目 `tools/SunEyeVision.BlobLabels/SunEyeVision.Tool.BlobLabels.csproj`。同一程序集携带 Runtime 和 Editor 两个产品组件声明，Release 构建和 30 项现有 BlobLabels 测试通过。你已完成当前 UI 的人工确认，本记录只复核工程接入、算法实现边界、结果投影和释放链。

原厂没有提供可直接复用的算法源码，但本轮通过 REA 对同版本 `IMVSBlobFindLabelsModu.dll` 做了静态反编译，已经取得 `Process`、`ProcessAlg`、`LimitJudge`、`ProcessOutput`、`GetAllParamList` 和 `SetParam` 的原生入口证据。证据确认了调用边界、参数名称、结果字段和判定状态写入位置，但没有把 `MVDMultiCategoryBlobFindCpp.dll` 内部的分割和特征计算展开为可执行等价实现。现有 HALCON 链仍属于本方算法实现，不能宣称与原厂数值等价。未取证行为保留为未确认，不用本方测试替代原厂事实。

## 原厂证据

- 资源和 XML 哈希清单：`docs/07-工具逆向/tools/imvsblobfindlabelsmodu/resources.json`
- 参数与端口索引：`docs/07-工具逆向/tools/imvsblobfindlabelsmodu/parameters.json`
- UI 控件和共享组清单：`docs/07-工具逆向/tools/imvsblobfindlabelsmodu/resources.json`
- 原厂逆向状态：`docs/07-工具逆向/tools/imvsblobfindlabelsmodu/imvsblobfindlabelsmodu-reverse.md`
- REA 原生证据包：`docs/evidence/bloblabels-rea-algorithm-evidence-20261120.json`
- 原厂 DLL SHA-256：`cc9b5b7f75319affb3176b5fa36d0cd16ca1671bf38bd3aa07797d3d087a8e01`
- REA 主证据记录：`ev_b26415d133438d2b90b6c80ad7d7776bca62ac61bd8e64e3cbada1148086bab6`（Process）、`ev_41276131b6bce70805bfc5e5859cd059f03a5e1a5bf7a7fc54bb4524627f022c`（ProcessAlg）、`ev_51d59dbf922254f0e56192a71a5cdf2cd96ad4601b99ed21d9c464b9cc86b98a`（LimitJudge）、`ev_c46ad7ff187daaf0b6cd2cb57c99bfa1fd846b6a7b93e7f01214f2adc42b45c5`（ProcessOutput）、`ev_e6d3d3e95bebb6cc0a223910839b621d172676d4e2df3192c76926d0ae64f889`（GetAllParamList）、`ev_a60f3185da1ba1233d16eaddbff47f717521aadd311a7b80cb03f81a481ab22d`（SetParam）。
- 原厂逆向文档此前仅记录资源登记。本轮 REA 已补齐原生宿主层调用边界，但算法 DLL 内部语义、动态输入输出值和释放时序仍未闭合。

## 工程迁移结果

| 项目 | 状态 |
|---|---|
| 单项目工程 | `SunEyeVision.Tool.BlobLabels.csproj` |
| Runtime 入口 | `SunEyeVision.Tool.BlobLabels.Runtime.BlobLabelsTool` |
| Editor 入口 | `SunEyeVision.Tool.BlobLabels.Wpf.BlobLabelsEditorFactory` |
| 组件声明 | 同一 Runtime 程序集包含 Runtime 与 Editor 两个 `ToolComponentAttribute` |
| 产品注册 | `product/ToolComponents.props` 两条组件记录均指向单项目 |
| 解决方案和探针 | `SunEyeVision.slnx`、RuntimeEditorProductionProbe、Inspection.Tests 已切换到单项目 |
| 旧发布残留 | 已移除 `SunEyeVision.Tool.BlobLabels.Wpf.dll` 与对应 deps，避免旧双项目组件参与封装 |

## 算法链复核

### 原厂静态事实

REA 反编译确认原厂 `Process` 的顺序为：

1. `GetProcessInput` 准备宿主输入。
2. `ProcessAlg` 创建并复用 `MVDMultiCategoryBlobFindCpp.dll` 的实例，设置图像/掩膜和阈值区域，执行算法并取得结果。
3. `LimitJudge` 读取 Blob 结果，对每个 Blob 计算判定位并写入状态缓冲，同时汇总模块状态。
4. `ProcessOutput` 将结果写入宿主输出，包括 `BlobNum`、`TotalArea`、`CenterX`、`CenterY`、`Angle`、`Perimeter`、`BoxWidth`、`BoxHeight`、`BlobAngle`、`AxisRatio`、`CentroidX`、`CentroidY`、`Circularity`、`Rectangularity`、`LongAxis`、`ShortAxis`、`Score`、`ClassLabel`、`LabelValue`、`ContourNum`、`ContourInfo`、`BinaryImage`、`BlobImage` 和 `BlobStatus`。

`GetAllParamList` 明确追加 `LabelWiseAreaSelectEnable`。`SetParam` 明确接受 `BlobNumLimit*`、`BlobAreaLimit*`、`BlobTotalAreaLimit*`、几何特征限制、`FindNum`、`SortFeature`、`SortMode`、`connectivity`、`OverlapRatio`、`MaxOutPixelNumEnable`、`SelectByOutPixel`、`MaxOutPixelNum`、`LabelWiseAreaSelectEnable` 和 `ThresholdArea`。这证明 `OverlapRatio` 与像素超界参数是原厂真实参数，不能从参数声明中删除，但当前没有足够证据实现其算法语义。

REA 证据是静态反编译结果，不是原厂运行黄金样例。`ProcessAlg` 的核心工作委托给 `MVDMultiCategoryBlobFindCpp.dll`，因此不能仅凭外层反编译推断阈值合并、连通域特征计算、重叠率和像素超界的具体数值规则。

原厂模块目录本身没有随模块复制的 `MVDMultiCategoryBlobFindCpp.dll`，但同一安装件的 SDK Runtime 提供了可取证的 x64 算法库：`C:\Program Files\VisionMaster4.4.0\MVDAlgorithmSDK\Runtime\x64\MVDMultiCategoryBlobFindCpp.dll`，SHA-256 为 `7627e0698acef65892bdd56c199dcff6c42e893aba7718b394f3d2485754b76d`。该库的 REA 证据单独归档在 `docs/evidence/bloblabels-external-algorithm-evidence-20261120.json`，不能与模块 DLL 的证据包混写。

### 外部算法库已确认的调用边界

REA 对该 x64 SDK Runtime DLL 取得 116 条证据。已确认的事实如下。

- `CreateMultiCategoryBlobFindToolInstance` 在实例指针为空时返回 `0x80100006`，否则分配 `0x78` 字节对象并进入初始化。`DestroyMultiCategoryBlobFindToolInstance` 对空指针返回同一错误，对有效对象调用虚析构路径后返回 0。
- 初始化路径创建通用 `MVD_ALGORITHM.DLL` 句柄，调用 `MVD_Algorithm_CreateHandle(..., 0x139, 0)`，并创建基础参数、性能辅助、预处理掩膜和区域图像辅助对象。
- 主处理路径先更新通用句柄，再调用 `MVD_Algorithm_Process(..., 0x139, ...)`，随后创建结果对象并取得 `OutSrcImage` 与 `OutDrawImage`。这与模块侧 `ProcessAlg` 委托到外部算法实例的事实相互印证。
- 外部库导入 `MVD_Algorithm_SetValueByString`、`GetValueByString`、`UpdateHandle`、`ReleaseWorkingMemory`、`ReleaseResultMemory`、`DestroyHandle`，以及图像、矩形、预处理掩膜对象的创建和销毁入口。由此可以确认算法实例、通用句柄和结果内存有独立的释放边界。
- 外部库的 Ghidra 过程名主要为 `FUN_...`。PDB GUID 为 `59142c46-1a7c-49be-8caa-d9d45d4bfbe1`，加载状态未知。因此 REA 已闭合外部封装和资源边界，不能把未加载 PDB 的内部函数名或通用算法参数解释成原厂源码事实。

该证据没有闭合阈值分割、标签面积、特征计算、重叠率、像素超界、分类标签和分数的数值规则，也没有提供同输入下的原厂输出黄金样例。现有 HALCON 链只能作为本方算法实现，不得宣称数值等价。

当前代码链位于 `tools/SunEyeVision.BlobLabels/Algorithms/BlobLabelsSession.cs`，执行顺序为：

1. 冻结参数并校验范围。
2. 读取图像、掩膜、检测 ROI、扇环 ROI 和可选 Fixture。
3. 通过 `HalAdapter.Runtime` 转换图像和区域资源。
4. 设置 HALCON 连通性作用域，要求 `max_connection=0`，退出时恢复 `tsp_neighborhood`。
5. 按阈值行进行灰度分割，执行新增/减少和可选标签面积筛选。
6. 连通域分析，读取面积、周长、质心、圆度、矩形度、轴、边界和轮廓。
7. 应用面积、周长、轴、圆度、矩形度、中心偏差和轴比筛选。
8. 按排序特征和排序方式排序，之后应用 `FindCount`。
9. 应用结果判定，生成结果记录、几何显示、二值图和 Blob 图资源。
10. 结果对象持有输出资源，失败路径释放临时资源，Session 关闭后拒绝再次执行。

本方链与原厂边界的直接差异是：本方在 `BlobLabelsSession.Execute` 内用 HALCON 完成 `ProcessAlg` 的内部职责，并在同一方法中完成筛选、排序、FindCount、判定和输出投影。原厂把这些边界拆成 `ProcessAlg`、`LimitJudge`、`ProcessOutput` 三个原生方法，并把核心算法继续委托给外部算法 DLL。当前实现保留了原厂参数和输出声明，但还没有经过原厂输入/输出黄金样例证明数值一致。

### 已验证的本方行为

- 单连通域计数、面积、结果行和显示几何。
- 面积降序排序与 FindCount 截断。
- 面积筛选。
- 空图像的零结果和失败判定。
- 缺少阈值的稳定错误码。
- 未取证的类别信息和重叠率设置会明确失败，不伪造原厂语义。
- 阈值区域编码、解码、追加和参数往返。
- 输出资源释放和 Session 生命周期由现有测试覆盖。

### 已由原厂证据确认、但本方仍未闭合的项目

- `ProcessAlg` 的外部算法 DLL 委托边界已确认，本方 HALCON 结果只能标记为等效目标，不能标记为已等价。
- `LimitJudge` 会保留每个 Blob 的结果并写入逐 Blob 状态缓冲。本方当前通过 `Filter` 先删除筛选不通过的候选，再对保留下来的候选做判定，需用黄金样例确认这是否与原厂 `SelectBy*` 和 `*Limit*` 的分层语义一致。
- `ProcessOutput` 的结果字段集合已确认。本方对 `Scores`、`ClassLabels`、`LabelValues`、`InspectionRectangles`、`InspectionAnnuli` 和屏蔽区输出继续标记为 unavailable，不能用零值填充。
- `OverlapRatio`、`MaxOutPixelNumEnable`、`SelectByOutPixel` 和 `MaxOutPixelNum` 已确认存在于原厂 `SetParam`，本方仍以稳定错误码拒绝未取证语义，这是有意的证据门禁，不是功能已实现。

## 仍未确认的算法事实

以下项目没有同版本原厂源码、原生反编译或运行黄金样例支撑，不能写成已同构：

- 原厂算法 DLL 内的阈值合并、反选和标签面积顺序。
- 原厂 4/8 连通的具体实现和边界行为。
- 原厂面积、周长、轴、圆度、矩形度、中心偏差和轴比的数值定义及取整规则。
- 原厂排序稳定性、同值排序和 `FindCount` 截断时机。
- `ClassInfo`/`ClassLabel`/`LabelValue` 的输入输出映射、`OverlapRatio`、`MaxOutPixelNum` 的运行语义。
- 数量、总面积及逐 Blob 判定的组合规则和状态输出。
- 屏蔽区、扇环区、Fixture 的合并顺序和边界裁剪。
- 二值图、Blob 图、轮廓、结果树和状态资源的原厂所有权与释放时序，以及外部算法实例的销毁时机。

因此本轮不添加所谓“等价修正”。需要原厂动态探针或反编译证据后才能闭合上述差异。

## 构建与测试

```text
dotnet build tools/SunEyeVision.BlobLabels/SunEyeVision.Tool.BlobLabels.csproj -c Release --no-restore -p:SunEyeVisionSkipPayloadSeal=true
0 errors, 2 existing warnings

dotnet test tests/SunEyeVision.Tools.Inspection.Tests/SunEyeVision.Tools.Inspection.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~BlobLabels
30 passed, 0 failed

Check-ToolManifest.ps1 -ToolId BlobLabels
passed
```

该 Release 构建产生的单项目程序集为 `SunEyeVision.Tool.BlobLabels.Runtime.dll`，其中同时存在 Runtime 和 Editor 声明。完整产品封装已在解决方案 Release 构建后执行，载荷只保留该单项目程序集。Agent Desktop 直接启动 Release 成功，窗口标题为“太阳眼视觉 - 机器视觉平台”，强制关闭后确认进程退出。固定单工具脚本本轮未在欢迎页找到 `blob-min`，因此没有把自动打开 BlobLabels 窗口记为通过，不能替代你已完成的人工 UI 确认。

本轮复核后再次执行完整 Release 封存，结果为 `Status=passed`、`Errors=[]`、`Warnings=[]`（编译器汇总仍有既有 XML/未使用变量警告、0 个错误），最新载荷封存哈希为 `5CDC77BBD3004318EA47A8EDF00AE833BDD06F008C413D0BC42C61AC9594C5BE`。BlobLabels 定向测试为 30/30 通过，发布清单复核为 `Status=passed`。

### 本轮算法映射修正

原厂 `ProcessOutput` 在同一结果链上分别写出 `Angle` 和 `BlobAngle`。结合 `MinBoudingRect` 的 `ROIBOX` 结构以及已验证 Blob 工具的几何映射，本方现在保持两个角度的来源分离：`SmallestRectangle2` 的角度继续进入 `MinimumBoundingRectangles[*].Angle`，`OrientationRegion` 的区域主方向进入顶层 `BlobAngles[*]`。此前把外接矩形角度同时投影为顶层 BlobAngle 的错误已修正。

本方当前仍保留三类有证据边界的未闭合项。`OverlapRatio`、`MaxOutPixelNum`、`ClassLabel/LabelValue` 的算法语义未从外部算法库闭合，继续在执行前给出明确诊断。`ContourOutputEnabled` 已由 `SetParam("BolbOutLineEnable")` 写入 native 字段 `this+0x15e88`，`ProcessOutput` 也读取该字段。当前仍未把“有 Blob/无 Blob”两条输出路径与本方轮廓投影逐值闭合，因此不擅自改变轮廓结果行为。外部算法库的实例、句柄、结果图像和 working/result memory 释放入口已取证，精确的跨层释放时序仍待原厂运行探针确认。

最新 Release 通过 Agent Desktop 启动验证，窗口标题为 `SunEyeVision · 起始页`，窗口可访问、可见且获得焦点，随后强制关闭并确认进程退出。该验证只覆盖程序启动和关闭，不宣称自动打开 BlobLabels 调试窗口。

## 下一步闭口条件

1. 对已取证的 `MVDMultiCategoryBlobFindCpp.dll` 补齐同版本 PDB 或可重复的原厂运行样例，闭合外部算法内部语义。
2. 用原厂输入图、参数和输出建立黄金样例，逐项比较几何、排序、判定、逐 Blob 状态和输出资源。
3. 复核 BlobLabels 结果页的完整动态行和资源释放探针。
4. 完成最终解决方案 Release 封存和 Agent Desktop 启动/打开 BlobLabels/关闭验证。
5. 在上述证据完成前，BlobLabels 只能标记为“单项目结构和本方算法测试通过，原厂算法等价未确认”，不能进入下一工具的算法闭口。

## Release Agent Desktop 运行探针

- 六工具精确节点名批量探针已通过，Blob 标签分析窗口成功打开并关闭。
- 结果记录在 `docs/evidence/six-tool-agentdesktop-runtime-20261121.json`，对应运行目录为 `Release/_validation/AgentDesktopToolReview/batch-all/20261120-001458-Blob标签分析1`。
- 该探针确认窗口准入和关闭链路，不替代 HALCON 与原厂算法的数值等价结论。



