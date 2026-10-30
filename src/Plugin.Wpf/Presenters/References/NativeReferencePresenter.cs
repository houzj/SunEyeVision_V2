using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;

namespace SunEyeVision.Plugin.Wpf.Presenters.References;

/// <summary>The original two-popup subscription template over one borrowed accepted field state.</summary>
internal sealed class NativeReferencePresenter : Decorator,IDisposable
{
    private readonly ReferenceFieldSession _session;
    private readonly ReferenceExpansionCoordinator _expansion;
    private readonly ReferencePopupScope _scope;
    private readonly ContentControl _content;
    private bool _disposed;
    internal NativeReferencePresenter(DeclaredFieldState state,ReferenceFieldOptions options,Func<OutputReference,string>? format,bool allowConstantText=false)
    {
        _expansion=new(this);
        _session=new(state,options,format,expand:_expansion.Open,preference:_expansion.Preference,allowConstantText:allowConstantText);
        try
        {
            Resources.MergedDictionaries.Add(new ResourceDictionary
            {Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/References/ReferenceField.xaml",UriKind.Relative)});
            _content=new ContentControl{Content=_session,ContentTemplate=(DataTemplate)FindResource("ButtonSelecter_sub")};Child=_content;
            _scope=new(this,_session,()=>!state.IsDisposed&&(allowConstantText||state.IsReferenceMode));
        }
        catch(Exception failure)
        {
            try{ParameterCleanup.Run(new Action[]{_expansion.Dispose,_session.Dispose,()=>Child=null});}
            catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
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
        ParameterCleanup.Run(new Action[]{()=>_session.SetSuspended(true),_scope.Dispose,_expansion.Dispose,
            _session.Dispose,()=>_content.Content=null,()=>Child=null});
    }
}
