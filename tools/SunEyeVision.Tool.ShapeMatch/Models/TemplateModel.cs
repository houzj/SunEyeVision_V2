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

    /// <summary>
    /// 模板数据模型
    /// 使用 Scharr+NMS 梯度引擎 + 每层独立提取
    /// 创建参数：Contrast, NumLevels, PyramidStart, PyramidEnd, MinChainLength
    /// </summary>
    [JsonDerivedType(typeof(TemplateModel), "TemplateModel")]
    public class TemplateModel : ObservableObject
    {
        private string _id = Guid.NewGuid().ToString();
        private string _name = string.Empty;
        private bool _isEnabled = true;
        private int _orderIndex;

        // ============ 创建参数 ============
        private float _contrast = 30f;
        private int _numLevels = 4;
        private int _pyramidStart = 3;       // 起始层（最粗层）
        private int _pyramidEnd = 1;         // 结束层（最细层）
        private int _minChainLength = 5;     // 最小链长
        private bool _isChainAuto = true;    // 链长模式（true=自动）

        private TemplateExtractionStatus _extractionStatus = TemplateExtractionStatus.NotExtracted;
        private List<RegionData> _inspectionRegions = new();
        private List<RegionData> _maskRegions = new();
        private List<CvOp.EdgePoint>? _edgePoints;
        private Dictionary<int, List<CvOp.EdgePoint>>? _pyramidEdgePoints;
        private byte[]? _sourceImageBytes;
        private double _referencePointX;
        private double _referencePointY;
        private Dictionary<int, LevelSizeInfo>? _pyramidLevelSizes;

        public string Id { get => _id; set => SetProperty(ref _id, value); }
        public string Name { get => _name; set => SetProperty(ref _name, value, "名称"); }
        public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value, "启用"); }
        public int OrderIndex { get => _orderIndex; set => SetProperty(ref _orderIndex, value); }

        // ============ 创建参数属性 ============
        public float Contrast { get => _contrast; set => SetProperty(ref _contrast, value, "对比度"); }
        public int NumLevels { get => _numLevels; set => SetProperty(ref _numLevels, value, "金字塔层数"); }
        public int PyramidStart { get => _pyramidStart; set => SetProperty(ref _pyramidStart, value, "起始层"); }
        public int PyramidEnd { get => _pyramidEnd; set => SetProperty(ref _pyramidEnd, value, "结束层"); }
        public int MinChainLength { get => _minChainLength; set => SetProperty(ref _minChainLength, value, "最小链长"); }
        public bool IsChainAuto { get => _isChainAuto; set => SetProperty(ref _isChainAuto, value); }

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

        /// <summary>第 1 层（全分辨率）边缘点列表</summary>
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

        /// <summary>各金字塔层实际尺寸: key=Level, value=(Width, Height)</summary>
        public Dictionary<int, LevelSizeInfo>? PyramidLevelSizes
        {
            get => _pyramidLevelSizes;
            set => SetProperty(ref _pyramidLevelSizes, value);
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
                Contrast = _contrast,
                NumLevels = _numLevels,
                PyramidStart = _pyramidStart,
                PyramidEnd = _pyramidEnd,
                MinChainLength = _minChainLength,
                IsChainAuto = _isChainAuto,

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
                PyramidLevelSizes = _pyramidLevelSizes?.ToDictionary(
                    kv => kv.Key,
                    kv => new LevelSizeInfo { Width = kv.Value.Width, Height = kv.Value.Height }),
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
    /// 金字塔层级的实际尺寸信息（用于显示时精确恢复坐标）
    /// </summary>
    public class LevelSizeInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
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
        private bool? _enableSubPixel;

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
        public bool? EnableSubPixel { get => _enableSubPixel; set => SetProperty(ref _enableSubPixel, value); }
    }
}
