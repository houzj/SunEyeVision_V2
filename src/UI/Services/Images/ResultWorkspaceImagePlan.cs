using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

namespace SunEyeVision.UI.Services.Images;

internal sealed class ResultWorkspaceImageSource : System.ComponentModel.INotifyPropertyChanged
{
    internal ResultWorkspaceImageSource(string displayName,OutputReference reference,IResultPublication? publication,
        Func<bool> current,bool isUpstream,bool hasImageValue)
    {
        DisplayName=displayName;Reference=reference;Publication=publication;Current=current;IsUpstream=isUpstream;
        HasImageValue=hasImageValue;
        Key=ParameterSettingCanonical.Serialize(reference);
    }
    internal ResultWorkspaceImageSource(string displayName, string key, FrozenImageResource? variableImage, Func<bool> current)
    {
        DisplayName = displayName;
        Key = key;
        VariableImage = variableImage;
        Current = current;
        IsUpstream = true;
        HasImageValue = variableImage is not null;
    }
    public string DisplayName {get;}
    public OutputReference? Reference {get;}
    internal FrozenImageResource? VariableImage {get;}
    public IResultPublication? Publication {get;}
    public Func<bool> Current {get;}
    public bool IsUpstream {get;}
    internal bool IsAuxiliaryImage {get;init;}
    internal bool HasImageValue {get;}
    public string? FileName { get; init; }
    public string Key {get;}
    private bool _isCheck;
    public bool IsCheck
    {
        get=>_isCheck;
        internal set
        {
            if(_isCheck==value)return;
            _isCheck=value;
            RefreshCheck();
        }
    }
    internal void RefreshCheck()=>PropertyChanged?.Invoke(this,new(nameof(IsCheck)));
    private bool _isBackground;
    public bool IsBackgroundImage
    {
        get=>_isBackground;
        internal set{if(_isBackground==value)return;_isBackground=value;PropertyChanged?.Invoke(this,new(nameof(IsBackgroundImage)));}
    }
    public string? OverlayUnavailableReason=>HasImageValue?null:"本轮未发布图像。";
    public string? BackgroundUnavailableReason=>OverlayUnavailableReason;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public override string ToString()=>DisplayName;
    internal ResultWorkspaceImageSource CreateInputItem()
        => Reference is { } reference
            ? new(DisplayName,reference,Publication,Current,IsUpstream,HasImageValue)
                {FileName=FileName,IsAuxiliaryImage=IsAuxiliaryImage}
            : new(DisplayName, Key, VariableImage, Current);
}
internal sealed class ResultWorkspaceImagePlan:IDisposable
{
    internal readonly List<ResultWorkspaceImageSource> Sources=new();
    internal readonly List<ResultWorkspaceImageSource> InputSources=new();
    internal readonly List<string> Diagnostics=new();
    internal ResultWorkspaceImageSource? DefaultOutput {get;set;}
    internal Guid? EditorScopeId {get;init;}
    internal bool IsPrivateDebug => EditorScopeId.HasValue;
    internal string? PrimaryInputKey {get;set;}
    private readonly List<IDisposable> _resources=new();
    internal bool IsDisposed {get;private set;}
    internal void Own(IDisposable resource)=>_resources.Add(resource);
    public void Dispose()
    {
        if(IsDisposed)return;IsDisposed=true;
        var failures=new List<Exception>();
        foreach(var resource in _resources)try{resource.Dispose();}catch(Exception error){failures.Add(error);}
        _resources.Clear();InputSources.Clear();Sources.Clear();DefaultOutput=null;
        if(failures.Count>0)throw new AggregateException("图像来源释放失败。",failures);
    }
}
