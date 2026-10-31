using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

namespace SunEyeVision.UI.Services.Images;

internal sealed class ResultWorkspaceImageSource : System.ComponentModel.INotifyPropertyChanged
{
    internal ResultWorkspaceImageSource(string displayName,OutputReference reference,IResultPublication? publication,
        Func<bool> current,int navigationIndex,bool isUpstream)
    {
        DisplayName=displayName;Reference=reference;Publication=publication;Current=current;NavigationIndex=navigationIndex;IsUpstream=isUpstream;
    }
    public string DisplayName {get;}
    public OutputReference Reference {get;}
    public IResultPublication? Publication {get;}
    public Func<bool> Current {get;}
    public int NavigationIndex {get;}
    public bool IsUpstream {get;}
    public string? FileName { get; init; }
    public string Key=>BindingReferenceSerializer.Serialize(Reference);
    public bool CanOverlay=>false;
    private bool _isCheck;
    public bool IsCheck
    {
        get=>_isCheck;
        internal set{if(_isCheck==value)return;_isCheck=value;PropertyChanged?.Invoke(this,new(nameof(IsCheck)));}
    }
    public bool IsBackgroundImage=>false;
    public string OverlayUnavailableReason=>"未实现：图像叠加暂不可用。当前勾选表示正在显示的图像。";
    public string BackgroundUnavailableReason=>"未实现：背景图切换暂不可用。";
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public override string ToString()=>DisplayName;
    internal ResultWorkspaceImageSource CreateInputItem()
        =>new(DisplayName,Reference,Publication,Current,NavigationIndex,IsUpstream){FileName=FileName};
}
internal sealed class ResultWorkspaceImagePlan:IDisposable
{
    internal readonly List<ResultWorkspaceImageSource> Sources=new();
    internal readonly List<ResultWorkspaceImageSource> InputSources=new();
    internal readonly List<string> Diagnostics=new();
    private readonly List<IResultPublication> _publications=new();
    internal bool IsDisposed {get;private set;}
    internal void Own(IResultPublication publication)=>_publications.Add(publication);
    public void Dispose()
    {
        if(IsDisposed)return;IsDisposed=true;
        var failures=new List<Exception>();
        foreach(var publication in _publications)try{publication.Dispose();}catch(Exception error){failures.Add(error);}
        _publications.Clear();InputSources.Clear();Sources.Clear();
        if(failures.Count>0)throw new AggregateException("图像来源发布释放失败。",failures);
    }
}
