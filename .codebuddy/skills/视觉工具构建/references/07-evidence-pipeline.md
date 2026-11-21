# 证据驱动的工具生产流水线（状态机 + 算法等价 + 反馈闭环）

> 顶层机制来源：`docs/collaboration/acceptance/工具生产流水线与自迭代机制-20261120.md`。
> 本文件承载「视觉工具构建」技能的**生产台账与门禁细则**；SKILL.md 只保留判定与门禁摘要。
> 与 `docs/collaboration/EVIDENCE_AGREEMENT.md` 一起执行：结论分「已确认 / 推断 / 未确认」，截图只能发现差异，不能单独证明内部实现。

## 1. 顶层原则

1. 每个工具只允许从**原厂证据 + 统一平台合同**生成，不从本方旧代码或局部截图猜测补语义。
2. 本方已验收工具（见 [06-reference-impl.md](06-reference-impl.md)）只作**工程范式源**（代码骨架、装配写法），不替代原厂证据。
3. 证据不明的项登记为 `unconfirmed`，**禁止生成猜测性代码**；下一轮只处理已经定位到具体证据的差异。
4. 每轮迭代保留六项留痕：输入证据哈希、生成参数、代码变更、构建日志、运行快照、未确认项。
5. 原厂与本方唯一允许的实现差异是底层算法库；工具 UI、参数语义、端口、结果结构和生命周期必须保持原厂对应。

### 1.1 技能存在的理由：重复分析逐轮固化

本技能不是一次性代码生成器，而是一条**越跑越省事的生产线**。每做完一个工具，把本来要重新分析一遍的东西留下来，下一个工具直接取用：

| 原本每个工具都要重做的事 | 固化成什么 | 下一轮怎么用 |
|---|---|---|
| 逐个认原厂 DLL / XML / 控件 / 端口 | 证据包 + Evidence ID 清单（`docs/evidence/original-release-units/`） | 按 ID 直接定位字段和控件，不重新逆向 |
| 反复推 UI 怎么摆、参数怎么绑 | 范式文档 `references/01`–`04` | 按范式线 A/B/C 直接套，只填差异点 |
| 反复搭工程、配双角色、登记 slnx | `references/05` csproj 模板 + 冻结样板 | 复制样板，只换工具身份字段 |
| 同类坑反复踩、反复定位 | 差异点清单 + 错误对照表 + 运行期差异队列 | 新差异并入清单，同类问题不再从头查 |
| 每次重新设计验证步骤 | 静态门禁 + 台账 + Agent Desktop 探针 | 直接跑既定门禁，只核新变更 |
| 验证完的东西下次不敢信 | 冻结的证据 + Release 摘要 + 测试基线 | 作为后续工具的模板来源和回归基准 |

**快** = 直接消费上面这些存量，不重做分析；**准** = 证据门禁 + 算法等价 + 差异队列挡住重复犯错。两者缺一：只快不准会批量产出错工具，只准不快就退化成逐工具人工审批。

> 因此每次迭代收尾必须问一句：这一轮**新固化了什么可复用资产**？没有新增资产，就不算完成一次有效迭代。

## 2. 单工具生产状态机

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

| 状态 | 进入条件 | 产出 / 门禁 | 对应 SKILL Step |
|---|---|---|---|
| `EvidenceCollect` | 工具进入生产批次 | 原厂 DLL/BAML/资源、Algorithm/AlgorithmTab/Display XML、ToolItemInfo、运行快照的原始证据路径 | Step-E |
| `EvidenceNormalize` | 原始证据到齐 | 工具身份、页签、分组、控件、字段、端口、资源、生命周期的结构化清单；每项带 Evidence ID | Step-E |
| `ToolModelReview` | 结构化清单完成 | UI 方案 + 算法方案 + 差异点清单；`unconfirmed` 项单独登记 | Step0–3 |
| `SingleProjectGenerate` | 方案评审通过 | 单项目目录（Runtime/Editor 双角色共享程序集）、参数/结果声明、资源清单、编辑器工厂、测试入口 | Step3.5–4 |
| `SharedChainBind` | 工具代码生成 | 公共参数页 / 页签 / 字段控件 / ROI / 结果树 / 底栏命令 / 生命周期接缝绑定；专属控件绑 Evidence ID | Step4 / 03 |
| `StaticGate` | 组装完成 | 静态映射、共享参数链、发布单元、基本页、组装映射门禁全过 | Step6 |
| `ReleaseBuild` | 静态门禁通过 | 全量 Release 构建 0 error；载荷封存哈希记录 | Step6 / 05 |
| `AgentDesktopProbe` | Release 可启动 | 工具窗口快照（基本页/运行参数/结果显示）+ 关闭释放回执 | Step7 / 05 |
| `EvidenceDiff` | 双侧快照齐备 | 原厂 ↔ 本方逐项差异表（页签/分组/字段/顺序/状态/资源） | Step5 + §8 |
| `PatchQueue` | 差异可定位 | 按差异阶段归类的修复队列；无证据的项保留 `unconfirmed`，不生成代码 | 本文件 §8 |
| `Regression` | 补丁实施 | 相称构建 + 相关测试 + 受影响的 Agent Desktop 复核 | Step6–7 |
| `Accepted` / `Unconfirmed` | 回归完成 | 通过项冻结证据与 Release 摘要；未确认项进入台账，不冒充通过 | 本文件 §6、§9 |

> 状态可停在 `Unconfirmed` 并继续交付 UI/功能闭口，但**不得**把算法未确认的工具写成"完整同构"。

## 3. 四层职责

| 层 | 读什么 | 输出 | 落到技能哪里 |
|---|---|---|---|
| **证据层（双链路）** | A 静态：原厂 DLL、BAML/资源、Algorithm/AlgorithmTab/Display XML、ToolItemInfo（REA/反编译）；B 实机：agent-desktop 驱动原厂程序打开含该节点方案的逐页签/弹窗快照+截图 | 工具身份、页签、分组、控件顺序、字段、端口、资源、生命周期证据（Evidence ID）；UI 对齐判定 = A∪B | Step-E / SKILL §0.4 / 本文件 §4.1 |
| **生成层** | 工具清单 + 证据 | 单项目目录、参数/结果声明、资源清单、编辑器工厂、测试入口；生成器**只填充证据明确的内容** | Step0–4 / references 01–06 |
| **验证层** | 生成物 | 静态映射、共享参数链、Runtime、WPF 组装探针、Release 构建、Agent Desktop 工具窗口验证、资源释放检查 | Step6–7 / references 05 |
| **算法等价层** | 原厂黄金样例 + 本方运行输出 | `AlgorithmEquivalent` / `AlgorithmUnconfirmed` / `AlgorithmDiverged` | Step-A / 本文件 §7 |

## 4. 证据门禁清单

新工具开工前，证据包最小集必须齐备或明确登记缺口：

| 证据 | 用途 | 本地参考位置 |
|---|---|---|
| 原厂模块 DLL / 托管程序集 | 工具身份、入口、端口、行为边界 | `docs/07-工具逆向/tools/<模块>/` |
| 反编译源码 / BAML / 资源 | 控件组合、生命周期、模板、资源键 | `docs/07-工具逆向/decompiled/Managed/`、`docs/07-工具逆向/pages/` |
| `Algorithm.xml` | 参数名、类型、默认值、端口 | `docs/evidence/original-release-units/<工具>/` |
| `AlgorithmTab.xml` / `Group.xml` | 页签、分组、控件清单、可见性 | 同上 |
| Display XML | 结果显示行、顺序、显示声明 | 同上 |
| `ToolItemInfo.xml` | 目录展示名、图标、displayType | 原厂模块目录 |
| 原厂运行快照 | 真实页签/分组/状态/底栏命令 | Agent Desktop 采集，存 Release `_validation/` 或 `docs/evidence/` |

### 4.1 双链路取证规范（UI 对齐的强制门禁）

#### 取证执行顺序（静态为主，真机补充复核，禁止跳步）

> **两条链路定位（重要，别搞反）**：
> - **链路 A 静态源码 = UI 同构的主力**。当控件体系已具备（L0 门禁通过、平台已逐字迁移原厂控件），
>   原厂反编译 `DialogUI*.xaml` + `Algorithm/AlgorithmTab.xml` + `zh-cn.xaml` + `DialogData.cs` 已经把控件树、
>   分组顺序、字段绑定、文案、命令写清楚——照静态同构即可**快速、高保证**地对齐 UI，不必每个工具都全量开真机。
> - **链路 B 真机 = 补充与复核**，不是每次的前置负担。只在静态定不了（只有 LinkName 引用、项数/标签/展开态缺失）
>   或需要复核映射陷阱（`Box Polygon` 静态 2 kind 却渲染 3 按钮）时才开原厂窗口。
> - **反模式**：把双链路做成"每次都全量真机截图"→ 开发越来越慢，还因自绘控件坐标定位浪费时间。
>   技能的目标是让开发**越来越快越来越准**：静态能定的不重复真机，真机只花在刀刃上。

| 步 | 动作 | 证据产出 | 何时可省 / 禁止 |
|---|---|---|---|
| 1 | **先查该工具自己的静态声明**：`AlgorithmTab.xml`（`ROISelection` 形状集、`GroupLinkItem` 分组与控件引用）+ `Modu.xml`（端口类型）+ 反编译 `DialogUI*.xaml`（控件树/顺序/绑定）+ `zh-cn.xaml`（文案）+ `DialogData.cs`（生命周期） | 每项 `文件:行号` | 静态能定的项（形状集、字段顺序、文案、命令）**到此即可落地，不必真机**；禁止用平台默认值/别的工具值代替本工具静态 |
| 2 | **判断静态充分性**：控件树/顺序/标签/绑定齐全 → 静态直接同构；只有 LinkName 引用、定不了项数/标签/展开态（如 InheritWayGroup/AssistGroup）→ 标记"需真机补" | 静态结论 或"需真机补"标记 | 禁止把"静态只有引用"当"没有此项"；也禁止明明静态齐全还去全量真机 |
| 3 | **真机只补静态定不了的项**：打开原厂含该节点方案，针对第 2 步标记的项取截图（如继承方式各选项、位置修正各状态、形状按钮数）；递归层级与操作见下方「窗口层级取证」 | 仅缺项的状态对 snapshot+PNG | 只对缺项取证，不全量重截；禁止预设项数、禁止只取默认态 |
| 4 | **针对性交叉验证**：只对第 3 步取的项做静态↔实机比对；发现映射陷阱（静态 2 kind vs 实机 3 按钮）时查清原因，不要改静态结论 | 交叉验证结论 | 静态充分项不必复核；禁止单凭任一条链路判争议项"已对齐" |
| 5 | **逐工具固化**：把各项数/标签/行顺序/形状集写进实现参数（`availableShapes`、`InheritedModeLabels`、`InsertParameterSource` 等），每条绑 Evidence ID | Evidence ID + 实现参数 | 禁止把某工具结论套到别的工具 |

> **静态多源冲突的处理（本次教训）**：同一个值可能在多个原厂静态源里不一致——如默认值在
> `AlgorithmTab.xml` 的 `CurValue`（AngleStart=-180）与反编译 `parameters.json` 的 `DefaultValue`（-45）不同。
> **默认值以专门记录默认值的源（`parameters.json`）为准**；`AlgorithmTab.xml` 的 `CurValue` 是配置界面初值/另一上下文，不覆盖默认。
> 遇冲突先查两个源各自语义再决定，**不要看到一个静态值就改**——改前先看有没有测试/权威源约束（本次测试断言 -45 救了一次误改）。
> 数值精度类差异（默认值、范围边界的小数）按项目口径不阻塞 UI 同构，不值得为它改测试或反复真机。

**判定口诀**：**静态先行、充分即落地；定不了才真机、只补缺项；逐工具固化、Evidence ID 绑定**。平台默认值和其它工具范式只用于加速实现，永远不是原厂事实。

两条链路的**分工**（A 为主、B 为辅）：

| 链路 | 工具 | 定什么 | 证据形式 | 定位 |
|---|---|---|---|---|
| **A 静态证据（主力）** | REA MCP（反编译/托管/原生）+ 原厂配置/资源文件 | 语义与结构：字段名、类型、默认值、控件组合、页签分组、命令、资源键、生命周期、**控件顺序、标签、文案** | `文件:行号` / 配置键 / XAML 节点 | 控件体系具备时，照静态即可**高保证快速同构**；绝大多数 UI 项由它定 |
| **B 实机证据（补充复核）** | agent-desktop 驱动已安装原厂程序 | 静态定不了的项：只有 LinkName 引用时的项数/标签/展开态、映射陷阱（kind 数 vs 按钮数）、真实可见性与状态 | `窗口快照编号` + 截图 PNG | **按需取证**，不是每次全量前置；发现静态↔实现偏差时复核 |

**历史返工根因（两个极端都要避免）**：
- 只看静态的"能打开、字段齐全"就宣判对齐 → 漏掉结构性错位（训练参数放错 Tab、漏工具条/底栏）——这类在控件体系迁移完整后，静态 XAML 的控件树/顺序其实也能看出来，关键是**读全静态**而非"能打开就算"。
- 把双链路做成每次全量真机 → 开发变慢、自绘控件坐标定位浪费时间——正确做法是静态充分即落地，真机只补缺项。

#### 链路 A 取证要点
- 反编译源码与配置 XML 一起读：`Algorithm/AlgorithmTab` 定页签/控件清单/默认值，`Display` 定结果显示行，`DialogData.cs` 定生命周期与字段，`DialogUI*.xaml` 定弹窗控件组合与绑定，`zh-cn.xaml` 定中文文案。
- 每个控件/字段记录 `文件:行号`；配置里 `CustomVisible`/`Trigger` 决定真实可见性，不能只凭控件存在就判定显示。

#### 链路 B 取证要点（agent-desktop）
- 前置：原厂程序已安装且可用；准备/复用一个**含该工具节点的原厂方案**（用户常已备好，先问再找，不重复造）。
- 命令链（CLI 路径以本机实际为准，见项目 agents 记忆）：
  ```powershell
  & $agent list-apps
  & $agent list-windows --app VisionMaster.exe
  & $agent mouse-click --xy "x,y" --count 2 --headed        # 双击节点开参数窗
  & $agent snapshot --window-id <id> --include-bounds        # 控件树+坐标
  & $agent screenshot --window-id <id> <out.png>             # 视觉截图
  ```
- 每个页签、每个弹窗（模板配置/创建窗口等）**分别**抓 `snapshot` + `screenshot`，存 `docs/evidence/<工具>-ui-<日期>/`。
- 弹窗内的工具条按钮、状态栏、分组标题、底栏命令以**截图为准**核对（这些常在共享样式/XAML 里，静态 XML 不一定列全）。
- 产物：每状态一对 `.json`（快照）+ `.png`（截图），命名体现工具-页签/弹窗-日期。

#### 窗口层级取证（强制，不可只取表面）

> **历史返工根因（本次）：只取了 tab 层截图 `param-window.png`，没展开页签内的功能控件，
> 也没把页签与弹窗关联取证——漏掉基本参数页 ROI 区域的绘制/继承/形状/掩膜整组控件，
> 误判了"继承"语义（把平台形状参数继承当成原厂 Region 连线继承），导致反复返工。**

取证必须覆盖以下**四层窗口层级**，缺哪层就登记哪层 `unconfirmed`，不得判定该层 UI 对齐：

| 层级 | 取证对象 | 必须记录的证据 | 漏取的后果 |
|------|---------|--------------|-----------|
| L1 页签层 | 参数窗口的页签集合与顺序 | 每页签 `snapshot`+`screenshot`，页签标题/数量/顺序 | 页签错位（本次已对齐） |
| **L2 页签内层** | **每个页签内的分组与功能控件** | **展开每组：分组标题、每组内控件清单与顺序、单选/开关/按钮文案、形状按钮个数** | **漏整组控件（本次基本页 ROI 区域）** |
| L3 功能弹窗层 | 页签按钮打开的内部窗口（模板配置/创建/编辑/导入等） | 每个弹窗单独 `snapshot`+`screenshot`：尺寸、工具条、列表、参数行、底栏命令 | 漏模板窗口全套控件 |
| **L4 弹窗内层** | **弹窗内的子交互层**（下拉展开、弹层滑杆、右键菜单、Tab 内再分组） | **触发后再抓 `snapshot`+`screenshot`（如点开下拉、点开尺寸气泡、展开高级参数）** | **漏子交互（如画笔尺寸 PxSlider、正负样本下拉）** |
| **L5+ 递归层** | **弹窗里再开的子弹窗（编辑/设置/选择…），以及子弹窗内的子交互** | **每开一层新窗口就重复 L3/L4：列窗口清单 → 逐个 `snapshot`+`screenshot` → 展开其内部交互**；直到再无可打开的新窗口 | **漏深层功能（如模板窗口里点"编辑样本"再开的窗口）** |

**递归终止条件**：当前窗口内所有可点击元素（按钮/链接/列表项/右键菜单）都不能再打开新窗口或新弹层，才停止取证。**禁止**预设"只有四层"或"这个窗口不会有子窗口"。

**页签↔弹窗关联取证（强制）**：
- 每个"打开弹窗"的按钮（创建/编辑/设置…），必须记录：它在哪个页签、按钮文案、打开的是哪个弹窗、弹窗关闭后回到哪个页签。
- 页签内的功能控件与弹窗内同类控件要**对照取证**（如基本页 ROI 与模板窗口 ROI 是否同一套语义/控件），禁止孤立看一处就下结论。
- 证据命名体现层级：`tools-ui-<日期>/<工具>-tab-basic.png`（L1/L2）、`.../<工具>-template-dialog.png`（L3）、`.../<工具>-dialog-<子交互>.png`（L4），Evidence ID 标层级。

**交叉验证时按层比对**：L1 差异看页签集合；L2 差异看每组控件清单（本次漏的就是这层）；L3 看弹窗骨架；L4 看子交互。禁止用 L1 截图代替 L2 结论。
1. 静态清单给出的控件，实机快照里应能找到；实机有而静态没列的，以实机为准登记补充。
2. **逐项比对维度**：页签集合与顺序、每页签内分组标题与控件顺序、按钮文案与图标、状态栏项、底栏命令、弹窗尺寸。产出"已一致 / 未实现 / 不一致"三分类差异表。
3. 缺链路 B 证据时，UI 对齐结论只能是 `unconfirmed`，**不得**写"已对齐"；可以交付"能打开/不报错"的功能闭口，但必须如实标注 UI 未核。
4. 既有本方工具复核同样适用：先走 B 拿原厂实机快照，再逐页签比对本方，禁止只凭本方截图或静态代码判定对齐。

### Evidence ID 绑定规则

- 每个**专属控件、字段、分组、页签、端口、资源**必须有唯一 Evidence ID，指向具体文件 + 行号 / 配置键 / 快照编号。
- 生成器只填充有 Evidence ID 的内容；**无 ID 的 UI 不得生成**。
- 普通字段进入公共参数集合链（不重复声明）；专属 UI 才允许工具侧接缝。
- 原有证据归档路径优先复用，缺口经取证统筹集中补齐，开发线不各自重复逆向。

### 未确认登记格式

| 项 | 类型 | 说明 |
|---|---|---|
| `item` | string | 具体未确认项（如"原厂直方图回调刷新时序"） |
| `evidenceChecked` | string[] | 已查过的证据路径，证明不是"没找" |
| `impact` | string | 影响哪个完整功能链 / 哪个决定 |
| `minProbe` | string | 解除该未确认所需的最小补证办法 |
| `status` | enum | `unconfirmed` / `inferred` / `confirmed`（`inferred` 须写明推导依据） |

### 4.2 控件体系就绪门禁（取证完成后、工具落地前，强制）

> **顺序铁律：取证 → 评估控件体系 → 不足先补控件体系 → 才做工具落地。**
> 历史返工（本次）：取证到原厂基本页 ROI 区域有绘制/继承/形状/掩膜控件，但没先盘控件体系，
> 直接内联 composer → 缺 Converter、缺数值输入框、缺样式键 → StaticResource 异常连环返工。
> 正确做法：先把缺失的 Converter/文本框/样式键逐字迁移补齐，再接 ROI 组合，一次通过。

#### 步骤 1 — 按取证清单列控件依赖
把 §4.1 取到的每个 UI 元素映射为控件体系依赖，逐项列出：
- 控件类型（ImageViewBox / SliderArrange / 数值输入框 / 遮罩 / 工具条按钮 / 样本列表行 …）
- 样式键（StaticResource / DynamicResource 的 x:Key）
- Converter（IValueConverter 及 x:Key）
- 图标资源（路径）
- 附加属性 / 接口（如可见性、IROIVisible）

#### 步骤 2 — 逐项判本方状态（四类）
| 状态 | 含义 | 处理 |
|---|---|---|
| **已激活** | 类/键/资源在 `src/Plugin.Wpf` 已存在且非 `.pending` | 直接复用，记 Evidence ID |
| **.pending 未激活** | 有反编译副本但未纳入编译 | 评估改名/解依赖后激活；**不整文件激活大字典**（避免无关键冲击其它工具），优先逐字提取所需块 |
| **等价件** | 无同名但有功能等价控件 | 登记差异（外观/交互），确认可替代才用；否则升级为需新建 |
| **缺失** | 本方无同名、无 `.pending`、无等价 | 查反编译源是否在 `docs/`；有则**逐字迁移**，无则记为新建成本 |

> **已验证资产冻结（避免反复返工，硬性）**：以下能力在本方已成熟验证，**对标时只核对、不重构、不为纯外观改动其结构**：
> - **位置修正的数据契约（Fixture / FIXTURE 端口）**：已验证可用且与原厂语义一致。禁止为"组标题重复/英文键名"这类外观问题去新建 section、改本地化键、挪锚点——历史返工：为此改了 ContourMatch 位置修正分组，引入 XML 语法错、dll 锁定、多轮返工，最后全部回退。**若外观有差异，登记差异即可，不动数据契约。**
> - 其它已冻结资产（后续发现持续追加）：在此登记"能力 → 冻结原因 → 禁止的改动类型"。
>
> 判断原则：**语义已对齐的能力，外观差异不值得动它**；把时间花在真正未实现/未对齐的能力上。

> **数据契约 vs UI 控制分离（位置修正取证结论，防止照搬错误范式）**：
> 原厂 `IMVSContourMatchModu.xml:52-72` 证明位置修正是**一个上游 FIXTURE 输入端口**（`Position Correction Info`，Style=FIXTURE），
> 内部含 Fixtured Point / Unfixtured Point 及 angle/scale 分量；与 `InputROI`（:39）同属模块输入契约。
> 实机展开（`docs/evidence/vm-contourmatch-ui-20261121/vm-poscorr-expanded.png`）的"选择方式[按信息/按点/按坐标]+修正信息引用行"
> 是**本模块 UI 控制**，决定如何解析该 FIXTURE 端口。
> - **不要照搬 PixelCount 的 `input.pose.*`（Mode/Information/BasePoint/RunPoint）范式**去假设位置修正是本模块字段体系——
>   那是另一工具的实现，原厂 ContourMatch 的位置修正数据来自上游端口，不是本模块 pose 字段。历史返工：照搬该范式建了 ContourMatchAssist，随即撤销。
> - 正确范式（已实机验证）：**数据契约 = 上游 FIXTURE 端口（本方 FixtureInput 对应，冻结不动）+ 本模块 UI 控制字段（选择方式；修正信息行直接绑 FixtureInput 端口）**。
>   位置修正 AssistGroup 在原厂是 **ROI 区域内末尾的一部分**（`AlgorithmTab.xml:37-39` AssistGroup 在 Tab_ROI Area 内），
>   实现时把 ROI 组合控件与 Assist 控件放进**同一个容器一次插入**，只生成一个"ROI区域"组头，顺序 ROI 在前、Assist 在后。
> - 通用教训：**先取证数据契约在"端口层"还是"字段层"，再决定 UI 范式**；不要从别的工具的 UI 范式反推数据契约。
>   **范式是加速复用的工具，原厂才是唯一标准**——范式本身必须先用原厂证据校验，不能把某工具的范式当通用标准照搬。

#### 位置修正统一范式（所有用到位置修正的工具一致，四状态实机取证）

证据：`docs/evidence/vm-contourmatch-ui-20261121/` 四张图（关闭/按信息/按点/按坐标）+ `IMVSContourMatchModu.xml:52-72`。
**位置修正是跨工具统一能力，实现范式必须一致**（轮廓匹配/灰度匹配/PixelCount/Blob 等所有含此能力的工具）：

| 状态 | UI 显示 |
|---|---|
| 开关关闭 | 仅"位置修正" ToggleButton（灰） |
| 开 + 按信息 | 选择方式（三选一）+ **修正信息**（FIXTURE 引用行，1 行） |
| 开 + 按点 | 选择方式 + **基准点(POINT 引用)/基准角度/基准尺度X/基准尺度Y/运行点(POINT 引用)/运行角度/运行尺度X/运行尺度Y**（8 行） |
| 开 + 按坐标 | 选择方式 + **基准点X/基准点Y/基准角度/基准尺度X/基准尺度Y/运行点X/运行点Y/运行角度/运行尺度X/运行尺度Y**（10 行，均引用行） |

实现要点（平台已验证，`NativeAssistUiDraftControls` + `ComposeInputEditor(referenceOnly:true)`）：
1. **开关**是独立 Boolean 字段，关闭时整个 Assist 组（选择方式+输入行）折叠不显示。
2. **选择方式** Integer radio 三选一（0 按信息 / 1 按点 / 2 按坐标）。
3. **修正信息/基准点/...** 全部是 referenceOnly 引用行，绑到上游端口或其分解字段；数据源来自**一个 FIXTURE 输入端口**（内含 Fixtured/Unfixtured Point + angle/scale-x/scale-y），不是工具本地字段。
4. `NativeAssistUiDraftControls(modeRow, informationRow, sources)` 的 sources 按 mode 分组：mode=1 给按点的 8 行、mode=2 给按坐标的 10 行；按信息时只显示 informationRow。
5. AssistGroup 位于 **ROI 区域内末尾**（`AlgorithmTab.xml:37-39`），与 ROI 控件放同一容器一次插入，只生成一个"ROI区域"组头。
6. 执行链：按信息由 `FixtureDeclarations.ToAffine2D(referencePose, runPose)` 完成；按点/按坐标由点位/数值构造等价 pose 后走同一 ToAffine2D（若平台未实现则登记未确认，不冒充）。

#### ROI 继承方式：逐工具取证，不可用统一项数（取证逻辑范例）

> **历史教训**：先看到平台默认 radio 是四项（按矩形区域/按矩形参数/按圆形区域/按圆形参数），
> 就把 ContourMatch 也配成四项——但原厂实机证明轮廓匹配只有**两项**（按区域/按参数），Blob 才是四项。
> 根因是拿平台默认值/别的工具范式当原厂事实。**不同工具的继承方式项数、标签、参数行都不同，必须逐工具取证。**

**取证逻辑（每个含 ROI 继承的工具都按此做）**：
1. **实机取三态截图**：ROI创建=绘制；ROI创建=继承 + 继承方式各选项逐一点开。记录继承方式** radio 有几项、每项文字、选中后显示哪些输入行及行顺序**。
   - 例（均已实机取证，见 `docs/evidence/vm-contourmatch-ui-20261121/` 与 Blob 五图）：
     - **轮廓匹配**：2 项（按区域/按参数）；按区域=1 行「区域」；按参数=5 行「ROI中心点X/Y/宽度/高度/角度」。
     - **Blob 分析**：4 项（按矩形区域/按矩形参数/按圆形区域/按圆形参数）；按矩形参数=5 行，按圆形参数=6 行「圆中心X/Y/内径/外径/起始角度/角度范围」。
2. **按取证项数配 composer**：`InheritedModeLabels` 决定 radio 显示几项与文字（用它将平台默认四项收敛为工具实际项数）；`InheritedModes` 项数必须与 labels 一致，且顺序对应 radio index。
3. **参数行文案与字段 ID**：`RectangleParameterRows`/`AnnulusParameterRows` 的 label 逐字用实机截图文字；字段 ID 用 `RoiInheritanceMaterializer.LeafId(family, member)`，与 `RoiInheritanceSpec` 的 `ParametersModeValue` 推导前缀一致。
4. **无圆形继承的工具**：`AnnulusSourceFieldId` 保留已声明字段（避免 Synthetic 无呈现报错），用 `ShowAnnulusSource=false` 隐藏；`AnnulusParameterRows=[]`。
5. **平台约束**：`NativeRoiInheritanceBinding.AcceptMode(index)` 直接索引 `InheritedModes[index]`——labels 与 modes 项数不一致会导致点击额外 radio 时 IndexOutOfRange、选中失败且无提示。

**禁止**：拿平台默认四项当原厂标准；把某个工具（如 Blob）的项数套到别的工具。每一项都要有该工具自己的实机快照 Evidence ID。

#### 形状按钮集：同样逐工具取证（不可照搬平台默认）

平台 `NativeRoiEditHost` 默认形状集是 `Box/SectorRing/Sector/Polygon`（4 kind，渲染成 4~5 个按钮）。**原厂每个工具的形状按钮集不同**：
- 证据：原厂 `IMVSContourMatchModuAlgorithmTab.xml:29` `<ROISelection>Box Polygon</ROISelection>`；实机截图（轮廓匹配 ROI创建=绘制）显示 3 个按钮（两个矩形 + 六边形）。
- 实现：按该工具原厂 XML 的 `ROISelection`，通过 `NativeRoiEditHost(..., availableShapes: ["Box","Polygon"])` 收敛（`NativeRoiEditor.SetAvailableShapes`，仅作用于形状主组，屏蔽区仍 PolygonOnly）。轮廓匹配已按此修正（截图 `docs/evidence/contourmatch-ui-ours-20261121/shapes-final.png` 与原厂一致）。
- **取证逻辑**：每个含 ROI 绘制的工具都要取「ROI创建=绘制」态截图，数清形状按钮个数与图标，并查该工具 `AlgorithmTab.xml` 的 `ROISelection` 声明交叉验证；按此传 `availableShapes`，不传则继承平台默认（多为错误）。

**共性结论**：ROI 区域的每一项——**继承方式项数/标签、形状按钮集、位置修正模式、参数行顺序**——都是逐工具不同的，必须逐工具实机取证 + XML 交叉验证；平台默认值和其它工具范式只能加速实现，不能当原厂事实。

#### 参数行顺序（框架缺陷已修，勿回退）

原厂 ROI 区域顺序：输出掩膜 → 区域类型 → ROI创建 → 继承方式 → **参数行（按参数模式显示）** → 屏蔽区创建 → 屏蔽区 → 位置修正。
- 平台 `NativeRoiUiDraftControls.EnsureParameterEditors` 原实现用 `AddSource(Children,row)`（= `Children.Add`）把参数行追加到控件**末尾**，导致参数行落到屏蔽区/位置修正之后——与原厂顺序错位（表现：切"按参数"后 ROI中心点X/Y/... 显示在屏蔽区下方）。
- 已修为 `InsertParameterSource`：把参数行 `Children.Insert` 到 `_maskRadio` 之前（紧跟继承方式）。这是**框架层缺陷修复，服务所有工具**，勿在工具层绕过、勿回退。
- 取证时把"参数行出现位置"也纳入逐工具截图核对（属于窗口层级 L2 的控件顺序维度）。

#### 步骤 3 — 缺口分档与补齐顺序
> **"对齐"的判定维度（禁止"看到有面板就说对齐"）**：
> 历史返工——声称"特征模板 tab 已对齐无需改动"，实际原厂创建/载入是顶部工具条、本方载入被遮、还多了不该有的"搜索区域"按钮。
> 判断一个 tab/page 是否对齐，必须**逐项列出并核对**：① 按钮/控件**清单**（有没有多余/缺失）；② 每个控件的**位置**（顶部工具条/左侧/右侧）；
> ③ **分栏与布局**（左右分栏、跨行覆盖）；④ 分组标题与文案；⑤ 控件顺序。缺任一维度不得判"已对齐"。
> 已有实机截图（用户给的或之前取的）必须拿出来逐项对照——**"静态为主"不等于"不用看已取到的实机证据"**。

> **展开态必须滚到底（验证硬要求）**：核对高级参数/展开区时，截图必须滚动到滚动条最底且连续两次截图不再变化才算看完；
> 只截展开瞬间或滚几步就下结论会漏字段（历史返工：用户多次指出"你没滚到底"，漏看结果页 Box点Y判断）。
> **可视区外的字段用 accessibility snapshot 确认是否渲染**（比截图可靠——截图受窗口高度/底栏遮挡影响）。
> "颜色/图标"等视觉细节看截图，"字段有没有/顺序对不对"看 snapshot bounds。

> **新增功能不得破坏原厂既有结构**：为某能力补 UI 入口时（如本地绘制搜索 ROI），入口必须放回该能力**所属的 tab/group**
> （搜索 ROI 属基本参数页 ROI 区域），禁止因为"那里好放"就塞进别的 tab（如特征模板页）改变原厂布局。
> 若某 tab 原厂没有此入口，说明该入口本来就不该在这里——回查该能力的正确归属，不要在错误位置新增按钮。

> **视觉差异先做三层归类，再动手（防止盲目试错）**：看到 UI 与原厂不一致时，按序判定归属——
> ① **框架层**：框架有没有提供该能力（如 HeaderlessSectionAnchors/Grid/FieldCondition）？有→不是框架问题。
> ② **控件体系**：原厂用的控件/图标/样式是否已迁移到 `src/Plugin.Wpf`？（用反编译 xaml 查原厂控件类型，如 SuperImageButton）已迁移→不是控件问题。
> ③ **装配**：前两层都具备，差异来自我用了手写近似控件、或布局参数（Width/Height/Margin/列宽）凭目测拍脑袋——**这是装配问题**。
> 历史返工：特征模板页创建/载入按钮本应用平台 `SuperImageButton` 三态，我手写 Button + 猜 96px 宽 → 反复对不齐；
> 查原厂 `featurematchconfigpanal.xaml:39-94` + 确认控件已迁移后，按原厂控件与参数重装即对齐。
> **铁律**：框架有、控件有，就禁止手写近似控件/猜参数——回去查原厂 xaml 逐字照控件与布局参数装。

> **发现自研壳必须重写，禁止打补丁（2026-11-21 教训）**：若某窗口/面板当初是"照控件清单手写"的近似壳
> （如 ContourMatchTemplateConfigWindow 用 StackPanel 拼原厂 ControlTemplate），后续发现它 UI/功能对不齐时，
> **不要继续给它补资源合并/样式顺序/缺按钮**——那是给错误结构续命，越补越乱（本次连续 4 次 StaticResource 异常）。
> 正确做法：① 回到 Step-C 控件体系门禁，逐项确认原厂控件/样式/图标已迁移；
> ② **按原厂 xaml 逐行重写**（品牌中性化 + 命名空间/图标路径替换），命令/数据接到本方契约；
> ③ 重写后才做实机对照。判定标准：本方窗口的控件树与原厂 xaml 一一对应，不是"看起来像"。

> **区分 UI 差异与算法层约束（先判归属再动手）**：字段显示差异不一定是 UI bug，先查 `Settings.Validate` 是否对该值有
> "非默认值即拒绝"约束（如 ContourMatch 极性/尺度范围类型/阈值类型，HALCON 模板创建期固定，运行期只支持单一值）。
> - **算法层约束**：字段显示数字而非原厂枚举文案（极性=1、阈值类型=0）——若算法只支持单一值，**不要 UI 枚举化成可选**（会误导用户、运行即报错）；登记为算法未确认项。
> - **UI 设计层**：首屏/高级归类错误、标签文案、控件顺序、多余/缺失控件——可直接修。
> 运行参数 tab 的对照教训：归类与文案是 UI 层（已修），枚举显示成数字是算法层（不改）。

| 档 | 类型 | 做法 |
|---|---|---|
| 低 | Converter、样式键、图标、简单控件（<50 行、自包含） | 逐字迁移反编译源，注册到中立命名字典（如 TemplateWindowButtonStyles.xaml），建中立键 |
| 中 | 复杂控件/等价适配（如 PxSlider→SliderArrange、遮罩→窗口层 Grid） | 写适配差异表，接线时验证 |
| 高 | 全新交互控件/算法 | 单独批次，先在方案里标成本，不冒充同构 |

#### 步骤 4 — 命名规范（强制）
- 本软件**只能中性命名**：类型名/样式键/字段名不得带品牌（Hik/Hikvision/VM 等）或尝试性词（temp/placeholder/XXX）。
- 反编译源路径可留在注释作取证依据，但**对外命名必须中性**（如原厂 HikTextBox → TemplateTextBox）。

#### 步骤 5 — 门禁判定
- 所有依赖 = 已激活 或 等价件已确认 → **就绪**，进入 Step0。
- 仍有低/中缺口 → **先补齐并构建验证（0 error）** 才进入 Step0；缺口的 Evidence ID 一并登记。
- 有高缺口 → 在方案标成本/未确认，不得用自研近似冒充同构。
- **禁止**"边做工具边补控件"——那是本次连环返工的根源。

## 5. 单工具输入清单（模板）

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

> 工具目录命名不统一（既有 `SunEyeVision.PixelCount`，也有 `SunEyeVision.Tool.BlobAnalysis`）。
> **禁止凭目录名猜测工程路径**，以 `product/ToolComponents.props` 与生产台账登记为准。

## 6. 生产台账：`docs/evidence/tool-batch-manifest-20261120.json`

台账是本流水线每工具的现行载体，新增工具必须追加一行：

| 字段 | 含义 | 填写要求 |
|---|---|---|
| `batch` | 推广批次 | 样板通过后递增 |
| `toolId` | 工具身份 | 与原厂模块名可追溯映射 |
| `runtimeProject` / `wpfProject` | 工程路径 | 单项目迁移后**两字段指向同一 csproj**（双角色共享程序集） |
| `evidence` | 证据路径 | 数组可列多项；必须可定位到文件 |
| `pages` | 页签清单 | 与原厂一致，顺序敏感 |
| `status` | 当前状态 | 用具体状态词，禁止笼统写"完成" |
| `unconfirmed` | 未确认项 | 逐项列，不得清空以显得通过 |
| `architecture` | `{singleProject, runtimeEditorSharedAssembly}` | 单项目结构标记 |
| `algorithm` | 算法等价块 | `adapter` / `originalModule` / `evidence` / `goldenCases` / `equivalenceStatus` / `comparisonRequired` / 可选 `externalLibrary` |

**当前台账事实**：所有已登记工具的 `algorithm.equivalenceStatus` 均为 `unconfirmed`，`goldenCases` 为空或缺失。没有任何工具达到完整同构，不能因 UI/功能通过而改写该字段。

## 7. 算法等价门禁

```text
OriginalEvidence → GoldenCase → OriginalRunCapture
                 → AdapterRunCapture → Normalize
                 → CompareWithTolerance
                 → AlgorithmEquivalent / AlgorithmUnconfirmed / AlgorithmDiverged
```

HALCON 或 OpenCvSharp 调用成功只证明本方算法能运行，**不证明与原厂算法逻辑一致**。

### GoldenCase 必须记录

| 字段 | 说明 |
|---|---|
| 原厂 DLL 哈希 | 版本与身份锁定 |
| 参数快照 | 本次运行的完整参数值 |
| 输入图像哈希 | 同图像可复现 |
| ROI 坐标系 | 避免坐标口径错位 |
| 输出字段映射 | 原厂字段 ↔ 本方字段 |
| 允许误差 | 各数值量的容差 |
| 采集方式 | 原厂运行、快照编号、操作人/脚本 |

### 归一化比较维度

状态、数量、数组顺序、区域覆盖、几何量、浮点误差、错误码、空输入行为。

### 状态判定

- 缺任一原厂基准 → `AlgorithmUnconfirmed`；工具可交付 UI/功能闭口，但**不得进入完整同构**。
- 有基准且全部维度在容差内 → `AlgorithmEquivalent`。
- 有基准但出现差异 → `AlgorithmDiverged`，差异进 `PatchQueue`，按 §8 归类。

> **逻辑对照优先（用户口径，2026-11-21）**：真实效果和精度由专门测试人员负责，开发期的算法对齐以**静态源码逻辑对照**为准——
> 按原厂 `AlgorithmTab.xml` 的 AlgorithmIndex + 参数语义，逐项确认本方有对应算法动作；
> AlgorithmIndex 相同即语义对应，能用 Halcon API 实现就实现，**不因缺 GoldenCase 而拒绝逻辑迁移**。
> 实机 GoldenCase 仅用于复核争议项与精度验收，不是开发期迁移的前置门禁。
> 区分"可实现"（如极性→metric、尺度→CreateScaledShapeModel、超时→CTS）与"能力模型限制"（如 Halcon 仅等比尺度、无横切语义），
> 后者如实登记 `AlgorithmUnconfirmed`，禁止伪造执行语义。

## 8. 反馈闭环与差异队列

### 8.1 失败归类（六阶段）

| 阶段 | 典型症状 | 修复入口 |
|---|---|---|
| 入口 | 工具不被加载、双角色注册失败 | 工程声明、`ToolComponents.props`、组件封存 |
| 声明 | 字段/端口/类型不匹配 | 参数/结果声明、`SdkTypeVocabulary`、证据映射 |
| 模板 | 页签顺序、分组、控件缺失 | 原厂 XML / 模板资源映射 |
| 组装 | 专属控件未上共享层、绑定失效 | 工具 Editor 接缝（**禁止先改公共框架**） |
| 执行 | 算法结果、状态、时序不符 | 先回原厂取证；缺证据则保留未确认 |
| 释放 | 重开残留、事件重复订阅、资源未回收 | 生命周期 `Cleanup` 闭链 |

### 8.2 修复优先级

1. 静态差异优先修工具声明和资源映射。
2. 组装差异优先修工具 Editor 接缝，禁止先改公共框架。
3. 运行差异先回原厂取证；缺证据就保留未确认状态。
4. 每次修复只允许影响当前工具和共享合同明确覆盖的范围。
5. 同一失败连续出现 → 自动提升为差异条目，**阻止工具进入下一批**。
6. 工具通过后冻结证据与 Release 摘要，作为后续工具的模板来源。

## 9. 样板出口与推广门禁

PixelCount 样板出口（按顶层机制）：

- 单项目工程入口和双角色注册。
- 原厂三页、ImageSource、ROI、掩膜、AssistGroup 与结果显示映射。
- `PixelModuRangeConfigList` 专用动态面板接缝。
- 普通字段进入已验证的公共参数集合链。
- Runtime 声明、结果投影、保存回读、关闭释放和 Release 构建。
- 原厂静态 UI 证据与自研 UI 快照差异清单。
- **独立出口**：算法输出等价基准。当前只有 HALCON 执行测试，缺同版本原厂输出基准 → `AlgorithmUnconfirmed`。

推广规则：样板全部通过后才复制其项目生成规则和验证规则到 FrameMean、IntensityMeasure，再进入后续批次。

> **机制与现状偏差（如实记录）**：台账 batch 1 已含 FrameMean/IntensityMeasure，但 PixelCount 算法等价出口仍未确认。
> 该偏差需由主线确认是"追认带未确认项推广"还是"补黄金样例后重新冻结"，不得在技能中把这种状态写成样板已全通过。

## 10. 验证命令与陷阱

```powershell
# 全量 Release（涉及工具插件或发布规则必须做）
dotnet build SunEyeVision.slnx -c Release --no-restore

# 部署验证：dev/ 被 .gitignore 屏蔽，必须用 PowerShell（rule-017）
Get-ChildItem -Path "d:\MyWork\SunEyeVision_V2\dev\Debug\plugins" -Directory |
    Where-Object Name -like "*Xxx*" | Select-Object -ExpandProperty FullName
```

> 插件目录名以程序集 `AssemblyName`（单项目通常带 `.Runtime`）为准，不凭工具目录名猜测。

- 构建前关闭运行中的 `SunEyeVision.UI.exe`（锁 DLL 会构建失败）。
- 运行验证从 `dev\Debug\SunEyeVision.UI.exe` 启动，不用 `dotnet run`。
- Agent Desktop 工具窗口验证必须覆盖打开 → 运行 → 关闭释放，并保存快照。
- 跨卷检查受限：需要另一卷的检查仍用正式 Release，另一卷只放限量一次性测试数据，验证后清理。
