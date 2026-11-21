# 流水线口径（政策层）

本文件是 `工具生产流水线与自迭代机制-20261120.md` 与 `完整同构与批量落地方案-20261120.md` 的执行摘要。口径冲突时以 `AGENTS.md` 和 `CURRENT_TASK.md` 顶部当前有效批次为准。

## 状态机

```text
EvidenceCollect → EvidenceNormalize → ToolModelReview
→ SingleProjectGenerate → SharedChainBind → StaticGate
→ ReleaseBuild → AgentDesktopProbe → EvidenceDiff
→ PatchQueue → Regression → Accepted / Unconfirmed
```

每轮必须保留：输入证据哈希、生成参数、代码变更、构建日志、运行快照、未确认项。未确认项只进登记表。

## 三层职责

| 层 | 输入 | 输出 |
|---|---|---|
| 证据层 | 原厂 DLL、BAML/资源、Algorithm/AlgorithmTab/Display XML、ToolItemInfo、运行快照 | 工具身份、页签、分组、控件、字段、端口、资源、生命周期证据 |
| 生成层 | 工具清单 + 证据 | 单项目目录、参数/结果声明、资源清单、编辑器工厂、测试入口；专属 UI 必须绑定 Evidence ID |
| 验证层 | 产物 + Release | 静态映射、共享参数链、Runtime 测试、WPF 组装探针、Release 构建、Agent Desktop 窗口验证、资源释放 |

失败按六阶段归类：入口、声明、模板、组装、执行、释放（见 `failure-taxonomy.md`）。

## 算法等价层

HALCON / OpenCvSharp 调用成功只证明能跑，不证明与原厂逻辑一致。

```text
OriginalEvidence → GoldenCase → OriginalRunCapture
→ AdapterRunCapture → Normalize → CompareWithTolerance
→ AlgorithmEquivalent / AlgorithmUnconfirmed / AlgorithmDiverged
```

黄金样本必须记录：原厂 DLL 哈希、参数快照、输入图像哈希、ROI 坐标系、输出字段映射、允许误差、采集方式。

没有黄金样本：状态固定 `AlgorithmUnconfirmed`，工具不得进入「完整同构」。当前 12 个工具全部属于此状态。

## 单工具清单（最小字段）

```json
{
  "toolId": "PixelCount",
  "batch": 1,
  "project": "tools/SunEyeVision.PixelCount/SunEyeVision.Tool.PixelCount.csproj",
  "pages": ["基本参数", "运行参数", "结果显示"],
  "sharedChain": ["Tab.Collection", "CollectionViewSource", "ParamListView", "ParamUserControl"],
  "specialControls": [
    { "type": "PixelModuRangeConfigList", "editorType": "...PixelCountRangePanel", "evidence": "pixelcount-ui-static-20261120" }
  ],
  "algorithm": {
    "adapter": "SunEyeVision.HalAdapter.Runtime",
    "originalModuleSha256": "...",
    "goldenCases": [],
    "equivalenceStatus": "unconfirmed",
    "comparisonRequired": ["module-status", "count", "roi", "mask", "error-behavior"]
  },
  "unconfirmed": ["..."]
}
```

完整示例见 `templates/tool-manifest-entry.json`。

## 反馈闭环（固定顺序）

1. 静态差异 → 修工具声明和资源映射。
2. 组装差异 → 修工具 Editor 接缝，**禁止先改公共框架**。
3. 运行差异 → 回到原厂取证；缺证据保持未确认。
4. 每次修复只影响当前工具和已固定合同覆盖的范围。
5. 同一失败连续出现 → 升级为差异条目，阻止进入下一批。
6. 工具通过后冻结证据与 Release 摘要，作为后续工具模板。

## 批次与顺序

| 批次 | 工具 | 复用模板 |
|---|---|---|
| 基线 | ImageSource、BlobAnalysis | 完整链基准 |
| 一 | PixelCount、FrameMean、IntensityMeasure | 数值、枚举、范围、结果表 |
| 二 | ColorExtract、DivideImage | 图像来源、ROI、范围、图像结果 |
| 三 | ContourMatch、GrayMatch、MarkFind | 模板资源、匹配参数、图像叠加 |
| 四 | BlobLabels、If | 结果判定、条件输出、多状态显示 |

当前只有 PixelCount 完成单项目样板迁移；其余批次仍按双项目在册，迁移时逐工具复制 PixelCount 规则，不要整批替换。

## 闭口标准

原厂静态证据完整；工程结构迁移完成；页签与分组一致；字段顺序和显隐一致；端口和结果一致；UI 截图对照通过；算法黄金样本对照通过；Release 构建通过；执行与取消通过；关闭与资源释放通过；未确认项已登记。

前六项 + 构建/执行/释放可自动判定；「UI 截图对照」「算法对照」需要人工或基准证据，不得由烟测代替。
