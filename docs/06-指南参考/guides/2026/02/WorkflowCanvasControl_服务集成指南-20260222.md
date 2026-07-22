# WorkflowCanvasControl 服务集成指南

## 概述

本文档说明如何将新创建的服务集成到现有的 `WorkflowCanvasControl` 中，实现职责分离和代码优化。

## 服务架构

### 核心服务列表

1. **CanvasStateManager** - 画布状态管理
   - 路径：`SunEyeVision.UI.Services.CanvasStateManager`
   - 职责：管理画布状态（空闲、拖拽、框选等），提供状态转换和历史记录

2. **PortService** - 端口服务
   - 路径：`SunEyeVision.UI.Services.PortService`
   - 职责：端口查找、高亮、位置计算、缓存优化

3. **ConnectionPathService** - 连接路径服务
   - 路径：`SunEyeVision.UI.Services.ConnectionPathService`
   - 职责：连接线路径计算、更新、缓存管理

4. **NodeSelectionService** - 节点选择服务
   - 路径：`SunEyeVision.UI.Services.NodeSelectionService`
   - 职责：节点选择管理、框选、位置记录

5. **ConnectionService** - 连接服务
   - 路径：`SunEyeVision.UI.Services.ConnectionService`
   - 职责：连接创建、删除、验证、循环检测

6. **CanvasConfig** - 画布配置
   - 路径：`SunEyeVision.UI.Services.CanvasConfig`
   - 职责：集中管理所有配置参数

## 集成步骤

### 第一步：修改 WorkflowCanvasControl 构造函数

在 `WorkflowCanvasControl.xaml.cs` 中，添加服务实例的初始化：

```csharp
public WorkflowCanvasControl()
{
    InitializeComponent();

    // 初始化服务
    InitializeServices();

    // 订阅事件服务
    SubscribeToServiceEvents();

    // 初始化其他组件
    InitializeComponents();
}

private void InitializeServices()
{
    // 创建状态管理器
    _stateManager = new CanvasStateManager();

    // 创建端口服务
    _portService = new PortService(this);

    // 创建连接路径服务
    _pathCache = new ConnectionPathCache(_nodes, _connections);
    _pathService = new ConnectionPathService(_pathCache, _nodes);

    // 创建节点选择服务
    _selectionService = new NodeSelectionService(_nodes);

    // 创建连接服务
    _connectionService = new ConnectionService(_nodes, _pathService, _portService);
}

private void SubscribeToServiceEvents()
{
    // 订阅状态变化事件
    _stateManager.StateChanged += OnCanvasStateChanged;

    // 订阅选择变化事件
    _selectionService.SelectionChanged += OnSelectionChanged;

    // 订阅连接创建/删除事件
    _connectionService.ConnectionCreated += OnConnectionCreated;
    _connectionService.ConnectionDeleted += OnConnectionDeleted;
}
```

### 第二步：替换节点选择逻辑

将原有的节点选择逻辑替换为使用 `NodeSelectionService`：

```csharp
// 原代码：
// private void SelectNode(WorkflowNode node, bool addToSelection)
// {
//     if (!addToSelection)
//     {
//         ClearAllSelections();
//     }
//     node.IsSelected = true;
//     _selectedNodes.Add(node);
// }

// 新代码：
private void SelectNode(WorkflowNode node, bool addToSelection)
{
    _selectionService.SelectNode(node, addToSelection);
}

// 原代码：
// private void ClearAllSelections()
// {
//     foreach (var node in _selectedNodes)
//     {
//         node.IsSelected = false;
//     }
//     _selectedNodes.Clear();
// }

// 新代码：
private void ClearAllSelections()
{
    _selectionService.ClearSelection();
}

// 原代码：
// private void RecordSelectedNodesPositions()
// {
//     _selectedNodesInitialPositions.Clear();
//     foreach (var node in _selectedNodes)
//     {
//         _selectedNodesInitialPositions[node.Id] = node.Position;
//     }
// }

// 新代码：
private void RecordSelectedNodesPositions()
{
    _selectionService.RecordSelectedNodesPositions();
}
```

### 第三步：替换连接创建逻辑

将原有的连接创建逻辑替换为使用 `ConnectionService`：

```csharp
// 原代码：
// private void CreateConnection(WorkflowNode sourceNode, WorkflowNode targetNode)
// {
//     // 大量的连接创建逻辑...
// }

// 新代码：
private void CreateConnection(WorkflowNode sourceNode, WorkflowNode targetNode)
{
    var connection = _connectionService.CreateConnection(sourceNode, targetNode);
    if (connection != null)
    {
        // 连接创建成功，更新UI
        UpdateConnectionUI(connection);
    }
}

// 原代码：
// private void CreateConnectionWithSpecificPort(...)
// {
//     // 大量的端口选择和连接创建逻辑...
// }

// 新代码：
private void CreateConnectionWithSpecificPort(
    WorkflowNode sourceNode,
    WorkflowNode targetNode,
    PortDirection sourcePort,
    PortDirection targetPort)
{
    var connection = _connectionService.CreateConnectionWithPorts(
        sourceNode,
        targetNode,
        sourcePort,
        targetPort
    );

    if (connection != null)
    {
        UpdateConnectionUI(connection);
    }
}
```

### 第四步：替换端口相关逻辑

将原有的端口查找、高亮等逻辑替换为使用 `PortService`：

```csharp
// 原代码：
// private PortDirection DetermineClickedPort(WorkflowNode node, Point clickPoint)
// {
//     // 大量的端口判断逻辑...
// }

// 新代码：
private PortDirection DetermineClickedPort(WorkflowNode node, Point clickPoint)
{
    return _portService.DetermineClickedPort(node, clickPoint);
}

// 原代码：
// private void HighlightTargetPort(WorkflowNode targetNode, PortDirection sourcePort)
// {
//     // 大量的端口高亮逻辑...
// }

// 新代码：
private void HighlightTargetPort(WorkflowNode targetNode, PortDirection sourcePort)
{
    _portService.HighlightTargetPort(targetNode, sourcePort);
}

// 原代码：
// private void ClearTargetPortHighlight()
// {
//     // 清除高亮的逻辑...
// }

// 新代码：
private void ClearTargetPortHighlight()
{
    _portService.ClearHighlight();
}
```

### 第五步：替换路径计算逻辑

将原有的路径计算逻辑替换为使用 `ConnectionPathService`：

```csharp
// 原代码：
// private string CalculateSmartPath(Point start, Point end)
// {
//     // 路径计算逻辑...
// }

// 新代码：
private string CalculateSmartPath(Point start, Point end)
{
    return _pathService.CalculateSmartPath(start, end);
}

// 更新连接路径时使用服务
private void UpdateConnectionPath(WorkflowConnection connection)
{
    _pathService.UpdateConnectionPath(connection);
}
```

### 第六步：使用状态管理器

将原有的状态管理逻辑替换为使用 `CanvasStateManager`：

```csharp
// 原代码：
// private bool _isDraggingNode;
// private bool _isDraggingConnection;
// private bool _isBoxSelecting;

// 新代码：使用状态管理器

// 在开始拖拽节点时
private void StartNodeDrag(WorkflowNode node, Point startPoint)
{
    _stateManager.TransitionTo(CanvasState.DraggingNode);
    // 其他拖拽逻辑...
}

// 在结束拖拽时
private void EndNodeDrag()
{
    _stateManager.TransitionTo(CanvasState.Idle);
    // 其他结束逻辑...
}

// 检查当前状态
private bool CanStartDrag()
{
    return _stateManager.CurrentState == CanvasState.Idle;
}
```

### 第七步：使用配置类

将硬编码的配置值替换为使用 `CanvasConfig`：

```csharp
// 原代码：
// const double NODE_WIDTH = 120;
// const double NODE_HEIGHT = 80;

// 新代码：
var nodeWidth = CanvasConfig.Node.DefaultWidth;
var nodeHeight = CanvasConfig.Node.DefaultHeight;

// 原代码：
// const double PORT_RADIUS = 6;

// 新代码：
var portRadius = CanvasConfig.Port.Radius;

// 原代码：
// const double CONNECTION_THICKNESS = 2;

// 新代码：
var connectionThickness = CanvasConfig.Connection.DefaultThickness;
```

## 事件处理

### 添加事件处理方法

```csharp
private void OnCanvasStateChanged(object sender, StateChangedEventArgs e)
{
    System.Diagnostics.Debug.WriteLine(
        $"Canvas state changed from {e.OldState} to {e.NewState}"
    );

    // 根据状态更新UI
    UpdateCursorForState(e.NewState);
}

private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
{
    // 处理选择变化
    if (e.Action == SelectionAction.Clear)
    {
        // 清除选择时的处理
        UpdateBoundingRectangle();
    }
    else if (e.Action == SelectionAction.Add)
    {
        // 添加选择时的处理
        foreach (var node in e.AddedNodes)
        {
            BringToFront(node);
        }
    }
}

private void OnConnectionCreated(object sender, ConnectionEventArgs e)
{
    // 连接创建后的处理
    UpdateBoundingRectangle();
}

private void OnConnectionDeleted(object sender, ConnectionEventArgs e)
{
    // 连接删除后的处理
    UpdateBoundingRectangle();
}

private void UpdateCursorForState(CanvasState state)
{
    Cursor = state switch
    {
        CanvasState.DraggingNode => Cursors.Hand,
        CanvasState.DraggingConnection => Cursors.Cross,
        CanvasState.BoxSelecting => Cursors.Cross,
        CanvasState.CreatingConnection => Cursors.Cross,
        _ => Cursors.Arrow
    };
}
```

## 清理工作

### 删除冗余代码

在集成完成后，可以删除以下冗余代码：

1. 删除原有的节点选择相关字段和方法
2. 删除原有的连接创建相关字段和方法
3. 删除原有的端口查找和高亮相关方法
4. 删除原有的路径计算方法
5. 删除硬编码的配置常量

### 保留的代码

以下代码应该保留在 `WorkflowCanvasControl` 中：

1. UI事件处理方法（鼠标、键盘事件）
2. XAML相关的方法（Loaded、Unloaded等）
3. 视觉树相关的方法（FindVisualChild等）
4. 与UI框架直接交互的方法

## 迁移策略

### 渐进式迁移

建议采用渐进式迁移策略：

1. **第一阶段**：集成配置类（CanvasConfig）
   - 替换所有硬编码的配置值
   - 验证功能正常

2. **第二阶段**：集成状态管理器（CanvasStateManager）
   - 替换状态管理逻辑
   - 验证状态转换正确

3. **第三阶段**：集成选择服务（NodeSelectionService）
   - 替换节点选择逻辑
   - 验证选择功能正常

4. **第四阶段**：集成端口服务（PortService）
   - 替换端口相关逻辑
   - 验证端口功能正常

5.）**第五阶段**：集成连接服务（ConnectionService）
   - 替换连接创建逻辑
   - 验证连接功能正常

6. **第六阶段**：集成路径服务（ConnectionPathService）
   - 替换路径计算逻辑
   - 验证路径显示正常

### 测试策略

在每个阶段完成后，进行以下测试：

1. **功能测试**：验证所有功能正常工作
2. **性能测试**：对比迁移前后的性能
3. **回归测试**：确保没有引入新的bug

## 优势依赖关系

```
WorkflowCanvasControl
    ├── CanvasStateManager (状态管理)
    ├── NodeSelectionService (选择管理)
    ├── ConnectionService (连接管理)
    │   ├── ConnectionPathService (路径计算)
    │   │   └── ConnectionPathCache (路径缓存)
    │   └── PortService (端口服务)
    └── CanvasConfig (配置管理)
```

## 注意事项

1. **线程安全**：服务类目前不是线程安全的，如果需要在多线程环境中使用，需要添加适当的同步机制
2. **内存管理**：注意事件订阅的生命周期，避免内存泄漏
3. **向后兼容**：在迁移过程中，保持向后兼容性，确保现有功能不受影响
4. **性能监控**：迁移后监控性能，确保没有性能下降
5. **日志记录**：在关键操作处添加日志记录，便于调试

## 总结

通过将 `WorkflowCanvasControl` 的职责分离到各个服务类中，可以实现：

1. **单一职责原则**：每个服务类只负责一个特定的功能
2. **开闭原则**：易于扩展新的功能，无需修改现有代码
3. **依赖倒置原则**：依赖抽象接口，而不是具体实现
4. **可测试性**：每个服务类都可以独立测试
5. **可维护性**：代码结构清晰，易于理解和维护

这种重构将大大提高代码质量和可维护性，为后续的功能扩展打下良好的基础。
