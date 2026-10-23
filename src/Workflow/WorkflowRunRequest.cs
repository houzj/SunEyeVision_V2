using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.Workflow;

/// <summary>一次根工作流运行的冻结输入。</summary>
/// <remarks>此对象只承载运行态数据，不能引用或修改方案设计对象。</remarks>
public sealed class WorkflowRunRequest
{
    public WorkflowRunRequest(
        string workflowId,
        IVisionImage? inputImage = null,
        IDictionary<string, object?>? globalVariables = null,
        IDictionary<string, object>? runtimeParameters = null,
        IDictionary<string, object>? contextVariables = null,
        IDictionary<string, IReadOnlyDictionary<string, object?>>? nodeParameterOverrides = null,
        ICameraCaptureService? cameraCaptureService = null,
        IReadOnlyDictionary<string, CameraFrame>? cameraFrames = null,
        IDictionary<string, IReadOnlyDictionary<string, object?>>? nodeBindingOverrides = null,
        SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationReceiveService? communicationInputs = null,
        SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationSendService? communicationOutputs = null)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
            throw new ArgumentException("工作流 ID 不能为空。", nameof(workflowId));

        WorkflowId = workflowId;
        CommunicationInputs = communicationInputs;
        CommunicationOutputs = communicationOutputs;
        InputImage = inputImage;
        GlobalVariables = new Dictionary<string, object?>(globalVariables ?? new Dictionary<string, object?>());
        RuntimeParameters = new Dictionary<string, object>(runtimeParameters ?? new Dictionary<string, object>());
        ContextVariables = new Dictionary<string, object>(contextVariables ?? new Dictionary<string, object>());
        NodeParameterOverrides = (nodeParameterOverrides ?? new Dictionary<string, IReadOnlyDictionary<string, object?>>())
            .ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(pair.Value));
        CameraCaptureService = cameraCaptureService;
        NodeBindingOverrides = (nodeBindingOverrides ?? new Dictionary<string, IReadOnlyDictionary<string, object?>>())
            .ToDictionary(p => p.Key, p => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(p.Value));
        CameraFrames = new Dictionary<string, CameraFrame>(cameraFrames ?? new Dictionary<string, CameraFrame>());
    }

    public string WorkflowId { get; }
    public SunEyeVision.Plugin.SDK.Execution.Parameters.IResourcePathResolver? ResourcePaths { get; init; }
    public SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationReceiveService? CommunicationInputs { get; }
    public SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationSendService? CommunicationOutputs { get; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> NodeBindingOverrides { get; }
    public IVisionImage? InputImage { get; }
    public IReadOnlyDictionary<string, object?> GlobalVariables { get; }
    public IReadOnlyDictionary<string, object> RuntimeParameters { get; }
    public IReadOnlyDictionary<string, object> ContextVariables { get; }

    /// <summary>仅应用到深拷贝后的执行参数，绝不写回方案节点参数。</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> NodeParameterOverrides { get; }
    public ICameraCaptureService? CameraCaptureService { get; }
    /// <summary>以节点 ID 寻址的本轮相机帧。宿主在执行完成后释放帧。</summary>
    public IReadOnlyDictionary<string, CameraFrame> CameraFrames { get; }
}
