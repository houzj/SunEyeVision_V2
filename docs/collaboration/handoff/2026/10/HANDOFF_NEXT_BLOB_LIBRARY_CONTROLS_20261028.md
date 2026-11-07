# Blob必需控件后续完整批（只读核证，不代表实施）

主仓唯一产品写者，三条只读线自动补位所得。当前C08四类手势修复不替代本清单；以下仍在阶段一，Blob业务不改。

> 2026-10-28 范围更正：用户最终归档 `references/vm-blob-ui-target-20261027.html` 定义全域ROI→矩形→圆环→多边形→扇形，无CircleMes普通圆。屏蔽区采用相同五种结构与图标顺序。以下普通圆方案仅留历史证据，禁止继续作为Blob第一阶段前置。当前先收口Assist十五角色，再补全域/无屏蔽的明确组状态和C17。
## 历史候选：普通圆 CircleMes（已由最终Blob目标排除，不作为前置）

- 激活原CircleMesROIStack.xaml/.cs.pending，原三项均SliderArrangeFloat、TwoWay尺寸MultiBinding。X/Y分别乘ImageWidth/Height，半径乘ImageWidth。
- 新NativeCircleRoiProjection复用唯一pending/stamp模型及INormalizedCircleMesROI；Fields为x/y/radius，PropertyNames仅Radius，GeometryKeys为radius；中心继承基类。Prepare按原动态范围X[-0.5W,1.5W]、Y[-0.5H,1.5H]、半径(0,1.5W]校验，仅更改编辑过字段，保其余精度。不调用硬编码±30000的PrepareCenter。
- 形状规范已有kind=circle。补ProjectionType/Create、Session准入/BeginDraw/ValueChangedCommand及CircleMes_ModuleEnableA、CircleMesA、Editor.Context转发、原RoiSelect资源和CircleMesTemplate/参数模板。
- 图标使用Geometry_circle_clickoff/on.png，绝不能用Ring图标。原选择器筛五形状顺序SectorRing→Sector→Box→Polygon→CircleMes；这不是VM Blob原四按钮声明。实施时仍须按用户最终vm-blob-target归档核决定呈现顺序，不把原通用选择器推断当用户目标。
- 原CircleMes/NormalizedCircleMes活动手柄改用本批同一gesture/template/layout proof，不再手抄旧Cancel；Attachment补样式/刷新/取消识别。绘制复用原单圆CircleROISubstitute，创建center/radius；DesignData索引上限3→4并补第5形状样例。
- 验收原图标/三行、非方形帧精度、Enter单写失焦、真实中心/半径手柄与绘制、拒绝恢复、重入取消、菜单历史、真实设计样例；不能用CircleROI圆环用例替代。

## 当前批 C09 Assist 三方式15角色组合

- 所有值仍由DeclaredFieldState唯一持有。建议NativeAssistOptions包含EnabledFieldId、ModeFieldId、FixturePort、BasePointFieldId、RunPointFieldId及Base/Run各五项(X/Y/Angle/ScaleX/ScaleY)。FixturePort.Id即角色身份，不重复定义，不在库内造VM FIXTURE/默认Affine2D。
- 一次预检15个真实唯一ID、同页同节、Enabled必填常量Boolean、Mode必填整数0/1/2；两POINT真实同坐标Geometry引用、十数值Float32来源；只有声明Constant|Reference才接InlineConstants。不补默认/角单位/矩阵算法。角色不得与Pair/Dialog/radio/ImageSources/图文组合冲突。
- 条件：Mode依Enabled；Fixture模式0；两个Point模式1；四X/Y模式2；六Angle/Scale模式非0。模式切换不统一清隐藏来源。
- 原关闭触发只清十float+CorrectInfo+RunPoint共12来源，OriginPoint未列不得擅清；FixtureInitPoint是显示目标，不属15编辑角色，关闭撤对应显示层。DoInitOperaion是重新装载当前IO选择，不是重置默认值或解算矩阵。
- SubmitCore在TrySetGroup前扩展完整关闭提案，再整体验证/接纳，不能writer清值而字段state不清。历史直接回放完整accepted字段，不再次触发关闭清空；重取正式引用/物理Frame证明。
- 当前DeclaredFieldsView已形成独立十五角色候选与真实NativeAssistScene。正式SettingsView接入前必须补完整Reference/Unset权威映射贯通草稿、捕获、Host、保存重开；不能只在ToolSettingsViewOptions尾加选项，也不能靠私有_unset/null/default缓存兜底。直接FieldsView通过不等于正式持久化合同已完成。
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


## 2026-10-28 再核原始证据与后续实施约束

### C08 全域组状态（未实施）

- 最终归档HTML SHA df78ec69e42eb8c0585eabcead0f52f046d77168fa476fca0bf6a2774bd0c44a，ROISelection="Box SectorRing Polygon Sector"，FullScreenEnable=true且前插FullScreen，屏蔽组复用；最终五按钮顺序FullDomain→Box→SectorRing→Polygon→Sector。原WPF WrapPanel的Full→Ring→Sector→Box→Polygon仅作差异证据，不覆盖用户确认目标。
- RoiEditSnapshot增加必填冻结Groups；GroupCoverage=None/FullDomain/Explicit，Explicit当且仅当组内有rows，另两态无rows；所有row组必须已声明，不从空列表猜整图/无屏蔽。
- SetCoverage组事务：FullDomain清本组rows及已删除选择，其他组不动；画完/粘贴→Explicit，删末项→None，取消/失捕/拒绝不动覆盖。FullDomain是实际图像EffectiveDomain，不造矩形或假对象。
- NativeRoiSession Capture/Install同版比较和Commands.PrepareCommand取消后复核必须包括Groups；纯Coverage变化Affected可空但须核组差异。SetDrawingGroup转发FullScreen状态，原Toggle不直接改已接纳根。
- 实际消费者：NativeRoiDesignData、NativeRoiInheritanceSample及History/Components、O06WindowPresentationCases全部自有owner。继承根保LocalItems+LocalCoverage，回本地恢复；fields/rows/groups/selection同历史事务、revision单调。当前UI/Blob没有直接消费本合同，库重建不冒称产品持久化完成。

### C09 三态权威映射（未实施）

- 复用c7b1dea6设计；字段ID→ConstantSetting/ReferenceSetting，缺键=Unset，UnsetSetting仅删除提案。Constant(null)/空序列/零各自独立。typed settings只确定性物化，不另存备份或重填默认。
- 完整批必须涵盖Contracts Draft/捕获、ToolSettingsEditSession、DeclaredSettingsView/公共工厂、RuntimeToolConfiguration/JSON、Host/历史、编译/执行/私有调试与所有工具包装，不能只WriteGroup支持Unset。初始默认仅新建一次，恢复不补默认。
- 四个已核漏项：ReferenceSetting必须保原BindingConversion；WorkflowFragmentCopy显式重映射map语义引用；SolutionResourceResolver从typed FileReferences/SelectedReferences改读稳定local.paths/local.selected Constant序列；保存全部声明字段而非仅可视字段（ImageSource11设置/6展示）。
- RuntimeInputBindings统一枚举输入端口ParamSettings与设置map引用，两域去重；WorkflowBindingContext/源失效/全局生产绑定/根代理/依赖编译都走统一入口，不能复制回旧表作桥。编辑期required Reference是已配置未解析，不能先typed物化而补默认；配置期与执行期边界同批定清。

### C17 导出（生命周期候选，原算法迁移仍待）

- 权威原helper为Apps.Data.Common.ImageSaveOpenCvHelper，归Apps.Data.dll，不是曾由using推测的Apps.UIHelper.Extern。安装件Apps.Data.dll SHA2ad51c179d4a5cf7e0c0373cd1526de8a75d53a3ea6cbe3ef73c0c5268e1c55c，MVID cb38f3bc-780b-400d-89f5-0ba51ce23489。
- 既有REA完整归档：D:/MyWork/REA逆向测试/docs/architecture-recovery/image-export-entry-audit.resources/Apps.Data.Common.ImageSaveOpenCvHelper.decompiled.cs，SHA954980db0062e35506753b5fd918aab2472f2606e71a28ae5f9a3132b50257ee；CIL SHA3496589e41ca17d3b459cf5e6c451436766fa3aff3c39d04de6a8101b7f05fc0。未执行VM。
- 原主入口只image+可见ShapeList，不含ROIList编辑层、ImageLayers独立栅格、背景/网格/zoom；不可偷换为整视口截图。原helper为OpenCV，各线宽至少2且坐标截断，Text字号max(图宽1%,声明font)并10..1000限幅，另有换行/中文字体规则；默认JPEG走OpenCV默认，当前WPF95并不等价。原底层PutTextZh/字体实机像素仍未证。
- 本批先保安全冻结：ShowDialog前完整准备，后只编码；raw从已有独立pixel lease同步短读保原Layout/Domain外像素，render保现原ShapeItemsPage算法，不能把后者算原OpenCV迁移完成。跨模态不持reader。
- 末次两个独立图层proof可互相改变未通知状态，单靠有限遍历不能证明整场景同一来源代；后续统一来源generation/失效通知合同，不能无限重复读proof冒充事务性证据。当前七组只证明已列生命周期分支，不解除该缺口。

### C17 Cross配置（未实施）

- 正式net9.0-windows已有PresentationFramework.Aero/ListBoxChrome（WindowsDesktop9.0.20参考闭包），不用厂商DLL或Border近似。
- 激活原CrossConfig.xaml/.cs和CrossData，380×420、两模型/同一非模态窗口；只合OriginalTheme+Localization+BaseWindow style+现SourceScrollBars+原SwitchButton及ListViewStyle1局部切片，四个厂商URI退出。
- SwitchButton仅swith_on/off.png；ListViewStyle1继续正式ListBoxChrome/RecScrollViewer。原颜色CurrentColor(SolidColorBrush)↔Stroke(string)本来依WPF BrushConverter，先保原TwoWay，验证选择后BindingExpression仍在；必要时保绑定赋值，不造新色板。
