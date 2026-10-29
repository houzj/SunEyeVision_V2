using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Wpf.Presenters.Input;

namespace SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources;

/// <summary>One visual composition for source settings. The host owns drafts, devices, sessions and actions.</summary>
public sealed class ImageSourceInputView : UserControl
{
    private readonly Grid _cameraPanel = new();
    public ImageSourceStrip SourceStrip {get;}=new();
    public ComboBox CameraPicker {get;}=NativeInputFactory.Enumeration();
    public TextBlock CameraStatus {get;}=new();
    public Button OpenCameraButton {get;}=new();
    public ImageSourceInputView()
    {
        var row=_cameraPanel; row.Margin=new Thickness(0,2,0,2);
        row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(130) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(36) });
        row.Children.Add(new TextBlock { Text="关联相机",VerticalAlignment=VerticalAlignment.Center });
        Grid.SetColumn(CameraPicker,1);CameraPicker.ToolTip=CameraStatus;row.Children.Add(CameraPicker);
        OpenCameraButton.Content=new Image { Width=26,Height=26,Stretch=Stretch.Uniform,
            Source=new BitmapImage(new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Composition/Apps.Style/resources/ic_camera-settings_normal.png",UriKind.Relative)) };
        OpenCameraButton.ToolTip="相机设置";OpenCameraButton.Background=Brushes.Transparent;
        OpenCameraButton.BorderThickness=new Thickness(0);Grid.SetColumn(OpenCameraButton,2);
        row.Children.Add(OpenCameraButton);
        var root=new StackPanel();root.Children.Add(SourceStrip);root.Children.Add(row);Content=root;IsCamera=false;
    }
    public bool IsCamera
    {
        get=>_cameraPanel.Visibility==Visibility.Visible;
        set {SourceStrip.Visibility=value?Visibility.Collapsed:Visibility.Visible;_cameraPanel.Visibility=value?Visibility.Visible:Visibility.Collapsed;}
    }
}
