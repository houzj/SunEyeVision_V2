using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Communication;

namespace SunEyeVision.Tool.ProtocolParse;

[JsonDerivedType(typeof(ProtocolParseParameters), "ProtocolParseParameters")]
public class ProtocolParseParameters : ToolParameters, IOutputContractProvider, INotifyOutputContractChanged
{
    private string _mode = "Text", _inputText = "", _separator = ",", _inputFormat = "Hex", _encoding = "UTF8";
    private bool _asciiData;
    private ObservableCollection<ProtocolParseField> _outputs = new() { new() };
    private HashSet<ProtocolParseField> _tracked = new();
    public ProtocolParseParameters() { _outputs.CollectionChanged += FieldsChanged; Track(); }
    public string Mode { get => _mode; set => SetProperty(ref _mode, value); }
    public string InputText { get => _inputText; set => SetProperty(ref _inputText, value); }
    public string Separator { get => _separator; set => SetProperty(ref _separator, value); }
    public string InputFormat { get => _inputFormat; set => SetProperty(ref _inputFormat, value); }
    public string StringEncoding { get => _encoding; set => SetProperty(ref _encoding, value); }
    public bool AsciiData { get => _asciiData; set => SetProperty(ref _asciiData, value); }
    public ObservableCollection<ProtocolParseField> Outputs
    {
        get => _outputs;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_outputs, value)) return;
            _outputs.CollectionChanged -= FieldsChanged;
            _outputs = value; _outputs.CollectionChanged += FieldsChanged; Track();
            OnPropertyChanged(nameof(Outputs)); OutputContractChanged?.Invoke();
        }
    }
    public event Action? OutputContractChanged;
    private void FieldsChanged(object? sender, NotifyCollectionChangedEventArgs e) { Track(); OutputContractChanged?.Invoke(); }
    private void Track()
    {
        foreach (var field in _tracked) field.PropertyChanged -= FieldChanged;
        _tracked = _outputs.ToHashSet();
        foreach (var field in _tracked) field.PropertyChanged += FieldChanged;
    }
    private void FieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProtocolParseField.Name) or nameof(ProtocolParseField.DataType)) OutputContractChanged?.Invoke();
    }
    public IReadOnlyList<OutputVariable> GetOutputContract()
    {
        var variables = new CommunicationToolResults().GetOutputVariables().ToList();
        foreach (var field in Outputs)
            variables.Add(field.Definition());
        return variables;
    }
}

public sealed class ProtocolParseField : ObservableObject
{
    public Guid FieldId { get; set; } = Guid.NewGuid();
    private string _name = "out0", _dataType = "string", _order = "ABCD";
    private int _start, _end;
    private string _numericInterpretation = "Auto";
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string DataType { get => _dataType; set => SetProperty(ref _dataType, value); }
    public int Start { get => _start; set => SetProperty(ref _start, value); }
    public int End { get => _end; set => SetProperty(ref _end, value); }
    public string Order { get => _order; set => SetProperty(ref _order, value); }
    public string NumericInterpretation { get => _numericInterpretation; set => SetProperty(ref _numericInterpretation, value); }

    public CommunicationOutputField OutputDefinition() => new(FieldId, Name, Vocabulary(DataType));
    internal OutputVariable Definition() => new CommunicationOutputField(FieldId,
        string.IsNullOrWhiteSpace(Name) ? "未命名" : Name, Vocabulary(DataType)).CreateContract();
    public static string Vocabulary(string type) => type switch
    { "int" => "Integer", "float" => "Double", "string" => "String", _ => throw new FormatException("输出类型必须是int、float或string。") };
}
