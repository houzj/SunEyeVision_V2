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
        private bool _isEditingExisting; // 编辑已有模板（含注册图像）
        private bool _hadManualExtraction; // 用户是否在对话框中手动点击了提取/预览
        private string? _localImagePath; // 本地图像的完整路径

        // 调试用中间图像
        private Mat? _debugMag;
        private Mat? _debugNms;

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

            // 金字塔起止层校验 — 实时弹窗提醒
            numPyramidStart.ValueChanged += OnPyramidLevelValueChanged;
            numPyramidEnd.ValueChanged += OnPyramidLevelValueChanged;


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
        /// 如果已有保存的边缘点数据，恢复显示叠加层
        /// </summary>
        private void TryRestoreContourOverlay()
        {
            if (_resultTemplate?.EdgePoints == null || _resultTemplate.EdgePoints.Count == 0)
                return;

            DrawEdgePointsOverlay(_resultTemplate.EdgePoints);
            PluginLogger.Info($"[ShapeMatchTemplateEditor] 恢复已保存的边缘点: {_resultTemplate.EdgePointCount}点",
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

            // 金字塔模式 — 默认自动
            if (cmbPyramidMode != null)
            {
                cmbPyramidMode.Items.Add("手动");
                cmbPyramidMode.Items.Add("自动");
                cmbPyramidMode.SelectionChanged += OnPyramidModeChanged;
                cmbPyramidMode.SelectedIndex = 1;
            }

            // 对比度模式 — 默认自动
            if (cmbContrastMode != null)
            {
                cmbContrastMode.Items.Add("手动");
                cmbContrastMode.Items.Add("自动");
                cmbContrastMode.SelectionChanged += OnContrastModeChanged;
                cmbContrastMode.SelectedIndex = 1;
            }

            // 链长模式 — 默认自动
            if (cmbChainLengthMode != null)
            {
                cmbChainLengthMode.Items.Add("手动");
                cmbChainLengthMode.Items.Add("自动");
                cmbChainLengthMode.SelectionChanged += OnChainLengthModeChanged;
                cmbChainLengthMode.SelectedIndex = 1;
            }

            PluginLogger.Info("[ShapeMatchTemplateEditor] 对话框初始化完成", "ShapeMatchTool");
        }

        /// <summary>
        /// 对比度模式变更 → 同步启用/禁用对比度 NumericUpDown
        /// </summary>
        private void OnContrastModeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (numContrast != null)
                numContrast.IsEnabled = cmbContrastMode?.SelectedIndex == 0;
        }

        /// <summary>
        /// 链长模式变更 → 同步启用/禁用最小链长 NumericUpDown
        /// </summary>
        private void OnChainLengthModeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (numMinChainLength != null)
                numMinChainLength.IsEnabled = cmbChainLengthMode?.SelectedIndex == 0;
        }

        /// <summary>
        /// 金字塔模式变更 → 同步启用/禁用起始层/结束层 NumericUpDown
        /// </summary>
        private void OnPyramidModeChanged(object sender, SelectionChangedEventArgs e)
        {
            bool isManual = cmbPyramidMode?.SelectedIndex == 0;
            if (numPyramidStart != null)
                numPyramidStart.IsEnabled = isManual;
            if (numPyramidEnd != null)
                numPyramidEnd.IsEnabled = isManual;
        }

        /// <summary>
        /// 金字塔起止层值变更校验 — 起始层(粗)不能小于结束层(细)
        /// </summary>
        private void OnPyramidLevelValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (numPyramidStart.IntValue < numPyramidEnd.IntValue)
            {
                MessageBox.Show($"起始层(粗L{numPyramidStart.IntValue})不能小于结束层(细L{numPyramidEnd.IntValue})，请重新设置。",
                    "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);

                // 自动将起始层修正为等于结束层
                numPyramidStart.IntValue = numPyramidEnd.IntValue;
            }
        }

        /// <summary>
        /// 加载已有模板数据进行编辑模式
        /// 直接引用原始对象，取消编辑时 OnCancelClick 会设置 DialogResult=false 保护数据
        /// </summary>
        public void LoadExistingTemplate(TemplateModel template)
        {
            if (template == null) return;

            _resultTemplate = template; // 直接引用，不做深拷贝
            _isEditingExisting = true;

            // Bind UI to data
            if (templateNameTextBox != null)
                templateNameTextBox.Text = _resultTemplate.Name;

            numPyramidStart.IntValue = _resultTemplate.PyramidStart;
            numPyramidEnd.IntValue = _resultTemplate.PyramidEnd;
            numContrast.IntValue = (int)_resultTemplate.Contrast;
            numMinChainLength.IntValue = _resultTemplate.MinChainLength;
            cmbChainLengthMode.SelectedIndex = _resultTemplate.IsChainAuto ? 1 : 0;

            // 加载区域数据
            foreach (var insp in _resultTemplate.InspectionRegions)
                DialogInspectionRegions.Add(insp);

            foreach (var mask in _resultTemplate.MaskRegions)
                DialogMaskRegions.Add(mask);

            // 恢复提取结果状态
            int edgePointCount = _resultTemplate.EdgePointCount;
            UpdateResultDisplay(_resultTemplate.ExtractionStatus, edgePointCount, 0, 0, 0, 0);

            PluginLogger.Info($"[ShapeMatchTemplateEditor] 已加载模板: {template.Name}, 点数={edgePointCount}", "ShapeMatchTool");
        }

        private void OnExtractClick(object sender, RoutedEventArgs e)
        {
            _hadManualExtraction = true;
            ExecuteContourExtraction();
            if (_resultTemplate?.EdgePoints != null && _resultTemplate.EdgePoints.Count > 0)
            {
                PreComputePyramidEdgePoints();
                PopulatePyramidLevelComboBox();
            }
        }



        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            if (_resultTemplate != null)
            {
                _resultTemplate.EdgePoints = null;
                _resultTemplate.PyramidEdgePoints = null;
                _resultTemplate.PyramidLevelSizes = null;
                _resultTemplate.ExtractionStatus = TemplateExtractionStatus.NotExtracted;
                _resultTemplate.SourceImageBytes = null;
            }

            cmbPyramidLevel.Items.Clear();
            cmbPyramidLevel.IsEnabled = false;
            ClearContourOverlay();
            UpdateResultDisplay(TemplateExtractionStatus.NotExtracted, 0, 0, 0, 0, 0);

            PluginLogger.Info("[ShapeMatchTemplateEditor] 已清除边缘点", "ShapeMatchTool");
        }

        private void ExecuteContourExtraction()
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
                // ===== 读取 UI 参数 =====
                bool pyramidAuto = cmbPyramidMode?.SelectedIndex == 1;
                int pyramidStart = numPyramidStart.IntValue, pyramidEnd = numPyramidEnd.IntValue;
                bool contrastAuto = cmbContrastMode?.SelectedIndex == 1;
                int contrastValue = numContrast.IntValue;
                bool chainAuto = cmbChainLengthMode?.SelectedIndex == 1;
                int chainLengthValue = numMinChainLength.IntValue;


                // Step 1: 灰度转换
                using var gray = new Mat();
                int channels = _currentMat.Channels();
                if (channels == 1) _currentMat.CopyTo(gray);
                else if (channels == 3) Cv2.CvtColor(_currentMat, gray, ColorConversionCodes.BGR2GRAY);
                else if (channels == 4) Cv2.CvtColor(_currentMat, gray, ColorConversionCodes.BGRA2GRAY);
                else Cv2.ExtractChannel(_currentMat, gray, 0);

                // Step 2: Scharr 梯度计算
                CvOp.ComputeShapeGradient(gray, out var gx, out var gy, out var mag);

                // Step 3: 构建 ROI 掩码
                var enabledInspections = DialogInspectionRegions.Where(r => r.IsEnabled).ToList();
                var enabledMasks = DialogMaskRegions.Where(r => r.IsEnabled).ToList();
                if (enabledInspections.Count == 0)
                {
                    MessageBox.Show("没有启用的检测区域。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                using var roiMask = BuildRoiMask(mag.Size(), enabledInspections, enabledMasks);

                // Step 4: 对比度（自动/手动）
                float actualContrast;
                if (contrastAuto)
                {
                    // 自动模式：Triangle Threshold 自适应阈值计算
                    actualContrast = CvOp.AutoComputeContrast(mag, roiMask);
                    PluginLogger.Info($"[ShapeMatchTemplateEditor] 自动对比度: {actualContrast:F1}", "ShapeMatchTool");
                }
                else
                {
                    actualContrast = contrastValue;
                }

                // Step 5: NMS 细线化
                using var nms = CvOp.ApplyNms(mag, gx, gy, actualContrast);

                // Step 6: ROI 掩码应用
                using var maskedNms = new Mat(nms.Size(), nms.Type(), Scalar.All(0));
                nms.CopyTo(maskedNms, roiMask);

                // 保存调试用中间图像
                _debugMag?.Dispose();
                _debugNms?.Dispose();
                _debugMag = mag.Clone();
                _debugNms = maskedNms.Clone();
                cmbDebugView.IsEnabled = true;
                cmbDebugView.SelectedIndex = 0;

                // Step 7: 收集边缘点（自动/手动链长）
                var edgePoints = CvOp.CollectEdgePoints(maskedNms, gx, gy,
                    autoMinChainLength: chainAuto,
                    manualMinChainLength: chainAuto ? 0 : chainLengthValue,
                    out int actualChainLength,
                    closingRadius: 1);

                if (edgePoints.Count == 0)
                {
                    gx.Dispose(); gy.Dispose(); mag.Dispose();
                    UpdateResultDisplay(TemplateExtractionStatus.Failed, 0, 0, 0, 0, 0);
                    PluginLogger.Warning("[ShapeMatchTemplateEditor] 未检测到边缘点（Contrast 过高或图像过平）", "ShapeMatchTool");
                    return;
                }

                // ★ 金字塔自动层数计算（基于边缘密度 + 图像尺寸）
                if (pyramidAuto)
                {
                    CvOp.AutoComputePyramidLevels(_currentMat.Width, _currentMat.Height,
                        edgePoints.Count, out pyramidStart, out pyramidEnd);
                }

                // Step 8: 验证金字塔范围
                if (pyramidStart < pyramidEnd)
                {
                    MessageBox.Show($"起始层(粗L{pyramidStart})不能小于结束层(细L{pyramidEnd})，请重新设置。",
                        "参数错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Step 9: 保存到模板
                _resultTemplate ??= new TemplateModel();
                _resultTemplate.EdgePoints = edgePoints;
                _resultTemplate.SetReferencePoint();
                PluginLogger.Info(
                    $"[ShapeMatchTemplateEditor] 参考点已设置: ({_resultTemplate.ReferencePointX:F4},{_resultTemplate.ReferencePointY:F4}), " +
                    $"基于 {edgePoints.Count} 个边缘点的质心",
                    "ShapeMatchTool");
                _resultTemplate.PyramidStart = pyramidStart;
                _resultTemplate.PyramidEnd = pyramidEnd;
                _resultTemplate.Contrast = actualContrast;
                _resultTemplate.NumLevels = pyramidStart - pyramidEnd + 1;
                _resultTemplate.MinChainLength = chainAuto ? actualChainLength : chainLengthValue;
                _resultTemplate.IsChainAuto = chainAuto;

                _resultTemplate.ExtractionStatus = TemplateExtractionStatus.Success;
                if (_currentMat != null && !_currentMat.Empty())
                {
                    Cv2.ImEncode(".png", _currentMat, out byte[] pngBytes);
                    _resultTemplate.SourceImageBytes = pngBytes;
                }

                UpdateResultDisplay(TemplateExtractionStatus.Success, edgePoints.Count, 0, 0, 0, 0);
                if (txtFilterStats != null)
                {
                    txtFilterStats.Text = $"边缘点: {edgePoints.Count} (Contrast={actualContrast:F1}, 链长={actualChainLength}, 金字塔=L{pyramidStart}~L{pyramidEnd})";
                }

                // ★ 自动模式 → 将计算出的值回写到 NumericUpDown（控件 disabled 但显示值让用户可见）
                if (contrastAuto)
                    numContrast.IntValue = (int)Math.Round(actualContrast);
                if (chainAuto)
                    numMinChainLength.IntValue = actualChainLength;
                if (pyramidAuto)
                {
                    numPyramidStart.IntValue = pyramidStart;
                    numPyramidEnd.IntValue = pyramidEnd;
                }

                DrawEdgePointsOverlay(edgePoints);
                PluginLogger.Info(
                    $"[ShapeMatchTemplateEditor] 提取成功 | " +
                    $"Scharr+NMS 边缘点={edgePoints.Count} " +
                    $"(Contrast={actualContrast:F1}, 金字塔=L{pyramidStart}~L{pyramidEnd})",
                    "ShapeMatchTool");

                gx.Dispose(); gy.Dispose(); mag.Dispose();
            }
            catch (Exception ex)
            {
                UpdateResultDisplay(TemplateExtractionStatus.Failed, 0, 0, 0, 0, 0);
                PluginLogger.Error($"[ShapeMatchTemplateEditor] 边缘提取失败: {ex.Message}", "ShapeMatchTool");
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

        private void DrawEdgePointsOverlay(List<CvOp.EdgePoint> edgePoints)
        {
            if (dialogImageControl == null || edgePoints.Count == 0) return;
            ClearContourOverlay();
            var display = new DisplayCollector();
            var style = new GraphicsStyle
            {
                Name = "EdgePoint",
                OkColor = Colors.LimeGreen,
                LineWidth = 1.5
            };
            var points = edgePoints.Select(p => new System.Windows.Point(p.X, p.Y)).ToList();
            display.AddPoints(style, points);
            dialogImageControl.ResultRenderLayerControl.SetDisplayCollector(display);
        }

        private void DrawEdgePointsOverlay(List<CvOp.EdgePoint> edgePoints, Color color)
        {
            if (dialogImageControl == null || edgePoints.Count == 0) return;
            ClearContourOverlay();
            var display = new DisplayCollector();
            var style = new GraphicsStyle { Name = "EdgePoint", OkColor = color, LineWidth = 1.5 };
            var points = edgePoints.Select(p => new System.Windows.Point(p.X, p.Y)).ToList();
            display.AddPoints(style, points);
            dialogImageControl.ResultRenderLayerControl.SetDisplayCollector(display);
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

            // ===== 第3步：校验轮廓数据（不再自动重提取） =====
            if (_resultTemplate.EdgePoints == null || _resultTemplate.EdgePoints.Count == 0)
            {
                if (DialogInspectionRegions.Count > 0)
                {
                    MessageBox.Show("边缘点数据为空，请先点击「提取轮廓」按钮生成。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
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

            bool hasContour = _resultTemplate.EdgePoints != null && _resultTemplate.EdgePoints.Count > 0;
            if (!hasContour)
                errors.Add("- 边缘点数据为空，请先执行轮廓提取。");

            if (_resultTemplate.SourceImageBytes == null || _resultTemplate.SourceImageBytes.Length == 0)
                errors.Add("- 注册图像数据为空，请确保已加载图像。");

            if (_resultTemplate.PyramidEdgePoints == null || _resultTemplate.PyramidEdgePoints.Count == 0)
                errors.Add("- 金字塔预计算数据为空，请重新提取。");

            if (_resultTemplate.PyramidLevelSizes == null || _resultTemplate.PyramidLevelSizes.Count == 0)
                errors.Add("- 金字塔尺寸数据为空，请重新提取。");

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
            _debugMag?.Dispose();
            _debugNms?.Dispose();
            if (_ownsMat)
            {
                _currentMat?.Dispose();
                _currentMat = null;
            }
        }

        #endregion

        #region 金字塔层级预览

        /// <summary>
        /// ComboBox 选择变更 → 切换预览到指定层级
        /// </summary>
        private void OnPyramidLevelSelected(object sender, SelectionChangedEventArgs e)
        {
            if (cmbPyramidLevel.SelectedItem is KeyValuePair<int, string> selected)
            {
                DrawSingleLevelEdgePoints(selected.Key);
                UpdatePyramidLevelInfo(selected.Key);
            }
        }

        private void DrawSingleLevelEdgePoints(int level)
        {
            if (_resultTemplate?.PyramidEdgePoints == null) return;
            if (!_resultTemplate.PyramidEdgePoints.TryGetValue(level, out var edgePoints)) return;

            // 所有层级已预归一化到统一参考点，显示时恢复为全分辨率绝对坐标
            double refX = _resultTemplate.ReferencePointX;
            double refY = _resultTemplate.ReferencePointY;
            // 使用实际存储的金字塔尺寸比，消除位运算估算误差
            double scaleX, scaleY;
            if (_resultTemplate.PyramidLevelSizes?.TryGetValue(level, out var levelSize) == true && levelSize.Width > 0)
            {
                scaleX = (double)_currentMat!.Width / levelSize.Width;
                scaleY = (double)_currentMat!.Height / levelSize.Height;
            }
            else
            {
                // 回退（Legacy 模板或旧数据）：使用位运算估算
                int estW = _currentMat?.Width >> level ?? 1;
                int estH = _currentMat?.Height >> level ?? 1;
                scaleX = (double)_currentMat!.Width / estW;
                scaleY = (double)_currentMat!.Height / estH;
            }
            var displayPoints = edgePoints.Select(p => new CvOp.EdgePoint
            {
                X = p.X * scaleX + refX,
                Y = p.Y * scaleY + refY,
                Direction = p.Direction,
                Magnitude = p.Magnitude
            }).ToList();
            DrawEdgePointsOverlay(displayPoints, Colors.LimeGreen);
        }

        private void PopulatePyramidLevelComboBox()
        {
            cmbPyramidLevel.Items.Clear();
            if (_resultTemplate?.PyramidEdgePoints == null)
            {
                cmbPyramidLevel.IsEnabled = false;
                return;
            }

            foreach (int level in _resultTemplate.PyramidEdgePoints.Keys
                         .Where(k => k <= _resultTemplate.PyramidStart && k >= _resultTemplate.PyramidEnd)
                         .OrderBy(k => k))
            {
                int pointCount = _resultTemplate.PyramidEdgePoints[level]?.Count ?? 0;
                string reduction = level == 1 ? "全分辨率" : (level >= 8 ? "极低分辨率" : $"1/{1 << (level - 1)}");
                string label = $"Level {level} ({reduction}) - {pointCount}pt";
                cmbPyramidLevel.Items.Add(new KeyValuePair<int, string>(level, label));
            }

            cmbPyramidLevel.DisplayMemberPath = "Value";
            cmbPyramidLevel.SelectedValuePath = "Key";
            cmbPyramidLevel.IsEnabled = true;

            cmbPyramidLevel.SelectedIndex = 0;
        }

        /// <summary>
        /// 预计算各金字塔层边缘点 — 每层独立提取，参数从 L1 等比缩放
        /// 每层独立运行 PyrDown → Scharr → NMS → CollectEdgePoints → 归一化
        /// 对比度/链长按 1/2^(level-1) 等比例适配
        /// </summary>
        private void PreComputePyramidEdgePoints()
        {
            if (_currentMat == null || _currentMat.Empty()) return;
            if (_resultTemplate?.EdgePoints == null || _resultTemplate.EdgePoints.Count == 0) return;

            int pyramidStart = _resultTemplate.PyramidStart;
            int pyramidEnd = _resultTemplate.PyramidEnd;

            if (pyramidStart < pyramidEnd)
                throw new InvalidOperationException(
                    $"模板金字塔范围无效: 起始层(粗L{pyramidStart}) < 结束层(细L{pyramidEnd})");

            using var gray = new Mat();
            if (_currentMat.Channels() == 1) _currentMat.CopyTo(gray);
            else if (_currentMat.Channels() == 3) Cv2.CvtColor(_currentMat, gray, ColorConversionCodes.BGR2GRAY);
            else Cv2.ExtractChannel(_currentMat, gray, 0);

            // L1 基准参数
            _resultTemplate.SetReferencePoint();
            double refX = _resultTemplate.ReferencePointX;
            double refY = _resultTemplate.ReferencePointY;
            float baseContrast = _resultTemplate.Contrast;
            int baseChainLen = _resultTemplate.MinChainLength;

            // L1 ROI 掩码（各层降采样后应用）
            var enabledInspections = DialogInspectionRegions.Where(r => r.IsEnabled).ToList();
            var enabledMasks = DialogMaskRegions.Where(r => r.IsEnabled).ToList();
            using var level1Mask = enabledInspections.Count > 0
                ? BuildRoiMask(gray.Size(), enabledInspections, enabledMasks)
                : null;

            var pyramidPoints = new Dictionary<int, List<CvOp.EdgePoint>>();
            _resultTemplate.PyramidLevelSizes = new Dictionary<int, LevelSizeInfo>();

            // Level 1: 全分辨率，归一化到参考点
            pyramidPoints[1] = _resultTemplate.EdgePoints
                .Select(p => new CvOp.EdgePoint
                {
                    X = p.X - refX, Y = p.Y - refY,
                    Direction = p.Direction, Magnitude = p.Magnitude
                }).ToList();

            _resultTemplate.PyramidLevelSizes[1] = new LevelSizeInfo
            {
                Width = gray.Width, Height = gray.Height
            };

            using var currentImage = gray.Clone();

            // Level 2+: 逐层 PyrDown + 独立提取，参数等比缩放
            for (int level = 2; level <= pyramidStart; level++)
            {
                int newW = currentImage.Width / 2;
                int newH = currentImage.Height / 2;
                if (newW < 16 || newH < 16) break;

                using var downsampled = new Mat();
                Cv2.PyrDown(currentImage, downsampled, new OpenCvSharp.Size(newW, newH));

                _resultTemplate.PyramidLevelSizes![level] = new LevelSizeInfo
                {
                    Width = downsampled.Width, Height = downsampled.Height
                };

                if (level < pyramidEnd || level > pyramidStart)
                {
                    downsampled.CopyTo(currentImage);
                    continue;
                }

                // ★ 对比度跨层不变（梯度已逐层归一化），链长等比缩放
                float adaptedContrast = baseContrast;
                int adaptedChainLen = Math.Max(baseChainLen / (1 << (level - 1)), 1);

                // 独立梯度计算
                CvOp.ComputeShapeGradient(downsampled, out var gx, out var gy, out var mag);
                using var nms = CvOp.ApplyNms(mag, gx, gy, adaptedContrast);

                // ROI 掩码降采样后应用
                using var maskedNms = level1Mask != null
                    ? ApplyMaskToLevel(nms, level1Mask, downsampled.Size())
                    : nms.Clone();

                // 固定使用适配后的手动链长（不再 per-level 自动计算）
                var levelPoints = CvOp.CollectEdgePoints(maskedNms, gx, gy,
                    autoMinChainLength: false,
                    manualMinChainLength: adaptedChainLen,
                    out int actualChainLength,
                    closingRadius: 1);

                // 归一化到参考点
                double scaleX = (double)downsampled.Width / gray.Width;
                double scaleY = (double)downsampled.Height / gray.Height;
                double levelRefX = refX * scaleX;
                double levelRefY = refY * scaleY;

                for (int i = 0; i < levelPoints.Count; i++)
                {
                    var p = levelPoints[i];
                    levelPoints[i] = new CvOp.EdgePoint
                    {
                        X = p.X - levelRefX, Y = p.Y - levelRefY,
                        Direction = p.Direction, Magnitude = p.Magnitude
                    };
                }

                // 不截断，全部保留
                pyramidPoints[level] = levelPoints;

                gx.Dispose(); gy.Dispose(); mag.Dispose();
                downsampled.CopyTo(currentImage);
            }

            _resultTemplate.PyramidEdgePoints = pyramidPoints;

            // 日志
            int maxLvl = pyramidPoints.Keys.Max();
            var logs = new List<string>
            {
                $"[ShapeMatchTemplateEditor] 金字塔预计算完成(参数适配): {pyramidPoints.Count}层 (1~{maxLvl})",
                $"  L1 基准: Contrast={baseContrast:F1}, ChainLen={baseChainLen}, 参考点=({refX:F4},{refY:F4})"
            };
            foreach (int lvl in _resultTemplate.PyramidLevelSizes.Keys.OrderBy(k => k))
            {
                var sz = _resultTemplate.PyramidLevelSizes[lvl];
                int ptCount = pyramidPoints.TryGetValue(lvl, out var pts) ? pts.Count : 0;
                logs.Add($"  L{lvl}: {sz.Width}x{sz.Height}, {ptCount}pt" +
                         (lvl >= pyramidEnd && lvl <= pyramidStart && lvl >= 2
                            ? $"  [Contrast={baseContrast:F1}(不变), ChainLen={Math.Max(baseChainLen / (1 << (lvl - 1)), 1)}]"
                            : ""));
            }
            PluginLogger.Info(string.Join("\n", logs), "ShapeMatchTool");
        }

        /// <summary>
        /// 将 L1 ROI 掩码降采样到目标层级分辨率后应用到 NMS 图
        /// </summary>
        private static Mat ApplyMaskToLevel(Mat nms, Mat level1Mask, OpenCvSharp.Size targetSize)
        {
            using var resizedMask = new Mat();
            Cv2.Resize(level1Mask, resizedMask, targetSize, 0, 0, InterpolationFlags.Nearest);
            var masked = new Mat(nms.Size(), nms.Type(), Scalar.All(0));
            nms.CopyTo(masked, resizedMask);
            return masked;
        }

        private void UpdatePyramidLevelInfo(int level)
        {
            if (_resultTemplate?.PyramidEdgePoints == null) return;

            bool hasLevel = _resultTemplate.PyramidEdgePoints.TryGetValue(level, out var edgePoints);
            int levelCount = edgePoints?.Count ?? 0;
            int originalCount = _resultTemplate.PyramidEdgePoints.TryGetValue(1, out var l1)
                ? l1.Count : 0;

            if (txtCurrentLevel != null)
                txtCurrentLevel.Text = hasLevel
                    ? level == 1
                        ? $"Level {level} (全分辨率)  {levelCount} / {originalCount}pt"
                        : $"Level {level} (1/{1 << (level - 1)})  {levelCount} / {originalCount}pt"
                    : "--";

            if (txtLevelAdvice != null)
            {
                string advice = levelCount switch
                {
                    < 5  => $"\u26a0 该层仅{levelCount}点，几乎无法匹配。请降低层级上限或增大Contrast",
                    < 20 => $"\u26a0 该层仅{levelCount}点（原始{originalCount}点），适合粗定位",
                    < 50 => $"\u2139 该层{levelCount}点（原始{originalCount}点），轮廓偏少",
                    _   => $"\u2713 该层{levelCount}点，轮廓充足"
                };
                txtLevelAdvice.Text = advice;
                txtLevelAdvice.Visibility = Visibility.Visible;
            }
        }

        #endregion

        #region 调试视图

        /// <summary>
        /// 调试视图切换：原始图像 / 对比度图 / NMS 边缘图
        /// </summary>
        private void OnDebugViewChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbDebugView.SelectedItem is not ComboBoxItem item) return;
            string tag = item.Tag?.ToString() ?? "original";

            try
            {
                switch (tag)
                {
                    case "original":
                        if (_currentMat != null && !_currentMat.Empty())
                            dialogImageControl?.LoadImageDownscaled(_currentMat);
                        break;

                    case "mag":
                        if (_debugMag != null && !_debugMag.Empty())
                        {
                            using var displayable = ConvertFloatMatToDisplayable(_debugMag);
                            dialogImageControl?.LoadImageDownscaled(displayable);
                        }
                        break;

                    case "nms":
                        if (_debugNms != null && !_debugNms.Empty())
                        {
                            using var displayable = ConvertFloatMatToDisplayable(_debugNms);
                            dialogImageControl?.LoadImageDownscaled(displayable);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Warning($"[ShapeMatchTemplateEditor] 切换调试视图失败: {ex.Message}", "ShapeMatchTool");
            }
        }

        /// <summary>
        /// 将 CV_32F 梯度图归一化转换为 CV_8U 可显示图像
        /// </summary>
        private static Mat ConvertFloatMatToDisplayable(Mat floatMat)
        {
            using var normalized = new Mat();
            Cv2.Normalize(floatMat, normalized, 0, 255, NormTypes.MinMax);
            var result = new Mat();
            normalized.ConvertTo(result, MatType.CV_8UC1);
            return result;
        }

        #endregion
    }
}
