using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.References;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>Projects immutable runtime declarations; captions never identify a field.</summary>
internal static class DeclaredReferenceCandidateBuilder
{
    internal static IReadOnlyList<string> MemberNames(OutputDisplayNode? output, IReadOnlyList<string> path)
    {
        IReadOnlyList<OutputDisplayNode> display = output?.Children ?? Array.Empty<OutputDisplayNode>();
        var names = new List<string>();
        foreach (var id in path)
        {
            var item = display.SingleOrDefault(item => item.FieldId == id);
            names.Add(item?.DisplayName ?? id);
            display = item?.Children ?? Array.Empty<OutputDisplayNode>();
        }
        return names.AsReadOnly();
    }

    internal static IReadOnlyList<DeclaredReferenceCandidate> Build(ToolSchema schema, Port port, string nodeId, string? scope, string nodeName, int moduleNumber)
    {
        if (string.IsNullOrWhiteSpace(scope)) throw new InvalidOperationException("来源缺少工作流作用域。");
        var display = schema.FindOutputDisplay(port.Id);
        var caption = string.IsNullOrWhiteSpace(display?.DisplayName) ? port.Id : display.DisplayName;
        var candidates = new List<DeclaredReferenceCandidate>();
        void Add(TypeRef type, IReadOnlyList<string> path)
        {
            var names = MemberNames(display, path);
            candidates.Add(new(ReferenceText.Format(moduleNumber, nodeName, caption, path, names), nodeId, scope, port, path)
                { NodeDisplayName = nodeName, OutputDisplayName = caption, MemberDisplayNames = names });
            while (type.Shape == ValueShape.Sequence) type = type.Element!;
            if (type.Shape == ValueShape.Record)
                foreach (var member in type.Fields) Add(member.Type, path.Append(member.Id).ToArray());
        }
        Add(port.Type, Array.Empty<string>());
        return candidates.AsReadOnly();
    }
}
