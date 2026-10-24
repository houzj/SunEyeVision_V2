using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private ToolResults RunSynchronousCore(IToolPlugin tool,ToolExecutionRequest request)
    {
        var parameters=request.Parameters;var watch=Stopwatch.StartNew();
        parameters.NodeResults=request.NodeResults??new Dictionary<string,ToolResults>();
        try
        {
            request.CancellationToken.ThrowIfCancellationRequested();
            ToolExecutionPreparation.Apply(parameters,parameters.NodeResults);
            var input=ImageFrameProvenance.CaptureInput(parameters.ResolvedInputImage);
            var result=tool.Run(parameters);result.ExecutionIdentity=request.ExecutionIdentity;
            if(request.CancellationToken.IsCancellationRequested||request.ExecutionIdentity?.IsCurrent==false)
            {
                (result as IDisposable)?.Dispose();
                return Terminal(ExecutionStatus.Cancelled,"Execution was cancelled or admission expired.");
            }
            ImageFrameProvenance.RecordResult(result,input,(tool as IImageFramePolicyProvider)?.ImageFramePolicy??ImageFramePolicy.Unknown);
            result.ExecutionTimeMs=watch.Elapsed.TotalMilliseconds;ApplyJudgmentStatus(parameters,result);return result;
        }
        catch(OperationCanceledException fault) when(request.CancellationToken.IsCancellationRequested&&fault.CancellationToken==request.CancellationToken)
        {return Terminal(ExecutionStatus.Cancelled,fault.Message);}
        catch(Exception fault){return Terminal(ExecutionStatus.Failed,fault.Message,fault);}
        ToolResults Terminal(ExecutionStatus status,string message,Exception? fault=null)
        {var result=CreateTerminalResult(tool.ResultType,status,message,fault);result.ExecutionIdentity=request.ExecutionIdentity;return result;}
    }
}
