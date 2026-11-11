using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Wpf.Adapters.Resources;

namespace SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources;

/// <summary>Camera selector composition. The host owns devices, actions and draft edits.</summary>
public partial class ImageSourceInputView : UserControl
{
    public ComboBox CameraPicker => (ComboBox)Resources[nameof(CameraPicker)];
    public TextBlock CameraStatus => (TextBlock)Resources[nameof(CameraStatus)];
    public Button OpenCameraButton => (Button)Resources[nameof(OpenCameraButton)];
    public ImageSourceInputView()
    {
        InitializeComponent();
        Resources.MergedDictionaries.Insert(0, OriginalThemeResources.Shared);
    }

    public bool IsCamera
    {
        get => Visibility == Visibility.Visible;
        set => Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }
}
