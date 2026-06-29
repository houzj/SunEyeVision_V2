---
name: 方案实施
description: >
  循环工程实施闭环（PDTAC + 内外双循环）。将审查通过的方案设计自动执行完整的实施闭环：
  Plan（需求理解）→ Do（代码实现）→ Test（构建验证）→ Act（自修复/调整）→ Commit（提交）。
  外循环（Self-Refinement）将实施中发现的错误模式沉淀为 Skill/Rule，实现持续进化。
  配合 solution-design-check（上游方案产出）和 build-verify（下游构建诊断）使用。
trigger_keywords:
  - 开始实施, 开始实现, 开始开发, 开始落地, 开始改代码
  - 实施, 实现, 开发, 落地, 方案落地, 落地实施
  - 代码修改, 改代码, 修Bug, 修bug, 修 bug, bug修复, Bug修复
  - implement, implement feature, fix bug, refactor
  - 功能开发, 开发功能, 实施计划, 实施需求, 开始改代码
  - 实施调度, 循环实施, 自动化实施
do_not_trigger:
  - simple questions
  - code review only
  - reading/analyzing without modification
  - documentation-only tasks
  - 方案设计阶段的讨论
  - solution-design-check 产出方案的过程
---

> ⭐ **项目级首选技能** | 同主题 user 级技能：`implementation-loop`
> **冲突时本技能优先**（遵循 rule-018 优先级：project > user > plugin）
> 本技能包含完整 PDTAC + Commit + Self-Refinement 双循环，比 user 级多 Commit 阶段和外循环沉淀

# 方案实施 — 循环工程实施闭环技能

## 概述

本技能实现完整的 **PDTAC + 内外双循环** 实施架构：

```
┌─────────────────────────────────────────────────────────────────────┐
│                外循环 (Meta Loop): Self-Refinement 沉淀                │
│                                                                     │
│  ┌─ R1: 识别错误模式 ─→ R2: 诊断根因 ─→ R3: 生成建议 ─→ R4: 持久化  │
│     (编译失败/规则违规)    (检索已有SKill)    (用户确认)    (Skill/Rule) │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │      内循环 (PDTAC): 一次一个修改点，Maker-Checker 分离        │   │
│  │                                                              │   │
│  │  Plan ──→ Do ──→ Test ──→ Act ──→ Commit                     │   │
│  │    │         │        │        │         │                    │   │
│  │  方案确认   代码修改   构建验证  自修复    提交               │   │
│  │    │         │        │        │         │                    │   │
│  │    └─── Maker Agent ─┤ Checker Agent ──┘                    │   │
│  │        (实施者)       (验证者)                                │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                                                                     │
│  STATE.md: 持久化状态，支持断点续传                                  │
│  Human Gate: 决策树 → 自动执行 / 升级给人                             │
└─────────────────────────────────────────────────────────────────────┘
```

### 设计参考来源

| 来源 | 贡献 | 应用位置 |
|------|------|---------|
| **agentic-engineering-framework** (GitHub) | 三层加载(L1/L2/L3)、Self-Refinement 外循环 | 元数据节 + Phase 0 |
| **loop-engineering/cobusgreyling** (210+ star) | Maker-Checker 分离、STATE.md、Human Gate | Phase 2/3 + Gate 节 |
| **PDTAC** (Plan-Do-Test-Act-Commit) | Commit 阶段、Check 变 Test | Phase 4 |
| **auto-rules/ychulin** (GitHub) | 规则自动沉淀机制 | Self-Refinement Phase |
| **Addy Osmani** | 就绪等级(L1→L2→L3) | 就绪等级节 |

---

## ⚠️ dev/ 目录读取强制约束

**`dev/` 被 `.gitignore` 第 174 行 `/dev/` 规则屏蔽。**
涉及 dev/ 下文件的操作（验证部署、查 DLL、扫 plugins/、读 ScriptRefs/）**必须**用 `execute_command` + PowerShell：

```powershell
Get-ChildItem -Path "d:\MyWork\SunEyeVision_V2-script\dev\Debug\data\ScriptRefs" -Filter "*.dll" -File
```

详细规范见 `rule-017: .gitignore 屏蔽目录的文件读取规范`。

---

## 加载模型（基于 agentic-engineering-framework 三层设计）

| 层级 | 内容 | 加载时机 | 约 Token 成本 |
|------|------|---------|:------------:|
| **L1** | YAML frontmatter（名称/描述/触发条件/Gate规则） | Agent 启动时自动加载 | ~200 |
| **L2** | SKILL.md 主体（完整的 PDCA+Refinement 工作流） | 触发关键词匹配后按需加载 | ~5k |
| **L3** | 具体资源文件（错误模式表、构建命令模板、差异报告模板） | 对应 Phase 执行时加载 | 按需 < 1k |

---

## 就绪等级（基于 loop-engineering L1→L2→L3 模型）

| 等级 | 模式 | 说明 | 进入条件 |
|:----:|------|------|---------|
| **L1** | Report | 报告模式：只分析问题并输出报告，不自动修复 | 新方案首次实施 |
| **L2** | Assist | 辅助修复：每次修改前先输出预览/建议，等待确认后执行 | 已验证过至少1次类似方案 |
| **L3** | Auto | 全自动：按方案直接实施，但保留 Human Gate 拦截点 | 相同方案类型已成功实施 ≥ 3 次 |

---

## 触发条件

| 条件 | 说明 |
|------|------|
| 用户明确要求 | "开始实施"、"开始改代码"、"落地"、"implement" |
| 已有方案设计 | 由 solution-design-check 产出或用户自行提供 |
| 方案已过审查 | 零兼容/报错优先/YAGNI/KISS/代码纯净原则已检查 |

若不满足则拒绝执行，要求补全前置条件。

---

## ━━━━ Phase 0: Setup（初始化）━━━━

### 进入条件
- 触发条件全部满足
- 方案设计已在对话上下文中

### 执行步骤

#### Step 0.1 — 读取/初始化 STATE.md

```powershell
$statePath = "d:\MyWork\SunEyeVision_V2-script\.codebuddy\skills\方案实施\STATE.md"
if (Test-Path $statePath) {
    # 恢复断点续传
    $state = [System.IO.File]::ReadAllText($statePath, [System.Text.UTF8Encoding]::new($false))
    # 解析已完成步骤，跳过
} else {
    # 创建新 STATE.md
}
```

STATE.md 格式：

```markdown
# 实施状态

## 方案信息
- 方案名称: {方案名称}
- 方案来源: solution-design-check / 用户提供
- 开始时间: {ISO 8601 时间}
- 就绪等级: L1(L2/L3)

## 修改清单
| # | 目标文件 | 操作 | 状态 | 前置依赖 |
|---|---------|------|------|---------|
| 1 | path/file.cs | 修改 | ✅ 完成 | - |
| 2 | path/file2.cs | 新增 | 🔄 进行中 | 1 |
| 3 | path/file3.cs | 修改 | ⏳ 待处理 | 1 |

## 错误记录
| 迭代 | 错误类型 | 修复方案 | 结果 |
|------|---------|---------|------|
| #1 | CS0246 | added using System.Linq | ✅ 通过 |

## Human Gate 记录
| 步骤 | 升级原因 | 用户决定 | 时间 |
|------|---------|---------|------|
| 步骤2 | 5次自修复未通过 | 用户指示跳过并手动修复 | {时间} |
```

#### Step 0.2 — 评估就绪等级

```
检查 STATE.md / 对话历史中相同方案类型的实施次数

├─ 从未实施过 → L1 Report 模式
├─ 已成功实施 1-2 次 → L2 Assist 模式
└─ 已成功实施 ≥ 3 次 → L3 Auto 模式
```

#### Step 0.3 — 确定 Human Gate 策略

| 场景 | L1 | L2 | L3 |
|------|:--:|:--:|:--:|
| 新文件创建 | 询问 | 询问 | 自动 |
| 删除文件 | 询问 | 询问 | 询问 |
| 修改已有代码 | 报告 | 预览后询问 | 自动 |
| 编译错误（第1次） | 报告 | 自动修复 | 自动修复 |
| 编译错误（≥3次同错误） | 报告 | 报告 + 升级给人 | 升级给人 |

### 退出条件
- [x] STATE.md 已初始化（或断点已恢复）
- [x] 就绪等级已确定
- [x] Human Gate 策略已确定
- [x] 已输出计划摘要

---

## ━━━━ Phase 1: Plan（需求理解）━━━━

### 进入条件
- Phase 0 完成

### 执行步骤

#### Step 1.1 — 检查方案完整性

```
检查方案文档是否存在
  ├─ 有 → Step 1.2
  └─ 无 → 要求提供方案报告，等待用户补充
          ❌ 禁止自行推理生成方案（YAGNI）
```

#### Step 1.2 — 提取修改清单

从方案中提取结构化修改清单：

| 字段 | 说明 |
|------|------|
| 修改点 | 方案编号（如 P0、P1） |
| 目标文件 | 绝对路径 |
| 变更类型 | 修改/新增/删除/重命名 |
| 核心变更 | 一句话描述要改什么 |
| 前置依赖 | 此修改前必须先做的步骤 |

#### Step 1.3 — 上下文确认（关键！差异检测）

对每个修改点，做 3 项上下文检查：

```
for each 修改点:
  1. 目标文件是否存在？
     ├─ 是 → 检查文件内容与方案描述是否匹配
     └─ 否 → 报告差异，等待用户指示

  2. 涉及的类/方法/接口签名是否匹配？
     ├─ 是 → 继续
     └─ 否 → 报告差异（给出新旧对比），等待用户指示

  3. 前置依赖文件是否已修改到位？
     ├─ 是 → 标记为"就绪"
     └─ 否 → 标记为"等待前置"
```

**差异报告模板**：

```
⚠️ 上下文差异发现：
  方案描述：在 ScriptTool.cs 第45行修改 getGlobal 委托
  实际代码：getGlobal 在第38行，且签名 Func<string, object?> 与方案一致
                    ↓
  已更新偏移量，按实际代码实施
```

#### Step 1.4 — 输出计划摘要

```markdown
## 实施计划

| 步骤 | 文件 | 操作 | 前置步骤 |
|------|------|------|----------|
| 1 | tools/.../ScriptTool.cs | 修改 | 无 |
| 2 | tools/.../ScriptBindingResolver.cs | 修改 | 1 |
| 3 | tools/.../ScriptParameters.cs | 修改 | 1 |

预计改动: 3 个文件
构建验证: 每步后单 csproj 编译
就绪等级: L2 Assist（同类型第2次实施）

### 差异检测结果
- ✅ step 1: 上下文完全匹配
- ✅ step 2: 上下文完全匹配
- ⚠️ step 3: 第42行预期 → 实际第38行,已调整
```

### 退出条件
- [x] 所有修改点的上下文已确认
- [x] 差异已报告、用户已回应
- [x] 计划摘要已输出
- [x] STATE.md 已更新修改清单

---

## ━━━━ Phase 2: Do（代码实现 — Maker Agent）━━━━

### 进入条件
- Phase 1 已输出计划摘要
- 用户未提出异议（L2/L3 默认执行，L1 需确认）

### 执行规范

每次只实现**一个修改点**，遵循 4 步原子操作：

```
Step 2.A: Read  — 读取目标文件当前内容
Step 2.B: Verify — 确认上下文与方案描述一致（同 Step 1.3）
Step 2.C: Write  — 执行修改（replace_in_file 优先）
Step 2.D: Preview — 输出修改预览（L1/L2 需等待确认，L3 自动继续）
```

### 写入工具优先级

| 优先级 | 工具 | 适用场景 |
|--------|------|---------|
| 1 | `replace_in_file` | 精确匹配上下文替换（原子操作，推荐） |
| 2 | `write_to_file` | 整文件替换/创建新文件 |
| 3 | PowerShell `WriteAllText` + UTF-8 无 BOM | 前两种均出现中文乱码时 |

### 中文乱码修复流程

```
write_to_file 写入
       ↓
验收检查：文件中的中文是否显示正常？
  ├─ 正常 → 完成 ✓
  └─ 乱码 → 第1次修复：重新用 write_to_file 写入
                  ↓
              验收检查
              ├─ 正常 → 完成 ✓
              └─ 仍乱码 → 改用 PowerShell WriteAllText + UTF-8 无 BOM
```

注意：
- `write_to_file` 的编码行为受 IDE 后端环境影响，大部分场景已正确输出 UTF-8。
- 仅当出现乱码时才走修复流程，无需提前规避。
- **禁止**使用 `echo` 命令输出中文（控制台编码问题）。

### 修改预览模板（L1/L2 时输出，等待确认）

```diff
--- a/tools/.../ScriptTool.cs
+++ b/tools/.../ScriptTool.cs
@@ -45,7 +45,7 @@
-    private Func<string, object?>? _getGlobal;
+    private Func<string, object?> _getGlobal = key => {
+        return GlobalVariables.Instance.GetValue(key);
+    };
```

### 退出条件
- [x] 当前修改点已实施
- [x] 修改预览已输出（L1/L2 已确认，L3 已自动继续）
- [x] STATE.md 已更新该修改点的状态为"进行中"

---

## ━━━━ Phase 3: Test（验证 — Checker Agent）━━━━

### 进入条件
- 当前修改点已完成代码修改
- 进入条件明确为**验证者视角**：不参与代码修改，只验证
- [强制] 必须先执行 Step 3.0.A 提取验收标准
  ├─ 有验收标准 → 进入 Step 3.0.B
  └─ 无验收标准 → **立即终止实施**（见 Step 3.0.A 终止流程）

### 3.0 业务验证（新增）

**位置：** 在 Phase 2 代码修改完成后、Phase 3.1 编译验证之前

**核心目的：** 验证代码修改是否达成了方案中定义的业务目标，而非仅检查代码编译通过。

#### Step 3.0.A — 提取验收标准（强制约束）

```
从方案文档中提取"验收标准"章节的全部条目
  ├─ 有验收标准 → 进入 Step 3.0.B
  └─ 无验收标准 → **立即终止实施**
      1. 输出报告："方案缺少验收标准章节，无法进行业务验证"
      2. 通知上游：要求 solution-design-check 补充验收标准后重新进入实施
      3. 将 STATE.md 状态标记为 "⏸ 暂停 - 等待方案补充验收标准"
      4. 跳转到 Phase 5 生成暂停报告（不执行 Commit）
```

#### Step 3.0.B — 生成验证手段

根据验收类型选择验证方式：

| 验收类型 | 验证手段 | 示例 |
|---------|---------|------|
| **接口/API** | 代码审查断言 | 检查方法签名、参数类型、返回值与方案一致 |
| **正向行为** | 最小脚本/代码片段执行 | 写 csx 脚本调用新 API，验证输出 |
| **边界/异常** | 代码审查 + 人工确认 | 检查 null/空/非法输入是否有保护 |
| **数据一致性** | 序列化往返检查 | 验证 `Serialize(Deserialize(x)) == x` |
| **纯重构** | 仅代码审查，标注无行为变化 | 检查重构后调用链、输出类型不变 |

#### Step 3.0.C — 输出业务验证报告

在进入编译验证前，输出业务验证结果：

```markdown
### 业务验证报告

| 验收条目 | 验证方式 | 结果 |
|---------|---------|------|
| 功能 1: setGlobal 写回后 getGlobal 可读取 | csx 脚本执行 | ✅ 通过 |
| 边界 1: setGlobal 传 null | 代码审查 | ✅ 通过（ValueOrNull 处理） |
| 数据 1: Mat 类型序列化往返 | 代码审查 + 示例测试 | ⚠️ 需手动确认 |

验证结论：
- ✅ {N} 条自动通过
- ⚠️ {M} 条需手动确认（列在注意事项中）
- ❌ {K} 条不通过 → 打回 Phase 2 修复
```

### 3.1 单步验证

每个修改点完成后立即验证（快速反馈）：

```powershell
# 编译受影响的项目（单 csproj）
dotnet build "d:\MyWork\SunEyeVision_V2-script\tools\受影响的工具\SunEyeVision.Tool.XXX.csproj" `
  --configuration Debug -v:minimal -nologo 2>&1 |
  Out-File -FilePath "impl_verify.log" -Encoding utf8

# 读取日志
[System.IO.File]::ReadAllText(
  "d:\MyWork\SunEyeVision_V2-script\impl_verify.log",
  [System.Text.UTF8Encoding]::new($false))
```

### 3.2 集成验证

所有修改点完成后（完整方案编译）：

```powershell
dotnet build "d:\MyWork\SunEyeVision_V2-script\SunEyeVision.slnx" `
  --configuration Debug -v:minimal -nologo 2>&1 |
  Out-File -FilePath "impl_verify.log" -Encoding utf8
```

### 3.3 错误模式识别表

| 错误码 | 典型信息 | 根因 | 修复策略 |
|--------|---------|------|---------|
| **CS0246** | `The type or namespace name 'X' could not be found` | 缺少 using / 引用 | 1. 加 using 2. 检查 csproj Reference |
| **CS1061** | `'X' does not contain a definition for 'Y'` | 方法名不对/不存在 | 1. 拼写检查 2. 检查签名 3. 多层对象要步进 |
| **CS7036** | `No overload for method 'X' takes N arguments` | 参数个数不匹配 | 1. 检查签名参数表 2. 可能缺 out 参数 |
| **CS1503** | `Cannot convert from 'A' to 'B'` | 类型转换错误 | 1. 强转 2. 拆箱 3. 用 as/is |
| **CS0117** | `'X' does not contain a definition for 'Y'` | 静态类名错/方法不存在 | 1. 检查类名 2. 检查方法静态与否 |
| **CS0103** | `The name 'X' does not exist in the current context` | 变量未定义/拼写错误 | 1. 检查声明 2. 检查作用域 |
| **CS0104** | `'X' is an ambiguous reference` | 多个命名空间都有同名类型 | 使用 fully qualified name |
| **CS0618** | `'X' is obsolete` | 使用了废弃 API | 查找替代 API，不保留 [Obsolete] 用法 |
| **CS1001** | `Identifier expected` | 语法错误 | 1. 检查拼写 2. 检查括号匹配 |
| **MSBuild 路径错** | `The specified path is not a valid file name` | HintPath 路径不对 | 1. 检查路径存在 2. 可能是相对路径起点错 |
| **MSBuild 部署错** | OutDir 不正确或 plugins/ 污染 | 见 build-verify 技能 | 检查 Directory.Build.targets |

### 3.4 自修复循环

```
编译错误 → 分析错误模式 → 找到根因 → 修复
                                         ↓
                                      重新编译
                                         ↓
                                      ┌── 通过 → 继续下一步
                                      └── 错误 → 重复（最多5次）
                                                         ↓
                                                      5次未通过 → 触发 Human Gate
```

**自修复约束**：
- 每次修复后必须重新编译
- 同一错误重复出现 ≥ 3 次 → 停止自修复，记录到错误记录，触发 Human Gate
- 区分 MSBuild 配置错误（参考 build-verify）与业务代码错误
- **禁止**吞错误继续推进
- **禁止**在 5 次循环后仍自动重试

### 3.5 Human Gate 触发

```python
if 自修复循环达到上限:
    # 收集所有遗留错误
    errors = parse_build_errors(log)
    
    # 输出 Human Gate 报告
    print(f"""
    ⛔ Human Gate 触发：自修复已耗尽（5次循环）
    
    遗留错误（{len(errors)} 个）：
    {errors_table}
    
    建议：
    - {诊断建议1}
    - {诊断建议2}
    
    请选择：
    1. 手动修复后继续
    2. 跳过此步骤继续下一步
    3. 终止实施
    """)
```

### 退出条件
- [x] 单步验证：当前修改点编译通过（0 error）
- [x] 集成验证（全部完成后）：`dotnet build` 0 error
- [x] 或 Human Gate 已触发、用户已回应
- [x] STATE.md 已更新错误记录

---

## ━━━━ Phase 4: Commit（提交）━━━━

### 进入条件
- 当前修改点 Test 通过（或 Human Gate 已放行）

### 执行步骤

#### Step 4.1 — 检查工作区状态

```powershell
git status --short
```

#### Step 4.2 — 提交修改

```powershell
git add -A
git commit -m "feat: {方案编号} - {核心变更描述}

{详细的修改说明，每行一个文件}

方案来源: solution-design-check
实施技能: 方案实施
就绪等级: L2 Assist"
```

Commit 消息规范（遵循 [Conventional Commits](https://www.conventionalcommits.org/)）：

| 类型 | 使用场景 |
|------|---------|
| `feat` | 新功能实施 |
| `fix` | Bug 修复 |
| `refactor` | 重构 |
| `perf` | 性能优化 |
| `chore` | 配置/构建相关 |

#### Step 4.3 — 回归验证（可选，L3 自动）

```powershell
# 快速检查：确认提交后的工作区干净
git status --short
```

### 退出条件
- [x] 当前修改点已提交
- [x] 工作区干净
- [x] STATE.md 已标记该修改点为"✅ 完成"
- [x] 有下一个修改点 → 返回 Phase 2
- [x] 无下一个修改点 → 进入 Phase 5

---

## ━━━━ Phase 5: Act（结果报告）━━━━

### 进入条件
- 所有修改点已完成 Phase 2→3→4 循环

### 报告模板

```markdown
## 实施完成报告

### 修改清单
| 文件 | 操作 | 说明 |
|------|------|------|
| tools/.../ScriptTool.cs | 修改 | 核心变更描述 |
| tools/.../ScriptBindingResolver.cs | 修改 | 核心变更描述 |
| tools/.../ScriptParameters.cs | 修改 | 核心变更描述 |

### 验证结果
- [x] dotnet build 通过（0 error, 0 warning）
- [x] 方案步骤 P0/P1/P2/... 全部完成
- [x] 无方案范围之外的额外修改（YAGNI）
- [x] 无 [Obsolete] / 条件编译 / 注释掉的代码残留

### 规则合规
- [x] rule-008 零兼容/报错优先 — 具体修改点
- [x] rule-015 禁止兜底回退 — 具体修改点
- [x] rule-003 日志规范 — 具体修改点
- [x] rule-013 编码规范 — 具体修改点

### 自修复统计
- 总循环次数: 3
- 错误模式: CS0246（缺 using）×1 → 自动修复
- Human Gate 触发: 0 次

### 注意事项
- ⚠️ 需要手动验证的事项（如有）
- 📌 文件路径变更说明（如有）
```

### 退出条件
- [x] 报告已输出
- [x] 用户已确认
- [x] 进入 Self-Refinement（Phase 0）

---

## ━━━━ Phase 0': Self-Refinement（外循环：学习沉淀）━━━━

### 进入条件
- 实施闭环完成
- 用户确认报告

### 核心目的
将实施过程中发现的**错误模式、修复经验、规则违规**持久化为可复用的 Skills/Rules，使技能持续进化。
基于 agentic-engineering-framework 的 Self-Refinement 机制 + auto-rules 的规则自动沉淀思想。

### 执行步骤

#### Step R1 — 从实施中提取可沉淀的知识

检查以下来源：

```
来源 A: 错误记录（自修复循环中记录的 CS 错误）
  ├─ 是否已有对应的 Skill/Rule 覆盖？
  ├─ 是 → 跳过
  └─ 否 → 进入 Step R2

来源 B: 上下文差异记录（Step 1.3 中发现方案与代码不符）
  ├─ 是否已更新方案？
  ├─ 是 → 跳过
  └─ 否 → 不要改方案！沉淀为"方案差异预警规则"

来源 C: 用户修正（用户纠正了 agent 的错误）
  ├─ 是否在现有 Skill/Rule 中有体现？
  ├─ 是 → 检查是否需要更新（表述不够清晰？）
  └─ 否 → 进入 Step R2

来源 D: 规则违规（构建验证通过，但 code review 发现违规）
  ├─ 对应 rule-XXX 是否已覆盖？
  ├─ 是 → 检查 agent 是否漏读了规则 → 更新触发条件
  └─ 否 → 进入 Step R2
```

#### Step R2 — 生成沉淀建议

```markdown
## Self-Refinement 建议

### 建议 1: 新增 Skill 规则
- **来源**: 错误记录 #2: CS0246 重复出现（缺 `using System.Linq`）
- **类型**: 错误模式规则
- **目标**: 下次遇到 LINQ 相关代码时自动补全 using
- **表达式**: `when 使用了 .Where()/.Select()/.Any() 且文件缺少 using System.Linq`
- **建议文件**: `.codebuddy/rules/.../auto-using-linq.mdc`

### 建议 2: 更新现有 Rule
- **来源**: 用户纠正：`HOperatorSet.FillUp` 的 out 变量命名不规范
- **目标**: rule-014 补充 out 变量命名规范
- **变更**: 在 rule-014 的示例中明确 out 变量命名模式：`out var result` / `out var filled`

### 建议 3: 更新本技能
- **来源**: 自修复循环中 "CS0104 ambigious reference" 耗时过长
- **目标**: 在错误模式表（3.3节）中加入 CS0104 及其修复策略
- **当前**: 已加入
```

#### Step R3 — 用户确认

```
输出 Self-Refinement 建议
  ├─ 用户确认 → Step R4: 持久化
  └─ 用户拒绝或修改 → 按用户要求调整后持久化

❌ 禁止：未经用户确认就自动创建/修改 Skill/Rule 文件
```

#### Step R4 — 持久化

```powershell
# 创建新 Skill/Rule 文件
[System.IO.File]::WriteAllText(
  "目标路径",
  $content,
  [System.Text.UTF8Encoding]::new($false)
)

# 更新 STATE.md
更新 Self-Refinement 状态为"✅ 已持久化"
```

### 退出条件
- [x] 所有可沉淀的知识已识别
- [x] 建议已输出且用户已确认
- [x] 已持久化为对应的 Skill/Rule
- [x] STATE.md 已记录本次 Refinement 历史
- [x] **STATE.md 重置**（下次实施从干净状态开始）

---

## 核心原则

1. **方案驱动** — 严格按已有方案实施，不自行增减范围（YAGNI）
2. **先读后改** — 修改前必须读取文件当前内容，确认上下文准确
3. **一次一个修改点** — 按方案步骤逐一修改，每步验证后再下一步
4. **Maker-Checker 分离** — 代码实现和验证由独立的逻辑完成
5. **构建验证闭环** — 每次修改后 `dotnet build`，最多自修复 5 次循环
6. **报错即停** — 发现方案与代码不符时，立即报告差异，不自行修正方案
7. **链路可追溯** — 每步修改都有摘要输出，STATE.md 记录所有状态
8. **持续进化** — 实施完成后的 Self-Refinement 使技能不断积累

## 禁止行为

- **禁止**超出方案范围自行添加功能（YAGNI）
- **禁止**在构建验证环节吞错误继续推进
- **禁止**修改前不读取文件当前内容
- **禁止**用 `Console.WriteLine` 输出中文（rule-013）
- **禁止**使用 `[Obsolete]` / 条件编译 / 注释保留旧代码
- **禁止**在 5 次自修复失败后仍自动重试
- **禁止**未经用户确认自动创建/修改 Skill/Rule（Self-Refinement）

## Human Gate 决策树

```
当前步骤是否自动执行？
  ├─ L1 → 所有修改都需要用户确认
  ├─ L2 → 已有预览确认的修改自动执行，创建/删除文件需确认
  └─ L3 → 自动执行，但以下场景必须升级：
       ├─ 删除已有文件
       ├─ 同一错误自修复 ≥ 3 次
       └─ 5 次自修复循环耗尽

编译验证是否升级为人？
  ├─ 0 error → 自动继续
  ├─ 1-2 error → L1 报告/L2-L3 自动修复
  ├─ 3+ error → 总是升级为人（可能是方案级问题）
  └─ 5 次循环后 → 总是升级为人
```

## 与其他技能的协作关系

```
solution-design-check
        │
        ▼
   方案实施 (本技能)  ←── build-verify (MSBuild 错误辅助诊断)
        │
        ├── code-legacy-cleanup (如需清理废弃代码)
        ├── vision-tool-builder (如需创建新视觉工具)
        └── wpf-vision-dev (如需 WPF 界面开发)
        │
        ▼
   Self-Refinement → 更新 Skills/Rules
```

## 验证清单

所有实施完成后**必须**逐项确认：

- [ ] 所有方案步骤已实施完成
- [ ] `dotnet build` 通过（0 error, 0 warning）
- [ ] 方案验收标准已逐条通过业务验证
- [ ] 无方案范围之外的额外修改（YAGNI）
- [ ] 无 `[Obsolete]` / 条件编译 / 注释掉的代码残留
- [ ] 无 `Console.WriteLine` / `Debug.WriteLine` 残留
- [ ] 无 `try-catch` 吞异常
- [ ] 修改符合项目所有编码规范
- [ ] Self-Refinement 建议已输出

## 参考

- [skill:solution-design-check] — 上游技能：产出带调研标注的方案
- [skill:build-verify] — 构建验证参考：MSBuild 错误分类、OutDir 验证
- [GitHub: agentic-engineering-framework/davidYichengWei] — Self-Refinement + 三层加载
- [GitHub: loop-engineering/cobusgreyling] — Maker-Checker 分离 + STATE.md + Human Gate
- [GitHub: auto-rules/ychulin] — 规则自动沉淀机制
- rule-002：命名规范
- rule-003：日志系统使用规范
- rule-008：开发与代码纯净原则
- rule-013：文件编码与写入规范
- rule-015：禁止兜底回退原则
- rule-016：MSBuild 项目配置规范
- rule-017：.gitignore 屏蔽目录的文件读取规范
