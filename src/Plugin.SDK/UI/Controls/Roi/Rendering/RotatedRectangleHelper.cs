using System;
using System.Windows;
using System.Windows.Media;

namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Rendering
{
    /// <summary>
    /// 旋转矩形辅助类 - 参考ROI.cs中的旋转矩形计算方法
    /// </summary>
    public class RotatedRectangleHelper
    {
        /// <summary>
        /// 规范化角度到[-180°, 180°]（参考ROI.NormalizeAngle 325-336行）
        /// 屏幕角度系定义：顺时针为正，0°表示箭头向右（+X方向）
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
        /// 屏幕角度系：0°时箭头向右，+90°向下，180°向左，-90°向上（顺时针为正）
        /// </summary>
        public static Point[] GetCorners(Point center, double width, double height, double rotation)
        {
            // 屏幕角度系：顺时针为正，0°向右
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
                    center.X + localX * cos - localY * sin,
                    center.Y + localX * sin + localY * cos
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
        /// 屏幕角度系：0°时箭头向右，+90°向下，180°向左，-90°向上（顺时针为正）
        /// </summary>
        public static (Point Start, Point End) GetDirectionArrow(Point center, double width, double rotation)
        {
            // 屏幕角度系：顺时针为正，0°向右
            var angleRad = rotation * Math.PI / 180;
            var cos = Math.Cos(angleRad);
            var sin = Math.Sin(angleRad);

            // 箭头终点：沿宽度方向穿出矩形前端，长度为宽度的一半
            // 箭头方向向量：(cos, sin) - 屏幕角度系，0°时箭头朝右
            var arrowLength = width / 2;
            var arrowEndX = center.X + arrowLength * cos;
            var arrowEndY = center.Y + arrowLength * sin;

            return (center, new Point(arrowEndX, arrowEndY));
        }

        /// <summary>
        /// 获取旋转手柄位置
        /// 参考ROI.GetRotationHandlePosition 420-446行
        /// 旋转手柄位置 = center + (height/4) * direction
        /// </summary>
        public static Point GetRotationHandlePosition(Point center, double width, double rotation, double scale = 1.0)
        {
            // 屏幕角度系：顺时针为正，0°向右
            var angleRad = rotation * Math.PI / 180;
            var sin = Math.Sin(angleRad);
            var cos = Math.Cos(angleRad);

            // 旋转手柄位置 = center + (width/4) * direction（箭头中点，屏幕角度系，无需/scale）
            var handleOffset = width / 4;
            var handleX = center.X + handleOffset * cos;
            var handleY = center.Y + handleOffset * sin;

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

    }
}
