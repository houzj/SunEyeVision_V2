using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Plugin.Editing.Wpf;
using SunEyeVision.UI.Services.Editing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    private DocumentSession? _productionEditorSession;
    private NodeConfigurationEditHost? _productionEditorHost;
    private readonly HashSet<ProductionEditorContext> _productionEditors=new();
    private readonly HashSet<SunEyeVision.UI.Features.GlobalVariableManager.GlobalVariableManagerViewModel> _variableEditors=new();
    private NodeConfigurationEditHost GetProductionEditorHost(DocumentSession session)
    {
        if(!ReferenceEquals(_productionEditorSession,session))ReleaseProductionEditors();
        _productionEditorSession=session;return _productionEditorHost=session.Editing.Host;
    }
    private void ClearProductionEditorHost() { }
    private void ReleaseProductionEditors()
    {
        ReleaseProductionEditorContexts(_productionEditors);
        foreach(var model in _variableEditors.ToArray())model.Discard();
        _variableEditors.Clear();_productionEditorHost=null;_productionEditorSession=null;
    }
    private void RefreshProductionPublication(DocumentSession session,DocumentEditReceipt receipt)
    {
        if(!ReferenceEquals(DocumentManager.CurrentSession,session)||!ReferenceEquals(receipt.Session,session))return;
        RefreshFormalPublishedResult();NotifyResultDisplayChanged();RefreshRootTabReadiness();
        BuildFlatRows();OnPropertyChanged(nameof(CanUndoRootWorkspace));OnPropertyChanged(nameof(CanRedoRootWorkspace));
    }
    internal static void ReleaseProductionEditorContexts(ISet<ProductionEditorContext> active)
    {foreach(var context in active.ToArray())context.Discard();}
    internal static void TrackProductionEditor(ISet<ProductionEditorContext> active,ProductionEditorContext context,object target,Action whenEmpty)
    {
        if(!context.IsActive||!active.Add(context))return;
        var control=target as FrameworkElement;Window? owner=null;
        void Closed(object? sender,EventArgs args)=>context.Discard();
        void BindOwner()
        {
            var next=target as Window??(control is null?null:Window.GetWindow(control));
            if(ReferenceEquals(next,owner))return;
            if(owner is not null)owner.Closed-=Closed;owner=next;if(owner is not null)owner.Closed+=Closed;
        }
        void Loaded(object sender,RoutedEventArgs args)=>BindOwner();
        void Released()
        {
            context.Deactivated-=Released;if(control is not null)control.Loaded-=Loaded;if(owner is not null)owner.Closed-=Closed;
            active.Remove(context);if(active.Count==0)whenEmpty();
        }
        context.Deactivated+=Released;if(control is not null)control.Loaded+=Loaded;BindOwner();
    }
}
