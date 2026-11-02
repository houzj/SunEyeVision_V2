using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Tests;

internal static class ImageSourceTestRuntime
{
    internal static WorkflowNodeBase Node(string id="source")=>new(id,id,id,"ImageLoad")
        {RuntimeConfiguration=RuntimeToolConfiguration.Create(RootCanvasFixture.Directory,"ImageLoad")};
    internal static ImageSourcePolicy Read(WorkflowNodeBase node)=>ImageSourcePolicy.Read(node)!;
    internal static void Edit(WorkflowNodeBase node,Action<ImageSourcePolicy> change)
    {var policy=Read(node);change(policy);policy.ApplyTo(node);}
}
