# WorkflowCanvasControl 结构优化方案

## 📊 当前问题分析

### 1. 职责过重
WorkflowCanvasControl 承担了过多职责：
- 节点管理（创建、删除、移动、选择）
- 连接管理（创建、删除、更新路径）
- 拖拽处理（节点拖拽、连接拖拽、框选）
- 端口交互（端口高亮、连接创建）
- 视觉渲染（箭头、路径、临时元素）
- 调试功能（日志、外接矩形）

### 2. 代码重复
- HitTest 逻辑重复出现
- 端口查找逻辑重复
- 路径计算逻辑重复
- 事件处理模式重复

### 3. 耦合度高
- UI 逻辑与业务逻辑混合
- 直接操作 ViewModel
- 难以进行单元测试

### 4. 性能问题
- 每次拖拽都刷新所有连接路径
- 缺少有效的缓存机制
- 频繁的视觉树查找

---

## 🎯 优化目标

1. **单一职责原则**：每个类只负责一个明确的功能
2. **开闭原则**：对扩展开放，对修改关闭
3. **依赖倒置**：依赖抽象而非具体实现
4. **可测试性**：所有组件都可以独立测试
5. **性能优化**：减少不必要的计算和渲染

---

## 🏗️ 优化后的架构

```
WorkflowCanvasControl (协调器)
    ├── ICanvasStateManager (状态管理)
    │   └── CanvasStateManager
    │
    ├── IInteractionHandler (交互处理)
    │   ├── NodeDragHandler (已存在)
    │   ├── ConnectionDragHandler
    │   ├── BoxSelectionHandler
    │   └── PortInteractionHandler
    │
    ├── INodeService (节点服务)
    │   ├── NodeCreationService
    │   ├── NodeSelectionService
    │   └── NodePositionService
    │
    ├── IConnectionService (连接服务)
    │   ├── ConnectionCreationService
    │   ├── ConnectionPathService
    │   └── ConnectionValidationService
    │
    ├── IPortService (端口服务)
    │   ├── PortFinderService
    │   ├── PortHighlightService
    │   └── PortSelectionService
    │
    ├── IVisualService (视觉服务)
    │   ├── ArrowRenderer
    │   ├── PathRenderer
    │   └── TempElementManager
    │
    └── IDebugService (调试服务)
        ├── DebugLogger
        └── BoundingRectangleManager
```

---

## 📝 详细设计

### 1. 状态管理器 (CanvasStateManager)

```csharp
/// <summary>
/// 画布状态枚举
/// </summary>
public enum CanvasState
{
    Idle,           // 空闲
    DraggingNode,   // 拖拽节点
    DraggingConnection, // 拖拽连接
    BoxSelecting,   // 框选
    CreatingConnection // 创建连接
}

/// <summary>
/// 画布状态管理器接口
/// </summary>
public interface ICanvasStateManager
{
    CanvasState CurrentState { get; }
    event EventHandler<CanvasStateChangedEventArgs> StateChanged;
    
    bool CanTransitionTo(CanvasState newState);
    void TransitionTo(CanvasState newState);
    void Reset();
}

/// <summary>
/// 画布状态管理器实现
/// </summary>
public class CanvasStateManager : ICanvasStateManager
{
    private CanvasState _currentState = CanvasState.Idle;
    private readonly Stack<CanvasState> _stateHistory = new Stack<CanvasState>();
    
    public CanvasState CurrentState => _currentState;
    public event EventHandler<CanvasStateChangedEventArgs>? StateChanged;
    
    public bool CanTransitionTo(CanvasState newState)
    {
        // 定义状态转换规则
        return (_currentState, newState) switch
        {
            (CanvasState.Idle, _) => true,
            (CanvasState.DraggingNode, CanvasState.Idle) => true,
            (CanvasState.DraggingConnection, CanvasState.Idle) => true,
            (CanvasState.BoxSelecting, CanvasState.Idle) => true,
            (CanvasState.CreatingConnection, CanvasState.Idle) => true,
            _ => (newState == CanvasState.Idle) // 任何状态都可以回到空闲
        };
    }
    
    public void TransitionTo(CanvasState newState)
    {
        if (!CanTransitionTo(newState))
        {
            throw new InvalidOperationException($"无法从 {_currentState} 转换到 {newState}");
        }
        
        var oldState = _currentState;
        _stateHistory.Push(_currentState);
        _currentState = newState;
        
        StateChanged?.Invoke(this, new CanvasStateChangedEventArgs(oldState, newState));
    }
    
    public void Reset()
    {
        _stateHistory.Clear();
        _currentState = CanvasState.Idle;
        StateChanged?.Invoke(this, new CanvasStateChangedEventArgs(_currentState, CanvasState.Idle));
    }
}
```

### 2. 连接拖拽处理器 (ConnectionDragHandler)

```csharp
/// <summary>
/// 连接拖拽处理器接口
/// </summary>
public interface IConnectionDragHandler
{
    bool IsDragging { get; }
    WorkflowConnection? DraggedConnection { get; }
    
    void StartDrag(WorkflowConnection connection, Point startPosition);
    void UpdateDrag(Point currentPosition);
    void EndDrag();
    void CancelDrag();
}

/// <summary>
/// 连接拖拽处理器实现
/// </summary>
public class ConnectionDragHandler : IConnectionDragHandler
{
    private readonly Canvas _canvas;
    private readonly IConnectionPathService _pathService;
    private readonly IVisualService _visualService;
    
    private bool _isDragging;
    private WorkflowConnection? _draggedConnection;
    private Point _startPosition;
    private Point _initialSourcePosition;
    private Point _initialTargetPosition;
    
    public bool IsDragging => _isDragging;
    public WorkflowConnection? DraggedConnection => _draggedConnection;
    
    public event EventHandler<DragEventArgs>? DragStarted;
    public event EventHandler<DragEventArgs>? Dragging;
    public event EventHandler<DragEventArgs>? DragEnded;
    
    public ConnectionDragHandler(
        Canvas canvas,
        IConnectionPathService pathService,
        IVisualService visualService)
    {
        _canvas = canvas;
        _pathService = pathService;
        _visualService = visualService;
    }
    
    public void StartDrag(WorkflowConnection connection, Point startPosition)
    {
        if (connection == null) return;
        
        _isDragging = true;
        _draggedConnection = connection;
        _startPosition = startPosition;
        _initialSourcePosition = connection.SourcePosition;
        _initialTargetPosition = connection.TargetPosition;
        
        DragStarted?.Invoke(this, new DragEventArgs(connection, startPosition));
    }
    
    public void UpdateDrag(Point currentPosition)
    {
        if (!_isDragging || _draggedConnection == null) return;
        
        var offset = currentPosition - _startPosition;
        
        // 更新连接点位置
        _draggedConnection.SourcePosition = new Point(
            _initialSourcePosition.X + offset.X,
            _initialSourcePosition.Y + offset.Y
        );
        
        // 更新路径
        _pathService.UpdateConnectionPath(_draggedConnection);
        
        Dragging?.Invoke(this, new DragEventArgs(_draggedConnection, currentPosition, offset));
    }
    
    public void EndDrag()
    {
        if (!_isDragging) return;
        
        _isDragging = false;
        var connection = _draggedConnection;
        _draggedConnection = null;
        
        DragEnded?.Invoke(this, new DragEventArgs(connection, _startPosition));
    }
    
    public void CancelDrag()
    {
        if (!_isDragging || _draggedConnection == null) return;
        
        // 恢复初始位置
        _draggedConnection.SourcePosition = _initialSourcePosition;
        _draggedConnection.TargetPosition = _initialTargetPosition;
        
        _pathService.UpdateConnectionPath(_draggedConnection);
        
        _isDragging = false;
        _draggedConnection = null;
    }
}
```

### 3. 框选处理器 (BoxSelectionHandler)

```csharp
/// <summary>
/// 框选处理器接口
/// </summary>
public interface IBoxSelectionHandler
{
    bool IsBoxSelecting { get; }
    Rect SelectionBounds { get; }
    
    void StartSelection(Point startPoint);
    void UpdateSelection(Point currentPoint);
    void EndSelection();
    void CancelSelection();
}

/// <summary>
/// 框选处理器实现
/// </summary>
public class BoxSelectionHandler : IBoxSelectionHandler
{
    private readonly Canvas _canvas;
    private readonly SelectionBox _selectionBox;
    private readonly INodeSelectionService _selectionService;
    private readonly ISpatialIndex _spatialIndex;
    
    private bool _isBoxSelecting;
    private Point _startPoint;
    
    public bool IsBoxSelecting => _isBoxSelecting;
    public Rect SelectionBounds { get; private set; }
    
    public event EventHandler<SelectionEventArgs>? SelectionStarted;
    public event EventHandler<SelectionEventArgs>? SelectionUpdated;
    public event EventHandler<SelectionEventArgs>? SelectionCompleted;
    
    public BoxSelectionHandler(
        Canvas canvas,
        SelectionBox selectionBox,
        INodeSelectionService selectionService,
        ISpatialIndex spatialIndex)
    {
        _canvas = canvas;
        _selectionBox = selectionBox;
        _selectionService = selectionService;
        _spatialIndex = spatialIndex;
    }
    
    public void StartSelection(Point startPoint)
    {
        _isBoxSelecting = true;
        _startPoint = startPoint;
        SelectionBounds = new Rect(startPoint, startPoint);
        
        _selectionBox.Visibility = Visibility.Visible;
        UpdateSelectionBox();
        
        SelectionStarted?.Invoke(this, new SelectionEventArgs(SelectionBounds));
    }
    
    public void UpdateSelection(Point currentPoint)
    {
        if (!_isBoxSelecting) return;
        
        var x = Math.Min(_startPoint.X, currentPoint.X);
        var y = Math.Min(_startPoint.Y, currentPoint.Y);
        var width = Math.Abs(currentPoint.X - _startPoint.X);
        var height = Math.Abs(currentPoint.Y - _startPoint.Y);
        
        SelectionBounds = new Rect(x, y, width, height);
        UpdateSelectionBox();
        
        // 使用空间索引快速查找节点
        var nodesInBounds = _spatialIndex.Query(SelectionBounds);
        _selectionService.SelectNodes(nodesInBounds, SelectionBounds);
        
        SelectionUpdated?.Invoke(this, new SelectionEventArgs(SelectionBounds));
    }
    
    public void EndSelection()
    {
        if (!_isBoxSelecting) return;
        
        _selectionBox.Visibility = Visibility.Collapsed;
        _isBoxSelecting = false;
        
        SelectionCompleted?.Invoke(this, new SelectionEventArgs(SelectionBounds));
    }
    
    public void CancelSelection()
    {
        if (!_isBoxSelecting) return;
        
        _selectionBox.Visibility = Visibility.Collapsed;
        _selectionService.ClearSelection();
        _isBoxSelecting = false;
    }
    
    private void UpdateSelectionBox()
    {
        Canvas.SetLeft(_selectionBox, SelectionBounds.X);
        Canvas.SetTop(_selectionBox, SelectionBounds.Y);
        _selectionBox.Width = SelectionBounds.Width;
        _selectionBox.Height = SelectionBounds.Height;
    }
}
```

### 4. 端口交互处理器 (PortInteractionHandler)

```csharp
/// <summary>
/// 端口交互处理器接口
/// </summary>
public interface IPortInteractionHandler
{
    void HandlePortMouseDown(Ellipse port, Point position);
    void HandlePortMouseUp(Ellipse port, Point position);
    void HandlePortMouseEnter(Ellipse port);
    void HandlePortMouseLeave(Ellipse port);
    void HandleCanvasMouseMove(Point position);
}

/// <summary>
/// 端口交互处理器实现
/// </summary>
public class PortInteractionHandler : IPortInteractionHandler
{
    private readonly Canvas _canvas;
    private readonly IPortService _portService;
    private readonly IConnectionCreationService _connectionService;
    private readonly IVisualService _visualService;
    
    private Ellipse? _sourcePort;
    private WorkflowNode? _sourceNode;
    private Point _sourcePosition;
    private bool _isCreatingConnection;
    
    public event EventHandler<ConnectionEventArgs>? ConnectionCreated;
    public event EventHandler<ConnectionEventArgs>? ConnectionCancelled;
    
    public PortInteractionHandler(
        Canvas canvas,
        IPortService portService,
        IConnectionCreationService connectionService,
        IVisualService visualService)
    {
        _canvas = canvas;
        _portService = portService;
        _connectionService = connectionService;
        _visualService = visualService;
    }
    
    public void HandlePortMouseDown(Ellipse port, Point position)
    {
        _sourcePort = port;
        _sourceNode = _portService.GetNodeFromPort(port);
        _sourcePosition = position;
        _isCreatingConnection = true;
        
        // 显示临时连接线
        _visualService.ShowTempConnectionLine(_sourcePosition, position);
    }
    
    public void HandlePortMouseUp(Ellipse port, Point position)
    {
        if (!_isCreatingConnection || _sourcePort == null) return;
        
        var targetNode = _portService.GetNodeFromPort(port);
        
        if (targetNode != null && targetNode != _sourceNode)
        {
            // 创建连接
            var connection = _connectionService.CreateConnection(
                _sourceNode,
                targetNode,
                _sourcePort,
                port
            );
            
            if (connection != null)
            {
                ConnectionCreated?.Invoke(this, new ConnectionEventArgs(connection));
            }
        }
        
        Cleanup();
    }
    
    public void HandlePortMouseEnter(Ellipse port)
    {
        if (_isCreatingConnection)
        {
            _portService.HighlightPort(port, true);
        }
    }
    
    public void HandlePortMouseLeave(Ellipse port)
    {
        _portService.HighlightPort(port, false);
    }
    
    public void HandleCanvasMouseMove(Point position)
    {
        if (_isCreatingConnection)
        {
            // 更新临时连接线
            _visualService.UpdateTempConnectionLine(_sourcePosition, position);
            
            // 高亮目标端口
            var targetPort = _portService.FindPortAtPosition(position);
            if (targetPort != null)
            {
                _portService.HighlightPort(targetPort, true);
            }
        }
    }
    
    private void Cleanup()
    {
        _visualService.HideTempConnectionLine();
        _sourcePort = null;
        _sourceNode = null;
        _isCreatingConnection = false;
    }
}
```

### 5. 连接路径服务 (ConnectionPathService)

```csharp
/// <summary>
/// 连接路径服务接口
/// </summary>
public interface IConnectionPathService
{
    string CalculatePath(Point start, Point end);
    void UpdateConnectionPath(WorkflowConnection connection);
    void UpdateAllConnections(IEnumerable<WorkflowConnection> connections);
    void MarkConnectionDirty(WorkflowConnection connection);
}

/// <summary>
/// 连接路径服务实现
/// </summary>
public class ConnectionPathService : IConnectionPathService
{
    private readonly ConnectionPathCache _pathCache;
    private readonly IConnectionValidationService _validationService;
    
    public ConnectionPathService(
        ConnectionPathCache pathCache,
        IConnectionValidationService validationService)
    {
        _pathCache = pathCache;
        _validationService = validationService;
    }
    
    public string CalculatePath(Point start, Point end)
    {
        // 使用智能路径计算
        return CalculateSmartPath(start, end);
    }
    
    public void UpdateConnectionPath(WorkflowConnection connection)
    {
        // 标记为脏，下次访问时重新计算
        _pathCache.MarkDirty(connection);
        
        // 立即更新（如果需要）
        var pathData = _pathCache.GetPathData(connection);
        if (pathData != null)
        {
            connection.PathData = pathData;
            UpdateArrowPosition(connection);
        }
    }
    
    public void UpdateAllConnections(IEnumerable<WorkflowConnection> connections)
    {
        foreach (var connection in connections)
        {
            UpdateConnectionPath(connection);
        }
    }
    
    public void MarkConnectionDirty(WorkflowConnection connection)
    {
        _pathCache.MarkDirty(connection);
    }
    
    private string CalculateSmartPath(Point start, Point end)
    {
        var deltaX = end.X - start.X;
        var deltaY = end.Y - start.Y;
        
        // 水平优先策略
        if (Math.Abs(deltaX) > Math.Abs(deltaY))
        {
            var midX = start.X + deltaX / 2;
            return $"M {start.X:F1},{start.Y:F1} L {midX:F1},{start.Y:F1} L {midX:F1},{end.Y:F1} L {end.X:F1},{end.Y:F1}";
        }
        else
        {
            var midY = start.Y + deltaY / 2;
            return $"M {start.X:F1},{start.Y:F1} L {start.X:F1},{midY:F1} L {end.X:F1},{midY:F1} L {end.X:F1},{end.Y:F1}";
        }
    }
    
    private void UpdateArrowPosition(WorkflowConnection connection)
    {
        // 计算箭头位置和角度
        var points = connection.PathPoints;
        if (points.Count >= 2)
        {
            var lastPoint = points[points.Count - 1];
            var secondLastPoint = points[points.Count - 2];
            
            connection.ArrowX = lastPoint.X;
            connection.ArrowY = lastPoint.Y;
            connection.ArrowAngle = CalculateArrowAngle(secondLastPoint, lastPoint);
        }
    }
    
    private double CalculateArrowAngle(Point from, Point to)
    {
        var deltaX = to.X - from.X;
        var deltaY = to.Y - from.Y;
        var angle = Math.Atan2(deltaY, deltaX) * 180 / Math.PI;
        return angle;
    }
}
```

### 6. 端口服务 (PortService)

```csharp
/// <summary>
/// 端口服务接口
/// </summary>
public interface IPortService
{
    Ellipse? GetPortElement(string nodeId, string portName);
    WorkflowNode? GetNodeFromPort(Ellipse port);
    Ellipse? FindPortAtPosition(Point position);
    void HighlightPort(Ellipse port, bool highlight);
    PortDirection DetermineBestPort(WorkflowNode source, WorkflowNode target);
    Point GetPortPosition(WorkflowNode node, PortDirection direction);
}

/// <summary>
/// 端口服务实现
/// </summary>
public class PortService : IPortService
{
    private readonly Canvas _canvas;
    private readonly Dictionary<string, Ellipse> _portCache;
    private readonly object _cacheLock;
    
    public PortService(Canvas canvas)
    {
        _canvas = canvas;
        _portCache = new Dictionary<string, Ellipse>();
        _cacheLock = new object();
    }
    
    public Ellipse? GetPortElement(string nodeId, string portName)
    {
        string cacheKey = $"{nodeId}_{portName}";
        
        lock (_cacheLock)
        {
            if (_portCache.TryGetValue(cacheKey, out var cachedPort))
            {
                return cachedPort;
            }
            
            // 查找端口元素
            var port = FindVisualChild<Ellipse>(_canvas, 
                e => e.Name == portName && 
                     GetNodeIdFromElement(e) == nodeId);
            
            if (port != null)
            {
                _portCache[cacheKey] = port;
            }
            
            return port;
        }
    }
    
    public WorkflowNode? GetNodeFromPort(Ellipse port)
    {
        // 从端口元素获取节点信息
        var nodeElement = FindVisualParent<Border>(port);
        if (nodeElement?.DataContext is WorkflowNode node)
        {
            return node;
        }
        return null;
    }
    
    public Ellipse? FindPortAtPosition(Point position)
    {
        // 使用 HitTest 查找端口
        var hitResults = VisualTreeHelper.HitTest(_canvas, position);
        if (hitResults != null)
        {
            var port = FindVisualParent<Ellipse>(hitResults.VisualHit);
            return port;
        }
        return null;
    }
    
    public void HighlightPort(Ellipse port, bool highlight)
    {
        if (highlight)
        {
            port.Stroke = Brushes.LimeGreen;
            port.StrokeThickness = 3;
            port.Opacity = 1.0;
        }
        else
        {
            port.Stroke = Brushes.Gray;
            port.StrokeThickness = 1;
            port.Opacity = 0.7;
        }
    }
    
    public PortDirection DetermineBestPort(WorkflowNode source, WorkflowNode target)
    {
        var deltaX = target.Position.X - source.Position.X;
        var deltaY = target.Position.Y - source.Position.Y;
        
        // 水平偏移主导
        if (Math.Abs(deltaX) > Math.Abs(deltaY))
        {
            return deltaX > 0 ? PortDirection.Right : PortDirection.Left;
        }
        else
        {
            return deltaY > 0 ? PortDirection.Bottom : PortDirection.Top;
        }
    }
    
    public Point GetPortPosition(WorkflowNode node, PortDirection direction)
    {
        var nodeCenterX = node.Position.X + CanvasConfig.NodeWidth / 2;
        var nodeCenterY = node.Position.Y + CanvasConfig.NodeHeight / 2;
        
        return direction switch
        {
            PortDirection.Top => new Point(nodeCenterX, node.Position.Y),
            PortDirection.Bottom => new Point(nodeCenterX, node.Position.Y + CanvasConfig.NodeHeight),
            PortDirection.Left => new Point(node.Position.X, nodeCenterY),
            PortDirection.Right => new Point(node.Position.X + CanvasConfig.NodeWidth, nodeCenterY),
            _ => new Point(nodeCenterX, nodeCenterY)
        };
    }
    
    private T? FindVisualChild<T>(DependencyObject parent, Func<T, bool>? predicate = null) where T : DependencyObject
    {
        if (parent == null) return null;
        
        var childrenCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            
            if (child is T t && (predicate == null || predicate(t)))
            {
                return t;
            }
            
            var result = FindVisualChild(child, predicate);
            if (result != null) return result;
        }
        
        return null;
    }
    
    private T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T t) return t;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }
    
    private string? GetNodeIdFromElement(DependencyObject element)
    {
        // 从元素获取节点ID
        var nodeElement = FindVisualParent<Border>(element);
        if (nodeElement?.DataContext is WorkflowNode node)
        {
            return node.Id;
        }
        return null;
    }
}
```

### 7. 视觉服务 (VisualService)

```csharp
/// <summary>
/// 视觉服务接口
/// </summary>
public interface IVisualService
{
    void ShowTempConnectionLine(Point start, Point end);
    void UpdateTempConnectionLine(Point start, Point end);
    void HideTempConnectionLine();
    void UpdateArrow(Path arrowPath, WorkflowConnection connection);
    void UpdateConnectionPath(Path path, WorkflowConnection connection);
}

/// <summary>
/// 视觉服务实现
/// </summary>
public class VisualService : IVisualService
{
    private readonly Canvas _canvas;
    private readonly Path _tempConnectionLine;
    private readonly IConnectionPathService _pathService;
    
    public VisualService(
        Canvas canvas,
        Path tempConnectionLine,
        IConnectionPathService pathService)
    {
        _canvas = canvas;
        _tempConnectionLine = tempConnectionLine;
        _pathService = pathService;
    }
    
    public void ShowTempConnectionLine(Point start, Point end)
    {
        var pathData = _pathService.CalculatePath(start, end);
        _tempConnectionLine.Data = Geometry.Parse(pathData);
        _tempConnectionLine.Visibility = Visibility.Visible;
    }
    
    public void UpdateTempConnectionLine(Point start, Point end)
    {
        var pathData = _pathService.CalculatePath(start, end);
        _tempConnectionLine.Data = Geometry.Parse(pathData);
    }
    
    public void HideTempConnectionLine()
    {
        _tempConnectionLine.Visibility = Visibility.Collapsed;
    }
    
    public void UpdateArrow(Path arrowPath, WorkflowConnection connection)
    {
        var rotateTransform = new RotateTransform(connection.ArrowAngle);
        arrowPath.RenderTransform = rotateTransform;
        
        Canvas.SetLeft(arrowPath, connection.ArrowX);
        Canvas.SetTop(arrowPath, connection.ArrowY);
    }
    
    public void UpdateConnectionPath(Path path, WorkflowConnection connection)
    {
        path.Data = Geometry.Parse(connection.PathData);
    }
}
```

### 8. 重构后的 WorkflowCanvasControl

```csharp
public partial class WorkflowCanvasControl : UserControl
{
    #region 服务依赖
    
    private readonly ICanvasStateManager _stateManager;
    private readonly INodeDragHandler _nodeDragHandler;
    private readonly IConnectionDragHandler _connectionDragHandler;
    private readonly IBoxSelectionHandler _boxSelectionHandler;
    private readonly IPortInteractionHandler _portInteractionHandler;
    private readonly IConnectionPathService _connectionPathService;
    private readonly IPortService _portService;
    private readonly IVisualService _visualService;
    
    #endregion
    
    public WorkflowCanvasControl()
    {
        InitializeComponent();
        
        // 初始化服务
        InitializeServices();
        
        // 订阅事件
        SubscribeToEvents();
    }
    
    private void InitializeServices()
    {
        // 状态管理器
        _stateManager = new CanvasStateManager();
        
        // 路径缓存
        var pathCache = new ConnectionPathCache(CurrentWorkflowTab?.WorkflowNodes ?? new ObservableCollection<WorkflowNode>());
        
        // 空间索引
        var spatialIndex = new GridSpatialIndex(cellSize: 200);
        
        // 端口服务
        _portService = new PortService(WorkflowCanvas);
        
        // 连接路径服务
        _connectionPathService = new ConnectionPathService(pathCache, null);
        
        // 视觉服务
        _visualService = new VisualService(WorkflowCanvas, TempConnectionLine, _connectionPathService);
        
        // 节点拖拽处理器
        _nodeDragHandler = new NodeDragHandler(
            WorkflowCanvas,
            _viewModel,
            node => SelectNode(node)
        );
        
        // 连接拖拽处理器
        _connectionDragHandler = new ConnectionDragHandler(
            WorkflowCanvas,
            _connectionPathService,
            _visualService
        );
        
        // 框选处理器
        _boxSelectionHandler = new BoxSelectionHandler(
            WorkflowCanvas,
            SelectionBox,
            null, // INodeSelectionService
            spatialIndex
        );
        
        // 端口交互处理器
        _portInteractionHandler = new PortInteractionHandler(
            WorkflowCanvas,
            _portService,
            null, // IConnectionCreationService
            _visualService
        );
    }
    
    private void SubscribeToEvents()
    {
        // 状态变化事件
        _stateManager.StateChanged += OnStateChanged;
        
        // 节点拖拽事件
        _nodeDragHandler.DragStarted += OnNodeDragStarted;
        _nodeDragHandler.Dragging += OnNodeDragging;
        _nodeDragHandler.DragEnded += OnNodeDragEnded;
        
        // 连接拖拽事件
        _connectionDragHandler.DragStarted += OnConnectionDragStarted;
        _connectionDragHandler.Dragging += OnConnectionDragging;
        _connectionDragHandler.DragEnded += OnConnectionDragEnded;
        
        // 框选事件
        _boxSelectionHandler.SelectionStarted += OnSelectionStarted;
        _boxSelectionHandler.SelectionUpdated += OnSelectionUpdated;
        _boxSelectionHandler.SelectionCompleted += OnSelectionCompleted;
        
        // 端口交互事件
        _portInteractionHandler.ConnectionCreated += OnConnectionCreated;
        _portInteractionHandler.ConnectionCancelled += OnConnectionCancelled;
    }
    
    #region 事件处理
    
    private void OnStateChanged(object? sender, CanvasStateChangedEventArgs e)
    {
        _viewModel?.AddLog($"[StateManager] 状态转换: {e.OldState} -> {e.NewState}");
    }
    
    private void OnNodeDragStarted(object? sender, DragEventArgs e)
    {
        _stateManager.TransitionTo(CanvasState.DraggingNode);
    }
    
    private void OnNodeDragging(object? sender, DragEventArgs e)
    {
        // 只更新受影响的连接
        if (e.Target is WorkflowNode node)
        {
            var affectedConnections = CurrentWorkflowTab?.WorkflowConnections
                .Where(c => c.SourceNodeId == node.Id || c.TargetNodeId == node.Id);
            
            if (affectedConnections != null)
            {
                _connectionPathService.UpdateAllConnections(affectedConnections);
            }
        }
    }
    
    private void OnNodeDragEnded(object? sender, DragEventArgs e)
    {
        _stateManager.TransitionTo(CanvasState.Idle);
    }
    
    #endregion
    
    #region 鼠标事件处理
    
    private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is WorkflowNode node)
        {
            var position = e.GetPosition(WorkflowCanvas);
            _nodeDragHandler.StartDrag(node, position);
        }
    }
    
    private void WorkflowCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(WorkflowCanvas);
        
        switch (_stateManager.CurrentState)
        {
            case CanvasState.DraggingNode:
                _nodeDragHandler.UpdateDrag(position);
                break;
                
            case CanvasState.DraggingConnection:
                _connectionDragHandler.UpdateDrag(position);
                break;
                
            case CanvasState.BoxSelecting:
                _boxSelectionHandler.UpdateSelection(position);
                break;
                
            case CanvasState.CreatingConnection:
                _portInteractionHandler.HandleCanvasMouseMove(position);
                break;
        }
    }
    
    private void WorkflowCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        switch (_stateManager.CurrentState)
        {
            case CanvasState.DraggingNode:
                _nodeDragHandler.EndDrag();
                break;
                
            case CanvasState.DraggingConnection:
                _connectionDragHandler.EndDrag();
                break;
                
            case CanvasState.BoxSelecting:
                _boxSelectionHandler.EndSelection();
                break;
        }
    }
    
    #endregion
}
```

---

## 📈 优化效果

### 1. 代码组织
- **职责分离**：每个类只负责一个明确的功能
- **易于维护**：修改某个功能只需修改对应的服务类
- **可扩展性**：添加新功能只需实现新的接口

### 2. 性能提升
- **缓存优化**：使用 ConnectionPathCache 避免重复计算
- **空间索引**：使用 SpatialIndex 加速节点查找
- **增量更新**：只更新受影响的连接，而非全部

### 3. 可测试性
- **单元测试**：每个服务都可以独立测试
- **依赖注入**：可以注入 Mock 对象进行测试
- **接口隔离**：测试时只需关注相关接口

### 4. 代码复用
- **服务复用**：服务可以在多个控件中复用
- **逻辑复用**：公共逻辑提取到服务中
- **减少重复**：消除重复的 HitTest 和端口查找代码

---

## 🚀 实施步骤

### 阶段 1：创建服务接口和基础实现
1. 创建所有服务接口
2. 实现基础的服务类
3. 编写单元测试

### 阶段 2：重构现有功能
1. 提取节点拖拽逻辑到 NodeDragHandler（已完成）
2. 创建 ConnectionDragHandler
3. 创建 BoxSelectionHandler
4. 创建 PortInteractionHandler

### 阶段 3：集成服务
1. 在 WorkflowCanvasControl 中集成所有服务
2. 替换现有的直接实现
3. 测试功能完整性

### 阶段 4：性能优化
1. 启用 ConnectionPathCache
2. 启用 SpatialIndex
3. 实现增量更新

### 阶段 5：清理和文档
1. 删除冗余代码
2. 更新文档
3. 代码审查

---

## 📝 注意事项

1. **向后兼容**：确保重构后功能与原有功能一致
2. **性能监控**：监控重构前后的性能指标
3. **测试覆盖**：确保所有功能都有测试覆盖
4. **渐进式重构**：不要一次性重构所有代码，分阶段进行

---

## 🔗 相关文档

- [画布优化计划](./画布优化计划.md)
- [连线开发计划](./连线开发计划.md)
- [EventBus使用指南](./EventBus使用指南.md)
