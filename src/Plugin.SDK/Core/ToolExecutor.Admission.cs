using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Execution.Helpers;

namespace SunEyeVision.Plugin.SDK.Core;

public interface IExecutionAdmissionProvider
{
    Func<bool>? ExecutionAdmission { get; }
    ToolExecutionOwner? RuntimeExecutionOwner => null;
}

public partial class ToolExecutor
{
    private static IDisposable ClaimRequest(ToolExecutionRequest request)
    {
        request.ClaimExecution();
        var ownership = new RequestOwnership(request);
        try { request.FreezeParameters(); return ownership; }
        catch { ownership.Dispose(); throw; }
    }
    private sealed class RequestOwnership(ToolExecutionRequest request):IDisposable
    {
        private ToolExecutionRequest? _request=request;
        public void Dispose()=>Interlocked.Exchange(ref _request,null)?.Dispose();
    }
    private static ToolExecutionRequest LegacyRequest(IToolPlugin tool, ToolParameters parameters,
        IDictionary<string,ToolResults>? results,string? nodeName,string? nodeId,CancellationToken cancellation,
        IDictionary<string,object?>? globals)
    {
        if(tool==null)throw new ArgumentNullException(nameof(tool));
        if(parameters==null)throw new ArgumentNullException(nameof(parameters));
        return new(parameters)
        {
            NodeResults=results,NodeName=nodeName,NodeId=nodeId??tool.ParamsType.Name,
            ExecutionOwner=(parameters as IExecutionAdmissionProvider)?.RuntimeExecutionOwner,
            CancellationToken=cancellation,GlobalVariables=globals,ResolvedInputImage=parameters.ResolvedInputImage,
            CommunicationInputs=parameters.CommunicationInputs,CommunicationOutputs=parameters.CommunicationOutputs
        };
    }

    private static IDisposable EnterExecution(ToolExecutionRequest request,IToolPlugin tool)
    {
        if(request.ExecutionLeaseHeld)throw new InvalidOperationException("This execution request is already running.");
        if(request.ExecutionIdentity==null)
        {
            var owner=request.ExecutionOwner??request.DefaultExecutionOwner??new ToolExecutionOwner();
            var scope=request.RunId==Guid.Empty?Guid.NewGuid():request.RunId;
            var node=request.NodeId??tool.ParamsType.Name;
            request.ExecutionIdentity=owner.Begin((request.WorkflowId??"standalone")+"/"+node,scope,ToolContract.Version,
                request.CancellationToken,(request.Parameters as IExecutionAdmissionProvider)?.ExecutionAdmission,request.RootRunId);
        }
        var identity=request.ExecutionIdentity;
        if(request.ExecutionOwner!=null&&!ReferenceEquals(identity.Owner,request.ExecutionOwner))
            throw new InvalidOperationException("Admitted execution belongs to another owner.");
        if(request.CancellationToken.IsCancellationRequested && !identity.HasStartedExecution)
            return new AdmissionLease(request,identity,new EmptyLease());
        try
        {
            var lease=identity.Owner.EnterRun(identity);request.ExecutionLeaseHeld=true;
            return new AdmissionLease(request,identity,lease);
        }
        catch{if(!identity.HasStartedExecution)identity.CompleteCallbacks();throw;}
    }
    private sealed class EmptyLease : IDisposable { public void Dispose() { } }
    private sealed class AdmissionLease(ToolExecutionRequest request,ToolExecutionIdentity identity,IDisposable lease):IDisposable
    {
        private IDisposable? _lease=lease;
        public void Dispose()
        {
            var active=Interlocked.Exchange(ref _lease,null);if(active==null)return;
            identity.CompleteCallbacks();request.ExecutionLeaseHeld=false;active.Dispose();
        }
    }
    // Rejected late results never reach a caller. Close both ownership contracts, preserving cleanup failures.
    private static void ReleaseRejectedResult(ToolResults result)
    {
        var failures=new List<Exception>();
        try{result.Resources?.Dispose();}catch(Exception error){failures.Add(error);}
        try{(result as IDisposable)?.Dispose();}catch(Exception error){failures.Add(error);}
        if(failures.Count>0)throw new AggregateException("Rejected tool result cleanup failed.",failures);
    }
    // Capture independent facts first; always drain the rejected result, including capture failure.
    // A new terminal result carries no original output/resources/Tag/display/ROI state.
    private static ToolResults RejectResult(Type resultType, ToolResults rejected, ToolExecutionRequest request)
    {
        ToolSideEffectReceipt? receipt = null;
        var failures = new List<Exception>();
        try { receipt = (rejected as IToolSideEffectReceiptSource)?.CaptureSideEffectReceipt(); }
        catch (Exception error) { failures.Add(error); }
        try { ReleaseRejectedResult(rejected); }
        catch (Exception error) { failures.Add(error); }
        if (failures.Count > 0) return SdkTerminal("Rejected tool receipt capture or cleanup failed.");
        if (receipt is null) return SdkTerminal("运行已取消或身份失效。");

        ToolResults? candidate = null;
        try
        {
            candidate = CreateTerminalResult(resultType, ExecutionStatus.Cancelled, "运行已取消或身份失效；已发生的发送不能撤回。");
            // Both inspectors and their enumerators are plugin code. Materialize them before
            // the final non-virtual ownership check; a successful inspector may still mutate this result.
            var images = candidate.GetDisplayImages().ToArray();
            var outputs = candidate.GetOutputVariables().ToArray();
            if (candidate.Resources is null && candidate is not IDisposable && candidate.Tag is null
                && !candidate.Display.HasItems && candidate.InspectionGeometries.Length == 0
                && candidate.MaskGeometries.Length == 0 && candidate.DeclaredOutput is null
                && images.All(image => image.Value is null)
                && outputs.All(output => output.RuntimeValue is null))
            {
                candidate.SideEffectReceipt = receipt;
                request.AttachResultMetadata(candidate);
                return candidate;
            }
        }
        catch (Exception error) { failures.Add(error); }
        if (candidate is not null)
            try { ReleaseRejectedResult(candidate); } catch (Exception error) { failures.Add(error); }
        return SdkTerminal("运行已取消或身份失效；已发生的发送收据已保留，工具终态构造未满足无输出/无资源约束。");

        ToolResults SdkTerminal(string message)
        {
            // Do not re-enter a plugin constructor when capture, inspection or cleanup fails.
            var terminal = new GenericToolResults
            {
                Status = failures.Count == 0 ? ExecutionStatus.Cancelled : ExecutionStatus.Failed,
                IsPass = false, ErrorMessage = message,
                ErrorStackTrace = failures.Count == 0 ? null : new AggregateException(message, failures).ToString(),
                SideEffectReceipt = receipt
            };
            request.AttachResultMetadata(terminal);
            return terminal;
        }
    }
    private ToolResults RunSynchronousCore(IToolPlugin tool,ToolExecutionRequest request)
    {
        var parameters=request.Parameters;var watch=Stopwatch.StartNew();
        parameters.NodeResults=request.NodeResults??new Dictionary<string,ToolResults>();
        try
        {
            request.CancellationToken.ThrowIfCancellationRequested();
            ToolExecutionPreparation.Apply(parameters,parameters.NodeResults);
            var input=ImageFrameProvenance.CaptureInput(parameters.ResolvedInputImage);
            var inputAddress=ImageFrameProvenance.CaptureAddress(parameters.ResolvedInputImage);
            var result=tool.Run(parameters);request.AttachResultMetadata(result);
            if(request.CancellationToken.IsCancellationRequested||request.ExecutionIdentity?.IsCurrent==false)
            {
                return RejectResult(tool.ResultType, result, request);
            }
            ImageFrameProvenance.RecordResult(result,input,(tool as IImageFramePolicyProvider)?.ImageFramePolicy??ImageFramePolicy.Unknown,
                inputAddress);
            result.ExecutionTimeMs=watch.Elapsed.TotalMilliseconds;ApplyJudgmentStatus(parameters,result);return result;
        }
        catch(OperationCanceledException fault) when(request.CancellationToken.IsCancellationRequested&&fault.CancellationToken==request.CancellationToken)
        {return Terminal(ExecutionStatus.Cancelled,fault.Message);}
        catch(Exception fault){return Terminal(ExecutionStatus.Failed,fault.Message,fault);}
        ToolResults Terminal(ExecutionStatus status,string message,Exception? fault=null)
        {var result=CreateTerminalResult(tool.ResultType,status,message,fault);request.AttachResultMetadata(result);return result;}
    }
}
