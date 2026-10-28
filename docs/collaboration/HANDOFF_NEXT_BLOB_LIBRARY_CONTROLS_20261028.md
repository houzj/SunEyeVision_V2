# Blob必需控件后续完整批（只读核证，不代表实施）

主仓唯一产品写者，三条只读线自动补位所得。当前C08四类手势修复不替代本清单；以下仍在阶段一，Blob业务不改。

## P0 普通圆 CircleMes（不是 CircleROI 圆环）

- 激活原CircleMesROIStack.xaml/.cs.pending，原三项均SliderArrangeFloat、TwoWay尺寸MultiBinding。X/Y分别乘ImageWidth/Height，半径乘ImageWidth。
- 新NativeCircleRoiProjection复用唯一pending/stamp模型及INormalizedCircleMesROI；Fields为x/y/radius，PropertyNames仅Radius，GeometryKeys为radius；中心继承基类。Prepare按原动态范围X[-0.5W,1.5W]、Y[-0.5H,1.5H]、半径(0,1.5W]校验，仅更改编辑过字段，保其余精度。不调用硬编码±30000的PrepareCenter。
- 形状规范已有kind=circle。补ProjectionType/Create、Session准入/BeginDraw/ValueChangedCommand及CircleMes_ModuleEnableA、CircleMesA、Editor.Context转发、原RoiSelect资源和CircleMesTemplate/参数模板。
- 图标使用Geometry_circle_clickoff/on.png，绝不能用Ring图标。原选择器筛五形状顺序SectorRing→Sector→Box→Polygon→CircleMes；这不是VM Blob原四按钮声明。实施时仍须按用户最终vm-blob-target归档核决定呈现顺序，不把原通用选择器推断当用户目标。
- 原CircleMes/NormalizedCircleMes活动手柄改用本批同一gesture/template/layout proof，不再手抄旧Cancel；Attachment补样式/刷新/取消识别。绘制复用原单圆CircleROISubstitute，创建center/radius；DesignData索引上限3→4并补第5形状样例。
- 验收原图标/三行、非方形帧精度、Enter单写失焦、真实中心/半径手柄与绘制、拒绝恢复、重入取消、菜单历史、真实设计样例；不能用CircleROI圆环用例替代。

## P1 C09 Assist 三方式15角色组合

- 所有值仍由DeclaredFieldState唯一持有。建议NativeAssistOptions包含EnabledFieldId、ModeFieldId、FixturePort、BasePointFieldId、RunPointFieldId及Base/Run各五项(X/Y/Angle/ScaleX/ScaleY)。FixturePort.Id即角色身份，不重复定义，不在库内造VM FIXTURE/默认Affine2D。
- 一次预检15个真实唯一ID、同页同节、Enabled必填常量Boolean、Mode必填整数0/1/2；两POINT真实同坐标Geometry引用、十数值Float32来源；只有声明Constant|Reference才接InlineConstants。不补默认/角单位/矩阵算法。角色不得与Pair/Dialog/radio/ImageSources/图文组合冲突。
- 条件：Mode依Enabled；Fixture模式0；两个Point模式1；四X/Y模式2；六Angle/Scale模式非0。模式切换不统一清隐藏来源。
- 原关闭触发只清十float+CorrectInfo+RunPoint共12来源，OriginPoint未列不得擅清；FixtureInitPoint是显示目标，不属15编辑角色，关闭撤对应显示层。DoInitOperaion是重新装载当前IO选择，不是重置默认值或解算矩阵。
- SubmitCore在TrySetGroup前扩展完整关闭提案，再整体验证/接纳，不能writer清值而字段state不清。历史直接回放完整accepted字段，不再次触发关闭清空；重取正式引用/物理Frame证明。
- 最小共享接点ToolSettingsViewOptions尾加AssistCompositions、DeclaredSettingsView传递、DeclaredFieldsView构造角色预检/原radio及条件组装、SubmitCore提案扩展；独立NativeAssistScene沿同链，不造隐藏Editor或第二图像。
- FIXTURE声明真实调用者提供；nativeABI、角单位/顺序、带位姿ROI正反变换合同未证不得猜。UI/引用/清空/历史可以实施，这些缺口仍属于全链未完成，不因UI通过删去。

## P2 C17 原十字配置、受管图像层及导出生命周期

- 原CrossConfig.xaml/.cs和CrossData.cs均pending，原CrossControl活动但缺模板重装/捕获失败/换context/卸载清理，须同完整批修。激活BaseWindow、原SliderArrange、ColorPickerControlView、SwitchButton两图、ListViewStyle1+RecScrollViewer最小资源闭包，不启用整份无关大字典。
- 两条Cross的index1/2，初始隐藏未锁、opacity1、width1；首次有效尺寸Init位置0.4W/H与0.6W/H、颜色#FFF76900/#FFFFFF00，needInit=false。首次打开只启用第一条。原X/Y 0..100000，width1..10/step1；居中用整数W/2,H/2。
- 原打开每次关闭旧配置窗并Show新非模态Owner窗口，复用两CrossData；关闭不回滚、不删除、不重置初始化。不能把十字功能做成bool toggle或OK/Cancel草稿。
- 原放大缩小Enlarge/Shrink、标1:1按钮实际Init，保原倍率/复位语义。全屏搬同一真实View，不新owner/图片。
- 唯一NativeResultViewport受控辅助层合成Cross，不旁路写ShapeList；失效先退窗口与手势，再摘本层，不能清结果/文字/ROI/选择；Dispose逐项异常也完成剩余清理。
- 原图/渲染图导出在模态前必须取得独立稳定租约或冻结导出副本；当前adapter模态后读取可能已退休，需真实换帧/关闭负测，不继续借旧对象。
- 工作流原件补核：背景替换非单纯SelectedOutputItem，已有背景/目标勾选/非当前背景/非运行等条件与确认；原输出1背景+最多3图像叠层，含透明度和z序。公共owner尚缺受管image-overlay入口；不要用RegionLayer代图像，也不由同模板推断输入有独立overlay。
- Cross原源码SHA：CrossData 3cca36302c25dfa4bb09add085989093c59566f37d7d9946e11bbe3b817fbfb1；XAML e0f3d32d94d7bb66745ba06def03f6cea39f37c255ab3ec4a085f71ab201ea1d；CS 5be350a0fd91b5301f0af41b9c91432947d0a8733d3e6a9e0d8e5fd6b251b2f2。原件目录D:/MyWork/REA逆向测试/analysis/decompiled/VMControls.WPF/VMControls.WPF/。

真实VS、实际指针全姿态、VM逐态外观和用户验收继续独立记录。此文件是直接实施准备，不是阶段准入或用户签收。
