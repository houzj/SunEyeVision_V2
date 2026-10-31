using SunEyeVision.Plugin.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.Workflow;

/// <summary>一次根工作流运行的冻结输入。</summary>
/// <remarks>冻结数据不能引用方案设计节点；宿主接纳能力只允许按原始运行身份提交纯参数值。</remarks>
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
        SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationSendService? communicationOutputs = null,
        IReadOnlyDictionary<string,ImageSourceRunInput>? sourceInputs = null)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
            throw new ArgumentException("工作流 ID 不能为空。", nameof(workflowId));

        WorkflowId = workflowId;
        CommunicationInputs = communicationInputs;
        CommunicationOutputs = communicationOutputs;
        InputImage = inputImage;
        GlobalVariables = (globalVariables ?? new Dictionary<string, object?>()).ToDictionary(
            pair => pair.Key, pair => GlobalVariable.CopyEditableValue(pair.Value), StringComparer.Ordinal);
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
        var sources=new Dictionary<string,ImageSourceRunInput>(StringComparer.Ordinal);
        foreach(var pair in sourceInputs??new Dictionary<string,ImageSourceRunInput>())
        {
            if(string.IsNullOrWhiteSpace(pair.Key)||pair.Value is null)throw new ArgumentException("图像源运行输入缺少节点身份或值。",nameof(sourceInputs));
            sources.Add(pair.Key,pair.Value);
        }
        SourceInputs=new System.Collections.ObjectModel.ReadOnlyDictionary<string,ImageSourceRunInput>(sources);
    }

    private IReadOnlyList<WorkflowGlobalProducerOperation> _globalProducers=Array.Empty<WorkflowGlobalProducerOperation>();
    public IReadOnlyList<WorkflowGlobalProducerOperation> GlobalProducers
    {
        get=>_globalProducers;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            var operations=value.ToArray();
            if(operations.Any(item=>item is null)
                ||operations.Select(item=>item.VariableId).Distinct(StringComparer.Ordinal).Count()!=operations.Length
                ||operations.Select(item=>item.VariableName).Distinct(StringComparer.Ordinal).Count()!=operations.Length)
                throw new ArgumentException("本批运行的每个全局变量必须有且仅有一个明确生产来源。",nameof(value));
            _globalProducers=Array.AsReadOnly(operations);
        }
    }
    public WorkflowGlobalRunValues? ProducedGlobals {get;init;}
    public RuntimeParameterValueAcceptance? ParameterValues {get;init;}
    public string WorkflowId { get; }
    public WorkflowRunConfiguration? Configuration { get; init; }
    public SunEyeVision.Plugin.Runtime.ToolExecutionOwner? ExecutionOwner { get; init; }
    public SunEyeVision.Plugin.SDK.Execution.Parameters.IResourcePathResolver? ResourcePaths { get; init; }
    public SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationReceiveService? CommunicationInputs { get; }
    public SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationSendService? CommunicationOutputs { get; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> NodeBindingOverrides { get; }
    /// <summary>Immutable source selections, separate from persistent settings and parameter bindings.</summary>
    public IReadOnlyDictionary<string,ImageSourceRunInput> SourceInputs { get; }
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
