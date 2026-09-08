using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 几何输入的本体（本地几何定义）：一段可绘制/可显示的用户几何（值 + 显示样式 + 编辑桥）。
    /// 语义定位：ROI/图形输入项 <see cref="Roi"/> 的本地几何承载；订阅模式下为 null（由 Binding 提供）。
    /// 本类只表达"几何值 + 样式"，不承载订阅语义（订阅归 <see cref="Roi.Binding"/>），
    /// 也不依赖 UI 控件层（编辑器命名空间 <c>UI.Controls.Roi</c> 仅消费本类型）。
    /// </summary>
    /// <remarks>
    /// 演进来源：原 <c>ShapeParameters</c>（UI.Controls.Roi.Models）剥离"区域定义/订阅"语义后的 Execution 域版本。
    /// </remarks>
    public class RoiShape : ObservableObject, IMonitorable
    {
        #region 私有字段

        private object? _geometry;
        private uint _fillColorArgb = 0x00000000;
        private uint _strokeColorArgb = 0xFF0000FF;
        private double _strokeThickness = 2.0;
        private double _opacity = 1.0;

        #endregion

        /// <summary>
        /// SDK 几何值（多边形/圆形/直线/矩形/旋转矩形/圆环/弧/点之一）。
        /// 序列化经 <see cref="GeometryJsonConverter"/> 写入 <c>$geom</c> 判别符还原具体类型。
        /// </summary>
        [JsonConverter(typeof(GeometryJsonConverter))]
        public object? Geometry
        {
            get => _geometry;
            set
            {
                if (SetProperty(ref _geometry, value))
                {
                    // Geometry 变化后同步通知扁平参数属性（UI 参数面板绑定源）
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
        /// 从 Geometry 推导形状类型（UI 下拉/绑定 [Type=X] 用）
        /// </summary>
        public ShapeType ShapeType => GeometryToShapeType(_geometry);

        #region 扁平参数属性（绘制参数面板绑定源）

        // 以下扁平属性均从 Geometry 实时推导（get）并重建 Geometry 写回（set）。
        // Geometry 是唯一序列化源，扁平属性为 UI 派生视图，标记 [JsonIgnore] 避免冗余序列化。

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

        private ObservableCollection<Point2D>? _points;

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

        #region 编辑辅助方法

        /// <summary>
        /// 获取中心点（UI 坐标）
        /// </summary>
        public Point2D GetCenter()
        {
            var wpfPt = _geometry switch
            {
                Circle2d c => new System.Windows.Point(c.Center.X, c.Center.Y),
                RotatedRect2d rr => new System.Windows.Point(rr.CenterX, rr.CenterY),
                Polygon2d poly => new System.Windows.Point(poly.Center.X, poly.Center.Y),
                Annulus2d a => new System.Windows.Point(a.Center.X, a.Center.Y),
                Line2d l => new System.Windows.Point(l.Point.X, l.Point.Y),
                System.Windows.Rect r => new System.Windows.Point(r.X + r.Width / 2, r.Y + r.Height / 2),
                _ => new System.Windows.Point(0, 0),
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

        private Point2D GetStartPoint()
        {
            if (_geometry is Line2d l)
                return new Point2D(l.Point.X, l.Point.Y);
            return new Point2D(0, 0);
        }

        private void SetStartPoint(Point2D point)
        {
            if (_geometry is Line2d l)
            {
                _geometry = new Line2d(new System.Windows.Point(point.X, point.Y), l.Direction);
                OnPropertyChanged(nameof(Geometry));
                NotifyGeometryDerivedProperties();
            }
        }

        private Point2D GetEndPoint()
        {
            if (_geometry is Line2d l)
            {
                var endPt = l.GetPointAt(100);
                return new Point2D(endPt.X, endPt.Y);
            }
            return new Point2D(0, 0);
        }

        private void SetEndPoint(Point2D point)
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

        private void SetSize(double width, double height)
        {
            switch (_geometry)
            {
                case RotatedRect2d rr:
                    _geometry = new RotatedRect2d(rr.Center, width, height, rr.Angle);
                    break;
                case System.Windows.Rect r:
                    _geometry = new System.Windows.Rect(r.X, r.Y, width, height);
                    break;
                default:
                    return;
            }
            NotifyGeometryDerivedProperties();
        }

        private void SyncPolygonFromPoints()
        {
            if (_points == null || _geometry is not Polygon2d poly) return;
            var vertices = _points.Select(p => new System.Windows.Point(p.X, p.Y)).ToArray();
            _geometry = new Polygon2d(vertices, poly.IsClosed);
            NotifyGeometryDerivedProperties();
        }

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

        #endregion

        /// <summary>
        /// 图形概要（绑定树/结果树/日志可读摘要）。按 Geometry 真实类型输出中文短名。
        /// </summary>
        public override string ToString()
        {
            return _geometry switch
            {
                Circle2d c => $"圆(({c.Center.X:F1},{c.Center.Y:F1}), R={c.Radius:F1})",
                RotatedRect2d rr => $"旋转矩形(({rr.Center.X:F1},{rr.Center.Y:F1}), {rr.Width:F1}x{rr.Height:F1}, {rr.Angle:F1}°)",
                System.Windows.Rect r => $"矩形(({r.X:F1},{r.Y:F1}), {r.Width:F1}x{r.Height:F1})",
                Polygon2d p => $"多边形({p.Vertices.Length}顶点)",
                Line2d l => $"直线(({l.Point.X:F1},{l.Point.Y:F1}), 方向({l.Direction.X:F1},{l.Direction.Y:F1}))",
                Annulus2d a => a.IsFullCircle
                    ? $"圆环(({a.Center.X:F1},{a.Center.Y:F1}), R=[{a.InnerRadius:F1},{a.OuterRadius:F1}])"
                    : $"圆环(({a.Center.X:F1},{a.Center.Y:F1}), R=[{a.InnerRadius:F1},{a.OuterRadius:F1}], [{a.StartAngle * 180 / Math.PI:F1}°,{a.EndAngle * 180 / Math.PI:F1}°])",
                Arc2d a => $"弧形(({a.Center.X:F1},{a.Center.Y:F1}), R={a.Radius:F1}, [{a.StartAngle * 180 / Math.PI:F1}°,{a.EndAngle * 180 / Math.PI:F1}°])",
                Point2D pt => $"点(({pt.X:F1},{pt.Y:F1}))",
                System.Windows.Point pt => $"点(({pt.X:F1},{pt.Y:F1}))",
                null => "空图形",
                _ => $"未知图形({_geometry?.GetType().Name ?? "Null"})",
            };
        }

        /// <summary>
        /// 按 ShapeType 创建默认 SDK 几何值（供编辑器新建区域/结果树绑定树设计期枚举属性字段）。
        /// 吸收原 <c>RoiData.CreateDefaultGeometry</c> 职责（值无意义，仅类型模板）。
        /// </summary>
        public static object CreateDefaultGeometry(ShapeType shapeType)
        {
            return shapeType switch
            {
                ShapeType.Circle           => new Circle2d(0, 0, 50),
                ShapeType.Line             => Line2d.FromTwoPoints(new System.Windows.Point(0, 0), new System.Windows.Point(100, 100)),
                ShapeType.Annulus          => new Annulus2d(0, 0, 30, 80),
                ShapeType.RotatedRectangle => new RotatedRect2d(0, 0, 100, 100, 0),
                ShapeType.Polygon          => new Polygon2d(new[]
                {
                    new System.Windows.Point(0, 0),
                    new System.Windows.Point(100, 0),
                    new System.Windows.Point(50, 100),
                }),
                ShapeType.Arc              => new Arc2d(0, 0, 50, 0, Math.PI),
                ShapeType.Point            => new Point2D(0, 0),
                _                          => new System.Windows.Rect(-50, -50, 100, 100),
            };
        }

        /// <summary>
        /// 将 Geometry 类型映射回 ShapeType 枚举
        /// </summary>
        public static ShapeType GeometryToShapeType(object? geometry)
        {
            return geometry switch
            {
                Point2D pt => ShapeType.Point,
                Circle2d => ShapeType.Circle,
                Line2d => ShapeType.Line,
                Annulus2d => ShapeType.Annulus,
                RotatedRect2d => ShapeType.RotatedRectangle,
                Polygon2d => ShapeType.Polygon,
                Arc2d => ShapeType.Arc,
                System.Windows.Rect => ShapeType.Rectangle,
                _ => ShapeType.None,
            };
        }

        /// <summary>
        /// 克隆
        /// </summary>
        public RoiShape Clone()
        {
            return new RoiShape
            {
                Geometry = CloneGeometry(_geometry),
                FillColorArgb = FillColorArgb,
                StrokeColorArgb = StrokeColorArgb,
                StrokeThickness = StrokeThickness,
                Opacity = Opacity,
            };
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

            if (_geometry != null)
            {
                var method = _geometry.GetType().GetMethod("GetMonitorFields", System.Type.EmptyTypes);
                if (method != null && method.Invoke(_geometry, null) is IReadOnlyList<(string, object?)> fields)
                    return fields;
            }

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
}
