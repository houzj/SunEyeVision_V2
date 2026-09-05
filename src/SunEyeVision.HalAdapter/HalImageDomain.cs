using System;
using HalconDotNet;

namespace SunEyeVision.HalAdapter
{
    /// <summary>
    /// 图像处理式算法工具的"区域限制落地"辅助库（与区域管线 HalconRegionPipeline 解耦）。
    /// 全图跑 / 按域跑是算法层决策：工具需按区域限制执行时显式调用 Restrict 把 ROI 落到图像 Domain；
    /// 不需要时直接以原图执行，本库不参与、不持有任何生命周期。
    /// </summary>
    public static class HalImageDomain
    {
        /// <summary>
        /// 把 ROI 落到图像定义域（按域执行入口）。
        /// roi 非空：ReduceDomain 生成新图（Domain=ROI，像素不变）；roi 为空：返回整图 Clone。
        /// 返回对象所有权归调用方（用后 Dispose）；不修改入参 source。
        /// </summary>
        public static HImage Restrict(HImage source, HRegion? roi)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (roi != null)
            {
                HOperatorSet.ReduceDomain(source, roi, out var reducedObj);
                return new HImage(reducedObj);
            }
            return source.Clone();
        }
    }
}
