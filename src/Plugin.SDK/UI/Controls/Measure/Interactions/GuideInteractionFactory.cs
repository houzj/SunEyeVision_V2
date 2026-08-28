using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Interactions
{
    /// <summary>
    /// 引导几何交互策略工厂
    /// </summary>
    /// <remarks>
    /// 按几何类型返回对应交互策略。未注册类型直接抛异常（rule-015 报错优先，不静默回退）。
    /// </remarks>
    public static class GuideInteractionFactory
    {
        private static readonly Dictionary<MeasureGeometryType, IGuideGeometryInteraction> _interactions =
            new()
            {
                [MeasureGeometryType.Line] = new LineGuideInteraction(),
                [MeasureGeometryType.Circle] = new CircleGuideInteraction(),
                [MeasureGeometryType.Arc] = new ArcGuideInteraction(),
            };

        /// <summary>
        /// 获取指定几何类型的交互策略
        /// </summary>
        /// <param name="geometryType">几何类型</param>
        /// <returns>对应的交互策略</returns>
        /// <exception cref="InvalidOperationException">未注册的几何类型</exception>
        public static IGuideGeometryInteraction Get(MeasureGeometryType geometryType)
        {
            if (_interactions.TryGetValue(geometryType, out var interaction))
                return interaction;
            throw new InvalidOperationException(
                $"未注册的引导几何交互策略: {geometryType}（rule-015 报错优先，请先在 GuideInteractionFactory 注册）");
        }
    }
}
