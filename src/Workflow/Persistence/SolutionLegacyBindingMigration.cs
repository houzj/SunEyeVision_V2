using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Workflow.Persistence;

/// <summary>Candidate document owner migration. Uses frozen declarations, never current result values or display names.
/// Only unchanged declarations are accepted; no aliases or lossy schema conversion are inferred.</summary>
internal static class SolutionLegacyBindingMigration
{
    internal static void Prepare(Solution candidate, List<PersistenceDiagnostic> diagnostics, bool legacyDocument = true)
    {
        var contractReader = new OutputBindingChangeService();
        foreach (var workflow in candidate.Workflows)
        {
            foreach(var node in workflow.Nodes.Where(n=>n.RuntimeConfiguration is not null))
            {
                var runtime=node.RuntimeConfiguration!;
                var targets=runtime.BindingTargets.ToDictionary(p=>p.Id,StringComparer.Ordinal);
                var fields=runtime.SettingsSchema?.Inputs.Select(p=>p.Id).ToHashSet(StringComparer.Ordinal)??new();
                foreach(var setting in node.ParamSettings.Settings)
                {
                    if(!targets.TryGetValue(setting.ParameterName,out var port))throw new JsonException("Unknown Runtime input or settings field.");
                    if(setting.BindingType==BindingType.Constant)
                    {
                        if(fields.Contains(port.Id))throw new JsonException("Runtime setting literals cannot have duplicate binding storage.");
                        if((port.EditModes&FieldEditModes.Constant)==0)throw new JsonException("Runtime input does not allow a constant.");
                        try{RuntimeInputBindings.Constant(port,setting.ConstantValue);}
                        catch(Exception error)when(error is ArgumentException or InvalidOperationException or FormatException or OverflowException)
                        {throw new JsonException("Invalid Runtime input literal.",error);}
                    }
                    else if(setting.BindingType!=BindingType.Binding||setting.ReferenceExpression is null)
                        throw new JsonException("Runtime requires an explicit semantic reference.");
                }
            }
            foreach(var node in workflow.Nodes)
                if(node.Parameters is IStoredConfigurationValidator configuration)
                {
                    var issues=configuration.ValidateStoredConfiguration(node.ParamSettings.Settings
                        .Where(setting=>setting.BindingType!=BindingType.Constant).Select(setting=>setting.ParameterName).ToArray());
                    if(issues.Count!=0)throw new JsonException(issues[0].Message,
                        "$.Workflows[Id="+workflow.Id+"].Nodes[Id="+node.Id+"].Parameters["+issues[0].FieldId+"]",null,null);
                }
            var nodes = workflow.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
            var declarations = nodes.ToDictionary(pair => pair.Key, pair =>
            {
                var outputs = new List<Port>();
                foreach (var output in contractReader.ReadContract(pair.Value) ?? Array.Empty<OutputVariable>())
                {
                    if (output.Kind != OutputVariableKind.Data) continue;
                    try { outputs.Add(BindingClrAdapter.DescribeOutput(output)); }
                    catch (ArgumentException) { /* Unsupported declarations are unavailable, never inferred from values. */ }
                }
                return new BindingSource(new(workflow.Id, pair.Key), Guid.Empty,
                    new ToolSchema(Array.Empty<Port>(), outputs), 0);
            }, StringComparer.Ordinal);
            var pending = new List<(WorkflowNodeBase Node, ParamSetting Setting, string Expression, OutputReference Reference, bool Legacy)>();
            OutputReference Resolve(OutputReference legacy, bool approvedLegacyAliases)
            {
                if (legacy.ScopeId is not null && legacy.ScopeId != workflow.Id)
                    throw new JsonException("Legacy source is outside the owning workflow.");
                if (!nodes.TryGetValue(legacy.NodeId, out var source)) throw new JsonException("Legacy source node is missing.");
                if(source.RuntimeConfiguration is not null&&approvedLegacyAliases)
                    throw new JsonException("Runtime sources do not accept legacy binding aliases.");
                var portId = legacy.PortId;
                if (approvedLegacyAliases && portId.StartsWith("name:", StringComparison.Ordinal))
                    throw new JsonException("Legacy named output aliases are not supported.");
                var port = declarations[legacy.NodeId].Schema.Outputs.SingleOrDefault(output => output.Id == portId)
                    ?? throw new JsonException("Legacy field has no unique approved declaration.");
                if (legacy.Type != port.Type || legacy.Storage != NumericStorageRules.Effective(port.Type, port.Storage))
                    throw new JsonException("Legacy source declaration changed; explicit schema conversion is required.");
                BindingSelection selection = legacy.Selection is BindingSelection.AtField dynamicIndex
                    ? new BindingSelection.AtField(Resolve(dynamicIndex.Reference, approvedLegacyAliases)) : legacy.Selection;
                return new(legacy.NodeId, port.Id, port.Type, legacy.MemberFieldPath, selection, workflow.Id, port.Storage);
            }
            foreach (var node in workflow.Nodes)
            foreach (var setting in node.ParamSettings.Settings)
            {
                if (setting.ReferenceExpression is null) continue;
                if (setting.SourceWorkflowId is { Length: > 0 } sourceWorkflow && sourceWorkflow != workflow.Id)
                    throw new JsonException("Legacy setting scope conflicts with its owning workflow.");
                string expression;
                bool legacyEnvelope;
                using (var envelope = JsonDocument.Parse(setting.ReferenceExpression))
                {
                    var major = envelope.RootElement.GetProperty("ContractMajor").GetInt32();
                    legacyEnvelope = major == 4;
                    if(legacyEnvelope&&node.RuntimeConfiguration is not null)
                        throw new JsonException("Runtime inputs do not accept legacy binding envelopes.");
                    if (legacyEnvelope && !legacyDocument) throw new JsonException("Format 2 cannot contain legacy binding envelopes.");
                    expression = legacyEnvelope
                        ? BindingReferenceSerializer.MigrateVersion4(setting.ReferenceExpression, old => Resolve(old, true))
                        : BindingReferenceSerializer.Serialize(Resolve(BindingReferenceSerializer.Deserialize(setting.ReferenceExpression), false));
                }
                var reference = BindingReferenceSerializer.Deserialize(expression);
                if (setting.SourceNodeId is { Length: > 0 } oldNode && oldNode != reference.NodeId)
                    throw new JsonException("Legacy node identity conflicts with its typed reference.");
                if (setting.SourceFieldId is Guid oldField && reference.PortId != "field:" + oldField.ToString("N"))
                    throw new JsonException("Legacy field identity conflicts with its typed reference.");
                pending.Add((node, setting, expression, reference, legacyEnvelope));
            }
            var predecessors = nodes.Keys.ToDictionary(id => id, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
            foreach (var edge in workflow.Connections)
                if (predecessors.TryGetValue(edge.TargetNodeId, out var previous) && nodes.ContainsKey(edge.SourceNodeId)) previous.Add(edge.SourceNodeId);
            foreach (var item in pending.Where(item => item.Setting.BindingType != BindingType.Constant))
                foreach (var dependency in WorkflowBindingContext.ReferenceNodes(item.Reference, workflow.Id))
                    predecessors[item.Node.Id].Add(dependency.NodeId);
            foreach (var item in pending)
            {
                Port target;
                if(item.Node.RuntimeConfiguration is { } runtime)
                {
                    target=runtime.BindingTargets.SingleOrDefault(port=>port.Id==item.Setting.ParameterName)
                        ??throw new JsonException("Runtime consumer input is not declared.");
                    if((target.EditModes&FieldEditModes.Reference)==0)throw new JsonException("Runtime input does not allow a reference.");
                }
                else
                {
                    var property = item.Node.Parameters.GetType().GetProperty(item.Setting.ParameterName);
                    var declared=(item.Node.Parameters as IConfigurationFieldProvider)?.GetConfigurationFields()
                        .SingleOrDefault(field=>field.StoragePath==item.Setting.ParameterName);
                    if (declared is null&&property is not { CanWrite: true }) throw new JsonException("Consumer input is not declared writable.");
                    if(declared is not null&&(declared.Port.EditModes&FieldEditModes.Reference)==0)
                        throw new JsonException("This declared consumer field does not allow a reference.");
                    try { target = declared?.Port??BindingClrAdapter.DescribePort(item.Setting.ParameterName, property!.PropertyType); }
                    catch (ArgumentException exception) { throw new JsonException("Legacy consumer requires an explicit typed adapter.", exception); }
                }
                var scope = new BindingScopeSnapshot(new(workflow.Id, item.Node.Id), candidate.ExecutionOwner.Id, 0,
                    declarations.Values, declarations.Values.Select(source => source.Node),
                    predecessors.Select(pair => new KeyValuePair<BindingNodeId, IReadOnlyList<BindingNodeId>>(
                        new(workflow.Id, pair.Key), pair.Value.Select(id => new BindingNodeId(workflow.Id, id)).ToArray())));
                var compilation = new ParameterBindingService().CompileBinding(item.Reference, target, scope, item.Setting.Conversion);
                if (compilation.Plan is null) throw new JsonException(compilation.Diagnostic?.Message ?? "Legacy binding compilation failed.");
            }
            // Accept only after every member of this workflow's candidate set validated. The caller owns the unpublished document.
            foreach (var item in pending)
            {
                if (item.Setting.ReferenceExpression == item.Expression) continue;
                item.Setting.ReferenceExpression = item.Expression;
                diagnostics.Add(new(item.Legacy ? "COMPAT_BINDING_4_TO_5" : "BINDING_SCOPE_NORMALIZED", "Compatibility", "Warning",
                    "$.Workflows[Id=" + workflow.Id + "].Nodes[Id=" + item.Node.Id + "].ParamSettings[" + item.Setting.ParameterName + "]",
                    "Reference was resolved against candidate declarations and compiled in its owning workflow."));
            }
        }
        candidate.SchemaVersion = SolutionFormatPolicy.Current;
    }
}

