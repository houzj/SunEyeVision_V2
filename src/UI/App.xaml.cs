using SunEyeVision.Infrastructure.Persistence.ApplicationData;
using SunEyeVision.UI.Services.Startup;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.IO;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using System.Diagnostics;
using System.Windows.Threading;
using System.Windows.Data;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.UI.Features.Dialogs;
using System.Runtime.InteropServices;

using SunEyeVision.Plugin.Editing.Wpf.Models;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Services.Monitoring;
using SunEyeVision.Foundation.Logging;
using System.Windows.Media;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Shell;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Shared.Events;

using SunEyeVision.UI.Features.Dialogs;

using SunEyeVision.UI.Features.GlobalVariableManager;
using SunEyeVision.UI.Features.Camera;

namespace SunEyeVision.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static readonly TimeSpan CameraRestoreTimeout = TimeSpan.FromSeconds(10);
    private readonly Stopwatch _startupStopwatch = Stopwatch.StartNew();
    private bool _releaseProbe;
    private bool _startupRejected;
    private bool _servicesReady;
    private PluginManager? _pluginManager;
    private IDisposable? _desktopDirectoryPin;
    private ApplicationDataPaths? _dataPaths;
    private string? _requestedSolutionPath;
    // VM 的启动顺序是先进入方案管理器，用户选择方案后才创建工作区并显示方案加载遮罩。
    // 这几个字段保存早期方案管理器的选择，避免插件准入完成后再次弹出一遍起始页。
    private bool _startupSolutionManagerShown;
    private StartPageAction _startupSelectedAction = StartPageAction.EmptyWorkspace;
    private string? _startupSelectedPath;
    private bool _startupNewSolutionRequested;
    public StartPageSettings StartPreferences { get; private set; } = new();
    private string StartPreferencesFile => _dataPaths!.ConfigFile;
    // Keep the named marker rooted through shutdown cleanup; the OS closes it at process exit.
    private static IDisposable? _runningApplicationRegistration;
    private static SingleInstanceCoordinator? _singleInstance;
    private bool _instanceClosing;
    private bool _startupActivationReady;
    private bool _activationInProgress;
    // RPC_S_SERVER_UNAVAILABLE = 0x000006BA = 1722
    private const int RPC_S_SERVER_UNAVAILABLE = 1722;
    private const int HRESULT_RPC_UNAVAILABLE = unchecked((int)0x800706BA);

    // 监控定时器
    private DispatcherTimer? _monitorTimer;
    private int _monitorReportInterval = 30; // 每30秒报告一次

    private MainWindow? _mainWindow;

    static App()
    {
        // 只记录警告和错误级别的绑定信息，避免性能问题
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        // First-run directory dialogs may temporarily be the only window; do not exit when they close.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            _runningApplicationRegistration = ApplicationLifetimeGate.TryRegister();
            if (_runningApplicationRegistration == null)
                throw new InvalidOperationException("SunEyeVision 正在卸载或维护，请等待操作结束后再启动。");
        }
        catch (Exception ex)
        {
            _startupRejected = true;
            if (e.Args.Length == 0 || (e.Args[0] != "--release-probe"&&e.Args[0]!="--toolbox-probe"))
                MessageBox.Show($"无法启动 SunEyeVision：{ex.Message}", "SunEyeVision", MessageBoxButton.OK, MessageBoxImage.Information);
            else if(e.Args.Length==2)
            {
                try{File.WriteAllText(e.Args[1],System.Text.Json.JsonSerializer.Serialize(new{status="Rejected",stage="startup-admission",error=ex.Message}));}
                catch { /* A rejected probe must still exit when its diagnostic path is unavailable. */ }
            }
            Shutdown(1);
            return;
        }
        // Release probe runs before device/service initialization, so it cannot connect hardware.
        if (e.Args.Length == 2 && (e.Args[0] == "--release-probe" || e.Args[0] == "--toolbox-probe"))
        {
            _releaseProbe = true;
            try
            {
                // Optional toolbox validation loads plugins and presentation metadata only.
                // It still exits before device/service/solution initialization.
                var inspectToolbox = e.Args[0] == "--toolbox-probe";
                var beforeAdmissionMs = _startupStopwatch.ElapsedMilliseconds;
                var probePluginManager=inspectToolbox?new PluginManager():null;
                if (inspectToolbox)
                    await probePluginManager!.LoadPluginsAsync();
                var admission = inspectToolbox ? probePluginManager!.DesktopAdmission ?? throw new InvalidOperationException("缺少已准入工具目录。") : null;
                var toolbox = admission is null ? null : new SunEyeVision.UI.Shell.Controls.Toolbox.ToolboxViewModel(admission);
                if(inspectToolbox)probePluginManager!.AssertCompleteReleaseToolSet();
                var info = new
                {
                    beforeAdmissionMs,
                    admissionStageMs = probePluginManager?.LastAdmissionStageMs,
                    admissionLoadSubstageMs = probePluginManager?.DesktopAdmission?.LastLoadSubstageMs,
                    probeTotalMs = _startupStopwatch.ElapsedMilliseconds,
                    releaseVersion = SunEyeVision.Foundation.Versioning.AppVersionInfo.Product,
                    channel = SunEyeVision.Foundation.Versioning.AppVersionInfo.Channel,
                    releaseDate = SunEyeVision.Foundation.Versioning.AppVersionInfo.ReleaseDate,
                    commit = SunEyeVision.Foundation.Versioning.AppVersionInfo.Commit,
                    toolContract = SunEyeVision.Plugin.Editing.Wpf.Core.ToolContract.Version,
                    startupWindowBackground = (TryFindResource(SunEyeVision.UI.Themes.Base.ThemeKeys.WindowBackgroundBrush) as SolidColorBrush)?.Color.ToString(),
                    toolboxCategories = toolbox?.Categories.Select(c => new { name = c.Name, icon = c.Icon, count = c.ToolCount }).ToArray(),
                    toolboxTools = toolbox?.AllTools.Select(t => new { id = t.ToolId, name = t.Name, category = t.Category, icon = t.Icon, iconSource=t.IconSource, available=t.IsAvailable }).ToArray(),
                    loadedDirectoryToolAssemblies = inspectToolbox ? AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetName().Name).Intersect(admission!.Directory.Tools.Select(t=>Path.GetFileNameWithoutExtension(t.AssemblyPath))).ToArray() : null,
                    toolComponents = inspectToolbox ? admission!.GetComponentMetadata() : null
                };
                File.WriteAllText(e.Args[1], System.Text.Json.JsonSerializer.Serialize(info));
                Shutdown(0);
            }
            catch(Exception ex)
            {
                try{File.WriteAllText(e.Args[1],System.Text.Json.JsonSerializer.Serialize(new{status="Rejected",stage="toolbox-admission",error=ex.Message}));}catch{ }
                Shutdown(1);
            }
            return;
        }
        try
        {
            StartupLaunchContext.Current = StartupLaunchContext.Parse(e.Args);
            _requestedSolutionPath = StartupLaunchContext.Current.SolutionPath;
        }
        catch (Exception ex)
        {
            _startupRejected = true;
            ThemeMessageDialog.Show(ex.Message, "启动参数无效", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            Shutdown(1); return;
        }
        try
        {
            _singleInstance = SingleInstanceCoordinator.TryStart(HandleInstanceActivationAsync, StartupLaunchContext.Current.InstanceIdentity);
            if (_singleInstance == null)
            {
                _startupRejected = true;
                var activation = await SingleInstanceCoordinator.SendAsync(_requestedSolutionPath, identity: StartupLaunchContext.Current.InstanceIdentity);
                if (!activation.Accepted)
                    ThemeMessageDialog.Show(activation.Message, "SunEyeVision 已在运行",
                        MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Info);
                Shutdown(activation.Accepted ? 0 : 2);
                return;
            }
        }
        catch (Exception ex)
        {
            _startupRejected = true;
            ThemeMessageDialog.Show("无法建立单实例保护，启动已取消：" + ex.Message,
                "SunEyeVision", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            Shutdown(1);
            return;
        }
        LogStartupStage("OnStartup.Enter", $"args={e.Args.Length}");
        // 设置控制台编码为UTF-8，解决中文乱码问题
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        LogStartupStage("OnStartup.Pre warm skipped", "VM-aligned startup has no generic image/thread-pool warmup");

        // 添加FirstChanceException处理 - 捕获RPC异常避免调试器中断
        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;

        base.OnStartup(e);
        LogStartupStage("Application base OnStartup completed");

        // 先准备数据根和起始页偏好。方案管理器只读取最近方案目录，不依赖
        // 插件准入、服务注册或完整工作区，因此可以在最慢的启动链之前先交给用户。
        try
        {
            InitializeDataPathsAndPreferences();
            if (_requestedSolutionPath is null && !StartPreferences.HideStartPage)
                ShowStartupSolutionManager();
        }
        catch (Exception ex)
        {
            LogStartupStage("Startup solution manager failed", ex.ToString());
            _startupRejected = true;
            ThemeMessageDialog.Show("无法打开方案管理器：" + ex.Message, "启动失败", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            Shutdown(1);
            return;
        }

        // 只有方案管理器完成选择后，才创建最终主窗口并显示方案加载遮罩。
        // 直接文件启动或用户关闭/隐藏方案管理器时仍走同一轻量壳路径。
        try
        {
            _mainWindow = new MainWindow();
            AttachMainWindowDiagnostics(_mainWindow);
            System.Windows.Application.Current.MainWindow = _mainWindow;
            ShowMainWindow(_mainWindow, "startup shell");
            _mainWindow.ShowSolutionLoadingOverlay("正在启动", "准备插件目录和运行环境");
        }
        catch (Exception ex)
        {
            LogStartupStage("MainWindow.Shell failed", ex.ToString());
            _startupRejected = true;
            ThemeMessageDialog.Show("主窗口壳创建失败：" + ex.Message, "启动失败", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            Shutdown(1); return;
        }

        // 添加全局异常处理
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // 注册文件关联（仅当前用户，无需管理员权限）
        if (!StartupLaunchContext.Current.IsIsolated) RegisterFileAssociation();

        // 工具目录必须先于实际文档准入就绪。
        _mainWindow?.UpdateSolutionLoadingOverlay("正在加载模块", "校验发布文件并读取工具目录");
        var pluginManager = new PluginManager(VisionLogger.Instance);
        _pluginManager = pluginManager;
        LogStartupStage("Plugin.Load begin", "source=release-payload.productComponents");
        // Admission is filesystem/assembly work and has no WPF affinity. Keep it off
        // the dispatcher so the visible startup surface can continue to receive input
        // while the approved runtime generation is prepared.
        await Task.Run(() => pluginManager.LoadPluginsAsync());
        LogStartupStage("Plugin.Load task joined");
        LogStartupStage("Plugin.Load stages",
            string.Join(", ", pluginManager.LastAdmissionStageMs.Select(item => $"{item.Key}={item.Value}ms")));
        LogStartupStage("Plugin.Assert begin");
        try
        {
            pluginManager.AssertCompleteReleaseToolSet();
            LogStartupStage("Plugin.Assert completed");
        }
        catch (Exception ex)
        {
            LogStartupStage("Plugin.Assert failed", ex.ToString());
            _startupRejected = true;
            ThemeMessageDialog.Show("模块加载不完整：" + ex.Message, "启动失败", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            Shutdown(1); return;
        }
        _mainWindow?.UpdateSolutionLoadingOverlay("正在加载模块", "工具目录读取完成");
        LogStartupStage("Plugin.Load completed");

        // 2. 标记插件已加载（全局状态）


        // 3. 初始化服务（包括节点显示适配器）
        _mainWindow?.UpdateSolutionLoadingOverlay("正在初始化服务", "注册运行时服务和图像适配器");
        LogStartupStage("Service.Initialize begin");
        // Service registration, adapter registration and log-writer admission are
        // filesystem/static-service work. They do not create a DocumentSession or
        // touch WPF objects, so keep them off the dispatcher while the startup
        // surface remains visible and movable.
        await Task.Run(() => ServiceInitializer.InitializeServices());
        LogStartupStage("Service.Initialize completed");
        // 日志系统已完成初始化，此处记录实际运行文件身份，避免启动早期埋点丢失。
        LogRuntimeIdentity();
        // 日志系统进入稳定态后自查一次文件写入是否已经在失败：降级必须可见，不能只留计数器没人读。
        ServiceInitializer.ReportFileLogWriteFailure();

        // 4. 使用默认路径
        var defaultPath = _dataPaths!.SolutionsDirectory;

        // 5. 初始化解决方案管理器
        _mainWindow?.UpdateSolutionLoadingOverlay("正在初始化解决方案", "准备方案仓库和用户设置");
        LogStartupStage("SolutionManager.Initialize begin", $"defaultPath={defaultPath}");
        var desktopAdmission=pluginManager.DesktopAdmission??throw new InvalidOperationException("缺少已准入运行目录。");
        _desktopDirectoryPin=pluginManager.PinDesktopHost();
        LogStartupStage("SolutionManager directory pinned");
        var solutionManagerClock = Stopwatch.StartNew();
        ServiceInitializer.InitializeSolutionManager(_dataPaths!, desktopAdmission.Runtime);
        solutionManagerClock.Stop();
        _servicesReady = true;
        _mainWindow?.UpdateSolutionLoadingOverlay("正在初始化解决方案", "解决方案管理器准备完成");
        LogStartupStage("SolutionManager.Initialize completed", $"elapsedMs={solutionManagerClock.Elapsed.TotalMilliseconds:F1}");

        // 5.2 校验唯一主题字典的语义键契约（VM 原件只有一套固定主题，不提供运行时切换）
        InitializeFixedTheme();

        // 初始化DispatcherTimer监控
        InitializeMonitoring();

        // 启动决策
        LogStartupStage("Startup decision begin");
        await HandleStartupDecisionAsync(desktopAdmission, _mainWindow);
        _startupActivationReady = true;
        LogStartupStage("HandleStartupDecision returned");
    }

    /// <summary>
    /// 处理启动决策
    /// </summary>
    private async Task HandleStartupDecisionAsync(
        SunEyeVision.Plugin.Infrastructure.Managers.Plugin.DesktopToolAdmission desktopAdmission,
        MainWindow? existingWindow = null)
    {
        var mainWindow = existingWindow ?? new MainWindow();
        var workspaceClock = Stopwatch.StartNew();
        mainWindow.InitializeWorkspace(desktopAdmission);
        workspaceClock.Stop();
        LogStartupStage("MainWindow.WorkspaceInitialized", $"elapsedMs={workspaceClock.Elapsed.TotalMilliseconds:F1}");
        if (!ReferenceEquals(_mainWindow, mainWindow))
        {
            _mainWindow = mainWindow;
            AttachMainWindowDiagnostics(mainWindow);
            System.Windows.Application.Current.MainWindow = mainWindow;
        }
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        mainWindow.UpdateSolutionLoadingOverlay("正在准备工作区", "启动服务已准备完成");
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        // 方案管理器已在插件准入前完成选择；此处只把最终工作区壳交给用户，
        // 并在已选择方案时保持加载遮罩连续可见。
        if (!mainWindow.IsVisible)
            ShowMainWindow(mainWindow, "workspace shell");
        // 选择了方案时保持覆盖层连续可见，避免出现“主窗口先露出空壳、再闪回加载框”的
        // 非 VM 跳变；只有空白工作区或关闭方案管理器才在这里撤掉覆盖层。
        if (_requestedSolutionPath is null && !_startupNewSolutionRequested)
            mainWindow.HideSolutionLoadingOverlay();

        if (_requestedSolutionPath != null)
        {
            // A file launch never routes through the browser/registration dialog.
            mainWindow.ShowSolutionLoadingOverlay("正在打开方案", "正在读取方案并准备工作区");
            try
            {
                if (mainWindow.DataContext is MainWindowViewModel vm && await vm.OpenExternalSolutionAsync(_requestedSolutionPath))
                    return;
            }
            finally { mainWindow.HideSolutionLoadingOverlay(); }
            // A failed file request remains recoverable via the start page, even when hidden by preference.
            await ShowStartPageAsync(mainWindow);
        }
        else if (_startupSolutionManagerShown && _startupNewSolutionRequested)
        {
            // 方案管理器已完成“新建”选择；覆盖层从这里开始才出现，和 VM 的
            // “先选方案、再进入工作区加载”时序一致。
            mainWindow.ShowSolutionLoadingOverlay("正在创建方案", "正在准备空白方案工作区");
            try
            {
                if (mainWindow.DataContext is MainWindowViewModel vm && await vm.NewEmptySolutionAsync())
                    return;
            }
            finally { mainWindow.HideSolutionLoadingOverlay(); }
        }
        else if (_startupSolutionManagerShown)
        {
            // 早期方案管理器已经由用户关闭；不要在插件准入完成后再弹第二次。
        }
        else if (StartPreferences.HideStartPage)
        {
            // The shell is already visible. No second Show is needed.
        }
        else
            await ShowStartPageAsync(mainWindow);
    }

    /// <summary>
    /// 在插件准入之前显示方案管理器。这里仅收集用户选择，不创建工作区、工具箱
    /// 或节点编辑器；选择完成后 OnStartup 才创建 MainWindow 并进入加载覆盖层。
    /// </summary>
    private void ShowStartupSolutionManager()
    {
        var recent = StartPreferences.BuildRecentFiles();
        var page = new StartPageWindow(StartPreferences.HideStartPage, recent, SetStartPageHidden, RemoveRecentSolution);
        LogStartupStage("SolutionManager.Visible", "before plugin admission");
        System.Windows.Application.Current.MainWindow = page;
        page.ShowDialog();

        _startupSolutionManagerShown = true;
        _startupSelectedAction = page.SelectedAction;
        _startupSelectedPath = page.SelectedPath;
        if (_startupSelectedAction == StartPageAction.OpenFile && !string.IsNullOrWhiteSpace(_startupSelectedPath))
            _requestedSolutionPath = _startupSelectedPath;
        else if (_startupSelectedAction == StartPageAction.NewSolution)
            _startupNewSolutionRequested = true;

        LogStartupStage("SolutionManager.Selection", $"action={_startupSelectedAction}; path={_startupSelectedPath ?? string.Empty}");
    }

    /// <summary>
    /// 数据根和起始页偏好在方案管理器前准备一次。后续服务/方案管理器初始化
    /// 复用同一实例，避免重复读取配置和重新判定根目录。
    /// </summary>
    private void InitializeDataPathsAndPreferences()
    {
        if (_dataPaths is not null)
            return;

        var context = StartupLaunchContext.Current;
        _dataPaths = context.IsIsolated
            ? ApplicationDataPaths.Open(Path.Combine(context.IsolatedRoot!, "Data"), "隔离启动根")
            : ApplicationDataPaths.OpenConfigured();
        ServiceInitializer.ConfigureDataPaths(_dataPaths);
        try
        {
            StartPreferences = StartPageSettings.Load(StartPreferencesFile);
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Warning, "起始页偏好无法读取，本次显示起始页：" + ex.Message, "App");
            StartPreferences = new();
        }
    }

    public void SetStartPageHidden(bool hidden)
    {
        bool previous = StartPreferences.HideStartPage;
        StartPreferences.HideStartPage = hidden;
        try { SaveShellPreferences(); }
        catch { StartPreferences.HideStartPage = previous; throw; }
    }

    public void RecordRecentSolution(SunEyeVision.Application.DocumentSession session)
    {
        var path = session.LastWrittenSnapshot?.TargetPath;
        if (string.IsNullOrEmpty(path)) return;
        StartPreferences.RecordOpened(path, session.Document.Name);
        try { SaveShellPreferences(); }
        catch (Exception ex) { VisionLogger.Instance.Log(LogLevel.Warning, "方案已打开；最近记录写入或目录登记未完整完成：" + ex.Message, "App"); }
    }

    internal void SaveShellPreferences()
    {
        var paths=_dataPaths??throw new InvalidOperationException("应用数据目录尚未初始化。");
        StartPreferences.Save(paths.ConfigFile);
    }
    internal void SaveWindowLayout(SunEyeVision.UI.Shared.LayoutConfig layout)
    {
        var previous=StartPreferences.WindowLayout;StartPreferences.WindowLayout=layout;
        try{SaveShellPreferences();}catch{StartPreferences.WindowLayout=previous;throw;}
    }

    private void RemoveRecentSolution(string path)
    {
        var files = StartPreferences.RecentFiles.ToList();
        var dismissed = StartPreferences.DismissedPaths.ToList();
        StartPreferences.RecentFiles.RemoveAll(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
        if (!StartPreferences.DismissedPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
            StartPreferences.DismissedPaths.Add(path);
        try { SaveShellPreferences(); }
        catch { StartPreferences.RecentFiles = files; StartPreferences.DismissedPaths = dismissed; throw; }
    }

    public async Task ShowStartPageAsync(MainWindow mainWindow)
    {
        if (mainWindow.DataContext is not MainWindowViewModel vm) return;
        bool wasVisible = mainWindow.IsVisible;
        while (true)
        {
            // Existing catalog entries are optional suggestions, not prerequisites for file opening.
            var recent = StartPreferences.BuildRecentFiles();
            var page = new StartPageWindow(StartPreferences.HideStartPage, recent, SetStartPageHidden, RemoveRecentSolution);
            if (wasVisible) page.Owner = mainWindow;
            page.ShowDialog();
            switch (page.SelectedAction)
            {
                case StartPageAction.OpenFile:
                    if (page.SelectedPath == null || !await vm.OpenExternalSolutionAsync(page.SelectedPath)) continue;
                    break;
                case StartPageAction.NewSolution:
                    if (!await vm.NewEmptySolutionAsync()) continue;
                    break;
                default: break; // Closing a start page never exits the application or discards an open workspace.
            }
            if (!mainWindow.IsVisible) ShowMainWindow(mainWindow, "start page selection");
            return;
        }
    }

    private Task? _pendingWorkspaceDeviceRestore;
    internal bool HasPendingWorkspaceDeviceRestore => _pendingWorkspaceDeviceRestore is { IsCompleted: false };
    internal async Task CompleteOpenedWorkspaceAsync()
    {
        if (System.Windows.Application.Current.MainWindow is not MainWindow main) return;
        if (!main.IsVisible) ShowMainWindow(main, "solution committed");
        main.ShowSolutionLoadingOverlay("正在恢复设备", "方案已打开，正在恢复已配置相机连接");
        try
        {
            var expected = ServiceInitializer.SolutionManager.CurrentSession;
            if (expected != null)
            {
                if (main.DataContext is MainWindowViewModel viewModel) viewModel.BindCommunicationToCurrentSession();
                await ServiceInitializer.CommunicationService.VerifyWorkspaceReadyAsync(expected.Document);
            }
            await RestoreCameraConnectionsAsync(ServiceInitializer.SolutionManager, main.UpdateSolutionLoadingOverlay, reportFailure: true);
        }
        finally { main.HideSolutionLoadingOverlay(); }
    }

    /// <summary>Reject new handoffs before closing windows/device cleanup; retain ownership until process exit.</summary>
    internal void MarkInstanceClosing()
    {
        _instanceClosing = true;
        _singleInstance?.BeginClosing();
    }

    internal async Task DrainPluginDirectoryAsync()
    {
        var manager = _pluginManager;
        if (manager is null) return;
        _desktopDirectoryPin?.Dispose();
        _desktopDirectoryPin=null;
        await manager.UnloadPluginsAsync();
        if (ReferenceEquals(_pluginManager, manager)) _pluginManager = null;
    }

    private Task<InstanceActivationResult> HandleInstanceActivationAsync(InstanceActivationRequest request)
    {
        return Dispatcher.InvokeAsync(() =>
        {
            if (_instanceClosing || Dispatcher.HasShutdownStarted)
                return new InstanceActivationResult(false, "SunEyeVision 正在退出，请等待退出完成后再启动。");
            if (request.ExpiresAt < DateTimeOffset.UtcNow)
                return new InstanceActivationResult(false, "启动请求已超时，请重试。");
            var visible = Windows.OfType<Window>().LastOrDefault(window => window.IsVisible && window.IsActive)
                ?? Windows.OfType<Window>().LastOrDefault(window => window.IsVisible);
            if (visible != null)
            {
                if (visible.WindowState == WindowState.Minimized) visible.WindowState = WindowState.Normal;
                visible.Activate();
            }
            if (string.IsNullOrEmpty(request.SolutionPath))
                return visible == null
                    ? new InstanceActivationResult(false, "SunEyeVision 正在启动，请稍后重试。")
                    : InstanceActivationResult.Delivered;
            if (!_startupActivationReady || _activationInProgress ||
                MainWindow is not MainWindow main || !main.IsVisible || !main.IsEnabled ||
                main.DataContext is not MainWindowViewModel viewModel || !viewModel.CanAcceptExternalActivation)
                return new InstanceActivationResult(false, "SunEyeVision 正在启动、处理对话框或切换方案，请完成当前操作后重试。");
            string path;
            try
            {
                path = Path.GetFullPath(request.SolutionPath);
                if (!path.EndsWith(".solution", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                    return new InstanceActivationResult(false, "方案文件不存在或扩展名不正确。");
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            { return new InstanceActivationResult(false, "方案路径无效。"); }
            _activationInProgress = true;
            // Ack delivery promptly; save/stop dialogs stay in the existing instance.
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    if (!_instanceClosing && main.IsVisible)
                        await viewModel.OpenExternalSolutionAsync(path);
                }
                finally { _activationInProgress = false; }
            }));
            return InstanceActivationResult.Delivered;
        }).Task;
    }


    private void ShowMainWindow(MainWindow mainWindow, string reason)
    {
        LogStartupStage("MainWindow.Show begin", $"reason={reason}");
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
        LogMainWindowState(mainWindow, "after Show");

        mainWindow.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            LogMainWindowState(mainWindow, "DispatcherPriority.ApplicationIdle");
        }));
    }

    private void AttachMainWindowDiagnostics(MainWindow mainWindow)
    {
        mainWindow.SourceInitialized += (_, _) => LogMainWindowState(mainWindow, "SourceInitialized");
        mainWindow.Loaded += (_, _) => LogMainWindowState(mainWindow, "Loaded");
        mainWindow.ContentRendered += (_, _) =>
        {
            LogMainWindowState(mainWindow, "ContentRendered");
        };
        mainWindow.Activated += (_, _) => LogMainWindowState(mainWindow, "Activated");
        mainWindow.StateChanged += (_, _) => LogMainWindowState(mainWindow, "StateChanged");
        mainWindow.Closed += (_, _) => LogMainWindowState(mainWindow, "Closed");
    }

    private void LogMainWindowState(Window window, string stage)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            LogStartupStage($"MainWindow.{stage}",
                $"handle=0x{handle.ToInt64():X}, visible={window.IsVisible}, visibility={window.Visibility}, " +
                $"state={window.WindowState}, left={window.Left:F0}, top={window.Top:F0}, " +
                $"width={window.ActualWidth:F0}, height={window.ActualHeight:F0}, " +
                $"showInTaskbar={window.ShowInTaskbar}, presentationSource={PresentationSource.FromVisual(window) != null}");
        }
        catch (Exception ex)
        {
            LogStartupStage($"MainWindow.{stage}.diagnostic failed", ex.Message);
        }
    }

    private void LogStartupStage(string stage, string? detail = null)
    {
        var message = $"[StartupTrace] +{_startupStopwatch.ElapsedMilliseconds}ms {stage}" +
            (string.IsNullOrWhiteSpace(detail) ? string.Empty : $" | {detail}");
        try
        {
            VisionLogger.Instance.Log(
                LogLevel.Info,
                message,
                "App");
        }
        catch
        {
            // 启动早期日志系统可能尚未完成初始化，埋点不能阻断应用启动。
        }

        // 隔离启动在日志系统初始化前也必须保留时间线，否则最慢的准入/服务阶段
        // 会被吞掉，只能看到后半段。正常用户启动不写该文件。
        try
        {
            var isolatedRoot = StartupLaunchContext.Current.IsolatedRoot;
            if (!string.IsNullOrWhiteSpace(isolatedRoot))
                File.AppendAllText(Path.Combine(isolatedRoot, "startup-trace.txt"),
                    $"{DateTime.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // 诊断文件失败不能改变启动语义。
        }
    }

    private void LogRuntimeIdentity()
    {
        try
        {
            var assembly = typeof(App).Assembly;
            var processPath = Process.GetCurrentProcess().MainModule?.FileName
                ?? assembly.Location;
            var fileInfo = new FileInfo(processPath);
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            var fileVersion = assembly
                .GetCustomAttribute<AssemblyFileVersionAttribute>()
                ?.Version;

            LogStartupStage(
                "RuntimeIdentity",
                $"pid={Environment.ProcessId}, assembly={assembly.GetName().Name}, " +
                $"assemblyLocation={assembly.Location}, processPath={processPath}, " +
                $"informationalVersion={informationalVersion ?? "<none>"}, " +
                $"fileVersion={fileVersion ?? "<none>"}, " +
                $"fileLastWriteUtc={fileInfo.LastWriteTimeUtc:O}, fileLength={fileInfo.Length}");
        }
        catch (Exception ex)
        {
            LogStartupStage("RuntimeIdentity failed", ex.Message);
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Exception ex = e.ExceptionObject as Exception;

        if (ex?.InnerException != null)
        {
            if (ex.InnerException.InnerException != null)
            {
                // 第二层内部异常，不做处理
            }
        }

        // 记录到日志系统
        try
        {
            var logger = VisionLogger.Instance;
            logger.Log(LogLevel.Error, $"全局未处理异常: {ex?.Message}", "App", ex);
            logger.Log(LogLevel.Error, $"异常类型: {ex?.GetType().FullName}", "App");
            logger.Log(LogLevel.Error, $"堆栈跟踪:\n{ex?.StackTrace}", "App");
            if (ex?.InnerException != null)
            {
                logger.Log(LogLevel.Error, $"内部异常: {ex.InnerException.Message}", "App", ex.InnerException);
            }
        }
        catch { }

        MessageBox.Show($"应用程序发生未处理的异常:\n{ex?.Message}\n\n堆栈跟踪:\n{ex?.StackTrace}",
            "严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // 记录到插件日志系统
        try
        {
            var logger = VisionLogger.Instance;
            logger.Log(LogLevel.Error, $"Dispatcher 未处理异常: {e.Exception.Message}", "App", e.Exception);
            logger.Log(LogLevel.Error, $"异常类型: {e.Exception.GetType().FullName}", "App");
            logger.Log(LogLevel.Error, $"堆栈跟踪: {e.Exception.StackTrace}", "App");
            // 完整异常链（含内层 XamlParseException 的行号/位置与逐帧堆栈），用于定位模板资源解析失败的具体 XAML。
            logger.Log(LogLevel.Error, $"完整异常链: {e.Exception}", "App");
            if (e.Exception.InnerException != null)
            {
                logger.Log(LogLevel.Error, $"内部异常: {e.Exception.InnerException.Message}", "App", e.Exception.InnerException);
            }
        }
        catch { }

        e.Handled = true; // 防止应用程序崩溃
        MessageBox.Show($"UI 线程发生异常:\n{e.Exception.Message}\n\n堆栈跟踪:\n{e.Exception.StackTrace}",
            "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>
    /// FirstChanceException处理 - 在异常被抛出但未被捕获时触发
    /// 用于捕获并标记RPC异常，避免调试器中断
    /// </summary>
    private void OnFirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
    {
        // 只捕获RPC服务器不可用异常，标记为已知的非致命异常
        if (e.Exception is COMException comEx)
        {
            if (comEx.ErrorCode == HRESULT_RPC_UNAVAILABLE || (comEx.ErrorCode & 0xFFFF) == RPC_S_SERVER_UNAVAILABLE)
            {
                // 标记为已知的非致命RPC异常，异常会被解码器的降级逻辑处理
                // 不记录日志，避免性能问题
            }
        }
        // 捕获WIC相关的内部异常
        else if (e.Exception is System.Reflection.TargetInvocationException tie &&
                 tie.InnerException is COMException innerComEx)
        {
            if (innerComEx.ErrorCode == HRESULT_RPC_UNAVAILABLE || (innerComEx.ErrorCode & 0xFFFF) == RPC_S_SERVER_UNAVAILABLE)
            {
                // 标记为已知的非致命RPC内部异常
                // 不记录日志，避免性能问题
            }
        }
    }

    private async Task RestoreCameraConnectionsAsync(SunEyeVision.Application.DocumentSessionManager solutionManager, Action<string, string>? statusReporter = null, bool reportFailure = false)
    {
        try
        {
            LogStartupStage("Camera connection restore begin");
            statusReporter?.Invoke("正在恢复相机连接", "正在根据设备列表连接相机，最多等待 10 秒");
            var restoreTask = CameraManagerViewModel
                .RestoreConnectionsForCurrentSolutionAsync(solutionManager);
            _pendingWorkspaceDeviceRestore = restoreTask;
            var completed = await Task.WhenAny(restoreTask, Task.Delay(CameraRestoreTimeout));
            if (!ReferenceEquals(completed, restoreTask))
            {
                LogStartupStage("Camera connection restore timeout",
                    $"timeoutMs={CameraRestoreTimeout.TotalMilliseconds:0}");
                statusReporter?.Invoke("相机连接等待超时", "部分设备未在 10 秒内完成连接，软件将继续启动");
                _ = ObserveLateCameraRestoreAsync(restoreTask);
                if (reportFailure) throw new TimeoutException("相机恢复等待超时，方案已提交，设备状态尚未确认。");
                return;
            }

            await restoreTask;
            LogStartupStage("Camera connection restore completed");
            statusReporter?.Invoke("相机连接处理完成", "正在准备进入主界面");
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(
                LogLevel.Error,
                $"方案加载后的相机连接恢复失败: {ex}",
                "App",
                ex);
            LogStartupStage("Camera connection restore failed", ex.Message);
            statusReporter?.Invoke("相机连接处理失败", $"软件将继续启动：{ex.Message}");
            if (reportFailure) throw;
        }
    }

    private async Task ObserveLateCameraRestoreAsync(Task restoreTask)
    {
        try
        {
            await restoreTask.ConfigureAwait(false);
            LogStartupStage("Late camera connection restore completed");
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Error,
                $"相机连接超时后后台恢复最终失败: {ex}", "App", ex);
            LogStartupStage("Late camera connection restore failed", ex.Message);
        }
    }

    /// <summary>
    /// 注册文件关联
    /// </summary>
    private void RegisterFileAssociation()
    {
#if DEBUG
        // Development runs must not replace the installed application's file association.
        return;
#else
        try
        {
            var fileAssociationService = new FileAssociationService();
            if (!FileAssociationService.IsInstalledLocation()) return;
            if (!fileAssociationService.IsRegistered())
            {
                fileAssociationService.RegisterFileAssociation();
            }
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Warning, $"文件关联注册失败: {ex.Message}", "App");
        }
#endif
    }

    /// <summary>
    /// 初始化唯一主题：Theme/DarkTheme.xaml 已在 App.xaml 静态合并，这里只做语义键契约校验并固定标题栏。
    /// VM 原件只有一套固定主题，不提供运行时切换与用户偏好。
    /// </summary>
    private void InitializeFixedTheme()
    {
        var resources = System.Windows.Application.Current?.Resources
            ?? throw new InvalidOperationException("Application.Resources 为空，无法校验主题字典。");
        var theme = resources.MergedDictionaries.FirstOrDefault(dictionary =>
                dictionary.Source?.OriginalString.EndsWith("Themes/Dark/DarkTheme.xaml", StringComparison.OrdinalIgnoreCase) == true)
            ?? throw new InvalidOperationException("主题字典 Themes/Dark/DarkTheme.xaml 未合并到 Application.Resources。");
        SunEyeVision.UI.Themes.Base.ThemeContract.Validate(theme);
        Services.WindowTitleBarThemer.UpdateTheme(true);
        VisionLogger.Instance.Log(LogLevel.Info, "固定主题字典校验完成：" + theme.Source, "App");
    }

    /// <summary>
    /// 初始化监控
    /// </summary>
    private void InitializeMonitoring()
    {
        // 监控功能已禁用，避免性能问题
        // 如需调试，可手动启用
    }

    /// <summary>
    /// 应用程序退出时清理
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        MarkInstanceClosing();
        if (_releaseProbe || _startupRejected || !_servicesReady)
        {
            base.OnExit(e);
            return;
        }
        // 停止监控定时器
        _monitorTimer?.Stop();

        try
        {
            ServiceInitializer.DisposeCommunicationRuntimeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Warning, $"退出时释放通讯运行时失败: {ex.Message}", "App", ex);
        }

        try
        {
            ServiceInitializer.DisposeCameraRuntimeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Warning, $"退出时释放相机运行时失败: {ex.Message}", "App", ex);
        }

        try { ServiceInitializer.SolutionManager.Dispose(); }
        catch (Exception ex) { VisionLogger.Instance.Log(LogLevel.Warning, $"退出时关闭方案会话失败: {ex.Message}", "App", ex); }

        base.OnExit(e);
    }

}

