using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Persistence.Variables;

namespace SunEyeVision.Persistence;

/// <summary>Strictly materializes an independent candidate. Application performs admitted-definition validation and publishes it.</summary>
public static class SolutionDocumentReader
{
    public static SolutionReadCandidate Read(ReadOnlySpan<byte> utf8,IReadOnlyDictionary<string,FrozenToolDeclarations> declarations,
        FrozenVariableTypeCatalog variableTypes,JsonSerializerOptions sourceOptions,DecodedResourcesScope scope,IVariableValueExtensionCodec? extension=null,string? baseDirectory=null)
    {
        ArgumentNullException.ThrowIfNull(declarations);ArgumentNullException.ThrowIfNull(variableTypes);
        ArgumentNullException.ThrowIfNull(sourceOptions);ArgumentNullException.ThrowIfNull(scope);
        scope.ClaimDecode();
        try
        {
            if(extension is not null && !ReferenceEquals(extension.Scope,scope)) throw new InvalidOperationException("Codec and candidate must use the same decode resource scope.");
            var options=SolutionSerializationOptions.Configure(sourceOptions,extension);
            var json=new UTF8Encoding(false,true).GetString(utf8).TrimStart('\uFEFF');
            SolutionDocumentValidator.CheckDuplicateKeys(json);
            using(var inspected=JsonDocument.Parse(json))
            {
                SolutionFormatPolicy.ValidateReadable(inspected.RootElement);
                var suspended=SuspendedStructurePreflight.Inspect(inspected.RootElement);
                if(suspended.Count!=0)throw new SuspendedStructureException(suspended);
            }
            var root=JsonNode.Parse(json) as JsonObject ?? throw new JsonException("Solution root must be an object.");
            var diagnostics=new List<PersistenceDiagnostic>();
            SolutionDocumentValidator.Prepare(root,diagnostics,variableTypes,options);
            var resources=new SolutionResourceResolver(baseDirectory).Inspect(root,diagnostics,declarations);
            var workflows=Array.AsReadOnly(((JsonArray?)root["Workflows"] ?? new JsonArray()).Select(flow=>ReadWorkflow(flow!,declarations,options)).ToArray());
            return new SolutionReadCandidate(scope,utf8)
            {
                Id=Required(root,"Id"),Name=Text(root,"Name",string.Empty),Description=Text(root,"Description",string.Empty),Version=Text(root,"Version","1.0"),
                AdditionalData=Value<Dictionary<string,JsonElement>?>(root,"AdditionalData",options,null),
                Workflows=workflows,GlobalVariables=List<GlobalVariable>(root,"GlobalVariables",options),Devices=List<Device>(root,"Devices",options),
                Communications=List<Communication>(root,"Communications",options),CommunicationEvents=List<CommunicationEventDefinition>(root,"CommunicationEvents",options),
                CommunicationSendEvents=List<CommunicationSendEventDefinition>(root,"CommunicationSendEvents",options),CommunicationHeartbeats=List<CommunicationHeartbeatDefinition>(root,"CommunicationHeartbeats",options),
                CommunicationTriggers=List<CommunicationTriggerDefinition>(root,"CommunicationTriggers",options),CommunicationResponses=Value(root,"CommunicationResponses",options,new CommunicationResponseConfiguration())!,
                DatabaseConfiguration=Value<DatabaseConfiguration?>(root,"DatabaseConfiguration",options,null),ExecutionStrategy=Value<ExecutionStrategy?>(root,"ExecutionStrategy",options,null),
                VersionHistory=List<SolutionVersion>(root,"VersionHistory",options),ResourcePlan=resources,Diagnostics=diagnostics.AsReadOnly()
            };
        }
        catch (Exception error) { scope.ReleaseAfterFailure(error); throw; }
    }
    private static WorkflowReadCandidate ReadWorkflow(JsonNode node,IReadOnlyDictionary<string,FrozenToolDeclarations> declarations,JsonSerializerOptions options)
    {
        var root=node as JsonObject ?? throw new JsonException("Workflow must be an object.");
        var nodes=Array.AsReadOnly(((JsonArray?)root["Nodes"] ?? new JsonArray()).Select(n=>ReadNode(n!,declarations)).ToArray());
        var edges=Array.AsReadOnly(((JsonArray?)root["Connections"] ?? new JsonArray()).Select(e=>ReadEdge(e!,options)).ToArray());
        return new(Required(root,"Id"),Text(root,"Name",string.Empty),Bool(root,"IsSubroutine",false),
            Value(root,"RootConfiguration",options,new RootWorkflowConfiguration())!,Value(root,"WorkspaceSettings",options,new RootWorkspaceSettings())!,nodes,edges,ReadAdditionalData(root));
    }
    private static NodeReadCandidate ReadNode(JsonNode node,IReadOnlyDictionary<string,FrozenToolDeclarations> declarations)
    {
        var root=node as JsonObject ?? throw new JsonException("Node must be an object.");
        var tool=Required(root,"ToolType");
        if(!declarations.TryGetValue(tool,out var declared)||declared.ToolId!=tool)throw new JsonException("Tool declaration is unavailable or mismatched: "+tool);
        if(root["Configuration"] is not JsonObject configuration)throw new JsonException("Node configuration is required.");
        using var document=JsonDocument.Parse(configuration.ToJsonString());var value=document.RootElement;
        var keys=value.EnumerateObject().Select(p=>p.Name).ToHashSet(StringComparer.Ordinal);
        if(!keys.SetEquals(new[]{"ToolId","Fields","CurrentValues","ParameterBindingOrder"}) || value.GetProperty("ToolId").GetString()!=tool)
            throw new JsonException("Configuration requires the same ToolId and exact fields.");
        var fields=DeclaredFieldJson.Read(declared,value.GetProperty("Fields"));
        var current=DeclaredFieldJson.Read(declared,value.GetProperty("CurrentValues"),true).ToDictionary(p=>p.Key,p=>p.Value as ConstantSetting ?? throw new JsonException("Current values require constants."),StringComparer.Ordinal);
        var order=value.GetProperty("ParameterBindingOrder").EnumerateArray().Select(p=>p.ValueKind==JsonValueKind.String?p.GetString()!:throw new JsonException("Binding order requires field identities.")).ToArray();
        var ports=declared.Schema.Inputs.Concat(declared.SettingsSchema.Inputs).Concat(declared.DisplaySchema.Inputs);
        var config=new ToolConfiguration(tool,new FieldSettings(ports,fields),current,parameterBindingOrder:order);
        return new(Required(root,"Id"),Text(root,"Name",string.Empty),Text(root,"DispName",string.Empty),tool,Int(root,"GlobalIndex",-1),Int(root,"LocalIndex",-1),Bool(root,"IsEnabled",true),
            OptionalText(root,"SubroutineId"),OptionalText(root,"SelectedDisplayReference"),Finite(root,"PositionX",0),Finite(root,"PositionY",0),Finite(root,"Width",140),Finite(root,"Height",90),config,ReadAdditionalData(root));
    }
    private static IReadOnlyDictionary<string,JsonElement>? ReadAdditionalData(JsonObject root)
    {
        if (root["AdditionalData"] is null) return null;
        if (root["AdditionalData"] is not JsonObject value) throw new JsonException("AdditionalData must be an object.");
        using var doc=JsonDocument.Parse(value.ToJsonString());
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string,JsonElement>(doc.RootElement.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.Clone(),StringComparer.Ordinal));
    }
    private static ConnectionReadCandidate ReadEdge(JsonNode node,JsonSerializerOptions options)
    {
        var root=node as JsonObject ?? throw new JsonException("Connection must be an object.");
        var manual=Value<List<CanvasPoint>?>(root,"ManualPathPoints",options,null);
        return new(Required(root,"Id"),Required(root,"SourceNodeId"),Required(root,"TargetNodeId"),Text(root,"SourcePort",string.Empty),Text(root,"TargetPort",string.Empty),manual is null?null:Array.AsReadOnly(manual.ToArray()));
    }
    private static IReadOnlyList<T> List<T>(JsonObject root,string name,JsonSerializerOptions options)
        => Array.AsReadOnly((Value<List<T>?>(root,name,options,null) ?? new List<T>()).ToArray());
    private static T? Value<T>(JsonObject root,string name,JsonSerializerOptions options,T? fallback)
        => root.TryGetPropertyValue(name,out var node) && node is not null ? JsonSerializer.Deserialize<T>(node.ToJsonString(),options) : fallback;
    private static string Required(JsonObject root,string name) => OptionalText(root,name) is { Length:>0 } value && !string.IsNullOrWhiteSpace(value) ? value : throw new JsonException(name+" is required.");
    private static string? OptionalText(JsonObject root,string name) => root[name] is null ? null : root[name]!.GetValue<string>();
    private static string Text(JsonObject root,string name,string fallback) => OptionalText(root,name) ?? fallback;
    private static int Int(JsonObject root,string name,int fallback) => root[name] is null ? fallback : root[name]!.GetValue<int>();
    private static bool Bool(JsonObject root,string name,bool fallback) => root[name] is null ? fallback : root[name]!.GetValue<bool>();
    private static double Finite(JsonObject root,string name,double fallback) { var number=root[name] is null?fallback:root[name]!.GetValue<double>();return double.IsFinite(number)?number:throw new JsonException("Node layout must be finite: "+name); }
}
