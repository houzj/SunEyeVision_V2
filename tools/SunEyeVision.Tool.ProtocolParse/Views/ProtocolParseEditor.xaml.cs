using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.Tool.ProtocolParse.Views;

public partial class ProtocolParseEditor : ToolDebugControlBase
{
    private ProtocolParseParameters _parameters = new();
    private bool _updatingDelimiter;
    private readonly HashSet<ProtocolParseField> _trackedFields = new();
    private System.Collections.ObjectModel.ObservableCollection<ProtocolParseField>? _observedOutputs;
    public override Size PreferredDebugWindowSize => new(520, 600);
    public ProtocolParseEditor()
    {
        InitializeComponent(); TypeColumn.ItemsSource = new[] { "int", "float", "string" };
        OrderColumn.ItemsSource = new[] { "ABCD", "BADC", "DCBA", "CDAB" };
        AvailableDataSources.CollectionChanged += (_, _) => RefreshAdvanced();
        SetParameters(_parameters);
    }
    public override void SetParameters(ToolParameters parameters)
    {
        if (parameters is not ProtocolParseParameters typed) throw new ArgumentException("协议解析参数类型错误。");
        base.SetParameters(parameters); PropertyChangedEventManager.RemoveHandler(_parameters, ParametersChanged, "");
        if (_observedOutputs != null) _observedOutputs.CollectionChanged -= OutputsChanged;
        foreach (var field in _trackedFields) PropertyChangedEventManager.RemoveHandler(field, FieldChanged, "");
        _trackedFields.Clear();
        _parameters = typed; DataContext = typed; PropertyChangedEventManager.AddHandler(typed, ParametersChanged, "");
        _observedOutputs = _parameters.Outputs;
        _observedOutputs.CollectionChanged += OutputsChanged;
        TrackFields(); RefreshMode(); RefreshDelimiter();
    }
    private void ParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProtocolParseParameters.Mode)) RefreshMode();
        if (e.PropertyName == nameof(ProtocolParseParameters.Separator)) RefreshDelimiter();
        if (e.PropertyName == nameof(ProtocolParseParameters.Outputs))
        {
            if (_observedOutputs != null) _observedOutputs.CollectionChanged -= OutputsChanged;
            _observedOutputs = _parameters.Outputs;
            _observedOutputs.CollectionChanged += OutputsChanged;
            SelectedFieldOptions.SetCurrentValue(ItemsControl.ItemsSourceProperty, _parameters.Outputs);
            TrackFields();
        }
        RefreshAdvanced();
    }
    private void RefreshMode()
    {
        var visible = _parameters.Mode == "Bytes" ? Visibility.Visible : Visibility.Collapsed;
        ByteSettings.Visibility = visible;
        TextSettings.Visibility = visible == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        StartColumn.Visibility = EndColumn.Visibility = OrderColumn.Visibility = visible;
        RefreshAdvanced();
    }
    private void RefreshDelimiter()
    {
        _updatingDelimiter = true;
        try
        {
            var options = CommunicationDelimiterCatalog.Defaults.ToList();
            if (!options.Any(o => o.Value == _parameters.Separator)) options.Add(new("自定义", _parameters.Separator));
            DelimiterOptions.ItemsSource = options; DelimiterOptions.SelectedValue = _parameters.Separator;
        }
        finally { _updatingDelimiter = false; }
    }
    private void DelimiterChanged(object sender, SelectionChangedEventArgs e)
    { if (!_updatingDelimiter && DelimiterOptions.SelectedItem is CommunicationDelimiterOption option) _parameters.Separator = option.Value; }
    protected override object ExecuteTool() => ExecuteWithLiveContext(new ProtocolParseTool(), _parameters);
    private void AddField_Click(object sender, RoutedEventArgs e)
    {
        var index = 0; while (_parameters.Outputs.Any(f => f.Name == "out" + index)) index++;
        var field = new ProtocolParseField { Name = "out" + index, Start = 0, End = 0 };
        _parameters.Outputs.Add(field); OutputsGrid.SelectedItem = field; OutputsGrid.ScrollIntoView(field);
    }
    private void DeleteField_Click(object sender, RoutedEventArgs e)
    { if ((sender as FrameworkElement)?.DataContext is ProtocolParseField field) _parameters.Outputs.Remove(field); }
    private void MoveUp_Click(object sender, RoutedEventArgs e) => Move(sender, -1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => Move(sender, 1);
    private void Move(object sender, int step)
    {
        if ((sender as FrameworkElement)?.DataContext is not ProtocolParseField field) return;
        var index = _parameters.Outputs.IndexOf(field); var next = index + step;
        if (index >= 0 && next >= 0 && next < _parameters.Outputs.Count)
        { _parameters.Outputs.Move(index, next); OutputsGrid.SelectedIndex = next; }
    }
    private void CustomSeparator_Click(object sender, RoutedEventArgs e) { CustomSeparator.Visibility = Visibility.Visible; CustomSeparator.Focus(); }

    private void OutputsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    { TrackFields(); RefreshAdvanced(); }
    private void TrackFields()
    {
        foreach (var field in _trackedFields) PropertyChangedEventManager.RemoveHandler(field, FieldChanged, "");
        _trackedFields.Clear();
        foreach (var field in _parameters.Outputs)
        { _trackedFields.Add(field); PropertyChangedEventManager.AddHandler(field, FieldChanged, ""); }
        if (SelectedFieldOptions.SelectedItem is not ProtocolParseField selected || !_parameters.Outputs.Contains(selected))
            SelectedFieldOptions.SelectedItem = _parameters.Outputs.FirstOrDefault();
    }
    private void FieldChanged(object? sender, PropertyChangedEventArgs e) => RefreshAdvanced();
    private void GridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedFieldOptions != null && OutputsGrid.SelectedItem is ProtocolParseField field)
            SelectedFieldOptions.SelectedItem = field;
    }
    private void SelectedFieldChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OutputsGrid != null && SelectedFieldOptions.SelectedItem is ProtocolParseField field)
            OutputsGrid.SelectedItem = field;
        RefreshAdvanced();
    }
    private void RefreshAdvanced()
    {
        if (InputFormatRow == null) return;
        var declaration = AvailableDataSources.FirstOrDefault(source =>
            source.GetBindingPath() == _parameters.InputText ||
            _parameters.InputText.StartsWith(source.GetBindingPath() + "[", StringComparison.Ordinal));
        var declaredType = declaration?.PropertyType;
        if (declaredType?.IsArray == true) declaredType = declaredType.GetElementType();
        var binary = declaredType == typeof(BinaryPayload) || declaration?.VariableType == "Binary";
        var stringInput = !binary && (declaration == null || declaredType == typeof(string) || declaredType == typeof(object));
        var bytes = _parameters.Mode == "Bytes";
        InputFormatRow.Visibility = bytes && stringInput ? Visibility.Visible : Visibility.Collapsed;
        EncodingRow.Visibility = bytes && !_parameters.AsciiData &&
            ((stringInput && _parameters.InputFormat == "Text") || _parameters.Outputs.Any(field => field.DataType == "string"))
            ? Visibility.Visible : Visibility.Collapsed;
        NumericRow.Visibility = bytes && !_parameters.AsciiData &&
            SelectedFieldOptions.SelectedItem is ProtocolParseField { DataType: "int" }
            ? Visibility.Visible : Visibility.Collapsed;
    }
}
