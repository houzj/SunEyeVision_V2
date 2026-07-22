# 图像选择器下拉项优化 - 实施完成

## 📋 问题描述

**原始问题**：图像选择器的下拉项只显示直接父节点的输出，无法显示所有上游节点的输出。

**影响范围**：
- `DataSourceQueryService.cs` - 数据源查询服务
- `Workflow.cs` - 工作流核心类

## 🔍 根本原因分析

### 问题1：Workflow 类缺少接口实现
- ❌ `Workflow` 类没有实现 `IWorkflowConnectionProvider` 接口
- ❌ `Workflow` 类没有实现 `INodeInfoProvider` 接口
- ❌ 缺少所有必需的接口方法

### 问题2：GetParentNodeIds 方法缺失
- `DataSourceQueryService` 调用了 `_connectionProvider.GetParentNodeIds(nodeId)`
- 但 `Workflow` 类中没有实现该方法

## ✅ 解决方案（方案1：合并为单一方法）

### 设计决策

选择**方案1（合并）**，理由：
1. ✅ **简单**：只需要一个方法
2. ✅ **符合需求**：`DataSourceQueryService` 需要所有上游节点
3. ✅ **减少混淆**：不需要区分"直接父节点"和"所有上游节点"
4. ✅ **性能优化**：内部使用递归+哈希集，效率高

### 实施内容

#### 1. 让 Workflow 类实现接口

**文件**: `src/Workflow/Workflow.cs` (第27行)

**修改前**:
```csharp
public class Workflow : ObservableObject
{
```

**修改后**:
```csharp
public class Workflow : ObservableObject, IWorkflowConnectionProvider, INodeInfoProvider
{
```

#### 2. 添加 IWorkflowConnectionProvider 接口方法

**文件**: `src/Workflow/Workflow.cs` (第254-311行)

##### 2.1 GetParentNodeIds - 递归查找所有上游节点

```csharp
/// <inheritdoc/>
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
```

**关键点**：
- 使用 **BFS（广度优先搜索）**遍历连接图
- 使用 `HashSet` 去重，避免循环依赖
- 返回**所有上游节点**（多层递归），不只是直接父节点

##### 2.2 GetChildNodeIds - 获取直接子节点

```csharp
/// <inheritdoc/>
public List<string> GetChildNodeIds(string nodeId)
{
    var childNodeIds = new List<string>();

    foreach (var connection in Connections.Where(c => c.SourceNodeId == nodeId))
    {
        childNodeIds.Add(connection.TargetNodeId);
    }

    return childNodeIds;
}
```

##### 2.3 GetAllNodeIds - 获取所有节点ID

```csharp
/// <inheritdoc/>
public List<string> GetAllNodeIds()
{
    return Nodes.Select(n => n.Id).ToList();
}
```

#### 3. 添加 INodeInfoProvider 接口方法

**文件**: `src/Workflow/Workflow.cs` (第313-393行)

##### 3.1 GetNodeName - 获取节点名称

```csharp
/// <inheritdoc/>
public string GetNodeName(string nodeId)
{
    var node = GetNode(nodeId);
    return node?.Name ?? nodeId;
}
```

##### 3.2 GetNodeType - 获取节点类型

```csharp
/// <inheritdoc/>
public string GetNodeType(string nodeId)
{
    var node = GetNode(nodeId);
    if (node == null)
        return "Unknown";

    var toolMetadata = ToolRegistry.GetToolMetadata(node.ToolType);
    return toolMetadata?.Name ?? node.ToolType.Name;
}
```

##### 3.3 GetNodeIcon - 获取节点图标

```csharp
/// <inheritdoc/>
public string? GetNodeIcon(string nodeId)
{
    var node = GetNode(nodeId);
    if (node == null)
        return null;

    var toolMetadata = ToolRegistry.GetToolMetadata(node.ToolType);
    return toolMetadata?.Icon;
}
```

##### 3.4 NodeExists - 检查节点是否存在

```csharp
/// <inheritdoc/>
public bool NodeExists(string nodeId)
{
    return Nodes.Any(n => n.Id == nodeId);
}
```

##### 3.5 GetResultType - 获取节点结果类型

```csharp
/// <inheritdoc/>
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

**关键点**：
- 通过反射从 `IToolPlugin<TParams, TResult>` 接口提取 `TResult` 类型
- 支持设计时推断输出属性类型

## 📊 接口方法对比

| 方法名 | 功能 | 是否递归 | 用途 |
|--------|------|----------|------|
| `GetParentNodeIds` | 获取所有上游节点（包括间接） | ✅ 是 | 显示所有数据源 |
| `GetChildNodeIds` | 获取直接子节点（一层） | ❌ 否 | 向下游导航 |
| `GetAllNodeIds` | 获取所有节点ID | ❌ 否 | 节点遍历 |
| `GetNodeName` | 获取节点名称 | ❌ 否 | UI 显示 |
| `GetNodeType` | 获取节点类型 | ❌ 否 | UI 显示 |
| `GetNodeIcon` | 获取节点图标 | ❌ 否 | UI 显示 |
| `NodeExists` | 检查节点是否存在 | ❌ 否 | 验证 |
| `GetResultType` | 获取结果类型 | ❌ 否 | 类型推断 |

## 🎯 预期效果

### 修改前
```
当前节点
├─ 父节点A ✅ 显示
└─ 父节点B
   ├─ 祖父节点A ❌ 不显示
   └─ 祖父节点B ❌ 不显示
```

**下拉列表**：[父节点A, 父节点B]

### 修改后
```
当前节点
├─ 父节点A ✅ 显示
└─ 父节点B
   ├─ 祖父节点A ✅ 显示
   └─ 祖父节点B ✅ 显示
```

**下拉列表**：[父节点A, 祖父节点A, 祖父节点B, 父节点B]

## 🔄 数据流

```
1. 用户点击图像选择器下拉框
   ↓
2. UI 调用 DataSourceQueryService.GetAvailableDataSources(nodeId)
   ↓
3. DataSourceQueryService 调用 _connectionProvider.GetParentNodeIds(nodeId)
   ↓
4. Workflow.GetParentNodeIds 递归查找所有上游节点
   ↓
5. 返回所有上游节点ID列表
   ↓
6. DataSourceQueryService 对每个节点调用 _nodeInfoProvider 方法获取信息
   ↓
7. 构建 AvailableDataSource 列表
   ↓
8. 返回给 UI 显示在下拉框中
```

## ✅ 验证清单

- [x] `Workflow` 类实现了 `IWorkflowConnectionProvider` 接口
- [x] `Workflow` 类实现了 `INodeInfoProvider` 接口
- [x] `GetParentNodeIds` 方法使用递归查找所有上游节点
- [x] `GetChildNodeIds` 方法获取直接子节点
- [x] `GetAllNodeIds` 方法获取所有节点ID
- [x] `GetNodeName` 方法获取节点名称
- [x] `GetNodeType` 方法获取节点类型
- [x] `GetNodeIcon` 方法获取节点图标
- [x] `NodeExists` 方法检查节点是否存在
- [x] `GetResultType` 方法获取节点结果类型
- [x] 没有编译错误（read_lints 检查通过）

## 📝 测试建议

### 单元测试
```csharp
// 测试 GetParentNodeIds 递归查找
[Fact]
public void GetParentNodeIds_ShouldFindAllUpstreamNodes()
{
    // Arrange
    var workflow = new Workflow();
    var nodeA = new WorkflowNodeBase { Id = "A" };
    var nodeB = new WorkflowNodeBase { Id = "B" };
    var nodeC = new WorkflowNodeBase { Id = "C" };
    var nodeD = new WorkflowNodeBase { Id = "D" };
    
    workflow.Nodes.Add(nodeA);
    workflow.Nodes.Add(nodeB);
    workflow.Nodes.Add(nodeC);
    workflow.Nodes.Add(nodeD);
    
    // 连接: A → B → C → D
    workflow.AddConnection(new WorkflowConnection("conn1", "A", "B"));
    workflow.AddConnection(new WorkflowConnection("conn2", "B", "C"));
    workflow.AddConnection(new WorkflowConnection("conn3", "C", "D"));
    
    // Act
    var upstreamNodes = workflow.GetParentNodeIds("D");
    
    // Assert
    Assert.Equal(3, upstreamNodes.Count);
    Assert.Contains("C", upstreamNodes); // 直接父节点
    Assert.Contains("B", upstreamNodes); // 间接父节点
    Assert.Contains("A", upstreamNodes); // 间接父节点
}

// 测试循环依赖处理
[Fact]
public void GetParentNodeIds_ShouldHandleCycles()
{
    // Arrange
    var workflow = new Workflow();
    var nodeA = new WorkflowNodeBase { Id = "A" };
    var nodeB = new WorkflowNodeBase { Id = "B" };
    
    workflow.Nodes.Add(nodeA);
    workflow.Nodes.Add(nodeB);
    
    // 循环连接: A → B → A
    workflow.AddConnection(new WorkflowConnection("conn1", "A", "B"));
    workflow.AddConnection(new WorkflowConnection("conn2", "B", "A"));
    
    // Act
    var upstreamNodes = workflow.GetParentNodeIds("A");
    
    // Assert - 应该避免无限循环
    Assert.Single(upstreamNodes);
    Assert.Contains("B", upstreamNodes);
}
```

### 集成测试
1. 创建一个复杂的工作流，包含多个层级的节点连接
2. 在任意节点上打开图像选择器
3. 验证下拉列表显示**所有上游节点**，不只是直接父节点
4. 验证没有重复节点（去重）
5. 验证节点顺序合理（BFS 遍历顺序）

## 📚 相关文件

- `src/Workflow/Workflow.cs` - 工作流核心类（已修改）
- `src/Plugin.SDK/Execution/Parameters/DataSourceQueryService.cs` - 数据源查询服务
- `src/Plugin.SDK/Execution/Parameters/IDataSourceQueryService.cs` - 数据源查询服务接口
- `src/Plugin.SDK/Execution/Parameters/GroupedDataSources.cs` - 分组数据源模型
- `src/Plugin.SDK/Execution/Parameters/TypeCategoryMapper.cs` - 类型分类器

## 🎓 技术要点

### BFS（广度优先搜索）
- **为什么使用 BFS**：保证节点按照"距离"的顺序返回
- **距离定义**：从当前节点到上游节点的连接跳数
- **优势**：UI 显示时，直接父节点排在前面，间接父节点排在后面

### HashSet 去重
- **为什么使用 HashSet**：避免重复节点和循环依赖
- **循环依赖场景**：A → B → C → A
- **处理方式**：使用 `visited` HashSet 记录已访问节点

### 反射推断 ResultType
- **技术点**：从 `IToolPlugin<TParams, TResult>` 接口提取泛型参数
- **目的**：设计时推断输出属性类型，支持类型过滤
- **实现**：遍历接口，匹配 `typeof(IToolPlugin<,>)`，提取第二个泛型参数

## 🔄 变更历史

| 日期 | 版本 | 变更内容 | 作者 |
|------|------|----------|------|
| 2026-04-09 | 1.0 | 初始版本，实现方案1（合并为单一方法） | AI Agent |

## 📖 参考资料

- BFS 算法：https://en.wikipedia.org/wiki/Breadth-first_search
- C# 泛型接口反射：https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/generics/reflection-and-generics
- 项目编码规范：`.codebuddy/rules/01-coding-standards/`
