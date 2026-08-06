using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Roi.Logic
{
    /// <summary>
    /// 解析后的区域结果
    /// </summary>
    public class ResolvedRegion
    {
        /// <summary>
        /// 形状类型
        /// </summary>
        public ShapeType ShapeType { get; set; }

        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }

        #region 几何参数

        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Angle { get; set; }
        public double Radius { get; set; }
        public double OuterRadius { get; set; }
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public double StartAngle { get; set; }
        public double EndAngle { get; set; }
        public List<Point2D> Points { get; set; } = new();

        #endregion

        /// <summary>
        /// 原始区域数据
        /// </summary>
        public RoiData? SourceData { get; set; }

        /// <summary>
        /// 创建无效区域
        /// </summary>
        public static ResolvedRegion Invalid(string errorMessage)
        {
            return new ResolvedRegion
            {
                IsValid = false,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <summary>
    /// 区域解析器 - 将抽象区域数据转换为几何参数
    /// </summary>
    public class RoiResolver
    {
        private readonly IParameterContext? _context;
        private readonly ILogger _logger;

        public RoiResolver(IParameterContext? context = null, ILogger? logger = null)
        {
            _context = context;
            _logger = logger ?? PluginLogger.Logger;
        }

        /// <summary>
        /// 解析区域数据
        /// </summary>
        public ResolvedRegion Resolve(RoiData? regionData)
        {
            if (regionData == null)
            {
                return ResolvedRegion.Invalid("区域数据为空");
            }

            if (regionData.Parameters == null)
            {
                return ResolvedRegion.Invalid("区域参数为空");
            }

            return regionData.Parameters switch
            {
                ShapeParameters shapeDef => ResolveShapeDefinition(shapeDef, regionData),
                FixedRoi fixedDef => ResolveFixedRegion(fixedDef, regionData),
                ComputedRoi computedDef => ResolveComputedRegion(computedDef, regionData),
                _ => ResolvedRegion.Invalid("未知的区域定义类型")
            };
        }

        /// <summary>
        /// 解析形状定义
        /// </summary>
        private ResolvedRegion ResolveShapeDefinition(ShapeParameters shapeDef, RoiData sourceData)
        {
            var result = new ResolvedRegion
            {
                ShapeType = shapeDef.ShapeType,
                IsValid = true,
                SourceData = sourceData
            };

            // 从 Geometry 复制几何参数
            if (shapeDef.Geometry is Circle2d c)
            {
                result.CenterX = c.Center.X; result.CenterY = c.Center.Y;
                result.Radius = c.Radius;
            }
            else if (shapeDef.Geometry is RotatedRect2d rr)
            {
                result.CenterX = rr.CenterX; result.CenterY = rr.CenterY;
                result.Width = rr.Width; result.Height = rr.Height;
                result.Angle = rr.Angle;
            }
            else if (shapeDef.Geometry is Line2d l)
            {
                result.StartX = l.Point.X; result.StartY = l.Point.Y;
                var endPt = l.GetPointAt(100);
                result.EndX = endPt.X; result.EndY = endPt.Y;
            }
            else if (shapeDef.Geometry is Annulus2d a)
            {
                result.CenterX = a.Center.X; result.CenterY = a.Center.Y;
                result.Radius = a.InnerRadius; result.OuterRadius = a.OuterRadius;
                result.StartAngle = a.StartAngle; result.EndAngle = a.EndAngle;
            }
            else if (shapeDef.Geometry is Polygon2d poly)
            {
                result.Points = poly.Vertices.Select(v => new Point2D(v.X, v.Y)).ToList();
            }
            else if (shapeDef.Geometry is System.Windows.Rect r)
            {
                result.CenterX = r.X + r.Width / 2; result.CenterY = r.Y + r.Height / 2;
                result.Width = r.Width; result.Height = r.Height;
            }
            else if (shapeDef.Geometry is Arc2d arc)
            {
                result.CenterX = arc.Center.X; result.CenterY = arc.Center.Y;
                result.Radius = arc.Radius;
                result.StartAngle = arc.StartAngle; result.EndAngle = arc.EndAngle;
            }

            // 验证参数
            var validationError = ValidateShapeParameters(result);
            if (validationError != null)
            {
                result.IsValid = false;
                result.ErrorMessage = validationError;
            }

            return result;
        }

        /// <summary>
        /// 解析固定区域定义
        /// </summary>
        private ResolvedRegion ResolveFixedRegion(FixedRoi fixedDef, RoiData sourceData)
        {
            if (_context == null)
            {
                return ResolvedRegion.Invalid("参数上下文为空，无法解析订阅区域");
            }

            var regionValue = _context.GetNodeOutputValue(
                fixedDef.SourceNodeId,
                fixedDef.OutputName,
                fixedDef.RegionIndex);

            if (regionValue == null)
            {
                return ResolvedRegion.Invalid($"无法获取节点 {fixedDef.SourceNodeId} 的输出 {fixedDef.OutputName}");
            }

            // 尝试将值转换为区域
            return ConvertToResolvedRegion(regionValue, sourceData);
        }

        /// <summary>
        /// 解析计算区域定义
        /// </summary>
        private ResolvedRegion ResolveComputedRegion(ComputedRoi computedDef, RoiData sourceData)
        {
            var result = new ResolvedRegion
            {
                ShapeType = computedDef.TargetShapeType,
                IsValid = true,
                SourceData = sourceData
            };

            // 解析每个参数
            result.CenterX = ResolveParameter(computedDef, "CenterX", 0.0);
            result.CenterY = ResolveParameter(computedDef, "CenterY", 0.0);
            result.Width = ResolveParameter(computedDef, "Width", 100.0);
            result.Height = ResolveParameter(computedDef, "Height", 100.0);
            result.Angle = ResolveParameter(computedDef, "Angle", 0.0);
            result.Radius = ResolveParameter(computedDef, "Radius", 50.0);
            result.OuterRadius = ResolveParameter(computedDef, "OuterRadius", 100.0);
            result.StartX = ResolveParameter(computedDef, "StartX", 0.0);
            result.StartY = ResolveParameter(computedDef, "StartY", 0.0);
            result.EndX = ResolveParameter(computedDef, "EndX", 100.0);
            result.EndY = ResolveParameter(computedDef, "EndY", 0.0);
            result.StartAngle = ResolveParameter(computedDef, "StartAngle", 0.0);
            result.EndAngle = ResolveParameter(computedDef, "EndAngle", 360.0);

            // 验证参数
            var validationError = ValidateShapeParameters(result);
            if (validationError != null)
            {
                result.IsValid = false;
                result.ErrorMessage = validationError;
            }

            return result;
        }

        /// <summary>
        /// 解析单个参数
        /// </summary>
        private double ResolveParameter(ComputedRoi computedDef, string parameterName, double defaultValue)
        {
            var source = computedDef.GetParameterBinding(parameterName);
            if (source == null)
            {
                return defaultValue;
            }

            var value = source.GetValue(_context, _logger);
            if (value == null)
            {
                return defaultValue;
            }

            // 尝试转换为double
            return Convert.ToDouble(value);
        }

        /// <summary>
        /// 验证形状参数
        /// </summary>
        private string? ValidateShapeParameters(ResolvedRegion region)
        {
            switch (region.ShapeType)
            {
                case ShapeType.Rectangle:
                case ShapeType.RotatedRectangle:
                    if (region.Width <= 0) return "宽度必须大于0";
                    if (region.Height <= 0) return "高度必须大于0";
                    break;

                case ShapeType.Circle:
                    if (region.Radius <= 0) return "半径必须大于0";
                    break;

                case ShapeType.Annulus:
                    if (region.Radius <= 0) return "内半径必须大于0";
                    if (region.OuterRadius <= region.Radius) return "外半径必须大于内半径";
                    break;

                case ShapeType.Line:
                    var length = Math.Sqrt(
                        Math.Pow(region.EndX - region.StartX, 2) +
                        Math.Pow(region.EndY - region.StartY, 2));
                    if (length < 1) return "直线长度太短";
                    break;

                case ShapeType.Polygon:
                    if (region.Points.Count < 3) return "多边形至少需要3个顶点";
                    break;

                case ShapeType.Arc:
                    if (region.Radius <= 0) return "半径必须大于0";
                    break;
            }

            return null;
        }

        /// <summary>
        /// 将值转换为解析后的区域
        /// </summary>
        private ResolvedRegion ConvertToResolvedRegion(object value, RoiData sourceData)
        {
            // 处理常见的区域类型
            // 这里可以根据实际需要扩展支持更多类型
            
            if (value is RoiData regionData)
            {
                return Resolve(regionData);
            }

            if (value is ResolvedRegion resolved)
            {
                return resolved;
            }

            // WpfRect → 轴对齐矩形
            if (value is Rect rect)
            {
                return RectToResolvedRegion(rect, sourceData);
            }

            // IVisionRegion → 取其外接矩形
            if (value is IVisionRegion visionRegion)
            {
                return VisionRegionToResolvedRegion(visionRegion, sourceData);
            }

            // 尝试从字典或匿名对象解析
            if (value is IDictionary<string, object> dict)
            {
                return ResolveFromDictionary(dict, sourceData);
            }

            return ResolvedRegion.Invalid($"无法将值类型 {value.GetType().Name} 转换为区域");
        }

        /// <summary>
        /// 从 WpfRect 转换为解析区域
        /// </summary>
        private static ResolvedRegion RectToResolvedRegion(Rect rect, RoiData sourceData)
        {
            return new ResolvedRegion
            {
                ShapeType = ShapeType.Rectangle,
                IsValid = true,
                SourceData = sourceData,
                CenterX = rect.X + rect.Width / 2,
                CenterY = rect.Y + rect.Height / 2,
                Width = rect.Width,
                Height = rect.Height
            };
        }

        /// <summary>
        /// 从 IVisionRegion 转换为解析区域（取外接矩形）
        /// </summary>
        private static ResolvedRegion VisionRegionToResolvedRegion(IVisionRegion visionRegion, RoiData sourceData)
        {
            var bbox = visionRegion.BoundingBox;
            return new ResolvedRegion
            {
                ShapeType = ShapeType.Rectangle,
                IsValid = true,
                SourceData = sourceData,
                CenterX = bbox.X + bbox.Width / 2,
                CenterY = bbox.Y + bbox.Height / 2,
                Width = bbox.Width,
                Height = bbox.Height
            };
        }

        /// <summary>
        /// 从字典解析区域
        /// </summary>
        private ResolvedRegion ResolveFromDictionary(IDictionary<string, object> dict, RoiData sourceData)
        {
            var result = new ResolvedRegion
            {
                SourceData = sourceData,
                IsValid = true
            };

            // 解析形状类型
            if (dict.TryGetValue("ShapeType", out var shapeTypeObj))
            {
                if (shapeTypeObj is ShapeType shapeType)
                    result.ShapeType = shapeType;
                else if (Enum.TryParse<ShapeType>(shapeTypeObj?.ToString(), out var parsed))
                    result.ShapeType = parsed;
            }

            // 解析几何参数
            if (dict.TryGetValue("CenterX", out var cx)) result.CenterX = Convert.ToDouble(cx);
            if (dict.TryGetValue("CenterY", out var cy)) result.CenterY = Convert.ToDouble(cy);
            if (dict.TryGetValue("Width", out var w)) result.Width = Convert.ToDouble(w);
            if (dict.TryGetValue("Height", out var h)) result.Height = Convert.ToDouble(h);
            if (dict.TryGetValue("Angle", out var a)) result.Angle = Convert.ToDouble(a);
            if (dict.TryGetValue("Radius", out var r)) result.Radius = Convert.ToDouble(r);
            if (dict.TryGetValue("OuterRadius", out var or)) result.OuterRadius = Convert.ToDouble(or);
            if (dict.TryGetValue("StartX", out var sx)) result.StartX = Convert.ToDouble(sx);
            if (dict.TryGetValue("StartY", out var sy)) result.StartY = Convert.ToDouble(sy);
            if (dict.TryGetValue("EndX", out var ex)) result.EndX = Convert.ToDouble(ex);
            if (dict.TryGetValue("EndY", out var ey)) result.EndY = Convert.ToDouble(ey);

            // 验证参数
            var validationError = ValidateShapeParameters(result);
            if (validationError != null)
            {
                result.IsValid = false;
                result.ErrorMessage = validationError;
            }

            return result;
        }
    }

    /// <summary>
    /// 区域解析器扩展方法
    /// </summary>
    public static class RoiResolverExtensions
    {
        /// <summary>
        /// 批量解析区域
        /// </summary>
        public static List<ResolvedRegion> ResolveAll(this RoiResolver resolver, IEnumerable<RoiData> regions)
        {
            var results = new List<ResolvedRegion>();
            foreach (var region in regions)
            {
                results.Add(resolver.Resolve(region));
            }
            return results;
        }
    }
}
