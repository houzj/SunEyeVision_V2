using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Rendering
{
    /// <summary>
    /// 编辑手柄类型（参考ROI编辑器的HandleType枚举）
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
        public HandleType Type { get; set; }
        public Point Position { get; set; }
        public Rect Bounds { get; set; }
        public Cursor Cursor { get; set; } = Cursors.Arrow;
        public double HandleSize { get; set; } = 60;
        /// <summary>
        /// 顶点索引（仅多边形顶点手柄使用）
        /// </summary>
        public int VertexIndex { get; set; } = -1;
    }

    /// <summary>
    /// 手柄渲染器 - 参考ROI编辑器的手柄绘制逻辑
    /// </summary>
    public class HandleRenderer
    {
        /// <summary>
        /// 计算自适应标签字体大小（与手柄同策略的幂律缩放）
        /// 幂律公式：screenSize = 12 / zoom^0.15，Clamp [9, 12] px
        ///
        /// 缩放行为：
        ///   低zoom(看全局) → 字体大(~12px) → 远距离可读
        ///   标准zoom      → 字体适中(~12px) → 舒适阅读
        ///   高zoom(看细节) → 字体小(~9px)  → 避免遮挡细节
        /// </summary>
        /// <param name="scale">当前视口缩放值(zoom)</param>
        /// <returns>WPF坐标系中的字体大小（经ScaleTransform放大后达到屏幕目标尺寸）</returns>
        public static double GetScaledLabelFontSize(double scale)
        {
            scale = Math.Max(scale, 0.01);
            const double A = 12.0;
            const double p = 0.15;
            var screenSize = A / Math.Pow(scale, p);
            screenSize = Math.Clamp(screenSize, 9.0, 12.0);
            return screenSize / scale;
        }

        /// <summary>
        /// 计算自适应手柄大小（强自适应，统一尺寸）
        /// 幂律公式：screenSize = 10 / zoom^0.15，Clamp [8, 12] px
        ///
        /// 缩放行为：
        ///   低zoom(看全局) → 手柄大(~12px) → 易于点击
        ///   标准zoom      → 手柄适中(~10px) → 舒适操作
        ///   高zoom(看细节) → 手柄小(~10px) → 避免遮挡
        /// </summary>
        /// <param name="scale">当前视口缩放值(zoom)</param>
        /// <returns>WPF坐标系中的手柄尺寸（经ScaleTransform放大后达到屏幕目标尺寸）</returns>
        public static double GetScaledHandleSize(double scale)
        {
            // 安全下限，防止除零或负数
            scale = Math.Max(scale, 0.01);

            // 幂律公式：screenSize = A / scale^p
            // 锚点：zoom=0.1→10.9px, zoom=1.0→10.0px（基准值）, 最小10px
            // 解得 A=10, p≈0.15
            const double A = 10.0;
            const double p = 0.15;

            var screenSize = A / Math.Pow(scale, p);

            // Clamp [10, 12]
            screenSize = Math.Clamp(screenSize, 10.0, 12.0);

            // 反除缩放 → WPF坐标（WPF渲染时再乘以 scale 得到屏幕像素）
            return screenSize / scale;
        }

        /// <summary>
        /// 计算自适应虚线间隔（与手柄同策略的幂律缩放）
        /// 幂律公式：screenSize = baseInterval / zoom^0.15，Clamp [baseInterval, baseInterval * 1.5] px
        ///
        /// 缩放行为：
        ///   低zoom(看全局) → 间隔大(~6px) → 避免虚线密集视觉噪声
        ///   标准zoom      → 间隔适中(~4px) → 虚线清晰
        ///   高zoom(看细节) → 间隔小(~4px) → 虚线精细
        /// </summary>
        /// <param name="scale">当前视口缩放值(zoom)</param>
        /// <param name="baseInterval">基准间隔（WPF坐标下zoom=1时的屏幕像素值），默认4.0</param>
        /// <returns>WPF坐标系中的虚线间隔（经ScaleTransform放大后达到屏幕目标尺寸）</returns>
        public static double GetScaledDashInterval(double scale, double baseInterval = 4.0)
        {
            scale = Math.Max(scale, 0.01);
            const double p = 0.15;
            var screenSize = baseInterval / Math.Pow(scale, p);
            screenSize = Math.Clamp(screenSize, baseInterval, baseInterval * 1.5);
            return screenSize / scale;
        }

        /// <summary>
        /// 创建矩形手柄（参考ROI编辑器CreateRectangleHandles 1071-1097行）
        /// 每个手柄根据类型使用不同尺寸
        /// </summary>
        public static EditHandle[] CreateRectangleHandles(Rect bounds, double scale = 1.0)
        {
            var handles = new List<EditHandle>
            {
                // 4个角点手柄
                new EditHandle
                {
                    Type = HandleType.TopLeft,
                    Position = new Point(bounds.Left, bounds.Top),
                    HandleSize = GetScaledHandleSize(scale),
                    Cursor = Cursors.SizeNWSE
                },
                new EditHandle
                {
                    Type = HandleType.TopRight,
                    Position = new Point(bounds.Right, bounds.Top),
                    HandleSize = GetScaledHandleSize(scale),
                    Cursor = Cursors.SizeNESW
                },
                new EditHandle
                {
                    Type = HandleType.BottomLeft,
                    Position = new Point(bounds.Left, bounds.Bottom),
                    HandleSize = GetScaledHandleSize(scale),
                    Cursor = Cursors.SizeNESW
                },
                new EditHandle
                {
                    Type = HandleType.BottomRight,
                    Position = new Point(bounds.Right, bounds.Bottom),
                    HandleSize = GetScaledHandleSize(scale),
                    Cursor = Cursors.SizeNWSE
                },
                // 4个边中点手柄
                new EditHandle
                {
                    Type = HandleType.Top,
                    Position = new Point(bounds.Left + bounds.Width / 2, bounds.Top),
                    HandleSize = GetScaledHandleSize(scale),
                    Cursor = Cursors.SizeNS
                },
                new EditHandle
                {
                    Type = HandleType.Bottom,
                    Position = new Point(bounds.Left + bounds.Width / 2, bounds.Bottom),
                    HandleSize = GetScaledHandleSize(scale),
                    Cursor = Cursors.SizeNS
                },
                new EditHandle
                {
                    Type = HandleType.Left,
                    Position = new Point(bounds.Left, bounds.Top + bounds.Height / 2),
                    HandleSize = GetScaledHandleSize(scale),
                    Cursor = Cursors.SizeWE
                },
                new EditHandle
                {
                    Type = HandleType.Right,
                    Position = new Point(bounds.Right, bounds.Top + bounds.Height / 2),
                    HandleSize = GetScaledHandleSize(scale),
                    Cursor = Cursors.SizeWE
                },
                // 中心手柄（用于拖动整个矩形）
                new EditHandle
                {
                    Type = HandleType.Center,
                    Position = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2),
                    HandleSize = GetScaledHandleSize(scale),
                    Cursor = Cursors.SizeAll
                }
            };

            // 统一计算 Bounds
            foreach (var handle in handles)
            {
                var size = handle.HandleSize;
                handle.Bounds = new Rect(
                    handle.Position.X - size / 2,
                    handle.Position.Y - size / 2,
                    size, size);
            }

            return handles.ToArray();
        }

        /// <summary>
        /// 创建圆形手柄（参考ROI编辑器CreateCircleHandles 1102-1126行）
        /// </summary>
        public static EditHandle[] CreateCircleHandles(Point center, double radius, double scale = 1.0)
        {
            var handles = new List<EditHandle>
            {
                new EditHandle
                {
                    Type = HandleType.Top,
                    Position = new Point(center.X, center.Y - radius),
                    HandleSize = GetScaledHandleSize(scale),
                    Cursor = Cursors.SizeAll
                },
                new EditHandle
                {
                    Type = HandleType.Bottom,
                    Position = new Point(center.X, center.Y + radius),
                    HandleSize = GetScaledHandleSize(scale),
                    Cursor = Cursors.SizeAll
                },
                new EditHandle
                {
                    Type = HandleType.Left,
                    Position = new Point(center.X - radius, center.Y),
                    HandleSize = GetScaledHandleSize(scale),
                    Cursor = Cursors.SizeAll
                },
                new EditHandle
                {
                    Type = HandleType.Right,
                    Position = new Point(center.X + radius, center.Y),
                    HandleSize = GetScaledHandleSize(scale),
                    Cursor = Cursors.SizeAll
                },
                // 中心手柄（用于拖动整个圆形）
                new EditHandle
                {
                    Type = HandleType.Center,
                    Position = center,
                    HandleSize = GetScaledHandleSize(scale),
                    Cursor = Cursors.SizeAll
                }
            };

            // 统一计算 Bounds
            foreach (var handle in handles)
            {
                var size = handle.HandleSize;
                handle.Bounds = new Rect(
                    handle.Position.X - size / 2,
                    handle.Position.Y - size / 2,
                    size, size);
            }

            return handles.ToArray();
        }

        /// <summary>
        /// 创建旋转矩形手柄（参考ROI编辑器CreateRotatedRectangleHandles 1131-1213行）
        /// </summary>
        public static EditHandle[] CreateRotatedRectangleHandles(Point[] corners, double rotation, Point bottomCenter, double scale = 1.0, double height = 0)
        {
            var handles = new System.Collections.Generic.List<EditHandle>();

            // 8个缩放手柄（4个角点 + 4个边中点）
            var handleTypes = new HandleType[]
            {
                HandleType.TopLeft, HandleType.Top, HandleType.TopRight,
                HandleType.Right, HandleType.BottomRight, HandleType.Bottom,
                HandleType.BottomLeft, HandleType.Left
            };

            // 计算各边中点
            var topCenter = new Point((corners[0].X + corners[1].X) / 2, (corners[0].Y + corners[1].Y) / 2);
            var rightCenter = new Point((corners[1].X + corners[2].X) / 2, (corners[1].Y + corners[2].Y) / 2);
            var bottomCenterRect = new Point((corners[2].X + corners[3].X) / 2, (corners[2].Y + corners[3].Y) / 2);
            var leftCenter = new Point((corners[3].X + corners[0].X) / 2, (corners[3].Y + corners[0].Y) / 2);

            // 角点和边中点位置
            var positions = new[] { corners[0], topCenter, corners[1], rightCenter, corners[2], bottomCenterRect, corners[3], leftCenter };

            for (int i = 0; i < 8; i++)
            {
                handles.Add(new EditHandle
                {
                    Type = handleTypes[i],
                    Position = positions[i],
                    HandleSize = GetScaledHandleSize(scale),
                    Cursor = GetRotatedCursor(handleTypes[i], rotation)
                });
            }

            // 旋转手柄
            var rectCenter = new Point((corners[0].X + corners[2].X) / 2, (corners[0].Y + corners[2].Y) / 2);
            var rotateHandlePos = CalculateRotationHandlePosition(rectCenter, rotation, scale, height);
            var rotateHandleSize = GetScaledHandleSize(scale);
            handles.Add(new EditHandle
            {
                Type = HandleType.Rotate,
                Position = rotateHandlePos,
                HandleSize = rotateHandleSize,
                Bounds = new Rect(rotateHandlePos.X - rotateHandleSize / 2, rotateHandlePos.Y - rotateHandleSize / 2, rotateHandleSize, rotateHandleSize),
                Cursor = Cursors.Hand
            });

            // 中心手柄
            var center = new Point((corners[0].X + corners[2].X) / 2, (corners[0].Y + corners[2].Y) / 2);
            var centerSize = GetScaledHandleSize(scale);
            handles.Add(new EditHandle
            {
                Type = HandleType.Center,
                Position = center,
                HandleSize = centerSize,
                Bounds = new Rect(center.X - centerSize / 2, center.Y - centerSize / 2, centerSize, centerSize),
                Cursor = Cursors.SizeAll
            });

            // 统一计算非特殊手柄的 Bounds（前8个）
            for (int i = 0; i < 8; i++)
            {
                var size = handles[i].HandleSize;
                handles[i].Bounds = new Rect(
                    handles[i].Position.X - size / 2,
                    handles[i].Position.Y - size / 2,
                    size, size);
            }

            return handles.ToArray();
        }

        /// <summary>
        /// 创建直线手柄（参考ROI编辑器CreateLineHandles 1218-1237行）
        /// </summary>
        public static EditHandle[] CreateLineHandles(Point startPoint, Point endPoint, double scale = 1.0)
        {
            var startSize = GetScaledHandleSize(scale);
            var endSize = GetScaledHandleSize(scale);

            var handles = new[]
            {
                new EditHandle
                {
                    Type = HandleType.LineStart,
                    Position = startPoint,
                    HandleSize = startSize,
                    Bounds = new Rect(startPoint.X - startSize / 2, startPoint.Y - startSize / 2, startSize, startSize),
                    Cursor = Cursors.SizeAll
                },
                new EditHandle
                {
                    Type = HandleType.LineEnd,
                    Position = endPoint,
                    HandleSize = endSize,
                    Bounds = new Rect(endPoint.X - endSize / 2, endPoint.Y - endSize / 2, endSize, endSize),
                    Cursor = Cursors.SizeAll
                }
            };

            return handles;
        }

        /// <summary>
        /// 创建多边形手柄（每个顶点一个可拖拽手柄 + 中心手柄）
        /// </summary>
        public static EditHandle[] CreatePolygonHandles(IList<Point2D> vertices, double scale = 1.0)
        {
            var handles = new List<EditHandle>();
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                var size = GetScaledHandleSize(scale);
                handles.Add(new EditHandle
                {
                    Type = HandleType.PolygonVertex,
                    Position = new Point(v.X, v.Y),
                    HandleSize = size,
                    Bounds = new Rect(v.X - size / 2, v.Y - size / 2, size, size),
                    Cursor = Cursors.SizeAll,
                    VertexIndex = i
                });
            }

            // 中心手柄（各顶点均值）
            if (vertices.Count > 0)
            {
                var centerX = vertices.Average(v => v.X);
                var centerY = vertices.Average(v => v.Y);
                var centerSize = GetScaledHandleSize(scale);
                handles.Add(new EditHandle
                {
                    Type = HandleType.Center,
                    Position = new Point(centerX, centerY),
                    HandleSize = centerSize,
                    Bounds = new Rect(centerX - centerSize / 2, centerY - centerSize / 2, centerSize, centerSize),
                    Cursor = Cursors.SizeAll
                });
            }

            return handles.ToArray();
        }

        /// <summary>
        /// 手柄标记（用于识别和清理）
        /// </summary>
        public const string HandleTag = "EditHandle";

        /// <summary>
        /// 清理Canvas上的所有手柄
        /// </summary>
        public static void ClearHandles(Canvas canvas)
        {
            var toRemove = new List<UIElement>();
            foreach (var child in canvas.Children)
            {
                if (child is Shape shape && shape.Tag as string == HandleTag)
                {
                    toRemove.Add(shape);
                }
            }

            foreach (var item in toRemove)
            {
                canvas.Children.Remove(item);
            }
        }

        /// <summary>
        /// 绘制手柄到Canvas
        /// </summary>
        /// <param name="canvas">目标画布</param>
        /// <param name="handles">手柄数组</param>
        /// <param name="shapeType">形状类型</param>
        /// <param name="scale">缩放比例</param>
        /// <param name="handleColor">手柄颜色（null=默认橙色，根据区域类型传入差异色）</param>
        public static void DrawHandles(Canvas canvas, EditHandle[] handles, ShapeType shapeType, double scale = 1.0, Brush? handleColor = null)
        {
            // 先清理旧手柄
            ClearHandles(canvas);

            // ★ Phase 4: 根据区域类型使用差异化手柄颜色
            Brush defaultFill = handleColor ?? Brushes.Orange;
            Brush defaultStroke = handleColor != null
                ? new SolidColorBrush(Color.FromArgb(255,
                    (byte)(((Color)defaultFill.GetValue(SolidColorBrush.ColorProperty)).R / 2),
                    (byte)(((Color)defaultFill.GetValue(SolidColorBrush.ColorProperty)).G / 2),
                    (byte)(((Color)defaultFill.GetValue(SolidColorBrush.ColorProperty)).B / 2)))
                : Brushes.DarkOrange;

            foreach (var handle in handles)
            {
                var strokeThickness = 1.5 / Math.Max(scale, 0.01);

                // 旋转手柄独立颜色
                var isRotateHandle = handle.Type == HandleType.Rotate;
                var handleShape = new Ellipse
                {
                    Width = handle.HandleSize,
                    Height = handle.HandleSize,
                    Fill = isRotateHandle ? Brushes.Red : defaultFill,
                    Stroke = isRotateHandle ? Brushes.DarkRed : defaultStroke,
                    StrokeThickness = strokeThickness,
                    Tag = HandleTag
                };

                Canvas.SetLeft(handleShape, handle.Position.X - handle.HandleSize / 2);
                Canvas.SetTop(handleShape, handle.Position.Y - handle.HandleSize / 2);
                canvas.Children.Add(handleShape);
            }
        }

        /// <summary>
        /// 计算旋转手柄位置（参考ROI.GetRotationHandlePosition）
        /// 旋转手柄位置 = center + (height/4) * direction
        /// </summary>
        public static Point CalculateRotationHandlePosition(Point center, double rotation, double scale = 1.0, double height = 0)
        {
            // 图像坐标系角度：顺时针为正，0°向下
            var angleRad = rotation * Math.PI / 180;
            var sin = Math.Sin(angleRad);
            var cos = Math.Cos(angleRad);

            // 旋转手柄位置 = center + (height/4) * direction（图像坐标系，无需/scale）
            var handleOffset = height > 0 ? height / 4 : 35;
            var handleX = center.X + handleOffset * sin;
            var handleY = center.Y + handleOffset * cos;

            return new Point(handleX, handleY);
        }

        /// <summary>
        /// 根据旋转角度获取手柄光标（参考ROI编辑器GetCursorForRotatedHandle 1242-1269行）
        /// </summary>
        private static Cursor GetRotatedCursor(HandleType handleType, double rotation)
        {
            // 将旋转角度归一化到0-360
            rotation = ((rotation % 360) + 360) % 360;

            // 根据手柄类型和旋转角度计算光标
            // 角落手柄
            if (handleType == HandleType.TopLeft || handleType == HandleType.BottomRight)
            {
                return GetRotatedCursor(rotation, 45, Cursors.SizeNWSE, Cursors.SizeNESW);
            }
            if (handleType == HandleType.TopRight || handleType == HandleType.BottomLeft)
            {
                return GetRotatedCursor(rotation, 45, Cursors.SizeNESW, Cursors.SizeNWSE);
            }

            // 边中点手柄
            if (handleType == HandleType.Top || handleType == HandleType.Bottom)
            {
                return GetRotatedCursor(rotation, 45, Cursors.SizeNS, Cursors.SizeWE);
            }
            if (handleType == HandleType.Left || handleType == HandleType.Right)
            {
                return GetRotatedCursor(rotation, 45, Cursors.SizeWE, Cursors.SizeNS);
            }

            return Cursors.SizeAll;
        }

        /// <summary>
        /// 获取旋转后的光标
        /// </summary>
        private static Cursor GetRotatedCursor(double rotation, double threshold, Cursor cursor1, Cursor cursor2)
        {
            var normalizedRotation = ((rotation % 90) + 90) % 90;
            return normalizedRotation < threshold || normalizedRotation > (90 - threshold) ? cursor1 : cursor2;
        }

        /// <summary>
        /// 命中测试（圆形距离检测，确保命中范围与手柄视觉大小一致）
        /// </summary>
        /// <param name="point">测试点（图像坐标）</param>
        /// <param name="handles">手柄列表</param>
        /// <param name="toleranceRatio">容差比例：命中半径 = handleSize * (0.5 + toleranceRatio)，默认0.3表示比手柄视觉半径大30%</param>
        public static HandleType HitTestHandle(Point point, EditHandle[] handles, double toleranceRatio = 0)
        {
            EditHandle? closestHandle = null;
            var closestDistance = double.MaxValue;

            foreach (var handle in handles)
            {
                // 计算到手柄中心的距离
                var dx = point.X - handle.Position.X;
                var dy = point.Y - handle.Position.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                
                // 命中半径 = handleSize * (0.5 + toleranceRatio)，即比视觉半径大 toleranceRatio
                var hitRadius = handle.HandleSize * (0.5 + toleranceRatio);
                
                if (distance <= hitRadius && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestHandle = handle;
                }
            }

            return closestHandle?.Type ?? HandleType.None;
        }
    }
}
