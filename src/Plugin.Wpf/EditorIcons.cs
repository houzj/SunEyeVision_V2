using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SunEyeVision.Plugin.Wpf;

/// <summary>Small vector affordances shared by fields; no tool icons or runtime services.</summary>
internal static class EditorIcons
{
    public static FrameworkElement Eye(bool visible)=>Draw("M1,9 C5,2 13,2 17,9 C13,16 5,16 1,9 M12,9 A3,3 0 1 1 6,9 A3,3 0 1 1 12,9"+(visible?"":" M2,16 L16,2"));
    public static FrameworkElement Pencil()=>Draw("M2,16 L6,15 L16,5 L13,2 L3,12 Z M11,4 L14,7 M2,18 L18,18");
    public static FrameworkElement Link()=>Draw("M8,6 L10,4 A4,4 0 0 1 16,10 L13,13 M10,12 L8,14 A4,4 0 0 1 2,8 L5,5 M6,10 L12,6");
    private static FrameworkElement Draw(string data)=>new System.Windows.Shapes.Path{Data=Geometry.Parse(data),Stroke=new SolidColorBrush(Color.FromRgb(140,140,140)),StrokeThickness=1.5,Width=18,Height=18,Stretch=Stretch.Uniform,SnapsToDevicePixels=true};
}
