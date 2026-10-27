using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

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
        private ImageSourceReference? _sourceCompareReference,_targetCompareReference;

        public bool EnableLoop { get => _enableLoop; set => SetProperty(ref _enableLoop, value); }
        public int LoopInterval { get => _loopInterval; set => SetProperty(ref _loopInterval, value); }
        public bool EnableBreak { get => _enableBreak; set => SetProperty(ref _enableBreak, value); }
        public string DataType
        {
            get=>_dataType;
            set
            {
                if(!SetProperty(ref _dataType,value))return;
                SourceCompareBinding="";TargetCompareBinding="";SourceCompareBindingDisplayText="";TargetCompareBindingDisplayText="";
                OnPropertyChanged(nameof(SourceCompareBindingDisplayText));OnPropertyChanged(nameof(TargetCompareBindingDisplayText));
            }
        }
        public string ComparisonOperator { get => _comparisonOperator; set => SetProperty(ref _comparisonOperator, value); }
        public string StartValueBinding { get => _startValueBinding; set => SetProperty(ref _startValueBinding, value); }
        public string StartValueBindingDisplayText { get => _startValueBindingDisplayText; set => SetProperty(ref _startValueBindingDisplayText, value); }
        public string EndValueBinding { get => _endValueBinding; set => SetProperty(ref _endValueBinding, value); }
        public string EndValueBindingDisplayText { get => _endValueBindingDisplayText; set => SetProperty(ref _endValueBindingDisplayText, value); }
        public string SourceCompareBinding { get => _sourceCompareReference is {} r?r.SourceNodeId+"."+r.PropertyName:_sourceCompareBinding; set {var hadReference=_sourceCompareReference is not null;_sourceCompareReference=null;var changed=SetProperty(ref _sourceCompareBinding,value);if(hadReference){OnPropertyChanged(nameof(SourceCompareReference));if(!changed)OnPropertyChanged(nameof(SourceCompareBinding));}} }
        public string SourceCompareBindingDisplayText { get => _sourceCompareBindingDisplayText; set => SetProperty(ref _sourceCompareBindingDisplayText, value); }
        public string TargetCompareBinding { get => _targetCompareReference is {} r?r.SourceNodeId+"."+r.PropertyName:_targetCompareBinding; set {var hadReference=_targetCompareReference is not null;_targetCompareReference=null;var changed=SetProperty(ref _targetCompareBinding,value);if(hadReference){OnPropertyChanged(nameof(TargetCompareReference));if(!changed)OnPropertyChanged(nameof(TargetCompareBinding));}} }
        public string TargetCompareBindingDisplayText { get => _targetCompareBindingDisplayText; set => SetProperty(ref _targetCompareBindingDisplayText, value); }
        public ImageSourceReference? SourceCompareReference
        {get=>_sourceCompareReference;set {if(SetProperty(ref _sourceCompareReference,value)){_sourceCompareBinding=value is null?"":value.SourceNodeId+"."+value.PropertyName;OnPropertyChanged(nameof(SourceCompareBinding));}}}
        public ImageSourceReference? TargetCompareReference
        {get=>_targetCompareReference;set {if(SetProperty(ref _targetCompareReference,value)){_targetCompareBinding=value is null?"":value.SourceNodeId+"."+value.PropertyName;OnPropertyChanged(nameof(TargetCompareBinding));}}}

    }
}
