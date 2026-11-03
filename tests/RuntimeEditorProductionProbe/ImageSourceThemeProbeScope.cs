namespace SunEyeVision.UI.Services.Images;
// 独立控件定位只使用Plugin.Wpf正式默认色板，不装配主程序主题服务；不替代产品主题验收。
public sealed class ImageSourceThemeScope:System.IDisposable
{
 public ImageSourceThemeScope(System.Windows.FrameworkElement target){}
 public void Dispose(){}
}
