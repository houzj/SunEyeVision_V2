using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Logic
{
    /// <summary>
    /// 编辑手柄类型
    /// </summary>
    public enum HandleType
    {
        None,
        // 角落手柄
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        // 边中点手柄
        Top,
        Bottom,
        Left,
        Right,
        // 旋转手柄
        Rotate,
        // 中心手柄（用于拖动）
        Center,
        // 直线端点手柄
        LineStart,
        LineEnd,
        // 多边形顶点手柄
        PolygonVertex
    }

    /// <summary>
    /// 编辑手柄信息
    /// </summary>
    public class EditHandle
    {
        /// <summary>
        /// 手柄类型
        /// </summary>
        public HandleType Type { get; set; }

        /// <summary>
        /// 手柄位置（图像坐标）
        /// </summary>
        public Point2D Position { get; set; }

        /// <summary>
        /// 手柄边界（用于命中测试）
        /// </summary>
        public Rect2D Bounds { get; set; }

        /// <summary>
        /// 手柄大小
        /// </summary>
        public double HandleSize { get; set; } = 12;

        /// <summary>
        /// 是否可见
        /// </summary>
        public bool IsVisible { get; set; } = true;
    }

    /// <summary>
    /// 矩形结构（简化版，用于命中测试）
    /// </summary>
    public struct Rect2D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public Rect2D(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public bool Contains(Point2D point)
        {
            return point.X >= X && point.X <= X + Width &&
                   point.Y >= Y && point.Y <= Y + Height;
        }

        public static Rect2D FromCenter(Point2D center, double size)
        {
            return new Rect2D(center.X - size / 2, center.Y - size / 2, size, size);
        }
    }

    /// <summary>
    /// 手柄管理器 - 创建和管理编辑手柄
    /// </summary>
    public class HandleManager
    {
        private readonly List<EditHandle> _handles = new List<EditHandle>();
        private double _handleSize = 12;
        private double _hitTolerance = 8; // 命中测试容差

        /// <summary>
        /// 当前手柄列表
        /// </summary>
        public IReadOnlyList<EditHandle> Handles => _handles.AsReadOnly();

        /// <summary>
        /// 手柄大小
        /// </summary>
        public double HandleSize
        {
            get => _handleSize;
            set => _handleSize = value;
        }

        /// <summary>
        /// 命中测试容差
        /// </summary>
        public double HitTolerance
        {
            get => _hitTolerance;
            set => _hitTolerance = value;
        }

        /// <summary>
        /// 清除所有手柄
        /// </summary>
        public void Clear()
        {
            _handles.Clear();
        }

        /// <summary>
        /// 为形状创建编辑手柄
        /// </summary>
        public void CreateHandles(ShapeParameters shape)
        {
            _handles.Clear();

            if (shape == null) return;

            switch (shape.ShapeType)
            {
                case ShapeType.Circle:
                    CreateCircleHandles(shape);
                    break;
                case ShapeType.RotatedRectangle:
                    CreateRotatedRectangleHandles(shape);
                    break;
                case ShapeType.Line:
                    CreateLineHandles(shape);
                    break;
                case ShapeType.Polygon:
                    CreatePolygonHandles(shape);
                    break;
                case ShapeType.Rectangle:
                default:
                    CreateRectangleHandles(shape);
                    break;
            }
        }

        /// <summary>
        /// 命中测试
        /// </summary>
        public HandleType HitTest(Point2D point)
        {
            foreach (var handle in _handles)
            {
                if (!handle.IsVisible) continue;

                // 扩大命中区域
                var expandedBounds = new Rect2D(
                    handle.Bounds.X - _hitTolerance,
                    handle.Bounds.Y - _hitTolerance,
                    handle.Bounds.Width + _hitTolerance * 2,
                    handle.Bounds.Height + _hitTolerance * 2);

                if (expandedBounds.Contains(point))
                {
                    return handle.Type;
                }
            }
            return HandleType.None;
        }

        #region 私有方法 - 创建手柄

        /// <summary>
        /// 创建矩形手柄（8个轴对齐手柄）
        /// </summary>
        private void CreateRectangleHandles(ShapeParameters shape)
        {
            var bounds = GetBounds(shape);

            var handles = new[]
            {
                (HandleType.TopLeft, bounds.Left, bounds.Top),
                (HandleType.TopRight, bounds.Right, bounds.Top),
                (HandleType.BottomLeft, bounds.Left, bounds.Bottom),
                (HandleType.BottomRight, bounds.Right, bounds.Bottom),
                (HandleType.Top, bounds.Left + bounds.Width / 2, bounds.Top),
                (HandleType.Bottom, bounds.Left + bounds.Width / 2, bounds.Bottom),
                (HandleType.Left, bounds.Left, bounds.Top + bounds.Height / 2),
                (HandleType.Right, bounds.Right, bounds.Top + bounds.Height / 2),
            };

            foreach (var (type, x, y) in handles)
            {
                AddHandle(type, new Point2D(x, y));
            }
        }

        /// <summary>
        /// 创建圆形手柄（4个对称半径手柄）
        /// </summary>
        private void CreateCircleHandles(ShapeParameters shape)
        {
            if (shape.Geometry is not Circle2d c) return;
            var center = new Point2D(c.Center.X, c.Center.Y);
            var radius = c.Radius;

            // 4个对称点手柄，用于调整半径
            var directions = new[]
            {
                (HandleType.Top, center.X, center.Y - radius),
                (HandleType.Bottom, center.X, center.Y + radius),
                (HandleType.Left, center.X - radius, center.Y),
                (HandleType.Right, center.X + radius, center.Y)
            };

            foreach (var (type, x, y) in directions)
            {
                AddHandle(type, new Point2D(x, y));
            }
        }

        /// <summary>
        /// 创建旋转矩形手柄（8个缩放手柄 + 1个旋转手柄 + 中心手柄）
        /// </summary>
        private void CreateRotatedRectangleHandles(ShapeParameters shape)
        {
            var corners = GetCorners(shape);
            if (corners == null || corners.Length != 4) return;

            // 角点顺序：TopLeft, TopRight, BottomRight, BottomLeft
            var handleTypes = new HandleType[]
            {
                HandleType.TopLeft, HandleType.Top, HandleType.TopRight,
                HandleType.Right, HandleType.BottomRight, HandleType.Bottom,
                HandleType.BottomLeft, HandleType.Left
            };

            // 计算各边中点
            var topCenter = new Point2D(
                (corners[0].X + corners[1].X) / 2,
                (corners[0].Y + corners[1].Y) / 2
            );
            var rightCenter = new Point2D(
                (corners[1].X + corners[2].X) / 2,
                (corners[1].Y + corners[2].Y) / 2
            );
            var bottomCenter = new Point2D(
                (corners[2].X + corners[3].X) / 2,
                (corners[2].Y + corners[3].Y) / 2
            );
            var leftCenter = new Point2D(
                (corners[3].X + corners[0].X) / 2,
                (corners[3].Y + corners[0].Y) / 2
            );

            // 按顺序添加手柄：角点 + 边中点
            var handlePositions = new Point2D[]
            {
                corners[0],      // TopLeft
                topCenter,       // Top
                corners[1],      // TopRight
                rightCenter,     // Right
                corners[2],      // BottomRight
                bottomCenter,    // Bottom
                corners[3],      // BottomLeft
                leftCenter       // Left
            };

            for (int i = 0; i < 8; i++)
            {
                AddHandle(handleTypes[i], handlePositions[i]);
            }

            // 中心手柄
            var center = shape.GetCenter();
            AddHandle(HandleType.Center, center);

            // 旋转手柄（在顶边中点上方）
            var direction = new Point2D(topCenter.X - center.X, topCenter.Y - center.Y);
            var length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
            if (length > 0)
            {
                // 归一化方向向量并延伸25像素
                var unitDir = new Point2D(direction.X / length, direction.Y / length);
                var rotateHandlePos = new Point2D(
                    topCenter.X + unitDir.X * 25,
                    topCenter.Y + unitDir.Y * 25
                );
                AddHandle(HandleType.Rotate, rotateHandlePos);
            }
        }

        /// <summary>
        /// 创建直线手柄（2个端点手柄）
        /// </summary>
        private void CreateLineHandles(ShapeParameters shape)
        {
            // 起点手柄
            AddHandle(HandleType.LineStart, shape.GetStartPoint());

            // 终点手柄
            AddHandle(HandleType.LineEnd, shape.GetEndPoint());
        }

        /// <summary>
        /// 创建多边形手柄（每个顶点一个手柄 + 中心手柄）
        /// </summary>
        private void CreatePolygonHandles(ShapeParameters shape)
        {
            if (shape.Geometry is not Polygon2d poly) return;
            for (int i = 0; i < poly.Vertices.Length; i++)
            {
                var v = poly.Vertices[i];
                AddHandle(HandleType.PolygonVertex, new Point2D(v.X, v.Y));
            }
            AddHandle(HandleType.Center, shape.GetCenter());
        }

        /// <summary>
        /// 添加手柄
        /// </summary>
        private void AddHandle(HandleType type, Point2D position)
        {
            _handles.Add(new EditHandle
            {
                Type = type,
                Position = position,
                Bounds = Rect2D.FromCenter(position, _handleSize),
                HandleSize = _handleSize,
                IsVisible = true
            });
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取形状边界
        /// </summary>
        private Bounds GetBounds(ShapeParameters shape)
        {
            if (shape.Geometry == null)
                return new Bounds(0, 0, 100, 100);

            if (shape.Geometry is System.Windows.Rect r)
                return new Bounds(r.X, r.Y, r.Width, r.Height);
            else if (shape.Geometry is Circle2d c)
                return new Bounds(c.Center.X - c.Radius, c.Center.Y - c.Radius, c.Diameter, c.Diameter);
            else if (shape.Geometry is RotatedRect2d)
                return GetRotatedBoundingBox(shape);
            else if (shape.Geometry is Line2d l)
            {
                var endPt = l.GetPointAt(100);
                var startPt = new System.Windows.Point(l.Point.X, l.Point.Y);
                return new Bounds(
                    Math.Min(startPt.X, endPt.X), Math.Min(startPt.Y, endPt.Y),
                    Math.Abs(endPt.X - startPt.X), Math.Abs(endPt.Y - startPt.Y));
            }
            else if (shape.Geometry is Polygon2d poly)
            {
                var bx = poly.BoundingBox;
                return new Bounds(bx.X, bx.Y, bx.Width, bx.Height);
            }

            return new Bounds(0, 0, 100, 100);
        }

        /// <summary>
        /// 获取旋转矩形的四个角点
        /// </summary>
        private Point2D[]? GetCorners(ShapeParameters shape)
        {
            if (shape.Geometry is not RotatedRect2d rr)
                return null;

            var center = new Point2D(rr.CenterX, rr.CenterY);
            var w = rr.Width;
            var h = rr.Height;
            var angleRad = rr.Angle * Math.PI / 180;
            var cos = Math.Cos(angleRad);
            var sin = Math.Sin(angleRad);

            var hw = w / 2;
            var hh = h / 2;

            Point2D Transform(double localX, double localY)
            {
                return new Point2D(
                    center.X + localX * cos - localY * sin,
                    center.Y + localX * sin + localY * cos
                );
            }

            return new Point2D[]
            {
                Transform(-hw, -hh),  // TopLeft
                Transform( hw, -hh),  // TopRight
                Transform( hw,  hh),  // BottomRight
                Transform(-hw,  hh)   // BottomLeft
            };
        }

        /// <summary>
        /// 获取旋转矩形的轴对齐包围盒
        /// </summary>
        private Bounds GetRotatedBoundingBox(ShapeParameters shape)
        {
            var corners = GetCorners(shape);
            if (corners == null || corners.Length == 0)
            {
                if (shape.Geometry is RotatedRect2d rr)
                    return new Bounds(rr.CenterX - rr.Width / 2, rr.CenterY - rr.Height / 2, rr.Width, rr.Height);
                return new Bounds(0, 0, 100, 100);
            }

            var minX = double.MaxValue;
            var minY = double.MaxValue;
            var maxX = double.MinValue;
            var maxY = double.MinValue;

            foreach (var corner in corners)
            {
                if (corner.X < minX) minX = corner.X;
                if (corner.Y < minY) minY = corner.Y;
                if (corner.X > maxX) maxX = corner.X;
                if (corner.Y < maxY) maxY = corner.Y;
            }

            return new Bounds(minX, minY, maxX - minX, maxY - minY);
        }

        #endregion

        #region 内部结构

        /// <summary>
        /// 边界结构
        /// </summary>
        private struct Bounds
        {
            public double Left { get; }
            public double Top { get; }
            public double Width { get; }
            public double Height { get; }
            public double Right => Left + Width;
            public double Bottom => Top + Height;

            public Bounds(double left, double top, double width, double height)
            {
                Left = left;
                Top = top;
                Width = width;
                Height = height;
            }
        }

        #endregion
    }
}
