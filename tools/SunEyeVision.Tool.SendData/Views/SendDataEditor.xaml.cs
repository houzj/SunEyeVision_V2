using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.Tool.SendData.Views;

public partial class SendDataEditor : ToolDebugControlBase
{
    private SendDataParameters _parameters = new();
    private bool _refreshing;
    private string _lastMode = "Device";
    private record Choice(string Id, string Name);
    public override Size PreferredDebugWindowSize => new(520, 600);
    public SendDataEditor()
    {
        InitializeComponent();
        Terminators.ItemsSource = new[] { new CommunicationDelimiterOption("无", ""), new("回车", "\r"), new("换行", "\n"), new("回车换行", "\r\n") };
        SetParameters(_parameters);
    }
    private ICommunicationSendService? Service => (DataProvider as DataSourceQueryService)?.CommunicationSendService;
    public override void SetParameters(ToolParameters parameters)
    {
        if (parameters is not SendDataParameters typed) throw new ArgumentException("发送数据参数类型错误。");
        base.SetParameters(parameters); PropertyChangedEventManager.RemoveHandler(_parameters, Changed, "");
        _refreshing = true;
        try { _parameters = typed; _lastMode = typed.DestinationKind; DataContext = typed; PropertyChangedEventManager.AddHandler(typed, Changed, ""); }
        finally { _refreshing = false; }
        Refresh();
    }
    public override void SetDataProvider(object provider) { base.SetDataProvider(provider); Refresh(); }
    private void Changed(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(SendDataParameters.DestinationKind)) Refresh(); }
    private void RefreshChoices(object? sender, EventArgs e) => Refresh();
    private void Refresh()
    {
        if (Devices == null || _refreshing) return; _refreshing = true;
        try
        {
            var catalog = Service?.GetTargets() ?? Array.Empty<CommunicationSendTarget>();
            Devices.ItemsSource = catalog.Where(t => t.Kind == "Device").GroupBy(t => t.DeviceId).Select(g => new Choice(g.Key,
                g.First().Protocol == "Raw" ? g.First().Name : g.First().DeviceName)).ToArray();
            Devices.SelectedValue = _parameters.DeviceId;
            Events.ItemsSource = catalog.Where(t => t.Kind == "Event").ToArray(); Events.SelectedValue = _parameters.EventId;
            if (_parameters.DestinationKind == "Event" && Events.SelectedItem is CommunicationSendTarget selectedEvent)
                foreach (var row in _parameters.Rows)
                    if (selectedEvent.Parameters?.FirstOrDefault(field => field.FieldId == row.EventFieldId) is { } field)
                    { row.Name = field.Name; }
            RowsGrid.Visibility = _parameters.DestinationKind == "Event" && string.IsNullOrWhiteSpace(_parameters.EventId)
                ? Visibility.Collapsed : Visibility.Visible;
            var deviceTargets = catalog.Where(t => t.Kind == "Device" && t.DeviceId == _parameters.DeviceId).ToArray();
            var raw = deviceTargets.Any(t => t.Protocol == "Raw");
            foreach (var row in _parameters.Rows) { row.TargetChoices = deviceTargets; row.RestrictType = _parameters.DestinationKind is "Variable" or "Event"; }
            DevicePanel.Visibility = _parameters.DestinationKind == "Device" ? Visibility.Visible : Visibility.Collapsed;
            EventPanel.Visibility = _parameters.DestinationKind == "Event" ? Visibility.Visible : Visibility.Collapsed;
            RawOptions.Visibility = _parameters.DestinationKind == "Device" && raw ? Visibility.Visible : Visibility.Collapsed;
            TargetColumn.Visibility = _parameters.DestinationKind == "Variable" || (_parameters.DestinationKind == "Device" && !raw) ? Visibility.Visible : Visibility.Collapsed;
            NameColumn.Visibility = _parameters.DestinationKind == "Event" ? Visibility.Visible : Visibility.Collapsed;
            DeleteColumn.Visibility = AddButton.Visibility = _parameters.DestinationKind == "Event" ? Visibility.Collapsed : Visibility.Visible;
            IgnoreOption.Visibility = _parameters.DestinationKind == "Device" && !raw ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception e) { PluginLogger.Warning(e.Message, "发送数据"); }
        finally { _refreshing = false; }
    }
    private void ModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || Devices == null) return;
        if (_lastMode == _parameters.DestinationKind) return;
        _lastMode = _parameters.DestinationKind;
        try { ConfigureVariables(Array.Empty<SendVariablePublication>()); } catch (Exception error) { PluginLogger.Warning(error.Message, "发送数据"); }
        _parameters.Rows = new() { new() }; _parameters.EventId = ""; _parameters.IgnoreErrors = false; Refresh();
    }
    private void DeviceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || Devices.SelectedItem is not Choice device) return;
        _parameters.DeviceId = device.Id; _parameters.Hex = false; _parameters.Separator = _parameters.Terminator = "";
        foreach (var row in _parameters.Rows) row.TargetId = ""; Refresh();
    }
    private void EventChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || Events.SelectedItem is not CommunicationSendTarget target) return;
        _parameters.EventId = target.Id;
        _parameters.Rows = new(target.Parameters?.Select(p => new SendDataRow { Name = p.Name, EventFieldId = p.FieldId, DataType = CommunicationSendTypes.Canonical(p.DataType) }) ?? Array.Empty<SendDataRow>());
        Refresh();
    }
    private void SelectVariable(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not SendDataRow row) return;
        var selected = (DataProvider as DataSourceQueryService)?.SelectReceiveVariable?.Invoke();
        if (selected == null) return;
        if (selected.DataType is not ("String" or "Integer" or "Double" or "Boolean" or "Binary"))
        { PluginLogger.Warning("该变量类型尚未接入发送工具。", "发送数据"); return; }
        var previous = (row.TargetId, row.TargetPath, row.DataType);
        try
        {
            row.TargetId = selected.Id; row.TargetPath = "全局." + selected.Name; row.DataType = selected.DataType;
            ConfigureVariables(_parameters.Rows.Where(r => r.TargetId != "").Select(r => new SendVariablePublication(r.TargetId,
                new CommunicationOutputField(r.FieldId, r.Name, r.DataType).BindingKey, r.DataType)).ToArray());
        }
        catch (Exception error)
        { (row.TargetId, row.TargetPath, row.DataType) = previous; PluginLogger.Warning(error.Message, "发送数据"); }
    }
    private void ConfigureVariables(IReadOnlyList<SendVariablePublication> mappings)
    {
        var id = CurrentNode?.GetType().GetProperty("Id")?.GetValue(CurrentNode)?.ToString();
        if (id != null && Service != null) Service.ConfigureVariablePublications(id, mappings);
    }
    private void AddRow(object sender, RoutedEventArgs e)
    {
        var n = 0; while (_parameters.Rows.Any(r => r.Name == $"out{n}")) n++;
        _parameters.Rows.Add(new() { Name = $"out{n}" }); Refresh();
    }
    private void DeleteRow(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not SendDataRow row) return;
        _parameters.Rows.Remove(row);
        if (_parameters.DestinationKind == "Variable")
            ConfigureVariables(_parameters.Rows.Where(r => r.TargetId != "").Select(r => new SendVariablePublication(r.TargetId,
                new CommunicationOutputField(r.FieldId, r.Name, r.DataType).BindingKey, r.DataType)).ToArray());
    }
    protected override object ExecuteTool() => ExecuteWithLiveContext(new SendDataTool(), _parameters);
    protected override async Task<object> ExecuteToolAsync()
    {
        var copy = System.Text.Json.JsonSerializer.Deserialize<SendDataParameters>(System.Text.Json.JsonSerializer.Serialize(_parameters))!;
        var request = CreateLiveExecutionRequest(copy, null);
        var executor = new ToolExecutor(new ParameterResolver(DataProvider as IDataSourceQueryService, VisionLogger.Instance), VisionLogger.Instance);
        return await executor.ExecuteAsync(new SendDataTool(), request);
    }
}
