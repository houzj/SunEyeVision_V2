using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

internal static class BlobResultGeometry
{
    internal static ResultGeometrySnapshot Capture(RecordValue? roi,IReadOnlyImage image,bool passed,bool transformed,bool pixelInspection,bool pixelMask)
    {
        var groups=new List<ResultGeometryGroup>();
        var input=roi??BlobLocalRoi.Empty;
        foreach(var group in new[]{BlobLocalRoi.Inspection,BlobLocalRoi.Mask})
        {
            var role=group==BlobLocalRoi.Mask?ResultGeometryRole.Mask:ResultGeometryRole.Inspection;
            var unavailable=transformed||(group==BlobLocalRoi.Mask?pixelMask:pixelInspection);
            var coverage=unavailable?ResultGeometryCoverage.Unavailable:BlobLocalRoi.Coverage(input,group) switch
            {BlobLocalRoi.FullDomain=>ResultGeometryCoverage.FullDomain,BlobLocalRoi.None=>ResultGeometryCoverage.None,_=>ResultGeometryCoverage.Explicit};
            var shapes=coverage==ResultGeometryCoverage.Explicit?BlobLocalRoi.Items(input,group).Where(item=>item.Get<bool>("enabled")).Select(item=>BlobRoiDisplayProjection.Shape(item.Get<RecordValue>("shape"))).ToArray():Array.Empty<RecordValue>();
            if(group==BlobLocalRoi.Inspection)
            {
                var rings=shapes.Where(shape=>shape.Get<string>("kind") is "annulus" or "sector").ToArray();
                groups.Add(new(ResultGeometryRole.Annulus,coverage==ResultGeometryCoverage.Explicit?ResultGeometryCoverage.Explicit:ResultGeometryCoverage.None,rings));
                shapes=shapes.Except(rings).ToArray();
            }
            groups.Add(new(role,coverage,shapes));
        }
        return new(image.Frame,BlobSchema.ImageFrame,passed,groups.OrderBy(group=>group.Role));
    }
}
