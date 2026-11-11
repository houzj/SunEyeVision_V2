using SunEyeVision.Plugin.Editing.Wpf;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Controls.Foundation;
using SunEyeVision.Plugin.Wpf.Native.Composition.Parameters;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
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
    private IDisposable? _commands;
    private bool _closed, _running, _closing, _closeAllowed, _assemblyReady;
    private readonly TaskCompletionSource<bool> _assemblyCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource<bool>? _closeCompletion;
    private Func<bool> _canRun = () => false;
    public RuntimeNodeConfigurationControl Editor { get; }
    public event EventHandler<RuntimeToolExecutionEventArgs>? ToolExecutionCompleted;
    public event EventHandler? Changed;
    public bool IsCurrent => !_closed;
    internal bool CanCompleteAssembly => !_closed && !_closing;
    public NativeToolCommandState State => new(Editor.IsContinuousDebugRequested, _running || Editor.IsDebugBusy, RunVisible: true, ConfirmVisible: true);
    public ICommand StartContinuous { get; }
    public ICommand Stop { get; }
    public ICommand ExecuteOnce { get; }
    public ICommand Confirm { get; }
    public ICommand HelpCommand { get; }
    internal Uri? HelpTopic { get; }
    internal Task AssemblyReadyTask => _assemblyCompletion.Task;

    public RuntimeToolEditorWindow(RuntimeNodeConfigurationControl editor, string? toolId = null)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
        Width = Plugin.Wpf.Native.Common.Localization.AppLocalizationService.GetLang("Language") == "English" ? 680 : 420; Height = 500;
        Resources.MergedDictionaries.Add(Plugin.Wpf.EditorResources.Shared);
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Foundation/BaseWindow.xaml", UriKind.Relative) });
        SetResourceReference(StyleProperty, typeof(BaseWindow));
        _frame = new NativeToolEditorFrame { EditorContent = editor };
        Content = _frame;
        HelpTopic = ResolveHelpTopic(toolId);
        HelpCommand = new ActionCommand(OpenHelp, () => HelpTopic is not null && !_closed && !_closing && IsVisible);
        if (HelpTopic is not null)
            TitleBar = new ContentControl { Content = string.Empty, ContentTemplate = (DataTemplate)_frame.FindResource("ToolWindowTitle") };
        StartContinuous = new ActionCommand(() => _ = RunContinuousAsync(), CanStartDebug);
        Stop = new ActionCommand(editor.StopContinuousDebug, () => !_closed && !_closing && Editor.IsContinuousDebugRequested);
        ExecuteOnce = new ActionCommand(() => _ = RunOnceAsync(), CanStartDebug);
        Confirm = new ActionCommand(ConfirmEditor, () => !_closed && !_closing && _assemblyReady && !_running && !Editor.IsDebugBusy);
        _frame.ResetCommand = new ActionCommand(ConfirmResetRunParameters, CanResetRunParameters);
        editor.ExecutionCompleted += ForwardResult;
        editor.DebugStateChanged += Notify;
        editor.ParameterPageChanged += PageChanged;
        Loaded += (_, _) => { if (!_closed) { _commands?.Dispose(); _commands = _frame.BindCommands(this); RefreshResetVisibility(); } };
        Closed += (_, _) =>
        {
            _closed = true;
            _assemblyCompletion.TrySetCanceled();
            void Release(Action action) { try { action(); } catch (Exception error) { Foundation.Logging.PluginLogger.Error(error.Message, nameof(RuntimeToolEditorWindow), error); } }
            Release(() => editor.ExecutionCompleted -= ForwardResult);
            Release(() => editor.DebugStateChanged -= Notify);
            Release(editor.Dispose);
            Release(() => editor.ParameterPageChanged -= PageChanged);
            Release(() => _commands?.Dispose()); _commands = null;
            Release(_frame.Dispose);
            _closeCompletion?.TrySetResult(true);
        };
    }
    private static Uri? ResolveHelpTopic(string? toolId)
    {
        var anchor = toolId switch
        {
            "ImageLoad" => "tool-image-source",
            "BlobAnalysis" => "tool-blob",
            _ => null
        };
        return anchor is null ? null : new UriBuilder(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "Help", "usermanual", "index.html")))
        {
            Fragment = anchor
        }.Uri;
    }
    private void OpenHelp()
    {
        var topic = HelpTopic!;
        try
        {
            if (!File.Exists(topic.LocalPath)) throw new FileNotFoundException("未找到本地工具帮助。", topic.LocalPath);
            Process.Start(new ProcessStartInfo(topic.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            if (IsVisible && !_closing)
                NativeMessageBox.ShowOwned(this, AppLocalizationService.GetLang("Lang_Help", "帮助"),
                    error.Message, MessageButtons.Normal, MessageIcons.Warning);
        }
    }
    private void ForwardResult(object? sender, RuntimeToolExecutionEventArgs args) => ToolExecutionCompleted?.Invoke(this, args);
    private void Notify() { Changed?.Invoke(this, EventArgs.Empty); CommandManager.InvalidateRequerySuggested(); }
    private void PageChanged()
    {
        ToolWindowAssemblyTrace.Event("TabChanged", Title ?? "unknown");
        RefreshResetVisibility();
        CommandManager.InvalidateRequerySuggested();
    }
    private void RefreshResetVisibility()
    {
        if (_closed) return;
        _frame.ResetVisibility = Editor.IsRunParameterPage ? Visibility.Visible : Visibility.Collapsed;
    }
    private bool CanResetRunParameters() => !_closed && !_closing && _assemblyReady && !_running
        && !Editor.IsDebugBusy && Editor.IsRunParameterPage;
    private bool CanStartDebug() => !_closed && !_closing && _assemblyReady && !_running && Editor.CanDebug && _canRun();

    private void ConfirmResetRunParameters()
    {
        var title = AppLocalizationService.GetLang("Lang_Sure", "确认");
        if (NativeMessageBox.ShowOwned(this, title,
                AppLocalizationService.GetLang("Lang_ResetAlgoParamTips", "确定要将所有算法参数恢复初始值吗") + "？",
                MessageButtons.YesOrNo, MessageIcons.Question) != Plugin.Wpf.Native.Display.PresentationControls.DialogResult.Yes
            || !CanResetRunParameters()) return;
        try
        {
            Editor.ResetRunParameters();
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            if (!_closed && !_closing)
                NativeMessageBox.ShowOwned(this, title, error.Message, MessageButtons.Normal, MessageIcons.Error);
            return;
        }
        if (!_closed && !_closing)
            NativeMessageBox.ShowOwned(this, title, AppLocalizationService.GetLang("Lang_ResetAlgoParamSuccess", "算法参数恢复初始值成功"),
                MessageButtons.Normal, MessageIcons.Ok);
    }
    internal void SetRunAvailability(Func<bool> canRun) { _canRun = canRun ?? throw new ArgumentNullException(nameof(canRun)); Notify(); }
    internal void SetAssemblyPending() { if (_closed) return; _assemblyReady = false; Notify(); }
    internal void SetAssemblyReady() { if (_closed) return; _assemblyReady = true; _assemblyCompletion.TrySetResult(true); Notify(); }
    internal void SetAssemblyFailure(Exception error)
    {
        if (_closed) return;
        _assemblyCompletion.TrySetException(error);
        Notify();
    }
    internal void RefreshRunAvailability() { if (!_closed) Notify(); }
    private async Task RunOnceAsync()
    {
        if (!CanStartDebug()) return;
        var previous = Editor.CurrentDebugSnapshot;
        _running = true;
        try { Notify(); await Editor.RunDebugAsync(); ShowFailedResult(previous); }
        catch (OperationCanceledException) { }
        catch (Exception error) { ReportRunError(error); }
        finally { _running = false; if (!_closed) Notify(); }
    }
    private async Task RunContinuousAsync()
    {
        if (!CanStartDebug()) return;
        var previous = Editor.CurrentDebugSnapshot;
        try { await Editor.StartContinuousDebugAsync(); ShowFailedResult(previous); }
        catch (OperationCanceledException) { }
        catch (Exception error) { ReportRunError(error); }
        finally { if (!_closed) Notify(); }
    }
    private void ReportRunError(Exception error)
    {
        Foundation.Logging.PluginLogger.Error(error.Message, nameof(RuntimeToolEditorWindow), error);
        ShowDiagnostic(error.Message);
    }
    private void ShowFailedResult(DeclaredOutputSnapshot? previous)
    {
        if (Editor.CurrentDebugSnapshot is { IsSuccess: false, ErrorMessage: { } message } current
            && !ReferenceEquals(previous, current))
            ShowDiagnostic(message);
    }
    private void ShowDiagnostic(string message, bool duringClose = false)
    {
        if (_closed || (!duringClose && _closing) || !IsVisible || string.IsNullOrWhiteSpace(message)) return;
        NativeMessageBox.ShowOwned(this, AppLocalizationService.GetLang("Lang_ParamSet", "参数设置"),
            message, MessageButtons.Normal, MessageIcons.Error);
    }
    public Task<bool> CloseAndDrainAsync()
    {
        Dispatcher.VerifyAccess();
        if (_closed) return Task.FromResult(true);
        if (_closeCompletion is { } pending) return pending.Task;
        Close();
        return _closeCompletion?.Task ?? Task.FromResult(_closed);
    }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs args)
    {
        ToolWindowAssemblyTrace.Event("CloseRequested", Title ?? "unknown");
        if (!_closeAllowed)
        {
            if (_closeCompletion is null)
            {
                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _closeCompletion = completion;
                _closing = true;
                try { Notify(); }
                catch (Exception error) { Foundation.Logging.PluginLogger.Error(error.Message, nameof(RuntimeToolEditorWindow), error); }
                try
                {
                    var drain = Editor.CloseDebugAsync();
                    if (drain.IsCompletedSuccessfully) _closeAllowed = true;
                    else _ = FinishClosingAsync(drain, completion);
                }
                catch (Exception error) { FailClosing(completion, error); }
            }
            args.Cancel = !_closeAllowed;
        }

        base.OnClosing(args);
        if (!_closeAllowed)
            args.Cancel = true;
        else if (args.Cancel)
        {
            _closeCompletion?.TrySetResult(false);
            _closeCompletion = null;
        }
    }
    private async Task FinishClosingAsync(Task drain, TaskCompletionSource<bool> completion)
    {
        try
        {
            await drain;
            // Even if the drain completed inline, let WPF return from the first Closing event.
            await Dispatcher.Yield(DispatcherPriority.Normal);
            if (_closed) return;
            _closeAllowed = true;
            Close();
            if (_closed) ToolWindowAssemblyTrace.Event("CloseCompleted", Title ?? "unknown");
        }
        catch (Exception error) { FailClosing(completion, error); }
    }
    private void FailClosing(TaskCompletionSource<bool> completion, Exception error)
    {
        completion.TrySetResult(false);
        Foundation.Logging.PluginLogger.Error(error.Message, nameof(RuntimeToolEditorWindow), error);
        Dispatcher.BeginInvoke(DispatcherPriority.Background,
            new Action(() => ShowDiagnostic("调试资源尚未释放：" + error.Message, duringClose: true)));
    }
    private void ConfirmEditor()
    {
        if (!TryConfirm(out var error) && !_closed && !_closing && IsVisible)
            NativeMessageBox.ShowOwned(this, AppLocalizationService.GetLang("Lang_ParamSet", "参数设置"),
                error, MessageButtons.Normal, MessageIcons.Warning);
    }
    public bool TryConfirm(out string error)
    {
        if (_closed || _closing || _running || Editor.IsDebugBusy || !_assemblyReady) { error = "参数页仍在组装或执行，请稍候。"; return false; }
        var committed = ToolEditorCommit.GetDraftContext(Editor) is ProductionEditorContext context
            ? context.TryCommitBeforeClose(Editor, out error)
            : ToolEditorCommit.TryCommit(Editor, out error);
        if (!committed) return false;
        var closing = CloseAndDrainAsync();
        if (!closing.IsCompleted || closing.IsCompletedSuccessfully && closing.Result) return true;
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
