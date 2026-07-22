# 事件驱动工作流优化方案实施指南

## 概述

本优化方案实现了事件驱动的异步工作流执行系统,支持本地图像处理(同步)和实时相机采集(异步)两种模式的统一架构。

## 核心组件

### 1. AsyncEventBus - 异步事件总线

支持两种发布模式:
- **Fire-and-forget**: 不等待处理器完成,适用于通知型事件
- **Wait-all**: 等待所有处理器完成,适用于需要确认的事件

**位置**: `SunEyeVision.Core/Events/AsyncEventBus.cs`

**使用示例**:
```csharp
// 创建异步事件总线
var eventBus = new AsyncEventBus(logger);

// 订阅事件
eventBus.Subscribe<ImageCapturedEvent>(async evt =>
{
    Console.WriteLine($"Image captured: {evt.DeviceId}, Frame: {evt.FrameNumber}");
});

// 发布事件 (fire-and-forget)
await eventBus.PublishAsync(new ImageCapturedEvent("Camera1", "camera-001", image, frameNum));

// 发布事件 (wait-all)
await eventBus.PublishAndWaitAsync(new ImageCapturedEvent("Camera1", "camera-001", image, frameNum));
```

### 2. ImageQueue - 图像队列

生产者-消费者模式的图像队列,支持4种溢出策略:

- **DropNewest**: 队列满时丢弃最新图像
- **DropOldest**: 队列满时丢弃最旧图像
- **Block**: 队列满时阻塞生产者
- **Overwrite**: 队列满时覆盖最旧图像

**位置**: `SunEyeVision.Workflow/ImageQueue.cs`

**使用示例**:
```csharp
// 创建图像队列 (容量10,丢弃最新策略)
var imageQueue = new ImageQueue(10, QueueOverflowStrategy.DropNewest, logger);

// 生产者: 入队图像
await imageQueue.EnqueueAsync(image, "camera-001", metadata);

// 消费者: 出队图像
var entry = await imageQueue.DequeueAsync(cancellationToken);
if (entry != null)
{
    Console.WriteLine($"Dequeued image: Seq={entry.SequenceNumber}");
}

// 获取队列统计
var stats = imageQueue.GetStatistics();
Console.WriteLine($"DropRate: {stats.DropRate}%");
```

### 3. TriggerManager - 触发器管理器

统一管理硬件触发、软件触发和定时器触发。

**位置**: `SunEyeVision.Workflow/TriggerManager.cs`

**使用示例**:
```csharp
// 创建触发器管理器
var triggerManager = new TriggerManager(logger, eventBus);

// 注册硬件触发
triggerManager.RegisterHardwareTrigger("trigger-io1", new HardwareTriggerConfig
{
    IoPin = 1,
    Edge = TriggerEdge.Rising,
    DebounceMs = 10
});

// 注册软件触发
triggerManager.RegisterSoftwareTrigger("trigger-manual", new SoftwareTriggerConfig
{
    TriggerName = "Manual Trigger",
    TriggerKey = "manual"
});

// 注册定时器触发
triggerManager.RegisterTimerTrigger("trigger-timer", new TimerTriggerConfig
{
    IntervalMs = 1000,  // 每秒触发一次
    InitialDelayMs = 0
});

// 启动定时器触发
triggerManager.StartTimerTrigger("trigger-timer");

// 手动触发软件触发
triggerManager.FireSoftwareTrigger("trigger-manual", image, metadata);
```

### 4. EventDrivenExecutor - 事件驱动执行器

自动响应触发并从队列中取出图像进行处理的工作流执行器。

**位置**: `SunEyeVision.Workflow/EventDrivenExecutor.cs`

**使用示例**:
```csharp
// 创建事件驱动执行器
var executor = new EventDrivenExecutor(
    syncEngine,
    imageQueue,
    logger,
    eventBus,
    pluginManager
);

// 订阅处理完成事件
executor.ProcessingCompleted += (sender, e) =>
{
    Console.WriteLine($"Processing completed: Success={e.Result.Success}, Duration={e.DurationMs}ms");
};

// 启动执行器 (指定工作流ID)
executor.Start("workflow-001");

// 停止执行器
executor.Stop();
```

### 5. BaseDeviceDriver 扩展

添加了触发器模式和ImageCaptured事件支持。

**新增属性**:
- `TriggerMode`: 触发器模式枚举
- `ImageCaptured`: 图像捕获事件

**新增方法**:
- `SetTriggerMode(TriggerMode mode)`: 设置触发器模式
- `TriggerCapture()`: 手动触发图像采集(软件触发)

**位置**: `SunEyeVision.DeviceDriver/BaseDeviceDriver.cs`

### 6. SimulatedCameraDriver 更新

实现了触发器逻辑和采集循环,支持与AsyncEventBus集成。

**位置**: `SunEyeVision.DeviceDriver/SimulatedCameraDriver.cs`

## 完整集成示例

```csharp
using SunEyeVision.Workflow;
using SunEyeVision.DeviceDriver;
using SunEyeVision.PluginSystem;
using Events = SunEyeVision.Events;

public class EventDrivenWorkflowIntegration
{
    private readonly ILogger _logger;
    private readonly Events.IAsyncEventBus _eventBus;
    private readonly ImageQueue _imageQueue;
    private readonly TriggerManager _triggerManager;
    private readonly EventDrivenExecutor _executor;
    private readonly SimulatedCameraDriver _camera;

    public EventDrivenWorkflowIntegration(ILogger logger, IPluginManager pluginManager)
    {
        _logger = logger;

        // 1. 创建异步事件总线
        _eventBus = new Events.AsyncEventBus(logger);

        // 2. 创建图像队列 (容量10,丢弃最新策略)
        _imageQueue = new ImageQueue(10, QueueOverflowStrategy.DropNewest, logger);

        // 3. 创建触发器管理器
        _triggerManager = new TriggerManager(logger, _eventBus);

        // 4. 创建同步执行引擎
        var syncEngine = new WorkflowExecutionEngine(..., pluginManager, logger);

        // 5. 创建事件驱动执行器
        _executor = new EventDrivenExecutor(
            syncEngine,
            _imageQueue,
            logger,
            _eventBus,
            pluginManager
        );

        // 6. 创建相机驱动
        _camera = new SimulatedCameraDriver("camera-001", logger, _eventBus);

        SetupEventSubscriptions();
    }

    private void SetupEventSubscriptions()
    {
        // 订阅图像捕获事件 -> 自动入队
        _eventBus.Subscribe<ImageCapturedEvent>(async evt =>
        {
            await _imageQueue.EnqueueAsync(evt.Image, evt.DeviceId, new { evt.FrameNumber });
        });

        // 订阅工作流触发事件 -> 触发处理
        _eventBus.Subscribe<WorkflowTriggerEvent>(async evt =>
        {
            // 执行器会自动从队列中取图处理
        });

        // 订阅处理完成事件
        _executor.ProcessingCompleted += (sender, e) =>
        {
            Console.WriteLine($"Processing completed: Success={e.Result.Success}, " +
                            $"Duration={e.DurationMs}ms, Seq={e.QueueEntry.SequenceNumber}");
        };
    }

    public void StartAsyncWorkflow()
    {
        // 1. 连接相机
        _camera.Connect();

        // 2. 设置触发器模式 (硬件触发)
        _camera.SetTriggerMode(TriggerMode.Hardware);

        // 3. 注册硬件触发
        _triggerManager.RegisterHardwareTrigger("trigger-io1", new HardwareTriggerConfig
        {
            IoPin = 1,
            Edge = TriggerEdge.Rising
        });

        // 4. 启动连续采集
        _camera.StartContinuousCapture();

        // 5. 启动事件驱动执行器
        _executor.Start("workflow-001");

        Console.WriteLine("Async workflow started successfully!");
    }

    public void StopAsyncWorkflow()
    {
        // 1. 停止执行器
        _executor.Stop();

        // 2. 停止相机采集
        _camera.StopContinuousCapture();

        // 3. 断开相机
        _camera.Disconnect();

        Console.WriteLine("Async workflow stopped.");
    }

    public void PrintStatistics()
    {
        Console.WriteLine("=== Event-Driven Workflow Statistics ===");
        Console.WriteLine($"Event Bus:\n{_eventBus.GetStatistics()}\n");
        Console.WriteLine($"Image Queue:\n{_imageQueue.GetStatistics()}\n");
        Console.WriteLine($"Trigger Manager:\n{_triggerManager.GetStatistics()}\n");
        Console.WriteLine($"Executor:\n{_executor.GetStatistics()}\n");
    }

    public void Dispose()
    {
        _executor?.Dispose();
        _triggerManager?.Dispose();
        _imageQueue?.Dispose();
        _eventBus?.Dispose();
        _camera?.Dispose();
    }
}
```

## 双模式执行引擎使用

### 同步模式 (本地图像处理)

适用于单张图像处理或离线批量处理:

```csharp
var syncEngine = new WorkflowExecutionEngine(...);
var result = await syncEngine.ExecuteWorkflow("workflow-001", localImage);
```

### 异步模式 (实时相机采集)

适用于实时连续处理:

```csharp
// 配置事件驱动架构
var eventBus = new AsyncEventBus(logger);
var imageQueue = new ImageQueue(10, QueueOverflowStrategy.DropNewest, logger);
var executor = new EventDrivenExecutor(syncEngine, imageQueue, logger, eventBus, pluginManager);

// 启动异步处理
executor.Start("workflow-001");
```

## 关键设计决策

### 1. 为什么使用异步事件总线?
- 解耦组件: 相机、触发器、处理器之间完全解耦
- 灵活扩展: 新增处理器只需订阅事件,无需修改现有代码
- 性能优化: fire-and-forget模式避免阻塞相机采集

### 2. 为什么使用图像队列?
- 缓冲生产者和消费者速度差异
- 可配置的溢出策略应对不同场景
- 避免图像丢失或内存溢出

### 3. 为什么统一触发器管理?
- 简化配置: 所有触发器通过统一接口管理
- 灵活切换: 无需修改代码即可切换触发方式
- 易于调试: 集中的触发器状态监控

### 4. 为什么保持同步引擎?
- 向后兼容: 现有本地图像处理代码无需修改
- 简单场景: 单次图像处理无需复杂的异步架构
- 测试友好: 同步代码更容易单元测试

## 性能优化建议

1. **队列容量配置**:
   - 高频采集(>100 FPS): 容量20-50,策略DropNewest
   - 正常采集(30 FPS): 容量10,策略DropNewest
   - 低频采集(<10 FPS): 容量5,策略Block

2. **事件处理优化**:
   - 图像显示使用fire-and-forget模式
   - 关键处理使用wait-all模式确保完成
   - 避免在事件处理器中执行耗时操作

3. **触发器去抖动**:
   - 硬件触发设置合理的DebounceMs(10-50ms)
   - 避免重复触发导致队列积压

4. **内存管理**:
   - 及时释放处理完成的图像
   - 使用对象池复用Mat对象
   - 监控队列大小,避免内存溢出

## 故障排查

### 1. 图像处理延迟高
- 检查工作流算法性能
- 增大队列容量或修改溢出策略
- 优化事件处理器,减少阻塞时间

### 2. 图像丢失率高
- 增大队列容量
- 切换到DropOldest策略保留最新图像
- 优化工作流处理速度

### 3. 内存占用过高
- 减小队列容量
- 检查图像是否正确释放
- 使用内存分析工具查找泄漏

### 4. 事件处理失败
- 检查AsyncEventBus统计信息
- 确认所有事件处理器正确处理异常
- 查看日志中的错误堆栈

## 后续扩展方向

1. **多相机支持**: 扩展为多相机并行采集和处理
2. **分布式处理**: 支持跨机器的分布式工作流执行
3. **云端集成**: 将处理结果上传到云端进行分析
4. **实时监控**: 添加性能监控和告警机制
5. **可视化配置**: 提供UI界面配置触发器和工作流

## 总结

本优化方案通过事件驱动架构实现了:
- ✅ 同步和异步双模式支持
- ✅ 灵活的触发器管理
- ✅ 可配置的图像队列
- ✅ 完全解耦的组件设计
- ✅ 向后兼容的接口

适用于从离线批处理到实时连续处理的各种场景。
