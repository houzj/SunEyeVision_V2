# 扫描极性与整圆回归修复交接

- 基线提交：ce71b606f83331f6d508147ec060cd3ce46bf6d1；分支feature/comm。
- 当前执行者：Codex，单写入；无提交推送，全部既有修改保留。
- 任务目标：扫描箭头、极性和实际取点对应；修复原生化后的圆查找回归，不降低阈值。
- 允许修改目录：HalAdapter、SDK测量显示/诊断、相关7类工具、协作文档；不修改标定、用户方案或设备。
- 已修改文件：NativeMetrologyResults.cs、MeasuredLineFitting.cs、EdgeFitDiagnostics.cs；MeasureBandOverlay.cs、MeasureEditorControl.xaml.cs；CircleDetectTool/Parameters、LineCircleMeasureTool/Results、CircleCircleMeasureTool/Results、PointCircleMeasureTool/Results；7个工具调试XAML及CURRENT_TASK.md。
- 已完成：4类圆工具5个整圆槽位统一原生整圆编码0/2π/positive，去掉旋转相位端点构造；显示/箭头/命中对应当前图像的规范整圆采样。保留圆心半径修正及圆弧的有向适配。共用取点回读实际极性/选取/模型参数，记录扫描方向、正负边缘数量、测量带；原生采用点幅值符号校验；空模型支持度诊断；界面语义提示。
- 证据：实际日志SunEyeVision_190606.txt在19:12:06报告两个圆原始点14/7、卡尺配置36/15、min_score0.7、原生结果空；参数包含0.0005082244/6.2836935316端点。证实空模型与错误整圆编码并存，不宣称未运行的原图已经恢复。
- 未完成：原图运行与交互人工验收；先前屏蔽专项、阈值/滤波批次暂缓，标定仍独立。
- 已执行构建与测试：首次全量Release0错误1277警告；最终全量Release0错误1093警告（62.46秒），独立UI Release0错误1065警告（18.83秒）。diff检查通过（既有LF/CRLF提示）。不新增/运行测试、不启动程序/设计器，遵用户前序限制。
- 人工验收结果：暂无。本轮没有触碰用户数据/方案或设备。
- 已知风险或禁止改动：不降低Threshold/DistanceThreshold/min_score，不用备用重拟合伪造原生成功，不按点序号对应测量带。原生支持度与旧全点重拟合不是等价契约，真正支持不足仍应失败。
- 下一步：用户在原图分别执行工作流与节点调试，核对实际测量带覆盖、采用点及失败诊断；若仍失败，使用本轮证据继续定位，不能直接归咎用户参数。

## 原生依据

- https://www.mvtec.com/doc/halcon/2405/en/add_metrology_object_circle_measure.html ：整圆参数与端点归一化。
- https://www.mvtec.com/doc/halcon/2405/en/set_metrology_object_param.html ：扫描方向、极性、选取及原生支持度。
- https://www.mvtec.com/doc/halcon/2405/en/get_metrology_object_measures.html ：点序未定义，不能按顺序分配到测量带。
- https://www.mvtec.com/doc/halcon/2405/en/get_metrology_object_result.html ：原生几何、实际采用点和幅值读取。
