using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json.Serialization;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 区域参数基类 - 提供检测区域和屏蔽区域的通用实现
    /// </summary>
    /// <remarks>
    /// 设计理念：
    /// 1. 所有需要区域管理的工具可以继承此基类
    /// 2. 避免重复定义 InspectionRegions 和 MaskRegions
    /// 3. 提供是否启用区域限制的通用参数
    /// 4. 统一区域参数的序列化和反序列化
    /// 
    /// 使用示例：
    /// <code>
    /// [JsonDerivedType(typeof(ThresholdParameters), "Threshold")]
    /// public class ThresholdParameters : RegionParameters
    /// {
    ///     public ParamValue<int> Threshold { get; set; }
    ///     
    ///     public ThresholdParameters()
    ///     {
    ///         Threshold = new ParamValue<int>(128);
    ///     }
    /// }
    /// </code>
    /// </remarks>
    [JsonDerivedType(typeof(RegionParameters), "RegionParameters")]
    public class RegionParameters : ToolParameters
    {
        private RegionCollectionParameter _inspectionRegions;
        private RegionCollectionParameter _maskRegions;
        private ParamValue<bool> _isRegionEnabled;
        private ImageSourceReference? _imageSourceRef;
        private AvailableDataSource? _imageSource;

        /// <summary>
        /// 构造函数
        /// </summary>
        public RegionParameters()
        {
            _inspectionRegions = new RegionCollectionParameter(RegionParameterMode.InspectionRegion);
            _maskRegions = new RegionCollectionParameter(RegionParameterMode.MaskRegion);
            _isRegionEnabled = new ParamValue<bool> { Value = false };
        }

        /// <summary>
        /// 检测区域 - 指定需要进行检测/处理的区域
        /// </summary>
        /// <remarks>
        /// 支持常量模式（用户直接绘制）和绑定模式（订阅其他节点输出）
        /// </remarks>
        [JsonPropertyOrder(10)]
        public RegionCollectionParameter InspectionRegions
        {
            get => _inspectionRegions;
            set => _inspectionRegions = value;
        }

        /// <summary>
        /// 屏蔽区域 - 指定需要排除的区域
        /// </summary>
        /// <remarks>
        /// 支持常量模式（用户直接绘制）和绑定模式（订阅其他节点输出）
        /// </remarks>
        [JsonPropertyOrder(11)]
        public RegionCollectionParameter MaskRegions
        {
            get => _maskRegions;
            set => _maskRegions = value;
        }

        /// <summary>
        /// 是否启用区域限制
        /// </summary>
        /// <remarks>
        /// false = 对整个图像进行处理
        /// true = 仅对有效区域（检测区域 - 屏蔽区域）进行处理
        /// </remarks>
        [JsonPropertyOrder(12)]
        public ParamValue<bool> IsRegionEnabled
        {
            get => _isRegionEnabled;
            set => _isRegionEnabled = value;
        }

        #region 图像源绑定

        /// <summary>
        /// 可序列化的图像源引用
        /// </summary>
        /// <remarks>
        /// 存储 ImageSource 的身份+展示信息（4个字符串），用于 JSON 序列化/反序列化。
        /// 运行时 ImageSource 从此引用通过 RestoreImageSource() 恢复。
        /// </remarks>
        [JsonPropertyOrder(5)]
        [JsonPropertyName("imageSourceRef")]
        public ImageSourceReference? ImageSourceRef
        {
            get => _imageSourceRef;
            set => _imageSourceRef = value;
        }

        /// <summary>
        /// 输入图像源（运行时，不序列化，从 ImageSourceRef 恢复）
        /// </summary>
        /// <remarks>
        /// AvailableDataSource 包含运行时数据（Mat 图像、TreeNode 等）无法序列化，
        /// 因此标记 [JsonIgnore] 不参与序列化。
        /// setter 中自动同步更新 ImageSourceRef。
        /// </remarks>
        [JsonIgnore]
        public AvailableDataSource? ImageSource
        {
            get => _imageSource;
            set
            {
                if (SetProperty(ref _imageSource, value, "输入图像"))
                {
                    // 自动同步到可序列化的引用
                    _imageSourceRef = ImageSourceReference.FromDataSource(value);
                }
            }
        }

        /// <summary>
        /// 从 AvailableDataSources 列表恢复 ImageSource
        /// </summary>
        /// <param name="dataSources">当前可用的数据源列表</param>
        /// <returns>是否成功恢复</returns>
        public bool RestoreImageSource(System.Collections.Generic.IEnumerable<AvailableDataSource>? dataSources)
        {
            if (_imageSourceRef == null || dataSources == null)
                return false;

            var matched = _imageSourceRef.MatchDataSource(dataSources);
            if (matched != null)
            {
                // 直接设置字段，避免触发 setter 中的重复同步
                _imageSource = matched;
                OnPropertyChanged(nameof(ImageSource));
                return true;
            }
            return false;
        }

        #endregion

        /// <summary>
        /// 按区域类型获取启用的区域列表
        /// </summary>
        /// <param name="type">区域类型（Inspection/Mask）</param>
        /// <returns>指定类型的启用区域集合</returns>
        public System.Collections.Generic.IEnumerable<RegionData> GetRegionsByType(RegionType type)
        {
            return type == RegionType.Inspection
                ? _inspectionRegions.Regions.Where(r => r.IsEnabled)
                : _maskRegions.Regions.Where(r => r.IsEnabled);
        }

        /// <summary>
        /// 获取有效区域列表（检测区域 - 屏蔽区域）
        /// </summary>
        /// <returns>有效区域集合</returns>
        /// <remarks>
        /// 此方法由算法层调用，计算最终的检测区域
        /// 有效区域 = 检测区域 ∩ (图像全集 - 屏蔽区域)
        /// </remarks>
        public System.Collections.Generic.IEnumerable<RegionData> GetEffectiveRegions()
        {
            var inspectionRegions = _inspectionRegions.GetEnabledRegions();
            var maskRegions = _maskRegions.GetEnabledRegions();

            if (inspectionRegions.Count() == 0)
            {
                // 没有检测区域，返回空列表
                return new System.Collections.Generic.List<RegionData>();
            }

            if (maskRegions.Count() == 0)
            {
                // 没有屏蔽区域，直接返回检测区域
                return new System.Collections.Generic.List<RegionData>(inspectionRegions);
            }

            // 简化实现：如果检测区域和屏蔽区域有重叠，从检测区域中移除
            // 实际应使用几何算法计算差集
            var effectiveRegions = new System.Collections.Generic.List<RegionData>();

            foreach (var inspectionRegion in inspectionRegions)
            {
                var isOverlapped = false;
                var inspectionBounds = GetRegionBounds(inspectionRegion);

                foreach (var maskRegion in maskRegions)
                {
                    var maskBounds = GetRegionBounds(maskRegion);

                    // 简单的边界盒重叠检测
                    if (IsBoundsOverlapped(inspectionBounds, maskBounds))
                    {
                        isOverlapped = true;
                        break;
                    }
                }

                // TODO: 实现精确的几何差分算法
                // 当前简化实现：如果有重叠，跳过该检测区域
                if (!isOverlapped)
                {
                    effectiveRegions.Add(inspectionRegion);
                }
            }

            return effectiveRegions;
        }

        /// <summary>
        /// 获取区域的边界盒
        /// </summary>
        private static Rect GetRegionBounds(RegionData region)
        {
            if (region.Parameters == null)
                return new Rect(0, 0, 0, 0);

            var shape = region.Parameters;
            var shapeType = shape.GetShapeType();

            if (!shapeType.HasValue || shape is not ShapeParameters shapeParams)
                return new Rect(0, 0, 0, 0);

            return shapeType.Value switch
            {
                ShapeType.Rectangle => new Rect(
                    (int)(shapeParams.CenterX - shapeParams.Width / 2),
                    (int)(shapeParams.CenterY - shapeParams.Height / 2),
                    (int)shapeParams.Width,
                    (int)shapeParams.Height
                ),
                ShapeType.Circle => new Rect(
                    (int)(shapeParams.CenterX - shapeParams.Radius),
                    (int)(shapeParams.CenterY - shapeParams.Radius),
                    (int)(shapeParams.Radius * 2),
                    (int)(shapeParams.Radius * 2)
                ),
                ShapeType.RotatedRectangle => new Rect(
                    (int)(shapeParams.CenterX - shapeParams.Width / 2),
                    (int)(shapeParams.CenterY - shapeParams.Height / 2),
                    (int)shapeParams.Width,
                    (int)shapeParams.Height
                ),
                ShapeType.Polygon => GetPolygonBounds(shapeParams.Points),
                ShapeType.Annulus => new Rect(
                    (int)(shapeParams.CenterX - shapeParams.OuterRadius > 0 ? shapeParams.OuterRadius : shapeParams.Radius + 20),
                    (int)(shapeParams.CenterY - shapeParams.OuterRadius > 0 ? shapeParams.OuterRadius : shapeParams.Radius + 20),
                    (int)(shapeParams.OuterRadius > 0 ? shapeParams.OuterRadius * 2 : (shapeParams.Radius + 20) * 2),
                    (int)(shapeParams.OuterRadius > 0 ? shapeParams.OuterRadius * 2 : (shapeParams.Radius + 20) * 2)
                ),
                _ => new Rect(0, 0, 0, 0)
            };
        }

        /// <summary>
        /// 获取多边形的边界盒
        /// </summary>
        private static Rect GetPolygonBounds(System.Collections.Generic.IList<UI.Controls.Region.Models.Point2D> points)
        {
            if (points == null || points.Count == 0)
                return new Rect(0, 0, 0, 0);

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var point in points)
            {
                if (point.X < minX) minX = (float)point.X;
                if (point.Y < minY) minY = (float)point.Y;
                if (point.X > maxX) maxX = (float)point.X;
                if (point.Y > maxY) maxY = (float)point.Y;
            }

            return new Rect(
                (int)minX,
                (int)minY,
                (int)(maxX - minX),
                (int)(maxY - minY)
                );
        }

        /// <summary>
        /// 检查两个边界盒是否重叠
        /// </summary>
        private static bool IsBoundsOverlapped(Rect bounds1, Rect bounds2)
        {
            if (bounds1.Width <= 0 || bounds1.Height <= 0 || bounds2.Width <= 0 || bounds2.Height <= 0)
                return false;

            return bounds1.X < bounds2.X + bounds2.Width &&
                   bounds1.X + bounds1.Width > bounds2.X &&
                   bounds1.Y < bounds2.Y + bounds2.Height &&
                   bounds1.Y + bounds1.Height > bounds2.Y;
        }


    }
}
