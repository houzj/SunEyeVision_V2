using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Tool.BlobLabels.Runtime;

namespace SunEyeVision.Tool.BlobLabels.Wpf.Views;

/// <summary>
/// 原件 <c>ThresholdAreaPanel</c> 的迁移：序号 / 灰度值 / 面积范围 / 反选，可增删行。
/// 取值仍写回原厂 <c>ThresholdArea</c> 字符串（序号#新增#灰度#下限#上限#反选），不引入第二套参数模型。
/// </summary>
public sealed class BlobLabelsThresholdAreaPanel : UserControl
{
    private readonly IToolSettingsDraft _draft;
    private readonly Action<FieldSettings>? _acceptCandidate;
    private readonly StackPanel _rows = new() { Orientation = Orientation.Vertical };
    private readonly TextBlock _error = new() { Foreground = System.Windows.Media.Brushes.Firebrick, FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly List<Row> _items = new();
    private bool _applying;

    private sealed record Row(int Index, TextBlock Number, TextBox Gray, TextBox Minimum, TextBox Maximum, CheckBox Inverse, Button Delete);

    public BlobLabelsThresholdAreaPanel(IToolSettingsDraft draft, Action<FieldSettings>? acceptCandidate)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _draft = draft;
        _acceptCandidate = acceptCandidate;
        var root = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 4, 0, 4) };
        root.Children.Add(Header());
        root.Children.Add(new ScrollViewer { Content = _rows, MaxHeight = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        root.Children.Add(new Button { Width = 24, Height = 24, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0), Padding = new Thickness(0) });
        ((Button)root.Children[^1]).Click += (_, _) => AppendRow();
        root.Children.Add(_error);
        Content = root;
        Reload();
    }

    public bool HasPendingEdits { get; private set; }

    private static FrameworkElement Header()
    {
        var grid = Columns();
        void Add(string text, int column)
        {
            var label = new TextBlock { Text = text, FontSize = 12, Foreground = System.Windows.Media.Brushes.DimGray, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(label, column);
            grid.Children.Add(label);
        }
        Add("序号", 0);
        Add("灰度值", 1);
        Add("面积范围", 2);
        Add("反选", 3);
        return grid;
    }

    private static Grid Columns()
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        foreach (var width in new[] { 44d, 70d, 176d, 44d, 44d })
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        return grid;
    }

    private void Reload()
    {
        _applying = true;
        try
        {
            _rows.Children.Clear();
            _items.Clear();
            IReadOnlyList<BlobLabelsThresholdArea> areas;
            try { areas = ReadAreas(); }
            catch (ArgumentException error) { _error.Text = error.Message; areas = Array.Empty<BlobLabelsThresholdArea>(); }
            foreach (var area in areas) AddRowControls(area);
            if (areas.Count != 0) _error.Text = "";
        }
        finally { _applying = false; }
    }

    private IReadOnlyList<BlobLabelsThresholdArea> ReadAreas()
        => _draft.CaptureFields().Read(BlobLabelsFields.ThresholdArea) is ConstantSetting { Value: string text }
            ? BlobLabelsThresholdAreas.Parse(text)
            : Array.Empty<BlobLabelsThresholdArea>();

    private void AppendRow()
    {
        try
        {
            var existing = ReadAreas();
            var next = BlobLabelsThresholdAreas.Append(existing);
            Apply(existing.Append(next));
            Reload();
        }
        catch (ArgumentException error) { _error.Text = error.Message; }
    }

    private void AddRowControls(BlobLabelsThresholdArea area)
    {
        var grid = Columns();
        var number = new TextBlock { Text = area.Index.ToString(CultureInfo.InvariantCulture), VerticalAlignment = VerticalAlignment.Center };
        var gray = Number(area.GrayValue);
        var minimum = Number(area.MinimumArea);
        var maximum = Number(area.MaximumArea);
        var inverse = new CheckBox { IsChecked = area.IsInverse, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        var delete = new Button { Content = "×", Width = 24, Height = 24, Padding = new Thickness(0) };
        void OnEdit(object? sender, EventArgs args) => Commit();
        gray.LostFocus += OnEdit;
        minimum.LostFocus += OnEdit;
        maximum.LostFocus += OnEdit;
        inverse.Checked += OnEdit; inverse.Unchecked += OnEdit;
        delete.Click += (_, _) =>
        {
            _items.RemoveAll(row => ReferenceEquals(row.Number, number));
            Commit(removed: number);
            Reload();
        };
        var range = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        range.Children.Add(minimum);
        range.Children.Add(new TextBlock { Text = " - ", VerticalAlignment = VerticalAlignment.Center });
        range.Children.Add(maximum);
        foreach (var (element, column) in new (UIElement, int)[] { (number, 0), (gray, 1), (range, 2), (inverse, 3), (delete, 4) })
        {
            Grid.SetColumn(element, column);
            grid.Children.Add(element);
        }
        _rows.Children.Add(grid);
        _items.Add(new(area.Index, number, gray, minimum, maximum, inverse, delete));
    }

    private static TextBox Number(int value) => new()
    {
        Text = value.ToString(CultureInfo.InvariantCulture),
        Height = 26,
        Margin = new Thickness(0, 0, 4, 0),
        VerticalContentAlignment = VerticalAlignment.Center
    };

    private void Commit(TextBlock? removed = null)
    {
        if (_applying) return;
        var areas = new List<BlobLabelsThresholdArea>();
        foreach (var row in _items)
        {
            if (ReferenceEquals(row.Number, removed)) continue;
            if (!TryNumber(row.Gray, 1, 255, "灰度值", out var gray)) return;
            if (!TryNumber(row.Minimum, 1, 999_999_999, "面积下限", out var minimum)) return;
            if (!TryNumber(row.Maximum, 1, 999_999_999, "面积上限", out var maximum)) return;
            if (minimum > maximum) { _error.Text = "面积下限不能超过上限。"; return; }
            areas.Add(new(areas.Count + 1, true, gray, minimum, maximum, row.Inverse.IsChecked == true));
        }
        Apply(areas);
    }

    private bool TryNumber(TextBox box, int minimum, int maximum, string name, out int value)
    {
        if (int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= minimum && value <= maximum) return true;
        _error.Text = $"{name}必须是 {minimum} 到 {maximum} 之间的整数。";
        return false;
    }

    private void Apply(IEnumerable<BlobLabelsThresholdArea> areas)
    {
        string encoded;
        try { encoded = BlobLabelsThresholdAreas.Encode(areas); }
        catch (ArgumentException error) { _error.Text = error.Message; return; }
        _error.Text = "";
        _applying = true;
        try
        {
            _draft.ApplyFields(new Dictionary<string, ParameterSetting>(StringComparer.Ordinal)
            {
                [BlobLabelsFields.ThresholdArea] = new ConstantSetting(encoded)
            }, candidate => _acceptCandidate?.Invoke(candidate));
            HasPendingEdits = true;
        }
        catch (ArgumentException error) { _error.Text = error.Message; }
        finally { _applying = false; }
    }
}
