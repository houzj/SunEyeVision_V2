# Blob 图像区域与屏蔽继承输入静态核查（2026-11-01）

基线 `a2ff34e4`；本记录只核已有 VM 4.4 反编译及本方源码，不运行原 VM/SDK，也不代表完整原厂消费链或产品实现。

## 已确认

- 原 `VM.PlatformSDKCS.Audit/VM.PlatformSDKCS/RoiManager.cs:286–303` 的 `SetInheritBlind(List<string>)`：null 抛异常，先设置 `ROIAreaType=0`，再以 `INPUT_TYPE_STRING` 和 `InputBlindPolygonString` 创建 InputItem 并传入整个字符串列表。反编译文件 SHA256 `7AFDB8642DE46D86525D75BBA5C78D80D038B893E66A31F186CAB0E5596EA243`。该签名可证 SDK 接受列表，不单独证明每项编码、最终IO索引或原生解析。
- 同文件 `SetMaskImage(ImageBaseData)`：null 抛异常，设置 `ROIAreaType=1`，以 `INPUT_TYPE_IMAGE` 输入 `InMask/InMaskWidth/InMaskHeight/InMaskPixelFormat`。现有原 `Group.xml` 的 `MaskImage` 候选及 `InputMask` 组合操作与此输入方向一致；不据此推断像素判定。
- 主仓 Blob `BlobTool.Describe`/`BlobInput.Read` 已有主图、检测 Region、屏蔽 Region、位姿、本地ROI及检测继承字段，没有对应掩膜图像或屏蔽字符串列表的正式输入。图像 publication、引用/索引、资源 reader、ROI 几何和 HALCON 区域运算可分别复用；不等于这两条 UI→配置→冻结→执行链已具备。
- 主仓 Script 的既有 ROI 类型、输出复制代码并不构成现行正式生产者：`AlgorithmNode.cs:109–112` 对无 RuntimeConfiguration 的旧 SDK 工具执行保护拒绝；不能为该批恢复旧执行或将测试 Record 冒充正式来源。
- 原屏蔽绘制仅多边形，用户明确要求多形状是本方扩展；新增本方多形不能称作原厂字符串格式或原版支持。

## 未确认与处理

- 屏蔽字符串：每项的坐标编码、坐标单位与顺序、闭合/分隔、空列表与坏项、索引、原生解析及资源释放均未证。不能仅凭 `List<string>` 构造解析器，也不能把通用 Shape/像素 Region 静默替换成原字符串语义。
- 掩膜图像：支持像素格式、零/非零或其他值域规则、尺寸不同、输入 Domain、检测/屏蔽合成顺序及输出掩膜语义均未证。不能直接把图像当作检测 Region、屏蔽 Region或已发布的二值图。
- 已向逆向负责人的既有对话发起上述两链集中补证调用；发送调用未取得确认，未确认送达或已有回件。现行入口继续明确保护，不新增没有消费链的正式字段或伪造生产者。后续如取得可追的解析/消费代码或隔离原机差分，再按完整链集中实施；若当前条件确已穷尽仍无法确认，按用户规则明确标识自研假设、影响与替换位置。

未进行本批产品代码修改、构建、运行或用户验收；本记录不改变完整ROI、Blob或S2/S3状态。