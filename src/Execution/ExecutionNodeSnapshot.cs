using System.Collections.ObjectModel;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Execution;

/// <summary>A session-issued, immutable execution view of the one admitted tool configuration.</summary>
public sealed class ExecutionNodeSnapshot
{
    internal ExecutionNodeSnapshot(string workflowId, string nodeId, ToolConfiguration configuration,
        IToolDefinition definition, RuntimeDirectorySnapshot directory, ToolSchema schema, ToolSchema settingsSchema,
        ToolSchema displaySchema, IReadOnlyDictionary<string, ParameterReferenceBehavior> referenceBehaviors,
        IReadOnlyDictionary<string, string> bindingNames, IReadOnlySet<string> activeInputs, FieldSettings dormantParameterDefaults,
        ToolHostDeclarationSnapshot hostDeclarations, IReadOnlyList<ManualReferenceSource> manualReferenceSources,
        ToolExecutionOwner owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId); ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentNullException.ThrowIfNull(configuration); ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(directory); ArgumentNullException.ThrowIfNull(owner);
        if (!directory.IsActive || owner.IsRetired || definition.ToolId != configuration.ToolId)
            throw new InvalidOperationException("执行快照的工具、作用域或准入目录不一致。");
        WorkflowId = workflowId; NodeId = nodeId; Configuration = configuration; Definition = definition;
        Directory = directory; Schema = schema; SettingsSchema = settingsSchema; DisplaySchema = displaySchema;
        var capabilities = definition.Capabilities() ?? throw new InvalidOperationException("工具缺少运行资源声明。");
        Capabilities = new ToolCapabilities(Array.AsReadOnly((capabilities.ResourceKeys
            ?? throw new InvalidOperationException("工具运行资源键缺失。")).ToArray()));
        var projection = (definition as SunEyeVision.Plugin.Contracts.Results.Presentation.IToolResultPresentationProvider)?.ResultProjection;
        HasResultDisplay = definition is SunEyeVision.Plugin.Contracts.Results.Presentation.IToolResultDisplayProvider;
        RequiredDisplayOutputs = Array.AsReadOnly((definition as SunEyeVision.Plugin.Contracts.Results.Presentation.IToolResultDisplayProvider)
            ?.ResultDisplay.RequiredOutputs.Select(request => new OutputValueRequest(request.PortId, request.FieldPath)).ToArray()
            ?? Array.Empty<OutputValueRequest>());
        foreach (var request in RequiredDisplayOutputs) request.Validate(schema);
        var shown = projection?.Kind == SunEyeVision.Plugin.Contracts.Results.Presentation.ResultProjectionKind.PropertyList
            ? projection.Columns.Where(column => column.Value is not null).Select(column => column.Value!.PortId).ToHashSet(StringComparer.Ordinal) : null;
        ResultFields = new ReadOnlyDictionary<string, DeclaredOutputField>(schema.Outputs.ToDictionary(port => port.Id,
            port => new DeclaredOutputField(port.Id, schema.FindOutputDisplay(port.Id)?.DisplayName ?? port.Id,
                "", null, null, null, shown is null || shown.Contains(port.Id)), StringComparer.Ordinal));
        ReferenceBehaviors = new ReadOnlyDictionary<string, ParameterReferenceBehavior>(
            new Dictionary<string, ParameterReferenceBehavior>(referenceBehaviors, StringComparer.Ordinal));
        BindingNames = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(bindingNames, StringComparer.Ordinal));
        ActiveInputs = System.Collections.Frozen.FrozenSet.ToFrozenSet(activeInputs, StringComparer.Ordinal);
        _dormantParameterDefaults = dormantParameterDefaults;
        HostDeclarations = new ToolHostDeclarationSnapshot(hostDeclarations.HostInputs,
            Array.AsReadOnly(hostDeclarations.ResourceFields.ToArray()), hostDeclarations.Fingerprint);
        ArgumentNullException.ThrowIfNull(manualReferenceSources);
        ManualReferenceSources = Array.AsReadOnly(manualReferenceSources.Select(source =>
            new ManualReferenceSource(source.PortId, Array.AsReadOnly(source.MemberPath.ToArray()))).ToArray());
        Owner = owner;
    }

    public string WorkflowId { get; }
    public string NodeId { get; }
    internal string OwnerDomainId => MakeOwnerDomainId(WorkflowId, NodeId);
    internal static string MakeOwnerDomainId(string workflowId, string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        // Match the existing formal and debug admission/retirement identity.
        return workflowId + "/" + nodeId;
    }
    public ToolConfiguration Configuration { get; }
    public ToolSchema Schema { get; }
    public ToolSchema SettingsSchema { get; }
    public ToolSchema DisplaySchema { get; }
    public ToolCapabilities Capabilities { get; }
    public IReadOnlyDictionary<string, DeclaredOutputField> ResultFields { get; }
    public bool HasResultDisplay { get; }
    internal IReadOnlyList<OutputValueRequest> RequiredDisplayOutputs { get; }
    public IReadOnlyDictionary<string, ParameterReferenceBehavior> ReferenceBehaviors { get; }
    public IReadOnlyDictionary<string, string> BindingNames { get; }
    public IReadOnlySet<string> ActiveInputs { get; }
    private readonly FieldSettings _dormantParameterDefaults;
    public ToolHostDeclarationSnapshot HostDeclarations { get; }
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources { get; }
    public Guid DocumentOwnerId => Owner.Id;
    internal IToolDefinition Definition { get; }
    internal RuntimeDirectorySnapshot Directory { get; }
    internal ToolExecutionOwner Owner { get; }
    internal IReadOnlyList<Port> BindingTargets => Array.AsReadOnly(Schema.Inputs.Concat(SettingsSchema.Inputs).Concat(DisplaySchema.Inputs).ToArray());

    // Keep the existing execution order: referenced parameter fields first, then all other
    // active non-display fields. A retained current value never becomes a second input source.
    internal IReadOnlyList<KeyValuePair<string, ParameterSetting>> CaptureExecutionFields()
    {
        var display = DisplaySchema.Inputs.Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
        var inputs = Schema.Inputs.Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
        var fields = Configuration.Fields.Capture()
            .Where(pair => !display.Contains(pair.Key) && !_dormantParameterDefaults.Declarations.ContainsKey(pair.Key)
                && (!inputs.Contains(pair.Key) || ActiveInputs.Contains(pair.Key)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var order = Configuration.ParameterBindingOrder;
        var referenced = order.ToHashSet(StringComparer.Ordinal);
        return Array.AsReadOnly(order.Where(fields.ContainsKey)
            .Select(id => new KeyValuePair<string, ParameterSetting>(id, fields[id]))
            .Concat(fields.Where(pair => !referenced.Contains(pair.Key))).ToArray());
    }

    internal object MaterializeSettings(IReadOnlyDictionary<string, object?> resolved)
    {
        if (Definition is not IToolSettingsFields fields)
            throw new InvalidOperationException("工具缺少正式设置声明。");
        var settings = Configuration.Fields.Select(SettingsSchema.Inputs);
        if (_dormantParameterDefaults.Declarations.Count != 0)
            settings = settings.Apply(_dormantParameterDefaults.Capture().ToDictionary(pair=>pair.Key,pair=>
                pair.Value is ConstantSetting {Value:null}&&settings.Declarations[pair.Key] is {Required:false,EditModes:FieldEditModes.Reference}
                    ? (ParameterSetting)new UnsetSetting() : pair.Value,StringComparer.Ordinal));
        return Definition.FreezeSettings(fields.MaterializeSettings(settings.Materialize(resolved)));
    }
}
