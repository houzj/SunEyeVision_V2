using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SunEyeVision.Domain.Solutions;

namespace SunEyeVision.Application.Communications;

public enum CommunicationRuntimeStatus { Disconnected, Connecting, Connected, Faulted }

/// <summary>Session-owned state for one communication definition; never persisted.</summary>
public sealed class CommunicationRuntimeState : INotifyPropertyChanged, IDisposable
{
    public CommunicationRuntimeState(Communication definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Definition.PropertyChanged += DefinitionChanged;
    }
    public Communication Definition { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    private CommunicationRuntimeStatus _status;
    private bool _intent, _waiting, _busy;
    private string _description = "", _error = "";
    public CommunicationRuntimeStatus RuntimeStatus { get => _status; set { if (!Set(ref _status,value)) return; Changed(nameof(IsRuntimeActive)); Changed(nameof(IsActuallyConnected)); Changed(nameof(RuntimeStatusText)); Changed(nameof(ProtocolStatusText)); } }
    public bool IsRuntimeActive => RuntimeStatus is CommunicationRuntimeStatus.Connecting or CommunicationRuntimeStatus.Connected;
    public bool IsActuallyConnected => RuntimeStatus == CommunicationRuntimeStatus.Connected;
    public bool IsConnectionEnabled { get => _intent; set => Set(ref _intent,value); }
    public bool IsWaitingForReconnect { get => _waiting; set => Set(ref _waiting,value); }
    public bool IsRuntimeOperationInProgress { get => _busy; set { if (Set(ref _busy,value)) Changed(nameof(CanToggleRuntime)); } }
    public string RuntimeConnectionDescription { get => _description; set { if (!Set(ref _description,value ?? "")) return; Changed(nameof(RuntimeStatusText)); Changed(nameof(ProtocolStatusText)); } }
    public string RuntimeConnectionError { get => _error; set { if (Set(ref _error,value ?? "")) Changed(nameof(HasRuntimeConnectionError)); } }
    public bool HasRuntimeConnectionError => !string.IsNullOrWhiteSpace(RuntimeConnectionError);
    public bool CanToggleRuntime => Definition.SupportsRuntimeToggle && Definition.IsEnabled && !IsRuntimeOperationInProgress;
    public string RuntimeStatusText => !string.IsNullOrEmpty(RuntimeConnectionDescription) ? RuntimeConnectionDescription : RuntimeStatus switch { CommunicationRuntimeStatus.Connected => "已连接", CommunicationRuntimeStatus.Connecting => "连接中", CommunicationRuntimeStatus.Faulted => "故障", _ => "已断开" };
    public string ProtocolStatusText => $"{Definition.ProtocolDisplayName} · {RuntimeStatusText}";
    private void DefinitionChanged(object? sender, PropertyChangedEventArgs e)
    { if(e.PropertyName is nameof(Communication.IsEnabled) or nameof(Communication.SupportsRuntimeToggle) or nameof(Communication.ProtocolKey)) Changed(nameof(CanToggleRuntime)); if(e.PropertyName is nameof(Communication.ProtocolKey) or nameof(Communication.ProtocolDisplayName)) Changed(nameof(ProtocolStatusText)); }
    private bool Set<T>(ref T field,T value,[CallerMemberName]string? name=null) { if(EqualityComparer<T>.Default.Equals(field,value))return false;field=value;Changed(name);return true; }
    private void Changed(string? name) => PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
    public void Dispose() => Definition.PropertyChanged -= DefinitionChanged;
}

/// <summary>Session-owned point observation; its collection remains observable while a log view is open.</summary>
public sealed class ModbusPointRuntimeState : INotifyPropertyChanged
{
    private string _value = ""; private bool _received;
    public event PropertyChangedEventHandler? PropertyChanged;
    public string CurrentValue { get => _value; set { value ??= ""; if(_value==value)return;_value=value;PropertyChanged?.Invoke(this,new(nameof(CurrentValue))); } }
    public bool HasReceivedData { get => _received; set { if(_received==value)return;_received=value;PropertyChanged?.Invoke(this,new(nameof(HasReceivedData))); } }
    public ObservableCollection<string> ReceiveRecords { get; } = new();
}
