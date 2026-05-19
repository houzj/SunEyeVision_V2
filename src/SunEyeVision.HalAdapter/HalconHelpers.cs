using System;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.HalAdapter
{
    /// <summary>
    /// Halcon 原生工具辅助扩展方法 - 简化入口图像/区域拆包样板代码
    /// </summary>
    public static class HalconToolHelpers
    {
        /// <summary>
        /// 从参数中获取 Halcon HImage（一键拆包 + 空检查）
        /// </summary>
        /// <exception cref="InvalidOperationException">输入图像为空或类型不匹配时抛出</exception>
        public static HImage GetHalconImage(this ToolParameters parameters)
        {
            var image = parameters.ResolvedInputImage;
            if (image is HalImage halImage)
            {
                var hImage = halImage.Unwrap();
                if (hImage != null && hImage.IsInitialized())
                    return hImage;
                throw new InvalidOperationException("输入图像为空（HImage 未初始化）");
            }
            throw new InvalidOperationException("输入图像不是 Halcon HImage，当前输入类型: " + (image?.GetType().Name ?? "null"));
        }

        /// <summary>
        /// 尝试获取 Halcon HImage（不抛异常）
        /// </summary>
        /// <param name="hImage">输出 HImage，未找到时为 null</param>
        /// <returns>是否成功获取</returns>
        public static bool TryGetHalconImage(this ToolParameters parameters, out HImage? hImage)
        {
            var image = parameters.ResolvedInputImage;
            if (image is HalImage halImage)
            {
                var unwrapped = halImage.Unwrap();
                if (unwrapped != null && unwrapped.IsInitialized())
                {
                    hImage = unwrapped;
                    return true;
                }
            }
            hImage = null;
            return false;
        }

        /// <summary>
        /// 获取 Halcon 配置参数值（从 HTuple 转 double）
        /// </summary>
        public static double GetDouble(this HTuple tuple) => tuple.D;

        /// <summary>
        /// 获取 Halcon 配置参数值（从 HTuple 转 long）
        /// </summary>
        public static long GetLong(this HTuple tuple) => tuple.L;
    }
}
