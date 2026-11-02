using SunEyeVision.Plugin.Wpf.Controls.Foundation;
using System.ComponentModel;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

namespace SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Display;

public partial class CrosshairSettingsWindow : BaseWindow
{
    private bool _retiring, _released;
    private Exception? _cleanupError;
    internal Exception? CloseFailure => _cleanupError is null ? LastCloseError : LastCloseError is null ? _cleanupError : new AggregateException(_cleanupError, LastCloseError);
    internal Action<CrosshairSettingsWindow>? AfterClosed { get; set; }
    public CrosshairSettingsWindow()
    {
        InitializeComponent();
        SetResourceReference(StyleProperty, typeof(BaseWindow));
    }
    internal void Retire()
    {
        _retiring = true;
        ParameterCleanup.Run(new Action[] { ReleaseEditor, Close,
            () => { if (CloseFailure is {} error) throw error; } });
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_retiring) e.Cancel = false;
    }
    protected override void OnClosed(EventArgs e)
    {
        try { ReleaseEditor(); } catch (Exception error) { _cleanupError = error; }
        base.OnClosed(e);
        var after = AfterClosed; AfterClosed = null;
        try { after?.Invoke(this); } catch (Exception error) { _cleanupError = _cleanupError is null ? error : new AggregateException(_cleanupError, error); }
    }
    private void ReleaseEditor()
    {
        if (_released) return; _released = true;
        ParameterCleanup.Run(new Action[] { Editor.ReleaseBindings, () => DataContext = null,
            () => Content = null });
    }
}
