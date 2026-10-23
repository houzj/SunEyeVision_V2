using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Shell.Controls.Panels;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.UI.Themes.Base;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed partial class SolutionManagerUiTests
{
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "方案界面测试超时");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private static ResourceDictionary Theme(string name) => new() { Source = new Uri($"pack://application:,,,/SunEyeVision.UI;component/Themes/{name}/{name}Theme.xaml") };
    private static void Render(Window window, string name, int width, int height)
    {
        window.Resources.MergedDictionaries.Clear(); window.Resources.MergedDictionaries.Add(Theme(name));
        window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/SunEyeVision.UI;component/Resources/Styles/CommonStyleResources.xaml") });
        var grid = (Grid)window.Content;
        grid.Width = width - grid.Margin.Left - grid.Margin.Right;
        grid.Height = height - grid.Margin.Top - grid.Margin.Bottom;
        grid.Measure(new Size(width, height)); grid.Arrange(new Rect(0, 0, width, height)); grid.UpdateLayout();
        grid.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        grid.UpdateLayout();
        Assert.Equal(((SolidColorBrush)window.FindResource(ThemeKeys.WindowBackgroundBrush)).Color, ((SolidColorBrush)window.Background).Color);
        var directory = Environment.GetEnvironmentVariable("SUNEYEVISION_SOLUTION_PREVIEW");
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            var backdrop = new DrawingVisual();
            using (var drawing = backdrop.RenderOpen()) drawing.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(backdrop); bitmap.Render(grid);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(Path.Combine(directory, window.GetType().Name + "-" + name + "-" + width + ".png")); encoder.Save(output);
        }
    }
    [Fact]
    public void Data_directory_dialog_does_not_write_until_user_confirms()
    {
        Sta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "SunEyeVision-DataDialog", Guid.NewGuid().ToString("N"));
            var dialog = new DataDirectoryDialog(new ApplicationDataLocationService(Path.Combine(root, "locator.json")), initial: root, allowLegacy: false);
            try
            {
                Render(dialog, "Dark", 620, 300); Render(dialog, "Light", 620, 300);
                Assert.False(Directory.Exists(root));
                Assert.Null(dialog.SelectedPaths);
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void Missing_images_can_be_cleared_and_empty_configuration_is_persisted()
    {
        Sta(() =>
        {
            var source = new ImageInputSource("missing");
            source.LoadFromConfig(new ImageSourceConfig { NodeId = "missing", FilePaths = new() { @"Z:\unavailable\one.png", @"Z:\unavailable\two.png" }, CurrentIndex = 0 });
            ImageSourceConfig? saved = null;
            source.ConfigChanged = value => saved = value;
            var control = new ImagePreviewControl { ActiveInputSource = source, ImageCollection = source.Images, CurrentImageIndex = source.CurrentIndex };
            Assert.Empty(source.Images); Assert.Equal(-1, source.CurrentIndex);
            Assert.False(control.ClearAllCommand.CanExecute(null));
            control.ClearInputReferences();
            Assert.Empty(source.Images); Assert.Equal(-1, source.CurrentIndex);
            Assert.NotNull(saved); Assert.Empty(saved!.FilePaths); Assert.Empty(saved.ForRunPaths);
            Assert.Equal(-1, saved.CurrentIndex); Assert.False(control.ClearAllCommand.CanExecute(null));
        });
    }

    [Fact]
    public void Unloading_preview_does_not_clear_source_or_persisted_paths()
    {
        Sta(() =>
        {
            var source = new ImageInputSource("source");
            source.AddImage(new ImageInfo { FilePath = @"Z:\missing.png", Name = "missing.png" });
            var control = new ImagePreviewControl { ActiveInputSource = source, ImageCollection = source.Images };
            control.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.Single(source.Images); Assert.Single(source.ToConfig().FilePaths);
            Assert.True(control.ClearAllCommand.CanExecute(null));
        });
    }

    [Fact]
    public void Replacing_and_removing_missing_reference_does_not_delete_real_file()
    {
        Sta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "SunEyeVision-ImageReference", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var file = Path.Combine(root, "resource.png"); File.WriteAllBytes(file, new byte[] { 1, 2, 3 });
                var source = new ImageInputSource("source");
                var missing = new ImageInfo { FilePath = Path.Combine(root, "deleted.png"), IsForRun = true };
                source.AddImage(missing); source.CurrentIndex = 0;
                source.ReplaceImage(missing, file);
                Assert.Equal(file, source.CurrentImage!.FilePath); Assert.True(source.CurrentImage.IsForRun);
                source.RemoveMissingImages(); Assert.Single(source.Images);
                source.ClearImages(); Assert.True(File.Exists(file));
            }
            finally { Directory.Delete(root, true); }
        });
    }

    [Fact]
    public void Clearing_releases_cached_image_and_cancels_pending_thumbnail()
    {
        Sta(() =>
        {
            var image = new ImageInfo { FilePath = @"Z:\missing.png" };
            image.EnsureThumbnail();
            image.CancelThumbnail();
            Assert.Equal(ThumbnailState.Pending, image.ThumbnailState);
            var source = new ImageInputSource("source"); source.AddImage(image);
            source.ClearImages(); Assert.Null(image.FullImage); Assert.Null(image.Thumbnail);
        });
    }

}
