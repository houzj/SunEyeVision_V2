using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.UI.Services.Images;

/// <summary>One UI-owned configuration acceptance; no pixels, result authority or execution is transferred.</summary>
internal sealed class SourceDisplayPreservationScope(Func<bool> current) : IDisposable
{
    internal bool IsCurrent=>current();
    private List<Action<IReadOnlyList<ScopedNodeIdentity>?>>? _participants=new();
    internal void Enlist(Action<IReadOnlyList<ScopedNodeIdentity>?> participant)
        => (_participants??throw new ObjectDisposedException(nameof(SourceDisplayPreservationScope))).Add(participant);
    internal void Accept(IReadOnlyList<ScopedNodeIdentity> affected)=>Complete(affected.ToArray());
    public void Dispose()=>Complete(null);
    private void Complete(IReadOnlyList<ScopedNodeIdentity>? affected)
    {
        var participants=_participants;_participants=null;
        if(participants is null)return;
        var failures=new List<Exception>();
        foreach(var participant in participants)try{participant(affected);}catch(Exception error){failures.Add(error);}
        if(failures.Count>0)throw new AggregateException("配置显示凭证结束失败。",failures);
    }
}
