# DispatcherTimer 监控系统使用指南

## 概述

DispatcherTimer监控系统是一个诊断工具，用于监控和诊断DispatcherTimer相关的问题，特别是"Parameter count mismatch"异常。

## 系统组件

### 1. DispatcherTimerMonitor (监控核心)

**位置**: `src/UI/Services/Monitoring/DispatcherTimerMonitor.cs`

**功能**:
- 自动监控所有注册的DispatcherTimer
- 分析Tick事件处理器的签名
- 检测参数数量不匹配问题
- 记录Timer的所有状态变更

**单例访问**:
```csharp
var monitor = DispatcherTimerMonitor.Instance;
```

### 2. MonitoredDispatcherTimer (包装器)

**位置**: `src/UI/Services/Monitoring/MonitoredDispatcherTimer.cs`

**功能**:
- 自动包装DispatcherTimer并注册到监控
- 提供与DispatcherTimer相同的API
- 自动记录所有操作到监控

## 使用方法

### 方法1: 使用MonitoredDispatcherTimer（推荐）

替换现有的DispatcherTimer创建代码：

```csharp
// ❌ 旧代码
var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
timer.Tick += OnTimerTick;
timer.Start();

// ✅ 新代码 - 使用MonitoredDispatcherTimer
var timer = new MonitoredDispatcherTimer(
    interval: TimeSpan.FromSeconds(1),
    timerName: "MyTimer"  // 可选，方便识别
);
timer.Tick += OnTimerTick;
timer.Start();
```

### 方法2: 手动注册到监控

如果需要保留现有代码，可以手动注册：

```csharp
// 创建定时器
var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

// 注册到监控
int timerId = DispatcherTimerMonitor.Instance.RegisterTimer(
    timer: timer,
    ownerName: "MyComponent",
    timerName: "MyTimer"
);

// 正常使用
timer.Tick += OnTimerTick;
timer.Start();
```

## 监控日志文件

### 1. dispatcher_timer_monitor.log

**位置**: 应用程序运行目录

**内容示例**:
```
================================================================================
DispatcherTimerMonitor 初始化: 2026-03-16 09:36:18.843
监控文件: C:\Users\...\dispatcher_timer_monitor.log
================================================================================
[09:36:18.843] Timer注册: ID=1, 名称=BatchUpdateTimer, 所有者=BatchUIUpdater, 间隔=0.033s, 启用=True
[09:36:18.843] Tick事件处理器已注册: Timer ID=1
  - 处理器数量: 1
  - 方法: BatchUIUpdater.OnTick
  - 签名: Void OnTick(System.Object, System.EventArgs)
[09:36:18.950] Timer状态变更: ID=1, 动作=Start
[09:36:19.050] Timer触发: ID=1, 次数=1
[09:36:19.083] Timer触发: ID=1, 次数=2
```

### 2. parameter_mismatch_detailed.log

**位置**: 应用程序运行目录

**触发条件**: 当发生TargetParameterCountException时自动生成

**内容示例**:
```

=== FirstChanceException - TargetParameterCountException ===
时间: 2026-03-16 09:36:24.062
异常: Parameter count mismatch.
异常类型: System.Reflection.TargetParameterCountException

异常堆栈:
   at System.Reflection.MethodBaseInvoker.ThrowTargetParameterCountException()
   at System.Reflection.RuntimeMethodInfo.Invoke(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
   at System.Delegate.DynamicInvokeImpl(Object[] args)
   at System.Windows.Threading.ExceptionWrapper.InternalRealCall(Delegate callback, Object args, Int32 numArgs)
   at System.Windows.Threading.ExceptionWrapper.TryCatchWhen(Object source, Delegate callback, Object args, Int32 numArgs, Delegate catchHandler)

完整调用堆栈:
   at System.Environment.get_StackTrace()
   at SunEyeVision.UI.App.OnFirstChanceException(Object sender, FirstChanceExceptionEventArgs e)
   ...

===================================
```

## 监控报告

### 自动报告

监控服务会每30秒自动生成一次监控报告到Debug输出。

### 手动生成报告

在任何时候都可以手动生成完整的监控报告：

```csharp
// 打印到Debug输出和日志文件
DispatcherTimerMonitor.Instance.PrintAllTimersInfo();

// 或者只获取报告字符串
string report = DispatcherTimerMonitor.Instance.GetAllTimersInfo();
Debug.WriteLine(report);
```

**报告示例**:
```
=== DispatcherTimer 监控报告 ===
生成时间: 2026-03-16 09:36:45.896
Timer总数: 10
--------------------------------------------------------------------------------

Timer ID: 1
  名称: BatchUpdateTimer
  所有者: BatchUIUpdater
  间隔: 0.033秒
  启用: True
  创建时间: 09:36:18.843
  Tick次数: 842
  最后Tick: 09:36:45.893
  最后状态变更: 09:36:18.950
  Tick事件处理器:
    处理器数量: 1
    - 方法: SunEyeVision.UI.Services.Performance.BatchUIUpdater.OnTick
      签名: Void OnTick(System.Object, System.EventArgs)
      参数: Object sender, EventArgs e

Timer ID: 2
  名称: ScrollTimer
  所有者: PropertyPanelControl
  间隔: 0.1秒
  启用: False
  创建时间: 09:36:19.050
  Tick次数: 15
  最后Tick: 09:36:44.123
  最后状态变更: 09:36:44.233
  Tick事件处理器:
    处理器数量: 1
    [错误] 签名验证失败: 参数类型不正确: 期望(object, EventArgs/DispatcherEventArgs), 实际(object, RoutedPropertyChangedEventArgs<double>)
    - 方法: PropertyPanelControl.OnScrollTimerTick
      签名: Void OnScrollTimerTick(System.Object, System.Windows.Controls.Primitives.ScrollEventArgs)
      参数: Object sender, ScrollEventArgs e

================================================================================
```

## 问题诊断

### 问题1: "Parameter count mismatch"异常

**症状**: 定期出现TargetParameterCountException异常

**诊断步骤**:
1. 查看`parameter_mismatch_detailed.log`文件
2. 找到异常发生的时间点
3. 查看同一时间的`dispatcher_timer_monitor.log`日志
4. 找到刚刚触发的Timer
5. 检查该Timer的Tick事件处理器签名

**常见原因**:
- Tick事件处理方法的签名不正确
- 应该是`void OnTick(object sender, EventArgs e)`
- 但实际可能是`void OnTick()`或其他签名

**修复方法**:
```csharp
// ❌ 错误的签名
private void OnTick()  // 缺少参数
{
    // ...
}

// ❌ 错误的签名
private void OnTick(object sender)  // 缺少EventArgs参数
{
    // ...
}

// ❌ 错误的签名
private void OnTick(object sender, RoutedEventArgs e)  // 参数类型错误
{
    // ...
}

// ✅ 正确的签名
private void OnTick(object sender, EventArgs e)
{
    // ...
}
```

### 问题2: 定时器未按预期触发

**诊断步骤**:
1. 生成监控报告：`DispatcherTimerMonitor.Instance.PrintAllTimersInfo()`
2. 检查`Tick次数`是否在增加
3. 检查`启用`状态是否为True
4. 检查`最后Tick`时间是否更新

### 问题3: 性能问题

**诊断步骤**:
1. 查看监控报告中的`Tick次数`
2. 找到Tick次数异常高的Timer
3. 检查间隔是否设置得太短
4. 检查Tick事件处理方法是否有性能问题

## 迁移现有代码

### 批量替换DispatcherTimer

1. 搜索所有`new DispatcherTimer`：
```bash
grep -r "new DispatcherTimer" src/UI/
```

2. 逐个替换：
```csharp
// 添加using
using SunEyeVision.UI.Services.Monitoring;

// 替换创建代码
var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
// 改为
var timer = new MonitoredDispatcherTimer(TimeSpan.FromSeconds(1));
```

3. 可选：添加定时器名称以便识别：
```csharp
var timer = new MonitoredDispatcherTimer(
    TimeSpan.FromSeconds(1),
    timerName: "ThumbnailLoader"  // 描述性的名称
);
```

## 注意事项

1. **性能影响**: 监控系统本身有轻微的性能开销，但在生产环境中是可接受的
2. **日志大小**: 监控日志文件会持续增长，建议定期清理
3. **隐私**: 监控日志可能包含类和方法名称，不包含敏感数据
4. **调试**: 监控系统主要用于开发调试，可以保留在生产环境但需要管理日志

## 高级用法

### 自定义监控间隔

在`App.xaml.cs`中修改报告间隔：

```csharp
private int _monitorReportInterval = 30; // 改为其他值（秒）
```

### 条件性监控

只在调试模式下启用监控：

```csharp
#if DEBUG
var timer = new MonitoredDispatcherTimer(TimeSpan.FromSeconds(1));
#else
var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
#endif
```

### 集成到单元测试

```csharp
[Test]
public void TestTimerBehavior()
{
    var timer = new MonitoredDispatcherTimer(
        TimeSpan.FromMilliseconds(100),
        timerName: "TestTimer"
    );

    int tickCount = 0;
    timer.Tick += (sender, e) => tickCount++;

    timer.Start();
    Thread.Sleep(300); // 等待3次Tick
    timer.Stop();

    Assert.AreEqual(3, tickCount);
    Assert.AreEqual(3, DispatcherTimerMonitor.Instance.GetAllTimersInfo()
        .Split(new[] { "Tick次数:" }, StringSplitOptions.None)
        .Count());
}
```

## 相关文件

- `src/UI/Services/Monitoring/DispatcherTimerMonitor.cs` - 监控核心
- `src/UI/Services/Monitoring/MonitoredDispatcherTimer.cs` - 包装器
- `src/UI/App.xaml.cs` - 初始化和全局异常处理
- `dispatcher_timer_monitor.log` - 监控日志
- `parameter_mismatch_detailed.log` - 异常日志

## 更新日志

### 2026-03-16
- 初始版本
- 支持DispatcherTimer监控
- 支持Tick事件处理器签名验证
- 支持异常详细日志记录
- 支持定期监控报告
