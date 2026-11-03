using System.Windows;
using System.Windows.Controls;
namespace SunEyeVision.Plugin.Wpf;
public static class EditorResources
{
 [ThreadStatic] private static int _sharedResourceScopeDepth;
 internal static bool IsSharedResourceScopeActive=>_sharedResourceScopeDepth>0;
 public static IDisposable BeginSharedResourceScope()
 {
  _sharedResourceScopeDepth++;return new SharedResourceScope();
 }
 private sealed class SharedResourceScope:IDisposable
 {
  private readonly int _threadId=Environment.CurrentManagedThreadId;private bool _disposed;
  public void Dispose()
  {
   if(_disposed)return;
   if(_threadId!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("WPF共享资源作用域必须在创建线程释放。");
   if(_sharedResourceScopeDepth<=0)throw new InvalidOperationException("WPF共享资源作用域失衡。");
   _disposed=true;_sharedResourceScopeDepth--;
  }
 }
 public static ResourceDictionary Create()=>new(){Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Generic.xaml",UriKind.Relative)};
 public static Style ErrorStyle()
 {
  var style=new Style(typeof(TextBlock));
  var empty=new Trigger{Property=TextBlock.TextProperty,Value=""};
  empty.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Collapsed));style.Triggers.Add(empty);return style;
 }
}
