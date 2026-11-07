# AI 协作交接单：所有卡尺首次分辨率适配

- 基线提交：ce71b606f83331f6d508147ec060cd3ce46bf6d1。
- 当前分支：feature/comm。
- 当前执行者：Codex单写入，无跨AI交接，无提交推送。
- 任务目标：所有带卡尺工具无图不可绘制，首次取真实绑定图像分辨率初始化；手动调整后重画、切图、重开保留。
- 允许修改目录：src/Plugin.SDK、tools及内部协作文档。
- 已修改文件：CaliperSizingState.cs、MeasureGuideParameters.cs、ToolDebugControlBase.cs、MeasureEditorControl.xaml.cs、MeasureGuideParameterPanel.xaml.cs、MeasurementEditorManagerControl.xaml.cs及7个卡尺工具调试控件、CURRENT_TASK.md、本交接单。

## 已完成

| 工具 | 卡尺槽位 | 接入 |
| --- | --- | --- |
| LineDetect | 0 | 首次实际输入分辨率适配，删除绘制结束重置 |
| CircleDetect（含圆弧） | 0 | 同一策略适用圆／弧，删除绘制结束重置 |
| CircleCircleMeasure | 0、1 | 独立初始化、独立保留 |
| LineLineMeasure | 0、1 | 独立初始化、独立保留 |
| LineCircleMeasure | 0、1 | 独立初始化、独立保留 |
| PointLineMeasure | 1 | 点无卡尺，线卡尺适配；识别旧默认全尺寸10×40 |
| PointCircleMeasure | 1 | 点无卡尺，圆卡尺适配 |

共7个工具、10个卡尺槽位。PointPointMeasure无卡尺，不注入无关逻辑。

绘制资格取工具实际ImageSource引用，通过当前源ToolResults及OutputVariableResolver取得IVisionImage；无源、失败源、空图、无可用预览时提示并拒绝进入绘制。选工具与实际落笔各校验一次，采集期间来源结果更换则拒绝。预览尺寸不同或可信帧身份不同亦拒绝，不再用“主图有任意图片”替代输入绑定。

首次落笔前初始化全尺寸：`s=min(image.Width,image.Height)/640`，法线全宽`clamp(40*s,10,512)`，切线全长`clamp(10*s,4,256)`，数量保持。存在位置修正缩放时先除以显示缩放比例再写参考参数，确保不重复放大。初始化使用原始图像像素，不受窗口尺寸、DPI或缩放影响。绘制预览使用初始化尺寸显示测量带，完成时不重置。

参数基类保存CaliperSizing状态，每个元素独立登记。新对象初始待适配；序列化包含该状态，重开和执行快照不会丢失。旧JSON未含该字段，反序列化回调标记PreserveExisting，不猜测原尺寸是否手动、不改旧方案数值。首次绘制前已通过面板／手柄调整尺寸则登记保留；外部传入非默认尺寸也保留。参数恢复不当作手动修改，数量修改不单独锁定尺寸。当前尺寸一旦初始化／用户接管，换图、重画与重开不会自动重算。

切换实际显示图像会取消并回滚未完成绘制／拖动；绑定或修正上下文改变也取消未完成交互，但不重置保存尺寸。单编辑器补齐与双编辑器相同的切图生命周期。旧InitializeGuideCaliberParamsForImage公开入口改走相同策略，不保留直接从预览Bitmap取尺寸的旁路。

## 验收、边界及下一步

- 已执行构建与测试：最终全量Release成功0错误1260警告（68.32秒），随后独立UI Release成功0错误1064警告（19.02秒）。23个插件主DLL及SDK／UI发布DLL与各自编译中间DLL的SHA256全部一致；git diff --check通过。本轮遵用户既定要求不新增／运行测试、不启动程序或VS设计器、不连接设备。最初构建的屏蔽控件不存在CancelDrawing错误已改为其公开EnterIdleMode并完成重建。
- 人工验收结果：本轮暂无，不宣称画面已验证。
- 未完成：真实方案与窗口人工验收；没有新增“按当前图像重置尺寸”的UI按钮。自动尺寸为初始化启发式，不代表适合任意目标特征的最优测量参数。
- 已知风险或禁止改动：未知图像谱系但同尺寸的预览保持旧插件兼容，不能保证其与输入图像同一帧。旧方案保持原像素尺寸，不因升级或换分辨率静默重采样。已有尺寸不随线长／圆半径继续自动变化。保留全部先前修改及plans/plan.json，不操作真实方案／设备。
- 下一步：如以后获准运行，再以不同分辨率、两个槽位、无图、绑定与预览不同、手调后重画／切图／保存重开进行人工验收。
