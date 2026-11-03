using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Execution;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    private (ExecutionVariableValues Values, List<ExecutionGlobalProducerOperation> Producers) CaptureRuntimeVariables(WorkflowDefinition[] flows, GlobalVariable[] definitions)
    {
                    var names = definitions.ToDictionary(variable => variable.Id, variable => variable.Name, StringComparer.Ordinal);
                    var sources = new List<BindingVariableSource>(); var initial = new Dictionary<string, object?>(StringComparer.Ordinal);
                    var issues = new Dictionary<string, Diagnostic>(StringComparer.Ordinal);
                    var producers = new List<ExecutionGlobalProducerOperation>();
                    foreach (var variable in definitions)
                    {
                        if (!GlobalProducerBindingRules.TryDescribeTarget(variable.Type, out var port, out var valueType, out _)) continue;
                        BindingNodeId? producer = null;
                        if (variable.ProducerBinding is { } binding)
                        {
                            var reason = variable.RebindingReason ?? variable.BindingValidationPending;
                            if (string.IsNullOrEmpty(reason))
                            {
                                var validation = GlobalProducerBindingRules.Validate(binding, variable,
                                    id => flows.SingleOrDefault(flow => flow.Id == id)?.Nodes,
                                    node => AdmitConfiguration(node, node.Configuration).Schema,
                                    () => Document.Id == _documentId && flows.Length == Document.Workflows.Count &&
                                        flows.Where((flow, index) => !ReferenceEquals(flow, Document.Workflows[index])).Any() == false);
                                if (validation.State is BindingCompatibility.Incompatible or BindingCompatibility.Unresolved) reason = validation.Reason;
                                else
                                {
                                    var reference = binding.SourceReference;
                                    producer = new(reference.ScopeId!, reference.NodeId);
                                    producers.Add(new ExecutionGlobalProducerOperation(variable.Id, variable.Name, reference, port, valueType));
                                }
                            }
                            if (!string.IsNullOrEmpty(reason)) issues.Add(variable.Id, new Diagnostic("variable-unavailable", reason, variable.Id));
                        }
                        sources.Add(new BindingVariableSource(new VariableReference(_documentId, variable.Id, port.Type, storage: port.Storage), producer));
                        if (!issues.ContainsKey(variable.Id)) initial.Add(variable.Id, variable.Value);
                    }
                    return (new ExecutionVariableValues(_documentId, sources, names, initial, issues), producers);
    }
}
