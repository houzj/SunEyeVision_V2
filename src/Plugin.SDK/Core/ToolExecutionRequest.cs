using System;
using System.Collections.Generic;
using System.Threading;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using ToolExecutionContext = SunEyeVision.Plugin.SDK.Execution.Parameters.ExecutionContext;

namespace SunEyeVision.Plugin.SDK.Core
{
    /// <summary>
    /// 工具的一次运行输入。
    /// </summary>
    /// <remarks>
    /// 此对象将持久化的 <see cref="Parameters"/> 与本次运行的数据分开表达。
    /// 当前为兼容既有工具，<see cref="ToolExecutor"/> 会在调用工具前将运行时字段映射到参数对象；
    /// 新入口和后续工具契约只应依赖此对象携带运行数据。
    /// </remarks>
    public sealed class ToolExecutionRequest
    {
        public ToolExecutionRequest(ToolParameters parameters)
        {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }

        /// <summary>持久化的工具配置。</summary>
        public ToolParameters Parameters { get; }

        /// <summary>本次运行可见的上游节点结果。</summary>
        public IDictionary<string, ToolResults>? NodeResults { get; init; }

        /// <summary>本次运行已解析的输入图像。</summary>
        private IVisionImage? _resolvedInputImage;
        public IVisionImage? ResolvedInputImage { get => _resolvedInputImage; init => _resolvedInputImage = value; }
        internal void SetResolvedInputImage(IVisionImage? image) => _resolvedInputImage = image;

        /// <summary>日志中的节点名称。</summary>
        public string? NodeName { get; init; }

        /// <summary>日志中的节点标识。</summary>
        public string? NodeId { get; init; }
        public string? WorkflowId { get; init; }

        /// <summary>本次运行的取消令牌。</summary>
        public CancellationToken CancellationToken { get; init; }

        /// <summary>宿主提供的应用级相机采集服务；不参与参数持久化。</summary>
        public ICameraCaptureService? CameraCaptureService { get; init; }
        public SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationReceiveService? CommunicationInputs { get; init; }
        public SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationSendService? CommunicationOutputs { get; init; }

        /// <summary>宿主在启动本轮前绑定的相机帧；所有权仍属于本轮运行请求。</summary>
        public CameraFrame? CameraFrame { get; init; }

        /// <summary>
        /// 本次运行的服务上下文。由 <see cref="ToolExecutor"/> 在执行前建立，
        /// 生命周期属于请求，不写入持久化参数。
        /// </summary>
        public ToolExecutionContext Context { get; internal set; } = ToolExecutionContext.Empty;

        /// <summary>本次运行使用的绑定定义。</summary>
        public ParameterBindingSet ParameterBindings { get; init; } = ParameterBindingSet.Empty;

        /// <summary>本次运行解析后的只读参数快照。</summary>
        public RuntimeParameterSnapshot? RuntimeParameters { get; internal set; }

        /// <summary>本次运行的全局变量。</summary>
        public IDictionary<string, object?>? GlobalVariables { get; init; }


        /// <summary>
        /// 只读输入视图。新工具应优先使用此入口，避免依赖参数对象中的旧运行时字段。
        /// </summary>
        public IToolInputReader Inputs
            => new ToolInputReader(ResolvedInputImage, NodeResults, GlobalVariables);
    }
}
