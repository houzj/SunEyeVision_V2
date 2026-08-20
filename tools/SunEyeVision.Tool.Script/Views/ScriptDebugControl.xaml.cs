using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using System.Xml;
using System.Collections.Immutable;
using System.Composition.Hosting;
using System.Runtime.InteropServices;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.SignatureHelp;
using Microsoft.CodeAnalysis.Text;

using SunEyeVision.Tool.Script.Completion;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using LogEntry = SunEyeVision.Tool.Script.Models.LogEntry;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Themes;
using SunEyeVision.Tool.Script.Models;
using SunEyeVision.Tool.Script.ScriptEngine;
using SunEyeVision.Workflow;
using ScriptExecutionContext = SunEyeVision.Plugin.SDK.Execution.Parameters.ExecutionContext;

namespace SunEyeVision.Tool.Script.Views
{
    public partial class ScriptDebugControl
    {
        private ScriptParameters _parameters = new();
        private readonly ObservableCollection<LogEntry> _logEntries = new();
        private ScriptTool? _scriptTool;

        // Roslyn completion workspace
        private ScriptWorkspace? _workspace;
        private bool _workspaceInitialized;                           // 实例 workspace 是否已建（含引用/项目/文档骨架），用于实例级复用
        private DocumentId? _documentId;                              // 生成的字段声明文档 ID（不可变，仅 RebuildWorkspace 赋值）
        private DocumentId? _userDocumentId;                          // 用户代码文档 ID（不可变，仅 RebuildWorkspace 赋值）
        private AvalonEditTextContainer? _userTextContainer;         // 用户文档增量文本通道（SourceTextContainer），随 RebuildWorkspace 重建
        private CompletionWindow? _completionWindow;
        private CompletionItem[] _currentItems;                        // 唯一数据源（单层，无后台刷新）
        private CancellationTokenSource? _throttleCts;                // 补全查询取消令牌（新查询取消旧查询）
        private OverloadInsightWindow? _insightWindow;                // 签名帮助浮窗（参数提示，复用实例）

        // 签名帮助：用公开语法树 + IMethodSymbol 定位方法调用（见 ShowSignatureHelpAsync）。
        // Roslyn 4.12 的 SignatureHelpService/ISignatureHelpProvider/Items 全是 internal（CS0122），
        // RoslynPad 靠 IgnoresAccessChecksTo 硬闯；我们不引入该包（保持插件目录纯净，rule-016），
        // 改用公开 API 自定位，对嵌套括号 GetImageSize(((HalImage)input1) 天然免疫。

        // Phase 1: 实时诊断
        private CancellationTokenSource? _diagnosticsCts;             // 500ms 诊断节流
        private IReadOnlyList<Diagnostic> _currentDiagnostics = [];   // 当前错误/警告列表（波浪线 + ToolTip 消费）
        private string? _lastDiagnosedCode;                            // 上次已诊断的代码，用于跳过不变代码
        private string? _lastGeneratedCode;                            // 上次已同步到 workspace 的 GeneratedCode，用于跳过无意义的 solution 重建
        private System.Windows.Controls.ToolTip? _diagnosticToolTip;   // 诊断悬停详情浮窗（Error/Warning 消息）
        private int _lastDiagToolTipOffset = -1;                       // 上次已显示 ToolTip 的诊断偏移（防 MouseMove 闪烁）

        // 初始化阶段抑制诊断（避免 TextChanged 在 workspace 就绪前触发诊断，
        // 消除 "[Diagnostic] 跳过: workspace=False" 日志噪声）
        private bool _suppressDiagnostic;

        // Phase 2b: Roslyn 语义高亮（IHighlighter 架构，生命周期由 RoslynHighlightingColorizer 管理）。
        // 保存 colorizer 引用用于文档就绪后触发语义模型预热（RebuildWorkspace 完成后调用 Warmup）。
        // 挂载标志：构造函数只创建不挂载（workspace 未就绪），RebuildWorkspace 完成后首次挂载（对齐参考时序）。
        private RoslynHighlightingColorizer? _semanticColorizer;
        private bool _semanticColorizerMounted;

        // 进程生命周期内共享的 MefHostServices（首次初始化慢，后续缓存命中快）
        private static MefHostServices? _sharedMefHost;
        private static object _mefLock = new();

        // 预热 ScriptWorkspace 缓存（工具注册时创建，SetParameters 中复用）
        private static ScriptWorkspace? _preWarmedWorkspace;

        // 补全引擎就绪标志（MEF + Workspace 预热完成后置 true）。
        // 应用启动后台预热（ScriptTool 静态构造 → WarmupForStartup）通常已就绪；
        // 用户在启动瞬间极速打开工具时，SetParameters 据此决定"立即重建 or 等待预热完成"。
        private static volatile bool _completionReady;

        // 补全就绪回调（预热完成时通知等待中的 SetParameters 重建 workspace）
        private static Action? _onCompletionReady;

        /// <summary>
        /// 线程安全地获取共享 MefHostServices（LazyInitializer 消除预热与主线程的竞态）。
        /// </summary>
        private static MefHostServices GetSharedMefHost()
        {
            return LazyInitializer.EnsureInitialized(ref _sharedMefHost, ref _mefLock, () =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var host = MefHostServices.Create(MefHostServices.DefaultAssemblies);
                sw.Stop();
                PluginLogger.Info($"[补全] MefHostServices 首次初始化: {sw.ElapsedMilliseconds}ms",
                    nameof(ScriptDebugControl));
                return host;
            });
        }

        /// <summary>
        /// 应用启动后台预热补全引擎（MEF + Workspace），对齐新版 RoslynPad 的 Task.Run 启动初始化。
        /// ScriptTool 静态构造调用（应用启动 LoadPlugins 实例化时自动触发），fire-and-forget 不阻塞启动。
        /// 幂等：双重检查锁保证进程内只预热一次；完成后置 _completionReady 并通知等待中的 SetParameters。
        /// </summary>
        internal static void WarmupForStartup()
        {
            lock (_warmupLock)
            {
                if (_warmupStarted) return;     // 幂等：进程内只预热一次
                _warmupStarted = true;
            }

            _ = Task.Run(async () =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                PluginLogger.Info("[补全] 启动后台预热 Roslyn 开始...", nameof(ScriptDebugControl));
                await WarmupRoslynAsync();
                await WarmupWorkspaceAsync();
                sw.Stop();
                _completionReady = true;
                PluginLogger.Info($"[补全] 启动后台预热完成: {sw.ElapsedMilliseconds}ms", nameof(ScriptDebugControl));

                // 通知等待中的 SetParameters（启动瞬间打开工具场景）重建 workspace
                var cb = Interlocked.Exchange(ref _onCompletionReady, null);
                cb?.Invoke();
            });
        }

        private static bool _warmupStarted;
        private static readonly object _warmupLock = new();

        /// <summary>
        /// 后台预热 Roslyn MefHostServices（线程安全，消除预热与主线程的竞态）。
        /// 预热结果存入静态缓存，SetParameters 中 RebuildWorkspace 可直接复用，
        /// 首次弹出不再阻塞 UI 线程。
        /// </summary>
        private static Task WarmupRoslynAsync()
        {
            return Task.Run(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                PluginLogger.Info("[补全] 后台预热 Roslyn 开始...", nameof(ScriptDebugControl));
                GetSharedMefHost();
                sw.Stop();
                PluginLogger.Info($"[补全] 后台预热 Roslyn 完成: {sw.ElapsedMilliseconds}ms", nameof(ScriptDebugControl));
            });
        }

        /// <summary>
        /// 后台预热 ScriptWorkspace（创建空项目骨架），
        /// SetParameters 中 RebuildWorkspace 直接复用，节省 ~200-500ms。
        /// </summary>
        private static Task WarmupWorkspaceAsync()
        {
            return Task.Run(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                PluginLogger.Info("[补全] 后台预热 Workspace 开始...", nameof(ScriptDebugControl));
                var host = GetSharedMefHost();
                var ws = new ScriptWorkspace(host);
                ws.TryApplyChanges(ws.CurrentSolution.AddProject(
                    ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(), "ScriptCompletion", "ScriptCompletion", LanguageNames.CSharp)));
                Interlocked.Exchange(ref _preWarmedWorkspace, ws);
                sw.Stop();
                PluginLogger.Info($"[补全] 后台预热 Workspace 完成: {sw.ElapsedMilliseconds}ms", nameof(ScriptDebugControl));
            });
        }

        public ObservableCollection<LogEntry> LogEntries => _logEntries;

        public string ScriptCode
        {
            get => _parameters.ScriptCode;
            set { _parameters.ScriptCode = value; OnPropertyChanged(nameof(ScriptCode)); }
        }

        public override bool ShowMaximizeButton => true;
        public override Size PreferredDebugWindowSize => new Size(1080, 650);

        public ScriptDebugControl()
        {
            var perfSw = System.Diagnostics.Stopwatch.StartNew();
            InitializeComponent();
            LogListBox.ItemsSource = _logEntries;
            UpdateStatus("初始化中...");

            // Phase 1: 语法高亮（自定义 xshd + Roslyn 语义高亮 + 实时错误标记）
            var typeName = GetType().Name;

            // Layer 1: 从输出目录加载自定义 xshd（含 BCL 类型关键词），失败则回退到内置 C# 高亮
            LoadCustomHighlighting();

            // Layer 2a: 实时诊断标记着色器（错误/警告在 span 下画波浪线，随主题，保留语法颜色）
            var errorMarker = new ErrorMarkerColorizer(() => _currentDiagnostics, ThemeResource.IsDarkTheme());
            CodeEditor.TextArea.TextView.BackgroundRenderers.Add(errorMarker);
            PluginLogger.Info("[Highlighting] 已加载实时诊断波浪线着色器", typeName);

            // Layer 2b: Roslyn 语义高亮着色器（IHighlighter 架构：按需逐行 + 防闪烁，对齐 RoslynPad）。
            //   AvalonEdit 只对可视行调 HighlightLine，命中行缓存直接复用（不闪烁）；未命中先用旧色平移再异步重算。
            //   类型=青/方法·属性=紫/字段=基础色，与 xshd 互补。生命周期由 RoslynHighlightingColorizer 自动管理。
            //   ★ 设计迁移（对齐参考项目时序）：不在构造函数挂载！构造函数里 workspace 尚未建（要等
            //   RebuildWorkspace 才建），此时挂载会导致首帧渲染 worker 拿到 document==null 丢弃可视行、
            //   缓存白行永不重分类（"打开白、编辑才变青"根因）。改为 RebuildWorkspace 完成后（UI线程）挂载，
            //   此时 workspace/文档已就绪，worker 首帧即可正确分类——与 RoslynPad 先 AddDocument 后挂 colorizer 一致。
            _semanticColorizer = new RoslynHighlightingColorizer(
                CodeEditor.Document,
                () => _workspace?.CurrentSolution.GetDocument(_userDocumentId),
                new ClassificationHighlightColors());

            // 当前行高亮：光标所在行浅蓝色背景，替代纯光标闪烁（提升代码定位体验）
            CodeEditor.Options.HighlightCurrentLine = true;
            CodeEditor.TextArea.TextView.CurrentLineBackground =
                new SolidColorBrush(Color.FromArgb(0x18, 0x00, 0x7A, 0xCC));
            if (CodeEditor.TextArea.TextView.CurrentLineBackground.CanFreeze)
                CodeEditor.TextArea.TextView.CurrentLineBackground.Freeze();
            PluginLogger.Info("[Highlighting] 已启用当前行高亮", typeName);

            // Phase 1: 实时诊断（文本变更 → 500ms 节流 → Roslyn 诊断 → 着色）
            CodeEditor.TextChanged += OnCodeEditorTextChanged;

            // Phase 2: 后台预热 Roslyn 补全（MEF + 程序集引用，窗口立即弹出，不阻塞 UI）
            // 幂等：应用启动已由 ScriptTool 静态构造触发 WarmupForStartup，此处仅兜底（独立打开工具场景）
            WarmupForStartup();

            // 自动补全事件
            CodeEditor.TextArea.TextEntered += TextArea_TextEntered;
            CodeEditor.TextArea.TextEntering += TextArea_TextEntering;

            // 诊断悬停详情：鼠标移到错误/警告 span 上显示消息（MouseMove 命中诊断 + MouseLeave 关闭）
            CodeEditor.TextArea.MouseMove += TextArea_MouseMove;
            CodeEditor.TextArea.MouseLeave += TextArea_MouseLeave;

            // 控件卸载时清理
            Unloaded += (_, _) =>
            {
                CodeEditor.TextArea.TextEntered -= TextArea_TextEntered;
                CodeEditor.TextArea.TextEntering -= TextArea_TextEntering;
                CodeEditor.TextArea.MouseMove -= TextArea_MouseMove;
                CodeEditor.TextArea.MouseLeave -= TextArea_MouseLeave;
                CodeEditor.TextChanged -= OnCodeEditorTextChanged;
                _userTextContainer?.Dispose();
                _userTextContainer = null;
                _workspace?.Dispose();
                _diagnosticsCts?.Cancel();
                _diagnosticsCts?.Dispose();
            };

            // 初始化变量配置面板（输入/输出 Tab）
            // 上游节点数据源在 SetDataProvider 中通过基类缓存桥接（避免此处硬编码 null）
            InitializeTabs();

            perfSw.Stop();
            PluginLogger.Info($"[Perf] ScriptDebugControl 构造函数: {perfSw.ElapsedMilliseconds}ms", typeName);
        }

        /// <summary>
        /// 从输出目录 Resources 子文件夹加载自定义 xshd 语法定义（含 BCL 类型关键词）。
        /// 文件不存在或加载失败时回退到内置 C# 高亮，不抛异常。
        /// </summary>
        private void LoadCustomHighlighting()
        {
            var typeName = GetType().Name;
            var assemblyDir = Path.GetDirectoryName(typeof(ScriptDebugControl).Assembly.Location);
            var xshdPath = Path.Combine(assemblyDir ?? AppContext.BaseDirectory, "Resources", "CSharpLight.xshd");

            if (!File.Exists(xshdPath))
            {
                PluginLogger.Warning($"[Highlighting] xshd 文件不存在: {xshdPath}，回退到内置 C# 高亮", typeName);
                CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
                return;
            }

            try
            {
                using var stream = File.OpenRead(xshdPath);
                using var reader = XmlReader.Create(stream);
                CodeEditor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                PluginLogger.Info($"[Highlighting] 已加载自定义 xshd: {xshdPath}, Name={CodeEditor.SyntaxHighlighting?.Name}", typeName);
            }
            catch (Exception ex)
            {
                PluginLogger.Warning($"[Highlighting] xshd 加载失败: {ex.Message}，回退到内置 C# 高亮", typeName);
                CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
            }
        }

        public override void SetParameters(ToolParameters parameters)
        {
            base.SetParameters(parameters);
            if (parameters is ScriptParameters scriptParams)
            {
                _parameters = scriptParams;
                _suppressDiagnostic = true;     // 抑制 CodeEditor.Text 触发器引发的诊断

                if (CodeEditor != null)
                {
                    CodeEditor.Text = _parameters.ScriptCode;
                }

                InitializeTabs();
                // ★ 补全就绪则立即重建 workspace；未就绪（启动瞬间极速打开工具）则先显示代码，
                //   注册就绪回调，预热完成后自动重建。绝不阻塞 UI 线程等 MEF 初始化锁（防竞态）。
                if (_completionReady)
                {
                    ScheduleRebuildForOpen();
                }
                else
                {
                    // 就绪前先显示代码（词法着色已由 xshd 生效，可编辑），状态栏提示就绪中
                    UpdateStatus("补全引擎就绪中...");
                    var capturedControl = this;
                    Interlocked.Exchange(ref _onCompletionReady, () =>
                    {
                        if (capturedControl.Dispatcher.CheckAccess())
                            capturedControl.ScheduleRebuildForOpen();
                        else
                            capturedControl.Dispatcher.BeginInvoke(capturedControl.ScheduleRebuildForOpen, DispatcherPriority.Background);
                    });
                }

                // ★ 参数注入完成 → 状态从"初始化中..."切到"就绪"
                //   此时编辑器代码已加载、Roslyn 预热已发起，对用户而言工具已就绪
                if (_completionReady)
                    UpdateStatus("就绪");
            }
        }

        /// <summary>
        /// 后台执行 workspace 重建 + 诊断/语义调度（补全就绪后调用）。
        /// 从 UI 线程 Dispatcher.Background 执行，重建不阻塞界面。
        /// </summary>
        private void ScheduleRebuildForOpen()
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                var perfSw = System.Diagnostics.Stopwatch.StartNew();
                RebuildWorkspace();
                PluginLogger.Info($"[Perf] RebuildWorkspace: {perfSw.ElapsedMilliseconds}ms", GetType().Name);
                _suppressDiagnostic = false;    // workspace 就绪，开放诊断

                // ★ 设计迁移（对齐参考项目时序）：workspace/文档已就绪，此刻才挂载语义高亮器。
                //   此时 worker 首帧渲染 _getUserDocument() 必非 null，可视行直接正确分类，
                //   根治"打开白、编辑才变青"（根因：构造函数挂载→首帧 document==null 丢弃可视行）。
                if (!_semanticColorizerMounted)
                {
                    CodeEditor.TextArea.TextView.LineTransformers.Add(_semanticColorizer);
                    _semanticColorizerMounted = true;
                    PluginLogger.Info("[Highlighting] 已挂载 Roslyn 语义高亮着色器（workspace 就绪后，对齐参考时序）", GetType().Name);
                }
                _semanticColorizer?.Warmup();   // 触发语义模型预热（TriggerWarmup 幂等）

                ScheduleDiagnosticAsync();
                ScheduleSemanticAsync();        // 首次打开：清缓存并重绘，可见行按需立即着色

                // 补全预热：后台跑一次 Roslyn GetCompletionsAsync 让 CompletionService 缓存命中，
                // 避免用户首次按键时 CompletionService 冷启动导致弹窗延迟。
                _ = WarmupCompletionAsync();
            }, DispatcherPriority.Background);
        }

        /// <summary>
        /// 设置数据查询服务（主工作流模式）— 桥接基类已填充的上游节点数据源到 InputTab。
        /// </summary>
        /// <remarks>
        /// 基类 SetDataProvider 内部已调用 PopulateParameterSources 从 DataSourceQueryService
        /// 拉取当前节点上游所有节点的输出，填充到基类 AvailableDataSources 集合。
        /// 此处将该集合（同一 ObservableCollection 实例）传给 InputTabControl，
        /// ParamBinding 通过 INotifyCollectionChanged 监听后续变化，无需重复桥接。
        /// 调试模式（未注入 queryService）下基类缓存保持空集合，ParamBinding 仅显示"无"/"全局变量"。
        /// </remarks>
        public override void SetDataProvider(object dataProvider)
        {
            base.SetDataProvider(dataProvider);

            // 将基类已填充的上游节点输出数据源桥接到输入 Tab
            InputTabControl.SetAvailableSources(AvailableDataSources);
        }

        // ===== 嵌入式变量管理面板 =====

        /// <summary>
        /// 初始化输入/输出变量配置 Tab
        /// </summary>
        private void InitializeTabs()
        {
            InputTabControl.Initialize(_parameters.Inputs, true);
            OutputTabControl.Initialize(_parameters.Outputs, false);
        }




        /// <summary>
        /// 退出编辑模式并重建 workspace（当用户确认时调用）
        /// </summary>
        public void ExitEditMode()
        {
            InputTabControl.ShowMonitorMode();
            OutputTabControl.ShowMonitorMode();
            RebuildWorkspace();
            ScheduleDiagnosticAsync();
            ScheduleSemanticAsync();
        }

        protected override void OnConfirmCommand(object sender, ExecutedRoutedEventArgs e)
        {
            SyncCodeToParameters();
            ExitEditMode();
            base.OnConfirmCommand(sender, e);
        }

        protected override object ExecuteTool()
        {
            SyncCodeToParameters();

            _logEntries.Clear();
            // 清除所有监视数据（输入 + 输出）
            ClearAllMonitorValues();

            // ★ 对齐普通工具（Blob/Threshold）调试模式：
            //    直接在 UI 层构建上游节点结果上下文和全局变量，
            //    然后直调 _scriptTool.Run()，不走 ToolRunner/ToolExecutor。
            _scriptTool ??= new ScriptTool();

            // 构建上游节点结果字典（从 DataProvider/DataSourceQueryService 查询）
            var nodeResults = BuildNodeResultsContext();
            if (nodeResults != null)
            {
                PluginLogger.Info($"[{nameof(ScriptDebugControl)}] 构建了 {nodeResults.Count} 个上游节点结果",
                    nameof(ScriptDebugControl));
            }

            var globalVars = GetDebugModeGlobalVariables();
            _parameters.NodeResults = nodeResults;
            _parameters.SetExecutionContext(new ScriptExecutionContext(
                logger: PluginLogger.Logger,
                toolName: "Script",
                nodeName: CurrentNode?.GetType().GetProperty("DispName")?.GetValue(CurrentNode) as string,
                globalVariables: globalVars));

            var result = Task.Run(() => _scriptTool.Run(_parameters)).GetAwaiter().GetResult();

            if (result == null)
            {
                UpdateStatus("失败");
                var errResult = new ScriptResults
                {
                    ToolName = "Script", ToolId = "Script", Timestamp = DateTime.Now,
                    ErrorMessage = "执行返回 null"
                };
                errResult.SetError("执行返回 null");
                return errResult;
            }

            // 收集编译错误到 UI 日志面板
            foreach (var error in result.CompileErrors)
                _logEntries.Add(new LogEntry { Text = error, IsError = true });

            // 收集运行日志
            foreach (var log in result.LogMessages)
                _logEntries.Add(new LogEntry { Text = log, IsError = false });

            // ★ 数据驱动：设置输出数据源，过滤掉未赋值的默认值
            OutputTabControl.ValueSource = FilterDefaultValues(result.OutputValues);

            // ★ 数据驱动：设置输入数据源
            InputTabControl.ValueSource = _parameters.ResolvedInputValues;

            CompileTimeLabel.Content = $"编译: {result.CompileTimeMs}ms";
            ExecTimeLabel.Content = $"执行: {result.ExecutionTimeMs}ms";
            UpdateStatus(result.IsSuccess ? "成功" : "失败");

            return result;
        }

        protected override bool CanExecuteTool()
        {
            return !string.IsNullOrWhiteSpace(CodeEditor?.Text);
        }

        /// <summary>
        /// 工作流引擎执行完成后通知。
        /// CurrentNode 是 UI 层的 WorkflowNode（工具层无引用 UI 程序集），
        /// 通过反射/属性检测（duck typing）获取 Parameters，通过 DSQS 查询 SSOT 获取输出结果。
        /// </summary>
        public override void OnExternalResultArrived()
        {
            // 输入：ScriptParameters.ResolvedInputValues 已被执行引擎原地更新，
            //       直接赋值新引用触发 DP 变更，无需 RefreshFromSource
            if (CurrentNode?.GetType().GetProperty("Parameters")?.GetValue(CurrentNode) is ScriptParameters sp)
                InputTabControl.ValueSource = sp.ResolvedInputValues;

            // 输出：通过 DSQS 查询 SSOT（WorkflowContext）获取最新结果
            var nodeId = GetNodeIdFromCurrentNode();
            if (!string.IsNullOrEmpty(nodeId) && DataProvider is IDataSourceQueryService queryService)
            {
                var context = queryService.GetNodeContext(nodeId);
                if (context?.Result is ScriptResults result)
                    OutputTabControl.ValueSource = FilterDefaultValues(result.OutputValues);
            }
        }



        /// <summary>
        /// 获取调试模式下的全局变量字典（反射访问 SunEyeVision.UI.Adapters.ServiceInitializer）
        /// </summary>
        /// <remarks>
        /// 工具层不能直接引用 SunEyeVision.UI 程序集（分层架构约束），
        /// 通过反射访问 SunEyeVision.UI.Adapters.ServiceInitializer.SolutionManager.CurrentSolution.GlobalVariables。
        /// 反射失败 → 静默返回 null，ScriptBindingResolver 会按规则报错。
        /// </remarks>
        private static IDictionary<string, object?>? GetDebugModeGlobalVariables()
        {
            try
            {
                var serviceInitializerType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("SunEyeVision.UI.Adapters.ServiceInitializer"))
                    .FirstOrDefault(t => t != null);
                if (serviceInitializerType == null) return null;

                var solutionManagerProp = serviceInitializerType.GetProperty(
                    "SolutionManager",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (solutionManagerProp == null) return null;

                var solutionManager = solutionManagerProp.GetValue(null);
                if (solutionManager == null) return null;

                var currentSolutionProp = solutionManager.GetType().GetProperty(
                    "CurrentSolution",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (currentSolutionProp == null) return null;

                var currentSolution = currentSolutionProp.GetValue(solutionManager) as SunEyeVision.Workflow.Solution;
                if (currentSolution == null) return null;

                var dict = new Dictionary<string, object?>();
                foreach (var v in currentSolution.GlobalVariables)
                {
                    dict[v.Name] = v.Value;
                }
                return dict;
            }
            catch (Exception ex)
            {
                PluginLogger.Error($"获取调试模式全局变量失败: {ex.Message}", nameof(ScriptDebugControl));
                return null;
            }
        }

        /// <summary>
        /// 从 DataProvider（DataSourceQueryService）构建上游节点结果上下文
        /// </summary>
        /// <remarks>
        /// 调试模式下，DataSourceQueryService 已注入 SSOT 读取器（委托到 WorkflowContext），
        /// 可实时读取上游节点执行结果。将结果构造成 IDictionary&lt;string, ToolResults&gt;
        /// 供 ScriptBindingResolver 解析节点属性绑定。
        /// 对齐普通工具（Blob/Threshold）在调试模式下手动从 _dataProvider 取值的做法。
        /// </remarks>
        /// <returns>节点 ID → ToolResults 字典（无上游节点时返回 null）</returns>
        private IDictionary<string, ToolResults>? BuildNodeResultsContext()
        {
            var nodeId = GetNodeIdFromCurrentNode();
            if (string.IsNullOrEmpty(nodeId))
                return null;

            if (DataProvider is not IDataSourceQueryService queryService)
                return null;

            var parentNodes = queryService.GetParentNodes(nodeId);
            if (parentNodes.Count == 0)
                return null;

            var dict = new Dictionary<string, ToolResults>();
            foreach (var parent in parentNodes)
            {
                var context = queryService.GetNodeContext(parent.NodeId);
                if (context?.Result != null)
                    dict[parent.NodeId] = context.Result;
            }

            return dict.Count > 0 ? dict : null;
        }

        /// <summary>
        /// 从 CurrentNode 反射提取节点 ID
        /// </summary>
        private string? GetNodeIdFromCurrentNode()
        {
            if (CurrentNode == null) return null;
            var idProperty = CurrentNode.GetType().GetProperty("Id");
            return idProperty?.GetValue(CurrentNode) as string;
        }

        private void UpdateStatus(string status)
        {
            StatusLabel.Content = status;
            StatusLabel.Foreground = status == "成功"
                ? System.Windows.Media.Brushes.LimeGreen
                : status == "失败"
                    ? System.Windows.Media.Brushes.Red
                    : System.Windows.Media.Brushes.Gray;
        }

        /// <summary>
        /// 过滤掉值为类型默认值的条目（未赋值的输出字段显示"无结果"）
        /// </summary>
        private static Dictionary<string, object?> FilterDefaultValues(Dictionary<string, object?> raw)
        {
            if (raw == null || raw.Count == 0)
                return raw;

            var filtered = new Dictionary<string, object?>(raw.Count);
            foreach (var kvp in raw)
            {
                if (kvp.Value == null)
                    continue;  // 引用类型默认值 null

                var type = kvp.Value.GetType();
                if (type.IsValueType && Equals(kvp.Value, Activator.CreateInstance(type)))
                    continue;  // 值类型默认值 0 / false / 0.0 等

                filtered[kvp.Key] = kvp.Value;
            }
            return filtered;
        }

        private void ClearResultButton_Click(object sender, RoutedEventArgs e)
        {
            _logEntries.Clear();
            ClearAllMonitorValues();
            CompileTimeLabel.Content = ""; ExecTimeLabel.Content = "";
            UpdateStatus("就绪");
        }

        /// <summary>
        /// 清除输入和输出 Tab 的所有监视值（含子节点）
        /// </summary>
        private void ClearAllMonitorValues()
        {
            InputTabControl.ClearAllValues();
            OutputTabControl.ClearAllValues();
        }

        private void CodeEditor_LostFocus(object sender, RoutedEventArgs e) => SyncCodeToParameters();

        /// <summary>
        /// 统一输出面板的双击处理：跳转到编译错误对应行号。
        /// 错误行号已来自用户文档语法树，直接对应编辑器行号，无需偏移。
        /// </summary>
        private void LogListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LogListBox.SelectedItem is LogEntry entry && entry.IsError && CodeEditor != null)
            {
                var match = Regex.Match(entry.Text, @"行 (\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var lineNumber))
                {
                    if (lineNumber < 1) lineNumber = 1;
                    CodeEditor.TextArea.Caret.Line = lineNumber;
                    CodeEditor.TextArea.Caret.Column = 1;
                    CodeEditor.ScrollToLine(lineNumber);
                    CodeEditor.TextArea.Caret.BringCaretToView();
                    CodeEditor.Focus();
                }
            }
        }

        /// <summary>
        /// 输出面板快捷键：Ctrl+A 全选、Ctrl+C 复制选中条目到剪贴板
        /// </summary>
        private void LogListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                LogListBox.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                CopySelectedLogEntries();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 右键菜单「复制选中」
        /// </summary>
        private void CopyLogMenuItem_Click(object sender, RoutedEventArgs e)
            => CopySelectedLogEntries();

        /// <summary>
        /// 右键菜单「全选」
        /// </summary>
        private void SelectAllLogMenuItem_Click(object sender, RoutedEventArgs e)
            => LogListBox.SelectAll();

        /// <summary>
        /// 将当前选中的日志条目文本复制到剪贴板（多条用 Environment.NewLine 连接）。
        /// 剪贴板可能被其他进程占用，重试若干次；重试耗尽必须明确告知用户（禁止静默失败）。
        /// </summary>
        private void CopySelectedLogEntries()
        {
            var selected = LogListBox.SelectedItems
                .OfType<LogEntry>()
                .Select(entry => entry.Text)
                .ToArray();

            if (selected.Length == 0)
            {
                PluginLogger.Warning("复制日志：未选中任何条目", nameof(ScriptDebugControl));
                return;
            }

            var text = string.Join(Environment.NewLine, selected);
            PluginLogger.Info($"复制日志：选中 {selected.Length} 条，文本长度 {text.Length} 字符", nameof(ScriptDebugControl));

            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    Clipboard.SetText(text);
                    PluginLogger.Success($"复制日志成功（第 {attempt + 1} 次尝试）", nameof(ScriptDebugControl));
                    return;
                }
                catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x800401D0))
                {
                    PluginLogger.Warning($"剪贴板被占用，重试 {attempt + 1}/10：{ex.Message}", nameof(ScriptDebugControl));
                    Thread.Sleep(50);
                }
            }

            // 重试耗尽：禁止静默返回，必须告知用户（rule-008 / rule-015）
            PluginLogger.Error("复制日志失败：剪贴板被占用，10 次重试均失败", nameof(ScriptDebugControl));
            MessageBox.Show(
                "剪贴板被其他程序占用，复制失败（已重试 10 次）。\n请关闭剪贴板管理工具（如 Win+V 历史记录、输入法、截图软件）后重试。",
                "复制失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// 引用程序集按钮点击 — 弹出对话框管理引用目录下的 DLL
        /// </summary>
        private void AssemblyRefButton_Click(object sender, RoutedEventArgs e)
        {
            var refDir = _parameters.AssemblyReferenceFullPath;
            Directory.CreateDirectory(refDir);

            var oldFiles = new HashSet<string>(Directory.EnumerateFiles(refDir, "*.dll"), StringComparer.OrdinalIgnoreCase);

            var dialog = new AssemblyReferenceDialog(refDir)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                var newFiles = new HashSet<string>(Directory.EnumerateFiles(refDir, "*.dll"), StringComparer.OrdinalIgnoreCase);
                if (!oldFiles.SetEquals(newFiles))
                {
                    SharedMetadataReferenceProvider.InvalidateScriptRefs(refDir);  // 失效引用缓存，强制重载新增 DLL
                    RebuildWorkspace(refreshReferences: true);
                }
            }
        }

        /// <summary>
        /// Demo示例按钮 — 将编辑器内容替换为 DemoCode（综合示例代码，方便学习测试）
        /// </summary>
        private void DemoCodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (CodeEditor == null) return;
            CodeEditor.Text = ScriptParameters.DemoCode;
            SyncCodeToParameters();
            RebuildWorkspace();
            ScheduleDiagnosticAsync();
            ScheduleSemanticAsync();
            UpdateStatus("已加载 Demo 示例");
        }

        /// <summary>
        /// 复位最简按钮 — 将编辑器内容替换为 MinimalCode（代码误删或想从头开始）
        /// </summary>
        private void ResetCodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (CodeEditor == null) return;
            CodeEditor.Text = ScriptParameters.MinimalCode;
            SyncCodeToParameters();
            RebuildWorkspace();
            ScheduleDiagnosticAsync();
            ScheduleSemanticAsync();
            UpdateStatus("已复位至最简代码");
        }





        private void SyncCodeToParameters()
        {
            if (CodeEditor != null)
                _parameters.ScriptCode = CodeEditor.Text;
        }

        // =======================================================
        // Phase 2: Roslyn 语义自动补全（50ms 节流 + 单层数据源 + 零闪烁）
        // =======================================================

        /// <summary>
        /// 重建/复用工作区（构造函数/SetParameters/用户增删 DLL/修改变量后调用）。
        /// ★ 实例级复用（对齐 RoslynPad 单文档语义）：workspace 首次建好后（含引用/项目/文档骨架），
        ///   后续 SetParameters / 文本变化不再 Dispose 重建，只更新文档文本；不同工具实例各持各的 workspace，零干扰。
        /// </summary>
        /// <param name="refreshReferences">用户增删 DLL 时传 true，强制刷新引用并完整重建；默认 false 走复用路径。</param>
        private void RebuildWorkspace(bool refreshReferences = false)
        {
            // 实例级复用：workspace 已建且非强制刷新引用 → 只更新 generated 文档文本（user 文档由增量容器自动同步）
            if (_workspaceInitialized && !refreshReferences)
            {
                var genCode = _parameters.GeneratedCode;
                _workspace!.SetCurrentSolution(_workspace.CurrentSolution
                    .WithDocumentText(_documentId!, SourceText.From(genCode)));
                _currentItems = [];   // 清空补全缓存
                return;
            }

            var generatedCode = _parameters.GeneratedCode;
            var userCode = _parameters.ScriptCode;
            var typeName = GetType().Name;
            var totalSw = System.Diagnostics.Stopwatch.StartNew();

            PluginLogger.Info($"[补全] RebuildWorkspace 开始: generatedCodeLen={generatedCode.Length}, userCodeLen={userCode.Length}", typeName);

            _diagnosticsCts?.Cancel();  // 取消正在进行的语义分析，防止访问已 Dispose 的 workspace
            _workspace?.Dispose();

            // 加载 MefHostServices 确保所有 CompletionProvider 可用（缓存机制，首次慢后续快）
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var host = GetSharedMefHost();
            PluginLogger.Info($"[补全] MefHostServices 创建完成: {sw.ElapsedMilliseconds}ms", typeName);

            Project project;
            if (_preWarmedWorkspace != null)
            {
                _workspace = _preWarmedWorkspace;
                _preWarmedWorkspace = null;
                PluginLogger.Info("[补全] 复用预热 Workspace", typeName);
            }
            else
            {
                _workspace = new ScriptWorkspace(host);
                PluginLogger.Info("[补全] ScriptWorkspace 实例已创建", typeName);
            }

            sw.Restart();
            var existingProject = _workspace.CurrentSolution.Projects.FirstOrDefault();
            if (existingProject != null)
            {
                project = existingProject.WithCompilationOptions(
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                        .WithAllowUnsafe(true));
                PluginLogger.Info($"[补全] 复用预热项目: {sw.ElapsedMilliseconds}ms", typeName);
            }
            else
            {
                var projectInfo = ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(), "ScriptCompletion", "ScriptCompletion", LanguageNames.CSharp);
                var newSolution = _workspace.CurrentSolution.AddProject(projectInfo);
                project = newSolution.GetProject(projectInfo.Id)!.WithCompilationOptions(
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                        .WithAllowUnsafe(true));
                PluginLogger.Info($"[补全] 项目创建完成: {sw.ElapsedMilliseconds}ms", typeName);
            }

            // 扫描引用：MetadataReferenceProvider 统一服务（ALC 扫描 + 过滤）→ ScriptRefs 用户自定义 DLL
            sw.Restart();
            var refDir = _parameters?.AssemblyReferenceFullPath;
            var refCount = 0;

            // 0. 宿主已加载程序集（运行时 ALC / TPA）：Script/IVisionImage/LogInfo 等宿主 API 元数据，
            //    与 ScriptCompiler 共享统一服务，保证 IntelliSense 与编译引用一致。
            var hostRefs = MetadataReferenceProvider.GetHostReferences();
            var seenRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mdRef in hostRefs)
            {
                if (mdRef is PortableExecutableReference per && per.FilePath != null)
                {
                    string key;
                    try { key = System.IO.Path.GetFullPath(per.FilePath); }
                    catch { key = per.FilePath; }
                    if (!seenRefs.Add(key)) continue;
                }
                project = project.AddMetadataReference(mdRef);
                refCount++;
            }
            PluginLogger.Info($"[补全] 宿主注入: {refCount} 个 (ALC/TPA 引用={hostRefs.Count})", typeName);

            // 1. ScriptRefs 目录（用户自定义第三方 DLL），通过共享缓存加载
            if (!string.IsNullOrEmpty(refDir))
            {
                var sharedRefs = SharedMetadataReferenceProvider.GetScriptRefs(refDir);
                foreach (var mdRef in sharedRefs)
                {
                    if (mdRef is PortableExecutableReference per && per.FilePath != null)
                    {
                        string key;
                        try { key = System.IO.Path.GetFullPath(per.FilePath); }
                        catch { key = per.FilePath; }
                        if (!seenRefs.Add(key)) continue;
                    }
                    project = project.AddMetadataReference(mdRef);
                    refCount++;
                }
                PluginLogger.Info($"[补全] ScriptRefs 已添加 {sharedRefs.Count} 个引用 ({sw.ElapsedMilliseconds}ms)", typeName);
            }
            else
            {
                PluginLogger.Warning("[补全] AssemblyReferenceFullPath 为空，未加载任何引用", typeName);
            }
            PluginLogger.Info($"[补全] 已添加 {refCount} 个 MetadataReference: {sw.ElapsedMilliseconds}ms", typeName);

            sw.Restart();
            // 用 SetCurrentSolution 直接替换（对齐 RoslynPad SetSubmissionDocument），
            // 接受 AddProject + AddMetadataReference 全部变化，避免 TryApplyChanges 钩子链异常。
            _workspace.SetCurrentSolution(project.Solution);
            PluginLogger.Info($"[补全] SetCurrentSolution(项目+引用): {sw.ElapsedMilliseconds}ms", typeName);

            sw.Restart();

            // ★ 添加两个文档：字段声明 partial class + 用户完整 partial class。
            //    Workspace 基类无 AddDocument 便捷方法，用 Solution.AddDocument(DocumentInfo) + TryApplyChanges 推进。
            var genDocId = DocumentId.CreateNewId(project.Id);
            var userDocId = DocumentId.CreateNewId(project.Id);

            var solution = project.Solution
                .AddDocument(DocumentInfo.Create(genDocId, "ScriptGenerated.cs", folders: null,
                    sourceCodeKind: SourceCodeKind.Regular))
                .AddDocument(DocumentInfo.Create(userDocId, "ScriptUser.cs", folders: null,
                    sourceCodeKind: SourceCodeKind.Regular));

            // 先以当前 generatedCode 文本同步 generated 文档（后续由 RunDiagnosticAsync 用 WithDocumentText 更新）
            solution = solution.WithDocumentText(genDocId, SourceText.From(generatedCode));

            // 用 SetCurrentSolution 直接替换（对齐 RoslynPad SetSubmissionDocument）：
            // TryApplyChanges 对 AddDocument 走 OnDocumentAdded 钩子链，在 Workspace 基类下会抛异常
            // 且被上层静默吞掉，导致 _userDocumentId 永不赋值、补全跳过。
            // SetCurrentSolution 无脑替换整个 solution，接受 AddDocument+ChangeDocument 全部变化。
            _workspace.SetCurrentSolution(solution);
            _documentId = genDocId;

            // 用户文档绑定 SourceTextContainer 增量通道：容器每次编辑事件携带局部 TextChangeRange，
            // 经 ScriptWorkspace.OpenDocument 注册，Roslyn 只重解析受影响区域（对齐 RoslynPad workspace.OpenDocument 模式）。
            _userTextContainer?.Dispose();                          // 重建前释放旧容器事件订阅
            _userTextContainer = new AvalonEditTextContainer(CodeEditor.Document);
            _workspace.OpenDocument(userDocId, _userTextContainer);  // ★ 增量通道注册
            _userDocumentId = userDocId;

            PluginLogger.Info($"[补全] 文档添加完成: genDocId={_documentId}, userDocId={_userDocumentId}: {sw.ElapsedMilliseconds}ms", typeName);

            _currentItems = [];   // 清空缓存，下次输入触发全新解析

            _workspaceInitialized = true;   // 引用/项目/文档骨架已就绪，后续 SetParameters 走实例级复用路径

            var refs = _workspace.CurrentSolution.Projects.First().MetadataReferences;
            PluginLogger.Info($"[补全] Workspace 重建完成: {refs.Count} 个引用, 总耗时={totalSw.ElapsedMilliseconds}ms", typeName);
        }

        /// <summary>
        /// 统一入口：字母 / 小数点 → 取消旧查询 → 单次 Roslyn 解析 → 单层弹窗
        /// 窗口打开期间：AvalonEdit CompletionList 接管客户端过滤（内置质量评分算法）
        /// </summary>
        private async void TextArea_TextEntered(object? sender, TextCompositionEventArgs e)
        {
            var typeName = GetType().Name;

            if (_workspace == null || _userDocumentId == null)
            {
                PluginLogger.Warning($"[补全] TextEntered 跳过: workspace={_workspace != null}, userDocumentId={_userDocumentId != null}, text='{e.Text}'", typeName);
                return;
            }

            bool isTriggerChar = e.Text == "." ||
                (!string.IsNullOrEmpty(e.Text) && (char.IsLetterOrDigit(e.Text[0]) || e.Text[0] == '_'));
            // 签名帮助：输入 '(' / ',' / ')' 都触发 Roslyn 内置 SignatureHelpService 查询。
            // 对齐 RoslynPad：定位/嵌套括号/参数索引/重载选择全由 Roslyn 内部 ISignatureHelpProvider 完成，
            // 查询返回 null（调用已闭合 / 非方法调用）时自动关闭浮窗。
            if (e.Text is "(" or "," or ")")
            {
                PluginLogger.Info($"[签名帮助] '{e.Text}' 触发 Roslyn 查询, caretOffset=" + CodeEditor.TextArea.Caret.Offset, typeName);
                _throttleCts?.Cancel();
                _throttleCts?.Dispose();
                _throttleCts = new CancellationTokenSource();
                var sigToken = _throttleCts.Token;
                await ShowSignatureHelpAsync(CodeEditor.TextArea.Caret.Offset, e.Text[0], sigToken);
                return;
            }

            if (!isTriggerChar)
            {
                return;
            }

            // 新查询取消旧查询（对齐 RoslynPad：TextEntered 直接查询，无节流滞后感）
            _throttleCts?.Cancel();
            _throttleCts?.Dispose();
            _throttleCts = new CancellationTokenSource();
            var token = _throttleCts.Token;

            try
            {
                bool isDot = e.Text == ".";

                if (isDot)
                {
                    await ThrottledRefreshAndShow('.', token);
                    return;
                }

                // 字母数字：窗口已开 → 沉默，AvalonEdit CompletionList 自动过滤（内置质量评分）
                // 无窗口 → 首次 Roslyn 查询
                bool windowActive = _completionWindow != null && _currentItems.Length > 0;
                if (!windowActive)
                {
                    await ThrottledRefreshAndShow(e.Text[0], token);
                }
            }
            catch (TaskCanceledException)
            {
                // 节流取消，忽略
            }
        }

        /// <summary>
        /// 文本输入中处理补全窗口的插入确认
        /// '.' 特殊处理：
        ///   - 前缀完全匹配（如 "string" 已打完）→ 关闭窗口，让 '.' 通过触发成员补全
        ///   - 前缀部分匹配（如 "str"）→ RequestInsertion 补全为 "string"，手动插入 '.'，
        ///     然后触发成员补全查询，避免 RequestInsertion 不设 e.Handled 导致事件流紊乱
        /// 其他非字母数字 → RequestInsertion（提交选中项，字符通过）
        /// 字母 → 放行，AvalonEdit CompletionList 自动过滤
        /// </summary>
        private void TextArea_TextEntering(object? sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && _completionWindow != null)
            {
                if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_')
                {
                    if (e.Text[0] == '.')
                    {
                        var prefix = GetPrefixFromCaret();
                        var selectedItem = _completionWindow.CompletionList.ListBox?.SelectedItem;
                        if (selectedItem is ICompletionData completionData &&
                            string.Equals(completionData.Text, prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            // 完全匹配：关闭窗口，'.' 通过 → TextEntered 触发成员补全
                            _completionWindow.Close();
                            _completionWindow = null;
                            return;
                        }

                        // 部分匹配：RequestInsertion 补全文本 → 手动插入 '.' → 触发成员补全
                        _completionWindow.CompletionList.RequestInsertion(e);
                        _completionWindow = null;

                        // RequestInsertion 不会设 e.Handled，需要自行吞掉 '.' 防止重复插入
                        e.Handled = true;

                        // 在补全后的文本后插入 '.'
                        var offset = CodeEditor.TextArea.Caret.Offset;
                        CodeEditor.Document.Insert(offset, ".");
                        CodeEditor.TextArea.Caret.Offset = offset + 1;

                        // 触发成员补全查询
                        _ = TriggerMemberCompletionAfterDot();
                        return;
                    }

                    _completionWindow.CompletionList.RequestInsertion(e);
                }
            }
        }

        /// <summary>
        /// 在 '.' 手动插入后，直接触发成员补全查询。
        /// 用户文档已绑定 SourceTextContainer 增量通道，workspace 文本随编辑器实时同步。
        /// </summary>
        private async Task TriggerMemberCompletionAfterDot()
        {
            // '.' 是独立触发动作（非连续打字路径），无竞态需取消，用 CancellationToken.None 免分配 CTS
            await ThrottledRefreshAndShow('.', CancellationToken.None);
        }

        /// <summary>
        /// 单次 Roslyn 解析 + 弹窗/更新窗口（仅从 ThrottledRefreshAndShow 调用）
        /// 窗口打开期间永远不会再次执行此方法
        /// </summary>
        private async Task ThrottledRefreshAndShow(char triggerChar, CancellationToken token)
        {
            var items = await FetchFreshCompletions(triggerChar, token);
            if (token.IsCancellationRequested)
            {
                return;
            }
            if (items.IsDefault || items.Length == 0)
            {
                return;
            }

            // ★ 唯一数据源写入点：只有这里会修改 _currentItems ★
            _currentItems = items.ToArray();

            ShowCompletionWindow(items);
        }

        /// <summary>
        /// 用 Roslyn 结果渲染/更新补全窗口（单次渲染，不重复创建）
        /// 数据直接注入 CompletionList，AvalonEdit 接管后续客户端过滤（内置质量评分）
        /// </summary>
        private void ShowCompletionWindow(ImmutableArray<CompletionItem> items)
        {
            var caretOffset = CodeEditor.TextArea.Caret.Offset;
            var prefix = GetPrefixFromCaret();

            if (_completionWindow != null)
            {
                // 已有窗口 → 更新数据源（Roslyn 刷新场景）
                PopulateData(_completionWindow.CompletionList.CompletionData, items);
                return;
            }

            var startOffset = caretOffset - prefix.Length;
            var endOffset = FindEndOfWord(caretOffset);

            // 创建新窗口，IsFiltering=true 为默认值，AvalonEdit 自动做智能过滤
            // 主题化：CompletionWindow 是 Popup，不走可视化树 DynamicResource，需手动取主题刷子（对齐 RoslynPad 设 CompletionBackground）
            var window = new CompletionWindow(CodeEditor.TextArea)
            {
                StartOffset = startOffset,
                EndOffset = endOffset,
                Background = ThemeResource.GetBrush(SdkThemeKeys.PanelBackgroundBrush),
                Foreground = ThemeResource.GetBrush(SdkThemeKeys.TextPrimaryBrush),
            };
            // 列表项背景/选中高亮也跟主题（ListBox 模板内资源）
            window.CompletionList.Background = ThemeResource.GetBrush(SdkThemeKeys.PanelBackgroundBrush);
            window.CompletionList.Foreground = ThemeResource.GetBrush(SdkThemeKeys.TextPrimaryBrush);
            window.CompletionList.ListBox.Background = ThemeResource.GetBrush(SdkThemeKeys.PanelBackgroundBrush);
            window.CompletionList.ListBox.Foreground = ThemeResource.GetBrush(SdkThemeKeys.TextPrimaryBrush);
            PopulateData(window.CompletionList.CompletionData, items);

            window.Closed += (_, _) =>
            {
                
                _completionWindow = null;
                _currentItems = [];    // 窗口关闭 → 数据作废，下次重新解析
            };

            window.Show();

            _completionWindow = window;
        }

        /// <summary>
        /// 将 Roslyn CompletionItem[] 直接注入 CompletionList（AvalonEdit 接管客户端过滤）
        /// </summary>
        private void PopulateData(IList<ICompletionData> data,
            ImmutableArray<CompletionItem> items)
        {
            // 取当前 workspace 文档，供 RoslynCompletionData 延迟加载描述（GetDescriptionAsync）与图标推断
            var doc = _workspace.CurrentSolution.GetDocument(_userDocumentId);
            data.Clear();
            foreach (var item in items)
                data.Add(new RoslynCompletionData(item, doc));
        }

        /// <summary>
        /// 从光标位置向左提取标识符前缀（字母数字+下划线）
        /// </summary>
        private string GetPrefixFromCaret()
        {
            var code = CodeEditor.Text;
            var caretOffset = CodeEditor.TextArea.Caret.Offset;
            int startOffset = caretOffset;
            while (startOffset > 0 && (char.IsLetterOrDigit(code[startOffset - 1]) || code[startOffset - 1] == '_'))
                startOffset--;
            return code.Substring(startOffset, caretOffset - startOffset);
        }

        /// <summary>
        /// 从光标位置向右扫描到标识符单词末尾（用于设置 CompletionWindow.EndOffset）
        /// </summary>
        private int FindEndOfWord(int fromOffset)
        {
            var code = CodeEditor.Text;
            int offset = fromOffset;
            while (offset < code.Length &&
                   (char.IsLetterOrDigit(code[offset]) || code[offset] == '_'))
                offset++;
            return Math.Max(offset, fromOffset);
        }




        /// <summary>
        /// 从 Roslyn 获取最新补全结果的原始 CompletionItem 列表
        /// 用户文档已绑定 SourceTextContainer 增量通道，workspace 取实时文本（无需 WithText 重建）
        /// 使用 _userDocumentId 保证补全在用户代码空间内
        /// </summary>
        private async Task<ImmutableArray<CompletionItem>> FetchFreshCompletions(
            char triggerChar, CancellationToken token)
        {
            var typeName = GetType().Name;

            if (_workspace == null || _userDocumentId == null)
            {
                PluginLogger.Warning($"[补全] FetchFreshCompletions 跳过: workspace={_workspace != null}, userDocumentId={_userDocumentId != null}", typeName);
                return default;
            }

            var code = CodeEditor.Text;
            var caretOffset = CodeEditor.TextArea.Caret.Offset;

            if (string.IsNullOrEmpty(code))
            {
                PluginLogger.Warning("[补全] FetchFreshCompletions 跳过: 代码为空", typeName);
                return default;
            }
            if (caretOffset <= 0)
            {
                PluginLogger.Warning($"[补全] FetchFreshCompletions 跳过: caretOffset={caretOffset}", typeName);
                return default;
            }

            try
            {
                // 从 workspace 获取用户文档。文档已绑定 SourceTextContainer 增量通道，
                // 文本与编辑器实时增量同步，无需 WithText 全量重建（消除连续打字卡顿）。
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var doc = _workspace.CurrentSolution.GetDocument(_userDocumentId);
                if (doc == null)
                {
                    PluginLogger.Warning($"[补全] workspace 中找不到 user document: {_userDocumentId}", typeName);
                    return default;
                }

                var completionService = CompletionService.GetService(doc);
                if (completionService == null)
                {
                    PluginLogger.Warning($"[补全] CompletionService 不可用, caretOffset={caretOffset}", typeName);
                    return default;
                }

                // ★ 传入精确的 CompletionTrigger（激活成员补全 Provider）
                var trigger = triggerChar != '\0'
                    ? CompletionTrigger.CreateInsertionTrigger(triggerChar)
                    : CompletionTrigger.Default;

                // 对齐 RoslynPad：GetCompletionsAsync 3 参调用；竞态由下方 token.IsCancellationRequested 丢弃过期结果守护
                var results = await completionService.GetCompletionsAsync(doc, caretOffset, trigger);

                if (token.IsCancellationRequested)
                {
                    return default;
                }
                if (results == null)
                {
                    PluginLogger.Warning($"[补全] GetCompletionsAsync 返回 null: {sw.ElapsedMilliseconds}ms", typeName);
                    return default;
                }

                return results.ItemsList.ToImmutableArray();
            }
            catch (OperationCanceledException)
            {
                PluginLogger.Info("[补全] 查询被取消（新输入接管）", typeName);
                return default;
            }
            catch (Exception ex)
            {
                PluginLogger.Warning($"[补全] FetchCompletions 异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", typeName);
                return default;
            }
        }

        /// <summary>
        /// 补全预热：后台对 user 文档跑一次 GetCompletionsAsync（光标在文档末尾，trigger='a'），
        /// 让 CompletionService 缓存命中。避免用户首次按键时 CompletionService 冷启动导致弹窗延迟。
        /// </summary>
        private async Task WarmupCompletionAsync()
        {
            try
            {
                if (_workspace == null || _userDocumentId == null) return;
                var doc = _workspace.CurrentSolution.GetDocument(_userDocumentId);
                if (doc == null) return;

                var caretOffset = CodeEditor.Text.Length;   // 文档末尾，触发完整 symbol 查询
                var completionService = CompletionService.GetService(doc);
                if (completionService == null) return;

                var trigger = CompletionTrigger.CreateInsertionTrigger('a');
                // 后台预热：不显示窗口、不写 _currentItems，只为命中 CompletionService 缓存
                await completionService.GetCompletionsAsync(doc, caretOffset, trigger)
                    .ConfigureAwait(false);
            }
            catch { /* 预热失败不阻塞主流程 */ }
        }

        // =======================================================
        // Phase 0: 签名帮助（语法树定位 InvocationExpression → OverloadInsightWindow 参数提示）
        // =======================================================

        /// <summary>
        /// 输入 '(' / ',' / ')' 后，用 Roslyn 内置签名帮助服务（SignatureHelpService）定位光标处方法调用并显示参数提示。
        /// 对齐 RoslynPad 最佳实践：签名帮助的定位（ApplicableSpan）、嵌套括号、参数索引、重载选择全部由 Roslyn 内部
        /// ISignatureHelpProvider 聚合完成，语法树与语义模型在 provider 内部保持一致（根治手写拆分导致版本错位、
        /// GetSymbolInfo 返回 null 的问题）。Roslyn 4.12 的 SignatureHelpService 是 internal，靠 IgnoresAccessChecksTo
        /// 编译期包访问（PrivateAssets=All，不污染插件输出目录）。
        /// 触发频率低（仅括号字符）+ 复用浮窗 + 取消竞态，无性能问题。
        /// </summary>
        private async Task ShowSignatureHelpAsync(int position, char triggerChar, CancellationToken token)
        {
            var typeName = GetType().Name;
            if (_workspace == null || _userDocumentId == null) return;

            var doc = _workspace.CurrentSolution.GetDocument(_userDocumentId);
            if (doc == null) return;

            // 触发原因：'(' 首次触发 → TypeCharCommand；',' / ')' 为补全中途续触发 → RetriggerCommand。
            var reason = triggerChar == '('
                ? SignatureHelpTriggerReason.TypeCharCommand
                : SignatureHelpTriggerReason.RetriggerCommand;
            var triggerInfo = new SignatureHelpTriggerInfo(reason, triggerChar);

            SignatureHelpItems? items = null;
            try
            {
                // 手写聚合全部 C# ISignatureHelpProvider（复制 RoslynPad AggregateSignatureHelpProvider.GetItemsAsync：
                // 取 ApplicableSpan 覆盖光标且最靠近光标的那组），不用 SignatureHelpService（其 API 在 4.12 不稳定）。
                var providers = GetSignatureHelpProviders();
                Microsoft.CodeAnalysis.SignatureHelp.SignatureHelpItems? best = null;
                foreach (var provider in providers)
                {
                    token.ThrowIfCancellationRequested();
                    // 对齐 RoslynPad：查询在后台线程执行（ConfigureAwait(false)），不卡 UI；
                    // 后续创建 OverloadInsightWindow 等 UI 操作单独用 Dispatcher 切回 UI 线程。
                    var current = await provider.GetItemsAsync(doc, position, triggerInfo, default, token).ConfigureAwait(false);
                    if (current != null && current.ApplicableSpan.IntersectsWith(position))
                    {
                        if (best == null || current.ApplicableSpan.Start > best.ApplicableSpan.Start)
                            best = current;
                    }
                }
                items = best;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                PluginLogger.Warning($"[签名帮助] Roslyn 查询异常: {ex.GetType().Name}: {ex.Message}", typeName);
                return;
            }

            if (token.IsCancellationRequested) return;

            // 对齐 RoslynPad（CodeTextEditor.ShowCompletion 用 ConfigureAwait(true)）：查询在后台线程
            // 完成（上面 ConfigureAwait(false)），所有 UI 操作（创建/更新/显示 OverloadInsightWindow）
            // 必须切回 UI 线程（WPF 控件需 STA），否则非 STA 线程创建控件抛 InvalidOperationException。
            await CodeEditor.Dispatcher.InvokeAsync(() =>
            {
                // 返回 null：光标不在任何可签名帮助的方法调用内（如调用已闭合、参数内部无调用）。
                // 此时关闭已打开的浮窗（对齐 RoslynPad：overloadProvider == null 时不显示签名帮助）。
                if (items == null || items.Items.Count == 0)
                {
                    PluginLogger.Info($"[签名帮助] 无适用方法调用，关闭浮窗: triggerChar='{triggerChar}'", typeName);
                    _insightWindow?.Close();
                    return;
                }

                PluginLogger.Info($"[签名帮助] 返回 {items.Items.Count} 个重载, SemanticParameterIndex={items.SemanticParameterIndex}", typeName);

                // 复用浮窗实例（对齐 RoslynPad _insightWindow 复用）
                if (_insightWindow == null)
                {
                    _insightWindow = new OverloadInsightWindow(CodeEditor.TextArea);
                    _insightWindow.Closed += (_, _) => { _insightWindow = null; };
                }
                // 每次显示前刷新主题色（Popup 不走可视化树 DynamicResource，切主题后复用实例必须重取）
                _insightWindow.Background = ThemeResource.GetBrush(SdkThemeKeys.PanelBackgroundBrush);
                _insightWindow.Foreground = ThemeResource.GetBrush(SdkThemeKeys.TextPrimaryBrush);
                if (_insightWindow.Provider is ScriptOverloadProvider existing)
                {
                    existing.Update(items);
                }
                else
                {
                    _insightWindow.Provider = new ScriptOverloadProvider(items);
                }
                _insightWindow.Show();
            });
        }

        /// <summary>线程安全地获取全部 C# ISignatureHelpProvider（MEF 组合，进程内一次构建）。</summary>
        private static ImmutableArray<ISignatureHelpProvider> _signatureHelpProviders;
        private static readonly object _signatureHelpLock = new();

        private static ImmutableArray<ISignatureHelpProvider> GetSignatureHelpProviders()
        {
            if (!_signatureHelpProviders.IsDefault) return _signatureHelpProviders;
            lock (_signatureHelpLock)
            {
                if (!_signatureHelpProviders.IsDefault) return _signatureHelpProviders;

                var sw = System.Diagnostics.Stopwatch.StartNew();
                // 从 Roslyn 默认 MEF 程序集构建组合容器，枚举所有 [Export(ISignatureHelpProvider)] 的 C# 提供器
                //（对齐 RoslynPad RoslynHost 的 MEF composition）。
                // 关键：排除 Pythia 相关 part。Roslyn 4.12 的 PythiaSignatureHelpProvider 是 IDE/AI 场景的壳，
                // 它 [Import] IPythiaSignatureHelpProviderImplementation（由 VS Pythia 集成层导出，独立工具没有）。
                // 若不过滤，MEF 激活该 provider 时抛 CompositionFailedException，导致整个枚举失败（实测 19:22 日志）。
                // 过滤后 C# 标准签名帮助 provider 完全不受影响（它们不依赖 Pythia）。
                var partTypes = MefHostServices.DefaultAssemblies
                    .SelectMany(a => a.DefinedTypes)
                    .Select(t => t.AsType())
                    .Where(t => !t.Name.Contains("Pythia"))
                    .ToArray();
                using var container = new ContainerConfiguration().WithParts(partTypes).CreateContainer();
                var providers = container
                    .GetExports<Lazy<ISignatureHelpProvider, OrderableLanguageMetadata>>()
                    .Where(p => p.Metadata.Language == LanguageNames.CSharp)
                    .Select(p => p.Value)
                    .ToImmutableArray();
                sw.Stop();
                PluginLogger.Info($"[签名帮助] ISignatureHelpProvider 聚合初始化: {providers.Length} 个提供器, {sw.ElapsedMilliseconds}ms", typeof(ScriptDebugControl).Name);

                _signatureHelpProviders = providers;
                return _signatureHelpProviders;
            }
        }

        /// <summary>
        /// 把 Roslyn SignatureHelpItems 适配为 AvalonEdit IOverloadProvider（OverloadInsightWindow 数据源）。
        /// 对齐 RoslynPad RoslynOverloadProvider：Header 用 TaggedText 拼方法签名（当前参数加粗），
        /// Content 显示方法文档 + 当前参数文档（DocumentationFactory 惰性求值）。
        /// </summary>
        private sealed class ScriptOverloadProvider : System.ComponentModel.INotifyPropertyChanged, IOverloadProvider
        {
            private SignatureHelpItems _signatureHelp;
            private IList<SignatureHelpItem> _items;
            private int _selectedIndex;

            public ScriptOverloadProvider(SignatureHelpItems signatureHelp)
            {
                _signatureHelp = signatureHelp;
                _items = signatureHelp.Items;
                _selectedIndex = signatureHelp.SelectedItemIndex ?? 0;
            }

            public void Update(SignatureHelpItems signatureHelp)
            {
                _signatureHelp = signatureHelp;
                _items = signatureHelp.Items;
                _selectedIndex = Math.Clamp(signatureHelp.SelectedItemIndex ?? _selectedIndex, 0, Math.Max(0, _items.Count - 1));
                RaisePropertyChanged(nameof(CurrentIndexText));
                RaisePropertyChanged(nameof(CurrentHeader));
                RaisePropertyChanged(nameof(CurrentContent));
            }

            public int Count => _items.Count;

            public int SelectedIndex
            {
                get => _selectedIndex;
                set
                {
                    if (_selectedIndex != value && value >= 0 && value < _items.Count)
                    {
                        _selectedIndex = value;
                        RaisePropertyChanged(nameof(SelectedIndex));
                        RaisePropertyChanged(nameof(CurrentIndexText));
                        RaisePropertyChanged(nameof(CurrentHeader));
                        RaisePropertyChanged(nameof(CurrentContent));
                    }
                }
            }

            public string CurrentIndexText => $"{_selectedIndex + 1}/{Count}";

            public object CurrentHeader => BuildHeader();

            public object CurrentContent => BuildContent();

            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

            private void RaisePropertyChanged(string name)
                => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

            private object BuildHeader()
            {
                var item = _items[_selectedIndex];
                var header = new System.Windows.Controls.WrapPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                };
                header.Children.Add(item.PrefixDisplayParts.ToTextBlock());
                if (!item.Parameters.IsDefault)
                {
                    for (var i = 0; i < item.Parameters.Length; i++)
                    {
                        var param = item.Parameters[i];
                        var isSelected = i == _signatureHelp.SemanticParameterIndex;
                        header.Children.Add(param.DisplayParts.ToTextBlock(isBold: isSelected));
                        if (i != item.Parameters.Length - 1)
                            header.Children.Add(item.SeparatorDisplayParts.ToTextBlock());
                    }
                }
                header.Children.Add(item.SuffixDisplayParts.ToTextBlock());
                return header;
            }

            private object BuildContent()
            {
                var item = _items[_selectedIndex];
                var content = new System.Windows.Controls.StackPanel();
                // 方法文档（惰性求值）
                var methodDoc = item.DocumentationFactory(System.Threading.CancellationToken.None).ToTextBlock();
                if (methodDoc.Inlines.Count > 0)
                    content.Children.Add(methodDoc);
                // 当前参数文档 + 参数名
                if (!item.Parameters.IsDefault)
                {
                    var index = _signatureHelp.SemanticParameterIndex;
                    if (index >= 0 && index < item.Parameters.Length)
                    {
                        var param = item.Parameters[index];
                        var paramDoc = param.DocumentationFactory(System.Threading.CancellationToken.None).ToTextBlock();
                        if (paramDoc.Inlines.Count > 0)
                        {
                            content.Children.Add(new System.Windows.Controls.WrapPanel
                            {
                                Orientation = System.Windows.Controls.Orientation.Horizontal,
                                Children =
                                {
                                    new System.Windows.Controls.TextBlock
                                    {
                                        Text = param.Name + ": ",
                                        FontWeight = System.Windows.FontWeights.Bold,
                                    },
                                    paramDoc,
                                },
                            });
                        }
                    }
                }
                if (content.Children.Count == 0)
                {
                    content.Children.Add(new System.Windows.Controls.TextBlock
                    { Text = "无参数", TextWrapping = System.Windows.TextWrapping.Wrap });
                }
                return content;
            }
        }

        // =======================================================
        // Phase 1: 实时诊断（Roslyn SemanticModel.GetDiagnostics → 红色波浪线）
        // =======================================================

        /// <summary>
        /// 编辑器文本变更 → 同步到 Parameters → 500ms 节流 → 诊断
        /// </summary>
        private void OnCodeEditorTextChanged(object? sender, EventArgs e)
        {
            // 初始化期间抑制诊断（SetParameters → RebuildWorkspace 完成前，
            // workspace 和 userDocumentId 尚未就绪，此时触发诊断只会打印
            // "[Diagnostic] 跳过: workspace=False" 日志噪声）
            if (_suppressDiagnostic) return;

            // ★ 用户改代码 → 旧执行结果失效，状态回"就绪"
            //   当前显示"成功/失败"时，用户改代码意味着新的执行周期开始
            if (StatusLabel.Content is string currentStatus &&
                (currentStatus == "成功" || currentStatus == "失败"))
            {
                UpdateStatus("就绪");
            }

            if (CodeEditor != null)
                _parameters.ScriptCode = CodeEditor.Text;
            ScheduleDiagnosticAsync();
            ScheduleSemanticAsync();
        }

        private void ScheduleDiagnosticAsync()
        {
            _diagnosticsCts?.Cancel();
            _diagnosticsCts?.Dispose();
            _diagnosticsCts = new CancellationTokenSource();
            var token = _diagnosticsCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500, token);
                    if (!token.IsCancellationRequested)
                        await RunDiagnosticAsync(token);
                }
                catch (OperationCanceledException) { }
            });
        }

        /// <summary>
        /// 更新 workspace 文本（双文档） → 获取诊断 → 红色波浪线
        /// </summary>
        private async Task RunDiagnosticAsync(CancellationToken token)
        {
            var typeName = GetType().Name;

            if (_workspace == null || _userDocumentId == null)
            {
                PluginLogger.Warning($"[Diagnostic] 跳过: workspace={_workspace != null}, userDocumentId={_userDocumentId != null}", typeName);
                return;
            }

            var generatedCode = _parameters.GeneratedCode;
            var userCode = _parameters.ScriptCode;

            // 代码未变则跳过诊断（避免同段代码重复 Roslyn 分析）
            if (userCode == _lastDiagnosedCode)
            {
                PluginLogger.Info("[Diagnostic] 代码未变，跳过诊断", typeName);
                return;
            }
            _lastDiagnosedCode = userCode;

            try
            {
                // 1. 更新 generated 文档文本：仅当 GeneratedCode 实际变化才 WithDocumentText。
                //    user 文档已绑定 SourceTextContainer 增量通道，随编辑器实时增量同步，不再 WithDocumentText 覆盖。
                //    ★ 避免每次编辑都重建 solution（generated 模板通常不变，重建会击穿 Roslyn 增量缓存）。
                if (generatedCode != _lastGeneratedCode)
                {
                    var newSolution = _workspace.CurrentSolution
                        .WithDocumentText(_documentId, SourceText.From(generatedCode));
                    if (!_workspace.TryApplyChanges(newSolution))
                    {
                        PluginLogger.Warning("[Diagnostic] TryApplyChanges 失败", typeName);
                        return;
                    }
                    _lastGeneratedCode = generatedCode;
                }

                var userDoc = _workspace.CurrentSolution.GetDocument(_userDocumentId);
                if (userDoc == null)
                {
                    PluginLogger.Warning("[Diagnostic] workspace 中找不到 user document", typeName);
                    return;
                }

                // 获取用户文档的语法树，用于过滤诊断
                var userSyntaxTree = await userDoc.GetSyntaxTreeAsync(token);
                var model = await userDoc.GetSemanticModelAsync(token);

                if (token.IsCancellationRequested) return;

                // 诊断 — 取用户文档中的错误与警告（Error 红波浪 / Warning 蓝波浪，行号直接对应编辑器行）
                // 波浪线 + ToolTip 均消费扁平 _currentDiagnostics，span 精准定位，不污染整行。
                var diagnostics = model!.GetDiagnostics(cancellationToken: token)
                    .Where(d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning
                        && d.Location.SourceTree == userSyntaxTree)
                    .ToImmutableArray();
                _currentDiagnostics = diagnostics;

                PluginLogger.Info($"[Diagnostic] Errors+Warnings={diagnostics.Length}", typeName);

                // 检测到 CS0518（预定义类型缺失）时输出 Warning 级汇总提示（仅一次，非逐条刷屏）
                if (diagnostics.Any(d => d.Id == "CS0518"))
                {
                    PluginLogger.Warning(
                        $"[Diagnostic] ⚠ 检测到 CS0518（预定义类型缺失），可能原因：运行时 BCL 引用（System.Private.CoreLib / System.Runtime）未正确加载，请检查 [MetadataRef] 初始化日志。",
                        typeName);
                }

                // 4. UI 线程重绘诊断波浪线：只重绘有诊断的行（不全量，避免连累语义着色行重绘导致闪烁）。
                //    ErrorMarkerColorizer 是 IBackgroundRenderer，波浪线随可视区渲染自动绘制，重绘诊断行即生效。
                var diag = diagnostics;
                await Dispatcher.InvokeAsync(() =>
                {
                    var textView = CodeEditor.TextArea.TextView;
                    var lineCount = CodeEditor.Document.LineCount;
                    foreach (var n in diag
                        .Select(d => d.Location.GetLineSpan().StartLinePosition.Line + 1)
                        .Distinct()
                        .Where(n => n >= 1 && n <= lineCount))
                    {
                        textView.Redraw(n, n, DispatcherPriority.Background);
                    }
                }, DispatcherPriority.Background, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                PluginLogger.Warning($"[ScriptDebug] Diagnostic error: {ex.Message}", typeName);
            }
        }

        // =======================================================
        // Phase 2b: Roslyn 语义高亮（IHighlighter 按行渲染）
        // =======================================================

        /// <summary>
        /// 编辑/重建后调度语义着色：只重绘当前可见行（不全量）。
        /// IHighlighter 架构下，AvalonEdit 对可见行调 HighlightLine，内部用 CompareAge 自动判断
        /// "行没变就复用旧缓存（不闪烁）、变了才重算"，无需手动清缓存。
        /// </summary>
        private void ScheduleSemanticAsync()
        {
            if (CodeEditor == null) return;

            var textView = CodeEditor.TextArea.TextView;
            if (textView.VisualLinesValid && textView.VisualLines.Count > 0)
            {
                // 只重绘当前可见行范围（对齐 RoslynPad 按可视行按需渲染，不全量 Redraw）
                var first = textView.VisualLines.First().FirstDocumentLine.LineNumber;
                var last = textView.VisualLines.Last().LastDocumentLine.LineNumber;
                textView.Redraw(first, last, DispatcherPriority.Background);
            }
            else
            {
                textView.Redraw(DispatcherPriority.Background);
            }
        }

        // =======================================================
        // CompletionData 包装 — 桥接 Roslyn CompletionItem -> AvalonEdit ICompletionData
        // =======================================================

        /// <summary>
        /// 将 Roslyn CompletionItem 包装为 AvalonEdit ICompletionData
        /// 使用 CompletionService.GetChangeAsync 获取精确的 TextChange 插入（含后缀补全如 ()）
        /// </summary>
        private sealed class RoslynCompletionData(CompletionItem item, Document? document) : ICompletionData
        {
            private System.Windows.Media.ImageSource? _image;
            private object? _description;
            private bool _loadingDescription;

            public System.Windows.Media.ImageSource? Image => _image ??= item.GetGlyph().ToImageSource();

            public string Text => item.FilterText;

            public object Content => item.DisplayText;

            public object? Description
            {
                get
                {
                    // 延迟加载签名/文档描述：首次访问异步填充，未就绪时回退内联描述（不阻塞弹窗）
                    if (!_loadingDescription)
                    {
                        _loadingDescription = true;
                        _ = LoadDescriptionAsync();
                    }
                    return _description ?? item.InlineDescription;
                }
            }

            private async Task LoadDescriptionAsync()
            {
                if (document == null) return;
                try
                {
                    var description = await CompletionService.GetService(document)
                        .GetDescriptionAsync(document, item).ConfigureAwait(true);
                    _description = string.Join(" ", description.TaggedParts.Select(p => p.Text));
                }
                catch
                {
                    // 描述加载失败保持 InlineDescription（UI 提示降级，不阻塞补全）
                    _description = item.InlineDescription;
                }
            }

            public double Priority => 0;

            public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
            {
                // ★ 不走 GetChangeAsync：CompletionItem.Span 是查询时快照值，
                //    AvalonEdit 自动过滤不会更新它，导致替换范围偏小（如只替换 "s" 而非 "str"）
                // ★ 直接从编辑器实际文本计算替换范围
                var code = textArea.Document.Text;
                var start = completionSegment.Offset;
                var end = start;
                while (end < code.Length &&
                       (char.IsLetterOrDigit(code[end]) || code[end] == '_'))
                    end++;
                var actualLen = Math.Min(end - start, 200);
                textArea.Document.Replace(start, actualLen, item.DisplayText);
            }
        }
    // =======================================================
    // Phase 2b: Roslyn 语义高亮 — 调色板 / 区间 / 着色器
    // =======================================================

    /// <summary>
    /// <summary>
    /// 从 Application.Resources 取主题刷子（补全弹窗/签名帮助浮窗是 Popup，不走可视化树，
    /// 无法靠 DynamicResource 自动解析，需手动按 SdkThemeKeys 从 App.Resources 取）。
    /// 取不到时返回 null，调用方回退 AvalonEdit 默认（不崩）。
    /// </summary>
    internal static class ThemeResource
    {
        public static Brush? GetBrush(string key)
        {
            var res = Application.Current?.Resources;
            if (res == null || string.IsNullOrEmpty(key)) return null;
            return res[key] as Brush;
        }

        /// <summary>判断当前是否暗色主题：扫描 MergedDictionaries 是否含 DarkTheme 字典（工具插件不引用 UI 层，靠 URI 识别）。</summary>
        public static bool IsDarkTheme()
        {
            var res = Application.Current?.Resources;
            if (res == null) return false;
            foreach (var dict in res.MergedDictionaries)
            {
                if (dict.Source != null &&
                    dict.Source.OriginalString.Contains("Themes/Dark/DarkTheme.xaml", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 支持打开文档的 Workspace（对齐 RoslynPad RoslynWorkspace.CanOpenDocuments）。
    /// AdhocWorkspace 是 sealed 且 CanOpenDocuments=false，无法派生；故继承 Workspace 基类。
    /// 用户文档经 AvalonEditTextContainer（SourceTextContainer）注册到 OpenDocument，
    /// Roslyn 走增量文本通道，仅重解析受影响语法树区域（替代 TextLoader 全量通道，消除打字卡顿）。
    /// </summary>
    public sealed class ScriptWorkspace : Workspace
    {
        public ScriptWorkspace(MefHostServices host) : base(host, WorkspaceKind.Host) { }

        public override bool CanOpenDocuments => true;

        // RebuildWorkspace 运行期重建 workspace：加项目、加元数据引用、加文档；
        // 后续诊断更新 generated 文档文本（ChangeDocument）。全部放行，
        // 否则 TryApplyChanges 对不支持的变化类型抛 NotSupportedException 导致 workspace 半初始化。
        public override bool CanApplyChange(ApplyChangesKind feature)
            => feature is ApplyChangesKind.AddProject
                or ApplyChangesKind.AddMetadataReference
                or ApplyChangesKind.AddDocument
                or ApplyChangesKind.ChangeDocument;

        /// <summary>将编辑器文档（SourceTextContainer）注册为增量文本通道。</summary>
        public void OpenDocument(DocumentId documentId, SourceTextContainer textContainer)
        {
            OnDocumentOpened(documentId, textContainer);
            OnDocumentContextUpdated(documentId);
        }

        /// <summary>
        /// 直接替换整个 solution（暴露基类 protected SetCurrentSolution，对齐 RoslynPad RoslynWorkspace）。
        /// RebuildWorkspace 用它一次性接受 AddProject/AddMetadataReference/AddDocument/WithDocumentText 全部变化，
        /// 不走 TryApplyChanges 的 OnProjectAdded/OnDocumentAdded 钩子链（Workspace 基类下这些钩子可能抛异常）。
        /// </summary>
        public void SetCurrentSolution(Microsoft.CodeAnalysis.Solution solution)
            => base.SetCurrentSolution(solution);

        protected override void ApplyDocumentTextChanged(DocumentId document, SourceText newText)
            => OnDocumentTextChanged(document, newText, PreservationMode.PreserveIdentity);
    }

    /// <summary>鼠标移到诊断 span 上：显示错误/警告消息（对齐 RoslynPad TextMarkerToolTipProvider）。</summary>
    private void TextArea_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_currentDiagnostics.Count == 0) return;

        TextViewPosition? position;
        try
        {
            position = CodeEditor.TextArea.TextView.GetPositionFloor(
                e.GetPosition(CodeEditor.TextArea.TextView) + CodeEditor.TextArea.TextView.ScrollOffset);
        }
        catch (ArgumentOutOfRangeException) { return; }

        if (!position.HasValue || position.Value.Location.IsEmpty) return;

        var offset = CodeEditor.Document.GetOffset(position.Value.Location);
        // 同一诊断内移动不重复刷新（防 MouseMove 高频闪烁）
        if (_diagnosticToolTip?.IsOpen == true && offset == _lastDiagToolTipOffset) return;

        var diag = _currentDiagnostics.FirstOrDefault(d =>
            d.Location.SourceSpan.Start <= offset && offset < d.Location.SourceSpan.End);
        if (diag == null) return;

        if (_diagnosticToolTip == null)
        {
            _diagnosticToolTip = new System.Windows.Controls.ToolTip
            {
                MaxWidth = 480,
                PlacementTarget = CodeEditor.TextArea,
                Content = diag.GetMessage(),
            };
            _diagnosticToolTip.Closed += (_, _) => _diagnosticToolTip = null;
        }
        else
        {
            _diagnosticToolTip.Content = diag.GetMessage();
        }
        _lastDiagToolTipOffset = offset;
        _diagnosticToolTip.IsOpen = true;
    }

    /// <summary>鼠标移开编辑区：关闭诊断悬停详情浮窗。</summary>
    private void TextArea_MouseLeave(object? sender, MouseEventArgs e)
    {
        if (_diagnosticToolTip != null)
            _diagnosticToolTip.IsOpen = false;
        _lastDiagToolTipOffset = -1;
    }

    }

    // =======================================================
    // Phase 1: 实时诊断着色器 — 编译错误/警告在 span 下画波浪线
    // =======================================================

    /// <summary>
    /// 编译诊断标记着色器：在错误/警告 span 底部绘制波浪线（对齐 RoslynPad TextMarkerService）。
    /// 采用 IBackgroundRenderer 绘制在文字下层，精准 span 不污染整行、不覆盖 xshd 语法颜色。
    /// 分级着色：Error 红 / Warning 蓝，颜色随暗色主题。运行在 KnownLayer.Selection 层。
    /// </summary>
    internal sealed class ErrorMarkerColorizer : IBackgroundRenderer
    {
        private readonly Func<IReadOnlyList<Diagnostic>> _getDiagnostics;
        private readonly bool _isDark;

        // 浅色主题：Error 红 / Warning 蓝
        private static readonly Brush ErrorBrush = FrozenBrush(0xFF, 0x00, 0x00);
        private static readonly Brush WarningBrush = FrozenBrush(0x1E, 0x90, 0xFF);
        // 暗色主题：Error 亮红 / Warning 亮蓝（深色底上更清晰）
        private static readonly Brush DarkErrorBrush = FrozenBrush(0xFF, 0x5F, 0x5F);
        private static readonly Brush DarkWarningBrush = FrozenBrush(0x64, 0xBE, 0xFF);

        private const double WaveOffset = 2.5;   // 波浪间距（像素）
        private const double WavePenWidth = 1.0; // 波浪线宽

        public ErrorMarkerColorizer(Func<IReadOnlyList<Diagnostic>> getDiagnostics, bool isDark)
        {
            _getDiagnostics = getDiagnostics;
            _isDark = isDark;
        }

        public KnownLayer Layer => KnownLayer.Selection;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            var diagnostics = _getDiagnostics();
            if (diagnostics.Count == 0 || !textView.VisualLinesValid) return;

            var visualLines = textView.VisualLines;
            if (visualLines.Count == 0) return;

            // 可视区偏移范围：只画可见行的诊断，滚动时按需绘制
            var viewStart = visualLines.First().FirstDocumentLine.Offset;
            var viewEnd = visualLines.Last().LastDocumentLine.EndOffset;

            foreach (var diag in diagnostics)
            {
                var span = diag.Location.SourceSpan;
                if (span.Start >= viewEnd || span.End <= viewStart) continue;

                var brush = GetSeverityBrush(diag.Severity);
                DrawWaveLine(drawingContext, textView, span.Start, span.Length, brush);
            }
        }

        private Brush GetSeverityBrush(DiagnosticSeverity severity)
            => severity switch
            {
                DiagnosticSeverity.Warning => _isDark ? DarkWarningBrush : WarningBrush,
                _ => _isDark ? DarkErrorBrush : ErrorBrush
            };

        /// <summary>沿 span 文字底部画一排小三角折线（波浪线），对齐 RoslynPad TextMarkerService.CreatePoints。</summary>
        private void DrawWaveLine(DrawingContext dc, TextView textView, int startOffset, int length, Brush brush)
        {
            var segment = new TextSegment { StartOffset = startOffset, Length = length };
            var rects = BackgroundGeometryBuilder.GetRectsForSegment(textView, segment);

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                foreach (var rect in rects)
                {
                    var bottomLeft = rect.BottomLeft;
                    var count = Math.Max((int)((rect.BottomRight.X - bottomLeft.X) / WaveOffset) + 1, 4);
                    ctx.BeginFigure(bottomLeft, isFilled: false, isClosed: false);
                    ctx.PolyLineTo(CreateWavePoints(bottomLeft, count), isStroked: true, isSmoothJoin: false);
                }
            }
            geometry.Freeze();

            var pen = new Pen(brush, WavePenWidth);
            pen.Freeze();
            dc.DrawGeometry(Brushes.Transparent, pen, geometry);
        }

        /// <summary>自底向上生成波浪点序列：每格交替上浮 offset 与回落，形成锯齿。</summary>
        private static List<Point> CreateWavePoints(Point bottomLeft, int count)
        {
            var points = new List<Point>(count);
            for (var i = 0; i < count; i++)
                points.Add(new Point(bottomLeft.X + i * WaveOffset, bottomLeft.Y - ((i + 1) % 2 == 0 ? WaveOffset : 0)));
            return points;
        }

        private static Brush FrozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            if (brush.CanFreeze) brush.Freeze();
            return brush;
        }
    }

    /// <summary>
    /// Roslyn TaggedText → WPF TextBlock（签名帮助 Header/Content 渲染）。
    /// 对齐 RoslynPad SymbolDisplayPartExtensions.ToTextBlock：逐段 TaggedText 生成 Run，
    /// 支持 isBold 高亮当前参数，按文本标签着色。
    /// 必须是顶层静态类（扩展方法不能定义在嵌套类中，rule-002/rule-008）。
    /// </summary>
    internal static class TaggedTextExtensions
    {
        public static System.Windows.Controls.TextBlock ToTextBlock(this IEnumerable<TaggedText> text, bool isBold = false)
        {
            var result = new System.Windows.Controls.TextBlock { TextWrapping = TextWrapping.Wrap };
            foreach (var part in text)
            {
                result.Inlines.Add(part.ToRun(isBold));
            }
            return result;
        }

        public static System.Windows.Documents.Run ToRun(this TaggedText text, bool isBold = false)
        {
            var run = new System.Windows.Documents.Run(text.Text);
            if (isBold) run.FontWeight = FontWeights.Bold;

            // 对齐 RoslynPad：关键词蓝、类型青（签名帮助浮窗为辅助 Popup，直接用固定 Brushes 色，
            // 不依赖 ScriptDebugControl 内部主题类，保证本顶层类可独立编译）。
            switch (text.Tag)
            {
                case TextTags.Keyword:
                    run.Foreground = Brushes.Blue;
                    break;
                case TextTags.Struct:
                case TextTags.Enum:
                case TextTags.TypeParameter:
                case TextTags.Class:
                case TextTags.Delegate:
                case TextTags.Interface:
                    run.Foreground = Brushes.Teal;
                    break;
            }
            return run;
        }
    }

}




