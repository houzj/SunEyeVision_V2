using SunEyeVision.Plugin.Wpf.Fields;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>Actual tool-owned fields placed in the original four-way inheritance layout.
/// A null selection means local drawing; field values and mode acceptance remain host-owned.</summary>
public sealed record NativeRoiInheritanceBinding(int? InitialMode,Action<int?> AcceptMode,
    DeclaredFieldEditor RectangleSource,DeclaredFieldEditor AnnulusSource,
    IReadOnlyList<DeclaredFieldEditor> RectangleParameters,IReadOnlyList<DeclaredFieldEditor> AnnulusParameters);
