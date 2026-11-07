# D07 同一原视口全屏移动子链交接

- 基线：6ff246a0（此前pane从45661f08接入）；独立分支codex/d07-result-fullscreen。
- 写入范围：新增ResultViewportFullscreenScope.cs、ResultViewportFullscreenScopeTests.cs及本文；不改Main/Shell/pane/SDK/公共库/runner，不构建、不写Release。

## 现有实现、差异与接入决策

原Shell全屏命令已经传实际ImageViewBox，NativeResultViewport独占该实例及Frame/层/资源租约；缺的是宿主全屏父槽生命周期。本批只移动同一FrameworkElement，绝不重建视口、读最新结果、复制图片或借第二图像reader。

固定internal ResultViewportFullscreenScope.Open(FrameworkElement view,Window owner)，属性View/Window/IsCurrent及Dispose；CloseError记录WPF关闭事件内的还原错误，显式Dispose仍抛聚合清理错误。root负责命令调用、保scope及切方案/主控件退场Dispose。实际view必须属于传入owner；不能借另一主窗的关闭生命周期。静态弱表按主窗互斥且按view唯一持有，输入/输出切换先还原旧侧，再移新侧。

捕获原逻辑父槽：Panel保存实际index，Decorator保存Child，ContentControl保存Content。ContentControl通过SetCurrentValue临时置空/还原，不销毁绑定表达式。实际原view移到本scope的无边框最大化Window；它的Viewport owner/同Frame回执/独立pane borrow都不更换。正常关闭、Esc、Dispose、主窗关闭和安装异常走同一退场/finally；只还原仍持有本view token且仍在本fullscreen槽位或本次尚未完成安装的view。原槽已被新Child占据或view已转给外部新parent时，后来的业务优先，不覆盖、不从新parent夺回；Panel恢复原index（当前长度不足时插末尾）。旧scope关闭回调/队列及重入切换不能动新scope。

关闭事件内不向WPF dispatcher抛未处理清理异常，保CloseError供宿主诊断；显式Dispose在所有退场动作完成后报告异常。主窗Closing先退全屏；若主窗关闭取消，dispatcher后台解除closing门禁，后续仍可进入，不把一次取消关闭记成永久退休。

## 公共视口实际Unloaded证据

公共NativeResultViewport只有Loaded重装，没有Unloaded退背景/像素lease（Presenters/Display/NativeResultViewport.cs）。NativeRoiAttachment.OnUnloaded（Presenters/Regions/NativeRoiAttachment.cs:315）取消未完成手势并ReleaseFrame，只清本ROI行/shape与刷新；不Dispose会话、attachment或背景owner。OnLoaded:180重新AttachFrame。因此同view移动可以保背景receipt/pane借阅，ROI未完成手势按已有原行为取消，加载后同attachment投影恢复；不绕第二viewport。本批新增实际窗口移动回归来验证该行为，尚未运行，不能只以源码核查当通过。

## 验收矩阵与未做范围

专用17项候选：Panel/Decorator/Content三槽分别Dispose/Window.Close/Esc真实退出（9）；真实NativeRoiSession+BeginRoiEditing跨Unloaded/Loaded仍同Frame/borrow（1）；输入输出互斥及迟到旧Dispose（1）；主窗关闭仅退全屏不偷关pane（1）；原Decorator/Content槽已替换不覆盖（2）；外部新parent不夺回（1）；Content绑定表达式保留（1）；错误owner拒绝及真实Loaded重入切另一侧后新scope存活（1）。每组使用实际Window与原ImageViewBox，资源由FrozenOutputResources及同publication提供，不造测试专属生产入口。

两C# Roslyn纯语法0错、git diff --check通过。未语义编译、未构建、未运行17项或真实VS/人工。root需同Main/Shell/ROI消费者整批集成，在唯一正式Release完成规定UI/full与专用回归；测试fixture/模板/全屏窗口事件的实际结果仍待运行。本批不修改工具业务、几何/文字层或Group，不代表D07整个目标完成。
