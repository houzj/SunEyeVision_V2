# 源文档：工具生产流水线与自迭代机制（2026-11-20）

保留用户提供的原始口径，作为本技能的上位描述。执行细节以 `pipeline-policy.md`、`gates.md`、`single-project-layout.md` 为准；冲突时以 `AGENTS.md` 与 `CURRENT_TASK.md` 顶部当前有效批次为准。

## 目标

将「取证、建模、生成、构建、运行复核、差异修复和回归」固化为一条可重复流水线。每个工具只允许从原厂证据和统一平台合同生成，失败结果进入差异队列，下一轮只处理已经定位的差异。

## 单工具生产状态机

```text
EvidenceCollect
  → EvidenceNormalize
  → ToolModelReview
  → SingleProjectGenerate
  → SharedChainBind
  → StaticGate
  → ReleaseBuild
  → AgentDesktopProbe
  → EvidenceDiff
  → PatchQueue
  → Regression
  → Accepted / Unconfirmed
```

每次迭代都必须保留输入证据哈希、生成参数、代码变更、构建日志、运行快照和未确认项。未确认项只进入登记表，不生成猜测性代码。

## 三层职责

### 证据层

读取原厂 DLL、BAML/资源、Algorithm XML、AlgorithmTab XML、Display XML、ToolItemInfo 和原厂运行快照。输出工具身份、页签、分组、控件、字段、端口、资源和生命周期证据。

### 生成层

根据工具清单生成单项目目录、参数/结果声明、资源清单、编辑器工厂和测试入口。生成器只填充证据明确的内容，专属 UI 必须绑定 Evidence ID。

### 验证层

统一执行静态映射、共享参数链、Runtime 测试、WPF 组装探针、Release 构建、Agent Desktop 工具窗口验证和资源释放检查。失败时按入口、声明、模板、组装、执行和释放六个阶段归类。

### 算法等价层

HALCON 或 OpenCvSharp 的调用成功只证明本方算法能运行，不证明与原厂算法逻辑一致。每个工具必须有同图像、同 ROI、同参数、同版本原厂输出基准，经过结果归一化后比较状态、数量、数组顺序、区域覆盖、几何量、浮点误差、错误码和空输入行为。缺少原厂基准时，算法等价状态保持 `unconfirmed`，工具不能进入完整同构状态。

```text
OriginalEvidence → GoldenCase → OriginalRunCapture
                 → AdapterRunCapture → Normalize
                 → CompareWithTolerance
                 → AlgorithmEquivalent / AlgorithmUnconfirmed / AlgorithmDiverged
```

算法基准必须记录原厂 DLL 哈希、参数快照、输入图像哈希、ROI 坐标系、输出字段映射、允许误差和采集方式。

## 单工具输入清单

```json
{
  "toolId": "PixelCount",
  "original": {
    "moduleXml": "...",
    "algorithmTabXml": "...",
    "displayXml": "...",
    "controlAssembly": "...",
    "toolItemInfo": "..."
  },
  "project": "tools/SunEyeVision.PixelCount/SunEyeVision.Tool.PixelCount.csproj",
  "sharedChain": "Tab.Collection/CollectionViewSource/ParamListView/ParamUserControl",
  "specialControls": [
    {
      "type": "PixelModuRangeConfigList",
      "consumer": "PixelCountRangePanel",
      "evidence": "pixelcount-ui-static-20261120"
    }
  ],
  "unconfirmed": []
}
```

## 反馈闭环

1. 静态差异优先修工具声明和资源映射。
2. 组装差异优先修工具 Editor 接缝，禁止先改公共框架。
3. 运行差异先回到原厂取证，缺证据就保留未确认状态。
4. 每次修复只允许影响当前工具和共享合同明确覆盖的范围。
5. 同一失败连续出现时，自动提升为差异条目，阻止工具进入下一批。
6. 工具通过后冻结其证据和 Release 摘要，作为后续工具的模板来源。

## PixelCount 样板出口

- 单项目工程入口和双角色注册。
- 原厂三页、ImageSource、ROI、掩膜、AssistGroup 和结果显示映射。
- `PixelModuRangeConfigList` 专用动态面板接缝。
- 普通字段进入 Blob 已验证的公共参数集合链。
- Runtime 声明、结果投影、保存回读、关闭释放和 Release 构建。
- 原厂静态 UI 证据与自研 UI 快照差异清单。

算法输出等价基准属于独立出口。PixelCount 当前只有 HALCON 执行测试，还没有同版本原厂输出基准，因此只能标记为 `AlgorithmUnconfirmed`。

只有 PixelCount 样板全部通过，才复制其项目生成规则和验证规则到 FrameMean、IntensityMeasure，再进入后续批次。
