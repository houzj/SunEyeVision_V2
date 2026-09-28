using System.Collections.ObjectModel;
using SunEyeVision.UI.Core;

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
    private string _separator = ";";
    private bool _characterLengthCompare;

    public CommunicationEventProcessingMethod ProcessingMethod { get; set; }
    public CommunicationEventType EventType { get; set; }
    public int ProcessingMethodIndex
    {
        get => (int)ProcessingMethod;
        set { ProcessingMethod = (CommunicationEventProcessingMethod)value; OnPropertyChanged(nameof(DisplayName)); }
    }
    public int EventTypeIndex
    {
        get => (int)EventType;
        set { EventType = (CommunicationEventType)value; OnPropertyChanged(nameof(DisplayName)); }
    }
    public string DisplayName => $"{ProcessingMethodDisplayName}-{EventTypeDisplayName}";
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
    public ObservableCollection<CommunicationEventOutputViewModel> Outputs { get; } = new();
}

public sealed class CommunicationEventOutputViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private string _result = string.Empty;
    private string _status = "未配置";

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Result { get => _result; set => SetProperty(ref _result, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
}
