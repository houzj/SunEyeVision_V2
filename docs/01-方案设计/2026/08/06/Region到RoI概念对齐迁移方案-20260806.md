# Region 到 RoI 概念对齐迁移方案（V1.0）

> 创建日期：2026-08-06
> 类型：方案设计（01-方案设计 / 2026 / 08）
> 状态：待评审

## 🔄 改前 → 改后 → 依据

### 改前（用户视角）

代码里"名不符实"：**名字叫 `Region` 的一整套东西，装的其实是"RoI（用户几何）"**。

- 33 个源文件堆在 `src/Plugin.SDK/UI/Controls/Region/` 下，命名空间全是 `...UI.Controls.Region.*` [来源] `RegionDefinition.cs:11`
- 但核心类 `ShapeParameters` 在 `SdkTypeVocabulary` 注册的词汇就是 **"RoI"**（图形定义）[来源] `SdkTypeVocabulary.cs:35,57`
- 同一时间，"Region" 又被明确用来指**算法像素区域**（`IVisionRegion`/`CvRegion`/`HalRegion`），且规则写明"禁止进入工作流契约" [来源] `SdkTypeVocabulary.cs:19-20`、`RegionConverter.cs:12-15`

**同一个词两个意思**，读代码要反复猜。外加 `Models/Roi/` 下 6 个文件（`IRoi`/`RectangleRoi`/`CircleRoi`/`PolygonRoi`/`AnnulusRoi`/`FullImageRoi`）是**死代码**——它们唯一被 `IRoiToolPlugin` 接口引用，而该接口**全仓无任何实现** [来源] `IToolPlugin.cs:202-207`。

### 改后（用户视角）

- "用户几何"整套改名为 **`Roi*`**：`RegionDefinition`→`RoiDefinition`、`RegionData`→`RoiData`、`RegionEditorControl`→`RoiEditorControl`、命名空间 → `...UI.Controls.Roi.*`
- "Region" 从此只剩一个意思（算法像素区域），"Roi" 只剩一个意思（用户几何），**代码自解释**
- `Models/Roi/` 6 个死文件 + `IRoiToolPlugin` 接口删除，RoI 概念只剩 `ShapeParameters` 一套实现
- 已有方案文件（旧配方 JSON）**照常读取**——`$type` 判别字符串 "Shape"/"Fixed"/"Computed" 是显式写的，与类名无关 [来源] `RegionDefinition.cs:16-19`

### 依据（业界参考）

| 框架 | 做法 | 借鉴 |
|------|------|------|
| **Halcon** | `HRegion`（iconic 像素）与 metrology 几何对象分离 | ✅ 像素区域与几何分离命名 |
| **VisionPro** | `CogRegion`（像素） vs `CogCircle`/`CogRectangle`（几何图形） | ✅ 几何类不带 Region 字样 |
| **VisionMaster** | 区域（mask）管理与 ROI 图形绘制分离 | ✅ UI 编辑对象叫 ROI |

---

## 方案全景

```
┌─ 现状（词义混乱）─────────────────────────────┐
│  UI.Controls.Region.*  ← 实为 RoI（用户几何）  │ ← 主错位，要迁移
│  Models/Roi/*          ← 死代码，无人实现      │ ← 要删除
│  IVisionRegion/CvRegion ← 算法区域（Region）   │ ← 命名正确，保留
└───────────────────────────────────────────────┘
        │ Phase 1+2 改名      │ Phase 3 删除
        ▼                     ▼
┌─ 目标（词义单一）─────────────────────────────┐
│  UI.Controls.Roi.*  ← RoI（用户几何）           │
│  IVisionRegion/CvRegion ← Region（算法区域）     │
└───────────────────────────────────────────────┘
```

---

## 调研记录

### 文件分布

| 文件 | 行号 | 角色 |
|------|------|------|
| [来源] `SdkTypeVocabulary.cs` | 35,57 | `ShapeParameters[]` = "RoI"（真相源） |
| [来源] `RegionConverter.cs` | 12-15 | Region↔RoI 桥接，"契约只认 RoI" |
| [来源] `RegionDefinition.cs` | 11,16-20,52 | 基类 + `$type` 判别串 + 3 子类 |
| [来源] `RegionData.cs` | 11 | 顶层容器（RoI 语义） |
| [来源] `ShapeType.cs` | 7,63 | `ShapeType`/`RegionSourceMode` |
| [来源] `RegionType.cs` | 6 | `RegionType` 枚举（RoI 语义） |
| [来源] `Views/*.xaml` | — | 8 个 SDK XAML 引 `clr-namespace:...Region.*` |
| [来源] `RegionParameters.cs` | 41 | 工具参数基类（6 工具继承） |
| [来源] `RegionCollectionParameter.cs` | 42 | 区域集合参数 |
| [来源] `RegionParameterMode.cs` | 9 | 区域参数模式枚举 |
| [来源] `IToolPlugin.cs` | 202-207 | `IRoiToolPlugin`（无实现） |
| [来源] `Models/Roi/*.cs` | — | 6 个死文件 |

### 引用方统计

| 层 | 文件数 | 代表 |
|----|:---:|------|
| SDK 内部 | ~40 | RegionConverter、ToolRegionHelper、ParamBindingBase、OutputVariableResolver、ToolResults、CvRegion、CvRegionHelper、ImageControl.xaml 等 |
| 工具层 | 25 | Threshold/Morphology/ImageThreshold/ImageFilter/BlobAnalysis/ShapeMatch 的 XAML+cs |
| UI 层 | 3 | MainWindowViewModel、NodeResultManager、GlobalVariableValueConverter |
| 测试 | 6 | SdkTypeVocabularyContractTests、RoiBindingContractTests、RegionConverterTests 等 |

---

## 问题分析（Phase 0）

- **表面需求**：Region 相关代码按概念迁移到 RoI
- **根本问题**：`Region` 一词双义（算法区域 vs 用户几何），代码不可自解释
- **问题分类**：**架构冗余**（两套 RoI 模型并存）+ **命名混乱**
- **架构归属**：`UI.Controls.Roi`（保持 UI 层，不挪层）
- **不改是否可覆盖**：否（死代码不删永远并存）
- **是否改变分层边界**：否（只改名）

### 方法对比

| 维度 | 方案 A：整体改名（推荐） | 方案 B：只改命名空间 | 方案 C：只删死代码 |
|------|:---:|:---:|:---:|
| 实现 | `Region*`→`Roi*` 全量 | 只改 `...Region`→`...Roi` | 只删 `Models/Roi` |
| 一致性 | ✅ 完全对齐 | ⚠️ 半对齐 | ❌ 主错位不解决 |
| 复杂度 | 🔴 60+ 文件 | 🟡 40+ | 🟢 8 |
| 长期 ROI | 🏆 3 年 | 🟡 1 年 | ❌ 无 |

> 用户已选定：**方案 A + C**（命名对齐 + 清理死代码）。

---

## Bug 模式分析（Phase 0.5）

**当前问题**：`Region` 双义 + RoI 双套并存。

**真因分类**：**设计缺陷**——`UI/Controls/Region/` 沿用了"ROI 编辑器"迁移前的旧目录名，迁移时只改了 UI 层没改命名空间 [来源] `ROI到Region迁移方案.md`；`Models/Roi` 是早期遗留，新体系建立后未清理。

**同类潜伏点**：
- [来源] `Execution/Parameters/RegionParameters.cs` — 参数系统"区域"同为 RoI 语义
- [来源] `Models/Roi/` — 6 死文件与新体系并存
- [来源] `RegionConverter.RoiToRegion` — 桥接器命名正确（Region 指算法区域）

**治本点**：以 `SdkTypeVocabulary`（"RoI"=`ShapeParameters[]`）为唯一真相源，统一承载该词汇的命名到 `Roi*`，删除不被真相源引用的旧模型。

---

## 业界对标（Phase 0.7）

| 框架 | 关键特征 | 借鉴/不借鉴 |
|------|---------|------------|
| Halcon | `HRegion` 与 metrology 几何分离 | ✅ 借鉴分离命名 |
| Cognex VisionPro | `CogRegion` vs `CogCircle` 几何类 | ✅ 借鉴几何类不带 Region |
| VisionMaster | 区域管理与 ROI 工具分离 | ✅ 借鉴 UI 对象叫 ROI |
| 任何把用户几何叫 Region 的做法 | — | ❌ 不借鉴 |

---

## 解决方案

### 完整命名映射表

#### 一、命名空间（6 个子空间 + 根）

| 原 | 新 |
|----|----|
| `SunEyeVision.Plugin.SDK.UI.Controls.Region` | `SunEyeVision.Plugin.SDK.UI.Controls.Roi` |
| `...Region.Models` / `.Views` / `.ViewModels` / `.Logic` / `.Rendering` / `.Converters` | 同名替换 `Roi.*` |

#### 二、Models（10 文件）

| 文件 | 类 | 动作 |
|------|----|----|
| `RegionDefinition.cs` | `RegionDefinition`→`RoiDefinition`；`FixedRegion`→`FixedRoi`；`ComputedRegion`→`ComputedRoi` | 改名 |
| — | `ShapeParameters` | **保留**（本就 RoI 语义） |
| `RegionData.cs` | `RegionData`→`RoiData` | 改名 |
| `RegionType.cs` | `RegionType`→`RoiType` | 改名 |
| `ShapeType.cs` | `RegionSourceMode`→`RoiSourceMode` | 改名 |
| — | `ShapeType`/`Point2D` | 保留 |
| `RegionEditorSettings.cs` | `RegionEditorSettings`→`RoiEditorSettings` | 改名 |
| `ParameterSource.cs` / `ParamSettingItem.cs` / `NodeOutputInfo.cs` / `ShapeToolItem.cs` / `GeometryJsonConverter.cs` | — | 保留 |

#### 三、Views（7 控件 + 7 code-behind）

| 原 | 新 | | 原 | 新 |
|----|----|--|----|----|
| `RegionEditorControl` | `RoiEditorControl` | | `ShapeToolBar` | 保留 |
| `RegionEditorManagerControl` | `RoiEditorManagerControl` | | `NodeSelectorPopup` | 保留 |
| `RegionInfoPanel` | `RoiInfoPanel` | | `DrawingParameterPanel` | 保留 |
| `RegionSubscribePanel` | `RoiSubscribePanel` | | | |

#### 四、ViewModels / Logic / Rendering / Converters

| 层 | 改名 | 保留 |
|----|------|------|
| ViewModels | `RegionEditorViewModel`→`RoiEditorViewModel` | `ParameterPanelViewModel`、`NodeSelectorViewModel` |
| Logic | `RegionResolver`→`RoiResolver`、`RegionEditorIntegration`→`RoiEditorIntegration` | `HandleManager`、`EditHistory`、`DataTypeConverter` |
| Rendering | `RegionHitTester`→`RoiHitTester`、`IRegionRenderer`→`IRoiRenderer` | `ShapeRenderer`、`WpfShapeRenderer`、`HandleRenderer`、`RotatedRectangleHelper` |
| Converters | `RegionConverters`→`RoiConverters` | — |
| 根 | `RegionEditorDemo`→`RoiEditorDemo` | — |

#### 五、执行层参数（Phase 2）

| 原 | 新 |
|----|----|
| `RegionParameters` | `RoiParameters` |
| `RegionCollectionParameter` | `RoiCollectionParameter` |
| `RegionParameterMode` | `RoiParameterMode` |

公共方法同步：`GetRegionsByType`→`GetRoisByType`、`GetEffectiveRegions`→`GetEffectiveRois`、`GetEnabledRegions`→`GetEnabledRois`、`GetVisibleRegions`→`GetVisibleRois`、`AddRegion/RemoveRegion/ClearRegions`→`AddRoi/RemoveRoi/ClearRois`（调用方同步，6 个工具参数类继承名同步）。

#### 六、删除（Phase 3）

| 项 | 原因 |
|----|------|
| `Models/Roi/` 6 个文件 | 死代码，仅被无人实现的接口引用 |
| `IToolPlugin.cs` 中 `IRoiToolPlugin` | 无实现者 |

#### 七、保留不动

`ShapeParameters`、`ShapeType`、`Point2D`、`RegionConverter`（桥接器名正）、`ToolRegionHelper`、`IVisionRegion`/`CvRegion`/`HalRegion`（算法区域）、`Generic.xaml`（样式资源无类名依赖，仅注释提及，1660 行附近检查即可）、`SdkTypeVocabulary` 中 "RoI" 词汇、JSON `$type` 判别串。

---

### 实施步骤

1. **Phase 1a**：目录 `UI/Controls/Region/` → `UI/Controls/Roi/`，批量改 33 个文件 `namespace`/`using`
2. **Phase 1b**：按映射表批量改类名；8 个 SDK XAML 改 `clr-namespace` + `x:Class`
3. **Phase 1c**：同步 SDK 执行层（~40 文件）、UI 层 3 文件、工具层 25 文件（含 8 个工具 XAML 的 `xmlns:region`）、测试 6 文件
4. **Phase 2**：`RegionParameters` 三件套 → `Roi*`，6 工具继承链 + 公共方法同步
5. **Phase 3**：删 `Models/Roi/` 6 文件 + `IRoiToolPlugin`
6. **构建验证**：`dotnet build SunEyeVision.slnx` 全量 0 错误，6 个工具 csproj 逐个编译
7. **残留扫描**：`search_content "UI.Controls.Region|Models.Roi|RegionDefinition|RegionData\b"` = 0 命中
8. **端到端冒烟**：启动程序，打开 Threshold 工具"区域设置"面板，绘制/编辑/订阅/保存方案

### 清理清单

| 文件 | 清理内容 | 原因 |
|------|---------|------|
| `Models/Roi/*.cs` | 删 6 文件 | 死代码 |
| `IToolPlugin.cs` | 删 `IRoiToolPlugin` | 无实现 |
| `UI/Controls/Roi/*.md` | 随目录迁移并更新文档内旧名 | 文档同步 |
| `PluginDevKit/sdk/net9.0/SunEyeVision.Plugin.SDK.xml` | 构建产物，重生成 | 不需手动改 |

---

## 防御机制

| 防御机制 | 说明 | 验证方式 |
|---------|------|---------|
| 契约测试 | `SdkTypeVocabulary` 契约断言 `ShapeParameters[]` 仍映射 "RoI" | unit test |
| 序列化往返测试 | 旧 JSON（$type="Shape"）反序列化为新 `ShapeParameters` 成功 | unit test |
| 全仓残留扫描 | 实施后旧命名零命中 | code review |
| 端到端冒烟 | 区域设置面板全流程手测 | manual |

---

## 验收标准

| # | 验收项 | 验收方法 | 关联测试 | 防御机制 | 备注 |
|---|--------|---------|---------|---------|------|
| 1 | `ShapeParameters[]` 仍映射 "RoI" | unit | SdkTypeVocabularyContractTests | 契约测试 | 语义不变 |
| 2 | 旧 JSON（$type="Shape"/"Fixed"/"Computed"）可反序列化 | unit | 新增往返测试 | 序列化契约 | 数据兼容 |
| 3 | 全仓 `UI.Controls.Region`/`Models.Roi`/`RegionDefinition` 零命中 | review | — | 残留扫描 | — |
| 4 | `SunEyeVision.slnx` 全量编译 0 错误 | build | — | 构建验证 | — |
| 5 | 6 工具 DebugControl 的 `region:` 控件正常显示 | manual | — | 冒烟 | 区域设置面板 |
| 6 | 绘制/拖动/订阅区域功能正常 | manual | — | 冒烟 | 画圆改半径 |
| 7 | `IRoiToolPlugin` + `Models.Roi` 全部删除 | review | — | 残留扫描 | — |

---

## 测试方法

| 类型 | 范围 | 自动化 |
|------|------|:---:|
| 单元测试 | 3 个 SDK 契约测试更新 + 1 个新增序列化往返 | ✅ |
| 端到端 | 6 个区域工具面板手测 | ❌ |
| 回归 | 保存/加载方案 | ❌ |

**手动步骤**：启动 → Threshold → 区域设置 → 画圆 → 拖手柄 → 存方案 → 重开 → 形状恢复。

---

## 风险评估

| 风险 | 影响 | 缓解 |
|------|------|------|
| 工具 XAML `clr-namespace` 漏改 | 编译期暴露 | Phase 1c 逐工具核对 + 全量构建 |
| 测试硬编码类型名 | 测试编译失败 | 6 文件同步 |
| 文档残留旧名 | 概念仍混淆 | 清理清单覆盖文档 |
| `$type` 判别串被误改 | 旧配方读取失败 | 序列化往返测试锁定 |

---

## 最终决策建议

1. **Phase 2 纳入** ✅ — 既然要"完整对齐"，`RegionParameters` 三件套同为 RoI 语义，一并改名；但它独立成 Phase，风险隔离，若只想动核心命名可临时裁掉。
2. **目录物理改名** ✅ — `UI/Controls/Region/` → `UI/Controls/Roi/`，git 可跟踪 rename，`git mv` 保历史。
3. **文档落地** — 本文件即为方案文档，待评审确认后由方案实施技能执行。

---

## 验证清单

- [x] rule-008：零兼容，死代码直接删，无兼容层
- [x] rule-008：报错优先，改名后编译失败即暴露遗漏
- [x] rule-010：`$type` 判别串不变，序列化格式不破坏
- [x] rule-002：`Roi*` PascalCase，`IRoiRenderer` 接口规范
- [x] 用户功能性视角开头（rule-019 说人话）
- [x] 方案对比 ≥3 + 长期 ROI
- [x] 防御机制 ≥2（契约测试 + 序列化往返 + 残留扫描 + 冒烟）
- [x] 全部结论有 [来源] 标注
