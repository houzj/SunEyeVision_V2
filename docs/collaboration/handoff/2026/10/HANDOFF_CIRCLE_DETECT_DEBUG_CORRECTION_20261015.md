# 圆查找调试执行漏修正修复交接

- 基线提交：ce71b606f83331f6d508147ec060cd3ce46bf6d1。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入；保留全部原有修改及plans/plan.json，无提交推送。
- 任务目标：工作流执行后打开圆查找窗口，不改参数或图像，工具执行仍应使用已绑定的位置修正。
- 允许修改目录：Plugin.SDK、CircleDetect插件、相关既有测试源码清理及docs；不操作真实方案或设备。
- 已修改文件：CircleDetectTool.cs、CircleDetectToolDebugControl.xaml.cs、ToolDebugControlBase.cs、ToolDebugResultSnapshot.cs、PositionCorrectionResolution.cs、CURRENT_TASK.md和ROI主方案。移除上一批基于错误缓存假设添加的测试源码片段，保留该文件原有用例。
- 已完成：确认ImageSourceReference.MatchDataSource恢复的绑定不含CurrentValue，CircleDetect旧算法只读该值，而AlgorithmNode工作流入口会主动填充值。算法改为从NodeResults捕获修正，卡尺／屏蔽共用；调试执行复制参数，窗口观察实际参数对象并在切换修正或来源时刷新；共享调试请求采集新结果，图像同快照解析，禁止残留NodeResults优先覆盖当前结果。修正源在显式结果快照中缺失时不回退CurrentValue。
- 未完成：其他仍使用ResolveFirstMatrix的工具未在本批全部迁移；完整运行轮次／模板基准版本及所有工具推广仍按主方案待办。
- 已执行构建与测试：最终dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -clp:ErrorsOnly成功，0错误、1091警告，84.21秒；diff检查通过。按用户要求未运行或新增测试、未启动Release／VS设计器。
- 发布核对：SDK SHA256 BF836B058E6E62C463E716324966CE1F35594D747A4D0F059A798B34FEED758C；CircleDetect SHA256 02EAC4B7229AB87DC4ADD316D6CA8A5AB5823EDF5836429EF8534E4B9024D2DB。编译中间输出与Release发布路径逐项相同。
- 人工验收结果：用户已确认前一轮改动无效；本批修复尚无人工验收，不将旧测试记录视为已验收。
- 已知风险或禁止改动：不再把查询服务独立缓存或缺完整工作流运行包当作本例唯一根因；DataSourceQueryService直接读WorkflowContext。同一源查询采集稳定不等于整工作流RunId原子冻结，本批不声称完成该能力。当前常用Release已更新，启动加载新程序集由用户控制。
- 下一步：如后续反馈同类工具差异，先核对实际ToolId，按同一执行契约迁移其修正读取路径；不以修改ROI原坐标或清除绑定掩盖差异。
