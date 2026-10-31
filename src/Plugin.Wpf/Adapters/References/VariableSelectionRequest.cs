using SunEyeVision.Plugin.Wpf.Controls.References.Variables;

namespace SunEyeVision.Plugin.Wpf.Adapters.References;

/// <summary>Borrowed selection lease. It does not own a field value or bypass its proposal validation.</summary>
public sealed class VariableSelectionRequest:IDisposable
{
    private ReferenceFieldSession? _session;
    private readonly long _generation,_acceptance,_uiGeneration,_candidateGeneration;
    public IReadOnlyList<VariableSelectionItem> Items {get;}
    public bool IsIndex {get;}
    public bool IsValid=>_session?.IsVariableRequestCurrent(this,_generation,_acceptance,_uiGeneration,_candidateGeneration)==true;
    public event Action? Invalidated;
    internal VariableSelectionRequest(ReferenceFieldSession session,long generation,long acceptance,long uiGeneration,long candidateGeneration,IReadOnlyList<VariableSelectionItem> items,bool isIndex=false)
    {_session=session;_generation=generation;_acceptance=acceptance;_uiGeneration=uiGeneration;_candidateGeneration=candidateGeneration;Items=items;IsIndex=isIndex;}
    public bool TrySelect(VariableSelectionItem item)=>IsValid&&Items.Any(candidate=>ReferenceEquals(candidate,item))&&_session!.SelectVariable(this,item);
    internal void Invalidate()
    {
        if(_session is null)return;_session=null;
        try{Invalidated?.Invoke();}finally{Invalidated=null;}
    }
    public void Dispose()
    {
        var session=_session;if(session is null)return;
        session.ReleaseVariableRequest(this,_generation);Invalidate();
    }
}
