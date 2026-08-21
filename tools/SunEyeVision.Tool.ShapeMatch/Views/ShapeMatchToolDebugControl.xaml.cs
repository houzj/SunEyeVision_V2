using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Tool.ShapeMatch.Models;
using WpfPoint = System.Windows.Point;

namespace SunEyeVision.Tool.ShapeMatch.Views;

public partial class ShapeMatchToolDebugControl
{
    #region 字段

    private DataSourceQueryService? _dataProvider;
    private readonly Dictionary<string, ImageSource> _previewCache = new();

    #endregion

    #region 属性

    public IToolPlugin? Tool { get; set; }

    public static readonly DependencyProperty ParametersProperty =
        DependencyProperty.Register(
            nameof(Parameters),
            typeof(ShapeMatchParameters),
            typeof(ShapeMatchToolDebugControl),
            new PropertyMetadata(null, OnParametersChanged));

        private static void OnParametersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ShapeMatchToolDebugControl control)
            {
                var oldParams = e.OldValue as ShapeMatchParameters;
                var newParams = e.NewValue as ShapeMatchParameters;
                PluginLogger.Info($"[OnParametersChanged] Parameters 变化 | " +
                    $"Old={(oldParams != null ? oldParams.GetHashCode().ToString("X8") : "null")} | " +
                    $"New={(newParams != null ? newParams.GetHashCode().ToString("X8") : "null")}",
                    "ShapeMatchTool");

                // 参数变更时立即更新位置修正矩阵
                control.UpdatePositionCorrectionMatrix();
            }
        }

    public ShapeMatchParameters Parameters
    {
        get => (ShapeMatchParameters)GetValue(ParametersProperty);
        set => SetValue(ParametersProperty, value);
    }

    #endregion

    #region 数据源管理

    public AvailableDataSource? SelectedImageSource => Parameters?.ImageSource;

    #endregion

    #region 主窗口 ImageControl 绑定

    protected ImageControl? _mainImageControl;

    public override void SetMainImageControl(ImageControl? imageControl)
    {
        _mainImageControl = imageControl;
        if (regionManager != null && imageControl != null)
        {
            regionManager.SetMainImageControl(imageControl);
            UpdatePositionCorrectionMatrix();
            PluginLogger.Info("已为区域管理器设置主窗口 ImageControl", "ShapeMatchTool");
        }
    }

    #endregion

    #region 构造函数

    public ShapeMatchToolDebugControl()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            var window = Window.GetWindow(this);
            if (window != null)
                window.Closing += OnWindowClosing;
        };

        if (templateListView != null)
            templateListView.SelectionChanged += OnTemplateSelectionChanged;

        Parameters = new ShapeMatchParameters();
        btnEditTemplate.IsEnabled = false;
        PluginLogger.Success("ShapeMatchToolDebugControl 构造函数完成", "ShapeMatchTool");
    }

    public ShapeMatchToolDebugControl(string toolId, IToolPlugin? toolPlugin, ToolMetadata? toolMetadata)
        : this()
    {
        Tool = toolPlugin;
    }

    public override void SetParameters(ToolParameters parameters)
    {
        base.SetParameters(parameters);

        if (parameters is ShapeMatchParameters shapeParams)
        {
            Parameters = shapeParams;
            int templateCount = shapeParams.Templates?.Count ?? 0;
            PluginLogger.Info($"[SetParameters] 参数注入完成 | Templates={templateCount}", "ShapeMatchTool");
            SyncMinContrastMode();
        }
        else
        {
            PluginLogger.Warning($"参数类型不匹配: 期望 ShapeMatchParameters，实际 {parameters?.GetType().Name}", "ShapeMatchTool");
        }
    }

    #endregion

    #region 资源清理

    public void Cleanup()
    {
        regionManager?.ClearMainOverlay();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        Cleanup();
    }

    #endregion

    #region 数据提供者设置

    public override void SetDataProvider(object dataProvider)
    {
        base.SetDataProvider(dataProvider);
        if (dataProvider is DataSourceQueryService queryService)
        {
            _dataProvider = queryService;
            regionManager.AvailableDataSources = AvailableDataSources;
            Parameters?.RestoreImageSource(AvailableDataSources);
            Parameters?.RestorePositionCorrectionSource(AvailableDataSources);
        }
    }

    public override void SetCurrentNode(object node)
    {
        if (node == null) return;
        base.SetCurrentNode(node);
    }

    #endregion

    #region 基类重写

    protected override object ExecuteTool()
    {
        var imageSource = Parameters?.ImageSource;
        if (imageSource == null)
            throw new InvalidOperationException("请选择输入图像源");

        if (_dataProvider == null)
            throw new InvalidOperationException("数据提供者未初始化");

        if (!_dataProvider.HasNodeExecuted(imageSource.SourceNodeId))
            throw new InvalidOperationException(
                $"当前工具缺少输入图像：图像源 '{imageSource.SourceNodeId}.{imageSource.PropertyName}' 没有值");

        if (Tool is not ShapeMatchTool shapeTool)
            throw new InvalidOperationException("工具实例无效");

        var runParams = (ShapeMatchParameters)Parameters!;

        // 从数据源获取并注入 HImage
        var image = _dataProvider.GetPropertyValue(
            imageSource.SourceNodeId,
            imageSource.PropertyName);

        if (image is HalImage halImage)
        {
            runParams.ResolvedInputImage = halImage;
        }
        else
        {
            throw new InvalidOperationException("输入图像不是 Halcon HImage 类型");
        }

        var result = shapeTool.Run(runParams);

        if (result is ShapeMatchResults shapeResult)
        {
            // 将匹配结果显示到主图像控件的渲染层（轮廓、匹配框、十字、文本等）
            _mainImageControl?.ResultRenderLayerControl?.SetDisplayCollector(shapeResult.Display);
        }

        // 执行后刷新位置修正矩阵
        UpdatePositionCorrectionMatrix();

        return result;
    }

    protected override bool CanExecuteTool()
    {
        return Parameters?.ImageSource != null && _dataProvider != null;
    }



    #endregion

    #region 模板操作事件处理

    private void OnCreateTemplateClick(object sender, RoutedEventArgs e)
    {
        var result = OpenTemplateEditor(existing: null);
        if (result != null)
        {
            result.OrderIndex = Parameters?.Templates?.Count ?? 0;
            Parameters?.Templates?.Add(result);

            PluginLogger.Info($"[创建模板] {result.Name} (ID={result.Id}) | 点数={result.EdgePointCount}", "ShapeMatchTool");

            if (templateListView != null)
            {
                templateListView.SelectedItem = result;
                templateListView.ScrollIntoView(result);
            }
        }
    }

    private void OnEditTemplateClick(object sender, RoutedEventArgs e)
    {
        TemplateModel? selected = null;

        if (sender is Button btn && btn.Tag is TemplateModel tagModel)
            selected = tagModel;
        else if (templateListView?.SelectedItem is TemplateModel listSelected)
            selected = listSelected;

        if (selected == null)
        {
            PluginLogger.Warning("[编辑模板] 请先选择一个模板", "ShapeMatchTool");
            MessageBox.Show("请先选择一个模板。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        PluginLogger.Info($"[编辑模板] 打开编辑器: {selected.Name}", "ShapeMatchTool");

        var result = OpenTemplateEditor(selected);
        if (result != null && templateListView != null)
        {
            UpdateRightPreviewArea(result);
        }
    }

    private TemplateModel? OpenTemplateEditor(TemplateModel? existing = null)
    {
        var dialog = new ShapeMatchTemplateEditorDialog();
        // 传入 BitmapSource 获取回调
        // 传入运行时的角度/缩放范围作为模板创建参数
        if (Parameters != null)
        {
            dialog.TemplateAngleStart = Parameters.AngleStart.Value;
            dialog.TemplateAngleExtent = Parameters.AngleEnd.Value - Parameters.AngleStart.Value;
            dialog.TemplateScaleMin = Parameters.ScaleMin.Value;
            dialog.TemplateScaleMax = Parameters.ScaleMax.Value;
        }

        dialog.GetCurrentImage = () =>
        {
            try
            {
                var hImg = Parameters?.GetHalconImage();
                if (hImg?.IsInitialized() == true)
                {
                    int channels = hImg.CountChannels();
                    if (channels > 1)
                    {
                        HOperatorSet.Rgb1ToGray(hImg, out var grayObj);
                        using var grayHImage = new HImage(grayObj);
                        return new HalImage(grayHImage).ToBitmapSource();
                    }
                    return new HalImage(hImg).ToBitmapSource();
                }
            }
            catch { }

            // 回退到 ImageControl 当前图像（同样转为灰度）
            var srcImage = _mainImageControl?.SourceImage;
            if (srcImage != null)
            {
                try
                {
                    var grayBitmap = new FormatConvertedBitmap();
                    grayBitmap.BeginInit();
                    grayBitmap.Source = srcImage;
                    grayBitmap.DestinationFormat = System.Windows.Media.PixelFormats.Gray8;
                    grayBitmap.EndInit();
                    grayBitmap.Freeze();
                    return grayBitmap;
                }
                catch { }
            }
            return srcImage;
        };

        if (existing != null)
            dialog.LoadExistingTemplate(existing);

        dialog.Owner = Window.GetWindow(this);
        dialog.LoadImageBeforeShow();

        if (dialog.ShowDialog() == true)
        {
            var result = dialog.GetResultTemplate();
            if (result != null)
            {
                PluginLogger.Success($"[编辑模板] 已更新: {result.Name} | 点数={result.EdgePointCount}", "ShapeMatchTool");
                return result;
            }
        }

        return null;
    }

    private void OnDeleteTemplateClick(object sender, RoutedEventArgs e)
    {
        TemplateModel? target = null;
        if (sender is Button btn && btn.Tag is TemplateModel tagModel)
            target = tagModel;
        else if (templateListView?.SelectedItem is TemplateModel listSelected)
            target = listSelected;

        if (target == null)
        {
            PluginLogger.Warning("[删除模板] 请先选择一个模板", "ShapeMatchTool");
            return;
        }

        string name = target.Name;
        var result = MessageBox.Show($"确定要删除模板「{name}」吗？", "确认删除",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        Parameters?.Templates?.Remove(target);
        ReindexTemplates();
        UpdateRightPreviewArea(null);

        PluginLogger.Info($"[删除模板] {name}", "ShapeMatchTool");
    }

    private void OnLoadTemplatesClick(object sender, RoutedEventArgs e)
    {
        PluginLogger.Info("[载入模板] 功能待实现: 从文件载入模板", "ShapeMatchTool");
    }

    private void OnMoveUpClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not TemplateModel tmpl) return;
        var templates = Parameters?.Templates;
        if (templates == null) return;

        int idx = templates.IndexOf(tmpl);
        if (idx <= 0) return;

        templates.RemoveAt(idx);
        templates.Insert(idx - 1, tmpl);
        ReindexTemplates();
        PluginLogger.Info($"[模板上移] {tmpl.Name}: {idx} → {idx - 1}", "ShapeMatchTool");
    }

    private void OnMoveDownClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not TemplateModel tmpl) return;
        var templates = Parameters?.Templates;
        if (templates == null) return;

        int idx = templates.IndexOf(tmpl);
        if (idx < 0 || idx >= templates.Count - 1) return;

        templates.RemoveAt(idx);
        templates.Insert(idx + 1, tmpl);
        ReindexTemplates();
        PluginLogger.Info($"[模板下移] {tmpl.Name}: {idx} → {idx + 1}", "ShapeMatchTool");
    }

    private void OnDeleteAllTemplatesClick(object sender, RoutedEventArgs e)
    {
        var templates = Parameters?.Templates;
        if (templates == null || templates.Count == 0) return;

        var result = MessageBox.Show(
            $"确定要删除全部 {templates.Count} 个模板吗？此操作不可撤销。",
            "确认清空",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        int count = templates.Count;
        templates.Clear();
        UpdateRightPreviewArea(null);
        PluginLogger.Info($"[删除所有模板] 已清空 {count} 个模板", "ShapeMatchTool");
    }

    private void OnTemplateSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool hasSelection = templateListView?.SelectedItem is TemplateModel;
        btnEditTemplate.IsEnabled = hasSelection;

        if (hasSelection)
        {
            var tmpl = (TemplateModel)templateListView.SelectedItem;
            UpdateRightPreviewArea(tmpl);
        }
        else
        {
            UpdateRightPreviewArea(null);
        }
    }

    private void UpdateRightPreviewArea(TemplateModel? template)
    {
        if (previewPlaceholder != null) previewPlaceholder.Visibility = Visibility.Collapsed;

        if (previewImage == null || rightStatusText == null ||
            rightPointCountText == null || rightSizeText == null) return;

        if (template == null)
        {
            previewImage.Source = null;
            if (previewPlaceholder != null) previewPlaceholder.Visibility = Visibility.Visible;
            rightStatusText.Text = "--";
            rightPointCountText.Text = "--";
            rightSizeText.Text = "--";
            return;
        }

        rightStatusText.Text = template.ExtractionStatus switch
        {
            TemplateExtractionStatus.Success => "✓ 已提取",
            TemplateExtractionStatus.Warning => "⚠ 警告",
            TemplateExtractionStatus.Failed => "✗ 失败",
            TemplateExtractionStatus.NotExtracted => "○ 未提取",
            _ => "--"
        };
        rightPointCountText.Text = template.EdgePointCount.ToString();
        rightSizeText.Text = $"{template.EdgePointCount}pt";

        var cacheKey = $"{template.Id}_{template.EdgePointCount}";
        if (_previewCache.TryGetValue(cacheKey, out var cached))
        {
            previewImage.Source = cached;
            return;
        }

        try
        {
            BitmapSource? sourceImg = null;
            if (template?.SourceImageBytes != null && template.SourceImageBytes.Length > 0
                && template.SourceImageWidth > 0 && template.SourceImageHeight > 0)
            {
                try
                {
                    int stride = template.SourceImageWidth;
                    sourceImg = BitmapSource.Create(
                        template.SourceImageWidth, template.SourceImageHeight,
                        96, 96, PixelFormats.Gray8, null,
                        template.SourceImageBytes, stride);
                    sourceImg.Freeze();
                }
                catch (Exception ex)
                {
                    PluginLogger.Warning($"[预览区] 注册图像解码失败: {ex.Message}", "ShapeMatchTool");
                }
            }

            if (sourceImg == null) return;

            // 计算检测区域联合 AABB
            double cropCenterX = 0, cropCenterY = 0, cropW = 0, cropH = 0;
            bool hasValidCrop = false;

            if (template.InspectionRegions.Count > 0)
            {
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;
                foreach (var insp in template.InspectionRegions)
                {
                    if (!insp.IsEnabled || insp.Parameters is not ShapeParameters s) continue;

                    if (s.Geometry is RotatedRect2d rrRect && Math.Abs(rrRect.Angle) < 0.01)
                    {
                        double l = rrRect.CenterX - rrRect.Width / 2, t = rrRect.CenterY - rrRect.Height / 2;
                        double r = l + rrRect.Width, b = t + rrRect.Height;
                        if (l < minX) minX = l; if (t < minY) minY = t;
                        if (r > maxX) maxX = r; if (b > maxY) maxY = b;
                    }
                    else if (s.Geometry is RotatedRect2d rr)
                    {
                        double angleRad = rr.Angle * Math.PI / 180;
                        double cosA = Math.Cos(angleRad), sinA = Math.Sin(angleRad);
                        double hw = rr.Width / 2, hh = rr.Height / 2;
                        for (int i = 0; i < 4; i++)
                        {
                            double cx = rr.CenterX + cosA * ((i & 2) == 0 ? -hw : hw) - sinA * ((i & 1) == 0 ? -hh : hh);
                            double cy = rr.CenterY + sinA * ((i & 2) == 0 ? -hw : hw) + cosA * ((i & 1) == 0 ? -hh : hh);
                            if (cx < minX) minX = cx; if (cy < minY) minY = cy;
                            if (cx > maxX) maxX = cx; if (cy > maxY) maxY = cy;
                        }
                    }
                    else if (s.Geometry is Circle2d circleVal)
                    {
                        double l = circleVal.Center.X - circleVal.Radius, t = circleVal.Center.Y - circleVal.Radius;
                        double r = circleVal.Center.X + circleVal.Radius, b = circleVal.Center.Y + circleVal.Radius;
                        if (l < minX) minX = l; if (t < minY) minY = t;
                        if (r > maxX) maxX = r; if (b > maxY) maxY = b;
                    }
                }
                if (maxX > minX && maxY > minY)
                {
                    cropCenterX = (minX + maxX) / 2;
                    cropCenterY = (minY + maxY) / 2;
                    cropW = maxX - minX;
                    cropH = maxY - minY;
                    hasValidCrop = true;
                }
            }

            if (hasValidCrop)
            {
                int margin = 20;
                int cropX = Math.Max(0, (int)(cropCenterX - cropW / 2) - margin);
                int cropY = Math.Max(0, (int)(cropCenterY - cropH / 2) - margin);
                int cropWidth = Math.Min(sourceImg.PixelWidth - cropX, (int)cropW + margin * 2);
                int cropHeight = Math.Min(sourceImg.PixelHeight - cropY, (int)cropH + margin * 2);

                if (cropWidth > 0 && cropHeight > 0)
                {
                    var cropped = new CroppedBitmap(sourceImg, new Int32Rect(cropX, cropY, cropWidth, cropHeight));

                    if (template.ContourChains != null && template.ContourChains.Count > 0)
                    {
                        var preview = BuildContourPreview(cropped, template.ContourChains,
                            cropX, cropY, template.ReferencePointX, template.ReferencePointY);
                        _previewCache[cacheKey] = preview;
                        previewImage.Source = preview;
                    }
                    else
                    {
                        _previewCache[cacheKey] = cropped;
                        previewImage.Source = cropped;
                    }
                    return;
                }
            }

            _previewCache[cacheKey] = sourceImg;
            previewImage.Source = sourceImg;
        }
        catch (Exception ex)
        {
            PluginLogger.Warning($"[预览区] 更新失败: {ex.Message}", "ShapeMatchTool");
            previewImage.Source = null;
            if (previewPlaceholder != null) previewPlaceholder.Visibility = Visibility.Visible;
        }
    }

    /// <summary>
    /// 将裁切的图像与轮廓链叠加为 DrawingImage（使用 DrawingImage 做折线）
    /// 轮廓以模型坐标系存储（参考点为原点），通过 refX/refY 平移到图像坐标系，
    /// 再减去 crop offset 映射到裁切区域
    /// </summary>
    private static ImageSource BuildContourPreview(
        ImageSource background,
        List<WpfPoint[]> contourChains,
        int offsetX, int offsetY,
        double refX, double refY)
    {
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            dc.DrawImage(background, new Rect(0, 0, background.Width, background.Height));

            var pen = new Pen(Brushes.Lime, 1.5);
            foreach (var chain in contourChains)
            {
                if (chain.Length < 2) continue;
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    double x0 = chain[0].X + refX - offsetX;
                    double y0 = chain[0].Y + refY - offsetY;
                    ctx.BeginFigure(new WpfPoint(x0, y0), false, false);
                    for (int i = 1; i < chain.Length; i++)
                    {
                        double xi = chain[i].X + refX - offsetX;
                        double yi = chain[i].Y + refY - offsetY;
                        ctx.LineTo(new WpfPoint(xi, yi), true, false);
                    }
                }
                geo.Freeze();
                dc.DrawGeometry(null, pen, geo);
            }
        }
        drawing.Freeze();
        return new DrawingImage(drawing);
    }

    private void ReindexTemplates()
    {
        var templates = Parameters?.Templates;
        if (templates == null) return;
        for (int i = 0; i < templates.Count; i++)
            templates[i].OrderIndex = i;
    }

    #endregion

    #region 最小对比度模式同步

    private void SyncMinContrastMode()
    {
        if (Parameters == null || cmbMinContrastMode == null) return;
        bool isAuto = Parameters.IsMinContrastAuto;
        cmbMinContrastMode.SelectedIndex = isAuto ? 0 : 1;
        SyncMinContrastEnabled();
    }

    private void OnMinContrastModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Parameters == null) return;
        Parameters.IsMinContrastAuto = cmbMinContrastMode.SelectedIndex == 0;
        SyncMinContrastEnabled();
    }

    private void SyncMinContrastEnabled()
    {
        if (minContrastConfig == null || cmbMinContrastMode == null) return;
        minContrastConfig.IsEnabled = cmbMinContrastMode.SelectedIndex != 0;
    }

    #endregion

    #region 外部结果到达 & 位置修正矩阵

    /// <summary>
    /// 外部结果到达通知 - 工作流引擎执行完成后调用，刷新位置修正矩阵
    /// </summary>
    public override void OnExternalResultArrived()
    {
        PluginLogger.Info("[OnExternalResultArrived] 外部结果到达，刷新位置修正矩阵", "ShapeMatchTool");

        PluginLogger.Info($"[OnExternalResultArrived] 前置检查 | " +
            $"IsEnabled={Parameters?.IsPositionCorrectionEnabled} | " +
            $"DataProvider={_dataProvider != null} | " +
            $"BindingSource={Parameters?.PositionCorrectionBindingSource != null}",
            "ShapeMatchTool");

        UpdatePositionCorrectionMatrix();
    }

    /// <summary>
    /// 从 DataSourceQueryService 实时查询位置修正矩阵并设置到 ROI 编辑器
    /// 与结果层（ShapeMatchTool.Run）使用同一数据源，保证交互层与结果层一致
    /// </summary>
    internal void UpdatePositionCorrectionMatrix()
    {
        PluginLogger.Info("[UpdatePositionCorrectionMatrix] 开始查询矩阵", "ShapeMatchTool");

        if (Parameters?.IsPositionCorrectionEnabled == true
            && _dataProvider != null
            && Parameters.PositionCorrectionBindingSource != null)
        {
            var source = Parameters.PositionCorrectionBindingSource;

            PluginLogger.Info($"[UpdatePositionCorrectionMatrix] 查询参数 | " +
                $"SourceNodeId={source.SourceNodeId} | PropertyName={source.PropertyName}",
                "ShapeMatchTool");

            var matrix = _dataProvider.GetPropertyValue(
                source.SourceNodeId, source.PropertyName) as AffineMatrix;

            var matrixValues = matrix != null
                ? $"[{matrix.M11:F4},{matrix.M12:F4},{matrix.M21:F4},{matrix.M22:F4},{matrix.OffsetX:F2},{matrix.OffsetY:F2}]"
                : "null";
            PluginLogger.Info($"[UpdatePositionCorrectionMatrix] 查询结果 | Matrix={matrixValues}", "ShapeMatchTool");

            if (matrix != null)
            {
                Parameters.LogInfo($"设置位置修正矩阵到 ROI 编辑器 (实时查询): {matrix}");
                regionManager?.SetPositionCorrectionMatrix(matrix);
                return;
            }
        }
        if (_dataProvider == null)
            return;

        PluginLogger.Warning("[UpdatePositionCorrectionMatrix] 矩阵为null，清空区域变换", "ShapeMatchTool");
        regionManager?.SetPositionCorrectionMatrix(null);
    }

    #endregion

    #region IToolControlInitializer

    private bool _isInitialized = false;

    public override async System.Threading.Tasks.Task InitializeAsync()
    {
        if (!_isInitialized)
        {
            _isInitialized = true;
            UpdatePositionCorrectionMatrix();
        }
        await System.Threading.Tasks.Task.CompletedTask;
    }

    #endregion
}
