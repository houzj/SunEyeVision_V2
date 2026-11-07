# 多模板联合搜索算法批次交接

- 基线提交：a444b21325eeb6b121cfe6bd059383b65eb172eb。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入，保留全部并存修改，未提交推送。
- 任务目标：通讯收尾后核对算法批次，推进HALCON原生多模板联合搜索，保留绑定、输出契约和屏蔽，不动标定。
- 允许修改目录：tools/SunEyeVision.Tool.ShapeMatch、本地算法方案与docs/collaboration。
- 已修改文件：ShapeMatchTool.cs、ShapeMatchParameters.cs、Models/TemplateModel.cs、Views/ShapeMatchToolDebugControl.xaml、本地HALCON分批方案、CURRENT_TASK.md及本交接。

## 批次核对与本批完成项

第一批卡尺和屏蔽、第二批阈值和滤波、第三批形态学/Blob/ShapeMatch基础整改，以及点类测量规范已有实现和构建记录。当前代码仍为HALCON取点后轮廓拟合，不恢复Metrology支持率门槛；本轮没有重复改写这些算法。各批原图和交互验收仍待人工。

本批将兼容模型合并为FindScaledShapeModels调用。以GetShapeModelParams实际返回的创建期MinContrast分组，避免联合接口使用最小值而隐式降低其他模板门槛；组内仅一个模型时调用原FindScaledShapeModel。NumMatches和MaxOverlap使用每模型元组，保持原每模板候选数量、同模板原生重叠和跨模板AABB业务去重规则，不改为全局数量预算或另一种旋转框去重。

显式层级裁剪到每个模型实际层数，以[start0,end0,start1,end1,...]传入；恰好两个模板也使用四元素而非有歧义的双元素。自动层级仍为0。原生返回Model零基索引用于模板身份映射；元组长度、索引范围及姿态有限性检查失败时明确报错，不重试旧算法、不降低门槛。

共享缓存锁覆盖所有准备、搜索、timeout变更和轮廓读取；加载前拒绝空/重复模板身份，避免同ID不同内容替换已准备句柄。输出仍走同一个姿态转换和结果构造出口，合并结果恢复模板原顺序以维持等分排序口径。轮廓原生对象和元组通过using在正常和异常路径释放。

## 兼容边界与使用差异

模板模型数据、序列化字段和默认值、输出字段/集合、绑定路径、位置修正、轮廓/矩阵坐标和排序截取不变。PerModelOverrides既有字段保留，本轮不擅自激活此前未接入的覆盖功能。搜索仍使用检测区减屏蔽区后的domain限制模型参考点，不改成像素涂黑或轮廓覆盖屏蔽。

MatchTimeoutMs现在约束每次原生联合搜索，同组共享一次预算，不再让该组每个模板分别耗尽完整预算。界面超时悬停提示、日志及本地方案已说明。不同MinContrast组仍分别调用，预算不等于整节点总耗时硬截止；HALCON超时后清理可能额外耗时。不承诺未经实测的加速比例或原始结果逐位一致。当前应用创建模型使用默认边界和无clutter流程，本批不加入异构特殊模型导入能力。

原生依据：[FindScaledShapeModels参数、层级、数量、重叠和MinContrast规则](https://www.mvtec.com/doc/halcon/2405/en/find_scaled_shape_models.html)。

## 构建和发布

- ShapeMatch插件首轮Release：18警告、0错误，6.42秒。
- 全量Release --no-restore -m:1 -clp:ErrorsOnly：1162警告、0错误，82.90秒。
- 独立UI Release：1069警告、0错误，16.79秒。
- PointLineMeasure按既有规则刷新目录内旧SDK/HalAdapter依赖：0警告、0错误，4.05秒；不改发布或插件加载规则。
- ShapeMatch及UI发布主DLL与build_temp编译DLL SHA256一致，相关diff检查通过；标定源码无diff。

## 待人工验收和下一步

遵前序用户限制，未运行测试、Release程序或设计器，不能将构建成功视为算法效果通过。

待人工验收：单模板及零模板、恰好双模板不同模型层数、三模板及以上、NumMatches=0/1/多值、同/不同MinContrast、等分排序及跨模板去重、模板身份和姿态/矩阵/轮廓绑定、订阅屏蔽和位置修正、联合超时及两入口一致性。未连接任何通讯设备。

通信不再扩展；本轮没有修改通信、工作流调度、SDK或标定。后续Triangle原生等价替换与任意卷积核编辑仍需专项定义，不因当前构建通过记作完成；标定继续独立处理。
