using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>One declared catalogue projection for node, tool and root editors; captions never identify a field.</summary>
internal static class DeclaredReferenceCandidateBuilder
{
    internal static IReadOnlyList<string> MemberNames(OutputVariable? output,IReadOnlyList<string> path)
    {
        IReadOnlyList<OutputDisplayNode> display=output?.DisplayMembers??Array.Empty<OutputDisplayNode>();
        var names=new List<string>();
        foreach(var id in path)
        {
            var item=display.SingleOrDefault(item=>item.FieldId==id);
            names.Add(item?.DisplayName??id);display=item?.Children??Array.Empty<OutputDisplayNode>();
        }
        return names.AsReadOnly();
    }
    internal static IReadOnlyList<DeclaredReferenceCandidate> Build(OutputVariable output,string nodeId,string? scope,string nodeName)
    {
        var port=BindingClrAdapter.DescribeOutput(output);
        var caption=string.IsNullOrWhiteSpace(output.DisplayName)?output.Name:output.DisplayName;
        var candidates=new List<DeclaredReferenceCandidate>();
        void Add(TypeRef type,IReadOnlyList<string> path)
        {
            var names=MemberNames(output,path);
            candidates.Add(new(nodeName+"."+(names.Count==0?caption:names[^1]),nodeId,scope,port,path)
                {NodeDisplayName=nodeName,OutputDisplayName=caption,MemberDisplayNames=names});
            while(type.Shape==ValueShape.Sequence)type=type.Element!;
            if(type.Shape==ValueShape.Record)foreach(var member in type.Fields)Add(member.Type,path.Append(member.Id).ToArray());
        }
        Add(port.Type,Array.Empty<string>());return candidates.AsReadOnly();
    }
}
