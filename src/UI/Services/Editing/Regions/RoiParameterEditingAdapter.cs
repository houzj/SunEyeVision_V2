using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.Wpf.Regions;

namespace SunEyeVision.UI.Services.Editing.Regions;

/// <summary>Current SDK geometry model adapter. No viewport, execution or global result ownership.</summary>
internal sealed class RoiParameterEditingAdapter(Func<RoiParameters> parameters,string group,Func<bool> isCurrent,
    Action<Action<RoiParameters>> accept)
{
    private RoiCollection Collection(RoiParameters owner)=>group switch {"inspection"=>owner.InspectionRois,"mask"=>owner.MaskRois,_=>throw new ArgumentException("未知区域角色。")};
    public IReadOnlyList<RoiEditItem> Read()=>Collection(parameters()).Rois.Where(roi=>roi.IsLocal).Select(roi=>new RoiEditItem(group,roi.Id,roi.Name,
        roi.LocalGeometry is { } local?RoiShapeValueAdapter.Freeze(local):new RecordValue(RoiDeclarations.Shape,new Dictionary<string,object?>{{"kind","none"}}),roi.IsEnabled,roi.IsVisible)).ToArray();
    public Guid? Write(Guid? id,RecordValue? geometry)
    {
        if(!isCurrent())throw new InvalidOperationException("当前区域编辑范围已失效。");
        if(geometry is not null)RoiGeometryEditing.Validate(geometry);
        Guid? acceptedId=id;
        accept(candidate=>
        {
            var collection=Collection(candidate);
            var roi=id.HasValue?collection.Rois.SingleOrDefault(roi=>roi.Id==id&&roi.IsLocal)
                ??throw new InvalidOperationException("本地区域已删除或来源已改变。"):null;
            if(geometry is null){if(roi is not null)collection.Rois.Remove(roi);acceptedId=null;return;}
            var shape=RoiShapeValueAdapter.Materialize(geometry);
            if(roi is not null)roi.LocalGeometry=shape;
            else{roi=Roi.CreateLocal((group=="mask"?"屏蔽区 ":"检测区 ")+(collection.Rois.Count+1),shape.Geometry!);collection.Rois.Add(roi);}
            acceptedId=roi.Id;
        });
        return acceptedId;
    }
    public void Enable(Guid id,bool enabled)
    {
        if(!isCurrent())throw new InvalidOperationException("当前区域编辑范围已失效。");
        accept(candidate=>
        {
            var roi=Collection(candidate).Rois.SingleOrDefault(roi=>roi.Id==id&&roi.IsLocal)
                ??throw new InvalidOperationException("本地区域已删除或来源已改变。");
            roi.IsEnabled=enabled;
        });
    }
}
