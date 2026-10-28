using System;
using System.Windows.Controls;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;
using SunEyeVision.Plugin.Wpf.Design.Results.PropertyList;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;
namespace SunEyeVision.Plugin.Wpf.Design.Results.ResultShell;
public partial class ResultWorkspaceDesignView : UserControl
{
    public ResultWorkspaceDesignView()
    {
        InitializeComponent();
        var selected = new ImageChoice("图像源1.输出图像");
        Workspace.InputItems = new[] { new ImageChoice("图像源1.输入图像") };
        Workspace.OutputItems = new[] { selected }; Workspace.SelectedOutputItem = selected;
        Workspace.SelectedInputItem = ((ImageChoice[])Workspace.InputItems)[0];
        Workspace.InputImage = new SampleImage(); Workspace.OutputImage = new SampleImage();
        Workspace.PropertyListContent = new PropertyListView { ItemsSource = PropertyListDesignView.CreateRows(), IsReadOnly = true };
        Workspace.SourceStripContent = new ImageSourceStrip();
        Workspace.HelpContent = "设计示例；真实工具未提供帮助时显示未提供工具帮助。历史服务尚未接入，保留禁用入口。";
    }
    public sealed record ImageChoice(string DisplayName)
    {
        public bool IsCheck => false;
        public bool CanOverlay => false;
        public bool IsBackgroundImage => false;
    }
    private sealed class SampleImage : IImageData
    {
        public int Width => 128; public int Height => 96;
        public byte[] Buffer { get; set; } = CreateBuffer();
        public string MemoryAddress => "0"; public string PixelFormat => "Gray8";
        private static byte[] CreateBuffer() { var b = new byte[128 * 96]; for (int y = 0; y < 96; y++) for (int x = 0; x < 128; x++) b[y * 128 + x] = (byte)((x / 16 + y / 16) % 2 == 0 ? 80 : 170); return b; }
    }
}
