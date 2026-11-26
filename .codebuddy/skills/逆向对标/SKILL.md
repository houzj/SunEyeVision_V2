---
name: 逆向对标
description: 原厂（VisionMaster / VM / VM4）逆向对标技能。用于任何"和原厂对齐、原厂取证、逆向分析"的需求：节点/画布交互、参数页、状态语义、视觉样式、数据结构、端口与执行行为、默认值与显隐条件。强制先取证再改：证据分级（反编译源码行号 > 原厂配置 XML > 原厂实机截图），证据写入对照文档，按证据实施，实测回归。触发场景（命中任一即用）：原厂、原厂VM、原厂VISIONMASTER、VisionMaster、VM4、VM 原厂、逆向、逆向分析、逆向对标、取证、原厂取证、原厂证据、证据链、反编译、反编译源码、decompiled、原厂怎么实现、原厂实现、原厂配置、原厂参数、原厂默认值、原厂触发条件、对照原厂、原厂对照、对标原厂、抄原厂、差异对照、样式/交互/状态/文案不一致、执行防呆、节点形状、端口、连线、框选、拖拽。
---

# 逆向对标技能

把"看起来像"变成"证据证明它是"。**任何对标改动，先取证，再动手。**

## 1 触发场景

**关键词命中即用**（用户原话里出现任一即可触发本技能）：原厂、原厂VM、VisionMaster、VM4、逆向、逆向分析、取证、原厂取证、原厂证据、反编译、decompiled、原厂怎么实现、原厂参数、原厂默认值、对照原厂、对标原厂、抄原厂、差异对照。

具体场景：

- 用户指某处"和原厂不一样"（样式/交互/状态/文案/流程）
- 需要产出原厂对照清单或差异表
- 需要判断某个行为"原厂到底怎么做"
- 需要原厂参数默认值、显隐条件、触发关系（Triggers）取证
- 正在实现某个原厂已有功能（画布、节点、参数页、执行、结果呈现）
- 手里已有原厂截图/XML/反编译产物，要把它变成可实施的差异清单

## 2 铁律（违反即返工）

1. **无证据不修改**。没有源码/配置级证据时，只能提出取证请求，不能改代码。
2. **截图只是线索**。目测像素差异只能用于"发现差异"，定性必须落到源码行号。
3. **不把推断写成结论**。文档里区分"已证实 / 待取证"。
4. **证据与代码同批提交**（commit 里含文档或注释中的行号引用）。
5. **不靠用户喂图找问题**。要自己用"源码规格 → 逐项比对 → 产出差异"的方式主动找。

## 3 证据来源（本仓库固定路径）

| 级别 | 来源 | 路径 |
|---|---|---|
| ① | 原厂反编译源码（优先） | `docs/07-工具逆向/decompiled/` |
| ② | 原厂配置文件 | `C:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\<模块>\` 下的 `ToolItemInfo.xml`（`displayType`）、`*AlgorithmTab.xml`、`*Cs.xml`；`decompiled/ColorFun/colorstyle*.xaml`（色值）；`decompiled/Lang/zh-cn.xaml`（文案） |
| ③ | 原厂实机截图 | 运行原厂程序，agent-desktop 截图 |

反编译目录里与画布最相关的文件：

```
WpfControls/base/diagramitemtemplate.xaml        节点 5 套模板 + 全部状态触发器
WpfControls/VMControls.WPF.Base/DiagramItem.xaml.cs    节点容器：拖动/端口/菜单
WpfControls/VMControls.WPF.Base/ItemsPage.cs           画布 ListView：框选/落点/空白点击
WpfControls/VMControls.WPF.Base/DiagramPage.xaml.cs    框选面板、批量移动、寻路
WpfControls/VMControls.WPF.Base/DiagramArea.xaml.cs    画布外壳、缩放、锁定
WpfControls/VMControls.WPF.Base/Connector.xaml(.cs)    端口
WpfControls/VMControls.WPF.Base/Connection.xaml.cs     连线
Controls/VM.Controls.Core/UIElementAdorner.cs          Adorner 定位机制（SetOffsets/GetDesiredTransform）
base/connection/connectionstyle.xaml                   连线样式
base/rectdragpanalbasestyle.xaml                       框选虚线框样式
```

## 4 五步流程

```
1 取证    grep 到具体行号 / 读配置项 / （必要时）截原厂实机图
2 定基线  写进 docs/collaboration/acceptance/<主题>.md：证据表（文件:行号 → 行为/数值）
3 对照   逐项比对现有实现，产出"已一致 / 未实现 / 不一致"三分类清单
4 实施   一次改一项，每项都能指出对应的证据行；改完立即构建
5 回归   能自动验证的自动验证；不能的（OLE 拖拽、需手势）明确交回用户验，并说明原因
```

## 5 取证手册

### 5.1 模板 / 样式类（XAML）

```powershell
# 找某状态触发了哪些设置
Select-String -Path <xaml> -Pattern 'IsModuleFailed|IsModuleForbidden|RunState' -Context 0,14
# 提取全部 TargetName/Property/Value（可程序化生成对照表）
Select-String -Path <xaml> -Pattern 'TargetName="([^"]+)" Property="([^"]+)" Value="([^"]*)"'
# 找几何定义
Select-String -Path <xaml> -Pattern 'PathGeometry x:Key=|NormalModulePathStyle'
```

### 5.2 行为链路类（CS）

先找入口事件，再顺着调用链读，别单点看：

```powershell
Select-String -Path <cs> -Pattern 'OnMouseLeftButtonDown|PreviewMouseMove|OnDrop|IsMouseDown' -Context 0,12
```

必须确认的三件事：**谁触发、改了谁、何时提交**。

### 5.3 类型/配置类（XML）

```powershell
# 节点形状由模块声明的 displayType 决定；未声明 = DEFAULT
Get-ChildItem '<模块目录>' -Filter ToolItemInfo.xml | ForEach-Object { [xml](Get-Content $_) }
```

### 5.4 色值 / 文案

```powershell
Select-String -Path docs/07-工具逆向/decompiled/ColorFun/colorstyle1.xaml -Pattern 'Col_EditProcess_'
Select-String -Path docs/07-工具逆向/decompiled/Lang/zh-cn.xaml -Pattern 'Tab_|Lang_'
```

## 6 已验证的原厂核心机制（可直接引用，不必重查）

| 机制 | 证据 | 要点 |
|---|---|---|
| 节点模板族 | `diagramitemtemplate.xaml:1789-1848` | Style 按 `DisplayType` 切 Template（NORMAL/Square/PROCESS/GROUP/NONE） |
| 节点形状 | 同文件 `1166-1181` | 模板内按 `TargetDisplayType` 换 Path.Data；IFBRANCH 同时把高度改 40 |
| 节点状态 | 同文件 `1182-1234` | OK 绿 / NG 红 / 配置错误灰虚线 3,2 / 禁用灰渐变 / 失败米色渐变+文字0.4 / 选中橙实线 |
| 端口 | `Connector.xaml` | 20×30 命中区，5×8 橙条 #FFA719，三边描边，悬停 8.5×12 |
| 端口出现 | `diagramitemtemplate.xaml:1152-1161` | 仅节点 `IsHited && !IsLockWorkArea` 时注入 |
| 空白点击 | `ItemsPage.cs:582-592` | 只记录框选起点，**不清除选中**（重写不调 base） |
| 框选 | `ItemsPage.cs:642-651` + `DiagramPage.xaml.cs:382` | 只在松开时收集框内节点，从不改 IsSelected；框选是 CurBoxOfItems 机制 |
| 拖动预览 | `DiagramItem.xaml.cs:654-672` | 真实控件 + 0.7 VisualBrush，放进 AdornerPanel |
| 拖动位移 | `UIElementAdorner.GetDesiredTransform` | 叠加 `TranslateTransform`，`SetOffsets` 后 `AdornerLayer.Update` |
| 拖动吸附 | `DiagramItem.xaml.cs:762-788` | 3px 死区 + 5px 量化 + 只在变化时重绘 |
| 落点 | `DiagramItem.xaml.cs:727` | 直接取预览 offset，无二次取整 |
| 对齐线 | `DiagramItem.xaml.cs:855-944` | 6 组精确相等，LightGreen 1px 虚线 5,2.5，两端留 5px |
| 执行防呆 | 原厂实机截图 | 参数未配置完整 → 弹「流程:1:3 节点名 模块参数未配置完整」并中止 |
| **特征匹配系列的工具关系** | `IMVSHPFeatureMatchModuCs.xml` vs `IMVSContourMatchModuCs.xml` | **HP 参数 53 ⊂ CM 参数 59，HP 独有 = 空**；CM 多出 NMS 多模型（`MultiModelMaxOverlap`/`NMSMaxOverlap`/`NMSSortType`）、`SkewX*`、`OutLinePointEnable`。两者 `Algorithm.xml` 差 7%、`dialoguistyle.xaml` 差 5 行。**HP 是 CM 的参数子集，不存在「HP 独有功能」**；`DialogUI` 39 vs 1874 行是窗口实现风格差异（CM 逻辑在 Data + XAML 触发器）。推论：共用层 `FeatureMatchCommonControl` 虽有 2 个引用者，但功能上是一个包含另一个 |

## 7 已知陷阱（真实踩过）

| 陷阱 | 表现 | 正确做法 |
|---|---|---|
| 先改后取证 | 改完再找依据，方向全错（把预览改成"连续跟随"） | 先读源码，再定实现 |
| 用我方截图当标准 | 把"颜色抽取是胶囊"当成原厂事实 | 查 `ToolItemInfo.xml` 的 displayType |
| 看错对象 | 对照 `ExecutionPortShape` 得出"端口配色不一致"，实际渲染是 `ConnectionPort` 模板 | 先确认运行时真正用的控件/模板 |
| 状态多份不同步 | `Ghosts` 与 `GhostMap` 两套状态，一处清空另一处没清 → 预览永久消失 | 生命周期收成一个对象（Ensure/Update/Dispose） |
| 每帧改视觉树 | 每帧 Remove/Add Children → 拖动卡顿 | 只建一次，之后只改变换；Adorner 层不参与画布布局 |
| 把两层压成一层 | 节点尺寸/布局塞进投影、形状塞进模板触发器，互相牵制 | 按原厂分层：模板族定布局，模板内定形状 |
| **按代码相似度判断"重复"** | 见我方两个面板长得像，就断言"原厂也共用一个、可合并"；实际查 `.csproj` 引用者后发现原厂只给系列内两个工具共用，第三个工具一直是自有面板 | **先查原厂引用关系**（`.csproj` 的 `ProjectReference`、`using`、`MergedDictionaries` 来源），再判重复；相似 ≠ 同源 |
| **按名字顺序推断枚举值** | 以为 `EnumEntry` 第 1 个 = 值 1；实际 `BinaryImage`=1、`Single`=2、`Double`=3，按顺序推会整体错位一位 | 只读 `<EnumEntry Value="…">`，不按出现顺序推断 |
| **先说"做不到"再查能力** | 断言 WPF 侧做不到 `EventTrigger`，实际本项目早已引用 `Microsoft.Xaml.Behaviors.Wpf` 并在用；断言某工具要改算法，实际枚举早已支持 6 种 | 主张"做不到/缺失"前，**先搜本方是否已有该能力**（包引用、枚举定义、能力判定函数） |
| **小工具默认"无需取证"** | 认为像素统计/颜色抽取/图像分割这类小工具没有触发条件，从未取证；实测分别有 4/3 条 `Triggers` 与 3/3/2 处 `CanTriggerRun` | **每个工具逐个取证**，包括"看起来简单"的；没有取证记录就等于没做 |
| **`Setter.TargetName` 指向不存在的控件** | 折叠列表里列了 24 项，其中 `ScoreLimitEnable` 在原厂 XML 里没有对应节点（冗余声明）；照单补齐会造出幽灵字段 | 取证时**核对每个 `TargetName` 在原厂 XML 中是否真实存在**，不存在的记为"原厂冗余"并跳过 |
| **认为所有工具走同一套声明通道** | 以为都要迁到 manifest，实际 `ImageSource` 用嵌入 `ImageSourceParameters.xml` + `DeclaredParameterSchema`（`visibleWhen`），语义已对齐 | 先确认声明通道（manifest / XML / 表驱动），**通道不同不代表未对齐** |
| **沿用旧规模数字** | 反复引用"该文件 229 行"，实际已增长到 847 行，导致工作量与风险估算全错 | 规模、引用数**每轮实测**，不引用历史结论 |
| **把"单测全绿"当界面可用** | 显示装配类问题（缺 `*DisplayUi`、缺输入行、字段 id 前缀重复）单测全过，只有真实渲染才崩 | 涉及参数页/结果页的改动，**探针必跑**；单测不能替代渲染验证 |

## 8 交付物格式

```markdown
## 证据
| 项 | 原厂值 | 证据 |
|---|---|---|
| … | … | diagramitemtemplate.xaml:1182 |

## 对照
| 项 | 原厂 | 我方 | 判定 |
|---|---|---|---|

## 结论
已一致 / 未实现 / 不一致（并给出实施顺序）
```

## 9 完成检查

- [ ] 每条结论都有 `文件:行号`
- [ ] 文档区分"已证实 / 待取证"
- [ ] 未实现项列入清单，标注优先级
- [ ] 构建 0 错误、Release 产物已更新
- [ ] 无法自动化验证的部分，明确说明原因并交给用户验

### 9.1 取证完备性

- [ ] 该工具**逐个**取过证（含"看起来简单"的小工具），无遗漏
- [ ] 每个 `Setter.TargetName` 都在原厂 XML 里**核对过存在性**，冗余声明单独列出
- [ ] 枚举取值读的是 `<EnumEntry Value>`，不是按出现顺序推断
- [ ] 声明通道已确认（manifest / 嵌入 XML / 表驱动），未把"通道不同"当成"未对齐"
- [ ] 规模、引用数、行号为本轮**实测**，未沿用历史数字

### 9.2 判断依据

- [ ] 主张"原厂共用一个实现、应予合并"前，已列出原厂 `.csproj` / `using` 的**引用者清单**
- [ ] 主张"做不到"前，已搜索本方是否存在该能力（包引用、枚举、能力判定函数）
- [ ] 主张"要改算法"前，已确认算法侧**真的不支持**（读枚举与 `IsSupported` 一类判定）
- [ ] 未把"我方代码相似"当作"原厂同源"的证据

### 9.3 验证方式

- [ ] 涉及参数页/结果页：探针跑过，四个 batch 全 PASS
- [ ] 涉及契约/声明：相关测试套件全绿，并**说明 SDK 既存失败项**
- [ ] 涉及发布载荷：确认新增程序集**已在载荷目录**（封存是按目录整体封，不是白名单）
- [ ] 单测全绿但界面可能崩的改动，已明确标注"需人工走查"
