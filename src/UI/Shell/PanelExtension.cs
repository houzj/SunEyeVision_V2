using System.Windows;

namespace SunEyeVision.UI.Shell
{
    /// <summary>
    /// 面板扩展接口
    /// 插件可通过此接口向面板系统注册自定义面板。
    /// </summary>
    public interface IPanelExtension
    {
        /// <summary>
        /// 面板ID
        /// </summary>
        string PanelId { get; }

        /// <summary>
        /// 面板显示名称
        /// </summary>
        string PanelName { get; }

        /// <summary>
        /// 面板图标
        /// </summary>
        string Icon { get; }

        /// <summary>
        /// 获取面板内容
        /// </summary>
        /// <returns>面板内容</returns>
        FrameworkElement GetPanelContent();
    }
}
