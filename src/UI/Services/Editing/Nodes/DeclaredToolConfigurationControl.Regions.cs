using SunEyeVision.Plugin.Editing.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.UI.Services.Images;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.UI.Services.Editing.Regions;

namespace SunEyeVision.UI.Services.Editing;

public sealed partial class DeclaredToolConfigurationControl
{
    private sealed record RegionPart(RegionFieldGroup Definition,RoiParameterEditingAdapter Model,RegionGeometryEditor View);
    private readonly List<RegionPart> _regionParts=new();
    private RoiViewportEditScope? _roiViewport;
    private Func<ResultViewportImageLease?>? _regionInputProvider;
    private long _regionScopeGeneration = -1;
    private Window? _regionWindow;
    private ProductionEditorContext? _regionContext;
    private bool IsLocalRegion(RegionPart part)=>_accessors.Single(field=>field.Port.Id==part.Definition.ModeFieldId).Read()?.ToString()==nameof(RegionFieldMode.LocalGeometry);
    private bool HasPendingRegions=>_roiViewport?.HasPendingGesture==true||_regionParts.Any(part=>IsLocalRegion(part)&&part.View.HasInvalidText);
    public IRoiEditViewport? RegionViewport=>_roiViewport;
    private bool RegionScopeCurrent()=>!_acceptingFields&&_parameters is not null&&
        CurrentNode is SunEyeVision.Workflow.WorkflowNodeBase node&&ReferenceEquals(node.Parameters,_parameters)&&
        (ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext context||
            context.IsValid&&ReferenceEquals(context.DraftNode,node));
    private RoiCollection CurrentRegionCollection(string role)=>RegionCollection(_parameters,role);
    private static RoiCollection RegionCollection(ToolParameters? owner,string role)=>owner is RoiParameters parameters
        ?role switch {"inspection"=>parameters.InspectionRois,"mask"=>parameters.MaskRois,_=>throw new ArgumentException("未知区域角色。")}
        :throw new InvalidOperationException("当前参数不包含区域。");
    private void BuildRegionEditors(IConfigurationFieldProvider provider)
    {
        foreach(var part in _regionParts)part.View.Dispose();_regionParts.Clear();
        if(_parameters is not RoiParameters||_fields is null)return;
        var generation=_configurationGeneration;
        foreach(var group in provider.ConfigurationSchema?.Presentation?.Regions??Array.Empty<RegionFieldGroup>())
        {
            var model=new RoiParameterEditingAdapter(()=>_parameters as RoiParameters??throw new InvalidOperationException("当前区域参数已失效。"),group.Role,
                ()=>generation==_configurationGeneration&&RegionScopeCurrent()&&_accessors.Single(field=>field.Port.Id==group.ModeFieldId).Read()?.ToString()==nameof(RegionFieldMode.LocalGeometry),
                prepare=>AcceptParameterCandidate(generation,(candidate,_)=>prepare((RoiParameters)candidate)));
            var view=new RegionGeometryEditor(group.Role,group.Role=="mask"?"屏蔽区域 · 本地几何":"检测区域 · 本地几何",model.Read,model.Write,model.Enable);
            view.ModelChanged+=OnAcceptedFieldNotification;view.SetViewport(_roiViewport);
            _fields.AddAfterField(group.ModeFieldId,view,new(group.ModeFieldId,nameof(RegionFieldMode.LocalGeometry)));
            _regionParts.Add(new(group,model,view));
        }
        _fields.PageChanged+=()=>_roiViewport?.CancelGesture();
        RefreshRegionScene();AttachRegionLifetime();
    }
    internal void SetRegionInputProvider(Func<ResultViewportImageLease?> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _roiViewport?.Dispose(); _roiViewport = null; _regionScopeGeneration = -1;
        _regionInputProvider = provider;
        RefreshRegionScene(); AttachRegionLifetime();
    }
    private void EnsureRegionScope()
    {
        if (_regionInputProvider is null || _regionParts.Count == 0) return;
        if (_roiViewport is not null && _regionScopeGeneration == _configurationGeneration) return;
        _roiViewport?.Dispose();
        var generation = _configurationGeneration; _regionScopeGeneration = generation;
        bool Current() => generation == _configurationGeneration && RegionScopeCurrent();
        _roiViewport = new RoiViewportEditScope(() =>
        {
            if (!Current()) return null;
            if (_parameters is MeasureGuideParameters { IsPositionCorrectionEnabled: true })
                throw new InvalidOperationException("位置修正区域需要已证明的精确坐标投影，当前手势未接入；原数值与模型保留。");
            return _regionInputProvider?.Invoke();
        }, Current, () => _regionParts.Where(IsLocalRegion).SelectMany(part => part.Model.Read()).ToArray(), proposal =>
        {
            if (!Current()) throw new InvalidOperationException("当前区域私有草稿已被替换。");
            if (_regionParts.Any(part => IsLocalRegion(part) && part.View.HasInvalidText))
                throw new InvalidOperationException("请先修正区域数值，再操作原区域手柄。");
            var groups = _regionParts.Where(IsLocalRegion).Select(part => part.Definition.Role).ToArray();
            var targets = proposal switch
            {
                RoiEditProposal.Create create => new[] { create.Group },
                RoiEditProposal.Replace replace => new[] { replace.Target.Group },
                RoiEditProposal.Delete delete => delete.Targets.Select(target => target.Group).ToArray(),
                _ => Array.Empty<string>()
            };
            if (targets.Any(group => !groups.Contains(group))) throw new InvalidOperationException("继承来源不能写入本地底稿。");
            RoiEditSelection selection = new RoiEditSelection.Keep();
            IReadOnlyList<RoiEditItem> accepted = Array.Empty<RoiEditItem>();
            AcceptParameterCandidate(generation, (candidate, _) =>
            {
                selection = RoiParameterEditingAdapter.Apply((RoiParameters)candidate, proposal);
                accepted = RoiParameterEditingAdapter.Read((RoiParameters)candidate, groups);
            });
            // Capture belongs to the installed private candidate, even if synchronous observers rebound the view.
            return (selection, accepted);
        }, () =>
        {
            if (!Current()) return;
            OnAcceptedFieldNotification();
            foreach (var part in _regionParts) part.View.Refresh();
        });
        foreach (var part in _regionParts) part.View.SetViewport(_roiViewport);
    }
    private void AttachRegionLifetime()
    {
        var window=Window.GetWindow(this);
        if(!ReferenceEquals(window,_regionWindow))
        {
            if(_regionWindow is not null){_regionWindow.Closed-=OnRegionWindowClosed;_regionWindow.PreviewKeyDown-=OnRegionWindowKey;}
            _regionWindow=window;
            if(window is not null){window.Closed+=OnRegionWindowClosed;window.PreviewKeyDown+=OnRegionWindowKey;}
        }
        if(ToolEditorCommit.GetDraftContext(this) is ProductionEditorContext context&&!ReferenceEquals(context,_regionContext))
        {if(_regionContext is not null)_regionContext.Deactivated-=ReleaseRegionViewport;_regionContext=context;context.Deactivated+=ReleaseRegionViewport;}
    }
    private void OnRegionWindowClosed(object? sender,EventArgs args)=>ReleaseRegionViewport();
    private void OnRegionWindowKey(object sender,System.Windows.Input.KeyEventArgs args)
    {
        if(args.Key==System.Windows.Input.Key.Escape&&_roiViewport is {IsActive:true,HasPendingGesture:true})
        {_roiViewport.CancelGesture();args.Handled=true;}
    }
    private void ReleaseRegionViewport()
    {
        _roiViewport?.Dispose();_roiViewport=null;_regionInputProvider=null;_regionScopeGeneration=-1;
        foreach(var part in _regionParts)part.View.SetViewport(null);
        if(_regionContext is not null)_regionContext.Deactivated-=ReleaseRegionViewport;_regionContext=null;
        if(_regionWindow is not null){_regionWindow.Closed-=OnRegionWindowClosed;_regionWindow.PreviewKeyDown-=OnRegionWindowKey;}_regionWindow=null;
    }
    private void RefreshRegionScene()
    {
        EnsureRegionScope();
        var items=new List<RoiEditItem>();
        var editable=new List<string>();
        foreach(var part in _regionParts)
        {
            var mode=_accessors.Single(field=>field.Port.Id==part.Definition.ModeFieldId).Read()?.ToString();
            var local=mode==nameof(RegionFieldMode.LocalGeometry);part.View.IsEnabled=local;
            part.View.ToolTip=local?"绘制与数值均编辑当前私有草稿。":"继承模式下不编辑本地底稿；切回本地后可继续。";
            if(local){items.AddRange(part.Model.Read());editable.Add(part.Definition.Role);}
        }
        _roiViewport?.SetEditableGroups(editable);
        _roiViewport?.SetScene(items);
        if (editable.Count > 0) _roiViewport?.Activate();
    }
    public override void OnExternalResultArrived(){base.OnExternalResultArrived();RefreshRegionScene();}
    private bool ValidateRegionEditors(out string error)
    {
        _roiViewport?.RefreshCurrentness();
        error=_regionParts.FirstOrDefault(part=>IsLocalRegion(part)&&part.View.HasInvalidText)?.View.Error??"";
        if(error.Length>0)return false;
        if(_roiViewport?.HasPendingGesture==true){error="请先完成区域绘制或按Esc取消，再确认或保存。";return false;}
        try
        {
            foreach(var part in _regionParts.Where(part=>part.View.IsEnabled))
                foreach(var item in part.Model.Read().Where(item=>item.Enabled))RoiGeometryEditing.Validate(item.Geometry);
            return true;
        }
        catch(ArgumentException exception){error=exception.Message;return false;}
    }
}
