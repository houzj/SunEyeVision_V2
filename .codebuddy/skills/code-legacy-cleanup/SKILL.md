---
name: code-legacy-cleanup
description: 遗留代码清理技能。用于清理废弃代码、重命名过时概念、删除孤立类。触发场景：代码清理、架构迁移、概念重命名。
---

# 遗留代码清理技能

## 触发场景

- 清理废弃架构模式（如 Recipe → Solution 迁移残留）
- 重命名错误的命令/方法/属性
- 删除孤立或无引用的类
- 移除注释掉的代码块（>20行）
- 合并重复功能
- 参考锚点
- 参考架构

## 清理流程

### 第1步：全面分析

先搜索再动手，确认影响范围：

```
search_content pattern="旧概念名" outputMode="files_with_matches"
search_content pattern="class.*旧类名" outputMode="files_with_matches"
```

分类发现：
- **核心类** - 旧概念的直接定义
- **引用类** - 使用旧概念的类
- **UI层** - ViewModel、Command、XAML绑定
- **文档** - 注释和说明文件

### 第2步：安全评估

删除前必须确认：
- 零外部引用
- 无运行时依赖（序列化、反射）
- 新架构完全替代了功能

高风险场景需谨慎：
- 有外部引用的类
- 运行时关键路径依赖
- 公共API或序列化依赖

### 第3步：按风险等级执行

1. **零风险**：删除无引用的孤立类
2. **低风险**：重命名语义错误（SaveCurrentRecipeCommand → SaveCurrentSolutionCommand），更新日志消息
3. **低风险**：删除大块注释代码（>20行）
4. **中风险**：删除有引用但已废弃的功能

### 第4步：验证

```
read_lints paths="<modified-file>"
dotnet build SunEyeVision.sln
```

## 项目特定清理场景

### Recipe → Solution 迁移残留

搜索关键词：`Recipe`、`RecipeGroup`、`RecipeId`、`RecipeRef`、`RecipeGroupName`、`RecipeJsonConverter`

已知替换映射：
- `Recipe` → `Solution`
- `RecipeGroup` → `SolutionGroup`
- `SaveCurrentRecipeCommand` → `SaveCurrentSolutionCommand`
- `配方` → `解决方案`（日志消息中）

### 注释代码清理标准

- 删除 >20 行的注释代码块
- 删除引用已删除功能的 TODO
- 保留有价值的设计说明注释

## 代码锚点 — 架构分层

清理代码时必须判断影响范围，以下锚点标识关键架构边界：

| 层 | 关键入口 | 路径 | 说明 |
|---|---|---|---|
| SDK 核心 | `IToolPlugin` | `src/Plugin.SDK/Core/IToolPlugin.cs` | 工具接口，修改影响所有工具 |
| SDK 核心 | `ToolParameters` | `src/Plugin.SDK/Execution/Parameters/ToolParameters.cs` | 参数基类，修改影响序列化 |
| SDK 核心 | `ToolResults` | `src/Plugin.SDK/Execution/Results/ToolResults.cs` | 结果基类，修改影响序列化 |
| SDK 模型 | `ObservableObject` | `src/Plugin.SDK/Models/ObservableObject.cs` | 属性通知基类，修改影响全局 |
| SDK 控件 | `ToolDebugControlBase` | `src/Plugin.SDK/UI/Controls/ToolDebugControlBase.cs` | 调试控件基类，修改影响所有工具 UI |
| SDK 控件 | `ImageControl` | `src/Plugin.SDK/UI/Controls/ImageControl/ImageControl.xaml.cs` | 图像控件，修改影响画布和调试窗口 |
| 工作流 | `WorkflowNodeBase` | `src/Workflow/WorkflowNode.cs` | 节点模型，修改影响序列化和画布 |
| UI 层 | `ViewModelBase` | `src/UI/ViewModels/ViewModelBase.cs` | VM 基类，修改影响所有 ViewModel |

## 约束

- **遵循 rule-008 零兼容原则**：不考虑向后兼容，直接删除旧代码，不保留适配层
- **遵循 rule-008 报错优先原则**：数据不完整/格式不匹配直接报错，不静默处理不自动修复
- **禁止**使用 `[Obsolete]` 标记（直接删除）
- **禁止**使用条件编译（`#if DEBUG`）
- **禁止**保留回退机制、兼容层、注释掉的代码
- 使用 Git 做版本控制，不在代码中保留旧实现
