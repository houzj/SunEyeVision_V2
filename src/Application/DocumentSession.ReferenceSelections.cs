using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

public enum ReferenceSelectionFieldKind { Input, Settings, Display }

/// <summary>Admitted output declaration and actual source node; no published value or resource is retained.</summary>
public sealed record ReferenceOutputSelection(NodeDefinition Actual, ToolSchema Schema,
    IReadOnlyList<ManualReferenceSource> ManualSources);

/// <summary>A variable's actual identity and its declared binding type, never inferred from its current value.</summary>
public sealed record ReferenceVariableSelection(GlobalVariable Actual, string Name, string Group, int Index,
    string Description, VariableReference Reference);

public sealed record ReferenceSelections(Guid EditSessionId, string WorkflowId, NodeDefinition Target,
    string FieldId, Port TargetPort, ReferenceSelectionFieldKind FieldKind,
    ParameterReferenceBehavior ReferenceBehavior, IReadOnlyList<ReferenceOutputSelection> Outputs,
    IReadOnlyList<ReferenceVariableSelection> Variables, IReadOnlyList<string> Diagnostics);

public sealed partial class DocumentSession
{
    /// <summary>Capture admitted reference declarations for one actual node/field. The caller must
    /// requery before selection and submit the resulting candidate through ValidateEdit/CommitEdit.
    /// No invocation output or borrowed value is owned by this snapshot.</summary>
    public ReferenceSelections CaptureReferenceSelections(string workflowId, NodeDefinition target, string fieldId)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldId);
        lock (_gate)
        {
            var scope = OpenScope(workflowId);
            var targetView = RequireNode(scope, target.Id);
            if (!ReferenceEquals(targetView.Node, target))
                throw new InvalidOperationException("目标节点不属于当前方案会话或已被替换。");
            var targetConfiguration = target.Configuration;
            var matches = targetView.Configuration.Schema.Inputs.Select(port => (port, ReferenceSelectionFieldKind.Input))
                .Concat(targetView.Configuration.SettingsSchema.Inputs.Select(port => (port, ReferenceSelectionFieldKind.Settings)))
                .Concat(targetView.Configuration.DisplaySchema.Inputs.Select(port => (port, ReferenceSelectionFieldKind.Display)))
                .Where(item => item.port.Id == fieldId).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("目标字段缺失或正式声明身份不唯一。");
            var (port, kind) = matches[0];
            if ((port.EditModes & FieldEditModes.Reference) == 0 &&
                !(kind == ReferenceSelectionFieldKind.Display && (port.EditModes & FieldEditModes.DisplayExpression) != 0))
                throw new InvalidOperationException("目标字段不支持引用模式。");
            var behavior = targetView.Configuration.ReferenceBehaviors.GetValueOrDefault(fieldId,
                ParameterReferenceBehavior.StrictExactlyOne);
            var graph = CaptureGraphQuery(scope);
            var incoming = graph.Edges.GroupBy(edge => edge.Target, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(edge => edge.Source).ToArray(), StringComparer.Ordinal);
            var predecessors = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>(); pending.Push(target.Id);
            while (pending.TryPop(out var current))
                if (incoming.TryGetValue(current, out var sources))
                    foreach (var source in sources) if (predecessors.Add(source)) pending.Push(source);
            predecessors.Remove(target.Id);
            if (kind == ReferenceSelectionFieldKind.Display) predecessors.Add(target.Id);
            var revision = ChangeRevision;
            var results = new List<ReferenceOutputSelection>();
            var diagnostics = new List<string>();
            foreach (var node in scope.Flow.Nodes.Where(node => predecessors.Contains(node.Id)))
            {
                try
                {
                    var admitted = RequireNode(scope, node.Id);
                    if (!ReferenceEquals(admitted.Node, node)) throw new InvalidOperationException("来源节点实例已变化。");
                    var manual = admitted.Configuration.ManualReferenceSources.Select(source =>
                        new ManualReferenceSource(source.PortId, source.MemberPath)).ToArray();
                    results.Add(new(node, admitted.Configuration.Schema, Array.AsReadOnly(manual)));
                }
                catch (InvalidOperationException error)
                { diagnostics.Add(node.DispName + "：来源声明不可用：" + error.Message); }
            }
            var variables = new List<ReferenceVariableSelection>();
            foreach (var selection in kind == ReferenceSelectionFieldKind.Display
                ? Array.Empty<VariableSelection>() : CaptureVariableSelections())
            {
                if (selection.Type == "Image" && port.Type != TypeRef.ResourceOf(ResourceKind.Image)) continue;
                if (!VariableValueDeclarations.TryGetConsumerPort(selection.Type, out var variablePort))
                { diagnostics.Add(selection.Name + "：变量类型尚无正式引用声明。"); continue; }
                var actual = selection.Actual;
                variables.Add(new(actual, selection.Name, actual.Group, actual.Index, actual.Description,
                    new VariableReference(_documentId, selection.Id, variablePort.Type, storage: variablePort.Storage)));
            }
            ValidateScopeCore(scope);
            if (ChangeRevision != revision || !ReferenceEquals(target.Configuration, targetConfiguration) ||
                !scope.Flow.Nodes.Any(node => ReferenceEquals(node, target)))
                throw new InvalidOperationException("查询期间方案、目标节点或配置已变化，请重新选择来源。");
            return new(EditSessionId, scope.WorkflowId, target, fieldId, port, kind, behavior,
                Array.AsReadOnly(results.ToArray()), Array.AsReadOnly(variables.ToArray()), Array.AsReadOnly(diagnostics.ToArray()));
        }
    }
}
