using System.Windows;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Common.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Rendering;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Interactions
{
    /// <summary>
    /// 直线引导交互策略
    /// </summary>
    public class LineGuideInteraction : IGuideGeometryInteraction
    {
        /// <inheritdoc/>
        public MeasureGeometryType GeometryType => MeasureGeometryType.Line;

        /// <inheritdoc/>
        public bool UpdatePreview(MeasureInteractionModel model, Point start, Point current)
        {
            if ((current - start).Length <= 1e-6) return false;
            model.SetLine(Line2d.FromTwoPoints(start, current), start, current);
            return true;
        }

        /// <inheritdoc/>
        public bool Complete(MeasureInteractionModel model, Point start, Point current)
        {
            if ((current - start).Length <= 1e-3) return false;
            model.SetLine(Line2d.FromTwoPoints(start, current), start, current);
            return true;
        }

        /// <inheritdoc/>
        public bool EditHandle(MeasureInteractionModel model, HandleType handleType, Point current)
        {
            if (!model.EditingLine.HasValue) return false;
            var start = model.LineStartPoint;
            var end = model.LineEndPoint;
            if (handleType == HandleType.LineStart)
                return model.UpdateLineEndpoints(current, end);
            if (handleType == HandleType.LineEnd)
                return model.UpdateLineEndpoints(start, current);
            return false;
        }

        /// <inheritdoc/>
        public EditHandle[] CreateHandles(MeasureInteractionModel model, double zoom)
        {
            return HandleRenderer.CreateLineHandles(model.LineStartPoint, model.LineEndPoint, zoom);
        }

        /// <inheritdoc/>
        public bool HitTestGuide(MeasureInteractionModel model, Point point, double tolerance)
        {
            if (!model.EditingLine.HasValue) return false;
            return GeometryToolkit.DistanceToSegment(point, model.LineStartPoint, model.LineEndPoint) <= tolerance;
        }
    }
}
