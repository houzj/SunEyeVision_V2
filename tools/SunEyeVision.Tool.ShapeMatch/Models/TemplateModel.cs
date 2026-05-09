using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;

namespace SunEyeVision.Tool.ShapeMatch.Models
{
    public enum ContourApproxMethod
    {
        None = 0,
        ApproxPolyDP = 1,
        UniformResample = 2,
        ApproxPolyDPAndResample = 3
    }

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
    /// </summary>
    [JsonDerivedType(typeof(TemplateModel), "TemplateModel")]
    public class TemplateModel : ObservableObject
    {
        private string _id = Guid.NewGuid().ToString();
        private string _name = string.Empty;
        private bool _isEnabled = true;
        private int _orderIndex;
        private int _smoothKernelSize = 3;
        private int _edgeLowThreshold = 50;
        private int _edgeHighThreshold = 150;
        private int _edgeApertureSize = 3;
        private ContourApproxMethod _approxMethod = ContourApproxMethod.ApproxPolyDP;
        private double _approxPrecision = 2.0;
        private int _minChainLength = 8;
        private double _minArea = 50.0;
        private int _retrievalMode = 0; // 0=External(最外层), 1=List(层次结构)
        private bool _useL2Gradient = true;
        private bool _useSubPixel = false;
        private List<List<Point2D>>? _contourGroups;
        private double _contourWidth;
        private double _contourHeight;
        private double _centerX;
        private double _centerY;
        private int _targetPointCount = 60;
        private TemplateExtractionStatus _extractionStatus = TemplateExtractionStatus.NotExtracted;
        private List<RegionData> _inspectionRegions = new();
        private List<RegionData> _maskRegions = new();

        public string Id { get => _id; set => SetProperty(ref _id, value); }
        public string Name { get => _name; set => SetProperty(ref _name, value, "名称"); }
        public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value, "启用"); }
        public int OrderIndex { get => _orderIndex; set => SetProperty(ref _orderIndex, value); }

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

        public int SmoothKernelSize
        {
            get => _smoothKernelSize;
            set => SetProperty(ref _smoothKernelSize, value, "平滑核大小");
        }

        public int EdgeLowThreshold
        {
            get => _edgeLowThreshold;
            set => SetProperty(ref _edgeLowThreshold, value, "低阈值");
        }

        public int EdgeHighThreshold
        {
            get => _edgeHighThreshold;
            set => SetProperty(ref _edgeHighThreshold, value, "高阈值");
        }

        public int EdgeApertureSize
        {
            get => _edgeApertureSize;
            set => SetProperty(ref _edgeApertureSize, value, "孔径");
        }

        public ContourApproxMethod ApproxMethod
        {
            get => _approxMethod;
            set => SetProperty(ref _approxMethod, value, "近似方法");
        }

        public double ApproxPrecision
        {
            get => _approxPrecision;
            set => SetProperty(ref _approxPrecision, value, "精度");
        }

        public int TargetPointCount
        {
            get => _targetPointCount;
            set => SetProperty(ref _targetPointCount, value, "目标点数");
        }

        public int MinChainLength
        {
            get => _minChainLength;
            set => SetProperty(ref _minChainLength, value, "最小链长");
        }

        public double MinArea
        {
            get => _minArea;
            set => SetProperty(ref _minArea, value, "最小面积");
        }

        public int RetrievalMode
        {
            get => _retrievalMode;
            set => SetProperty(ref _retrievalMode, value, "轮廓检索模式");
        }

        public bool UseL2Gradient
        {
            get => _useL2Gradient;
            set => SetProperty(ref _useL2Gradient, value, "L2梯度");
        }

        public bool UseSubPixel
        {
            get => _useSubPixel;
            set => SetProperty(ref _useSubPixel, value, "亚像素轮廓");
        }

        public List<List<Point2D>>? ContourGroups
        {
            get => _contourGroups;
            set => SetProperty(ref _contourGroups, value);
        }

        public double ContourWidth { get => _contourWidth; set => SetProperty(ref _contourWidth, value); }
        public double ContourHeight { get => _contourHeight; set => SetProperty(ref _contourHeight, value); }
        public double CenterX { get => _centerX; set => SetProperty(ref _centerX, value); }
        public double CenterY { get => _centerY; set => SetProperty(ref _centerY, value); }

        public TemplateExtractionStatus ExtractionStatus
        {
            get => _extractionStatus;
            set => SetProperty(ref _extractionStatus, value, "提取状态");
        }

        [JsonIgnore]
        public int ContourPointCount => _contourGroups?.Sum(g => g.Count) ?? 0;

        private int _contourGroupCount = 1;

        /// <summary>轮廓组数量（多轮廓模式下有效，单轮廓默认为1）</summary>
        public int ContourGroupCount
        {
            get => _contourGroupCount;
            set => SetProperty(ref _contourGroupCount, value);
        }

        private byte[]? _sourceImageBytes;

        /// <summary>
        /// 创建模板时的原始图像（PNG无损），用于离线预览和编辑恢复。
        /// 在 ExecuteContourExtraction(saveResult:true) 中从 _currentMat 提取。
        /// 序列化为 Base64 存储在 .solution 文件中。
        /// </summary>
        public byte[]? SourceImageBytes
        {
            get => _sourceImageBytes;
            set => SetProperty(ref _sourceImageBytes, value);
        }

        /// <summary>
        /// 等距重采样 — 沿轮廓路径等弧长间隔取点，精确控制目标点数
        /// </summary>
        public static Point2f[] ResampleContour(OpenCvSharp.Point[] contour, int targetCount)
        {
            if (contour.Length <= targetCount || targetCount < 3)
                return contour.Select(p => new Point2f(p.X, p.Y)).ToArray();

            return ComputeUniformResample(contour.Length, targetCount, i =>
            {
                int idx = Math.Min(i, contour.Length - 1);
                return (contour[idx].X, contour[idx].Y);
            });
        }

        /// <summary>
        /// 等距重采样（Point2f 重载）— 用于亚像素轮廓
        /// </summary>
        public static Point2f[] ResampleContour(Point2f[] contour, int targetCount)
        {
            if (contour.Length <= targetCount || targetCount < 3)
                return contour.ToArray();

            return ComputeUniformResample(contour.Length, targetCount, i =>
            {
                int idx = Math.Min(i, contour.Length - 1);
                return (contour[idx].X, contour[idx].Y);
            });
        }

        /// <summary>
        /// 深拷贝当前模板（用于编辑模式下的取消回退）
        /// </summary>
        public TemplateModel DeepClone()
        {
            var clone = new TemplateModel
            {
                Id = _id,
                Name = _name,
                IsEnabled = _isEnabled,
                OrderIndex = _orderIndex,
                SmoothKernelSize = _smoothKernelSize,
                EdgeLowThreshold = _edgeLowThreshold,
                EdgeHighThreshold = _edgeHighThreshold,
                EdgeApertureSize = _edgeApertureSize,
                ApproxMethod = _approxMethod,
                ApproxPrecision = _approxPrecision,
                TargetPointCount = _targetPointCount,
                MinChainLength = _minChainLength,
                MinArea = _minArea,
                RetrievalMode = _retrievalMode,
                UseL2Gradient = _useL2Gradient,
                UseSubPixel = _useSubPixel,
                ExtractionStatus = _extractionStatus,
                ContourWidth = _contourWidth,
                ContourHeight = _contourHeight,
                CenterX = _centerX,
                CenterY = _centerY,
                ContourGroupCount = _contourGroupCount,
                ContourGroups = _contourGroups?.Select(g => g.Select(p => new Point2D { X = p.X, Y = p.Y }).ToList()).ToList(),
                SourceImageBytes = _sourceImageBytes?.ToArray(),
                InspectionRegions = _inspectionRegions.Select(r => (RegionData)r.Clone()).ToList(),
                MaskRegions = _maskRegions.Select(r => (RegionData)r.Clone()).ToList()
            };
            return clone;
        }

        /// <summary>
        /// 等距重采样核心算法 — 沿路径等弧长间隔取点
        /// </summary>
        private static Point2f[] ComputeUniformResample(int length, int targetCount, Func<int, (double X, double Y)> getPoint)
        {
            // 1. 计算总弧长
            double totalLength = 0;
            var prev = getPoint(0);
            for (int i = 1; i < length; i++)
            {
                var curr = getPoint(i);
                double dx = curr.X - prev.X;
                double dy = curr.Y - prev.Y;
                totalLength += Math.Sqrt(dx * dx + dy * dy);
                prev = curr;
            }

            if (totalLength <= 0)
            {
                var pts = new Point2f[length];
                for (int i = 0; i < length; i++)
                {
                    var p = getPoint(i);
                    pts[i] = new Point2f((float)p.X, (float)p.Y);
                }
                return pts;
            }

            // 2. 采样间隔
            double interval = totalLength / targetCount;

            // 3. 沿路径等距取点
            var result = new List<Point2f>(targetCount);
            var start = getPoint(0);
            result.Add(new Point2f((float)start.X, (float)start.Y));

            double accumulated = 0;
            prev = getPoint(0);
            for (int i = 1; i < length && result.Count < targetCount; i++)
            {
                var curr = getPoint(i);
                double dx = curr.X - prev.X;
                double dy = curr.Y - prev.Y;
                double segLen = Math.Sqrt(dx * dx + dy * dy);
                accumulated += segLen;

                while (accumulated >= interval && result.Count < targetCount)
                {
                    result.Add(new Point2f((float)curr.X, (float)curr.Y));
                    accumulated -= interval;
                }
                prev = curr;
            }

            return result.ToArray();
        }

        /// <summary>
        /// 旧格式兼容：反序列化时自动将 NaN 分隔扁平列表（ContourPoints）转换为 ContourGroups
        /// </summary>
        [JsonPropertyName("ContourPoints")]
        private List<Point2D>? DeprecatedContourPoints
        {
            set
            {
                if (value == null) return;
                var groups = new List<List<Point2D>>();
                var current = new List<Point2D>();
                foreach (var pt in value)
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
                _contourGroups = groups;
            }
        }
    }

    /// <summary>
    /// 每模型参数覆盖 - 用于多模板时覆盖全局参数
    /// </summary>
    public class PerModelParamOverride : ObservableObject
    {
        private string _templateId = string.Empty;
        private int? _pyramidLevels;
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
        public int? PyramidLevels { get => _pyramidLevels; set => SetProperty(ref _pyramidLevels, value); }
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
