using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Models
{
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

        public bool EnableLoop { get => _enableLoop; set => SetProperty(ref _enableLoop, value); }
        public int LoopInterval { get => _loopInterval; set => SetProperty(ref _loopInterval, value); }
        public bool EnableBreak { get => _enableBreak; set => SetProperty(ref _enableBreak, value); }
        public string DataType { get => _dataType; set => SetProperty(ref _dataType, value); }
        public string ComparisonOperator { get => _comparisonOperator; set => SetProperty(ref _comparisonOperator, value); }
        public string StartValueBinding { get => _startValueBinding; set => SetProperty(ref _startValueBinding, value); }
        public string StartValueBindingDisplayText { get => _startValueBindingDisplayText; set => SetProperty(ref _startValueBindingDisplayText, value); }
        public string EndValueBinding { get => _endValueBinding; set => SetProperty(ref _endValueBinding, value); }
        public string EndValueBindingDisplayText { get => _endValueBindingDisplayText; set => SetProperty(ref _endValueBindingDisplayText, value); }
        public string SourceCompareBinding { get => _sourceCompareBinding; set => SetProperty(ref _sourceCompareBinding, value); }
        public string SourceCompareBindingDisplayText { get => _sourceCompareBindingDisplayText; set => SetProperty(ref _sourceCompareBindingDisplayText, value); }
        public string TargetCompareBinding { get => _targetCompareBinding; set => SetProperty(ref _targetCompareBinding, value); }
        public string TargetCompareBindingDisplayText { get => _targetCompareBindingDisplayText; set => SetProperty(ref _targetCompareBindingDisplayText, value); }
    }
}
