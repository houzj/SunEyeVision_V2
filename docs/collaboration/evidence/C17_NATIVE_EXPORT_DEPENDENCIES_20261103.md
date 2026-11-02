# C17 原图与结果图导出：实现依赖及证据边界

范围：VM 4.4.0 的所列原右键入口、程序化入口及 `ImageSaveOpenCvHelper`；复用 `docs/architecture-recovery/image-export-entry-audit.*`、`export-text-rendering-audit.*`。静态证据，不启动 VM/SDK，未修改消费项目或证明编码字节/图形像素等价。

## 入口、输入与释放

| 入口 | 已证行为 |
|---|---|
| `analysis/decompiled/VMBaseControls/VMBaseControls/ImageViewBox.xaml.cs:2154-2176` | `SaveOriginalImage` 在模态对话框前取 `ImageDataSource` 引用；确认文件名后 `SaveRawImageSyn(path,image)`，省略质量参数。 |
| 同文件 `:2179-2203` | `SaveRenderImage` 在对话框前选当前图像，并将 `ShapeList` 中不实现 `IIsVisible` 或 `IsVisible=true` 的顶层项 ToList；确认后调用 `SaveRoiImageSyn(path,image,shapes,null)`。没有从此入口加入编辑 ROIList 或图像叠层。此 ToList 是列表拷贝，未证明图像与成员深冻结。 |
| `VMControls.WPF/RenderControlBase.xaml.cs:1856-1900` | 程序化原图可选输入/输出图，`SaveOriginalImage` 和 `SaveRenderedImage` 默认质量 95、验证 1..100；后者直接 `new List<object>(ShapeList)`，与右键入口的顶层可见性过滤不完全相同。 |

`ImageSaveOpenCvHelper.decompiled.cs:53-187` 同步方法进入私有实现。`CheckData:878-900` 检查目录/小写扩展名、Buffer 或 MemoryAddress、w*h 或 w*h*3 和磁盘空间；MemoryAddress 分支会将读出的数组写回 `imageData.Buffer`。右键的质量默认 0：仅 jpg/jpeg 且质量 1..100 才传 `[1,quality,0]` 给 ImEncode，否则传 null，具体编码默认值取决于实际库；程序化入口传 95。`SaveImageCompressionRatio=70` 与 `GetImageEncoding` 存在不等于该同步入口使用了 70。传非空 controlImage 时另有解码、缩放、纵向拼接与无参数编码分支，所核右键入口传 null。

结果图 `SaveRoiImage` 的 mat 在 finally 中 Dispose；原图 `SaveRawImage` 正常路径 Dispose，catch 通知 ErrorEvent 后 `throw ex`，所核方法没有外层 finally。`WriteByteToFile` 吞写入异常返回 false，调用方未检查该布尔值；不能据此声称出错一定提示或一定泄漏。目标实现可采用明确标识的安全释放/报告策略，不伪称原异常语义。

## 图像和图形分派

`bytesToMat:735-781`：Rgb24 创建三通道并颜色转换代码4；Rgb24_P3 先按三个平面重排，再代码4；其他图像按单通道处理，结果图 `isRender=true` 使用转换代码8 扩为三通道，原图 `isRender=false` 保持单通道。需在目标 OpenCvSharp 版本核 enum 值和缓冲所有权；本体中的 `finally` 释放中间 mat。多种 3 通道布局不能仅按长度当成同一颜色顺序。

`SaveRoiImage:118-141` 遍历顶层 `IShape` 或 `Tuple<string,object>` 且 Item2 为 `List<object>`，后者逐 `IShape` 绘制；`DrawShape:197-345` 对非 `IMultiPoint` 的 opacity=0 跳过，而不是将 0..1 全部按 WPF 做 alpha 混合。分派顺序包含 IMultiText、圆、椭圆、环扇、线、矩形、点、坐标、箭头、角度、多边形、折线、cross、IMultiPoint。普通 tuple 依实际接口逐图形，不等同低质量 bitmap。

| 类型 | 可直接迁移的静态步骤与边界 |
|---|---|
| rectangle | `DrawRect:516-538` 以中心/宽高/角构造 OpenCV RotatedRect，Points 后四条 `Cv2.Line`；线宽整数至少2，BGR、LineTypes 8。未见屏幕绘制的 skew、fill 或 IIndex。 |
| annular | `DrawArc:668-716` 等于360度走两圆；其他范围在起止两端连径向线，再以两次 Ellipse 画内外弧。负角实际 OpenCV 版本表现须验证。 |
| polygon | `DrawPolygon:355-390` 点数须大于2，闭合 Polylines；Fill 非空且颜色 alpha>0 时 FillPoly，线宽至少2。非零 alpha 不代表与背景透明混合。 |
| cross/point | cross 画贯穿图像横竖线；点画长度由 3×线宽决定的十字（最少线宽2，LineTypes 16），区分普通 point 与 cross。 |
| 多文本 | `DrawShape:203-223` 按 Position 分组，每组初始垂直偏移0、逐项累计 `DrawText` 返回高度；`:540-627,938-947,971-980` 排除模块错误文本，字号 `max(int(width*0.01), font)`，<10 或 >1000 回10；WPF FormattedText 用 InvariantCulture、Microsoft YaHei 测宽高，再按中/字母/数字样本宽度插入换行；原五参数 `DrawMultiLineText` 一次调用 PutTextZh。不要误用另有宽高参数的七参数分支。 |
| IMultiPoint | 微点逐个画圆；outline 从非零 opacity 且有数据的项目收集后交 `DrawOutLinePoint`；Rectangles 再逐个 DrawRect。轮廓结构解包需对照 `:1200` 后代码和绑定数据布局，不能用通用点列表替代。 |

## 原件身份与平台依赖

- 原 `Apps.Data.dll` SHA-256 `2AD51C179D4A5CF7E0C0373CD1526DE8A75D53A3EA6CBE3EF73C0C5268E1C55C`，MVID `cb38f3bc-780b-400d-89f5-0ba51ce23489`；保存的 helper 反编译文本 SHA-256 `954980DB0062E35506753B5FD918AAB2472F2606E71A28AE5F9A3132B50257EE`。
- 原 `Applications/3rdLib/OpenCv/OpenCvSharp.dll` Assembly/FileVersion `1.0.0.0`、MVID `3cd2bd95-9250-4f68-90aa-cab227a0fa5a`、SHA-256 `16AE93D84A694B1099C33AA97F248FCFBDC05FE4D552C5FD67F6F9579AD15F9B`；引用 token `6adad1e807fea099`。程序集版本不是 OpenCV 核心版本，不能据此断定上游 OpenCV 版本。
- x64 `OpenCvSharpExtern.dll` 候选 SHA-256 `A5F159E4A8266104B03B973A3431C3725FBFACF8046251AA3568B94F166CE62D`；x86 候选 `7E8AF1F007422C634671850294CEB54D94F44EA4C9ACF92906D26E6C56E40903`。静态候选不证明实际装载、transitive DLL 闭包或 native core 版本。
- `OpenCvSharp.Cv2.PutTextZh:8850-8863` 验证输入和 disposed，调用 `NativeMethods.core_putTextZh` 后 Fix、KeepAlive。其声明 `NativeMethods.cs:6998` 是 `OpenCvSharpExtern`、Cdecl、ExactSpelling、`LPStr` 文本与字体、Point、int、Scalar、int italic/underline；它是 P/Invoke 声明而非函数体，不证明 UTF-8 或具体字形光栅化。已保存原托管反编译和 CIL；当前 REA 会话 native Ghidra provider 报 `GHIDRA_INSTALL_DIR` 未设置及 Java 不可用，因此本轮未取得 native 函数体；这不是全部静态工具永不可用的证明。
- `HikExternalCall.Common.ExternalCallHelper.IntPtr2Bytes` 的本地反编译 `analysis/decompiled/VM.ExternalCall.Audit/HikExternalCall.Common/ExternalCallHelper.cs:54` 是判空、分配数组、`Marshal.Copy`；目标可以实现等效的受控字节拷贝。helper `GetImageBufferByFileName:1122-1185` 的 `VM.Utility.MemoryHelper.OpenFileMapping/MapViewOfFile/UnmapViewOfFile/CloseHandle` 是 Windows 文件映射适配；含16字节头、长度和偏移检查，适配层隔离而非假设可跨平台直用。`LimitedConcurrencyLevelTaskScheduler(1)` 用于异步入口；这里的同步入口不需把调度器当绘图依赖。`ExternalCallHelper` 也出现在 `MatToBytes`，不能推断所有 native 内存都可安全复制。

目标项目现有 OpenCvSharp4 `4.13.0.20260222` 可承载标准 Mat、几何、编码调用，但不因类型同名就与原定制 `1.0.0.0` 包装或 native ABI、图元取整/字体/编码默认行为同态；尤其 `PutTextZh` 不可假定在新版存在。建议在图像数据→结果图形→文字栅格→编码四个边界定义适配器，优先沿上述源条件实施；文字暂用明确标记的自研字体渲染假设，影响中文布局/换行/像素，替换点为文字栅格适配器，待取得匹配 `core_putTextZh` 机器码/实际加载证据或隔离差分再替换。原生完整实现和依赖集仍未闭合，不将该占位称原版效果。
