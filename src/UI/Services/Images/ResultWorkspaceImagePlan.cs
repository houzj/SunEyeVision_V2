using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

namespace SunEyeVision.UI.Services.Images;

internal sealed record ResultWorkspaceImageSource(string DisplayName,OutputReference Reference,
    IResultPublication? Publication,Func<bool> Current,int NavigationIndex,bool IsUpstream)
{
    public string Key=>BindingReferenceSerializer.Serialize(Reference);
    public bool CanOverlay=>false;
    public bool IsCheck=>false;
    public bool IsBackgroundImage=>false;
    public override string ToString()=>DisplayName;
}
internal sealed class ResultWorkspaceImagePlan:IDisposable
{
    internal readonly List<ResultWorkspaceImageSource> Sources=new();
    internal readonly List<string> Diagnostics=new();
    private readonly List<IResultPublication> _publications=new();
    internal bool IsDisposed {get;private set;}
    internal void Own(IResultPublication publication)=>_publications.Add(publication);
    public void Dispose()
    {
        if(IsDisposed)return;IsDisposed=true;
        var failures=new List<Exception>();
        foreach(var publication in _publications)try{publication.Dispose();}catch(Exception error){failures.Add(error);}
        _publications.Clear();
        if(failures.Count>0)throw new AggregateException("图像来源发布释放失败。",failures);
    }
}
