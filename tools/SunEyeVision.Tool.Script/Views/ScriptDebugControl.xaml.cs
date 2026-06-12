using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using LogEntry = SunEyeVision.Tool.Script.Models.LogEntry;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Tool.Script.Models;
using SunEyeVision.Tool.Script.ScriptEngine;

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
        private Document? _document;
        private CompletionWindow? _completionWindow;

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
            UpdateStatus("就绪");

            // Phase 1: 应用原生 C# 浅色语法高亮
            CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");

            // Phase 2: 初始化 Roslyn 补全工作区
            InitializeWorkspace();

            // 自动补全事件
            CodeEditor.TextArea.TextEntered += TextArea_TextEntered;
            CodeEditor.TextArea.TextEntering += TextArea_TextEntering;

            // 控件卸载时清理工作区
            Unloaded += (_, _) =>
            {
                CodeEditor.TextArea.TextEntered -= TextArea_TextEntered;
                CodeEditor.TextArea.TextEntering -= TextArea_TextEntering;
                _workspace?.Dispose();
            };
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
                _parameters.AssemblyReferences,
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
                var match = Regex.Match(entry.Text, @"line (\d+)");
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
        /// 引用程序集按钮点击 — 弹出对话框管理 DLL 引用
        /// </summary>
        private void AssemblyRefButton_Click(object sender, RoutedEventArgs e)
        {
            var currentRefs = new ObservableCollection<string>(
                _parameters.AssemblyReferences ?? new List<string>());

            var dialog = new AssemblyReferenceDialog(currentRefs)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                _parameters.AssemblyReferences = dialog.GetReferences().ToList();
            }
        }

        protected override void OnConfirmCommand(object sender, ExecutedRoutedEventArgs e)
        {
            SyncCodeToParameters();
            base.OnConfirmCommand(sender, e);
        }

        // =======================================================
        // Phase 2: Roslyn 语义自动补全
        // =======================================================

        /// <summary>
        /// 初始化 AdhocWorkspace，添加与 ScriptCompiler 一致的引用
        /// </summary>
        private void InitializeWorkspace()
        {
            _workspace = new AdhocWorkspace();

            var project = _workspace.AddProject("ScriptCompletion", LanguageNames.CSharp);

            project = project.WithCompilationOptions(
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithAllowUnsafe(true));

            // 添加与 ScriptCompiler 相同的元数据引用
            foreach (var asm in ScriptCompiler.GetDefaultAssemblies())
            {
                if (!string.IsNullOrEmpty(asm.Location))
                {
                    project = project.AddMetadataReference(
                        MetadataReference.CreateFromFile(asm.Location));
                }
            }

            // 创建空文档（首次补全时填充代码文本）
            _document = _workspace.AddDocument(project.Id, "Script.cs", SourceText.From(""));
        }

        /// <summary>
        /// 文本输入后触发补全（点号、字母输入）
        /// </summary>
        private void TextArea_TextEntered(object? sender, TextCompositionEventArgs e)
        {
            if (e.Text == ".")
            {
                _ = ShowCompletionAsync();
            }
        }

        /// <summary>
        /// 文本输入中处理补全窗口的插入确认
        /// </summary>
        private void TextArea_TextEntering(object? sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && _completionWindow != null)
            {
                // 补全窗口打开时，非字母数字字符触发插入
                if (!char.IsLetterOrDigit(e.Text[0]))
                {
                    _completionWindow.CompletionList.RequestInsertion(e);
                }
            }
        }

        /// <summary>
        /// 异步请求 Roslyn 补全并显示 CompletionWindow
        /// </summary>
        private async Task ShowCompletionAsync()
        {
            if (_workspace == null || _document == null) return;

            try
            {
                var code = CodeEditor.Text;
                var caretOffset = CodeEditor.TextArea.Caret.Offset;

                // 更新工作区文档的源代码文本
                var sourceText = SourceText.From(code);
                _document = _document.WithText(sourceText);

                var completionService = CompletionService.GetService(_document);
                if (completionService == null) return;

                var results = await completionService.GetCompletionsAsync(
                    _document, caretOffset);

                if (results == null || results.ItemsList.Count == 0) return;

                // 显示补全窗口
                var window = new CompletionWindow(CodeEditor.TextArea);
                var data = window.CompletionList.CompletionData;

                foreach (var item in results.ItemsList)
                {
                    data.Add(new RoslynCompletionData(item));
                }

                window.Closed += (_, _) => _completionWindow = null;
                window.Show();
                _completionWindow = window;
            }
            catch
            {
                // Roslyn 补全失败时静默忽略（不影响编辑体验）
            }
        }

        // =======================================================
        // CompletionData 包装 — 桥接 Roslyn CompletionItem -> AvalonEdit ICompletionData
        // =======================================================

        /// <summary>
        /// 将 Roslyn CompletionItem 包装为 AvalonEdit ICompletionData
        /// </summary>
        private sealed class RoslynCompletionData(CompletionItem item) : ICompletionData
        {
            public System.Windows.Media.ImageSource? Image => null;

            public string Text => item.DisplayText;

            public object Content => item.DisplayText;

            public object? Description => item.InlineDescription;

            public double Priority => 0;

            public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
            {
                textArea.Document.Replace(completionSegment, item.DisplayText);
            }
        }
    }
}

