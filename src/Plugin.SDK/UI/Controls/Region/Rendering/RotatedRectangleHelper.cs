using System;
using System.Windows;
using System.Windows.Media;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Rendering
{
    /// <summary>
    /// 旋转矩形辅助类 - 参考ROI.cs中的旋转矩形计算方法
    /// </summary>
    public class RotatedRectangleHelper
    {
        /// <summary>
        /// 规范化角度到[-180°, 180°]（参考ROI.NormalizeAngle 325-336行）
        /// 图像坐标系角度定义：顺时针为正，0°表示箭头向下（+Y方向）
        /// </summary>
        public static double NormalizeAngle(double angle)
        {
            // 模360得到[0, 360)
            angle = angle % 360;
            if (angle < 0) angle += 360;

            // 转换到[-180°, 180°]
            if (angle > 180)
                angle -= 360;

            return angle;
        }

        /// <summary>
        /// 获取旋转矩形的四个角点（世界坐标）
        /// 参考ROI.GetCorners 345-381行
        /// 角点顺序：TopLeft, TopRight, BottomRight, BottomLeft
        /// 尺寸语义：Width垂直于箭头方向，Height沿箭头方向
        /// 图像坐标系角度：0°时箭头向下，90°向右，180°向上，-90°向左（顺时针为正）
        /// </summary>
        public static Point[] GetCorners(Point center, double width, double height, double rotation)
        {
            // 图像坐标系角度：顺时针为正，0°向下
            var angleRad = rotation * Math.PI / 180;
            var cos = Math.Cos(angleRad);
            var sin = Math.Sin(angleRad);

            // 本地坐标（未旋转）的四个角点
            var hw = width / 2;
            var hh = height / 2;

            // 应用旋转变换得到世界坐标
            Point Transform(double localX, double localY)
            {
                return new Point(
                    center.X + localX * cos + localY * sin,
                    center.Y - localX * sin + localY * cos
                );
            }

            return new Point[]
            {
                Transform(-hw, -hh),  // TopLeft
                Transform( hw, -hh),  // TopRight
                Transform( hw,  hh),  // BottomRight
                Transform(-hw,  hh)   // BottomLeft
            };
        }

        /// <summary>
        /// 获取方向箭头几何数据
        /// 参考ROI.GetDirectionArrow 390-411行
        /// 箭头从中心沿高度方向指向下边中点
        /// 尺寸语义：Height沿箭头方向（高度），Width垂直于箭头方向（宽度）
        /// 图像坐标系角度：0°时箭头向下，90°向右，180°向上，-90°向左（顺时针为正）
        /// </summary>
        public static (Point Start, Point End) GetDirectionArrow(Point center, double height, double rotation)
        {
            // 图像坐标系角度：顺时针为正，0°向下
            var angleRad = rotation * Math.PI / 180;
            var sin = Math.Sin(angleRad);
            var cos = Math.Cos(angleRad);

            // 箭头终点：沿高度方向指向下边中点
            // 箭头方向向量：(sin, cos) - 箭头朝下
            // 下边中点距离中心：h/2
            var arrowEndX = center.X + (height / 2) * sin;
            var arrowEndY = center.Y + (height / 2) * cos;

            return (center, new Point(arrowEndX, arrowEndY));
        }

        /// <summary>
        /// 获取旋转手柄位置
        /// 参考ROI.GetRotationHandlePosition 420-446行
        /// 手柄位于下边中点，然后沿下方方向向外偏移35像素
        /// </summary>
        public static Point GetRotationHandlePosition(Point center, double height, double rotation)
        {
            // 图像坐标系角度：顺时针为正，0°向下
            var angleRad = rotation * Math.PI / 180;
            var sin = Math.Sin(angleRad);
            var cos = Math.Cos(angleRad);

            // 下边中点坐标（与箭头方向一致）
            var bottomCenterX = center.X + (height / 2) * sin;
            var bottomCenterY = center.Y + (height / 2) * cos;

            // 旋转手柄位于下边中点，沿下方方向向外偏移35像素
            const double handleOffset = 35;
            var handleX = bottomCenterX + handleOffset * sin;
            var handleY = bottomCenterY + handleOffset * cos;

            return new Point(handleX, handleY);
        }

        /// <summary>
        /// 获取旋转矩形精确的轴对齐包围盒
        /// 参考ROI.GetRotatedBoundingBox 452-474行
        /// </summary>
        public static Rect GetRotatedBoundingBox(Point[] corners)
        {
            if (corners.Length != 4)
                return new Rect();

            var minX = double.MaxValue;
            var minY = double.MaxValue;
            var maxX = double.MinValue;
            var maxY = double.MinValue;

            foreach (var corner in corners)
            {
                if (corner.X < minX) minX = corner.X;
                if (corner.Y < minY) minY = corner.Y;
                if (corner.X > maxX) maxX = corner.X;
                if (corner.Y > maxY) maxY = corner.Y;
            }

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// 辅助元素标记（与手柄相同，统一清理）
        /// </summary>
        public const string HelperTag = HandleRenderer.HandleTag;

        /// <summary>
        /// 方向箭头专用标记（避免被手柄清理）
        /// </summary>
        public const string DirectionArrowTag = "DirectionArrow";

        /// <summary>
        /// 绘制方向箭头（参考ROI编辑器DrawDirectionArrow 1354-1425行）
        /// </summary>
        public static void DrawDirectionArrow(System.Windows.Controls.Canvas canvas, Point center, Point topCenter, Brush strokeBrush)
        {
            // 检查尺寸是否足够绘制箭头
            if (center.X == topCenter.X && center.Y == topCenter.Y) return;

            // 绘制主箭头线（从中心到顶边中点）
            var arrowLine = new System.Windows.Shapes.Line
            {
                X1 = center.X,
                Y1 = center.Y,
                X2 = topCenter.X,
                Y2 = topCenter.Y,
                Stroke = strokeBrush,
                StrokeThickness = 15,
                Tag = DirectionArrowTag
            };
            canvas.Children.Add(arrowLine);

            // 计算箭头方向
            var dx = topCenter.X - center.X;
            var dy = topCenter.Y - center.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 5) return;

            // 归一化方向向量
            var ux = dx / length;
            var uy = dy / length;

            // 箭头参数
            var arrowSize = 10;
            var arrowAngle = 25 * Math.PI / 180; // 箭头夹角25°

            // 计算箭头两翼的端点
            var cosA = Math.Cos(arrowAngle);
            var sinA = Math.Sin(arrowAngle);

            // 左翼（逆时针旋转）
            var leftX = topCenter.X - arrowSize * (ux * cosA + uy * sinA);
            var leftY = topCenter.Y - arrowSize * (-ux * sinA + uy * cosA);

            // 右翼（顺时针旋转）
            var rightX = topCenter.X - arrowSize * (ux * cosA - uy * sinA);
            var rightY = topCenter.Y - arrowSize * (ux * sinA + uy * cosA);

            // 绘制箭头两翼
            var leftWing = new System.Windows.Shapes.Line
            {
                X1 = topCenter.X,
                Y1 = topCenter.Y,
                X2 = leftX,
                Y2 = leftY,
                Stroke = strokeBrush,
                StrokeThickness = 15,
                Tag = DirectionArrowTag
            };
            canvas.Children.Add(leftWing);

            var rightWing = new System.Windows.Shapes.Line
            {
                X1 = topCenter.X,
                Y1 = topCenter.Y,
                X2 = rightX,
                Y2 = rightY,
                Stroke = strokeBrush,
                StrokeThickness = 15,
                Tag = DirectionArrowTag
            };
            canvas.Children.Add(rightWing);
        }

        /// <summary>
        /// 绘制旋转手柄连接线（参考ROI编辑器DrawRotateHandleLine 1431-1459行）
        /// </summary>
        public static void DrawRotateHandleLine(System.Windows.Controls.Canvas canvas, Point center, double height, double rotation, Point rotateHandlePos)
        {
            // 先绘制方向箭头
            var arrow = GetDirectionArrow(center, height, rotation);
            DrawDirectionArrow(canvas, arrow.Start, arrow.End, new SolidColorBrush(Color.FromRgb(0, 0, 255)));

            // 绘制从下边中点到旋转手柄的连接线（虚线）
            var angleRad = rotation * Math.PI / 180;
            var sin = Math.Sin(angleRad);
            var cos = Math.Cos(angleRad);

            var bottomCenterX = center.X + (height / 2) * sin;
            var bottomCenterY = center.Y + (height / 2) * cos;
            var bottomCenter = new Point(bottomCenterX, bottomCenterY);

            var connectorLine = new System.Windows.Shapes.Line
            {
                X1 = bottomCenter.X,
                Y1 = bottomCenter.Y,
                X2 = rotateHandlePos.X,
                Y2 = rotateHandlePos.Y,
                Stroke = Brushes.Blue,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 2 },
                Tag = DirectionArrowTag
            };
            canvas.Children.Add(connectorLine);
        }
    }
}
