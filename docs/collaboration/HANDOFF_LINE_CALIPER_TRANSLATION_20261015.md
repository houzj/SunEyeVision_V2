# AI 协作交接单：直线卡尺整体移动

- 基线提交：ce71b606f83331f6d508147ec060cd3ce46bf6d1。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入，无跨AI交接、无提交推送。
- 任务目标：修复直线查找拖动主体／卡尺带时仅移动一个端点的问题。
- 允许修改目录：tools/SunEyeVision.Tool.LineDetect、src/Plugin.SDK/UI/Controls/Measure及协作文档。
- 已修改文件：LineDetectToolDebugControl.xaml.cs、LineGuideInteraction.cs、CURRENT_TASK.md、本交接单。
- 已完成：几何事件先捕获完整起终点；回写两个参数期间禁止参数通知反向SetGuide，防止第一项写入时覆盖模型中的第二项。参数面板主动编辑仍正常回填；位置修正逆转换路径不变。端点手柄改为Hand，与命中查询提示一致，主体／卡尺带保留SizeAll整体移动。
- 根因证据：GuideGeometryChangedEventArgs.Model是编辑器同一模型；旧回调写LineStartPoint后同步PropertyChanged立即SetGuide(start,new; end,old)，后续从model读取LineEndPoint已是旧值。整体移动因此变成只移动起点，并非Translate没有更新端点。
- 未完成：真实窗口拖动人工验收；不宣称已完成运行验收。
- 已执行构建与测试：全量Release成功，0错误1273警告（125.86秒）；独立UI Release成功，0错误1065警告（34.07秒）。SDK及直线查找发布主DLL与编译中间DLL的SHA256一致，git diff --check通过。按用户此前要求不新增／运行测试、不启动程序。
- 人工验收结果：暂无。
- 已知风险或禁止改动：保留所有此前改动和plans/plan.json；不操作真实方案／设备、不改算法或通信执行语义。
- 下一步：如用户反馈其他交互异常，继续按实际事件链检查；后续运行验收需获得用户授权。
