using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Display.VisionUi;
using SunEyeVision.Plugin.Wpf.Native.Display.Apps.ColorFun;

namespace SunEyeVision.Plugin.Wpf.Controls;

/// <summary>组合迁入原色板、页签模板与高级取色算法；只编辑颜色，不承担工具保存。</summary>
public sealed class VmColorPicker : UserControl
{
    private readonly SourceAdvancedColorPicker _advanced;
    private bool _syncing;

    public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
        nameof(SelectedColor), typeof(Color), typeof(VmColorPicker),
        new FrameworkPropertyMetadata(Colors.Red, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChanged));

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public static readonly RoutedEvent SelectedColorChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(SelectedColorChanged), RoutingStrategy.Bubble,
        typeof(RoutedPropertyChangedEventHandler<Color>), typeof(VmColorPicker));
    public event RoutedPropertyChangedEventHandler<Color> SelectedColorChanged
    {
        add => AddHandler(SelectedColorChangedEvent, value);
        remove => RemoveHandler(SelectedColorChangedEvent, value);
    }
    public event EventHandler? PaletteColorChosen;

    public VmColorPicker()
    {
        Width = 208;
        Resources.MergedDictionaries.Add(ColorThemesResourceDictionary.Instance);
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/VisionUi/SourceColorPickerStyles.xaml", UriKind.Relative)
        });
        var palette = new SourceColorPalette();
        _advanced = new SourceAdvancedColorPicker();
        palette.Selected += text =>
        {
            if (ColorConverter.ConvertFromString(text) is not Color color) return;
            SelectRgb(color);
            PaletteColorChosen?.Invoke(this, EventArgs.Empty);
        };
        _advanced.ColorChanged += brush =>
        {
            if (_syncing) return;
            _syncing = true;
            try { SelectRgb(brush.Color); }
            finally { _syncing = false; }
        };
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "默认", Content = palette });
        tabs.Items.Add(new TabItem { Header = "更多", Content = _advanced });
        Content = tabs;
    }

    private void SelectRgb(Color color) => SetCurrentValue(SelectedColorProperty,
        Color.FromArgb(SelectedColor.A, color.R, color.G, color.B));

    private static void OnColorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var picker = (VmColorPicker)sender;
        if (picker._advanced != null && !picker._syncing)
        {
            picker._syncing = true;
            try { picker._advanced.SynchronizeSelection((Color)args.NewValue); }
            finally { picker._syncing = false; }
        }
        picker.RaiseEvent(new RoutedPropertyChangedEventArgs<Color>((Color)args.OldValue, (Color)args.NewValue, SelectedColorChangedEvent));
    }
}
