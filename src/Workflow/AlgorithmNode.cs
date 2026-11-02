using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// 算法节点
    /// </summary>
    public partial class AlgorithmNode : WorkflowNodeBase
    {
/// <summary>运行请求提供的共享采集服务，不属于节点持久化状态。</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public IResourcePathResolver? ResourcePaths { get; set; }
        public ICameraCaptureService? CameraCaptureService { get; set; }
        public SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationReceiveService? CommunicationInputs { get; set; }
        public SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationSendService? CommunicationOutputs { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string? ExecutionWorkflowId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public ToolExecutionOwner? ExecutionOwner { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public Guid ExecutionRunId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public Guid ExecutionRootRunId { get; set; }
        internal ToolExecutionIdentity? AdmittedIdentity { get; set; }
        internal WorkflowBindingContext? BindingContext { get; set; }
        internal IReadOnlyDictionary<string,SunEyeVision.Plugin.Contracts.BindingPlan> CompiledBindings { get; set; }
            = new Dictionary<string,SunEyeVision.Plugin.Contracts.BindingPlan>();
        internal SunEyeVision.Plugin.Contracts.ToolSchema? BindingOutputSchema { get; set; }
        internal IReadOnlyList<SunEyeVision.Plugin.Contracts.OutputValueRequest> OutputRequests { get; set; }
            = Array.Empty<SunEyeVision.Plugin.Contracts.OutputValueRequest>();
        internal IVisionImage? WorkflowInputImage { get; set; }

        /// <summary>本轮运行在启动节点前绑定的帧；节点不拥有其生命周期。</summary>
        public CameraFrame? CameraFrame { get; set; }

        /// <summary>
        /// 上次执行结果
        /// </summary>
        public AlgorithmResult LastResult { get; private set; }

        /// <summary>
        /// 上次工具执行结果（保留原始完整结果）
        /// </summary>
        public DeclaredOutputSnapshot? LastToolResult { get; private set; }

        public AlgorithmNode(string id,string name,string dispName,RuntimeToolConfiguration configuration)
            :base(id,name,dispName,configuration.ToolId)
        {RuntimeConfiguration=configuration.Copy();}

        /// <summary>
        /// 执行节点算法（带绑定解析）
        /// </summary>
        /// <param name="nodeResults">其他节点的执行结果缓存（用于动态绑定解析）</param>
        /// <param name="globalVariables">本次工作流运行的全局变量</param>
        /// <param name="cancellationToken">本次工作流运行的取消令牌</param>
        public AlgorithmResult Execute(
            IDictionary<string, DeclaredOutputSnapshot>? nodeResults,
            IDictionary<string, object?>? globalVariables = null,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(nodeResults, globalVariables, cancellationToken).GetAwaiter().GetResult();

        /// <summary>
        /// 异步执行节点。工作流主链使用此入口，使异步工具不会被同步阻塞。
        /// </summary>
        public async Task<AlgorithmResult> ExecuteAsync(
            IDictionary<string, DeclaredOutputSnapshot>? nodeResults,
            IDictionary<string, object?>? globalVariables = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
            {
                return AlgorithmResult.CreateSuccess(
                    null,
                    0);
            }

            OnBeforeExecute();

            try
            {
                if (RuntimeConfiguration is not null)
                {
                    var result=await ExecuteRuntimeNodeAsync(nodeResults,cancellationToken).ConfigureAwait(false);
                    if(cancellationToken.IsCancellationRequested||AdmittedIdentity?.IsCurrent==false)
                    {result.Dispose();cancellationToken.ThrowIfCancellationRequested();throw new InvalidOperationException("Runtime节点身份已失效，拒绝发布结果。");}
                    StoreLastToolResult(result);
                    LastResult=ConvertToAlgorithmResult(result);
                }
                else
                {
                    throw new InvalidOperationException("节点没有已准入的Runtime配置；旧SDK工具执行已隔离。");
                }

                OnAfterExecute(LastResult);
                return LastResult;
            }
            catch (OperationCanceledException)
            {
                var result = AlgorithmResult.CreateCancelled();
                OnAfterExecute(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = AlgorithmResult.CreateError($"节点 {Name} 执行失败: {ex.Message}");
                OnAfterExecute(result);
                return result;
            }
        }



        /// <summary>
        /// 将 DeclaredOutputSnapshot 转换为 AlgorithmResult
        /// </summary>
        private void StoreLastToolResult(DeclaredOutputSnapshot result)
        {
            var previous = LastToolResult;
            LastToolResult = result;
            if (!ReferenceEquals(previous, result)) previous?.Dispose();
        }
        internal void ReleaseLastResult()
        {
            LastToolResult?.Dispose();
            LastToolResult = null;
        }

        private AlgorithmResult ConvertToAlgorithmResult(DeclaredOutputSnapshot result)
        {
            if (result.Status == ExecutionStatus.Cancelled)
            {
                var cancelled = AlgorithmResult.CreateCancelled(result.ErrorMessage ?? "执行被取消");
                cancelled.Result = result;
                return cancelled;
            }

            if (!result.IsSuccess)
            {
                var failed = AlgorithmResult.CreateError(result.ErrorMessage ?? "处理失败");
                // Retain this execution's diagnostic payload without changing failure semantics.
                failed.Result = result;
                return failed;
            }

            var algorithmResult = AlgorithmResult.CreateSuccess(null, result.ExecutionTimeMs);
            algorithmResult.Result = result;
            return algorithmResult;
        }



    }
}
