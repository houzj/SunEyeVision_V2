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
    /// 边界收敛为「图像源 + 区域管理 + 位置修正」三类公共职责：
    ///   - 图像源：对已灰度图做 ReduceDomain（区域限制的实现手段），输出受限定义域图；
    ///   - 区域管理：ROI/掩码构建（有向性/剥转、订阅区域解析）；
    ///   - 位置修正：按形状有向性并入区域变换。
    /// 图像预处理等"算法首段"不属于公共前置，由工具在取得 WorkingImage 后自行实现（如 Blob 需就地替换
    /// 处理图时，先 DetachWorkingImage 接管所有权再处理）。
    /// 本类型仅对齐"图像处理式"形态（灰度 + ROI/掩码 + ReduceDomain）；Metrology/匹配类工具前置语义不同，不适用。
    /// </summary>
    public static class HalconImageOpPipeline
    {
        /// <summary>
        /// 区域掩码预处理结果（区域对象所有权随实例释放；WorkingImage 可经 DetachWorkingImage 移交）。
        /// </summary>
        public sealed class MaskedImage : IDisposable
        {
            private HImage? _workingImage;
            private bool _disposed;

            /// <summary>核心算子输入图像（有区域时已 ReduceDomain；无区域时为全图 Clone）。所有权归本实例，除非已 DetachWorkingImage。</summary>
            public HImage WorkingImage =>
                _workingImage ?? throw new InvalidOperationException(
                    "WorkingImage 已通过 DetachWorkingImage 移交所有权，不能再次访问");

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
                _workingImage = workingImage;
                RoiRegion = roiRegion;
                InspectionRegion = inspectionRegion;
                MaskRegion = maskRegion;
                Timings = timings;
            }

            /// <summary>
            /// 移交 WorkingImage 所有权给调用方（供算法就地替换处理图等场景，如 Blob 图像预处理）。
            /// 调用后本实例 Dispose 不再释放 WorkingImage（改由调用方负责）；WorkingImage 属性亦不可再访问。
            /// </summary>
            public HImage DetachWorkingImage()
            {
                var image = _workingImage;
                _workingImage = null;
                return image;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                _workingImage?.Dispose();
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
