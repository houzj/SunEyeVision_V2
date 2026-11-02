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
## 实施前只读复核补正：正式原像素与保护条件

NativeResultImageFrame的PixelFormat/Buffer为Domain打透明的显示BGRA拷贝，不代表原图；正式视口导出从PrepareOriginalBitmap取得OriginalLayout的Gray8/Bgr24/Bgra32原像素。Gray16仅存在于IRangeImageData备用入口，本批分别验证，不宣称正式视口已接入16位。Gray16原图PNG保真；原件未证16位绘色强度规则，带图形16位导出暂保护拒绝，不自造0..255到0..65535映射。

BGRA是本方必要平台适配，非VM原helper已证输入：保留未绘像素的B/G/R/A；图形按原正opacity实色覆盖，四通道绘制显式alpha255，不因三参数Scalar的默认第四分量0把图形变透明。AA边缘按当前库规则记录；若备用入口给预乘格式，先明确转换到直alpha。PNG保位深和alpha；Gray16的BMP/JPEG、非全不透明BGRA的BMP/JPEG若当前编码路径不能保真则保护拒绝，不静默降8位或丢alpha；全不透明BGRA转BGR可作为明确平台路径。

文字保护仅在原实际会触发native的分支发生：顶层可见性与非IMultiPoint整体opacity0先排除，Tuple内部不另加visibility筛选；IMultiText内部不统一逐项opacity过滤。IText的右/下越界、模块错误、空串按原对应重载，坐标轴零向量不强造文字；角度使用的另一DrawText重载没有IText右/下越界排除，不能借此绕过保护。缺字不能假称成功。

原轮廓IMVS_PATMATCH_POINT_INFO为float X/Y/Score与int Index，共16字节；按16步长、Score转int与原颜色分支，Index不消费，残余不足整结构按原忽略；不能用12字节点数组。polygon先画闭边再FillPoly，矩形不添加原未绘的fill/skew/index。完整验收加入以上差异的独立像素、零/有效轴、刚好边界/越界及16字节多点包场景。
备用IImageData入口同时纳入本单元：Rgb24_P3按原三平面重排后再换色，不能直接当Rgb24；仅MemoryAddress供像素时按16字节头及checked期望长度/offset边界取独立拷贝，不写回借入源。旧Byte2ImageSource映射方法含未检signed offset和固定三通道限制，不以同名方法存在就当安全基础；仅在导出适配内做有界托管映射读取，未知header字段不新增猜测语义，不宣称传输原子。

MMF采样完成后再次读取16字节头；忙态或头变化即拒绝。原头完整更新协议及ABA稳定代数未证，不额外造同步协议，不能将普通静态映射测试说成并发采样原子证明；正式视口基于已冻结IReadOnlyImage主链，与该备用传输边界分别记录。

## 当前平台包来源（不是原厂ABI等价声明）

两包沿用仓库已用版本；实际缓存nuspec的repository commit为7d3246d82ce206c034c4ecac1fbae752d1e2657d。这里只记录包声明和文件身份，不将其许可声明套用于原VM源码或定制DLL。

|包|版本|声明许可|nuspec SHA256|缓存nupkg SHA256|
|---|---|---|---|---|
|opencvsharp4|4.13.0.20260222|Apache-2.0（本地nuspec声明）|614E51191577890E5A541A90FBA1A24F2DDF1FBB356AA3C9416F03FC9646FE1E|943501DC983C89CA5E74BAFE95D90FF057C58BD556EB933737B0D1E5688A6C12|
|opencvsharp4.runtime.win|4.13.0.20260222|Apache-2.0（本地nuspec声明）|1FF43BA530849B2D0BC1129E20AEF7A4DFE45899C9441B4AB4FC06724BEAABAE|F12E83EBD276A48A035DAB033C74423EBB64A6E6010306C35B7B3A1E82966831|

## 原托管转换已补证（同单元统一校正）

见C17_NATIVE_EXPORT_NUMERIC_CIL_20261103.md。原Point/Size(double,double)均conv.i4向零截断；DrawRect的Width/Height实际走Size2f(double,double)，先int再float，中心明确先float，四角又向零截整。原RotatedRect.Points为托管single算法，非native不可取证；几何适配显式保留该顺序，不能因新库同名而直接替代。LineTypes与色转换枚举值已原PE实读。合并轴向量先float再Normalize的guard修正，一次固定最终源码；现有候选关联回归保留原身份，不移签。最终候选针对完整导出/原视口/屏幕文字，再由主仓正式同版集成回归，避免无差别重跑不相关候选历史。
