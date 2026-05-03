namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Models
{
    /// <summary>
    /// 区域类型 - 决定区域在编辑器和算法中的角色
    /// </summary>
    public enum RegionType
    {
        /// <summary>
        /// 检测区域 - 指定需要进行检测/处理的区域
        /// 渲染：透明填充 + 蓝色边框
        /// </summary>
        Inspection = 0,

        /// <summary>
        /// 屏蔽区域 - 指定需要从检测区域中排除的区域
        /// 渲染：半透明橙色填充 + 橙色边框
        /// </summary>
        Mask = 1
    }
}
