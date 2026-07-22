# ObservableObject V4.0 性能调试指南

## 版本信息
- **版本**: V4.0_20260310_优化版
- **特性**: 订阅者耗时监控 + 调用栈追踪

## 新增功能

### 1. 订阅者级性能监控
`OnPropertyChanged` 现在会记录每个订阅者的调用耗时，帮助你快速定位瓶颈：

```
[PropertyChanged] ========== 开始触发属性变更 ==========
[PropertyChanged] 目标类型: RegionEditorViewModel, 属性名: SelectedRegion
[PropertyChanged] 订阅者数量: 3
[PropertyChanged] → 调用订阅者: RegionEditorControl.OnViewModelPropertyChanged()
[PropertyChanged] ✓ 订阅者 RegionEditorControl.OnViewModelPropertyChanged() 耗时: 2528 ms
[PropertyChanged] 🔥🔥🔥 订阅者 RegionEditorControl.OnViewModelPropertyChanged() 耗时过长: 2528 ms
[PropertyChanged] ========== 属性变更完成，总耗时: 2528 ms ==========
```

### 2. 自动瓶颈识别
- **黄色警告**: 单个订阅者耗时 > 100ms
- **红色错误**: 总耗时 > 1000ms

### 3. 订阅者类型识别
自动显示每个订阅者的类名和方法名：
- `DrawingParameterPanel.OnViewModelPropertyChanged()` - UI层订阅者
- `RegionEditorControl.UpdateRegionOverlay()` - 控件层订阅者
- `StaticMethod` - 静态方法订阅者

## 使用方式

### 步骤1: 编译项目
```powershell
msbuild src/Plugin.SDK/Plugin.SDK.csproj /t:Build /p:Configuration=Debug
```

### 步骤2: 运行应用并触发操作
在日志显示器中观察以下内容：

1. **ObservableObject 加载确认**
```
========================================
ObservableObject 已加载
版本: V4.0_20260310_优化版
时间: 2026-03-10 14:15:30.123
特性: 属性变更监控 + StackTrace调用者追踪 + 订阅者耗时监控
========================================
```

2. **属性变更监控**
当你选择区域时，会看到详细的性能分析：
```
[RegionEditor] SelectedRegion setter 被调用，新值=区域_1, 旧值=null
[性能监控] SetProperty() 耗时: 0 ms, propertyChanged=true
[RegionEditor] ✓ SelectedRegion 已更新，使用延迟更新

[性能监控] UpdateEditingShape() 耗时: 5 ms
[性能监控] 开始通知11个属性变更...
[性能监控]   通知 HasSelection 耗时: 0 ms
[性能监控]   通知 CanEdit 耗时: 0 ms
[性能监控]   通知 SelectedRegionMode 耗时: 0 ms
[性能监控]   通知 SelectedRegionShapeType 耗时: 0 ms
[性能监控]   通知 IsDrawingMode 耗时: 0 ms
[性能监控]   通知 IsSubscribeByRegionMode 耗时: 0 ms
[性能监控]   通知 IsSubscribeByParameterMode 耗时: 0 ms
[性能监控]   通知 ParameterBindings 耗时: 0 ms
[性能监控]   通知 HasParametersVisible 耗时: 0 ms
[性能监控] 🔥 通知 Parameters 耗时: 123 ms
[PropertyChanged] ========== 开始触发属性变更 ==========
[PropertyChanged] 目标类型: RegionEditorViewModel, 属性名: Parameters
[PropertyChanged] 订阅者数量: 1
[PropertyChanged] → 调用订阅者: DrawingParameterPanel.OnViewModelPropertyChanged()
[PropertyChanged] ✓ 订阅者 DrawingParameterPanel.OnViewModelPropertyChanged() 耗时: 123 ms
[PropertyChanged] ========== 属性变更完成，总耗时: 123 ms ==========
```

## 分析瓶颈

### 情况1: 单个订阅者耗时过长
```
[PropertyChanged] 🔥🔥🔥 订阅者 DrawingParameterPanel.OnViewModelPropertyChanged() 耗时过长: 2528 ms
```
**原因**: 该订阅者的实现存在性能问题

**排查步骤**:
1. 查看 `DrawingParameterPanel.xaml.cs` 的 `OnViewModelPropertyChanged` 方法
2. 检查 `UpdateLayout()` 调用耗时
3. 分析绑定刷新操作（`InvalidateVisual()`）

### 情况2: 订阅者数量过多
```
[PropertyChanged] 订阅者数量: 15
[PropertyChanged] ========== 属性变更完成，总耗时: 1200 ms ==========
```
**原因**: 订阅者太多，总耗时累积

**优化方案**:
1. 检查是否所有订阅者都需要响应这个属性
2. 使用条件判断减少不必要的订阅者处理
3. 考虑批量更新机制

### 情况3: Dispatcher 延迟
```
[RegionEditor] SelectedRegion setter 被调用
[性能监控] SelectedRegion setter 总耗时: 2 ms
... (2.5秒后) ...
[性能监控] UpdateEditingShape() 被调用
```
**原因**: `Dispatcher.BeginInvoke(DispatcherPriority.Background)` 导致延迟

**解决方案**:
- 改用 `DispatcherPriority.Normal` 或 `DispatcherPriority.Send`
- 或者直接同步调用（如果不会阻塞UI线程）

## 性能优化建议

### 1. 减少 OnPropertyChanged 调用
```csharp
// ❌ 错误：逐个通知
OnPropertyChanged(nameof(HasSelection));
OnPropertyChanged(nameof(CanEdit));
OnPropertyChanged(nameof(SelectedRegionMode));

// ✅ 正确：批量通知（如果订阅者支持）
// 或者只通知关键属性
```

### 2. 条件订阅
```csharp
private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    // 只处理需要的属性
    if (e.PropertyName != nameof(RegionEditorViewModel.Parameters) &&
        e.PropertyName != nameof(RegionEditorViewModel.SelectedShapeType))
    {
        return;  // 提前返回，避免不必要的处理
    }

    // 处理逻辑...
}
```

### 3. 避免重复布局
```csharp
// ❌ 错误：每次属性变化都触发布局
InvalidateVisual();
UpdateLayout();  // 可能很慢

// ✅ 正确：批量处理，延迟布局
_dispatcher.BeginInvoke(() =>
{
    InvalidateVisual();
    UpdateLayout();
}, DispatcherPriority.Input);
```

## 监控输出示例

### 正常情况
```
[PropertyChanged] ========== 开始触发属性变更 ==========
[PropertyChanged] 目标类型: RegionEditorViewModel, 属性名: SelectedShapeType
[PropertyChanged] 订阅者数量: 2
[PropertyChanged] → 调用订阅者: RegionEditorControl.OnViewModelPropertyChanged()
[PropertyChanged] ✓ 订阅者 RegionEditorControl.OnViewModelPropertyChanged() 耗时: 1 ms
[PropertyChanged] → 调用订阅者: DrawingParameterPanel.OnViewModelPropertyChanged()
[PropertyChanged] ✓ 订阅者 DrawingParameterPanel.OnViewModelPropertyChanged() 耗时: 2 ms
[PropertyChanged] ========== 属性变更完成，总耗时: 3 ms ==========
```

### 性能问题
```
[PropertyChanged] ========== 开始触发属性变更 ==========
[PropertyChanged] 目标类型: RegionEditorViewModel, 属性名: Parameters
[PropertyChanged] 订阅者数量: 1
[PropertyChanged] → 调用订阅者: DrawingParameterPanel.OnViewModelPropertyChanged()
[PropertyChanged] ✓ 订阅者 DrawingParameterPanel.OnViewModelPropertyChanged() 耗时: 2528 ms
[PropertyChanged] 🔥🔥🔥 订阅者 DrawingParameterPanel.OnViewModelPropertyChanged() 耗时过长: 2528 ms
[PropertyChanged] ========== 属性变更完成，总耗时: 2528 ms ==========
[PropertyChanged] 🔥🔥🔥 严重瓶颈：RegionEditorViewModel.Parameters 总耗时 2528 ms，订阅者数量: 1
```

## 下一步行动

1. ✅ 编译并运行应用
2. ✅ 触发区域选择操作
3. ✅ 查看日志显示器中的性能分析
4. ✅ 根据日志定位瓶颈订阅者
5. ✅ 优化瓶颈代码
6. ✅ 重新测试验证

## 技术细节

### 实现原理
```csharp
protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
{
    if (PropertyChanged == null) return;

    // 获取所有订阅者
    var invocationList = PropertyChanged.GetInvocationList();

    // 逐个调用并记录耗时
    foreach (var subscriber in invocationList)
    {
        var subscriberStopwatch = Stopwatch.StartNew();
        subscriber.DynamicInvoke(this, new PropertyChangedEventArgs(propertyName));
        subscriberStopwatch.Stop();

        // 记录耗时并发出警告
        if (subscriberStopwatch.ElapsedMilliseconds > 100)
        {
            PluginLogger.Warning($"🔥🔥🔥 订阅者耗时过长: {subscriberStopwatch.ElapsedMilliseconds} ms", logSource);
        }
    }
}
```

### 性能影响
- **正常情况**: 每次调用增加 < 1ms（Stopwatch + 日志）
- **瓶颈情况**: 可以帮助定位 > 100ms 的订阅者
- **生产环境**: 可通过条件编译或配置开关关闭详细日志

## 相关文档
- [属性变更通知统一规范](property_notification_migration_summary.md)
- [ObservableObject 源代码](../src/Plugin.SDK/Models/ObservableObject.cs)
- [性能优化最佳实践](performance_optimization_guide.md)
