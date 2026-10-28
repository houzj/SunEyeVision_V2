using System;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;
using SunEyeVision.Plugin.Wpf.Design.Results.PropertyList;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources;
namespace SunEyeVision.Plugin.Wpf.Design.Results.ResultShell;
public partial class ResultWorkspaceDesignView : UserControl, IDisposable
{
    private NativeRoiSamples.Image? _inputRaster, _outputRaster;
    private long _generation;
    private bool _disposed;
    public IReadOnlyImage? InputRaster => _inputRaster;
    public IReadOnlyImage? OutputRaster => _outputRaster;
    public NativeResultViewport? InputViewportOwner { get; private set; }
    public NativeResultViewport? OutputViewportOwner { get; private set; }
    public ResultWorkspaceDesignView()
    {
        InitializeComponent();
        var selected = new ImageChoice("图像源1.输出图像");
        Workspace.InputItems = new[] { new ImageChoice("图像源1.输入图像") };
        Workspace.OutputItems = new[] { selected }; Workspace.SelectedOutputItem = selected;
        Workspace.SelectedInputItem = ((ImageChoice[])Workspace.InputItems)[0];
        Workspace.PropertyListContent = new PropertyListView { ItemsSource = PropertyListDesignView.CreateRows(), IsReadOnly = true };
        Workspace.SourceStripContent = new ImageSourceStrip();
        Workspace.HelpContent = "设计示例；真实工具未提供帮助时显示未提供工具帮助。历史服务尚未接入，保留禁用入口。";
        Loaded += OnLoaded; Unloaded += OnUnloaded;
    }
    public sealed record ImageChoice(string DisplayName)
    {
        public bool IsCheck => false;
        public bool CanOverlay => false;
        public bool IsBackgroundImage => false;
    }
    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (!ReferenceEquals(args.OriginalSource, this) || _disposed || InputViewportOwner is not null) return;
        var generation = ++_generation;
        try
        {
            _inputRaster = new(); _outputRaster = new();
            var input = NativeResultViewport.Attach(Workspace.InputViewport); InputViewportOwner = input;
            var output = NativeResultViewport.Attach(Workspace.OutputViewport); OutputViewportOwner = output;
            if (!input.BindImage(_inputRaster, () => !_disposed && generation == _generation && ReferenceEquals(input, InputViewportOwner))
                || !output.BindImage(_outputRaster, () => !_disposed && generation == _generation && ReferenceEquals(output, OutputViewportOwner)))
                throw new InvalidOperationException("原结果工作区设计图像未接纳。");
        }
        catch { ReleaseFrames(); throw; }
    }
    private void OnUnloaded(object sender, RoutedEventArgs args)
    { if (ReferenceEquals(args.OriginalSource, this)) ReleaseFrames(); }
    private void ReleaseFrames()
    {
        ++_generation;
        var input = InputViewportOwner; InputViewportOwner = null;
        var output = OutputViewportOwner; OutputViewportOwner = null;
        var inputRaster = _inputRaster; _inputRaster = null;
        var outputRaster = _outputRaster; _outputRaster = null;
        try { input?.Dispose(); }
        finally { try { output?.Dispose(); } finally { try { inputRaster?.Dispose(); } finally { outputRaster?.Dispose(); } } }
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        Loaded -= OnLoaded; Unloaded -= OnUnloaded; ReleaseFrames();
    }
}
