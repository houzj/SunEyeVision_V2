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
using System.Windows.Controls;
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
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.SignatureHelp;
using Microsoft.CodeAnalysis.Text;

using CommonControl;
using SunEyeVision.Tool.Script.Completion;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Themes;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using LogEntry = SunEyeVision.Tool.Script.Models.LogEntry;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Tool.Script.Models;
using SunEyeVision.Tool.Script.ScriptEngine;
using SunEyeVision.Plugin.SDK.Core;

namespace SunEyeVision.Tool.Script.Views
{
    public partial class ScriptDebugControl : IToolEditorCommitParticipant
    {
        public IEnumerable<string> VariableNames => _parameters.Inputs.Select(f => f.Name).Concat(_parameters.Outputs.Select(f => f.Name));
        public bool TryValidateEditor(out string error) => _parameters.TryPublishIoContract(out error);
        public bool TryCommitEditor(out string error)
        {
            if (!InputTabControl.TryCommitEdits(out error) || !OutputTabControl.TryCommitEdits(out error)) return false;
            SyncCodeToParameters();
            if (!_parameters.TryPublishIoContract(out error)) { UpdateStatus(error); return false; }
            OnContractChanged();
            return true;
        }
        private ScriptParameters _parameters = new();
        private readonly ObservableCollection<LogEntry> _logEntries = new();
        private ScriptTool? _scriptTool;
        private CancellationTokenSource? _viewCts;
        private bool _contractRefreshPending;
        private string? _analysisGeneratedCode;
        private string? _observedIoIdentity;
        public sealed record CompilationTiming(string Key, bool CacheHit, bool JoinedInFlight, double ReferenceMs,
            double ParseMs, double EmitMs, double WaitMs, double TotalMs);
        public CompilationTiming? LastCompilationTiming { get; private set; }
        private void RecordCompilation(ScriptCompiler.CompileResult result) => LastCompilationTiming = new(result.CacheKey,
            result.CacheHit, result.JoinedInFlight, result.ReferenceMs, result.ParseMs, result.EmitMs, result.WaitMs, result.TotalMs);

        // 编辑器能力（workspace/高亮/诊断/补全/签名）已全部封装在公共内核 ScriptEditorCore（CommonControl），
        // 本控件组合 EditorCore 并注入引用/代码/图标/语法高亮来源。变量面板/执行/日志保留在本工具层。

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

            // 编辑器能力（语义高亮/补全/签名/诊断/workspace）全部封装在公共内核 ScriptEditorCore。
            // 本控件仅注入工具特有的引用/代码/图标来源，内核自动完成编辑器功能。
            EditorCore.SetCompletionIconProvider(item => item.GetGlyph().ToImageSource());
            EditorCore.SetReferenceSnapshotProvider(() => _parameters.CaptureReferences().AnalysisReferences);
            EditorCore.SetAnalysisValidator(() => _parameters.TryPublishIoContract(out _));
            EditorCore.SetAsyncReferenceSnapshotProvider(token =>
            {
                var names = _parameters.AssemblyReferences.ToArray();
                var directory = _parameters.AssemblyReferenceFullPath;
                return Task.Run(() => ScriptReferenceSnapshot.Capture(names, directory).AnalysisReferences, token);
            });
            EditorCore.SetCodeSource(() => _parameters.GeneratedCode, () => EditorCore.GetCode());
            EditorCore.SetDocumentProvider(() => EditorCore.Workspace?.CurrentSolution.GetDocument(EditorCore.UserDocumentId));
            EditorCore.SetSuppressDiagnostic(true);   // workspace 就绪前抑制诊断

            // 初始化变量配置面板（输入/输出 Tab）
            // 上游节点数据源在 SetDataProvider 中通过基类缓存桥接（避免此处硬编码 null）
            InitializeTabs();

            // 关闭埋点：窗口关闭（含 X 关闭）时控件卸载，记录当前是否已同步代码。
            // 用于定位"修改后未执行直接关闭 → 重新打开代码丢失"的根因。
            Unloaded += OnControlUnloaded;
            Loaded += OnViewLoaded;

            perfSw.Stop();
            PluginLogger.Info($"[Perf] ScriptDebugControl 构造函数: {perfSw.ElapsedMilliseconds}ms", GetType().Name);
        }

        /// <summary>
        /// 控件卸载（窗口关闭）时统一同步代码（含 X 关闭路径）。
        /// 修复：X 关闭不触发 OnConfirmCommand/ExecuteTool，导致编辑器最新代码未写回节点参数，
        /// 重新打开调试窗口时读到旧代码丢失。此处兜底同步，保证任何关闭方式都保存最新代码。
        /// </summary>
        private void OnControlUnloaded(object sender, RoutedEventArgs e)
        {
            _viewCts?.Cancel(); _viewCts?.Dispose(); _viewCts = null;
            _parameters.InputContractChanged -= OnContractChanged;
            _parameters.OutputContractChanged -= OnContractChanged;
            _parameters.PropertyChanged -= OnParametersEnvironmentChanged;
            var editorLen = EditorCore.GetCode()?.Length ?? 0;
            var paramLen = _parameters.ScriptCode?.Length ?? 0;
            var synced = EditorCore.GetCode() == _parameters.ScriptCode;
            PluginLogger.Info(
                $"[关闭路径] 控件卸载(窗口关闭) | 编辑器代码长度={editorLen}, 参数代码长度={paramLen}, 已同步={synced}",
                GetType().Name);

            // 未同步则补同步：编辑器最新代码写回节点参数，避免关闭后代码丢失
            if (!synced)
            {
                SyncCodeToParameters();
                PluginLogger.Info($"[关闭路径] X关闭补同步代码: 长度={EditorCore.GetCode()?.Length ?? 0}", GetType().Name);
            }

            // 关闭时预编译最新代码：保证之后工作流执行命中缓存零编译（编译与执行解耦）
            // 关闭保存编辑内容，不等待Emit。
        }

        /// <summary>
        /// 构建 workspace 引用来源：按当前节点 AssemblyReferences（唯一真相源）组装，
        /// 经 ResolveAssemblyPath 逐个解析（BCL/宿主锚定/ScriptRefs），与编译引用保持一致。
        /// 供内核 ScriptEditorCore.SetReferenceProvider 使用。
        /// </summary>
        public override void SetParameters(ToolParameters parameters)
        {
            base.SetParameters(parameters);
            if (parameters is ScriptParameters scriptParams)
            {
                _parameters.InputContractChanged -= OnContractChanged;
                _parameters.OutputContractChanged -= OnContractChanged;
                _parameters.PropertyChanged -= OnParametersEnvironmentChanged;
                _parameters = scriptParams;
                _analysisGeneratedCode = null;
                _parameters.TryPublishIoContract(out _);
                _observedIoIdentity = _parameters.IoIdentity;
                _parameters.PropertyChanged += OnParametersEnvironmentChanged;

                // 订阅输入/输出契约变化：改输入输出类型/增删变量后重建 workspace，
                // 让诊断器用最新字段类型（修复"改类型后诊断仍报旧类型错误"）。
                scriptParams.InputContractChanged += OnContractChanged;
                scriptParams.OutputContractChanged += OnContractChanged;

                // 加载代码到内核编辑器（补全/诊断/workspace 由内核统一处理）
                EditorCore.SetSuppressDiagnostic(true);   // 加载代码时抑制诊断
                EditorCore.LoadCode(_parameters.ScriptCode);
                InitializeTabs();

                // workspace 重建 + 语义预热改由 InitializeAsync（窗口 Show 前被 await）统一触发，
                // 实现"打开即着色、无白色过渡"。此处不再异步后置。
                // 注意：调试模式（无 InitializeAsync 调用路径）仍需显式初始化，见调试入口处。

                // 迁移保障：代码引用了 Halcon 但引用列表缺 halcondotnetxl → 提示补引用（rule-015 不静默）
                CheckHalconReference();

                // 参数注入完成 → 状态切到"就绪"
                UpdateStatus("语义准备中，可编辑");
                if (IsLoaded) OnViewLoaded(this, new RoutedEventArgs());
            }
        }

        /// <summary>
        /// 异步初始化（窗口 Show 前由宿主 await）：重建 workspace + 完成语义预热，
        /// 实现"打开即着色、无白色过渡"。覆盖基类空实现。
        /// </summary>
        public override Task InitializeAsync()
        {
            UpdateStatus("语义准备中，可编辑");
            return Task.CompletedTask;
        }

        private async void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            _parameters.InputContractChanged -= OnContractChanged; _parameters.InputContractChanged += OnContractChanged;
            _parameters.OutputContractChanged -= OnContractChanged; _parameters.OutputContractChanged += OnContractChanged;
            _parameters.PropertyChanged -= OnParametersEnvironmentChanged; _parameters.PropertyChanged += OnParametersEnvironmentChanged;
            _viewCts?.Cancel(); _viewCts?.Dispose(); _viewCts = new CancellationTokenSource();
            var token = _viewCts.Token;
            try
            {
                await Dispatcher.Yield(DispatcherPriority.Background);
                if (!_parameters.TryPublishIoContract(out var error)) { UpdateStatus(error); return; }
                await EditorCore.PrepareWorkspaceAsync(token);
                token.ThrowIfCancellationRequested();
                _analysisGeneratedCode = _parameters.GeneratedCode;
                TryPrefillInputMonitor(); TryPrefillOutputMonitor();
                await EditorCore.WaitForSemanticReadyAsync();
                token.ThrowIfCancellationRequested();
                EditorCore.ScheduleDiagnosticAsync();
                UpdateStatus("就绪");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!token.IsCancellationRequested) UpdateStatus("准备失败: " + ex.Message); }
        }

        /// <summary>
        /// 打开调试窗口时主动查询当前节点已保存的执行结果（SSOT），填充输出监视面板。
        /// 与 <see cref="OnExternalResultArrived"/> 复用同一查询链路，避免打开窗口时输出空白
        /// （输入有预填充、输出没有 → 两者不对称）。防御：无节点 ID/无 DataProvider/结果为空/异常 → 静默跳过。
        /// </summary>
        private void TryPrefillOutputMonitor()
        {
            try
            {
                var nodeId = GetNodeIdFromCurrentNode();
                if (string.IsNullOrEmpty(nodeId) || DataProvider is not IDataSourceQueryService queryService)
                    return;

                var context = queryService.GetNodeContext(nodeId);
                if (context?.Result is ScriptResults result)
                {
                    OutputTabControl.ValueSource = FilterDefaultValues(result.OutputValues);
                    PluginLogger.Info($"[{nameof(ScriptDebugControl)}] 输出预填充 {result.OutputValues.Count} 个值",
                        nameof(ScriptDebugControl));
                }
            }
            catch (Exception ex)
            {
                // 预填充失败不阻塞打开窗口（rule-003 日志不抛）；执行时仍会走完整逻辑
                PluginLogger.Info($"[{nameof(ScriptDebugControl)}] 输出预填充跳过: {ex.Message}",
                    nameof(ScriptDebugControl));
            }
        }

        /// <summary>
        /// 打开调试窗口时主动解析一次输入绑定，将已存在的上游值（如工作流执行后保留的节点结果）
        /// 填充到输入监视面板（监控模式），无需点击执行即可看到输入值。
        /// 防御：无输入配置/无上游节点结果/解析异常 → 静默跳过（执行时仍会正常解析）。
        /// </summary>
        private void TryPrefillInputMonitor()
        {
            try
            {
                if (_parameters == null || _parameters.Inputs == null || _parameters.Inputs.Count == 0)
                    return;

                var request = CreateLiveExecutionRequest(_parameters, null);
                var nodeResults = request.NodeResults;
                var globalVars = request.GlobalVariables;
                var resolved = ScriptBindingResolver.Resolve(
                    _parameters.Inputs,
                    globalVars,
                    nodeResults);

                InputTabControl.ValueSource = resolved;
                PluginLogger.Info($"[{nameof(ScriptDebugControl)}] 输入预填充 {resolved.Count} 个值",
                    nameof(ScriptDebugControl));
            }
            catch (Exception ex)
            {
                // 预填充失败不阻塞打开窗口（rule-003 日志不抛）；执行时仍会走完整解析并报错
                PluginLogger.Info($"[{nameof(ScriptDebugControl)}] 输入预填充跳过: {ex.Message}",
                    nameof(ScriptDebugControl));
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
            // 退出编辑模式后按最新参数（含生成的字段声明）重建 workspace
            OnContractChanged();
        }

        protected override void OnConfirmCommand(object sender, ExecutedRoutedEventArgs e)
        {
            // 确定按钮路径：确认关闭前保存最新代码
            PluginLogger.Info("[关闭路径] 确定按钮 OnConfirmCommand 触发，先同步代码", GetType().Name);
            SyncCodeToParameters();
            // 确认关闭前预编译：代码变化后的最新代码编译入缓存，保证之后工作流执行命中缓存零编译
            if (!TryCommitEditor(out var error)) { UpdateStatus(error); e.Handled = true; return; }
            ExitEditMode();
            base.OnConfirmCommand(sender, e);
        }

        protected override object ExecuteTool() => throw new InvalidOperationException("脚本执行请使用异步入口。");

        protected override async Task<object> ExecuteToolAsync()
        {
            if (!TryCommitEditor(out var commitError)) throw new InvalidOperationException(commitError);
            var token = _viewCts?.Token ?? CancellationToken.None;
            await EditorCore.PrepareWorkspaceAsync(token);
            var session = EditorCore.AnalysisSession;
            var stamp = session.Capture();
            _logEntries.Clear();
            // 清除所有监视数据（输入 + 输出）
            ClearAllMonitorValues();

            _scriptTool ??= new ScriptTool();
            var request = CreateLiveExecutionRequest(_parameters, null);
            var execution = (ScriptParameters)request.Parameters;
            execution.IsDebugMode = true;
            UpdateStatus("排队/编译中，可继续编辑");
            var compiled = await new ScriptCompiler().CompileAsync(new[] { execution.GeneratedCode, execution.ScriptCode },
                "ScriptDebug", execution.AssemblyReferenceFullPath, execution.AssemblyReferences, token);
            RecordCompilation(compiled);
            if (!await IsCurrentCompilationAsync(compiled, execution, stamp, token))
                throw new OperationCanceledException("编辑内容或引用已变化，请重新执行当前版本。");
            if (!compiled.Success)
            {
                var failure = new ScriptResults { CompileErrors = compiled.Diagnostics };
                failure.SetError(string.Join("; ", compiled.Diagnostics));
                foreach (var error in compiled.Diagnostics) _logEntries.Add(new LogEntry { Text = error, IsError = true });
                UpdateStatus("编译失败"); return failure;
            }
            var executor = new ToolExecutor(new ParameterResolver(), VisionLogger.Instance);
            execution.PreparedCompilation = compiled;
            execution.ExecutionAdmission = () => !token.IsCancellationRequested && session.IsCurrent(stamp);
            var toolResult = await Task.Run(async () =>
            {
                token.ThrowIfCancellationRequested();
                if (!session.IsCurrent(stamp)) throw new OperationCanceledException("排队请求已过时。");
                return await executor.ExecuteAsync(_scriptTool, request);
            }, token);
            if (toolResult is not ScriptResults result)
                throw new InvalidOperationException(toolResult.ErrorMessage ?? "脚本工具未返回有效结果。");

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
            if (token.IsCancellationRequested || !IsLoaded) return result;

            // 收集编译错误到 UI 日志面板
            foreach (var error in result.CompileErrors)
                _logEntries.Add(new LogEntry { Text = error, IsError = true });

            // 收集运行日志
            foreach (var log in result.LogMessages)
                _logEntries.Add(new LogEntry { Text = log, IsError = false });

            // ★ 数据驱动：设置输出数据源，过滤掉未赋值的默认值
            OutputTabControl.ValueSource = FilterDefaultValues(result.OutputValues);

            // ★ 数据驱动：设置输入数据源
            InputTabControl.ValueSource = execution.ResolvedInputValues;

            CompileTimeLabel.Content = $"编译: {result.CompileTimeMs}ms";
            ExecTimeLabel.Content = $"执行: {result.ExecutionTimeMs}ms";
            UpdateStatus(result.IsSuccess ? "成功" : "失败");

            return result;
        }

        protected override bool CanExecuteTool()
        {
            return !string.IsNullOrWhiteSpace(EditorCore.GetCode());
        }

        /// <summary>
        /// 工作流引擎执行完成后通知。
        /// CurrentNode 是 UI 层的 WorkflowNode（工具层无引用 UI 程序集），
        /// 通过反射/属性检测（duck typing）获取 Parameters，通过 DSQS 查询 SSOT 获取输出结果。
        /// </summary>
        public override void OnExternalResultArrived()
        {
            TryPrefillInputMonitor();

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
            var brushKey = status == "成功"
                ? SdkThemeKeys.SuccessBrush
                : status == "失败"
                    ? SdkThemeKeys.ErrorBrush
                    : SdkThemeKeys.TextDisabledBrush;
            StatusLabel.SetResourceReference(Control.ForegroundProperty, brushKey);
        }

        /// <summary>
        /// 迁移保障：检测代码引用了 Halcon（HOperatorSet/using HalconDotNet）但当前节点引用列表缺 halcondotnetxl。
        /// 缺失 → 黄字日志提示补引用（rule-015 不静默），不自动添加（用户决定是否启用 halcon）。
        /// </summary>
        private void CheckHalconReference()
        {
            if (_parameters == null) return;
            var code = _parameters.ScriptCode ?? string.Empty;

            // 引用列表含 halcondotnetxl 则无需提示
            bool hasHalconRef = _parameters.AssemblyReferences.Any(n =>
                n.Equals("halcondotnetxl", StringComparison.OrdinalIgnoreCase));
            if (hasHalconRef) return;

            // 代码是否用到了 Halcon（粗检：HOperatorSet 或 using HalconDotNet）
            bool usesHalcon = code.Contains("HOperatorSet", StringComparison.Ordinal)
                || code.Contains("using HalconDotNet", StringComparison.Ordinal)
                || code.Contains("HalImage", StringComparison.Ordinal);

            if (usesHalcon)
            {
                PluginLogger.Warning(
                    "脚本代码引用了 Halcon 算子（HOperatorSet/HalImage），但当前节点的程序集引用列表未包含 halcondotnetxl。" +
                    "请点击\"引用程序集\"勾选 halcondotnetxl 后重新编译。",
                    nameof(ScriptDebugControl));
            }
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

        /// <summary>
        /// 统一输出面板的双击处理：跳转到编译错误对应行号。
        /// 错误行号已来自用户文档语法树，直接对应编辑器行号，无需偏移。
        /// </summary>
        private void LogListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LogListBox.SelectedItem is LogEntry entry && entry.IsError)
            {
                var match = Regex.Match(entry.Text, @"行 (\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var lineNumber))
                {
                    if (lineNumber < 1) lineNumber = 1;
                    var editor = EditorCore.Editor;
                    editor.TextArea.Caret.Line = lineNumber;
                    editor.TextArea.Caret.Column = 1;
                    editor.ScrollToLine(lineNumber);
                    editor.TextArea.Caret.BringCaretToView();
                    editor.Focus();
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
        /// 引用程序集按钮点击 — 弹出对话框管理当前节点的程序集引用勾选列表。
        /// 勾选变化写回当前节点 AssemblyReferences（唯一真相源），保存后失效缓存 + 重建 workspace。
        /// </summary>
        private void AssemblyRefButton_Click(object sender, RoutedEventArgs e)
        {
            var refDir = _parameters.AssemblyReferenceFullPath;
            Directory.CreateDirectory(refDir);

            // 记录勾选前引用列表，用于检测是否变化
            var oldRefs = _parameters.AssemblyReferences.ToList();

            var dialog = new AssemblyReferenceDialog(refDir, _parameters.AssemblyReferences)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                var refsChanged = !oldRefs.SequenceEqual(
                    _parameters.AssemblyReferences, StringComparer.OrdinalIgnoreCase);

                // ScriptRefs 目录 DLL 变化 → 失效引用缓存；引用列表变化 → 重建 workspace
                // 同名 DLL 内容替换不改变勾选名称，也必须重新核验快照。
                {
                    SharedMetadataReferenceProvider.InvalidateScriptRefs(refDir);
                    EditorCore.RebuildWorkspace(refreshReferences: true);
                    EditorCore.ScheduleDiagnosticAsync();
                }
            }
        }

        /// <summary>
        /// Demo示例按钮 — 将编辑器内容替换为 DemoCode（综合示例代码，方便学习测试）
        /// </summary>
        private void DemoCodeButton_Click(object sender, RoutedEventArgs e)
        {
            EditorCore.LoadCode(ScriptParameters.DemoCode);
            SyncCodeToParameters();
            EditorCore.InitializeWorkspace();
            UpdateStatus("已加载 Demo 示例");
        }

        /// <summary>
        /// 复位最简按钮 — 将编辑器内容替换为 MinimalCode（代码误删或想从头开始）
        /// </summary>
        private void ResetCodeButton_Click(object sender, RoutedEventArgs e)
        {
            EditorCore.LoadCode(ScriptParameters.MinimalCode);
            SyncCodeToParameters();
            EditorCore.InitializeWorkspace();
            UpdateStatus("已复位至最简代码");
        }





        /// <summary>
        /// 将编辑器当前代码同步回工具参数（执行/确认前调用，保证运行时编译用最新代码）。
        /// </summary>
        private void SyncCodeToParameters()
        {
            var newCode = EditorCore.GetCode();
            _parameters.ScriptCode = newCode;
            PluginLogger.Info($"[SyncCodeToParameters] 已同步代码: 长度={newCode?.Length ?? 0}", GetType().Name);
        }

        /// <summary>
        /// 输入/输出契约变化（改类型/增删变量）时重建编辑器 workspace 并重新诊断。
        /// 保证诊断器使用最新字段类型，修复"改类型后仍报旧类型错误、需重新键入才恢复"的问题。
        /// </summary>
        private void OnContractChanged()
        {
            if (!_parameters.TryPublishIoContract(out var immediateError))
            { EditorCore.InvalidateAnalysisEnvironment(); UpdateStatus(immediateError); return; }
            var changedGenerated = _parameters.GeneratedCode;
            if (_observedIoIdentity != _parameters.IoIdentity)
            {
                _observedIoIdentity = _parameters.IoIdentity;
                EditorCore.AnalysisSession.AdvanceDocument();
            }
            if (_contractRefreshPending) return;
            _contractRefreshPending = true;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                _contractRefreshPending = false;
                if (!_parameters.TryPublishIoContract(out var error))
                { EditorCore.InvalidateAnalysisEnvironment(); UpdateStatus(error); return; }
                var generated = _parameters.GeneratedCode;
                if ((_analysisGeneratedCode == generated && EditorCore.IsWorkspaceReady) || !IsLoaded) return;
                try
                {
                    await EditorCore.PrepareWorkspaceAsync(_viewCts?.Token ?? CancellationToken.None);
                    _analysisGeneratedCode = generated;
                    EditorCore.ScheduleDiagnosticAsync();
                    UpdateStatus("就绪");
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { UpdateStatus("准备失败: " + ex.Message); }
            }), DispatcherPriority.Background);
        }

        private async Task<bool> IsCurrentCompilationAsync(ScriptCompiler.CompileResult result, ScriptParameters frozen,
            ScriptAnalysisSession.Stamp stamp, CancellationToken token)
        {
            if (!EditorCore.AnalysisSession.IsCurrent(stamp) || !_parameters.TryPublishIoContract(out _)
                || EditorCore.GetCode() != frozen.ScriptCode || _parameters.GeneratedCode != frozen.GeneratedCode
                || _parameters.IoIdentity != frozen.IoIdentity) return false;
            if (!result.Success && result.ReferenceSnapshot == null) return true;
            var names = _parameters.AssemblyReferences.ToArray();
            var directory = _parameters.AssemblyReferenceFullPath;
            var latest = await Task.Run(() => ScriptReferenceSnapshot.Capture(names, directory), token);
            return !token.IsCancellationRequested && EditorCore.AnalysisSession.IsCurrent(stamp)
                && _parameters.TryPublishIoContract(out _) && EditorCore.GetCode() == frozen.ScriptCode
                && _parameters.GeneratedCode == frozen.GeneratedCode && _parameters.IoIdentity == frozen.IoIdentity
                && _parameters.AssemblyReferences.SequenceEqual(names) && _parameters.AssemblyReferenceFullPath == directory
                && latest.Identity == result.ReferenceSnapshot?.Identity;
        }

        private void OnParametersEnvironmentChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ScriptParameters.AssemblyReferences) or nameof(ScriptParameters.AssemblyReferenceDir))
                EditorCore.InvalidateAnalysisEnvironment();
        }

        public async Task<IReadOnlyList<string>> PreCompileAsync(CancellationToken token = default)
        {
            if (!TryCommitEditor(out var error)) { UpdateStatus(error); return new[] { error }; }
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _viewCts?.Token ?? CancellationToken.None);
            token = linked.Token;
            await EditorCore.PrepareWorkspaceAsync(token);
            var stamp = EditorCore.AnalysisSession.Capture();
            var frozen = (ScriptParameters)ToolExecutionPreparation.CopyParameters(_parameters);
            UpdateStatus("排队/编译中，可继续编辑");
            var result = await new ScriptCompiler().CompileAsync(new[] { frozen.GeneratedCode, frozen.ScriptCode }, "ScriptPrecompile",
                frozen.AssemblyReferenceFullPath, frozen.AssemblyReferences, token);
            RecordCompilation(result);
            if (!await IsCurrentCompilationAsync(result, frozen, stamp, token)) throw new OperationCanceledException("编译结果已过时。");
            CompileTimeLabel.Content = result.CacheHit ? $"缓存命中；等待 {result.WaitMs:F1}ms"
                : $"编译生成: {result.EmitMs:F1}ms；等待 {result.WaitMs:F1}ms";
            UpdateStatus(result.Success ? "预编译成功" : "编译失败");
            foreach (var diagnostic in result.Diagnostics) _logEntries.Add(new LogEntry { Text = diagnostic, IsError = true });
            return result.Diagnostics;
        }

        private async void PreCompileButton_Click(object sender, RoutedEventArgs e)
        {
            try { await PreCompileAsync(); }
            catch (OperationCanceledException) { if (IsLoaded) UpdateStatus("编译等待已取消/过时"); }
            catch (Exception ex) { if (IsLoaded) UpdateStatus("编译失败: " + ex.Message); }
        }

    }

}
