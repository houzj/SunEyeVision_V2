## 当前候选结果
UI/全量Release均退出0、0错误；完整显示44及ROI命中/重入/退出10组通过，实际加载逐条SHA匹配见BLOB_RESULT_INTERACTION_CANDIDATE_20261103.json。真实InputHitTest证明不重叠结果可命中、重叠处保留上层ROI命中、绘制取消恢复、旧租约不清新会话。真实Popup自动打开/关闭和VM原机时序未代测；未称所有原厂算法特征或低质量生产路径完成。下文最初待验状态为设计历史，以此回执为准。

# 结果显示提示与命中完整批次

候选分支codex/blob-result-interaction-20261103，产品基线dd6b08f67；主仓正式状态34002fbd5。复用既有候选工作区，不复制程序或算法库。当前为实施中，尚未通过整链验收，不改变上批矩形/输出路由正式自测范围。

## 原件与当前差异

原交付：D:/MyWork/REA逆向测试/docs/joint-audit/B08-九对象四ROI样式交互对照-20261102.md。原Display.xml声明Blob、最大内接矩形、检测矩形、两Fixture点及扇环Tips；其余文本/轮廓/屏蔽没有该声明。原ObjectBase.SetBaseProperty618先取Tips再填中心、状态及样式。FeatureBase.GetValue515起按映射数组各值ToString替换模板，占用同次索引；越界回第一项属于原feature行为，不能泛化为本方结果行缺失自动借上一行。默认空映射及null另有分支。

本方NativeShapeProjection和NativeRoiResultProjection的ToolTip原先恒空；NativeRoiAttachment.Initialize还无条件设置ShapeHitTestVisible=false，退出不恢复。ImageViewBox原默认true，原绘制状态ChangeHitTestVisible统一控制结果与ROI命中；原模板按图像、ShapeItemsPage、ROIItemsPage、额外Canvas叠放。原RectShape等只在非空Tips下允许命中。

## 整体实现与边界

- 保持原高质量绘制对象生产路径，非视觉投影携带显式IncludeMeasurements，缺省false。仅真实Blob矩形、冻结检测矩形和扇环开启；屏蔽、多边形选择高亮和普通通用几何不自动加提示。既有对象/scene/lease身份及整场原子准入复用。
- 矩形模板为CenterX:{0},CenterY:{1} Width:{2} Height:{3} Angle:{4}；扇环模板为CenterX:{0} CenterY:{1} OuterRadius:{2} InnerRadius:{3} StartAngle:{4} AngleRange:{5}。当前取同一冻结显示原语的真实测量值，以当前文化数字字符串呈现，不造额外来源或修改结果schema。HALCON四角反算角、扇环负角归一和double精度是明确的当前平台适配；不宣称等于原float特征或S3算法输出，尤其退化矩形角不能由四角证明与原厂特征角同值。将来真实算法特征接入时替换非视觉投影，保留原可视控件。
- 移除ROI接入的永久命中禁用，复用原绘制状态门禁；不得永久强制true或旧lease退出覆盖新owner。已核ImageViewBox.xaml.cs:2057–2070：PrepareNativeRoi经current门禁设置IsStateForDrawROI=true，UnprepareNativeRoi按当前owner设置false；新增接入、绘制准备/取消及退出的命中断言，运行结果仍待验。
- 图形仍只读，不因Tips开启变成ROI可拖拽对象。检查ROI控制点、绘制、缩放与提示命中不互相遮挡，单图与双图分别验证。无需改算法、参数声明或撤销持久化模型。
- 最大内接矩形、Fixture两点和轮廓缺真实生产值继续保护；不以框/默认点伪造。原低质量Tuple/TagDrawingVisual批量链当前不是本方生产路径，直接调用渲染方法仅作原语测试，不称生产集成。
- 原ShapeCanvas只在命中Tag时更新ToolTipContent，空白无命中/离开缺显式清理；是否及时关闭实际Popup须实测，不把源码推断当原机时序证据。本方换帧/退役清场须避免旧提示及旧回调影响新对象；如补强清理，须标明宿主保护。

## 验收出口

候选UI和全量Release构建；真实显示Render/Window/Roi及工作区回归；原视口ROI接入/绘制/退出/重入、当前提示与原高质量真实Loaded图形、不同结果行与角色样式、单双图、清空/换帧/关闭与旧owner。验证原几何Tips对象与HitTest实际一致，Popup真实显示/消失及自然键鼠另记实际覆盖，不能用字符串断言代替。目录载荷SHA与候选/正式回执分开。实际VS由用户另验，不作为本次自动自测前置。

原九对象、四ROI及S2/S3完整范围保留。当前构建/测试尚未执行；已有新断言只覆盖Tips原语入口和ROI接入命中，其他上述出口仍须补齐，不能称整批完成。