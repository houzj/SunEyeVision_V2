using System;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.Workflow;

/// <summary>Builds host-owned private debug input from the admitted capability.</summary>
public static class RuntimeHostInputAdapters
{
    public static IHostInputPreparer? CreateDebug(WorkflowNodeBase node,string workflowId,ToolExecutionOwner owner,
        IResourcePathResolver? resourcePaths,ICameraCaptureService? cameraService)
    {
        var configuration=node.RuntimeConfiguration??throw new InvalidOperationException("节点缺少正式配置。");
        var declaration=configuration.Directory.GetHostInputs(configuration.ToolId);
        if(declaration is null)return null;
        if(declaration.CapabilityId!="image-source"||declaration.Version!=1)
            throw new InvalidOperationException("宿主没有该输入能力的适配器。");
        var policy=ImageSourcePolicy.ReadFields(node.Id,configuration.ReadFieldValues());
        if(policy.SourceKind==Plugin.SDK.Vision.Contracts.ImageSourceKind.Camera)
            return new ImageSourceHostInputPreparer(null,policy.SNInitialValue,cameraService);
        var resolver=resourcePaths as Persistence.SolutionResourceResolver
            ??throw new InvalidOperationException("图像源调试缺少冻结资源基目录。");
        return ImageSourceDebugCursor.For(owner).Prepare(workflowId,policy,resolver);
    }
}
