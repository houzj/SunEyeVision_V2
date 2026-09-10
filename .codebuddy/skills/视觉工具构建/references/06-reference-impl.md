# 参考实现与代码锚点

## 1. 参考源选择矩阵（Step1 用）

| 需求 | 参考实现 | 可借鉴 |
|---|---|---|
| 图像处理式（区域 + 算子链 + 统计） | `tools/SunEyeVision.BlobAnalysis/` | 区域两段式、统计结果、多分组结果树 |
| 引导检测式（单引导 metrology） | `tools/SunEyeVision.Tool.LineDetect/` | 卡尺参数面板、gen_param 翻译、检测点提取 |
| 引导检测式（圆） | `tools/SunEyeVision.Tool.CircleDetect/` | 圆引导 + 圆拟合 |
| 模板匹配 | `tools/SunEyeVision.Tool.ShapeMatch/` | 模板参数 + 匹配结果 |
| **几何测量式（最新范式）** | `tools/SunEyeVision.Tool.LineLineMeasure/` | **双卡尺装配 + ParamValue + MeasureJudge + 结果分组（v4 定稿）** |
| 参数两态（常量↔绑定）样板 | `tools/SunEyeVision.Tool.ImageThreshold/`、`.../Tool.Threshold/` | `ConfigSetting` + `.Value` 绑法 |
| 区域启用开关 / 位置修正移除 | `tools/SunEyeVision.Tool.Threshold/` | `IsRegionEnabled` + CheckBox |
| 滤波类参数面板 | `tools/SunEyeVision.Tool.ImageFilter/` | 枚举/数值参数组织 |
| 形态学算子链 | `tools/SunEyeVision.Tool.Morphology/` | 结构元素参数 |

> ⚠️ **禁止**参考 `tools/SunEyeVision.Tool.ImageLoad/`（`HasDebugWindow=false`，无调试窗口，非本技能对象）。
> ⚠️ Blob 目录为 `SunEyeVision.BlobAnalysis`（**无 `Tool.` 前缀**），csproj 名为 `SunEyeVision.Tool.BlobAnalysis.csproj`。

## 2. 最新测量范式文件清单（`LineLineMeasure`，v4 定稿）

| 文件 | 职责 |
|---|---|
| `LineLineMeasureTool.cs` | `[Tool]` + `IToolPlugin<,>` + `Run`（metrology measure → 几何量 → 判定）+ `CreateDebugControl` |
| `LineLineMeasureParameters.cs` | `[JsonDerivedType]` + `RoiParameters` + `Judgments` + `E1*/E2*` ParamValue + 样式 |
| `LineLineMeasureResults.cs` | `ToolResults` + 拟合/测量结果 + `GetPropertyMap` + `FillDisplay` |
| `Views/LineLineMeasureToolDebugControl.xaml(.cs)` | 三 Tab + 双卡尺装配 + 事件回写 + 恢复链 + `Cleanup` |
| `SunEyeVision.Tool.LineLineMeasure.csproj` | 工程骨架（见 [05](05-project-skeleton.md)） |

## 3. SDK 基座代码锚点

| 基座 | 位置 | 关键成员 |
|---|---|---|
| `ParamValue<T>` | `src/Plugin.SDK/Execution/Parameters/ParamValue.cs` | `Value:36`、`BindingConfig:67`、`ValueSource:97`、`IsBinding:115`、`CreateConstant:145`、`CreateBinding:167` |
| `MeasureThreshold` | `src/Plugin.SDK/Execution/Parameters/MeasureThreshold.cs` | `MeasureThreshold:14`、`MeasureJudgeResult:68`、`MeasureJudge:95`、`EvaluateAll:98`、`IsAllPass:134` |
| `MeasureElementsEditorControl` | `src/Plugin.SDK/UI/Controls/Measure/Views/MeasureElementsEditorControl.xaml.cs` | `ActiveElementIndex:96`、`Element1:107`、`Element2:110`、`ConfigureElement:116`、`ClearElementToolSelection:138`、`SetPositionCorrectionMatrix:145`、`SetMainImageControl:157`、`RefreshOverlay:194`、`ClearMainOverlay:196` |
| `MeasureEditorControl` | `src/Plugin.SDK/UI/Controls/Measure/Views/MeasureEditorControl.xaml.cs` | `GuideGeometryChanged:134`、`CaliberParamsChanged:139`、`SelectionChanged:144`、`SetCaliberParams:216`、`SetAllowedGeometryTypes:233`、`SetGuide:292/302/312/323` |
| 交互模型 | `src/Plugin.SDK/UI/Controls/Measure/Models/MeasureInteractionModel.cs` | `EditingLine`、`LineStartPoint`、`LineEndPoint` 等 |
| 事件参数 | `.../Measure/Models/GuideGeometryChangedEventArgs.cs`、`CaliberParamsChangedEventArgs.cs` | `GeometryType`、`Model`、`IsDrawing` / `Width`、`Length`、`Count` |
| 词目表 | `src/Plugin.SDK/Models/SdkTypeVocabulary.cs` | 输出变量类型词目（未登记类型直接报错） |
| 结果树分组 | `src/UI/Shell/Windows/MainWindowViewModel.cs` | `ModuleResultGroupOrder:4524`、`GroupOrderIndex:4547`、`FormatTypedValue:4556` |
| 判定基座契约测试 | `tests/SunEyeVision.Plugin.SDK.Tests/MeasureJudgeContractTests.cs` | 13 条契约 |

## 4. 工具侧实现锚点（`LineLineMeasure`）

| 关注点 | 位置 |
|---|---|
| `Run` 主体 | `LineLineMeasureTool.cs:36` |
| 引导校验（Fail-Fast） | `LineLineMeasureTool.cs:48-56` |
| metrology 双对象 + 一次 apply | `LineLineMeasureTool.cs:68-103` |
| 鲁棒拟合两段式 | `LineLineMeasureTool.cs:249` |
| 几何量（`AngleLl`/`DistancePl`/`IntersectionLl`） | `LineLineMeasureTool.cs:129-181` |
| 卡尺口径换算（半长 → 全长） | `LineLineMeasureTool.cs:193-199` |
| 判定调用 | `LineLineMeasureTool.cs:202-213` |
| gen_param 翻译 | `LineLineMeasureTool.cs:356-376` |
| `CreateDebugControl` | `LineLineMeasureTool.cs:380` |
| 判定列表定义 | `LineLineMeasureParameters.cs:35` |
| E1 参数组 | `LineLineMeasureParameters.cs:48-61` |
| 样式定义 | `LineLineMeasureParameters.cs:84-193` |
| `GetPropertyMap` | `LineLineMeasureResults.cs:375` |
| `FillDisplay` | `LineLineMeasureResults.cs:148` |
| `Cleanup` | `LineLineMeasureToolDebugControl.xaml.cs:65` |
| 编辑器事件回写 | `LineLineMeasureToolDebugControl.xaml.cs:84-128` |
| 恢复链（引导 + 卡尺 + 刷新） | `LineLineMeasureToolDebugControl.xaml.cs:130-161` |
| 枚举索引翻译 | `LineLineMeasureToolDebugControl.xaml.cs:167-204` |
| 生命周期覆写 | `LineLineMeasureToolDebugControl.xaml.cs:219-281` |

## 5. 相关技能与规则

| 名称 | 用途 |
|---|---|
| `[skill:构建验证]` | MSBuild 验证，区分配置错误与业务错误 |
| `[skill:方案测试]` | 全链路验收 + 测试报告 |
| `[skill:halcon-docs]` | 核对 Halcon 算子参数语义（极性/返回值） |
| rule-004 | 复用优先（禁止重复造轮子） |
| rule-014 | Halcon 算子一律 `HOperatorSet` 静态方法 |
| rule-015 | 禁止兜底回退（无值 → 报错/NG，不静默） |
| rule-016 | MSBuild 项目配置规范（`IsToolPlugin`、`Private=false`） |
| rule-017 | `.gitignore` 屏蔽目录（`dev/`）必须用 PowerShell 读取 |
