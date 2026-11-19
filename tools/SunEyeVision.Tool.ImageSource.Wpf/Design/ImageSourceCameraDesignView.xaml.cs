using System.Windows.Controls;

namespace SunEyeVision.Tool.ImageSource.Wpf.Design;

/// <summary>Standalone camera sample that instantiates the actual parameter editor over its Runtime draft.</summary>
public partial class ImageSourceCameraDesignView : UserControl
{
    public ImageSourceCameraDesignView()
    {
        InitializeComponent();
        var context=ImageSourceDesignData.CreateParameterContext(camera:true);
        Content=new Views.ImageSourceParameterEditor(context.Draft,context.Options);
    }
}
