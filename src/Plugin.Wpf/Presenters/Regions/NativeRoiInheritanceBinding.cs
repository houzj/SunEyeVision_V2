using SunEyeVision.Plugin.Wpf.Fields;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>Actual tool-owned fields placed in the original four-way inheritance layout.
/// A null selection means local drawing; field values and mode acceptance remain host-owned.</summary>
public sealed record NativeRoiInheritanceBinding(int? InitialMode,Action<int?> AcceptMode,
    DeclaredFieldEditor RectangleSource,DeclaredFieldEditor AnnulusSource,
    // VM's parameter templates are created when the corresponding inheritance
    // mode is selected. Keep the source rows eager, but defer the two heavy
    // parameter groups until they are actually visible.
    Func<IReadOnlyList<DeclaredFieldEditor>> RectangleParametersFactory,
    Func<IReadOnlyList<DeclaredFieldEditor>> AnnulusParametersFactory);
