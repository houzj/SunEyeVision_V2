---
name: 方案设计
description: >
  方案设计技能。用于在出方案前进行全面的代码信息调研，
  所有调研结论必须有可追溯的信息源标注（文件路径+行号），
  然后输出完整方案报告（含修改清单+验收标准+测试方法）。
  然后移交「方案实施」技能执行。
  方案实施完成后由「方案测试」技能进行全链路验证。

  Trigger keywords: 方案/方案设计/架构方案/重构方案/优化方案/给出方案
  整合方案/技术方案/设计方案/实施计划

  DO NOT trigger on: simple bug fixes, single-file edits, routine coding tasks.
---

# 方案设计 — 基于代码调研的方案设计流程

> 本技能遵循 rule-008 开发与代码纯净原则（YAGNI/KISS/零兼容/报错优先）
> 和 rule-004 方案设计要求（全局视角/复用优先）。
> 任何"先打补丁再说"、"只修当前 bug"、"不做业界对比"的输出均视为违规。

## 目的

确保每个方案设计都经过四个必要阶段：

1. **信息调研 (Research)** — 出方案前必须全面搜索代码库，确保方案基于真实代码上下文
2. **源头标注 (Source Citation)** — 每个发现和决策必须标注来源位置，禁止无依据推测
3. **bug 模式分析 (Bug Pattern Analysis)** — 治本而非治标，识别 bug 背后的模式
4. **业界对标 (Industry Benchmark)** — 调研 3+ 成熟做法，避免基于错误前提的设计

然后输出完整方案报告，移交 `方案实施` 技能执行。

---

## 工作流 — 五阶段方案设计

### ━━━ Phase 0: 问题分析与方案验证（强制前置）━━━

> **在搜索任何代码之前，先想清楚该不该做、怎么做、为什么做。**

#### Step 1 — 拆解需求，定位根本问题

按顺序回答，答案必须基于当前项目实际，不能凭空推断：

1. **表面需求是什么？** — 用户的原话是什么
2. **根本问题是什么？** — 这个需求解决了什么本质痛点
3. **这个问题属于哪一类？**
   - 功能缺失（有这个功能但没做）
   - 实现方式有误（做了但做错了）
   - 架构冗余（两个地方做同一件事）
   - 边界情况（只在特定输入下出问题）

#### Step 2 — 架构分配检查

回答以下问题，答案必须来自实际代码搜索（标注来源），不能靠 LLM 记忆：

1. 现有系统的分层中，这个需求最自然的归属在哪一层？
2. 如果加到 A 层，B 层已有类似机制吗？（"会不会两个地方在做同一件事"）
3. 如果不做这个改动，现有机制是否能通过其他方式覆盖此需求？
4. 这个改动是否改变已有分层边界？（"是不是在打补丁"）

#### Step 3 — 方法对比

列出至少 3 种可能的实现方式，对比：

| 维度 | 方案 A（推荐） | 方案 B（备选） | 方案 C（备选） |
|------|--------------|--------------|--------------|
| 实现方式 | 一句话描述 | 一句话描述 | 一句话描述 |
| 与架构一致性 | 是否符合现有分层 | 是否符合现有分层 | 是否符合现有分层 |
| 复杂度 | 改动量预估 | 改动量预估 | 改动量预估 |
| 维护成本 | 长期维护难度 | 长期维护难度 | 长期维护难度 |
| 扩展性 | 是否容易扩展新场景 | 是否容易扩展新场景 | 是否容易扩展新场景 |
| 长期 ROI | 6 个月 / 1 年 / 3 年 | 同上 | 同上 |

**【否决门】Phase 0 通关检查**

```
以下任一条件不满足，退回原始需求重新澄清：

□ Step 1 明确了根本问题而非表面需求
□ Step 2 确认方案归属层与现有架构一致，无冗余
□ Step 3 至少对比了 3 种方案，推荐方案理由清晰
□ 以上答案均有代码来源标注，非 LLM 记忆
```

---

### ━━━ Phase 0.5: Bug 模式分析（强制 — agent-025 条款 1）━━━

> **违规判定**：只修当前 bug 不分析 bug 模式 → ❌ 违规
> 不允许"先把这次修了下次再说"，必须识别并消除背后的 bug 模式。

#### 步骤 1 — 当前 bug 的真因分类

| bug 类型 | 描述 | 治本做法 |
|---------|------|---------|
| **症状级 bug** | 表面表现（报错、卡顿） | 不止修症状，要追到底层 |
| **契约漏洞** | 双端协议无保护机制 | 加契约测试 + 端到端冒烟 |
| **状态泄漏** | AsyncLocal / 静态字段污染 | 加状态隔离 + 显式恢复 |
| **设计缺陷** | 架构层面就注定要出问题 | 必须重构而非打补丁 |
| **实现偏差** | 写了代码但没接上 | 接上 + 加 Fail-Forward |

#### 步骤 2 — 反问"这个 bug 还会以什么形式出现？"

```
问自己 3 个问题：
  1. 如果这个 bug 的产生条件再次发生，会再出错吗？
  2. 同类问题是否在别的地方也潜伏着？
     → search_content 搜相似 pattern
  3. 现有架构的哪个环节让这类 bug 必然出现？
     → 这个环节就是"治本点"
```

#### 步骤 3 — 治本点识别

输出：

```markdown
### Bug 模式分析

**当前 bug**: {描述}

**真因分类**: {契约漏洞/状态泄漏/设计缺陷/...}

**同类潜伏点**:
- [来源] path/file.cs:行号 — 同 pattern 在此处也存在
- [来源] path/file2.cs:行号 — 另一处类似

**治本点**: 修复 {治本点} 而非 {当前症状点}

**证据缺失警告**: 如果找不到同类潜伏点，写"本次为孤例，但仍按治本标准修复，防止未来复制"
```

---

### ━━━ Phase 0.7: 业界对标（强制 — agent-025 条款 2）━━━

> **违规判定**：方案没有业界对比 → ❌ 违规
> 必须调研 3+ 成熟框架/产品的做法，标注可追溯引用（框架名 + 文档链接 + 关键特征）。

#### 步骤 1 — 候选对标对象选择

按问题领域选 3+ 个：

| 领域 | 候选对标对象 |
|------|------------|
| 视觉检测框架 | Halcon / Cognex VisionPro / OpenCV / 海康威视 VisionMaster / LabVIEW Vision |
| 图像处理算子 | Halcon HOperatorSet / OpenCV Cv2 / scikit-image |
| WPF/.NET 桌面 | CommunityToolkit.Mvvm / Avalonia UI / Prism |
| 工具注册/管理 | VS Code Extension API / JetBrains Plugin SDK / VisionMaster 工具库 |
| 参数系统 | Halcon HALCONC++ 参数模型 / VisionMaster 流程参数 |
| 序列化 | System.Text.Json / Halcon HSerialize / VisionMaster Recipe |
| 插件架构 | MEF / MAF / VS Code Extension Host |

#### 步骤 2 — 调研并输出对标表

每个对标对象必须给出：

| 字段 | 说明 |
|------|------|
| 框架名 | 名称 + 链接 |
| 关键特征 | 该框架怎么解决这个问题 |
| 文档引用 | 官方文档锚点（不是泛泛的官网） |
| 借鉴/不借鉴 | 对本方案的启示 |

#### 步骤 3 — 决策依据沉淀

把"业界 3+ 做法"沉淀为方案报告的"依据"章节，每个设计决策至少有 1 个业界引用支撑。

**反面案例（违规）**：
- "参考了某框架" → 没说哪个框架、哪份文档、什么特征
- "业界最佳实践" → 没说哪家的实践

**正面案例**：
- "MCP 协议采用 stdio + JSON-RPC，依据 modelcontextprotocol.io 官方 spec 2025-06-18 §Transport"

---

### ━━━ Phase 1: 信息调研 (Research) ━━━

在出任何方案之前，必须全面搜索代码库。

#### 调研范围 (SunEyeVision 适配)

| 调研维度 | 搜索方法 | 目的 |
|---------|---------|------|
| 涉及文件定位 | search_file / search_content | 确定修改波及的文件 |
| 类/接口定义 | search_content | 找到类定义、接口签名、基类 |
| 调用链 | search_content 搜索调用方 | 确认修改的影响范围 |
| XAML 绑定 | search_content | 了解 UI 绑定和资源引用 |
| Halcon 算子调用 | search_content "HOperatorSet\|region.FillUp\|region.Connection" | 检查 rule-014 合规性 |
| csproj / MSBuild 配置 | search_file "*.csproj" / read_file | 检查 OutDir / Reference / IsToolPlugin |
| Toolkit 注册机制 | search_content "ToolRegistry\|IToolPlugin" | 了解工具注册/参数提取 |
| 参数系统 | search_content "ToolParameters\|GenericToolParameters\|ParametersTypeName" | 分析参数序列化/绑定 |
| 插件部署目录 | PowerShell Get-ChildItem dev/Debug/plugins/ | 验证构建产物（rule-017） |
| Rule 规则 | search_content "rule-\|\.mdc" | 确认 Rule 文件中的约束 |
| 现有 Skill | search_file / search_content | 复用现有技能模式 |
| **同类 bug 模式** | search_content pattern 搜索 | Phase 0.5 步骤 2 要求 |
| **业界对标** | web_search / web_fetch | Phase 0.7 要求 |

#### 调研记录格式

```markdown
## 调研记录

### 文件分布
| 文件 | 行号 | 角色 |
|------|------|------|
| AgentWpf/ViewModels/XxxViewModel.cs | 10-50 | 定义/调用点 |
| AgentWpf/Services/XxxService.cs | 80-120 | 实现/调用方 |

### 类/接口定义
- [来源] AgentWpf/ViewModels/XxxViewModel.cs:25 — XxxViewModel 定义，继承 ObservableObject
- [来源] AgentWpf/Services/XxxService.cs:42 — XxxService 类定义

### 调用链
- [来源] AgentWpf/ViewModels/XxxViewModel.cs:88 — 调用 XxxService.DoSomething()
- [来源] AgentWpf/MainWindow.xaml:40 — XAML 绑定来源

### XAML 资源引用
- [来源] AgentWpf/Themes/Styles.xaml:15 — 按钮样式样式定义
- [来源] AgentWpf/Themes/Colors.xaml:20 — 主题色定义

### 现有模式参考
- [来源] AgentWpf/ViewModels/ExistingViewModel.cs:55 — 类似功能实现模式

### 同类 bug 模式（Phase 0.5 输出）
- [来源] path/file.cs:行号 — 同 pattern 在此处也存在
- [来源] path/file2.cs:行号 — 另一处类似

### 业界对标（Phase 0.7 输出）
- [来源] https://... — 框架名 + 文档锚点
```

> 调研要全面，不要只搜一个文件就停。必须找到：定义、实现、调用方、UI 绑定、同类 pattern、业界做法。

---

### ━━━ Phase 2: 源头标注 (Source Citation) ━━━

方案报告中每个关键结论必须有可追溯的来源标注。

#### 标注规则

1. **每个发现点** 必须标注 [来源] 文件路径:行号
2. **禁止** 无依据的推断 — 如果找不到代码证据，标注为 [推测] 并说明原因
3. **引用格式**：
   - `[来源] path/File.cs:行号` — 简要说明
   - `[来源] path/File.cs:开始行-结束行` — 跨行范围
   - `[来源] https://url#anchor` — 业界文档引用
   - `[推测]` — 没有代码证据，基于...的推断
4. **标注位置**：方案报告中每个章节末尾，或内联在发现点后

#### 标注示例

```markdown
## 问题分析
- ChatHistory 上下文丢失
  - [来源] AgentEmployeeViewModel.cs:276 — 使用 InvokePromptAsync 单次调用
  - [来源] AgentChatViewModel.cs:1038-1045 — 正确使用 ChatHistory 的示例
  - [推测] 因两个 ViewModel 的开发路径不同导致模式不一致
```

---

## 方案报告输出

调研和标注完成后，必须输出结构化的方案报告，输出格式请参考 `templates/solution-report.md`。

方案报告必须包含（agent-025 硬性章节）：

| 章节 | 必需 | 说明 | 用于 |
|------|:----:|------|------|
| **改前/改后/依据** | ✅ | 用户功能性视角 + 业界引用 | 全员评审 |
| **问题分析** | ✅ | 调研结论 + 来源标注 | 设计依据 |
| **Bug 模式分析** | ✅ | Phase 0.5 输出，真因 + 同类潜伏 | 治本依据 |
| **业界对标** | ✅ | Phase 0.7 输出，3+ 框架对比 | 设计依据 |
| **方案对比** | ✅ | ≥3 候选 + 长期 ROI + 推荐理由 | 决策 |
| **修改清单** | ✅ | 文件路径、变更类型、核心变更、前置依赖 | 方案实施 |
| **清理清单** | ✅ | 文件路径、清理内容（注释残留/死代码/无用 using/XML doc）、原因 | 方案实施 Phase 2 Step E |
| **防御机制** | ✅ | 至少 2 项：契约测试 / 端到端冒烟 / Fail-Forward / 失败兜底 | 方案测试 |
| **验收标准** | ✅ | 可验证的硬性标准，每个修改点对应一条 | 方案测试 |
| **测试方法** | ✅ | 自动/手动/code review 标记，关联测试用例 | 方案测试 |
| **风险评估** | ✅ | 实施风险、回退策略 | 实施决策 |

### 验收标准格式要求

每条验收标准必须包含：
1. **验收项** — 一句话描述
2. **验收方法** — unit test / manual / code review
3. **关联测试** — 对应测试用例编号（如有）
4. **对应防御机制** — 这条验收覆盖哪个防御机制

示例：

```markdown
| # | 验收项 | 验收方法 | 关联测试 | 对应防御机制 | 备注 |
|---|--------|---------|---------|------------|------|
| 1 | Employee 模型含 Username/PasswordHash | code review | - | 契约测试 | |
| 2 | AuthService.Login() 返回 Employee | unit test | UT-AS-01~03 | 单元测试 | |
| 3 | Viewer 不可见系统设置页 | unit test | UT-PM-04 | 端到端冒烟 | 回归 |
| 4 | 添加员工后自动创建登录账号 | manual | - | 端到端冒烟 | 界面操作 |
```

---

## 自检清单（输出前必跑）

```
□ 0. 用户功能性视角（改前/改后/依据）写在最前
□ 1. 方案对比 ≥3 候选 + 长期 ROI
□ 2. Bug 模式分析（真因 + 同类潜伏 + 治本点）
□ 3. 业界对标 ≥3 框架（每条决策有可追溯引用）
□ 4. 防御机制 ≥2 项（契约测试 / 端到端冒烟 / Fail-Forward / 失败兜底）
□ 5. 验收标准覆盖"启动 → 用户看到结果"完整路径
□ 6. 没有"先打补丁"话术
□ 7. 所有结论有代码来源标注
□ 8. 推荐方案有明确理由（依据 X、Y、Z 条款）
□ 9. 清理清单已列出：每个修改文件对应的注释残留/死代码/XML doc 清理项
□ 10. 无"新增时未对应清理"的残留黑洞（新增 3 行代码却留下 10 行旧注释 = 净污染）
```

任一项未通过 → 重新修改方案，不要输出。

---

## 移交实施

方案报告通过用户确认后：

1. 调用 `.codebuddy/skills/方案实施/` 执行
2. 将方案报告作为 Input 传给方案实施
3. 方案实施负责构建验证和代码修改
4. 方案实施完成后自动移交「方案测试」进行全链路验证

## 参考

- **rule-004: 方案设计要求** — 全局视角/复用优先/零兼容
- **rule-008: 开发与代码纯净原则** — 零兼容/报错优先/YAGNI/KISS
- **rule-010: 方案系统实现规范** — System.Text.Json / JsonPolymorphic / PascalCase
- **rule-012: 参数系统约束** — Dictionary 绑定 / ToolRegistry 注册机制
- **rule-014: Halcon HOperatorSet 调用规范** — 禁止实例方法
- **rule-015: 禁止兜底回退原则** — 类型不匹配直接报错
- **rule-016: MSBuild 项目配置规范** — OutDir / 插件部署 / 污染拦截
- **rule-017: .gitignore 屏蔽目录的文件读取规范** — dev/ 用 PowerShell
- skill:方案实施 — 方案通过后移交实施
- skill:方案测试 — 实施完成后全链路验证
- skill:build-verify — MSBuild 验证辅助
- skill:cv-engine-builder — 视觉算子封装
- skill:wpf-vision-dev — WPF 视觉开发
