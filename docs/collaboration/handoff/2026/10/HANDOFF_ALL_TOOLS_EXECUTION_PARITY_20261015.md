# AI 协作交接单：全工具执行入口一致性

- 基线提交：ce71b606f83331f6d508147ec060cd3ce46bf6d1。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入；未切换执行者，未创建提交或推送。
- 任务目标：消除工作流与节点调试在相同输入和参数下因绑定恢复、修正解析、上下文准备不同而产生的结果差异。
- 允许修改目录：src/Plugin.SDK、src/Workflow、src/UI、tools及相关内部方案／协作文档。
- 已修改文件：核心为ToolExecutionPreparation、ToolExecutor、ToolExecutionRequest、ToolParameters、PositionCorrectionResolution／Resolver、ToolDebugResultSnapshot、ToolDebugControlBase、AlgorithmNode、ToolParameterSnapshot及各工具算法／调试控件／测量结果类；脚本移除Workflow项目依赖。保留全部此前并存修改及plans/plan.json。

## 已完成

共同执行前准备负责实际图像、AvailableDataSource运行副本、位置矩阵与文本绑定；工作流不再另行填充一套CurrentValue。调试先复制设计参数，按当前查询结果采集上游快照，再交给共同执行器；宿主与工作流使用相同序列化选项。缺失或无效的启用修正不得静默当作未启用，绑定路径不等于运行值。同步与异步调用均进入准备流程。

矩阵从本次结果解析并固定；测量引导要求保形变换，区域工具保留合法仿射能力。修复隐藏基类属性导致错误读取修正开关／来源的情况。共同预览刷新在实际参数来源变化后重新解析，支持矩阵数组；直线查找修复真实参数对象订阅。结果引导几何和矩阵固定在当次结果中，样式仍可实时变化，避免用新参数重画旧几何。

| 覆盖工具 | 排查及处理 |
| --- | --- |
| CircleDetect、LineDetect | 算法与屏蔽过滤共享捕获矩阵；移除仅读CurrentValue路径，修复参数／预览刷新 |
| BlobAnalysis、ImageFilter、ImageThreshold、Morphology、ShapeMatch | 去除缓存空值时静默跳过修正的分支，统一算法与区域预览解析 |
| Threshold | 补齐区域流水线的位置修正传递和调试区域刷新 |
| CircleCircleMeasure、LineLineMeasure、LineCircleMeasure、PointLineMeasure、PointCircleMeasure | 统一执行矩阵；冻结本次结果的引导几何／卡尺与矩阵 |
| PointPointMeasure | 接入共同准备；点坐标为上游结果，不重复修正，常量模式保持 |
| Script | 去掉直接Run及反射宿主取变量，接入共同请求、异步执行器及输入监视上下文 |
| ReceiveData、SendData、ProtocolParse、ProtocolAssemble | 原共享入口继承参数副本及统一准备；保留等待、取消和逐次报文消费语义 |
| Condition、NPointCalibration、NPointVerification | 原共享入口统一准备；校准结果显式提交机制保持 |
| ImageLoad | 配置窗口没有独立算法执行按钮；工作流执行走共同执行器 |
| Subroutine | 配置窗口无本地Run分叉；子工作流及内部工具沿执行引擎／共同准备链路 |

合计23个插件项目、24个ToolId。覆盖为源码路径检查与实现，不代表逐工具真实运行验收。

## 构建、发布与验收

- 全量：`dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -clp:ErrorsOnly`，0错误、887警告，40.28秒。
- UI：`dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore -m:1 -clp:ErrorsOnly`，0错误、768警告，14.56秒。
- 受限构建曾遇到XML／DLL写入Access denied；获准提权后正常构建成功，没有改权限或终止程序。
- 发布目录：Release/SunEyeVision_v1.0.0/SunEyeVision。23个插件主DLL与各自build_temp中间DLL的SHA256全部一致；SDK和Workflow发布DLL亦已核对一致。
- `git diff --check`通过，仅现有换行符提示。
- 已执行测试：遵用户要求，本轮没有新增或运行测试，也未启动Release程序、VS设计器或操作真实方案／设备。
- 人工验收结果：本轮尚无；历史自动化记录不能替代本轮视觉一致性验收。

## 未完成、已知边界与下一步

一致性前提是同一配置、图像与上游结果。工作流会执行上游，调试使用当前可用上游结果；不为了“相同”而重放TCP旧报文，不擅自重跑设备链路。快照固定结果对象及矩阵，不等于所有上游可变对象已变成全量不可变运行包。

完整运行轮次／模板基准版本、所有预览旁路的帧身份、统一事务撤销历史仍属后续设计，不宣称整套ROI方案完成。不得覆盖已有协作修改，不得把旧失败测试标成已通过。后续如获授权进行运行验收，应使用隔离方案与图像检查两入口；未经授权不连接设备或打开真实用户方案。
