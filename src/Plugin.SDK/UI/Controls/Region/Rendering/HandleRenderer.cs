using System;
using System.Collections.Generic;
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
        LineEnd
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
        public double HandleSize { get; set; } = 12;
    }

    /// <summary>
    /// 手柄渲染器 - 参考ROI编辑器的手柄绘制逻辑
    /// </summary>
    public class HandleRenderer
    {
        private const double DefaultHandleSize = 12;

        /// <summary>
        /// 创建矩形手柄（参考ROI编辑器CreateRectangleHandles 1071-1097行）
        /// </summary>
        public static EditHandle[] CreateRectangleHandles(Rect bounds, double handleSize = DefaultHandleSize)
        {
            var handles = new List<EditHandle>
            {
                // 4个角点手柄
                new EditHandle
                {
                    Type = HandleType.TopLeft,
                    Position = new Point(bounds.Left, bounds.Top),
                    Bounds = new Rect(bounds.Left - handleSize / 2, bounds.Top - handleSize / 2, handleSize, handleSize),
                    Cursor = Cursors.SizeNWSE
                },
                new EditHandle
                {
                    Type = HandleType.TopRight,
                    Position = new Point(bounds.Right, bounds.Top),
                    Bounds = new Rect(bounds.Right - handleSize / 2, bounds.Top - handleSize / 2, handleSize, handleSize),
                    Cursor = Cursors.SizeNESW
                },
                new EditHandle
                {
                    Type = HandleType.BottomLeft,
                    Position = new Point(bounds.Left, bounds.Bottom),
                    Bounds = new Rect(bounds.Left - handleSize / 2, bounds.Bottom - handleSize / 2, handleSize, handleSize),
                    Cursor = Cursors.SizeNESW
                },
                new EditHandle
                {
                    Type = HandleType.BottomRight,
                    Position = new Point(bounds.Right, bounds.Bottom),
                    Bounds = new Rect(bounds.Right - handleSize / 2, bounds.Bottom - handleSize / 2, handleSize, handleSize),
                    Cursor = Cursors.SizeNWSE
                },
                // 4个边中点手柄
                new EditHandle
                {
                    Type = HandleType.Top,
                    Position = new Point(bounds.Left + bounds.Width / 2, bounds.Top),
                    Bounds = new Rect(bounds.Left + bounds.Width / 2 - handleSize / 2, bounds.Top - handleSize / 2, handleSize, handleSize),
                    Cursor = Cursors.SizeNS
                },
                new EditHandle
                {
                    Type = HandleType.Bottom,
                    Position = new Point(bounds.Left + bounds.Width / 2, bounds.Bottom),
                    Bounds = new Rect(bounds.Left + bounds.Width / 2 - handleSize / 2, bounds.Bottom - handleSize / 2, handleSize, handleSize),
                    Cursor = Cursors.SizeNS
                },
                new EditHandle
                {
                    Type = HandleType.Left,
                    Position = new Point(bounds.Left, bounds.Top + bounds.Height / 2),
                    Bounds = new Rect(bounds.Left - handleSize / 2, bounds.Top + bounds.Height / 2 - handleSize / 2, handleSize, handleSize),
                    Cursor = Cursors.SizeWE
                },
                new EditHandle
                {
                    Type = HandleType.Right,
                    Position = new Point(bounds.Right, bounds.Top + bounds.Height / 2),
                    Bounds = new Rect(bounds.Right - handleSize / 2, bounds.Top + bounds.Height / 2 - handleSize / 2, handleSize, handleSize),
                    Cursor = Cursors.SizeWE
                },
                // 中心手柄（用于拖动整个矩形）
                new EditHandle
                {
                    Type = HandleType.Center,
                    Position = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2),
                    Bounds = new Rect(bounds.X + bounds.Width / 2 - handleSize / 2, bounds.Y + bounds.Height / 2 - handleSize / 2, handleSize, handleSize),
                    Cursor = Cursors.SizeAll
                }
            };

            return handles.ToArray();
        }

        /// <summary>
        /// 创建圆形手柄（参考ROI编辑器CreateCircleHandles 1102-1126行）
        /// </summary>
        public static EditHandle[] CreateCircleHandles(Point center, double radius, double handleSize = DefaultHandleSize)
        {
            var handles = new List<EditHandle>
            {
                new EditHandle
                {
                    Type = HandleType.Top,
                    Position = new Point(center.X, center.Y - radius),
                    Bounds = new Rect(center.X - handleSize / 2, center.Y - radius - handleSize / 2, handleSize, handleSize),
                    Cursor = Cursors.SizeAll
                },
                new EditHandle
                {
                    Type = HandleType.Bottom,
                    Position = new Point(center.X, center.Y + radius),
                    Bounds = new Rect(center.X - handleSize / 2, center.Y + radius - handleSize / 2, handleSize, handleSize),
                    Cursor = Cursors.SizeAll
                },
                new EditHandle
                {
                    Type = HandleType.Left,
                    Position = new Point(center.X - radius, center.Y),
                    Bounds = new Rect(center.X - radius - handleSize / 2, center.Y - handleSize / 2, handleSize, handleSize),
                    Cursor = Cursors.SizeAll
                },
                new EditHandle
                {
                    Type = HandleType.Right,
                    Position = new Point(center.X + radius, center.Y),
                    Bounds = new Rect(center.X + radius - handleSize / 2, center.Y - handleSize / 2, handleSize, handleSize),
                    Cursor = Cursors.SizeAll
                },
                // 中心手柄（用于拖动整个圆形）
                new EditHandle
                {
                    Type = HandleType.Center,
                    Position = center,
                    Bounds = new Rect(center.X - handleSize / 2, center.Y - handleSize / 2, handleSize, handleSize),
                    Cursor = Cursors.SizeAll
                }
            };

            return handles.ToArray();
        }

        /// <summary>
        /// 创建旋转矩形手柄（参考ROI编辑器CreateRotatedRectangleHandles 1131-1213行）
        /// </summary>
        public static EditHandle[] CreateRotatedRectangleHandles(Point[] corners, double rotation, Point bottomCenter, double handleSize = DefaultHandleSize)
        {
            var handles = new System.Collections.Generic.List<EditHandle>();

            // 8个缩放手柄（4个角点 + 4个边中点）
            // 角点顺序：TopLeft, TopRight, BottomRight, BottomLeft
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

            // 角点手柄
            var positions = new[] { corners[0], topCenter, corners[1], rightCenter, corners[2], bottomCenterRect, corners[3], leftCenter };

            for (int i = 0; i < 8; i++)
            {
                handles.Add(new EditHandle
                {
                    Type = handleTypes[i],
                    Position = positions[i],
                    Bounds = new Rect(positions[i].X - handleSize / 2, positions[i].Y - handleSize / 2, handleSize, handleSize),
                    Cursor = GetRotatedCursor(handleTypes[i], rotation)
                });
            }

            // 旋转手柄（在下边中点下方）
            var rotateHandlePos = CalculateRotationHandlePosition(bottomCenterRect, rotation);
            handles.Add(new EditHandle
            {
                Type = HandleType.Rotate,
                Position = rotateHandlePos,
                Bounds = new Rect(rotateHandlePos.X - handleSize / 2, rotateHandlePos.Y - handleSize / 2, handleSize, handleSize),
                Cursor = Cursors.Hand
            });

            // 中心手柄（用于拖动整个旋转矩形）
            var center = new Point((corners[0].X + corners[2].X) / 2, (corners[0].Y + corners[2].Y) / 2);
            handles.Add(new EditHandle
            {
                Type = HandleType.Center,
                Position = center,
                Bounds = new Rect(center.X - handleSize / 2, center.Y - handleSize / 2, handleSize, handleSize),
                Cursor = Cursors.SizeAll
            });

            return handles.ToArray();
        }

        /// <summary>
        /// 创建直线手柄（参考ROI编辑器CreateLineHandles 1218-1237行）
        /// </summary>
        public static EditHandle[] CreateLineHandles(Point startPoint, Point endPoint, double handleSize = DefaultHandleSize)
        {
            var handles = new[]
            {
                new EditHandle
                {
                    Type = HandleType.LineStart,
                    Position = startPoint,
                    Bounds = new Rect(startPoint.X - handleSize / 2, startPoint.Y - handleSize / 2, handleSize, handleSize),
                    Cursor = Cursors.SizeAll
                },
                new EditHandle
                {
                    Type = HandleType.LineEnd,
                    Position = endPoint,
                    Bounds = new Rect(endPoint.X - handleSize / 2, endPoint.Y - handleSize / 2, handleSize, handleSize),
                    Cursor = Cursors.SizeAll
                }
            };

            return handles;
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

            // ★ 诊断日志：手柄清理
            if (toRemove.Count > 0)
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[HandleRenderer.ClearHandles] 清理手柄 | HandleCount={toRemove.Count} | CanvasChildren={canvas.Children.Count}",
                    "HandleRenderer");
            }

            foreach (var item in toRemove)
            {
                canvas.Children.Remove(item);
            }
        }

        /// <summary>
        /// 绘制手柄到Canvas（参考ROI编辑器DrawEditHandles 1281-1347行）
        /// </summary>
        public static void DrawHandles(Canvas canvas, EditHandle[] handles, ShapeType shapeType)
        {
            // ★ 诊断日志：手柄绘制
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[HandleRenderer.DrawHandles] 绘制手柄 | HandleCount={handles?.Length ?? 0} | ShapeType={shapeType} | CanvasChildren={canvas.Children.Count}",
                "HandleRenderer");

            // 先清理旧手柄
            ClearHandles(canvas);

            foreach (var handle in handles)
            {
                Shape handleShape;

                // 旋转手柄使用圆形，其他根据形状类型决定
                if (handle.Type == HandleType.Rotate)
                {
                    handleShape = new Ellipse
                    {
                        Width = handle.HandleSize,
                        Height = handle.HandleSize,
                        Fill = Brushes.LightGreen,
                        Stroke = Brushes.Green,
                        StrokeThickness = 1.5,
                        Tag = HandleTag
                    };
                }
                else if (shapeType == ShapeType.Circle || shapeType == ShapeType.Line)
                {
                    // 圆形和直线端点使用圆形形状
                    handleShape = new Ellipse
                    {
                        Width = handle.HandleSize,
                        Height = handle.HandleSize,
                        Fill = Brushes.White,
                        Stroke = Brushes.Blue,
                        StrokeThickness = 1.5,
                        Tag = HandleTag
                    };
                }
                else
                {
                    // 矩形和旋转矩形使用方形手柄
                    handleShape = new Rectangle
                    {
                        Width = handle.HandleSize,
                        Height = handle.HandleSize,
                        Fill = Brushes.White,
                        Stroke = Brushes.Blue,
                        StrokeThickness = 1.5,
                        Tag = HandleTag
                    };
                }

                Canvas.SetLeft(handleShape, handle.Position.X - handle.HandleSize / 2);
                Canvas.SetTop(handleShape, handle.Position.Y - handle.HandleSize / 2);
                canvas.Children.Add(handleShape);
            }
        }

        /// <summary>
        /// 计算旋转手柄位置（参考ROI.GetRotationHandlePosition）
        /// </summary>
        public static Point CalculateRotationHandlePosition(Point bottomCenter, double rotation)
        {
            // 图像坐标系角度：顺时针为正，0°向下
            var angleRad = rotation * Math.PI / 180;
            var sin = Math.Sin(angleRad);
            var cos = Math.Cos(angleRad);

            // 旋转手柄位于下边中点，沿下方方向向外偏移35像素
            const double handleOffset = 35;
            var handleX = bottomCenter.X + handleOffset * sin;
            var handleY = bottomCenter.Y + handleOffset * cos;

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
        /// 命中测试（参考ROI编辑器HitTestHandle 1023-1045行）
        /// </summary>
        public static HandleType HitTestHandle(Point point, EditHandle[] handles, double tolerance = 8)
        {
            foreach (var handle in handles)
            {
                var expandedBounds = new Rect(
                    handle.Bounds.X - tolerance,
                    handle.Bounds.Y - tolerance,
                    handle.Bounds.Width + tolerance * 2,
                    handle.Bounds.Height + tolerance * 2);

                if (expandedBounds.Contains(point))
                {
                    return handle.Type;
                }
            }

            return HandleType.None;
        }
    }
}
