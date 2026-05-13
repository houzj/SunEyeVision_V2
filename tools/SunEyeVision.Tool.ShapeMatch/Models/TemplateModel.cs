using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using SunEyeVision.CvEngine;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;

namespace SunEyeVision.Tool.ShapeMatch.Models
{
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

    // ============ 参数模式枚举 ============
    public enum ScaleMode { Auto, Manual }
    public enum ThresholdMode { Auto, Manual }
    public enum ChainLengthMode { Auto, Manual }

    /// <summary>
    /// 模板数据模型（v4.0）
    /// 使用 Scharr+NMS 梯度引擎，存储扁平 EdgePoint 列表
    /// 参数对齐 VM 布局：尺度模式/阈值模式/链长模式
    /// </summary>
    [JsonDerivedType(typeof(TemplateModel), "TemplateModel")]
    public class TemplateModel : ObservableObject
    {
        private string _id = Guid.NewGuid().ToString();
        private string _name = string.Empty;
        private bool _isEnabled = true;
        private int _orderIndex;

        // ============ v4.0 创建参数（7个，对齐VM）============
        private ScaleMode _scaleMode = ScaleMode.Auto;
        private int _speedScale = 4;        // 0~9，仅 Manual 有效
        private int _featureScale = 0;      // 0~9，仅 Manual 有效
        private ThresholdMode _thresholdMode = ThresholdMode.Auto;
        private float _contrast = 30f;      // 1~255，仅 Manual 有效
        private ChainLengthMode _chainLengthMode = ChainLengthMode.Auto;
        private int _minChainLength = 10;   // 0~200，仅 Manual 有效

        private TemplateExtractionStatus _extractionStatus = TemplateExtractionStatus.NotExtracted;
        private List<RegionData> _inspectionRegions = new();
        private List<RegionData> _maskRegions = new();
        private List<CvOp.EdgePoint>? _edgePoints;
        private Dictionary<int, List<CvOp.EdgePoint>>? _pyramidEdgePoints;
        private byte[]? _sourceImageBytes;
        private double _referencePointX;
        private double _referencePointY;

        public string Id { get => _id; set => SetProperty(ref _id, value); }
        public string Name { get => _name; set => SetProperty(ref _name, value, "名称"); }
        public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value, "启用"); }
        public int OrderIndex { get => _orderIndex; set => SetProperty(ref _orderIndex, value); }

        // ============ 创建参数属性 ============
        public ScaleMode ScaleMode { get => _scaleMode; set => SetProperty(ref _scaleMode, value); }
        public int SpeedScale { get => _speedScale; set => SetProperty(ref _speedScale, value, "速度尺度"); }
        public int FeatureScale { get => _featureScale; set => SetProperty(ref _featureScale, value, "特征尺度"); }
        public ThresholdMode ThresholdMode { get => _thresholdMode; set => SetProperty(ref _thresholdMode, value); }
        public float Contrast { get => _contrast; set => SetProperty(ref _contrast, value, "对比度"); }
        public ChainLengthMode ChainLengthMode { get => _chainLengthMode; set => SetProperty(ref _chainLengthMode, value); }
        public int MinChainLength { get => _minChainLength; set => SetProperty(ref _minChainLength, value, "最小链长"); }

        public List<RegionData> InspectionRegions
        {
            get => _inspectionRegions;
            set => SetProperty(ref _inspectionRegions, value);
        }

        public List<RegionData> MaskRegions
        {
            get => _maskRegions;
            set => SetProperty(ref _maskRegions, value);
        }

        public TemplateExtractionStatus ExtractionStatus
        {
            get => _extractionStatus;
            set => SetProperty(ref _extractionStatus, value, "提取状态");
        }

        /// <summary>第 0 层（全分辨率）边缘点列表</summary>
        public List<CvOp.EdgePoint>? EdgePoints
        {
            get => _edgePoints;
            set => SetProperty(ref _edgePoints, value);
        }

        /// <summary>各金字塔层边缘点: key=Level</summary>
        public Dictionary<int, List<CvOp.EdgePoint>>? PyramidEdgePoints
        {
            get => _pyramidEdgePoints;
            set => SetProperty(ref _pyramidEdgePoints, value);
        }

        /// <summary>创建模板时的原始图像（PNG无损）</summary>
        public byte[]? SourceImageBytes
        {
            get => _sourceImageBytes;
            set => SetProperty(ref _sourceImageBytes, value);
        }

        /// <summary>固化参考点 X（模板创建时由 EdgePoints 质心计算）</summary>
        public double ReferencePointX
        {
            get => _referencePointX;
            set => SetProperty(ref _referencePointX, value);
        }

        /// <summary>固化参考点 Y（模板创建时由 EdgePoints 质心计算）</summary>
        public double ReferencePointY
        {
            get => _referencePointY;
            set => SetProperty(ref _referencePointY, value);
        }

        [JsonIgnore]
        public int EdgePointCount => _edgePoints?.Count ?? 0;

        /// <summary>
        /// 根据当前 EdgePoints 设置参考点（模板创建/编辑后调用一次）
        /// </summary>
        public void SetReferencePoint()
        {
            if (_edgePoints == null || _edgePoints.Count == 0) return;
            ReferencePointX = _edgePoints.Average(p => p.X);
            ReferencePointY = _edgePoints.Average(p => p.Y);
        }

        /// <summary>
        /// 深拷贝当前模板
        /// </summary>
        public TemplateModel DeepClone()
        {
            var clone = new TemplateModel
            {
                Id = _id,
                Name = _name,
                IsEnabled = _isEnabled,
                OrderIndex = _orderIndex,
                ScaleMode = _scaleMode,
                SpeedScale = _speedScale,
                FeatureScale = _featureScale,
                ThresholdMode = _thresholdMode,
                Contrast = _contrast,
                ChainLengthMode = _chainLengthMode,
                MinChainLength = _minChainLength,
                ExtractionStatus = _extractionStatus,
                EdgePoints = _edgePoints?.Select(p => new CvOp.EdgePoint
                {
                    X = p.X, Y = p.Y,
                    Direction = p.Direction, Magnitude = p.Magnitude
                }).ToList(),
                PyramidEdgePoints = _pyramidEdgePoints?.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.Select(p => new CvOp.EdgePoint
                    {
                        X = p.X, Y = p.Y,
                        Direction = p.Direction, Magnitude = p.Magnitude
                    }).ToList()),
                SourceImageBytes = _sourceImageBytes?.ToArray(),
                InspectionRegions = _inspectionRegions.Select(r => (RegionData)r.Clone()).ToList(),
                MaskRegions = _maskRegions.Select(r => (RegionData)r.Clone()).ToList(),
                ReferencePointX = _referencePointX,
                ReferencePointY = _referencePointY
            };
            return clone;
        }
    }

    /// <summary>
    /// 每模型参数覆盖 - 用于多模板时覆盖全局参数
    /// </summary>
    public class PerModelParamOverride : ObservableObject
    {
        private string _templateId = string.Empty;
        private double? _minMatchScore;
        private int? _maxMatchCount;
        private int? _matchTimeoutMs;
        private double? _angleStep;
        private double? _angleMin;
        private double? _angleMax;
        private double? _scaleMin;
        private double? _scaleMax;
        private double? _scaleStep;
        private int? _minMatchDistance;
        private bool? _enableSubPixel;
        private double? _subPixelRadius;

        public string TemplateId { get => _templateId; set => SetProperty(ref _templateId, value); }
        public double? MinMatchScore { get => _minMatchScore; set => SetProperty(ref _minMatchScore, value); }
        public int? MaxMatchCount { get => _maxMatchCount; set => SetProperty(ref _maxMatchCount, value); }
        public int? MatchTimeoutMs { get => _matchTimeoutMs; set => SetProperty(ref _matchTimeoutMs, value); }
        public double? AngleStep { get => _angleStep; set => SetProperty(ref _angleStep, value); }
        public double? AngleMin { get => _angleMin; set => SetProperty(ref _angleMin, value); }
        public double? AngleMax { get => _angleMax; set => SetProperty(ref _angleMax, value); }
        public double? ScaleMin { get => _scaleMin; set => SetProperty(ref _scaleMin, value); }
        public double? ScaleMax { get => _scaleMax; set => SetProperty(ref _scaleMax, value); }
        public double? ScaleStep { get => _scaleStep; set => SetProperty(ref _scaleStep, value); }
        public int? MinMatchDistance { get => _minMatchDistance; set => SetProperty(ref _minMatchDistance, value); }
        public bool? EnableSubPixel { get => _enableSubPixel; set => SetProperty(ref _enableSubPixel, value); }
        public double? SubPixelRadius { get => _subPixelRadius; set => SetProperty(ref _subPixelRadius, value); }
    }
}
