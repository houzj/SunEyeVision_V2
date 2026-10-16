# BlobAnalysis和ShapeMatch优化交接

- 基线提交：a444b21325eeb6b121cfe6bd059383b65eb172eb。
- 当前分支：feature/comm；当前执行者：Codex，单写入，保留其他并存修改；无提交推送。
- 任务目标：继续本地HALCON方案，保留绑定、输出和屏蔽，不动标定；诊断协议组装失效导致整轮未执行。
- 允许修改目录：tools/SunEyeVision.BlobAnalysis、tools/SunEyeVision.Tool.ShapeMatch及本地方案、协作文档；本轮不改工作流执行代码。
- 已修改文件：BlobAnalysisTool.cs、BlobAnalysisParameters.cs；ShapeMatchTool.cs、ShapeMatchParameters.cs、Views/ShapeMatchToolDebugControl.xaml；全算法本地方案及CURRENT_TASK.md。
- 已完成：Blob面积／圆度／矩形度统一原生SelectShape，范围有限性验证；BinaryInv有效域差集及形态学最终限域，避免屏蔽复活。开闭／顶帽／黑帽使用既有迭代参数。原Circle仍按较大宽高决定半径，但1×1不放大；不改为Ellipse。均值／高斯复用共享HALCON卷积，中值用MedianRect核尺寸；完整像素上下文计算后恢复有效域，均衡直方图保留原有效域。排序同值稳定、数量截取后标签重排不依赖轮廓开关，区域和数值索引一致；空选区统计补零。
- ShapeMatch：保留每模板FindScaledShapeModel；跨模板AABB兼容去重按原生分数选优，用户排序在之后应用；不重复抑制同模板结果。无交集矩形先判宽高，修复负Rect异常。保留现有全部模型数据和输出，NumMatches开放0全部候选（默认1不变），显式搜索层级原生回读实际层数；有限值、有效尺度、元组长度及实际层级有校验／日志。模型缓存替换／timeout设置／原生搜索共用缓存锁，避免句柄生命周期及可变参数竞争。
- 屏蔽语义：Blob排除像素及最终区域；ShapeMatch保持原生domain限制模型参考点，不保证轮廓不经过屏蔽区、不涂黑屏蔽像素。未新增轮廓覆盖率算法。跨模板AABB IoU是保留的业务兼容规则，不等同HALCON旋转矩形重叠；未迁移一次多模板联合搜索。
- 输出兼容：未删除／重命名任何绑定字段或枚举。Blob BinaryMask／灰度统计仍为Tab3筛选前选区，OK/NG仅判断不剔除；筛选区域和数值对应。排序、正确预处理核和迭代纠错可能改变结果，不承诺旧错误数值等价。
- 工作流诊断：WorkflowExecutionEngine.BuildExecutionPlan在调度前调用OutputBindingChangeService.EnsureExecutable，对任何RebindingRequired直接抛错；协议组装失效导致所有节点未执行。用户认为上游不应被阻断，方案已记录节点失效隔离专项：上游正常执行，问题节点失败，依赖下游禁止消费失效/历史结果，无关分支遵循错误策略。需处理计划、依赖、节点准备、状态及连续执行，不简单删除校验；本轮未实施。实际来源字段删除还是身份恢复异常未确定；截图失效项空白与错误标记残留需改进。
- 未完成：节点级错误隔离、失效绑定具体身份溯源、多模板联合搜索、Triangle原生替换、任意卷积核编辑及独立标定；原图和交互待人工验收。
- 已执行构建与测试：首轮常用Release被SunEyeVision.UI进程17052锁定（141警告2复制错误，25.59秒）；隔离PackagingRoot构建1030警告0错误（83.79秒）。用户关闭程序后，最终常用全量Release1030警告0错误（72.11秒），独立UI Release1065警告0错误（21.82秒）；BlobAnalysis/ShapeMatch/SDK/HalAdapter/UI发布与编译中间DLL哈希一致。diff检查通过，仅既有换行提示；工作流／协议／标定源码无diff。未新增/运行测试，未启动程序/VS设计器，未操作设备。隔离产物保留在artifacts/build-verification/blob-shapematch-20261016；日常仍启动Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe。
- 发布补充：PointLineMeasure目录的HalAdapter副本通过原有插件Release构建刷新（0警告0错误，3.38秒），未改构建或加载规则；最终哈希与宿主一致。
- 人工验收结果：暂无；用户仅确认关闭运行程序。
- 已知风险或禁止改动：不自动覆盖绑定、按同名误接回来源、降低阈值或屏蔽错误。模板缓存全局互斥会串行化并发模型搜索，不改变调度。保留既有坐标转换和标定。
- 下一步：人工验收原图、屏蔽及匹配排序；节点失效隔离另行实施，不作为本批已修复项。进一步确认具体失效输出身份，不按名称猜测回绑。
