using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Native.Numeric.ParameterPresentation.ParamTabControl;
using SunEyeVision.Tool.ColorExtract.Runtime;

namespace SunEyeVision.Tool.ColorExtract.Wpf.Views;

/// <summary>
/// 原厂 <c>ColorExtractList</c>/<c>UserScrollView</c> 的声明字段接缝。
/// 卡片、颜色空间、三通道范围和反转状态都回写到原厂九字段序列化值。
/// </summary>
public sealed class ColorExtractRangePanel : UserControl
{
    private const double ChannelMinimum = 0;
    private const double ChannelMaximum = 255;
    private readonly IToolSettingsDraft _draft;
    private readonly Action<FieldSettings>? _acceptCandidate;
    private readonly StackPanel _cards = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _details = new();
    private readonly TextBlock _error = new() { Foreground = Brushes.Firebrick, FontSize = 12 };
    private readonly ComboBox _colorSpace = new() { Width = 182, Height = 32 };
    private readonly CheckBox _inversion = new() { Content = "颜色反转", VerticalAlignment = VerticalAlignment.Center };
    private readonly SliderArrange[] _low = new SliderArrange[3];
    private readonly SliderArrange[] _high = new SliderArrange[3];
    private IReadOnlyList<ColorExtractRange> _ranges = Array.Empty<ColorExtractRange>();
    private int _selected;
    private bool _loading;

    public ColorExtractRangePanel(IToolSettingsDraft draft, Action<FieldSettings>? acceptCandidate)
    {
        _draft = draft;
        _acceptCandidate = acceptCandidate;
        _colorSpace.ItemsSource = new[]
        {
            new ColorSpaceItem(ColorExtractColorSpace.Rgb, "RGB"),
            new ColorSpaceItem(ColorExtractColorSpace.Hsv, "HSV"),
            new ColorSpaceItem(ColorExtractColorSpace.Hsi, "HSI")
        };
        _colorSpace.DisplayMemberPath = nameof(ColorSpaceItem.DisplayName);
        _colorSpace.SelectedValuePath = nameof(ColorSpaceItem.Value);
        AutomationProperties.SetName(this, "颜色区间面板");
        AutomationProperties.SetName(_colorSpace, "颜色空间");
        AutomationProperties.SetName(_inversion, "颜色反转");
        _colorSpace.SelectionChanged += (_, _) => ApplySelectedRange();
        _inversion.Checked += (_, _) => ApplySelectedRange();
        _inversion.Unchecked += (_, _) => ApplySelectedRange();
        for (var index = 0; index < 3; index++)
        {
            var channel = index;
            _low[index] = CreateSlider(() => ApplyChannel(channel));
            _high[index] = CreateSlider(() => ApplyChannel(channel));
            AutomationProperties.SetName(_low[index], $"通道{index + 1}下限");
            AutomationProperties.SetName(_high[index], $"通道{index + 1}上限");
        }

        var root = new StackPanel();
        root.Children.Add(new TextBlock
        {
            Text = "抽取列表",
            Foreground = Brushes.DimGray,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(18, 0, 0, 0)
        });
        var cardBorder = new Border
        {
            Height = 82,
            Margin = new Thickness(18, 10, 10, 0),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(211, 211, 211)),
            Background = new SolidColorBrush(Color.FromRgb(252, 252, 252)),
            Child = new ScrollViewer
            {
                Height = 80,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                Content = _cards
            }
        };
        root.Children.Add(cardBorder);
        root.Children.Add(new TextBlock
        {
            Text = "参数设置",
            Foreground = Brushes.DimGray,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(18, 16, 0, 5)
        });
        BuildDetails(root);
        root.Children.Add(_error);
        Content = root;
        Reload();
    }

    public bool HasPendingEdits { get; private set; }

    private void BuildDetails(Panel root)
    {
        var spaceRow = new Grid { Height = 32, Margin = new Thickness(18, 0, 0, 0) };
        spaceRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });
        spaceRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        spaceRow.Children.Add(new TextBlock { Text = "颜色空间", VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(_colorSpace, 1);
        spaceRow.Children.Add(_colorSpace);
        root.Children.Add(spaceRow);
        root.Children.Add(_details);
        var inversionRow = new Grid { Height = 32, Margin = new Thickness(18, 10, 0, 0) };
        inversionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });
        inversionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_inversion, 1);
        inversionRow.Children.Add(_inversion);
        root.Children.Add(inversionRow);
    }

    private void Reload()
    {
        _loading = true;
        try
        {
            _ranges = ColorExtractRanges.Parse(ReadText());
            _selected = Math.Clamp(_selected, 0, _ranges.Count - 1);
            RebuildCards();
            RebuildDetails();
            _error.Text = string.Empty;
        }
        catch (ArgumentException error) { _error.Text = error.Message; }
        catch (Exception error) { _error.Text = $"颜色范围面板组装失败：{error.GetType().Name}: {error.Message}"; }
        finally { _loading = false; }
    }

    private string ReadText()
        => _draft.CaptureFields().Read(ColorExtractFields.ColorRange) is ConstantSetting { Value: string text }
            ? text
            : ColorExtractRanges.DefaultSerialized;

    private void RebuildCards()
    {
        _cards.Children.Clear();
        for (var index = 0; index < _ranges.Count; index++)
        {
            var rangeIndex = index;
            var range = _ranges[index];
            var border = new Border
            {
                Width = 60,
                Height = 64,
                Margin = new Thickness(3, 0, 5, 0),
                BorderThickness = new Thickness(1),
                BorderBrush = index == _selected ? new SolidColorBrush(Color.FromRgb(224, 224, 224)) : Brushes.Transparent,
                Background = new SolidColorBrush(Color.FromRgb(252, 252, 252)),
                CornerRadius = new CornerRadius(2)
            };
            AutomationProperties.SetName(border, $"颜色区间{index + 1}");
            var grid = new Grid();
            var ellipse = new Ellipse
            {
                Width = 54,
                Height = 54,
                Fill = CreateBrush(range),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(ellipse);
            var check = new CheckBox
            {
                IsChecked = range.IsChecked,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            AutomationProperties.SetName(check, $"颜色区间{index + 1}启用");
            check.Checked += (_, _) => SetChecked(rangeIndex, true);
            check.Unchecked += (_, _) => SetChecked(rangeIndex, false);
            grid.Children.Add(check);
            border.Child = grid;
            border.MouseLeftButtonUp += (_, e) =>
            {
                if (e.OriginalSource is CheckBox) return;
                _selected = rangeIndex;
                Reload();
            };
            var container = new Grid { Width = 64, Height = 68 };
            container.Children.Add(border);
            var delete = new Button
            {
                Width = 18,
                Height = 18,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = _ranges.Count > 1 ? Visibility.Collapsed : Visibility.Collapsed,
                Content = CreateDeleteImage()
            };
            AutomationProperties.SetName(delete, $"删除颜色区间{index + 1}");
            delete.Click += (_, _) => DeleteRange(rangeIndex);
            container.Children.Add(delete);
            container.MouseEnter += (_, _) => delete.Visibility = _ranges.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            container.MouseLeave += (_, _) => delete.Visibility = Visibility.Collapsed;
            _cards.Children.Add(container);
        }
        var add = new Button
        {
            Width = 64,
            Height = 64,
            Margin = new Thickness(3),
            Padding = new Thickness(0),
            Content = CreateAddImage()
        };
        AutomationProperties.SetName(add, "新增颜色区间");
        add.Click += (_, _) => AddRange();
        _cards.Children.Add(add);
    }

    private void RebuildDetails()
    {
        _details.Children.Clear();
        if (_ranges.Count == 0) return;
        var range = _ranges[_selected];
        _colorSpace.SelectedValue = range.ColorSpace;
        _inversion.IsChecked = range.IsColorInversion;
        for (var index = 0; index < 3; index++)
        {
            _low[index].StartValue = range.LowOf(index + 1);
            _high[index].StartValue = range.HighOf(index + 1);
            var row = new Grid { Height = 64, Margin = new Thickness(18, 10, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new TextBlock { Text = $"通道{index + 1}", VerticalAlignment = VerticalAlignment.Center });
            var sliders = new StackPanel { Orientation = Orientation.Vertical, Width = 182 };
            sliders.Children.Add(_low[index]);
            sliders.Children.Add(_high[index]);
            Grid.SetColumn(sliders, 1);
            row.Children.Add(sliders);
            _details.Children.Add(row);
        }
    }

    private SliderArrange CreateSlider(Action changed)
    {
        var slider = new SliderArrange
        {
            Minimum = ChannelMinimum,
            Maximum = ChannelMaximum,
            SliderTickFrequency = 1,
            Width = 182,
            Height = 28
        };
        slider.OnStartValueChange += (_, _) => changed();
        return slider;
    }

    private void ApplyChannel(int channel)
    {
        if (_loading || _ranges.Count == 0) return;
        var range = _ranges[_selected];
        var values = _ranges.ToArray();
        values[_selected] = channel switch
        {
            0 => range with { C1Low = (int)_low[0].StartValue, C1High = (int)_high[0].StartValue },
            1 => range with { C2Low = (int)_low[1].StartValue, C2High = (int)_high[1].StartValue },
            _ => range with { C3Low = (int)_low[2].StartValue, C3High = (int)_high[2].StartValue }
        };
        ApplyRanges(values);
    }

    private void ApplySelectedRange()
    {
        if (_loading || _ranges.Count == 0) return;
        var range = _ranges[_selected];
        var space = _colorSpace.SelectedValue is ColorExtractColorSpace value ? value : range.ColorSpace;
        ApplyRanges(_ranges.Select((item, index) => index == _selected
            ? item with { ColorSpace = space, IsColorInversion = _inversion.IsChecked == true }
            : item).ToArray());
    }

    private void SetChecked(int index, bool value)
    {
        if (_loading || index >= _ranges.Count) return;
        ApplyRanges(_ranges.Select((item, current) => current == index ? item with { IsChecked = value } : item).ToArray());
    }

    private void AddRange()
    {
        if (_ranges.Count >= ColorExtractRanges.MaximumCount)
        {
            _error.Text = $"颜色区间最多 {ColorExtractRanges.MaximumCount} 项。";
            return;
        }
        _selected = _ranges.Count;
        ApplyRanges(_ranges.Append(ColorExtractRanges.DefaultRange).ToArray());
    }

    private void DeleteRange(int index)
    {
        if (_ranges.Count <= 1) return;
        _selected = Math.Clamp(index - 1, 0, _ranges.Count - 2);
        ApplyRanges(_ranges.Where((_, current) => current != index).ToArray());
    }

    private void ApplyRanges(IReadOnlyList<ColorExtractRange> ranges)
    {
        try
        {
            var encoded = ColorExtractRanges.Format(ranges);
            _draft.ApplyFields(new Dictionary<string, ParameterSetting>(StringComparer.Ordinal)
            {
                [ColorExtractFields.ColorRange] = new ConstantSetting(encoded)
            }, candidate => _acceptCandidate?.Invoke(candidate));
            HasPendingEdits = true;
            _error.Text = string.Empty;
            Reload();
        }
        catch (ArgumentException error) { _error.Text = error.Message; }
        catch (Exception error) { _error.Text = $"颜色范围提交失败：{error.GetType().Name}: {error.Message}"; }
    }

    private static Image CreateAddImage() => new()
    {
        Width = 54,
        Height = 54,
        Source = Resource("btn_add-color_normal.png"),
        Stretch = Stretch.None
    };

    private static Image CreateDeleteImage() => new()
    {
        Width = 18,
        Height = 18,
        Source = Resource("ic_selected_delete_normal.png"),
        Stretch = Stretch.None
    };

    private static BitmapImage Resource(string file)
    {
        var uri = new Uri($"/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/ParameterPresentation/resources/{file}", UriKind.Relative);
        return new BitmapImage(uri);
    }

    private static SolidColorBrush CreateBrush(ColorExtractRange range)
    {
        // 原厂 RangeInfo.SetColor：HSV/HSI 色相允许环回区间（High < Low），
        // 先把 High 加 255 再取中点；普通平均会把环回色相画成错误颜色。
        var c1 = range.ColorSpace is ColorExtractColorSpace.Hsv or ColorExtractColorSpace.Hsi
            ? HueMidpoint(range.C1Low, range.C1High)
            : (range.C1Low + range.C1High) / 2.0;
        var c2 = (range.C2Low + range.C2High) / 2.0;
        var c3 = (range.C3Low + range.C3High) / 2.0;
        return range.ColorSpace switch
        {
            ColorExtractColorSpace.Rgb => new SolidColorBrush(Color.FromRgb((byte)c1, (byte)c2, (byte)c3)),
            ColorExtractColorSpace.Hsv => Hsv(c1, c2, c3),
            _ => Hsi(c1, c2, c3)
        };
    }

    private static double HueMidpoint(int low, int high)
    {
        if (high < low) high += 255;
        return (low + high) / 2.0;
    }

    private static SolidColorBrush Hsv(double h, double s, double v)
    {
        if (h > 255) h -= 255;
        var hue = h / 255.0 * 360.0;
        var saturation = s / 255.0;
        var value = v / 255.0;
        var chroma = value * saturation;
        var x = chroma * (1 - Math.Abs((hue / 60 % 2) - 1));
        var m = value - chroma;
        var (r, g, b) = hue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };
        return new SolidColorBrush(Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255)));
    }

    private static SolidColorBrush Hsi(double h, double s, double i)
    {
        // Ported from the original RangeInfo.HSI2RGB implementation.
        if (h > 255) h -= 255;
        var hue = h / 255.0;
        var saturation = s / 255.0;
        var intensity = i / 255.0;
        var angle = hue * 2 * Math.PI;
        double red;
        double green;
        double blue;
        if (angle >= 0 && angle < 2 * Math.PI / 3)
        {
            blue = intensity * (1 - saturation);
            red = intensity * (1 + saturation * Math.Cos(angle) / Math.Cos(Math.PI / 3 - angle));
            green = 3 * intensity - (red + blue);
        }
        else if (angle < 4 * Math.PI / 3)
        {
            red = intensity * (1 - saturation);
            green = intensity * (1 + saturation * Math.Cos(angle - 2 * Math.PI / 3) / Math.Cos(Math.PI - angle));
            blue = 3 * intensity - (red + green);
        }
        else
        {
            green = intensity * (1 - saturation);
            blue = intensity * (1 + saturation * Math.Cos(angle - 4 * Math.PI / 3) / Math.Cos(5 * Math.PI / 3 - angle));
            red = 3 * intensity - (green + blue);
        }
        return new SolidColorBrush(Color.FromRgb(
            ToByte(red * 255 + 0.5), ToByte(green * 255 + 0.5), ToByte(blue * 255 + 0.5)));
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(value, 0, 255);

    private readonly record struct ColorSpaceItem(ColorExtractColorSpace Value, string DisplayName);

    protected override AutomationPeer OnCreateAutomationPeer()
        => new ColorExtractRangePanelAutomationPeer(this);

    private sealed class ColorExtractRangePanelAutomationPeer(ColorExtractRangePanel owner)
        : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(ColorExtractRangePanel);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override List<AutomationPeer>? GetChildrenCore()
        {
            var peers = new List<AutomationPeer>();
            foreach (var element in NamedDescendants((FrameworkElement)Owner))
            {
                var name = AutomationProperties.GetName(element);
                if (string.IsNullOrWhiteSpace(name)) continue;
                var peer = UIElementAutomationPeer.CreatePeerForElement(element);
                if (peer is not null) peers.Add(peer);
            }
            return peers;
        }

        private static IEnumerable<FrameworkElement> NamedDescendants(DependencyObject root)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                if (VisualTreeHelper.GetChild(root, index) is not FrameworkElement child) continue;
                yield return child;
                foreach (var descendant in NamedDescendants(child)) yield return descendant;
            }
        }
    }
}
