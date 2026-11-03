using System.Text.Json;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Persistence.Variables;

namespace SunEyeVision.Persistence;

/// <summary>Synchronous owner-gated encoding of the current V4 domain document, followed by strict candidate decoding.</summary>
public static class SolutionDocumentWriter
{
    public static EncodedSolutionDocument Capture(SolutionDefinition document,
        IReadOnlyDictionary<string,FrozenToolDeclarations> declarations, FrozenVariableTypeCatalog variableTypes,
        JsonSerializerOptions options, IVariableValueExtensionCodec? extension = null, string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(document); ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(variableTypes); ArgumentNullException.ThrowIfNull(options);
        if (document.SchemaVersion != 4) throw new JsonException("Only V4 documents can be encoded.");
        var jsonOptions=SolutionSerializationOptions.Configure(options,extension);
        jsonOptions.PropertyNamingPolicy=null; jsonOptions.DictionaryKeyPolicy=null;
        using var stream=new MemoryStream();
        using (var writer=new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("Id",document.Id); writer.WriteString("Name",document.Name); writer.WriteString("Description",document.Description);
            writer.WriteNumber("SchemaVersion",4); writer.WriteString("Version",document.Version);
            WriteValue(writer,"AdditionalData",document.AdditionalData,jsonOptions);
            writer.WriteStartArray("Workflows");
            foreach(var workflow in document.Workflows)
            {
                writer.WriteStartObject(); writer.WriteString("Id",workflow.Id); writer.WriteString("Name",workflow.Name);
                writer.WriteBoolean("IsSubroutine",workflow.IsSubroutine);
                WriteValue(writer,"AdditionalData",workflow.AdditionalData,jsonOptions);
                WriteValue(writer,"RootConfiguration",workflow.RootConfiguration,jsonOptions);
                WriteValue(writer,"WorkspaceSettings",workflow.WorkspaceSettings,jsonOptions);
                writer.WriteStartArray("Nodes");
                foreach(var node in workflow.Nodes)
                {
                    writer.WriteStartObject(); writer.WriteString("Id",node.Id); writer.WriteString("Name",node.Name); writer.WriteString("DispName",node.DispName);
                    writer.WriteString("ToolType",node.ToolType); writer.WriteNumber("GlobalIndex",node.GlobalIndex); writer.WriteNumber("LocalIndex",node.LocalIndex);
                    writer.WriteBoolean("IsEnabled",node.IsEnabled); writer.WriteString("SubroutineId",node.SubroutineId); writer.WriteString("SelectedDisplayReference",node.SelectedDisplayReference);
                    writer.WriteNumber("PositionX",node.PositionX); writer.WriteNumber("PositionY",node.PositionY); writer.WriteNumber("Width",node.Width); writer.WriteNumber("Height",node.Height);
                    WriteValue(writer,"AdditionalData",node.AdditionalData,jsonOptions);
                    writer.WriteStartObject("Configuration"); writer.WriteString("ToolId",node.Configuration.ToolId);
                    writer.WritePropertyName("Fields"); ParameterSettingCanonical.Write(writer,node.Configuration.Fields.Capture());
                    writer.WritePropertyName("CurrentValues"); ParameterSettingCanonical.Write(writer,node.Configuration.CaptureCurrentValues().ToDictionary(p=>p.Key,p=>(ParameterSetting)p.Value,StringComparer.Ordinal));
                    WriteValue(writer,"ParameterBindingOrder",node.Configuration.ParameterBindingOrder,jsonOptions);
                    writer.WriteEndObject(); writer.WriteEndObject();
                }
                writer.WriteEndArray(); writer.WriteStartArray("Connections");
                foreach(var connection in workflow.Connections)
                {
                    writer.WriteStartObject(); writer.WriteString("Id",connection.Id); writer.WriteString("SourceNodeId",connection.SourceNodeId);
                    writer.WriteString("TargetNodeId",connection.TargetNodeId); writer.WriteString("SourcePort",connection.SourcePort); writer.WriteString("TargetPort",connection.TargetPort);
                    WriteValue(writer,"ManualPathPoints",connection.ManualPathPoints,jsonOptions); writer.WriteEndObject();
                }
                writer.WriteEndArray(); writer.WriteEndObject();
            }
            writer.WriteEndArray();
            WriteValue(writer,"GlobalVariables",document.GlobalVariables,jsonOptions); WriteValue(writer,"Devices",document.Devices,jsonOptions);
            WriteValue(writer,"Communications",document.Communications,jsonOptions); WriteValue(writer,"CommunicationEvents",document.CommunicationEvents,jsonOptions);
            WriteValue(writer,"CommunicationSendEvents",document.CommunicationSendEvents,jsonOptions); WriteValue(writer,"CommunicationHeartbeats",document.CommunicationHeartbeats,jsonOptions);
            WriteValue(writer,"CommunicationTriggers",document.CommunicationTriggers,jsonOptions); WriteValue(writer,"CommunicationResponses",document.CommunicationResponses,jsonOptions);
            WriteValue(writer,"DatabaseConfiguration",document.DatabaseConfiguration,jsonOptions); WriteValue(writer,"ExecutionStrategy",document.ExecutionStrategy,jsonOptions);
            WriteValue(writer,"VersionHistory",document.VersionHistory,jsonOptions); writer.WriteEndObject();
        }
        var bytes=stream.ToArray();
        var scope=extension?.Scope ?? new DecodedResourcesScope();
        using var candidate=SolutionDocumentReader.Read(bytes,declarations,variableTypes,options,scope,extension,baseDirectory);
        return new EncodedSolutionDocument(bytes,candidate.Diagnostics,candidate.ResourcePlan,baseDirectory);
    }
    private static void WriteValue<T>(Utf8JsonWriter writer,string name,T value,JsonSerializerOptions options)
    { writer.WritePropertyName(name); JsonSerializer.Serialize(writer,value,options); }
}

/// <summary>Owns only validated bytes and detached diagnostics; never owns a live document or borrowed image.</summary>
public sealed class EncodedSolutionDocument
{
    private readonly byte[] _bytes;
    private readonly IReadOnlyList<PersistenceDiagnostic> _diagnostics;
    private readonly IReadOnlyList<FrozenResourceReference> _resources;
    private readonly string? _baseDirectory;
    internal EncodedSolutionDocument(byte[] bytes,IReadOnlyList<PersistenceDiagnostic> diagnostics,IReadOnlyList<FrozenResourceReference> resources,string? baseDirectory)
    { _bytes=bytes.ToArray(); _diagnostics=Array.AsReadOnly(diagnostics.ToArray()); _resources=Array.AsReadOnly(resources.ToArray()); _baseDirectory=baseDirectory is null?null:Path.GetFullPath(baseDirectory); }
    public byte[] CopyBytes() => _bytes.ToArray();
    /// <summary>Rebase declared file references for Save As/Copy, then strictly validate the new bytes.</summary>
    public EncodedSolutionDocument Rebase(string targetDirectory, IReadOnlyDictionary<string,FrozenToolDeclarations> declarations,
        FrozenVariableTypeCatalog variableTypes, JsonSerializerOptions options, IVariableValueExtensionCodec? extension = null)
    {
        if (_baseDirectory is null) throw new InvalidOperationException("原方案目录未知，无法重定位相对资源。");
        targetDirectory=Path.GetFullPath(targetDirectory);
        var json=System.Text.Encoding.UTF8.GetString(_bytes);
        var rebased=SolutionResourceResolver.RebaseCopy(json,_baseDirectory,targetDirectory,declarations);
        var bytes=System.Text.Encoding.UTF8.GetBytes(rebased);
        var scope=extension?.Scope ?? new DecodedResourcesScope();
        using var candidate=SolutionDocumentReader.Read(bytes,declarations,variableTypes,options,scope,extension,targetDirectory);
        return new EncodedSolutionDocument(bytes,candidate.Diagnostics,candidate.ResourcePlan,targetDirectory);
    }
    public FrozenSolutionDocument BindTarget(string targetPath,TargetExpectation? expected=null)
    {
        var targetDirectory=Path.GetDirectoryName(Path.GetFullPath(targetPath));
        if (_resources.Any(r=>!Path.IsPathRooted(r.Reference)) &&
            (_baseDirectory is null || !string.Equals(Path.TrimEndingDirectorySeparator(_baseDirectory),Path.TrimEndingDirectorySeparator(targetDirectory!),StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("跨目录保存含相对资源的方案须先完成资源引用重定位及重新验证。");
        return new(targetPath,_bytes,_diagnostics,_resources,expected);
    }
}
