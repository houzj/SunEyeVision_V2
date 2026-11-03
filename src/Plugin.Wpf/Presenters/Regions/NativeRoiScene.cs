using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>Real parameter/image composition. Session is borrowed; no tool or execution bootstrap.</summary>
public sealed class NativeRoiScene : Grid, IDisposable
{
    private long _generation;
    private bool _disposed;
    private NativeRoiSession? _attached;
    private readonly NativeParameterRow _row;
    public NativeRoiEditor Editor {get;}=new();
    public NativeRoiViewport Viewport {get;}=new();
    public ParamListView ParameterHost {get;}=new();
    internal event Action? Disposing;
    public static readonly DependencyProperty SessionProperty=DependencyProperty.Register(nameof(Session),typeof(NativeRoiSession),typeof(NativeRoiScene),new PropertyMetadata(null,OnSession));
    public NativeRoiSession? Session
    {
        get=>(NativeRoiSession?)GetValue(SessionProperty);
        set {Dispatcher.VerifyAccess();if(_disposed&&value is not null)throw new ObjectDisposedException(nameof(NativeRoiScene));SetCurrentValue(SessionProperty,value);}
    }
    public NativeRoiScene()
    {
        Background=new SolidColorBrush(Color.FromRgb(242,243,245));
        ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(380)});
        ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        _row=new(new NativeParameterGroup("",true),Editor);
        ParameterHost.SetRows(new[]{_row});
        Children.Add(ParameterHost);SetColumn(Viewport,1);Children.Add(Viewport);
    }
    private static void OnSession(DependencyObject sender,DependencyPropertyChangedEventArgs args)=>((NativeRoiScene)sender).Attach();
    private bool Current(long generation,NativeRoiSession? target)=>!_disposed&&generation==_generation&&ReferenceEquals(Session,target);
    private void Attach()
    {
        if(_disposed)return;
        var target=Session;if(ReferenceEquals(target,_attached))return;
        var generation=++_generation;_attached=null;
        try
        {
            Editor.Bind(null);if(!Current(generation,target))return;
            Viewport.Bind(null);if(!Current(generation,target))return;
            Editor.Bind(target);if(!Current(generation,target))return;
            Viewport.Bind(target);if(!Current(generation,target))return;
            _attached=target;
        }
        catch(Exception error)
        {
            if(!Current(generation,target))throw;
            var cleanupGeneration=++_generation;
            bool OwnCleanup()=>!_disposed&&cleanupGeneration==_generation;
            NativeRoiCallbacks.Run(new Action[]{()=>throw error,
                ()=>{if(OwnCleanup())SetCurrentValue(SessionProperty,null);},
                ()=>{if(OwnCleanup()&&Session is null)Editor.Bind(null);},
                ()=>{if(OwnCleanup()&&Session is null)Viewport.Bind(null);}});
        }
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;++_generation;_attached=null;
        var handlers=Disposing?.GetInvocationList().Cast<Action>().ToArray()??[];Disposing=null;
        NativeRoiCallbacks.Run(handlers.Select(handler=>(Action)(()=>handler())).Concat(new Action[]{
            ()=>ClearValue(SessionProperty),()=>ParameterHost.SetRows(null),_row.Dispose,Editor.Dispose,Viewport.Dispose,()=>Children.Clear()}));
    }
}
