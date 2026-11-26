using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

/// <summary>Same-run pixel points; producer paths are flattened by their consumer without a second pose transform.</summary>
/// <param name="Scores">
/// 逐点分数，对应原件 MultiPointShape 的三态配色（0=OK、1=中间态黄色、其余=NG）；
/// 为空时按整条结果的 Passed 取 0/2。
/// </param>
public sealed record NativeOutlineOverlay(ImageFrameAddress SourceFrame,CoordinateFrame Coordinates,
    IReadOnlyList<Point2> Points,int ResultIndex,bool Passed,Color Ok,Color Ng,
    IReadOnlyList<float>? Scores=null,double Opacity=1,double Width=2);