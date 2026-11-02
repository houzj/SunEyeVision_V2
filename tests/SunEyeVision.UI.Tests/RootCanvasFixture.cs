using System.IO;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Tests;
internal static class RootCanvasFixture
{
    private static readonly List<SolutionManager> Managers=new();
    private static readonly Lazy<RuntimeDirectorySnapshot> DirectoryValue=new(()=>
    {
        var root=Environment.GetEnvironmentVariable("SUNEYE_ROOT_TEST_PAYLOAD")??throw new InvalidOperationException("必须提供本工作区已封包的 Runtime 目录。");
        return new ReleasePayloadAuthority(root).PrepareRuntimeDirectory();
    });
    internal static RuntimeDirectorySnapshot Directory=>DirectoryValue.Value;
    internal static WorkflowNodeBase Node(string id)=>new(id,id,id,"ImageLoad"){RuntimeConfiguration=RuntimeToolConfiguration.Create(Directory,"ImageLoad")};
    internal static void SetValue(WorkflowNodeBase node,long value)=>node.RuntimeConfiguration=node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{"sequence.initial",value}});
    internal static long Value(WorkflowNodeBase node)=>(long)((SunEyeVision.Plugin.Contracts.ConstantSetting)node.RuntimeConfiguration!.Fields.Read("sequence.initial")).Value!;
    internal static SolutionManager Attach(Solution document)
    {
        var root=Environment.GetEnvironmentVariable("S2_CASE_ROOT")??throw new InvalidOperationException("必须提供隔离测试数据目录。");
        var manager=new SolutionManager(Path.Combine(root,Guid.NewGuid().ToString("N")),Directory);manager.SetCurrentSolution(document,false);Managers.Add(manager);return manager;
    }
    internal static void Close(){foreach(var manager in Managers)manager.CloseSolution(false);Managers.Clear();}
}
