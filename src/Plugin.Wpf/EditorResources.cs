using System.Windows;
using System.Windows.Controls;
namespace SunEyeVision.Plugin.Wpf;
public static class EditorResources
{
 public static ResourceDictionary Create()=>new(){Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Generic.xaml",UriKind.Relative)};
 public static Style ErrorStyle()
 {
  var style=new Style(typeof(TextBlock));
  var empty=new Trigger{Property=TextBlock.TextProperty,Value=""};
  empty.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Collapsed));style.Triggers.Add(empty);return style;
 }
}
