using System;
using System.Collections.Generic;
using System.Linq;
using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Helpers;

namespace SunEyeVision.Tool.BlobAnalysis;

/// <summary>Current SDK input boundary. Invalid references fail; valid empty Regions remain empty.</summary>
internal static class BlobInputAdapter
{
    internal static AffineMatrix? ResolveCorrection(BlobAnalysisParameters parameters)
    {
        if(!parameters.IsPositionCorrectionEnabled)return null;
        var reference=ToolExecutionPreparation.ReadReference(parameters,"PositionCorrectionBindingSource");
        if(reference is null||parameters.NodeResults is null
            ||!parameters.NodeResults.TryGetValue(reference.SourceNodeId,out var result)||!result.IsSuccess)
            throw new InvalidOperationException("BLOB_POSE_SOURCE_INVALID: 位置修正必须来自本次执行的明确来源。");
        var path=PropertyPathParser.ParsePropertyPath(reference.PropertyName);
        if(path.Count==0)throw new InvalidOperationException("BLOB_POSE_SOURCE_INVALID: 位置修正路径为空。");
        var field=result.GetOutputVariables().SingleOrDefault(v=>string.Equals(v.Name,path[0].propertyName,StringComparison.OrdinalIgnoreCase));
        if(field is {IsCollection:true}&&path[0].arrayIndex is not >=0
            ||field?.DeclaredType?.IsArray==true&&path[0].arrayIndex is not >=0)
            throw new InvalidOperationException("BLOB_POSE_SELECTION_REQUIRED: 矩阵数组必须显式选择元素，不能默认取第一项。");
        var value=OutputVariableResolver.Resolve(result,reference.PropertyName,reference.SourceNodeId);
        if(value is Array)throw new InvalidOperationException("BLOB_POSE_SELECTION_REQUIRED: 矩阵数组必须显式选择元素，不能默认取第一项。");
        var resolved=parameters.ResolvedPositionCorrection??PositionCorrectionResolution.Capture(parameters,requireConformal:false);
        if(!resolved.CanUse)throw new InvalidOperationException(resolved.Message);
        if(!resolved.FramePairingVerified)
            throw new InvalidOperationException("BLOB_POSE_FRAME_UNVERIFIED: 无法证明位置修正与当前图像同帧，不能猜测采用。");
        return resolved.Matrix;
    }

    internal static IReadOnlyImage CaptureImage(BlobAnalysisParameters parameters,IOutputResources resources)
    {
        var reference=ToolExecutionPreparation.ReadReference(parameters,"ImageSource");
        if(reference is not null&&parameters.NodeResults?.TryGetValue(reference.SourceNodeId,out var result)==true
            &&result.DeclaredOutput is { } declared)
        {
            using var reader=declared.AcquireReader();
            var field=declared.Fields.SingleOrDefault(f=>reference.SourceFieldId is Guid id
                ?f.Value.FieldId==id:string.Equals(f.Value.Name,reference.PropertyName,StringComparison.OrdinalIgnoreCase));
            if(field.Key is null||reader.Values is null)throw new InvalidOperationException("BLOB_IMAGE_SOURCE_INVALID: 本次声明图像来源不可用。");
            return resources.RetainInput(reader.Values.Get<IReadOnlyImage>(field.Key));
        }
        var physicalFrame=ImageFrameProvenance.CaptureInput(parameters.ResolvedInputImage);
        var invocation=parameters.Context?.ExecutionIdentity;
        RunIdentity? captureIdentity=invocation is null?null:invocation.ToInvocationIdentity(parameters.Context!.NodeId??Runtime.BlobDefinition.Id);
        var frame=new ImageFrameAddress(physicalFrame??Guid.NewGuid(),physicalFrame.HasValue?"sdk-image":parameters.Context?.NodeId??"input",
            captureIdentity,Runtime.BlobDefinition.ImageInput);
        using var captured=parameters.ResolvedInputImage is ITypedImageSnapshotSource source
            ?source.CaptureSnapshot(frame):HalImageSnapshotSource.Capture(parameters.GetHalconImage(),frame);
        return resources.RetainInput(captured);
    }

    internal static IReadOnlyRegion? ResolveRegion(BlobAnalysisParameters parameters,bool mask,IReadOnlyImage image,
        IOutputResources resources,AffineMatrix? correction)
    {
        var mode=mask?parameters.MaskMode:parameters.InspectionMode;
        if(!Enum.IsDefined(mode))throw new ArgumentException("BLOB_ROI_MODE_INVALID: 未知区域来源模式。");
        if(mode==RegionFieldMode.InheritedRegion)
        {
            var region=(mask?parameters.MaskRegionInput:parameters.InspectionRegionInput)
                ??throw new InvalidOperationException("BLOB_REGION_SOURCE_INVALID: 已选择像素区域订阅，但本次无有效来源。");
            ResourceContractRules.ValidateRegion(region);
            if(region.SourceFrame!=image.Frame||region.Frame!=Runtime.BlobSchema.ImageFrame)
                throw new InvalidOperationException("BLOB_REGION_FRAME_MISMATCH: 图像与像素区域不是同一来源帧/坐标系。");
            return resources.RetainRegion(region); // Already in current image coordinates: no second pose transform.
        }
        var collection=mask?parameters.MaskRois:parameters.InspectionRois;
        var rois=collection.Rois.Where(r=>r.IsEnabled&&(mode==RegionFieldMode.LocalGeometry?r.IsLocal:!r.IsLocal)).ToArray();
        var context=parameters.NodeResults is null?null:new NodeResultsParameterContext(parameters.NodeResults);
        // Collection-level bindings must be explicit rather than silently ignored by the old geometry path.
        if(collection.BindingConfig is not null)throw new InvalidOperationException("BLOB_ROI_SOURCE_INVALID: 集合订阅应使用明确的几何项或像素区域输入。");
        if(rois.Length==0)
        {
            if(mode==RegionFieldMode.InheritedGeometry)throw new InvalidOperationException("BLOB_ROI_SOURCE_INVALID: 已选择几何订阅，但没有明确来源。");
            return null; // Local Unset; distinct from a subscribed valid empty sequence below.
        }
        var shapeCount=0;
        foreach(var roi in rois)
        {
            if(roi.IsSubscribed)ValidateBinding(roi.Binding,parameters.NodeResults);
            if(roi.IsComposed&&roi.ParameterBindings is { } fields)
                foreach(var binding in fields.Values.Where(b=>b.BindingType==BindingType.Binding))ValidateBinding(binding,parameters.NodeResults);
            var shapes=roi.ResolveRuntime(context);
            shapeCount+=shapes.Count;
            if(roi.IsLocal&&shapes.Count==0)throw new InvalidOperationException("BLOB_ROI_GEOMETRY_INVALID: 已启用的本地几何没有有效形状。");
        }
        if(shapeCount==0)return resources.CreateRegion(Runtime.BlobSchema.ImageFrame,image.Frame,Array.Empty<RegionRun>());
        // Rasterize each active geometry, applying the same neutral affine convention as the kernel
        // exactly once to local reference geometry. Inherited geometry/Region is already current-frame.
        HOperatorSet.GenEmptyRegion(out var empty);
        using(empty)
        {
            var merged=new HRegion(empty);
            try
            {
                foreach(var roi in rois)
                {
                    if(roi.ResolveRuntime(context).Count==0)continue;
                    using var native=ToolRegionHelper.BuildHalconRegion(new[]{roi},parameters.NodeResults);
                    RecordValue? pose=null;
                    if(roi.IsLocal&&correction is { } m)
                        pose=new(SpatialDeclarations.Affine2D,new Dictionary<string,object?>{
                            ["m11"]=m.M11,["m12"]=m.M12,["m21"]=m.M21,["m22"]=m.M22,
                            ["offset-x"]=m.OffsetX,["offset-y"]=m.OffsetY,
                            ["from"]=Runtime.BlobSchema.ImageFrame.Id,["to"]=Runtime.BlobSchema.ImageFrame.Id});
                    using var transformed=HalconResourceBridge.TransformRegion(native,pose);
                    var next=merged.Union2(transformed);merged.Dispose();merged=next;
                }
                return HalconResourceBridge.CaptureRegion(merged,Runtime.BlobSchema.ImageFrame,image.Frame,resources);
            }
            finally{merged.Dispose();}
        }
    }

    private static void ValidateBinding(ParamSetting? binding,IDictionary<string,ToolResults>? results)
    {
        if(binding is null||binding.BindingType!=BindingType.Binding||string.IsNullOrWhiteSpace(binding.SourceNodeId)
            ||string.IsNullOrWhiteSpace(binding.SourceProperty)||results is null
            ||!results.TryGetValue(binding.SourceNodeId,out var result)||!result.IsSuccess)
            throw new InvalidOperationException("BLOB_ROI_SOURCE_INVALID: 区域绑定的本次来源无效。");
        var value=OutputVariableResolver.Resolve(result,binding.SourceProperty,binding.SourceNodeId);
        if(value is null)
            throw new InvalidOperationException("BLOB_ROI_SOURCE_INVALID: 区域绑定返回null，不能当作有效空区域。");
        if(value is RoiShape[] shapes&&shapes.Any(shape=>shape?.Geometry is null))
            throw new InvalidOperationException("BLOB_ROI_SOURCE_INVALID: 区域序列包含无效形状，不能当作有效空区域。");
    }
}
