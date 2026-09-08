namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models
{
    /// <summary>
    /// 区域来源模式（UI 交互态：绘制 / 订阅）。
    /// 注意：订阅语义最终由执行模型 Roi.Binding（ParamSetting）表达，本枚举仅用于编辑器交互 UI。
    /// </summary>
    public enum RoiSourceMode
    {
        /// <summary>
        /// 绘制模式 - 区域来源于用户手动绘制
        /// </summary>
        Drawing,

        /// <summary>
        /// 订阅模式 - 区域来源于其他节点的输出
        /// </summary>
        Subscribe
    }
}
