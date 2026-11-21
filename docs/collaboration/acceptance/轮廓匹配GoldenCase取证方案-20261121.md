# 轮廓匹配 GoldenCase 取证方案（2026-11-21）

## 1. 目的

按三阶段铁律，UI/tab 层对齐已完成，进入算法对齐。算法对齐的验收基准是 GoldenCase——原厂同图同参数的一次完整运行输出。
无 GoldenCase → `AlgorithmUnconfirmed`（技能 §7）；有基准才能把 A 类迁移项（极性/尺度/多模型）从"猜实现"变成"按基准验证"。

**约束**：CV(CvEngine.FindShape) 线不用，整个软件用 Halcon 对齐（用户 2026-11-21）。

## 2. 取证目标

为以下三类判定提供基准：
1. **A 类迁移验收**：极性 metric、尺度范围（CreateScaledShapeModel/FindScaledShapeModel）、多模型——迁移后与本方案基准比对。
2. **C 类行为取证**：噪点考虑 / 延拓阈值 / 横切角度 三个 native 特有参数——取"开/关"输出差异，确定自研目标。
3. **通用等价**：默认参数下一次完整运行，验证框/分数/角度/轮廓/点字段口径。

## 3. 取证前置条件

| 项 | 要求 |
|---|---|
| 原厂程序 | VisionMaster 4.4.0，含 IMVSContourMatchModu |
| DLL 版本锁定 | 记录 `IMVSContourMatchModu.dll` / `IMVSContourMatchModuCs.dll` 的 SHA256 |
| 输入图像 | 固定一张适合轮廓匹配的图（建议有清晰矩形/多边形目标，含旋转与尺度变化各一个），记录 SHA256 |
| 模板 | 用该图固定区域训练一个模板，导出/固定模板数据并记录哈希 |
| ROI | 固定搜索区域（记录坐标），本方与原厂用同一坐标系 |
| 工具 | agent-desktop（原厂实机）+ 本方 Release 程序 |

## 4. 原厂取证步骤（逐 Case）

### Case G0：默认参数基线
1. 打开原厂含轮廓匹配节点的方案，加载固定图像与模板。
2. 参数设为默认（AlgorithmTab.xml 的 CurValue / parameters.json 的 DefaultValue，冲突时以 parameters.json 为准）。
3. 执行一次运行，用 agent-desktop 抓结果页 `snapshot --window-id` + `screenshot`。
4. 导出结果数据（若原厂支持结果表格导出则导出 CSV；否则从 snapshot 的 statictext 逐行抄录）。
5. 记录运行耗时、状态（OK/NG）、匹配个数。

### Case G1：极性（Polarity）
- 取两态：Ignored(0) / Considered(1)，其余参数同 G0。
- 每态记录：匹配个数、各匹配分数、角度、框坐标。
- 判定目标：确认极性改变是否影响结果、影响哪几个量。

### Case G2：尺度范围（ScaleStart/End、RangeScaleType）
- 取三态：等比 1.0~1.0；等比 0.8~1.2；XY 分别设置。
- 每态记录：匹配个数、各匹配分数、**scale 输出**、框坐标。
- 判定目标：确认原厂是否输出逐匹配 scale，尺度范围如何参与匹配。

### Case G3：噪点考虑 / 延拓阈值 / 横切角度（C 类）
- 每个参数取开/关（或非 0）两态，其余同 G0。
- 记录输出差异（个数/分数/框/轮廓点）。
- 判定目标：确定自研实现要实现什么行为。

### Case G4：多模型（UseMatchAllMode / MultiModelMaxOverlap）
- 取两态：单模型 / 全部搜索模式 + 不同重叠率。
- 记录：是否输出多个模型的匹配、重叠率如何影响去重。

## 5. 必须记录的字段（每个 Case）

按技能 §7「GoldenCase 必须记录」：

| 字段 | 记录内容 |
|---|---|
| 原厂 DLL 哈希 | SHA256 of IMVSContourMatchModu.dll / Cs.dll |
| 参数快照 | 本次运行的完整参数（从参数页 snapshot 抄录，或导出配置文件） |
| 输入图像哈希 | SHA256 |
| 模板哈希 | 模板数据文件 SHA256 |
| ROI 坐标系 | 搜索区域坐标 + 图像坐标系口径 |
| 输出字段映射 | 见下表 |
| 允许误差 | 见 §7 |
| 采集方式 | snapshot_id + 截图路径 + 操作人/脚本 |

## 6. 输出字段映射（原厂 ↔ 本方）

本方结果 schema（`ContourMatchSchema.cs`）：`output.count` / `scores` / `match-points` / `angles` / `scales` / `boxes` / `outline-points` / `status`。

| 原厂结果项（Display.xml） | 本方字段 | 比较方式 |
|---|---|---|
| Feature Match Result（框 CenterX/Y/Width/Height/Angle） | `output.boxes` | 几何量，容差 ±1px / ±0.5° |
| Feature Match Result Score | `output.scores` | 浮点，容差待定（建议 ±0.02） |
| Feature Match Points（MatchPointX/Y） | `output.match-points` | ±1px |
| Feature Match Contour Points（OutLineInfo） | `output.outline-points` | 点数 + 覆盖度 |
| 状态（ModuStatus） | `output.status` | 精确 |
| 个数（MatchNum） | `output.count` | 精确 |

**坐标口径**：原厂 Display.xml 的 CenterX/CenterY 与 Halcon row/column 的轴定义不同（本方 Box.AngleDegrees 已按产品约定取反，见 ContourMatchSession.cs:318）。取证时记录原厂是"列/行"还是"行/列"，归一化比较时统一到屏幕坐标。

## 7. 容差与比较维度

按技能 §7：状态、数量、数组顺序、区域覆盖、几何量、浮点误差、错误码、空输入行为。

初始容差建议（取证后按实际调整）：
- 状态/个数：精确
- 框坐标：±1px；角度 ±0.5°
- 分数：±0.02（若原厂与 Halcon 评分函数不同，需放宽或标 AlgorithmDiverged）
- 轮廓点：数量精确、坐标 ±1px、覆盖度 ≥95%
- scale：±0.01（若原厂输出）

## 8. 验收判定

- 有 G0 基准 + 全部维度在容差内 → G0 `AlgorithmEquivalent`
- A 类每项（G1/G2/G4）有基准 + 迁移后在容差内 → 该项可迁移，撤销对应 NOT_READY
- C 类（G3）有基准 + 自研实现在容差内 → 撤销 NOT_READY；无基准 → 维持 `AlgorithmUnconfirmed`
- 任一维度超差 → `AlgorithmDiverged`，差异进 PatchQueue（skill §8）

## 9. 执行顺序

1. 先固定前置条件（图/模板/ROI/DLL 哈希）——这是所有 Case 可比的前提。
2. 跑 G0 基线，验证字段映射与坐标口径（这一步常发现坐标系错位，先解决再跑其它 Case）。
3. 跑 G1/G2/G4（A 类），作为迁移验收基准。
4. 跑 G3（C 类），作为自研目标。
5. 全部基准落盘后，再改算法代码——禁止"边改边取基准"（会导致基准被实现带偏）。

## 10. 证据落盘

- 路径：`docs/evidence/contourmatch-goldencase-20261121/`
- 每 Case 一个子目录：`G0-default/` `G1-polarity/` `G2-scale/` `G3-native/` `G4-multimodel/`
- 每子目录：`snapshot.json`（含 ref/bounds/states）、`screenshot.png`、`parameters.json`、`README.md`（结论）
- 索引：回写 `docs/collaboration/acceptance/架构层同构方案与十工具复核-20261121.md`
