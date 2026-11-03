using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Application;

public sealed record ExecutionReadinessIssue(ScopedNodeIdentity Node, string Message);

public sealed partial class DocumentSession
{
    /// <summary>Checks declarations and bindings without starting a run or taking input ownership.</summary>
    public IReadOnlyList<ExecutionReadinessIssue> CheckWorkflowReadiness(string workflowId)
    {
        EnsureOwnerCanEdit();
        using var mutation = _owner.BeginDocumentMutation();
        lock (_gate)
        {
            EnsureOwnerCanEdit(); _preparing = true;
            try
            {
                var scope = OpenScope(workflowId);
                if (scope.Flow.IsSubroutine) throw new InvalidOperationException("暂停的子流程不能从根运行入口执行。 ");
                var graph = GraphState.Capture(scope.Flow);
                var actualVariables = Document.GlobalVariables.ToArray();
                GlobalVariable[] definitions;
                lock (GlobalVariable.ValueGate) definitions = GlobalVariable.CaptureDefinitions(actualVariables).ToArray();
                var errors = new List<ExecutionReadinessIssue>();
                foreach (var item in graph.Nodes.Where(item => item.Enabled))
                {
                    try
                    {
                        CandidateInputValidation.Validate(_documentId, workflowId, _owner.Id, ChangeRevision, graph,
                            definitions, new HashSet<string>(StringComparer.Ordinal) { item.Node.Id }, false, AdmitConfiguration);
                        var admitted = AdmitConfiguration(item.Node, item.Configuration);
                        foreach (var (_, reference) in BindingRelationCatalog.VariableReferences(admitted, executionOnly: true))
                        {
                            var variable = definitions.SingleOrDefault(value => value.Id == reference.VariableId);
                            var reason = variable?.RebindingReason ?? variable?.BindingValidationPending;
                            if (!string.IsNullOrEmpty(reason)) throw new InvalidOperationException(reason);
                        }
                    }
                    catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException or OverflowException or System.Text.Json.JsonException)
                    { errors.Add(new(new(workflowId, item.Node.Id), error.Message)); }
                }
                ValidateScopeCore(scope);
                if (!graph.MatchesCurrent(scope.Flow) || actualVariables.Length != Document.GlobalVariables.Count ||
                    actualVariables.Where((variable, index) => !ReferenceEquals(variable, Document.GlobalVariables[index])).Any())
                    throw new InvalidOperationException("运行检查期间正式方案已改变。 ");
                lock (GlobalVariable.ValueGate)
                    if (!GlobalVariable.CaptureDefinitions(actualVariables).Select(VariableConfigurationSignature)
                        .SequenceEqual(definitions.Select(VariableConfigurationSignature)))
                        throw new InvalidOperationException("运行检查期间变量声明已改变。 ");
                return errors.AsReadOnly();
            }
            finally { _preparing = false; }
        }
    }
}
