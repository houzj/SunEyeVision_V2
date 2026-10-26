using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.UI.Services.Editing.Regions;

namespace SunEyeVision.UI.Services.Editing;

public sealed partial class DeclaredToolConfigurationControl
{
    private sealed record RegionPart(RegionFieldGroup Definition,RoiParameterEditingAdapter Model,RegionGeometryEditor View);
    private readonly List<RegionPart> _regionParts=new();
    private RoiViewportEditScope? _roiViewport;
    private ImageControl? _regionImage;
    private Window? _regionWindow;
    private ProductionEditorContext? _regionContext;
    private bool HasPendingRegions=>_roiViewport?.HasPendingGesture==true||_regionParts.Any(part=>part.View.HasInvalidText);
    public IRoiEditViewport? RegionViewport=>_roiViewport;
    private bool RegionScopeCurrent()=>ToolEditorCommit.GetDraftContext(this) is ProductionEditorContext context?context.IsValid:_parameters is not null;
    private void BuildRegionEditors(IConfigurationFieldProvider provider)
    {
        foreach(var part in _regionParts)part.View.Dispose();_regionParts.Clear();
        if(_parameters is not RoiParameters parameters||_fields is null)return;
        foreach(var group in provider.ConfigurationSchema?.Presentation?.Regions??Array.Empty<RegionFieldGroup>())
        {
            var model=new RoiParameterEditingAdapter(parameters,group.Role,()=>RegionScopeCurrent()&&_accessors.Single(field=>field.Port.Id==group.ModeFieldId).Read()?.ToString()==nameof(RegionFieldMode.LocalGeometry));
            var view=new RegionGeometryEditor(group.Role,group.Role=="mask"?"屏蔽区域 · 本地几何":"检测区域 · 本地几何",model.Read,model.Write,model.Enable);
            view.ModelChanged+=RefreshRegionScene;view.SetViewport(_roiViewport);
            _fields.AddAfterField(group.ModeFieldId,view,new(group.ModeFieldId,nameof(RegionFieldMode.LocalGeometry)));
            _regionParts.Add(new(group,model,view));
        }
        RefreshRegionScene();AttachRegionLifetime();
    }
    public override void SetMainImageControl(ImageControl? imageControl)
    {
        base.SetMainImageControl(imageControl);
        if(ReferenceEquals(_regionImage,imageControl))return;
        _roiViewport?.Dispose();_regionImage=imageControl;
        _roiViewport=imageControl is null?null:new RoiViewportEditScope(imageControl,RegionScopeCurrent);
        foreach(var part in _regionParts)part.View.SetViewport(_roiViewport);
        RefreshRegionScene();AttachRegionLifetime();
    }
    private void AttachRegionLifetime()
    {
        var window=Window.GetWindow(this);
        if(!ReferenceEquals(window,_regionWindow))
        {if(_regionWindow is not null)_regionWindow.Closed-=OnRegionWindowClosed;_regionWindow=window;if(window is not null)window.Closed+=OnRegionWindowClosed;}
        if(ToolEditorCommit.GetDraftContext(this) is ProductionEditorContext context&&!ReferenceEquals(context,_regionContext))
        {if(_regionContext is not null)_regionContext.Deactivated-=ReleaseRegionViewport;_regionContext=context;context.Deactivated+=ReleaseRegionViewport;}
    }
    private void OnRegionWindowClosed(object? sender,EventArgs args)=>ReleaseRegionViewport();
    private void ReleaseRegionViewport()
    {
        _roiViewport?.Dispose();_roiViewport=null;_regionImage=null;
        foreach(var part in _regionParts)part.View.SetViewport(null);
        if(_regionContext is not null)_regionContext.Deactivated-=ReleaseRegionViewport;_regionContext=null;
        if(_regionWindow is not null)_regionWindow.Closed-=OnRegionWindowClosed;_regionWindow=null;
    }
    private void RefreshRegionScene()
    {
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
        if(_roiViewport is null||_regionImage is null||_parameters is not MeasureGuideParameters guide)return;
        if(!guide.IsPositionCorrectionEnabled){_roiViewport.SetPose(null,true);return;}
        if(DataProvider is not IDataSourceQueryService query){_roiViewport.SetPose(null,false,"位置修正来源尚未就绪。");return;}
        var correction=PositionCorrectionResolution.Capture(guide,query,requireConformal:false);
        var preview=correction.ForPreview(PreviewFrameProvenance.Read(_regionImage.SourceImage),_regionImage.SourceImage is not null);
        _roiViewport.SetPose(correction.Matrix?.ToWpfMatrix(),preview.CanUse&&preview.FramePairingVerified,preview.Message);
    }
    public override void OnExternalResultArrived(){base.OnExternalResultArrived();RefreshRegionScene();}
    private bool ValidateRegionEditors(out string error)
    {
        error=_regionParts.FirstOrDefault(part=>part.View.HasInvalidText)?.View.Error??"";
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
