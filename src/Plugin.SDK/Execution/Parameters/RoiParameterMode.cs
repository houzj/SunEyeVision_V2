using System.Text.Json.Serialization;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 几何输入角色（检测 / 屏蔽）
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RoiParameterMode
    {
        /// <summary>
        /// 检测区 - 指定需要进行检测/处理区域对应的几何输入
        /// </summary>
        InspectionRoi = 0,

        /// <summary>
        /// 屏蔽区 - 指定需要排除区域对应的几何输入
        /// </summary>
        MaskRoi = 1
    }
}
