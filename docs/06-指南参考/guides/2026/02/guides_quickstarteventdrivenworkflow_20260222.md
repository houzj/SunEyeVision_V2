# 事件驱动工作流快速入门指南

## 编译状态

✅ **SunEyeVision.Core** - 编译成功 (0 errors)
✅ **SunEyeVision.Workflow** - 编译成功 (0 errors)
✅ **SunEyeVision.DeviceDriver** - 编译成功 (0 errors)

## 新增核心组件

### 1. 异步事件总线 (AsyncEventBus)
**位置**: `SunEyeVision.Core/Events/AsyncEventBus.cs`

```csharp
// 创建
var eventBus = new AsyncEventBus(logger);

// 订阅事件
eventBus.Subscribe<ImageCapturedEvent>(async evt => {
    Console.WriteLine($"Image: {evt.DeviceId}, Frame: {evt.FrameNumber}");
});

// 发布 (fire-and-forget)
await eventBus.PublishAsync(new ImageCapturedEvent(...));

// 发布 (wait-all)
await eventBus.PublishAndWaitAsync(new ImageCapturedEvent(...));
```

### 2. 图像队列 (ImageQueue)
**位置**: `SunEyeVision.Workflow/ImageQueue.cs`

```csharp
// 创建 (容量10, 丢弃最新策略)
var queue = new ImageQueue(10, QueueOverflowStrategy.DropNewest, logger);

// 生产者: 入队
await queue.EnqueueAsync(image, "camera-001", metadata);

// 消费者: 出队
var entry = await queue.DequeueAsync(cancellationToken);

// 统计信息
var stats = queue.GetStatistics();
Console.WriteLine($"DropRate: {stats.DropRate}%");
```

### 3. 触发器管理器 (TriggerManager)
**位置**: `SunEyeVision.Workflow/TriggerManager.cs`

```csharp
var triggerManager = new TriggerManager(logger, eventBus);

// 注册硬件触发
triggerManager.RegisterHardwareTrigger("io1", new HardwareTriggerConfig {
    IoPin = 1,
    Edge = TriggerEdge.Rising,
    DebounceMs = 10
});

// 注册软件触发
triggerManager.RegisterSoftwareTrigger("manual", new SoftwareTriggerConfig {
    TriggerName = "Manual",
    TriggerKey = "manual"
});

// 注册定时器触发
triggerManager.RegisterTimerTrigger("timer1", new TimerTriggerConfig {
    IntervalMs = 1000
});


// 启动定时器
triggerManager.StartTimerTrigger("timer1");
// 手动触发
triggerManager.FireSoftwareTrigger("manual", image, metadata);
```

### 4. 事件驱动执行器 (EventDrivenExecutor)
**位置**: `SunEyeVision.Workflow/EventDrivenExecutor.cs`

```csharp
// 创建
var executor = new EventDrivenExecutor(
    syncEngine,
    imageQueue,
    logger,
    eventBus,
    pluginManager
);

// 订阅处理完成
executor.ProcessingCompleted += (sender, e) => {
    Console.WriteLine($"Done: Success={e.Result.Success}, Time={e.DurationMs}ms");
};

// 启动
executor.Start("workflow-001");

// 停止
executor.Stop();

// 统计
var stats = executor.GetStatistics();
Console.WriteLine($"SuccessRate: {stats.SuccessRate}%");
```

### 5. 设备驱动扩展
**位置**: `SunEyeVision.DeviceDriver/BaseDeviceDriver.cs`

```csharp
// 触发器模式
camera.SetTriggerMode(TriggerMode.Hardware);  // 硬件触发
camera.SetTriggerMode(TriggerMode.Software);  // 软件触发
camera.SetTriggerMode(TriggerMode.Timer);     // 定时器触发

// 手动触发 (软件触发模式)
var image = camera.TriggerCapture();

// 订阅图像捕获事件
camera.ImageCaptured += (sender, e) => {
    Console.WriteLine($"Captured: {e.DeviceId}, Frame: {e.FrameNumber}");
    // 处理图像或入队
};
```

## 完整集成示例

### 异步工作流 (实时相机处理)

```csharp
// 1. 创建核心组件
var eventBus = new AsyncEventBus(logger);
var imageQueue = new ImageQueue(10, QueueOverflowStrategy.DropNewest, logger);
var triggerManager = new TriggerManager(logger, eventBus);
var syncEngine = new WorkflowExecutionEngine(...);
var executor = new EventDrivenExecutor(syncEngine, imageQueue, logger, eventBus, pluginManager);

// 2. 设置相机
var camera = new SimulatedCameraDriver("camera-001", logger);
camera.Connect();
camera.SetTriggerMode(TriggerMode.Hardware);
camera.StartContinuousCapture();

// 3. 设置事件订阅
camera.ImageCaptured += async (sender, e) => {
    await imageQueue.EnqueueAsync(e.Image, e.DeviceId, null);
};

// 4. 配置触发器
triggerManager.RegisterHardwareTrigger("io1", new HardwareTriggerConfig {
    IoPin = 1,
    Edge = TriggerEdge.Rising
});

// 5. 启动执行器
executor.Start("workflow-001");

Console.WriteLine("Async workflow started!");

// 运行一段时间后
// executor.Stop();
// camera.StopContinuousCapture();
// camera.Disconnect();
```

### 同步工作流 (本地图像处理)

```csharp
// 直接使用同步引擎
var result = await syncEngine.ExecuteWorkflow("workflow-001", localImage);

if (result.Success)
{
    Console.WriteLine("Workflow completed successfully");
}
else
{
    Console.WriteLine($"Errors: {string.Join(", ", result.Errors)}");
}
```

## 性能优化建议

### 队列容量配置

| 场景 | 采集速率 | 容量 | 溢出策略 |
|------|---------|--------|---------|
| 高频采集 | >100 FPS | 20-50 | DropNewest |
| 正常采集 | 30 FPS | 10 | DropNewest |
| 低频采集 | <10 FPS | 5 | Block |

### 触发器配置

- **硬件触发**: DebounceMs 10-50ms, 避免重复触发
- **软件触发**: 适合手动控制场景
- **定时器触发**: IntervalMs根据需求设置,常见值为100-1000ms

### 内存管理

- 及时释放已处理的图像
- 监控队列大小,避免溢出
- 使用`GetStatistics()`定期检查状态

## 故障排查

### 问题: 处理延迟高
**解决方案**:
- 检查工作流算法性能
- 增大队列容量
- 优化事件处理器

### 问题: 图像丢失
**解决方案**:
- 切换到DropOldest策略
- 增大队列容量
- 优化处理速度

### 问题: 内存占用高
**解决方案**:
- 减小队列容量
- 确保图像正确释放
- 查找内存泄漏

## 下一步

详细文档请参考:
- `docs/EventDrivenWorkflowOptimization.md` - 完整优化方案文档

## 架构优势

✅ **解耦设计**: 相机、触发器、处理器完全解耦
✅ **灵活扩展**: 新增功能无需修改现有代码
✅ **双模式支持**: 同步和异步模式共存
✅ **向后兼容**: 现有代码无需修改
✅ **高性能**: 异步处理,fire-and-forget模式
✅ **易测试**: 组件独立,易于单元测试

## 总结

事件驱动工作流优化方案已成功实施,所有核心组件编译通过。系统现在支持:

1. ✅ 异步事件总线 (fire-and-forget + wait-all)
2. ✅ 可配置图像队列 (4种溢出策略)
3. ✅ 统一触发器管理 (硬件/软件/定时器)
4. ✅ 事件驱动执行器
5. ✅ 设备驱动扩展 (触发器模式 + 事件)

适用于从离线批处理到实时连续处理的各种场景。
