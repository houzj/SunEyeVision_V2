using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.UI.Services
{
    /// <summary>
    /// 窗口系统标题栏明暗主题应用器。
    /// 通过 DWM 沉浸式暗色 API（DwmSetWindowAttribute + DWMWA_USE_IMMERSIVE_DARK_MODE）让系统标题栏跟随应用主题。
    /// 仅作用于 <see cref="WindowStyle"/> 不为 None 的窗口（系统标题栏窗口），自绘窗口（WindowStyle=None）不受影响。
    /// </summary>
    public static class WindowTitleBarThemer
    {
        /// <summary>DWM 沉浸式暗色属性 ID（Win10 1903+ / Win11）</summary>
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_20 = 20;

        /// <summary>DWM 沉浸式暗色属性 ID（Win10 1809，旧版）</summary>
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_19 = 19;

        private const int E_INVALIDARG = unchecked((int)0x80070057);

        /// <summary>当前主题是否暗色（供窗口基类在 OnSourceInitialized 时读取）</summary>
        public static bool IsDarkTheme { get; private set; }

        private static readonly VisionLogger _logger = VisionLogger.Instance;

        /// <summary>
        /// 在主题切换时更新全局当前主题，并刷新所有已打开的窗口标题栏。
        /// </summary>
        /// <param name="isDark">是否暗色主题</param>
        public static void UpdateTheme(bool isDark)
        {
            IsDarkTheme = isDark;

            foreach (Window window in Application.Current.Windows)
            {
                Apply(window, isDark);
            }
        }

        /// <summary>
        /// 对单个窗口应用系统标题栏明暗。
        /// 仅作用于 WindowStyle != None 的窗口；自绘窗口（WindowStyle=None）直接跳过。
        /// DWM 调用失败（旧系统不支持等）记 Warning 日志，绝不静默、不影响窗口正常使用（Fail-Forward）。
        /// </summary>
        /// <param name="window">目标窗口</param>
        /// <param name="isDark">是否暗色主题</param>
        public static void Apply(Window window, bool isDark)
        {
            if (window == null || window.WindowStyle == WindowStyle.None)
            {
                return;
            }

            IntPtr hwnd;
            try
            {
                hwnd = new WindowInteropHelper(window).Handle;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Warning, $"获取窗口句柄失败，跳过标题栏主题应用: {ex.Message}", "WindowTitleBarThemer", ex);
                return;
            }

            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            int useDarkMode = isDark ? 1 : 0;

            // 先试属性值 20（Win10 1903+ / Win11），E_INVALIDARG 则回退试 19（Win10 1809）
            if (!TryApplyAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_20, ref useDarkMode)
                && !TryApplyAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_19, ref useDarkMode))
            {
                _logger.Log(LogLevel.Warning,
                    $"窗口标题栏沉浸式暗色设置失败（当前系统可能不支持 DWM 暗色标题栏）: {window.Title}", "WindowTitleBarThemer");
            }
        }

        private static bool TryApplyAttribute(IntPtr hwnd, int attribute, ref int value)
        {
            int result = DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));
            if (result == 0)
            {
                return true;
            }

            if (result == E_INVALIDARG)
            {
                return false;
            }

            _logger.Log(LogLevel.Warning, $"DwmSetWindowAttribute 失败，HRESULT=0x{result:X8}", "WindowTitleBarThemer");
            return false;
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
