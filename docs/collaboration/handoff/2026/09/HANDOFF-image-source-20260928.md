# 图像源本轮实施交接

- 基线提交：`4a2b8a52`（本轮开始时主线 HEAD；工作区已有其他未提交改动）。
- 当前分支：`main`。
- 当前执行者：Codex。
- 任务目标：按图像源方案完成本地/相机分模式设置、统一图像输出、逻辑 SN 与本地序列运行语义；不做拼接、订阅文件夹或重复文件导入。
- 允许修改目录：主线图像源相关的 UI、Workflow、Plugin.SDK、ImageLoad 工具、测试和方案文档。
- 已修改文件：`src/UI/Features/WorkflowUI/ImageSourceSettingsControl.cs`、`src/UI/Services/Workflow/WorkflowExecutionManager.cs`、`src/UI/Shell/Controls/Shell/ImageResultControl.xaml.cs`、`src/UI/Shell/Windows/MainWindowViewModel.cs`、`src/Workflow/ImageSourceConfig.cs`、`src/Plugin.SDK/Vision/Contracts/ImageOutputFormat.cs`、`tools/SunEyeVision.Tool.ImageLoad/{ImageLoadParameters,ImageLoadResults,ImageLoadTool}.cs`、相关测试及图像源方案文档。
- 已完成：删除“定位到图像预览器”；调试配置分基本/高级；本地模式显示序列摘要、自动切换及末张停止，相机模式显示关联设备、状态、超时和现有相机设置/预览入口；两来源共用 Original/MONO8/RGB24 输出格式和 SN 初始值；工作流覆盖参数以快照进入插件；连续运行仅成功输出递增 SN；本地模式仅成功后推进，失败留在当前图并短退避；末张成功后通知停止；配置随节点序列化。
- 未完成：真实相机触发/连续运行、可见 UI、VS 设计时及多图像源/子流程组合尚未经人工验收。Release 隐藏启动 15 秒只确认方案加载，未确认主界面 Show 完成；不能据此断言启动成功。
- 已执行构建与测试：全量 Release 构建成功（0 错误、1137 警告）；全量 Debug 构建成功（0 错误、1233 警告，输出 `dev/Debug`）；图像源工具 Release 定向测试 9/9；UI 持久化/连续停止 Release 定向测试 4/4；`git diff --check` 无空白错误。
- 人工验收结果：未执行可见交互验收。Release 冒烟 PID 20148 由本轮精确结束，日志为 `logs/2026/09/28/SunEyeVision_095346.txt`。
- 已知风险或禁止改动：不要把当前输出格式转换理解为修改相机 PixelFormat；未验证的 16 位或 Bayer 图像不可声称支持。用户原有 `plans/*`、`.codebuddy/*` 未提交改动不得覆盖。真实相机测试前确认无第二个实例占用设备。
- 下一步：用户从 `dev/Debug/SunEyeVision.UI.exe` 可见启动，分别用本地多图序列和真实相机验证调试窗、输出格式、SN、失败重试、末张停止、工作流停止和关闭预览后的连接状态；记录结果后再决定是否扩大到多图像源/子流程场景。
