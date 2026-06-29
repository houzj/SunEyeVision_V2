---
name: solution-design-check
description: >
  方案设计审查技能。用于确保每个方案设计之前先进行全面的代码信息调研，
  所有调研结论必须有可追溯的信息源标注（文件路径+行号），
  然后按照零兼容、报错优先、YAGNI、KISS、代码纯净原则进行合规审查。

  Trigger keywords: 方案/方案设计/架构方案/重构方案/优化方案/整合方案/
  技术方案/设计方案/实施计划/code review/代码审查/修改计划

  DO NOT trigger on: simple bug fixes, single-file edits, or routine coding tasks.
---

# Solution Design Check — 基于代码调研的方案设计审查

## Purpose

确保每个方案设计都经过三个必要阶段：

1. **信息调研 (Research)** — 出方案前必须全面搜索代码库，确保方案基于真实代码上下文
2. **源头标注 (Source Citation)** — 每个发现和决策必须标注来源位置，禁止无依据推测
3. **合规审查 (Compliance Check)** — 检查方案是否符合零兼容/报错优先/YAGNI/KISS/代码纯净原则

## Workflow — 三阶段方案设计流程

### ━━━━━ Phase 1: 信息调研 (Research) ━━━━━

在出任何方案之前，必须先用 [subagent:code-explorer] 全面搜索代码库。

#### 调研范围

| 调研维度 | 搜索方法 | 目的 |
|---------|---------|------|
| 涉及文件定位 | search_file / search_content | 确定修改波及的文件 |
| 接口/类型定义 | codebase_search | 找到类定义、接口签名、基类 |
| 调用链 | search_content 搜索调用方 | 确认修改的影响范围 |
| 配置/数据结构 | 搜索序列化、配置格式 | 了解数据格式和绑定方式 |
| 现有模式 | 搜索类似实现 | 复用现有设计模式 |

#### 调研记录模板

调研过程中必须记录如下格式的信息：

```markdown
## 调研记录

### 文件分布
| 文件 | 行号 | 角色 |
|------|------|------|
| path/to/file1.cs | 10-50 | 定义/调用/配置 |
| path/to/file2.cs | 80-120 | 引用/调用方 |

### 接口/类型定义
- [来源] path/to/IFoo.cs:15 - IFoo 接口定义，方法签名
- [来源] path/to/BarService.cs:42 - BarService 实现 IFoo

### 调用链
- [来源] path/to/Consumer.cs:88 - 调用 IFoo.ExecuteAsync()
- [来源] path/to/ViewModel.cs:120 - 通过 _barService 间接调用

### 配置/数据格式
- [来源] path/to/WorkflowJsonConverter.cs:22 - JSON 多态反序列化格式
- [来源] path/to/Recipe.cs:30 - Recipe 存储结构

### 现有模式参考
- [来源] path/to/ExistingTool.cs:55 - 类似功能的实现模式
```

> 调研要全面，不要只搜一个文件就停。必须找到：定义、实现、调用方、调用方的调用方。

### ━━━━━ Phase 2: 源头标注 (Source Citation) ━━━━━

方案报告中每个关键结论必须有可追溯的来源标注。

#### 标注规则

1. **每个发现点** 必须标注 [来源] 文件路径:行号
2. **禁止** 无依据的推断 — 如果找不到代码证据，标注为 [推测] 并说明原因
3. **引用格式**：
   - [来源] path/File.cs:行号 — 简要说明
   - [来源] path/File.cs:开始行-结束行 — 跨行范围
   - [推测] — 没有代码证据，基于...的推断
4. **标注位置**：方案报告中每个章节末尾，或内联在发现点后

#### 标注示例

```markdown
## 问题分析
- ScriptDebugControl 初始化耗时过长
  - [来源] ScriptDebugControl.xaml.cs:85-120 — 构造函数中间步调用 RebuildWorkspace
  - [来源] ScriptDebugControl.xaml.cs:200 — RebuildWorkspace 中调用 MefHostServices.Create()
- [推测] MEF 首次创建可能扫描了大量程序集（MefHostServices.Create 行为如此）

## 修改方案
- 将 RebuildWorkspace 改为异步调用
  - [来源] ScriptDebugControl.xaml.cs:201 — 调用点
  - [来源] ScriptEngine/ScriptSandbox.cs:55 — RebuildWorkspace 定义
  - [来源] ScriptEngine/ScriptCompiler.cs:30 — 内部调用链
```

### ━━━━━ Phase 3: 合规审查 (Compliance Check) ━━━━━

方案通过调研和标注后，检查是否符合项目设计原则。

#### 1. 零兼容 — 禁止回退/兜底/备用方案

- [ ] 方案是否只有一条正确路径，没有 "A 不行就 B"？
- [ ] 是否出现 "fallback"、"backup"、"兜底"、"revert to"、"otherwise try" 等关键词？
- [ ] 旧接口/类是否直接删除，而不是标记 `[Obsolete]`？
- [ ] 是否存在兼容层、适配器、桥接模式？
- [ ] 旧数据格式与新方案冲突时，是否直接报错拒绝，而不是自动转换？

#### 2. 报错优先 (Fail-Fast) — 禁止静默处理

- [ ] 数据不完整/类型不匹配/状态无效时是否直接抛异常？
- [ ] 是否有 `try-catch` 吞异常？删除。
- [ ] 是否有 null/空/默认值静默跳过问题？替换为 `throw`。
- [ ] 是否有 `if (result == null) return default;`？替换为 `throw`。

#### 3. YAGNI — 只实现当前需要的

- [ ] 每个类/方法/参数是否都有当前明确需求支撑？
- [ ] 是否有 "用户可能需要"、"将来可能用" 的假设？删除。
- [ ] 是否有不必要的抽象（单实现的接口、多余的基类）？删除。

#### 4. KISS — 保持简单

- [ ] 这是解决该问题的最简方式吗？
- [ ] 有过度设计吗？文件越少、类越少、行越少 = 越好。
- [ ] 方案是否复用了现有基础设施（日志、事件总线、ObservableObject、DataSourceQueryService）？

#### 5. 代码纯净

- [ ] 是否有 `[Obsolete]` 标记、条件编译（`#if`）、注释掉的代码？删除。
- [ ] 是否有未使用的类、方法、参数、字段？删除。
- [ ] 调研阶段是否有无依据的推测没标注为 [推测]？打回补充来源。

#### 6. 验收标准完整性 — 检查方案是否定义了"什么是正确"（硬性约束）

- [ ] **方案必须包含"验收标准"章节**
  - 缺失 → **合规审查不通过，不允许进入实施**
  - 注明：验收标准是判断方案是否实现的依据，不可省略
  - 见 Review process 第 4 条：验收标准不通过 = 整体审查不通过
- [ ] 验收标准是否覆盖以下维度（至少 1 条）？
  - 功能验收（正常流程）
  - 边界/异常验收（错误处理、非法输入）
  - 数据一致性验收（序列化往返、状态同步）
- [ ] 验收标准是否可验证？（避免"代码质量良好"类主观描述）
  - ✅ 正确：`调用 setGlobal("key", mat) 后，getGlobal("key") 返回同一 Mat 对象`
  - ❌ 不合格：`功能正常`、`性能良好`、`代码干净`
- [ ] 验收标准是否定义了具体验证手段？（而非仅性能数字/主观评价）
  - ✅ 正确：`Run 按钮点击后，用秒表测量 UI 响应延迟 < 50ms`
  - ❌ 不合格：`Run 按钮点击后 UI 不卡顿`
- [ ] 验收标准是否与方案中"修改清单"的变更点一一对应？
  - 每个修改点引起的功能变化，都应有一条对应的验收条目
  - 纯重构（不影响外部行为）可标注 `[重构] 行为无变化，不产生新验收标准`

## Implementation Rules

### Anti-patterns to immediately flag:

```csharp
// VIOLATION: fallback pattern
var result = PlanA();
if (result == null) result = PlanB();  // Reject this design
return result;

// CORRECT: single path
var result = SingleCorrectPath();  // Works in ALL scenarios
return result;
```

```csharp
// VIOLATION: silent fallback
try { result = DoSomething(); }
catch { result = defaultValue; }  // Reject

// CORRECT: let it throw
result = DoSomething();  // On failure, exception propagates naturally
```

### Source Citation Anti-patterns:

```markdown
// VIOLATION: unsourced claim（无依据断言）
ScriptDebugControl 性能瓶颈在 MEF 初始化。

// CORRECT: sourced finding（有来源的发现）
ScriptDebugControl 性能瓶颈在 MEF 初始化。
// [来源] ScriptDebugControl.xaml.cs:200 - MefHostServices.Create() 在 UI 线程同步调用
// [来源] ScriptSandbox.cs:55 - MefHostServices 实例缓存首次创建耗时约 3s
```

```markdown
// VIOLATION: vague reference（模糊引用）
参考了 WorkflowEngine 的实现。

// CORRECT: precise source（精确来源）
参考了 WorkflowEngine 的实现。
// [来源] WorkflowEngine.cs:25-60 - WorkflowEngine 初始化流程
// [来源] WorkflowEngine.cs:88 - RegisterTool 方法签名
```

### Review process:

1. **Phase 1**: 使用 code-explorer 全面搜索 — 文件、调用链、配置、现有模式
2. **Phase 2**: 每个发现点标注来源 — [来源] 文件:行号，无依据标注 [推测]
3. **Phase 3**: 逐条检查 6 条合规规则
4. **验收标准硬性门禁**（Gate Check）：
   - 第 6 条（验收标准完整性）不通过 → **合规审查整体不通过，不允许进入实施**
   - 方案必须发回补充验收标准章节，补充后重新审查
   - 禁止跳过此条直接进入实施阶段
5. 如果 Phase 3 发现 Phase 1/2 调研不足 — 打回补充调研，不继续推进

## References

- rule-008: 开发与代码纯净原则
- rule-015: 禁止兜底回退原则
- rule-018: Skill 优先级与选择规范
- [skill:方案实施] — 方案通过后，**必须**调用项目级 `.codebuddy/skills/方案实施/`（PDTAC + Commit + Self-Refinement）
  - **禁止**调用 user 级 `implementation-loop`（命名冲突，已被 rule-018 覆盖）
- solution-design-template.md — 方案报告格式模板（含调研记录表）

这些规则和模板在 `.codebuddy/rules/` 和 `.codebuddy/skills/` 目录中。

---

## 方案通过后下一步

**必须**调用 `.codebuddy/skills/方案实施/` （project 级），
**禁止**调用 `implementation-loop`（user 级，命名冲突已被 rule-018 覆盖）。
