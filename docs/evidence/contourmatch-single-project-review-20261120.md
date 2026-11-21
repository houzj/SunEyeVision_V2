# ContourMatch 单项目与算法链复核

日期 2026-11-20

## 原厂证据边界

- 工具身份、页签、参数和特征模板窗口来自 `docs/evidence/contourmatch-review-20261118.json` 登记的原厂 XML、反编译 XAML 和托管入口。
- 参数提供者、Process、ProcessAlg、ProcessOutput、训练入口和原生运行时链来自 `docs/plugins/IMVSCONTOURMATCHMODU-*.md` 系列报告。
- 这些证据确认了模板参数、运行参数、结果声明和模板窗口生命周期的接缝。原厂算法 DLL 的数值实现、模型文件编码、搜索策略和失败清理仍没有同版本可执行闭环证据。

## 本次代码迁移

轮廓匹配已从 Runtime/Wpf 双项目合并到 `tools/SunEyeVision.ContourMatch/SunEyeVision.Tool.ContourMatch.csproj`。单项目保留 Runtime 与 Editor 两个产品组件，运行时和编辑器共用同一程序集的源码基线。

- `ContourMatchTool.cs` 保留工具入口、参数读写、结果声明和会话创建。
- `Algorithms/ContourMatchSession.cs` 保留图像、ROI、位置修正、HALCON 搜索、排序、判定、结果资源和释放链。
- `Parameters/` 保留设置、字段声明、模板编码和模板创建算法接缝。
- `Results/` 保留结果行、几何和结果显示声明。
- `Editor/` 保留原厂特征模板窗口接缝、参数页、结果页和设计数据。

产品注册、解决方案、测试项目和发布清单已全部指向单项目路径。主 Release 载荷中已删除旧的 `SunEyeVision.Tool.ContourMatch.Wpf.dll`，不保留双项目回退。

## 当前算法链复核

`ContourMatchSession` 的实际链路是固定的。

1. 冻结并校验设置，解码模板并检查角度覆盖。
2. 读取图像、ROI 和可选 Fixture，使用 `HalAdapter` 复制资源并转换到 HALCON。
3. 将 ROI 变换到 Fixture 后与图像有效域求交，空区域直接返回稳定错误码。
4. 调用 HALCON `FindShapeModel`，读取匹配中心、角度和分数。
5. 变换模型轮廓，计算最小外接矩形和轮廓点，按声明的排序方式编号。
6. 执行角度、分数、匹配点和外接框限位判定，生成结果表、几何叠加和输出资源。
7. 释放匹配结果区域、输出资源和临时模型句柄，异常路径也执行清理。

上述链路是本方实现事实。由于现有原厂证据尚未把原生算法的参数映射和数值语义闭合，不能把 HALCON `FindShapeModel` 的数值结果标记为原厂等价。以下项目保持未确认。

- 原厂模型字节格式与本方 `.cmatchmodel` 编码的等价性。
- 金字塔、边缘阈值、链长、掩膜和极性参数对原厂算法的精确映射。
- 原厂重叠率、超时、延拓和多模板搜索的数值边界。
- 原厂结果矩形、轮廓点、排序稳定性和失败状态的逐字段等价性。
- 原厂训练失败、取消、进度遮罩和释放时序。

没有黄金样本和原厂同版本运行输出前，不修改这些算法语义，也不宣称算法闭口。

## 验证结果

- 轮廓匹配专项测试 28/28 通过。
- `Check-ToolManifest.ps1 -ToolId ContourMatch` 通过，失败项为空。
- Release 单项目构建通过，最后一次探针构建后已重新封存，当前载荷哈希为 `5CDC77BBD3004318EA47A8EDF00AE833BDD06F008C413D0BC42C61AC9594C5BE`。
- 当前测试覆盖参数契约、执行输入校验、模板编解码、结果显示契约和编辑器模板面板接缝。
- `ControlNamingProbe` 通过，确认完整产品目录准入 12 个工具，轮廓匹配的四个页签和模板面板接缝通过。
- `RuntimeEditorProductionProbe` 能完成 12 工具目录准入，但继续执行 ImageSource→Blob 的长链路时无输出并超出本轮等待，已主动终止，不能把该长链路记作轮廓匹配通过。
- 原厂特征模板窗口的资源、帮助和进度遮罩仍按既有证据登记为未确认，不能作为已同构结论。

## Release Agent Desktop 运行探针

- 六工具精确节点名批量探针已通过，轮廓匹配窗口成功打开并关闭。
- 结果记录在 `docs/evidence/six-tool-agentdesktop-runtime-20261121.json`，对应运行目录为 `Release/_validation/AgentDesktopToolReview/batch-all/20261120-001315-轮廓匹配1`。
- 该探针确认窗口准入和关闭链路，不替代模板训练与匹配数值语义的原厂等价证据。

