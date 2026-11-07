# 非标定算法批次收尾核对交接

- 基线提交：a444b21325eeb6b121cfe6bd059383b65eb172eb。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入，保留全部并存修改，未提交推送。
- 任务目标：核对除标定外的计划批次，补齐遗漏，区分代码完成与人工验收，保留绑定、输出及屏蔽。
- 允许修改目录：本地HALCON方案与docs/collaboration；本轮未发现需补写的计划功能，不修改源码。
- 已修改文件：全算法工具HALCON标准流程分批优化方案-20261015.md、CURRENT_TASK.md及本交接。

## 结论与范围

本地方案约定的非标定代码实施及Triangle等价性审计均已完成；本轮对照执行路径未发现尚未实现的计划条目。这里的完成不包含真实图像、设计器、运行交互或全部边界验收，不保证程序没有其他运行缺陷。本轮补齐统一覆盖表、兼容边界与验收清单，并纠正首批B仍写“后续完善屏蔽解析”的过期描述。

14个视觉工具已覆盖：LineDetect、CircleDetect、LineLineMeasure、LineCircleMeasure、CircleCircleMeasure、PointLineMeasure、PointCircleMeasure、PointPointMeasure、Threshold、ImageThreshold、ImageFilter、Morphology、BlobAnalysis和ShapeMatch。图像源、通信、协议、条件、子程序和用户脚本不属于视觉算法替换范围。NPointCalibration/NPointVerification标定未实施，保持原模型与数据，不进入本轮完成结论。

## 代码核对依据

| 批次 | 当前执行代码核对 | 实施记录 |
| --- | --- | --- |
| 卡尺A | 七工具十槽位调用MeasuredLineFitting.Capture及线/圆Fit；原生取点、轮廓拟合、固定距离筛点和诊断已接入，工具无最终Metrology模型/min_score消费 | HANDOFF_CALIPER_AVAILABLE_POINTS_20261015.md |
| 屏蔽B | ToolRegionHelper运行时解析与点过滤、扇环原生角度转换、整圆分支，查找工具拟合前过滤 | HANDOFF_MASK_THRESHOLD_FILTER_20261015.md及HANDOFF_MASK_FILTER_MORPHOLOGY_20261016.md |
| 阈值 | HalconThresholdOperations有效域反转、DynThreshold及灰度Render，ImageThreshold保留Triangle=16 | 上述屏蔽/阈值交接及HANDOFF_TRIANGLE_NATIVE_AUDIT_20261016.md |
| 滤波及任意核 | HalconLinearFilters原生卷积/类型恢复；ImageFilter新增默认关闭的任意核入口、矩阵验证、显式除数及原Sharpen保留路径 | HANDOFF_IMAGEFILTER_CUSTOM_KERNEL_20261016.md |
| 形态学 | 原生矩形/十字/椭圆、七种操作与Iterations、结果Intersection有效域 | HANDOFF_MASK_FILTER_MORPHOLOGY_20261016.md |
| Blob | 原生SelectShape and筛选、排序/截取后统一索引与标签、预处理及形态学限域 | HANDOFF_BLOB_SHAPEMATCH_20261016.md |
| ShapeMatch | 原生创建对比度分组，FindScaledShapeModels参数/索引、单组单模板路径、原姿态构造及排序去重保留 | HANDOFF_BLOB_SHAPEMATCH_20261016.md及HANDOFF_SHAPEMATCH_JOINT_SEARCH_20261016.md |
| 点类规范 | PointMeasurementGeometry及点点/点线/点圆有限值和单点校验、稳定判定键 | HANDOFF_POINT_GEOMETRY_RECONNECT_LIMIT_20261016.md |
| 配套失效隔离 | WorkflowExecutionDependencyGraph.InputIssues、调度传入问题集合及节点拒绝执行、成功输出发布前清除本轮旧变量值 | HANDOFF_BINDING_FAILURE_ISOLATION_20261016.md |

屏蔽专项只规范工具已有能力，不向原本无屏蔽入口的测量工具新增配置。共享NativeMetrologyResults历史帮助类仍在仓库，不作为当前工具最终模型成功条件；没有为清单整洁而删除公共接口。普通距离、点投影、点排序/筛点及业务去重保留，不为形式“全原生”作不等价替换。

## 发布核对与构建状态

本轮读取并比较14个相关工具插件、SDK、HalAdapter、UI及已有插件目录共享依赖副本SHA256，均与build_temp/obj各自Release/net9.0-windows编译DLL一致。Blob项目目录为SunEyeVision.BlobAnalysis，但csproj和程序集为SunEyeVision.Tool.BlobAnalysis；最初按目录名检查显示未命中，核实正确身份后发布与编译DLL一致，不是缺少部署。

最近一次全量Release1165警告0错误、86.82秒，独立UI1069警告0错误、16.65秒，来自上一任意核批次；本轮仅更新文档，没有新构建或新发布。不把旧构建结果表述为本次执行。标定源码无diff；本轮没有修改绑定、输出或屏蔽代码。

## 待人工验收

具体步骤和预期结果见[人工验收操作手册](ALGORITHM_MANUAL_ACCEPTANCE_STEPS_20261016.md)。39个用例定义按14个工具分组，C01至C06须逐工具执行或说明不适用，不以共用步骤已写好替代实际验收。所有用例初始状态为未执行。

遵前序限制，未运行测试、Release程序或Visual Studio设计器。以下统一清单尚无本轮人工通过记录：

1. 原方案与新方案的保存/重开、已有X[]/X[n]及图形/参数绑定；工作流和节点调试使用相同输入时结果一致。
2. 七个卡尺工具的扫描方向、边缘极性、真实候选点、部分无边缘、退化点诊断，整圆/圆弧输出及直线显示。
3. 已有屏蔽的本地/订阅/拼装、全屏蔽、扇环跨零角、位置修正一次和边界离散化差异。
4. 固定/自适应/反转/截断/置零/范围/Otsu及兼容Triangle原图效果，输出图像/掩码/阈值/统计与位深含义。
5. 均值/高斯/中值/双边、自定义核及默认3×3兼容；非方/偶数/非对称/零和核、除数、原生参考点、类型量化、边界及有效域。
6. 形态学核形状、七操作及迭代；Blob筛选、等值稳定排序、数量截取、轮廓开关、区域/数值索引、OK/NG。
7. 单/双/多模板、创建对比度分组、每模板数量、显式层级、超时、姿态/矩阵/轮廓和原生参考点domain屏蔽。
8. 绑定失效时合法上游及无关分支继续、问题节点及依赖下游失败/跳过，不消费历史值；具体旧失效输出身份原因需原方案证据，不能按同名自动回绑。

Triangle保留兼容实现、ShapeMatch跨模板AABB业务去重、domain仅约束参考点、滤波屏蔽不阻断邻域读取以及整数恢复量化均为已确认兼容边界，不是待补写的功能。PerModelOverrides未接入的覆盖功能仍不在当前方案内擅自启用。

- 已完成：既定非标定代码范围、Triangle审计、统一收尾状态、当前发布一致性核对。
- 未完成：上述人工验收；独立标定按要求未实施。
- 已知风险或禁止改动：不得将构建或静态核对等同实际效果通过，不自动放宽阈值/支持度、不改旧绑定/输出/屏蔽，不恢复通信扩展、不动标定。
- 下一步：按上述验收项记录真实结果，发现可复现问题再修复；本地方案的非标定实现不再自动扩批。
