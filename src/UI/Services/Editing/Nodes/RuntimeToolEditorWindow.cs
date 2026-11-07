using SunEyeVision.Plugin.Editing.Wpf;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Controls.Foundation;
using SunEyeVision.Plugin.Wpf.Native.Composition.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Commands;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Fields;

namespace SunEyeVision.UI.Services.Editing;

public sealed class RuntimeToolExecutionEventArgs(DeclaredOutputSnapshot? result, string? error) : EventArgs
{
    public DeclaredOutputSnapshot? Result { get; } = result;
    public string? Error { get; } = error;
}

/// <summary>Production Runtime editor using the original parameter-window composition.</summary>
public sealed class RuntimeToolEditorWindow : BaseWindow, INativeToolCommands
{
    private readonly NativeToolEditorFrame _frame;
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    private IDisposable? _commands;
    private bool _closed, _running, _closing, _closeAllowed, _assemblyReady;
    private Task<bool>? _closeTask;
    private Func<bool> _canRun = () => false;
    public RuntimeNodeConfigurationControl Editor { get; }
    public event EventHandler<RuntimeToolExecutionEventArgs>? ToolExecutionCompleted;
    public event EventHandler? Changed;
    public bool IsCurrent => !_closed;
    public NativeToolCommandState State => new(false, _running || Editor.IsDebugBusy, !_closing && _assemblyReady && !_running && Editor.CanDebug && _canRun(), true);
    public ICommand StartContinuous { get; }
    public ICommand Stop { get; }
    public ICommand ExecuteOnce { get; }
    public ICommand Confirm { get; }

    public RuntimeToolEditorWindow(RuntimeNodeConfigurationControl editor)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
        Width = Plugin.Wpf.Native.Common.Localization.AppLocalizationService.GetLang("Language") == "English" ? 680 : 420; Height = 500;
        Resources.MergedDictionaries.Add(Plugin.Wpf.EditorResources.Create());
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Foundation/BaseWindow.xaml", UriKind.Relative) });
        SetResourceReference(StyleProperty, typeof(BaseWindow));
        _frame = new NativeToolEditorFrame { EditorContent = editor, ExternalContent = _message };
        Content = _frame;
        StartContinuous = new ActionCommand(() => { }, () => false);
        Stop = new ActionCommand(editor.StopDebug, () => !_closed && Editor.IsDebugBusy);
        ExecuteOnce = new ActionCommand(() => _ = RunOnceAsync(), () => !_closed && !_closing && _assemblyReady && !_running && Editor.CanDebug && _canRun());
        Confirm = new ActionCommand(ConfirmEditor, () => !_closed && !_closing && _assemblyReady && !_running);
        editor.ExecutionCompleted += ForwardResult;
        editor.DebugStateChanged += Notify;
        Closing += ClosingEditor;
        editor.AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(PageChanged), true);
        Loaded += (_, _) => { if (!_closed) { _commands = _frame.BindCommands(this); RefreshResetVisibility(); } };
        Closed += (_, _) =>
        {
            _closed = true;
            void Release(Action action) { try { action(); } catch (Exception error) { Foundation.Logging.PluginLogger.Error(error.Message, nameof(RuntimeToolEditorWindow), error); } }
            Release(() => editor.ExecutionCompleted -= ForwardResult);
            Release(() => editor.DebugStateChanged -= Notify);
            Release(editor.Dispose);
            Release(() => editor.RemoveHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(PageChanged)));
            Release(() => _commands?.Dispose()); _commands = null;
            Release(_frame.Dispose);
        };
    }
    private void ForwardResult(object? sender, RuntimeToolExecutionEventArgs args)
    {
        _message.Text = args.Error ?? "";
        _message.Visibility = args.Error is null ? Visibility.Collapsed : Visibility.Visible;
        ToolExecutionCompleted?.Invoke(this, args);
    }
    private void Notify() { Changed?.Invoke(this, EventArgs.Empty); CommandManager.InvalidateRequerySuggested(); }
    private void PageChanged(object sender, SelectionChangedEventArgs args)
    {
        ToolWindowAssemblyTrace.Event("TabChanged", Title ?? "unknown");
        RefreshResetVisibility();
    }
    private void RefreshResetVisibility()
    {
        if (_closed) return;
        var body = Editor.Content as FrameworkElement;
        var settings = body as DeclaredSettingsView ?? body?.FindName("Editor") as DeclaredSettingsView;
        var tabs = (settings?.Content as DeclaredFieldsView)?.Content as TabControl;
        _frame.ResetVisibility = (tabs?.SelectedItem as TabItem)?.Header as string == "运行参数" ? Visibility.Visible : Visibility.Collapsed;
    }
    internal void SetRunAvailability(Func<bool> canRun) { _canRun = canRun ?? throw new ArgumentNullException(nameof(canRun)); Notify(); }
    internal void SetAssemblyPending() { if (_closed) return; _assemblyReady = false; _message.Text = "正在组装参数页…"; _message.Visibility = Visibility.Visible; Notify(); }
    internal void SetAssemblyReady() { if (_closed) return; _assemblyReady = true; _message.Text = ""; _message.Visibility = Visibility.Collapsed; Notify(); }
    internal void RefreshRunAvailability() { if (!_closed) Notify(); }
    private async Task RunOnceAsync()
    {
        if (_closed || _closing || _running || !Editor.CanDebug || !_canRun()) return;
        _running = true; Notify();
        try { await Editor.RunDebugAsync(); }
        catch (Exception error) { Foundation.Logging.PluginLogger.Error(error.Message, nameof(RuntimeToolEditorWindow), error); }
        finally { _running = false; if (!_closed) Notify(); }
    }
    public Task<bool> CloseAndDrainAsync()
    {
        Dispatcher.VerifyAccess();
        if (_closed) return Task.FromResult(true);
        Close(); return _closeTask ?? Task.FromResult(_closed);
    }
    private void ClosingEditor(object? sender,System.ComponentModel.CancelEventArgs args)
    {
        ToolWindowAssemblyTrace.Event("CloseRequested", Title ?? "unknown");
        if (_closeAllowed) return;
        _closing=true; Notify();
        var drain=Editor.CloseDebugAsync();
        if (drain.IsCompletedSuccessfully) { _closeAllowed=true; return; }
        args.Cancel=true;
        _closeTask ??= FinishClosingAsync(drain);
    }
    private async Task<bool> FinishClosingAsync(Task drain)
    {
        try { await drain; _closeAllowed=true; Close(); ToolWindowAssemblyTrace.Event("CloseCompleted", Title ?? "unknown"); return _closed; }
        catch(Exception error)
        {
            _message.Text="调试资源尚未释放："+error.Message;_message.Visibility=Visibility.Visible;
            Foundation.Logging.PluginLogger.Error(error.Message,nameof(RuntimeToolEditorWindow),error);
            return false;
        }
    }
    private void ConfirmEditor()
    {
        if (!TryConfirm(out var error))
        { MessageBox.Show(this, error, "配置未提交", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
    }
    public bool TryConfirm(out string error)
    {
        if (_closed || _closing || _running || !_assemblyReady) { error = "参数页仍在组装，请稍候。"; return false; }
        if (!ToolEditorCommit.TryCommit(Editor, out error)) return false;
        Close();
        if (_closed) return true;
        error = "关闭被取消，编辑窗口仍然打开。";
        return false;
    }
    private sealed class ActionCommand(Action execute, Func<bool> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
        public bool CanExecute(object? parameter) => canExecute();
        public void Execute(object? parameter) { if (canExecute()) execute(); }
    }
}
