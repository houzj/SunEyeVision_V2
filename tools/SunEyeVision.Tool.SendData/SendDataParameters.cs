using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Communication;

namespace SunEyeVision.Tool.SendData;

[JsonDerivedType(typeof(SendDataParameters), "SendDataParameters")]
public class SendDataParameters : ToolParameters, IOutputContractProvider, INotifyOutputContractChanged, ICommunicationBindingInputProvider
{
    private string _kind = "Device", _device = "", _event = "", _separator = "", _terminator = "";
    private bool _hex, _ignore;
    private ObservableCollection<SendDataRow> _rows = new() { new() };
    private readonly HashSet<SendDataRow> _observed = new();
    public SendDataParameters() { Observe(); }
    public string DestinationKind { get => _kind; set { if (SetProperty(ref _kind, value)) OutputContractChanged?.Invoke(); } }
    public string DeviceId { get => _device; set => SetProperty(ref _device, value); }
    public string EventId { get => _event; set => SetProperty(ref _event, value); }
    public string Separator { get => _separator; set => SetProperty(ref _separator, value); }
    public string Terminator { get => _terminator; set => SetProperty(ref _terminator, value); }
    public bool Hex { get => _hex; set => SetProperty(ref _hex, value); }
    public bool IgnoreErrors { get => _ignore; set => SetProperty(ref _ignore, value); }
    public ObservableCollection<SendDataRow> Rows
    {
        get => _rows;
        set { _rows.CollectionChanged -= RowsChanged; foreach (var row in _observed) row.PropertyChanged -= Changed; _observed.Clear();
            _rows = value ?? new(); Observe(); OnPropertyChanged(nameof(Rows)); OutputContractChanged?.Invoke(); }
    }
    private void Observe()
    {
        _rows.CollectionChanged -= RowsChanged; _rows.CollectionChanged += RowsChanged; Track();
    }
    private void RowsChanged(object? sender, NotifyCollectionChangedEventArgs e) { Track(); OutputContractChanged?.Invoke(); }
    private void Track()
    {
        foreach (var old in _observed.Where(r => !_rows.Contains(r)).ToArray()) { old.PropertyChanged -= Changed; _observed.Remove(old); }
        foreach (var row in _rows) if (_observed.Add(row)) row.PropertyChanged += Changed;
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => OutputContractChanged?.Invoke();
    public event Action? OutputContractChanged;
    public IReadOnlyList<string> GetInputExpressions() => Rows.Select(r => r.InputText).ToArray();
    public IReadOnlyList<CommunicationOutputField> VariableFields() => DestinationKind == "Variable"
        ? Rows.Select(r => new CommunicationOutputField(r.FieldId, r.Name, CommunicationSendTypes.WorkflowType(r.DataType))).ToArray() : Array.Empty<CommunicationOutputField>();
    public IReadOnlyList<OutputVariable> GetOutputContract() => new SendDataResults(VariableFields()).GetOutputVariables();
}

public sealed class SendDataRow : ObservableObject
{
    public Guid FieldId { get; set; } = Guid.NewGuid();
    public Guid EventFieldId { get; set; }
    private string _name = "data", _target = "", _path = "请选择", _input = "", _type = "String";
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string TargetId { get => _target; set { if (SetProperty(ref _target, value)) OnPropertyChanged(nameof(AllowedDataTypes)); } }
    public string TargetPath { get => _path; set => SetProperty(ref _path, value); }
    public string InputText { get => _input; set => SetProperty(ref _input, value); }
    public string DataType { get => _type; set { if (SetProperty(ref _type, value)) OnPropertyChanged(nameof(AllowedDataTypes)); } }
    [JsonIgnore]
    public IReadOnlyList<string> TypeChoices => CommunicationSendTypes.Choices;
    private bool _restrict;
    [JsonIgnore]
    public bool RestrictType { get => _restrict; set { if (SetProperty(ref _restrict, value)) OnPropertyChanged(nameof(AllowedDataTypes)); } }
    [JsonIgnore]
    public IReadOnlyList<Type> AllowedDataTypes
    {
        get
        {
            return new[] { CommunicationSendTypes.BindingType(DataType) };
        }
    }
    private IReadOnlyList<CommunicationSendTarget> _choices = Array.Empty<CommunicationSendTarget>();
    [JsonIgnore]
    public IReadOnlyList<CommunicationSendTarget> TargetChoices { get => _choices; set { if (SetProperty(ref _choices, value)) OnPropertyChanged(nameof(AllowedDataTypes)); } }
}

public sealed class SendDataResults : CommunicationToolResults
{
    public SendDataResults() { }
    public SendDataResults(IEnumerable<CommunicationOutputField> fields) : base(fields) { }
    public long Submitted { get; set; }
    public long Skipped { get; set; }
    public long Failed { get; set; }
    public List<BinaryPayload> Payloads { get; } = new();
    public List<string> SentData { get; } = new();
    public static readonly CommunicationOutputField SentDataField = new(new("f3b044b5-1a5d-4440-8d1b-eac95f3ca39b"), "已发送数据", "String");
    public static readonly CommunicationOutputField SubmittedField = new(new("9fc8aac5-0353-4462-9524-f0fe4d5dc113"), "成功数量", "Integer");
    public static readonly CommunicationOutputField SkippedField = new(new("53c34d99-1f75-40ab-90c5-20c6ff47e67e"), "跳过数量", "Integer");
    public static readonly CommunicationOutputField FailedField = new(new("a7d14949-ce81-4a1e-9389-43bc0db3eeda"), "失败数量", "Integer");
    public static readonly CommunicationOutputField PayloadField = new(new("0c9c28f2-d227-46e9-aa69-242e5076ce3c"), "已提交字节", "Binary");
    public override IReadOnlyList<OutputVariable> GetOutputVariables()
    {
        var fields = base.GetOutputVariables().ToList();
        fields.Add(SubmittedField.CreateContract(Submitted)); fields.Add(SkippedField.CreateContract(Skipped));
        fields.Add(FailedField.CreateContract(Failed)); fields.Add(PayloadField.CreateContract(Payloads.Count > 0 ? Payloads.ToArray() : null));
        fields.Add(SentDataField.CreateContract(SentData.Count > 0 ? SentData.ToArray() : null));
        return fields;
    }
}
