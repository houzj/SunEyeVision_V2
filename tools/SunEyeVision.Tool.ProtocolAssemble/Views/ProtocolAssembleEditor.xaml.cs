using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.Tool.ProtocolAssemble.Views;

public partial class ProtocolAssembleEditor : ToolDebugControlBase
{
    private ProtocolAssembleParameters _parameters = new();
    private bool _updatingDelimiter;
    public override Size PreferredDebugWindowSize => new(400, 550);
    public ProtocolAssembleEditor() { InitializeComponent(); SetParameters(_parameters); }
    public override void SetParameters(ToolParameters parameters)
    {
        if (parameters is not ProtocolAssembleParameters typed) throw new ArgumentException("协议组装参数类型错误。");
        base.SetParameters(parameters);
        System.ComponentModel.PropertyChangedEventManager.RemoveHandler(_parameters, ParametersChanged, "");
        _parameters = typed; DataContext = typed; System.ComponentModel.PropertyChangedEventManager.AddHandler(_parameters, ParametersChanged, "");
        RefreshDelimiter();
    }
    private void ParametersChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(ProtocolAssembleParameters.Separator)) RefreshDelimiter(); }
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
    private void DelimiterChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    { if (!_updatingDelimiter && DelimiterOptions.SelectedItem is CommunicationDelimiterOption option) _parameters.Separator = option.Value; }
    protected override object ExecuteTool() => ExecuteWithLiveContext(new ProtocolAssembleTool(), _parameters);
    private void AddPart_Click(object sender, RoutedEventArgs e)
    {
        var part = new ProtocolAssemblyPart(); _parameters.Parts.Add(part);
        PartsGrid.SelectedItem = part; PartsGrid.ScrollIntoView(part);
    }
    private void DeletePart_Click(object sender, RoutedEventArgs e)
    { if ((sender as FrameworkElement)?.DataContext is ProtocolAssemblyPart part) _parameters.Parts.Remove(part); }
    private void MoveUp_Click(object sender, RoutedEventArgs e) => Move(sender, -1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => Move(sender, 1);
    private void Move(object sender, int step)
    {
        if ((sender as FrameworkElement)?.DataContext is not ProtocolAssemblyPart part) return;
        var index = _parameters.Parts.IndexOf(part); var next = index + step;
        if (index >= 0 && next >= 0 && next < _parameters.Parts.Count) { _parameters.Parts.Move(index, next); PartsGrid.SelectedIndex = next; }
    }
    private void CustomSeparator_Click(object sender, RoutedEventArgs e) { CustomSeparator.Visibility = Visibility.Visible; CustomSeparator.Focus(); }
}
