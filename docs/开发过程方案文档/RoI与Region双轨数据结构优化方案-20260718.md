# RoI 与 Region 双轨数据结构 + T[] 统一传递 优化方案（整合最终版）

> 文档状态：整合定稿版（2026-07-18 讨论定稿）
> 已确认决策：
> 1. 复用 `ShapeParameters` 作为 RoI 的 SDK 类型（不新造 `Roi` 类，遵循 YAGNI）
> 2. 几何多态修复走"方案 A"：新增专用 `ShapeParametersJsonConverter`，不动几何类（`Circle2d` 等）本身
> 3. 词汇表遵循 tuple 语义：仅注册数组形态，标量 = 长度 1 的数组，**永不为标量单列词目**
> 4. 数组在树中**单行显示、值用 `;` 串联**，**不展开成 `[i]` 子节点**（用户反馈"展开炸树"）
> 5. 单元素绑定走手动路径 `源.属性[i]`，不靠树行
> 6. 全局变量通道并轨为一等 `AvailableDataSource`，与节点输出共用同一棵绑定树/过滤/`;` 显示
> 7. 传递层统一 `T[]` 契约，工具 Run 内部标量/数组按算法实际需要
> 8. 本方案保存至本地 `docs/开发过程方案文档/`

## 🔄 改前 → 改后 → 依据（说人话）

### 改前（用户视角）
1. **手画的圆/矩形在方案保存再打开后，区域莫名其妙失效**——圆的"中心、半径"这些元数据在序列化时被丢成了看不懂的 `JsonElement`，重开就认不出是个圆。
2. **下游工具想接上游 Blob 算出来的区域，连线列表里找不到上游输出**——Blob 一次算出好几个区域（数组），但下游区域输入被认成"单个区域"，类型对不上，线连不上。
3. **"区域"被一刀切禁止当全局变量**，但你手画的 RoI（圆/矩形参数）明明是值得存下来的"图形定义"，系统没把它和"算出来的区域"分开。
4. **数组在结果树/绑定树里展开成 `[0][1][2]…` 子节点**，Blob 算出几百个连通域时，树直接炸成几百行。
5. **全局变量和节点输出走两套绑定逻辑**——全局变量在树里是扁平 action 节点、无类型、无分组，和节点输出长得不一样、行为也不一致。

### 改后（用户视角）
- 手画的圆/矩形，**保存方案、关闭、再打开，图形精确还原**（存的是参数：中心X、中心Y、半径，而不是像素）。
- 在变量表能新建一个叫 **"RoI"** 的全局变量，存手画图形的参数；方案里随用随取。
- 拖一根线，把 **Blob 算出的区域数组** 直接连到下游区域输入，**线能连上、运行能用**。
- 下游区域输入**同时认两种来源**：手画的 RoI，或上游算出来的 Region——但系统内部只认一种"统一区域"类型，不让你操心底层差异。
- **数组在树里只占一行**，值用 `;` 串起来显示（`区域(120x80); 区域(60x40); …`），数据再多树也不炸。
- **全局变量和节点输出在同一棵绑定树里**，带类型、带分组、值同样用 `;` 串联，两者无法区分。
- 需要"数组里某一个"（如第 57 个 Blob）时，在绑定路径框手填 `Blob分析3.连通域区域[57]` 即可，树不用长行。

### 依据（业界成熟做法）
- **Halcon（MVTec）**：`gen_circle`/`gen_rectangle2` 生成 `HRegion`（参数化 ROI）；`connection`/`threshold` 输出也是 `HRegion`（特征区域）；`reduce_domain(Image, Region)` 只认 `HRegion`，来源无关；tuple 语义（一个数=长度1的tuple，标量不与数组并列）。
  - 引用：`gen_circle` https://www.mvtec.com/doc/halcon/13/en/gen_circle.html
- **海康 VisionMaster**：工具独立"ROI 区域设置"（可手绘/继承上游），Blob 输出"区域"结果；ROI 是参数、Region 是产物。
  - 引用：https://www.cnblogs.com/stevenchen2016/p/18259330
- **Cognex VisionPro**：`CogRectangleAffine`（用户 ROI）vs `CogBlobTool.Results.GetBlobs()` 返回 `CogRegion`（产物），下游统一消费 `CogRegion`。
- **用户反馈**：数组展开成子节点、数据多时树炸了 → 改为单行 `;` 串联（rule-008 KISS）。

---

## 〇、词汇表与绑定设计原则（本次修订确立）

> 本节是讨论中确立的底层契约，所有词汇表改动（步骤1）、绑定改动（步骤4）、树形改动（步骤6）都必须遵守。

**原则 1：词汇表字符串 = tuple（数组）语义；标量 = 长度 1 的 tuple。词表永不为标量单列词目。**
- 正向 `TypeToString`（`SdkTypeVocabulary.cs:20-30`）只注册数组版（`double[]→"Double"`、`string[]→"String"`…）；标量（`double`）不在字典里，靠 `ToTypeVocabulary` 的 **Step 4 自动提升**（`double→double[]`，`SdkTypeVocabulary.cs:111-117`）归并到同一词目。
- 因此 `double`（标量）与 `double[]`（数组）共享 `"Double"` 一个词目——这正是 Halcon tuple 思想（一个数就是长度 1 的 tuple）。单值不是独立类型，强行单列 `"Double(单值)"` 才是浪费。
- 本次步骤1 新增的 `RoI` 同样遵循：`TypeToString[typeof(ShapeParameters[])]="RoI"`（数组），**不另设单值 `RoI` 词目**；手画单个矩形仍是"长度 1 的 RoI 数组"。

**原则 2：绑定匹配只看 CLR 类型（`IsArray`），不看词汇表字符串。**
- 连线筛选 `FilterDataSourcesByType`（`ParamBindingBase.cs:177-206`）比较的是 `ds.PropertyType` / `targetType` 这两个 **CLR Type** 的 `IsArray`，词汇表字符串只是序列化/显示标签，不参与连线匹配。
- 普通工具参数（Blob/Threshold 等）的 `DataType` 直接写 CLR `Type`（`typeof(double)` / `typeof(IVisionRegion[])`），根本不走 `ToSystemType`。
- 只有"用字符串声明类型"的场景才消费反向映射：脚本输入/输出、全局变量、以及 Region 参数经 `ToSystemType("Region")` 反查。其中 **Region 参数的目标类型由词表反查得出**，故步骤1 把 `ToSystemType("Region")` 改为 `IVisionRegion[]` 才能连上。

**原则 3（二次修订）：数组 = 单节点 + `;` 串联标签；默认折叠不铺行，可展开按字段聚合下钻。**
- **折叠态（默认）**：数组节点显示为 `属性名 (类型, N项): 值1; 值2; …`（单行 `;` 串联），树高度与数组长度**无关**，根除大数据炸树。超长串列宽截断，完整串放 ToolTip（悬停看全 N 项）。
- **展开态（用户主动点 `+`）**：才物化子节点（WPF `HierarchicalDataTemplate` 绑定 `Children`，展开才生成），按元素类型下钻：
  - `ShapeParameters[]`（RoI）：子节点按**字段聚合**（把对象数组"透视"成字段列）——列出该 RoI 类型的所有参数字段（矩形→左上X/左上Y/宽/高；圆→中心X/中心Y/半径），每个字段节点的值为**所有 RoI 元素在该字段上的值**用 `;` 串联（如 `左上X: 100; 150; 200`）。字段值来自各几何类已有的 `IMonitorable.GetMonitorFields()`，按字段名跨元素聚合。
  - `IVisionRegion[]`（Region 特征区域）：展开时**封顶**（如前 100 个），超出部分用溢出节点 `… 还有 N 项（输入索引查看）`，避免上百个 blob 铺满炸树（防炸优先，rule-008 KISS）。
  - 数值/字符串数组：展开即逐元素 `[0]/[1]…`，大数组同样封顶。
- 依据：用户反馈"数组展开成子节点、数据多时树炸了" → 默认折叠单行串联（防炸）；又反馈"RoI[] 要有子节点看左上/宽/高" → 展开后按**字段聚合**下钻（左上X/左上Y/宽/高 各为跨元素数组、用 `;` 串联），与防炸不冲突。
- 数组元素摘要规则（折叠态标签）：
  - 数值/字符串：`实际值1; 实际值2; …`
  - `IVisionRegion[]`：每个元素用紧凑摘要 `区域(宽x高)` 或 `区域(像素数)`（取 `BoundingBox`/`PixelCount`）
  - `ShapeParameters[]`（RoI）：`圆(半径50); 矩形(左上..宽..高..)`（取几何关键参数）

**原则 4：单元素 `[i]` 绑定走手动路径 `源.属性[i]`，不靠树行。**
- 标量目标想绑数组第 i 个元素：在 BindingSource 文本框写 `源.属性[i]`（`FilterDataSourcesByType` 注释已支持，`ParamBindingBase.cs:174`），`ScriptBindingResolver` 解析；越界直接报错（rule-015 不静默）。
- 树不再渲染 `[i]` 子节点（见原则 3），单元素绑定完全由手动路径承载。

**原则 5（新增）：传递层 `T[]` 统一，中间处理随意；全局变量与节点输出共用同一套 `AvailableDataSource` machinery。**
- 所有"数据进出"（节点输出→参数、全局变量→参数）一律 `T[]` 契约（tuple 语义）；工具 Run 内部用标量还是数组按算法实际需要，系统不进车间指手画脚。
- 全局变量不再自建扁平 action 节点，而是并轨为一等 `AvailableDataSource`（`SourceNodeName="全局变量"`、`PropertyName=变量名`、`PropertyType=变量实际存储类型`(必为 `T[]`)、`VariableType=词汇字符串`），流入与节点输出完全相同的 `FilterDataSourcesByType`+`BuildTreeStructure`+`;` 显示流程。两条通道行为一致。

---

## 一、调研记录（代码事实，可追溯）

### 文件分布
| 文件 | 行号 | 角色 |
|------|------|------|
| `src/Plugin.SDK/Models/SdkTypeVocabulary.cs:20-30` | `TypeToString` 正向，仅数组版 + `IVisionRegion`/`IVisionImage`/`Mat` 单值 | 类型词汇表（前向） |
| `src/Plugin.SDK/Models/SdkTypeVocabulary.cs:37-55` | `StringToType` 反向，全为标量（`"Region"→IVisionRegion` 等） | 类型词汇表（反向） |
| `src/Plugin.SDK/Models/SdkTypeVocabulary.cs:103-117` | Step 3 数组递归 + Step 4 标量自动提升 | tuple 归并实现 |
| `src/Plugin.SDK/Models/SdkTypeVocabulary.cs:54` | `["Rectangle"] = typeof(ShapeParameters)` | 遗留映射（已坏，序列化报 NotSupported） |
| `src/Workflow/GlobalVariable.cs:350-354` | `IsValidType` 闸门 `type != "Region"` | Region 禁止建全局变量 |
| `src/Workflow/GlobalVariableValueConverter.cs:123,166` | `IVisionRegion → throw NotSupportedException` | Region 不序列化（保留） |
| `src/Workflow/GlobalVariableValueConverter.cs:133-138` | `ParseAdaptive` 标量/数组两态 | 形状歧义根因 |
| `src/Plugin.SDK/UI/Controls/ParamBinding.cs:354-361` | 全局变量 action 节点（扁平、无过滤、无分组） | 通道不一致根因 |
| `src/Plugin.SDK/UI/Controls/ParamBinding.cs:738-745` | `CompleteGlobalVariableBinding` 只生成 `__GlobalVars__.变量名` 裸路径 | 无类型/无 `[i]` |
| `src/Plugin.SDK/UI/Controls/ParamBindingBase.cs:105-158` | `BuildTreeStructure` 按 `SourceNodeName`+`GroupName` 建树 | 绑定树构建 |
| `src/Plugin.SDK/UI/Controls/ParamBindingBase.cs:177-206` | `FilterDataSourcesByType` | 连线时类型筛选（按 CLR `IsArray`） |
| `src/Plugin.SDK/Execution/Parameters/AvailableDataSource.cs:143` | `Children`（原 `[i]` 子节点载体） | 数组元素节点（步骤6 改为不展开） |
| `src/Plugin.SDK/Execution/Helpers/ToolRegionHelper.cs:78-153` | `BuildHalconRegion` | 已能把 RoI/Fixed/Computed 统一成 HRegion |
| `src/Plugin.SDK/UI/Controls/Region/Models/RegionDefinition.cs:15-18` | `RegionDefinition [JsonPolymorphic]` | RoI/固定/计算 三态多态 |
| `src/Plugin.SDK/UI/Controls/Region/Models/RegionDefinition.cs:67` | `public object? Geometry` | RoI 几何元数据字段（无多态判别符） |
| `src/Plugin.SDK/Models/Geometry/Circle2d.cs:11` | `readonly struct Circle2d` | 几何类无 `[JsonPolymorphic]` |
| `src/Plugin.SDK/Execution/Parameters/RegionParameters.cs:66,79` | `InspectionRegions`/`MaskRegions` | 下游区域输入模型 |
| `tools/SunEyeVision.Tool.Threshold/ThresholdResults.cs:137` | `OutputRegions = IVisionRegion[]` | Blob 输出天生是数组 |
| `src/Plugin.SDK/UI/Controls/Region/Models/ParameterSource.cs:90,121` | `NodeOutputSource.Index` + `GetNodeOutputValue(..., index)` | 单元素 `[i]` 运行时切片 |
| `tests/SunEyeVision.Plugin.SDK.Tests/SdkTypeVocabularyContractTests.cs:37` | `ToTypeVocabulary(IVisionRegion,"Region")` | 现成契约测试（扩展点） |

### 接口/类型定义
- `src/Plugin.SDK/VisionAdapters/IVisionRegion.cs:10-29` — 统一区域接口（PixelCount/BoundingBox/GetMaskData/Clone）。
- `src/Plugin.SDK/UI/Controls/Region/Models/RegionDefinition.cs:51-293` — `ShapeParameters`（RoI）持有 `object? Geometry`，每个几何类 `IMonitorable.GetMonitorFields()` 已暴露元数据。
- `src/Plugin.SDK/Execution/Helpers/ToolRegionHelper.cs:137-153` — `BuildFromFixedRegion` 已能 `value is IVisionRegion → BuildHalconRegionFromVisionRegion`，下游已能消费上游 Region 输出。

### 调用链
- Blob 工具 `OutputRegions = IVisionRegion[]`（数组）→ 下游 `RegionParameters.InspectionRegions`（含 `RegionData`）→ `ToolRegionHelper.BuildHalconRegion` 解析为 HRegion。
- 连线筛选：`FilterDataSourcesByType(targetType, ...)` 比较 **CLR `IsArray`**（原则 2）。Blob 输出 `PropertyType=IVisionRegion[]`（数组），下游区域输入的 `targetType` 当前被词汇表反查 `ToSystemType("Region")` 成**单数** `IVisionRegion` → 触发 Rule 2（标量参数不显示数组）→ **连不上**。步骤1 将反向改为 `IVisionRegion[]` 即修复。

### 现有模式参考
- `RegionDefinition.cs:15-18` 的 `[JsonPolymorphic]` 三态模型可复用其"通用判别符"套路，给 `ShapeParameters.Geometry` 补 converter。
- `ToolRegionHelper.cs:78-88` 的 `region.Parameters switch { ShapeParameters / FixedRegion / ComputedRegion }` 已是"RoI 与 Region 统一汇入 HRegion"的现成证据——双轨汇入单类型方向正确。
- 节点输出已通过 `AvailableDataSource` 走过滤/建树/显示全流程——全局变量并轨复用此 machinery（原则 5）。

---

## 二、问题分析

- **表面需求**：①序列化各图形元数据；②下游区域同时支持 RoI 与 Region；③数组不炸树；④全局变量与节点输出一致。
- **根本问题**：系统把 **"参数化的图形定义（RoI）"** 与 **"运行时算出的特征区域（Region）"** 混在同一 `Region` 概念里，导致 RoI 该序列化却没正确序列化（几何元数据多态丢失）、Region 该进绑定却因单值/数组形态不匹配被挡、且两者都被全局变量闸门一刀切；叠加数组树渲染朴素（`[i]` 铺行）与全局变量双通道双实现，形成"炸树 + 行为不一致"双痛点。
- **问题分类**：**架构缺陷 + 契约漏洞 + UI 渲染朴素**。

### Bug 模式分析
- **当前 bug**：①手画 RoI 重开失效；②Region 数组连不上下游；③无 RoI 全局变量；④数组树炸（数百行）；⑤全局变量绑定与节点输出不一致（扁平、无类型）。
- **真因分类**：**契约漏洞 + 静默降级 + 双通道双实现 + 树渲染朴素**。
- **同类潜伏点**：`ParseAdaptive` 标量/数组两态（`GlobalVariableValueConverter.cs:133-138`）与 `SdkTypeVocabulary.StringToType` 数值反向标量，是同一"未真正落实 T[] 统一"的两种表现；`AvailableDataSource.Children` 的"按索引铺行"是树炸的直接代码原因；`ParamBinding.cs:354` 扁平 action 节点是通道不一致的直因。
- **治本点**：
  1. 修复 `object? Geometry` 多态往返（新增 `ShapeParametersJsonConverter` 写 `$type`），而非在全局变量层打补丁；
  2. 词汇表 `Region`→数组 + 新增 `RoI`，让连线 `IsArray` 一致；
  3. 全局变量并轨为一等 `AvailableDataSource`，消除双通道双实现；
  4. 数组树形改为单行 `;` 串联，根除爆炸；
  5. 存储层强制 T[]，消除形状歧义。

---

## 三、业界对标

| 框架/产品 | 关键特征 | 借鉴点 |
|-----------|---------|--------|
| Halcon (MVTec) | `gen_circle` 产出 ROI(`HRegion`)；`reduce_domain` 只认 `HRegion`，来源无关；tuple 语义 | 下游统一消费 `Region`，RoI 可提升为 Region |
| 海康 VisionMaster | 工具独立"ROI 区域设置"，Blob 输出"区域"结果；ROI 是参数、Region 是产物 | RoI 与 Region 两概念分离、RoI 可序列化 |
| Cognex VisionPro | `CogRectangleAffine`(用户 ROI) vs `CogBlobTool.Results.GetBlobs()` 返回 `CogRegion`(产物) | 下游统一认 Region 类型 |
| OpenCV | `cv::Rect`/`cv::RotatedRect`(参数) vs `cv::Mat` mask(产物)，无绑定模型 | 不借鉴（无数据流绑定语义） |

---

## 四、方案对比（已定稿为方案 A）

| 维度 | 方案 A（采纳） | 方案 B（仅数组化 Region 绑定） | 方案 C（`[i]` 子节点 + 封顶展开） |
|------|--------------|------------------------------|------------------------------|
| 实现方式 | 词汇表加 `RoI`、Region 改数组；`FilterDataSourcesByType` 加 RoI→Region 兼容；新增 `ShapeParametersJsonConverter`；全局变量并轨；数组单行 `;` 显示 | 只改 `ToSystemType("Region")`→数组 + 筛选规则 | 保留 `[i]` 子节点，大数据封顶/懒展开 |
| 架构一致性 | ✅ 复用 `ToolRegionHelper` + 双通道并轨 + 炸树根治 | ⚠️ 只解连线，RoI 持久化/炸树未治 | ⚠️ 树仍可能炸、通道仍不一致 |
| 复杂度 | 中（1 converter + 词汇表 + 筛选 + 并轨 + 显示改） | 低 | 中 |
| 治本性 | ✅ 修复 `object? Geometry` 契约漏洞 + 并轨 + 根治炸树 | ❌ 治标 | ❌ 未治本 |
| 长期 ROI | 6月✅/1年✅/3年✅ | 6月⚠️/1年❌ | 6月⚠️/1年⚠️ |

**采纳理由**：方案 A 同时满足"治本（修复几何多态根因）+ 双轨干净（RoI 可序列化、Region 仅绑定）+ 复用现有 `ToolRegionHelper` 汇入模型 + 全局变量并轨消除双实现 + 数组单行 `;` 根治炸树 + 改动最小"，契合 rule-008/rule-015，且全程遵守 〇 节五条设计原则。

---

## 五、解决方案（最终实施内容）

### 步骤1 — 词汇表：Region 改数组、新增 RoI（遵守 〇 原则 1，rule-010 PascalCase）
- `SdkTypeVocabulary.cs:29` 改 `TypeToString["Region"] = typeof(IVisionRegion[])`（前向改为数组/tuple）。
- `SdkTypeVocabulary.cs:45` 改 `StringToType["Region"] = typeof(IVisionRegion[])`（反向改数组——修复 一、调用链 的连线根因）。
- 新增 `StringToType["RoI"] = typeof(ShapeParameters[])` 与 `TypeToString[typeof(ShapeParameters[])] = "RoI"`（**数组形态**，与 〇 原则 1 一致；不另设单值 `RoI`）。
- 删除遗留 `StringToType["Rectangle"] = typeof(ShapeParameters)`（行54，已坏，rule-008 零兼容直接删）。
- **不新增任何标量单值词目**：`double`/`bool` 等标量经 Step 4 自动提升归并到既有 `"Double"`/`"Boolean"`（见 〇 原则 1），无需也不允许单列。

### 步骤2 — 全局变量闸门：放开 RoI、保留 Region 禁止（rule-015 报错优先）
- `GlobalVariable.cs:353` 改 `type != "Region" && type != "RoI"` → 实际即 `type is "Region" → 拒绝`，其余放行。

### 步骤3 — 序列化：新增 `ShapeParametersJsonConverter` 修复几何多态（治本）
- 新增 `src/Plugin.SDK/Models/ShapeParametersJsonConverter.cs`：序列化 `ShapeParameters` 时对 `Geometry` 额外写 `$type` 判别符（如 `"$geom":"Circle2d"`），反序列化时据 `$type` 用 `JsonSerializer.Deserialize(element, Type.GetType(...))` 还原具体几何类。
- 在 `RegionParameters`/工作流序列化 `JsonSerializerOptions` 注册该 converter（参考 `WorkflowJsonConverter.cs:19` 注册位置）。
- `GlobalVariableValueConverter.cs` 新增 `RoI` 分支：`ParseValue` 走 `ShapeParameters` → 用上述 converter 解析；`SerializeValue` 的 `switch` 加 `ShapeParameters sp => ("RoI", sp)`。
- `GlobalVariableValueConverter.cs:123,166` 的 `IVisionRegion → throw NotSupported` **保留不动**（Region 永不序列化）。

### 步骤4 — 绑定层：Region 目标接受 RoI 源（单向提升，遵守 〇 原则 2）
- `ParamBindingBase.cs:177-206` `FilterDataSourcesByType`：在精确匹配规则前新增——若 `targetType` 是 `IVisionRegion[]`（Region 输入，数组），且数据源 `VariableType=="RoI"`（RoI 源，数组），则匹配。反向不成立（Region 源不可喂 RoI 输入）。
- 运行解析：RoI 源经 `ToolRegionHelper.BuildFromShapeParams`（ToolRegionHelper.cs:110-132）提升为 `HRegion`，与计算 Region 统一汇入——此路径已存在，无需重造。
- 单元素 `[i]` 绑定：走手动路径 `源.属性[i]`（原则 4），不靠树行；本步骤不生成 `[i]` 子节点。

### 步骤5 — 全局变量通道并轨为一等 AvailableDataSource（遵守 〇 原则 5）
- 改造 `GlobalVariableSelectDialog`（`CompleteGlobalVariableBinding`）：选中变量后**构造 `AvailableDataSource`**（`SourceNodeName="全局变量"`、`PropertyName=变量名`、`PropertyType=变量实际存储类型`(必为 `T[]`)、`VariableType=词汇字符串`），流入与节点输出完全相同的 `FilterDataSourcesByType`+`BuildTreeStructure`+`;` 显示流程。
- **删除** `ParamBinding.cs:354` 的扁平"全局变量"action 节点与 `CompleteGlobalVariableBinding` 的裸字符串路径。
- 效果：全局变量在绑定树里带类型、带分组（归入"全局变量"组）、值用 `;` 串联，与节点输出无法区分。

### 步骤6 — 数组树形：单行 `;` 串联 + 可展开下钻（遵守 〇 原则 3）
- **默认折叠态**：数组节点显示为 `属性名 (类型, N项): 值1; 值2; …`（单行 `;` 串联），不主动铺开子节点（防炸）。
- **展开下钻**（`AvailableDataSource.Children` 改为**展开才物化**）：
  - `ShapeParameters[]`（RoI）：子节点按字段聚合——矩形→左上X/左上Y/宽/高，圆→中心X/中心Y/半径；每个字段节点值 = 所有 RoI 元素该字段 `;` 串联。
  - `IVisionRegion[]`（Region）：展开封顶（如前 100），溢出节点引导索引查看。
  - 数值/字符串：展开逐元素 `[i]`，大数组同样封顶。
- 超长 `;` 串列宽截断，完整串放 ToolTip（悬停看全 N 项）。
- 单元素绑定（标量目标要第 i 个）：走手动路径 `源.属性[i]`（原则 4），不靠树行。

### 步骤7 — 存储层强制 T[]（消除形状歧义，rule-015）
- `GlobalVariableValueConverter.cs:133-138` 的 `ParseAdaptive` **删除标量分支**，`Double` 永远存 `double[]`、`Integer` 永远存 `long[]`…。填 `3.14` 也存成 `[3.14]`，与词汇表 T[] 完全一致。

### 清理清单
| 文件 | 清理内容 | 原因 |
|------|---------|------|
| `src/Plugin.SDK/Models/SdkTypeVocabulary.cs:54` | 删除 `["Rectangle"] = typeof(ShapeParameters)` 遗留映射 | 已坏，靠 NotSupported 兜底，零兼容直接删 |
| `GlobalVariableValueConverter.cs:24` | 更新类 XML doc 注释（去掉"区域(Region)可序列化"误导，明确 RoI 可序列化、Region 不可） | 注释与实现不一致 |
| `GlobalVariableValueConverter.cs:133-138` | 删除 `ParseAdaptive` 标量分支 | 强制 T[]，消除形状歧义 |
| `src/Plugin.SDK/UI/Controls/ParamBinding.cs:354-361` | 删除扁平"全局变量"action 节点 | 并轨为一等 ADS（原则 5） |
| `src/Plugin.SDK/UI/Controls/ParamBinding.cs:738-745` | 改 `CompleteGlobalVariableBinding` 为构造 ADS | 同上 |
| `AvailableDataSource.cs:143` / `ModuleResultNode` 数组展开 | 移除 `[i]` 子节点生成，改单行 `;` 串联 | 根治炸树（原则 3） |

### 防御机制
| 防御机制 | 说明 | 验证方式 |
|---------|------|---------|
| 契约测试（SdkTypeVocabulary） | 扩展 `SdkTypeVocabularyContractTests.cs`：断言 `ToSystemType("Region")==IVisionRegion[]`、`ToTypeVocabulary(IVisionRegion[])=="Region"`、`ToSystemType("RoI")==ShapeParameters[]`、`ToTypeVocabulary(ShapeParameters[])=="RoI"`；**追加 tuple 原则断言**：`ToTypeVocabulary(typeof(double))=="Double"` 且 `ToTypeVocabulary(typeof(double[]))=="Double`（标量经 Step4 归并到同一词目）、正向 `TypeToString` 字典**不包含** `typeof(double)` 键（无独立单值词目）、`ToSystemType("Double")==typeof(double)`、`ToSystemType("Double[]")==typeof(double[])` | unit test |
| 端到端冒烟 | 手画圆→存方案→重开→断言区域边界非空；Blob 输出→连线到下游区域输入→运行→断言下游吃到区域；RoI 全局变量引用生效；数组树单行 `;` 显示不炸（1000 项仍一行） | manual + 集成 |
| Fail-Forward | `ShapeParametersJsonConverter` 遇未知 `$type` 抛 `NotSupportedException`；`ParseAdaptive` 非数组 JSON 抛异常（不静默，rule-015）；`源.属性[i]` 越界直接报错 | unit test |

---

## 六、工具输入/输出优化

- **输出（已正确，无需改工具代码）**：`ThresholdResults.cs:137`/`BlobAnalysisResults.cs:202`/`MorphologyResults.cs:120` 的 `OutputRegions = IVisionRegion[]` 天生数组，结果树单行 `;` 串联显示。
- **输入（本次收口）**：区域输入（`InspectionRegions`/`MaskRegions`）目标类型经 `ToSystemType("Region")` 反查改为 `IVisionRegion[]` 后，与上游 `IVisionRegion[]` 输出 `IsArray` 一致（原则 2），连线可见；同时接受 RoI 源（步骤4 单向提升）。下游工具代码零改动。
- **T[] 统一传递（原则 5）**：全局变量与节点输出共用 ADS machinery，输入绑定行为一致；工具 Run 内部标量/数组写法按算法实际需要，系统不干预。

---

## 七、最终结果树 & 绑定树（最终形态）

### 7.1 结果树（选中「Blob分析3」）
```
Blob分析3 (根)
├── 输出图像 (Image[], 1项): <图像>
├── 连通域区域 (Region[], 3项): 区域(120x80); 区域(60x40); 区域(200x150)
├── 直方图数据 (Double[], 256项): 0.1; 0.2; 0.05; 0.3; …
├── 统计
│   ├── 白色像素数 (Integer[], 1项): 1234
│   └── 白色像素比例(%) (Double[], 1项): 0.37
└── 判断
    └── 是否通过 (Boolean[], 1项): true
```
> **传递链统一 T[]**：连标量结果也存为 `T[], 1项`（步骤7 存储层强制 T[]、删 ParseAdaptive 标量分支）。数组永远单行、值 `;` 串联；1000 个区域也只是一行（列宽截断，悬停看全）。单值本质仍是数组，绑定 `源.属性[0]` 取标量（原则 4）。

### 7.2 绑定树（给区域参数选源）
```
节点输出
├── Blob分析3
│   ├── 输出图像 (Image)
│   └── 连通域区域 (Region[], 1000项): 区域(…); 区域(…); …   ← 折叠单行,展开封顶前100
├── 阈值化4
│   └── 二值区域 (Region[], 1项): 区域(300x200)
└── 全局变量                              ← 与节点输出同机制、同显示
    └── 检测框 (RoI[], 1项): 矩形(左上100,80 宽50 高30)      ← 折叠单行
```

**展开 `检测框` 子节点（按字段聚合，每个字段跨所有 RoI 元素 `;` 串联）：**
```
└── 检测框 (RoI[], 3项)
    ├── 左上X (Double[], 3项): 100; 150; 200
    ├── 左上Y (Double[], 3项): 80; 90; 110
    ├── 宽 (Double[], 3项): 50; 60; 70
    └── 高 (Double[], 3项): 30; 40; 50
```
> 子节点是**字段**（左上X/左上Y/宽/高），每个字段本身又是"跨所有 RoI 元素的数组"，值用 `;` 串联。这正符合你说的"左上的X[]串联、Y[]串联、宽串联、高串联"。单矩形时即 `左上X: 100`。全局变量与节点输出无法区分。

### 7.3 单元素绑定（标量目标要第 57 个区域）
- 在参数绑定的 BindingSource 文本框写：`Blob分析3.连通域区域[57]` → 运行时 `GetNodeOutputValue(nodeId, name, 57)` 切片；越界直接报错（rule-015）。不靠树行。

---

## 八、验收标准

### 功能验收
| # | 验收项 | 验收方法 | 关联测试 |
|---|--------|---------|---------|
| 1 | 词汇表 `Region` 反查为 `IVisionRegion[]`，`RoI` 反查为 `ShapeParameters[]`；`double` 与 `double[]` 均归并到 `"Double"`（tuple 原则） | unit | 契约测试 |
| 2 | 手画圆/矩形/多边形存为 RoI 全局变量，重开方案后图形参数精确还原（`Geometry is Circle2d` 为真，中心/半径正确） | unit + manual | 契约测试 + 冒烟 |
| 3 | Blob 工具 `OutputRegions`(数组) 能连到下游区域输入（连线列表可见并可选） | manual | 端到端冒烟 |
| 4 | 下游区域输入可同时接 RoI 源与 Region 源，运行均生效 | manual | 端到端冒烟 |
| 5 | 数组在结果树/绑定树**单行 `;` 串联**（默认折叠），1000 项不炸树；全局变量与节点输出同形态、同分组 | manual | 端到端冒烟 |
| 5b | `RoI[]` 可**展开按字段聚合**看几何参数（左上X/左上Y/宽/高 各为跨元素数组、`;` 串联），值正确 | manual | 端到端冒烟 |
| 6 | 标量绑数组第 i 个：手填 `源.属性[i]` 运行取到；越界报错 | manual | Fail-Forward |
| 7 | 仍无法创建 `Region` 类型全局变量（闸门保留） | unit/manual | 契约测试 |
| 8 | `ParseAdaptive` 非数组 JSON 抛异常（强制 T[]） | unit | Fail-Forward |

### 边界/异常验收
| # | 验收项 | 验收方法 | 关联测试 |
|---|--------|---------|---------|
| 1 | `ShapeParametersJsonConverter` 遇未知 `$type` 抛 `NotSupportedException`（不静默） | unit | Fail-Forward |
| 2 | `GlobalVariableValueConverter` 遇 `IVisionRegion` 值仍抛 NotSupported | unit | 契约测试 |
| 3 | 脚本将数组源喂给单值输入却未写 `[i]` → 抛异常（rule-015 不静默） | unit | 契约测试 |
| 4 | `源.属性[i]` 越界（i ≥ 数组长度）→ 运行时报错，非静默空值 | manual | Fail-Forward |

### 数据一致性验收
| # | 验收项 | 验收方法 | 关联测试 |
|---|--------|---------|---------|
| 1 | RoI JSON 序列化→反序列化往返一致（ShapeType/几何字段无损） | unit | 契约测试 |
| 2 | 历史方案（含手画区域）重开后区域不再静默置空 | manual | 端到端冒烟 |
| 3 | 全局变量强制 T[] 后，标量值（如 `3.14`）存储为 `[3.14]`，与词汇表 T[] 一致 | unit | 契约测试 |

---

## 九、测试方法
| 测试类型 | 范围 | 自动化 |
|---------|------|:------:|
| 单元测试 | SdkTypeVocabulary 映射（含 tuple 原则断言）、ShapeParameters converter 往返、GlobalVariable 闸门、ParseAdaptive 强制 T[] | ✅ |
| 集成测试 | Blob→下游区域连线 + 运行 | ❌（手动/UI） |
| 端到端测试 | 手画RoI→存方案→重开还原；数组单行 `;` 显示不炸；全局变量并轨 | ❌（手动） |
| 回归测试 | 既有 RegionParameters 序列化（工具参数里的手画区域）往返仍正确 | ✅ |

### 手动验证步骤
1. 新建方案 → 区域编辑器手画圆 → 设为某工具检测区域 → 保存 → 关闭 → 重开 → 断言圆区域仍在且形状正确。
2. 变量表新建 "RoI" 类型全局变量（存手画矩形参数）→ 引用到工具区域输入 → 运行 → 断言生效；确认绑定树里该变量带类型、带"全局变量"分组、值用 `;` 串联。
3. Blob 分析节点 → 拖线到下游区域输入 → 连线列表可见 Blob 输出（数组）→ 运行 → 断言下游吃到区域。
4. 选中 Blob 节点 → 结果树中"连通域区域"显示单行 `;` 串联（`区域(…); 区域(…); …`），即使 1000 项也只有一行。
5. 给标量区域输入手填 `Blob分析3.连通域区域[57]` → 运行 → 断言取到第 58 个 Blob；填越界索引 → 运行时报错。

---

## 十、风险评估
| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| `ToSystemType("Region")` 改为数组后，其他调用方若期望单值 `IVisionRegion` 可能报错 | 中等 | 契约测试覆盖反查；实施时 grep 所有 `ToSystemType("Region")` 调用点逐一确认 |
| `ShapeParametersJsonConverter` 注册位置影响既有工具参数序列化 | 低 | converter 仅增强 `Geometry` 的 `$type` 写入，向后兼容既有不含 `$type` 的 JSON |
| Region 闸门改动误放开 Region 全局变量 | 低 | 显式断言 `IsValidType("Region")==false` |
| 误加"单值词目"（如独立 `"RoI"` 单值）破坏 tuple 原则 | 低 | 契约测试断言正向 `TypeToString` 不含标量 `double` 键、且 `ToTypeVocabulary(double)=="Double"`；Code Review 按 〇 原则 1 卡口 |
| 移除 `[i]` 子节点后，既有 `源.属性[i]` 绑定路径失效 | 低 | 手动路径机制保留（原则 4），契约测试覆盖 |
| 全局变量并轨改动影响既有变量引用 | 中 | 并轨仅改变"入口"形态（构造 ADS），运行时 `__GlobalVars__.X` 注入机制不变（WorkflowExecutionEngine.cs:723-731）；冒烟覆盖 |

### 已知限制（后续清理，不在本方案范围）
- 词汇表**前向数组 / 反向标量**的不对称仅在本方案对 Region 做反向数组化后局部收敛；`Image`（`IVisionImage`/`Mat`）及数值类型仍保持"前向数组、反向标量"现状。当前不影响绑定（普通参数走 CLR `Type`，脚本数组场景用显式 `"Double[]"` 等遗留词条），列入后续方案统一对称化。

---

## 十一、验证清单（实施后逐条核验）
- [ ] 遵循 rule-008：零兼容，`["Rectangle"]`、ParseAdaptive 标量分支、`[i]` 子节点已删，无兜底/兼容层
- [ ] 遵循 rule-015：`ShapeParametersJsonConverter` 未知 `$type` 报错；脚本数组喂单值缺 `[i]` 报错；`源.属性[i]` 越界报错；ParseAdaptive 非数组抛异常，非静默
- [ ] 遵循 rule-010：新增序列化用 System.Text.Json + PascalCase，无 Newtonsoft
- [ ] 遵循 〇 原则 1：词汇表仅数组(tuple)词目，无独立单值词目（`double`/`RoI` 均经 Step4/数组形态归并）
- [ ] 遵循 〇 原则 2：绑定匹配基于 CLR `IsArray`，未引入基于词汇表字符串的匹配
- [ ] 遵循 〇 原则 3：数组 = 单节点 + `;` 串联（默认折叠不铺行）；RoI[] 可展开下钻看几何参数、Region[] 展开封顶防炸
- [ ] 遵循 〇 原则 4：单元素 `[i]` 走手动路径，未靠树行
- [ ] 遵循 〇 原则 5：全局变量并轨为一等 ADS，与节点输出共用过滤/建树/`;` 显示
- [ ] 两条通道（节点输出/全局变量）在绑定树、过滤、`;` 显示行为一致
- [ ] 验收标准所有条目通过；防御机制（契约测试/冒烟/Fail-Forward）已实现
- [ ] 移交方案测试 skill 按上表验收
