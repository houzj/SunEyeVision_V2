using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation.ParamTab;
using SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation.ParamTabControl;
using SunEyeVision.Plugin.Wpf.Native.Input.Interaction.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.ParameterPresentation.ParamTabControl;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>
/// 原厂 <c>ColorExtractList</c> + <c>UserScrollView</c> 的共享控件：
/// 颜色卡片列表、颜色空间、三通道直方图与上下限、颜色反转。
/// 工具只提供九字段区间值并持久化 <see cref="RangesChanged"/>，不自行拼装卡片或通道行。
/// </summary>
public sealed partial class NativeColorRangeList : UserControl
{
    private const double ChannelMinimum = 0;
    private const double ChannelMaximum = 255;
    private readonly ComboBox _colorSpace = new() { Width = 182, Height = 32 };
    private readonly BooleanControl _inversion = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly SliderArrange[] _low = new SliderArrange[3];
    private readonly SliderArrange[] _high = new SliderArrange[3];
    private readonly HistogramSingle[] _histograms = new HistogramSingle[3];
    private readonly ColorSpaceItem[] _spaceItems =
    {
        new(ColorRangeSpace.Rgb, "RGB"),
        new(ColorRangeSpace.Hsv, "HSV"),
        new(ColorRangeSpace.Hsi, "HSI")
    };
    private bool _loading;

    public NativeColorRangeList()
    {
        InitializeComponent();
        AutomationProperties.SetName(this, "颜色区间面板");
        BuildDetails();
        LoadValues(Array.Empty<ColorRangeValue>());
    }

    /// <summary>原厂 ColorExtractList.MAX_NUMBER。</summary>
    public const int MaximumCount = 16;

    public ObservableCollection<NativeColorRangeCard> Cards { get; } = new();

    public NativeColorRangeCard? SelectedCard => CardList.SelectedItem as NativeColorRangeCard;

    /// <summary>任一区间值变化（勾选、滑条、色彩空间、反转、增删）后触发，消费者负责持久化。</summary>
    public event EventHandler? RangesChanged;

    /// <summary>达到原厂最大区间数，或删除到只剩一项时被拒绝。</summary>
    public event EventHandler<ColorRangeRejectedEventArgs>? Rejected;

    public IReadOnlyList<ColorRangeValue> ReadValues() => Cards.Select(card => card.Value).ToArray();

    /// <summary>载入区间并选中指定项；不触发 <see cref="RangesChanged"/>。</summary>
    public void LoadValues(IReadOnlyList<ColorRangeValue> values, int selectedIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(values);
        _loading = true;
        try
        {
            foreach (var card in Cards) card.Changed -= OnCardChanged;
            Cards.Clear();
            foreach (var value in values) AddCardCore(value);
            if (Cards.Count == 0) AddCardCore(ColorRangeValue.Default);
            CardList.SelectedIndex = Math.Clamp(selectedIndex, 0, Cards.Count - 1);
            ApplyDetails();
        }
        finally { _loading = false; }
    }

    private void BuildDetails()
    {
        _colorSpace.ItemsSource = _spaceItems;
        _colorSpace.DisplayMemberPath = nameof(ColorSpaceItem.DisplayName);
        _colorSpace.SelectedValuePath = nameof(ColorSpaceItem.Value);
        _colorSpace.Style = TryFindResource("GlobalItemComboBoxStyle") as Style;
        AutomationProperties.SetName(_colorSpace, "颜色空间");
        _colorSpace.SelectionChanged += (_, _) => OnColorSpaceChanged();

        DetailStack.Children.Add(SpaceRow());

        for (var index = 0; index < 3; index++)
        {
            var channel = index;
            _low[index] = CreateSlider(() => OnSliderChanged(channel));
            _high[index] = CreateSlider(() => OnSliderChanged(channel));
            _histograms[index] = new HistogramSingle
            {
                Width = 108,
                InnerHeight = 50,
                DataPoints = new PointCollection()
            };
            AutomationProperties.SetName(_histograms[index], $"通道{index + 1}直方图");
            AutomationProperties.SetName(_low[index], $"通道{index + 1}下限");
            AutomationProperties.SetName(_high[index], $"通道{index + 1}上限");
            DetailStack.Children.Add(ChannelRow(index));
        }

        _inversion.OffContent = SwitchImage("swith_off.png");
        _inversion.OnContent = SwitchImage("swith_on.png");
        AutomationProperties.SetName(_inversion, "颜色反转");
        _inversion.OnCheckedChange += (_, _) => OnInversionChanged();
        DetailStack.Children.Add(InversionRow());
    }

    private FrameworkElement SpaceRow()
    {
        var row = new LeftRightControl { Height = 32, ChildLeftWidth = 108, Margin = new Thickness(0) };
        row.LeftContent = new TextBlock { Text = Localize("Color Space"), VerticalAlignment = VerticalAlignment.Center };
        row.RightContent = _colorSpace;
        return row;
    }

    private FrameworkElement ChannelRow(int index)
    {
        var row = new LeftRightControl
        {
            Height = 64,
            Margin = new Thickness(0, 10, 0, 0),
            ChildLeftWidth = 108
        };
        row.LeftContent = new TextBlock
        {
            Text = Localize(index switch { 0 => "ChannelOne", 1 => "ChannelTwo", _ => "ChannelThree" }),
            VerticalAlignment = VerticalAlignment.Center
        };
        var histogramBorder = new Border
        {
            Width = 108,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(211, 211, 211)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 0, 0, 0),
            Child = _histograms[index]
        };
        var sliders = new StackPanel { Orientation = Orientation.Vertical, Width = 68, Margin = new Thickness(10, 0, 0, 0) };
        Detach(_low[index]);
        Detach(_high[index]);
        sliders.Children.Add(_low[index]);
        sliders.Children.Add(_high[index]);
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(histogramBorder);
        content.Children.Add(sliders);
        row.RightContent = content;
        return row;
    }

    private FrameworkElement InversionRow()
    {
        var row = new LeftRightControl { Height = 32, ChildLeftWidth = 108, Margin = new Thickness(0, 10, 0, 0) };
        row.LeftContent = new TextBlock { Text = Localize("ColorInversion"), VerticalAlignment = VerticalAlignment.Center };
        row.RightContent = _inversion;
        return row;
    }

    private SliderArrange CreateSlider(Action changed)
    {
        var slider = new SliderArrange
        {
            Minimum = ChannelMinimum,
            Maximum = ChannelMaximum,
            SliderTickFrequency = 1,
            Width = 68,
            Height = 28
        };
        slider.OnStartValueChange += (_, _) => changed();
        return slider;
    }

    private void AddCardCore(ColorRangeValue value)
    {
        var card = new NativeColorRangeCard(value);
        card.Changed += OnCardChanged;
        Cards.Add(card);
    }

    private void OnCardChanged(object? sender, EventArgs e)
    {
        if (_loading) return;
        RangesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        ApplyDetails();
    }

    private void ApplyDetails()
    {
        var card = SelectedCard;
        _loading = true;
        try
        {
            if (card is null) return;
            var value = card.Value;
            _colorSpace.SelectedItem = _spaceItems.FirstOrDefault(item => item.Value == value.ColorSpace);
            for (var index = 0; index < 3; index++)
            {
                _low[index].StartValue = value.LowOf(index + 1);
                _high[index].StartValue = value.HighOf(index + 1);
            }
            _inversion.IsChecked = value.IsColorInversion;
        }
        finally { _loading = false; }
    }

    private void OnColorSpaceChanged()
    {
        if (_loading || SelectedCard is null) return;
        if (_colorSpace.SelectedItem is not ColorSpaceItem item) return;
        SelectedCard.Value = SelectedCard.Value with { ColorSpace = item.Value };
    }

    private void OnSliderChanged(int index)
    {
        if (_loading || SelectedCard is null) return;
        SelectedCard.Value = SelectedCard.Value.WithChannel(
            index + 1, (int)_low[index].StartValue, (int)_high[index].StartValue);
    }

    private void OnInversionChanged()
    {
        if (_loading || SelectedCard is null) return;
        SelectedCard.Value = SelectedCard.Value with { IsColorInversion = _inversion.IsChecked == true };
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        if (Cards.Count >= MaximumCount)
        {
            Rejected?.Invoke(this, new ColorRangeRejectedEventArgs($"颜色区间最多 {MaximumCount} 项。"));
            return;
        }
        _loading = true;
        try
        {
            AddCardCore(ColorRangeValue.Default);
            CardList.SelectedIndex = Cards.Count - 1;
            ApplyDetails();
        }
        finally { _loading = false; }
        CardScroll.ScrollToRightEnd();
        RangesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: NativeColorRangeCard card }) return;
        if (Cards.Count <= 1)
        {
            Rejected?.Invoke(this, new ColorRangeRejectedEventArgs(Localize("Lang_PmuNeedOneRange")));
            return;
        }
        var index = Cards.IndexOf(card);
        card.Changed -= OnCardChanged;
        Cards.Remove(card);
        CardList.SelectedIndex = Math.Clamp(index - 1, 0, Cards.Count - 1);
        ApplyDetails();
        RangesChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void Detach(FrameworkElement element)
    {
        switch (element.Parent)
        {
            case Panel panel: panel.Children.Remove(element); break;
            case ContentControl content: content.Content = null; break;
        }
    }

    private static Image SwitchImage(string file) => new()
    {
        Stretch = Stretch.None,
        Source = new BitmapImage(new Uri(
            $"/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Composition/ParameterPresentation/resources/{file}",
            UriKind.Relative))
    };

    private string Localize(string key) => TryFindResource(key) as string ?? key;

    private sealed record ColorSpaceItem(ColorRangeSpace Value, string DisplayName);
}
