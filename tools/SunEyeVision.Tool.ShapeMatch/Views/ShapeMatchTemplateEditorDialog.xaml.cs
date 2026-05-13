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

            // 尺度模式
            if (cmbScaleMode != null)
            {
                cmbScaleMode.Items.Add("自动 (Auto)");
                cmbScaleMode.Items.Add("手动 (Manual)");
                cmbScaleMode.SelectedIndex = 0;
            }

            // 阈值模式
            if (cmbThresholdMode != null)
            {
                cmbThresholdMode.Items.Add("自动 (Auto)");
                cmbThresholdMode.Items.Add("手动 (Manual)");
                cmbThresholdMode.SelectedIndex = 0;
            }

            // 链长模式
            if (cmbChainLengthMode != null)
            {
                cmbChainLengthMode.Items.Add("自动 (Auto)");
                cmbChainLengthMode.Items.Add("手动 (Manual)");
                cmbChainLengthMode.SelectedIndex = 0;
            }

            // 初始化 Auto/Manual 参数启用状态
            UpdateParameterEnabledStates();

            PluginLogger.Info("[ShapeMatchTemplateEditor] 对话框初始化完成", "ShapeMatchTool");
        }

        /// <summary>
        /// 根据当前 ComboBox 选择的模式，启用/禁用对应的数值参数控件
        /// </summary>
        private void UpdateParameterEnabledStates()
        {
            // 尺度模式：Auto → 禁用速度/特征尺度，Manual → 启用
            bool scaleManual = cmbScaleMode?.SelectedIndex == 1;
            if (numSpeedScale != null) numSpeedScale.IsEnabled = scaleManual;
            if (numFeatureScale != null) numFeatureScale.IsEnabled = scaleManual;

            // 阈值模式：Auto → 禁用对比度，Manual → 启用
            bool thresholdManual = cmbThresholdMode?.SelectedIndex == 1;
            if (numContrast != null) numContrast.IsEnabled = thresholdManual;

            // 链长模式：Auto → 禁用最小链长，Manual → 启用
            bool chainManual = cmbChainLengthMode?.SelectedIndex == 1;
            if (numMinChainLength != null) numMinChainLength.IsEnabled = chainManual;
        }

        private void OnScaleModeChanged(object sender, SelectionChangedEventArgs e)
        {
            bool scaleManual = cmbScaleMode.SelectedIndex == 1;
            numSpeedScale.IsEnabled = scaleManual;
            numFeatureScale.IsEnabled = scaleManual;
        }

        private void OnThresholdModeChanged(object sender, SelectionChangedEventArgs e)
        {
            numContrast.IsEnabled = cmbThresholdMode.SelectedIndex == 1;
        }

        private void OnChainLengthModeChanged(object sender, SelectionChangedEventArgs e)
        {
            numMinChainLength.IsEnabled = cmbChainLengthMode.SelectedIndex == 1;
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

            cmbScaleMode.SelectedIndex = (int)_resultTemplate.ScaleMode;
            numSpeedScale.IntValue = _resultTemplate.SpeedScale;
            numFeatureScale.IntValue = _resultTemplate.FeatureScale;
            cmbThresholdMode.SelectedIndex = (int)_resultTemplate.ThresholdMode;
            numContrast.IntValue = (int)_resultTemplate.Contrast;
            cmbChainLengthMode.SelectedIndex = (int)_resultTemplate.ChainLengthMode;
            numMinChainLength.IntValue = _resultTemplate.MinChainLength;

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
                var scaleMode = (ScaleMode)(cmbScaleMode?.SelectedIndex ?? 0);
                var thresholdMode = (ThresholdMode)(cmbThresholdMode?.SelectedIndex ?? 0);
                var chainLengthMode = (ChainLengthMode)(cmbChainLengthMode?.SelectedIndex ?? 0);
                int speedScale = numSpeedScale.IntValue;
                int featureScale = numFeatureScale.IntValue;
                int minChainLengthValue = numMinChainLength.IntValue;

                // Step 1: 灰度转换
                using var gray = new Mat();
                int channels = _currentMat.Channels();
                if (channels == 1) _currentMat.CopyTo(gray);
                else if (channels == 3) Cv2.CvtColor(_currentMat, gray, ColorConversionCodes.BGR2GRAY);
                else if (channels == 4) Cv2.CvtColor(_currentMat, gray, ColorConversionCodes.BGRA2GRAY);
                else Cv2.ExtractChannel(_currentMat, gray, 0);

                // Step 2: Scharr 梯度计算（与运行时 100% 相同）
                CvOp.ComputeShapeGradient(gray, out var gx, out var gy, out var mag);

                // Step 3: 构建 ROI 掩码（提前构建，用于限定对比度计算范围）
                var enabledInspections = DialogInspectionRegions.Where(r => r.IsEnabled).ToList();
                var enabledMasks = DialogMaskRegions.Where(r => r.IsEnabled).ToList();
                if (enabledInspections.Count == 0)
                {
                    MessageBox.Show("没有启用的检测区域。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                using var roiMask = BuildRoiMask(mag.Size(), enabledInspections, enabledMasks);

                // Step 4: 在 ROI 范围内计算对比度（避免背景强边缘拉高阈值）
                float actualContrast = numContrast.IntValue;
                if (thresholdMode == ThresholdMode.Auto)
                {
                    using var maskedMag = new Mat();
                    mag.CopyTo(maskedMag, roiMask);
                    actualContrast = (float)Math.Round(CvOp.AutoComputeContrast(maskedMag));
                    numContrast.IntValue = (int)actualContrast;
                }

                // Step 5: NMS 细线化（用原始 mag 保持边界邻域判断正确）
                using var nms = CvOp.ApplyNms(mag, gx, gy, actualContrast);

                // Step 6: 将 ROI 掩码应用到 NMS 结果，过滤出 ROI 内边缘点
                // 新建全零矩阵，只在 mask 非零处拷贝 NMS 结果（保留 NMS 细线化，ROI 外清零）
                using var maskedNms = new Mat(nms.Size(), nms.Type(), Scalar.All(0));
                nms.CopyTo(maskedNms, roiMask);

                // 保存调试用中间图像（ROI 掩码后，确保与 EdgePoints 一致）
                _debugMag?.Dispose();
                _debugNms?.Dispose();
                _debugMag = mag.Clone();
                _debugNms = maskedNms.Clone();
                cmbDebugView.IsEnabled = true;
                cmbDebugView.SelectedIndex = 0; // 默认切回"原始"

                // Step 7: 确定链长模式
                bool autoChainLen = chainLengthMode == ChainLengthMode.Auto;

                // Step 8: 收集边缘点（已移除 optimization 参数）
                var edgePoints = CvOp.CollectEdgePoints(maskedNms, gx, gy, autoChainLen, minChainLengthValue, out int actualChainLength, closingRadius: 1);
                if (edgePoints.Count == 0)
                {
                    gx.Dispose(); gy.Dispose(); mag.Dispose();
                    UpdateResultDisplay(TemplateExtractionStatus.Failed, 0, 0, 0, 0, 0);
                    PluginLogger.Warning("[ShapeMatchTemplateEditor] 未检测到边缘点（Contrast 过高或图像过平）", "ShapeMatchTool");
                    return;
                }

                // Step 9: 确定实际尺度（Auto → 根据 ROI 尺寸自动计算）
                if (scaleMode == ScaleMode.Auto)
                {
                    // 使用图像尺寸估算金字塔层级
                    int minDim = Math.Min(gray.Width, gray.Height);
                    speedScale = Math.Max(0, Math.Min(9, (int)Math.Round(Math.Log(Math.Max(minDim / 64.0, 1), 2))));
                    featureScale = Math.Max(0, Math.Min(speedScale, speedScale - 3));
                    numSpeedScale.IntValue = speedScale;
                    numFeatureScale.IntValue = featureScale;
                }

                // Step 10: 验证尺度范围
                if (speedScale < featureScale)
                {
                    PluginLogger.Warning($"[ShapeMatchTemplateEditor] 速度尺度({speedScale}) < 特征尺度({featureScale})，已自动修正", "ShapeMatchTool");
                    featureScale = speedScale; // 修正为速度尺度
                }

                // Step 11: 保存到模板
                _resultTemplate ??= new TemplateModel();
                _resultTemplate.EdgePoints = edgePoints;
                _resultTemplate.SetReferencePoint();
                _resultTemplate.ScaleMode = scaleMode;
                _resultTemplate.SpeedScale = speedScale;
                _resultTemplate.FeatureScale = featureScale;
                _resultTemplate.ThresholdMode = thresholdMode;
                _resultTemplate.Contrast = actualContrast;
                _resultTemplate.ChainLengthMode = chainLengthMode;
                if (chainLengthMode == ChainLengthMode.Auto)
                {
                    _resultTemplate.MinChainLength = actualChainLength;
                    numMinChainLength.IntValue = actualChainLength;
                }
                else
                {
                    _resultTemplate.MinChainLength = minChainLengthValue;
                }
                _resultTemplate.ExtractionStatus = TemplateExtractionStatus.Success;
                if (_currentMat != null && !_currentMat.Empty())
                {
                    Cv2.ImEncode(".png", _currentMat, out byte[] pngBytes);
                    _resultTemplate.SourceImageBytes = pngBytes;
                }

                UpdateResultDisplay(TemplateExtractionStatus.Success, edgePoints.Count, 0, 0, 0, 0);
                string chainInfo = chainLengthMode == ChainLengthMode.Auto ? $"链长≥{actualChainLength}(自动)" : $"链长≥{minChainLengthValue}";
                if (txtFilterStats != null)
                {
                    string scaleInfo = scaleMode == ScaleMode.Auto ? $"尺度: auto={speedScale}→{featureScale}" : $"尺度: {speedScale}→{featureScale}";
                    string contrastInfo = thresholdMode == ThresholdMode.Auto ? $"Contrast={actualContrast}(自动)" : $"Contrast={actualContrast}";
                    txtFilterStats.Text = $"边缘点: {edgePoints.Count} ({chainInfo}, {scaleInfo}, {contrastInfo})";
                }

                DrawEdgePointsOverlay(edgePoints);
                PluginLogger.Info(
                    $"[ShapeMatchTemplateEditor] 提取成功 | " +
                    $"Scharr+NMS 边缘点={edgePoints.Count} " +
                    $"(Contrast={actualContrast}, 尺度={speedScale}→{featureScale}, 链长={chainInfo})",
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
            var points = edgePoints.Select(p => new Point2d(p.X, p.Y)).ToList();
            display.AddPoints(style, points);
            dialogImageControl.ResultRenderLayerControl.SetDisplayCollector(display);
        }

        private void DrawEdgePointsOverlay(List<CvOp.EdgePoint> edgePoints, Color color)
        {
            if (dialogImageControl == null || edgePoints.Count == 0) return;
            ClearContourOverlay();
            var display = new DisplayCollector();
            var style = new GraphicsStyle { Name = "EdgePoint", OkColor = color, LineWidth = 1.5 };
            var points = edgePoints.Select(p => new Point2d(p.X, p.Y)).ToList();
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

            // 非 Level 0 的坐标是从降采样图像提取的，需映射回全分辨率空间
            if (level > 0)
            {
                int scale = 1 << level; // 2^level
                var scaledPoints = edgePoints.Select(p => new CvOp.EdgePoint
                {
                    X = p.X * scale,
                    Y = p.Y * scale,
                    Direction = p.Direction,
                    Magnitude = p.Magnitude
                }).ToList();
                DrawEdgePointsOverlay(scaledPoints, Colors.LimeGreen);
            }
            else
            {
                DrawEdgePointsOverlay(edgePoints, Colors.LimeGreen);
            }
        }

        private void PopulatePyramidLevelComboBox()
        {
            cmbPyramidLevel.Items.Clear();
            if (_resultTemplate?.PyramidEdgePoints == null)
            {
                cmbPyramidLevel.IsEnabled = false;
                return;
            }

            foreach (int level in _resultTemplate.PyramidEdgePoints.Keys.OrderBy(k => k))
            {
                int pointCount = _resultTemplate.PyramidEdgePoints[level]?.Count ?? 0;
                string label = level == 0
                    ? $"Level 0 (全分辨率) - {pointCount}pt"
                    : $"Level {level} (1/{1 << level}) - {pointCount}pt";
                cmbPyramidLevel.Items.Add(new KeyValuePair<int, string>(level, label));
            }

            cmbPyramidLevel.DisplayMemberPath = "Value";
            cmbPyramidLevel.SelectedValuePath = "Key";
            cmbPyramidLevel.IsEnabled = true;

            int defaultLevel = _resultTemplate.PyramidEdgePoints.Keys
                .OrderByDescending(k => k)
                .FirstOrDefault(k => (_resultTemplate.PyramidEdgePoints[k]?.Count ?? 0) >= 5);
            cmbPyramidLevel.SelectedIndex = Math.Max(0, defaultLevel);
        }

        private void PreComputePyramidEdgePoints()
        {
            if (_currentMat == null || _currentMat.Empty()) return;
            if (_resultTemplate?.EdgePoints == null || _resultTemplate.EdgePoints.Count == 0) return;

            // 获取当前尺度设置
            bool isScaleManual = _resultTemplate.ScaleMode == ScaleMode.Manual;
            int speedScale = _resultTemplate.SpeedScale;
            int featureScale = _resultTemplate.FeatureScale;

            // Auto 模式下，preComputation 阶段使用 SpeedScale/FeatureScale（已在 ExecuteContourExtraction 中计算）
            if (speedScale < featureScale)
                throw new InvalidOperationException($"速度尺度({speedScale}) < 特征尺度({featureScale}) — 金字塔范围无效");

            using var gray = new Mat();
            if (_currentMat.Channels() == 1) _currentMat.CopyTo(gray);
            else if (_currentMat.Channels() == 3) Cv2.CvtColor(_currentMat, gray, ColorConversionCodes.BGR2GRAY);
            else Cv2.ExtractChannel(_currentMat, gray, 0);

            // 构建全分辨率 ROI 掩码（所有层级共享，逐级缩放）
            var enabledInspections = DialogInspectionRegions.Where(r => r.IsEnabled).ToList();
            var enabledMasks = DialogMaskRegions.Where(r => r.IsEnabled).ToList();
            using Mat? fullRoiMask = enabledInspections.Count > 0
                ? BuildRoiMask(gray.Size(), enabledInspections, enabledMasks)
                : null;

            var pyramidPoints = new Dictionary<int, List<CvOp.EdgePoint>>();

            // Level 0: 直接用已有结果
            pyramidPoints[0] = _resultTemplate.EdgePoints;

            using var currentImage = gray.Clone();

            // 阈值模式决定对比度值（在 ROI 范围内统计）
            float contrast;
            if (_resultTemplate.ThresholdMode == ThresholdMode.Auto)
            {
                using var initMag = AutoComputeMagAtLevel(currentImage);
                using var maskedMag = new Mat();
                if (fullRoiMask != null)
                    initMag.CopyTo(maskedMag, fullRoiMask);
                else
                    initMag.CopyTo(maskedMag);
                contrast = (float)Math.Round(CvOp.AutoComputeContrast(maskedMag));
            }
            else
            {
                contrast = _resultTemplate.Contrast;
            }
            bool autoChainLen = _resultTemplate.ChainLengthMode == ChainLengthMode.Auto;
            int manualChainLen = _resultTemplate.MinChainLength;

            // 修改了 endLevel 为 speedScale（原 PyramidStart）
            for (int level = 1; level <= speedScale; level++)
            {
                int newW = gray.Width / (1 << level);
                int newH = gray.Height / (1 << level);
                if (newW < 32 || newH < 32) break;

                using var downsampled = new Mat();
                Cv2.PyrDown(currentImage, downsampled, new OpenCvSharp.Size(newW, newH));

                CvOp.ComputeShapeGradient(downsampled, out var gx, out var gy, out var mag);

                // 所有层级统一使用 Level 0 的基准对比度（避免 PyDown 后动态分布不同导致标准不一致）
                using var nms = CvOp.ApplyNms(mag, gx, gy, contrast);

                // 将全分辨率 ROI 掩码缩放到当前层级尺寸并应用
                if (fullRoiMask != null)
                {
                    using var levelMask = new Mat();
                    Cv2.Resize(fullRoiMask, levelMask, downsampled.Size(), interpolation: InterpolationFlags.Nearest);
                    using var maskedNms = new Mat(nms.Size(), nms.Type(), Scalar.All(0));
                    nms.CopyTo(maskedNms, levelMask);

                    var levelPoints = CvOp.CollectEdgePoints(maskedNms, gx, gy, autoChainLen, manualChainLen, out _, closingRadius: 1);
                    if (levelPoints.Count > 0)
                        pyramidPoints[level] = levelPoints;
                }
                else
                {
                    var levelPoints = CvOp.CollectEdgePoints(nms, gx, gy, autoChainLen, manualChainLen, out _, closingRadius: 1);
                    if (levelPoints.Count > 0)
                        pyramidPoints[level] = levelPoints;
                }

                gx.Dispose(); gy.Dispose(); mag.Dispose();
                downsampled.CopyTo(currentImage);
            }

            _resultTemplate.PyramidEdgePoints = pyramidPoints;
            PluginLogger.Info($"[ShapeMatchTemplateEditor] 金字塔预计算完成: {pyramidPoints.Count}层 (0~{pyramidPoints.Keys.Max()})", "ShapeMatchTool");
        }

        /// <summary>
        /// 计算梯度幅值（用于金字塔预计算的初始 Auto 阈值）
        /// </summary>
        private static Mat AutoComputeMagAtLevel(Mat grayImage)
        {
            CvOp.ComputeShapeGradient(grayImage, out var gx, out var gy, out var mag);
            gx.Dispose(); gy.Dispose();
            return mag;
        }

        private void UpdatePyramidLevelInfo(int level)
        {
            if (_resultTemplate?.PyramidEdgePoints == null) return;

            bool hasLevel = _resultTemplate.PyramidEdgePoints.TryGetValue(level, out var edgePoints);
            int levelCount = edgePoints?.Count ?? 0;
            int originalCount = _resultTemplate.PyramidEdgePoints.TryGetValue(0, out var l0)
                ? l0.Count : 0;

            if (txtCurrentLevel != null)
                txtCurrentLevel.Text = hasLevel
                    ? $"Level {level} (1/{1 << level})  {levelCount} / {originalCount}pt"
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
