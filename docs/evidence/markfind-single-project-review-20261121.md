# MarkFind 单项目与算法链复核

日期 2026-11-21

## 原厂证据边界

- 原厂图形定位页签、模型配置面板、参数字段和模型状态来自 `docs/evidence/markfind-review-20261118.json` 登记的反编译 XAML、托管入口和 AlgorithmTab XML。
- 原厂 `IMVSMarkFindModu` 的源码级 Process/ProcessAlg/ProcessOutput 和算法 DLL 数值语义当前没有同版本可执行黄金样本，不能把 HALCON 形状模板结果视为原厂通过。

## 本次代码迁移

图形定位已从 Runtime/Wpf 双项目合并到 `tools/SunEyeVision.MarkFind/SunEyeVision.Tool.MarkFind.csproj`。同一程序集继续声明 Runtime 与 Editor 组件，解决方案、产品注册、测试引用和发布清单均已改为单项目。

- `MarkFindTool.cs` 保留入口、字段读写、端口和结果声明。
- `Algorithms/MarkFindSession.cs` 保留图像、ROI、Fixture、HALCON 形状模板搜索、结果判定和资源释放。
- `Parameters/` 保留设置、字段声明、模型编解码和模型创建接缝。
- `Results/` 保留结果行、几何和结果显示声明。
- `Editor/` 保留图形定位模型面板、参数页、结果页和设计数据。

主 Release 载荷移除旧的 `SunEyeVision.Tool.MarkFind.Wpf.dll`，不保留双项目回退。

## 当前算法链复核

`MarkFindSession` 的实际链路如下。

1. 冻结设置并校验模型、图像、ROI 和输入约束。
2. 通过 `HalAdapter` 复制图像和区域，按 Fixture 变换 ROI，与图像有效域求交。
3. 使用 HALCON `FindShapeModel` 搜索模型，读取匹配中心、角度和分数。
4. 变换模板轮廓，计算最小外接矩形和可选轮廓点，按分数排序或保留原顺序。
5. 执行角度、分数、匹配点和外接框限位判定，生成结果表、图像区域和几何叠加。
6. 在成功、失败、取消和异常路径释放候选区域、输出资源和模型句柄。

这些是本方代码事实，下面行为仍未由原厂证据闭合。

- 原厂模型文件格式、训练参数和 HALCON 模型的等价性。
- 原厂金字塔、阈值、定位方式、旋转、投影和 Mark 类型的数值语义。
- 原厂掩膜、ROI 限制、结果排序、状态和异常输出。
- 原厂训练失败、编辑模型、取消、进度和资源释放时序。

没有原厂黄金样本前，不修改这些算法语义，也不标记为算法同构通过。

## 验证结果

- 图形定位专项测试 21/21 通过。
- 迁移过程中补齐模型序列化文件所需的 `System.IO` 引用。
- ControlNamingProbe 已复核完整产品目录和图形定位页签，图形定位页为“基本参数/特征模型/运行参数/结果显示”，特征模型面板已物化。
- 专项测试 21/21 通过，Release 最终封存哈希为 `5CDC77BBD3004318EA47A8EDF00AE833BDD06F008C413D0BC42C61AC9594C5BE`。
- 该哈希仅证明本次载荷一致性，不代表 HALCON 与原厂算法的数值等价。

## Release Agent Desktop 运行探针

- 六工具精确节点名批量探针已通过，图形定位窗口成功打开并关闭。
- 结果记录在 `docs/evidence/six-tool-agentdesktop-runtime-20261121.json`，对应运行目录为 `Release/_validation/AgentDesktopToolReview/batch-all/20261120-001424-图形定位1`。
- 该探针确认窗口准入和关闭链路，不替代模型训练、匹配和结果排序语义的原厂等价证据。

