using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

public sealed record BlobVmAnnulus(double CenterX,double CenterY,double InnerRadius,double OuterRadius,double StartAngle,double AngleRange);

/// <summary>
/// VM BlobFindResult 的中立投影。可直接对应的成员保留值，原件结果中暂无等价资源的成员保持 null 并列入 Unavailable。
/// </summary>
public sealed record BlobVmResultProjection(
    int? ModuStatus,
    IReadOnlyList<int> BlobStatus,
    int BlobNum,
    IReadOnlyList<float> Area,
    float TotalArea,
    IReadOnlyList<Point2> CentroidPoint,
    IReadOnlyList<BlobBounds> MinBoudingRect,
    IReadOnlyList<float>? MinGrayValue,
    IReadOnlyList<float>? MaxGrayValue,
    IReadOnlyList<float>? GrayContrast,
    IReadOnlyList<BlobBounds>? MaxAreaInnerRect,
    IReadOnlyList<BlobBounds> BlobRect,
    IReadOnlyList<float> BlobRectUpLeftX,
    IReadOnlyList<float> BlobRectUpLeftY,
    IReadOnlyList<float> BlobRectBottomRightX,
    IReadOnlyList<float> BlobRectBottomRightY,
    IReadOnlyList<float> BlobAngle,
    IReadOnlyList<float> Perimeter,
    IReadOnlyList<float> LongAxis,
    IReadOnlyList<float> ShortAxis,
    IReadOnlyList<float> Circularity,
    IReadOnlyList<float> Rectangularity,
    IReadOnlyList<float>? Score,
    IReadOnlyImage? BinaryImageComb,
    IReadOnlyImage? BlobImageComb,
    IReadOnlyList<BlobBounds>? ROI,
    IReadOnlyList<BlobVmAnnulus>? ROIAnnulus,
    RecordValue? ContourInfo,
    int? ContourNum,
    IReadOnlyList<int>? BlindPolygonPointNum,
    IReadOnlyList<Point2>? BlindPolygonPoints,
    IReadOnlyList<string>? BlindPolygonString,
    IReadOnlyImage? OutputMask,
    IReadOnlySet<string> Unavailable)
{
    public static BlobVmResultProjection Create(IReadOnlyList<BlobMeasurement> measurements,IReadOnlyImage? binary,IReadOnlyImage? blob)
    {
        var ordered=measurements.ToArray();
        var bounds=Array.AsReadOnly(ordered.Select(item=>item.Bounds).ToArray());
        var unavailable=new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(MaxAreaInnerRect), nameof(Score), nameof(ROI), nameof(ROIAnnulus), nameof(ContourInfo),
            nameof(BlindPolygonPointNum), nameof(BlindPolygonPoints), nameof(BlindPolygonString), nameof(OutputMask), nameof(ModuStatus)
        };
        var minGray=OptionalSequence(ordered.Select(item=>item.MinGrayValue));
        var maxGray=OptionalSequence(ordered.Select(item=>item.MaxGrayValue));
        var grayContrast=OptionalSequence(ordered.Select(item=>item.GrayContrast));
        if(minGray is null) unavailable.Add(nameof(MinGrayValue));
        if(maxGray is null) unavailable.Add(nameof(MaxGrayValue));
        if(grayContrast is null) unavailable.Add(nameof(GrayContrast));
        var contourCount=ordered.Sum(item=>item.Boundary.Sum(path=>path.Points.Count));
        return new(
            ModuStatus:null,
            BlobStatus:Array.AsReadOnly(ordered.Select(item=>item.JudgmentPassed?1:0).ToArray()),
            BlobNum:ordered.Length,
            Area:Array.AsReadOnly(ordered.Select(item=>(float)item.Area).ToArray()),
            TotalArea:(float)ordered.Sum(item=>item.Area),
            CentroidPoint:Array.AsReadOnly(ordered.Select(item=>new Point2(item.CentroidX,item.CentroidY)).ToArray()),
            MinBoudingRect:bounds,
            MinGrayValue:minGray,MaxGrayValue:maxGray,GrayContrast:grayContrast,MaxAreaInnerRect:null,
            BlobRect:bounds,
            BlobRectUpLeftX:Array.AsReadOnly(ordered.Select(item=>(float)item.Bounds.X).ToArray()),
            BlobRectUpLeftY:Array.AsReadOnly(ordered.Select(item=>(float)item.Bounds.Y).ToArray()),
            BlobRectBottomRightX:Array.AsReadOnly(ordered.Select(item=>(float)(item.Bounds.X+item.Bounds.Width)).ToArray()),
            BlobRectBottomRightY:Array.AsReadOnly(ordered.Select(item=>(float)(item.Bounds.Y+item.Bounds.Height)).ToArray()),
            BlobAngle:Array.AsReadOnly(ordered.Select(item=>(float)item.OrientedBounds.AngleDegrees).ToArray()),
            Perimeter:Array.AsReadOnly(ordered.Select(item=>(float)item.Perimeter).ToArray()),
            LongAxis:Array.AsReadOnly(ordered.Select(item=>(float)Math.Max(item.OrientedBounds.Width,item.OrientedBounds.Height)).ToArray()),
            ShortAxis:Array.AsReadOnly(ordered.Select(item=>(float)Math.Min(item.OrientedBounds.Width,item.OrientedBounds.Height)).ToArray()),
            Circularity:Array.AsReadOnly(ordered.Select(item=>(float)item.Circularity).ToArray()),
            Rectangularity:Array.AsReadOnly(ordered.Select(item=>(float)item.Rectangularity).ToArray()),
            Score:null,BinaryImageComb:binary,BlobImageComb:blob,
            ROI:null,ROIAnnulus:null,ContourInfo:null,ContourNum:contourCount,
            BlindPolygonPointNum:null,BlindPolygonPoints:null,BlindPolygonString:null,OutputMask:null,
            Unavailable:unavailable);
    }

    private static IReadOnlyList<float>? OptionalSequence(IEnumerable<double?> values)
    {
        var items=values.ToArray();
        if(items.Any(value=>value is null)) return items.Length==0?Array.Empty<float>():null;
        return Array.AsReadOnly(items.Select(value=>(float)value!.Value).ToArray());
    }

}
