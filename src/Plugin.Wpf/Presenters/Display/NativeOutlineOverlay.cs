using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

/// <summary>Same-run pixel points; producer paths are flattened by their consumer without a second pose transform.</summary>
public sealed record NativeOutlineOverlay(ImageFrameAddress SourceFrame,CoordinateFrame Coordinates,
    IReadOnlyList<Point2> Points,int ResultIndex,bool Passed,Color Ok,Color Ng);