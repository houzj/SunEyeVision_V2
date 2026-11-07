using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using DeleteTextBox=SunEyeVision.Plugin.Wpf.Controls.References.Primitives.DeleteTextBox;

namespace SunEyeVision.Plugin.Wpf.Presenters.References;

/// <summary>The original two-popup subscription template over one borrowed accepted field state.</summary>
internal sealed class NativeReferencePresenter : Decorator,IDisposable
{
    private readonly ReferenceFieldSession _session;
    private readonly ReferenceExpansionCoordinator _expansion;
    private readonly ReferencePopupScope _scope;
    private readonly ContentControl _content;
    private DeleteTextBox? _deleteText;
    private bool _disposed;
    internal NativeReferencePresenter(DeclaredFieldState state,ReferenceFieldOptions options,Func<OutputReference,string>? format,bool allowConstantText=false)
    {
        _expansion=new(this,options.WindowOwner);
        _session=new(state,options,format,expand:_expansion.Open,preference:_expansion.Preference,allowConstantText:allowConstantText,selectVariable:_expansion.OpenVariable,isExpanded:_expansion.IsExpanded);
        try
        {
            Resources.MergedDictionaries.Add(new ResourceDictionary
            {Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/References/ReferenceField.xaml",UriKind.Relative)});
            _content=new ContentControl{Content=_session,ContentTemplate=(DataTemplate)FindResource("ButtonSelecter_sub")};Child=_content;
            _scope=new(this,_session,()=>!state.IsDisposed&&(allowConstantText||state.IsReferenceMode));
            Loaded+=OnLoaded;
        }
        catch(Exception failure)
        {
            try{ParameterCleanup.Run(new Action[]{_expansion.Dispose,_session.Dispose,()=>Child=null});}
            catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
    }
    private void OnLoaded(object sender,RoutedEventArgs args)
    {
        if(_disposed||_deleteText is not null)return;
        _deleteText=FindVisual<DeleteTextBox>(_content);
        if(_deleteText is not null)_deleteText.KeyUp+=OnDelete;
    }
    private void OnDelete(object sender,KeyEventArgs args)
    {
        if(args.Key==Key.Delete&&!_disposed&&!(_deleteText?.IsReadOnly??true))_session.ClearFromUi();
    }
    private static T? FindVisual<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T match)return match;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
            if(FindVisual<T>(VisualTreeHelper.GetChild(root,i)) is {} child)return child;
        return null;
    }
    public bool TryCommitPending()=>!_disposed&&_session.TryCommitPending();
    internal bool HasPendingEdits=>!_disposed&&_session.HasPendingText;
    internal void PrepareForVisibility(bool visible)
    {
        if(_disposed)return;
        _scope.PrepareForVisibility(visible);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        ParameterCleanup.Run(new Action[]{()=>{Loaded-=OnLoaded;if(_deleteText is not null)_deleteText.KeyUp-=OnDelete;_deleteText=null;},()=>_session.SetSuspended(true),_scope.Dispose,_expansion.Dispose,
            _session.Dispose,()=>_content.Content=null,()=>Child=null});
    }
}
