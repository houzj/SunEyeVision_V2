# Blob显示链原厂对应与剩余缺口（2026-11-02）
状态：复用现有静态证据的对应审查，不是新运行取证、显示全量等价或阶段完成声明。

## 已有证据与复用
来源：D:/MyWork/REA逆向测试/docs/joint-audit/B08-结果显示配置到视口静态证据-20261101.md，VM 4.4.0 Build20250516@1555331；本仓display-migration-manifest.json保留原件SHA。已有证据覆盖完整链的多个阶段，不能因本方有适配就认定未逆向或要求全部重写。

| 环节 | 原厂定位 | 本方对应及决定 |
| --- | --- | --- |
| 九类显示声明与样式 | Blob AlgorithmTab:2561–2714、Display XML；GetDisplayObjectOperation:25–61；ObjectBase:131–246等 | 正式显示Fields及NativeGraphicDisplayOptions/TextDisplayOptions复用原件；缺实际输出项保护。模块UserData与本方方案Fields是存储接点差异，不能声称持久化实现原样一致。 |
| 本次结果与数组 | FeatureBase:309–394、ObjectBase:622–631 | Publication及实际结果行/element-id连接，状态与未产出数据差异明确保留。原轮次与本方Run/Frame合同需行为对照，不复制VM全局模块服务。 |
| 缓存、刷新与选图 | RenderDataCenter.SetRenderDataToStore:72、SetDataToStore:167、BeforeExchange:221、Exchange:238；ModuleResultView.UpdateRender:677、DrawShapesByStore:1012、ActiveUpdateShapes:1383 | ResultWorkspacePresenter借同轮结果，NativeResultViewport接纳图像与显示层。epoch/lease/proof及原子安装为宿主适配，未证明是原厂同一内部机制。原迟到回调/缓存交换规则集中补证。 |
| 图形绘制 | ImageViewBox.ShapeList→ShapeItemsPage.OnItemsSourceChanged:109、AddShape:174、DrawShapes:265、ClearShapes:425 | 迁入原控件/模板；NativeShapeProjection转换本方数据，Blob旋转框当前是四角polygon，需与原IRectangle的旋转、线宽等核对，不能凭同形认等价。 |
| ROI编辑 | ModuleDisplayDecorator:512聚合ROIList；ModuleResultView:2250–2269路由；ROIItemsPage.OnRoiListChanged:192、OnApplyTemplate:213 | NativeRoiAttachment接入原ROIList/ROIItemsPage；参数草稿/确认/历史为本方宿主接点。屏蔽多形为用户明确扩展，保留差异。 |
| 退出清理 | ModuleDisplayDecorator.DeInit:1049–1119、RenderDataCenter.DeInit:270 | 各租约/附件/窗口退出已自测的结果按既有回执记录；静态DeInit不能证明全部原生缓冲和在途回调释放等价。 |
| 导出 | C17_NATIVE_EXPORT_RENDERING_GAP_20261029.json及原ImageSaveOpenCvHelper | 原OpenCV与当前WPF RenderTargetBitmap路径存在已知差异。该项保留未实现/差异，不与屏幕显示、ROI编辑混为同一完成状态。 |

## 集中补证与实施出口
已于本轮通过既有逆向负责人对话01a15280-de08-7361-80d8-92d9b1ddeb25发送增量请求；发送不等于已取得回件。不启动原VM/SDK，不扩大原测试授权。
1. 原rect到IRectangle角度单位/旋转轴/中心及实际绘制，对本方四角polygon给保留或调整决定。
2. 原多次执行缓存交换/选图/主动刷新、换模块/取消/迟到回调与buffer所有权，给可复用机制和必要宿主接点。
3. 九对象缺项索引/状态颜色、无执行样式刷新、ROIList与ShapeList单双pane路由/叠放。
4. 导出复用原实现及PutTextZh/字体/库版本缺口独立列出。
已有材料足够的分支直接复用；当前条件确实不可证的行为显式记录假设、影响及替换点，不以泛称待证无限阻塞。不把缺基础占位称为功能已实现。

## 长依赖边界
显示依赖实际输出及运行身份；ROI编辑依赖草稿和图像坐标；导出依赖独立原绘制及字体编码。现有正确基础复用，不为完成Blob提前搬全VM框架。基础具备的完整链按自测出口交付；缺基础分支保护占位且完整待办保留。S1/S2/S3不改序，不以本审查代产品修复。
## 本轮已收增量（静态，非运行验收）
逆向负责人已完成并保存在共享工作区；本线实际读取核收，不再停留在“已发请求”状态。
来源：D:/MyWork/REA逆向测试/docs/joint-audit/B08-结果渲染链迁移补证-20261102.md。
- RectShape按中心宽高创建RectangleGeometry/DrawRectangle，先负Skew再同中心Rotate，WPF消费度数，线宽按ThicknessScale；本方四角polygon只可能几何边界对应，笔画、填充、命中、低质量路径等不能据此视为等价。下一项核本方rotated-bounds实际声明与转换，给原IRectangle复用/调整决定，未核前不重写。
- 原单双图路由已证：单图ROIList在结果imageView；双图ROIList在输入imageViewIn，结果仍ShapeList。原ImageViewBox正常模板顺序为图像、ShapeItemsPage、ROIItemsPage、扩展Canvas；需与当前实际路由核对，不凭“共用视口”笼统判断。
- 缓存按执行计数分buffer/spare、原交换含3ms等待与完成标志；不能宣称所有字典交换原子/线程安全。选图替换按是否还被另一pane引用决定退订，主动刷新有具体分支；不推广任意样式变更均自动刷新。
- 原回调缓存部分越界索引回落0及类型默认与直接feature取值分开；不能默默将本方严格拒绝称为原厂策略，也不能未经完整生产合同决策直接引入原默认回落。
- 原迟到回调/取消全部竞态及native buffer ownership仍未闭合，ImageData实体和native provider尚可追，不可写成静态已穷尽。现epoch/lease标明本方保护，替换点为图像提供者/显示会话。
- 导出仍独立：原PutTextZh包装至core_putTextZh已有证据，native实现体、实际加载版本、编码/字形像素未证。不能把候选DLL文件存在当加载证明。
上述发现为后续完整链差异决定，不改变当前数值批次或S1→S2→S3顺序，不宣称原厂数值/像素等价。
来源SHA256：16A829F963FEC77AD7218C68A0E2896764A7783EDC46369047F768F6FAD75F89
