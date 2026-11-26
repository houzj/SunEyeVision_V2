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
using SunEyeVision.UI.Shell.Controls.Common;
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
using SunEyeVision.Plugin.Runtime;

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
    private LoadingWindow? _startupLoadingWindow;
    private readonly CancellationTokenSource _startupCancellation = new();
    private readonly CancellationTokenSource _startupWatchdogCancellation = new();
    private int _startupCancellationHandled;
    private int _startupWatchdogStopped;
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StartupCancellationTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StartupDispatcherGrace = TimeSpan.FromSeconds(3);
    private static readonly string StartupTracePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SunEyeVision", "Logs", "startup-trace.txt");
    private string? _lastStartupStage;

    static App()
    {
        // 只记录警告和错误级别的绑定信息，避免性能问题
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        ResetStartupTrace();
        WriteStartupMarker("after-reset");
        // First-run directory dialogs may temporarily be the only window; do not exit when they close.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        // 与 VM 原件一致：工作目录固定为程序目录，保证从快捷方式、文件关联或任意目录启动时相对路径可用。
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        try
        {
            WriteStartupMarker("before-application-lifetime-gate");
            _runningApplicationRegistration = ApplicationLifetimeGate.TryRegister();
            if (_runningApplicationRegistration == null)
                throw new InvalidOperationException("SunEyeVision 正在卸载或维护，请等待操作结束后再启动。");
            WriteStartupMarker("after-application-lifetime-gate");
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
            WriteStartupMarker("before-argument-parse");
            StartupLaunchContext.Current = StartupLaunchContext.Parse(e.Args);
            _requestedSolutionPath = StartupLaunchContext.Current.SolutionPath;
            WriteStartupMarker("after-argument-parse");
        }
        catch (Exception ex)
        {
            _startupRejected = true;
            ThemeMessageDialog.Show(ex.Message, "启动参数无效", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            Shutdown(1); return;
        }
        try
        {
            WriteStartupMarker("before-single-instance-register");
            _singleInstance = SingleInstanceCoordinator.TryStart(HandleInstanceActivationAsync, StartupLaunchContext.Current.InstanceIdentity);
            WriteStartupMarker(_singleInstance == null ? "after-single-instance-register-null" : "after-single-instance-register");
            if (_singleInstance == null)
            {
                _startupRejected = true;
                WriteStartupMarker("before-single-instance-forward");
                var activation = await SingleInstanceCoordinator.SendAsync(_requestedSolutionPath, identity: StartupLaunchContext.Current.InstanceIdentity);
                WriteStartupMarker($"after-single-instance-forward-accepted={activation.Accepted}");
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

        // 先显示启动加载窗口，再完成运行环境初始化。原厂先完成启动阶段，
        // 方案管理器属于启动完成后的用户入口，不能反过来阻塞插件和服务初始化。
        try
        {
            _startupLoadingWindow = new LoadingWindow("正在准备运行环境");
            _startupLoadingWindow.CancelRequested += StartupLoadingWindow_CancelRequested;
            _startupLoadingWindow.Show();
            _ = MonitorStartupTimeoutAsync();
            InitializeDataPathsAndPreferences();
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
        try
        {
            await Task.Run(() => pluginManager.LoadPluginsAsync(_startupCancellation.Token), _startupCancellation.Token);
        }
        catch (OperationCanceledException) when (_startupCancellation.IsCancellationRequested)
        {
            await CancelStartupAsync();
            return;
        }
        catch (Exception ex)
        {
            // 载荷哈希与磁盘程序不一致时，明确提示重新封存，而不是落到
            // Dispatcher 未处理异常对话框里只显示堆栈。
            LogStartupStage("Plugin.Load failed", ex.ToString());
            _startupRejected = true;
            ThemeMessageDialog.Show(
                "模块加载失败：" + ex.Message +
                "\n\n如果刚刚重新构建过程序，请重新封存 Release 载荷，或使用 scripts/verification/Invoke-ReleaseLaunch.ps1 启动。",
                "启动失败", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            Shutdown(1);
            return;
        }
        if (await StopStartupIfRequestedAsync()) return;
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
        var desktopAdmission = pluginManager.DesktopAdmission ?? throw new InvalidOperationException("缺少已准入运行目录。");

        // 2. 标记插件已加载（全局状态）


        // 3. 初始化服务（包括节点显示适配器）
        _mainWindow?.UpdateSolutionLoadingOverlay("正在初始化服务", "注册运行时服务和图像适配器");
        LogStartupStage("Service.Initialize begin");
        // Service registration, adapter registration and log-writer admission are
        // filesystem/static-service work. They do not create a DocumentSession or
        // touch WPF objects, so keep them off the dispatcher while the startup
        // surface remains visible and movable.
        await Task.Run(() => ServiceInitializer.InitializeServices());
        if (await StopStartupIfRequestedAsync()) return;
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
        _desktopDirectoryPin=pluginManager.PinDesktopHost();
        if (await StopStartupIfRequestedAsync()) return;
        LogStartupStage("SolutionManager directory pinned");
        var solutionManagerClock = Stopwatch.StartNew();
        ServiceInitializer.InitializeSolutionManager(_dataPaths!, desktopAdmission.Runtime);
        solutionManagerClock.Stop();
        _servicesReady = true;
        _mainWindow?.UpdateSolutionLoadingOverlay("正在初始化解决方案", "解决方案管理器准备完成");
        LogStartupStage("SolutionManager.Initialize completed", $"elapsedMs={solutionManagerClock.Elapsed.TotalMilliseconds:F1}");
        if (await StopStartupIfRequestedAsync()) return;

        // 5.2 校验唯一主题字典的语义键契约（VM 原件只有一套固定主题，不提供运行时切换）
        InitializeFixedTheme();

        // 初始化DispatcherTimer监控
        InitializeMonitoring();

        // 启动决策
        LogStartupStage("Startup decision begin");
        StopStartupWatchdog();
        // The startup loading window ends before the welcome page. The main
        // workspace remains hidden until the user chooses a solution.
        _startupLoadingWindow?.CloseProgrammatically();
        _startupLoadingWindow = null;
        // 原厂 GuideWindow 语义（证据见 docs/07-工具逆向/证据-原厂引导窗口启动语义-20261117.md）：
        // 工作区必须先就绪，引导页只是盖在其上的模态引导层；关闭它不创建任何方案。
        await HandleStartupDecisionAsync(desktopAdmission, _mainWindow);
        _startupActivationReady = true;
        LogStartupStage("HandleStartupDecision returned");
        StartExecutionJitWarmup(desktopAdmission);
    }

    // The execution chain is managed code, so the CLR compiles its methods the first time a
    // real run reaches them. That is the first-run penalty a natively compiled platform does
    // not have, and it is the only part HALCON platform admission cannot cover. Preparing the
    // methods on a below-normal background thread moves the compilation into the gap between
    // startup and the first user execution without touching tool state or solution resources.
    private void StartExecutionJitWarmup(DesktopToolAdmission admission)
    {
        var assemblies = admission.Runtime.RuntimeAssemblies
            .Concat(new[]
            {
                typeof(SunEyeVision.Application.DocumentSession).Assembly,
                typeof(SunEyeVision.Execution.WorkflowExecutionEngine).Assembly
            })
            .Distinct()
            .ToArray();
        var thread = new Thread(() =>
        {
            try { RuntimeMethodWarmup.Prepare("execution-chain", assemblies); }
            catch (Exception error) { LogStartupStage("Execution JIT warmup failed", error.Message); }
        })
        {
            IsBackground = true,
            Name = "SunEyeVision.ExecutionJitWarmup",
            Priority = ThreadPriority.BelowNormal
        };
        thread.Start();
    }

    private void StartupLoadingWindow_CancelRequested(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _startupCancellationHandled, 1) != 0)
            return;
        _startupRejected = true;
        _startupCancellation.Cancel();
        _startupLoadingWindow?.ShowCancelling();
        LogStartupStage("Startup cancellation requested", "source=loading-window");
    }

    private async Task<bool> StopStartupIfRequestedAsync()
    {
        if (!_startupCancellation.IsCancellationRequested)
            return false;
        await CancelStartupAsync();
        return true;
    }

    private async Task CancelStartupAsync()
    {
        _startupRejected = true;
        _startupCancellation.Cancel();
        try
        {
            var drain = DrainPluginDirectoryAsync();
            if (await Task.WhenAny(drain, Task.Delay(StartupCancellationTimeout)) != drain)
            {
                LogStartupStage("Startup cancellation cleanup timeout",
                    $"timeoutMs={StartupCancellationTimeout.TotalMilliseconds:0}");
            }
            else
            {
                await drain;
            }
        }
        catch (Exception ex)
        {
            LogStartupStage("Startup cancellation cleanup failed", ex.ToString());
        }
        finally
        {
            _startupLoadingWindow?.CloseProgrammatically();
            _startupLoadingWindow = null;
            if (!Dispatcher.HasShutdownStarted)
                Shutdown(0);
        }
    }

    private async Task MonitorStartupTimeoutAsync()
    {
        try
        {
            await Task.Delay(StartupTimeout, _startupWatchdogCancellation.Token).ConfigureAwait(false);
            if (Volatile.Read(ref _startupWatchdogStopped) != 0 || _startupRejected || _startupActivationReady)
                return;
            LogStartupStage("Startup timeout", $"timeoutMs={StartupTimeout.TotalMilliseconds:0}");
            // UI 线程被同步阶段堵住时，InvokeAsync 会一直排队：先探测 Dispatcher
            // 是否还能处理消息；无响应则走硬中止，保证不会出现只能杀进程的卡死窗口。
            var dispatcher = Dispatcher;
            var ping = dispatcher.InvokeAsync(() => { }, DispatcherPriority.Send);
            var completed = await Task.WhenAny(ping.Task, Task.Delay(StartupDispatcherGrace)).ConfigureAwait(false);
            if (!ReferenceEquals(completed, ping.Task))
            {
                AbortHungStartup("启动超时且界面线程无响应");
                return;
            }
            try { await ping.Task.ConfigureAwait(false); } catch { }
            await dispatcher.InvokeAsync(async () => await FailStartupTimeoutAsync());
        }
        catch (OperationCanceledException) when (_startupWatchdogCancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            LogStartupStage("Startup watchdog failed", error.ToString());
        }
    }

    /// <summary>
    /// UI 线程无响应或清理阶段无法退出时的最后一道保障：写独立轨迹、用独立 STA 线程
    /// 显示失败原因，然后强制结束进程。不能依赖 Dispatcher，否则同样被堵死。
    /// </summary>
    private void AbortHungStartup(string reason)
    {
        var stage = _lastStartupStage ?? "unknown";
        try
        {
            LogStartupStage("Startup hard abort", reason);
            var directory = Path.GetDirectoryName(StartupTracePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.AppendAllText(StartupTracePath,
                $"{DateTime.Now:O} [StartupTrace] HARD-ABORT reason={reason}; lastStage={stage}{Environment.NewLine}");
        }
        catch { }
        var thread = new Thread(() =>
        {
            try
            {
                MessageBox.Show(
                    $"启动已超过 {StartupTimeout.TotalSeconds:0} 秒且界面无响应，程序将强制退出。\n\n" +
                    $"最后阶段：{stage}\n\n启动轨迹：{StartupTracePath}",
                    "SunEyeVision 启动失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch { }
            Environment.Exit(1);
        })
        {
            IsBackground = false,
            Name = "SunEyeVision.StartupAbort"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private async Task FailStartupTimeoutAsync()
    {
        if (_startupRejected || Dispatcher.HasShutdownStarted) return;
        _startupRejected = true;
        _startupCancellation.Cancel();
        _startupLoadingWindow?.ShowError("启动超时：运行环境未能在 30 秒内完成。正在退出，请检查 startup-trace.txt。");
        try
        {
            ThemeMessageDialog.Show(
                "启动超时：运行环境未能在 30 秒内完成。\n\n请查看 startup-trace.txt 或日志中的最后一个 StartupTrace 阶段。",
                "SunEyeVision 启动失败", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
        }
        finally
        {
            _startupLoadingWindow?.CloseProgrammatically();
            _startupLoadingWindow = null;
            if (!Dispatcher.HasShutdownStarted) Shutdown(1);
        }
        await Task.CompletedTask;
    }

    private void StopStartupWatchdog()
    {
        if (Interlocked.Exchange(ref _startupWatchdogStopped, 1) == 0)
            _startupWatchdogCancellation.Cancel();
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
        // 原厂 GuideWindow 时序：引导层出现时主界面保持隐藏，关闭引导后才显示工作区，
        // 避免“欢迎页浮在已可见主界面上”的双窗口观感。
        var showShellBeforeGuide = _requestedSolutionPath != null;
        if (showShellBeforeGuide && !mainWindow.IsVisible)
            ShowMainWindow(mainWindow, "workspace shell");

        var openedByRequest = false;
        if (_requestedSolutionPath != null)
        {
            // A file launch never routes through the browser/registration dialog.
            mainWindow.ShowSolutionLoadingOverlay("正在打开方案", "正在读取方案并准备工作区");
            string? failure = null;
            try
            {
                if (mainWindow.DataContext is MainWindowViewModel vm)
                {
                    if (await vm.OpenExternalSolutionAsync(_requestedSolutionPath)) openedByRequest = true;
                    else failure = vm.LastWorkspaceOpenResult?.Error;
                }
            }
            finally { mainWindow.HideSolutionLoadingOverlay(); }
            if (!openedByRequest)
            {
                // 打开失败绝不能停在空壳：先保证有可编辑工作区，再明确报错。
                await EnsureEditableWorkspaceAsync(mainWindow);
                ThemeMessageDialog.Show(
                    string.IsNullOrWhiteSpace(failure) ? "该方案未被接纳，已为你保留可编辑的空白工作区。" : failure!,
                    "方案打开失败", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            }
        }
        else
        {
            // 未指定方案时先建立可编辑空白工作区：引导层关闭后才不会落到"无效方案"。
            await EnsureEditableWorkspaceAsync(mainWindow);
        }

        mainWindow.HideSolutionLoadingOverlay();
        // 回放上次引导页选择的工具箱优先分类：不显示引导层（已隐藏/直接开文件）时同样生效。
        ApplyToolboxPreset(mainWindow);
        if (!openedByRequest && !StartPreferences.HideStartPage)
            await ShowStartPageAsync(mainWindow);
        else if (!mainWindow.IsVisible)
            // 引导页被隐藏或直接打开方案失败时没有走 ShowStartPageAsync 的收尾，
            // 必须在这里显式显示工作区，否则进程持有单实例锁但没有任何可见窗口，
            // 后续启动会被单实例转发拒绝（exit=2）。
            ShowMainWindow(mainWindow, "start page hidden or solution request");
    }

    /// <summary>
    /// 应用引导页卡片选择的工具箱优先分类（原厂 ToolBoxType 语义：只影响工具箱显示，不建方案）。
    /// </summary>
    private void ApplyToolboxPreset(MainWindow mainWindow)
    {
        var category = StartPreferences.ToolboxPreset switch
        {
            "locate" or "Position" => "定位",
            "detect" or "Defect" => "缺陷检测",
            "identify" or "Recognition" => "识别",
            _ => null
        };
        if (mainWindow.DataContext is not MainWindowViewModel vm) return;
        try
        {
            // 原厂语义：按选定类型调整工具箱分类优先级（目标分类置顶；通用方案恢复默认排列）。
            vm.Toolbox.ApplyPriorityPreset(category);
            // 默认保持工具箱收起（图标条），不自动展开；仅在列表模式下把目标分类展开到可见位置。
            var target = category is null ? null : vm.Toolbox.Categories.FirstOrDefault(c => c.Name == category);
            if (target is not null && !vm.Toolbox.IsCompactMode)
            {
                foreach (var item in vm.Toolbox.Categories) item.IsExpanded = ReferenceEquals(item, target);
                target.IsExpanded = true;
            }
            LogStartupStage("StartPage.ToolboxPreset", $"preset={StartPreferences.ToolboxPreset ?? "-"}; category={category ?? "-"}; first={vm.Toolbox.Categories.FirstOrDefault()?.Name ?? "-"}");
        }
        catch (Exception ex) { LogStartupStage("StartPage.ToolboxPresetFailed", ex.Message); }
    }

    /// <summary>
    /// 确保当前存在可编辑工作区（无会话时创建空白方案）。引导层出现前后都以此为不变量。
    /// </summary>
    private async Task<bool> EnsureEditableWorkspaceAsync(MainWindow mainWindow)
    {
        if (ServiceInitializer.SolutionManager.CurrentSession is not null) return true;
        mainWindow.ShowSolutionLoadingOverlay("正在创建方案", "正在准备空白方案工作区");
        try
        {
            if (mainWindow.DataContext is MainWindowViewModel vm && await vm.NewEmptySolutionAsync()) return true;
            return false;
        }
        finally { mainWindow.HideSolutionLoadingOverlay(); }
    }

    /// <summary>
    /// 在插件准入之前显示方案管理器。这里仅收集用户选择，不创建工作区、工具箱
    /// 或节点编辑器；选择完成后 OnStartup 才创建 MainWindow 并进入加载覆盖层。
    /// </summary>
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
            LogStartupStage("StartPage.Result", $"action={page.SelectedAction}; template={page.SelectedTemplate ?? "-"}");
            // 原厂语义：卡片只记录工具箱优先分类，关闭只收尾，两者都不创建方案。
            if (page.SelectedTemplate is { Length: > 0 } preset)
            {
                StartPreferences.ToolboxPreset = preset;
                try { SaveShellPreferences(); } catch (Exception ex) { LogStartupStage("StartPage.PresetSaveFailed", ex.Message); }
            }
            switch (page.SelectedAction)
            {
                case StartPageAction.OpenFile:
                    if (page.SelectedPath == null || !await vm.OpenExternalSolutionAsync(page.SelectedPath)) continue;
                    break;
                case StartPageAction.NewSolution:
                    // 工作区在引导层出现前已建立；这里只保证不变量成立。
                    if (!await EnsureEditableWorkspaceAsync(mainWindow)) continue;
                    break;
                default:
                    // 关闭引导层：直接进入已有可编辑工作区，不新建、不退出。
                    if (!await EnsureEditableWorkspaceAsync(mainWindow)) continue;
                    break;
            }
            ApplyToolboxPreset(mainWindow);
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
    internal string PluginDrainDiagnostics => _pluginManager?.RuntimeLeaseDiagnostics ?? "插件管理器不存在";

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
        // 始终使用显式关机：主窗口 OnClosed 里已显式调用 Shutdown()，退出行为不变；
        // 但不能用 OnMainWindowClose——Application.MainWindow 可能被欢迎页或工具参数窗口占用，
        // 那样关闭这些附属窗口会把整个应用一起关掉（缺陷 DEF-001）。
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        System.Windows.Application.Current.MainWindow = mainWindow;
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
        _lastStartupStage = string.IsNullOrWhiteSpace(detail) ? stage : $"{stage} | {detail}";
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

        // 启动卡死时 UI 线程可能无法再写日志，因此无论是否隔离启动都同步落一份
        // 独立时间线；硬超时中止时据此报告最后阶段。
        try
        {
            var isolatedRoot = StartupLaunchContext.Current.IsolatedRoot;
            var path = string.IsNullOrWhiteSpace(isolatedRoot)
                ? StartupTracePath
                : Path.Combine(isolatedRoot, "startup-trace.txt");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.AppendAllText(path, $"{DateTime.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // 诊断文件失败不能改变启动语义。
        }
    }

    private static void ResetStartupTrace()
    {
        try
        {
            if (File.Exists(StartupTracePath)) File.Delete(StartupTracePath);
        }
        catch
        {
            // 删除失败时保留旧文件，后续追加仍可定位本次启动。
        }
    }

    /// <summary>
    /// 启动早期不能依赖 StartupLaunchContext 或日志服务；用固定路径留下最小路径证据。
    /// </summary>
    private static void WriteStartupMarker(string stage)
    {
        try
        {
            var directory = Path.GetDirectoryName(StartupTracePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.AppendAllText(StartupTracePath,
                $"{DateTime.Now:O} [StartupMarker] {stage}{Environment.NewLine}");
        }
        catch
        {
            // 启动标记只用于诊断，不能改变启动语义。
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
        StopStartupWatchdog();
        _startupWatchdogCancellation.Dispose();
        _startupCancellation.Cancel();
        try { _startupLoadingWindow?.CloseProgrammatically(); } catch { }
        _startupLoadingWindow = null;
        MarkInstanceClosing();
        if (_releaseProbe || _startupRejected || !_servicesReady)
        {
            RuntimeLifecycleProbe.Log("application-exit");
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

        // 记录方案、插件和结果消费者释放后的最终资源计数。
        RuntimeLifecycleProbe.Log("application-exit");
        base.OnExit(e);
    }

}

