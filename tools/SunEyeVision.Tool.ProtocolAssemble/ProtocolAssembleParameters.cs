using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Communication;

namespace SunEyeVision.Tool.ProtocolAssemble;

[JsonDerivedType(typeof(ProtocolAssembleParameters), "ProtocolAssembleParameters")]
public class ProtocolAssembleParameters : ToolParameters, IOutputContractProvider
{
    private string _separator = ",";
    public string Separator { get => _separator; set => SetProperty(ref _separator, value); }
    public ObservableCollection<ProtocolAssemblyPart> Parts { get; set; } = new() { new() };

    public static readonly CommunicationOutputField TextOutput = new(new Guid("6C3C023A-AFB8-4A22-96F5-46D882C552D4"), "组装结果", "String");
    public static readonly CommunicationOutputField BytesOutput = new(new Guid("E1E36CBF-62EA-479A-A0DB-120D33C25B2E"), "16进制组装结果", "Binary");
    public static IReadOnlyList<CommunicationOutputField> OutputFields { get; } = Array.AsReadOnly(new[] { TextOutput, BytesOutput });

    public IReadOnlyList<OutputVariable> GetOutputContract() => new CommunicationToolResults(OutputFields).GetOutputVariables();
}

public sealed class ProtocolAssemblyPart : ObservableObject
{
    public Guid PartId { get; set; } = Guid.NewGuid();
    private string _inputText = "";
    public string InputText { get => _inputText; set => SetProperty(ref _inputText, value); }
}
