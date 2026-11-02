# C17 图像导出完整单元实施设计（2026-11-03）

基线003ef7af9。上一显示命名单元已正式自动自测完成。本批是同一视口控件的原图与带图形导出整链，非Blob算法批，不修改工作流调度、参数模型或现行编辑事务。

## 完整需求与证据

原始入口、数据与分派、字体和编码见C17_NATIVE_EXPORT_DEPENDENCIES_20261103.md（逆向负责人静态交付），原helper SHA954980DB0062E35506753B5FD918AAB2472F2606E71A28AE5F9A3132B50257EE。原右键与程序化入口的质量和可见性有差异，当前产品真实消费为ImageViewBox右键；不为未接入程序化入口增加无用途API。原OpenCV与现有4.13版本不等同，迁移原静态步骤并明确平台差异，不能冒称原机逐像素/编码字节等价。

## 已有实现—行为—决策

|位置|行为对应及差异|决策|
|---|---|---|
|NativeResultViewport.Export.cs、ImageViewBox.SaveOriginalImage/SaveRenderImage|模态前实际帧、全部图层proof与预算；可见ShapeList；取消、重入、关闭保护|复用并保全，不借图像reader或可变图形跨模态；主线只核具体变更|
|NativeImageExport.PrepareRaw/CreateBitmap|受控复制实际图像像素，Gray8/Bgr/Bgra及Gray16原图冻结已验|复用正确快照能力；平台多通道/位深与原件边界单列，不静默窄化本方已支持真实图像|
|PrepareRendered|WPF ShapeItemsPage→RenderTargetBitmap/Pbgra32；alpha/布局与原OpenCV绘制不同|集中替换为按原helper分派的OpenCV绘制，不保留WPF近似导出回退；屏幕显示原件不受影响|
|SavePrepared|WPF编码且JPEG95|当前右键改用OpenCV无显式质量参数的编码；复用冻结位图，验证格式/路径与异常释放，不伪造原程序化95消费者|
|原PutTextZh→core_putTextZh|托管布局、分组和测量可定位；原native字形ABI/实际加载及像素未闭合|本批非空可见文字栅格路径明确受保护拒绝，不自造字体替代，不计文字导出已实现。空字串/已被原规则排除的文字按原规则；含文字轴/角度同样不得漏字后假称成功|

## 数据、所有权、架构和全分派

公共可视原件和导出适配仅Plugin.Wpf，Blob.Wpf不增加绘制实现。Plugin.Wpf显式引用本项目既有OpenCvSharp4同版本及部署规则需要的原生运行包，不引入SDK/UI宿主或算法库反向依赖。候选必须真实restore/build，不复制现成Release或厂商运行库。

沿当前IImageData取得独立冻结位图；渲染在原文件框前完成，临时Mat均受控释放；只冻结纯像素带过模态，不能携带源指针/reader/活跃Mat/可变shape。保留当前绑定与图层退役proof前后核查、原子预算拒绝和同帧替换保护。

按原helper整体迁移顶层IShape与Tuple<string,object>/List<object>分派，顺序一致。覆盖圆、椭圆、扇环、线/贯穿线、旋转矩形（包括退化）、点/微点、多边形/折线、十字、箭头、坐标/角度及IMultiPoint原包布局。只导出入口可见ShapeList，不把ROI编辑附件、网格或ImageLayers拼入。非零Opacity多数按原不作WPF alpha混合、填充按原alpha门禁、线宽/取整/边界均沿原分支，不创造通用统一规则。未闭合文字路径须在完整写入前拒绝，不能静默漏绘；不支持或非法数据明确拒绝/按已证原忽略规则，不能借WPF近似回退。原内存越界、吞写异常不照搬：目标按明确安全适配做长度检查、释放和失败报告，单独记录。

输出PNG/BMP/JPG/JPEG沿实际右键路径。原图灰度保持单通道，结果灰度扩BGR，RGB/平面RGB转换沿原步骤；本方BGR/BGRA/Gray16已支持布局保持数据合同并单列平台适配（例如JPEG不支持alpha/16位不得默默丢失）。文件选取保留覆盖确认。编码完成后写文件；可采用同目录临时文件原子提交保证失败不损原文件，明确属于宿主安全适配并测试清理。导出动作不改参数、CurrentValues、Dirty、历史或方案文件。

## 文件边界与唯一写入者

沿用一个现有候选worktree D:/MyWork/line-aiproject/blob-display-neutral-20261103，新分支codex/blob-image-export-20261103；唯一实施线display_neutral_implementation。只允许修改NativeImageExport及专用新partial/helpers、NativeResultViewport.Export.cs、当前ImageViewBox两导出方法、Plugin.Wpf.csproj必要依赖、专属导出验证/必要现有消费者断言与验证runner专轨。不得改Catalog、主仓O06、工作流、Blob字段和算法。主线负责证据/合同、Catalog、原方案及集成；只读评审不写产品。保留候选原有CURRENT_TASK未提交内容。

## 整体验收与未实现

真实Blob发布图像与可见结果→同帧快照→右键导出命令/文件选择→原图或OpenCV绘制→编码→写入→解码像素/尺寸/格式，不能只测helper可创建文件。文件对话框可由受控非视觉选择边界注入测试；另记自然系统对话框人工验证，不以人工作为实施闭口前置。

专轨一次覆盖：单/三/四通道与Gray16真实合同；原图不含图形；Tuple多类型、正常/退化/正负角矩形、正负扇环、多边形Fill/非零opacity、微点/十字/可见性；PNG/BMP无损像素，JPEG编码默认政策并记录库差异；文字/含字坐标角度受保护。沿已冻结快照验证换源、同帧重用、新结果、取消、关闭及通知重入；源图、配置、历史不变；异常写入/释放/临时文件不泄漏。已有视口/显示/十字/工作区/混合B08/预览相称回归，不把原屏幕文字测试继续借‘导出WPF截图’充当屏幕证据，应改用实际屏幕渲染观测并另验文字导出保护。

候选构建/自测及真实加载身份与主仓正式分开；主线集成后规定UI+全量Release和同版回归。原文字native、原机像素与编码版本等价继续列未完成；本批支持范围可按自动自测交付，保护不计功能实现。原C17完整范围、B08及S3不删，不扩建无关通用平台，不重复迁入底栏/窗口。