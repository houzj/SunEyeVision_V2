# Blob原矩形与结果视口完整调整设计（2026-11-02）
基线：主仓1e9d7ad46；复用候选工作区，分支codex/blob-render-chain-20261102。数值命名批已正式自测完成。本批只处理已核显示链差异，不改变HALCON算法、输出JSON/方案字段、ROI编辑业务、连续/重置或其他工具。

## 证据和复用决定
原证：REA docs/joint-audit/B08-结果渲染链迁移补证-20261102.md及仓内BLOB_RENDER_CHAIN_ALIGNMENT_20261102.md。原RectShape消费IRectangle的中心/宽高/角度/倾斜及样式，原结果ShapeList只在输出imageView，编辑ROIList单图输出/双图输入，模板层次图像→Shape→ROI→Canvas。
已有正确链：NativeShapeProjection.RectangleProjection实现IRectangle，原RectShape及样式已迁入；RoiEditing路由及模板叠放已符原证；同次Publication、帧/坐标域、租约、场景原子替换和清理继续复用，标明本方保护而非原机制。

## 已证差异与成片实施
1. BlobSession的SmallestRectangle2经BuildRotatedRectangle产出固定顺序4角点，Schema/ResultDisplayDescriptor仅存这些点；Presenter目前转PolygonGeometry，丢失原矩形绘制类型。合同将Quadrilateral明确命名为RectangleCorners，不保留旧别名；输出值与持久格式不改。公共NativeGeometryOverlay增加明确矩形角点呈现标记；只有该声明的4点走原IRectangle投影，其他真实Polygon不变，不按外观猜类型。
2. 原矩形投影从4点计算中心、两相邻边长及角度，校验有限值/完整顺序/闭合对边/直角，采用有界相对浮点容差；同帧/像素域/样式及全景预算仍统一。正常宽度以首边方向atan2，宽0从另一边确定同几何角度，全0稳定取0。允许非负宽高：原RectShape未拒0，不能套严格Positive的OrientedRectangleGeometry而清空合法图文。不全局放宽几何合同、不回退轴对齐框、不悄悄降为多边形。该数学重建是本方数据适配，不宣称HALCON原始phi保留或VM算法角度等价。
3. ResultWorkspacePresenter.Display只为_output接纳正式结果图文；先清除_input旧常驻Display，保留独立图像/编辑ROI/辅助十字和各租约。单/双图切换、同帧/异帧均核当前owner，旧回调不能重新安装输入结果图文。当前选择高亮的双pane适配不在本次原证直接覆盖范围，不擅改或称原厂等价，独立选择层隔离回归保留。
4. 仍用现有整景prepare/proof/预算/安装/失败清理；不新增renderer、全局服务、第二可写外观或资源所有者。退出和取消通过既有lease完整退役。

## 验收出口
- 投影：实际原IRectangle/RectShape、高/低质量原绘制路径；正常旋转/0宽/0高/全0、非法非矩形拒绝，样式/帧/预算和旧proof。
- 真实Blob：单像素、横/竖/斜细线及旋转区域，经实际HALCON输出记录4点与投影；一项退化不使全帧其他文字/矩形消失。不预断本机HALCON返回值。
- 正式组合：原结果颜色/透明度/宽度、文本、ROI/屏蔽/扇环共存；相关旧测试按实际角色/同次声明核对，不以总类型数量误判。
- 单/双图：同物理帧输入不安装结果图文，输出仍显示；异帧拒绝；ROI编辑仍单图输出/双图输入；切换/重跑/取消/关闭/旧租约不清新景，独立十字/高亮不误删。
- 既有ResultDisplay完整轨、B08混合整窗、ROI继承/预览受影响范围；先候选UI/全量及相称验证，固定提交后集中主仓正式Release，不复制现成程序。VS由用户单独验。
- 导出OpenCV/native、像素及VM生产角度等价不由本批证明；连续/重置与原A00–A23清单继续保留。

状态：002候选UI/全量构建已通过；Render专项3、完整图文All44、运行层19、真实结果工作区4已通过并核实际加载SHA。B0812、十字7及ROI预览24也已通过，六轨加载SHA逐一匹配；待固定提交和集中正式Release。旧类型/总数测试前提的失败保留，不将测试修正当产品修复。
数值审查补充：长度使用缩放hypot避免平方溢出；闭合/直角容差同时计边长相对误差和输入坐标16 ULP舍入上界。加入中心1e9、半长10/0.02、角0.7的已知误拒样本，并保留明显非矩形拒绝；不以无限放宽容差掩盖错误。

复审保护：坐标16 ULP若超过最短非零边的千分之一则明确拒绝“精度不足”，避免把不可判定四点硬转矩形；全0仅接受四点完全相同。补1e16同向平行四边形拒绝，并核正负角及原高/低质量Transform实际还原四角，不将离屏大坐标算视觉验收。真实斜线样本用4/8邻接均连通的台阶线，面积oracle为1/16/16/21/61。
