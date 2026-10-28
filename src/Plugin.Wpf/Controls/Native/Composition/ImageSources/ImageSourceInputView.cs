using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Wpf.Presenters.Input;

namespace SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources;

/// <summary>One visual composition for source settings. The host owns drafts, devices, sessions and actions.</summary>
public sealed class ImageSourceInputView : UserControl
{
    private readonly StackPanel _cameraPanel=new();
    public ImageSourceStrip SourceStrip {get;}=new();
    public ComboBox CameraPicker {get;}=NativeInputFactory.Enumeration();
    public TextBlock CameraStatus {get;}=new(){TextWrapping=TextWrapping.Wrap};
    public Button OpenCameraButton {get;}=new(){Content="相机设置 / 预览…",Margin=new Thickness(0,6,0,0)};
    public ImageSourceInputView()
    {
        _cameraPanel.Children.Add(new TextBlock{Text="关联相机"});
        _cameraPanel.Children.Add(CameraPicker);_cameraPanel.Children.Add(CameraStatus);
        _cameraPanel.Children.Add(OpenCameraButton);
        var root=new StackPanel();root.Children.Add(SourceStrip);root.Children.Add(_cameraPanel);Content=root;
        IsCamera=false;
    }
    public bool IsCamera
    {
        get=>_cameraPanel.Visibility==Visibility.Visible;
        set {SourceStrip.Visibility=value?Visibility.Collapsed:Visibility.Visible;_cameraPanel.Visibility=value?Visibility.Visible:Visibility.Collapsed;}
    }
}
