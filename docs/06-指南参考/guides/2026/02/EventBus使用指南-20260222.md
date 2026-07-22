# EventBus 使用指南

## 概述

EventBus 是一个事件总线实现，提供模块间解耦的通信机制。它使用发布-订阅模式，允许模块之间通过事件进行通信，而无需直接依赖。

## 核心特性

- ✅ **解耦通信**：模块间通过事件通信，降低耦合度
- ✅ **类型安全**：使用泛型确保类型安全
- ✅ **线程安全**：支持多线程环境下的安全使用
- ✅ **异常隔离**：单个处理器异常不影响其他处理器
- ✅ **统计功能**：提供事件总线使用统计信息
- ✅ **灵活订阅**：支持动态订阅和取消订阅

## 快速开始

### 1. 创建 EventBus 实例

```csharp
using SunEyeVision.Events;

// 创建 EventBus 实例
var eventBus = new EventBus(logger);
```

### 2. 定义事件

```csharp
// 继承 EventBase 定义自己的事件
public class MyCustomEvent : EventBase
{
    public string Property1 { get; set; }
    public int Property2 { get; set; }

    public MyCustomEvent(string source) : base(source)
    {
    }
}
```

### 3. 订阅事件

```csharp
// 订阅事件
eventBus.Subscribe<MyCustomEvent>((eventData) =>
{
    Console.WriteLine($"Event received: {eventData.Property1}, {eventData.Property2}");
});
```

### 4. 发布事件

```csharp
// 发布事件
var myEvent = new MyCustomEvent("MyModule")
{
    Property1 = "Hello",
    Property2 = 42
};
eventBus.Publish(myEvent);
```

## 预定义事件

框架提供了一些常用的事件类型：

### WorkflowExecutedEvent
工作流执行完成事件

```csharp
eventBus.Subscribe<WorkflowExecutedEvent>(OnWorkflowExecuted);

void OnWorkflowExecuted(WorkflowExecutedEvent eventData)
{
    Console.WriteLine($"Workflow {eventData.WorkflowName} completed");
    Console.WriteLine($"Success: {eventData.Success}");
    Console.WriteLine($"Duration: {eventData.ExecutionDurationMs}ms");
}
```

### WorkflowNodeExecutedEvent
工作流节点执行完成事件

```csharp
eventBus.Subscribe<WorkflowNodeExecutedEvent>(OnNodeExecuted);

void OnNodeExecuted(WorkflowNodeExecutedEvent eventData)
{
    Console.WriteLine($"Node {eventData.NodeName} completed");
    Console.WriteLine($"Algorithm: {eventData.AlgorithmType}");
    Console.WriteLine($"Duration: {eventData.ExecutionDurationMs}ms");
}
```

### ErrorEvent
错误事件

```csharp
eventBus.Subscribe<ErrorEvent>(OnError);

void OnError(ErrorEvent eventData)
{
    Console.WriteLine($"Error: {eventData.ErrorMessage}");
    Console.WriteLine($"Severity: {eventData.Severity}");
    Console.WriteLine($"Source: {eventData.Source}");
}
```

### LogEvent
日志事件

```csharp
eventBus.Subscribe<LogEvent>(OnLog);

void OnLog(LogEvent eventData)
{
    Console.WriteLine($"[{eventData.LogLevel}] {eventData.Message}");
}
```

## 在 Workflow 模块中使用

### 方式1：使用 WorkflowEventPublisher

```csharp
// 创建事件发布器
var eventPublisher = new WorkflowEventPublisher(eventBus);

// 发布工作流执行事件
eventPublisher.PublishWorkflowStarted("workflow-001", "Image Processing");
eventPublisher.PublishWorkflowExecuted(
    "workflow-001", 
    "Image Processing", 
    success: true, 
    durationMs: 1250, 
    nodesExecuted: 5
);

// 发布节点执行事件
eventPublisher.PublishNodeExecuted(
    "workflow-001",
    "node-001",
    "Gaussian Blur",
    "GaussianBlur",
    success: true,
    durationMs: 245
);
```

### 方式2：使用 EventEnabledWorkflow

```csharp
// 创建支持事件的 Workflow
var workflow = new EventEnabledWorkflow(id, name, logger, eventBus);

// 添加节点
workflow.AddNode(node1);
workflow.AddNode(node2);

// 执行工作流（自动发布事件）
var results = workflow.Execute(inputImage);
```

## 高级用法

### 多个订阅者

```csharp
// 订阅多个处理器到同一个事件
eventBus.Subscribe<LogEvent>((e) => Console.WriteLine($"Handler 1: {e.Message}"));
eventBus.Subscribe<LogEvent>((e) => Console.WriteLine($"Handler 2: {e.Message}"));
eventBus.Subscribe<LogEvent>((e) => Console.WriteLine($"Handler 3: {e.Message}"));

// 发布时所有处理器都会被调用
eventBus.Publish(new LogEvent("Test", "Test message"));
```

### 取消订阅

```csharp
// 定义处理器
EventHandler<LogEvent> handler = (e) => Console.WriteLine(e.Message);

// 订阅
eventBus.Subscribe(handler);

// 取消订阅
eventBus.Unsubscribe(handler);
```

### 查看统计信息

```csharp
// 获取事件总线统计
var stats = eventBus.GetStatistics();
Console.WriteLine(stats);

// 输出示例：
// EventBus Statistics:
//   - Total subscriptions: 10
//   - Total unsubscriptions: 2
//   - Active subscriptions: 8
//   - Total published events: 25
//   - Total executed handlers: 72
//   - Failed handler executions: 1
```

### 清理所有订阅

```csharp
// 清空所有订阅
eventBus.Clear();
```

## 线程安全

EventBus 实现了线程安全，可以在多线程环境下使用：

```csharp
// 多个线程同时发布事件
Parallel.For(0, 10, i =>
{
    eventBus.Publish(new LogEvent($"Thread {i}", $"Message {i}"));
});
```

## 异常处理

EventBus 会捕获并记录处理器中的异常，确保单个处理器的异常不会影响其他处理器：

```csharp
// 订阅可能抛出异常的处理器
eventBus.Subscribe<LogEvent>((e) =>
{
    // 即使这里抛出异常，其他处理器仍会被调用
    throw new InvalidOperationException("Test exception");
});

// 订阅正常的处理器
eventBus.Subscribe<LogEvent>((e) =>
{
    // 这个处理器仍会被调用
    Console.WriteLine("Normal handler executed");
});
```

## 最佳实践

### 1. 使用有意义的事件源

```csharp
// ✅ 好的做法
var event = new LogEvent("WorkflowEngine", "Processing started");

// ❌ 不好的做法
var event = new LogEvent("", "Processing started");
```

### 2. 及时取消不再需要的订阅

```csharp
// 订阅
eventBus.Subscribe<LogEvent>(handler);

// 使用完成后取消订阅
eventBus.Unsubscribe(handler);
```

### 3. 使用弱引用避免内存泄漏

如果订阅者是长期存在的对象，考虑使用弱引用或使用时取消订阅。

### 4. 处理器要快速

事件处理器应该快速执行，避免耗时操作：

```csharp
// ✅ 好的做法 - 快速处理
eventBus.Subscribe<LogEvent>((e) =>
{
    Console.WriteLine(e.Message);
});

// ❌ 不好的做法 - 耗时操作
eventBus.Subscribe<LogEvent>((e) =>
{
    // 不要在事件处理器中执行耗时操作
    Thread.Sleep(1000);
    ProcessLargeData();
});
```

## 完整示例

```csharp
using System;
using SunEyeVision.Events;

class Program
{
    static void Main()
    {
        // 创建 EventBus
        var eventBus = new EventBus(logger);

        // 订阅事件
        SetupSubscriptions(eventBus);

        // 执行工作流
        var workflow = new EventEnabledWorkflow(id, name, logger, eventBus);
        var results = workflow.Execute(image);

        // 显示统计
        Console.WriteLine(eventBus.GetStatistics());

        // 清理
        eventBus.Dispose();
    }

    static void SetupSubscriptions(IEventBus eventBus)
    {
        // 工作流事件
        eventBus.Subscribe<WorkflowExecutedEvent>(OnWorkflowExecuted);
        
        // 节点事件
        eventBus.Subscribe<WorkflowNodeExecutedEvent>(OnNodeExecuted);
        
        // 错误事件
        eventBus.Subscribe<ErrorEvent>(OnError);
    }

    static void OnWorkflowExecuted(WorkflowExecutedEvent e)
    {
        Console.WriteLine($"Workflow {e.WorkflowName} completed in {e.ExecutionDurationMs}ms");
    }

    static void OnNodeExecuted(WorkflowNodeExecutedEvent e)
    {
        Console.WriteLine($"Node {e.NodeName} completed in {e.ExecutionDurationMs}ms");
    }

    static void OnError(ErrorEvent e)
    {
        Console.WriteLine($"Error: {e.ErrorMessage}");
    }
}
```

## API 参考

### IEventBus 接口

```csharp
public interface IEventBus
{
    void Subscribe<TEvent>(EventHandler<TEvent> handler) where TEvent : IEvent;
    void Unsubscribe<TEvent>(EventHandler<TEvent> handler) where TEvent : IEvent;
    void Publish<TEvent>(TEvent eventData) where TEvent : IEvent;
    void Clear();
    string GetStatistics();
}
```

### IEvent 接口

```csharp
public interface IEvent
{
    DateTime Timestamp { get; }
    string Source { get; }
}
```

## 注意事项

1. **性能考虑**：大量事件和订阅者可能影响性能
2. **内存管理**：及时取消不再需要的订阅
3. **异常处理**：处理器内部应该妥善处理异常
4. **事件顺序**：不保证订阅者的执行顺序
5. **递归事件**：避免在事件处理器中发布相同类型的事件，可能导致递归

## 未来扩展

计划中的功能：

- [ ] 事件过滤
- [ ] 事件重放
- [ ] 事件持久化
- [ ] 异步事件处理
- [ ] 事件优先级
- [ ] 事件超时
