using System;
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
using System.Runtime.Loader;
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
using Microsoft.CodeAnalysis.Classification;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using LogEntry = SunEyeVision.Tool.Script.Models.LogEntry;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Tool.Script.Models;
// ScriptEngine 命名空间通过父命名空间 SunEyeVision.Tool.Script 隐式可用

namespace SunEyeVision.Tool.Script.Views
{
    public partial class ScriptDebugControl
    {
        private ScriptParameters _parameters = new();
        private readonly ObservableCollection<ScriptParamItem> _inputVariables = new();
        private readonly ObservableCollection<ScriptParamItem> _outputVariables = new();
        private readonly ObservableCollection<LogEntry> _logEntries = new();

        // Roslyn completion workspace
        private AdhocWorkspace? _workspace;
        private DocumentId? _documentId;                              // 不可变标识，仅 RebuildWorkspace 赋值
        private CompletionWindow? _completionWindow;
        private CompletionItem[] _currentItems;                        // 唯一数据源（单层，无后台刷新）
        private CancellationTokenSource? _throttleCts;                // 50ms 节流令牌

        // Phase 1: 实时诊断
        private CancellationTokenSource? _diagnosticsCts;             // 500ms 诊断节流
        private IReadOnlyList<Diagnostic> _currentDiagnostics = [];   // 当前错误列表

        // Phase 2: 语义高亮缓存
        private IReadOnlyList<ClassifiedSpan>? _classificationSpans;

        // 进程生命周期内共享的 MefHostServices 和默认程序集引用（首次慢，后续快）
        private static MefHostServices? _sharedMefHost;
        private static List<MetadataReference>? _defaultReferences;

        private static MefHostServices GetSharedMefHost()
        {
            if (_sharedMefHost == null)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                _sharedMefHost = MefHostServices.Create(MefHostServices.DefaultAssemblies);
                sw.Stop();
                PluginLogger.Info($"[补全] MefHostServices 首次初始化: {sw.ElapsedMilliseconds}ms",
                    nameof(ScriptDebugControl));
            }
            else
            {
                PluginLogger.Info("[补全] MefHostServices 缓存命中", nameof(ScriptDebugControl));
            }
            return _sharedMefHost;
        }

        /// <summary>
        /// 判断程序集是否与脚本编译/补全相关（排除 WPF、Roslyn 自身、Win32/UI 等）
        /// </summary>
        private static bool IsScriptRelevantAssembly(string? assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName)) return false;

            if (assemblyName is "System.Runtime" or "System.Private.CoreLib"
                or "netstandard" or "mscorlib")
                return true;

            if (assemblyName.StartsWith("System.") || assemblyName == "System")
                return !assemblyName.StartsWith("System.Windows")
                    && !assemblyName.StartsWith("System.Xaml");

            if (assemblyName.StartsWith("Microsoft."))
                return !assemblyName.StartsWith("Microsoft.Win32")
                    && !assemblyName.StartsWith("Microsoft.UI")
                    && assemblyName != "Microsoft.CodeAnalysis"
                    && !assemblyName.StartsWith("Microsoft.CodeAnalysis.");

            if (assemblyName.StartsWith("SunEyeVision."))
                return true;

            return !assemblyName.StartsWith("Presentation")
                && !assemblyName.StartsWith("UIAutomation")
                && assemblyName != "WindowsBase"
                && assemblyName != "Accessibility"
                && assemblyName != "DirectWriteForwarder"
                && assemblyName != "ReachFramework"
                && !assemblyName.StartsWith("Avalon");
        }

        private static List<MetadataReference> GetDefaultReferences()
        {
            if (_defaultReferences == null)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var refs = new List<MetadataReference>();
                int totalAsm = 0, filteredCount = 0;
                foreach (var asm in AssemblyLoadContext.Default.Assemblies)
                {
                    totalAsm++;
                    if (asm.IsDynamic) continue;
                    if (!IsScriptRelevantAssembly(asm.GetName().Name)) continue;
                    filteredCount++;
                    try { refs.Add(MetadataReference.CreateFromFile(asm.Location)); }
                    catch { }
                }
                _defaultReferences = refs;
                sw.Stop();
                PluginLogger.Info($"[补全] 程序集引用首次初始化: ALC总={totalAsm}, 过滤后={filteredCount}, 成功创建={refs.Count}, 耗时={sw.ElapsedMilliseconds}ms",
                    nameof(ScriptDebugControl));
            }
            else
            {
                PluginLogger.Info($"[补全] 程序集引用缓存命中: {_defaultReferences.Count} 个引用",
                    nameof(ScriptDebugControl));
            }
            return _defaultReferences;
        }

        public ObservableCollection<ScriptParamItem> InputVariables => _inputVariables;
        public ObservableCollection<ScriptParamItem> OutputVariables => _outputVariables;
        public ObservableCollection<LogEntry> LogEntries => _logEntries;

        public string ScriptCode
        {
            get => _parameters.ScriptCode;
            set { _parameters.ScriptCode = value; OnPropertyChanged(nameof(ScriptCode)); }
        }

        public override bool ShowMaximizeButton => true;
        public override Size PreferredDebugWindowSize => new Size(1000, 650);

        public ScriptDebugControl()
        {
            InitializeComponent();
            InputDataGrid.ItemsSource = _inputVariables;
            OutputDataGrid.ItemsSource = _outputVariables;
            LogListBox.ItemsSource = _logEntries;
            UpdateStatus("初始化中...");

            // Phase 1: 语法高亮（自定义 xshd + Roslyn 语义高亮 + 实时错误标记）
            var typeName = GetType().Name;

            // Layer 1: 从输出目录加载自定义 xshd（含 BCL 类型关键词），失败则回退到内置 C# 高亮
            LoadCustomHighlighting();

            // Layer 2a: Roslyn 语义高亮着色器（替代正则猜测的 ScriptSemanticColorizer）
            var semanticColorizer = new RoslynSemanticColorizer(
                () => _classificationSpans,
                RoslynClassificationColors.GetBrush);
            CodeEditor.TextArea.TextView.LineTransformers.Add(semanticColorizer);
            PluginLogger.Info("[Highlighting] 已加载 Roslyn 语义高亮着色器", typeName);

            // Layer 2b: 实时错误标记着色器（红色标记编译错误行）
            var errorMarker = new ErrorMarkerColorizer(() => _currentDiagnostics, false);
            CodeEditor.TextArea.TextView.LineTransformers.Add(errorMarker);
            PluginLogger.Info("[Highlighting] 已加载实时错误标记着色器", typeName);

            // 当前行高亮：光标所在行浅蓝色背景，替代纯光标闪烁（提升代码定位体验）
            CodeEditor.Options.HighlightCurrentLine = true;
            CodeEditor.TextArea.TextView.CurrentLineBackground =
                new SolidColorBrush(Color.FromArgb(0x18, 0x00, 0x7A, 0xCC));
            if (CodeEditor.TextArea.TextView.CurrentLineBackground.CanFreeze)
                CodeEditor.TextArea.TextView.CurrentLineBackground.Freeze();
            PluginLogger.Info("[Highlighting] 已启用当前行高亮", typeName);

            // 设置语义高亮主题色（当前为浅色主题，与 xshd 颜色一致）
            RoslynClassificationColors.SetTheme(false);

            // Phase 1: 实时诊断（文本变更 → 500ms 节流 → Roslyn 诊断 → 着色）
            CodeEditor.TextChanged += OnCodeEditorTextChanged;

            // Phase 2: 初始化 Roslyn 补全工作区（同步完成，窗口弹出时已就绪）
            RebuildWorkspace();
            ScheduleSemanticAnalysis();  // 初始化后立即触发语义分析，避免打开编辑器时无语义高亮

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
            };
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
                if (CodeEditor != null)
                {
                    CodeEditor.Text = _parameters.ScriptCode;
                }
                SyncInputVariables();
                SyncOutputVariables();
                RebuildWorkspace();  // Recipe 恢复后重建 Roslyn 工作区，确保补全使用最新引用
                ScheduleSemanticAnalysis();  // 重建后立即触发语义分析，更新高亮
            }
        }




        private void SyncInputVariables()
        {
            _inputVariables.Clear();
            if (_parameters.InputVariables != null)
                foreach (var item in _parameters.InputVariables)
                    _inputVariables.Add(item);
        }

        private void SyncOutputVariables()
        {
            _outputVariables.Clear();
            if (_parameters.OutputVariables != null)
                foreach (var item in _parameters.OutputVariables)
                    _outputVariables.Add(item);
        }

        private void SyncCodeToParameters()
        {
            if (CodeEditor != null)
                _parameters.ScriptCode = CodeEditor.Text;
        }

        protected override object ExecuteTool()
        {
            SyncCodeToParameters();
            _parameters.InputVariables = _inputVariables.ToList();
            _logEntries.Clear();
            foreach (var item in _outputVariables) item.Value = null;

            using var lifecycleManager = new ScriptEngine.ScriptLifecycleManager();
            // Global variable delegates - injected by workflow engine in workflow mode
            Func<string, object?>? getGlobal = null;
            Action<string, object?>? setGlobal = null;

            var errors = lifecycleManager.Execute(
                _parameters.ScriptCode,
                _parameters.AssemblyReferenceFullPath,
                getGlobal, setGlobal);

            // 编译错误用红色标记加入统一输出
            foreach (var error in errors)
                _logEntries.Add(new LogEntry { Text = error, IsError = true });

            // 运行日志用白色标记
            foreach (var log in lifecycleManager.LogMessages)
                _logEntries.Add(new LogEntry { Text = log, IsError = false });

            UpdateOutputValues(lifecycleManager.OutputValues);

            CompileTimeLabel.Content = $"编译: {lifecycleManager.CompileTimeMs}ms";
            ExecTimeLabel.Content = $"执行: {lifecycleManager.ExecutionTimeMs}ms";
            UpdateStatus(errors.Count == 0 ? "成功" : "失败");

            var result = new ScriptResults
            {
                ToolName = "Script", ToolId = "Script", Timestamp = DateTime.Now,
                CompileErrors = errors, CompileTimeMs = lifecycleManager.CompileTimeMs,
                ExecutionTimeMs = lifecycleManager.ExecutionTimeMs,
                LogMessages = lifecycleManager.LogMessages.ToList()
            };
            foreach (var kvp in lifecycleManager.OutputValues)
                result.OutputValues[kvp.Key] = kvp.Value;

            return result;
        }

        protected override bool CanExecuteTool()
        {
            return !string.IsNullOrWhiteSpace(CodeEditor?.Text);
        }

        private void UpdateOutputValues(IReadOnlyDictionary<string, object?> outputValues)
        {
            foreach (var kvp in outputValues)
            {
                var existing = _outputVariables.FirstOrDefault(v => v.Name == kvp.Key);
                if (existing != null)
                    existing.Value = kvp.Value;
                else
                    _outputVariables.Add(new ScriptParamItem
                    {
                        Name = kvp.Key,
                        Value = kvp.Value,
                        TypeName = kvp.Value?.GetType().Name ?? "null"
                    });
            }
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

        private void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteTool();
        }

        private void ClearCodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (CodeEditor != null) { CodeEditor.Text = string.Empty; SyncCodeToParameters(); }
        }

        private void ClearResultButton_Click(object sender, RoutedEventArgs e)
        {
            _logEntries.Clear();
            foreach (var item in _outputVariables) item.Value = null;
            CompileTimeLabel.Content = ""; ExecTimeLabel.Content = "";
            UpdateStatus("就绪");
        }

        private void CodeEditor_LostFocus(object sender, RoutedEventArgs e) => SyncCodeToParameters();

        /// <summary>
        /// 统一输出面板的双击处理：跳转到编译错误对应行号
        /// </summary>
        private void LogListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LogListBox.SelectedItem is LogEntry entry && entry.IsError && CodeEditor != null)
            {
                var match = Regex.Match(entry.Text, @"行 (\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var lineNumber))
                {
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




        protected override void OnConfirmCommand(object sender, ExecutedRoutedEventArgs e)
        {
            SyncCodeToParameters();
            base.OnConfirmCommand(sender, e);
        }

        // =======================================================
        // Phase 2: Roslyn 语义自动补全（50ms 节流 + 单层数据源 + 零闪烁）
        // =======================================================

        /// <summary>
        /// 动态重建工作区（构造函数/SetParameters/用户增删 DLL 后调用）
        /// 放弃旧 workspace，用当前代码文本 + 引用目录下的所有 DLL 重建
        /// </summary>
        private void RebuildWorkspace()
        {
            var currentCode = CodeEditor?.Text ?? "";
            var typeName = GetType().Name;
            var totalSw = System.Diagnostics.Stopwatch.StartNew();

            PluginLogger.Info($"[补全] RebuildWorkspace 开始: 代码长度={currentCode.Length}", typeName);

            _diagnosticsCts?.Cancel();  // 取消正在进行的语义分析，防止访问已 Dispose 的 workspace
            _workspace?.Dispose();

            // 加载 MefHostServices 确保所有 CompletionProvider 可用（缓存机制，首次慢后续快）
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var host = GetSharedMefHost();
            PluginLogger.Info($"[补全] MefHostServices 创建完成: {sw.ElapsedMilliseconds}ms", typeName);

            _workspace = new AdhocWorkspace(host);
            PluginLogger.Info("[补全] AdhocWorkspace 实例已创建", typeName);

            sw.Restart();
            var project = _workspace.AddProject("ScriptCompletion", LanguageNames.CSharp);
            project = project.WithCompilationOptions(
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithAllowUnsafe(true));
            PluginLogger.Info($"[补全] 项目创建完成: {sw.ElapsedMilliseconds}ms", typeName);

            // 扫描引用：默认 ALC 已加载程序集 + ScriptRefs 用户自定义 DLL
            sw.Restart();
            var refDir = _parameters?.AssemblyReferenceFullPath;
            var refCount = 0;

            // 1. 默认 ALC 已加载程序集（缓存 + 过滤，首次慢后续快）
            try
            {
                var defaultRefs = GetDefaultReferences();
                foreach (var mdRef in defaultRefs)
                {
                    project = project.AddMetadataReference(mdRef);
                    refCount++;
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Warning($"[补全] 枚举 ALC 程序集失败: {ex.Message}", typeName);
            }

            // 2. ScriptRefs 目录（用户自定义第三方 DLL）
            if (!string.IsNullOrEmpty(refDir))
            {
                if (!Directory.Exists(refDir))
                {
                    PluginLogger.Warning($"[补全] 引用目录不存在: {refDir}", typeName);
                }
                else
                {
                    var dllFiles = Directory.EnumerateFiles(refDir, "*.dll").ToList();
                    PluginLogger.Info($"[补全] 引用目录扫描到 {dllFiles.Count} 个 DLL: {refDir}", typeName);
                    foreach (var dll in dllFiles)
                    {
                        try
                        {
                            var mdRef = MetadataReference.CreateFromFile(dll);
                            project = project.AddMetadataReference(mdRef);
                            refCount++;
                        }
                        catch (Exception ex)
                        {
                            PluginLogger.Warning($"[补全] 加载 DLL 失败: {dll}, 错误: {ex.Message}", typeName);
                        }
                    }
                }
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
            var document = _workspace.AddDocument(project.Id, "Script.cs",
                SourceText.From(currentCode));
            _documentId = document.Id;
            PluginLogger.Info($"[补全] 文档添加完成，文档 ID={_documentId}: {sw.ElapsedMilliseconds}ms", typeName);

            _currentItems = [];   // 清空缓存，下次输入触发全新解析

            var refs = _workspace.CurrentSolution.Projects.First().MetadataReferences;
            PluginLogger.Info($"[补全] Workspace 重建完成: {refs.Count} 个引用, 代码长度={currentCode.Length}, 总耗时={totalSw.ElapsedMilliseconds}ms", typeName);
        }

        /// <summary>
        /// 统一入口：字母 / 小数点 → 50ms 节流 → 单次 Roslyn 解析 → 单层弹窗
        /// 窗口打开期间：AvalonEdit CompletionList 接管客户端过滤（内置质量评分算法）
        /// </summary>
        private async void TextArea_TextEntered(object? sender, TextCompositionEventArgs e)
        {
            var typeName = GetType().Name;

            if (_workspace == null || _documentId == null)
            {
                PluginLogger.Warning($"[补全] TextEntered 跳过: workspace={_workspace != null}, documentId={_documentId != null}, text='{e.Text}'", typeName);
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
        /// </summary>
        private async Task<ImmutableArray<CompletionItem>> FetchFreshCompletions(char triggerChar = '\0')
        {
            var typeName = GetType().Name;

            if (_workspace == null || _documentId == null)
            {
                PluginLogger.Warning($"[补全] FetchFreshCompletions 跳过: workspace={_workspace != null}, documentId={_documentId != null}", typeName);
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
                // 从 workspace 获取最新 Document，使用局部 doc，不修改任何字段
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var doc = _workspace.CurrentSolution.GetDocument(_documentId);
                if (doc == null)
                {
                    PluginLogger.Warning($"[补全] workspace 中找不到 document: {_documentId}", typeName);
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
        // Phase 2: 语义高亮（Roslyn Classifier.GetClassifiedSpans → 精准着色）
        // 合并为一条 500ms 节流管线，共享同一 workspace 快照
        // =======================================================

        /// <summary>
        /// 编辑器文本变更 → 500ms 节流 → 诊断+语义分析（共享 workspace 快照）
        /// </summary>
        private void OnCodeEditorTextChanged(object? sender, EventArgs e)
        {
            ScheduleSemanticAnalysis();
        }

        private void ScheduleSemanticAnalysis()
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
                        await RunSemanticAnalysisAsync(token);
                }
                catch (OperationCanceledException) { }
            });
        }

        /// <summary>
        /// 更新 workspace 文本 → 获取诊断 → 获取语义分类 → UI 重绘
        /// </summary>
        private async Task RunSemanticAnalysisAsync(CancellationToken token)
        {
            var typeName = GetType().Name;

            if (_workspace == null || _documentId == null)
            {
                PluginLogger.Warning($"[SemanticHighlight] 跳过: workspace={_workspace != null}, documentId={_documentId != null}", typeName);
                return;
            }

            var code = CodeEditor.Text;
            if (string.IsNullOrEmpty(code))
            {
                PluginLogger.Warning("[SemanticHighlight] 跳过: 代码为空", typeName);
                return;
            }

            try
            {
                // 1. 更新 workspace 文本（TryApplyChanges 确保 Document 在 workspace solution 中，
                //    Classifier.GetClassifiedSpansAsync 需要 workspace-backed SemanticModel）
                var sourceText = SourceText.From(code);
                var newSolution = _workspace.CurrentSolution.WithDocumentText(_documentId, sourceText);
                if (!_workspace.TryApplyChanges(newSolution))
                {
                    PluginLogger.Warning("[SemanticHighlight] TryApplyChanges 失败", typeName);
                    return;
                }
                var doc = _workspace.CurrentSolution.GetDocument(_documentId);
                if (doc == null)
                {
                    PluginLogger.Warning("[SemanticHighlight] workspace 中找不到 document", typeName);
                    return;
                }

                // 2. 并行获取诊断 + 语义分类（使用局部 doc，不修改字段）
                var modelTask = doc.GetSemanticModelAsync(token);
                var spansTask = Classifier.GetClassifiedSpansAsync(doc, new TextSpan(0, code.Length), token);

                await Task.WhenAll(modelTask, spansTask);

                if (token.IsCancellationRequested) return;

                var model = modelTask.Result;
                var allSpans = spansTask.Result;

                // Phase 1: 诊断
                var diagnostics = model!.GetDiagnostics(cancellationToken: token)
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToImmutableArray();
                _currentDiagnostics = diagnostics;

                // Phase 2: 语义分类缓存（使用全部 Roslyn 分类，覆盖 Layer 1 的 xshd）
                _classificationSpans = allSpans.ToImmutableArray();

                PluginLogger.Info($"[SemanticHighlight] TotalSpans={allSpans.Count()}, CachedSpans={_classificationSpans.Count}", typeName);
                var typeGroups = _classificationSpans.GroupBy(s => s.ClassificationType)
                    .Select(g => $"{g.Key}={g.Count()}")
                    .ToArray();
                PluginLogger.Info($"[SemanticHighlight] Types: {string.Join(", ", typeGroups)}", typeName);

                // 3. UI 线程重绘
                await Dispatcher.InvokeAsync(() => CodeEditor.TextArea.TextView.Redraw(), DispatcherPriority.Background, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                PluginLogger.Warning($"[ScriptDebug] SemanticAnalysis error: {ex.Message}", typeName);
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
    }

    // =======================================================
    // Phase 1: 实时诊断着色器 — 编译错误行标记为红色文字
    // Phase 2: Roslyn 语义高亮着色器 — 替代正则猜测的 ScriptSemanticColorizer
    // =======================================================

    /// <summary>
    /// 编译错误标记着色器：将 _currentDiagnostics 中的错误位置标记为红色。
    /// 运行在 Layer 1 内置高亮之后，通过 DocumentColorizingTransformer 增量覆盖。
    /// </summary>
    internal sealed class ErrorMarkerColorizer : DocumentColorizingTransformer
    {
        private readonly Func<IReadOnlyList<Diagnostic>> _getDiagnostics;
        private static readonly Brush ErrorBrush = FrozenBrush(0xCC, 0x00, 0x00);
        private static readonly Brush DarkErrorBrush = FrozenBrush(0xF4, 0x47, 0x47);

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

            var brush = _isDark ? DarkErrorBrush : ErrorBrush;
            var lineStart = line.Offset;
            var lineEnd = line.EndOffset;

            foreach (var diag in diagnostics)
            {
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
                        tp.SetForegroundBrush(brush);
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

    /// <summary>
    /// Roslyn 语义着色器：读取预计算的 ClassifiedSpan 缓存，在 AvalonEdit
    /// 内置语法高亮之上增量覆盖语义颜色。
    /// 运行在内置 C# 高亮之后，只覆盖标识符文本范围。
    /// </summary>
    internal sealed class RoslynSemanticColorizer : DocumentColorizingTransformer
    {
        private readonly Func<IReadOnlyList<ClassifiedSpan>?> _getSpans;
        private readonly Func<string, Brush?> _getBrush;

        public RoslynSemanticColorizer(
            Func<IReadOnlyList<ClassifiedSpan>?> getSpans,
            Func<string, Brush?> getBrush)
        {
            _getSpans = getSpans;
            _getBrush = getBrush;
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            var spans = _getSpans();
            if (spans == null || spans.Count == 0) return;

            var lineStart = line.Offset;
            var lineEnd = line.EndOffset;

            // 二分查找第一个 >= lineStart 的 span
            var idx = BinarySearchFirstSpan(spans, lineStart);
            if (idx < 0) return;

            for (int i = idx; i < spans.Count; i++)
            {
                var span = spans[i];
                if (span.TextSpan.Start >= lineEnd) break;  // 超出当前行

                var start = Math.Max(span.TextSpan.Start, lineStart);
                var end = Math.Min(span.TextSpan.End, lineEnd);
                if (start >= end) continue;

                var brush = _getBrush(span.ClassificationType);
                if (brush == null) continue;

                ChangeLinePart(start, end, e =>
                {
                    if (e.TextRunProperties is VisualLineElementTextRunProperties tp)
                        tp.SetForegroundBrush(brush);
                });
            }
        }

        private static int BinarySearchFirstSpan(
            IReadOnlyList<ClassifiedSpan> spans, int lineStart)
        {
            int lo = 0, hi = spans.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (spans[mid].TextSpan.End <= lineStart)
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }
            return lo < spans.Count ? lo : -1;
        }
    }

    /// <summary>
    /// Roslyn 分类类型 → 颜色刷映射（浅色/深色两套）
    /// 语义分类覆盖 Layer 1 语法高亮，语法分类返回 null 让 Layer 1 生效。
    /// </summary>
    public static class RoslynClassificationColors
    {
        // ── 浅色主题 ──
        private static readonly Brush LightKeyword   = FrozenBrush(0x00, 0x00, 0xFF);
        private static readonly Brush LightString    = FrozenBrush(0xA3, 0x15, 0x15);
        private static readonly Brush LightType      = FrozenBrush(0x2B, 0x91, 0xAF);  // 类/结构/接口/枚举/委托
        private static readonly Brush LightMethod    = FrozenBrush(0x74, 0x56, 0x9F);  // 方法/属性
        private static readonly Brush LightField     = FrozenBrush(0xE0, 0x7B, 0x00);  // 字段/枚举成员
        private static readonly Brush LightLocal     = FrozenBrush(0xCC, 0x00, 0x00);  // 局部变量/参数

        // ── 深色主题 ──
        private static readonly Brush DarkKeyword   = FrozenBrush(0x56, 0x9C, 0xD6);
        private static readonly Brush DarkString    = FrozenBrush(0xCE, 0x91, 0x78);
        private static readonly Brush DarkType      = FrozenBrush(0x4E, 0xC9, 0xB0);
        private static readonly Brush DarkMethod    = FrozenBrush(0xDC, 0xDC, 0xAA);
        private static readonly Brush DarkField     = FrozenBrush(0xD7, 0xBA, 0x7D);
        private static readonly Brush DarkLocal     = FrozenBrush(0xF4, 0x47, 0x47);

        private static bool _isDark;

        public static void SetTheme(bool isDark) => _isDark = isDark;

        public static Brush? GetBrush(string classificationType)
        {
            bool dark = _isDark;
            return classificationType switch
            {
                // 语义分类（覆盖 Layer 1 语法高亮的标识符范围）
                ClassificationTypeNames.ClassName         => dark ? DarkType   : LightType,
                ClassificationTypeNames.StructName        => dark ? DarkType   : LightType,
                ClassificationTypeNames.InterfaceName     => dark ? DarkType   : LightType,
                ClassificationTypeNames.EnumName          => dark ? DarkType   : LightType,
                ClassificationTypeNames.DelegateName      => dark ? DarkType   : LightType,
                ClassificationTypeNames.TypeParameterName => dark ? DarkType   : LightType,

                ClassificationTypeNames.MethodName          => dark ? DarkMethod : LightMethod,
                ClassificationTypeNames.ExtensionMethodName => dark ? DarkMethod : LightMethod,
                ClassificationTypeNames.PropertyName        => dark ? DarkMethod : LightMethod,
                ClassificationTypeNames.EventName           => dark ? DarkMethod : LightMethod,

                ClassificationTypeNames.FieldName      => dark ? DarkField : LightField,
                ClassificationTypeNames.EnumMemberName => dark ? DarkField : LightField,
                ClassificationTypeNames.ConstantName   => dark ? DarkField : LightField,

                ClassificationTypeNames.LocalName     => dark ? DarkLocal : LightLocal,
                ClassificationTypeNames.ParameterName => dark ? DarkLocal : LightLocal,

                // 语法分类（覆盖 Layer 1 的 xshd，保持颜色一致）
                ClassificationTypeNames.Keyword        => dark ? DarkKeyword   : LightKeyword,
                ClassificationTypeNames.StringLiteral  => null,  // 字符串由 xshd 处理
                ClassificationTypeNames.Comment        => null,  // 注释由 xshd 处理
                ClassificationTypeNames.NumericLiteral => null,  // 数字由 xshd 处理
                ClassificationTypeNames.Operator       => null,
                ClassificationTypeNames.Punctuation    => null,
                ClassificationTypeNames.WhiteSpace     => null,

                _ => null
            };
        }

        private static Brush FrozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            if (brush.CanFreeze) brush.Freeze();
            return brush;
        }
    }
}

