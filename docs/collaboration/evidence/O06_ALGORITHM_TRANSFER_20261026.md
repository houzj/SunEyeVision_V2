# O06 算法职责迁移实施核查

2026-10-26；作者 blob_algorithm_scope。只读源码分析，基线提交 `24cd4ffd5993ad31582c81a0b11a82663b013ec2` 加主对话进行中的工作区增量；不是冻结源码或正式产物测试报告。当前产品唯一写者为 /root。本路仅写本文件，未改产品、构建、运行或提交；完成后释放本文件写权。

## 推荐闭合路径与首要错误

最短完整链：ImageFilter.OutputImage → ImageThreshold.BinaryMask → 严格二值 Morphology.BinaryMask → Blob“不二值化”。Blob 同时提供明确的单阈值、双阈值策略。ROI 始终是执行范围，不代替分割数据；本路径不需要新增含糊的分割 Region 输入。

- Blob 当前 `Threshold/MaxValue` 实际送入 HALCON `Threshold(Threshold,MaxValue)`，是灰度选取区间上下限；参数注释却把 MaxValue 称为输出亮值。ImageThreshold.MaxValue 才是渲染亮值。新合同必须分清，不能直接沿用同一解释。
- Morphology 当前先固定 `Threshold(...,128,"max")`，不能直接充当迁出后的专用二值形态学。
- ImageThreshold.OutputImage 经 Render，受 MaxValue 与 Trunc/ToZero 影响；只有其 BinaryMask 明确固定 0/255。严格二值链应显式选择 BinaryMask。
- ImageFilter 无直方图均衡化；Blob Circle 与 Morphology Ellipse 尺寸语义不同；形态学枚举数值也不同。迁出时必须承接，不能无声删功能或枚举强转。

## 逐文件最小不丢功能清单

以下路径均相对主仓。每项现状来自实际读取源码；推荐变更尚未实施。

| 文件/证据 | 现状与推荐变更 | 必要验证 / VM 未知边界 |
| --- | --- | --- |
| `tools/SunEyeVision.BlobAnalysis/Runtime/BlobSettings.cs` | 现有 Binary/BinaryInv 只是极性，且内含通用滤波/形态学。新增独立 Single/Range/None 分割策略，极性单列；阈值命名表达真实含义。移出通用设置/枚举，按活动模式验证。 | 单阈值边界、范围等值与逆序、未活动字段不误阻塞；VM 原生端点公式未知。 |
| `Runtime/BlobSession.cs` | 当前 Gray→ROI→预处理→区间阈值→形态学→连接。改 Gray/严格二值准入→ROI→明确分割→连接；保留有效域、筛选、排序限数、判定、按需输出。建议 Single 为 HALCON `[T,255]`，Range 为 `[min,max]`，反向为有效域差集；这是本方明确合同，不称 VM 等价。 | ROI/Domain/Mask/空区域、取消、反向不越域；VM 阈值和统计原生链未知。 |
| `Runtime/BlobSession.Operations.cs` | 删除迁出的 ApplyPreprocessing、形态学及结构元素实现；保留 SelectRegions、EmptyRegion、旋转矩形等 Blob 必需函数。 | 多孔边界、最终集合、矩形方向与现有回归不退化。 |
| `BlobAnalysisParameters.cs` | 同步删除通用参数、运行描述、验证、旧选项方法/后备字段；加入新分割策略和活动字段规则。 | 克隆/草稿/快照、常量/订阅、默认值和保存重开；不保留双轨执行。 |
| `BlobInputContract.cs`、`BlobConfigurationFields.cs`、`Runtime/BlobPresentation.cs` | 同步 Entries、Groups、稳定字段声明、默认字面量、名称、条件和配对布局。旧字段直接反射 CLR 路径，删属性不删表会使 FindSlot 失败。 | 声明唯一、全部字段读写、非法字面量、切模式及保存；与主对话 UI 写入协调。 |
| `BlobSdkAdapter.cs` | Settings 显式映射新策略；CopyResult 的实际阈值/策略回写同步。不得把不二值化伪报为阈值128。 | 现行入口和中立 Runtime 对同输入输出一致。 |
| `Runtime/BlobOutput.cs`、`Runtime/BlobSchema.cs`、`BlobOutputContract.cs`、`BlobAnalysisResults.cs` | 回写“实际策略、有效阈值”的语义同步；无阈值应明确未使用。若使用 Nullable/新增字段，声明、消费者和保存合同一起改，不暗改旧字段含义。 | typed consumer、输出树、请求输出、资源生命周期；原有 Results 外部修改由主对话保留并整合。 |
| `tools/SunEyeVision.Tool.Morphology/MorphologyTool.cs` | 去掉灰度转换和固定128分割，改 Gray8 严格0/255准入、提取255；形态学后继续与有效域相交。保留全部现有操作和结果判断。 | 1/127/254拒绝、RGB/BGRA拒绝、孔洞/边缘/屏蔽区/多次迭代；VM 结构元素锚点与迭代公式未知。 |
| `MorphologyParameters.cs`、`MorphologyResults.cs` 及对应 Views/Converters | 现有 Rect/Cross/Ellipse 不能假称承接 Circle。为保留 Blob 圆形能力，显式增加 Circle 或给出经批准的新尺寸语义；参数、选项、显示、保存同步。OutputImage/BinaryMask 当前均是固定0/255，可保持。 | 圆/椭圆/矩形/十字及1像素轴；保存后操作/形状不漂移。 |
| `tools/SunEyeVision.Tool.ImageFilter/ImageFilterParameters.cs`、`ImageFilterTool.cs` 及 Views/Converters | 现有 Blur/Gaussian/MedianBlur/Bilateral/Filter2D；补 HistogramEqualization 承接 Blob 的 EquHistoImage，保留已有滤波能力。均值/高斯/中值可复用，但保持全图邻域上下文与最终有效域限制。 | 四种迁出预处理、ROI边界、输入不变；均衡化按有效域还是全图统计必须明确并验证，不能假称 VM 等价。 |
| `tools/SunEyeVision.Tool.ImageThreshold/ImageThresholdTool.cs`、`ImageThresholdResults.cs`、`src/SunEyeVision.HalAdapter/HalconThresholdOperations.cs` | 已有 InRange 与二值掩码；BinaryMask 固定255/0，OutputImage 不保证。可复用，不把 Trunc/ToZero 改成二值；完整链明确来源字段。 | MaxValue=200时 OutputImage被严格链拒绝、BinaryMask通过；端点与反向独立断言。 |
| 三个上游工具的灰度转换 | 当前均把3/4通道直接送 Rgb1ToGray；若完整链覆盖BGRA，应复用 Blob 的前三色通道组成RGB、Alpha不参与亮度的处理。Morphology严格二值模式应直接拒绝彩色。 | BGR非等通道、BGRA不同Alpha、Domain和Frame保持；仅源码不能代运行。 |
| `src/Workflow/Persistence/SolutionLegacyBindingMigration.cs`、现行保存校验消费者 | 当前仅支持明确声明和旧Blob别名，不会推导新分割策略。移除参数后旧配置/引用必须明确迁移或诊断拒绝，不得静默丢失已启用预处理/形态学。若不需要旧开发方案兼容，可直接拒绝并提示重新配置。 | 旧字段/旧绑定不被静默吞掉，新三模式保存重开，来源删除与重新选择。 |

显式枚举差异：Blob `TopHat=4, BottomHat=5`；Morphology `Gradient=4, TopHat=5, BlackHat=6`。Blob Circle 使用 `radius=max(width,height)/2`；Morphology Ellipse 使用 `rx=width/2, ry=height/2` 且一像素轴退为线/点。迁移必须按名称与业务语义映射。

## 严格二值准入、消费与所有权

1. None 模式先检查原始输入像素格式为 Gray8，再检查约定域内所有像素仅0/255；不能先转灰度、归一化或按128/非零选取后再声称严格。不接受彩色伪二值和非8位。建议检查输入实际 Domain 内全部像素，再与 ROI 组合，避免 ROI 改动改变输入合法性；若最终产品改为只检查有效执行域，须明确写入合同和测试。有效空域可接受为空，检查过程支持取消。
2. Blob `BlobInputAdapter.CaptureImage()` 优先用 DeclaredOutput reader 取得 IReadOnlyImage 并 RetainInput，否则捕获旧 ResolvedInputImage。需分别覆盖声明资源路径与现行图像来源路径，不只测直接调用。
3. 上游结果由 GetPropertyMap 声明，GetDisplayImages 提供图像；三个工具参数均为 JsonDerivedType 的 RoiParameters。要验证真实工作流来源引用与保存，不只依赖结果属性存在。
4. `InspectionGeometries/MaskGeometries` 是配置几何，Blob `input.inspection.region` 是执行范围；不能把这些当作阈值分割结果。现有继承 Region 准入要求 SourceFrame 与坐标框架一致；空订阅不得回退满图，已修正的像素区域不得再次变换。
5. 保留 Blob 独立 output scope、输入不变、reader 延后释放与失败清理。上游传统工具异常可能返回已创建部分图像；下游应拒绝失败来源，防止消费部分输出。

## 输出口径保持或显式调整

- 二值图来自筛选前分割；最终 Region、OK/NG、测量数组、统计和显示使用同一筛选/排序/限数集合；NG 不被筛除。
- 白像素比例仍是分割白像素数 / 全图宽高，不能顺手改成ROI分母或最终Blob面积。
- GrayImage 当前是预处理前灰度；Histogram/ChannelMeans 当前对分割区域中的 processed 灰度计算。移出滤波后应明确变为所选输入灰度；若输入为二值链，灰度统计自然成为二值数据，不能冒称原图灰度统计。需要原图灰度统计时应另有明确来源合同，不能暗取其他节点。
- 保留完整多环/孔洞 boundary；单 contour 仍是有损外环投影及诊断。输出需求可覆盖显示开关。

## 正式Release验证路径

实施完成后由主对话全量构建并冻结受测身份，所有程序及算法DLL使用 `Release/SunEyeVision_v1.0.0/SunEyeVision`；测试数据/日志置于 Release 下，不复制payload。以下是可执行现有回归入口，尚未由本路执行；旧用例随新合同调整，不能为保旧测试留双轨实现。

```powershell
dotnet build SunEyeVision.slnx -c Release --no-restore
powershell -File scripts/verification/Run-S2ReleaseValidation.ps1 -Stage S3 -Track RuntimeKernel
powershell -File scripts/verification/Run-S2ReleaseValidation.ps1 -Stage S3 -Track Consumption
powershell -File scripts/verification/Run-S2ReleaseValidation.ps1 -Stage S3 -Track InputBoundaries
powershell -File scripts/verification/Run-S2ReleaseValidation.ps1 -Stage S3 -Track Related
powershell -File scripts/verification/Run-S2ReleaseValidation.ps1 -Stage S4 -Track FullChain
powershell -File scripts/verification/Run-S1ReleaseValidation.ps1 -Tracks Fields,Workflow,Snapshot
```

新增必要断言建议落入 `scripts/verification/S3RuntimeKernelCases.cs`、`S3InputBoundaryCases.cs`、`S3ConsumptionCases.cs`、`S4FullChainCases.cs` 或同规则的关联工具验证，不复制产品：

- `[0,T-1,T,T+1,255]` 验单阈值及反向；`[min-1,min,max,max+1]` 验范围、等阈值和逆序拒绝。
- None接受全0、全255、混合0/255；拒绝1/127/254、RGB/BGRA、非8位，含Domain与ROI内外非法值约定。
- Threshold.MaxValue=200：选OutputImage拒绝、选BinaryMask通过；ImageFilter→Threshold→Morphology→Blob真实来源绑定与保存重开。
- 单点、细桥、孔洞、边缘和mask夹缝验证全部形态学操作、多次迭代及结构元素；膨胀不得恢复屏蔽像素。
- 最终集合限数、NG保留、多孔边界、需求生成、全图分母；空ROI、错帧、位置修正、取消、失败来源、重复运行与reader延后释放。
- 新字段常量/订阅切换、草稿确认/取消、撤销重做、非法保存、来源删除；BGR/BGRA灰度转换和输入不变。

## 工作量与未做范围

按单一产品写者、复用现有执行/资源框架估计：Blob合同和算法迁移约1.5–3小时；上游均衡化、严格形态学和圆形承接约1–2小时；有意义的Release整链/保存/生命周期回归及修复约1.5–3小时。合计约4–8小时工程量，取决于新保存策略、BGRA接缝及实际失败；这是范围估计，不是完成承诺。UI视觉重组、实际VS设计器、用户人工验收和新增VM取证不计入。

本路没有新产品PASS。VM单阈值端点、原生阈值/形态学公式、结构元素锚点/尺寸、迭代、灰度统计与不二值化输入准入仍未知；XML、截图与本方HALCON实现不能证明等价。优先采用可说明的本方合同，只有影响已批准需求时再按取证约定补证。阶段实施、验证、效果和未做范围由主对话统一回原O06，不以本报告代替主文档核收。
