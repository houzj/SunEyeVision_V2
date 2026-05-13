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

            // 金字塔模式
            if (cmbPyramidMode != null)
            {
                cmbPyramidMode.Items.Add("自动 (Auto)");
                cmbPyramidMode.Items.Add("手动 (Manual)");
                cmbPyramidMode.SelectedIndex = 0;
            }

            // 密度模式
            if (cmbDensityMode != null)
            {
                cmbDensityMode.Items.Add("自动 (Auto)");
                cmbDensityMode.Items.Add("手动 (Manual)");
                cmbDensityMode.SelectedIndex = 0;
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
            // 金字塔模式：Auto → 禁用起始/结束层，Manual → 启用
            bool pyramidManual = cmbPyramidMode?.SelectedIndex == 1;
            if (numPyramidStart != null) numPyramidStart.IsEnabled = pyramidManual;
            if (numPyramidEnd != null) numPyramidEnd.IsEnabled = pyramidManual;

            // 密度模式：Auto → 禁用密度系数，Manual → 启用
            bool densityManual = cmbDensityMode?.SelectedIndex == 1;
            if (numDensityFactor != null) numDensityFactor.IsEnabled = densityManual;

            // 阈值模式：Auto → 禁用对比度，Manual → 启用
            bool thresholdManual = cmbThresholdMode?.SelectedIndex == 1;
            if (numContrast != null) numContrast.IsEnabled = thresholdManual;

            // 链长模式：Auto → 禁用最小链长，Manual → 启用
            bool chainManual = cmbChainLengthMode?.SelectedIndex == 1;
            if (numMinChainLength != null) numMinChainLength.IsEnabled = chainManual;
        }

        private void OnPyramidModeChanged(object sender, SelectionChangedEventArgs e)
        {
            bool pyramidManual = cmbPyramidMode.SelectedIndex == 1;
            numPyramidStart.IsEnabled = pyramidManual;
            numPyramidEnd.IsEnabled = pyramidManual;
        }

        private void OnDensityModeChanged(object sender, SelectionChangedEventArgs e)
        {
            numDensityFactor.IsEnabled = cmbDensityMode.SelectedIndex == 1;
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

            cmbPyramidMode.SelectedIndex = (int)_resultTemplate.PyramidMode;
            numPyramidStart.IntValue = _resultTemplate.PyramidStart;
            numPyramidEnd.IntValue = _resultTemplate.PyramidEnd;
            cmbDensityMode.SelectedIndex = (int)_resultTemplate.DensityMode;
            numDensityFactor.Value = _resultTemplate.DensityFactor;
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
                var pyramidMode = (PyramidMode)(cmbPyramidMode?.SelectedIndex ?? 0);
                var densityMode = (DensityMode)(cmbDensityMode?.SelectedIndex ?? 0);
                var thresholdMode = (ThresholdMode)(cmbThresholdMode?.SelectedIndex ?? 0);
                var chainLengthMode = (ChainLengthMode)(cmbChainLengthMode?.SelectedIndex ?? 0);
                int pyramidStart = numPyramidStart.IntValue;
                int pyramidEnd = numPyramidEnd.IntValue;
                float densityFactor = (float)numDensityFactor.Value;
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

                // Step 9: 确定金字塔范围（Auto → 根据图像尺寸自动计算）
                if (pyramidMode == PyramidMode.Auto)
                {
                    int minDim = Math.Min(gray.Width, gray.Height);
                    if (minDim <= 128)
                    {
                        pyramidEnd = 1;
                    }
                    else
                    {
                        pyramidEnd = Math.Max(1, Math.Min(10, (int)Math.Round(Math.Log(Math.Max(minDim / 64.0, 1), 2))));
                    }
                    pyramidStart = Math.Max(1, pyramidEnd - 3);
                    numPyramidStart.IntValue = pyramidStart;
                    numPyramidEnd.IntValue = pyramidEnd;
                }

                // Step 10: 验证金字塔范围
                if (pyramidStart > pyramidEnd)
                {
                    PluginLogger.Warning($"[ShapeMatchTemplateEditor] 起始层({pyramidStart}) > 结束层({pyramidEnd})，已自动修正", "ShapeMatchTool");
                    pyramidEnd = pyramidStart;
                }

                // Step 11: 保存到模板
                _resultTemplate ??= new TemplateModel();
                _resultTemplate.EdgePoints = edgePoints;
                _resultTemplate.SetReferencePoint();
                PluginLogger.Info(
                    $"[ShapeMatchTemplateEditor] 参考点已设置: ({_resultTemplate.ReferencePointX:F4},{_resultTemplate.ReferencePointY:F4}), " +
                    $"基于 {edgePoints.Count} 个边缘点的质心",
                    "ShapeMatchTool");
                _resultTemplate.PyramidMode = pyramidMode;
                _resultTemplate.PyramidStart = pyramidStart;
                _resultTemplate.PyramidEnd = pyramidEnd;
                _resultTemplate.DensityMode = densityMode;
                _resultTemplate.DensityFactor = densityFactor;
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
                    string pyramidInfo = pyramidMode == PyramidMode.Auto ? $"金字塔: auto L{pyramidStart}~L{pyramidEnd}" : $"金字塔: L{pyramidStart}~L{pyramidEnd}";
                    string contrastInfo = thresholdMode == ThresholdMode.Auto ? $"Contrast={actualContrast}(自动)" : $"Contrast={actualContrast}";
                    txtFilterStats.Text = $"边缘点: {edgePoints.Count} ({chainInfo}, {pyramidInfo}, {contrastInfo})";
                }

                DrawEdgePointsOverlay(edgePoints);
                PluginLogger.Info(
                    $"[ShapeMatchTemplateEditor] 提取成功 | " +
                    $"Scharr+NMS 边缘点={edgePoints.Count} " +
                    $"(Contrast={actualContrast}, 金字塔=L{pyramidStart}~L{pyramidEnd}, 链长={chainInfo})",
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

        /// <summary>
        /// 预计算各金字塔层边缘点 — L0 单源混合模式
        /// L0 一次 NMS+CollectEdgePoints，各层通过坐标投影 + 方向从该层梯度重读 + 散布抽样
        /// </summary>
        private void PreComputePyramidEdgePoints()
        {
            if (_currentMat == null || _currentMat.Empty()) return;
            if (_resultTemplate?.EdgePoints == null || _resultTemplate.EdgePoints.Count == 0) return;

            // 获取金字塔范围（已在 ExecuteContourExtraction 中计算）
            int pyramidStart = _resultTemplate.PyramidStart; // 起始层（最细）
            int pyramidEnd = _resultTemplate.PyramidEnd;     // 结束层（最粗）

            if (pyramidStart > pyramidEnd)
                throw new InvalidOperationException($"起始层({pyramidStart}) > 结束层({pyramidEnd}) — 金字塔范围无效");

            using var gray = new Mat();
            if (_currentMat.Channels() == 1) _currentMat.CopyTo(gray);
            else if (_currentMat.Channels() == 3) Cv2.CvtColor(_currentMat, gray, ColorConversionCodes.BGR2GRAY);
            else Cv2.ExtractChannel(_currentMat, gray, 0);

            // ★ 获取统一参考点（Level 0 质心，所有层级归一化到同一个原点）
            _resultTemplate.SetReferencePoint();
            double refX = _resultTemplate.ReferencePointX;
            double refY = _resultTemplate.ReferencePointY;

            var pyramidPoints = new Dictionary<int, List<CvOp.EdgePoint>>();
            _resultTemplate.PyramidLevelSizes = new Dictionary<int, LevelSizeInfo>();

            // Level 0: 直接用已有结果，归一化到参考点
            pyramidPoints[0] = _resultTemplate.EdgePoints
                .Select(p => new CvOp.EdgePoint
                {
                    X = p.X - refX,
                    Y = p.Y - refY,
                    Direction = p.Direction,
                    Magnitude = p.Magnitude
                }).ToList();

            _resultTemplate.PyramidLevelSizes[0] = new LevelSizeInfo
            {
                Width = gray.Width,
                Height = gray.Height
            };

            using var currentImage = gray.Clone();

            // 计算密度比例
            float densityRatio = ComputeDensityRatio(
                _resultTemplate.DensityMode, _resultTemplate.DensityFactor,
                _resultTemplate.EdgePoints.Count);

            // 逐层 PyrDown + 梯度 → 投影 L0 点 + 重读方向
            for (int level = 1; level <= pyramidEnd; level++)
            {
                int newW = gray.Width / (1 << level);
                int newH = gray.Height / (1 << level);
                if (newW < 32 || newH < 32) break;

                using var downsampled = new Mat();
                Cv2.PyrDown(currentImage, downsampled, new OpenCvSharp.Size(newW, newH));

                // 记录实际 PyrDown 输出尺寸
                _resultTemplate.PyramidLevelSizes![level] = new LevelSizeInfo
                {
                    Width = downsampled.Width,
                    Height = downsampled.Height
                };

                // 不在目标范围内则继续缩放下一层
                if (level < pyramidStart || level > pyramidEnd)
                {
                    downsampled.CopyTo(currentImage);
                    continue;
                }

                // 计算该层梯度
                CvOp.ComputeShapeGradient(downsampled, out var gx, out var gy, out var mag);

                // 坐标缩放因子：L0 全分辨率 → 当前层
                double scaleX = (double)downsampled.Width / gray.Width;
                double scaleY = (double)downsampled.Height / gray.Height;
                double levelRefX = refX * scaleX;
                double levelRefY = refY * scaleY;

                var levelPoints = new List<CvOp.EdgePoint>();
                for (int i = 0; i < pyramidPoints[0].Count; i++)
                {
                    var p = pyramidPoints[0][i];

                    // 将 L0 绝对坐标映射到当前层
                    double absX = p.X + refX;
                    double absY = p.Y + refY;
                    int lx = (int)Math.Round(absX * scaleX);
                    int ly = (int)Math.Round(absY * scaleY);

                    // 边界检查
                    if (lx < 0 || lx >= downsampled.Width || ly < 0 || ly >= downsampled.Height)
                        continue;

                    // 从该层梯度重读方向 + 幅值
                    float dir = (float)Math.Atan2(gy.At<float>(ly, lx), gx.At<float>(ly, lx));
                    float magVal = mag.At<float>(ly, lx);

                    // 幅值过滤：低于 1.0 视为噪声
                    if (magVal <= 1.0f) continue;

                    levelPoints.Add(new CvOp.EdgePoint
                    {
                        X = (float)(lx - levelRefX),
                        Y = (float)(ly - levelRefY),
                        Direction = dir,
                        Magnitude = magVal
                    });
                }

                // 应用密度散布抽样
                if (densityRatio < 1.0f && levelPoints.Count > 0)
                {
                    int targetCount = Math.Max(10, (int)(levelPoints.Count * densityRatio));
                    if (levelPoints.Count > targetCount)
                    {
                        levelPoints = CvOp.SelectScatteredFeatures(levelPoints, targetCount, 3.0f);
                    }
                }

                pyramidPoints[level] = levelPoints;

                gx.Dispose(); gy.Dispose(); mag.Dispose();
                downsampled.CopyTo(currentImage);
            }

            _resultTemplate.PyramidEdgePoints = pyramidPoints;

            int maxLvl = pyramidPoints.Keys.Max();
            PluginLogger.Info(
                $"[ShapeMatchTemplateEditor] 金字塔预计算完成(L0单源): {pyramidPoints.Count}层 (0~{maxLvl}), " +
                $"密度比={densityRatio:F3}",
                "ShapeMatchTool");

            // ★ 坐标监控：输出金字塔各层尺寸和点数信息
            var sizeInfo = _resultTemplate.PyramidLevelSizes;
            var refLog = new List<string> { $"  参考点: ({refX:F4}, {refY:F4}) 在原图 {gray.Width}x{gray.Height}" };
            if (sizeInfo != null)
            {
                foreach (int lvl in sizeInfo.Keys.OrderBy(k => k))
                {
                    var sz = sizeInfo[lvl];
                    int ptCount = pyramidPoints.TryGetValue(lvl, out var pts) ? pts.Count : 0;
                    refLog.Add($"  L{lvl}: 实际尺寸={sz.Width}x{sz.Height}, 边缘点数={ptCount}");
                }
            }
            PluginLogger.Info(
                $"[ShapeMatchTemplateEditor] 金字塔坐标基准:\n" + string.Join("\n", refLog),
                "ShapeMatchTool");
        }

        /// <summary>
        /// 计算密度比例（各层散布抽样的保留比例）
        /// Auto模式根据L0点数自适应；Manual模式以Medium为基准×用户系数
        /// </summary>
        private static float ComputeDensityRatio(DensityMode mode, float densityFactor, int l0PointCount)
        {
            if (mode == DensityMode.Auto)
            {
                // Auto: 根据L0点数分级
                if (l0PointCount < 500) return 1.0f;   // 点少不精简
                if (l0PointCount < 2000) return 0.7f;  // Low
                if (l0PointCount < 8000) return 0.4f;  // Medium
                return 0.2f;                            // High
            }
            else
            {
                // Manual: Medium基准(0.4) × 用户系数
                float ratio = 0.4f * densityFactor;
                return Math.Clamp(ratio, 0.05f, 1.0f);
            }
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
