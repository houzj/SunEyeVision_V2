using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HalconDotNet;
using Microsoft.Win32;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Tool.ShapeMatch.Models;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using WpfWindow = System.Windows.Window;

namespace SunEyeVision.Tool.ShapeMatch.Views;

/// <summary>
/// 模板创建/编辑对话框 — 使用 Halcon 创建形状模板
/// </summary>
public partial class ShapeMatchTemplateEditorDialog : WpfWindow
{
    #region 字段

    private bool _ownsMat;
    private TemplateModel? _resultTemplate;
    private bool _isEditingExisting;
    private bool _hadManualExtraction;
    private string? _localImagePath;
    private HImage? _currentHImage;
    private bool _ownsHImage;

    // 外部传入的当前图像
    public Func<BitmapSource?>? GetCurrentImage { get; set; }

    // 对话框内部区域数据源
    public ObservableCollection<RegionData> DialogInspectionRegions { get; } = new();
    public ObservableCollection<RegionData> DialogMaskRegions { get; } = new();

    private const double DEG2RAD = Math.PI / 180.0;

    #endregion

    #region 构造函数

    public ShapeMatchTemplateEditorDialog()
    {
        InitializeComponent();
        InitializeComboBoxes();
        DataContext = this;

        numPyramidStart.ValueChanged += OnPyramidLevelValueChanged;
        numPyramidEnd.ValueChanged += OnPyramidLevelValueChanged;
    }

    public void LoadImageBeforeShow()
    {
        try
        {
            bool isEditing = _isEditingExisting;

            // 编辑模式：从注册图像解码
            if (isEditing && _resultTemplate?.SourceImageBytes is { Length: > 0 })
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = new System.IO.MemoryStream(_resultTemplate.SourceImageBytes);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    if (dialogImageControl != null)
                    {
                        dialogImageControl.LoadImage(bitmap);
                        BindRegionManagerToImage();
                        SelectImageSourceCombo("registered");
                    }

                    TryRestoreContourOverlay();
                    PluginLogger.Info("[ShapeMatchTemplateEditor] 从注册图像加载（编辑模式）", "ShapeMatchTool");
                    return;
                }
                catch (Exception ex)
                {
                    PluginLogger.Warning($"[ShapeMatchTemplateEditor] 注册图像解码失败: {ex.Message}", "ShapeMatchTool");
                }
            }

            // 非编辑模式：获取当前图像
            var bmp = GetCurrentImage?.Invoke();
            if (bmp != null && dialogImageControl != null)
            {
                dialogImageControl.LoadImage(bmp);
                UpdateCurrentHImage(bmp);
                BindRegionManagerToImage();
                SelectImageSourceCombo("current");
                PluginLogger.Info("[ShapeMatchTemplateEditor] 加载当前图像", "ShapeMatchTool");
            }
        }
        catch (Exception ex)
        {
            PluginLogger.Warning($"[ShapeMatchTemplateEditor] 加载图像失败: {ex.Message}", "ShapeMatchTool");
        }
    }

    private void TryRestoreContourOverlay()
    {
        if (_resultTemplate?.ContourChains == null || _resultTemplate.ContourChains.Count == 0)
            return;

        DrawContourOverlay(_resultTemplate.ContourChains,
            _resultTemplate.ReferencePointX, _resultTemplate.ReferencePointY);
        PluginLogger.Info($"[ShapeMatchTemplateEditor] 恢复已保存的轮廓: {_resultTemplate.EdgePointCount}点",
            "ShapeMatchTool");
    }

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

        // 角度步长模式 — 默认自动
        if (cmbAngleStepMode != null)
        {
            cmbAngleStepMode.Items.Add("手动");
            cmbAngleStepMode.Items.Add("自动");
            cmbAngleStepMode.SelectionChanged += (s, e) =>
            {
                numAngleStep.IsEnabled = cmbAngleStepMode?.SelectedIndex == 0;
            };
            cmbAngleStepMode.SelectedIndex = 1;
        }

        // 缩放步长模式 — 默认自动
        if (cmbScaleStepMode != null)
        {
            cmbScaleStepMode.Items.Add("手动");
            cmbScaleStepMode.Items.Add("自动");
            cmbScaleStepMode.SelectionChanged += (s, e) =>
            {
                numScaleStep.IsEnabled = cmbScaleStepMode?.SelectedIndex == 0;
            };
            cmbScaleStepMode.SelectedIndex = 1;
        }

        // 最小对比度模式 — 默认自动
        if (cmbMinContrastMode != null)
        {
            cmbMinContrastMode.Items.Add("手动");
            cmbMinContrastMode.Items.Add("自动");
            cmbMinContrastMode.SelectionChanged += (s, e) =>
            {
                numMinContrast.IsEnabled = cmbMinContrastMode?.SelectedIndex == 0;
            };
            cmbMinContrastMode.SelectedIndex = 1;
        }

        // 极性匹配
        if (cmbMetricMode != null)
        {
            cmbMetricMode.Items.Add("use_polarity");
            cmbMetricMode.Items.Add("ignore_global_polarity");
            cmbMetricMode.Items.Add("ignore_local_polarity");
            cmbMetricMode.SelectedIndex = 0;
        }

        // 优化模式
        if (cmbOptimizationMode != null)
        {
            cmbOptimizationMode.Items.Add("auto");
            cmbOptimizationMode.Items.Add("none");
            cmbOptimizationMode.Items.Add("point_reduction_low");
            cmbOptimizationMode.Items.Add("point_reduction_medium");
            cmbOptimizationMode.Items.Add("point_reduction_high");
            cmbOptimizationMode.SelectedIndex = 0;
        }

        PluginLogger.Info("[ShapeMatchTemplateEditor] 对话框初始化完成", "ShapeMatchTool");
    }

    private void OnContrastModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (numContrast != null)
            numContrast.IsEnabled = cmbContrastMode?.SelectedIndex == 0;
    }

    private void OnPyramidModeChanged(object sender, SelectionChangedEventArgs e)
    {
        bool isManual = cmbPyramidMode?.SelectedIndex == 0;
        if (numPyramidStart != null) numPyramidStart.IsEnabled = isManual;
        if (numPyramidEnd != null) numPyramidEnd.IsEnabled = isManual;
    }

    private void OnPyramidLevelValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (numPyramidStart.IntValue < numPyramidEnd.IntValue)
        {
            MessageBox.Show($"起始层(粗L{numPyramidStart.IntValue})不能小于结束层(细L{numPyramidEnd.IntValue})，请重新设置。",
                "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            numPyramidStart.IntValue = numPyramidEnd.IntValue;
        }
    }

    #endregion

    #region 加载已有模板

    public void LoadExistingTemplate(TemplateModel template)
    {
        if (template == null) return;

        _resultTemplate = template;
        _isEditingExisting = true;

        if (templateNameTextBox != null)
            templateNameTextBox.Text = _resultTemplate.Name;

        numPyramidStart.IntValue = _resultTemplate.PyramidStart;
        numPyramidEnd.IntValue = _resultTemplate.PyramidEnd;
        numContrast.IntValue = (int)_resultTemplate.Contrast;

        foreach (var insp in _resultTemplate.InspectionRegions)
            DialogInspectionRegions.Add(insp);
        foreach (var mask in _resultTemplate.MaskRegions)
            DialogMaskRegions.Add(mask);

        int edgePointCount = _resultTemplate.EdgePointCount;
        UpdateResultDisplay(_resultTemplate.ExtractionStatus, edgePointCount, 0, 0, 0, 0);

        PluginLogger.Info($"[ShapeMatchTemplateEditor] 已加载模板: {template.Name}, 点数={edgePointCount}", "ShapeMatchTool");
    }

    #endregion

    #region 提取按钮

    private void OnExtractClick(object sender, RoutedEventArgs e)
    {
        _hadManualExtraction = true;
        ExecuteContourExtraction();
        if (_resultTemplate?.ContourChains != null && _resultTemplate.ContourChains.Count > 0)
        {
            PopulatePyramidLevelComboBox();
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (_resultTemplate != null)
        {
            _resultTemplate.ModelData = null;
            _resultTemplate.ContourChains = null;
            _resultTemplate.LevelContours = null;
            _resultTemplate.ExtractionStatus = TemplateExtractionStatus.NotExtracted;
            _resultTemplate.SourceImageBytes = null;
        }

        cmbPyramidLevel.Items.Clear();
        cmbPyramidLevel.IsEnabled = false;
        ClearContourOverlay();
        UpdateResultDisplay(TemplateExtractionStatus.NotExtracted, 0, 0, 0, 0, 0);

        PluginLogger.Info("[ShapeMatchTemplateEditor] 已清除轮廓", "ShapeMatchTool");
    }

    #endregion

    #region Halcon 模板创建

    private void ExecuteContourExtraction()
    {
        if (_currentHImage == null || !_currentHImage.IsInitialized())
        {
            MessageBox.Show("请先加载图像后再提取轮廓。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
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
            int pyramidStart = numPyramidStart.IntValue;
            int pyramidEnd = numPyramidEnd.IntValue;
            bool contrastAuto = cmbContrastMode?.SelectedIndex == 1;
            int contrastValue = numContrast.IntValue;
            bool angleStepManual = cmbAngleStepMode?.SelectedIndex == 0;
            double angleStepDeg = numAngleStep.Value;
            bool scaleStepManual = cmbScaleStepMode?.SelectedIndex == 0;
            double scaleStepValue = numScaleStep.Value;
            bool minContrastManual = cmbMinContrastMode?.SelectedIndex == 0;
            double minContrastValue = numMinContrast.Value;

            // ===== 灰度转换 =====
            HImage grayImage;
            int channels = _currentHImage!.CountChannels();
            if (channels > 1)
            {
                HOperatorSet.Rgb1ToGray(_currentHImage, out var grayObj);
                grayImage = new HImage(grayObj);
            }
            else
            {
                grayImage = _currentHImage.Clone();
            }

            using (grayImage)
            {
                // ===== 构建 ROI =====
                var enabledInspections = DialogInspectionRegions.Where(r => r.IsEnabled).ToList();
                var enabledMasks = DialogMaskRegions.Where(r => r.IsEnabled).ToList();
                if (enabledInspections.Count == 0)
                {
                    MessageBox.Show("没有启用的检测区域。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                HRegion? roiRegion = BuildRoiRegion(enabledInspections, enabledMasks);

                try
                {
                    // ===== ReduceDomain =====
                    HImage reducedImage;
                    if (roiRegion != null)
                    {
                        HOperatorSet.ReduceDomain(grayImage, roiRegion, out var reducedObj);
                        reducedImage = new HImage(reducedObj);
                    }
                    else
                    {
                        reducedImage = grayImage.Clone();
                    }

                    using (reducedImage)
                    {
                        // ===== 构建 Halcon 参数 =====
                        int numLevels = pyramidAuto ? 0 : pyramidStart;

                        double angleStart = -45.0;
                        double angleExtent = 90.0;

                        HTuple angleStep = angleStepManual
                            ? new HTuple(angleStepDeg * DEG2RAD)
                            : new HTuple("auto");

                        double scaleMin = 0.9;
                        double scaleMax = 1.1;
                        HTuple scaleStep = scaleStepManual
                            ? new HTuple(scaleStepValue)
                            : new HTuple("auto");

                        string optimization = GetSelectedOptimization();
                        string metric = GetSelectedMetric();

                        HTuple contrast = contrastAuto
                            ? new HTuple("auto")
                            : new HTuple(contrastValue);

                        HTuple minContrast = minContrastManual
                            ? new HTuple(minContrastValue)
                            : new HTuple("auto");

                        // ===== 调用 Halcon create_scaled_shape_model =====
                        HOperatorSet.CreateScaledShapeModel(
                            reducedImage, numLevels,
                            angleStart * DEG2RAD, angleExtent * DEG2RAD, angleStep,
                            scaleMin, scaleMax, scaleStep,
                            optimization, metric,
                            contrast, minContrast,
                            out HTuple modelID);

                        using (modelID)
                        {
                            // ===== 自匹配获取参考点在图像中的位置 =====
                            double refCol, refRow, refAngle, refScale;
                            try
                            {
                                HOperatorSet.FindScaledShapeModel(
                                    grayImage, modelID,
                                    angleStart * DEG2RAD, angleExtent * DEG2RAD,
                                    scaleMin, scaleMax,
                                    0.5,     // minScore
                                    1,       // numMatches
                                    0.5,     // maxOverlap
                                    "none",  // subPixel（仅定位不要求精度）
                                    0,       // numLevels（全层级）
                                    0.9,     // greediness
                                    out var matchRows, out var matchCols,
                                    out var matchAngles, out var matchScales,
                                    out var matchScores);

                                if (matchScores.Length > 0)
                                {
                                    refCol = matchCols[0].D;
                                    refRow = matchRows[0].D;
                                    refAngle = matchAngles[0].D;
                                    refScale = matchScales[0].D;
                                }
                                else
                                {
                                    // 回退到图像中心
                                    HOperatorSet.GetImageSize(grayImage,
                                        out var imgW, out var imgH);
                                    refCol = imgW.D / 2.0;
                                    refRow = imgH.D / 2.0;
                                    refAngle = 0;
                                    refScale = 1.0;
                                }
                            }
                            catch (Exception ex)
                            {
                                PluginLogger.Warning(
                                    $"[ShapeMatchTemplateEditor] 模板自匹配失败: {ex.Message}",
                                    "ShapeMatchTool");
                                HOperatorSet.GetImageSize(grayImage,
                                    out var imgW, out var imgH);
                                refCol = imgW.D / 2.0;
                                refRow = imgH.D / 2.0;
                                refAngle = 0;
                                refScale = 1.0;
                            }

                            // 获取原始模型坐标系轮廓（不做预变换）
                            var rawContours = TemplateModel.GetShapeModelContours(modelID, 1);

                            // 序列化模型
                            byte[] serializedData = SerializeModel(modelID);

                            // 保存到模板
                            _resultTemplate ??= new TemplateModel();
                            _resultTemplate.ModelData = serializedData;
                            _resultTemplate.ContourChains = rawContours;
                            _resultTemplate.ReferencePointX = refCol;
                            _resultTemplate.ReferencePointY = refRow;
                            _resultTemplate.Contrast = contrastAuto ? 0 : contrastValue;
                            _resultTemplate.ExtractionStatus = TemplateExtractionStatus.Success;

                            // 保存源图像
                            if (_currentHImage != null && _currentHImage.IsInitialized())
                            {
                                SaveSourceImageBytes(_currentHImage);
                            }

                            // 获取各层轮廓（原始模型坐标系）
                            _resultTemplate.LevelContours = new Dictionary<int, List<WpfPoint[]>>();
                            int actualMaxLevel = 0;
                            for (int lvl = 1; lvl <= 10; lvl++)
                            {
                                try
                                {
                                    var rawLevel = TemplateModel.GetShapeModelContours(
                                        modelID, lvl);
                                    _resultTemplate.LevelContours[lvl] = rawLevel;
                                    actualMaxLevel = lvl;
                                }
                                catch
                                {
                                    break;
                                }
                            }

                            // 设置金字塔层数
                            _resultTemplate.NumLevels = actualMaxLevel;
                            _resultTemplate.PyramidStart = pyramidAuto ? actualMaxLevel : pyramidStart;
                            _resultTemplate.PyramidEnd = Math.Min(pyramidEnd, actualMaxLevel);

                            int totalPoints = _resultTemplate.EdgePointCount;
                            UpdateResultDisplay(TemplateExtractionStatus.Success, totalPoints, 0, 0, 0, 0);

                            txtFilterStats.Text = $"轮廓点: {totalPoints} (金字塔={_resultTemplate.NumLevels}层, 对比度={contrastValue})";

                            // 创建时回写自动值
                            if (contrastAuto)
                                numContrast.IntValue = contrastValue;
                            if (pyramidAuto)
                            {
                                numPyramidStart.IntValue = _resultTemplate.PyramidStart;
                            }

                            DrawContourOverlay(rawContours, refCol, refRow);

                            PluginLogger.Info(
                                $"[ShapeMatchTemplateEditor] Halcon 模板创建成功 | " +
                                $"轮廓点={totalPoints}, 金字塔={_resultTemplate.NumLevels}层",
                                "ShapeMatchTool");
                        }
                    }
                }
                finally
                {
                    roiRegion?.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            UpdateResultDisplay(TemplateExtractionStatus.Failed, 0, 0, 0, 0, 0);
            PluginLogger.Error($"[ShapeMatchTemplateEditor] Halcon 模板创建失败: {ex.Message}", "ShapeMatchTool");
            MessageBox.Show($"模板创建失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 构建 ROI 区域（检测区域并集 - 屏蔽区域）
    /// </summary>
    private static HRegion? BuildRoiRegion(List<RegionData> inspections, List<RegionData> masks)
    {
        HRegion? result = null;

        // 检测区域并集
        foreach (var region in inspections.Where(r => r.IsEnabled))
        {
            var hReg = RegionDataToHRegion(region);
            if (hReg == null) continue;

            if (result == null)
                result = hReg;
            else
            {
                HOperatorSet.Union2(result, hReg, out var unionObj);
                result.Dispose();
                hReg.Dispose();
                result = new HRegion(unionObj);
            }
        }

        if (result == null) return null;

        // 屏蔽区域差集
        foreach (var mask in masks.Where(r => r.IsEnabled))
        {
            var hMask = RegionDataToHRegion(mask);
            if (hMask == null) continue;

            HOperatorSet.Difference(result, hMask, out var diffObj);
            result.Dispose();
            hMask.Dispose();
            result = new HRegion(diffObj);
        }

        return result;
    }

    private static HRegion? RegionDataToHRegion(RegionData region)
    {
        if (region.Parameters is not ShapeParameters shape) return null;

        return shape.ShapeType switch
        {
            ShapeType.Rectangle => new HRegion(
                shape.CenterY - shape.Height / 2,
                shape.CenterX - shape.Width / 2,
                shape.CenterY + shape.Height / 2,
                shape.CenterX + shape.Width / 2),

            ShapeType.Circle => new HRegion(
                shape.CenterY, shape.CenterX, shape.Radius),

            ShapeType.Polygon when shape.Points.Count >= 3
                => BuildPolygonHRegion(shape.Points),

            ShapeType.RotatedRectangle => new HRegion(
                shape.CenterY - shape.Height / 2,
                shape.CenterX - shape.Width / 2,
                shape.CenterY + shape.Height / 2,
                shape.CenterX + shape.Width / 2),

            _ => null
        };
    }

    private static HRegion BuildPolygonHRegion(IList<Point2D> points)
    {
        var rows = new HTuple();
        var cols = new HTuple();
        for (int i = 0; i < points.Count; i++)
        {
            rows = rows.TupleConcat(points[i].Y);
            cols = cols.TupleConcat(points[i].X);
        }
        var region = new HRegion();
        region.GenRegionPolygon(rows, cols);
        return region;
    }

    /// <inheritdoc cref="TemplateModel.GetShapeModelContours"/>

    /// <summary>
    /// 序列化 Halcon 形状模型到 byte[]
    /// </summary>
    /// <summary>
    /// 序列化 Halcon 形状模型到 byte[]（使用临时文件 + write_shape_model）
    /// </summary>
    private static byte[] SerializeModel(HTuple modelID)
    {
        string tempFile = System.IO.Path.GetTempFileName();
        try
        {
            HOperatorSet.WriteShapeModel(modelID, tempFile);
            return System.IO.File.ReadAllBytes(tempFile);
        }
        finally
        {
            if (System.IO.File.Exists(tempFile))
                System.IO.File.Delete(tempFile);
        }
    }

    private string GetSelectedMetric()
    {
        int idx = cmbMetricMode?.SelectedIndex ?? 0;
        return idx switch
        {
            1 => "ignore_global_polarity",
            2 => "ignore_local_polarity",
            _ => "use_polarity"
        };
    }

    private string GetSelectedOptimization()
    {
        int idx = cmbOptimizationMode?.SelectedIndex ?? 0;
        return idx switch
        {
            1 => "none",
            2 => "point_reduction_low",
            3 => "point_reduction_medium",
            4 => "point_reduction_high",
            _ => "auto"
        };
    }

    private void SaveSourceImageBytes(HImage hImage)
    {
        try
        {
            HOperatorSet.WriteImage(hImage, "png", 0, "temp_shape_match.png");
            var bytes = System.IO.File.ReadAllBytes("temp_shape_match.png");
            _resultTemplate!.SourceImageBytes = bytes;
            System.IO.File.Delete("temp_shape_match.png");
        }
        catch
        {
            PluginLogger.Warning("[ShapeMatchTemplateEditor] 无法保存源图像", "ShapeMatchTool");
        }
    }

    /// <summary>
    /// 更新 _currentHImage 从 BitmapSource（用于各图像加载路径）
    /// </summary>
    private void UpdateCurrentHImage(BitmapSource bmp)
    {
        if (_ownsHImage) _currentHImage?.Dispose();
        _ownsHImage = true;

        using var visionImage = VisionImage.FromBitmapSource(bmp);
        if (visionImage is HalImage halImage)
        {
            // Clone 获取 HImage 所有权，HalImage 的 Dispose 不影响克隆
            _currentHImage = halImage.Unwrap().Clone();
        }
        else
        {
            _currentHImage = null;
        }
    }

    /// <summary>
    /// 通过 VisionImage.FromBitmapSource 将 BitmapSource 转换为 HImage（用于 ExecuteContourExtraction 的回退路径）
    /// </summary>
    private bool TryLoadCurrentMat()
    {
        var bmp = GetCurrentImage?.Invoke();
        if (bmp == null) return false;

        try
        {
            if (_ownsHImage) _currentHImage?.Dispose();

            using var visionImage = VisionImage.FromBitmapSource(bmp);

            if (visionImage is HalImage halImage)
            {
                _currentHImage = halImage.Unwrap().Clone();
                _ownsHImage = true;
                return _currentHImage.IsInitialized();
            }

            throw new InvalidOperationException($"图像类型不匹配：期望 HalImage，实际为 {visionImage.GetType().Name}");
        }
        catch (Exception ex)
        {
            PluginLogger.Warning($"[ShapeMatchTemplateEditor] BitmapSource→HImage 转换失败: {ex.Message}", "ShapeMatchTool");
            return false;
        }
    }

    #endregion

    #region 轮廓渲染

    private void DrawContourOverlay(List<WpfPoint[]> contourChains,
        double originCol, double originRow,
        double angle = 0, double scale = 1.0,
        int level = 1)
    {
        if (dialogImageControl == null || contourChains.Count == 0) return;
        ClearContourOverlay();
        var display = new DisplayCollector();
        var style = new GraphicsStyle
        {
            Name = "TemplateContour",
            OkColor = Colors.LimeGreen,
            LineWidth = 1.5
        };

        // 构建仿射变换矩阵
        // 变换：P_image = scale * 2^(level-1) * R(angle) * P_model + (originCol, originRow)
        double levelScale = 1 << (level - 1);
        double combinedScale = scale * levelScale;
        double cosA = Math.Cos(angle);
        double sinA = Math.Sin(angle);
        var transformMatrix = new System.Windows.Media.Matrix(
            combinedScale * cosA,  combinedScale * sinA,
           -combinedScale * sinA,  combinedScale * cosA,
            originCol, originRow);

        foreach (var chain in contourChains)
        {
            if (chain.Length <= 1) continue;
            // 传入原始链点 + 变换矩阵，渲染器 PushTransform 完成变换
            display.AddContour(style, chain, transformMatrix, isPass: true, isClosed: false);
        }

        dialogImageControl.ResultRenderLayerControl.SetDisplayCollector(display);
    }

    private void ClearContourOverlay()
    {
        if (dialogImageControl == null) return;
        dialogImageControl.ResultRenderLayerControl.Clear();
    }

    #endregion

    #region 结果显示

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

        // 轮廓链数
        int chainCount = _resultTemplate?.ContourChains?.Count ?? 0;
        if (txtContourChainCount != null)
            txtContourChainCount.Text = chainCount > 0 ? chainCount.ToString() : "--";

        // 金字塔层数
        if (txtActualLevels != null && _resultTemplate != null && _resultTemplate.ExtractionStatus == TemplateExtractionStatus.Success)
            txtActualLevels.Text = $"{_resultTemplate.NumLevels} 层";

        // 实际对比度
        if (txtActualContrast != null && _resultTemplate != null && _resultTemplate.ExtractionStatus == TemplateExtractionStatus.Success)
            txtActualContrast.Text = $"{_resultTemplate.Contrast:F0}";
    }

    #endregion

    #region 图像源选择

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

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new System.IO.MemoryStream(bytes);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                // 强制统一为 Gray8，消除 Halcon PNG → Indexed8 格式分歧
                var converted = new FormatConvertedBitmap();
                converted.BeginInit();
                converted.Source = bitmap;
                converted.DestinationFormat = PixelFormats.Gray8;
                converted.EndInit();
                converted.Freeze();

                dialogImageControl?.LoadImage(converted);
                UpdateCurrentHImage(converted);
                BindRegionManagerToImage();
            }
            else if (source == "current")
            {
                var bmp = GetCurrentImage?.Invoke();
                if (bmp == null)
                {
                    MessageBox.Show("当前没有可用图像。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                dialogImageControl?.LoadImage(bmp);
                UpdateCurrentHImage(bmp);
                BindRegionManagerToImage();
            }
            else if (source == "local")
            {
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

                var bitmap2 = new BitmapImage(new Uri(_localImagePath));
                bitmap2.Freeze();
                dialogImageControl?.LoadImage(bitmap2);
                UpdateCurrentHImage(bitmap2);
                BindRegionManagerToImage();
            }

            PluginLogger.Info($"[ShapeMatchTemplateEditor] 已切换至{source}图像", "ShapeMatchTool");
        }
        catch (Exception ex)
        {
            PluginLogger.Error($"[ShapeMatchTemplateEditor] 加载{source}图像失败: {ex.Message}", "ShapeMatchTool");
        }
    }

    private void BindRegionManagerToImage()
    {
        dialogRegionManager?.SetMainImageControl(dialogImageControl);
    }

    #endregion

    #region 对话框按钮

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        var name = templateNameTextBox?.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("请输入模板名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            templateNameTextBox?.Focus();
            return;
        }

        _resultTemplate ??= new TemplateModel();
        _resultTemplate.Name = name;

        // 同步区域数据
        _resultTemplate.InspectionRegions.Clear();
        foreach (var insp in DialogInspectionRegions)
            _resultTemplate.InspectionRegions.Add(insp);

        _resultTemplate.MaskRegions.Clear();
        foreach (var mask in DialogMaskRegions)
            _resultTemplate.MaskRegions.Add(mask);

        // 校验轮廓数据
        if (_resultTemplate.ModelData == null || _resultTemplate.ModelData.Length == 0)
        {
            if (DialogInspectionRegions.Count > 0)
            {
                MessageBox.Show("模型数据为空，请先点击「提取轮廓」按钮生成。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        // 保存注册图像
        if (_resultTemplate.SourceImageBytes == null && _currentHImage?.IsInitialized() == true)
        {
            SaveSourceImageBytes(_currentHImage);
        }

        // 统一验证
        var errors = new List<string>();
        if (DialogInspectionRegions.Count == 0)
            errors.Add("- 请至少绘制一个检测区域。");

        if (_resultTemplate.ModelData == null || _resultTemplate.ModelData.Length == 0)
            errors.Add("- 模型数据为空，请先执行轮廓提取。");

        if (_resultTemplate.SourceImageBytes == null || _resultTemplate.SourceImageBytes.Length == 0)
            errors.Add("- 注册图像数据为空，请确保已加载图像。");

        if (errors.Count > 0)
        {
            MessageBox.Show(string.Join("\n", errors), "验证失败", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        _resultTemplate = null;
        DialogResult = false;
        Close();
    }

    public TemplateModel? GetResultTemplate() => _resultTemplate;

    #endregion

    #region 清理资源

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        dialogRegionManager?.ClearMainOverlay();
        ClearContourOverlay();
        if (_ownsHImage)
        {
            _currentHImage?.Dispose();
            _currentHImage = null;
        }
    }

    #endregion

    #region 金字塔层级预览

    private void OnPyramidLevelSelected(object sender, SelectionChangedEventArgs e)
    {
        if (cmbPyramidLevel.SelectedItem is KeyValuePair<int, string> selected)
        {
            DrawSingleLevelContour(selected.Key);
            UpdatePyramidLevelInfo(selected.Key);
        }
    }

    private void DrawSingleLevelContour(int level)
    {
        if (_resultTemplate?.LevelContours == null) return;
        if (!_resultTemplate.LevelContours.TryGetValue(level, out var contours)) return;

        DrawContourOverlay(contours,
            _resultTemplate.ReferencePointX, _resultTemplate.ReferencePointY,
            level: level);
    }

    private void PopulatePyramidLevelComboBox()
    {
        cmbPyramidLevel.Items.Clear();
        if (_resultTemplate?.LevelContours == null)
        {
            cmbPyramidLevel.IsEnabled = false;
            return;
        }

        foreach (int level in _resultTemplate.LevelContours.Keys
                     .Where(k => k <= _resultTemplate.PyramidStart && k >= _resultTemplate.PyramidEnd)
                     .OrderBy(k => k))
        {
            int ptCount = _resultTemplate.LevelContours[level]?.Sum(c => c.Length) ?? 0;
            string reduction = level == 1 ? "全分辨率" : $"1/{1 << (level - 1)}";
            string label = $"Level {level} ({reduction}) - {ptCount}pt";
            cmbPyramidLevel.Items.Add(new KeyValuePair<int, string>(level, label));
        }

        cmbPyramidLevel.DisplayMemberPath = "Value";
        cmbPyramidLevel.SelectedValuePath = "Key";
        cmbPyramidLevel.IsEnabled = true;
        cmbPyramidLevel.SelectedIndex = 0;
    }

    private void UpdatePyramidLevelInfo(int level)
    {
        if (_resultTemplate?.LevelContours == null) return;

        bool hasLevel = _resultTemplate.LevelContours.TryGetValue(level, out var contours);
        int levelCount = contours?.Sum(c => c.Length) ?? 0;
        int originalCount = _resultTemplate.EdgePointCount;

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
                < 5 => $"\u26a0 该层仅{levelCount}点，几乎无法匹配。请降低层级上限或增大Contrast",
                < 20 => $"\u26a0 该层仅{levelCount}点（原始{originalCount}点），适合粗定位",
                < 50 => $"\u2139 该层{levelCount}点（原始{originalCount}点），轮廓偏少",
                _ => $"\u2713 该层{levelCount}点，轮廓充足"
            };
            txtLevelAdvice.Text = advice;
            txtLevelAdvice.Visibility = Visibility.Visible;
        }
    }

    #endregion
}
