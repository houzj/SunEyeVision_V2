# PixelCount 运行参数（阈值范围）架构对照与修复

## 原厂事实（反编译）

来源：`IMVSPixelCountModuVAControl.dll`（SHA-256 `1b0e731c…2ef0b1`），`ilspycmd 11.2` 反编译，BAML 资源已还原为 XAML。

| 类型 | 职责 |
|---|---|
| `Apps.XmlParser.ParamTab.PixelModuRangeConfigList : NameValueItem, IDynamicChild` | 参数对象：`ConfigList`、`AddCommand`、`DeleteCommand`、`DelEnable`、`GetDynamicChild()`；上限 32；新增项 `0/255/1` |
| `Apps.XmlParser.ParamTab.PixelModuRangeConfigItem : IntegerBettween` | 一行就是一个共享参数节点，只重写 `DisplayNameA` 显示序号；两个 `Integer` 子节点 `min_Threshold_i` / `max_Threshold_i` |
| `IMVSPixelCountModuVAControl.UI.RangeConfigListPanel` | 只有 `ItemTemplate` 依赖属性，加载 `ui/rangeconfiglistpanel.xaml` |

容器模板（`RangeConfigListPanel.xaml`）内容：

- 合并 `Apps.Style` 的按钮、ItemsControl、ComboBox 样式，以及 `Apps.XmlUI` 的 `ParamUserControlDataTemplate.xaml`
- 标题 `Threshold Range`
- `ItemsControl ItemsSource="{Binding ConfigList}"`
- 增删按钮分别绑定 `AddCommand` / `DeleteCommand`，删除按钮 `Visibility` 绑定 `DelEnable`
- 图标来自 `Apps.Style` 资源

行模板（`paramusercontroldatatemplate.xaml`）：

```xml
<DataTemplate x:Key="PixelModuRangeConfigList">
  <localUI:RangeConfigListPanel>
    <localUI:RangeConfigListPanel.ItemTemplate>
      <DataTemplate>
        <Grid Margin="10,0">
          <ContentControl ContentTemplate="{StaticResource IntegerBettween}" Content="{Binding}" />
        </Grid>
      </DataTemplate>
    </localUI:RangeConfigListPanel.ItemTemplate>
  </localUI:RangeConfigListPanel>
</DataTemplate>
```

结论：**容器模板可以属于工具，但行、输入控件、引用按钮、按钮样式、图标全部来自公共层；工具不手绘任何一行。**

## 修复前本方实现

`PixelCountRangePanel` 用 `StackPanel + TextBox + 手写 Button + BitmapImage` 重建整张列表：

- 行输入是裸 `TextBox`，没有原厂上下微调
- 引用按钮是画出来的禁用图标，不是订阅会话
- 增删按钮和行布局在工具内
- 持久化是单个字符串 `ThresholdRanges`

## 修复内容

### 新增公共组件

`src/Plugin.Wpf/Fields/NativeRangeListPanel.cs`：

- 标题、`ScrollViewer`（`MaxHeight=233`）、增删命令条
- 行使用迁移后的 `SliderArrangeBase`（`SliderType=between`），即原厂 `IntegerBettween` 模板，自带上下微调和数值校验
- `DelEnable` 等价规则：行数 > 1 才显示删除
- 增删上限、上下限、步长由工具传入
- 区间行没有声明引用会话，链接按钮保留外观但禁用点击，避免进入无数据源的订阅模式

### 工具接缝

`tools/SunEyeVision.PixelCount/Editor/Views/PixelCountRangePanel.cs` 缩减为编码/解码 + 提交：

- 读：`PixelCountRanges.Parse`
- 写：`PixelCountRanges.Format` → `ApplyFields(ThresholdRanges)`
- 不再创建任何输入控件

## 验证

- `dotnet build tools/SunEyeVision.PixelCount/SunEyeVision.Tool.PixelCount.csproj -c Release`：0 错误
- 载荷封存：`85D12D59401451FE29671EA6B8ED32A8C090902A003D1F5507B560A4E90A7CFB`
- 运行截图与逐项对照：待补

## 仍未做

- 容器尚未由公共参数树按 `DisplayType` 实例化，仍由工具显式挂载；这是下一步平台改造。
- 持久化仍是单字符串，原厂为 `Threshold_nums` + 逐条 `min_Threshold_i` / `max_Threshold_i`。
- 结果页「运行参数」多出的页头（section 名与页名重复）未处理。
