# 下一段 ROI 实施输入

2026-10-26，主对话只读复核；算法路仍为唯一产品写者。本文件是实施接续依据，不代表ROI已实现或通过。

已批准界面需接通矩形区域、矩形参数、圆区域、圆参数；像素Region保持独立，不能改名充数。VM用户截图证明这些界面选项和展开状态；不据此推定原生单位、集合广播或错误策略。

本次新核实的现有链：

- `DeclaredToolConfigurationControl.RegionSources.cs`只枚举`IsSubscribed`，没有`IsComposed`。`Resolve()`拒绝显式Index，`Setting()`只生成根输出引用，因此不能仅增加四个单选按钮。
- `DeclaredToolConfigurationControl.Sources.cs`的Geometry候选只收`RoiShape[]`，不收圆检测的`Circle2d[]`。实际`CircleDetectResults.Circle/Center`已正式声明；新区域继承应明确承接相应的几何类型和中立转换，不能用反射结果活值推导候选。
- `RoiFieldSchema`已有矩形中心/宽高/角度、圆中心/半径的正式字段线索；`RoiComposeResolver`支持标量和数组，但缺失值/解析失败会退回默认形状，且字符串被宽松解析。新模式必须验证必需字段、有限值、正尺寸、实际类型与数组长度，失败不可造默认100×100/半径50。
- 中立`BindingSelection`目前仅All/AtConstant/AtField，没有按形状类型过滤；`WorkflowBindingContext`对旧`[Type=...]`解析不自动转typed计划。不能把旧字符串筛选路径直接写入新typed引用并声称已消费。整集合形状过滤或明确元素选择必须设计、执行与测试一致。
- 算法路复核还定位两个实际执行出口：`RoiInput.ResolveSubscription`目前只接`RoiShape[]`/单个`RoiShape`/`Rect`，并不接`Circle2d[]`；`RoiInput.ResolveFlatParameterRaw`也吞解析失败为null。须连同拼装器一起承接或严格化，不能只修备用入口。`BindingInputCatalog`把每个拼装参数固定声明为`double[]`，标量广播需同时得到真实typed目标合同支持。
- 三种现有存储来源`Roi.Source`配合`DeclaredShapeType`、`Binding`、`ParameterBindings`可以承载四种继承的形状×来源模式，界面无需另存坐标。标量广播、数组等长、长度1数组与空成功集合的行为须在实施时明定并验证；空集合不可与缺字段或失败引用混淆。

同帧检查有可复用的真实证据入口：

- `Plugin.SDK/Execution/Helpers/ImageFrameProvenance.cs`已有弱引用元数据，`GetInputFrame(ToolResults)`可取某工具本次输入的物理来源Guid，`CaptureInput(IVisionImage)`可取当前SDK输入来源。未知来源返回null，不推断。
- `ToolExecutor.Admission.RunSynchronousCore()`在真实准备输入后捕获Frame并`RecordResult()`；不是仅有界面缓存。新几何/参数继承可以审查此入口的可用性，并同时验证来源属于本次正式执行。
- 这个物理Guid与中立`IReadOnlyImage.Frame`不能直接混为一物：`WorkflowBindingContext.FreezeResult`给传统图像输出建立的是声明捕获地址。参数/几何来源可在SDK边界用双方已知物理来源比较；像素Region仍走现有中立完整Frame严格准入。无证明时不能只因都是double就放行，更不能放宽像素Region检查。
- 常量是用户明确给当前图像的配置值；来源为其他节点的数值应明确坐标/单位和同帧策略。尺寸与位置是否需要相同来源证明，应按现有可证能力制定，未证明的能力不能假称已经支持。
- 来源还须经`BindingRunSnapshot`验证本次run身份，物理Guid相同不能放过同来源旧run；几何值的`CoordinateFrame`只表坐标语义，不能代替物理帧证明。

交回产品写权后应按一个完整接缝实施：声明/稳定字段与显隐 → 当前Roi存储映射和活动绑定 → typed候选及明确选择 → 严格拼装与帧校验 → 草稿/执行/保存/删除来源 → 界面同态截图及正式回归。复用已有Roi和几何真值，不另存第二份坐标；不新造与实际消费无关的配置树。

必要验收至少包含检测区和屏蔽区的四种继承、混合常量/引用、缺字段/失败引用/NaN/非正尺寸、数组空值/长度与显式索引、同帧/错帧/过期来源、模式切换/确定取消、保存重开、实际结果联动与位置修正仅一次。仍需核实际源码及正式Release；此清单不能代替测试结果。
