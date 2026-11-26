using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Tool.IntensityMeasure.Runtime;

namespace SunEyeVision.Tool.IntensityMeasure.Wpf.Views;

/// <summary>
/// 结果页：亮度测量是单结果（属性列表），呈现内容与叠加需求全部来自 Runtime 声明
/// （<see cref="IntensityMeasureTool.ResultProjection"/> 与 <see cref="IntensityMeasureResultDisplay.Descriptor"/>），
/// 这里只组合官方呈现控件，没有第二套模型。
/// </summary>
public partial class IntensityMeasureResultView : UserControl, IToolResultView
{
    private readonly IToolResultView? _presenter;
    private bool _disposed;

    public IntensityMeasureResultView()
    {
        InitializeComponent();
    }

    public IntensityMeasureResultView(ToolSchema schema, ResultProjectionDescriptor projection)
    {
        InitializeComponent();
        (ResultHost.Content as IDisposable)?.Dispose();
        _presenter = CreatePresenter(schema, projection);
        ResultHost.Content = _presenter.View;
        _presenter.SelectionHighlightChanged += OnSelectionHighlightChanged;
    }

    private static IToolResultView CreatePresenter(ToolSchema schema, ResultProjectionDescriptor projection)
        => projection.Kind switch
        {
            ResultProjectionKind.PropertyList => new DeclaredPropertyList(schema, projection),
            ResultProjectionKind.StandardTable => new DeclaredResultTable(schema, projection),
            _ => throw new ArgumentException("亮度测量的结果投影必须是属性列表或标准表。", nameof(projection))
        };

    private IToolResultView Presenter => _presenter ?? throw new InvalidOperationException("结果视图需要正式输出声明。");
    public FrameworkElement View => this;
    public IReadOnlyList<Port> Sources => Presenter.Sources;
    public event EventHandler<ResultSelection>? SelectionHighlightChanged;

    private void OnSelectionHighlightChanged(object? sender, ResultSelection selection)
    {
        if (ReferenceEquals(sender, _presenter)) SelectionHighlightChanged?.Invoke(this, selection);
    }

    public void Present(IResultPublication publication) => Presenter.Present(publication);

    public void Clear() => Presenter.Clear();

    /// <summary>组合式释放：宿主内容、呈现控件与事件订阅一起清理，单个失败不阻断其余释放。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var errors = new List<Exception>();
        try { ResultHost.Content = null; } catch (Exception error) { errors.Add(error); }
        try { _presenter?.Dispose(); } catch (Exception error) { errors.Add(error); }
        finally
        {
            if (_presenter is not null) _presenter.SelectionHighlightChanged -= OnSelectionHighlightChanged;
            SelectionHighlightChanged = null;
        }
        if (errors.Count > 0) throw new AggregateException("亮度测量结果视图清理失败。", errors);
    }
}
