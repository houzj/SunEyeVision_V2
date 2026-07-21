using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Models
{
    /// <summary>
    /// 区域定义基类
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(ShapeParameters), "Shape")]
    [JsonDerivedType(typeof(FixedRegion), "Fixed")]
    [JsonDerivedType(typeof(ComputedRegion), "Computed")]
    public abstract class RegionDefinition : ObservableObject
    {
        private Guid _id = Guid.NewGuid();
        private RegionSourceMode _mode = RegionSourceMode.Drawing;

        public Guid Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public RegionSourceMode Mode
        {
            get => _mode;
            set => SetProperty(ref _mode, value);
        }

        /// <summary>
        /// 获取形状类型（如果适用）
        /// </summary>
        public abstract ShapeType? GetShapeType();

        /// <summary>
        /// 克隆
        /// </summary>
        public abstract RegionDefinition Clone();
    }

    /// <summary>
    /// 形状参数 - 用户绘制的形状参数。
    /// 持有 UI 样式信息和 SDK 几何类型实例。
    /// </summary>
    public class ShapeParameters : RegionDefinition, IMonitorable
    {
        #region 私有字段

        private object? _geometry;
        private uint _fillColorArgb = 0x00000000;
        private uint _strokeColorArgb = 0xFF0000FF;
        private double _strokeThickness = RegionEditorSettings.Default.DefaultStrokeThickness;
        private double _opacity = 0.3;

        #endregion

        /// <summary>
        /// SDK 几何类型实例。
        /// Circle2d / Line2d / Annulus2d / RotatedRect2d / Polygon2d / Arc2d / Point2D / System.Windows.Rect
        /// </summary>
        [JsonConverter(typeof(GeometryJsonConverter))]
        public object? Geometry
        {
            get => _geometry;
            set => SetProperty(ref _geometry, value);
        }

        #region 样式属性

        public uint FillColorArgb
        {
            get => _fillColorArgb;
            set => SetProperty(ref _fillColorArgb, value, "填充颜色");
        }

        public uint StrokeColorArgb
        {
            get => _strokeColorArgb;
            set => SetProperty(ref _strokeColorArgb, value, "边框颜色");
        }

        public double StrokeThickness
        {
            get => _strokeThickness;
            set => SetProperty(ref _strokeThickness, value, "边框厚度");
        }

        public double Opacity
        {
            get => _opacity;
            set => SetProperty(ref _opacity, value, "透明度");
        }

        #endregion

        /// <summary>
        /// 从 Geometry 推导 ShapeType 枚举（UI 下拉用）
        /// </summary>
        public ShapeType ShapeType => GeometryToShapeType(_geometry);

        public ShapeParameters()
        {
            Mode = RegionSourceMode.Drawing;
        }

        public override ShapeType? GetShapeType() => GeometryToShapeType(_geometry);

        #region 编辑辅助方法

        /// <summary>
        /// 获取中心点（UI 坐标）
        /// </summary>
        public Point2D GetCenter()
        {
            var wpfPt = _geometry switch
            {
                Circle2d c       => c.Center,
                RotatedRect2d rr => new System.Windows.Point(rr.CenterX, rr.CenterY),
                Polygon2d poly   => poly.Center,
                Annulus2d a      => a.Center,
                Line2d l         => l.Point,
                System.Windows.Rect r => new System.Windows.Point(r.X + r.Width / 2, r.Y + r.Height / 2),
                _                => new System.Windows.Point(0, 0),
            };
            return new Point2D(wpfPt.X, wpfPt.Y);
        }

        /// <summary>
        /// 设置中心点（创建新 Geometry，仅对中心点可变的类型生效）
        /// </summary>
        public void SetCenter(Point2D center)
        {
            _geometry = _geometry switch
            {
                Circle2d c => new Circle2d(center.X, center.Y, c.Radius),
                RotatedRect2d rr => new RotatedRect2d(center.X, center.Y, rr.Width, rr.Height, rr.Angle),
                Annulus2d a => new Annulus2d(center.X, center.Y, a.InnerRadius, a.OuterRadius, a.StartAngle, a.EndAngle),
                System.Windows.Rect r => new System.Windows.Rect(center.X - r.Width / 2, center.Y - r.Height / 2, r.Width, r.Height),
                _ => _geometry,
            };
            OnPropertyChanged(nameof(Geometry));
        }

        /// <summary>
        /// 获取直线起点
        /// </summary>
        public Point2D GetStartPoint()
        {
            if (_geometry is Line2d l)
                return new Point2D(l.Point.X, l.Point.Y);
            return new Point2D(0, 0);
        }

        /// <summary>
        /// 设置直线起点
        /// </summary>
        public void SetStartPoint(Point2D point)
        {
            if (_geometry is Line2d l)
            {
                var dir = l.Direction;
                _geometry = new Line2d(new System.Windows.Point(point.X, point.Y), dir);
                OnPropertyChanged(nameof(Geometry));
            }
        }

        /// <summary>
        /// 获取直线终点（基于点向式，从点+方向逆推终点）
        /// </summary>
        public Point2D GetEndPoint()
        {
            if (_geometry is Line2d l)
            {
                // 取一个足够远的点表示"终点"
                var endPt = l.GetPointAt(100);
                return new Point2D(endPt.X, endPt.Y);
            }
            return new Point2D(0, 0);
        }

        /// <summary>
        /// 设置直线终点（重新计算方向）
        /// </summary>
        public void SetEndPoint(Point2D point)
        {
            if (_geometry is Line2d l)
            {
                var startPt = new System.Windows.Point(l.Point.X, l.Point.Y);
                var endPt = new System.Windows.Point(point.X, point.Y);
                var dir = endPt - startPt;
                if (dir.LengthSquared > 1e-20)
                    _geometry = new Line2d(startPt, dir);
                OnPropertyChanged(nameof(Geometry));
            }
        }

        /// <summary>
        /// 获取直线长度
        /// </summary>
        public double GetLineLength()
        {
            return _geometry switch
            {
                Line2d l => 100 * l.Direction.Length,  // GetPointAt(100) 的长度
                _ => 0,
            };
        }

        /// <summary>
        /// 获取直线角度（数学角度系统）
        /// </summary>
        public double GetLineAngle()
        {
            return _geometry switch
            {
                Line2d l => -l.Angle * 180 / Math.PI,
                _ => 0,
            };
        }

        #endregion

        /// <summary>
        /// 将 Geometry 类型映射回 ShapeType 枚举
        /// </summary>
        public static ShapeType GeometryToShapeType(object? geometry)
        {
            return geometry switch
            {
                Point2D pt       => ShapeType.Point,
                Circle2d         => ShapeType.Circle,
                Line2d           => ShapeType.Line,
                Annulus2d        => ShapeType.Annulus,
                RotatedRect2d    => ShapeType.RotatedRectangle,
                Polygon2d        => ShapeType.Polygon,
                Arc2d            => ShapeType.Arc,
                System.Windows.Rect => ShapeType.Rectangle,
                _                => ShapeType.None,
            };
        }

        public override RegionDefinition Clone()
        {
            var clone = new ShapeParameters
            {
                Id = Id,
                Mode = Mode,
                Geometry = CloneGeometry(_geometry),
                FillColorArgb = FillColorArgb,
                StrokeColorArgb = StrokeColorArgb,
                StrokeThickness = StrokeThickness,
                Opacity = Opacity,
            };
            return clone;
        }

        private static object? CloneGeometry(object? geometry)
        {
            return geometry switch
            {
                Polygon2d poly => new Polygon2d(
                    poly.Vertices.Select(v => new System.Windows.Point(v.X, v.Y)).ToArray(), poly.IsClosed),
                Point2D pt => new Point2D(pt.X, pt.Y),
                ICloneable cloneable => cloneable.Clone(),
                _ => geometry,  // struct 值类型直接复制
            };
        }

        public IReadOnlyList<(string FieldName, object? FieldValue)> GetMonitorFields()
        {
            if (_geometry is IMonitorable m)
                return m.GetMonitorFields();

            // 几何 struct（Circle2d 等）已实现 GetMonitorFields() 但未声明 IMonitorable 接口
            // （方案原则：不动几何类本身）。鸭子类型调用，使 RoI 字段聚合（步骤6 原则3）能跨元素透视几何字段。
            if (_geometry != null)
            {
                var method = _geometry.GetType().GetMethod("GetMonitorFields", System.Type.EmptyTypes);
                if (method != null && method.Invoke(_geometry, null) is IReadOnlyList<(string, object?)> fields)
                    return fields;
            }

            // 兜底
            return _geometry switch
            {
                System.Windows.Rect r => new (string, object?)[]
                {
                    ("X", r.X), ("Y", r.Y), ("宽", r.Width), ("高", r.Height),
                },
                Point2D pt => new (string, object?)[]
                {
                    ("X", pt.X), ("Y", pt.Y),
                },
                _ => new (string, object?)[] { ("类型", _geometry?.GetType().Name ?? "Null") },
            };
        }
    }

    /// <summary>
    /// 固定区域 - 订阅其他节点的区域输出
    /// </summary>
    public class FixedRegion : RegionDefinition
    {
        public string SourceNodeId { get; set; } = string.Empty;
        public string OutputName { get; set; } = string.Empty;
        public int? RegionIndex { get; set; }

        public FixedRegion()
        {
            Mode = RegionSourceMode.Subscribe;
        }

        public FixedRegion(string nodeId, string outputName, int? index = null)
        {
            Mode = RegionSourceMode.Subscribe;
            SourceNodeId = nodeId;
            OutputName = outputName;
            RegionIndex = index;
        }

        public override ShapeType? GetShapeType() => null;

        public override RegionDefinition Clone()
        {
            return new FixedRegion(SourceNodeId, OutputName, RegionIndex)
            {
                Id = Id
            };
        }
    }

    /// <summary>
    /// 计算区域 - 每个参数可单独绑定
    /// </summary>
    public class ComputedRegion : RegionDefinition
    {
        public ShapeType TargetShapeType { get; set; } = ShapeType.Rectangle;
        public Dictionary<string, ParameterSource> ParameterBindings { get; set; } = new();

        public ComputedRegion()
        {
            Mode = RegionSourceMode.Subscribe;
        }

        public override ShapeType? GetShapeType() => TargetShapeType;

        public void SetParameterBinding(string parameterName, ParameterSource source)
        {
            ParameterBindings[parameterName] = source;
        }

        public ParameterSource? GetParameterBinding(string parameterName)
        {
            return ParameterBindings.TryGetValue(parameterName, out var source) ? source : null;
        }

        public void RemoveParameterBinding(string parameterName)
        {
            ParameterBindings.Remove(parameterName);
        }

        public object? ResolveParameter(string parameterName, IParameterContext? context)
        {
            var source = GetParameterBinding(parameterName);
            return source?.GetValue(context);
        }

        public override RegionDefinition Clone()
        {
            var clone = new ComputedRegion
            {
                Id = Id,
                TargetShapeType = TargetShapeType
            };
            foreach (var kvp in ParameterBindings)
            {
                clone.ParameterBindings[kvp.Key] = kvp.Value.Clone();
            }
            return clone;
        }
    }
}
