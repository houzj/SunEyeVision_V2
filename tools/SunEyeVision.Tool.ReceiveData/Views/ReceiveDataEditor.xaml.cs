using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.Tool.ReceiveData.Views;

public partial class ReceiveDataEditor : ToolDebugControlBase
{
    private ReceiveDataParameters _parameters = new();
    private bool _refreshing;
    private record DeviceChoice(string Id, string Name);
    public override Size PreferredDebugWindowSize => new(460, 600);
    public ReceiveDataEditor() { InitializeComponent(); SetParameters(_parameters); }
    public override void SetParameters(ToolParameters parameters)
    {
        if (parameters is not ReceiveDataParameters typed) throw new ArgumentException("接收数据参数类型错误。");
        base.SetParameters(parameters); PropertyChangedEventManager.RemoveHandler(_parameters, Changed, "");
        _parameters = typed; DataContext = typed; PropertyChangedEventManager.AddHandler(typed, Changed, ""); Refresh();
    }
    public override void SetDataProvider(object provider) { base.SetDataProvider(provider); Refresh(); }
    private ICommunicationReceiveService? Service => (DataProvider as DataSourceQueryService)?.CommunicationReceiveService;
    private void Changed(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(ReceiveDataParameters.SourceKind)) Refresh(); }
    private void SourceKindChanged(object sender, SelectionChangedEventArgs e) { if (Devices != null) Refresh(); }
    private void RefreshDevices(object? sender, EventArgs e) => Refresh();
    private void Refresh()
    {
        if (Devices == null || _refreshing) return;
        _refreshing = true;
        try
        {
            var sources = Service?.GetSources() ?? Array.Empty<CommunicationReceiveSource>();
            Devices.ItemsSource = sources.GroupBy(s => s.DeviceId).Select(g => new DeviceChoice(g.Key, string.IsNullOrEmpty(g.First().DeviceName) ? g.First().DisplayName : g.First().DeviceName)).ToArray();
            Devices.SelectedItem = Devices.Items.Cast<DeviceChoice>().FirstOrDefault(d => d.Id == _parameters.DeviceId);
            var targets = sources.Where(s => s.DeviceId == _parameters.DeviceId).ToArray();
            Targets.ItemsSource = targets.Where(t => t.TargetId != "").ToArray();
            Targets.SelectedItem = targets.FirstOrDefault(s => s.TargetId == _parameters.TargetId && s.TargetId != "");
            var hasTargets = targets.Any(s => s.TargetId != "");
            Targets.Visibility = TargetLabel.Visibility = hasTargets ? Visibility.Visible : Visibility.Collapsed;
            DeviceOutputName.Width = hasTargets ? double.NaN : 260;
            DeviceOutputName.HorizontalAlignment = HorizontalAlignment.Left;
            if (hasTargets) { DeviceOutputName.Width = 100; }
            DeviceRow.Visibility = DeviceOutput.Visibility = _parameters.SourceKind == "Device" ? Visibility.Visible : Visibility.Collapsed;
            VariableOutput.Visibility = _parameters.SourceKind == "Variable" ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { _refreshing = false; }
    }
    private void DeviceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || Devices.SelectedItem is not DeviceChoice device) return;
        _parameters.DeviceId = device.Id; _parameters.TargetId = ""; _parameters.OutputType = "Binary"; Refresh();
    }
    private async void TargetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || Targets.SelectedItem is not CommunicationReceiveSource source) return;
        _parameters.TargetId = source.TargetId;
        if (source.DataType != "Native") { _parameters.OutputType = source.DataType; return; }
        try
        {
            var sample = await (Service ?? throw new InvalidOperationException("读取服务未接通。" )).ReadAsync(source.DeviceId, source.TargetId);
            if (_parameters.SourceKind == "Device" && _parameters.DeviceId == source.DeviceId && _parameters.TargetId == source.TargetId)
                _parameters.OutputType = CommunicationReceiveService.ValueType(sample.Value);
            Targets.ToolTip = source.DisplayName;
        }
        catch (Exception error) { Targets.ToolTip = error.Message; PluginLogger.Warning(error.Message, "接收数据"); }
    }
    private void SelectVariable_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ReceiveVariableMapping row) return;
        var variable = (DataProvider as DataSourceQueryService)?.SelectReceiveVariable?.Invoke();
        if (variable == null) return;
        if (variable.DataType is not ("Binary" or "String" or "Integer" or "Double" or "Boolean"))
        { PluginLogger.Warning("该变量类型暂不支持接收。", "接收数据"); return; }
        row.VariableId = variable.Id; row.VariablePath = "全局." + variable.Name; row.DataType = variable.DataType;
    }
    private void AddVariable_Click(object sender, RoutedEventArgs e)
    {
        var n = 0; while (_parameters.Variables.Any(v => v.Name == $"out{n}")) n++;
        _parameters.Variables.Add(new() { Name = $"out{n}" });
    }
    private void DeleteVariable_Click(object sender, RoutedEventArgs e)
    { if ((sender as FrameworkElement)?.DataContext is ReceiveVariableMapping row) _parameters.Variables.Remove(row); }
    protected override object ExecuteTool() => ExecuteWithLiveContext(new ReceiveDataTool(), _parameters);
    protected override async Task<object> ExecuteToolAsync()
    {
        var copy = new ReceiveDataParameters(); copy.CopyFrom(_parameters);
        copy.Variables = new(_parameters.Variables.Select(row => new ReceiveVariableMapping
        { FieldId = row.FieldId, Name = row.Name, VariableId = row.VariableId, VariablePath = row.VariablePath, DataType = row.DataType }));
        var request = CreateLiveExecutionRequest(copy, null);
        var executor = new ToolExecutor(new ParameterResolver(DataProvider as IDataSourceQueryService, VisionLogger.Instance), VisionLogger.Instance);
        return await Task.Run(async () => await executor.ExecuteAsync(new ReceiveDataTool(), request));
    }
}
