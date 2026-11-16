namespace SunEyeVision.Plugin.Contracts;

/// <summary>Position correction expressed by the reference and run poses.</summary>
public static class FixtureDeclarations
{
    public static TypeRef Point { get; } = TypeRef.RecordOf("spatial.fixture.point", new[]
    {
        new Port("x", DataType.Number, Storage:NumericStorage.Float32),
        new Port("y", DataType.Number, Storage:NumericStorage.Float32)
    });

    public static TypeRef Pose { get; } = TypeRef.RecordOf("spatial.fixture.pose", new[]
    {
        new Port("point", Point),
        new Port("angle", DataType.Number, Storage:NumericStorage.Float32),
        new Port("scale-x", DataType.Number, Storage:NumericStorage.Float32),
        new Port("scale-y", DataType.Number, Storage:NumericStorage.Float32)
    });

    public static TypeRef Info { get; } = TypeRef.RecordOf("spatial.fixture", new[]
    {
        new Port("reference", Pose), new Port("run", Pose)
    });

    /// <summary>Host convention: angles are screen degrees and execution scales must be positive.</summary>
    public static RecordValue ToAffine2D(RecordValue fixture, CoordinateFrame frame)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(frame);
        if (fixture.Declaration != Info) throw new ArgumentException("位置修正信息声明不匹配。", nameof(fixture));
        var reference = ReadPose(fixture.Get<RecordValue>("reference"));
        var run = ReadPose(fixture.Get<RecordValue>("run"));
        var scaleX = run.ScaleX / reference.ScaleX;
        var scaleY = run.ScaleY / reference.ScaleY;
        var (sinBase, cosBase) = Math.SinCos(reference.Angle);
        var (sinRun, cosRun) = Math.SinCos(run.Angle);
        var m11 = cosRun * scaleX * cosBase + sinRun * scaleY * sinBase;
        var m21 = cosRun * scaleX * sinBase - sinRun * scaleY * cosBase;
        var m12 = sinRun * scaleX * cosBase - cosRun * scaleY * sinBase;
        var m22 = sinRun * scaleX * sinBase + cosRun * scaleY * cosBase;
        var affine = new RecordValue(SpatialDeclarations.Affine2D, new Dictionary<string, object?>
        {
            ["m11"] = m11, ["m12"] = m12, ["m21"] = m21, ["m22"] = m22,
            ["offset-x"] = run.X - m11 * reference.X - m21 * reference.Y,
            ["offset-y"] = run.Y - m12 * reference.X - m22 * reference.Y,
            ["from"] = frame.Id, ["to"] = frame.Id
        });
        SpatialDeclarations.ValidateAffine2D(affine, frame.Id, frame.Id);
        return affine;
    }

    private static (double X, double Y, double Angle, double ScaleX, double ScaleY) ReadPose(RecordValue value)
    {
        var point = value.Get<RecordValue>("point");
        var scaleX = value.Get<double>("scale-x");
        var scaleY = value.Get<double>("scale-y");
        if (scaleX <= 0 || scaleY <= 0) throw new ArgumentException("位置修正执行要求尺度大于零。", nameof(value));
        return (point.Get<double>("x"), point.Get<double>("y"), value.Get<double>("angle") * Math.PI / 180, scaleX, scaleY);
    }
}
