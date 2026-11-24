using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 原厂 VM.ModuleControl.PxSlider（hppyramidfeaturepatmatchdialoguistyle.xaml:679/721）的等价实现：
/// <b>继承 Slider</b>（与原厂一致），Value 即 RangeBase.Value，双向绑定天然生效；
/// 只把模板换成「轨道 + 数值框 + px」的浮层外观。
/// 原实现误用 UserControl + 自定义 Value DP，导致 Popup 里 TwoWay 绑定不回写 PanRectWidth。
/// </summary>
public sealed class PxSlider : Slider
{
    private const string TemplateXaml = """
<ControlTemplate TargetType="{x:Type Slider}"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Border Background="White" BorderBrush="#C2C2C2" BorderThickness="1" Padding="10,8" MinWidth="380">
    <Grid>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="Auto" />
      </Grid.ColumnDefinitions>
      <Track x:Name="PART_Track" Grid.Column="0" VerticalAlignment="Center" MinWidth="200" Margin="0,0,10,0">
        <Track.DecreaseRepeatButton>
          <RepeatButton Command="Slider.DecreaseLarge" Background="#E7EAEA" BorderThickness="0" Height="4" />
        </Track.DecreaseRepeatButton>
        <Track.Thumb>
          <Thumb Width="12" Height="22" Background="#F5F5F5" BorderBrush="#808080" BorderThickness="1" />
        </Track.Thumb>
        <Track.IncreaseRepeatButton>
          <RepeatButton Command="Slider.IncreaseLarge" Background="#E7EAEA" BorderThickness="0" Height="4" />
        </Track.IncreaseRepeatButton>
      </Track>
      <TextBox Grid.Column="1" Width="62" Height="26" TextAlignment="Center" VerticalContentAlignment="Center"
               BorderBrush="#C2C2C2" BorderThickness="1" Foreground="#666666"
               Text="{Binding Value, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}" />
      <TextBlock Grid.Column="2" Text="px" Foreground="#666666" VerticalAlignment="Center" Margin="6,0,0,0" />
    </Grid>
  </Border>
</ControlTemplate>
""";

    public PxSlider()
    {
        Template = (ControlTemplate)XamlReader.Parse(TemplateXaml);
        // 原厂笔刷以像素为单位（PanRectWidth 默认 10），按 1px 步进。
        Minimum = 1;
        Maximum = 200;
        TickFrequency = 1;
        IsSnapToTickEnabled = true;
        FocusVisualStyle = null;

        Loaded += (_, _) =>
        {
            // 浮层在 Popup 里，DataContext 继承链不保证成立；打开时对齐到窗口数据模型，
            // 让 Value 的 TwoWay 绑定有可写源（原厂 DataContext 由 TemplatedParent 直接给出）。
            var windowData = Window.GetWindow(this)?.DataContext;
            if (windowData is not null && !ReferenceEquals(DataContext, windowData))
                DataContext = windowData;
        };

        // 原厂 XAML 用 EventTrigger 监听这两个事件（拖动期间抑制画布 ROI 绘制）。
        AddHandler(Thumb.DragStartedEvent,
            new DragStartedEventHandler((_, _) => OnThumbMouseLeftButtonDown?.Invoke(this, EventArgs.Empty)));
        AddHandler(Thumb.DragCompletedEvent,
            new DragCompletedEventHandler((_, _) => OnThumbMouseLeftButtonUp?.Invoke(this, EventArgs.Empty)));
    }

    /// <summary>拖拽开始：原厂 XAML 的 EventTrigger 挂在这里。</summary>
    public event EventHandler? OnThumbMouseLeftButtonDown;

    /// <summary>拖拽结束。</summary>
    public event EventHandler? OnThumbMouseLeftButtonUp;

    /// <summary>原厂 PxSlider 的拖拽开始钩子（供 CallMethodAction 调用）。</summary>
    public void ThumbMouseLeftButtonDown() { }

    /// <summary>原厂 PxSlider 的拖拽结束钩子。</summary>
    public void ThumbMouseLeftButtonUp() { }
}
