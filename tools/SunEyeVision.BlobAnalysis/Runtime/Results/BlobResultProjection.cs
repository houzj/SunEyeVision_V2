using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

public sealed record BlobAnnulus(double CenterX,double CenterY,double InnerRadius,double OuterRadius,double StartAngle,double AngleRange);

/// <summary>
/// VM BlobFindResult 的中立投影。可直接对应的成员保留值，原件结果中暂无等价资源的成员保持 null 并列入 Unavailable。
/// </summary>
public sealed record BlobResultProjection(
    int ModuStatus,
    IReadOnlyList<int> BlobStatus,
    int BlobNum,
    IReadOnlyList<float> Area,
    float TotalArea,
    IReadOnlyList<Point2> CentroidPoint,
    IReadOnlyList<BlobOrientedBounds> MinBoudingRect,
    IReadOnlyList<float>? MinGrayValue,
    IReadOnlyList<float>? MaxGrayValue,
    IReadOnlyList<float>? GrayContrast,
    IReadOnlyList<BlobOrientedBounds> MaxAreaInnerRect,
    IReadOnlyList<BlobBounds> BlobRect,
    IReadOnlyList<float> BlobRectUpLeftX,
    IReadOnlyList<float> BlobRectUpLeftY,
    IReadOnlyList<float> BlobRectBottomRightX,
    IReadOnlyList<float> BlobRectBottomRightY,
    IReadOnlyList<float>? BlobAngle,
    IReadOnlyList<float> Perimeter,
    IReadOnlyList<float> LongAxis,
    IReadOnlyList<float> ShortAxis,
    IReadOnlyList<float> Circularity,
    IReadOnlyList<float> Rectangularity,
    IReadOnlyList<float>? Score,
    IReadOnlyImage? BinaryImageComb,
    IReadOnlyImage? BlobImageComb,
    IReadOnlyList<BlobOrientedBounds>? ROI,
    IReadOnlyList<BlobAnnulus>? ROIAnnulus,
    RecordValue? ContourInfo,
    int? ContourNum,
    IReadOnlyList<int>? BlindPolygonPointNum,
    IReadOnlyList<Point2>? BlindPolygonPoints,
    IReadOnlyList<string>? BlindPolygonString,
    IReadOnlyImage? OutputMask,
    IReadOnlySet<string> Unavailable)
{
    public static BlobResultProjection Create(IReadOnlyList<BlobMeasurement> measurements,IReadOnlyImage? binary,IReadOnlyImage? blob,
        ResultGeometrySnapshot geometry,
        SequenceValue? maskPolygons=null, IReadOnlyImage? maskImage=null, bool publishContour=false, CancellationToken cancellation=default)
    {
        var ordered=measurements.ToArray();
        var bounds=Array.AsReadOnly(ordered.Select(item=>item.Bounds).ToArray());
        var unavailable=new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(Score)
        };
        var minGray=OptionalSequence(ordered.Select(item=>item.MinGrayValue));
        var maxGray=OptionalSequence(ordered.Select(item=>item.MaxGrayValue));
        var grayContrast=OptionalSequence(ordered.Select(item=>item.GrayContrast));
        var blobAngle=OptionalSequence(ordered.Select(item=>item.BlobAngleDegrees));
        var rectangles = ReadDetectionGeometry(geometry, ResultGeometryRole.Inspection,
            shape => shape.Get<string>("kind") == "rectangle", shape =>
            {
                var center = shape.Get<PointGeometry>("center").Point;
                return new BlobOrientedBounds(center.X, center.Y, shape.Get<double>("width"),
                    shape.Get<double>("height"), shape.Get<double>("angle-degrees"));
            });
        var annuli = ReadDetectionGeometry(geometry, ResultGeometryRole.Annulus,
            shape => shape.Get<string>("kind") is "annulus" or "sector", shape =>
            {
                var center = shape.Get<PointGeometry>("center").Point;
                var start = shape.Get<double>("start-radians");
                var end = shape.Get<double>("end-radians");
                return new BlobAnnulus(center.X, center.Y, shape.Get<double>("inner-radius"),
                    shape.Get<double>("outer-radius"), start * 180d / Math.PI,
                    Math.Clamp((end - start) * 180d / Math.PI, -360d, 360d));
            });
        if (rectangles is null) unavailable.Add(nameof(ROI));
        if (annuli is null) unavailable.Add(nameof(ROIAnnulus));
        if(minGray is null) unavailable.Add(nameof(MinGrayValue));
        if(maxGray is null) unavailable.Add(nameof(MaxGrayValue));
        if(grayContrast is null) unavailable.Add(nameof(GrayContrast));
        if(blobAngle is null) unavailable.Add(nameof(BlobAngle));
        int? contourCount = publishContour
            ? ordered.Sum(item => item.UnorderedContour?.Count ?? item.Boundary.Sum(path => path.Points.Count)) : null;
        var contourInfo = contourCount is > 0 ? CaptureContourInfo(ordered, contourCount.Value, cancellation) : null;
        var masks=maskPolygons?.Cast<PolygonGeometry>().ToArray();
        if(masks is null){unavailable.Add(nameof(BlindPolygonPointNum));unavailable.Add(nameof(BlindPolygonPoints));unavailable.Add(nameof(BlindPolygonString));}
        return new(
            ModuStatus:geometry.Passed?1:0,
            BlobStatus:Array.AsReadOnly(ordered.Select(item=>item.JudgmentPassed?1:0).ToArray()),
            BlobNum:ordered.Length,
            Area:Array.AsReadOnly(ordered.Select(item=>(float)item.Area).ToArray()),
            TotalArea:(float)BlobNumericSemantics.Float32Sum(ordered.Select(item=>item.Area)),
            CentroidPoint:Array.AsReadOnly(ordered.Select(item=>new Point2(item.CentroidX,item.CentroidY)).ToArray()),
            MinBoudingRect:Array.AsReadOnly(ordered.Select(item=>item.OrientedBounds).ToArray()),
            MinGrayValue:minGray,MaxGrayValue:maxGray,GrayContrast:grayContrast,
            MaxAreaInnerRect:Array.AsReadOnly(ordered.Select(item=>item.InnerRectangle).OfType<BlobOrientedBounds>().ToArray()),
            BlobRect:bounds,
            BlobRectUpLeftX:Array.AsReadOnly(ordered.Select(item=>(float)item.Bounds.X).ToArray()),
            BlobRectUpLeftY:Array.AsReadOnly(ordered.Select(item=>(float)item.Bounds.Y).ToArray()),
            BlobRectBottomRightX:Array.AsReadOnly(ordered.Select(item=>(float)(item.Bounds.X+item.Bounds.Width)).ToArray()),
            BlobRectBottomRightY:Array.AsReadOnly(ordered.Select(item=>(float)(item.Bounds.Y+item.Bounds.Height)).ToArray()),
            BlobAngle:blobAngle,
            Perimeter:Array.AsReadOnly(ordered.Select(item=>(float)item.Perimeter).ToArray()),
            LongAxis:Array.AsReadOnly(ordered.Select(item=>BlobNumericSemantics.LongAxis(item.OrientedBounds)).ToArray()),
            ShortAxis:Array.AsReadOnly(ordered.Select(item=>BlobNumericSemantics.ShortAxis(item.OrientedBounds)).ToArray()),
            Circularity:Array.AsReadOnly(ordered.Select(item=>(float)item.Circularity).ToArray()),
            Rectangularity:Array.AsReadOnly(ordered.Select(item=>(float)item.Rectangularity).ToArray()),
            Score:null,BinaryImageComb:binary,BlobImageComb:blob,
            ROI:rectangles,ROIAnnulus:annuli,ContourInfo:contourInfo,ContourNum:contourCount,
            BlindPolygonPointNum:masks is null?null:Array.AsReadOnly(masks.Select(polygon=>polygon.Vertices.Count).ToArray()),
            BlindPolygonPoints:masks is null?null:Array.AsReadOnly(masks.SelectMany(polygon=>polygon.Vertices).ToArray()),
            BlindPolygonString:masks is null?null:Array.AsReadOnly(masks.Select(BlobMaskInheritance.Encode).ToArray()),OutputMask:maskImage,
            Unavailable:unavailable);
    }

    private static RecordValue CaptureContourInfo(IReadOnlyList<BlobMeasurement> blobs, int count, CancellationToken cancellation)
    {
        var x = new double[count];
        var y = new double[count];
        var score = new double[count];
        var index = new long[count];
        var pointIndex = 0;
        for (var blobIndex = 0; blobIndex < blobs.Count; blobIndex++)
        {
            var blob = blobs[blobIndex];
            foreach (var point in BlobSchema.OutlinePoints(blob))
            {
                if ((pointIndex & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                x[pointIndex] = (float)point.X;
                y[pointIndex] = (float)point.Y;
                score[pointIndex] = blob.JudgmentPassed ? 0 : 2;
                index[pointIndex++] = blobIndex;
            }
        }
        return new(BlobSchema.ContourInfoType, new Dictionary<string, object?>
        {
            ["x"] = x, ["y"] = y, ["score"] = score, ["blob-index"] = index
        });
    }

    private static IReadOnlyList<T>? ReadDetectionGeometry<T>(ResultGeometrySnapshot geometry, ResultGeometryRole role,
        Func<RecordValue, bool> supported, Func<RecordValue, T> project)
    {
        var group = geometry.Groups.SingleOrDefault(group => group.Role == role);
        if (group is null || group.OutlineOnly || group.Coverage is ResultGeometryCoverage.Unavailable or ResultGeometryCoverage.FullDomain
            || !group.Shapes.All(supported))
            return null;
        return Array.AsReadOnly(group.Shapes.Select(project).ToArray());
    }

    private static IReadOnlyList<float>? OptionalSequence(IEnumerable<double?> values)
    {
        var items=values.ToArray();
        if(items.Any(value=>value is null)) return items.Length==0?Array.Empty<float>():null;
        return Array.AsReadOnly(items.Select(value=>(float)value!.Value).ToArray());
    }

}
