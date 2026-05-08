using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Tool.ShapeMatch.Models;
using SunEyeVision.CvEngine;
// 消除与 OpenCvSharp 的类型冲突
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using WpfWindow = System.Windows.Window;

namespace SunEyeVision.Tool.ShapeMatch.Views
{
    /// <summary>
    /// 模版创建/编辑对话框 - 支持图像选择、ROI绘制、轮廓提取的完整交互界面
    /// </summary>
    public partial class ShapeMatchTemplateEditorDialog : WpfWindow
    {
        #region 字段

        private Mat? _currentMat;
        private bool _ownsMat; // 是否拥有 Mat 所有权（本地载入的图像需要释放）
        private TemplateModel? _resultTemplate;
        private TemplateModel? _originalTemplate; // 编辑模式时保存原模板引用，用于深拷贝
        private List<Point2D>? _previewContourPoints;
        private bool _isEditingExisting; // 编辑已有模板（含注册图像）
        private bool _hadManualExtraction; // 用户是否在对话框中手动点击了提取/预览
        private string? _localImagePath; // 本地图像的完整路径

        // 外部传入的当前图像（来自主窗口ImageControl）
        public Func<BitmapSource?>? GetCurrentImage { get; set; }
        public Func<Mat?>? GetCurrentMat { get; set; }

        // 对话框内部区域数据源
        public ObservableCollection<RegionData> DialogInspectionRegions { get; } = new();
        public ObservableCollection<RegionData> DialogMaskRegions { get; } = new();

        #endregion

        #region 构造函数

        public ShapeMatchTemplateEditorDialog()
        {
            InitializeComponent();
            InitializeComboBoxes();
            DataContext = this;
        }

        /// <summary>
        /// 在 ShowDialog 之前预加载图像资源
        /// 确保弹窗后图像立即可见，用户不会看到空白窗口
        /// </summary>
        public void LoadImageBeforeShow()
        {
            try
            {
                Mat? mat = null;
                bool isEditing = _isEditingExisting;

                // 编辑模式：从注册图像解码（内存解码，速度很快）
                if (isEditing && _resultTemplate?.SourceImageBytes is { Length: > 0 })
                {
                    mat = Cv2.ImDecode(_resultTemplate.SourceImageBytes, ImreadModes.Color);
                }

                if (mat != null && !mat.Empty())
                {
                    if (_ownsMat) _currentMat?.Dispose();
                    _currentMat = mat;
                    _ownsMat = true;

                    if (dialogImageControl != null)
                    {
                        dialogImageControl.LoadImageDownscaled(mat);
                        BindRegionManagerToImage();
                        SelectImageSourceCombo("registered");
                        PluginLogger.Info("[ShapeMatchTemplateEditor] 从注册图像加载（编辑模式）", "ShapeMatchTool");
                    }

                    // 恢复轮廓叠加
                    TryRestoreContourOverlay();
                }
                else
                {
                    // 非编辑模式或注册图像无效：获取当前图像（轻量引用）
                    var curMat = GetCurrentMat?.Invoke();
                    if (curMat != null && !curMat.Empty())
                    {
                        _currentMat = curMat;
                        _ownsMat = false;

                        if (dialogImageControl != null)
                        {
                            dialogImageControl.LoadImageDownscaled(curMat);
                            BindRegionManagerToImage();
                            SelectImageSourceCombo("current");
                            PluginLogger.Info("[ShapeMatchTemplateEditor] 加载当前图像", "ShapeMatchTool");
                        }
                        return;
                    }

                    // 回退：通过 BitmapSource 加载
                    var bmp = GetCurrentImage?.Invoke();
                    if (bmp != null && dialogImageControl != null)
                    {
                        dialogImageControl.LoadImage(bmp);
                        BindRegionManagerToImage();
                        SelectImageSourceCombo("current");
                        PluginLogger.Info("[ShapeMatchTemplateEditor] 加载当前图像（BitmapSource）", "ShapeMatchTool");
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Warning($"[ShapeMatchTemplateEditor] 加载图像失败: {ex.Message}", "ShapeMatchTool");
            }
        }

        /// <summary>
        /// 如果已有保存的轮廓数据，恢复显示轮廓叠加层
        /// </summary>
        private void TryRestoreContourOverlay()
        {
            if (_resultTemplate?.ContourPoints == null || _resultTemplate.ContourPoints.Count == 0)
                return;

            DrawContourOverlays(_resultTemplate.ContourPointGroups,
                _resultTemplate.ExtractionStatus != TemplateExtractionStatus.Success);
            PluginLogger.Info($"[ShapeMatchTemplateEditor] 恢复已保存的轮廓: {_resultTemplate.ContourPointCount}点",
                "ShapeMatchTool");
        }

        /// <summary>
        /// 设置 ComboBox 选定项
        /// </summary>
        private void SelectImageSourceCombo(string source)
        {
            if (imageSourceCombo == null) return;
            foreach (ComboBoxItem item in imageSourceCombo.Items)
            {
                if (item.Tag?.ToString() == source)
                {
                    imageSourceCombo.SelectedItem = item;
                    return;
                }
            }
        }

        private void OnImageSourceSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (imageSourceCombo?.SelectedItem is not ComboBoxItem item) return;
            var source = item.Tag?.ToString();
            if (string.IsNullOrEmpty(source)) return;

            LoadImageBySource(source);
        }

        private void LoadImageBySource(string source)
        {
            try
            {
                if (source == "registered")
                {
                    var bytes = _resultTemplate?.SourceImageBytes;
                    if (bytes == null || bytes.Length == 0)
                    {
                        MessageBox.Show("当前模板没有注册图像数据。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var mat = Cv2.ImDecode(bytes, ImreadModes.Color);
                    if (mat == null || mat.Empty())
                    {
                        MessageBox.Show("注册图像数据无效或已损坏。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (_ownsMat) _currentMat?.Dispose();
                    _currentMat = mat;
                    _ownsMat = true;

                    if (dialogImageControl != null)
                    {
                        dialogImageControl.LoadImageDownscaled(mat);
                        BindRegionManagerToImage();
                    }
                }
                else if (source == "current")
                {
                    var matFunc = GetCurrentMat;
                    if (matFunc != null)
                    {
                        var mat = matFunc();
                        if (mat != null && !mat.Empty())
                        {
                            _currentMat = mat;
                            _ownsMat = false;

                            if (dialogImageControl != null)
                            {
                                dialogImageControl.LoadImageDownscaled(mat);
                                BindRegionManagerToImage();
                            }
                            return;
                        }
                    }

                    // 回退：BitmapSource
                    var bmpFunc = GetCurrentImage;
                    BitmapSource? bmp = null;
                    if (bmpFunc != null)
                        bmp = bmpFunc();

                    if (bmp == null)
                    {
                        MessageBox.Show("当前没有可用图像。请先在基本参数中选择输入图像并执行一次。",
                            "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (dialogImageControl != null)
                    {
                        dialogImageControl.LoadImage(bmp);
                        BindRegionManagerToImage();
                    }
                }
                else if (source == "local")
                {
                    // 如果没有已加载的本地图像路径，先打开文件对话框选择
                    if (string.IsNullOrEmpty(_localImagePath))
                    {
                        var openDialog = new OpenFileDialog
                        {
                            Title = "选择参考图像文件",
                            Filter = "图像文件|*.bmp;*.png;*.jpg;*.jpeg;*.tiff;*.tif|所有文件|*.*"
                        };
                        if (openDialog.ShowDialog() != true) return;
                        _localImagePath = openDialog.FileName;
                    }

                    var localMat = Cv2.ImRead(_localImagePath, ImreadModes.Color);
                    if (localMat.Empty())
                    {
                        MessageBox.Show("无法加载本地图像，文件可能已损坏。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        _localImagePath = null;
                        return;
                    }

                    if (_ownsMat) _currentMat?.Dispose();
                    _currentMat = localMat;
                    _ownsMat = true;

                    if (dialogImageControl != null)
                    {
                        dialogImageControl.LoadImageDownscaled(localMat);
                        BindRegionManagerToImage();
                    }
                }
                else
                {
                    return;
                }

                PluginLogger.Info($"[ShapeMatchTemplateEditor] 已切换至{source}图像", "ShapeMatchTool");
            }
            catch (Exception ex)
            {
                PluginLogger.Error($"[ShapeMatchTemplateEditor] 加载{source}图像失败: {ex.Message}", "ShapeMatchTool");
            }
        }

        #endregion

        #region 图像源选择

        private void BindRegionManagerToImage()
        {
            if (dialogRegionManager != null && dialogImageControl != null)
            {
                dialogRegionManager.SetMainImageControl(dialogImageControl);
            }
        }

        #endregion

        #region 初始化

        private void InitializeComboBoxes()
        {
            // 图像源选择
            if (imageSourceCombo != null)
            {
                imageSourceCombo.Items.Add(new ComboBoxItem { Content = "当前图像", Tag = "current" });
                imageSourceCombo.Items.Add(new ComboBoxItem { Content = "注册图像", Tag = "registered" });
                imageSourceCombo.Items.Add(new ComboBoxItem { Content = "本地图像", Tag = "local" });
                imageSourceCombo.SelectedIndex = -1;
            }

            // 近似方法 - 使用 Dictionary 显示友好名称
            var approxItems = new Dictionary<string, ContourApproxMethod>
            {
                ["多边形近似 (ApproxPolyDP)"] = ContourApproxMethod.ApproxPolyDP,
                ["不简化 (None)"] = ContourApproxMethod.None,
                ["等距重采样"] = ContourApproxMethod.UniformResample,
                ["多边形近似 + 重采样"] = ContourApproxMethod.ApproxPolyDPAndResample
            };

            if (cmbApproxMethod != null)
            {
                cmbApproxMethod.ItemsSource = approxItems;
                cmbApproxMethod.DisplayMemberPath = "Key";
                cmbApproxMethod.SelectedValuePath = "Value";
                cmbApproxMethod.SelectedValue = ContourApproxMethod.ApproxPolyDP;
            }

            // 轮廓检索模式
            if (cmbRetrievalMode != null)
            {
                cmbRetrievalMode.Items.Add("External (仅最外层)");
                cmbRetrievalMode.Items.Add("List (层次结构)");
                cmbRetrievalMode.SelectedIndex = 0;
            }

            // 初始同步控件可见性
            UpdateApproxMethodVisibility();

            PluginLogger.Info("[ShapeMatchTemplateEditor] 对话框初始化完成", "ShapeMatchTool");
        }

        /// <summary>
        /// 近似方法切换时更新相关控件的可见性
        /// </summary>
        private void OnApproxMethodChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateApproxMethodVisibility();
        }

        /// <summary>
        /// 根据当前选中的近似方法，控制"近似精度"和"目标点数"的可见性
        /// </summary>
        private void UpdateApproxMethodVisibility()
        {
            if (cmbApproxMethod == null || numApproxPrecision == null || numTargetPointCount == null) return;

            var selected = cmbApproxMethod.SelectedValue as ContourApproxMethod?;

            // 近似精度：ApproxPolyDP 或 组合模式下可见
            bool showPrecision = selected == ContourApproxMethod.ApproxPolyDP
                              || selected == ContourApproxMethod.ApproxPolyDPAndResample;
            numApproxPrecision.Visibility = showPrecision ? Visibility.Visible : Visibility.Collapsed;

            // 目标点数：UniformResample 或 组合模式下可见
            bool showTargetCount = selected == ContourApproxMethod.UniformResample
                                || selected == ContourApproxMethod.ApproxPolyDPAndResample;
            numTargetPointCount.Visibility = showTargetCount ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 加载已有模板数据进行编辑模式
        /// 使用深拷贝保护原始数据，取消编辑时原模板不受影响
        /// </summary>
        public void LoadExistingTemplate(TemplateModel template)
        {
            if (template == null) return;

            _originalTemplate = template;
            _resultTemplate = template.DeepClone();
            _isEditingExisting = true;

            // Bind UI to clone's data
            if (templateNameTextBox != null)
                templateNameTextBox.Text = _resultTemplate.Name;

            numSmoothKernel.IntValue = _resultTemplate.SmoothKernelSize;
            numLowThreshold.IntValue = _resultTemplate.EdgeLowThreshold;
            numHighThreshold.IntValue = _resultTemplate.EdgeHighThreshold;
            numApertureSize.IntValue = _resultTemplate.EdgeApertureSize;

            if (cmbApproxMethod != null)
                cmbApproxMethod.SelectedValue = _resultTemplate.ApproxMethod;

            numApproxPrecision.Value = _resultTemplate.ApproxPrecision;
            numTargetPointCount.IntValue = _resultTemplate.TargetPointCount;
            UpdateApproxMethodVisibility();

            numMinChainLength.IntValue = _resultTemplate.MinChainLength;
            numMinArea.Value = _resultTemplate.MinArea;
            if (cmbRetrievalMode != null)
                cmbRetrievalMode.SelectedIndex = _resultTemplate.RetrievalMode;
            chkL2Gradient.IsChecked = _resultTemplate.UseL2Gradient;
            chkUseSubPixel.IsChecked = _resultTemplate.UseSubPixel;

            // 加载区域数据（深拷贝版本的区域数据）
            foreach (var insp in _resultTemplate.InspectionRegions)
                DialogInspectionRegions.Add(insp);

            foreach (var mask in _resultTemplate.MaskRegions)
                DialogMaskRegions.Add(mask);

            // 恢复提取结果状态
            UpdateResultDisplay(_resultTemplate.ExtractionStatus, _resultTemplate.ContourPointCount,
                _resultTemplate.ContourWidth, _resultTemplate.ContourHeight, _resultTemplate.CenterX, _resultTemplate.CenterY);

            PluginLogger.Info($"[ShapeMatchTemplateEditor] 已加载模板(深拷贝): {template.Name}", "ShapeMatchTool");
        }

        private void OnExtractClick(object sender, RoutedEventArgs e)
        {
            _hadManualExtraction = true;
            ExecuteContourExtraction(saveResult: true);
        }

        private void OnPreviewClick(object sender, RoutedEventArgs e)
        {
            _hadManualExtraction = true;
            ExecuteContourExtraction(saveResult: false);
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            _previewContourPoints = null;

            if (_resultTemplate != null)
            {
                _resultTemplate.ContourPoints = null;
                _resultTemplate.ExtractionStatus = TemplateExtractionStatus.NotExtracted;
            }

            ClearContourOverlay();
            UpdateResultDisplay(TemplateExtractionStatus.NotExtracted, 0, 0, 0, 0, 0);

            PluginLogger.Info("[ShapeMatchTemplateEditor] 已清除轮廓", "ShapeMatchTool");
        }

        private void ExecuteContourExtraction(bool saveResult)
        {
            if (_currentMat == null || _currentMat.Empty())
            {
                MessageBox.Show("请先加载图像后再提取轮廓。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (DialogInspectionRegions.Count == 0)
            {
                MessageBox.Show("请先在图像上绘制检测区域（有效区）。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 读取参数
                int smoothKernel = numSmoothKernel.IntValue;
                int lowThreshold = numLowThreshold.IntValue;
                int highThreshold = numHighThreshold.IntValue;
                int apertureSize = numApertureSize.IntValue;
                var approxMethod = cmbApproxMethod?.SelectedValue as ContourApproxMethod? ?? ContourApproxMethod.ApproxPolyDP;
                double approxPrecision = numApproxPrecision.Value;
                int targetPointCount = numTargetPointCount.IntValue;
                int minChainLength = numMinChainLength.IntValue;
                double minArea = numMinArea.Value;
                int retrievalMode = cmbRetrievalMode?.SelectedIndex ?? 0;
                bool useL2Gradient = chkL2Gradient.IsChecked ?? true;
                bool useSubPixel = chkUseSubPixel.IsChecked ?? false;

                // ===== Step 1: 判断图像通道数，转为灰度图（全图） =====
                using var gray = new Mat();
                int channels = _currentMat.Channels();
                if (channels == 1)
                {
                    _currentMat.CopyTo(gray);
                }
                else if (channels == 3)
                {
                    Cv2.CvtColor(_currentMat, gray, ColorConversionCodes.BGR2GRAY);
                }
                else if (channels == 4)
                {
                    Cv2.CvtColor(_currentMat, gray, ColorConversionCodes.BGRA2GRAY);
                }
                else
                {
                    Cv2.ExtractChannel(_currentMat, gray, 0);
                }

                // 高斯模糊预处理（降低噪声，改善边缘检测质量）
                using var blurred = new Mat();
                if (smoothKernel > 1)
                    Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(smoothKernel, smoothKernel), 0);
                else
                    gray.CopyTo(blurred);

                // 边缘检测（Canny）— 全图检测
                using var edges = new Mat();
                Cv2.Canny(blurred, edges, lowThreshold, highThreshold, apertureSize, useL2Gradient);

                // ===== Step 2: 构建 ROI 掩码 =====
                // 多检测区域并集 - 多屏蔽区域差集 → 最终有效区域
                var enabledInspections = DialogInspectionRegions.Where(r => r.IsEnabled).ToList();
                var enabledMasks = DialogMaskRegions.Where(r => r.IsEnabled).ToList();

                if (enabledInspections.Count == 0)
                {
                    MessageBox.Show("没有启用的检测区域。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                using var roiMask = BuildRoiMask(edges.Size(), enabledInspections, enabledMasks);

                // ===== Step 3: 应用 ROI 掩码 =====
                // 仅保留 ROI 内的边缘像素，ROI 外的边缘被清零
                Cv2.BitwiseAnd(edges, roiMask, edges);

                // ===== Step 4: 查找轮廓 =====
                var retrievalModes = retrievalMode == 0 ? RetrievalModes.External : RetrievalModes.List;
                Cv2.FindContours(edges, out OpenCvSharp.Point[][] contours, out _, retrievalModes, ContourApproximationModes.ApproxNone);

                if (contours.Length == 0)
                {
                    UpdateResultDisplay(TemplateExtractionStatus.Failed, 0, 0, 0, 0, 0);
                    PluginLogger.Warning("[ShapeMatchTemplateEditor] 未检测到轮廓", "ShapeMatchTool");
                    return;
                }

                // 最小链长过滤 + 最小面积过滤
                var validContours = contours
                    .Where(c => c.Length >= minChainLength && Cv2.ContourArea(c) >= minArea)
                    .ToList();

                if (validContours.Count == 0)
                {
                    UpdateResultDisplay(TemplateExtractionStatus.Failed, 0, 0, 0, 0, 0);
                    PluginLogger.Warning("[ShapeMatchTemplateEditor] 有效轮廓数量为0，全部被过滤（最小链长/最小面积）", "ShapeMatchTool");
                    return;
                }

                // 多边形近似 / 重采样收集（ROI 坐标系）
                var approximatedGroups = new List<OpenCvSharp.Point[]>();

                foreach (var contour in validContours)
                {
                    OpenCvSharp.Point[] approximated;

                    if (approxMethod == ContourApproxMethod.ApproxPolyDP)
                    {
                        // 只做 Douglas-Peucker 近似
                        approximated = Cv2.ApproxPolyDP((IEnumerable<OpenCvSharp.Point>)contour, approxPrecision, true);
                    }
                    else if (approxMethod == ContourApproxMethod.UniformResample)
                    {
                        // 等距重采样：保留全量轮廓点，不做 Douglas-Peucker，直接重采样到目标点数
                        approximated = TemplateModel.ResampleContour(contour, targetPointCount)
                            .Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y))
                            .ToArray();
                    }
                    else if (approxMethod == ContourApproxMethod.ApproxPolyDPAndResample)
                    {
                        // 组合模式：先 ApproxPolyDP 去冗余 → 再等距重采样到目标点数
                        var afterDP = Cv2.ApproxPolyDP((IEnumerable<OpenCvSharp.Point>)contour, approxPrecision, true);
                        if (afterDP.Length >= 3)
                        {
                            approximated = TemplateModel.ResampleContour(afterDP, targetPointCount)
                                .Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y))
                                .ToArray();
                        }
                        else
                        {
                            approximated = afterDP; // 点太少，不再重采样
                        }
                    }
                    else // None
                    {
                        approximated = contour;
                    }

                    if (approximated.Length < 3) continue;
                    approximatedGroups.Add(approximated);
                }

                if (approximatedGroups.Count == 0)
                {
                    UpdateResultDisplay(TemplateExtractionStatus.Failed, 0, 0, 0, 0, 0);
                    PluginLogger.Warning("[ShapeMatchTemplateEditor] 多边形近似后无有效轮廓", "ShapeMatchTool");
                    return;
                }

                // ===== 亚像素细化 =====
                // 使用模糊前的灰度图做梯度计算（梯度计算自带平滑，不需要额外模糊）
                List<Point2f[]> refinedGroups;
                if (useSubPixel && gray != null && !gray.Empty())
                {
                    refinedGroups = CvOp.RefineContoursToSubPixel(gray, approximatedGroups, apertureSize);
                    PluginLogger.Info($"[ShapeMatchTemplateEditor] 亚像素轮廓细化完成", "ShapeMatchTool");
                }
                else
                {
                    refinedGroups = approximatedGroups
                        .Select(g => g.Select(p => new Point2f(p.X, p.Y)).ToArray())
                        .ToList();
                }

                // ===== 转换为 Point2D + NaN 分隔 + 原图坐标 =====
                var contourPoints = new List<Point2D>();
                var allSubPixelRoi = new List<Point2f>(); // 用于整体矩/包围盒
                int contourGroupCount = 0;

                for (int gi = 0; gi < refinedGroups.Count; gi++)
                {
                    var group = refinedGroups[gi];

                    // NaN 分隔符（第一条轮廓前不需要）
                    if (contourPoints.Count > 0)
                    {
                        contourPoints.Add(new Point2D { X = double.NaN, Y = double.NaN });
                    }

                    foreach (var pt in group)
                    {
                        contourPoints.Add(new Point2D { X = pt.X, Y = pt.Y });
                        allSubPixelRoi.Add(new Point2f(pt.X, pt.Y));
                    }

                    contourGroupCount++;
                }

                // 计算整体包围盒和中心点（原图坐标）
                var moments = Cv2.Moments(allSubPixelRoi.ToArray());
                double overallCx = moments.M10 / moments.M00;
                double overallCy = moments.M01 / moments.M00;

                // 包围盒：用 MinAreaRect 支持浮点坐标
                var minAreaRect = Cv2.MinAreaRect(allSubPixelRoi.ToArray());
                double contourWidth = useSubPixel ? minAreaRect.Size.Width : Math.Round(minAreaRect.Size.Width);
                double contourHeight = useSubPixel ? minAreaRect.Size.Height : Math.Round(minAreaRect.Size.Height);

                // 有效点数（不含NaN分隔符）
                int validPointCount = contourPoints.Count(p => !double.IsNaN(p.X));

                // 更新显示
                if (saveResult)
                {
                    _resultTemplate ??= new TemplateModel();
                    _resultTemplate.ContourPoints = contourPoints;
                    _resultTemplate.SmoothKernelSize = smoothKernel;
                    _resultTemplate.EdgeLowThreshold = lowThreshold;
                    _resultTemplate.EdgeHighThreshold = highThreshold;
                    _resultTemplate.EdgeApertureSize = apertureSize;
                    _resultTemplate.ApproxMethod = approxMethod;
                    _resultTemplate.ApproxPrecision = approxPrecision;
                    _resultTemplate.TargetPointCount = targetPointCount;
                    _resultTemplate.ExtractionStatus = TemplateExtractionStatus.Success;
                    _resultTemplate.ContourWidth = minAreaRect.Size.Width;
                    _resultTemplate.ContourHeight = minAreaRect.Size.Height;
                    _resultTemplate.CenterX = overallCx;
                    _resultTemplate.CenterY = overallCy;
                    _resultTemplate.ContourGroupCount = contourGroupCount;
                    _resultTemplate.MinChainLength = minChainLength;
                    _resultTemplate.MinArea = minArea;
                    _resultTemplate.RetrievalMode = retrievalMode;
                    _resultTemplate.UseL2Gradient = useL2Gradient;
                    _resultTemplate.UseSubPixel = useSubPixel;

                    // 保存原始图像（PNG无损），仅当是新创建或覆盖时保存
                    if (_currentMat != null && !_currentMat.Empty())
                    {
                        Cv2.ImEncode(".png", _currentMat, out byte[] pngBytes);
                        _resultTemplate.SourceImageBytes = pngBytes;
                    }

                    UpdateResultDisplay(TemplateExtractionStatus.Success, validPointCount,
                        minAreaRect.Size.Width, minAreaRect.Size.Height,
                        overallCx, overallCy);

                    PluginLogger.Info($"[ShapeMatchTemplateEditor] 提取成功 | 轮廓组={contourGroupCount} 点数={validPointCount} | 包围盒={minAreaRect.Size.Width:F0}x{minAreaRect.Size.Height:F0}", "ShapeMatchTool");
                }
                else
                {
                    // 预览模式：临时保存点用于绘制
                    _previewContourPoints = contourPoints;
                    UpdateResultDisplay(TemplateExtractionStatus.Success, validPointCount,
                        minAreaRect.Size.Width, minAreaRect.Size.Height, overallCx, overallCy);
                }

                // 在 ImageControl 上叠加绘制多轮廓组（以分组形式传入，避免重复NaN拆分）
                var groupsToDraw = saveResult && _resultTemplate != null
                    ? _resultTemplate.ContourPointGroups
                    : SplitContourGroups(contourPoints);
                DrawContourOverlays(groupsToDraw, chkShowPointsOnly.IsChecked ?? false);
            }
            catch (Exception ex)
            {
                UpdateResultDisplay(TemplateExtractionStatus.Failed, 0, 0, 0, 0, 0);
                PluginLogger.Error($"[ShapeMatchTemplateEditor] 轮廓提取失败: {ex.Message}", "ShapeMatchTool");
            }
        }

        /// <summary>
        /// 构建 ROI 掩码：多个检测区域并集 - 多个屏蔽区域差集
        /// 使用 RegionMaskBuilder 统一处理所有形状类型
        /// </summary>
        private static Mat BuildRoiMask(OpenCvSharp.Size imageSize,
            List<RegionData> inspectionRegions, List<RegionData> maskRegions)
        {
            // 检测区域并集：白色 = 有效区域
            using var inspMask = RegionMaskBuilder.CreateMask(imageSize, inspectionRegions);

            if (maskRegions.Count == 0)
                return inspMask.Clone();

            // 屏蔽区域：白色 = 排除区域
            using var maskMask = RegionMaskBuilder.CreateMaskMask(imageSize, maskRegions);

            // 差集：inspMask AND NOT maskMask
            using var invMask = new Mat();
            Cv2.BitwiseNot(maskMask, invMask);
            var result = new Mat();
            Cv2.BitwiseAnd(inspMask, invMask, result);
            return result;
        }

        private void DrawContourOverlays(List<List<Point2D>> contourGroups, bool showPointsOnly = false)
        {
            if (dialogImageControl == null || contourGroups.Count == 0) return;

            ClearContourOverlay();

            var display = new DisplayCollector();
            var style = new GraphicsStyle
            {
                Name = showPointsOnly ? "ContourPoint" : "Contour",
                OkColor = Colors.LimeGreen,
                LineWidth = 3.0
            };

            if (showPointsOnly)
            {
                var allPoints = contourGroups
                    .SelectMany(g => g)
                    .Select(p => new Point2d(p.X, p.Y))
                    .ToList();
                display.AddPoints(style, allPoints);
            }
            else
            {
                var groups = contourGroups
                    .Select(g => g.Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y)).ToArray())
                    .ToList();
                display.AddContours(style, groups);
            }

            dialogImageControl.ResultRenderLayerControl.SetDisplayCollector(display);
        }

        /// <summary>
        /// 将平铺的轮廓点列表（含NaN分隔符）分割为按组分组的列表
        /// </summary>
        private static List<List<Point2D>> SplitContourGroups(List<Point2D>? flatPoints)
        {
            if (flatPoints == null || flatPoints.Count == 0)
                return new List<List<Point2D>>();

            var groups = new List<List<Point2D>>();
            var current = new List<Point2D>();
            foreach (var pt in flatPoints)
            {
                if (double.IsNaN(pt.X))
                {
                    if (current.Count > 0)
                    {
                        groups.Add(current);
                        current = new List<Point2D>();
                    }
                }
                else
                {
                    current.Add(new Point2D { X = pt.X, Y = pt.Y });
                }
            }
            if (current.Count > 0)
                groups.Add(current);
            return groups;
        }

        private void ClearContourOverlay()
        {
            if (dialogImageControl == null) return;
            // 只清结果层，不影响交互层的区域矩形
            dialogImageControl.ResultRenderLayerControl.Clear();
        }

        private void UpdateResultDisplay(TemplateExtractionStatus status, int pointCount,
            double width, double height, double cx, double cy)
        {
            if (txtResultStatus != null)
            {
                txtResultStatus.Text = status switch
                {
                    TemplateExtractionStatus.Success => "✓ 成功",
                    TemplateExtractionStatus.Warning => "⚠ 警告",
                    TemplateExtractionStatus.Failed => "✗ 失败",
                    TemplateExtractionStatus.NotExtracted => "--",
                    _ => status.ToString()
                };
                txtResultStatus.Foreground = status switch
                {
                    TemplateExtractionStatus.Success => new SolidColorBrush(Color.FromRgb(0x22, 0x8B, 0x22)),
                    TemplateExtractionStatus.Warning => new SolidColorBrush(Color.FromRgb(0xF5, 0xA6, 0x23)),
                    TemplateExtractionStatus.Failed => new SolidColorBrush(Color.FromRgb(0xD3, 0x2F, 0x2F)),
                    _ => new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99))
                };
            }

            if (txtPointCount != null)
                txtPointCount.Text = pointCount > 0 ? pointCount.ToString() : "--";

            if (txtBoundingRect != null)
                txtBoundingRect.Text = (width > 0 && height > 0) ? $"{width:F0} x {height:F0}" : "--";

            if (txtCenterPoint != null)
                txtCenterPoint.Text = (cx > 0 || cy > 0) ? $"({cx:F1}, {cy:F1})" : "--";
        }

        #endregion

        #region 对话框按钮

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            // ===== 第1步：校验名称（单项校验，聚焦到输入框） =====
            var name = templateNameTextBox?.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请输入模版名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                templateNameTextBox?.Focus();
                return;
            }

            _resultTemplate ??= new TemplateModel();
            _resultTemplate.Name = name;

            // ===== 第2步：同步区域数据（始终执行） =====
            _resultTemplate.InspectionRegions.Clear();
            foreach (var insp in DialogInspectionRegions)
                _resultTemplate.InspectionRegions.Add(insp);

            _resultTemplate.MaskRegions.Clear();
            foreach (var mask in DialogMaskRegions)
                _resultTemplate.MaskRegions.Add(mask);

            // ===== 第3步：重新提取轮廓（确保与当前检测区域匹配） =====
            if (DialogInspectionRegions.Count > 0 && _currentMat != null && !_currentMat.Empty())
            {
                ExecuteContourExtraction(saveResult: true);
                PluginLogger.Info("[ShapeMatchTemplateEditor] 确定时重新提取轮廓，区域数: {RegionCount}", "ShapeMatchTool");
            }

            // ===== 第4步：保存注册图像（如果还没有） =====
            if (_resultTemplate.SourceImageBytes == null && _currentMat != null && !_currentMat.Empty())
            {
                Cv2.ImEncode(".png", _currentMat, out byte[] pngBytes);
                _resultTemplate.SourceImageBytes = pngBytes;
            }

            // ===== 第5步：四项必填项统一验证（单次弹窗） =====
            var errors = new List<string>();

            if (DialogInspectionRegions.Count == 0)
                errors.Add("- 请至少绘制一个检测区域。");

            bool hasContour = _resultTemplate.ContourPoints != null && _resultTemplate.ContourPoints.Count > 0;
            if (!hasContour)
                errors.Add("- 轮廓数据为空，请先执行轮廓提取。");

            if (_resultTemplate.SourceImageBytes == null || _resultTemplate.SourceImageBytes.Length == 0)
                errors.Add("- 注册图像数据为空，请确保已加载图像。");

            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errors), "验证失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // ===== 全部通过 =====
            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            _resultTemplate = null;
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 获取对话框操作后的模板结果（仅当 DialogResult=True 时有效）
        /// </summary>
        public TemplateModel? GetResultTemplate() => _resultTemplate;

        #endregion

        #region 清理资源

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (dialogRegionManager != null)
            {
                dialogRegionManager.ClearMainOverlay();
            }

            ClearContourOverlay();
            if (_ownsMat)
            {
                _currentMat?.Dispose();
                _currentMat = null;
            }
        }

        #endregion
    }
}
