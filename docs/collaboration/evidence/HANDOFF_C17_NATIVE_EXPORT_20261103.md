# C17 原生图像导出完整单元候选交接

- 基线：1e7c59f1f8becf278aad499e742fed36920074f4；独立分支 codex/blob-image-export-20261103。
- 实施工作区：D:/MyWork/line-aiproject/blob-display-neutral-20261103；唯一产品/测试写者 display_neutral_implementation。
- 完整合同：C17_NATIVE_EXPORT_WHOLE_DESIGN_20261103.md；原 helper SHA-256 954980DB0062E35506753B5FD918AAB2472F2606E71A28AE5F9A3132B50257EE。

## 已有能力与本批迁移

保留原模态前快照、实际帧身份、全图层 proof、预算原子拒绝及 reader 释放链。正式视口的结果导出改从 frame.PrepareOriginalBitmap 获取真实 Gray8/Bgr24/Bgra32 原布局，不把 Domain 显示副本当原像素。文件对话框只接冻结 BitmapSource，不携带 Mat、源 reader、可变 shape 或共享映射句柄。

NativeImageExport 的 WPF 场景截图与 WPF/JPEG95 编码已移除。专用 Drawing partial 集中迁入原 helper 的整个托管分派：顶层 IShape/指定 Tuple<string,object> 的 List<object>、原接口顺序、父/子 opacity 条件、圆/椭圆/扇环/旋转与退化矩形/普通与贯穿线/点/十字/箭头/多边形/折线/IMultiPoint。多边形先闭边后 Fill；矩形不添 fill、skew 或 index；IMultiPoint 父 opacity0 仍走子项，轮廓严格16字节（3 float+int Index），Score 强转 int 决定 OK/默认黄色NG/红色，Index 不用，末尾不足一结构按原忽略。

顶部可见性仅在原菜单入口过滤，Tuple 内不擅加过滤。ROI 编辑附件与 ImageLayers 不进入导出。非零 opacity 的几何按原实色绘制，不保留原 WPF alpha 混合近似。屏幕原件显示路径不改；原屏幕文字测试已改为观察实际 WPF 视口，不借导出截图冒充屏幕证据。

右键编码使用项目既有 OpenCvSharp4/运行包 4.13.0.20260222，ImEncode 无显式 quality；未新造无消费者的程序化95入口。编码成功后同目录临时写入/Flush/原子替换，异常报告与 finally 清理属于明确的宿主安全适配。必要内部文件选择/错误报告非视觉测试边界走实际 SaveOriginalImageCommand/SaveRenderImageCommand，不绕开原菜单命令。

原 OpenCv 几何构造已另核 CIL（C17-原版OpenCv几何转换CIL证据-20261103.md）：Point(double)/Size(double) 向零截整，矩形 Size2f(double)先 int 再 float，中心保 float；本适配显式复现原 single 顺序的 RotatedRect.Points，再将顶点截整。坐标轴按原先 float 再 Normalize/非NaN，极小向量下溢不会误触文字保护。已用独立像素 oracle 验正负小数坐标、椭圆尺寸、小数矩形宽高与中心保小数、下溢轴；枚举值与原 CIL 元数据一致。仍不声称原 native 光栅/编码版本逐像素等价。

## 平台适配与保护（不计作原厂完整功能已实现）

- 正式 NativeResultViewport：Gray8 原图保持单通道，结果扩 BGR；Bgr24/Bgra32 按真实原像素导出，包括 Domain 外像素。BGRA 是本方附加平台支持，未绘像素保 RGBA；绘制第四分量显式255。AA边缘按4.13实际规则，不宣称原版同态。
- 灰度16位仅 fallback IRangeImageData/Gray16 原图 PNG 保真；正式视口主链未接 Gray16，不冒称已接。16位带图形渲染与 BMP/JPEG 明确受保护拒绝，不发明色映射或静默降位。
- BGRA PNG 保 alpha；非全不透明 BGRA 的 BMP/JPEG 明确拒绝，全 A255 时明确转 BGR。Pbgra fallback 明确转直 alpha 后编码。
- fallback Rgb24_P3 在独立 clone 后按原三个平面重排交错；普通缓冲不改借入源。MemoryAddress 按原32位长度、busy位、offset14的Int16读16字节头；checked长度/offset>=16/实际容量验证，独立拷贝后重读头并拒绝 busy/变化，句柄 using 释放。双头仅有界尽力采样，未知ABA不提供并发原子承诺，不新增共享传输协议。
- 原 PutTextZh 原生字体 ABI 未闭合，原分派实际需要绘制的非空文字及有效坐标轴/角度明确拒绝；空串、模块错误、IText X>width/Y>height、父opacity0等沿原条件排除，边界等于尺寸仍受保护。IMultiText 内不额外按子opacity过滤，角度的字符串重载不套 IText 越界排除。文字保护不计文字导出已实现。
- 非有限/不安全数值、负几何尺寸、无效映射、编码不支持及文件失败做明确安全拒绝，不照搬原内存越界或吞异常。原包与4.13版本差异、原机像素/字形/编码字节等价仍未证。

## 构建与测试记录

机器回执由 C17_NATIVE_EXPORT_CANDIDATE_20261103.json 汇总，包含最终实际加载路径/SHA、额外测试源码SHA、原 helper SHA、OpenCV托管与native实际加载身份、构建和失败记录。候选不是主仓正式验收，VS与自然文件对话框用户另验。

- restore001 无网络源失败、restore002 配置含不存在本地nupkg源失败均保留；restore003 直接用本机已存在 NuGet 缓存目录作为本地nupkg源，完整恢复成功。未复制 assets、DLL、Release 或厂商运行库，未更改仓库 NuGet.Config。
- 11份真实生成 packages.lock：Plugin.Wpf/Wpf消费者传递新增同版 OpenCv/System.Memory；其余依赖图增加 Plugin.Wpf 依赖说明。BlobWpf/BlobTests还规范化 Runtime 项目 key、移除旧多余桥项目 entry，无包版本变化；保留真实恢复输出，不手造旧锁。
- UI001 两个 pattern 变量重名失败已集中修正。NativeExport001/002 测试宿主命名空间/using错误保留；003锁文件测试仅接受IOException过窄，实际UnauthorizedAccessException已明确纳入文件系统失败检查。未据此改产品吞错或放松旧文件与临时文件断言。
- 最终完整 NativeExport 专轨23组为14组本批导出及9组真实Blob夹具前置，不把23全部称独立导出。几何另有±角矩形协方差、±90扇环端点/中心孔、小数线宽最少2的独立像素检查，不仅 AnyInk。

取整修正前中间版本：UI003/full002零错误，六轨33/44/7/4/12/24=124通过；导出23通过。最后屏幕文字观测仍沿旧图像导出黑底断言而失败，日志保留；现以实际视口的透明文字图与同视口清空文字基底逐像素相等替代该断言，不删除OK/NG/透明度/XY/缩放检查。随后屏幕XY仍用旧image-pixel比例而失败的008回执亦保留；009统一用ShapeItemsPage→NativeView真实变换的坐标差分，容差仍2像素，通过后产品未变。中间回执保留原SHA，未移签最终DLL。

最终取整/轴guard集中修正后 UI004/full003零错误（警告6490/6439）；只对变更跑完整导出23、原视口33及独立屏幕文字1，全部通过。实际Wpf SHA D315410A83FE3CBFFCEC28645BED309EDD572C2DE45B3ED4D8F75B22B2AA6863，MVID b5dc9a99-ff62-4b9b-bd5b-3f912835533e。主线接续统一正式六轨；本线没有对无关整窗历史再重复全批，也不把中间六轨称最后DLL已通过。

## 交接边界

主线负责 Catalog/O06 原方案和正式集成，不在本线改动。原 CURRENT_TASK 脏内容保留，本线只附加未提交记录避免覆盖主线。本批按已有控件完整导出链交付，未展开 Blob 算法、共享图像架构、参数/工作流合同或其他原件家族。

候选记录实际 rawSHA；Git按现有gitattributes规范内容与行尾。若checkout改变raw行尾，不把候选源码rawSHA直接视为主仓已验，主线应以自身正式构建/回执为准。为避免纯行尾导致无行为收益的整批重建，本批不在绿回归后另做行尾修饰。
