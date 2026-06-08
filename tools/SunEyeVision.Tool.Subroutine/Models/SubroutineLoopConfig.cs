using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.Subroutine.Models
{
    /// <summary>
    /// 子程序循环配置 — 对齐 VisionMaster 21000 循环设置面板
    /// </summary>
    public class SubroutineLoopConfig : ObservableObject
    {
        private bool _enableLoop;
        private int _loopInterval;
        private bool _enableBreak;
        private string _dataType = "int";
        private string _comparisonOperator = ">";
        private string _startValueBinding = string.Empty;
        private string _startValueBindingDisplayText = string.Empty;
        private string _endValueBinding = string.Empty;
        private string _endValueBindingDisplayText = string.Empty;
        private string _sourceCompareBinding = string.Empty;
        private string _sourceCompareBindingDisplayText = string.Empty;
        private string _targetCompareBinding = string.Empty;
        private string _targetCompareBindingDisplayText = string.Empty;

        /// <summary>启用循环</summary>
        public bool EnableLoop
        {
            get => _enableLoop;
            set => SetProperty(ref _enableLoop, value);
        }

        /// <summary>循环间隔(毫秒)</summary>
        public int LoopInterval
        {
            get => _loopInterval;
            set => SetProperty(ref _loopInterval, value);
        }

        /// <summary>启用中断循环</summary>
        public bool EnableBreak
        {
            get => _enableBreak;
            set => SetProperty(ref _enableBreak, value);
        }

        /// <summary>循环数据类型</summary>
        public string DataType
        {
            get => _dataType;
            set => SetProperty(ref _dataType, value);
        }

        /// <summary>比较运算符（&gt; &gt;= &lt; &lt;= == !=）</summary>
        public string ComparisonOperator
        {
            get => _comparisonOperator;
            set => SetProperty(ref _comparisonOperator, value);
        }

        /// <summary>起始值绑定源</summary>
        public string StartValueBinding
        {
            get => _startValueBinding;
            set => SetProperty(ref _startValueBinding, value);
        }

        /// <summary>起始值绑定显示文本（格式：节点名.属性名），对接 ParamBinding.BindingDisplayText</summary>
        public string StartValueBindingDisplayText
        {
            get => _startValueBindingDisplayText;
            set => SetProperty(ref _startValueBindingDisplayText, value);
        }

        /// <summary>结束值绑定源</summary>
        public string EndValueBinding
        {
            get => _endValueBinding;
            set => SetProperty(ref _endValueBinding, value);
        }

        /// <summary>结束值绑定显示文本（格式：节点名.属性名），对接 ParamBinding.BindingDisplayText</summary>
        public string EndValueBindingDisplayText
        {
            get => _endValueBindingDisplayText;
            set => SetProperty(ref _endValueBindingDisplayText, value);
        }

        /// <summary>源比较值绑定源</summary>
        public string SourceCompareBinding
        {
            get => _sourceCompareBinding;
            set => SetProperty(ref _sourceCompareBinding, value);
        }

        /// <summary>源比较值绑定显示文本（格式：节点名.属性名），对接 ParamBinding.BindingDisplayText</summary>
        public string SourceCompareBindingDisplayText
        {
            get => _sourceCompareBindingDisplayText;
            set => SetProperty(ref _sourceCompareBindingDisplayText, value);
        }

        /// <summary>目的比较值绑定源</summary>
        public string TargetCompareBinding
        {
            get => _targetCompareBinding;
            set => SetProperty(ref _targetCompareBinding, value);
        }

        /// <summary>目的比较值绑定显示文本（格式：节点名.属性名），对接 ParamBinding.BindingDisplayText</summary>
        public string TargetCompareBindingDisplayText
        {
            get => _targetCompareBindingDisplayText;
            set => SetProperty(ref _targetCompareBindingDisplayText, value);
        }
    }
}
