using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Controls.Foundation;
using SunEyeVision.Plugin.Wpf.Native.Composition.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Commands;

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
    private CancellationTokenSource? _continuous;
    private bool _closed, _running;
    public RuntimeNodeConfigurationControl Editor { get; }
    public event EventHandler<RuntimeToolExecutionEventArgs>? ToolExecutionCompleted;
    public event EventHandler? Changed;
    public bool IsCurrent => !_closed;
    public NativeToolCommandState State => new(_continuous is not null, _running, !_running, true);
    public ICommand StartContinuous { get; }
    public ICommand Stop { get; }
    public ICommand ExecuteOnce { get; }
    public ICommand Confirm { get; }

    public RuntimeToolEditorWindow(RuntimeNodeConfigurationControl editor)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
        Width = 370; Height = 420;
        Resources.MergedDictionaries.Add(Plugin.Wpf.EditorResources.Create());
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Foundation/BaseWindow.xaml", UriKind.Relative) });
        SetResourceReference(StyleProperty, typeof(BaseWindow));
        _frame = new NativeToolEditorFrame { EditorContent = editor, ExternalContent = _message };
        Content = _frame;
        StartContinuous = new ActionCommand(() => _ = RunContinuousAsync(), () => !_closed && !_running && _continuous is null);
        Stop = new ActionCommand(StopContinuous, () => !_closed && _continuous is not null);
        ExecuteOnce = new ActionCommand(() => _ = RunOnceAsync(), () => !_closed && !_running && _continuous is null);
        Confirm = new ActionCommand(ConfirmEditor, () => !_closed && !_running);
        editor.ExecutionCompleted += ForwardResult;
        Loaded += (_, _) => { if (!_closed) _commands = _frame.BindCommands(this); };
        Closed += (_, _) =>
        {
            _closed = true;
            void Release(Action action) { try { action(); } catch (Exception error) { Plugin.SDK.Logging.PluginLogger.Error(error.Message, nameof(RuntimeToolEditorWindow), error); } }
            Release(StopContinuous);
            Release(() => editor.ExecutionCompleted -= ForwardResult);
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
    private async Task RunOnceAsync()
    {
        if (_closed || _running) return;
        _running = true; Notify();
        try { await Editor.RunDebugAsync(); }
        catch (Exception error) { Plugin.SDK.Logging.PluginLogger.Error(error.Message, nameof(RuntimeToolEditorWindow), error); StopContinuous(); }
        finally { _running = false; if (!_closed) Notify(); }
    }
    private async Task RunContinuousAsync()
    {
        if (_closed || _running || _continuous is not null) return;
        var cancellation = new CancellationTokenSource(); _continuous = cancellation; Notify();
        try
        {
            while (!_closed && !cancellation.IsCancellationRequested)
            {
                await RunOnceAsync();
                if (cancellation.IsCancellationRequested || _closed) break;
                await Task.Delay(500, cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally { if (ReferenceEquals(_continuous, cancellation)) _continuous = null; cancellation.Dispose(); if (!_closed) Notify(); }
    }
    private void StopContinuous() { var current = _continuous; _continuous = null; current?.Cancel(); if (!_closed) Notify(); }
    private void ConfirmEditor()
    {
        if (!TryConfirm(out var error))
        { MessageBox.Show(this, error, "配置未提交", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
    }
    public bool TryConfirm(out string error)
    {
        if (_closed || _running) { error = "编辑器正在关闭或执行。"; return false; }
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
