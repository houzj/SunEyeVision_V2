using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SunEyeVision.Domain.Solutions;

namespace SunEyeVision.Application.Communications;

public enum CommunicationRuntimeStatus { Disconnected, Connecting, Connected, Faulted }
public sealed record CommunicationStateSnapshot(CommunicationRuntimeStatus Status, bool IsConnectionEnabled,
    bool IsWaitingForReconnect, bool IsRuntimeOperationInProgress, string Description, string Error);

/// <summary>State for a single definition. Create, apply and retire on the owning UI dispatcher.</summary>
public sealed class CommunicationRuntimeState : INotifyPropertyChanged, IDisposable
{
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private bool _retired;
    private CommunicationStateSnapshot _snapshot = new(CommunicationRuntimeStatus.Disconnected,false,false,false,"","");
    public CommunicationRuntimeState(Communication definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Definition.PropertyChanged += DefinitionChanged;
    }
    public Communication Definition { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public CommunicationStateSnapshot Snapshot => _snapshot;
    public CommunicationRuntimeStatus RuntimeStatus => _snapshot.Status;
    public bool IsRuntimeActive => RuntimeStatus is CommunicationRuntimeStatus.Connecting or CommunicationRuntimeStatus.Connected;
    public bool IsActuallyConnected => RuntimeStatus == CommunicationRuntimeStatus.Connected;
    public bool IsConnectionEnabled => _snapshot.IsConnectionEnabled;
    public bool IsWaitingForReconnect => _snapshot.IsWaitingForReconnect;
    public bool IsRuntimeOperationInProgress => _snapshot.IsRuntimeOperationInProgress;
    public string RuntimeConnectionDescription => _snapshot.Description;
    public string RuntimeConnectionError => _snapshot.Error;
    public bool HasRuntimeConnectionError => !string.IsNullOrWhiteSpace(RuntimeConnectionError);
    public bool CanToggleRuntime => Definition.SupportsRuntimeToggle && Definition.IsEnabled && !IsRuntimeOperationInProgress;
    public string RuntimeStatusText => !string.IsNullOrEmpty(RuntimeConnectionDescription) ? RuntimeConnectionDescription : RuntimeStatus switch { CommunicationRuntimeStatus.Connected => "已连接", CommunicationRuntimeStatus.Connecting => "连接中", CommunicationRuntimeStatus.Faulted => "故障", _ => "已断开" };
    public string ProtocolStatusText => $"{Definition.ProtocolDisplayName} · {RuntimeStatusText}";
    public bool TryApply(Communication expectedDefinition, CommunicationStateSnapshot snapshot)
    {
        RequireOwner(); ArgumentNullException.ThrowIfNull(snapshot);
        if(_retired || !ReferenceEquals(expectedDefinition,Definition)) return false;
        if(snapshot.Description is null || snapshot.Error is null) throw new ArgumentException("State descriptions must be non-null.",nameof(snapshot));
        if(_snapshot==snapshot)return true;
        _snapshot=snapshot;
        foreach(var name in new[]{nameof(Snapshot),nameof(RuntimeStatus),nameof(IsRuntimeActive),nameof(IsActuallyConnected),nameof(IsConnectionEnabled),nameof(IsWaitingForReconnect),nameof(IsRuntimeOperationInProgress),nameof(RuntimeConnectionDescription),nameof(RuntimeConnectionError),nameof(HasRuntimeConnectionError),nameof(CanToggleRuntime),nameof(RuntimeStatusText),nameof(ProtocolStatusText)}) Changed(name);
        return true;
    }
    private void DefinitionChanged(object? sender, PropertyChangedEventArgs e)
    {
        RequireOwner(); if(_retired)return;
        if(e.PropertyName is nameof(Communication.IsEnabled) or nameof(Communication.SupportsRuntimeToggle) or nameof(Communication.ProtocolKey) or nameof(Communication.ConnectionType)) Changed(nameof(CanToggleRuntime));
        if(e.PropertyName is nameof(Communication.ProtocolKey) or nameof(Communication.ProtocolDisplayName) or nameof(Communication.ConnectionType)) Changed(nameof(ProtocolStatusText));
    }
    private void Changed([CallerMemberName]string? name=null) => PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
    private void RequireOwner() { if(Environment.CurrentManagedThreadId!=_ownerThread)throw new InvalidOperationException("Communication runtime state must be accessed on its owning dispatcher."); }
    public void Dispose() { RequireOwner(); if(_retired)return;_retired=true;Definition.PropertyChanged -= DefinitionChanged; }
}

public sealed record ModbusPointUpdate(string? Value = null, bool? HasReceivedData = null,
    string? AppendRecord = null, bool ClearRecords = false, int? HistoryLimit = null);

/// <summary>Point state is tied to the actual definition instance; updates and disposal are dispatcher-serialized.</summary>
public sealed class ModbusPointRuntimeState : INotifyPropertyChanged, IDisposable
{
    private readonly int _ownerThread=Environment.CurrentManagedThreadId;
    private readonly ObservableCollection<string> _records=new();
    private bool _retired; private string _value=""; private bool _received;
    public ModbusPointRuntimeState(ModbusAddressPoint definition)
    { Definition=definition ?? throw new ArgumentNullException(nameof(definition)); ReceiveRecords=new(_records); }
    public ModbusAddressPoint Definition { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public string CurrentValue => _value;
    public bool HasReceivedData => _received;
    public ReadOnlyObservableCollection<string> ReceiveRecords { get; }
    public bool TryApply(ModbusAddressPoint expectedDefinition, ModbusPointUpdate update)
    {
        RequireOwner(); ArgumentNullException.ThrowIfNull(update);
        if(_retired || !ReferenceEquals(expectedDefinition,Definition))return false;
        if(update.HistoryLimit is < 0)throw new ArgumentOutOfRangeException(nameof(update));
        if(update.Value is { } value && _value!=value){_value=value;Changed(nameof(CurrentValue));}
        if(update.HasReceivedData is { } received && _received!=received){_received=received;Changed(nameof(HasReceivedData));}
        if(update.ClearRecords)_records.Clear();
        if(update.AppendRecord is { } record)_records.Add(record);
        if(update.HistoryLimit is { } limit)while(_records.Count>limit)_records.RemoveAt(0);
        return true;
    }
    private void Changed(string name)=>PropertyChanged?.Invoke(this,new(name));
    private void RequireOwner() { if(Environment.CurrentManagedThreadId!=_ownerThread)throw new InvalidOperationException("Modbus point state must be accessed on its owning dispatcher."); }
    public void Dispose() { RequireOwner();_retired=true; }
}
