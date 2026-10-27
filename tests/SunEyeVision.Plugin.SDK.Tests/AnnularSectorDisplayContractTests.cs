using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public class AnnularSectorDisplayContractTests
{
    [Fact]
    public void Group_type_filters_only_annulus_values_and_keeps_existing_field_projection()
    {
        var item = new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.AnnularSector };
        Assert.Equal(typeof(Annulus2d[]), item.ParamType);
        var sources = new[] { typeof(Annulus2d[]), typeof(Arc2d[]), typeof(Circle2d[]), typeof(double[]) }
            .Select((type, i) => new AvailableDataSource { SourceNodeId = "n", PropertyName = "p" + i, PropertyType = type }).ToList();
        var selected = ParamBindingBase.FilterDataSourcesByType(sources, item.ParamType!);
        Assert.Equal("p0", Assert.Single(selected).PropertyName);
        Assert.Equal("Annulus", SdkTypeVocabulary.ToTypeVocabulary(typeof(Annulus2d)));
        Assert.Equal(typeof(Annulus2d[]), SdkTypeVocabulary.ToSystemType("Annulus"));
    }

    [Fact]
    public void Actual_Wpf_annular_sector_has_hole_radial_edges_major_sweep_and_seamless_full_ring()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var make = typeof(ResultRenderLayer).GetMethod("CreateAnnulusGeometry", BindingFlags.NonPublic | BindingFlags.Static)!;
                Geometry GeometryOf(Annulus2d a) => (Geometry)make.Invoke(null, new object[] { a })!;
                Point At(double r, double a) => new(60 + r * Math.Cos(a), 60 + r * Math.Sin(a));
                var quarter = GeometryOf(new Annulus2d(new Point(60,60), 15, 40, 0, Math.PI / 2));
                Assert.True(quarter.FillContains(At(25, Math.PI / 4)));
                Assert.False(quarter.FillContains(new Point(60,60)));
                Assert.False(quarter.FillContains(At(25, Math.PI)));
                var major = GeometryOf(new Annulus2d(new Point(60,60), 15, 40, 5.5, 5.5 + 1.5 * Math.PI));
                Assert.True(major.FillContains(At(25, 5.5 + Math.PI)));
                Assert.False(major.FillContains(At(25, 5.5 - 0.2)));
                var disk = GeometryOf(new Annulus2d(new Point(60,60), 0, 40, 0, Math.PI / 2));
                Assert.True(disk.FillContains(new Point(65,65)));
                Assert.True(GeometryOf(new Annulus2d(new Point(), -1, 3)).IsEmpty());
                Assert.True(GeometryOf(new Annulus2d(new Point(), 1, 3, 0, 0)).IsEmpty());

                var style = new GraphicsStyle { OkColor = Colors.Lime, NgColor = Colors.Red, LineWidth = 2 };
                var collector = new DisplayCollector(); collector.AddAnnulus(style, new Annulus2d(new Point(60,60), 15, 40));
                var layer = new ResultRenderLayer(); var surface = new Grid { Width=120, Height=120, Background=Brushes.Black };
                surface.Children.Add(layer); layer.SetDisplayCollector(collector);
                surface.Measure(new Size(120,120)); surface.Arrange(new Rect(0,0,120,120)); surface.UpdateLayout();
                byte[] Render(string file)
                {
                    var frame = new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => frame.Continue=false));
                    System.Windows.Threading.Dispatcher.PushFrame(frame); surface.UpdateLayout();
                    var bitmap = new RenderTargetBitmap(120,120,96,96,PixelFormats.Pbgra32); bitmap.Render(surface);
                    if (Environment.GetEnvironmentVariable("S2_CASE_ROOT") is {} path)
                    { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var output=System.IO.File.Create(System.IO.Path.Combine(path,file)); encoder.Save(output); }
                    var pixels=new byte[120*120*4]; bitmap.CopyPixels(pixels,120*4,0); return pixels;
                }
                var outline=Render("group-annular-sector-full-outline.png");
                int Channel(byte[] p,int x,int y,int channel) => p[(y*120+x)*4+channel];
                Assert.True(Channel(outline,100,60,1)>100, "Outer boundary must render.");
                Assert.True(Channel(outline,75,60,1)>100, "Inner boundary must render.");
                Assert.Equal(0,Channel(outline,85,60,1)); // no 360-degree radial seam
                var graphic=Assert.IsType<GraphicsDisplayItem>(Assert.Single(collector.Items)); graphic.IsFilled=true;
                layer.SetDisplayCollector(null); layer.SetDisplayCollector(collector);
                var filled=Render("group-annular-sector-full-filled.png");
                Assert.True(Channel(filled,85,60,1)>200);
                Assert.Equal(0,Channel(filled,60,60,1)); // hole stays empty
                layer.SetDisplayCollector(null);
            }
            catch(Exception error) { failure=error; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        if(failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
