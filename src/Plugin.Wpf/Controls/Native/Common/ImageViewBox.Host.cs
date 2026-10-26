using System;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Common;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;

public partial class ImageViewBox
{
    public static readonly DependencyProperty ExtensionProviderProperty = DependencyProperty.Register(
        nameof(ExtensionProvider), typeof(IImageViewExtensionProvider), typeof(ImageViewBox),
        new PropertyMetadata(null, OnExtensionProviderChanged));

    public IImageViewExtensionProvider? ExtensionProvider
    {
        get => (IImageViewExtensionProvider?)GetValue(ExtensionProviderProperty);
        set => SetValue(ExtensionProviderProperty, value);
    }

    private void AttachPresentationHost()
    {
        Loaded += OnPresentationLoaded;
        Unloaded += OnPresentationUnloaded;
    }

    private static void OnExtensionProviderChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (ImageViewBox)sender;
        view.ReleasePresentation();
        if (view.IsLoaded) view.ImageBoxAddition = view.ExtensionProvider?.CreatePresentation(view);
    }

    private void OnPresentationLoaded(object sender, RoutedEventArgs args)
    {
        if (!ReferenceEquals(args.OriginalSource, this)) return;
        ImageBoxAddition ??= ExtensionProvider?.CreatePresentation(this);
    }

    private void OnPresentationUnloaded(object sender, RoutedEventArgs args)
    {
        if (ReferenceEquals(args.OriginalSource, this)) ReleasePresentation();
    }

    private void ReleasePresentation()
    {
        var presentation = ImageBoxAddition;
        ImageBoxAddition = null;
        presentation?.Dispose();
    }
}
