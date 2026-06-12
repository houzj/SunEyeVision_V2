using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
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
        private readonly ObservableCollection<string> _errorMessages = new();
        private readonly ObservableCollection<string> _logMessages = new();

        public ObservableCollection<ScriptParamItem> InputVariables => _inputVariables;
        public ObservableCollection<ScriptParamItem> OutputVariables => _outputVariables;
        public ObservableCollection<string> ErrorMessages => _errorMessages;
        public ObservableCollection<string> LogMessages => _logMessages;

        public string ScriptCode
        {
            get => _parameters.ScriptCode;
            set { _parameters.ScriptCode = value; OnPropertyChanged(nameof(ScriptCode)); }
        }

        public ScriptDebugControl()
        {
            InitializeComponent();
            InputDataGrid.ItemsSource = _inputVariables;
            OutputDataGrid.ItemsSource = _outputVariables;
            ErrorListBox.ItemsSource = _errorMessages;
            LogListBox.ItemsSource = _logMessages;
            UpdateStatus("就绪");
        }

        public override void SetParameters(ToolParameters parameters)
        {
            base.SetParameters(parameters);
            if (parameters is ScriptParameters scriptParams)
            {
                _parameters = scriptParams;
                if (CodeEditor != null) CodeEditor.Text = _parameters.ScriptCode;
                SyncInputVariables();
                SyncOutputVariables();
                SyncDllReferences();
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

        private void SyncDllReferences()
        {
            DllReferenceList.Items.Clear();
            if (_parameters.AssemblyReferences != null)
                foreach (var dll in _parameters.AssemblyReferences)
                    DllReferenceList.Items.Add(dll);
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
            _errorMessages.Clear();
            _logMessages.Clear();
            foreach (var item in _outputVariables) item.Value = null;

            using var lifecycleManager = new ScriptLifecycleManager();
            // Global variable delegates - injected by workflow engine in workflow mode
            Func<string, object?>? getGlobal = null;
            Action<string, object?>? setGlobal = null;

            var errors = lifecycleManager.Execute(
                _parameters.ScriptCode,
                _parameters.AssemblyReferences,
                getGlobal, setGlobal);

            foreach (var error in errors) _errorMessages.Add(error);
            foreach (var log in lifecycleManager.LogMessages) _logMessages.Add(log);
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
            _errorMessages.Clear(); _logMessages.Clear();
            foreach (var item in _outputVariables) item.Value = null;
            CompileTimeLabel.Content = ""; ExecTimeLabel.Content = "";
            UpdateStatus("就绪");
        }

        private void CodeEditor_LostFocus(object sender, RoutedEventArgs e) => SyncCodeToParameters();

        private void ErrorListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ErrorListBox.SelectedItem is string errorMsg && CodeEditor != null)
            {
                var match = Regex.Match(errorMsg, @"line (\d+)");
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

        protected override void OnConfirmCommand(object sender, ExecutedRoutedEventArgs e)
        {
            SyncCodeToParameters();
            base.OnConfirmCommand(sender, e);
        }
    }
}
