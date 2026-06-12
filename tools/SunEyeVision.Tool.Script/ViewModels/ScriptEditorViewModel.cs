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
    /// 鑴氭湰缂栬緫鍣?ViewModel 鈥?绠＄悊浠ｇ爜缂栬緫銆佸彉閲忕洃瑙嗐€佺紪璇戦敊璇€佹棩蹇楁樉绀恒€?    /// </summary>
    public class ScriptEditorViewModel
    {
        #region 瀛楁

        private ScriptParameters _parameters;
        private readonly ObservableCollection<ScriptParamItem> _inputVariables = new();
        private readonly ObservableCollection<ScriptParamItem> _outputVariables = new();
        private readonly ObservableCollection<string> _errorMessages = new();
        private readonly ObservableCollection<string> _logMessages = new();

        #endregion

        #region 灞炴€?
        /// <summary>
        /// 褰撳墠鍙傛暟瀵硅薄
        /// </summary>
        public ScriptParameters Parameters => _parameters;

        /// <summary>
        /// 鑴氭湰浠ｇ爜
        /// </summary>
        public string ScriptCode
        {
            get => _parameters.ScriptCode;
            set => _parameters.ScriptCode = value;
        }

        /// <summary>
        /// 杈撳叆鍙橀噺鍒楄〃锛堢粦瀹氬埌 DataGrid锛?        /// </summary>
        public ObservableCollection<ScriptParamItem> InputVariables => _inputVariables;

        /// <summary>
        /// 杈撳嚭鍙橀噺鍒楄〃锛堢粦瀹氬埌 DataGrid锛岃繍琛屽悗鏄剧ず鍊硷級
        /// </summary>
        public ObservableCollection<ScriptParamItem> OutputVariables => _outputVariables;

        /// <summary>
        /// 缂栬瘧/杩愯鏃堕敊璇垪琛?        /// </summary>
        public ObservableCollection<string> ErrorMessages => _errorMessages;

        /// <summary>
        /// 鏃ュ織淇℃伅鍒楄〃
        /// </summary>
        public ObservableCollection<string> LogMessages => _logMessages;

        /// <summary>
        /// 鎵ц鑰楁椂锛堟绉掞級
        /// </summary>
        public long ExecutionTimeMs { get; set; }

        /// <summary>
        /// 缂栬瘧鑰楁椂锛堟绉掞級
        /// </summary>
        public long CompileTimeMs { get; set; }

        #endregion

        #region 鏋勯€犲嚱鏁?
        public ScriptEditorViewModel()
        {
            _parameters = new ScriptParameters();
        }

        public ScriptEditorViewModel(ScriptParameters parameters)
        {
            _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }

        #endregion

        #region 鍏紑鏂规硶

        /// <summary>
        /// 璁剧疆鍙傛暟瀵硅薄锛堢敱 SetParameters 娉ㄥ叆锛?        /// </summary>
        public void SetParameters(ScriptParameters parameters)
        {
            _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            SyncInputVariables();
            SyncOutputVariables();
        }

        /// <summary>
        /// 浠庡弬鏁板悓姝ヨ緭鍏ュ彉閲忓埌 ObservableCollection
        /// </summary>
        public void SyncInputVariables()
        {
            _inputVariables.Clear();
            if (_parameters.InputVariables != null)
            {
                foreach (var item in _parameters.InputVariables)
                {
                    _inputVariables.Add(item);
                }
            }
        }

        /// <summary>
        /// 浠庡弬鏁板悓姝ヨ緭鍑哄彉閲忓埌 ObservableCollection
        /// </summary>
        public void SyncOutputVariables()
        {
            _outputVariables.Clear();
            if (_parameters.OutputVariables != null)
            {
                foreach (var item in _parameters.OutputVariables)
                {
                    _outputVariables.Add(item);
                }
            }
        }

        /// <summary>
        /// 浣跨敤鎵ц缁撴灉鏇存柊杈撳嚭鍙橀噺鍊?        /// </summary>
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
        /// 鏇存柊缂栬瘧/杩愯鏃堕敊璇垪琛?        /// </summary>
        public void UpdateErrors(List<string> errors)
        {
            _errorMessages.Clear();
            foreach (var error in errors)
            {
                _errorMessages.Add(error);
            }
        }

        /// <summary>
        /// 鏇存柊鏃ュ織淇℃伅鍒楄〃
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
        /// 娓呴櫎鎵€鏈夎緭鍑?        /// </summary>
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