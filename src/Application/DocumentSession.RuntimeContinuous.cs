using SunEyeVision.Domain.Workflows;
using SunEyeVision.Execution;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    /// <summary>Reserve a root and capture the first round on the real owner thread.</summary>
    public DocumentContinuousRun CaptureContinuousRun(IReadOnlyList<string> workflowIds,
        IHostInputFactory? hostFactory = null, Action<Action>? ownerPost = null)
    {
        EnsureOwnerCanEdit(); ArgumentNullException.ThrowIfNull(workflowIds);
        var selected = workflowIds.ToArray();
        var context = SynchronizationContext.Current;
        var post = ownerPost ?? (context is not null ? action => context.Post(_ => action(), null) :
            throw new InvalidOperationException("没有所属线程同步上下文；中立宿主必须提供所属线程投递器。"));
        var reservation = new ExecutionRunBatch(_owner,_directory);
        DocumentRunBatch? first = null;
        try
        {
            first = CaptureRunBatchCore(selected,null,null,hostFactory,reservation);
            var flows = GetWorkflows();
            var settings = selected.ToDictionary(id => id,
                id => flows.Single(flow => flow.Id == id && !flow.IsSubroutine).WorkspaceSettings, StringComparer.Ordinal);
            return new DocumentContinuousRun(this,reservation,first,settings,hostFactory,post);
        }
        catch(Exception failure)
        {
            var errors=new List<Exception> {failure};
            if(first is not null)try{first.Dispose();}catch(Exception error){errors.Add(error);}
            try{reservation.Dispose();}catch(Exception error){errors.Add(error);}
            if(errors.Count>1)throw new AggregateException("连续运行准备及释放失败。",errors);
            throw;
        }
    }
    internal DocumentRunBatch CaptureContinuousRound(IReadOnlyList<string> workflowIds,
        IHostInputFactory? hostFactory, ExecutionRunBatch reservation)
        => CaptureRunBatchCore(workflowIds,null,null,hostFactory,reservation);
}
