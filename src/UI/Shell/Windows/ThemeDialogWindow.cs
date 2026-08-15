using System.Windows;
using SunEyeVision.UI.Services;

namespace SunEyeVision.UI.Shell.Windows
{
    /// <summary>
    /// 支持系统标题栏主题化的弹窗基类。
    /// 窗口句柄初始化时（OnSourceInitialized）按当前主题自动应用系统标题栏明暗，
    /// 使弹窗标题栏随应用主题（亮/暗）同步切换。
    /// 新弹窗继承本基类即自动获得该能力（单一控制点），无需逐窗口处理。
    /// </summary>
    public class ThemeDialogWindow : Window
    {
        /// <summary>
        /// 窗口句柄初始化后，按当前主题应用系统标题栏明暗。
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            WindowTitleBarThemer.Apply(this, WindowTitleBarThemer.IsDarkTheme);
        }
    }
}
