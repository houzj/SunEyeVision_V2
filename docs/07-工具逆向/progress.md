# 工具逆向证据库 建设进度

更新：2026-11-07

## 一、当前阶段

第 1–7 项完成，硬失败清零。`docs/08-工具预览/` 的 162 个调试窗已从三栏外壳全部升级为控件级还原，索引与对照页均加搜索，零外壳页、零断链。

## 二、已完成

### 第 1 项 骨架与模板契约

| 项 | 内容 | 状态 |
|---|---|---|
| 前置校验 | `git check-ignore` 确认 `docs/07-工具逆向/` 与 `decompiled/` 未被屏蔽；`07` 前缀空闲 | 通过 |
| 库根 README | 来源、继承边界、状态枚举、七件套、9 步链、7 环节、7 维对照、使用路径、版权边界 | 完成 |
| 卡片契约 | `contracts/card.schema.json`（已迭代到 v1.2） | 完成 |
| 页面契约 | `contracts/page.sections.json`：8 个固定章节、7 环节、7 维、9 步、分类扩展、硬失败清单 | 完成 |
| 模板说明 | `contracts/_template/README.md` | 完成 |
| 校验脚本 | `scripts/tool-reverse/validate-cards.ps1`（已迭代到 v2） | 完成 |
| 索引脚本 | `scripts/tool-reverse/generate-index.ps1` | 完成 |

### 第 2 项 控件库源码入库

| 项 | 内容 | 状态 |
|---|---|---|
| 源码入库 | 799 个 `.cs`、1,686 文件、6.52 MB | 完成 |
| 来源锚点 | 四个源 DLL 的 SHA-256 与大小记入 `decompiled/README.md` | 完成 |
| 映射关系 | 程序集级粗映射 + 指向权威逐文件对照表 | 完成 |
| 历史台账纠偏 | 查明 `Input/migration-manifest.json` 是早期批次（保留原厂命名空间），与当前中立化命名不一致 | 完成 |

| 本目录 | 源程序集 | `.cs` | 文件总数 | 体积 |
|---|---|---|---|---|
| `decompiled/Controls/` | VM.Controls | 289 | 389 | 1.27 MB |
| `decompiled/WpfControls/` | VMControls.WPF | 215 | 826 | 2.99 MB |
| `decompiled/BaseControls/` | VMBaseControls | 133 | 222 | 0.79 MB |
| `decompiled/PcUi/` | VM.PcUI | 162 | 249 | 1.47 MB |
| `decompiled/ModuleControls/` | 73 个工具模块 `*Control.dll`（第二批） | 822 | 1,189 | 6.03 MB |
| `decompiled/XmlParser/` | Apps.XmlParser（第二批） | 365 | 366 | 1.11 MB |

> **第二批（2026-11-05）**：由我方从本机原件直接反编译，补齐「工具参数面板还原」的最后一环。工具 XML 里出现的 173 个控件类型，仅靠第一批 7 个库命中 0 个，加这两项后 172 个有实现，剩余 1 项 `GlobalRunParam` 已查实是 `SolutionModuleAlgorithmTab.xml` 的元素名而非控件类型，实际覆盖 173/173。逐程序集清单、SHA-256、未入库项（`LightControl.dll` 为原生程序集）与两份 `DynamicObjectConfigControl` 的差异，见 `decompiled/ModuleControls/README.md`。

### 第 3 项 162 张工具卡迁入

| 项 | 内容 | 状态 |
|---|---|---|
| 卡片入库 | 162 张卡、1,134 文件、5.53 MB | 完成 |
| 索引生成 | `index.json`：162 卡，状态/档案/形态/优先级四维统计 | 完成 |
| 契约对齐 | 依据真实数据把契约从 v1.0 迭代到 v1.2 | 完成 |
| 完整性校验 | 七件套缺失 0；3 条硬失败为真实内容缺陷 | 通过（3 条待补） |

**实测分布**

| 维度 | 分布 |
|---|---|
| 状态 | `inventory-only` 117、`source-partial` 43、`source-indexed` 2 |
| 档案 | `standard` 159、`deep` 3 |
| UI 预览形态 | `resource-control-evidence` 152、`evidence-layout` 4、`schema-preview` 3、`empty` 3 |
| 优先级 | P0 23、P1 13、P2 105、P3 10、P4 8 |

### 第 4 项 原页归档与我方页面生成

| 项 | 内容 | 状态 |
|---|---|---|
| Codex 原站归档 | `_reference-codex/site/`：工具 163 页、入口 26 页、数据 87、执行 7、主题 5、根页 2，合计 290 文件 / 28.93 MB | 完成 |
| 归档说明 | `_reference-codex/README.md`：明确标注「描述 VisionReplica 状态，不是我方」 | 完成 |
| 页面生成器 | `scripts/tool-reverse/generate-pages.ps1`：按 8 章节契约从卡片渲染 | 完成 |
| 我方工具页 | `pages/` 下 162 个页面，1.99 MB，命名 `<分类>-<模块名>.html` | 完成 |
| 总览索引页 | `index.html` 45 KB：状态筛选表 + 162 工具全表 + 附属入口 | 完成 |
| 打开方式 | 纯静态 HTML，零脚本、零 fetch，双击 `index.html` 即可用；不需要任何本地服务器（原 `serve-pages.ps1` 已删除） | 通过 |
| 状态冒称修正 | 删掉「有 UI 预览文件即判 partial」与「单字关键词即判已覆盖」两处推断，改为未记录一律 unknown | 完成 |

### 第 5 项 图像源样板与条内五问

| 项 | 内容 | 状态 |
|---|---|---|
| 源码证据 | 5 条带 SHA-256 的直读证据（ImageListPanelBase.cs `DF02FAA3…`、ImageListPanel.xaml、LocalImageListView.xaml/.cs、zh-cn.xaml 术语） | 完成 |
| 条内五问 | 计数分子 / 按节点数据源 / 随节点重解析 / 自动切换语义 / 三控件形态，全部由反编译源码直读闭合 | 完成 |
| 当前实现差异 | 5 条差异写进卡片 `currentImplementation`，不再沿用 Codex 的 VisionReplica 描述 | 完成 |
| 薄结论页 | `docs/collaboration/evidence/VM图像源条节点语义-20261105.html`（21 KB） | 完成 |

### 第 6 项 三类样板卡与模板适配

| 样板 | 类别 | 做了什么 |
|---|---|---|
| 图像源 `imagesourcemodule` | image-roi | 7 环节：2 done（参数与ROI、显示）、3 partial、2 unknown；9 步：3 confirmed、3 partial、3 unknown；7 维：4 partial、3 not-established |
| Blob 分析 `imvsblobfindmodu` | result-view | 原卡 sourceEvidence 为空、unknowns 只有一句，本次补 8 条带哈希证据；7 环节全 partial；9 步 4 confirmed / 5 inferred；7 维 4 partial / 3 not-established |
| 条件分支 `ifbranchmodule` | compound-control | 纠正状态：原 `source-partial` 依据的三份反编译源码不在本仓库，降为 `inventory-only`；补 7 环节 / 9 步 / 7 维结构与「引用但缺文件」清单 |

**模板适配结论**：三类工具共用同一套 8 章节骨架，差异只在 `categoryExtensions`。为此新增契约 v1.3 的 `extendedSections`（analysisStages / semanticChain / algorithmComparison），把原来页面写死的「全部 unknown / 全部 not-established」改为卡片数据驱动；新增 `result-view`、`declarative-parameters`、`compound-control` 三组类扩展条目。

### 第 7 项 调试窗预览控件级还原（进行中）

原来的 `docs/08-工具预览/preview/` 全是三栏外壳：左侧一张「参数名 / 类型 / 默认值」表格，中间空画布，右侧输出表。控件形态、页签、声明顺序都没体现，看不出原件界面长什么样。

升级后由 `scripts/tool-reverse/generate-previews.ps1` 生成，数据只取卡片七件套：

| 数据 | 来源 | 用于 |
|---|---|---|
| 页签 | `resources.json.uiTabs` | 页签栏（多页签时其余标「归属未取证」） |
| 控件 | `resources.json.uiControls[]` | 左列控件行，类型决定形态（下拉 / 开关 / 数值 / 双值 / 按钮+绑定 / ROI / 折叠组 / 显示项） |
| 默认值 | `parameters.json.parameters[].children` | 填进控件 |
| 输入 | `parameters.json.inputs` | 左列输入区（Combination / Filter + 类型） |
| 输出 | `parameters.json.outputs` | 右列结果区（名称 + 类型 + 数组标记） |
| 共享组 | `resources.json.sharedUiGroups` | 右列共享组清单 |

分列规则：`ImageViewConfig` 是结果视图的显示开关（对应原件 `Display.xml` 的视图项），放右列「显示项」，不混进参数区。

162 个已全部升级（`-All` 批量耗时 28 秒），索引与对照页零外壳页、零断链。样板三个的实测数据：

| 工具 | 控件 | 默认值 | 输入 | 输出 | 页签 |
|---|---|---|---|---|---|
| `imagesourcemodule` | 25 | 23 | 5 | 11 | 1 |
| `imvsblobfindmodu` | 94（参数 85 + 显示项 9） | 105 | 6 | 33 | 3 |
| `ifbranchmodule` | 2 | 0 | 0 | 3 | 1 |

**入口规则**：`docs/07-工具逆向/pages/*.html` 与 `index.html` 的预览列指向调试窗 HTML。控件级 → 可点击「打开调试窗预览」；只有外壳 → 标「外壳待升级」；无文件 → 显示「无预览」且不渲染链接。SVG 降为「控件树证据」，不再当预览图。

**还原边界（页面底部固定声明）**：不还原像素级样式、控件到页签的归属、显隐与可用条件、值联动、ROI 交互与运行结果；控件形态由类型推断，未归类类型按文本框显示并保留原始类型标签。控件声明过少的工具会在页面里说明「界面主体由内嵌复合控件或代码构建，不在 AlgorithmTabRoot 声明里」。

**原厂图标接入**：工具栏的运行 / 连续运行 / 停止，图像条条的适应 / 放大 / 缩小，改用已入库反编译资源里的原厂 png（`decompiled/WpfControls/resources`、`decompiled/PcUi/resources`、`decompiled/Controls/.../images`，共 10 张约 11 KB），复制到 `docs/08-工具预览/assets/icons/` 共享一份；画布加 20 px 网格底。图标缺失时自动退回纯文字按钮，不会出现坏图。

**UI 方案能做到哪一步，缺什么**：

| 项 | 现状 | 缺什么 |
|---|---|---|
| 控件清单与顺序 | 已还原（2871 条声明） | — |
| 控件形态 | 已还原（按类型推断 173 种） | 未归类类型只能按文本框 |
| 页签 | 已还原 | 多页签的控件归属未取证 |
| 参数默认值 | 已还原（1701 条） | — |
| 工具栏与画布图标 | 已接入原厂 png | 只覆盖通用按钮，工具专属图标要逐工具找 |
| 像素级布局（宽高/行列/间距/配色） | 未做 | **原件 XML 只给 `type` 和 `name`，没有宽高与行列属性**；控件库 881 个 Style 与 355 个 XAML 在库，但逐个解析成本高 |
| 外观校验 | 未做 | 没有原机窗口截图做基准，无法证明「还原对了」 |

结论：UI 方案 HTML **不需要算法源码**，118 个 inventory-only 的工具一样能出；它们出不了的是「像素级还原」，卡在缺布局属性和缺原机基准，不是卡在反编译。

**8 个工具出不了控件级界面**：`calculatormodule`、`imvscolorimagegenerationmodu`、`dataassemblemodule`、`globaltriggermodule`、`globalvariablemodule`、`lightcontrol`、`localvariablemodule`、`procedurecomm`。原因是原件 `AlgorithmTabRoot.xml` 里没有控件别名声明，界面由内嵌复合控件或代码构建。要还原只能读反编译 XAML（`globalvariablemodule` 已按 `GlobalVariableDialog.xaml` 在 07 卡片里做了 20 条控件记录），不能靠资源声明批量出。

**搜索**：`docs/07-工具逆向/index.html` 与 `docs/08-工具预览/index.html` 都加了搜索框，纯前端过滤（按工具名 / 模块 ID / 分类），`file://` 直接可用，不依赖本地服务。08 索引同时按分类分组，命中时隐藏空组并显示命中数。

**批量命令**：

```
powershell -File scripts/tool-reverse/generate-previews.ps1 -All   # 约 28 秒
powershell -File scripts/tool-reverse/generate-pages.ps1
```

## 三、待办（按依赖顺序）

| # | 任务 | 依赖 | 状态 |
|---|---|---|---|
| 5 | 图像源样板 + 条内五问 + 薄结论页 | 4 | 完成 |
| 6 | Blob / 条件分支样板 | 5 | 完成 |
| 7 | 迁移链补证（T-0009 第二组） | 6 | 已完成（`.sol/.prc` 同构、映射表 14/190 全量、失败 dump 弱证据；见 `docs/collaboration/evidence/VM方案格式版本与兼容迁移-20261105.html` §1.7） |
| 8 | 核验回执 + 文档索引登记 | 7 | 回执已完成：`docs/collaboration/bus/results/T-0009-rea.md`（七件套 0 缺失、`validate-cards` 硬失败 0、页面对齐 162/162、哈希无冒称）。**文档索引登记待做**：`docs/00-索引/` 正在 HTML 化改造，待其收口后跑一次生成 |

## 四、真实缺陷处置

### 已修复（2026-11-05）

| 工具 | 缺陷 | 处置 | 结论 |
|---|---|---|---|
| `dataanalysismodule` | `ui-preview.json` 内容为 `{}` | 按 `DataAnalysisModuleAlgorithmTab.xml` 控件别名恢复 8 条控件 + 1 个页签，附 6 份 XML 的 SHA-256 | 已补，状态仍 `source-partial`（绑定与显隐未取证） |
| `globalvariablemodule` | `ui-preview.json` 内容为 `{}` | 模块 XML 的 `AlgorithmTabRoot` 内无控件声明，改从已入库反编译 `GlobalVariableDialog.xaml`（752 行）直读 20 条控件，逐条带行号 | 已补，窗口与工具节点的挂接列为 `missing` |
| `imvsgeometrictransformmodu` | `ui-preview.json` 内容为 `{}` | 按 `IMVSGeometricTransformModuAlgorithmTab.xml` 恢复 2 条控件 + 2 个页签 + 2 个共享控件组 | 已补，枚举取值与联动未取证 |

修复后全量校验：**硬失败 0**，UI 预览形态 `resource-control-evidence=155`、`evidence-layout=4`、`schema-preview=3`、`empty=0`。

### 未闭合（提示级，不阻断）

另有 15 张卡的 `algorithm-semantics.md` 未覆盖 9 步全部。属组织方式差异（已确认/未确认双段、执行链编号列表），契约已把 9 步齐全降为提示级。

**状态分布变化**：迁入时实测 `source-partial` 43 / `inventory-only` 117；第 6 项把 `ifbranchmodule` 从 `source-partial` 降为 `inventory-only` 后为 **42 / 118**（`source-indexed` 仍 2）。这次下调是纠偏，不是退步：原状态依据的三份反编译源码不在本仓库，按状态口径不能算已恢复。

## 五、踩过的坑

1. **契约不能凭单张卡定**：v1.0 按一张卡写，全量校验 162 卡大面积失败。必须先统计真实分布。
2. **`replace_in_file` 改中文有损坏风险**：曾把「件」写成 U+FFFD。中文改动后必须扫描 `U+FFFD`；批量编辑优先整文件 `write_to_file`。
3. **`.ps1` 无 BOM 被 PS 5.1 按 GBK 解析**：乱码并破坏字符串终止符。脚本落地后必须转 UTF-8 BOM。
4. **`git status` 未跟踪目录折叠**：核数量必须加 `-uall`。
5. **旧迁移台账不能当当前真源**：`Input/migration-manifest.json` 是早期批次。
6. **`@($null)` 是 1 个元素**：会让「字段缺失」被误报成「条目字段不全」。
7. **PS 5.1 不支持 `[Net.Http.HttpClient]`**：要用 `Invoke-WebRequest` 并从异常里取 Response 读 body。
8. **状态不许用旁证推断**：有 UI 预览文件、参数计数大于 0，都不等于该环节已核对。宁可标 unknown。

## 五之二、参数面板预览（`docs/08-工具预览/ui/`，2026-11-05 第二轮）

调试窗预览之外另做一套**参数配置面板**预览，还原对象是原件算法参数面板（标题栏 + 页签 + 分类 + 参数行 + 底部命令组），数据源全部来自原件：

| 层 | 材料 | 用法 |
|---|---|---|
| 结构 | `<Module>AlgorithmTab.xml` | `Tabs/Tab/Categorys/Category/Items` 逐行，顺序不改；`ZHCustomTabWidth/Height` 定窗口尺寸 |
| 行容器 | `decompiled/XmlUi/Apps.XmlUI.ParamTab/LeftRightControl.xaml` | 左列 `ChildLeftWidth`（`LeftWidthConverter` 中文 90）+ 右列 `*`，行高 32，标签 14px `#666666` |
| 类型分发 | `decompiled/XmlUi/styles/paramusercontroldatatemplate.xaml` | 202 个类型 → 真实 DataTemplate，决定每行长什么样 |
| 控件 | `decompiled/XmlUi/Apps.XmlUI.ParamTabControl/*.xaml` | NumericControl（1px 边框 + 20px 箭头）、RoiSelectControl、SliderArrange（取自 PcUi）等真实构成 |
| 样式 | `decompiled/AppsStyle/`（27 xaml + 727 图标） | `NormalRadioButton`（24px `check-box_on/off.png`）、`ExpandButtonStyle`（22 高）、`*RoiBtnStyle`（36×34）、`eyeToggleButton`（32）、`ListViewComboBoxStyle`（30 高）、`swith_on/off.png`（48×32） |
| 主题色 | `decompiled/ColorFun/colorstyle1.xaml` | `Col_Theme=#E76900`、标签 `#666666`、面板底 `#F2F3F5`、分隔线 `#E0DFDE`、下拉边框 `#CCC7C6` |
| 共用组 | `decompiled/Groups/Group.xml` | `GroupLinkItem` 就地展开，含组内 Triggers 参与显隐求值 |
| 语言 | `decompiled/Lang/*.xaml` + 主程序 `resources/zh-cn.xaml` | 中文标签；查不到的中文保留原件英文键并加红色虚线下划线 |

结果：162 页（159 个有 XML、3 个缺原件目录），4,929 个参数行，18 张原件图标按需复制到 `ui/assets/icons/`，缺失图标 0；未还原类型 9 种 / 9 行，页内红框标出，不做假控件；另有 7 行「我方不采用」（相机类 3 种），页内蓝框写明原因与替代。生成器 `scripts/tool-reverse/generate-tool-ui.ps1`，索引 `ui/index.html`。

版本差异声明（2026-11-06）：`contracts/card.schema.json` 升到 v1.4，新增 `currentImplementation.notAdopted`（`type` / `reason` / `replacement`）。相机类卡片登记 `CameraPreview`、`MvsFeatureTree`、`ClockSource` 三项为主动不采用——原件这三件套都绑定海康 MVS，我方取流走 `ICameraFrameSink` 会话与帧所有权、参数走 `ICameraAdapter.GetParameterSchemaAsync()` 的 schema、时钟源取设备枚举符号名并归一化。页面按 `notAdopted` 渲染蓝框说明，不再计入未还原欠账；同时页顶给蓝色「我方设计与原件形态不同」横幅，索引给「与原件有差异」徽章。

### 五之三、9 种工具自带复合控件接入（2026-11-06）

原件里这 9 类控件的界面不在公共模板字典中，而在各工具自带的 `*Control.dll` 里。第二批证据入库后，生成器新增 7 个控件族，逐类按原件 XAML 的列宽 / 行高 / 底色 / 图标还原：

| 控件族 | 覆盖类型 | 依据原件 | 还原出的结构 |
|---|---|---|---|
| `c-threshold` | `ThresholdAreaList` | `ThresholdAreaPanel.xaml` | 表头 1.2\*/1.5\*/4.8\*/1.6\* 四列 + 行 5 列（序号 / 灰度 / 面积范围 / 反向 / 删除）+ 底部新增 |
| `c-imgsrc` | `ImageSourceList` | `ImageSourceListView.xaml` | 固定高 133 滚动，行 = 90px 标签 + 下拉（原件 ComboBox 右移 -12） |
| `c-rangecfg` | `PixelModuRangeConfigList` | `RangeConfigListPanel.xaml` | 灰底 `#f2f3f5` + 标题 + 列表 MaxHeight 233 + add/delete 图标 |
| `c-timecfg` | `TimeConfig` | `TimeConfig.xaml` | 80px 名称 + 两个 200×32 模块下拉 + 50px 删除 + 底部新增 |
| `c-model` | `Panel_EdgepatMatch` / `Panel_EdgePairpatMatch` / `PathDrawingGuide` | `EdgepatMatchConfigPanel.xaml` / `PathDrawPannel.xaml` | 工具条（创建 / 导入 / 编辑模板、路径类为新建 / 修改模型 + 导入导出）+ 1px 边框模型预览区 |
| `c-filecfg` | `ReadFileConfigData` | `ReadFilePanel.xaml` | 单 ListBox，行形态按 `DisplayType` 动态选模板 |
| `c-color` | `ColorConfigEx` | `paramusercontroldatatemplate.xaml`（`x:Key="ColorConfig"`） | 颜色块 + 关联按钮 |

口径约束：每个复合块顶部固定写「结构取自原件 `<xaml 路径>`」；块内未取得的部分（行模板由代码注入、颜色/模板弹层内容、模型预览的实际图像数据）就地写明，不做假控件；示例行只是占位，不冒充运行期数据。

验证结果：全量重生成 162 页，`参数行 4929 / 初始隐藏 2919 / 未还原类型行 0 / 我方不采用 7`，缺失图标 0；9 个归属页逐页核对 `class="cmp"`、控件族标记、来源 XAML 名与 `class="unknown"` 计数（9 页均为 0）。

## 六、未闭合项

- Codex 结论未经我方逐条复验，只有三个样板工具会做抽样复核
- 文档索引 `docs/00-索引/文档索引.html` 尚未登记本库（第 8 项）
- 另有 5 个程序集尚未入库（`VMControls.WPF.Release`、`Apps.PcUI.ImageSourceAudit`、`VM.Core`、`CompatibleTool.Audit`、`iMVS-6000PlatformSDKCS`）；原列的 `ImageSourceModuleControl` 已随第二批入库
- ~~参数面板还剩 9 种复合控件类型未还原~~ **已闭环（2026-11-06）**：这 9 种（`ThresholdAreaList`、`Panel_EdgePairpatMatch`、`Panel_EdgepatMatch`、`ImageSourceList`、`ColorConfigEx`、`PathDrawingGuide`、`ReadFileConfigData`、`TimeConfig`、`PixelModuRangeConfigList`）的实现源码随第二批入库后，已按各自原件 `*Panel.xaml` 的结构接入 `generate-tool-ui.ps1`。全量重生成后「未还原类型行」由 **9 降为 0**，9 个归属页逐页核对通过。相机类的 `CameraPreview` / `MvsFeatureTree` / `ClockSource`（7 行）仍按 `notAdopted` 登记为主动不采用，不算欠账
- 弹层内容（下拉候选、颜色选择、滑杆展开、订阅树）未还原；`SliderArrange` 只画收起态

## 七、越界说明

本阶段只新增 `docs/07-工具逆向/`、`docs/08-工具预览/ui/` 与 `scripts/tool-reverse/` 下文件，未改动 `src/`、`tests/`、既有 885 份证据文件。未 commit、未 push。
