# OrthogonalPathCalculator 测试指南

## 已修复的问题

### 1. Substring 越界异常 (已修复)
**文件**: `SunEyeVision.UI/Controls/WorkflowCanvasControl.xaml.cs:773`

**问题描述**:
```csharp
System.Diagnostics.Debug.WriteLine($"[DragStart] 节点:{node.Name}({node.Id.Substring(0,8)}...) 端口:{portName} 位置:({portPosition.X:F0},{portPosition.Y:F0})");
```

当节点 ID 长度小于 8 时，Substring 会抛出 `Index and length must refer to a location within the string` 异常。

**修复方案**:
```csharp
System.Diagnostics.Debug.WriteLine($"[DragStart] 节点:{node.Name}({node.Id.Substring(0, Math.Min(8, node.Id.Length))}...) 端口:{portName} 位置:({portPosition.X:F0},{portPosition.Y:F0})");
```

### 2. 添加调试日志到 SmartPathConverter (已修复)
**文件**: `SunEyeVision.UI/Converters/SmartPathConverter.cs`

**问题描述**:
路径数据未正确创建时，缺少调试信息，难以诊断问题。

**修复方案**:
添加了详细的调试日志，包括：
- Convert 方法调用信息
- 节点查找结果
- 缓存命中/未命中信息
- 生成的路径数据
- 异常堆栈跟踪

## 当前配置

### 画布类型
- **默认**: WorkflowCanvasControl
- **配置位置**: `WorkflowTabViewModel.cs:76`
  ```csharp
  CanvasType = CanvasType.WorkflowCanvas;
  ```

### 路径算法
- **默认**: AIStudio.Wpf.DiagramDesigner (MIT)
- **配置位置**: `PathCalculatorFactory.cs:34`
  ```csharp
  public static PathCalculatorType CurrentCalculatorType { get; set; } = PathCalculatorType.AIStudio;
  ```

### ConnectionPathCache 初始化
- **位置**: `WorkflowCanvasControl.xaml.cs:266-299`
- **默认使用**: PathCalculatorFactory.CreateCalculator()
- **备用方案**: OrthogonalPathCalculator

## 如何切换到 OrthogonalPathCalculator

### 方法 1: 通过代码（推荐用于测试）
在测试代码中调用：
```csharp
var mainWindow = Application.Current.MainWindow as MainWindow;
mainWindow?.SetPathCalculator("Orthogonal");
```

### 方法 2: 修改默认配置
修改 `PathCalculatorFactory.cs:34`:
```csharp
public static PathCalculatorType CurrentCalculatorType { get; set; } = PathCalculatorType.Orthogonal;
```

## 测试步骤

### 1. 启动应用
运行 `test_orthogonal_path.bat` 或直接启动 SunEyeVision.UI.exe

### 2. 观察日志输出
在 Visual Studio 的 "输出" 窗口中查看日志，关注：
- `[SmartPathConverter]` - 路径转换器日志
- `[PathCache]` - 路径缓存日志
- `[WorkflowCanvas]` - 画布初始化日志
- `[Path_Loaded]` - 路径元素加载日志

### 3. 测试场景

#### 场景 1: 查看现有连接线
- 应用启动后应该有 2 个连接（conn_1, conn_2）
- 检查连接线是否正确显示
- 检查箭头位置和角度是否正确

#### 场景 2: 拖拽节点
- 拖拽现有节点
- 观察连接线是否跟随更新
- 检查路径是否正确重新计算

#### 场景 3: 创建新连接
- 从一个节点端口拖拽到另一个节点端口
- 观察临时连接线是否正确显示
- 释放鼠标后检查连接线是否正确创建

#### 场景 4: 测试不同端口方向
- 测试 Top-Bottom, Bottom-Top, Left-Right, Right-Left 等不同端口组合
- 观察路径策略是否正确选择
- 检查箭头角度是否正确

## 预期结果

### 正常日志输出
```
[SmartPathConverter] Convert called for connection: conn_1
[SmartPathConverter] Source node: node_1, Target node: node_2
[SmartPathConverter] Cache hit for connection: conn_1
[SmartPathConverter] Generated path data for connection conn_1: M 100,100 L 200,100...
[Path_Loaded] ? Path加载，连接ID: conn_1
[Path_Loaded] ? 路径数据: 1个Figure, 3个Segment
[ArrowPath] 连接conn_1 箭头渲染完成: 位置(250.0,100.0), 角度180.0°
```

### 异常日志输出
```
[SmartPathConverter] Convert called for connection: conn_1
[SmartPathConverter] PathCache is null for connection: conn_1
[SmartPathConverter] Exception for connection conn_1: ...
[Path_Loaded] ? 路径数据未正确创建
```

## 诊断问题

### 问题 1: 路径数据未正确创建
**症状**: `[Path_Loaded] ? 路径数据未正确创建`

**可能原因**:
1. SmartPathConverter.PathCache 为 null
2. ConnectionPathCache 初始化失败
3. 路径计算失败（异常被捕获）

**排查步骤**:
1. 检查 `[WorkflowCanvas] ConnectionPathCache 创建成功` 日志
2. 检查 `[SmartPathConverter] PathCache is null` 日志
3. 查看异常堆栈跟踪

### 问题 2: 箭头位置在 (0,0)
**症状**: `[ArrowPath] 连接conn_1 箭头渲染完成: 位置(0.0,0.0), 角度0.0°`

**可能原因**:
1. ConnectionPathCache.CalculatePath 未被调用
2. 箭头位置计算失败

**排查步骤**:
1. 检查 `[PathCache] 路径计算完成` 日志
2. 检查箭头位置计算逻辑
3. 确认目标节点和端口位置正确

### 问题 3: 临时连接线可见性异常
**症状**: 大量 `[TempLine] ⚠ 临时连接线可见性异常：Collapsed，强制隐藏`

**可能原因**:
1. 临时连接线 Visibility 属性处理不当
2. 鼠标事件触发时机问题

**排查步骤**:
1. 检查 Port_MouseLeftButtonDown 方法
2. 检查 Canvas_MouseMove 方法
3. 确认临时连接线 Visibility 属性设置

## 下一步优化

1. **UI 添加切换按钮**: 在菜单栏或工具栏添加路径算法切换按钮
2. **性能优化**: 实现虚拟化渲染和批量更新
3. **碰撞检测**: 实现完整的节点碰撞检测
4. **撤销重做**: 集成完整的撤销重做系统

## 参考资料

- OrthogonalPathCalculator 实现: `SunEyeVision.UI/Services/PathCalculators/OrthogonalPathCalculator.cs`
- ConnectionPathCache 实现: `SunEyeVision.UI/Services/ConnectionPathCache.cs`
- WorkflowCanvasControl 实现: `SunEyeVision.UI/Controls/WorkflowCanvasControl.xaml.cs`
- SmartPathConverter 实现: `SunEyeVision.UI/Converters/SmartPathConverter.cs`
