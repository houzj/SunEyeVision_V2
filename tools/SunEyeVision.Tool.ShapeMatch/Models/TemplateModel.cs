using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using WpfPoint = System.Windows.Point;

namespace SunEyeVision.Tool.ShapeMatch.Models;

public enum SortMode
{
    None = 0,
    ScoreDescending = 1,
    AngleDescending = 2,
    XAscending = 3,
    YAscending = 4,
    XThenYAscending = 5,
    YThenXAscending = 6
}

public enum MultiTemplateMode
{
    Sequential = 0,
    ParallelIndependent = 1,
    ParallelShared = 2
}

public enum TemplateExtractionStatus
{
    NotExtracted = 0,
    Success = 1,
    Warning = 2,
    Failed = 3
}

/// <summary>
/// 模板数据模型 — 存储序列化的 Halcon shape_model 数据
/// </summary>
[JsonDerivedType(typeof(TemplateModel), "TemplateModel")]
public class TemplateModel : ObservableObject
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = string.Empty;
    private bool _isEnabled = true;
    private int _orderIndex;

    // ============ Halcon 模型数据 ============
    private byte[]? _modelData;             // serialize_shape_model → byte[]
    private float _actualContrast = 30f;    // 创建时实际使用的对比度（auto 时由 Halcon/HUI 估算）
    private int _numLevels = 4;             // 金字塔层数
    private double _actualAngleStep;        // 实际角度步长（弧度，get_shape_model_params 回读）
    private double _actualScaleStep;        // 实际缩放步长（get_shape_model_params 回读）
    private float _actualMinContrast;       // 实际最小对比度（get_shape_model_params 回读）

    private TemplateExtractionStatus _extractionStatus = TemplateExtractionStatus.NotExtracted;
    private List<RoiData> _inspectionRegions = new();
    private List<RoiData> _maskRegions = new();
    private byte[]? _sourceImageBytes;        // raw pixel data (灰度值, 非 PNG)
    private int _sourceImageWidth;
    private int _sourceImageHeight;
    private double _referencePointX;        // 模板参考点 X（模板轮廓质心，用于编辑器还原）
    private double _referencePointY;        // 模板参考点 Y

    // 编辑器显示数据（由 get_shape_model_contours 构建）
    private List<WpfPoint[]>? _contourChains;      // Level 1 轮廓链
    private Dictionary<int, List<WpfPoint[]>>? _levelContours; // 各层轮廓

    public string Id { get => _id; set => SetProperty(ref _id, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value, "名称"); }
    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value, "启用"); }
    public int OrderIndex { get => _orderIndex; set => SetProperty(ref _orderIndex, value); }

    // ============ Halcon 模型数据 ============

    /// <summary>序列化的 shape_model 数据（由 serialize_shape_model 输出，Recipe 持久化）</summary>
    public byte[]? ModelData
    {
        get => _modelData;
        set => SetProperty(ref _modelData, value);
    }

    /// <summary>创建时实际使用的对比度（auto 时由 Halcon 估算或 UI 值回写）</summary>
    public float ActualContrast
    {
        get => _actualContrast;
        set => SetProperty(ref _actualContrast, value, "实际对比度");
    }

    /// <summary>金字塔层数（创建时实际生成的总层数）</summary>
    public int NumLevels
    {
        get => _numLevels;
        set => SetProperty(ref _numLevels, value, "金字塔层数");
    }

    /// <summary>实际角度步长（弧度，get_shape_model_params 回读）</summary>
    public double ActualAngleStep
    {
        get => _actualAngleStep;
        set => SetProperty(ref _actualAngleStep, value);
    }

    /// <summary>实际缩放步长（get_shape_model_params 回读）</summary>
    public double ActualScaleStep
    {
        get => _actualScaleStep;
        set => SetProperty(ref _actualScaleStep, value);
    }

    /// <summary>实际最小对比度（get_shape_model_params 回读）</summary>
    public float ActualMinContrast
    {
        get => _actualMinContrast;
        set => SetProperty(ref _actualMinContrast, value);
    }

    // ============ 编辑器 / 显示数据 ============

    /// <summary>Level 1 轮廓链（用于编辑器显示，get_shape_model_contours 输出）</summary>
    public List<WpfPoint[]>? ContourChains
    {
        get => _contourChains;
        set
        {
            SetProperty(ref _contourChains, value);
            OnPropertyChanged(nameof(EdgePointCount));
        }
    }

    /// <summary>各金字塔层轮廓（用于层级预览）</summary>
    public Dictionary<int, List<WpfPoint[]>>? LevelContours
    {
        get => _levelContours;
        set => SetProperty(ref _levelContours, value);
    }

    /// <summary>轮廓点总数</summary>
    [JsonIgnore]
    public int EdgePointCount => _contourChains?.Sum(c => c.Length) ?? 0;

    // ============ 区域数据 ============

    public List<RoiData> InspectionRegions
    {
        get => _inspectionRegions;
        set => SetProperty(ref _inspectionRegions, value);
    }

    public List<RoiData> MaskRegions
    {
        get => _maskRegions;
        set => SetProperty(ref _maskRegions, value);
    }

    public TemplateExtractionStatus ExtractionStatus
    {
        get => _extractionStatus;
        set => SetProperty(ref _extractionStatus, value, "提取状态");
    }

    /// <summary>创建时的原始图像数据（raw pixel bytes，IplImage 格式：width*height 的 byte 灰度值）</summary>
    public byte[]? SourceImageBytes
    {
        get => _sourceImageBytes;
        set => SetProperty(ref _sourceImageBytes, value);
    }

    /// <summary>原始图像宽度</summary>
    public int SourceImageWidth
    {
        get => _sourceImageWidth;
        set => SetProperty(ref _sourceImageWidth, value);
    }

    /// <summary>原始图像高度</summary>
    public int SourceImageHeight
    {
        get => _sourceImageHeight;
        set => SetProperty(ref _sourceImageHeight, value);
    }

    /// <summary>参考点 X（模板创建时 Halcon 参考点在图像中的列坐标，由 FindScaledShapeModel 自匹配结果赋值）</summary>
    public double ReferencePointX
    {
        get => _referencePointX;
        set => SetProperty(ref _referencePointX, value);
    }

    /// <summary>参考点 Y（模板创建时 Halcon 参考点在图像中的行坐标）</summary>
    public double ReferencePointY
    {
        get => _referencePointY;
        set => SetProperty(ref _referencePointY, value);
    }

    // ============ 共享 Halcon 工具方法 ============

    /// <summary>
    /// 获取形状模型轮廓（对齐 Halcon get_shape_model_contours）
    /// </summary>
    public static List<WpfPoint[]> GetShapeModelContours(HTuple modelID, int level)
    {
        HOperatorSet.GetShapeModelContours(out var xldContours, modelID, level);
        var result = new List<WpfPoint[]>();
        HOperatorSet.CountObj(xldContours, out var numObj);
        int count = numObj.I;

        for (int i = 1; i <= count; i++)
        {
            HOperatorSet.SelectObj(xldContours, out var singleObj, i);
            HOperatorSet.GetContourXld(singleObj, out var rowArr, out var colArr);
            int ptCount = rowArr.Length;
            var points = new WpfPoint[ptCount];
            for (int j = 0; j < ptCount; j++)
            {
                points[j] = new WpfPoint(colArr[j].D, rowArr[j].D);
            }
            result.Add(points);
            singleObj.Dispose();
        }

        xldContours.Dispose();
        return result;
    }
}

/// <summary>
/// 每模型参数覆盖 — 用于多模板时覆盖全局参数
/// </summary>
public class PerModelParamOverride : ObservableObject
{
    private string _templateId = string.Empty;
    private double? _minMatchScore;
    private int? _maxMatchCount;
    private int? _matchTimeoutMs;
    private double? _angleStart;
    private double? _angleEnd;
    private double? _scaleMin;
    private double? _scaleMax;

    public string TemplateId { get => _templateId; set => SetProperty(ref _templateId, value); }
    public double? MinMatchScore { get => _minMatchScore; set => SetProperty(ref _minMatchScore, value); }
    public int? MaxMatchCount { get => _maxMatchCount; set => SetProperty(ref _maxMatchCount, value); }
    public int? MatchTimeoutMs { get => _matchTimeoutMs; set => SetProperty(ref _matchTimeoutMs, value); }
    public double? AngleStart { get => _angleStart; set => SetProperty(ref _angleStart, value); }
    public double? AngleEnd { get => _angleEnd; set => SetProperty(ref _angleEnd, value); }
    public double? ScaleMin { get => _scaleMin; set => SetProperty(ref _scaleMin, value); }
    public double? ScaleMax { get => _scaleMax; set => SetProperty(ref _scaleMax, value); }
}
