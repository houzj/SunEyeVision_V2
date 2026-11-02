using System.Text.Json.Serialization;

namespace SunEyeVision.Domain.Workflows;

/// <summary>Finite, platform-neutral coordinates in the workflow canvas.</summary>
public readonly record struct CanvasPoint
{
    [JsonConstructor]
    public CanvasPoint(double x, double y)
    {
        if (!double.IsFinite(x)) throw new ArgumentOutOfRangeException(nameof(x));
        if (!double.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(y));
        X = x;
        Y = y;
    }
    public double X { get; }
    public double Y { get; }
}
