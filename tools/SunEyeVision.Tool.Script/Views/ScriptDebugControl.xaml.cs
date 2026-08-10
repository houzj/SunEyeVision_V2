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
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using LogEntry = SunEyeVision.Tool.Script.Models.LogEntry;
using SunEyeVision.Plugin.SDK.UI.Controls;
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
        private AdhocWorkspace? _workspace;
        private DocumentId? _documentId;                              // 生成的字段声明文档 ID（不可变，仅 RebuildWorkspace 赋值）
        private DocumentId? _userDocumentId;                          // 用户代码文档 ID（不可变，仅 RebuildWorkspace 赋值）
        private CompletionWindow? _completionWindow;
        private CompletionItem[] _currentItems;                        // 唯一数据源（单层，无后台刷新）
        private CancellationTokenSource? _throttleCts;                // 50ms 节流令牌

        // Phase 1: 实时诊断
        private CancellationTokenSource? _diagnosticsCts;             // 500ms 诊断节流
        private IReadOnlyList<Diagnostic> _currentDiagnostics = [];   // 当前错误列表
        private string? _lastDiagnosedCode;                            // 上次已诊断的代码，用于跳过不变代码

        // 初始化阶段抑制诊断（避免 TextChanged 在 workspace 就绪前触发诊断，
        // 消除 "[Diagnostic] 跳过: workspace=False" 日志噪声）
        private bool _suppressDiagnostic;

        // Phase 2b: Roslyn 语义高亮
        private CancellationTokenSource? _semanticCts;             // 100ms 语义着色节流
        private IReadOnlyList<SemanticSpan>? _semanticSpans;        // 当前语义着色区间（offset / length / brush），供 SemanticColorizer 读取

        // 进程生命周期内共享的 MefHostServices（首次初始化慢，后续缓存命中快）
        private static MefHostServices? _sharedMefHost;
        private static object _mefLock = new();

        // 预热 AdhocWorkspace 缓存（工具注册时创建，SetParameters 中复用）
        private static AdhocWorkspace? _preWarmedWorkspace;

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
        /// 后台预热 Roslyn MefHostServices（线程安全，消除预热与主线程的竞态）。
        /// 预热结果存入静态缓存，SetParameters 中 RebuildWorkspace 可直接复用，
        /// 首次弹出不再阻塞 UI 线程。
        /// </summary>
        private static async Task WarmupRoslynAsync()
        {
            await Task.Run(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                PluginLogger.Info("[补全] 后台预热 Roslyn 开始...", nameof(ScriptDebugControl));
                GetSharedMefHost();
                sw.Stop();
                PluginLogger.Info($"[补全] 后台预热 Roslyn 完成: {sw.ElapsedMilliseconds}ms", nameof(ScriptDebugControl));
            });
        }

        /// <summary>
        /// 后台预热 AdhocWorkspace（创建空项目骨架），
        /// SetParameters 中 RebuildWorkspace 直接复用，节省 ~200-500ms。
        /// </summary>
        private static async Task WarmupWorkspaceAsync()
        {
            await Task.Run(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                PluginLogger.Info("[补全] 后台预热 Workspace 开始...", nameof(ScriptDebugControl));
                var host = GetSharedMefHost();
                var ws = new AdhocWorkspace(host);
                ws.AddProject("ScriptCompletion", LanguageNames.CSharp);
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

            // Layer 2a: 实时错误标记着色器（浅红背景标记编译错误行，保留语法颜色）
            var errorMarker = new ErrorMarkerColorizer(() => _currentDiagnostics, false);
            CodeEditor.TextArea.TextView.LineTransformers.Add(errorMarker);
            PluginLogger.Info("[Highlighting] 已加载实时错误标记着色器", typeName);

            // Layer 2b: Roslyn 语义高亮着色器（基于 SemanticModel，对类型=青/方法·属性=紫/字段=金 上色，与 xshd 互补）
            //   ColorizeLine 覆盖前景色，与 Layer 2a 的背景标记互不干扰
            var semanticColorizer = new SemanticColorizer(() => _semanticSpans);
            CodeEditor.TextArea.TextView.LineTransformers.Add(semanticColorizer);
            PluginLogger.Info("[Highlighting] 已加载 Roslyn 语义高亮着色器", typeName);

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
            _ = WarmupRoslynAsync();
            _ = WarmupWorkspaceAsync();

            // 自动补全事件
            CodeEditor.TextArea.TextEntered += TextArea_TextEntered;
            CodeEditor.TextArea.TextEntering += TextArea_TextEntering;

            // 控件卸载时清理
            Unloaded += (_, _) =>
            {
                CodeEditor.TextArea.TextEntered -= TextArea_TextEntered;
                CodeEditor.TextArea.TextEntering -= TextArea_TextEntering;
                CodeEditor.TextChanged -= OnCodeEditorTextChanged;
                _workspace?.Dispose();
                _diagnosticsCts?.Cancel();
                _diagnosticsCts?.Dispose();
                _semanticCts?.Cancel();
                _semanticCts?.Dispose();
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
                // ★ 后台异步执行：窗口先显示，补全引擎和诊断在后台就绪。
                //   用户先看到编辑器，3-4s 后 IntelliSense 可用（对齐 VS Code 的标准行为）。
                _ = Dispatcher.InvokeAsync(() =>
                {
                    var perfSw = System.Diagnostics.Stopwatch.StartNew();
                    RebuildWorkspace();
                    PluginLogger.Info($"[Perf] RebuildWorkspace: {perfSw.ElapsedMilliseconds}ms", GetType().Name);
                    _suppressDiagnostic = false;    // workspace 就绪，开放诊断
                    ScheduleDiagnosticAsync();
                    ScheduleSemanticAsync(0);       // 首次打开立即着色（0ms）
                }, DispatcherPriority.Background);

                // ★ 参数注入完成 → 状态从"初始化中..."切到"就绪"
                //   此时编辑器代码已加载、Roslyn 预热已发起，对用户而言工具已就绪
                UpdateStatus("就绪");
            }
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
                    RebuildWorkspace();
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
        /// 动态重建工作区（构造函数/SetParameters/用户增删 DLL/修改变量后调用）
        /// 添加两个文档：生成的字段声明 partial class + 用户完整 partial class，通过 Roslyn 自动合并
        /// </summary>
        private void RebuildWorkspace()
        {
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
                _workspace = new AdhocWorkspace(host);
                PluginLogger.Info("[补全] AdhocWorkspace 实例已创建", typeName);
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
                project = _workspace.AddProject("ScriptCompletion", LanguageNames.CSharp);
                project = project.WithCompilationOptions(
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
            if (!_workspace.TryApplyChanges(project.Solution))
            {
                PluginLogger.Warning("[补全] TryApplyChanges 失败: 无法应用项目变更到 workspace", typeName);
            }
            PluginLogger.Info($"[补全] TryApplyChanges: {sw.ElapsedMilliseconds}ms", typeName);

            sw.Restart();

            // ★ 添加两个文档：字段声明 partial class + 用户完整 partial class
            var genDoc = _workspace.AddDocument(project.Id, "ScriptGenerated.cs",
                SourceText.From(generatedCode));
            _documentId = genDoc.Id;

            var userDoc = _workspace.AddDocument(project.Id, "ScriptUser.cs",
                SourceText.From(userCode));
            _userDocumentId = userDoc.Id;

            PluginLogger.Info($"[补全] 文档添加完成: genDocId={_documentId}, userDocId={_userDocumentId}: {sw.ElapsedMilliseconds}ms", typeName);

            _currentItems = [];   // 清空缓存，下次输入触发全新解析

            var refs = _workspace.CurrentSolution.Projects.First().MetadataReferences;
            PluginLogger.Info($"[补全] Workspace 重建完成: {refs.Count} 个引用, 总耗时={totalSw.ElapsedMilliseconds}ms", typeName);
        }

        /// <summary>
        /// 统一入口：字母 / 小数点 → 50ms 节流 → 单次 Roslyn 解析 → 单层弹窗
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
            if (!isTriggerChar)
            {
                PluginLogger.Info($"[补全] TextEntered 非触发字符: '{e.Text}'", typeName);
                return;
            }

            PluginLogger.Info($"[补全] TextEntered 触发: text='{e.Text}', caretOffset={CodeEditor.TextArea.Caret.Offset}", typeName);

            // 取消上一次节流，重新计时
            _throttleCts?.Cancel();
            _throttleCts?.Dispose();
            _throttleCts = new CancellationTokenSource();
            var token = _throttleCts.Token;

            try
            {
                await Task.Delay(50, token);     // 50ms 节流：快速连击停手后才解析
                if (token.IsCancellationRequested)
                {
                    PluginLogger.Info("[补全] 节流已被取消，不执行补全查询", typeName);
                    return;
                }

                bool isDot = e.Text == ".";

                if (isDot)
                {
                    PluginLogger.Info("[补全] '.' 触发成员补全 Re-query", typeName);
                    await ThrottledRefreshAndShow('.');
                    return;
                }

                // 字母数字：窗口已开 → 沉默，AvalonEdit CompletionList 自动过滤（内置质量评分）
                // 无窗口 → 首次 Roslyn 查询
                bool windowActive = _completionWindow != null && _currentItems.Length > 0;
                PluginLogger.Info($"[补全] 字母触发: text='{e.Text}', windowActive={windowActive}, currentItems={_currentItems.Length}", typeName);
                if (!windowActive)
                {
                    await ThrottledRefreshAndShow(e.Text[0]);
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
        /// 在 '.' 手动插入后，直接触发成员补全查询（不再需要同步 workspace，
        /// FetchFreshCompletions 使用 WithText 避免竞态）
        /// </summary>
        private async Task TriggerMemberCompletionAfterDot()
        {
            await ThrottledRefreshAndShow('.');
        }

        /// <summary>
        /// 单次 Roslyn 解析 + 弹窗/更新窗口（仅从 ThrottledRefreshAndShow 调用）
        /// 窗口打开期间永远不会再次执行此方法
        /// </summary>
        private async Task ThrottledRefreshAndShow(char triggerChar = '\0')
        {
            var typeName = GetType().Name;
            PluginLogger.Info($"[补全] ThrottledRefreshAndShow 开始: triggerChar='{(triggerChar == '\0' ? "\\0" : triggerChar.ToString())}', caretOffset={CodeEditor.TextArea.Caret.Offset}", typeName);

            var items = await FetchFreshCompletions(triggerChar);
            if (items.IsDefault || items.Length == 0)
            {
                PluginLogger.Warning($"[补全] Roslyn 未返回补全结果, caretOffset={CodeEditor.TextArea.Caret.Offset}, items.IsDefault={items.IsDefault}, items.Length={items.Length}", typeName);
                return;
            }

            PluginLogger.Info($"[补全] 获取到 {items.Length} 个补全项，准备弹窗", typeName);

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
            var typeName = GetType().Name;
            var caretOffset = CodeEditor.TextArea.Caret.Offset;
            var prefix = GetPrefixFromCaret();

            PluginLogger.Info($"[补全] ShowCompletionWindow: items={items.Length}, caretOffset={caretOffset}, prefix='{prefix}'", typeName);

            if (_completionWindow != null)
            {
                // 已有窗口 → 更新数据源（Roslyn 刷新场景）
                PluginLogger.Info($"[补全] 复用已有 CompletionWindow，更新 {items.Length} 项", typeName);
                PopulateData(_completionWindow.CompletionList.CompletionData, items);
                return;
            }

            var startOffset = caretOffset - prefix.Length;
            var endOffset = FindEndOfWord(caretOffset);
            PluginLogger.Info($"[补全] 创建新 CompletionWindow: startOffset={startOffset}, endOffset={endOffset}, prefix='{prefix}'", typeName);

            // 创建新窗口，IsFiltering=true 为默认值，AvalonEdit 自动做智能过滤
            var window = new CompletionWindow(CodeEditor.TextArea) { StartOffset = startOffset, EndOffset = endOffset };
            PopulateData(window.CompletionList.CompletionData, items);

            window.Closed += (_, _) =>
            {
                PluginLogger.Info($"[补全] CompletionWindow 已关闭，清空 _currentItems", typeName);
                _completionWindow = null;
                _currentItems = [];    // 窗口关闭 → 数据作废，下次重新解析
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            window.Show();
            PluginLogger.Info($"[补全] CompletionWindow.Show() 完成: {sw.ElapsedMilliseconds}ms", typeName);
            _completionWindow = window;
        }

        /// <summary>
        /// 将 Roslyn CompletionItem[] 直接注入 CompletionList（AvalonEdit 接管客户端过滤）
        /// </summary>
        private void PopulateData(IList<ICompletionData> data,
            ImmutableArray<CompletionItem> items)
        {
            data.Clear();
            foreach (var item in items)
                data.Add(new RoslynCompletionData(item, null));
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
        /// 使用 document.WithText() 避免与诊断线程的 TryApplyChanges 竞态
        /// 使用 _userDocumentId 保证补全在用户代码空间内
        /// </summary>
        private async Task<ImmutableArray<CompletionItem>> FetchFreshCompletions(char triggerChar = '\0')
        {
            var typeName = GetType().Name;

            if (_workspace == null || _userDocumentId == null)
            {
                PluginLogger.Warning($"[补全] FetchFreshCompletions 跳过: workspace={_workspace != null}, userDocumentId={_userDocumentId != null}", typeName);
                return default;
            }

            var code = CodeEditor.Text;
            var caretOffset = CodeEditor.TextArea.Caret.Offset;

            PluginLogger.Info($"[补全] FetchFreshCompletions 进入: codeLen={code.Length}, caretOffset={caretOffset}, triggerChar='{(triggerChar == '\0' ? "\\0" : triggerChar.ToString())}'", typeName);

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
                // 从 workspace 获取用户文档，使用局部 doc，不修改任何字段
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var doc = _workspace.CurrentSolution.GetDocument(_userDocumentId);
                if (doc == null)
                {
                    PluginLogger.Warning($"[补全] workspace 中找不到 user document: {_userDocumentId}", typeName);
                    return default;
                }
                doc = doc.WithText(SourceText.From(code));
                PluginLogger.Info($"[补全] WithText 完成: {sw.ElapsedMilliseconds}ms", typeName);

                sw.Restart();
                var completionService = CompletionService.GetService(doc);
                if (completionService == null)
                {
                    PluginLogger.Warning($"[补全] CompletionService 不可用, caretOffset={caretOffset}", typeName);
                    return default;
                }
                PluginLogger.Info($"[补全] GetService 完成: {sw.ElapsedMilliseconds}ms", typeName);

                // ★ 传入精确的 CompletionTrigger（激活成员补全 Provider）
                var trigger = triggerChar != '\0'
                    ? CompletionTrigger.CreateInsertionTrigger(triggerChar)
                    : CompletionTrigger.Default;

                sw.Restart();
                PluginLogger.Info($"[补全] 调用 GetCompletionsAsync: triggerType={trigger.Kind}, triggerChar='{(triggerChar == '\0' ? "\\0" : triggerChar.ToString())}'", typeName);

                var results = await completionService.GetCompletionsAsync(doc, caretOffset, trigger);

                if (results == null)
                {
                    PluginLogger.Warning($"[补全] GetCompletionsAsync 返回 null: {sw.ElapsedMilliseconds}ms", typeName);
                    return default;
                }

                var items = results.ItemsList.ToImmutableArray();
                var topItems = items.Take(5).Select(i => i.DisplayText);
                PluginLogger.Info($"[补全] GetCompletionsAsync 返回 {items.Length} 个结果: {sw.ElapsedMilliseconds}ms, 前5项=[{string.Join(", ", topItems)}]", typeName);

                return items;
            }
            catch (Exception ex)
            {
                PluginLogger.Warning($"[补全] FetchCompletions 异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", typeName);
                return default;
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
                // 1. 更新 workspace 文本：双文档同步更新
                var newSolution = _workspace.CurrentSolution
                    .WithDocumentText(_documentId, SourceText.From(generatedCode))
                    .WithDocumentText(_userDocumentId, SourceText.From(userCode));
                if (!_workspace.TryApplyChanges(newSolution))
                {
                    PluginLogger.Warning("[Diagnostic] TryApplyChanges 失败", typeName);
                    return;
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

                // 诊断 — 只取用户文档中的错误（行号直接对应编辑器行）
                var diagnostics = model!.GetDiagnostics(cancellationToken: token)
                    .Where(d => d.Severity == DiagnosticSeverity.Error
                        && d.Location.SourceTree == userSyntaxTree)
                    .ToImmutableArray();
                _currentDiagnostics = diagnostics;

                PluginLogger.Info($"[Diagnostic] Errors={diagnostics.Length}", typeName);
                // 完整输出诊断详情（不限量），辅助排查编译错误
                var hasMissingRef = false;
                foreach (var diag in diagnostics)
                {
                    var loc = diag.Location.GetLineSpan();
                PluginLogger.Info(
                    $"[Diagnostic]   {diag.Id} @ line {loc.StartLinePosition.Line + 1}: {diag.GetMessage()}",
                    typeName);
                    if (diag.Id == "CS0518") hasMissingRef = true;
                }

                // 检测到 CS0518（预定义类型缺失）时输出 Warning 级汇总提示
                if (hasMissingRef)
                {
                    PluginLogger.Warning(
                        $"[Diagnostic] ⚠ 检测到 CS0518（预定义类型缺失），可能原因：运行时 BCL 引用（System.Private.CoreLib / System.Runtime）未正确加载，请检查 [MetadataRef] 初始化日志。",
                        typeName);
                }

                // 4. UI 线程重绘（红色错误标记）
                await Dispatcher.InvokeAsync(() => CodeEditor.TextArea.TextView.Redraw(), DispatcherPriority.Background, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                PluginLogger.Warning($"[ScriptDebug] Diagnostic error: {ex.Message}", typeName);
            }
        }

        // =======================================================
        // Phase 2b: Roslyn 语义高亮（SemanticModel → 类型/方法/属性/字段 着色）
        // =======================================================

        /// <summary>
        /// 编辑/重建后调度语义着色：独立 100ms 节流（首次打开传 0 立即着色）。
        /// 与诊断线程（500ms）互不干扰：本方法使用 doc.WithText 局部快照，不修改 workspace 共享状态。
        /// </summary>
        private void ScheduleSemanticAsync(int delayMs = 100)
        {
            _semanticCts?.Cancel();
            _semanticCts?.Dispose();
            _semanticCts = new CancellationTokenSource();
            var token = _semanticCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delayMs, token);
                    if (!token.IsCancellationRequested)
                        await RunSemanticAsync(token);
                }
                catch (OperationCanceledException) { }
            });
        }

        /// <summary>
        /// 基于 SemanticModel 对标识符上色：类型=青、方法/事件=紫、属性=紫、字段=金。
        /// 与 xshd 语法高亮互补（xshd 仅覆盖 BCL 类型名与声明/调用，本类覆盖所有符号含用户类型）。
        /// 着色结果缓存到 _semanticSpans，由 SemanticColorizer.ColorizeLine 按行消费。
        /// </summary>
        private async Task RunSemanticAsync(CancellationToken token)
        {
            var typeName = GetType().Name;

            if (_workspace == null || _userDocumentId == null)
            {
                PluginLogger.Warning($"[Semantic] 跳过: workspace={_workspace != null}, userDocumentId={_userDocumentId != null}", typeName);
                return;
            }

            var code = _parameters.ScriptCode;
            if (string.IsNullOrEmpty(code))
            {
                _semanticSpans = [];
                await Dispatcher.InvokeAsync(() => CodeEditor.TextArea.TextView.Redraw(), DispatcherPriority.Background, token);
                return;
            }

            try
            {
                var doc = _workspace.CurrentSolution.GetDocument(_userDocumentId);
                if (doc == null)
                {
                    PluginLogger.Warning("[Semantic] workspace 中找不到 user document", typeName);
                    return;
                }
                doc = doc.WithText(SourceText.From(code));

                var root = await doc.GetSyntaxRootAsync(token);
                var model = await doc.GetSemanticModelAsync(token);
                if (root == null || model == null || token.IsCancellationRequested) return;

                var spans = new List<SemanticSpan>();
                foreach (var syntaxToken in root.DescendantTokens())
                {
                    var brush = ClassifyToken(syntaxToken, model);
                    if (brush != null)
                        spans.Add(new SemanticSpan(syntaxToken.SpanStart, syntaxToken.Span.Length, brush));
                }

                _semanticSpans = spans;
                PluginLogger.Info($"[Semantic] 已生成 {spans.Count} 个语义着色区间", typeName);

                if (!token.IsCancellationRequested)
                    await Dispatcher.InvokeAsync(() => CodeEditor.TextArea.TextView.Redraw(), DispatcherPriority.Background, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                PluginLogger.Warning($"[Semantic] 着色异常: {ex.Message}", typeName);
            }
        }

        /// <summary>
        /// 根据标识符的语义符号返回前景色，无语义意义（局部变量/参数/命名空间）返回 null 保持默认色。
        /// </summary>
        private static Brush? ClassifyToken(SyntaxToken token, SemanticModel model)
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken)) return null;
            var node = token.Parent;
            if (node == null) return null;

            var symbol = model.GetSymbolInfo(node).Symbol;
            if (symbol == null)
                symbol = model.GetTypeInfo(node).Type;
            if (symbol == null) return null;

            return symbol.Kind switch
            {
                SymbolKind.NamedType => SemanticBrushes.Type,
                SymbolKind.Method => SemanticBrushes.Method,
                SymbolKind.Property => SemanticBrushes.Property,
                SymbolKind.Field => SemanticBrushes.Field,
                SymbolKind.Event => SemanticBrushes.Method,
                _ => null
            };
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
            public System.Windows.Media.ImageSource? Image => null;

            public string Text => item.FilterText;

            public object Content => item.DisplayText;

            public object? Description => item.InlineDescription;

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
    /// 语义着色调色板（与 CSharpLight.xshd 对齐：Type=#267F99 青，Method=#6F42C1 紫）。
    /// </summary>
    private static class SemanticBrushes
    {
        public static readonly Brush Type = FrozenSolidBrush(0x26, 0x7F, 0x99);   // 类型：青
        public static readonly Brush Method = FrozenSolidBrush(0x6F, 0x42, 0xC1); // 方法/事件：紫
        public static readonly Brush Property = FrozenSolidBrush(0x6F, 0x42, 0xC1); // 属性：紫（对齐 VS）
        public static readonly Brush Field = FrozenSolidBrush(0x79, 0x5E, 0x26);  // 字段：金

        private static Brush FrozenSolidBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            if (brush.CanFreeze) brush.Freeze();
            return brush;
        }
    }

    /// <summary>
    /// 单个语义着色区间（文档绝对偏移 + 长度 + 前景色）。
    /// </summary>
    internal sealed class SemanticSpan(int start, int length, Brush brush)
    {
        public int Start { get; } = start;
        public int Length { get; } = length;
        public Brush Brush { get; } = brush;
    }

    /// <summary>
    /// Roslyn 语义高亮着色器：根据 SemanticModel 将标识符（类型/方法/属性/字段）着色为对应前景色。
    /// 运行在 xshd 语法高亮之后，通过 DocumentColorizingTransformer 增量覆盖前景，
    /// 与 ErrorMarkerColorizer（背景）互补，互不干扰。
    /// </summary>
    internal sealed class SemanticColorizer : DocumentColorizingTransformer
    {
        private readonly Func<IReadOnlyList<SemanticSpan>?> _getSpans;

        public SemanticColorizer(Func<IReadOnlyList<SemanticSpan>?> getSpans)
        {
            _getSpans = getSpans;
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            var spans = _getSpans();
            if (spans == null || spans.Count == 0) return;

            var lineStart = line.Offset;
            var lineEnd = line.EndOffset;

            foreach (var span in spans)
            {
                if (span.Start >= lineEnd) continue;
                if (span.Start + span.Length <= lineStart) continue;

                var start = Math.Max(span.Start, lineStart);
                var end = Math.Min(span.Start + span.Length, lineEnd);
                if (start >= end) continue;

                ChangeLinePart(start, end, e =>
                {
                    if (e.TextRunProperties is VisualLineElementTextRunProperties tp)
                        tp.SetForegroundBrush(span.Brush);
                });
            }
        }
    }

    }

    // =======================================================
    // Phase 1: 实时诊断着色器 — 编译错误行标记为红色背景
    // =======================================================

    /// <summary>
    /// 编译错误标记着色器：将 _currentDiagnostics 中的错误位置标记为浅红背景。
    /// 仅设置背景色，不覆盖 xshd 语法颜色（保留关键字蓝、类型青、方法紫）。
    /// 运行在 Layer 1 内置高亮之后，通过 DocumentColorizingTransformer 增量覆盖。
    /// </summary>
    internal sealed class ErrorMarkerColorizer : DocumentColorizingTransformer
    {
        private readonly Func<IReadOnlyList<Diagnostic>> _getDiagnostics;
        private static readonly Brush ErrorBgBrush = FrozenBrush(0xFF, 0xE0, 0xE0);      // 浅红背景
        private static readonly Brush DarkErrorBgBrush = FrozenBrush(0x5C, 0x00, 0x00);  // 深红背景（暗色主题）

        private readonly bool _isDark;

        public ErrorMarkerColorizer(Func<IReadOnlyList<Diagnostic>> getDiagnostics, bool isDark)
        {
            _getDiagnostics = getDiagnostics;
            _isDark = isDark;
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            var diagnostics = _getDiagnostics();
            if (diagnostics == null || diagnostics.Count == 0) return;

            var brush = _isDark ? DarkErrorBgBrush : ErrorBgBrush;
            var lineStart = line.Offset;
            var lineEnd = line.EndOffset;

            foreach (var diag in diagnostics)
            {
                // 过滤"引用缺失导致的级联误报"：缺 using/缺引用的错误不应让整行变红
                // CS0246: 找不到类型/命名空间  CS0234: 类型或命名空间不存在  CS0103: 名称不存在于当前上下文
                if (diag.Id is "CS0246" or "CS0234" or "CS0103") continue;

                var span = diag.Location.SourceSpan;
                if (span.Start >= lineEnd) continue;
                if (span.End <= lineStart) continue;

                var start = Math.Max(span.Start, lineStart);
                var end = Math.Min(span.End, lineEnd);
                if (start >= end) continue;

                ChangeLinePart(start, end, e =>
                {
                    if (e.TextRunProperties is VisualLineElementTextRunProperties tp)
                    {
                        // ★ 背景色覆盖，保留 xshd 语法前景色
                        tp.SetBackgroundBrush(brush);
                    }
                });
            }
        }

        private static Brush FrozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            if (brush.CanFreeze) brush.Freeze();
            return brush;
        }
    }
}




