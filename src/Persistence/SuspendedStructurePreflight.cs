using System.Text.Json;

namespace SunEyeVision.Persistence;

/// <summary>Evidence-only V4 content retained for a paused control-flow kind; never an executable node.</summary>
public sealed record SuspendedStructure(string Path,string Kind,JsonElement Content);
public sealed class SuspendedStructureException : InvalidOperationException
{
    public SuspendedStructureException(IReadOnlyList<SuspendedStructure> structures)
        : base("方案包含当前暂停的 Group / 控制流结构，未发布活动方案。") => Structures=structures;
    public IReadOnlyList<SuspendedStructure> Structures { get; }
}
public static class SuspendedStructurePreflight
{
    public static IReadOnlyList<SuspendedStructure> Inspect(JsonElement root)
    {
        var result=new List<SuspendedStructure>();
        if(!root.TryGetProperty("Workflows",out var workflows)||workflows.ValueKind!=JsonValueKind.Array)return result.AsReadOnly();
        var w=0;foreach(var workflow in workflows.EnumerateArray())
        {
            var path=$"$.Workflows[{w++}]";
            if(workflow.ValueKind!=JsonValueKind.Object)continue;
            if(workflow.TryGetProperty("IsSubroutine",out var sub)&&sub.ValueKind==JsonValueKind.True)
                result.Add(new(path,"SubroutineWorkflow",workflow.Clone()));
            if(!workflow.TryGetProperty("Nodes",out var nodes)||nodes.ValueKind!=JsonValueKind.Array)continue;
            var n=0;foreach(var node in nodes.EnumerateArray())
            {
                var nodePath=path+$".Nodes[{n++}]";
                if(node.ValueKind!=JsonValueKind.Object||!node.TryGetProperty("ToolType",out var type)||type.ValueKind!=JsonValueKind.String)continue;
                var name=type.GetString();
                if(string.Equals(name,"Subroutine",StringComparison.OrdinalIgnoreCase)||string.Equals(name,"Condition",StringComparison.OrdinalIgnoreCase))
                    result.Add(new(nodePath,name!,node.Clone()));
            }
        }
        return result.AsReadOnly();
    }
}
