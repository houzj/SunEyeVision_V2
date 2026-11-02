using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Controls.Foundation;

namespace SunEyeVision.Plugin.Wpf.Controls.References.Variables;

/// <summary>Original array display and paging over a caller-owned value snapshot.</summary>
public partial class VariableArrayDialog : BaseWindow
{
    private IReadOnlyList<string> _values = Array.Empty<string>();
    private int _page = 1;
    private int _pageCount = 40;
    private bool _closed;

    public sealed record ArrayValueRow(string Index, string Value);
    public ObservableCollection<ArrayValueRow> BindItems { get; } = new();
    public string WindowTitle { get; }
    public int ItemsTotalCount => _values.Count;
    public int PageCount
    {
        get => _pageCount;
        set { if (_closed || value <= 0 || value == _pageCount) return; _pageCount = value; _page = 1; RefreshPage(); OnPropertyChanged(nameof(PageCount)); OnPropertyChanged(nameof(CurrentPageNum)); }
    }
    public int CurrentPageNum
    {
        get => _page;
        set { if (_closed || value == _page) return; _page = value; RefreshPage(); OnPropertyChanged(nameof(CurrentPageNum)); }
    }

    public VariableArrayDialog() : this("数组", DesignerProperties.GetIsInDesignMode(new DependencyObject())
        ? Enumerable.Range(1, 12).Select(index => index.ToString()) : Array.Empty<string>()) { }

    public VariableArrayDialog(string title, IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        WindowTitle = title;
        InitializeComponent();
        SetResourceReference(StyleProperty, typeof(BaseWindow));
        Closed += OnDialogClosed;
        UpdateArrayValue(values);
    }

    public void UpdateArrayValue(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Dispatcher.VerifyAccess();
        if (_closed) return;
        _values = Array.AsReadOnly(values.ToArray());
        _page = 1;
        OnPropertyChanged(nameof(ItemsTotalCount));
        OnPropertyChanged(nameof(CurrentPageNum));
        RefreshPage();
    }

    private void RefreshPage()
    {
        if (_closed) return;
        var totalPages = Math.Max(1, (_values.Count + _pageCount - 1) / _pageCount);
        _page = Math.Clamp(_page, 1, totalPages);
        BindItems.Clear();
        var start = (_page - 1) * _pageCount;
        for (var i = start; i < Math.Min(start + _pageCount, _values.Count); i++)
            BindItems.Add(new ArrayValueRow((i + 1) + ":", _values[i]));
    }

    private void OnDialogClosed(object? sender, EventArgs args)
    {
        _closed = true;
        Closed -= OnDialogClosed;
        BindItems.Clear();
        _values = Array.Empty<string>();
        DataContext = null;
    }
}
