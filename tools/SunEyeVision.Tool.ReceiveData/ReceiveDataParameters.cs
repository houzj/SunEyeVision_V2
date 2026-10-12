using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Communication;

namespace SunEyeVision.Tool.ReceiveData;

[JsonDerivedType(typeof(ReceiveDataParameters), "ReceiveDataParameters")]
public class ReceiveDataParameters : ToolParameters, IOutputContractProvider, INotifyOutputContractChanged, IReceiveVariableInputProvider
{
    private string _sourceKind = "Device", _deviceId = "", _targetId = "", _outputName = "out0", _outputType = "Binary";
    private ObservableCollection<ReceiveVariableMapping> _variables = new();
    private readonly HashSet<ReceiveVariableMapping> _observed = new();
    public ReceiveDataParameters() { _variables.Add(new()); Observe(); }
    public Guid FieldId { get; set; } = Guid.NewGuid();
    public string SourceKind { get => _sourceKind; set { if (SetProperty(ref _sourceKind, value)) OutputContractChanged?.Invoke(); } }
    public string DeviceId { get => _deviceId; set => SetProperty(ref _deviceId, value); }
    public string TargetId { get => _targetId; set => SetProperty(ref _targetId, value); }
    public string OutputName { get => _outputName; set { if (SetProperty(ref _outputName, value)) OutputContractChanged?.Invoke(); } }
    public string OutputType { get => _outputType; set { if (SetProperty(ref _outputType, value)) OutputContractChanged?.Invoke(); } }
    public ObservableCollection<ReceiveVariableMapping> Variables
    {
        get => _variables;
        set { _variables.CollectionChanged -= CollectionChanged; foreach (var row in _observed) row.PropertyChanged -= RowChanged;
            _observed.Clear(); _variables = value ?? new(); Observe(); OnPropertyChanged(nameof(Variables)); OutputContractChanged?.Invoke(); }
    }
    public event Action? OutputContractChanged;
    private void Observe() { _variables.CollectionChanged -= CollectionChanged; _variables.CollectionChanged += CollectionChanged; SyncRows(); }
    private void SyncRows()
    {
        foreach (var old in _observed.Where(v => !_variables.Contains(v)).ToArray()) { old.PropertyChanged -= RowChanged; _observed.Remove(old); }
        foreach (var row in _variables) if (_observed.Add(row)) row.PropertyChanged += RowChanged;
    }
    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) { SyncRows(); OutputContractChanged?.Invoke(); }
    private void RowChanged(object? sender, PropertyChangedEventArgs e) => OutputContractChanged?.Invoke();
    public CommunicationOutputField OutputDefinition() => new(FieldId, OutputName, OutputType);
    public IReadOnlyList<string> GetReceiveVariableIds() => SourceKind == "Variable" ? Variables.Select(v => v.VariableId).ToArray() : Array.Empty<string>();
    public IReadOnlyList<CommunicationOutputField> Definitions() => SourceKind == "Variable" ? Variables.Select(v => v.Definition()).ToArray() : new[] { OutputDefinition() };
    public IReadOnlyList<OutputVariable> GetOutputContract() => new CommunicationToolResults(Definitions()).GetOutputVariables();
}
public sealed class ReceiveVariableMapping : ObservableObject
{
    public Guid FieldId { get; set; } = Guid.NewGuid();
    private string _name = "out0", _id = "", _path = "请选择", _type = "String";
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string VariableId { get => _id; set => SetProperty(ref _id, value); }
    public string VariablePath { get => _path; set => SetProperty(ref _path, value); }
    public string DataType { get => _type; set => SetProperty(ref _type, value); }
    public CommunicationOutputField Definition() => new(FieldId, Name, DataType);
}
