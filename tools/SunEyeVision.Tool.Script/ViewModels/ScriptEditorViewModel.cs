using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Tool.Script.Models;

namespace SunEyeVision.Tool.Script.ViewModels
{
    /// <summary>
    /// 脚本编辑器 ViewModel — 管理代码编辑、变量监视、编译错误、日志显示。
    /// </summary>
    public class ScriptEditorViewModel
    {
        #region 字段

        private ScriptParameters _parameters;
        private readonly ObservableCollection<ScriptParamItem> _inputVariables = new();
        private readonly ObservableCollection<ScriptParamItem> _outputVariables = new();
        private readonly ObservableCollection<string> _errorMessages = new();
        private readonly ObservableCollection<string> _logMessages = new();

        #endregion

        #region 属性
        /// <summary>
        /// 当前参数对象
        /// </summary>
        public ScriptParameters Parameters => _parameters;

        /// <summary>
        /// 脚本代码
        /// </summary>
        public string ScriptCode
        {
            get => _parameters.ScriptCode;
            set => _parameters.ScriptCode = value;
        }

        /// <summary>
        /// 输入变量列表（绑定到 DataGrid）
        /// </summary>
        public ObservableCollection<ScriptParamItem> InputVariables => _inputVariables;

        /// <summary>
        /// 输出变量列表（绑定到 DataGrid，运行后显示值）
        /// </summary>
        public ObservableCollection<ScriptParamItem> OutputVariables => _outputVariables;

        /// <summary>
        /// 编译/运行时错误列表
        /// </summary>
        public ObservableCollection<string> ErrorMessages => _errorMessages;

        /// <summary>
        /// 日志信息列表
        /// </summary>
        public ObservableCollection<string> LogMessages => _logMessages;

        /// <summary>
        /// 执行耗时（毫秒）
        /// </summary>
        public long ExecutionTimeMs { get; set; }

        /// <summary>
        /// 编译耗时（毫秒）
        /// </summary>
        public long CompileTimeMs { get; set; }

        #endregion

        #region 构造函数
        public ScriptEditorViewModel()
        {
            _parameters = new ScriptParameters();
        }

        public ScriptEditorViewModel(ScriptParameters parameters)
        {
            _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }

        #endregion

        #region 公开方法

        /// <summary>
        /// 设置参数对象（由 SetParameters 注入）
        /// </summary>
        public void SetParameters(ScriptParameters parameters)
        {
            _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            SyncInputVariables();
            SyncOutputVariables();
        }

        /// <summary>
        /// 从参数同步输入变量到监视集合
        /// </summary>
        public void SyncInputVariables()
        {
            _inputVariables.Clear();
            if (_parameters.Inputs != null)
            {
                foreach (var input in _parameters.Inputs)
                {
                    _inputVariables.Add(new ScriptParamItem
                    {
                        Name = input.Name,
                        TypeName = input.TypeName,
                        Value = null
                    });
                }
            }
        }

        /// <summary>
        /// 从参数同步输出变量到监视集合
        /// </summary>
        public void SyncOutputVariables()
        {
            _outputVariables.Clear();
            if (_parameters.Outputs != null)
            {
                foreach (var output in _parameters.Outputs)
                {
                    _outputVariables.Add(new ScriptParamItem
                    {
                        Name = output.Name,
                        TypeName = output.TypeName,
                        Value = null
                    });
                }
            }
        }

        /// <summary>
        /// 使用执行结果更新输出变量值
        /// </summary>
        public void UpdateOutputs(Dictionary<string, object?> outputValues)
        {
            foreach (var kvp in outputValues)
            {
                var existing = _outputVariables.FirstOrDefault(v => v.Name == kvp.Key);
                if (existing != null)
                {
                    existing.Value = kvp.Value;
                }
                else
                {
                    _outputVariables.Add(new ScriptParamItem
                    {
                        Name = kvp.Key,
                        Value = kvp.Value,
                        TypeName = kvp.Value?.GetType().Name ?? "null"
                    });
                }
            }
        }

        /// <summary>
        /// 更新编译/运行时错误列表
        /// </summary>
        public void UpdateErrors(List<string> errors)
        {
            _errorMessages.Clear();
            foreach (var error in errors)
            {
                _errorMessages.Add(error);
            }
        }

        /// <summary>
        /// 更新日志信息列表
        /// </summary>
        public void UpdateLogs(List<string> logs)
        {
            _logMessages.Clear();
            foreach (var log in logs)
            {
                _logMessages.Add(log);
            }
        }

        /// <summary>
        /// 清除所有输出
        /// </summary>
        public void ClearResults()
        {
            _errorMessages.Clear();
            _logMessages.Clear();
            foreach (var item in _outputVariables)
            {
                item.Value = null;
            }
            ExecutionTimeMs = 0;
            CompileTimeMs = 0;
        }

        #endregion
    }
}
