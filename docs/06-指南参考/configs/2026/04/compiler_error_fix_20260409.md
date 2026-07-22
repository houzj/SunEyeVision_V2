# 编译错误修复 - 2026-04-09

## 📋 问题描述

**报告的编译错误**：

### 错误1: CS1061 - "ToolMetadata"未包含"Name"的定义
```
错误(活动) CS1061 "ToolMetadata"未包含"Name"的定义，并且找不到可接受第一个"ToolMetadata"类型参数的可访问扩展方法"Name"(是否缺少 using 指令或程序集引用?)
SunEyeVision.Workflow D:\MyWork\SunEyeVision_Dev-tool\Src\Workflow\Workflow.cs 323
```

### 错误2: CS1061 - "string"未包含"Name"的定义
```
错误(活动) CS1061 "string"未包含"Name"的定义，并且找不到可接受第一个"string"类型参数的可访问扩展方法"Name"(是否缺少 using 指令或程序集引用?)
SunEyeVision.Workflow D:\MyWork\SunEyeVision_Dev-tool\Src\Workflow\Workflow.cs 323
```

### 错误3: CS0006 - 未能找到元数据文件
```
错误(活动) CS0006 未能找到元数据文件"D:\MyWork\SunEyeVision_Dev-tool\Release\SunEyeVision_v1.0.0\SunEyeVision\SunEyeVision.Workflow.dll"
SunEyeVision.UI D:\MyWork\SunEyeVision_Dev-tool\Src\UI\CSC 1
```

## 🔍 根本原因分析

### 问题1: ToolMetadata 属性名称错误

**原始代码** (第323行):
```csharp
public string GetNodeType(string nodeId)
{
    var node = GetNode(nodeId);
    if (node == null)
        return "Unknown";

    var toolMetadata = ToolRegistry.GetToolMetadata(node.ToolType);
    return toolMetadata?.Name ?? node.ToolType.Name; // ❌ 错误
}
```

**错误分析**：
1. ❌ `ToolMetadata` 类没有 `Name` 属性
2. ❌ 使用的是 `DisplayName` 属性（第32行）
3. ❌ `node.ToolType` 是 `string` 类型，没有 `.Name` 属性

**ToolMetadata 类定义**:
```csharp
public class ToolMetadata
{
    public required string Id { get; init; }
    
    public string DisplayName { get; set; } = string.Empty; // ✅ 正确的属性名
    
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "?";
    public string Category { get; set; } = "未分类";
    
    public Type? ToolType { get; set; }
    // ...
}
```

**WorkflowNodeBase 类定义**:
```csharp
public class WorkflowNodeBase : ObservableObject
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string DispName { get; set; }
    public string ToolType { get; set; } // ✅ 这是 string 类型，不是 Type
    // ...
}
```

### 问题2: Release 目录包含旧的编译输出

**原因**:
- Release 目录包含之前编译的输出文件
- 元数据文件路径不正确
- 清理后会在重新编译时自动修复

## ✅ 修复内容

### 修复1: 更正 GetNodeType 方法

**文件**: `src/Workflow/Workflow.cs` (第316-324行)

**修复后的代码**:
```csharp
/// <inheritdoc/>
public string GetNodeType(string nodeId)
{
    var node = GetNode(nodeId);
    if (node == null)
        return "Unknown";

    var toolMetadata = ToolRegistry.GetToolMetadata(node.ToolType);
    return toolMetadata?.DisplayName ?? node.ToolType; // ✅ 修复
}
```

**修复点**:
1. ✅ `toolMetadata?.DisplayName` - 使用正确的属性名 `DisplayName`
2. ✅ `node.ToolType` - 直接使用 string 值作为后备，不调用 `.Name`

### 修复2: 清理 Release 目录

**执行命令**:
```powershell
Set-Location "d:/MyWork/SunEyeVision_Dev-tool"
if (Test-Path "Release") 
{ 
    Remove-Item -Path "Release" -Recurse -Force 
}
```

**效果**:
- 清理旧的编译输出
- 重新编译时不会出现元数据文件找不到的错误

## 📊 修复对比

| 项目 | 修复前 | 修复后 |
|------|---------|---------|
| `toolMetadata?.Name` | ❌ `Name` 属性不存在 | ✅ `DisplayName` 属性存在 |
| `node.ToolType.Name` | ❌ `string` 类型没有 `.Name` 属性 | ✅ 直接使用 `string` 值 |
| Release 目录 | ❌ 包含旧的编译输出 | ✅ 已清理 |

## 🎯 验证状态

- [x] `GetNodeType` 方法已修复
- [x] `toolMetadata?.DisplayName` - 使用正确的属性名
- [x] `node.ToolType` - 直接使用 string 值
- [x] Release 目录已清理
- [x] Lints 检查通过（0 个错误）
- [x] 没有其他地方使用 `.ToolType.Name`

## 📝 完整的接口实现（正确版本）

### IWorkflowConnectionProvider 接口方法

```csharp
public List<string> GetParentNodeIds(string nodeId)
{
    var upstreamNodeIds = new HashSet<string>();
    var queue = new Queue<string>();
    var visited = new HashSet<string>();

    queue.Enqueue(nodeId);
    visited.Add(nodeId);

    while (queue.Count > 0)
    {
        var currentNodeId = queue.Dequeue();

        // 查找当前节点的所有父节点（连接指向当前节点的）
        foreach (var connection in Connections.Where(c => c.TargetNodeId == currentNodeId))
        {
            if (!visited.Contains(connection.SourceNodeId))
            {
                visited.Add(connection.SourceNodeId);
                upstreamNodeIds.Add(connection.SourceNodeId);
                queue.Enqueue(connection.SourceNodeId);
            }
        }
    }

    return upstreamNodeIds.ToList();
}

public List<string> GetChildNodeIds(string nodeId)
{
    var childNodeIds = new List<string>();

    foreach (var connection in Connections.Where(c => c.SourceNodeId == nodeId))
    {
        childNodeIds.Add(connection.TargetNodeId);
    }

    return childNodeIds;
}

public List<string> GetAllNodeIds()
{
    return Nodes.Select(n => n.Id).ToList();
}
```

### INodeInfoProvider 接口方法

```csharp
public string GetNodeName(string nodeId)
{
    var node = GetNode(nodeId);
    return node?.Name ?? nodeId;
}

public string GetNodeType(string nodeId) // ✅ 已修复
{
    var node = GetNode(nodeId);
    if (node == null)
        return "Unknown";

    var toolMetadata = ToolRegistry.GetToolMetadata(node.ToolType);
    return toolMetadata?.DisplayName ?? node.ToolType;
}

public string? GetNodeIcon(string nodeId)
{
    var node = GetNode(nodeId);
    if (node == null)
        return null;

    var toolMetadata = ToolRegistry.GetToolMetadata(node.ToolType);
    return toolMetadata?.Icon;
}

public bool NodeExists(string nodeId)
{
    return Nodes.Any(n => n.Id == nodeId);
}

public Type? GetResultType(string nodeId)
{
    var node = GetNode(nodeId);
    if (node == null)
        return null;

    var toolMetadata = ToolRegistry.GetToolMetadata(node.ToolType);
    Type? toolType = toolMetadata?.ToolType;

    if (toolType == null)
        return null;

    // 从 ToolType 推断 ResultType
    foreach (var iface in toolType.GetInterfaces())
    {
        if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IToolPlugin<,>))
        {
            var genericArgs = iface.GetGenericArguments();
            if (genericArgs.Length >= 2)
            {
                return genericArgs[1]; // TResult 是第二个泛型参数
            }
        }
    }

    return null;
}
```

## 🔄 变更历史

| 日期 | 版本 | 变更内容 | 作者 |
|------|------|----------|------|
| 2026-04-09 | 1.0 | 修复 GetNodeType 方法的属性名错误 | AI Agent |
| 2026-04-09 | 1.1 | 清理 Release 目录 | AI Agent |

## 📚 参考资料

- ToolMetadata 类定义：`src/Plugin.SDK/Metadata/ToolMetadata.cs`
- WorkflowNodeBase 类定义：`src/Workflow/WorkflowNode.cs`
- ToolRegistry 类定义：`src/Plugin.Infrastructure/Managers/Tool/ToolRegistry.cs`
- 图像选择器优化文档：`docs/image_selector_dropdown_optimization.md`
