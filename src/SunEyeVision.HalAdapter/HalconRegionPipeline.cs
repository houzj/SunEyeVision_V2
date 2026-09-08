using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.HalAdapter
{
    /// <summary>
    /// 图像处理式算法工具的纯区域前置管线（供图像阈值化/图像滤波/图像二值化/形态学处理/Blob 等复用）。
    /// 边界：只做「区域 + 位置修正」两类公共职责（纯几何，不接触任何图像对象）：
    ///   - 区域管理：检测/屏蔽区域订阅解析 + 构建差集（区域策略由调用方列表决定：空 = 全图，有 = 有区域即限制）；
    ///   - 位置修正：按形状有向性并入区域变换（无向剥转 / 有向完整，对齐 UI 语义）。
    /// 图像执行方式（全图跑 / 按域跑）属算法层决策，不在本管线内：工具需按域执行时，
    /// 对原图调用 HalImageDomain.Restrict(grayImage, roiScope.RoiRegion) 自行落地并管理图生命周期。
    /// Metrology/匹配类工具区域语义不同，不适用。
    /// </summary>
    public static class HalconRegionPipeline
    {
        /// <summary>
        /// 区域解析结果（region 对象所有权随本实例；不含任何图像对象）。
        /// </summary>
        public sealed class RoiScope : IDisposable
        {
            private bool _disposed;

            /// <summary>区域限制处理域（检测区−屏蔽区 差集；无区域 = null）</summary>
            public HRegion? RoiRegion { get; }

            /// <summary>检测区域合并（可视化/诊断用；无区域 = null）</summary>
            public HRegion? InspectionRegion { get; }

            /// <summary>屏蔽区域合并（可视化/诊断用；无区域 = null）</summary>
            public HRegion? MaskRegion { get; }

            /// <summary>分步耗时（ROI构建），供调用方并入自身 stepTimings</summary>
            public IReadOnlyList<(string Step, long Ms)> Timings { get; }

            internal RoiScope(
                HRegion? roiRegion,
                HRegion? inspectionRegion,
                HRegion? maskRegion,
                IReadOnlyList<(string Step, long Ms)> timings)
            {
                RoiRegion = roiRegion;
                InspectionRegion = inspectionRegion;
                MaskRegion = maskRegion;
                Timings = timings;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                RoiRegion?.Dispose();
                InspectionRegion?.Dispose();
                MaskRegion?.Dispose();
            }
        }

        /// <summary>
        /// 解析区域前置（纯几何，不接触图像）：
        /// 检测/屏蔽区域订阅解析 → 位置修正（按形状有向性）→ 检测−屏蔽差集。
        /// </summary>
        /// <param name="imgWidth">图像宽（像素）</param>
        /// <param name="imgHeight">图像高（像素）</param>
        /// <param name="inspectionRois">检测几何输入（参考坐标系；空 = 不限制）</param>
        /// <param name="maskRois">屏蔽几何输入（参考坐标系；空 = 不屏蔽）</param>
        /// <param name="nodeResults">下游节点结果（区域订阅解析用）</param>
        /// <param name="correctionMatrix">位置修正矩阵（参考坐标 → 当前图像坐标；可为 null）</param>
        /// <returns>区域解析结果（region 所有权随返回对象，调用方用后 Dispose）</returns>
        public static RoiScope Resolve(
            int imgWidth,
            int imgHeight,
            IEnumerable<Roi> inspectionRois,
            IEnumerable<Roi> maskRois,
            IDictionary<string, ToolResults>? nodeResults = null,
            AffineMatrix? correctionMatrix = null)
        {
            var stepSw = Stopwatch.StartNew();
            var (roi, insp, mask) = ToolRegionHelper.BuildHalconROI(
                imgWidth, imgHeight,
                inspectionRois ?? Enumerable.Empty<Roi>(),
                maskRois ?? Enumerable.Empty<Roi>(),
                nodeResults, correctionMatrix);
            stepSw.Stop();

            var timings = new List<(string Step, long Ms)>
            {
                ("ROI构建", stepSw.ElapsedMilliseconds),
            };
            return new RoiScope(roi, insp, mask, timings);
        }
    }
}
