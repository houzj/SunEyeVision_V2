# C：根流程底栏与耗时窗迁移交接

- 基线提交：adc727ba1eabf1d3af3bbbf2e5d22494fb402597
- 当前分支：codex/workflow-timing-footer（独立 worktree）
- 当前执行者：Codex C
- 任务目标：R17 根流程呈现链；Group 冻结，主集成统一接线、构建与人工验收。
- 允许修改目录：两个控件/窗口文件、专用测试和本交接；共享模型/执行器/Presenter/主文档只读。

## 集中设计与原件证据

原件 `D:/MyWork/REA逆向测试/src/VisionEditor/MainWindow.xaml` 的 WindowStatusBar：左侧配置图标与状态，流程/模块/算法三个时长区域，右侧80px缩放滑条、100%复位。`MainWindow.ShellStatus.cs` RefreshShellConfigurationStatus/ShowTimeClick/CloseTimeDialog/CloseTimeOnProcedureChange 是完整入口及单窗生命周期；不是 WinForms EditorShell.cs。`MainWindow.FrameworkResultHistory.cs` 明确框架算法耗时缺失时为不可用，不能以零冒充采样。`ShellTimeDialog.xaml(.cs)` 为370×420、最小370×260，35px摘要、32px表头、34px行，三列120/*/*；恢复件固定深色，本产品使用已有主题动态资源适配暗亮。

实现只拥有视图：接收宿主长期执行快照（同一对象与ObservableCollection）；不执行、不断言SDK原生计时语义、不复制图像、不修改结果。关闭只释放本窗ItemsSource/DataContext，快照由执行管理器继续拥有。共享Presenter已有Show前关闭、Closed退订；Shell已有Tab切换/卸载/主窗关闭入口，集成侧审核，不另建第二套单窗管理。

## 已实施

- 替换原620×520四列详情为恢复件370×420三列表；执行状态保留工具提示，未运行/等待行显示—，不呈现零耗时假采样。
- 底栏保留流程文本和执行详情命令，增加模块/算法真实计时槽、配置状态槽；动态主题、缩放命令归宿主。
- 窗口关闭释放视图绑定，不清空共享快照、不暂停执行。
- 新增WorkspaceTimingPresentationTests，检查窄窗尺寸/共享快照/关闭所有权、未知计时占位和缩放请求。

## 集成合同与剩余

`WorkspaceStatusBarControl.ModuleTimeText`、`AlgorithmTimeText` 为字符串DP，默认—，数字不含单位。`ConfigurationState` 类型 `WorkspaceConfigurationState`（Waiting/Ready/Incomplete）。父集成接正式选中节点快照及配置就绪状态，禁止用UI刷新次数或前一流程结果冒充。状态图标暂为语义字符适配；需要集成侧统一接原件bottombar_config.png、bottombar_run.png、bottombar_abnormal.png，当前白名单不含新增资源。不要据此宣称图标已原样迁移。

- 未执行子worktree程序构建/部署/测试；仅静态核对，所有测试需主分支同版Release集中运行。
- 原件本机运行、VS设计器、真实桌面暗亮全窗、用户人工验收未执行，不能记通过。
- Group模块/算法选择不扩展；无新SDK/执行器合同。
- 下一步：父集成读取提交，将真实节点计时与状态统一接线，运行专用测试和原有生命周期回归，核对暗亮WPF布局与原图资源。
