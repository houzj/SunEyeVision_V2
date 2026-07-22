# 设计时绑定优化方案实施总结

## 📋 问题背景

**问题描述**：图像阈值工具的图像源选择器下拉项为空，导致用户无法选择父节点的输出图像作为输入。

**根本原因**：`ParentNodeInfoExtensions.ExtractOutputProperties` 方法在节点未执行时直接返回，导致无法提取输出属性定义，违反了"设计时绑定"原则。

---

## 🎯 优化目标

实现**设计时绑定**功能：
- ✅ 节点未执行时，从工具元数据提取输出属性定义
- ✅ 节点已执行时，从执行结果提取输出属性（包含实际值）
- ✅ 用户在工作流设计阶段就能进行参数绑定，无需等待节点执行

---

## 📝 实施方案

### 1. ParentNodeInfo.cs 修改

**文件**：`src/Plugin.SDK/Execution/Parameters/ParentNodeInfo.cs`

**修改内容**：

#### 1.1 添加必要的 using 语句
```csharp
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Logging;
```

#### 1.2 修改 ExtractOutputProperties 方法
**原有逻辑**：
```csharp
public static void ExtractOutputProperties(this ParentNodeInfo nodeInfo, ToolResults? result)
{
    nodeInfo.LastResult = result;

    if (result == null)
        return;  // ❌ 提前返回，节点未执行时无法提取属性

    // ... 提取逻辑
}
```

**新逻辑**：
```csharp
public static void ExtractOutputProperties(this ParentNodeInfo nodeInfo, ToolResults? result)
{
    nodeInfo.LastResult = result;

    if (result != null)
    {
        // ✅ 节点已执行：从执行结果中提取输出属性（包含实际值）
        nodeInfo.ExecutionStatus = result.Status;
        nodeInfo.ExecutionTimeMs = result.ExecutionTimeMs;
        nodeInfo.ErrorMessage = result.ErrorMessage;

        var resultItems = result.GetResultItems();
        foreach (var item in resultItems)
        {
            var dataSource = new AvailableDataSource
            {
                SourceNodeId = nodeInfo.NodeId,
                SourceNodeName = nodeInfo.NodeName,
                SourceNodeType = nodeInfo.NodeType,
                PropertyName = item.Name,
                DisplayName = item.DisplayName ?? item.Name,
                PropertyType = item.Value?.GetType() ?? typeof(object),
                CurrentValue = item.Value,
                Unit = item.Unit,
                Description = item.Description,
                GroupName = nodeInfo.NodeName
            };

            nodeInfo.OutputProperties.Add(dataSource);
        }

        VisionLogger.Instance.Log(LogLevel.Info,
            $"[ParentNodeInfoExtensions.ExtractOutputProperties] 从执行结果提取输出属性 - nodeId={nodeInfo.NodeId}, 属性数量={nodeInfo.OutputProperties.Count}",
            "ParentNodeInfoExtensions");
    }
    else
    {
        // ✅ 节点未执行：从工具元数据中提取输出属性定义（不包含值）
        VisionLogger.Instance.Log(LogLevel.Info,
            $"[ParentNodeInfoExtensions.ExtractOutputProperties] 节点未执行，从工具元数据提取输出属性定义 - nodeId={nodeInfo.NodeId}, nodeType={nodeInfo.NodeType}",
            "ParentNodeInfoExtensions");

        // 从工具注册表获取输出属性定义
        var toolProperties = ToolRegistry.GetToolOutputProperties(nodeInfo.NodeType, nodeInfo.NodeId);
        nodeInfo.OutputProperties.AddRange(toolProperties);

        // 更新节点名称
        foreach (var prop in nodeInfo.OutputProperties)
        {
            prop.SourceNodeName = nodeInfo.NodeName;
            prop.GroupName = nodeInfo.NodeName;
        }

        VisionLogger.Instance.Log(LogLevel.Success,
            $"[ParentNodeInfoExtensions.ExtractOutputProperties] 从元数据提取输出属性成功 - nodeId={nodeInfo.NodeId}, 属性数量={nodeInfo.OutputProperties.Count}",
            "ParentNodeInfoExtensions");
    }
}
```

---

### 2. DataSourceQueryService.cs 修改

**文件**：`src/Plugin.SDK/Execution/Parameters/DataSourceQueryService.cs`

**修改内容**：

#### 2.1 添加必要的 using 语句
```csharp
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Logging;
```

#### 2.2 添加 ExtractPropertiesFromResult 方法
```csharp
/// <summary>
/// 从执行结果中提取输出属性
/// </summary>
private List<AvailableDataSource> ExtractPropertiesFromResult(ToolResults result, string nodeId)
{
    var properties = new List<AvailableDataSource>();

    string nodeName = _nodeInfoProvider?.GetNodeName(nodeId) ?? nodeId;
    string nodeType = _nodeInfoProvider?.GetNodeType(nodeId) ?? "Unknown";

    var resultItems = result.GetResultItems();
    foreach (var item in resultItems)
    {
        var dataSource = new AvailableDataSource
        {
            SourceNodeId = nodeId,
            SourceNodeName = nodeName,
            SourceNodeType = nodeType,
            PropertyName = item.Name,
            DisplayName = item.DisplayName ?? item.Name,
            PropertyType = item.Value?.GetType() ?? typeof(object),
            CurrentValue = item.Value,
            Unit = item.Unit,
            Description = item.Description,
            GroupName = nodeName
        };

        properties.Add(dataSource);
    }

    return properties;
}
```

#### 2.3 添加 ExtractPropertiesFromMetadata 方法
```csharp
/// <summary>
/// 从工具元数据中提取输出属性定义（节点未执行时）
/// </summary>
private List<AvailableDataSource> ExtractPropertiesFromMetadata(string nodeId)
{
    var properties = new List<AvailableDataSource>();

    string nodeName = _nodeInfoProvider?.GetNodeName(nodeId) ?? nodeId;
    string nodeType = _nodeInfoProvider?.GetNodeType(nodeId) ?? "Unknown";
    string? nodeIcon = _nodeInfoProvider?.GetNodeIcon(nodeId);

    // 从工具注册表获取输出属性定义
    var toolProperties = ToolRegistry.GetToolOutputProperties(nodeType, nodeId);
    properties.AddRange(toolProperties);

    // 更新节点名称
    foreach (var prop in properties)
    {
        prop.SourceNodeName = nodeName;
        prop.GroupName = nodeName;
    }

    VisionLogger.Instance.Log(LogLevel.Info,
        $"[DataSourceQueryService.ExtractPropertiesFromMetadata] 从元数据提取输出属性 - nodeId={nodeId}, nodeType={nodeType}, 属性数量={properties.Count}",
        "DataSourceQueryService");

    return properties;
}
```

#### 2.4 修改 CreateParentNodeInfo 方法
**原有逻辑**：
```csharp
private ParentNodeInfo CreateParentNodeInfo(string nodeId, int order)
{
    // ... 创建 nodeInfo

    var result = GetNodeResult(nodeId);
    if (result != null)
    {
        nodeInfo.ExecutionStatus = result.Status;
        nodeInfo.ExecutionTimeMs = result.ExecutionTimeMs;
        nodeInfo.ErrorMessage = result.ErrorMessage;

        // 提取输出属性
        nodeInfo.ExtractOutputProperties(result);
    }

    return nodeInfo;
}
```

**新逻辑**：
```csharp
private ParentNodeInfo CreateParentNodeInfo(string nodeId, int order)
{
    // ... 创建 nodeInfo

    // 获取执行结果（可能为null）
    var result = GetNodeResult(nodeId);

    // ✅ 统一调用 ExtractOutputProperties，内部会根据 result 是否为null自动选择提取路径
    nodeInfo.ExtractOutputProperties(result);

    return nodeInfo;
}
```

#### 2.5 修改 GetNodeOutputProperties 方法
**原有逻辑**：
```csharp
public List<AvailableDataSource> GetNodeOutputProperties(string parentNodeId)
{
    var properties = new List<AvailableDataSource>();

    var result = GetNodeResult(parentNodeId);
    if (result == null)
    {
        // ❌ 返回空列表
        return properties;
    }

    // 提取属性
    return ExtractPropertiesFromResult(result, parentNodeId);
}
```

**新逻辑**：
```csharp
public List<AvailableDataSource> GetNodeOutputProperties(string parentNodeId)
{
    var properties = new List<AvailableDataSource>();

    // 获取节点结果
    var result = GetNodeResult(parentNodeId);
    if (result != null)
    {
        // ✅ 从执行结果中提取属性
        return ExtractPropertiesFromResult(result, parentNodeId);
    }

    // ✅ 节点未执行，尝试从工具元数据提取
    if (_nodeInfoProvider != null)
    {
        return ExtractPropertiesFromMetadata(parentNodeId);
    }

    // 返回空属性列表
    return properties;
}
```

---

### 3. ToolRegistry.cs 修改

**文件**：`src/Plugin.Infrastructure/Managers/Tool/ToolRegistry.cs`

**修改内容**：

#### 3.1 添加 GetToolOutputProperties 方法
```csharp
/// <summary>
/// 获取工具的输出属性定义（无需执行）
/// </summary>
/// <param name="toolId">工具ID</param>
/// <param name="nodeId">节点ID（用于填充数据源信息）</param>
/// <returns>输出属性列表</returns>
public static List<AvailableDataSource> GetToolOutputProperties(string toolId, string nodeId)
{
    var properties = new List<AvailableDataSource>();

    // 获取工具实例
    var tool = CreateToolInstance(toolId);
    if (tool == null || tool.ResultType == null)
    {
        VisionLogger.Instance.Log(LogLevel.Warning,
            $"[ToolRegistry.GetToolOutputProperties] 工具实例或结果类型为null - toolId={toolId}",
            "ToolRegistry");
        return properties;
    }

    // 创建空的结果实例
    ToolResults? result;
    try
    {
        result = (ToolResults?)Activator.CreateInstance(tool.ResultType);
    }
    catch (Exception ex)
    {
        VisionLogger.Instance.Log(LogLevel.Warning,
            $"[ToolRegistry.GetToolOutputProperties] 创建结果实例失败 - toolId={toolId}, ResultType={tool.ResultType.FullName}, 错误={ex.Message}",
            "ToolRegistry");
        return properties;
    }

    if (result == null)
    {
        VisionLogger.Instance.Log(LogLevel.Warning,
            $"[ToolRegistry.GetToolOutputProperties] 结果实例创建失败 - toolId={toolId}",
            "ToolRegistry");
        return properties;
    }

    // 获取结果项（属性定义）
    var resultItems = result.GetResultItems();
    foreach (var item in resultItems)
    {
        var dataSource = new AvailableDataSource
        {
            SourceNodeId = nodeId,
            SourceNodeName = nodeId,  // 将由调用方覆盖
            SourceNodeType = toolId,
            PropertyName = item.Name,
            DisplayName = item.DisplayName ?? item.Name,
            PropertyType = item.Value?.GetType() ?? typeof(object),
            CurrentValue = item.Value,  // 为空
            Unit = item.Unit,
            Description = item.Description,
            GroupName = toolId
        };

        properties.Add(dataSource);
    }

    VisionLogger.Instance.Log(LogLevel.Info,
        $"[ToolRegistry.GetToolOutputProperties] 成功提取输出属性 - toolId={toolId}, 属性数量={properties.Count}",
        "ToolRegistry");

    return properties;
}
```

---

## 🔄 工作流程

### 场景1：节点未执行（设计时绑定）

1. 用户在工作流设计器中连接两个节点
2. 打开阈值工具的属性面板
3. 系统调用 `GetParentNodes()` 获取父节点信息
4. `CreateParentNodeInfo()` 被调用
5. `GetNodeResult(nodeId)` 返回 `null`（节点未执行）
6. `nodeInfo.ExtractOutputProperties(null)` 被调用
7. 由于 `result == null`，进入 else 分支
8. 调用 `ToolRegistry.GetToolOutputProperties()` 从工具元数据提取输出属性定义
9. 创建空的 `ToolResults` 实例
10. 调用 `GetResultItems()` 获取属性定义
11. 填充 `AvailableDataSource` 列表（不包含实际值，只有属性定义）
12. **结果**：下拉列表显示可用的输出属性，用户可以选择绑定

### 场景2：节点已执行（运行时绑定）

1. 用户执行工作流
2. 父节点执行完成，结果被缓存
3. `GetNodeResult(nodeId)` 返回实际的 `ToolResults` 实例
4. `nodeInfo.ExtractOutputProperties(result)` 被调用
5. 由于 `result != null`，进入 if 分支
6. 从执行结果中提取输出属性（包含实际值）
7. 填充 `AvailableDataSource` 列表（包含实际值）
8. **结果**：下拉列表显示可用的输出属性及其当前值

---

## 📊 修改文件汇总

| 文件 | 修改类型 | 修改内容 |
|------|---------|---------|
| `src/Plugin.SDK/Execution/Parameters/ParentNodeInfo.cs` | 🔴 关键 | 修改 ExtractOutputProperties 方法，添加 else 分支 |
| `src/Plugin.SDK/Execution/Parameters/DataSourceQueryService.cs` | 🔴 关键 | 添加 ExtractPropertiesFromResult 和 ExtractPropertiesFromMetadata 方法，简化 CreateParentNodeInfo 方法 |
| `src/Plugin.Infrastructure/Managers/Tool/ToolRegistry.cs` | 🔴 关键 | 添加 GetToolOutputProperties 方法 |

---

## ✅ 验证要点

### 功能验证
- [ ] 节点未执行时，下拉列表显示输出属性定义
- [ ] 节点已执行时，下拉列表显示输出属性及其值
- [ ] 用户可以在设计阶段进行参数绑定
- [ ] 绑定后正确保存到配置文件

### 日志验证
- [ ] 节点未执行时，日志显示"从工具元数据提取输出属性定义"
- [ ] 节点已执行时，日志显示"从执行结果提取输出属性"
- [ ] 日志显示提取的属性数量

### 代码质量
- [ ] 无编译错误
- [ ] 无 linter 警告
- [ ] 遵循命名规范
- [ ] 添加了充分的注释

---

## 🎯 预期效果

### 用户体验
1. **设计阶段**：用户可以在工作流设计器中自由连接节点并配置参数绑定，无需等待节点执行
2. **直观反馈**：下拉列表显示可用的输出属性，清楚标识属性名称和类型
3. **无缝切换**：节点执行后，下拉列表自动更新为包含实际值的版本

### 技术优势
1. **解耦**：UI 层与执行层解耦，设计时不依赖执行状态
2. **复用**：工具元数据既用于设计时，也用于运行时
3. **可维护性**：清晰的代码结构，易于理解和扩展

---

## 📚 相关规则

- **[rule-003: 日志系统使用规范](./docs/rules/01-coding-standards/logging-system.mdc)**：使用 VisionLogger 记录日志
- **[rule-002: 命名规范](./docs/rules/01-coding-standards/naming-conventions.mdc)**：遵循命名规范
- **[rule-004: 方案设计要求](./docs/rules/02-development-process/solution-design.mdc)**：完整的方案设计

---

## 🔄 变更历史

| 日期 | 版本 | 变更内容 | 作者 |
|------|------|----------|------|
| 2026-04-09 | 1.0 | 初始版本，实施设计时绑定优化方案 | AI Assistant |

---

## 💡 后续优化建议

1. **性能优化**：考虑缓存工具元数据提取结果，避免重复创建实例
2. **扩展性**：支持自定义属性过滤器，允许用户选择显示哪些属性
3. **UI 增强**：在属性面板中显示节点的执行状态（已执行/未执行）
4. **错误处理**：当工具元数据提取失败时，显示友好的错误提示

---

## 📞 联系方式

如有问题或建议，请联系开发团队。

---

## 🔄 合并记录

### 合并日期
2026-04-11

### 合并分支
- 源分支: eature/tool-improvement
- 目标分支: main

### 合并提交
- Commit ID: 7dc17f3
- 提交信息: "Merge branch 'feature/tool-improvement' into main"

### 合并内容
- 完全重构数据源管理（查询、绑定、选择）
- 实现基于变量池的设计时绑定框架
- 优化 ThresholdTool 参数系统
- 新增日志面板，移除属性面板
- 完善开发规范文档

### 冲突解决
所有冲突文件均使用工具分支的版本（工具分支的重构更先进）：
- src/Plugin.Infrastructure/Managers/Tool/ToolRegistry.cs
- src/Plugin.SDK/Execution/Parameters/DataSourceQueryService.cs
- src/Plugin.SDK/Execution/Parameters/ParentNodeInfo.cs
- src/UI/ViewModels/MainWindowViewModel.cs
- src/UI/ViewModels/WorkflowTabControlViewModel.cs
- src/Workflow/WorkflowEngine.cs

### 合并前提交
- Commit ID: c69443d
- 提交信息: "feat: 添加工具元数据提取支持（main分支）"

### 后续测试
- [ ] 编译测试
- [ ] 数据源查询功能测试
- [ ] 参数绑定功能测试
- [ ] 工具执行功能测试
- [ ] UI 功能验证

### 推送远程
等待测试完成后推送：git push origin main

---

**文档更新时间**: 2026-04-11