using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 区域掩码构建器 - 将区域定义绘制到 OpenCV 掩码
    /// </summary>
    public class RegionMaskBuilder : IDisposable
    {
        private readonly Size _imageSize;
        private readonly Mat _mask;

        /// <summary>
        /// 创建检测区域掩码（Inspection Mask）
        /// </summary>
        /// <param name="imageSize">图像尺寸</param>
        /// <param name="inspectionRegions">检测区域列表</param>
        /// <returns>
        /// ROI掩码：
        /// - 空列表 → 全白（整图有效）
        /// - 非空 → 黑底+白色区域
        /// </returns>
        public static Mat CreateInspectionMask(Size imageSize, IEnumerable<UI.Controls.Region.Models.RegionData> inspectionRegions)
        {
            return CreateMask(imageSize, inspectionRegions);
        }

        /// <summary>
        /// 创建遮罩区域掩码（Mask Mask）
        /// </summary>
        /// <param name="imageSize">图像尺寸</param>
        /// <param name="maskRegions">遮罩区域列表</param>
        /// <returns>
        /// 遮罩掩码：
        /// - 空列表 → 全黑（无遮罩）
        /// - 非空 → 黑底+白色遮罩区域
        /// </returns>
        public static Mat CreateMaskMask(Size imageSize, IEnumerable<UI.Controls.Region.Models.RegionData> maskRegions)
        {
            var regionList = maskRegions?.ToList() ?? new List<UI.Controls.Region.Models.RegionData>();

            if (regionList.Count == 0)
                return new Mat(imageSize, MatType.CV_8UC1, Scalar.Black);

            using var builder = new RegionMaskBuilder(imageSize);
            builder.AddRegions(regionList);
            return builder.GetMask();
        }

        /// <summary>
        /// 从区域列表快速创建掩码（一行代码版本）
        /// </summary>
        /// <param name="imageSize">图像尺寸</param>
        /// <param name="regions">区域列表</param>
        /// <returns>ROI掩码（调用者负责释放）</returns>
        public static Mat CreateMask(Size imageSize, IEnumerable<UI.Controls.Region.Models.RegionData> regions)
        {
            var regionList = regions?.ToList() ?? new List<UI.Controls.Region.Models.RegionData>();

            if (regionList.Count == 0)
            {
                return new Mat(imageSize, MatType.CV_8UC1, Scalar.White);
            }

            using var builder = new RegionMaskBuilder(imageSize);
            builder.AddRegions(regionList);
            return builder.GetMask();
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="imageSize">图像尺寸</param>
        public RegionMaskBuilder(Size imageSize)
        {
            _imageSize = imageSize;
            _mask = new Mat(imageSize, MatType.CV_8UC1, Scalar.Black);
        }

        /// <summary>
        /// 获取构建的掩码（返回克隆副本，调用者负责释放）
        /// </summary>
        public Mat GetMask()
        {
            return _mask.Clone();
        }

        /// <summary>
        /// 添加单个区域到掩码
        /// </summary>
        /// <param name="region">区域数据</param>
        public void AddRegion(RegionData region)
        {
            if (!region.IsEnabled || region.Parameters == null)
                return;

            var shape = region.Parameters;
            var shapeType = shape.GetShapeType();

            if (!shapeType.HasValue)
                return;

            switch (shapeType.Value)
            {
                case ShapeType.Rectangle:
                    DrawRectangle(shape);
                    break;
                case ShapeType.Circle:
                    DrawCircle(shape);
                    break;
                case ShapeType.Polygon:
                    DrawPolygon(shape);
                    break;
                case ShapeType.Line:
                    // 直线不适合做掩码区域，记录警告
                    break;
                case ShapeType.Point:
                    // 点不适合做掩码区域，记录警告
                    break;
                case ShapeType.RotatedRectangle:
                    DrawRotatedRectangle(shape);
                    break;
                case ShapeType.Annulus:
                    DrawAnnulus(shape);
                    break;
                case ShapeType.Arc:
                    // 弧形不适合做掩码区域，记录警告
                    break;
            }
        }

        /// <summary>
        /// 添加多个区域到掩码
        /// </summary>
        public void AddRegions(IEnumerable<RegionData> regions)
        {
            foreach (var region in regions)
            {
                AddRegion(region);
            }
        }

        /// <summary>
        /// 绘制矩形
        /// </summary>
        private void DrawRectangle(RegionDefinition shape)
        {
            if (shape is not ShapeParameters shapeParams)
                return;

            var x = (int)(shapeParams.CenterX - shapeParams.Width / 2);
            var y = (int)(shapeParams.CenterY - shapeParams.Height / 2);
            var rect = new Rect(x, y, (int)shapeParams.Width, (int)shapeParams.Height);

            Cv2.Rectangle(_mask, rect, Scalar.White, -1);
        }

        /// <summary>
        /// 绘制圆形
        /// </summary>
        private void DrawCircle(RegionDefinition shape)
        {
            if (shape is not ShapeParameters shapeParams)
                return;

            var center = new Point((int)shapeParams.CenterX, (int)shapeParams.CenterY);
            var radius = (int)shapeParams.Radius;

            Cv2.Circle(_mask, center, radius, Scalar.White, -1, LineTypes.Link8, 0);
        }

        /// <summary>
        /// 绘制旋转矩形
        /// </summary>
        private void DrawRotatedRectangle(RegionDefinition shape)
        {
            if (shape is not ShapeParameters shapeParams)
                return;

            var center = new Point2f((float)shapeParams.CenterX, (float)shapeParams.CenterY);
            var size = new Size2f((float)shapeParams.Width, (float)shapeParams.Height);
            var angle = (float)(shapeParams.Angle * 180.0 / Math.PI); // 转换为度数

            var rotatedRect = new RotatedRect(center, size, angle);

            var points = rotatedRect.Points();

            // 转换为 Point 数组
            var pointArray = new OpenCvSharp.Point[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                pointArray[i] = new OpenCvSharp.Point((int)points[i].X, (int)points[i].Y);
            }

            Cv2.FillPoly(_mask, new[] { pointArray }, Scalar.White);
        }

        /// <summary>
        /// 绘制多边形
        /// </summary>
        private void DrawPolygon(RegionDefinition shape)
        {
            if (shape is not ShapeParameters shapeParams)
                return;

            if (shapeParams.Points == null || shapeParams.Points.Count < 3)
                return;

            var pointArray = new OpenCvSharp.Point[shapeParams.Points.Count];
            for (int i = 0; i < shapeParams.Points.Count; i++)
            {
                pointArray[i] = new OpenCvSharp.Point(
                    (int)shapeParams.Points[i].X,
                    (int)shapeParams.Points[i].Y
                );
            }

            Cv2.FillPoly(_mask, new[] { pointArray }, Scalar.White);
        }

        /// <summary>
        /// 绘制圆环
        /// </summary>
        private void DrawAnnulus(RegionDefinition shape)
        {
            if (shape is not ShapeParameters shapeParams)
                return;

            var center = new Point((int)shapeParams.CenterX, (int)shapeParams.CenterY);
            var innerRadius = (int)shapeParams.Radius;
            var outerRadius = (int)shapeParams.OuterRadius;

            if (outerRadius <= 0)
                outerRadius = innerRadius + 20; // 默认外环

            // 绘制外圆
            Cv2.Circle(_mask, center, outerRadius, Scalar.White, -1, LineTypes.Link8, 0);

            // 绘制内圆（黑色，实现圆环效果）
            Cv2.Circle(_mask, center, innerRadius, Scalar.Black, -1, LineTypes.Link8, 0);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _mask?.Dispose();
        }
    }
}
