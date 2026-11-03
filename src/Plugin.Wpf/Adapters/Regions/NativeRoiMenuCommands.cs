using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Adapters.Regions;

/// <summary>Nonvisual interception before any original collection or clone mutation.</summary>
internal sealed record NativeRoiMenuCommands(Func<IROI,bool> CanDelete,Action<IROI> Delete,
    Func<IROI,bool> CanCopy,Action<IROI> Copy,Func<bool> CanPaste,Action<Point> Paste);
