 
## 2026-11-20 原厂工具工程结构与本方目标结构纠偏

本轮直接核对 VisionMaster 4.4.0 安装目录中的 Blob、ContourMatch、FrameMean、PixelCount VA、ColorExtract、DivideImage 等发布目录。原厂共同发布单元为算法 DLL、Cs 桥接 DLL/XML、Algorithm.xml、AlgorithmTab.xml、Display.xml、ToolItemInfo.xml，部分工具额外带 Control.dll、Librarys.Config 和状态图片。该目录体现发布职责边界，不要求本方复制原厂程序集数量，也不允许创建伪造的 Algorithm.dll 或 Bridge.dll。

本方目标统一为每工具一个产品项目。项目内部按 Algorithms、Parameters、Results、Editor、Resources 划分职责。公共参数页、页签、字段模板、ROI、AssistGroup、结果树、图像叠加、底栏和窗口生命周期继续由已经验证的 Plugin.Wpf 链路负责。工具层只提供工具身份、参数、端口、结果声明、资源身份、算法会话和有证据的专属 Editor 接缝。HALCON/OpenCvSharp 通过独立适配层接入，算法调用可以组合多个库操作，但工具不管理第三方原生库生命周期。

完整映射和迁移门禁见 docs/evidence/原厂工具工程结构分析与本方目标结构-20261120.md。PixelCount 的 goldenCases 仍为空，算法等价为 unconfirmed。共享参数页烟测、Release 构建和窗口启动不能替代原厂固定样本的算法对照。
