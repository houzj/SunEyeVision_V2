# C：总览与根画布模式集中收尾交接

- 基线提交：ba889d4a（主集成固定同版基线）
- 当前分支：codex/workflow-mode-closeout
- 执行者：Codex C；独占worktree workflow-timing-footer/SunEyeVision_V2
- 允许修改：ShellWorkspaceControl.xaml(.cs)/RootPresentation.cs、专用测试、本交接；父额外授权新增画布WorkspaceLifecycle.cs窄退出入口。

## 设计、证据及适配

恢复件`D:/MyWork/REA逆向测试/src/VisionEditor/WorkflowOverview.cs` WorkflowViewClick先关闭流程列表及工具弹层、ResetEditingGesture，再把总览__solution与根流程视口分别保存，清理选择；OpenWorkflowFromOverview保存总览后再选择根。`WorkflowTabs.cs` RefreshAll隐藏ProcedureTabScroll/WorkflowActionsRight，单个/全局四态图像与tooltip随模式切换。完整迁移适配分工：RootOverview负责自己的拖拽及视口，Tab仍拥有根画布视口；Shell统一模式出口，复用搜索（含ID窗）、流程列表、耗时Presenter及画布已有取消函数；不修改Group、执行器、参数或持久化合同。

## 已实施完整链

- 新公开只读模式DP IsRootOverviewVisible；保持internal SetOverviewVisible测试接缝。四态图像使用既有RootWorkflowOverviewDrawings资源，根为单个、总览为全局，原tooltip全部流程/子流程。
- 模板总览时隐藏Tab标题滚动区域、固定新增和右侧流程列表/管理按钮，保留40px模式按钮栏；新增布局计算也按模式门禁，避免局部Visibility优先级泄漏固定新增。
- 切换先关闭搜索/ID窗、流程列表、耗时详情、取消总览/根画布手势及小地图捕获，清理原画布选择，再切显隐。画布窄partial复用CancelVmCanvasGesture/ClearVmWireSelection/ClearAllSelections，无新节点或Group语义。
- 进入捕获根视口，退出通过已有QueueRestoreWorkspaceView恢复；总览视口留给RootOverview，切换不把总览缩放写入Tab。底栏缩放及ZoomScaleChanged事件按当前模式选源，总览ViewportChanged即时联动。
- 总览禁用流程耗时详情，流程/模块/算法不呈现当前根旧值；退出重绑正式快照文字。卸载统一关闭弹层并取消手势。
- 新RootWorkspaceModeTests覆盖真实Shell模板、模式显隐、两个独立缩放、弹层/ID/耗时关闭；需独立进程运行（同现有RootWorkspaceHostTests），不以测试数量代替全窗验收。

## 集成依赖与验证边界

- RootOverview缩放范围0.1..4，现底栏Maximum固定2；已通知父集成为WorkspaceStatusBar增加MaximumScale DP（默认2、总览4），父已确认MaximumScale名字并独占实现；本批已接先最大值再Scale，且同步期间屏蔽ValueChanged反向请求，防止退出总览4→根2时篡改根比例。新测试使用总览3.4/根1.3覆盖该路径。父需将该共享合同同时集成才能编译。
- 主窗右侧图像/结果区不在本批允许文件内；模式DP可由父按原件和宿主需要接显隐。全局工具箱折叠不扩，本批仅对应流程工作区右侧操作区。
- 子worktree未构建、部署或运行程序；仅XML解析与git diff --check。主集成同版Release运行新测试及既有Host/Canvas相关回归后，核暗亮全窗、原四态与真实桌面手势取消。VS设计器及用户人工验收未通过/未执行。
- 无main同步、无Group迁移、无SDK公共扩展。父统一原主文档和CURRENT_TASK；本交接不声称R17或全部工作区已人工闭环。
