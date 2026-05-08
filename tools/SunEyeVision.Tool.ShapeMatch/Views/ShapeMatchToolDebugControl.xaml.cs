using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Tool.ShapeMatch.Models;

namespace SunEyeVision.Tool.ShapeMatch.Views
{
    /// <summary>
    /// 轮廓匹配工具调试控件
    /// </summary>
    public partial class ShapeMatchToolDebugControl
    {
        #region 字段

        private DataSourceQueryService? _dataProvider;

        private readonly Dictionary<string, System.Windows.Media.ImageSource> _previewCache = new();

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
                PluginLogger.Info("已为区域管理器设置主窗口 ImageControl", "ShapeMatchTool");
            }
        }

        #endregion

        #region 构造函数

        public ShapeMatchToolDebugControl()
        {
            InitializeComponent();

            this.Loaded += (s, e) =>
            {
                var window = System.Windows.Window.GetWindow(this);
                if (window != null)
                {
                    window.Closing += OnWindowClosing;
                }
            };

            // 模板列表选择变更
            if (templateListView != null)
            {
                templateListView.SelectionChanged += OnTemplateSelectionChanged;
            }

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
                PluginLogger.Success($"已加载节点参数: MinScore={shapeParams.MinMatchScore.Value}", "ShapeMatchTool");
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
            if (regionManager != null)
            {
                regionManager.ClearMainOverlay();
            }
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
            {
                throw new InvalidOperationException("请选择输入图像源");
            }

            if (_dataProvider == null)
            {
                throw new InvalidOperationException("数据提供者未初始化");
            }

            if (!_dataProvider.HasNodeExecuted(imageSource.SourceNodeId))
            {
                throw new InvalidOperationException("图像源节点尚未执行，请先执行前驱节点");
            }

            var imageMat = _dataProvider.GetPropertyValue(
                imageSource.SourceNodeId,
                imageSource.PropertyName
            ) as Mat;

            if (imageMat == null || imageMat.Empty())
            {
                throw new InvalidOperationException("无法获取图像数据或图像为空");
            }

            if (Tool is not ShapeMatchTool shapeTool)
            {
                throw new InvalidOperationException("工具实例无效");
            }

            var runParams = (ShapeMatchParameters)Parameters!;
            runParams.ResolvedInputImage = imageMat;
            return shapeTool.Run(runParams);
        }

        protected override bool CanExecuteTool()
        {
            return Parameters?.ImageSource != null && _dataProvider != null;
        }

        #endregion

        #region 模板操作事件处理

        private void OnCreateTemplateClick(object sender, RoutedEventArgs e)
        {
            // 直接弹出模版编辑对话框（完整编辑器：图像/ROI/轮廓提取）
            var result = OpenTemplateEditor(existing: null);
            if (result != null)
            {
                result.OrderIndex = Parameters?.Templates?.Count ?? 0;
                Parameters?.Templates?.Add(result);

                PluginLogger.Info($"[创建模板] {result.Name} (ID={result.Id}) | 点数={result.ContourPointCount}", "ShapeMatchTool");

                if (templateListView != null)
                {
                    templateListView.SelectedItem = result;
                    templateListView.ScrollIntoView(result);
                }

                // SelectionChanged 事件会自动触发 UpdateRightPreviewArea
                // 此处无需重复调用
            }
        }

        private void OnEditTemplateClick(object sender, RoutedEventArgs e)
        {
            TemplateModel? selected = null;

            if (sender is Button btn && btn.Tag is TemplateModel tagModel)
            {
                selected = tagModel;
            }
            else if (templateListView?.SelectedItem is TemplateModel listSelected)
            {
                selected = listSelected;
            }

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

        /// <summary>
        /// 打开模版创建/编辑对话框
        /// </summary>
        private TemplateModel? OpenTemplateEditor(TemplateModel? existing = null)
        {
            var dialog = new ShapeMatchTemplateEditorDialog();

            // 传入当前图像获取方法
            dialog.GetCurrentImage = () => _mainImageControl?.SourceImage;
            dialog.GetCurrentMat = () => Parameters?.ResolvedInputImage;

            // 编辑模式：加载已有数据
            if (existing != null)
            {
                dialog.LoadExistingTemplate(existing);
            }

            dialog.Owner = System.Windows.Window.GetWindow(this);

            // ★ 弹窗前预加载图像，确保显示时图像立即可见
            dialog.LoadImageBeforeShow();

            if (dialog.ShowDialog() == true)
            {
                var result = dialog.GetResultTemplate();
                if (result != null && existing == null)
                {
                    return result;
                }
                else if (result != null && existing != null)
                {
                    // LoadExistingTemplate 已将 _resultTemplate 设为 existing，
                    // OnOkClick 已原地修改了所有属性，result 就是 existing 本身。
                    // 只有非同一对象时才需要复制回去（保留防御性代码避免未来重构出问题）
                    if (!ReferenceEquals(result, existing))
                    {
                        existing.Name = result.Name;
                        existing.ContourPoints = result.ContourPoints;
                        existing.SmoothKernelSize = result.SmoothKernelSize;
                        existing.EdgeLowThreshold = result.EdgeLowThreshold;
                        existing.EdgeHighThreshold = result.EdgeHighThreshold;
                        existing.EdgeApertureSize = result.EdgeApertureSize;
                        existing.ApproxMethod = result.ApproxMethod;
                        existing.ApproxPrecision = result.ApproxPrecision;
                        existing.ExtractionStatus = result.ExtractionStatus;
                        existing.ContourWidth = result.ContourWidth;
                        existing.ContourHeight = result.ContourHeight;
                        existing.CenterX = result.CenterX;
                        existing.CenterY = result.CenterY;
                        existing.InspectionRegions.Clear();
                        foreach (var insp in result.InspectionRegions)
                            existing.InspectionRegions.Add(insp);
                        existing.MaskRegions.Clear();
                        foreach (var mask in result.MaskRegions)
                            existing.MaskRegions.Add(mask);
                        existing.SourceImageBytes = result.SourceImageBytes;
                    }

                    PluginLogger.Success($"[编辑模板] 已更新: {existing.Name} | 点数={existing.ContourPointCount}", "ShapeMatchTool");
                    return existing;
                }
            }

            return null;
        }

        private void OnDeleteTemplateClick(object sender, RoutedEventArgs e)
        {
            TemplateModel? target = null;

            if (sender is Button btn && btn.Tag is TemplateModel tagModel)
            {
                target = tagModel;
            }
            else if (templateListView?.SelectedItem is TemplateModel listSelected)
            {
                target = listSelected;
            }

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
            // TODO: 从文件载入模板数据（后续实现）
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

            // 编辑模板按钮: 有选中项时才可点击
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

        /// <summary>
        /// 更新右侧预览区（ROI裁切+轮廓叠加），带缓存支持
        /// </summary>
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

            // 显示状态信息
            rightStatusText.Text = template.ExtractionStatus switch
            {
                TemplateExtractionStatus.Success => "✓ 已提取",
                TemplateExtractionStatus.Warning => "⚠ 警告",
                TemplateExtractionStatus.Failed => "✗ 失败",
                TemplateExtractionStatus.NotExtracted => "○ 未提取",
                _ => "--"
            };
            rightPointCountText.Text = template.ContourPointCount.ToString();
            rightSizeText.Text = $"{template.ContourWidth:F0} x {template.ContourHeight:F0}";

            // 缓存命中直接返回
            var cacheKey = $"{template.Id}_{template.ContourPointCount}";
            if (_previewCache.TryGetValue(cacheKey, out var cached))
            {
                previewImage.Source = cached;
                return;
            }

            // 使用模板注册图像（SourceImageBytes）生成预览
            try
            {
                BitmapSource? sourceImg = null;

                if (template?.SourceImageBytes != null &&
                    template.SourceImageBytes.Length > 0)
                {
                    try
                    {
                        using var ms = new MemoryStream(template.SourceImageBytes);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        sourceImg = bitmap;
                    }
                    catch (Exception ex)
                    {
                        PluginLogger.Warning($"[预览区] 注册图像解码失败: {ex.Message}", "ShapeMatchTool");
                    }
                }

                if (sourceImg == null) return;

                // 计算所有检测区域的联合 AABB
                double cropCenterX = 0, cropCenterY = 0, cropW = 0, cropH = 0;
                bool hasValidCrop = false;

                if (template.InspectionRegions.Count > 0)
                {
                    double minX = double.MaxValue, minY = double.MaxValue;
                    double maxX = double.MinValue, maxY = double.MinValue;
                    foreach (var insp in template.InspectionRegions)
                    {
                        if (!insp.IsEnabled || insp.Parameters is not ShapeParameters s) continue;

                        switch (s.ShapeType)
                        {
                            case ShapeType.Rectangle when Math.Abs(s.Angle) < 0.01:
                            {
                                double left = s.CenterX - s.Width / 2;
                                double top = s.CenterY - s.Height / 2;
                                double right = left + s.Width;
                                double bottom = top + s.Height;
                                if (left < minX) minX = left;
                                if (top < minY) minY = top;
                                if (right > maxX) maxX = right;
                                if (bottom > maxY) maxY = bottom;
                                break;
                            }
                            case ShapeType.Rectangle:
                            case ShapeType.RotatedRectangle:
                            {
                                double angleRad = s.Angle * Math.PI / 180;
                                double cosA = Math.Cos(angleRad);
                                double sinA = Math.Sin(angleRad);
                                double hw = s.Width / 2, hh = s.Height / 2;
                                for (int i = 0; i < 4; i++)
                                {
                                    double cx = s.CenterX
                                        + cosA * ((i & 2) == 0 ? -hw : hw)
                                        - sinA * ((i & 1) == 0 ? -hh : hh);
                                    double cy = s.CenterY
                                        + sinA * ((i & 2) == 0 ? -hw : hw)
                                        + cosA * ((i & 1) == 0 ? -hh : hh);
                                    if (cx < minX) minX = cx;
                                    if (cy < minY) minY = cy;
                                    if (cx > maxX) maxX = cx;
                                    if (cy > maxY) maxY = cy;
                                }
                                break;
                            }
                            case ShapeType.Circle:
                            {
                                double left = s.CenterX - s.Radius;
                                double top = s.CenterY - s.Radius;
                                double right = s.CenterX + s.Radius;
                                double bottom = s.CenterY + s.Radius;
                                if (left < minX) minX = left;
                                if (top < minY) minY = top;
                                if (right > maxX) maxX = right;
                                if (bottom > maxY) maxY = bottom;
                                break;
                            }
                            default:
                                continue;
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

                        if (template.ContourPoints != null && template.ContourPoints.Count > 0)
                        {
                            var preview = BuildContourPreview(cropped, template.ContourPointGroups, cropX, cropY);
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

                // 无有效裁切区域时，显示原图
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
        /// 将裁切的图像与轮廓点叠加为 DrawingImage，所有轮廓合并到单个 StreamGeometry
        /// </summary>
        private static System.Windows.Media.ImageSource BuildContourPreview(
            System.Windows.Media.ImageSource background,
            List<List<Point2D>> contourGroups,
            int offsetX, int offsetY)
        {
            var drawing = new System.Windows.Media.DrawingGroup();
            using (var dc = drawing.Open())
            {
                dc.DrawImage(background, new System.Windows.Rect(0, 0, background.Width, background.Height));

                if (contourGroups.Count > 0)
                {
                    var pen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.Lime, 2.0);
                    var geometry = new System.Windows.Media.StreamGeometry();
                    using (var ctx = geometry.Open())
                    {
                        foreach (var group in contourGroups)
                        {
                            if (group.Count < 2) continue;
                            var first = new System.Windows.Point(group[0].X - offsetX, group[0].Y - offsetY);
                            ctx.BeginFigure(first, false, false);
                            for (int i = 1; i < group.Count; i++)
                                ctx.LineTo(new System.Windows.Point(group[i].X - offsetX, group[i].Y - offsetY), true, true);
                        }
                    }
                    geometry.Freeze();
                    dc.DrawGeometry(null, pen, geometry);
                }
            }
            drawing.Freeze();
            return new System.Windows.Media.DrawingImage(drawing);
        }

        private void ReindexTemplates()
        {
            var templates = Parameters?.Templates;
            if (templates == null) return;

            for (int i = 0; i < templates.Count; i++)
            {
                templates[i].OrderIndex = i;
            }
        }

        #endregion

        #region IToolControlInitializer 实现

        public override async System.Threading.Tasks.Task InitializeAsync()
        {
            await System.Threading.Tasks.Task.CompletedTask;
        }

        #endregion
    }
}
