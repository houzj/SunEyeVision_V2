using System.Text.Json.Serialization;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 区域参数模式
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RoiParameterMode
    {
        /// <summary>
        /// 检测区域 - 指定需要进行检测/处理的区域
        /// </summary>
        InspectionRegion = 0,

        /// <summary>
        /// 屏蔽区域 - 指定需要排除的区域
        /// </summary>
        MaskRegion = 1
    }
}
