using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Stateless projection of the sole private persisted ROI into the existing neutral display declaration.</summary>
public static class BlobRoiDisplayProjection
{
    public static RecordValue Shape(RecordValue shape)
    {
        BlobLocalRoi.ValidateShape(shape);
        var fields=shape.CopyFields().ToDictionary(pair=>pair.Key,pair=>pair.Value);
        if(shape.Contains("angle-degrees"))fields["angle-degrees"]=BlobLocalRoi.NormalizeDegrees(shape.Get<double>("angle-degrees"));
        PointGeometry Point(RecordValue point)=>new(RoiDeclarations.ImageFrame,new(point.Get<double>("x"),point.Get<double>("y")));
        if(shape.Contains("center"))fields["center"]=Point(shape.Get<RecordValue>("center"));
        if(shape.Contains("vertices"))
        {
            fields["vertices"]=new SequenceValue(RoiDeclarations.Shape.Fields.Single(port=>port.Id=="vertices").Type,
                shape.Get<SequenceValue>("vertices").Cast<RecordValue>().Select(point=>(object?)Point(point)));
            fields["closed"]=true;
        }
        return new RecordValue(RoiDeclarations.Shape,fields);
    }
}
