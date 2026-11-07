# 彩图生成 工具逆向主文档

- 稳定工具 ID：imvscolorimagegenerationmodu
- 原厂工具：IMVSColorImageGenerationModu
- 当前状态：inventory-only；本轮补齐资源/UI/字段证据

## 已确认
- 输入字段：InputImage, InImage, InImageWidth, InImageHeight, InImagePixelFormat, InputImage1, InImage1, InImageWidth1, InImageHeight1, InImagePixelFormat1, InputImage2, InImage2, InImageWidth2, InImageHeight2, InImagePixelFormat2
- 输出字段：ModuRunTime, ModuStatus, OutputImage, OutImage, OutImageWidth, OutImageHeight, OutImagePixelFormat
- 参数：
- 控件：ImageSource, ImageSource1, ImageSource2, ImageFormat
 - 资源与 SHA-256：resources.json

## 结果与生命周期边界
- Process/ProcessAlg、结果写回、判定、显示发布、保存恢复、reader/释放均未取得工具专属函数体，保持 unknown。
- UI 预览为 XML 字段结构示意，不是原厂桌面截图或运行时等价证明。

## 附件
evidence.json、parameters.json、resources.json、algorithm-semantics.md、ui-preview.json、ui-preview.svg。
