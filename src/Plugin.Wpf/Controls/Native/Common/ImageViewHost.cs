using System;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.Interface;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;

namespace SunEyeVision.Plugin.Wpf.Native.Common;

/// <summary>Visual session extracted from VM's image addition; owns no module or IO model.</summary>
public interface IImageViewPresentation : IDisposable
{
    IImageViewBoxWAddition? InnerImageViewWAddition { get; }
    object? ROIViewOut { get; }
    object? InputElementOut { get; }
    ICommand? MouseWheelCommand { get; }
    bool DoImageSourceChangedOut(out bool returnValue);
    void ImageSourceChangedOut();
    bool ShrinkOut();
    bool InitOut();
    bool EnlargeOut();
    void ROIListChangedOut(object eventArgs);
}

/// <summary>Explicit optional visual extension; no vendor Ioc or workflow access.</summary>
public interface IImageViewExtensionProvider
{
    IImageViewPresentation? CreatePresentation(ImageViewBox view);
    void PrepareResources(ImageViewBox view, bool profile);
}
