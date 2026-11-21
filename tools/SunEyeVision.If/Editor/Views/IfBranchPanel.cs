using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.If.Runtime;

namespace SunEyeVision.Tool.If.Wpf.Views;

/// <summary>
/// 原厂 <c>ButtonSelecter_IfBranch</c> 的动态行迁移。
/// 行文本使用原厂 IfBranchText 编解码，来源列复用正式引用字段模板，数值和状态列复用公共模板。
/// 当前宿主控制输出仍是四个已声明槽位，超过四行的原厂动态端口行为保持未确认并明确拒绝。
/// </summary>
internal sealed class IfBranchPanel : UserControl, IDisposable
{
    private readonly IToolSettingsDraft _draft;
    private readonly Action<FieldSettings>? _acceptCandidate;
    private readonly ReferenceFieldOptions _references;
    private readonly Func<IReadOnlyList<int>> _readModuleIds;
    private readonly StackPanel _rows = new();
    private readonly ComboBox _type = new() { Width = 68, Height = 28 };
    private readonly TextBlock _error = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 4, 8, 0) };
    private readonly List<DeclaredFieldRow> _sourceRows = new();
    private readonly IfBranchDynamicModel _model = new();
    private bool _disposed;
    private bool _loading;

    public IfBranchPanel(IToolSettingsDraft draft, Action<FieldSettings>? acceptCandidate, ReferenceFieldOptions references,
        Func<IReadOnlyList<int>>? readModuleIds = null)
    {
        _draft = draft;
        _acceptCandidate = acceptCandidate;
        _references = references;
        _readModuleIds = readModuleIds ?? (() => Array.Empty<int>());
        AutomationProperties.SetName(this, "条件分支动态行");
        _type.ItemsSource = _model.SelectValueBasicTypes;
        _type.SelectedIndex = _model.SelectTypeIndex;
        AutomationProperties.SetName(_type, "条件类型");
        _type.SelectionChanged += (_, _) => _model.SelectTypeIndex = Math.Max(0, _type.SelectedIndex);
        Content = BuildView();
        Reload();
    }

    private UIElement BuildView()
    {
        var root = new StackPanel { Margin = new Thickness(6, 0, 6, 4) };
        root.Children.Add(new TextBlock { Text = "条件分支", FontWeight = FontWeights.Bold, Foreground = Brushes.DimGray, Margin = new Thickness(8, 3, 0, 4) });
        root.Children.Add(Header());
        root.Children.Add(_rows);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        footer.Children.Add(_type);
        var add = new Button { Width = 28, Height = 28, Padding = new Thickness(0), Margin = new Thickness(6, 0, 0, 0), Content = Image("add_normal.png"), ToolTip = "新增条件" };
        AutomationProperties.SetName(add, "新增条件");
        add.Click += (_, _) => AddRow();
        footer.Children.Add(add);
        root.Children.Add(footer);
        root.Children.Add(_error);
        return root;
    }

    private static FrameworkElement Header()
    {
        var grid = Columns();
        Add(grid, "名称", 0); Add(grid, "条件", 1); Add(grid, "有效值范围", 2); Add(grid, "判断", 3); Add(grid, "执行模块ID", 4); Add(grid, "操作", 5);
        return grid;
    }

    private static Grid Columns()
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        foreach (var width in new[] { 56d, 132d, 180d, 68d, 96d, 34d })
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        return grid;
    }

    private static void Add(Grid grid, string text, int column)
    {
        var label = new TextBlock { Text = text, FontSize = 12, Foreground = Brushes.DimGray, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(label, column); grid.Children.Add(label);
    }

    private void Reload()
    {
        _loading = true;
        try
        {
            foreach (var row in _sourceRows) row.Dispose();
            _sourceRows.Clear(); _rows.Children.Clear(); _error.Text = "";
            var text = ReadBranchText();
            _model.GetParam(text);
            if (_model.Items.Count > IfFields.ConditionCount)
            {
                _error.Text = $"当前宿主只声明 {IfFields.ConditionCount} 个控制输出，无法承载更多原厂动态分支。";
                return;
            }
            for (var index = 0; index < _model.Items.Count; index++) AddRowControls(index, _model.Items[index]);
        }
        catch (Exception error) { _error.Text = "条件分支行组装失败：" + error.Message; }
        finally { _loading = false; }
    }

    private string ReadBranchText()
        => _draft.CaptureFields().Read(IfFields.BranchText) is ConstantSetting { Value: string text } ? text : "0";

    private void AddRow()
    {
        if (_model.Items.Count >= IfFields.ConditionCount)
        {
            _error.Text = $"当前宿主只声明 {IfFields.ConditionCount} 个控制输出。";
            return;
        }
        _model.AddIO();
        Commit();
        Reload();
    }

    private void RemoveRow(IfBranchDynamicRow row)
    {
        var index = _model.Items.IndexOf(row);
        _model.RemoveIO(row);
        var changes = new Dictionary<string, ParameterSetting>(StringComparer.Ordinal)
        {
            [IfFields.BranchText] = new ConstantSetting(_model.SetParamUnit())
        };
        if (index >= 0 && index < IfFields.ConditionCount)
        {
            changes[IfFields.EnabledField(index)] = new ConstantSetting(false);
            changes[IfFields.SourceField(index)] = new UnsetSetting();
        }
        _draft.ApplyFields(changes, candidate => _acceptCandidate?.Invoke(candidate));
        Reload();
    }

    private void AddRowControls(int index, IfBranchDynamicRow row)
    {
        var grid = Columns();
        var name = new TextBlock { Text = row.Name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
        Grid.SetColumn(name, 0); grid.Children.Add(name);

        var sourcePort = IfFields.Schema.Inputs.Single(port => port.Id == IfFields.SourceField(index));
        var source = new DeclaredFieldRow(sourcePort, "", _draft.CaptureFields().Read(sourcePort.Id), setting =>
        {
            _draft.ApplyFields(new Dictionary<string, ParameterSetting>
            {
                [sourcePort.Id] = setting,
                [IfFields.EnabledField(index)] = new ConstantSetting(true)
            }, candidate => _acceptCandidate?.Invoke(candidate));
        }, _references, showClearButton: true);
        source.MinHeight = 28; sourceRowsAdd(source);
        Put(grid, source, 1);

        var minimum = Number(row.Minimum); var maximum = Number(row.Maximum);
        minimum.LostFocus += (_, _) => CommitRange(row, minimum, maximum);
        maximum.LostFocus += (_, _) => CommitRange(row, minimum, maximum);
        var range = new StackPanel { Orientation = Orientation.Horizontal };
        range.Children.Add(minimum); range.Children.Add(new TextBlock { Text = " - ", VerticalAlignment = VerticalAlignment.Center }); range.Children.Add(maximum);
        Put(grid, range, 2);

        var state = new ComboBox { Width = 62, Height = 28, ItemsSource = new[] { IfBranchTextCodec.OkState, IfBranchTextCodec.NgState }, SelectedItem = row.State };
        state.SelectionChanged += (_, _) => { if (!_loading && state.SelectedItem is int value) { row.State = value; Commit(); } };
        Put(grid, state, 3);

        var moduleIds = ReadModuleIds();
        var runId = new ComboBox { Width = 86, Height = 28, ItemsSource = moduleIds, SelectedItem = row.RunModuleId, ToolTip = "IdList 运行时投影，原厂等价性尚未确认" };
        runId.SelectionChanged += (_, _) => { if (!_loading && runId.SelectedItem is int value) { row.RunModuleId = value; Commit(); } };
        Put(grid, runId, 4);

        var delete = new Button { Width = 24, Height = 24, Padding = new Thickness(0), Content = Image("delete_normal.png"), ToolTip = "删除" };
        AutomationProperties.SetName(delete, $"删除条件{index + 1}");
        delete.Click += (_, _) => RemoveRow(row);
        Put(grid, delete, 5);
        _rows.Children.Add(grid);
    }

    private void sourceRowsAdd(DeclaredFieldRow row) { _sourceRows.Add(row); }

    private int[] ReadModuleIds()
    {
        try
        {
            return new[] { IfBranchTextCodec.UnselectedRunModuleId }
                .Concat(_readModuleIds())
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
        }
        catch
        {
            return new[] { IfBranchTextCodec.UnselectedRunModuleId };
        }
    }

    private void CommitRange(IfBranchDynamicRow row, TextBox minimum, TextBox maximum)
    {
        if (_loading || !double.TryParse(minimum.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var low)
            || !double.TryParse(maximum.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var high)) return;
        try { row.Minimum = low; row.Maximum = high; Commit(); } catch (Exception error) { _error.Text = error.Message; }
    }

    private void Commit()
    {
        if (_loading) return;
        try
        {
            _draft.ApplyFields(new Dictionary<string, ParameterSetting>(StringComparer.Ordinal)
            {
                [IfFields.BranchText] = new ConstantSetting(_model.SetParamUnit())
            }, candidate => _acceptCandidate?.Invoke(candidate));
            _error.Text = "";
        }
        catch (Exception error) { _error.Text = error.Message; }
    }

    private static TextBox Number(double value) => new() { Width = 78, Height = 28, Text = value.ToString("R", CultureInfo.InvariantCulture), VerticalContentAlignment = VerticalAlignment.Center };
    private static void Put(Grid grid, UIElement element, int column) { Grid.SetColumn(element, column); grid.Children.Add(element); }
    private static Image Image(string name) => new() { Width = 20, Height = 20, Stretch = Stretch.None, Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/ParameterPresentation/resources/" + name, UriKind.Relative)) };

    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        foreach (var row in _sourceRows) row.Dispose();
        _sourceRows.Clear(); _rows.Children.Clear(); Content = null;
    }
}
