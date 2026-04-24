using OpenCvSharp;

namespace SunEyeVision.CvEngine
{
    /// <summary>
    /// OpenCV类型转换工具类
    /// </summary>
    internal static class CvConvert
    {
        /// <summary>
        /// 将CvThresholdType转换为OpenCvSharp的ThresholdTypes
        /// </summary>
        public static ThresholdTypes ToOpenCvThresholdType(CvThresholdType type)
        {
            return type switch
            {
                CvThresholdType.Binary => ThresholdTypes.Binary,
                CvThresholdType.BinaryInv => ThresholdTypes.BinaryInv,
                CvThresholdType.Trunc => ThresholdTypes.Trunc,
                CvThresholdType.ToZero => ThresholdTypes.Tozero,
                CvThresholdType.ToZeroInv => ThresholdTypes.TozeroInv,
                _ => ThresholdTypes.Binary
            };
        }

        /// <summary>
        /// 将CvAdaptiveMethod转换为OpenCvSharp的AdaptiveThresholdTypes
        /// </summary>
        public static AdaptiveThresholdTypes ToOpenCvAdaptiveType(CvAdaptiveMethod method)
        {
            return method switch
            {
                CvAdaptiveMethod.Mean => AdaptiveThresholdTypes.MeanC,
                CvAdaptiveMethod.Gaussian => AdaptiveThresholdTypes.GaussianC,
                _ => AdaptiveThresholdTypes.MeanC
            };
        }

        /// <summary>
        /// 将CvConnectivity转换为OpenCvSharp的PixelConnectivity
        /// </summary>
        public static PixelConnectivity ToOpenCvConnectivity(CvConnectivity connectivity)
        {
            return connectivity switch
            {
                CvConnectivity.Connect4 => PixelConnectivity.Connectivity4,
                CvConnectivity.Connect8 => PixelConnectivity.Connectivity8,
                _ => PixelConnectivity.Connectivity8
            };
        }
    }
}
