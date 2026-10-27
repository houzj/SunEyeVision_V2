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
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/References/ReferenceField.xaml",UriKind.Relative)});
        _content=new ContentControl{Content=_session,ContentTemplate=(DataTemplate)FindResource("ButtonSelecter_sub")};Child=_content;
        _scope=new(this,_session,()=>!state.IsDisposed&&(allowConstantText||state.IsReferenceMode));
    }
    public bool TryCommitPending()=>!_disposed&&_session.TryCommitPending();
    internal void PrepareForVisibility(bool visible)
    {
        if(_disposed)return;
        _scope.PrepareForVisibility(visible);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        _expansion.Dispose();_scope.Dispose();_session.Dispose();_content.Content=null;Child=null;
    }
}
