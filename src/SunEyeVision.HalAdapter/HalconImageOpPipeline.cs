using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;

namespace SunEyeVision.HalAdapter
{
    /// <summary>
    /// 图像处理式算法工具公共前置管线（可选组件，供图像阈值化/图像滤波/图像二值化/形态学处理/Blob 等复用）。
    /// 只做"机械管线"，不承载区域启用策略：
    ///   区域是否应用由调用方决定——传入空区域列表 = 全图；传入已配置区域 = 有区域即限制（语义统一）。
    /// 本类型仅对齐"图像处理式"形态（灰度 + ROI/掩码 + ReduceDomain）；Metrology/匹配类工具前置语义不同，不适用。
    /// </summary>
    public static class HalconImageOpPipeline
    {
        /// <summary>
        /// 区域掩码预处理结果（所有权随实例释放）。
        /// </summary>
        public sealed class MaskedImage : IDisposable
        {
            /// <summary>核心算子输入图像（有区域时已 ReduceDomain；无区域时为全图 Clone）。所有权归本实例。</summary>
            public HImage WorkingImage { get; }

            /// <summary>应用了区域限制时的处理域（无区域 = null）</summary>
            public HRegion? RoiRegion { get; }

            /// <summary>检测区域合并（可视化/诊断用；无区域 = null）</summary>
            public HRegion? InspectionRegion { get; }

            /// <summary>屏蔽区域合并（可视化/诊断用；无区域 = null）</summary>
            public HRegion? MaskRegion { get; }

            /// <summary>分步耗时（ROI构建 / ReduceDomain），供调用方并入自身 stepTimings</summary>
            public IReadOnlyList<(string Step, long Ms)> Timings { get; }

            internal MaskedImage(
                HImage workingImage,
                HRegion? roiRegion,
                HRegion? inspectionRegion,
                HRegion? maskRegion,
                IReadOnlyList<(string Step, long Ms)> timings)
            {
                WorkingImage = workingImage;
                RoiRegion = roiRegion;
                InspectionRegion = inspectionRegion;
                MaskRegion = maskRegion;
                Timings = timings;
            }

            public void Dispose()
            {
                WorkingImage.Dispose();
                RoiRegion?.Dispose();
                InspectionRegion?.Dispose();
                MaskRegion?.Dispose();
            }
        }

        /// <summary>
        /// 在已灰度图像上执行公共区域前置：ROI/掩码构建（含位置修正按形状有向性）→ ReduceDomain。
        /// 区域策略由调用方决定：传空区域集合 = 全图；传已配置区域 = 有区域即限制。
        /// </summary>
        /// <param name="grayImage">灰度图像（有彩色通道需先 Rgb1ToGray，由调用方完成）</param>
        /// <param name="imgWidth">图像宽（像素）</param>
        /// <param name="imgHeight">图像高（像素）</param>
        /// <param name="inspectionRegions">检测区域（参考坐标系；空 = 不限制）</param>
        /// <param name="maskRegions">屏蔽区域（参考坐标系；空 = 不屏蔽）</param>
        /// <param name="nodeResults">下游节点结果（区域订阅解析用）</param>
        /// <param name="correctionMatrix">位置修正矩阵（参考坐标 → 当前图像坐标；可为 null）</param>
        /// <returns>掩码处理结果（WorkingImage 所有权随返回对象，调用方用后 Dispose）</returns>
        public static MaskedImage Prepare(
            HImage grayImage,
            int imgWidth,
            int imgHeight,
            IEnumerable<RoiData> inspectionRegions,
            IEnumerable<RoiData> maskRegions,
            IDictionary<string, ToolResults>? nodeResults = null,
            AffineMatrix? correctionMatrix = null)
        {
            ArgumentNullException.ThrowIfNull(grayImage);

            var timings = new List<(string Step, long Ms)>();
            var stepSw = Stopwatch.StartNew();

            var (roi, insp, mask) = ToolRegionHelper.BuildHalconROI(
                imgWidth, imgHeight,
                inspectionRegions ?? Enumerable.Empty<RoiData>(),
                maskRegions ?? Enumerable.Empty<RoiData>(),
                nodeResults, correctionMatrix);
            stepSw.Stop();
            timings.Add(("ROI构建", stepSw.ElapsedMilliseconds));

            HImage working;
            if (roi != null)
            {
                stepSw.Restart();
                HOperatorSet.ReduceDomain(grayImage, roi, out var reducedObj);
                working = new HImage(reducedObj);
                stepSw.Stop();
                timings.Add(("ReduceDomain", stepSw.ElapsedMilliseconds));
            }
            else
            {
                stepSw.Restart();
                working = grayImage.Clone();
                stepSw.Stop();
                timings.Add(("ReduceDomain", stepSw.ElapsedMilliseconds));
            }

            return new MaskedImage(working, roi, insp, mask, timings);
        }
    }
}
