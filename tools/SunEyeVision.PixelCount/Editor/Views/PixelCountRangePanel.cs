using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Tool.PixelCount.Runtime;

namespace SunEyeVision.Tool.PixelCount.Wpf.Views;

/// <summary>
/// 原厂 <c>PixelModuRangeConfigList</c> 的命令条接缝：只负责增删有效区间数量。
/// 区间行是声明字段（32 组固定槽位），由公共参数页按 <c>range.{i}.min/max</c> 渲染，
/// 工具层不创建任何输入控件。
/// </summary>
public sealed class PixelCountRangePanel : UserControl
{
    private readonly IToolSettingsDraft _draft;
    private readonly Action<FieldSettings>? _acceptCandidate;
    private readonly Action? _changed;
    private readonly TextBlock _error = new() { Foreground = Brushes.Firebrick, FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly Button _delete;

    public PixelCountRangePanel(IToolSettingsDraft draft, Action<FieldSettings>? acceptCandidate, Action? changed = null)
    {
        _draft = draft;
        _acceptCandidate = acceptCandidate;
        _changed = changed;
        var add = IconButton("add_normal.png", new Thickness(7, 6, 0, 0));
        add.Click += (_, _) => ChangeCount(+1);
        _delete = IconButton("delete_normal.png", new Thickness(4, 4, 0, 0));
        _delete.Click += (_, _) => ChangeCount(-1);
        var commands = new StackPanel { Orientation = Orientation.Horizontal };
        commands.Children.Add(add);
        commands.Children.Add(_delete);
        var root = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
        root.Children.Add(commands);
        root.Children.Add(_error);
        Content = root;
        UpdateDeleteVisibility();
    }

    public bool HasPendingEdits { get; private set; }

    private long Count
        => _draft.CaptureFields().Read(PixelCountFields.RangeCount) is ConstantSetting { Value: long count } ? count : 1;

    private void ChangeCount(int delta)
    {
        var next = Count + delta;
        if (next < 1 || next > PixelCountRanges.MaximumCount)
        {
            _error.Text = $"阈值范围必须在 1 到 {PixelCountRanges.MaximumCount} 条之间。";
            return;
        }
        try
        {
            _draft.ApplyFields(
                new Dictionary<string, ParameterSetting>(StringComparer.Ordinal)
                {
                    [PixelCountFields.RangeCount] = new ConstantSetting(next)
                },
                candidate => _acceptCandidate?.Invoke(candidate));
            HasPendingEdits = true;
            _error.Text = "";
            UpdateDeleteVisibility();
            _changed?.Invoke();
        }
        catch (ArgumentException error)
        {
            _error.Text = error.Message;
        }
    }

    private void UpdateDeleteVisibility()
        => _delete.Visibility = Count > 1 ? Visibility.Visible : Visibility.Collapsed;

    private static Button IconButton(string name, Thickness margin) => new()
    {
        Content = new Image
        {
            Stretch = Stretch.None,
            Source = new BitmapImage(new Uri(
                $"/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Composition/ParameterPresentation/resources/{name}",
                UriKind.Relative))
        },
        Width = 24,
        Height = 24,
        Margin = margin,
        Padding = new Thickness(0),
        BorderThickness = new Thickness(0),
        Background = Brushes.Transparent
    };
}
