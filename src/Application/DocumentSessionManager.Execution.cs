using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

public sealed partial class DocumentSessionManager
{
    /// <summary>Capture a run from the currently installed actual document, never from a caller-provided copy.</summary>
    public DocumentRunBatch CaptureCurrentRunBatch(IEnumerable<string> workflowIds,
        IReadOnlyDictionary<ScopedNodeIdentity, IHostInputPreparer>? hostInputs = null,
        IReadOnlyDictionary<string, WorkflowImageInput>? workflowImages = null)
    {
        EnsureAvailable();
        var session = CurrentSession ?? throw new InvalidOperationException("当前没有打开的方案。 ");
        _busy = true;
        try
        {
            var batch = session.CaptureRunBatch(workflowIds, hostInputs, workflowImages);
            if (!ReferenceEquals(CurrentSession, session))
            {
                batch.Dispose();
                throw new InvalidOperationException("运行捕获期间活动方案已变化。 ");
            }
            return batch;
        }
        finally { _busy = false; }
    }
}
