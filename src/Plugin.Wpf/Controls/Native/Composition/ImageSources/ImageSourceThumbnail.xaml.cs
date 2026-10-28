using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources;
/// <summary>Original ImageView visual closure. Host projects decoded immutable ImageSource; no vendor byte converter is copied.</summary>
public partial class ImageSourceThumbnail : UserControl
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(ImageSource), typeof(ImageSourceThumbnail), new PropertyMetadata(null));
    public ImageSource? Source { get => (ImageSource?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public ImageSourceThumbnail() { InitializeComponent(); }
}
