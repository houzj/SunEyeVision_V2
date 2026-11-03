using SunEyeVision.Domain.Workflows;
using SunEyeVision.Domain.Solutions;

namespace SunEyeVision.Application;

/// <summary>A live document reference, not a separately persisted consumer registration.</summary>
public sealed record VariableConsumer(string WorkflowId, string WorkflowName, NodeDefinition Node, string FieldId);

public sealed partial class DocumentSession
{
    /// <summary>Derive consumers from currently admitted typed field references, including dynamic indexes.</summary>
    public IReadOnlyList<VariableConsumer> GetVariableConsumers(GlobalVariable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);
        var variableId = variable.Id;
        ArgumentException.ThrowIfNullOrWhiteSpace(variableId);
        lock (_gate)
        {
            EnsureOpen();
            var variables = Document.GlobalVariables.Where(variable => variable.Id == variableId).ToArray();
            if (Document.Id != _documentId || variables.Length != 1 || !ReferenceEquals(variables[0], variable))
                throw new InvalidOperationException("变量不属于当前方案或身份不唯一。");
            var result = new List<VariableConsumer>();
            foreach (var flow in GetWorkflows())
            {
                var scope = OpenScope(flow.Id);
                var nodeIds = scope.Flow.Nodes.Select(node => node.Id).ToArray();
                if (nodeIds.Distinct(StringComparer.Ordinal).Count() != nodeIds.Length)
                    throw new InvalidOperationException("流程节点身份不唯一。");
                foreach (var nodeId in nodeIds)
                {
                    var view = RequireNode(scope, nodeId);
                    foreach (var fieldId in BindingRelationCatalog.VariableReferences(view.Configuration)
                        .Where(item => item.Reference.SolutionId == _documentId && item.Reference.VariableId == variableId)
                        .Select(item => item.FieldId).Distinct(StringComparer.Ordinal))
                        result.Add(new(flow.Id, flow.Name, view.Node, fieldId));
                }
            }
            EnsureOpen();
            if (Document.Id != _documentId || Document.GlobalVariables.Count(variable => variable.Id == variableId) != 1 ||
                !ReferenceEquals(Document.GlobalVariables.Single(variable => variable.Id == variableId), variables[0]))
                throw new InvalidOperationException("查询期间变量或方案身份已变化。");
            return Array.AsReadOnly(result.ToArray());
        }
    }
}
