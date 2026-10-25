using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.Wpf.Regions;

namespace SunEyeVision.UI.Services.Editing.Regions;

/// <summary>Current SDK geometry model adapter. No viewport, execution or global result ownership.</summary>
internal sealed class RoiParameterEditingAdapter(RoiParameters parameters,string group,Func<bool> isCurrent)
{
    private RoiCollection Collection=>group switch {"inspection"=>parameters.InspectionRois,"mask"=>parameters.MaskRois,_=>throw new ArgumentException("未知区域角色。")};
    public IReadOnlyList<RoiEditItem> Read()=>Collection.Rois.Where(roi=>roi.IsLocal).Select(roi=>new RoiEditItem(group,roi.Id,roi.Name,
        roi.LocalGeometry is { } local?RoiShapeValueAdapter.Freeze(local):new RecordValue(RoiDeclarations.Shape,new Dictionary<string,object?>{{"kind","none"}}),roi.IsEnabled,roi.IsVisible)).ToArray();
    public Guid? Write(Guid? id,RecordValue? geometry)
    {
        if(!isCurrent())throw new InvalidOperationException("当前区域编辑范围已失效。");
        var roi=id.HasValue?Collection.Rois.SingleOrDefault(roi=>roi.Id==id&&roi.IsLocal)??throw new InvalidOperationException("本地区域已删除或来源已改变。"):null;
        if(geometry is null){if(roi is not null)Collection.Rois.Remove(roi);return null;}
        RoiGeometryEditing.Validate(geometry);var shape=RoiShapeValueAdapter.Materialize(geometry);
        if(roi is not null)roi.LocalGeometry=shape;
        else{roi=Roi.CreateLocal((group=="mask"?"屏蔽区 ":"检测区 ")+(Collection.Rois.Count+1),shape.Geometry!);Collection.Rois.Add(roi);}
        return roi.Id;
    }
    public void Enable(Guid id,bool enabled)
    {if(!isCurrent())throw new InvalidOperationException("当前区域编辑范围已失效。");Collection.Rois.Single(roi=>roi.Id==id&&roi.IsLocal).IsEnabled=enabled;}
}
