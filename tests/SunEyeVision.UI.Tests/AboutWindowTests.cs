using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Core.Versioning;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.UI.Themes.Base;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class AboutWindowTests
{
    [Fact]
    public void Information_uses_running_build_metadata_and_real_environment()
    {
        var info = new AboutSoftwareInformation();
        Assert.Contains(info.Rows, row => row.Label == "产品名称" && row.Value == "SunEyeVision");
        Assert.Contains(info.Rows, row => row.Label == "程序目录" && row.Value == AppContext.BaseDirectory);
        Assert.Contains(info.Rows, row => row.Label == "发布日期" && row.Value == AppVersionInfo.ReleaseDate);
        var version = AppVersionInfo.Channel == "stable" ? AppVersionInfo.Product : AppVersionInfo.Product + "_" + AppVersionInfo.Channel;
        Assert.Contains(info.Rows, row => row.Label == "发布版本" && row.Value == version);
        Assert.All(info.Rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Value)));
        Assert.Contains("代码基线", info.ToDiagnosticText());
        Assert.Contains("未提交修改", info.ToDiagnosticText());
    }

    [Fact]
    public void Window_renders_both_themes_and_updates_existing_resource_references()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            AboutWindow? window = null;
            try
            {
                window = new AboutWindow();
                var content = (Grid)window.Content;
                foreach (var theme in new[] { "Dark", "Light", "Dark" })
                {
                    window.Resources.MergedDictionaries.Clear();
                    window.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri($"pack://application:,,,/SunEyeVision.UI;component/Themes/{theme}/{theme}Theme.xaml")
                    });
                    window.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/SunEyeVision.UI;component/Resources/Styles/CommonStyleResources.xaml")
                    });
                    window.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/SunEyeVision.UI;component/Resources/Branding/Logo.xaml")
                    });
                    content.Measure(new Size(592, 500));
                    content.Arrange(new Rect(0, 0, 592, 500));
                    content.UpdateLayout();
                    var brandPanel = Assert.IsType<StackPanel>(Assert.IsType<Border>(content.Children[0]).Child);
                    Assert.IsType<DrawingImage>(Assert.IsType<Image>(brandPanel.Children[0]).Source);
                    Assert.Equal(((SolidColorBrush)window.FindResource(ThemeKeys.WindowBackgroundBrush)).Color,
                        ((SolidColorBrush)window.Background).Color);
                    Assert.Equal(((SolidColorBrush)window.FindResource(ThemeKeys.TextPrimaryBrush)).Color,
                        ((SolidColorBrush)window.Foreground).Color);
                    Assert.IsType<AboutSoftwareInformation>(window.DataContext);
                    Assert.Contains(content.Children.Cast<UIElement>(), child => child is ScrollViewer);
                    var preview = Environment.GetEnvironmentVariable("SUNEYEVISION_ABOUT_PREVIEW");
                    if (!string.IsNullOrEmpty(preview))
                    {
                        Directory.CreateDirectory(preview);
                        var bitmap = new RenderTargetBitmap(592, 500, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(content);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var output = File.Create(Path.Combine(preview, $"about-{theme}.png"));
                        encoder.Save(output);
                    }
                }
            }
            catch (Exception ex) { error = ex; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "关于窗口主题测试超时");
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
