using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Workflow.Persistence;

namespace SunEyeVision.Workflow;

/// <summary>Private editor-owner sequence. It never mutates the production workflow cursor.</summary>
internal sealed class ImageSourceDebugCursor
{
    private static readonly ConditionalWeakTable<ToolExecutionOwner,ImageSourceDebugCursor> Owners=new();
    internal static ImageSourceDebugCursor For(ToolExecutionOwner owner)=>Owners.GetValue(owner,_=>new());
    private readonly object _gate=new();
    private readonly LocalImageSequenceService _sequences=new();
    private string? _signature;
    private string? _workflowId;
    private long _generation;
    private bool _active;

    internal ImageSourceRunInput Prepare(string workflowId,ImageSourcePolicy policy,SolutionResourceResolver resolver)
    {
        var plan=ImageSourcePolicy.CreateLocalPlan(policy.NodeId,policy,resolver);
        if(plan.Entries.Count==0)
            throw new InvalidOperationException("当前来源没有可运行的图像。");
        var signature=JsonSerializer.Serialize(new {workflowId,plan});
        lock(_gate)
        {
            if(_active)throw new InvalidOperationException("图像源调试读取仍在执行或排空。");
            if(_signature!=signature)
            {
                // Begin validates the replacement before publishing it. Keep the previous
                // sequence alive if validation fails, so reverting a draft remains usable.
                var previousWorkflowId=_workflowId;
                var previousGeneration=_generation;
                var generation=_sequences.Begin(workflowId,new[]{plan});
                if(previousWorkflowId is not null&&previousWorkflowId!=workflowId)
                    _sequences.End(previousWorkflowId,previousGeneration);
                _generation=generation;_workflowId=workflowId;_signature=signature;
            }
            var ticket=_sequences.GetInputs(workflowId,_generation).Single();
            _active=true;
            return new ImageSourceRunInput(ticket.Entry?.Address??string.Empty,policy.SNInitialValue)
            {
                LocalReadAdmission=identity=>_sequences.AcceptRead(ticket,identity.RequestId,true,identity.CancellationToken).Entry.Address,
                ReadFinished=()=>Finish(ticket)
            };
        }
    }

    private void Finish(LocalImageRunInput ticket)
    {
        lock(_gate)
        {
            try { _sequences.FinishReadRound(ticket.WorkflowId,ticket.RunGeneration,ticket.IterationVersion); }
            finally { _active=false; }
        }
    }
}
