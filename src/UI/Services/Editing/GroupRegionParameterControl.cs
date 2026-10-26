using System;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Views;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>Specialized ROI editor with a private shadow, shared display lease and explicit prepare boundary.</summary>
internal sealed class GroupRegionParameterControl : UserControl, IToolEditorPendingState, IDisposable
{
    private readonly GroupRunParameterSession _session;
    private SubroutineRunParameter _item;
    private readonly Func<ImageControl?>? _image;
    private readonly RoiEditorManagerControl _manager=new();
    private readonly RoiCollection _value;
    private string _baseline;
    private bool _disposed;
    internal GroupRegionParameterControl(GroupRunParameterSession session,SubroutineRunParameter item,Func<ImageControl?>? image)
    {
        _session=session;_item=item;_image=image;
        var slots=session.Resolve(item);
        var source=(RoiCollection)GroupRunParameterSession.Read(slots[0])!;
        _value=JsonSerializer.Deserialize<RoiCollection>(JsonSerializer.Serialize(source,WorkflowSerializationOptions.Compact),WorkflowSerializationOptions.Compact)!;
        _baseline=Signature();_manager.SetSingleCollection(_value);Content=_manager;
        IsEnabled=!slots.Any(s=>s.Node.ParamSettings.GetSetting(s.Property.Name) is {BindingType:not BindingType.Constant}||GroupRunParameterSession.Read(s) is RoiCollection r&&r.IsBinding);
        Loaded+=(_,_)=>RefreshDisplay();IsVisibleChanged+=(_,_)=>RefreshDisplay();Unloaded+=(_,_)=>_manager.ClearMainOverlay();
    }
    private string Signature()=>JsonSerializer.Serialize(_value,WorkflowSerializationOptions.Compact);
    public bool HasPendingEdits=>!_disposed&&Signature()!=_baseline;
    internal RoiCollection Value=>_value;
    internal Guid ParameterId=>_item.Id;
    internal void Rebind(SubroutineRunParameter item){if(item.Id!=_item.Id)throw new InvalidOperationException("区域曝光身份不一致。");_item=item;}
    private void RefreshDisplay()
    {
        if(_disposed||!IsVisible||!IsEnabled){_manager.ClearMainOverlay();return;}
        _manager.SetMainImageControl(_image?.Invoke());
    }
    internal bool TryPrepare(out string error)
    {
        error="";
        if(_disposed)return true;
        if(!ToolEditorCommit.TryPrepare(_manager,out error))return false;
        if(!HasPendingEdits)return true;
        try{_session.Write(_item,_value);_baseline=Signature();return true;}
        catch(Exception ex){error=ex.GetBaseException().Message;return false;}
    }
    public void Dispose(){if(_disposed)return;_disposed=true;_manager.ClearMainOverlay();Content=null;}
}
