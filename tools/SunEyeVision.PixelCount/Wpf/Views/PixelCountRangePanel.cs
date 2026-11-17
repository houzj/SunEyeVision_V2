using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Tool.PixelCount.Runtime;

namespace SunEyeVision.Tool.PixelCount.Wpf.Views;

/// <summary>原厂 RangeConfigListPanel 的声明字段接缝，保留动态区间的增删与严格持久化。</summary>
public sealed class PixelCountRangePanel : UserControl
{
    private readonly IToolSettingsDraft _draft;
    private readonly Action<FieldSettings>? _acceptCandidate;
    private readonly StackPanel _rows = new();
    private readonly TextBlock _error = new() { Foreground = Brushes.Firebrick, FontSize = 12 };
    private readonly List<(TextBox Low, TextBox High)> _items = new();
    private bool _applying;

    public PixelCountRangePanel(IToolSettingsDraft draft, Action<FieldSettings>? acceptCandidate)
    {
        _draft = draft;
        _acceptCandidate = acceptCandidate;
        var root = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
        root.Children.Add(new TextBlock { Text = "阈值范围", FontWeight = FontWeights.Bold, FontSize = 14, Foreground = Brushes.DimGray });
        root.Children.Add(new ScrollViewer { Content = _rows, MaxHeight = 233, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var add = new Button { Content = "+", Width = 24, Height = 24, Margin = new Thickness(7, 6, 0, 0), Padding = new Thickness(0) };
        add.Click += (_, _) => AddRange();
        root.Children.Add(add);
        root.Children.Add(_error);
        Content = root;
        Reload();
    }

    public bool HasPendingEdits { get; private set; }

    private void Reload()
    {
        _applying = true;
        _rows.Children.Clear();
        _items.Clear();
        try
        {
            foreach (var range in PixelCountRanges.Parse(ReadText())) AddRow(range);
            _error.Text = "";
        }
        catch (ArgumentException error) { _error.Text = error.Message; }
        finally { _applying = false; }
    }

    private string ReadText()
        => _draft.CaptureFields().Read(PixelCountFields.ThresholdRanges) is ConstantSetting { Value: string text } ? text : PixelCountRanges.DefaultEncoded;

    private void AddRange()
    {
        try
        {
            var ranges = PixelCountRanges.Parse(ReadText()).ToList();
            if (ranges.Count >= PixelCountRanges.MaximumCount) throw new ArgumentException($"阈值范围最多 {PixelCountRanges.MaximumCount} 条。");
            ranges.Add(PixelCountRanges.Default);
            Apply(PixelCountRanges.Format(ranges));
            Reload();
        }
        catch (ArgumentException error) { _error.Text = error.Message; }
    }

    private void AddRow(PixelCountRange range)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        var low = Number(range.Minimum);
        var high = Number(range.Maximum);
        var delete = new Button { Content = "×", Width = 24, Height = 24, Padding = new Thickness(0) };
        low.LostFocus += (_, _) => Commit();
        high.LostFocus += (_, _) => Commit();
        delete.Click += (_, _) =>
        {
            _items.Remove((low, high));
            Commit();
            Reload();
        };
        row.Children.Add(new TextBlock { Text = "范围", Width = 44, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(low);
        row.Children.Add(new TextBlock { Text = " - ", VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(high);
        row.Children.Add(delete);
        _rows.Children.Add(row);
        _items.Add((low, high));
    }

    private static TextBox Number(int value) => new()
    {
        Text = value.ToString(CultureInfo.InvariantCulture), Width = 72, Height = 26,
        Margin = new Thickness(0, 0, 4, 0), VerticalContentAlignment = VerticalAlignment.Center
    };

    private void Commit()
    {
        if (_applying) return;
        try
        {
            var ranges = _items.Select(item =>
            {
                if (!int.TryParse(item.Low.Text, out var low) || !int.TryParse(item.High.Text, out var high))
                    throw new ArgumentException("阈值范围必须是整数。");
                return new PixelCountRange(low, high);
            }).ToArray();
            Apply(PixelCountRanges.Format(ranges));
        }
        catch (ArgumentException error) { _error.Text = error.Message; }
    }

    private void Apply(string encoded)
    {
        _applying = true;
        try
        {
            _draft.ApplyFields(new Dictionary<string, ParameterSetting>(StringComparer.Ordinal)
            {
                [PixelCountFields.ThresholdRanges] = new ConstantSetting(encoded)
            }, candidate => _acceptCandidate?.Invoke(candidate));
            HasPendingEdits = true;
            _error.Text = "";
        }
        finally { _applying = false; }
    }
}
