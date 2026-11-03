using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    private void PrepareRemovedRootBindings(FrozenRootCollection collection,
        Dictionary<WorkflowDefinition, GraphState> graphs, IReadOnlyList<GlobalVariable> variables)
    {
        foreach (var (flow, policy) in collection.Draft.Deletions)
        {
            var nodeIds = flow.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
            var hardReference = Document.Devices.Any(device => device.WorkflowRef == flow.Id) ||
                Document.CommunicationTriggers.Any(trigger => trigger.TargetId == flow.Id ||
                    trigger.ParameterMappings.Any(mapping => nodeIds.Contains(mapping.TargetNodeId))) ||
                Document.CommunicationResponses.WorkflowIdentifiers.ContainsKey(flow.Id) ||
                Document.CommunicationEvents.SelectMany(item => item.AssemblyOutputs).Any(item => nodeIds.Contains(item.SourceNodeId)) ||
                Document.CommunicationSendEvents.SelectMany(item => item.AssemblyOutputs).Any(item => nodeIds.Contains(item.SourceNodeId));
            if (hardReference) throw new InvalidOperationException("流程仍被设备或通信配置引用，请先解除引用。");
            var produced = variables.Where(variable => variable.ProducerBinding is { } producer &&
                producer.SourceReference.ScopeId == flow.Id).ToArray();
            if (produced.Length == 0) continue;
            if (policy == RootDeletionPolicy.RejectReferenced)
                throw new InvalidOperationException("流程仍为全局变量提供数据，请先解除引用或明确标记待重绑。");
            const string reason = "流程已删除，请重新绑定数据来源";
            foreach (var variable in produced)
            {
                variable.ProducerBinding = null;
                variable.RebindingReason = reason;
                variable.BindingValidationPending = string.Empty;
            }
            var variableIds = produced.Select(variable => variable.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var (targetFlow, graph) in graphs.ToArray())
            {
                var replacements = new Dictionary<NodeDefinition, ToolConfiguration>(ReferenceEqualityComparer.Instance);
                foreach (var item in graph.Nodes)
                {
                    var admitted = AdmitConfiguration(item.Node, item.Configuration);
                    var changes = new Dictionary<string, ParameterSetting>(StringComparer.Ordinal);
                    foreach (var field in BindingRelationCatalog.VariableReferences(admitted)
                        .Where(reference => reference.Reference.SolutionId == _documentId && variableIds.Contains(reference.Reference.VariableId))
                        .Select(reference => reference.FieldId).Distinct(StringComparer.Ordinal))
                    {
                        var setting = admitted.Configuration.Fields.Read(field);
                        if (setting is VariableSetting value) changes.Add(field, value with { RebindingReason = reason });
                        else if (setting is ReferenceSetting reference) changes.Add(field, reference with { RebindingReason = reason });
                    }
                    if (changes.Count != 0) replacements.Add(item.Node, admitted.WithFieldSettings(changes).Configuration);
                }
                if (replacements.Count != 0) graphs[targetFlow] = graph.WithConfigurations(replacements);
            }
        }
    }
}
