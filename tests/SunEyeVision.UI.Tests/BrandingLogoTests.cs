using System;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class BrandingLogoTests
{
    [Fact]
    public void Solution_icon_uses_folded_document_and_same_brand_geometry()
    {
        RunSta(() =>
        {
            var resources = LogoResources();
            var icon = Assert.IsType<DrawingImage>(resources["SunEyeVision.SolutionIcon"]);
            var group = Assert.IsType<DrawingGroup>(icon.Drawing);
            Assert.Equal(3, group.Children.Count);
            var brand = Assert.IsType<DrawingGroup>(group.Children[2]);
            Assert.Equal(new Matrix(0.7,0,0,0.7,76,96), brand.Transform.Value);
            Assert.Same(Assert.IsType<DrawingImage>(resources["SunEyeVision.BrandLogo"]).Drawing, brand.Children[0]);
            Assert.True(icon.IsFrozen);
        });
    }

    [Fact]
    public void Solution_ico_has_seven_sizes_and_document_silhouette()
    {
        RunSta(() =>
        {
            var decoder = BitmapDecoder.Create(
                new Uri("pack://application:,,,/SunEyeVision.UI;component/Icons/solution.ico"),
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            Assert.Equal(new[] {16,24,32,48,64,128,256},
                decoder.Frames.Select(frame => frame.PixelWidth).OrderBy(width => width).ToArray());
            var largest = decoder.Frames.OrderByDescending(frame => frame.PixelWidth).First();
            var bitmap = new FormatConvertedBitmap(largest, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[256 * 256 * 4];
            bitmap.CopyPixels(pixels, 256 * 4, 0);
            Assert.Equal(0, pixels[(128 * 256 + 15) * 4 + 3]); // Document side margin, unlike square app tile.
            Assert.Equal(255, pixels[(40 * 256 + 128) * 4 + 3]);
            Assert.Contains(Enumerable.Range(0, 256 * 256),
                i => pixels[i * 4 + 3] == 255 && pixels[i * 4 + 2] > 220 && pixels[i * 4 + 1] > 130 && pixels[i * 4] < 70);
        });
    }

    [Fact]
    public void Desktop_icon_has_ice_blue_tile_and_reuses_the_transparent_logo()
    {
        RunSta(() =>
        {
            var resources = LogoResources();
            var logo = Assert.IsType<DrawingImage>(resources["SunEyeVision.BrandLogo"]);
            var icon = Assert.IsType<DrawingImage>(resources["SunEyeVision.AppIcon"]);
            var group = Assert.IsType<DrawingGroup>(icon.Drawing);
            Assert.Equal(2, group.Children.Count);
            Assert.Same(logo.Drawing, group.Children[1]);
            var background = Assert.IsType<GeometryDrawing>(group.Children[0]);
            var geometry = Assert.IsType<RectangleGeometry>(background.Geometry);
            Assert.Equal(new Rect(8, 8, 496, 496), geometry.Rect);
            Assert.Equal(104, geometry.RadiusX);
            var gradient = Assert.IsType<LinearGradientBrush>(background.Brush);
            Assert.Equal(new[] { Color.FromRgb(232,246,255), Color.FromRgb(183,218,242), Color.FromRgb(119,176,217) },
                gradient.GradientStops.Select(stop => stop.Color).ToArray());
            Assert.True(icon.IsFrozen);
        });
    }

    [Fact]
    public void Every_ico_frame_has_opaque_background_and_transparent_rounded_corners()
    {
        RunSta(() =>
        {
            var decoder = BitmapDecoder.Create(
                new Uri("pack://application:,,,/SunEyeVision.UI;component/Icons/suneyevision.ico"),
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            foreach (var frame in decoder.Frames)
            {
                var bitmap = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                byte[] Pixel(int x, int y)
                {
                    var value = new byte[4];
                    bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), value, 4, 0);
                    return value;
                }
                Assert.Equal(0, Pixel(0,0)[3]);
                var top = Pixel(frame.PixelWidth / 2, frame.PixelHeight / 8);
                var bottom = Pixel(frame.PixelWidth / 2, frame.PixelHeight * 7 / 8);
                Assert.Equal(255, top[3]);
                Assert.Equal(255, bottom[3]);
                Assert.True(top[2] > bottom[2]); // Visible top-light/bottom-blue gradient.
                Assert.True(bottom[0] > bottom[2]);
            }
        });
    }

    private static ResourceDictionary LogoResources() => new()
    {
        Source = new Uri("pack://application:,,,/SunEyeVision.UI;component/Resources/Branding/Logo.xaml")
    };

    [Fact]
    public void Logo_is_native_vector_with_selected_palette_and_no_background()
    {
        RunSta(() =>
        {
            var logo = Assert.IsType<DrawingImage>(LogoResources()["SunEyeVision.BrandLogo"]);
            var group = Assert.IsType<DrawingGroup>(logo.Drawing);
            Assert.Equal(6, group.Children.Count);
            Assert.True(logo.IsFrozen);
            Assert.InRange(group.Bounds.Width, 450, 460);
            Assert.InRange(group.Bounds.Height, 195, 200);
            var shapes = group.Children.Select(Assert.IsType<GeometryDrawing>).ToArray();
            Assert.All(shapes, shape => Assert.Null(shape.Pen));
            Assert.Equal(Color.FromRgb(9, 100, 172), Assert.IsType<SolidColorBrush>(shapes[0].Brush).Color);
            Assert.Equal(Color.FromRgb(242, 174, 22), Assert.IsType<SolidColorBrush>(shapes[2].Brush).Color);
            Assert.All(shapes.Skip(3), shape => Assert.Equal(Colors.White, Assert.IsType<SolidColorBrush>(shape.Brush).Color));
        });
    }

    [Fact]
    public void Same_vector_renders_transparently_at_ui_sizes_and_both_themes()
    {
        RunSta(() =>
        {
            var image = new Image { Stretch = Stretch.Uniform };
            image.Resources.MergedDictionaries.Add(LogoResources());
            image.SetResourceReference(Image.SourceProperty, "SunEyeVision.BrandLogo");
            DrawingImage? first = null;
            foreach (var theme in new[] { "Dark", "Light", "Dark" })
            {
                var themeResources = new ResourceDictionary
                {
                    Source = new Uri($"pack://application:,,,/SunEyeVision.UI;component/Themes/{theme}/{theme}Theme.xaml")
                };
                if (image.Resources.MergedDictionaries.Count > 1)
                    image.Resources.MergedDictionaries.RemoveAt(1);
                image.Resources.MergedDictionaries.Add(themeResources);
                foreach (var size in new[] { 24, 55, 128 })
                {
                    image.Width = image.Height = size;
                    image.Measure(new Size(size, size));
                    image.Arrange(new Rect(0, 0, size, size));
                    image.UpdateLayout();
                    var source = Assert.IsType<DrawingImage>(image.Source);
                    first ??= source;
                    Assert.Same(first, source);
                    var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(image);
                    var pixels = new byte[size * size * 4];
                    bitmap.CopyPixels(pixels, size * 4, 0);
                    Assert.Equal(0, pixels[3]); // Outside mark is genuinely transparent.
                    Assert.Contains(Enumerable.Range(0, size * size), i => pixels[i * 4 + 3] == 255);
                    var directory = Environment.GetEnvironmentVariable("SUNEYEVISION_LOGO_PREVIEW");
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                        Save(bitmap, Path.Combine(directory, $"logo-transparent-{size}.png"));
                        var background = theme == "Dark" ? Color.FromRgb(36, 36, 36) : Colors.White;
                        var visual = new DrawingVisual();
                        using (var dc = visual.RenderOpen())
                        {
                            dc.DrawRectangle(new SolidColorBrush(background), null, new Rect(0, 0, size, size));
                            dc.DrawImage(bitmap, new Rect(0, 0, size, size));
                        }
                        var preview = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
                        preview.Render(visual);
                        Save(preview, Path.Combine(directory, $"logo-{theme}-{size}.png"));
                    }
                }
            }
        });
    }

    [Fact]
    public void Windows_icon_has_seven_native_sizes_instead_of_magnifying_small_frame()
    {
        RunSta(() =>
        {
            var decoder = BitmapDecoder.Create(
                new Uri("pack://application:,,,/SunEyeVision.UI;component/Icons/suneyevision.ico"),
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            Assert.Equal(new[] { 16, 24, 32, 48, 64, 128, 256 },
                decoder.Frames.Select(frame => frame.PixelWidth).OrderBy(width => width).ToArray());
            Assert.All(decoder.Frames, frame => Assert.Equal(frame.PixelWidth, frame.PixelHeight));
        });
    }

    private static void Save(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void RunSta(Action action)
    {
        // The test host has no WPF Application startup to register the pack URI parser.
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
        _ = System.IO.Packaging.PackUriHelper.Create(new Uri("http://localhost/"));
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Logo test timed out");
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
