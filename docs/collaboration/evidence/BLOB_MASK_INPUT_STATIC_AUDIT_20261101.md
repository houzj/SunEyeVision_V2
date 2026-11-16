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

## 后续静态取证补充

- 本机 VisionMaster 4.4.0 Build20250516 安装件 `Applications/Module(sp)/x64/Location/IMVSBlobFindModu/IMVSBlobFindModu.dll`，SHA-256 `4DE695D8C3051045C9010739686F94C32D66A36EE486207FE749FB183FFF4C9A`，与 REA C03 保留身份一致。使用 PE 段表定位字符串及 `dumpbin /disasm:bytes`、`/imports` 复核，以下均为 RVA。
- 输入端在 `0x1BC59`、`0x1BD36` 引用 `InputBlindPolygonString`，IAT `0x2D5C8` 为 `VM_M_GetString`。先探测返回和数量，再循环以序号取项。每次提供 0x400 字节缓冲，`0x1BCA0`–`0x1BCAA` 将数量上限截到 64。
- `0x1BD77` 将 `0x2F0E4` 的单字节逗号传给拆分调用 `0x1AC40`。`0x1BDA8`–`0x1BDB7` 拒绝奇数个标记及超过 32 对；失败支路设置 `0xE000030B`。`0x1BE57`、`0x1BE7E` 通过 IAT `0x2D270` 调用 `atof` 后收窄为 float，交替存入一组点，再由 `0x1BEBB` 追加到区域输入集合。
- 输出端分别写入 X/Y 点列，并在 `0x14661`–`0x1475F` 拼接相同顺序的坐标。原件在每对内部及相邻点之间使用逗号，末尾不加分隔符。`0x19D70` 把 float 扩为 double 后交给流格式化，`0x19E51` 置位 `0x2000`（MSVC `ios_base::fixed`），该路径使用流的默认小数精度。最终在 `0x14790` 通过 IAT `0x2D5B0` 调 `VM_M_SetString` 发布 `BlindPolygonString`。
- 本方使用不变区域格式和六位小数复现正常载荷，输入以有限 float32 为界。对非法坐标、少于三个有效点、超量或超长输入明确报错，不沿用 `atof` 的无效值折零或 64 项截断行为。原件语言环境、极值舍入、错误码对外呈现、位置修正下的原厂坐标与实际跨 SDK 载荷仍待运行对照。
