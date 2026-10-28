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
    internal static RoiEditSelection Apply(RoiParameters candidate, RoiEditProposal proposal)
    {
        RoiCollection CollectionFor(string role) => role switch { "inspection" => candidate.InspectionRois, "mask" => candidate.MaskRois, _ => throw new ArgumentException("未知区域角色。") };
        Roi Find(RoiEditTarget target) => CollectionFor(target.Group).Rois.SingleOrDefault(roi => roi.Id == target.Id && roi.IsLocal)
            ?? throw new InvalidOperationException("原区域目标已删除或改为继承来源。");
        void Check(RecordValue shape)
        {
            RoiGeometryEditing.Validate(shape);
            if (!RoiViewportEditScope.Supported(new("check", Guid.NewGuid(), "", shape)))
                throw new NotSupportedException("当前宿主仅接纳矩形、完整圆环及多边形，不把部分圆环改名为扇区。");
        }
        switch (proposal)
        {
            case RoiEditProposal.Create create:
                Check(create.Geometry);
                var shape = RoiShapeValueAdapter.Materialize(create.Geometry);
                var collection = CollectionFor(create.Group);
                var added = Roi.CreateLocal((create.Group == "mask" ? "屏蔽区 " : "检测区 ") + (collection.Rois.Count + 1), shape.Geometry!);
                collection.Rois.Add(added); return new RoiEditSelection.Select(new(create.Group, added.Id));
            case RoiEditProposal.Replace replace:
                Check(replace.Geometry); Find(replace.Target).LocalGeometry = RoiShapeValueAdapter.Materialize(replace.Geometry);
                return new RoiEditSelection.Select(replace.Target);
            case RoiEditProposal.Delete delete:
                var targets = delete.Targets.Select(target => (Target: target, Roi: Find(target))).ToArray();
                foreach (var target in targets) CollectionFor(target.Target.Group).Rois.Remove(target.Roi);
                return new RoiEditSelection.Clear();
            default: throw new NotSupportedException("私有区域草稿尚未接入专属撤销重做，请使用接纳后的方案历史。");
        }
    }
    internal static IReadOnlyList<RoiEditItem> Read(RoiParameters owner, IEnumerable<string> groups)
    {
        var result = new List<RoiEditItem>();
        foreach (var role in groups)
        {
            var collection = role switch { "inspection" => owner.InspectionRois, "mask" => owner.MaskRois, _ => throw new ArgumentException("未知区域角色。") };
            result.AddRange(collection.Rois.Where(roi => roi.IsLocal).Select(roi => new RoiEditItem(role, roi.Id, roi.Name,
                roi.LocalGeometry is { } local ? RoiShapeValueAdapter.Freeze(local) : new RecordValue(RoiDeclarations.Shape, new Dictionary<string, object?> { ["kind"] = "none" }), roi.IsEnabled, roi.IsVisible)));
        }
        return Array.AsReadOnly(result.ToArray());
    }
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
