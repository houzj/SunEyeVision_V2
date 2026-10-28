# D07 本地文件显示解码资源交接

- 基线提交：352d1eba；与ROI宿主为同独立workflow-overview树，未修改已有产品文件。
- 当前分支：codex/d07-native-roi-host。
- 当前执行者：Codex / source_editor。
- 任务目标：本地文件预览真正适配现typed图像资源/原视口，不生成运行结果或给ROI授权。
- 允许修改：仅新增LocalResultImagePreview.cs、专用tests与本交接；Main/Shell/Pane/公共SDK/Runtime不改，不构建不写Release。

## 已有—行为—差异—决策

既有LocalImageAssetCache.LoadImageOptimized使用新流OnLoad解码并冻结BitmapImage，ImageCache.ReadVersion是内容SHA版本，复用此正式文件解码入口与缓存。旧Bitmap显示对象不等于算法图像；新helper把实际解码像素转换为真实FrozenImageResource，独立Frame/Domain/布局与pixel lease，不给Bitmap贴算法Frame标签。

FrozenImageResource现有AcquirePixels/AcquireDomainReader各有独立retain，NativeResultViewport.BindImage取得像素lease后无需decoder owner继续存活。复用该资源合同，不新增共享协议或fakeRun/publication。

## 新接口及调用链

`LocalResultImagePreview.LoadAsync(string path,CancellationToken token=default)`后台解码，返回IDisposable owner；`Image:IReadOnlyImage`、`FilePath`、`ContentVersion`只读。Image在owner退出后拒读；已借pixel/domain与原viewport自己的pixel lease继续有效。

宿主以当前选择/epoch核验LoadAsync完成→`pane.TryPresentPreview(decoded.Image,hostCurrent,out error)`同步取独立像素lease→无论成功失败/被新选择取代都Dispose decoded。hostCurrent取宿主选择身份，不取decoder disposed状态；decoder退出不等于背景显示失效。文件预览没有publication，`pane.BorrowCurrentImage()`继续返回null，不能授权节点实际输入或ROI。

## 格式、来源及生命周期

- 正规绝对路径，解码前后复用SHA版本核对；版本改变拒绝安装。每次创建独立真实FrameId，SourceId含绝对路径及SHA，不同路径或不同版本不会伪同一物理帧。ProducerRun/SourcePortId/SchemaRevision均null。
- Gray8及常见灰度位深/灰调色板显示转换Gray8；无alpha彩色Bgr24；有alpha与透明调色板转非预乘Bgra32，保持BGR顺序及alpha。源高位深的显示转换不冒称算法原精度。
- 实际布局按4字节对齐stride分配，CopyPixels填明确buffer；Domain来自该文件解码器新建完整栅格的每行实际有效像素范围。透明是像素alpha，仍为栅格位置，不猜算法Domain；这不是未知借用算法图的尺寸回退。
- 每一阶段协作检查取消，资源创建后的任何取消/异常通过finally释放尚未转移的FrozenImageResource。创建前失败不产生owner。Dispose原子/idempotent，无自定义释放回调或资源插件，故不引入可抛回调伪造测试钩子。
- 同步WIC解码/CopyPixels不可强制抢占，检查点之间可完成后才响应取消；取消/选择取代若在返回后发生由宿主检查hostCurrent并Dispose未安装owner，不以token给帧伪身份。

## 文件与验收

1. src/UI/Services/Images/LocalResultImagePreview.cs。
2. tests/SunEyeVision.UI.Tests/LocalResultImagePreviewTests.cs，4项实际PNG文件回归：Gray8+padding与实际Domain；decoder退出后像素/domain/原pane存活及preview无ROI借阅；BGR/半透明/透明调色板；同路径同mtime内容版本与异路径Frame；预取消/缺文件/损坏文件失败后可正常解码、幂等释放。
3. 本交接。

仅两C#纯语法与git diff检查；无语义编译、Release构建、测试运行、VS/人工。测试依同批真实ResultImagePane.TryPresentPreview，不复制pane产品或伪造publication。root应整合helper消费者后把LocalResultImagePreviewTests挂正式同源轨，规定UI/full和实际本地来源切换/关闭验证由主线集中执行。本交接不代表D07或整个目标闭环。
