using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

namespace SunEyeVision.Tool.BlobAnalysis.Presentation;

/// <summary>UI-owned scalar projection; retains no Region, image or native handle.</summary>
public sealed record BlobAnalysisRow(long ElementId,int Label,double Area,double Perimeter,double CentroidX,double CentroidY,
    double Circularity,double Rectangularity,Rect BoundingBox)
{
    public string BoundingRectText=>$"{(int)BoundingBox.Width}×{(int)BoundingBox.Height}";
    public static BlobAnalysisRow From(RecordValue record)
    {
        if(record.Declaration!=BlobSchema.BlobType)throw new ArgumentException("Unexpected Blob record declaration.");
        var bounds=record.Get<RecordValue>("bounds");
        return new(record.Get<long>("element-id"),checked((int)record.Get<long>("label")),record.Get<double>("area"),record.Get<double>("perimeter"),
            record.Get<double>("centroid-x"),record.Get<double>("centroid-y"),record.Get<double>("circularity"),record.Get<double>("rectangularity"),
            new(bounds.Get<double>("x"),bounds.Get<double>("y"),bounds.Get<double>("width"),bounds.Get<double>("height")));
    }
}
