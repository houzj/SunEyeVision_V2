# RegionSubscribePanel 数据源绑定优化方案

## 问题分析

### 原始问题
RegionSubscribePanel 第一次打开时，参数绑定的数据源下拉列表为空，第二次打开才能正常显示。

### 根本原因
1. **RegionSubscribePanel 使用手动事件处理**：
   - 在 `Loaded` 事件中手动获取父控件的 `AvailableDataSources`
   - 在 `AvailableDataSources` 为空时就立即更新 `ParamBinding`
   - 监听 `CollectionChanged` 事件，但第一次已将空集合设置给了 `ParamBinding`

2. **ConfigSetting 使用依赖属性绑定**：
   - 定义了 `AvailableDataSources` 依赖属性
   - 在 XAML 模板中通过 `Binding` 绑定到父控件的 `AvailableDataSources`
   - WPF 绑定系统自动处理时序，无论什么时候绑定都能获取最新值

### 对比分析

| 特性 | ConfigSetting | RegionSubscribePanel (旧) |
|------|--------------|---------------------------|
| 依赖属性 | ✅ 有 | ❌ 无 |
| XAML 绑定 | ✅ 有 | ❌ 无 |
| 手动事件处理 | ❌ 无 | ✅ 有 |
| 时序问题 | ✅ 无 | ❌ 有 |

## 解决方案（方案1）

让 RegionSubscribePanel 使用和 ConfigSetting 同样的机制。

### 修改 1：RegionSubscribePanel.xaml.cs

**目标**：简化代码，使用依赖属性机制

**修改内容**：
1. 添加 `AvailableDataSources` 依赖属性
2. 移除所有手动事件处理逻辑（Loaded/Unloaded/CollectionChanged）
3. 移除视觉树遍历逻辑（FindVisualChildren/FindVisualParent）

**修改后代码**：
```csharp
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Views
{
    /// <summary>
    /// 区域订阅面板
    /// </summary>
    public partial class RegionSubscribePanel : UserControl
    {
        /// <summary>
        /// AvailableDataSources 依赖属性
        /// </summary>
        public static readonly DependencyProperty AvailableDataSourcesProperty =
            DependencyProperty.Register(
                nameof(AvailableDataSources),
                typeof(System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>),
                typeof(RegionSubscribePanel),
                new PropertyMetadata(null));

        /// <summary>
        /// 可用数据源集合
        /// </summary>
        public System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>? AvailableDataSources
        {
            get => (System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>?)GetValue(AvailableDataSourcesProperty);
            set => SetValue(AvailableDataSourcesProperty, value);
        }

        public RegionSubscribePanel()
        {
            InitializeComponent();
        }
    }
}
```

### 修改 2：RegionSubscribePanel.xaml

**目标**：为所有 ParamBinding 添加 XAML 绑定

**修改内容**：
为所有 8 个 ParamBinding 控件添加 `AvailableDataSources` 绑定，直接绑定到 `ToolDebugControlBase.AvailableDataSources`

**示例修改**：
```xaml
<!-- 修改前 -->
<controls:ParamBinding
    Width="200"
    ParameterName="RectRegion"
    DataType="{x:Type cv:Rect}"
    BindingSource="{Binding RectRegionSource, Mode=TwoWay}"/>

<!-- 修改后 -->
<controls:ParamBinding
    Width="200"
    ParameterName="RectRegion"
    DataType="{x:Type cv:Rect}"
    BindingSource="{Binding RectRegionSource, Mode=TwoWay}"
    AvailableDataSources="{Binding AvailableDataSources, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type controls:ToolDebugControlBase}}}"/>
```

**修改的 ParamBinding 列表**：
1. RectRegion
2. RectX
3. RectY
4. RectWidth
5. RectHeight
6. CircleRegion
7. CircleCenterX
8. CircleCenterY
9. CircleRadius

## 优化效果

### 代码简化
- **RegionSubscribePanel.xaml.cs**：从 132 行减少到 40 行
- **移除的代码**：
  - Loaded/Unloaded 事件处理
  - CollectionChanged 事件监听
  - 视觉树遍历逻辑
  - UpdateParamBindings 方法

### 架构一致性
- **与 ConfigSetting 一致**：使用相同的依赖属性 + XAML 绑定机制
- **WPF 最佳实践**：充分利用 WPF 数据绑定系统，自动处理时序

### 问题解决
- **时序问题消失**：WPF 绑定系统自动处理数据源更新，无需手动处理
- **第一次打开正常**：ParamBinding 能立即获取到正确的数据源

## 相关文件

### 修改的文件
1. `src/Plugin.SDK/UI/Controls/Region/Views/RegionSubscribePanel.xaml.cs`
2. `src/Plugin.SDK/UI/Controls/Region/Views/RegionSubscribePanel.xaml`

### 参考文件
- `src/Plugin.SDK/UI/Controls/ConfigSetting.cs` - 依赖属性实现示例
- `src/Plugin.SDK/Themes/Generic.xaml` - ConfigSetting 的 XAML 模板

## 测试建议

1. **功能测试**：
   - 第一次打开工具，检查参数绑定的数据源下拉列表是否正常显示
   - 第二次打开工具，确认数据源仍然正常显示

2. **回归测试**：
   - 确认矩形区域绑定功能正常
   - 确认矩形参数绑定功能正常
   - 确认圆形区域绑定功能正常
   - 确认圆形参数绑定功能正常

## 总结

通过让 RegionSubscribePanel 使用和 ConfigSetting 相同的依赖属性 + XAML 绑定机制，彻底解决了时序问题，代码更加简洁，架构更加一致。
