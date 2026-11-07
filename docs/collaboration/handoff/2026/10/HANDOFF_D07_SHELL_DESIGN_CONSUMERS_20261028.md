# D07 原结果Shell图像所有权设计消费者交接

- 基线提交：410748cd；公共Shell签名由root同批退役旧DP后集成。
- 当前分支：codex/d07-native-roi-host。
- 执行者：Codex / source_editor，独立workflow-overview树。
- 目标：设计时同原Shell原视口/typed帧组装，不保旧InputImage/OutputImage绑定。
- 允许文件：ResultWorkspaceDesignView.xaml.cs及NativeResultCompositionTests.cs的原Shell图像断言；未改实际Shell、XAML、Runtime、Contracts或公共NativeResultViewport。

## 已有—差异—决策

原设计把私有SampleImage:IImageData经Shell.InputImage/OutputImage DP绑定原件ImageDataSource；与公共单owner viewport并存冲突。新设计删除该SampleImage平行数据实现，复用既有NativeRoiSamples.Image（真正IReadOnlyImage、Frame/Domain、独立pixel/domain reader），每侧均以NativeResultViewport.Attach(Workspace.InputViewport/OutputViewport)连接同原ImageViewBox再BindImage，不另造预览控件或主程序依赖。

原Input/Output来源items与独立选择、双图布局、属性树、来源条、帮助/禁用历史入口保留。设计样例公开InputRaster/OutputRaster与InputViewportOwner/OutputViewportOwner，便于设计样例审阅真实资源，不是旧Shell DP兼容桥。

## 生命周期与回归

- 实际Loaded创建两份独立源及两个owner，固定本次代次/owner current闭包；无workflow/算法执行。
- Unloaded先撤代次与引用、释放两owner再释放两源；独立取得的pixel lease仍有效。重载同一原视图重新Attach新owner与新物理Frame，来源选择和Shell布局不被重建。
- 设计View实现IDisposable，最终移除事件并释放资源；未加载时无像素owner，接入失败同一完整清理链。
- NativeResultCompositionTests原4个InputImage/OutputImage断言改为实际挂点/owner、typed Frame及原ReadPixel结果。原Dark/Light与重载测试语义保留，并在同重载用例核Unloaded后owner/源退役、独立pixels存活、reload新owner/Frame和原视图一致。没有新增镜像路径测试或改变其它树表行为。

两C#纯语法0错、git diff --check通过。当前分支Shell尚无新属性属冻结合同候选，必须与root删除旧DP/绑定并提供InputViewport/OutputViewport同批集成；未语义编译、未运行、未VS设计器、未人工验收，不冒称同源通过。不构建、不写Release。主线统一规定UI/full及原结果组合实际Loaded/主题/reload回归，并另列VS设计器验收。
