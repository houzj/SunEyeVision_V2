# AI 协作交接单 HALCON 原生流程第一批 A

- 基线提交：ce71b606f83331f6d508147ec060cd3ce46bf6d1。
- 当前分支：feature/comm。
- 当前执行者：Codex单写入，无跨AI交接，无提交推送。
- 任务目标：先保存全算法工具分批方案，再实施标准流程迁移；保留绑定、输出契约及屏蔽功能，标定独立处理。
- 允许修改目录：SDK、HalAdapter、7个卡尺工具、方案及协作文档。
- 已修改文件：新增全算法工具HALCON标准流程分批优化方案-20261015.md及NativeMetrologyResults.cs；EdgeFitDiagnostics、MeasuredLineFitting、MeasuredCircleFitting；LineDetect、CircleDetect、LineLineMeasure、LineCircleMeasure、CircleCircleMeasure、PointLineMeasure、PointCircleMeasure相关Tool／Results／Parameters／XAML。

## 已完成

七个工具、十个卡尺槽位的无屏蔽场景读取原生Metrology最终模型，不再重新拟合。共五条直线、五个圆槽位，圆查找同时覆盖圆弧模式。NativeMetrologyResults统一读取all_param、score、used_edges行列坐标和实际min_score／distance_threshold；拒绝空模型、非有限模型、无效半径／端点及数量违约，不偷偷截取多个实例。默认原生单实例与支持度未放宽。

既有RMSE字段保留，使用原生实际拟合点到最终模型的距离计算，仅为输出统计，不反馈筛点或重拟合。直线端点只按引导方向定向；图层继续显示一条到本次图像边界的直线。圆弧继续按既有显示系引导扫掠角输出，不用无序采样点首末值改变弧方向。

所有卡尺工具共用原始点及实际测量带采集，圆工具不再吞取点异常；圆查找、圆圆、点圆新增失败前保存的诊断及失败图层，线圆删除圆的重复拟合实现。双元素工具先采集两组点再读取模型。结果树诊断显示原生路径、分数、门槛及失败阶段；未采用点仅供诊断，不反馈算法。已有结果字段静态核对均未删除；原绑定路径、字段类型、数组语义及位置修正入口不变，未改plans/plan.json。

直线／圆查找启用屏蔽时，保留原点过滤及HALCON拟合兼容路径，并明确标记屏蔽兼容HALCON拟合。此前未接入的MeasuredCircleFitting仅用于这一例外，非无屏蔽主路径。其点集校验、去重、数值退化和模型有效性检查不改变用户距离阈值；兼容路径现有屏蔽几何支持范围不扩大，也不静默删除。原生Metrology忽略domain，未用ReduceDomain假冒屏蔽。

高级参数页统一为高级拟合参数，不让仅含距离阈值的折叠组冒充一套独立RANSAC配置。旧参数配置赋值只补缺诊断样式，保留原样式与判断项。原生没有有效模型时保持失败，不伪造零值或重用旧结果；前批失败结果传递和下游保护保持。

## 构建与验收

- 已执行构建与测试：最初构建发现HALCON结果读取重载二义性，改显式index.I；再构建发现圆圆／点圆参数类缺少ArgumentNullException命名空间，已限定为System.ArgumentNullException。最终全量Release成功0错误1262警告（77.59秒），独立UI Release成功0错误1065警告（21.16秒）。
- 7个修改插件主DLL及宿主SDK／HalAdapter／Workflow／UI均与Release编译中间DLL的SHA256一致；静态结果字段核对无原字段删除，diff检查通过，只有换行提示。
- 人工验收结果：暂无。遵用户前序要求不新增／运行测试，不启动Release或VS设计器，不操作真实方案或设备；编译及哈希不等于真实图像验证。
- 未完成：第一批B屏蔽专项、第二批阈值／滤波语义、第三批形态学／Blob／匹配、独立标定模型评估及真实图像验收。
- 已知风险或禁止改动：原生min_score和used_edges会使旧方案数值／点数／是否成功发生变化，不承诺数值等价；不能为恢复旧成功率降低门槛。禁止删除屏蔽功能、擅改输出数组或自动改绑定索引。标定源码、数据和设备均未修改。
- 下一步：先确认屏蔽兼容例外的可替代原生能力，同时按主方案进入阈值／滤波的参数语义修复；不能把第一批A完成写成全量工具已迁移。
