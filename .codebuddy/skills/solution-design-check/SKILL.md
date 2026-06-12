---
name: solution-design-check
description: >
  This skill should be used when the user asks for a solution design, 
  architecture plan, refactoring plan, implementation plan, optimization 
  plan, or code improvement proposal. It enforces the project's zero-fallback, 
  fail-fast, YAGNI, KISS design principles (rule-008, rule-015).

  Trigger keywords: 方案/方案设计/架构方案/重构方案/优化方案/整合方案/
  技术方案/设计方案/实施计划/code review/代码审查/修改计划

  DO NOT trigger on: simple bug fixes, single-file edits, or routine coding tasks.
---

# Solution Design Check — rule-008 / rule-015 合规审查

## Purpose

When generating a solution design, every recommendation must be evaluated against the project's core design principles. This skill ensures no fallback/backup mechanisms are proposed, no silent error handling, no unnecessary abstractions.

## Checklist — Apply to every proposed change

### 1. 零兼容 — 禁止回退/兜底/备用方案

- [ ] Is the proposed solution a **single correct path**, not "A, and if A fails then B"?
- [ ] If you find yourself writing "fallback", "backup", "兜底", "revert to", "otherwise try" — **STOP**. Rethink the design so the primary path works correctly in all cases.
- [ ] Are old interfaces/classes being deleted, not marked `[Obsolete]`?
- [ ] Is there any compatibility layer, adapter, or bridge pattern? Delete it.
- [ ] Old data formats that conflict with the new design? **Reject them with an error**, don't auto-convert.

### 2. 报错优先 (Fail-Fast) — 禁止静默处理

- [ ] Does the code throw when data is incomplete, type mismatched, or state invalid?
- [ ] Any `try-catch` that swallows exceptions? Remove it.
- [ ] Any null/empty/default return value that silently skips a problem? Replace with exception.
- [ ] Any `if (result == null) return default;` or similar? Replace with `throw`.

### 3. YAGNI — 只实现当前需要的

- [ ] Is every class/method/parameter justified by a **current requirement**?
- [ ] Any "用户可能需要" or "将来可能用" assumptions? Remove them.
- [ ] Any unnecessary abstractions (interfaces with one implementation, extra base classes)? Remove them.

### 4. KISS — 保持简单

- [ ] Is this the simplest approach that solves the problem?
- [ ] Any over-engineering? Fewer files, fewer classes, fewer lines = better.
- [ ] Does the solution reuse existing infrastructure (logger, event bus, ObservableObject, DataSourceQueryService) instead of inventing new ones?

### 5. 代码纯净

- [ ] Any `[Obsolete]` attributes, conditional compilation (`#if`), commented-out code? Remove.
- [ ] Any unused classes, methods, parameters, fields? Remove.

## Implementation Rules

### Anti-patterns to immediately flag:

```csharp
// ❌ VIOLATION: fallback pattern
var result = PlanA();
if (result == null) result = PlanB();  // ← Reject this design
return result;

// ✅ CORRECT: single path
var result = SingleCorrectPath();  // Works in ALL scenarios
return result;
```

```csharp
// ❌ VIOLATION: silent fallback
try { result = DoSomething(); }
catch { result = defaultValue; }  // ← Reject

// ✅ CORRECT: let it throw
result = DoSomething();  // On failure, exception propagates naturally
```

### When reviewing a proposed change:

1. Read the proposed change carefully
2. For each code addition/modification, check against the 5 rules above
3. If ANY violation is found, reject the approach and redesign
4. Always prefer: **fix the primary path** rather than adding a secondary/safety path

## References

- rule-008: 开发与代码纯净原则
- rule-009: TBD (placeholder for future)
- rule-015: 禁止兜底回退原则

These rules are documented in `.codebuddy/rules/` directory.
