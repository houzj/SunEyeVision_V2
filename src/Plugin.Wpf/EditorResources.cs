using System.Windows;
namespace SunEyeVision.Plugin.Wpf;
public static class EditorResources
{
 public static ResourceDictionary Create()=>new(){Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Generic.xaml",UriKind.Relative)};
}
