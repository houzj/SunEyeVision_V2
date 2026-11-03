using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

/// <summary>Presentation-only references use front/self declarations and never create execution edges.</summary>
internal static class CandidateDisplayValidation
{
    internal static void Validate(string workflowId, Guid ownerId, long revision, GraphState graph,
        IReadOnlySet<string> participants, bool validateAll,
        Func<NodeDefinition, ToolConfiguration, AdmittedToolConfiguration> admit)
    {
        var configurations = graph.Nodes.ToDictionary(item => item.Node.Id, item => admit(item.Node, item.Configuration), StringComparer.Ordinal);
        var incoming = graph.Edges.GroupBy(edge => edge.TargetNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.SourceNodeId).ToArray(), StringComparer.Ordinal);
        foreach (var item in graph.Nodes)
        {
            if (!validateAll && !participants.Contains(item.Node.Id)) continue;
            var configuration = configurations[item.Node.Id];
            if (configuration.DisplaySchema.Inputs.Count == 0) continue;
            var eligible = new HashSet<string>(StringComparer.Ordinal) { item.Node.Id };
            var pending = new Stack<string>(); pending.Push(item.Node.Id);
            while (pending.TryPop(out var current))
                if (incoming.TryGetValue(current, out var sources))
                    foreach (var source in sources) if (eligible.Add(source)) pending.Push(source);
            var declarations = eligible.Select(id => new BindingSource(new(workflowId, id), Guid.Empty, configurations[id].Schema, 0)).ToArray();
            var scope = new ResultDisplayBindingScope(new(workflowId, item.Node.Id), ownerId, revision, declarations);
            var compiler = new ParameterBindingService();
            foreach (var port in configuration.DisplaySchema.Inputs)
            {
                var setting = configuration.Configuration.Fields.Read(port.Id);
                if (setting is VariableSetting) throw new InvalidOperationException("显示字段不接受全局变量来源。");
                void ValidateReference(OutputReference reference, string? format, bool expression, BindingConversion conversion)
                {
                    var selected = ParameterReferenceRules.SelectedType(reference);
                    while (selected.Shape == ValueShape.Sequence) selected = selected.Element!;
                    if (selected.Shape != ValueShape.Scalar || selected.Scalar is not (ScalarKind.Int64 or ScalarKind.Float64 or ScalarKind.Text))
                        throw new InvalidOperationException("显示文本来源必须是已声明的数值或文字。");
                    if (format is not null && selected.Scalar is not (ScalarKind.Int64 or ScalarKind.Float64))
                        throw new InvalidOperationException("显示数值格式只能用于数值来源。");
                    var expected = expression ? selected : port.Type;
                    while (expected.Shape == ValueShape.Sequence) expected = expected.Element!;
                    var target = new Port(port.Id, TypeRef.SequenceOf(expected), Nullable: true);
                    var compiled = compiler.CompileBinding(reference, target, scope, conversion);
                    if (compiled.Plan is null) throw new InvalidOperationException(compiled.Diagnostic?.Message ?? "显示来源未通过前序或自身声明校验。");
                }
                if (setting is ReferenceSetting reference)
                {
                    if (!string.IsNullOrEmpty(reference.RebindingReason)) throw new InvalidOperationException(reference.RebindingReason);
                    ValidateReference(reference.Reference, null, false, reference.Conversion);
                }
                else if (setting is DisplayExpressionSetting expression)
                    foreach (var slot in expression.Slots)
                    {
                        var source = slot.Reference;
                        if (source is null)
                        {
                            var path = slot.Self!;
                            var selfPort = configuration.Schema.Outputs.SingleOrDefault(output => output.Id == path.PortId)
                                ?? throw new InvalidOperationException("显示自身输出字段不存在。");
                            source = new OutputReference(item.Node.Id, selfPort.Id, selfPort.Type, path.MemberFieldPath,
                                scopeId: workflowId, storage: selfPort.Storage);
                        }
                        ValidateReference(source, slot.Format, true, BindingConversion.Automatic);
                    }
            }
        }
    }
}