# AI 协作交接单

- 基线提交：b12656d8；当前分支main。当前执行者Codex，主线唯一产品写者。
- 任务目标：先Blob所需原控件库，达真实准入再Blob Runtime/Wpf重组。当前仅C08 Box/Sector与共用数值修复，不是完整C08或第一阶段退出。
- 允许修改：现有Plugin.Wpf、正式验证脚本、原O06/协作文档。Blob业务冻结；用户BlobParameterEditor.xaml SelectedPageIndex=2保持未提交，不推送。
- 已修改文件：NativeBoxRoiSession/Editor/Viewport删除并直接替换为公共NativeRoiSession/Projection/Editor/Viewport；激活原SectorROIStack；原RoiSelectControl/局部RoiResources及原SectorStyle；原Rect/Sector绑定及捕获生命周期；三处数值内层样式；RoiGeometryEditing显式sector；Catalog、窗口检查、原方案及生成器。

## 已完成

1. 原Box五项和原Sector六项同一物理帧/Group+Guid的暂存投影；宿主持唯一正式几何。公共ScopeId/Revision/Frame快照与expected stamp原子接纳，版本回退/同版本换内容/换根旧提交拒绝。
2. 读回调、选择取消、观察者异常和Dispose/重绑均受门控；只读泛型集合先整体安装再通知（原ROIItemsPage需要泛型识别）。失败观察者不截断关键释放/通知，异常传播而不伪报未接纳。
3. 原Sector内外半径/中心/角度保真实精度与原绑定；明确sector与annulus不同。原恢复件360弧端点重合退径向线，使用原完整圆环双Ellipse绘图基元修该渲染状态，保六项参数及Sector类型。不宣称VM实机有该问题。
4. 原Rect/Sector几何及布局SetCurrentValue，重套模板退旧MouseDown，正常结束先清手柄再释放capture，失捕/捕获失败撤临时几何。真实OS指针操作仍未证。
5. 页面隐式TextBox MinHeight=30影响原数值内部，三处叶样式隔离后边界断言/实际PNG通过。F2与原0.00中点不一致导致Popup关闭二次写320.13，现读取原Binding.StringFormat作为显示基线。
6. Catalog C08列33原源/5本方依赖；全部104原来源SHA复核，原来源与本方适配分开。原O06 nativeSectorSceneCandidate记录完整变化/失败/边界，生成701唯一锚点且幂等。

## 已执行构建与测试

最终规定UI011零错误6645警告101.16秒、全量Release002零错误6655警告70.75秒。沿用同一正式程序集，本轮不重建。Window070=36、FieldEditing064=31、RuntimeEditor044=10、DesignTime020=28、FullChain103=7、Components188=9，正式EXE探针exit0/22工具。全部自有验证句柄已结束。

检查点：evidence/O06_BLOB_NATIVE_SECTOR_CHECKPOINT_20261027.json，核20源码增量、760发布项、174加载SHA和六轨源码；包括用户未提交XAML，不套旧九轨成绩。实际partial/fullcircle PNG及文字修复截图已查看，不替代VM/VS用户验收。

保留失败：Window059—069期间非泛型集合导致图形缺失、错误资源前引用/模板位置、原捕获失败状态、360径向线、F2/0.00误写；另有测试夹具通知异常、library-only更新触发payload SHA拒绝、一次诊断测试误用CopyValues。最终070才36过。原日志不可删除改绿。

## 未完成与下一步

- 原SectorRing四项/Circle完整圆环 → Polygon原Point双坐标及顶点槽位/拓扑代次 → 原行宿主/同链设计入口 → 绘制/继承/位姿。详细只读回件已归evidence/O06_ROI_CLOSURE_QUEUE_20261027.json。
- SectorRing必须原NormalizedCircleROI而非Sector360，部分annulus不能静默画整圆。Polygon须原ToBtn/PointSource，不能用Float假冒或坐标hash当身份，Recycling不能依赖全部视觉行存在。
- 公共库目前无ROI独立Design入口；建议正式NativeRoiScene同Session组装两真实视图，设计fixture只用正式Contracts样本，不引Runtime/Blob/算法/SDK，按帧/域/reader独立释放。
- C01临时ItemsControl祖先缺ChildMargin；应先原ParamListView行宿主与实例模板闭包，禁止按业务程序集搜资源/全局缓存。实际fallback已核为ParamsTabUserControl配原TabControlStyle/PART_SelectedContentHost；ParamsTabControlPanal是另一原资源，不激活空holder，不猜换TabControlEx。五份关键原XAML/代码SHA已主线复核，详见队列证据。
- 复制/删除/粘贴原入口在图像快捷键/菜单，不是新操作栏。原件先集合Add/Remove与当前只读投影冲突，需要先宿主操作请求原子接纳再装快照；不得开放投影集合可写。撤销重做属于宿主事务，原UI入口尚未证。
- Assist组使用Boolean+原RadioSelecter3(5槽仅3可见)+ButtonSelecter引用，坐标角度亦为float订阅，不套滑条范围。现input.pose只有最终Affine2D，不能承载全部配置；真实变换方向/顺序尚未证，库UI与执行语义分开。
- 完整绘制/继承/多对象/复制删除撤销、真实拖动、生产原子根接入、VS同场景、VM同态人工、命名治理与其余C01—C17仍未完成。第一阶段继续，Blob业务重组未准入。

## 人工验收与禁止事项

用户已报告此前设计器数值文字错位，本批已自动修复并实看PNG；本批真实VS、VM同状态与用户人工未验，不代签。不要在用户VS检查期间重建。保留用户页索引；不推送、不引旧SDK视觉后备、不新增框架项目、不复制产品或算法库。
