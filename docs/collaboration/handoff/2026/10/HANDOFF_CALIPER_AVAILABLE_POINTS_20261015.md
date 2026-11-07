# 卡尺有效点拟合统一修复交接

- 基线提交：a444b21325eeb6b121cfe6bd059383b65eb172eb。
- 当前分支：feature/comm；当前执行者Codex，单写入，无提交推送。
- 任务目标：以实际有效点拟合，不引入支持度配置，不受Metrology最终模型70%支持度门槛限制。
- 允许修改目录：HalAdapter、相关七类卡尺工具、已有本地方案及协作文档；不动标定、用户方案和设备。
- 已修改文件：MeasuredLineFitting.cs、MeasuredCircleFitting.cs；LineDetect、CircleDetect、LineLineMeasure、LineCircleMeasure、CircleCircleMeasure、PointLineMeasure、PointCircleMeasure的Tool.cs；全算法工具HALCON标准流程分批优化方案、CURRENT_TASK.md。
- 已完成：十个拟合槽位统一使用已有点集HALCON拟合器，取消无屏蔽/有屏蔽两种成功口径；工具不再消费NativeMetrologyResults。实际取点/极性/first-last/测量带保留；有屏蔽先排除对应点，不再需要屏蔽后支持率分母。
- 参数与输出：不新增支持度字段，不调整Threshold/DistanceThreshold/Sigma/min_score。保留既有固定距离筛点和内点精化、RMSE及判定；完整Circle[]、圆弧Arc[]、圆心及半径、各距离角度输出和数组绑定均不改。诊断明确标识HALCON取点＋轮廓拟合，不冒充Metrology score。
- 失败处理：有效独立点不足、非有限值、圆共线/模型退化、HALCON算子异常仍明确失败并保留本轮取点证据；不以“点多于最少数量”保证几何一定正确，不自动放宽阈值或回退另一算法。
- 未完成：原图与交互人工验收；后续屏蔽订阅/拼装解析专项、阈值/滤波批次，标定仍独立。已有测量工具没有屏蔽配置的未新增该UI，现有屏蔽功能保持。
- 已执行构建与测试：全量Release0错误1158警告（112.39秒），独立UI Release0错误1065警告（19.81秒）；七插件及SDK/HalAdapter/UI发布DLL哈希均与Release编译中间文件一致，diff检查通过（仅既有换行提示）。不新增/运行测试、不启动程序/VS设计器，遵用户前序限制。
- 人工验收结果：暂无。17/36和7/15点不会再仅因不足70%被拒绝，但实际拟合质量须以原图运行结果确认。
- 已知风险或禁止改动：不能恢复隐藏支持率门槛，不能为了保证成功自动降低边缘/距离阈值；NativeMetrologyResults历史辅助类保留但无卡尺工具调用。保留工作目录所有其他修改。
- 下一步：用原方案核对工作流/调试两入口的圆心半径、实际候选/屏蔽/内外点和失败阶段；继续屏蔽几何专项，之后阈值与滤波。

## 当前原生拟合算子

- https://www.mvtec.com/doc/halcon/2405/en/fit_line_contour_xld.html
- https://www.mvtec.com/doc/halcon/2405/en/fit_circle_contour_xld.html
