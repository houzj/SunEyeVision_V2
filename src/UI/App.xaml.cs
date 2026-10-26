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
using SunEyeVision.Workflow;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Services.Monitoring;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Vision.Hosting;
using SunEyeVision.CvAdapter;
using SunEyeVision.HalAdapter;
using SunEyeVision.UI.Converters;
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
public partial class App : Application
{
    private static readonly TimeSpan CameraRestoreTimeout = TimeSpan.FromSeconds(10);
    private readonly Stopwatch _startupStopwatch = Stopwatch.StartNew();
    private bool _releaseProbe;
    private bool _startupRejected;
    private bool _servicesReady;
    private ApplicationDataPaths? _dataPaths;
    private string? _requestedSolutionPath;
    public StartPageSettings StartPreferences { get; private set; } = new();
    private string StartPreferencesFile => Path.Combine(_dataPaths!.Root, "Config", "start-page.json");
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
    private double _imageWarmupBlockingMs;
    private bool _imageWarmupContentRecorded;
    private ImageWarmupMode EffectiveImageWarmupMode => StartupLaunchContext.Current.ImageWarmupOverride ?? ImageWarmupMode.Off;

    // 启动页（OnStartup 创建，HandleStartupDecision 关闭）。
    // 用全限定名避免与 System.Windows.SplashScreen（WPF 官方类）冲突。
    private SunEyeVision.UI.Shell.Windows.SplashScreen? _splashScreen;

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
                var probePluginManager=inspectToolbox?new PluginManager():null;
                if (inspectToolbox)
                    probePluginManager!.LoadPlugins(Path.Combine(AppContext.BaseDirectory, "plugins"));
                var toolbox = inspectToolbox ? new SunEyeVision.UI.Shell.Controls.Toolbox.ToolboxViewModel() : null;
                if(inspectToolbox)probePluginManager!.AssertCompleteReleaseToolSet(SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.DirectoryCatalog?.ToolIds ?? Array.Empty<string>());
                var info = new
                {
                    releaseVersion = SunEyeVision.Core.Versioning.AppVersionInfo.Product,
                    channel = SunEyeVision.Core.Versioning.AppVersionInfo.Channel,
                    releaseDate = SunEyeVision.Core.Versioning.AppVersionInfo.ReleaseDate,
                    commit = SunEyeVision.Core.Versioning.AppVersionInfo.Commit,
                    toolContract = SunEyeVision.Plugin.SDK.Core.ToolContract.Version,
                    defaultTheme = Services.ThemeManager.DefaultTheme.ToString(),
                    startupWindowBackground = (TryFindResource(SunEyeVision.UI.Themes.Base.ThemeKeys.WindowBackgroundBrush) as SolidColorBrush)?.Color.ToString(),
                    toolboxCategories = toolbox?.Categories.Select(c => new { name = c.Name, icon = c.Icon, count = c.ToolCount }).ToArray(),
                    toolboxTools = toolbox?.AllTools.Select(t => new { id = t.ToolId, name = t.Name, category = t.Category, icon = t.Icon, iconSource=t.IconSource, available=t.IsAvailable }).ToArray(),
                    loadedDirectoryToolAssemblies = inspectToolbox ? AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetName().Name).Intersect(SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.DirectoryCatalog!.Tools.Select(t=>Path.GetFileNameWithoutExtension(t.AssemblyPath))).ToArray() : null,
                    toolComponents = inspectToolbox ? SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.GetComponentMetadata() : null
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

        // Normal startup avoids forced empty-task warmup; isolated comparisons can explicitly enable it.
        if (EffectiveImageWarmupMode == ImageWarmupMode.On)
        {
            var warmupClock = Stopwatch.StartNew();
            PrewarmThreadPool();
            _imageWarmupBlockingMs = warmupClock.Elapsed.TotalMilliseconds;
        }
        _splashScreen?.UpdateStage("正在启动", 8, "运行环境准备完成");
        LogStartupStage("OnStartup.Pre warm completed", $"mode={EffectiveImageWarmupMode}, blockingMs={_imageWarmupBlockingMs:F3}");

        // 启动页：在插件加载/服务初始化/解决方案加载等耗时阶段提供品牌反馈（含进度+耗时+动画）。
        // 显示失败不阻塞启动（try-catch 跳过，仅告警）。
        try
        {
            _splashScreen = new SunEyeVision.UI.Shell.Windows.SplashScreen();
            _splashScreen.Show();
            _splashScreen.UpdateStage("正在启动", 2, "准备应用环境");
            LogStartupStage("Splash.Show completed", $"visible={_splashScreen.IsVisible}, handle=0x{new WindowInteropHelper(_splashScreen).Handle.ToInt64():X}");
        }
        catch (Exception splashEx)
        {
            _splashScreen = null;
            VisionLogger.Instance.Log(LogLevel.Warning, $"启动页显示失败（不影响启动）: {splashEx.Message}", "App");
        }

        // 添加FirstChanceException处理 - 捕获RPC异常避免调试器中断
        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;

        base.OnStartup(e);
        LogStartupStage("Application base OnStartup completed");

        // 添加全局异常处理
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // 注册文件关联（仅当前用户，无需管理员权限）
        if (!StartupLaunchContext.Current.IsIsolated) RegisterFileAssociation();

        // ===== P0优化：调整启动顺序，先加载插件 =====
        // 原因：SolutionSettings.Save() 使用 WorkflowSerializationOptions.Default
        // 会触发 ParameterTypeRegistry.EnsureInitialized()，需要插件已加载
        // 解决：在任何可能触发序列化操作之前加载插件

        // 1. 初始化插件管理器（在任何其他初始化之前）
        _splashScreen?.UpdateStage("正在加载模块", 10, "校验发布文件并读取工具目录");
        var pluginManager = new PluginManager(VisionLogger.Instance);
        string pluginsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        LogStartupStage("Plugin.Load begin", $"path={pluginsPath}");
        pluginManager.LoadPlugins(pluginsPath);
        try
        {
            pluginManager.AssertCompleteReleaseToolSet(SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.DirectoryCatalog?.ToolIds ?? Array.Empty<string>());
        }
        catch (Exception ex)
        {
            _startupRejected = true;
            _splashScreen?.CloseImmediately(); _splashScreen = null;
            ThemeMessageDialog.Show("模块加载不完整：" + ex.Message, "启动失败", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            Shutdown(1); return;
        }
        _splashScreen?.UpdateStage("正在加载模块", 35, "工具目录读取完成");
        LogStartupStage("Plugin.Load completed");

        // 2. 标记插件已加载（全局状态）
        _splashScreen?.UpdateProgress(35);
        PluginLoader.MarkAsLoaded();

        // 2.5 初始化视觉图像加载入口
        // HalImage 及其静态工厂方法已内置在 Plugin.SDK.Imaging 中
        // 使用 VisionImage.FromFile / VisionImage.FromBitmapSource 直接加载图像

        // Select/restore the user data root before creating any persistent service.
        try
        {
            var context = StartupLaunchContext.Current;
            var locations = new ApplicationDataLocationService(context.IsIsolated ? Path.Combine(context.IsolatedRoot!, "data-location.json") : null);
            if (context.IsIsolated) _dataPaths = locations.Prepare(Path.Combine(context.IsolatedRoot!, "Data"));
            string? reason = null;
            if (_dataPaths == null) { try { _dataPaths = locations.Locate(); } catch (Exception ex) { reason = ex.Message; } }
            while (_dataPaths == null)
            {
                _splashScreen?.CloseImmediately(); _splashScreen = null;
                var directoryDialog = new DataDirectoryDialog(locations, reason: reason);
                if (directoryDialog.ShowDialog() != true) { _startupRejected = true; Shutdown(0); return; }
                try
                {
                    var selected = directoryDialog.SelectedPaths!;
                    if (directoryDialog.ImportLegacy)
                        locations.ImportLegacy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "solution_settings.json"), selected);
                    locations.Remember(selected); _dataPaths = selected;
                }
                catch (Exception ex) { reason = ex.Message; }
            }
            ServiceInitializer.ConfigureDataPaths(_dataPaths);
            try { StartPreferences = StartPageSettings.Load(StartPreferencesFile); }
            catch (Exception ex) { VisionLogger.Instance.Log(LogLevel.Warning, "起始页偏好无法读取，本次显示起始页：" + ex.Message, "App"); StartPreferences = new(); }
        }
        catch (Exception ex)
        {
            _startupRejected = true;
            ThemeMessageDialog.Show("无法初始化数据目录：" + ex.Message, "数据目录", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            Shutdown(1); return;
        }

        // 3. 初始化服务（包括节点显示适配器）
        _splashScreen?.UpdateStage("正在初始化服务", 50, "注册运行时服务和图像适配器");
        LogStartupStage("Service.Initialize begin");
        ServiceInitializer.InitializeServices();
        LogStartupStage("Service.Initialize completed");
        // 日志系统已完成初始化，此处记录实际运行文件身份，避免启动早期埋点丢失。
        LogRuntimeIdentity();

        // 3.1 注册 IVisionImage → BitmapSource 显示桥（SDK 层通过 VisionImageConverterLocator 调用）
        VisionImageConverterLocator.Current = new UiVisionImageToBitmapSourceConverter();

        // 3.1.5 S5 对称架构装配：注册图像加载工厂（默认 Halcon 引擎）与 Cv 词汇（Mat→Image）
        ComposeVisionAdapters();

        // 3.5 Phase 2: 注册工作流节点类型转换器
        // 确保反序列化时 WorkflowNodeBase 自动转换为 WorkflowNode 实例
        WorkflowNodeFactory.TypeConverter = baseNode =>
            baseNode is SunEyeVision.UI.Shared.WorkflowNode ? baseNode : SunEyeVision.UI.Shared.WorkflowNode.FromBase(baseNode);

        // 4. 使用默认路径
        var defaultPath = _dataPaths!.SolutionsDirectory;

        // 5. 初始化解决方案管理器
        _splashScreen?.UpdateStage("正在初始化解决方案", 65, "准备方案仓库和用户设置");
        LogStartupStage("SolutionManager.Initialize begin", $"defaultPath={defaultPath}");
        ServiceInitializer.InitializeSolutionManager(_dataPaths!);
        _servicesReady = true;
        _splashScreen?.UpdateStage("正在初始化解决方案", 70, "解决方案管理器准备完成");
        LogStartupStage("SolutionManager.Initialize completed");

        // 5.2 初始化主题管理器（读回持久化主题并应用语义色板）
        InitializeThemeManager();

        // 5.2.5 调试：全局 Button 悬停埋点，确认悬停触发器是否触发（排查暗色主题悬停无反应）
        RegisterButtonHoverTrace();

        // 5.5 注册全局变量选择请求事件处理（点击"全局变量"动作节点 → 打开选择对话框）
        EventManager.RegisterClassHandler(typeof(ParamBinding),
            ParamBinding.GlobalVariableSelectionRequestedEvent,
            new RoutedEventHandler(OnGlobalVariableSelectionRequested));

        // 5.6 订阅 ConfigSetting 全局变量解绑事件（清理 ConsumerBinding + 刷新 UI）
        ConfigSetting.BeforeGlobalVariableUnbinding += OnConfigSettingBeforeGlobalVariableUnbinding;

        // 5.7 订阅全局变量重命名事件（更新已打开的 ConfigSetting 绑定显示）
        var eventBus = ServiceLocator.Instance.GetService<SunEyeVision.Core.Events.IEventBus>();
        if (eventBus != null)
        {
            var renameHandler = new SunEyeVision.Core.Events.EventHandler<UIEvents.GlobalVariableRenamedEvent>(OnGlobalVariableRenamed);
            eventBus.Subscribe(renameHandler);
        }

        // 初始化DispatcherTimer监控
        InitializeMonitoring();

        // 启动决策
        LogStartupStage("Startup decision begin");
        await HandleStartupDecisionAsync();
        _startupActivationReady = true;
        LogStartupStage("HandleStartupDecision returned");
    }

    /// <summary>
    /// 处理启动决策
    /// </summary>
    private async Task HandleStartupDecisionAsync()
    {
        var mainWindow = new MainWindow();
        AttachMainWindowDiagnostics(mainWindow);
        Application.Current.MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _splashScreen?.UpdateStage("正在准备工作区", 85, "启动服务已准备完成");
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        _splashScreen?.CloseImmediately(); _splashScreen = null;

        if (_requestedSolutionPath != null)
        {
            // A file launch never routes through the browser/registration dialog.
            if (mainWindow.DataContext is MainWindowViewModel vm && await vm.OpenExternalSolutionAsync(_requestedSolutionPath))
            {
                ShowMainWindow(mainWindow, "file launch");
                return;
            }
            // A failed file request remains recoverable via the start page, even when hidden by preference.
            await ShowStartPageAsync(mainWindow);
        }
        else if (!StartPreferences.HideStartPage && ServiceInitializer.SolutionManager.Settings.UserPreferences.ContainsKey("StartupMode") &&
                 ServiceInitializer.SolutionManager.Settings.StartupMode is "Last" or "Default")
        {
            // Preserve an explicitly configured legacy restore policy; an explicit file request always takes precedence.
            var manager = ServiceInitializer.SolutionManager;
            var selected = manager.Settings.StartupMode == "Last" ? manager.GetMetadata(manager.Settings.CurrentSolutionId) : manager.GetDefaultSolutionMetadata();
            var restorePath = manager.Settings.StartupMode == "Last" ? StartPreferences.RecentFiles.FirstOrDefault()?.Path ?? selected?.FilePath : selected?.FilePath;
            if (restorePath != null && mainWindow.DataContext is MainWindowViewModel vm && await vm.OpenExternalSolutionAsync(restorePath))
                ShowMainWindow(mainWindow, "explicit restore policy");
            else await ShowStartPageAsync(mainWindow);
        }
        else if (StartPreferences.HideStartPage)
            ShowMainWindow(mainWindow, "start page hidden");
        else
            await ShowStartPageAsync(mainWindow);
    }

    public void SetStartPageHidden(bool hidden)
    {
        bool previous = StartPreferences.HideStartPage;
        StartPreferences.HideStartPage = hidden;
        try { StartPreferences.Save(StartPreferencesFile); }
        catch { StartPreferences.HideStartPage = previous; throw; }
    }

    public void RecordRecentSolution(Solution solution)
    {
        if (string.IsNullOrEmpty(solution.FilePath)) return;
        StartPreferences.RecordOpened(solution.FilePath, solution.Name);
        try { StartPreferences.Save(StartPreferencesFile); }
        catch (Exception ex) { VisionLogger.Instance.Log(LogLevel.Warning, "方案已打开，但最近记录未保存：" + ex.Message, "App"); }
    }

    private void RemoveRecentSolution(string path)
    {
        var files = StartPreferences.RecentFiles.ToList();
        var dismissed = StartPreferences.DismissedPaths.ToList();
        StartPreferences.RecentFiles.RemoveAll(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
        if (!StartPreferences.DismissedPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
            StartPreferences.DismissedPaths.Add(path);
        try { StartPreferences.Save(StartPreferencesFile); }
        catch { StartPreferences.RecentFiles = files; StartPreferences.DismissedPaths = dismissed; throw; }
    }

    public async Task ShowStartPageAsync(MainWindow mainWindow)
    {
        if (mainWindow.DataContext is not MainWindowViewModel vm) return;
        bool wasVisible = mainWindow.IsVisible;
        while (true)
        {
            // Existing catalog entries are optional suggestions, not prerequisites for file opening.
            var recent = StartPreferences.RecentFiles.Concat(ServiceInitializer.SolutionManager.GetAllMetadata()
                .Select(x => new RecentSolutionFile { Name = x.Name, Path = x.FilePath, LastOpened = x.LastAccessTime }))
                .Where(x => !string.IsNullOrWhiteSpace(x.Path) && !StartPreferences.DismissedPaths.Contains(x.Path, StringComparer.OrdinalIgnoreCase))
                .GroupBy(x => Path.GetFullPath(x.Path), StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First()).OrderByDescending(x => x.LastOpened).ToArray();
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
        if (Application.Current.MainWindow is not MainWindow main) return;
        if (!main.IsVisible) ShowMainWindow(main, "solution committed");
        main.ShowSolutionLoadingOverlay("正在恢复设备", "方案已打开，正在恢复已配置相机连接");
        try
        {
            var expected = ServiceInitializer.SolutionManager.CurrentSolution;
            if (expected != null) await ServiceInitializer.CommunicationService.VerifyWorkspaceReadyAsync(expected);
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
        _splashScreen?.UpdateStage("正在显示主界面", 98, "主窗口即将显示");
        LogStartupStage("MainWindow.Show begin", $"reason={reason}");
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
        LogMainWindowState(mainWindow, "after Show");
        _splashScreen?.UpdateStage("启动完成", 100, "主界面已显示");

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
            RecordImageWarmupContentRendered();
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
        try
        {
            VisionLogger.Instance.Log(
                LogLevel.Info,
                $"[StartupTrace] +{_startupStopwatch.ElapsedMilliseconds}ms {stage}" +
                (string.IsNullOrWhiteSpace(detail) ? string.Empty : $" | {detail}"),
                "App");
        }
        catch
        {
            // 启动早期日志系统可能尚未完成初始化，埋点不能阻断应用启动。
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

    private async Task RestoreCameraConnectionsAsync(SolutionManager solutionManager, Action<string, string>? statusReporter = null, bool reportFailure = false)
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
    /// S5 对称架构装配：注册视觉适配层到 SDK 注册点。
    /// ① 图像加载工厂：默认注册 Halcon 引擎（HalAdapter.HalImageFactory）到 VisionImageFactoryLocator，
    ///    使 SDK 门面 VisionImage.FromFile 与工具经统一入口加载；
    /// ② Cv 词汇：CvAdapter 把 OpenCvSharp.Mat 注册为契约词目 "Image"（GlobalVariableValueConverter
    ///    序列化裸 Mat 图像时依赖，恢复 S1 删除静态 Mat 映射后的运行期供给，D2）。
    /// 跨引擎 Cv↔Hal 提取（VisionBackendExtractorLocator）仅在使用双引擎的工作流装配时按需注册，
    /// 当前工具层统一 Halcon，不预注册以保持装配最小（D4）。
    /// </summary>
    private void ComposeVisionAdapters()
    {
        VisionImageFactoryLocator.Current = new HalImageFactory();
        CvAdapterStartup.RegisterSdkVocabulary();
    }

    /// <summary>
    /// Thread-pool warmup retained for explicit isolated on/deferred comparisons.
    /// This adjusts thread minima and waits for empty tasks; it does not predecode images.
    /// </summary>
    private void PrewarmThreadPool()
    {
        try
        {
            // 设置最小线程数，确保线程池立即可用
            ThreadPool.GetMinThreads(out int minWorker, out int minIO);
            ThreadPool.SetMinThreads(
                Math.Max(minWorker, 8),  // 至少8个工作线程
                Math.Max(minIO, 4)       // 至少4个IO线程
            );

            // 预热：启动几个空任务，强制线程池创建线程
            var warmupTasks = new Task[4];
            for (int i = 0; i < 4; i++)
            {
                warmupTasks[i] = Task.Run(() => Thread.Sleep(10));
            }
            Task.WaitAll(warmupTasks);
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Warning, $"线程池预热失败: {ex.Message}", "App");
        }
    }

    private async void RecordImageWarmupContentRendered()
    {
        if (_imageWarmupContentRecorded) return;
        _imageWarmupContentRecorded = true;
        var contentRenderedMs = _startupStopwatch.Elapsed.TotalMilliseconds;
        var mode = EffectiveImageWarmupMode;
        WriteImageWarmupMeasurement("content-rendered", contentRenderedMs, null, null, null);
        if (mode != ImageWarmupMode.Deferred)
        {
            WriteImageWarmupMeasurement("completed", contentRenderedMs, null, null, null);
            return;
        }
        var queuedMs = _startupStopwatch.Elapsed.TotalMilliseconds;
        try
        {
            var timing = await Task.Run(() =>
            {
                var startedMs = _startupStopwatch.Elapsed.TotalMilliseconds;
                PrewarmThreadPool();
                return (Started: startedMs, Ended: _startupStopwatch.Elapsed.TotalMilliseconds);
            });
            WriteImageWarmupMeasurement("completed", contentRenderedMs, queuedMs, timing.Started, timing.Ended);
        }
        catch (Exception ex) { LogStartupStage("Image warmup measurement failed", ex.Message); }
    }

    private void WriteImageWarmupMeasurement(string phase, double contentRenderedMs,
        double? queuedMs, double? workerStartedMs, double? workerEndedMs)
    {
        var context = StartupLaunchContext.Current;
        if (!context.IsIsolated || context.ImageWarmupOverride == null) return;
        try
        {
            ThreadPool.GetMinThreads(out var workers, out var io);
            var data = new
            {
                phase, processId = Environment.ProcessId, sourceCommit = SunEyeVision.Core.Versioning.AppVersionInfo.Commit,
                mode = EffectiveImageWarmupMode.ToString(), runtime = Environment.Version.ToString(),
                startupBlockingMs = _imageWarmupBlockingMs, contentRenderedElapsedMs = contentRenderedMs,
                backgroundQueuedElapsedMs = queuedMs, backgroundStartedElapsedMs = workerStartedMs,
                backgroundCompletedElapsedMs = workerEndedMs,
                backgroundWorkMs = workerEndedMs - workerStartedMs,
                minimumWorkerThreads = workers, minimumIoThreads = io,
                boundary = "ContentRendered is a rendered-window timestamp; it is not an input-latency assertion"
            };
            var path = Path.Combine(context.IsolatedRoot!, "image-warmup-" + phase + ".json");
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, System.Text.Json.JsonSerializer.Serialize(data));
            File.Move(temporary, path); // Each phase is created once in a fresh isolated root.
        }
        catch (Exception ex) { LogStartupStage("Image warmup measurement write failed", ex.Message); }
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
    /// 初始化主题管理器：创建实例、读回持久化主题并应用，注册到服务定位器供顶栏等访问。
    /// </summary>
    private void InitializeThemeManager()
    {
        var settings = ServiceInitializer.SolutionManager.Settings;
        var themeManager = new Services.ThemeManager(settings);
        themeManager.Initialize();
        ServiceLocator.Instance.Register(themeManager);
    }

    /// <summary>
    /// 调试：全局 Button 悬停埋点。确认悬停事件是否触发、IsMouseOver 触发器是否改变 Background。
    /// 用于排查"暗色主题下按钮悬停无反应"（触发器未触发 vs 颜色变化太微弱）。
    /// </summary>
    private static void RegisterButtonHoverTrace()
    {
        try
        {
            EventManager.RegisterClassHandler(typeof(System.Windows.Controls.Button),
                System.Windows.Controls.Control.MouseEnterEvent,
                new System.Windows.Input.MouseEventHandler((sender, e) =>
                {
                    if (sender is not System.Windows.Controls.Button btn) return;
                    var bg = btn.Background;
                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"[按钮悬停] MouseEnter | Name={btn.Name} | Content={btn.Content} | " +
                        $"Background={(bg as SolidColorBrush)?.Color.ToString() ?? bg?.ToString() ?? "null"} | " +
                        $"HasStyle={(btn.Style != null)} | StyleKey={(btn.Style?.GetType().Name ?? "null")}",
                        "App-BtnHover");
                }), true);

            EventManager.RegisterClassHandler(typeof(System.Windows.Controls.Button),
                System.Windows.Controls.Control.MouseLeaveEvent,
                new System.Windows.Input.MouseEventHandler((sender, e) =>
                {
                    if (sender is not System.Windows.Controls.Button btn) return;
                    var bg = btn.Background;
                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"[按钮悬停] MouseLeave | Name={btn.Name} | Background={(bg as SolidColorBrush)?.Color.ToString() ?? "null"}",
                        "App-BtnHover");
                }), true);
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Warning, $"按钮悬停埋点注册失败: {ex.Message}", "App-BtnHover");
        }
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
        // 保存用户设置
        var solutionManager = ServiceInitializer.SolutionManager;
        try { solutionManager?.SaveUserSettings(); }
        catch (Exception ex) { VisionLogger.Instance.Log(LogLevel.Error, "退出时配置保存失败，原配置保留：" + ex.Message, "App", ex); }

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

        base.OnExit(e);
    }

    /// <summary>
    /// 全局变量选择请求处理 — 点击"全局变量"动作节点时打开选择对话框并完成绑定（消费者侧路径）。
    /// 对话框提供搜索/分组筛选/表格视图与按参数类型自动过滤等便捷能力。
    /// </summary>
    private static void OnGlobalVariableSelectionRequested(object sender, RoutedEventArgs e)
    {
        if (sender is not ParamBinding paramBinding)
            return;

        var solutionManager = ServiceInitializer.SolutionManager;
        if (solutionManager == null)
            return;

        // 使用 SdkTypeVocabulary 将参数的 DataType 映射为全局变量类型字符串，用于选择器过滤
        var filterType = SdkTypeVocabulary.ToTypeVocabulary(paramBinding.DataType);

        // 无兜底原则（rule-015）：DataType 已设置但无法映射到任何全局变量通用类型 → 直接报错
        if (filterType == null && paramBinding.DataType != null)
        {
            ThemeMessageDialog.Show(
                $"参数类型 \"{paramBinding.DataType.Name}\" 不是全局变量支持的通用类型。\n" +
                $"\n请将工具输出适配为通用类型后再绑定全局变量。\n" +
                $"支持的通用类型：Integer, Double, String, Boolean, Image, RoI, Transform。",
                "不支持的参数类型", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Warning);
            e.Handled = true;
            return;
        }

        // 打开全局变量选择对话框（带搜索/分组筛选/表格视图，按数据类型过滤）
        var selectedVariable = GlobalVariableSelectDialog.ShowSelect(solutionManager, filterType);
        if (selectedVariable != null)
        {
            paramBinding.CompleteGlobalVariableBinding(selectedVariable.Name);
            RecordConsumerBinding(paramBinding, selectedVariable, solutionManager);
        }

        e.Handled = true;
    }

    /// <summary>
    /// 处理 ConfigSetting 解绑全局变量 — 清理 ConsumerBinding 并刷新 UI
    /// </summary>
    private static void OnConfigSettingBeforeGlobalVariableUnbinding(ConfigSetting configSetting, string variableName)
    {
        try
        {
            var solutionManager = ServiceInitializer.SolutionManager;
            if (solutionManager?.CurrentSolution == null)
                return;

            var variable = solutionManager.CurrentSolution.GlobalVariables
                .FirstOrDefault(v => v.Name == variableName);
            if (variable == null)
                return;

            // 从视觉树向上查找最近的 ToolDebugControlBase，获取节点信息
            var toolControl = FindVisualParent<ToolDebugControlBase>(configSetting);
            string? nodeId = null;
            string? paramName = configSetting.ParameterName;

            if (toolControl?.CurrentNode is { } node)
            {
                nodeId = node.GetType().GetProperty("Id")?.GetValue(node) as string;
            }

            if (string.IsNullOrEmpty(nodeId) || string.IsNullOrEmpty(paramName))
                return;

            // 节点 ID 只在所属工作流作用域内有意义；旧方案缺少 WorkflowId 时，
            // 只有在全方案中能唯一定位节点才允许兼容清理，避免误删其他工作流的同名绑定。
            var matchingWorkflows = solutionManager.CurrentSolution.Workflows
                .Where(w => (w.Nodes ?? Enumerable.Empty<WorkflowNodeBase>()).Any(n => n.Id == nodeId))
                .ToList();
            if (matchingWorkflows.Count != 1)
                return;

            var workflowId = matchingWorkflows[0].Id;

            // 查找并移除匹配的 ConsumerBinding
            var removed = variable.ConsumerBindings.RemoveAll(c =>
                c.NodeId == nodeId
                && c.ParameterName == paramName
                && (c.WorkflowId == workflowId || string.IsNullOrEmpty(c.WorkflowId)));

            if (removed > 0)
            {
                variable.NotifyOutputDisplayChanged();
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[监控] 清理 ConsumerBinding 成功 | " +
                    $"variable={variableName} | nodeId={nodeId} | param={paramName}",
                    "App-Monitor");
            }
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Error,
                $"[监控] 清理 ConsumerBinding 异常 | {ex.Message}",
                "App-Monitor", ex);
        }
    }

    /// <summary>
    /// 全局变量重命名事件处理 — 通过 ConfigSetting 静态通知机制更新所有已打开实例的绑定显示
    /// </summary>
    private static void OnGlobalVariableRenamed(UIEvents.GlobalVariableRenamedEvent evt)
    {
        VisionLogger.Instance.Log(LogLevel.Info,
            $"[监控] 全局变量重命名事件进入 | old='{evt.OldName}' → new='{evt.NewName}' | source={evt.Source}",
            "App-Monitor");
        try
        {
            ConfigSetting.NotifyAllGlobalVariableRenamed(evt.OldName, evt.NewName);

            VisionLogger.Instance.Log(LogLevel.Success,
                $"[监控] 全局变量重命名 ConfigSetting 通知完成 | old='{evt.OldName}' → new='{evt.NewName}'",
                "App-Monitor");
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Error,
                $"[监控] 全局变量重命名更新异常 | {ex.Message}",
                "App-Monitor", ex);
        }
    }

    /// <summary>
    /// 记录消费者绑定：参数绑定到全局变量时自动追加 ConsumerBinding 记录
    /// </summary>
    private static void RecordConsumerBinding(ParamBinding paramBinding, GlobalVariable variable, SolutionManager solutionManager)
    {
        try
        {
            var logger = VisionLogger.Instance;

            // ★ 从视觉树向上查找最近的 ToolDebugControlBase，获取节点 ID 和名称
            // 使用动态解析而非预设置，避免因 TabItem 懒加载导致 ParamBinding 创建时未设置 NodeId
            string? nodeId = null;
            string? nodeName = null;

            var toolControl = FindVisualParent<ToolDebugControlBase>(paramBinding);

            // ★ 监控日志1：FindVisualParent 结果
            logger.Log(LogLevel.Info,
                $"[监控] FindVisualParent<ToolDebugControlBase> | " +
                $"found={(toolControl != null)} | " +
                $"controlType={toolControl?.GetType().FullName ?? "null"}",
                "App-Monitor");

            if (toolControl?.CurrentNode is { } node)
            {
                // ★ 监控日志2：CurrentNode 存在
                logger.Log(LogLevel.Info,
                    $"[监控] CurrentNode | " +
                    $"nodeType={node.GetType().FullName}",
                    "App-Monitor");

                nodeId = node.GetType().GetProperty("Id")?.GetValue(node) as string;
                nodeName = node.GetType().GetProperty("DispName")?.GetValue(node) as string
                        ?? node.GetType().GetProperty("Name")?.GetValue(node) as string;

                // ★ 监控日志3：反射提取结果
                logger.Log(LogLevel.Info,
                    $"[监控] 节点信息反射提取 | " +
                    $"Id={nodeId ?? "null"} | " +
                    $"DispName/Name={nodeName ?? "null"}",
                    "App-Monitor");
            }
            else
            {
                // ★ 监控日志4：CurrentNode 为 null 或 toolControl 为 null
                logger.Log(LogLevel.Warning,
                    $"[监控] ★★★ CurrentNode 不可用 ★★★ | " +
                    $"toolControl null={(toolControl == null)} | " +
                    $"CurrentNode null={(toolControl?.CurrentNode == null)}",
                    "App-Monitor");
            }

            if (string.IsNullOrEmpty(nodeId) || string.IsNullOrEmpty(nodeName))
            {
                logger.Log(LogLevel.Warning,
                    $"[监控] ★★★ 节点信息为空，ConsumerBinding 终止 ★★★ | " +
                    $"nodeId={nodeId ?? "null"} | " +
                    $"nodeName={nodeName ?? "null"} | " +
                    $"toolControl found={(toolControl != null)} | " +
                    $"CurrentNode null={(toolControl?.CurrentNode == null)}",
                    "App-Monitor");
                return;
            }

            var paramName = paramBinding.ParameterName;
            // ★ 监控日志5：参数名
            logger.Log(LogLevel.Info,
                $"[监控] ParameterName={paramName ?? "null"}",
                "App-Monitor");

            if (string.IsNullOrEmpty(paramName))
            {
                logger.Log(LogLevel.Warning,
                    "[监控] ★★★ ParameterName 为空，ConsumerBinding 终止 ★★★",
                    "App-Monitor");
                return;
            }

            // ★ 查找节点所属工作流名称和参数显示名称
            string? workflowName = null;
            string? workflowId = null;
            string? parameterDisplayName = null;

            var solution = solutionManager.CurrentSolution;
            if (solution != null)
            {
                foreach (var wf in solution.Workflows)
                {
                    var workflowNode = wf.Nodes?.FirstOrDefault(n => n.Id == nodeId);
                    if (workflowNode != null)
                    {
                        workflowName = wf.Name ?? wf.Id;
                        workflowId = wf.Id;

                        // 从参数的 [DisplayName] 属性获取中文显示名
                        if (workflowNode.Parameters != null)
                        {
                            var prop = workflowNode.Parameters.GetType().GetProperty(paramName);
                            if (prop != null)
                            {
                                var attr = prop.GetCustomAttribute<DisplayNameAttribute>(false);
                                parameterDisplayName = attr?.DisplayName ?? paramName;
                            }
                        }
                        break;
                    }
                }
            }

            // ★ 监控日志6：工作流查找结果
            logger.Log(LogLevel.Info,
                $"[监控] 工作流查找 | " +
                $"solution null={(solution == null)} | " +
                $"workflowCount={solution?.Workflows?.Count ?? 0} | " +
                $"found={(workflowName != null)} | " +
                $"workflowName={workflowName ?? "null"} | " +
                $"parameterDisplayName={parameterDisplayName ?? "null"}",
                "App-Monitor");

            // 检查是否已存在相同的消费者绑定
            var existing = variable.ConsumerBindings
                .FirstOrDefault(c => c.WorkflowId == (workflowId ?? string.Empty)
                    && c.NodeId == nodeId
                    && c.ParameterName == paramName);

            if (existing != null)
            {
                // 已存在则更新节点名称、工作流名称、参数显示名称
                existing.NodeName = nodeName;
                if (workflowId != null) existing.WorkflowId = workflowId;
                if (workflowName != null) existing.WorkflowName = workflowName;
                if (parameterDisplayName != null) existing.ParameterDisplayName = parameterDisplayName;
            }
            else
            {
                // 追加新的消费者绑定（含 WorkflowName + ParameterDisplayName）
                variable.ConsumerBindings.Add(new ConsumerBinding
                {
                    WorkflowId = workflowId ?? "",
                    NodeId = nodeId,
                    NodeName = nodeName,
                    WorkflowName = workflowName ?? "",
                    ParameterName = paramName,
                    ParameterDisplayName = parameterDisplayName ?? paramName
                });
            }

            // ★ 监控日志7：ConsumerBinding 操作结果
            logger.Log(existing != null ? LogLevel.Info : LogLevel.Success,
                $"[监控] ConsumerBinding {(existing != null ? "更新" : "添加")}成功 | " +
                $"consumerCount={variable.ConsumerBindings.Count} | " +
                $"DisplayPath={variable.OutputTargetDisplayText}",
                "App-Monitor");

            // ★ 通知 UI 刷新"目标输出"列
            variable.NotifyOutputDisplayChanged();

            // ★ 监控日志8：UI 刷新通知完成
            logger.Log(LogLevel.Info,
                $"[监控] NotifyOutputDisplayChanged 已调用 | " +
                $"OutputTargetDisplayText 新值={variable.OutputTargetDisplayText}",
                "App-Monitor");

            // 持久化保存
            try
            {
                if (solutionManager.CurrentSolution?.FilePath != null)
                    solutionManager.SaveSolution(solutionManager.CurrentSolution.FilePath);
            }
            catch { /* 静默忽略保存失败，不影响用户体验 */ }
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Error,
                $"[监控] ★★★ RecordConsumerBinding 异常 ★★★ | {ex.Message} | " +
                $"StackTrace: {ex.StackTrace}",
                "App-Monitor", ex);
        }
    }

    /// <summary>
    /// 在视觉树中向上查找指定类型的父控件
    /// </summary>
    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        if (child == null)
            return null;

        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T typedParent)
                return typedParent;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

}

