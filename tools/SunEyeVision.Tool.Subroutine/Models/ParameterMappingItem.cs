using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.Subroutine.Models
{
    /// <summary>
    /// 参数映射项（用于对话框显示）
    /// </summary>
    public class ParameterMappingItem : ObservableObject
    {
        private string _parameterName = string.Empty;
        private string _dataType = "object";
        private string _sourceDisplay = "未设置";

        /// <summary>参数名称</summary>
        public string ParameterName
        {
            get => _parameterName;
            set => SetProperty(ref _parameterName, value);
        }

        /// <summary>数据类型</summary>
        public string DataType
        {
            get => _dataType;
            set => SetProperty(ref _dataType, value);
        }

        /// <summary>订阅源显示文本</summary>
        public string SourceDisplay
        {
            get => _sourceDisplay;
            set => SetProperty(ref _sourceDisplay, value);
        }
    }
}
