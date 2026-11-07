# 工具逆向证据库

本库回答一个问题：**要逆向/实现某个工具时，去哪找全部原件证据，怎么用最短时间做完。**

不是文档收藏夹，是取证台账。每张卡对应一个工具，每份证据必须能追到「文件 + 行号/偏移 + SHA-256」。

## 一、库的边界

| 项 | 说明 |
|---|---|
| 覆盖对象 | VisionMaster 4.4.0（V4.4.0 Build20250516@1555331）安装版工具 |
| 工具规模 | 162 张工具卡 / 163 个对照页（含主界面入口） |
| 证据形态 | 反编译重建源码、原厂 XML 资源、受控运行观察、原件页面截图 |
| 不做的事 | 不修改产品代码、不启动原厂程序、不声称「已完成」 |
| 结论口径 | 见下「状态枚举」，不给笼统完成百分比 |

## 二、数据来源与继承边界（重要）

本库的 162 张工具卡、163 个对照页、`decompiled/` 反编译源码，**继承自 Codex 的 VisionReplica 逆向项目**（`D:\MyWork\REA逆向测试`，2026-10 至 11 月产出）。

继承规则：

1. **原样继承的，标 `source: codex`**，我方未逐条复验。使用前先看该卡的 `status` 与 `unknowns`。
2. **我方改写/新增的，标 `source: suneyevision`**，并列明改写原因。
3. **「当前实现与差异」章节一律我方重写。** Codex 原页该章节描述的是 VisionReplica 的 JSON 目录状态（例如「无当前 ModuleId 映射」「优先级 deferred」），**与我方 SunEyeVision 现状无关，直接引用会张冠李戴**。
4. Codex 原页原样归档在 `_reference-codex/`，只作取证来源，不进主导航、不作我方现状。

## 三、状态枚举（全站统一）

| 状态 | 含义 | 判据 |
|---|---|---|
| `inventory-only` | 仅有清单，无源码证据 | 只有 XML 资源与工具项声明 |
| `source-indexed` | 源码已定位，未逐项解读 | 找到程序集与类型，未读函数体 |
| `source-partial` | 源码部分恢复，部分环节有证据 | 至少一个环节有反编译证据 |
| `source-confirmed` | 环节级有反编译源码直接支撑 | 该环节可指到具体文件与行号 |

**`pending` 不计入闭合。** 缺证据就写 `unknown`，不补默认值、不写推测（对齐 rule-015）。

## 四、目录

```
docs/07-工具逆向/
├── README.md              本文件
├── index.html             总览入口（162 行总表：状态 / 对照页 / 预览 / 证据）
├── index.json             机器可读索引
├── progress.md            建设进度与未闭合项
├── contracts/             模板契约（字段定义与枚举，生成脚本与人工核对共用）
├── tools/                 每工具一卡，七件套
├── pages/                 我方工具对照页（由卡片数据生成，顶部内嵌 UI 方案图）
├── _reference-codex/      Codex 原页归档（只读，非我方现状）
├── 历史-REA逆向测试/      旧 VisionReplica 架构/源码树/插件边界方案原文（只读证据）
└── decompiled/            反编译重建源码（按职责命名，见其 README）
```

**打开方式：直接双击 `index.html`，`file://` 即可完整浏览，不需要启动任何本地服务。** 全站无 `fetch` / `XMLHttpRequest` / 根路径引用，断网也可用。

## 五、每工具卡七件套

| 文件 | 内容 | 关键要求 |
|---|---|---|
| `<toolid>-reverse.md` | 逆向记录 | 每条结论带文件定位 + SHA-256 + 可确认范围 |
| `evidence.json` | 证据索引 | 身份、状态、计数、`unknowns[]`、来源报告 |
| `parameters.json` | 参数全量表 | 保留原厂 XML 树结构，不二次加工 |
| `resources.json` | 资源清单 | XML 资源哈希、UI 页签、控件类型 |
| `ui-preview.json` | UI 预览描述 | 控件树 + `missing[]` + 状态 |
| `ui-preview.svg` | 布局预览 | 证据性布局，**非像素级**，注明替换条件 |
| `algorithm-semantics.md` | 9 步算法语义链 | 每步标 `unknown` 直到有原厂证据 |

## 六、9 步算法语义链（顺序不可变）

| # | 步骤 | 要回答什么 |
|---|---|---|
| 1 | `init` | 创建/初始化：构造函数、注册表、资源绑定 |
| 2 | `parameters` | 参数读取与条件显隐：参数对象、绑定路径、触发器 |
| 3 | `inputsRoi` | 输入/ROI/变量准备：输入节点、所有权、reader 生命周期 |
| 4 | `process` | Process/ProcessAlg/原生桥：函数体、调用约定、返回码 |
| 5 | `branchFailure` | 分支、失败与取消：谓词、错误码、停止路径 |
| 6 | `resultWrite` | 结果写回与判定：结果结构、状态码、判定链 |
| 7 | `displayPublish` | 图形/文字显示发布：结果视图、DisplayType、通知链 |
| 8 | `persistence` | 保存恢复：序列化键、版本迁移、重绑 |
| 9 | `release` | DeInit/Destroy/释放：所有权、reader lease、释放顺序 |

此链只接受原厂反编译源码、资源、汇编/ABI 或受控运行证据。**产品意图、截图或自研实现不能单独证明 VM 算法语义。**

## 七、7 个分析环节（每卡固定）

`ui` / `common` / `parametersRoi` / `execution` / `display` / `storage` / `native`

每环节字段：`id`、`status`、`detail`、`links[]`。

## 八、7 维算法对照（每卡固定）

| 维度 | 对照什么 |
|---|---|
| `originalAlgorithm` | 原版具体算法/入口 |
| `currentCalculation` | 我方当前计算路径 |
| `defaultsPrePost` | 参数默认与前后处理 |
| `boundariesCoordinatesTypes` | 边界/坐标/类型 |
| `pixelNumerics` | 像素/数值差异与误差 |
| `nativeAbi` | 原 native/ABI |
| `validationInputs` | 验证输入/基线 |

状态：`not-established` / `partial` / `established`。未建立对比一律写 `not-established`，**不拿 UI 例证、XML 参数数量或编译通过冒充算法等价**。

## 九、怎么用（最短路径实现一个工具）

1. 双击 `index.html`，在 162 行总表里按状态或工具名定位目标工具
2. 点该行「预览」，直接在浏览器看 `ui-preview.svg`：这是据反编译源码推出的该工具 UI 方案
3. 点该行「证据」里的任一项直达卡片文件；或用「对照页」进完整对照页（页内已内嵌 UI 方案图与七件套直达）
4. 读 `<toolid>-reverse.md`：确认原件位置、版本、已有证据与未知项
5. 读 `algorithm-semantics.md`：9 步逐项看哪些 `unknown`，这些就是要攻的点
6. 读 `parameters.json`：拿全量参数名与默认值，直接对上我方参数模型
7. 读 `ui-preview.json`：控件树、显隐条件、绑定、主题态、`missing[]`
8. 到 `decompiled/` 按程序集找对应源码（`SolutionMigration/`、`WpfControls/`、`Controls/` 等）
9. 实现后回填该卡 `status` 与环节 `detail`，**没验证的不改成 done**
10. 跑 `scripts/validate-cards.ps1` 自检字段完整性

### 预览列与 `docs/08-工具预览/` 的区别

| 对象 | 依据 | 内容 |
|---|---|---|
| 本库「预览」列 `tools/<id>/ui-preview.svg` | 反编译源码 + 原厂资源 | 控件树、分区、显隐条件、绑定，**是该工具的 UI 方案** |
| `docs/08-工具预览/preview/<id>.html` | 原件 `ToolItemInfo` 参数声明 | 调试窗外壳：参数树 / 输入 / 输出列表，不含算法与控件树推论 |

两者口径不同，不互相替代、不合并。

### 已知数据不一致（根因已定位，待补齐建模）

`ui-preview.svg` 图上标注的 `controls` 数**取自 `resources.json` 的 `uiControls`**，而 `ui-preview.json` 的 `controls` 数组是另一套模型，两者不是同一份数据：

| 工具 | `resources.json` uiControls | `ui-preview.json` controls | SVG 标注 |
|---|---|---|---|
| `imvsblobfindmodu` | 94 | 93 | 94 |
| `imvsc2cmeasuremodu` | 70 | 69 | 70 |
| `dataqueuemodule` | 3 | 1 | 3 |

162 个工具中 44 个存在该差异，规律与上表一致：**SVG 用的是 XML 资源原始控件清单，`ui-preview.json` 少建了部分控件**。这不是口径选择，是 `ui-preview.json` 控件树建模不完整。

当前处理：对照页内如实标注差异，统计以 `ui-preview.json` 为准，SVG 不改。根治方式是补全 `ui-preview.json` 的控件树，使其覆盖 `resources.json` 的全部 `uiControls`；否则「预览」列看到的控件数与「UI 描述」里的控件树始终对不上。

## 十、版权与使用边界

`decompiled/` 与各卡中的 `sourceEvidence` 指向 VisionMaster 4.4.0 商业程序集的反编译重建产物，仅用于本项目内部对照与自研实现：

- 不对外分发、不随产品发布、不打包进安装包
- 每次入库记录来源程序集、SHA-256、抓取日期
- 产品代码不得直接引用这些文件

Codex 原项目 `ROADMAP.md` 原文写明「这些本地证据不随 Git 上传」；本库按项目所有者决定纳入版本管理（便于换机迁移），此差异在此显式记录。

## 十一、免责与否定性声明

本库任何一张卡、任何一个页面，都**不证明**该工具在原厂中已注册、已加载、可执行或已完成。它们只记录「看到了什么、在哪看到的、还有什么没看到」。

## 十二、旧 REA 方案迁移归档

旧逆向项目的架构和源码树原文已迁入 docs/07-工具逆向/历史-REA逆向测试/。该目录只保存历史证据；其独立实现、.vmx.json 格式和未闭合 SDK/ABI 结论不自动成为当前产品方案。当前迁移动作统一登记在 docs/01-方案设计/2026/11/06/最终源码树评审稿-证据分级-20261106.html。


