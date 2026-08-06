using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models
{
    /// <summary>
    /// 区域定义基类
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(ShapeParameters), "Shape")]
    [JsonDerivedType(typeof(FixedRoi), "Fixed")]
    [JsonDerivedType(typeof(ComputedRoi), "Computed")]
    public abstract class RoiDefinition : ObservableObject
    {
        private Guid _id = Guid.NewGuid();
        private RoiSourceMode _mode = RoiSourceMode.Drawing;

        public Guid Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public RoiSourceMode Mode
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
        public abstract RoiDefinition Clone();
    }

    /// <summary>
    /// 形状参数 - 用户绘制的形状参数。
    /// 持有 UI 样式信息和 SDK 几何类型实例。
    /// </summary>
    public class ShapeParameters : RoiDefinition, IMonitorable
    {
        #region 私有字段

        private object? _geometry;
        private ObservableCollection<Point2D>? _points;
        private uint _fillColorArgb = 0x00000000;
        private uint _strokeColorArgb = 0xFF0000FF;
        private double _strokeThickness = RoiEditorSettings.Default.DefaultStrokeThickness;
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
            set
            {
                if (SetProperty(ref _geometry, value))
                {
                    // Geometry 变化后，同步通知所有扁平参数属性（绘制参数面板的绑定源），实现双向联动
                    NotifyGeometryDerivedProperties();
                }
            }
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

        #region 扁平参数属性（绘制参数面板绑定源）

        // 以下扁平属性均从 Geometry 实时推导（get）并重建 Geometry 写回（set）。
        // Geometry 是唯一序列化源，扁平属性为 UI 派生视图，标记 [JsonIgnore] 避免冗余序列化。

        /// <summary>
        /// 中心 X（矩形/旋转矩形/圆形/多边形共用）
        /// </summary>
        [JsonIgnore]
        public double CenterX
        {
            get => GetCenter().X;
            set
            {
                var pt = GetCenter();
                if (Math.Abs(pt.X - value) < 1e-9) return;
                SetCenter(new Point2D(value, pt.Y));
            }
        }

        /// <summary>
        /// 中心 Y（矩形/旋转矩形/圆形/多边形共用）
        /// </summary>
        [JsonIgnore]
        public double CenterY
        {
            get => GetCenter().Y;
            set
            {
                var pt = GetCenter();
                if (Math.Abs(pt.Y - value) < 1e-9) return;
                SetCenter(new Point2D(pt.X, value));
            }
        }

        /// <summary>
        /// 宽度（矩形/旋转矩形）
        /// </summary>
        [JsonIgnore]
        public double Width
        {
            get => _geometry switch
            {
                RotatedRect2d rr => rr.Width,
                System.Windows.Rect r => r.Width,
                _ => 0,
            };
            set => SetSize(value, Height);
        }

        /// <summary>
        /// 高度（矩形/旋转矩形）
        /// </summary>
        [JsonIgnore]
        public double Height
        {
            get => _geometry switch
            {
                RotatedRect2d rr => rr.Height,
                System.Windows.Rect r => r.Height,
                _ => 0,
            };
            set => SetSize(Width, value);
        }

        /// <summary>
        /// 半径（圆形）
        /// </summary>
        [JsonIgnore]
        public double Radius
        {
            get => _geometry is Circle2d c ? c.Radius : 0;
            set
            {
                if (_geometry is not Circle2d c || Math.Abs(c.Radius - value) < 1e-9) return;
                _geometry = new Circle2d(c.Center, value);
                NotifyGeometryDerivedProperties();
            }
        }

        /// <summary>
        /// 旋转角度（度，旋转矩形）
        /// </summary>
        [JsonIgnore]
        public double Angle
        {
            get => _geometry is RotatedRect2d rr ? rr.Angle : 0;
            set
            {
                if (_geometry is not RotatedRect2d rr || Math.Abs(rr.Angle - value) < 1e-9) return;
                _geometry = new RotatedRect2d(rr.Center, rr.Width, rr.Height, value);
                NotifyGeometryDerivedProperties();
            }
        }

        /// <summary>
        /// 直线起点 X
        /// </summary>
        [JsonIgnore]
        public double StartX
        {
            get => GetStartPoint().X;
            set
            {
                var pt = GetStartPoint();
                if (Math.Abs(pt.X - value) < 1e-9) return;
                SetStartPoint(new Point2D(value, pt.Y));
            }
        }

        /// <summary>
        /// 直线起点 Y
        /// </summary>
        [JsonIgnore]
        public double StartY
        {
            get => GetStartPoint().Y;
            set
            {
                var pt = GetStartPoint();
                if (Math.Abs(pt.Y - value) < 1e-9) return;
                SetStartPoint(new Point2D(pt.X, value));
            }
        }

        /// <summary>
        /// 直线终点 X
        /// </summary>
        [JsonIgnore]
        public double EndX
        {
            get => GetEndPoint().X;
            set
            {
                var pt = GetEndPoint();
                if (Math.Abs(pt.X - value) < 1e-9) return;
                SetEndPoint(new Point2D(value, pt.Y));
            }
        }

        /// <summary>
        /// 直线终点 Y
        /// </summary>
        [JsonIgnore]
        public double EndY
        {
            get => GetEndPoint().Y;
            set
            {
                var pt = GetEndPoint();
                if (Math.Abs(pt.Y - value) < 1e-9) return;
                SetEndPoint(new Point2D(pt.X, value));
            }
        }

        /// <summary>
        /// 多边形顶点（可编辑集合，顶点变更时重建 Polygon2d）
        /// </summary>
        [JsonIgnore]
        public ObservableCollection<Point2D>? Points
        {
            get
            {
                if (_geometry is not Polygon2d poly) return null;
                if (_points == null)
                {
                    _points = new ObservableCollection<Point2D>(
                        poly.Vertices.Select(v => new Point2D(v.X, v.Y)));
                    _points.CollectionChanged += (_, _) => SyncPolygonFromPoints();
                    foreach (var pt in _points)
                        pt.PropertyChanged += (_, _) => SyncPolygonFromPoints();
                }
                return _points;
            }
        }

        #endregion

        /// <summary>
        /// 根据宽高重建矩形/旋转矩形几何
        /// </summary>
        private void SetSize(double width, double height)
        {
            var shapeType = GeometryToShapeType(_geometry);
            switch (shapeType)
            {
                case ShapeType.RotatedRectangle when _geometry is RotatedRect2d rr:
                    _geometry = new RotatedRect2d(rr.Center, width, height, rr.Angle);
                    break;
                case ShapeType.Rectangle when _geometry is System.Windows.Rect r:
                    _geometry = new System.Windows.Rect(r.X, r.Y, width, height);
                    break;
                default:
                    return;
            }
            NotifyGeometryDerivedProperties();
        }

        /// <summary>
        /// 从顶点集合同步回 Polygon2d
        /// </summary>
        private void SyncPolygonFromPoints()
        {
            if (_points == null || _geometry is not Polygon2d poly) return;
            var vertices = _points.Select(p => new System.Windows.Point(p.X, p.Y)).ToArray();
            _geometry = new Polygon2d(vertices, poly.IsClosed);
            NotifyGeometryDerivedProperties();
        }

        /// <summary>
        /// 通知所有由 Geometry 推导的扁平属性（面板绑定源）
        /// </summary>
        private void NotifyGeometryDerivedProperties()
        {
            OnPropertyChanged(nameof(ShapeType));
            OnPropertyChanged(nameof(CenterX));
            OnPropertyChanged(nameof(CenterY));
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(Radius));
            OnPropertyChanged(nameof(Angle));
            OnPropertyChanged(nameof(StartX));
            OnPropertyChanged(nameof(StartY));
            OnPropertyChanged(nameof(EndX));
            OnPropertyChanged(nameof(EndY));
            OnPropertyChanged(nameof(Points));
        }

        public ShapeParameters()
        {
            Mode = RoiSourceMode.Drawing;
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
            NotifyGeometryDerivedProperties();
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
                NotifyGeometryDerivedProperties();
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
                NotifyGeometryDerivedProperties();
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

        public override RoiDefinition Clone()
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
    public class FixedRoi : RoiDefinition
    {
        public string SourceNodeId { get; set; } = string.Empty;
        public string OutputName { get; set; } = string.Empty;
        public int? RegionIndex { get; set; }

        public FixedRoi()
        {
            Mode = RoiSourceMode.Subscribe;
        }

        public FixedRoi(string nodeId, string outputName, int? index = null)
        {
            Mode = RoiSourceMode.Subscribe;
            SourceNodeId = nodeId;
            OutputName = outputName;
            RegionIndex = index;
        }

        public override ShapeType? GetShapeType() => null;

        public override RoiDefinition Clone()
        {
            return new FixedRoi(SourceNodeId, OutputName, RegionIndex)
            {
                Id = Id
            };
        }
    }

    /// <summary>
    /// 计算区域 - 每个参数可单独绑定
    /// </summary>
    public class ComputedRoi : RoiDefinition
    {
        public ShapeType TargetShapeType { get; set; } = ShapeType.Rectangle;
        public Dictionary<string, ParameterSource> ParameterBindings { get; set; } = new();

        public ComputedRoi()
        {
            Mode = RoiSourceMode.Subscribe;
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

        public override RoiDefinition Clone()
        {
            var clone = new ComputedRoi
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
