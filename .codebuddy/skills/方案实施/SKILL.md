---
name: 方案实施
description: >
  PDTAC 轻量实施闭环：Plan（理解需求）→ Do（代码实现）→ Test（构建验证）→ Act（修复/报告）。
  每次只改一个修改点，构建验证闭环，Human Gate 决策。
  实施完成后移交「方案测试」技能进行全链路验证。
  严格遵循方案设计阶段的"治本"原则，不打补丁、不擅自简化。
trigger_keywords:
  - 开始实施, 开始实现, 开始开发, 开始改代码
  - 实施, 实现, 开发, 落地, 落地实施
  - 代码修改, 改代码, 修Bug, implement, fix bug, refactor
do_not_trigger:
  - simple questions
  - code review only
  - reading/analyzing without modification
  - documentation-only tasks
---

# 方案实施 — 轻量 PDTAC 实施闭环

> 本技能遵循 rule-008 开发与代码纯净原则（零兼容/报错优先/YAGNI/KISS）
> 和 rule-004 方案设计要求（治本优先/复用优先）。
> 实施时必须**严格按方案报告执行**，不允许"先打补丁下次再说"，不允许"为了赶进度跳过防御机制"。

## 概述

轻量版实施闭环，保留核心骨架，去掉 SunEyeVision 专有内容和过重机制。

```
┌─────────────────────────────────────────────────────────────┐
│                PDTAC 内循环 + 移交测试                        │
│                                                             │
│  Plan ──→ Do ──→ Test ──→ Act ──→ 移交方案测试              │
│    │        │        │        │        │                    │
│  治本验证  代码实现  构建验证  自修复/报告 外部验证闭环       │
│    │        │        │        │        │                    │
│    └─── 一次一个修改点 ──→ 构建验证闭环 ──┘                 │
│                                                             │
│  Human Gate: L2 级别（预览后自动执行）                       │
└─────────────────────────────────────────────────────────────┘

外循环（三个 skill 之间）:
  方案设计 → 方案实施 → 方案测试 → (回退→方案设计/方案实施)
```

## 就绪等级

| 等级 | 模式 | 说明 |
|:----:|------|------|
| **L1** | Report | 报告模式：只分析问题和输出计划，不自动修改 |
| **L2** | Assist | 辅助模式：输出修改预览后自动执行（默认） |
| **L3** | Auto | 全自动：仅删除文件和 3 次修复失败需确认 |

## Human Gate 策略

| 场景 | L1 | L2 | L3 |
|------|:--:|:--:|:--:|
| 新文件创建 | 询问 | 询问 | 自动 |
| 删除文件 | 询问 | 询问 | 询问 |
| 修改已有代码 | 报告 | 预览后询问 | 自动 |
| 编译错误（≤2次） | 报告 | 自动修复 | 自动修复 |
| 编译错误（≥3次同错） | 报告 | 升级给人 | 升级给人 |

---

## Phase 0: 治本验证（强制前置 — agent-025 条款 1、6）

> **目的**：防止"打补丁话术"，确认实施步骤是"治本"而非"先这样改了下次再说"。

在进入 Phase 1 之前，必须输出"治本验证表"，逐条核对方案：

```markdown
## 治本验证表

| # | 方案中要求 | 实际做法 | 治本？ | 备注 |
|---|-----------|---------|:-----:|------|
| 1 | {方案要求 1} | {本次实际做法} | ✅/❌ | |
| 2 | {方案要求 2 — 防御机制} | {本次实际做法} | ✅/❌ | |
| 3 | {方案要求 3 — 验收标准} | {本次实际做法} | ✅/❌ | |

### 任何"❌"必须升级处理
- 升级到方案设计阶段，重新评估
- 禁止"先这样做了再补"等打补丁话术
```

**触发条件**：
- 方案要求"加契约测试"，实施时跳过 → ❌ 升级
- 方案要求"加端到端冒烟"，实施时只写 mock → ❌ 升级
- 方案要求"治本"（识别 bug 模式），实施时只修当前 bug → ❌ 升级
- 方案"防御机制 ≥2 项"，实施时只做了 1 项 → ❌ 升级

---

## Phase 1: Plan（理解需求）

1. **检查方案完整性** — 必须有明确的修改清单
2. **治本验证通过** — Phase 0 已通过
3. **提取修改点** — 结构化：文件路径、变更类型、核心变更、前置依赖
4. **上下文确认** — 读取目标文件，确认与方案描述匹配

```
for each 修改点:
  1. 目标文件是否存在？
  2. 涉及的类/方法签名是否匹配？
  3. 前置依赖是否已到位？
  4. 对应的验收标准是什么？  ← 关联到方案报告的验收表
  5. 对应的防御机制是什么？  ← 契约测试 / 端到端冒烟 / Fail-Forward
```

---

## Phase 2: Do（实施 — 一次一个修改点）

每次只改一个文件，4 步原子操作：

```
Step A: Read     — 读取目标文件当前内容
Step B: Verify   — 确认上下文与方案一致
Step C: Write    — 执行修改（replace_in_file / write_to_file）
Step D: Preview  — 输出 diff 预览
```

**工具优先级：**
1. `replace_in_file` — 精确上下文替换（推荐）
2. `write_to_file` — 整文件替换/新建
3. PowerShell WriteAllText + UTF-8 — 前两种出现中文乱码时备用

**每个修改点完成后立即记录**：

```markdown
### 修改点 #N
- 文件: path/file.cs
- 方案要求: {方案中此点的核心变更}
- 实际改动: {diff 摘要}
- 对应验收标准: {方案报告中的 AC-NN}
- 对应防御机制: {契约测试 / 端到端冒烟 / Fail-Forward}
- 状态: ✅ 已完成 / ⏸ 待续
```

---

## Phase 3: Test（构建验证）

每个修改点完成后立即验证：

```powershell
# 验证单个工具 csproj（推荐，快速反馈）
dotnet build "tools/SunEyeVision.Tool.XXX/SunEyeVision.Tool.XXX.csproj" `
  --configuration Debug -v:minimal -nologo

# 验证 slnx（集成阶段）
dotnet build SunEyeVision.slnx --configuration Debug -v:minimal -nologo
```

### 工具插件部署验证（修改 csproj 时强制 — rule-016）

> ⚠️ `dev/` 目录被 .gitignore 屏蔽，必须用 PowerShell（rule-017）

```powershell
$ToolName = "SunEyeVision.Tool.XXX"
$PluginPath = "d:\MyWork\SunEyeVision_V2-script\dev\Debug\plugins\$ToolName"

# 查看插件目录内容（应仅含 <ToolName>.dll/.pdb + 显式资源）
Get-ChildItem -Path $PluginPath -File | Select-Object Name

# 检查无 NuGet 污染
Get-ChildItem -Path $PluginPath -File |
  Where-Object { $_.Name -notmatch "^$ToolName\.(dll|pdb)$" }

# 检查无语言子目录 / wpftmp / runtimes
Get-ChildItem -Path $PluginPath -Directory
```

### WPF + SunEyeVision 常见错误模式

| 错误 | 典型信息 | 根因 | 修复 |
|------|---------|------|------|
| XAML 命名空间 | `The name 'xxx' does not exist in namespace` | 缺 xmlns 引用 | 添加 `xmlns:xxx="clr-namespace:..."` |
| XAML d/mc 缺失 | `The property 'Ignorable' does not exist` | 缺 mc 命名空间 | 添加 `mc:Ignorable="d"` |
| CS0246 | `Type/namespace 'X' not found` | 缺 using | 补充 using 语句 |
| CS1061 | `'X' does not contain definition for 'Y'` | 方法名不对 | 检查签名 |
| CS0103 | `Name 'X' does not exist` | 变量未定义 | 检查声明和作用域 |
| XAML Binding | `Cannot find source for binding` | DataContext 未设置 | 检查 Binding 路径 |
| XAML StringFormat | `'{}' is required` | StringFormat 花括号未转义 | 加 `{}{0}` 前缀 |
| **MSB4xxx** | `MSB4006` / `MSB4090` | MSBuild target 不存在/属性未定义 | 检查项目文件和配置 |
| **MSB5xxx** | `MSB5009` / `MSB5011` | 项目文件格式错误 | 检查 csproj 格式 |
| **OutDir 冲突** | 未定义 OutDir / 输出到错误目录 | 缺少 `IsToolPlugin` / 重复声明 OutDir | 检查 Directory.Build.targets |
| **Halcon 引用** | `The type 'HImage' exists in both...` | Reference 路径冲突 | 检查 HintPath 是否统一 |
| **Plugin.SDK 引用** | `Cannot find type 'IToolPlugin'` | SDK 引用路径错误 | 检查 Reference HintPath |

### 自修复循环

```
编译错误 → 分析 → 修复 → 重新编译
                         ↓
                    ┌── 通过 → 继续下一步
                    └── 错误 → 重复（最多3次）
                                      ↓
                                   3次未通过 → 升级给人
```

**自修复计数器**：
- 同类错误出现 3 次 → 自动升级到 Human Gate
- 升级后必须输出"3 次失败根因分析"

---

## Phase 4: Act（结果报告）

所有修改点完成后输出实施完成报告。

**实施完成报告必须从用户功能性视角描述**（agent-025 条款）：

```markdown
## 实施完成报告

### 1. 用户功能视角（核心）

#### 改前用户看到什么
{用户的痛点/问题}

#### 改后用户看到什么
{用户现在能正常使用}

#### 解决了哪些功能性问题
- 问题 1: ...
- 问题 2: ...
- 问题 3: ...

### 2. 验收结果（来自方案报告）

| # | 验收标准 | 状态 | 验证方法 | 关联测试 |
|---|---------|:----:|---------|---------|
| AC-01 | {方案报告中的标准} | ✅/❌ | {实际如何验证} | {测试编号} |
| ... |

### 3. 防御机制验证

| # | 防御机制 | 方案要求 | 实际落地 | 状态 |
|---|---------|---------|---------|:----:|
| 1 | 契约测试 | {方案描述} | {实施做法} | ✅/❌ |
| 2 | 端到端冒烟 | {方案描述} | {实施做法} | ✅/❌ |
| 3 | Fail-Forward | {方案描述} | {实施做法} | ✅/❌ |

### 4. 修改清单

| 文件 | 操作 | 说明 | 对应 AC |
|------|------|------|---------|
| path/file.cs | 修改 | 核心变更 | AC-01 |

### 5. 构建验证

- [x] dotnet build 通过（0 error）
- [x] 所有方案步骤完成
- [x] 无方案范围外的额外修改（YAGNI）
- [x] 无 [Obsolete] / 注释掉的代码残留
- [x] **治本验证全部通过**（Phase 0 治本验证表）

### 6. 自修复统计

- 总循环次数: N
- 错误模式: xxx
- Human Gate 触发: N 次
- 升级原因: {根因}

### 7. 移交测试

- [ ] 已移交「方案测试」skill 进行全链路验证
- [ ] 待接收测试结果
```

---

## Phase 5: 移交测试（新增）

实施完成报告输出后，自动移交「方案测试」skill：

```
1. 汇总以下信息传递给方案测试：
   ├── 方案报告中的验收标准清单
   ├── 方案报告中的防御机制清单
   ├── 修改文件清单（直接修改 + 关联影响）
   ├── 编译结果（构建是否通过）
   └── Phase 4 的实施完成报告

2. 触发方案测试流程：
   ┌── 全部通过 → ✅ 方案完成，关闭
   ├── 部分失败(轻微) → 记录问题，方案完成
   ├── 部分失败(核心功能) → 接收测试反馈，修复后重测
   └── 严重失败(设计问题) → 回退到方案设计 skill

3. 回退处理：
   - 实施偏差（代码写错了）→ 收到测试反馈后修复，重新移交测试
   - 设计缺陷（方案有问题）→ 回退到方案设计，输出修正方案
```

---

## 核心原则

1. **方案驱动** — 严格按已有方案实施，不自行增减
2. **治本优先** — 实施前必须通过治本验证（Phase 0），禁止打补丁
3. **先读后改** — 修改前必须读取文件当前内容
4. **一次一个修改点** — 每步验证后再下一步
5. **构建验证闭环** — 每次修改后 dotnet build，最多自修复 3 次
6. **防御机制落地** — 方案要求的所有防御机制必须在实施中真实落地（不只是写到文档）
7. **报错即停** — 发现方案与代码不符时立即报告

## 禁止行为

- **禁止**超出方案范围自行添加功能（YAGNI）
- **禁止**在构建验证环节吞错误继续推进
- **禁止**修改前不读取文件当前内容
- **禁止**使用 `[Obsolete]` / 条件编译 / 注释保留旧代码
- **禁止**在 3 次自修复失败后仍自动重试
- **禁止**跳过方案中的防御机制（agent-025 条款 4 硬性要求）
- **禁止**"先打补丁下次再说"等治标话术（agent-025 条款 6 硬性要求）
- **禁止** 实施完成报告只列"改了什么文件"，必须从用户功能视角描述

## 参考

- skill:方案设计 — 方案报告来源（必读，治本验证的来源）
- skill:方案测试 — 实施完成后移交验证
- skill:build-verify — MSBuild 构建验证辅助
- **rule-003: 日志系统使用规范** — LogInfo/LogError，禁止 Console.WriteLine
- **rule-008: 开发与代码纯净原则** — 零兼容/报错优先/YAGNI/KISS
- **rule-013: 文件编码与写入规范** — 优先 replace_in_file，中文用 UTF-8 无 BOM
- **rule-014: Halcon HOperatorSet 调用规范** — 禁止实例方法调用
- **rule-015: 禁止兜底回退原则** — 未映射类型直接报错
- **rule-016: MSBuild 项目配置规范** — OutDir / 插件部署 / 污染拦截
- **rule-017: .gitignore 屏蔽目录的文件读取规范** — dev/ 必须用 PowerShell
