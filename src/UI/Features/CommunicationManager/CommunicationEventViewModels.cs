using System.Collections.ObjectModel;
using System.Collections.Generic;
using SunEyeVision.UI.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.UI.Features.CommunicationManager;

public enum CommunicationEventProcessingMethod
{
    Text,
    ByteMatch,
    Script
}

public enum CommunicationEventType
{
    ProtocolParse,
    ProtocolCompare
}

public sealed class CommunicationReceiveEventViewModel : ViewModelBase
{
    private string _selectedDeviceId = string.Empty;
    private string _separator = ",";
    private bool _characterLengthCompare;
    private int _characterLength = 1;
    private bool _isMessageExpanded;
    private bool _showMessageAsHex;
    private string _messageText = string.Empty;
    private bool _isAssemblyConfigTab;
    private bool _echoToDevice;
    private string _name = string.Empty;

    public CommunicationEventProcessingMethod ProcessingMethod { get; set; }
    public CommunicationEventType EventType { get; set; }
    public bool IsProtocolCompare => EventType == CommunicationEventType.ProtocolCompare && ProcessingMethod != CommunicationEventProcessingMethod.Script;
    public bool IsProtocolParse => !IsProtocolCompare;
    public int ProcessingMethodIndex
    {
        get => (int)ProcessingMethod;
        set
        {
            ProcessingMethod = (CommunicationEventProcessingMethod)value;
            if (ProcessingMethod == CommunicationEventProcessingMethod.ByteMatch)
                EventType = CommunicationEventType.ProtocolCompare;
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(AvailableEventTypeNames));
            OnPropertyChanged(nameof(SelectedEventTypeName));
            OnPropertyChanged(nameof(HasEventType));
            OnPropertyChanged(nameof(ProcessingDescription));
            OnPropertyChanged(nameof(IsProtocolCompare));
            OnPropertyChanged(nameof(IsProtocolParse));
        }
    }
    public int EventTypeIndex
    {
        get => (int)EventType;
        set
        {
            EventType = (CommunicationEventType)value;
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(IsProtocolCompare));
            OnPropertyChanged(nameof(IsProtocolParse));
        }
    }
    public string DisplayName => string.IsNullOrWhiteSpace(_name)
        ? ProcessingMethod == CommunicationEventProcessingMethod.Script ? ProcessingMethodDisplayName : $"{ProcessingMethodDisplayName}-{EventTypeDisplayName}"
        : _name;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
    public IReadOnlyList<string> AvailableEventTypeNames => ProcessingMethod == CommunicationEventProcessingMethod.ByteMatch
        ? new[] { "协议比对" }
        : new[] { "协议解析", "协议比对" };
    public bool HasEventType => ProcessingMethod != CommunicationEventProcessingMethod.Script;
    public string SelectedEventTypeName
    {
        get => EventType == CommunicationEventType.ProtocolCompare ? "协议比对" : "协议解析";
        set
        {
            EventType = value == "协议比对" ? CommunicationEventType.ProtocolCompare : CommunicationEventType.ProtocolParse;
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(IsProtocolCompare));
            OnPropertyChanged(nameof(IsProtocolParse));
        }
    }
    public string ProcessingDescription => ProcessingMethod switch
    {
        CommunicationEventProcessingMethod.Script => "说明：对输入数据进行解析和组装，方式为 Python 脚本",
        CommunicationEventProcessingMethod.ByteMatch => "说明：按照配置的字节规则比对收到的数据",
        _ => "说明：按照配置的规则解析收到的数据"
    };
    public string ProcessingMethodDisplayName => ProcessingMethod switch
    {
        CommunicationEventProcessingMethod.ByteMatch => "字节匹配",
        CommunicationEventProcessingMethod.Script => "脚本",
        _ => "文本"
    };
    public string EventTypeDisplayName => EventType == CommunicationEventType.ProtocolCompare ? "协议比对" : "协议解析";
    public string SelectedDeviceId { get => _selectedDeviceId; set => SetProperty(ref _selectedDeviceId, value); }
    public string Separator { get => _separator; set => SetProperty(ref _separator, value); }
    public bool CharacterLengthCompare { get => _characterLengthCompare; set => SetProperty(ref _characterLengthCompare, value); }
    public int CharacterLength { get => _characterLength; set => SetProperty(ref _characterLength, value < 1 ? 1 : value); }
    public bool IsMessageExpanded
    {
        get => _isMessageExpanded;
        set
        {
            if (!SetProperty(ref _isMessageExpanded, value)) return;
            OnPropertyChanged(nameof(MessagePanelHeader));
            OnPropertyChanged(nameof(OutputListMaxHeight));
            OnPropertyChanged(nameof(CompareListMaxHeight));
        }
    }
    public string MessagePanelHeader => IsMessageExpanded ? "收起 〉" : "打开消息";
    public double OutputListMaxHeight => IsMessageExpanded ? 220 : 260;
    public double CompareListMaxHeight => IsMessageExpanded ? 120 : 240;
    public bool ShowMessageAsHex { get => _showMessageAsHex; set => SetProperty(ref _showMessageAsHex, value); }
    public string MessageText { get => _messageText; set => SetProperty(ref _messageText, value); }
    public ObservableCollection<CommunicationEventOutputViewModel> Outputs { get; } = new();
    public ObservableCollection<CommunicationCompareInputViewModel> CompareInputs { get; } = new();
    public ObservableCollection<AvailableDataSource> CompareInputDataSources { get; } = new();
    public ObservableCollection<CommunicationAssemblyOutputViewModel> AssemblyOutputs { get; } = new();
    public bool IsAssemblyConfigTab
    {
        get => _isAssemblyConfigTab;
        set
        {
            if (!SetProperty(ref _isAssemblyConfigTab, value)) return;
            OnPropertyChanged(nameof(IsInputConfigTab));
        }
    }
    public bool IsInputConfigTab => !IsAssemblyConfigTab;
    public bool EchoToDevice { get => _echoToDevice; set => SetProperty(ref _echoToDevice, value); }
}

public sealed class CommunicationEventOutputViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private string _dataType = "int";
    private string _result = string.Empty;
    private string _status = "未接收";

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string DataType { get => _dataType; set => SetProperty(ref _dataType, value); }
    public string Result { get => _result; set => SetProperty(ref _result, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
}

public sealed class CommunicationCompareInputViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private string _dataType = "int";
    private string _compareRule = "不比较";
    private int _minimum;
    private int _maximum = 1;
    private string _compareValue = string.Empty;
    private string _result = string.Empty;
    private string _status = "未接收";
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string DataType
    {
        get => _dataType;
        set
        {
            if (!SetProperty(ref _dataType, value)) return;
            OnPropertyChanged(nameof(IsString));
            OnPropertyChanged(nameof(IsNumericRange));
        }
    }
    public bool IsString => string.Equals(DataType, "string", System.StringComparison.OrdinalIgnoreCase);
    public bool IsNumericRange => !IsString;
    public string CompareRule { get => _compareRule; set => SetProperty(ref _compareRule, value); }
    public int Minimum { get => _minimum; set => SetProperty(ref _minimum, value); }
    public int Maximum { get => _maximum; set => SetProperty(ref _maximum, value); }
    public string CompareValue { get => _compareValue; set => SetProperty(ref _compareValue, value); }
    public string Result { get => _result; set => SetProperty(ref _result, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
}

public sealed class CommunicationAssemblyOutputViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private string _dataType = "int";
    private string _content = string.Empty;
    private string _result = string.Empty;
    private string _bindingSource = string.Empty;
    private string _bindingDisplayText = string.Empty;
    private bool _isContentBound;
    private ObservableCollection<AvailableDataSource>? _availableDataSources;
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string DataType { get => _dataType; set => SetProperty(ref _dataType, value); }
    public string Content { get => _content; set => SetProperty(ref _content, value); }
    public string Result { get => _result; set => SetProperty(ref _result, value); }
    public string BindingSource
    {
        get => _bindingSource;
        set
        {
            if (!SetProperty(ref _bindingSource, value)) return;
            IsContentBound = !string.IsNullOrWhiteSpace(value);
        }
    }
    public string BindingDisplayText { get => _bindingDisplayText; set => SetProperty(ref _bindingDisplayText, value); }
    public bool IsContentBound { get => _isContentBound; set => SetProperty(ref _isContentBound, value); }
    public ObservableCollection<AvailableDataSource>? AvailableDataSources
    {
        get => _availableDataSources;
        set => SetProperty(ref _availableDataSources, value);
    }
}
