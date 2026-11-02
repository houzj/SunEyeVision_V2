using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>Stateless UI boundary; the private scalar record remains the sole stored value.</summary>
internal static class BlobLocalRoiProjection
{
    internal static RecordValue ToNative(RecordValue shape)
    {
        var projected=BlobRoiDisplayProjection.Shape(shape);
        RoiGeometryEditing.Validate(projected);
        return projected;
    }
    internal static RecordValue FromNative(RecordValue shape,RecordValue? previous=null)
    {
        RoiGeometryEditing.Validate(shape);
        var fields=shape.CopyFields().Where(pair=>pair.Key!="closed").ToDictionary(pair=>pair.Key,pair=>pair.Value);
        if(shape.Contains("center")){var point=shape.Get<PointGeometry>("center").Point;fields["center"]=BlobLocalRoi.Point(point.X,point.Y);}
        if(shape.Contains("vertices"))fields["vertices"]=new SequenceValue(BlobLocalRoi.ShapeType.Fields.Single(p=>p.Id=="vertices").Type,
            shape.Get<SequenceValue>("vertices").Cast<PointGeometry>().Select(p=>(object?)BlobLocalRoi.Point(p.Point.X,p.Point.Y)));
        if(previous?.Get<string>("kind")=="rectangle"&&shape.Get<string>("kind")=="rectangle"
            &&BlobLocalRoi.NormalizeDegrees(previous.Get<double>("angle-degrees"))==BlobLocalRoi.NormalizeDegrees(shape.Get<double>("angle-degrees")))
            fields["angle-degrees"]=previous.Get<double>("angle-degrees");
        var result=new RecordValue(BlobLocalRoi.ShapeType,fields);BlobLocalRoi.ValidateShape(result);return result;
    }
}
