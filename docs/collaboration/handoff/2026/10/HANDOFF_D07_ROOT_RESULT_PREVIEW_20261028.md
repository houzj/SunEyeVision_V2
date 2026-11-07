# 根流程配置正式图像预览整链交接

- 基线：cb3fe8cc；独立分支codex/d07-root-result-preview。此前本人候选提交均保留原分支，没有重写历史。
- 允许范围：RootWorkflowConfigurationWindow及其私有图像partial、Context只读owner、专用真实回归。无主仓/root写入，无公共合同/算法/Group改动。
- 问题证据：旧RefreshPublishedResults每轮清Preview.Source后只读OutputCache.CurrentOutputImage；正式NodeResultManager已退出该缓存写入，Runtime结果真实有DeclaredOutput也显示无图。原根窗口回归只核标量表两行，未覆盖图像。

## 最终实现

原窗口Image替换同一公共原ImageViewBox；私有ResultImagePane独占该view。保ImageNodes、标量Results、参数编辑和运行按钮链；新增ImagePorts，明确由用户选择实际完整snapshot.Schema中单一Image资源输出，默认空选。ImageLoad的output.image/灰图各独立列出，不猜像素、不从资源枚举补端口。换节点不沿用端口；同节点新run仅在正式Port完全一致（含类型/存储）时保留用户选择。

RootWorkflowConfigurationContext仅新增构造时冻结的只读ExecutionOwner，原构造签名不变。每次显示/惰性借阅要求Context.IsValid、Root.Nodes同ID唯一且同实例、readResult还是同结果/snapshot、实际ExecutionIdentity.Owner同document owner且IsCurrent、ToInvocationIdentity等于snapshot.Identity、DocumentSessionId等于owner.Id、SourceWorkflowId等于Root.Id、声明完整。端口还须属于当前选择。使用ToolResultViews.FromSnapshot+正式OutputReference，不取Main当前pub、不构造RunIdentity或图像Frame。

窗口自有publication持snapshot reader，pane持其独立Borrow及像素lease。同snapshot/节点/port轮询只RefreshCurrentness，不重借和重呈现。换端口、换节点、新run、无合法输入/失效、异常均先递增epoch，Clear归还pane Borrow再Dispose窗口publication；无权Dispose其他Main表publication。关闭聚合尝试timer退订、context解绑、pane/publication归还、context.Dispose及字段释放，单项异常不阻挡其它动作。真实DP同步重入会使当前守卫失效并排队最新图像刷新，不重放旧候选。

## 文件与依赖

- src/UI/Shell/Windows/RootWorkflowConfigurationWindow.xaml：原视口+端口选择。
- 同名.xaml.cs：移除旧缓存入口、聚合关闭，保标量和参数业务。
- RootWorkflowConfigurationWindow.Images.cs：完整私有选择/借阅/身份/失效/关闭链。
- src/UI/Services/Editing/RootWorkflowConfigurationContext.cs：只读ExecutionOwner及构造赋值。
- tests/SunEyeVision.UI.Tests/ResultWorkspaceProductionTests.cs：仅将原真实Fixture可见性private改internal，原13例不减不改，供新真实宿主用例复用。
- tests/SunEyeVision.UI.Tests/RootResultImagePreviewTests.cs：两个实际Main.OpenRootConfiguration窗口场景，使用正式已打开方案/ToolExecutor/NodeResultManager/实际资源。

无需csproj显式列源；新tests按现SDK默认包含，root需把新测试类纳入正式所选回归轨（本路不改runner）。依赖基线已有ResultImagePane/ToolResultViews/原ImageViewBox，未造第二图像实现。

## 验收矩阵与当前证据

候选场景1：真实窗口默认空选→ImageLoad至少两个声明端口→显式选图→同snapshot轮询保持同receipt→换灰图→换节点清空选择/旧图→新节点图像→关闭pane无frame、Main原表pub仍current→最终fixture所有资源owner只释放一次。

候选场景2：真实窗口同节点新run保持同正式端口并换实际Frame/像素；错workflow scope和错document owner结果拒绝且端口清空。已有13项D07回归及根窗口标量/参数/运行/保存测试保留。

仅执行C#纯语法解析、XAML XML解析和git diff --check；未语义编译、未构建、未跑正式Release、未真实VS/人工验收。不能把候选断言记作通过。需要root集中规定UI/full构建、正式RootResultImagePreviewTests与原RootWorkspaceHostTests/D07/ImageSources/结果资源回归，并实际打开根窗口人工选图验收。没有把完整graphics/text/ROI背景推断扩入本批。
