# D07 原ROI宿主私有编辑链交接

- 基线提交：45661f08；公共viewport合同218c3228。
- 当前分支：codex/d07-native-roi-host。
- 当前执行者：Codex / source_editor，独立workflow-overview worktree唯一写者。
- 任务目标：实际冻结输入帧上的原件ROI会话、私有参数接纳、Apply/取消与方案历史保存整链；不改Main/Shell/公共库/SDK/Blob/Group/runner。
- 集成依赖：pane线6ff246a0及其后续预览候选，src/UI/Services/Images/ResultImagePane.cs提供同合同ResultViewportImageLease；root Main工厂仅按当前节点已证明的实际输入接SetRegionInputProvider。

## 已有—行为—差异—决策

1. 原RoiViewportEditScope使用SDK ImageControl、WeakTable全局槽、鼠标捕获、私有Canvas和自绘；完整替换为ResultViewportImageLease→NativeRoiSession→NativeResultViewport.BeginRoiEditing，删除旧手势/画笔/图层后备。实际pending来自INativeRoiEditLease.HasPendingGesture，未以普通字段代替。
2. 现AcceptParameterCandidate已具整组私有候选验证和安装，不能擅自把每次手势正式Host.Commit。原Create/Replace/Delete直接调用同一私有安装，冻结完整scene/selection后返回acceptance，Publish再刷新数值页及scene。Apply/Save沿原ProductionEditorContext/DocumentEditSession，Cancel不污染正式节点。
3. 原GeometryChanged只能单对象，不能承载Delete/Undo/Redo。新native提案不再发该事件回写，Delete多个目标一次私有安装，稳定group+ID不变；创建由真实模型分配新ID。
4. NativeSession只接受原件矩形、圆环、扇形、多边形；当前持久RoiShapeValueAdapter没有独立sector身份。宿主只无损投影矩形、完整圆环、多边形；circle/point/line/arc/partial annulus/开放折线polygon等完整原数据继续保留于私有模型和现数值入口。未支持项不进入原session，不因其中一项阻塞其它支持项，不把partial annulus改名为sector。
5. 私有区域草稿没有同一现成专属Undo/Redo基础，原session history明确false/false及诊断；不建局部stack、不把手势改为正式自动提交。Apply后的全局Undo/Redo/保存重开仍复用现Host。
6. 原SDK位置修正矩阵自绘没有冻结合同中的精确native编辑投影入口。位置修正enabled时当前手势明确禁用诊断，数值及完整模型保留；不猜矩阵、不近似模型坐标。此为待正式投影能力具备后的未做项，不记作完成。

## 资源、帧及通知边界

- provider必须返回独立publication Borrow，仅实际节点输入证明可用，不拿任选背景、preview或尺寸猜配对；无合法input时手势不可用且明确诊断。
- 新session固定Image.Frame、EffectiveDomain.Frame及尺寸；BeginRoiEditing再次核原viewport owner/epoch/receipt/current。scope保持自己的借阅直到换帧、换草稿、关闭或退场，Dispose先interaction、session、最后借阅，不Clear/Dispose背景pane。
- 同一receipt激活只临时取得/释放独立候选借阅，保当前手势；source/帧或代次替换会退出旧session、旧请求不能提交。Apply/Save前先RefreshCurrentness再核真实pending。
- 私有接纳prepare内冻结完整scene，避免安装后的同步通知已重绑时重读新页面。DeclaredToolConfigurationControl最终节点安装后复核generation/参数slots/context，已失去视图身份则不覆盖新页面的_parameters。
- 接纳回执含单调revision、精确Affected与Selection；Publish只在原scope仍current时刷新。数值页有无效文本时拒原手势提案，继承来源不写本地底稿；切页/Esc/关闭沿原CancelGesture/Dispose。

## 文件

- src/UI/Services/Editing/Regions/RoiViewportEditScope.cs：整片原session/lease适配及资源生命周期，无SDK鼠标/自绘。
- src/UI/Services/Editing/Regions/RoiParameterEditingAdapter.cs：原提案单组候选更新及完整数值scene只读投影。
- src/UI/Services/Editing/Nodes/DeclaredToolConfigurationControl.Regions.cs：SetRegionInputProvider、同草稿原session接纳/帧/关闭/pending联动。
- src/UI/Services/Editing/Nodes/DeclaredToolConfigurationControl.cs：仅最终私有参数安装后的通知重绑守卫。
- tests/SunEyeVision.UI.Tests/NativeRoiHostScopeTests.cs：5项真实原viewport/pane/session及ImageThreshold实际参数/ProductionEditorContext/Host回归。

## 验收矩阵与状态

新用例覆盖：mixed未支持circle/partial annulus保留；原Create/Replace/Delete；矩形/完整环/多边形与inspection/mask双角色；真实pending阻Apply、Cancel；私有不进入正式历史、Apply后Host Undo/Redo、Repository保存重开稳定ID；新输入epoch退役旧请求且不清背景；私有安装后页面退休不旧Publish。没有新增产品测试专用Session公开API，测试只读取实际私有session后走其正式公开命令，不Invoke私有鼠标处理。

本分支现src/tools未见实际工具实现IConfigurationFieldProvider，仅接口和DeclaredToolConfigurationControl消费者。因而当前回归使用既有ImageThreshold真实RoiParameters及Host测试原scope/模型/正式历史，未冒称一个不存在的工具schema已做真实DeclaredControl完整Loaded人工核验；当前实际工具接入范围由root核主线候选后安排。

五C#纯语法0错、git diff --check通过。未进行语义编译、正式Release构建、测试运行、VS设计器、VM同态或人工验收；无私有Release、无复制DLL。root应把NativeRoiHostScopeTests五项挂同源正式轨，统一Main已证输入provider后集中UI/full/原ROI与图像结果关联回归。sector、位置修正精确投影及私有专属Undo/Redo保明确依赖占位；完整总清单不缩减。
