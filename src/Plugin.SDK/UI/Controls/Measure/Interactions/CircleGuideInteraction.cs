using System;
using System.Windows;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Rendering;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Interactions
{
    /// <summary>
    /// 圆引导交互策略
    /// </summary>
    public class CircleGuideInteraction : IGuideGeometryInteraction
    {
        /// <inheritdoc/>
        public MeasureGeometryType GeometryType => MeasureGeometryType.Circle;

        /// <inheritdoc/>
        public bool UpdatePreview(MeasureInteractionModel model, Point start, Point current)
        {
            var radius = (current - start).Length;
            if (radius <= 1e-6) return false;
            model.SetCircle(new Circle2d(start, radius));
            return true;
        }

        /// <inheritdoc/>
        public bool Complete(MeasureInteractionModel model, Point start, Point current)
        {
            var radius = (current - start).Length;
            if (radius <= 1e-3) return false;
            model.SetCircle(new Circle2d(start, radius));
            return true;
        }

        /// <inheritdoc/>
        public bool EditHandle(MeasureInteractionModel model, HandleType handleType, Point current)
        {
            if (!model.EditingCircle.HasValue) return false;
            if (handleType != HandleType.Left && handleType != HandleType.Right &&
                handleType != HandleType.Top && handleType != HandleType.Bottom)
                return false;
            var center = model.EditingCircle.Value.Center;
            return model.UpdateCircleRadius((current - center).Length);
        }

        /// <inheritdoc/>
        public EditHandle[] CreateHandles(MeasureInteractionModel model, double zoom)
        {
            if (!model.EditingCircle.HasValue) return Array.Empty<EditHandle>();
            var c = model.EditingCircle.Value;
            return HandleRenderer.CreateCircleHandles(c.Center, c.Radius, zoom);
        }

        /// <inheritdoc/>
        public bool HitTestGuide(MeasureInteractionModel model, Point point, double tolerance)
        {
            if (!model.EditingCircle.HasValue) return false;
            var c = model.EditingCircle.Value;
            // 对齐 Roi 编辑器 IsPointInCircle：命中整个圆形区域（含内部），而非仅圆周
            return (point - c.Center).Length <= c.Radius + tolerance;
        }
    }
}
