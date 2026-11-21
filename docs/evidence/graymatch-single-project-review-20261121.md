# GrayMatch 单项目与算法链复核

日期 2026-11-21

## 原厂证据边界

- 原厂页签、灰度模板训练窗口和运行/结果字段来自 `docs/evidence/graymatch-review-20261118.json` 登记的 XML、反编译 XAML 和托管入口。
- 原厂工具身份是 `IMVSGrayMatchModuVA`。旧版 `IMVSGrayMatchModu` 与 VA 参数清单不能混用，当前代码按 VA 工具身份保留差异。
- 原厂训练、Process、ProcessAlg、ProcessOutput 和原生算法的同版本数值证据尚未形成可执行黄金样本，因此本轮只确认接入边界和资源生命周期，不宣称数值等价。

## 本次代码迁移

灰度匹配已从 Runtime/Wpf 双项目合并到 `tools/SunEyeVision.GrayMatch/SunEyeVision.Tool.GrayMatch.csproj`。Runtime 和 Editor 组件继续由同一程序集声明，解决方案、产品注册、测试项目和发布清单都已经指向单项目。

- `GrayMatchTool.cs` 保留工具入口、字段读写、结果声明和会话创建。
- `Algorithms/GrayMatchSession.cs` 保留图像、ROI、Fixture、HALCON NCC 搜索、排序、判定和资源释放。
- `Parameters/` 保留 VA 设置、字段声明、灰度模板编解码和模板创建接缝。
- `Results/` 保留结果行、几何和结果显示声明。
- `Editor/` 保留灰度模板面板、参数页、结果页和设计数据。

主 Release 载荷移除旧的 `SunEyeVision.Tool.GrayMatch.Wpf.dll`，不保留双项目回退。

## 当前算法链复核

`GrayMatchSession` 的实际链路如下。

1. 冻结设置，校验模型、角度范围、金字塔结束层和其他声明约束。
2. 读取图像、ROI 和可选 Fixture，经 `HalAdapter` 复制到 HALCON，按 Fixture 变换 ROI 并与图像有效域求交。
3. 使用 `ReduceDomain` 限制搜索区域，加载灰度模板。
4. 调用 HALCON `FindNccModel`，读取中心、角度和分数，按照声明的排序方式稳定编号。
5. 根据模板外接框和匹配姿态生成结果矩形，捕获结果区域资源。
6. 执行角度、分数、匹配点和外接框限位判定，生成结果表和叠加几何。
7. 在成功、失败和取消路径释放结果区域、输出资源和模型句柄。

这些是本方代码事实。原厂同版本算法内部仍没有确认以下行为。

- 灰度模板文件和原厂训练模型的字节等价性。
- VA 金字塔层、角度步长、分数、掩膜和固定点的精确算法语义。
- 原厂 ROI 单区域限制、位置修正、掩膜擦除和多模型行为。
- 原厂结果矩形、排序、状态和异常输出的逐字段等价性。
- 原厂训练失败、进度、取消和释放时序。

没有原厂黄金样本前，不把 HALCON NCC 结果标记为原厂算法通过，也不自行补齐未确认行为。

## 验证结果

- 灰度匹配专项测试 30/30 通过。
- Release 单项目构建通过，构建过程无错误。
- `Check-ToolManifest.ps1 -ToolId GrayMatch` 通过，算法等价保持 `unconfirmed`。
- 迁移过程中补齐了模板序列化文件所需的 `System.IO` 引用。
- ControlNamingProbe 通过，完整产品目录和灰度匹配四个页签/模板面板接缝均通过。
- 最后一次探针构建后已重新封存 Release，当前载荷哈希为 `5CDC77BBD3004318EA47A8EDF00AE833BDD06F008C413D0BC42C61AC9594C5BE`。

## Release Agent Desktop 运行探针

- 六工具精确节点名批量探针已通过，灰度匹配窗口成功打开并关闭。
- 结果记录在 `docs/evidence/six-tool-agentdesktop-runtime-20261121.json`，对应运行目录为 `Release/_validation/AgentDesktopToolReview/batch-all/20261120-001349-灰度匹配1`。
- 该探针确认窗口准入和关闭链路，不替代 NCC 模型、参数和结果语义的原厂等价证据。

