using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls.Icons;
using SunEyeVision.Plugin.SDK.UI.Windows;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.UI.Shell.Controls.Toolbox;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.Tool.ProtocolAssemble;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ToolboxPresentationTests
{
    [Fact]
    public void Category_catalog_matches_confirmed_workflow_order()
    {
        Assert.Equal(new[] { "采集", "定位", "测量", "识别", "缺陷检测", "标定", "运算", "图像处理", "颜色处理", "拆分组合", "图形生成", "逻辑工具", "通信", "自定义组工具", "自定义工具" }, ToolCategories.Definitions.Select(d => d.Name));
        Assert.Equal(ToolCategories.Definitions.OrderBy(d => d.Order), ToolCategories.Definitions);
        Assert.All(ToolCategories.Definitions, d => Assert.True(ToolIconGeometry.Contains(d.IconKey)));
        Assert.Equal(1000, ToolCategories.GetDefinition("扩展分类").Order);
    }

    [Fact]
    public void Toolbox_reads_deployed_directory_without_loading_tool_types()
    {
        var root=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT") ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","_validation","ToolboxCases")),Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root,"plugins","sample"));
        File.WriteAllText(Path.Combine(root,"ToolGroups.xml"),"<ToolGroups SchemaVersion='1'><Group Name='通信' Order='14' Icon='CommunicationImageEnable.png' HoverIcon='CommunicationImageHover.png'/><Tool Id='first' Category='通信' Directory='sample'/><Tool Id='second' Category='通信' Directory='sample'/></ToolGroups>");
        File.WriteAllText(Path.Combine(root,"plugins","sample","ToolItemInfo.xml"),"<ToolItems SchemaVersion='1' Assembly='missing.dll'><Tool Id='second' DisplayName='发送' Category='通信' Order='40' Icon='send' EntryType='Missing.Second'/><Tool Id='first' DisplayName='接收' Category='通信' Order='10' Icon='receive' EntryType='Missing.First'/></ToolItems>");
        var directory=new SunEyeVision.Plugin.Infrastructure.Managers.Plugin.ToolDirectoryCatalog(Path.Combine(root,"plugins"),Path.Combine(root,"ToolGroups.xml"));
        var model=new ToolboxViewModel(directory);
        Assert.Null(Assert.Single(model.Categories).Tools);
        model.SelectedCategory="通信";
        Assert.Equal(new[]{"first","second"},model.SelectedCategoryTools.Select(t=>t.ToolId));
        Assert.True(model.IsCompactModePopupOpen);
        model.IsCompactMode=false;
        Assert.False(model.IsCompactModePopupOpen);
        Assert.Equal(230,model.PreferredWidth);
        Assert.False(File.Exists(Path.Combine(root,"plugins","sample","missing.dll")));
    }
    [Fact]
    public void Communication_nodes_and_metadata_share_icon_keys_without_changing_saved_identity()
    {
        ToolRegistry.RegisterTool(typeof(ProtocolParseTool)); ToolRegistry.RegisterTool(typeof(ProtocolAssembleTool));
        foreach (var id in new[] { "ProtocolParse", "ProtocolAssemble" })
        {
            var node = new SunEyeVision.UI.Shared.WorkflowNode("existing-node", "旧名称", "通信", id);
            Assert.Equal(ToolRegistry.GetToolMetadata(id)!.Icon, node.Icon);
            Assert.Equal("existing-node", node.Id);
            Assert.True(ToolIconGeometry.Contains(node.Icon));
        }
    }

    [Fact]
    public void All_icon_geometries_are_frozen_and_pair_semantics_are_distinct()
    {
        var keys = new[] { "generic", "camera", "image", "sliders", "locate", "region", "ruler", "axes", "exchange", "branch", "code", "receive", "send", "parse", "assemble", "circle", "line", "match", "threshold", "binarize", "morphology", "blob", "point-point", "point-line", "point-circle", "line-line", "line-circle", "circle-circle", "verify", "subroutine" };
        Assert.All(keys, key => { Assert.True(ToolIconGeometry.Contains(key)); Assert.True(ToolIconGeometry.Get(key).IsFrozen); Assert.False(ToolIconGeometry.Get(key).Bounds.IsEmpty); });
        Assert.NotEqual(ToolIconGeometry.Get("parse").ToString(), ToolIconGeometry.Get("assemble").ToString());
        Assert.NotEqual(ToolIconGeometry.Get("receive").ToString(), ToolIconGeometry.Get("send").ToString());
        Assert.Same(ToolIconGeometry.Get("generic"), ToolIconGeometry.Get("unregistered"));
    }

    [Fact]
    public void Vector_icons_render_in_two_appearances_and_standard_title_uses_same_key()
    {
        OnSta(() =>
        {
            var gallery = new StackPanel { Width = 660, Background = Brushes.White };
            foreach (var dark in new[] { false, true })
            {
                var surface = new StackPanel { Background = dark ? new SolidColorBrush(Color.FromRgb(35, 35, 35)) : Brushes.White, Margin = new Thickness(0, 0, 0, 8) };
                var foreground = dark ? Brushes.White : Brushes.Black;
                surface.Children.Add(new TextBlock { Text = dark ? "深色 · SDK矢量图标" : "浅色 · SDK矢量图标", Foreground = foreground, Margin = new Thickness(12) });
                var row = new WrapPanel();
                foreach (var definition in ToolCategories.Definitions)
                {
                    var cell = new StackPanel { Width = 72, Margin = new Thickness(0, 4, 0, 10) };
                    cell.Children.Add(new ToolIcon { Key = definition.IconKey, Foreground = foreground, Width = 28, Height = 28, HorizontalAlignment = HorizontalAlignment.Center });
                    cell.Children.Add(new TextBlock { Text = definition.Name, Foreground = foreground, TextAlignment = TextAlignment.Center, FontSize = 12 }); row.Children.Add(cell);
                }
                surface.Children.Add(row);
                var tools = new WrapPanel();
                foreach (var item in new[] { ("receive", "接收数据"), ("parse", "协议解析"), ("assemble", "协议组装"), ("send", "发送数据") })
                {
                    var cell = new StackPanel { Width = 145, Margin = new Thickness(10, 4, 10, 12) };
                    cell.Children.Add(new ToolIcon { Key = item.Item1, Foreground = foreground, Width = 24, Height = 24, HorizontalAlignment = HorizontalAlignment.Center });
                    cell.Children.Add(new TextBlock { Text = item.Item2, Foreground = foreground, TextAlignment = TextAlignment.Center, FontSize = 12 }); tools.Children.Add(cell);
                }
                surface.Children.Add(tools); gallery.Children.Add(surface);
            }
            gallery.Measure(new Size(660, double.PositiveInfinity)); gallery.Arrange(new Rect(0, 0, 660, gallery.DesiredSize.Height)); gallery.UpdateLayout();
            var bitmap = new RenderTargetBitmap(660, (int)Math.Ceiling(gallery.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(gallery);
            Assert.True(bitmap.PixelWidth > 0);
            var root = AppContext.BaseDirectory;
            while (!System.IO.File.Exists(System.IO.Path.Combine(root, "SunEyeVision.slnx"))) root = System.IO.Directory.GetParent(root)!.FullName;
            var directory = System.IO.Path.Combine(root, "artifacts", "validation", "toolbox-presentation-20261012"); System.IO.Directory.CreateDirectory(directory);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = System.IO.File.Create(System.IO.Path.Combine(directory, "icon-gallery.png"))) encoder.Save(file);
            var window = new DefaultDebugWindow(new UserControl()) { ToolIconKey = "parse" };
            var titleIcon = Assert.IsType<ToolIcon>(window.FindName("TitleToolIcon"));
            window.Measure(new Size(400, 550)); window.Arrange(new Rect(0, 0, 400, 550));
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
            Assert.Equal("parse", titleIcon.Key); window.Close();
            var control = new ToolboxControl { DataContext = new ToolboxViewModel() };
            control.Measure(new Size(300, 700)); control.Arrange(new Rect(0, 0, 300, 700));
        });
    }

    private static void OnSta(Action action)
    {
        Exception? error = null; var thread = new Thread(() => { try { action(); } catch (Exception e) { error = e; } });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(25)), "图标STA测试超时");
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
    public abstract class Dummy : IToolPlugin<GenericToolParameters, GenericToolResults> { public GenericToolResults Run(GenericToolParameters p) => new(); }
    [Tool("_presentation_acquisition", "采集", Category = ToolCategories.Acquisition, Icon = "image")] public sealed class Acquisition : Dummy { }
    [Tool("_presentation_preprocessing", "预处理", Category = ToolCategories.Preprocessing, Icon = "sliders")] public sealed class Preprocessing : Dummy { }
    [Tool("_presentation_location", "定位", Category = ToolCategories.Location, Icon = "locate")] public sealed class Location : Dummy { }
    [Tool("_presentation_region", "区域", Category = ToolCategories.Location, Icon = "region")] public sealed class Region : Dummy { }
    [Tool("_presentation_measurement", "测量", Category = ToolCategories.Measurement, Icon = "ruler")] public sealed class Measurement : Dummy { }
    [Tool("_presentation_calibration", "标定", Category = ToolCategories.Calibration, Icon = "axes")] public sealed class Calibration : Dummy { }
    [Tool("_presentation_communication", "通信", Category = ToolCategories.Communication, Icon = "exchange")] public sealed class Communication : Dummy { }
    [Tool("_presentation_control", "控制", Category = ToolCategories.Control, Icon = "branch")] public sealed class Control : Dummy { }
    [Tool("_presentation_script", "脚本", Category = ToolCategories.Control, Icon = "code")] public sealed class Script : Dummy { }
    [Tool("_presentation_first", "后字母名称", Category = ToolCategories.Measurement, Icon = "ruler", Order = 1)] public sealed class FirstMeasure : Dummy { }
    [Tool("_presentation_second", "前字母名称", Category = ToolCategories.Measurement, Icon = "ruler", Order = 2)] public sealed class SecondMeasure : Dummy { }
}
