# 主题颜色系统优化实施总结

## 实施时间
2026-04-02

## 优化目标
将主题颜色从 10 个基础颜色扩展到 10 个系列 × 5 个变体 = 50 个主题颜色，符合 Office/WPF 标准颜色选择器风格。

## 核心原则
- **主题颜色**：包含变体（浅色/基础/深色），每系列 5 个颜色
- **标准颜色**：保持简单，只有 10 个常用颜色，无变体

## 实施内容

### 1. 创建 ThemeColorPalette.cs
**文件位置**: `src/Plugin.SDK/UI/Themes/ThemeColorPalette.cs`

**核心类**:

#### ThemeColorSeries 类
```csharp
public class ThemeColorSeries
{
    public string Name { get; }                    // 系列名称
    public Color BaseColor { get; }                // 基础颜色
    public List<Color> LighterVariants { get; }    // 浅色变体（2个）
    public List<Color> DarkerVariants { get; }     // 深色变体（2个）
    public List<Color> AllColors { get; }          // 所有颜色（5个）
}
```

**颜色变体生成算法**:
- 浅色变体：`LightenColor(baseColor, 0.15 * i)` (i = 1, 2)
- 深色变体：`DarkenColor(baseColor, 0.15 * i)` (i = 1, 2)

#### ThemeColorPalette 静态类
```csharp
public static class ThemeColorPalette
{
    public static List<ThemeColorSeries> ThemeColorSeriesList { get; }  // 10个系列
    public static List<Color> StandardColors { get; }                   // 10个标准颜色
    public static uint ColorToUInt(Color color);
    public static Color UIntToColor(uint argb);
}
```

**主题颜色系列（10个）**:
1. 深红 (C00000)
2. 红色 (FF0000)
3. 橙色 (FFC000)
4. 黄色 (FFFF00)
5. 浅绿 (92D050)
6. 绿色 (00B050)
7. 浅蓝 (00B0F0)
8. 蓝色 (0070C0)
9. 紫色 (7030A0)
10. 品红 (FF00FF)

**标准颜色（10个）**:
1. 黑色 (000000)
2. 灰色 (808080)
3. 银色 (C0C0C0)
4. 白色 (FFFFFF)
5. 栗色 (800000)
6. 橄榄色 (808000)
7. 深绿 (008000)
8. 青色 (008080)
9. 深蓝 (000080)
10. 紫色 (800080)

### 2. 更新 ColorPicker.cs
**文件位置**: `src/Plugin.SDK/UI/Controls/ColorPicker.cs`

**主要变更**:

1. **添加命名空间引用**:
```csharp
using SunEyeVision.Plugin.SDK.UI.Themes;
```

2. **更新预设颜色属性**:
```csharp
// 旧代码（已删除）
public static uint[] ThemeColors => new uint[] { ... };
public static uint[] StandardColors => new uint[] { ... };

// 新代码（使用传统属性语法，确保 XAML 能正确识别）
public static List<ThemeColorSeries> ThemeColorSeriesList
{
    get { return ThemeColorPalette.ThemeColorSeriesList; }
}

public static List<Color> StandardColorList
{
    get { return ThemeColorPalette.StandardColors; }
}
```

**注意**: 使用传统属性语法而非表达式体成员（`=>`），确保 XAML 编译器能正确识别静态属性。

3. **更新命令处理方法**:
```csharp
// 支持直接传递 Color 对象
private void OnSelectThemeColorExecuted(object sender, ExecutedRoutedEventArgs e)
{
    if (e.Parameter is Color color)
    {
        SelectedColor = ThemeColorPalette.ColorToUInt(color);
    }
}
```

4. **删除旧的选择方法**:
```csharp
// 已删除 SelectThemeColor(int index) 和 SelectStandardColor(int index)
```

### 3. 更新 Generic.xaml
**文件位置**: `src/Plugin.SDK/UI/Themes/Generic.xaml`

**主要变更**:

1. **添加命名空间引用**:
```xml
xmlns:themes="clr-namespace:SunEyeVision.Plugin.SDK.UI.Themes"
```

2. **主题颜色显示（动态绑定）**:
```xml
<!-- 每行一个系列，包含5个变体 -->
<ItemsControl ItemsSource="{Binding Source={x:Static controls:ColorPicker.ThemeColorSeriesList}}">
    <ItemsControl.ItemTemplate>
        <DataTemplate DataType="themes:ThemeColorSeries">
            <ItemsControl ItemsSource="{Binding AllColors}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Button Command="{x:Static controls:ColorPicker.SelectThemeColorCommand}"
                                CommandParameter="{Binding}">
                            <Button.Background>
                                <SolidColorBrush Color="{Binding}"/>
                            </Button.Background>
                        </Button>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

3. **标准颜色显示（简单网格）**:
```xml
<!-- 使用 WrapPanel 显示标准颜色 -->
<ItemsControl ItemsSource="{Binding Source={x:Static controls:ColorPicker.StandardColorList}}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <WrapPanel/>
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Button Command="{x:Static controls:ColorPicker.SelectStandardColorCommand}"
                    CommandParameter="{Binding}">
                <Button.Background>
                    <SolidColorBrush Color="{Binding}"/>
                </Button.Background>
            </Button>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

## 技术特点

### 1. Office 风格的颜色变体
- 每个主题颜色系列包含 5 个颜色（浅-浅-基础-深-深）
- 变体通过 RGB 插值计算，确保颜色平滑过渡
- 符合 Office/WPF 标准颜色选择器的视觉风格

### 2. 清晰的架构分层
- **ThemeColorPalette**: 集中管理颜色定义和转换
- **ColorPicker**: 提供 UI 控件和命令处理
- **Generic.xaml**: 使用数据绑定动态显示颜色

### 3. 易于扩展
- 添加新的主题颜色系列：只需在 ThemeColorPalette 中添加 ThemeColorSeries
- 添加新的标准颜色：只需在 StandardColors 列表中添加 Color
- 无需修改 XAML 或其他代码

### 4. 类型安全
- 使用 Color 对象而不是 uint 索引
- 通过命令参数传递 Color 对象
- 避免索引越界等错误

## 编译结果
- **状态**: ✅ 编译成功
- **警告**: 122 个（都是现有警告，与本次修改无关）
- **错误**: 0 个

## 使用示例

### 在 XAML 中使用
```xml
<controls:ColorPicker SelectedColor="{Binding DisplayColor, Mode=TwoWay}" />
```

### 主题颜色显示效果
```
系列1: [浅色1] [浅色2] [基础色] [深色1] [深色2]
系列2: [浅色1] [浅色2] [基础色] [深色1] [深色2]
...
系列10: [浅色1] [浅色2] [基础色] [深色1] [深色2]
```

### 标准颜色显示效果
```
[黑色] [灰色] [银色] [白色] [栗色] [橄榄色] [深绿] [青色] [深蓝] [紫色]
```

## 后续优化建议

### 1. 性能优化
- 考虑使用 VirtualizingStackPanel 提升大量颜色时的性能
- 可以添加颜色缓存机制，避免重复计算

### 2. 功能扩展
- 添加"最近使用颜色"功能
- 支持自定义颜色系列
- 添加颜色名称提示（Tooltip）

### 3. 主题支持
- 支持深色主题下的颜色显示
- 提供主题切换功能

## 总结

本次优化成功实现了 Office 风格的主题颜色系统，将主题颜色从 10 个扩展到 50 个（10 系列 × 5 变体），同时保持了标准颜色的简洁性（10 个常用颜色）。通过清晰的架构设计和类型安全的实现，提高了代码的可维护性和可扩展性。

## 问题解决记录

### 问题 1: XAML 绑定错误
**错误信息**:
```
错误 XDG0012 无法识别或访问成员"StandardColorList"
错误 XDG0012 无法识别或访问成员"ThemeColorSeriesList"
```

**原因分析**:
使用了表达式体成员（expression-bodied member）语法：
```csharp
public static List<ThemeColorSeries> ThemeColorSeriesList => ThemeColorPalette.ThemeColorSeriesList;
```

在某些情况下，XAML 编译器可能无法正确识别表达式体成员作为静态属性。

**解决方案**:
改用传统的属性语法：
```csharp
public static List<ThemeColorSeries> ThemeColorSeriesList
{
    get { return ThemeColorPalette.ThemeColorSeriesList; }
}
```

**验证结果**:
- ✅ 编译成功，0 个错误
- ✅ XAML 绑定正常工作
- ✅ 颜色选择功能正常
