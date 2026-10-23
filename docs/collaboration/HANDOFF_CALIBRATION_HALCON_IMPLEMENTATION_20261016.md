# HALCON原生9加3标定实施交接

- 基线提交：3a5dc6dd54f372699a450ad7862b52c961cd2797。
- 当前分支：main，工作区D:/MyWork/SunEyeVision_V2；未创建提交或推送。
- 当前执行者：Codex，唯一写入者。
- 任务目标：按本地原生9+3方案同时调整算法与UI，构建Release。
- 允许修改目录：相关Plugin.SDK、HalAdapter、N点标定插件、标定测试及本地方案/交接；未修改通讯工作区。

## 修改和完成范围

- Plugin.SDK新增Models/Calibration/PlanarCalibration.cs纯数据模型，ToolExecutionRequest新增CalibrationEvidence可选入口。跨模块改动为加法契约，不引入HALCON句柄或UI调度。
- HalAdapter新增HalconPlanarCalibration：原生九点辅助仿射、实际编码器R角的两参数偏心求解、最终点变换。检查有限值、几何退化、R跨度、轴心漂移、参考复拍和完整残差，确定释放原生资源。
- 删除tools/SunEyeVision.Tool.NPointCalibration/NPointCalibrationSolver.cs自写QR/IRLS；移除权重与稳健尺度参数。文件可从Git基线恢复，但不作为产品运行回退。
- 更新NPointCalibrationParameters/Results/Tool/File及NPointVerificationTool，新增CalibrationRuntime和NPointSampleCsv。保留主输出键，训练与独立验证分开；候选和正式文件分离、原子发布并保留备份、内容摘要/配置指纹/取消保护。
- 两个工具各三个Tab调整，点表编辑及逐点误差、文件/CSV/清空/发布提示更新；新CalibrationDialogs使用SDK资源，不引用UI宿主。六Tab离线图像保存在artifacts/validation/calibration-native-ui。
- 新草稿/CSV/候选文件版本3，算法HalconAffineEncoderOffsetV1。旧格式明确拒绝，不自动兼容、不覆盖旧文件。点表清空不删除活动文件；停用点保留审计。
- 插件项目增加HalAdapter引用并更新插件/测试锁图，没有升级外部NuGet版本。35项回归替换旧稳健求解测试，覆盖原生数值、负向门槛、证据、取消/过期、CSV、候选隔离及独立发布。

## 构建与测试

| 检查 | 结果及证据 |
|---|---|
| 标定Release回归 | 35通过、0失败、0跳过，artifacts/validation/calibration-native-tests/calibration-native-release.trx |
| WPF离线检查 | STA加载六Tab、手动/触发与示教显隐、12行点表；六张渲染图已目视检查，修正标签宽度及外部结果状态文字 |
| 全量Release | 1108警告、0错误，30.38秒；calibration-native-full-release-final.log |
| 独立UI Release | 953警告、0错误，13.81秒；calibration-native-ui-release.log |
| 安全启动和插件注册 | Release exe --toolbox-probe退出0；NPointCalibration/NPointVerification、坐标标定分类及axes/verify图标均存在；calibration-native-toolbox-probe.json |
| 发布程序集 | SDK、HalAdapter、UI、N点插件SHA256与中间编译DLL一致。点线测量插件目录中旧共享依赖按既有单插件构建规则刷新，见calibration-native-shared-dependency-refresh.log |

上述日志位于artifacts/validation，构建警告包含既有XML注释、可空性、过时API和测试分析器告警，不宣称零警告。测试使用合成数据及临时文件，不连接现场设备。安全探针在服务和方案初始化之前退出，未运行普通主程序启动流程。

## 未完成与人工验收

自动采样尚缺宿主/通信上游生产可信CalibrationAcquisitionEvidence。本轮只完成数据入口及消费校验；到位、新运动步、新帧和实际XYR证据不齐时明确拒绝。没有给操作者增加手填时间/帧ID的伪通过入口，也没有扩展运动/通讯协调器。

手动点表支持求解和训练检查；新点表不会自动获得相机/工作平面身份，正式发布仍依赖有效环境及独立新采样，不能绕开门槛。近零Mark偏心不能仅凭该Mark证明R方向，界面诊断提示现场独立方向检查。

实际Visual Studio设计器、主程序深浅主题/不同缩放、每个模态弹窗的实际点击、真实机构XYR含义和现场精度尚待人工验收；离线WPF渲染不替代这些验收。

## 风险与下一步

1. 先在Release的N点节点检查三Tab、点表副本编辑/取消、CSV错误不覆盖和结果失效提示；设置正的现场误差阈值后，用明确为轴心XYR的新点表求解，不把Mark坐标填成轴心。
2. 验证节点检查三Tab、候选选择、错误候选拒绝、验证失败保留原正式文件。自动采样未接通前不能把无法采点当成已具备现场验证。
3. 后续单独实施可信采样协调器生产证据，与工作流开始时快照及相机帧配对；接通后再开展独立新平移/旋转采样、发布和真实工艺验收。不扩展到TCP、运动规划或抓取工具。
4. 保留旧标定数据及备份，重新标定后才切换新格式。209组对照实验属于旧基线，原脚本依赖已删除的旧求解源码，历史复现应回到基线，不恢复旧生产算法。

人工验收结果：本轮无新增用户现场确认，不标记现场通过。5个用户_capture/_probe文件及此前文档保留。文档写作技能用于更新当前状态和边界；未进行跨AI交接，后续如交接须按项目规则记录有意义的临时提交。
