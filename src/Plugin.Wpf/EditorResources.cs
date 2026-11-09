using System.Windows;
using System.Windows.Controls;
namespace SunEyeVision.Plugin.Wpf;
public static class EditorResources
{
 // 对齐 VM 原件：主题资源以“共享字典实例”形式提供给各控件（globalresources.xaml 用 x:Static 单例），
 // 而不是靠“外层已合并”的运行时标志。共享实例只解析一次，控件各自引用同一份，资源边界仍由每个控件自己声明。
 private static readonly Lazy<ResourceDictionary> SharedDictionary=new(()=>new(){Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Generic.xaml",UriKind.Relative)});
 /// <summary>全进程共享的主题字典实例。控件应把它加入自身 Resources.MergedDictionaries，保证挂树前也能解析 StaticResource。</summary>
 public static ResourceDictionary Shared=>SharedDictionary.Value;
 public static Style ErrorStyle()
 {
  var style=new Style(typeof(TextBlock));
  var empty=new Trigger{Property=TextBlock.TextProperty,Value=""};
  empty.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Collapsed));style.Triggers.Add(empty);return style;
 }
}
