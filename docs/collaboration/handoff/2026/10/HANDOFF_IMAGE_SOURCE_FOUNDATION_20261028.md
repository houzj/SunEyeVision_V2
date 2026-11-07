# 图像源业务基础候选交接（未编译、未验收）

- 基线提交：d749f38e；主线待统一基线ad951eb1。本文件所在中文保存点为本批候选检查点，提交号另随交接消息发送。
- 当前分支：codex/workflow-toolbox-dataflow。
- 当前执行者：Codex，87f4独立worktree唯一写入者。
- 任务目标：先图像与结果业务基础，再Plugin.Wpf原控件及主界面组合；Group暂停，完整目标不缩小。
- 允许修改目录：本独立worktree的UI业务、Workflow源编辑/资源解析及对应测试文档；未修改Plugin.Wpf或SDK公共合同，未写主仓。

## 本批候选

1. NodeConfigurationEditHost接ImageSourceConfig复制/签名/候选结构验证/原子投影与回滚；纯源编辑由基线签名推导，并在绑定适配后复核工具签名，允许空源配置保存，不能免检被改坏的工具参数。
2. ImageInputSource只读条目展示，先正式接纳后安装/通知/释放资源；导入一次接纳，勾选/删除/清空拒绝不改变现场，运行游标与持久起点分开。
3. MainWindow源预览与ImageSourceSettingsControl字段统一正式历史；自有设置窗口/当前嵌入页权限、跨方案/其他窗口拒绝、重载订阅、Undo/Redo投影。设置即时接纳，关闭不回放或回滚已接纳编辑。
4. 运行计划只来自冻结ImageSourceConfig及同次资源基目录；ResolveAddress不做I/O，后台可用性检查复用。删ExecuteWorkflowInternal旧UI读取后备链，覆盖只写本轮副本。现序列服务仍唯一推进；执行器保存跨次运行态游标，按根实例/源定义匹配，每源重选独立复位，根源删除/换代/Dispose清理。根就绪状态同步。
5. 结果变量选择接BeginVariableEdit/ProducerBinding正式接纳；图像Info/模式/cache及解码从旧控件移到Shared/Services.Thumbnail，不留静态兼容转发；旧视觉尚待原控件替换。

## 验证及未完成

- 已执行：31个C#文件Roslyn语法零错误；两个XAML/测试项目XML可解析；git diff --check通过。仅静态，不等价语义编译。
- 未执行：UI/全量Release构建、所有新增/调整测试、实际运行及深浅主题、真实VS设计器、用户人工验收。没有使用其他目录产物冒充正式Release；未部署、未启动、未推送。
- 新增测试：ImageSourceAcceptanceTests、ImageSourceConfigurationHistoryTests（含实际ImageLoad参数）、LocalImageConfigurationSnapshotTests；RootWorkspaceHostTests补真实设置事件/历史/重载/权限；LocalImageExecutionTests补无预览跨次执行和旧预览隔离；导入取消前后边界及保存/解码消费同步。
- 待集成核点：主线新工具目录合同/参数架构差异、Host签名/绑定适配的实际语义、正式图像源控件启闭/文件引用/多源运行/失败取消/保存重开。编译或回归发现问题需集中修正，不以当前静态结果宣称通过。
- D1其他结果发布/缓存/物理帧/高亮资源链、原图像结果/模块树表/缩略图/历史帮助组合仍未完成；C14上下文及逐投影物理来源由主线公共库与本方消费者整体冻结后实施，C17随后。主线ROI的317组不是本批成绩。
- 已知禁止：不改Group/循环语义/Blob业务；不覆盖主线正式Release；保留用户BlobParameterEditor.xaml页索引2未提交修改。
- 下一步：主线按完整检查点统一基线/解决冲突，在唯一正式Release运行规定构建和相称生产链回归，再协调结果公共合同。人工验收待实际程序启动后单独记录。

## 本批文件

- `docs/01-方案设计/2026/08/11/主界面产品级重构方案-最终版-20260811-134458.md`
- `docs/collaboration/CURRENT_TASK.md`
- `src/UI/Features/WorkflowUI/ImageSourceSettingsControl.cs`
- `src/UI/Services/Workflow/RootWorkflowReadiness.cs`
- `src/UI/Services/Workflow/WorkflowExecutionManager.LocalImages.cs`
- `src/UI/Services/Workflow/WorkflowExecutionManager.cs`
- `src/UI/Shared/ImageInputSource.cs`
- `src/UI/Shared/WorkflowNodeModel.cs`
- `src/UI/Shell/Controls/Panels/ImagePreviewControl.FileImport.cs`
- `src/UI/Shell/Controls/Panels/ImagePreviewControl.xaml`
- `src/UI/Shell/Controls/Panels/ImagePreviewControl.xaml.cs`
- `src/UI/Shell/Controls/Shell/ImageResultControl.xaml`
- `src/UI/Shell/Controls/Shell/ImageResultControl.xaml.cs`
- `src/UI/Shell/Windows/MainWindowDocumentHistory.cs`
- `src/UI/Shell/Windows/MainWindowRootWorkspace.cs`
- `src/UI/Shell/Windows/MainWindowViewModel.cs`
- `src/Workflow/NodeConfigurationEditHost.cs`
- `src/Workflow/Persistence/SolutionResourceResolver.cs`
- `tests/SunEyeVision.UI.Tests/FallbackEffectivenessTests.cs`
- `tests/SunEyeVision.UI.Tests/ImagePreviewGeneratedDecodeTests.cs`
- `tests/SunEyeVision.UI.Tests/ImagePreviewInteractionTests.cs`
- `tests/SunEyeVision.UI.Tests/ImagePreviewLoadTests.cs`
- `tests/SunEyeVision.UI.Tests/ImageSourceSavePersistenceTests.cs`
- `tests/SunEyeVision.UI.Tests/LocalImageExecutionTests.cs`
- `tests/SunEyeVision.UI.Tests/RootWorkspaceHostTests.cs`
- `tests/SunEyeVision.UI.Tests/SolutionManagerUiTests.cs`
- `tests/SunEyeVision.UI.Tests/SolutionPersistenceContractTests.cs`
- `tests/SunEyeVision.UI.Tests/SunEyeVision.UI.Tests.csproj`
- `src/UI/Services/Thumbnail/ImageCache.cs`
- `src/UI/Services/Thumbnail/LocalImageAssetCache.cs`
- `src/UI/Shared/ImageInfo.cs`
- `src/UI/Shell/Windows/MainWindowImageSourceEditing.cs`
- `src/UI/Shell/Windows/MainWindowResultBindingEditing.cs`
- `tests/SunEyeVision.UI.Tests/ImageSourceAcceptanceTests.cs`
- `tests/SunEyeVision.UI.Tests/ImageSourceConfigurationHistoryTests.cs`
- `tests/SunEyeVision.UI.Tests/LocalImageConfigurationSnapshotTests.cs`
