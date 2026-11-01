using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>Stateless UI boundary; the private scalar record remains the sole stored value.</summary>
internal static class BlobLocalRoiProjection
{
    internal static RecordValue ToNative(RecordValue shape)
    {
        BlobLocalRoi.ValidateShape(shape);
        var fields=shape.CopyFields().ToDictionary(pair=>pair.Key,pair=>pair.Value);
        if(shape.Contains("angle-degrees"))fields["angle-degrees"]=BlobLocalRoi.NormalizeDegrees(shape.Get<double>("angle-degrees"));
        if(shape.Contains("center")){var point=shape.Get<RecordValue>("center");fields["center"]=RoiGeometryEditing.Point(new(point.Get<double>("x"),point.Get<double>("y")));}
        if(shape.Contains("vertices"))
        {
            fields["vertices"]=new SequenceValue(RoiDeclarations.Shape.Fields.Single(p=>p.Id=="vertices").Type,
                shape.Get<SequenceValue>("vertices").Cast<RecordValue>().Select(p=>(object?)RoiGeometryEditing.Point(new(p.Get<double>("x"),p.Get<double>("y")))));
            fields["closed"]=true;
        }
        return RoiGeometryEditing.Checked(fields);
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
