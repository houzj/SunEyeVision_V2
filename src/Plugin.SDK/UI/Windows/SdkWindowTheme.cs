using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SunEyeVision.Plugin.SDK.UI.Windows
{
    /// <summary>
    /// 系统标题栏明暗主题应用器（SDK 侧，供 tools / 宿主所有 Window 复用）。
    /// 通过 DWM 沉浸式暗色 API（DwmSetWindowAttribute + DWMWA_USE_IMMERSIVE_DARK_MODE）
    /// 让系统标题栏跟随应用主体主题。
    /// 仅作用于 <see cref="WindowStyle"/> 不为 None 的系统标题栏窗口；自绘窗口（WindowStyle=None）自动跳过。
    /// 宿主主题管理器（UI 层）调用 <see cref="SetDark"/> 同步全局状态。
    /// </summary>
    public static class SdkWindowTheme
    {
        /// <summary>DWM 沉浸式暗色属性 ID（Win10 1903+ / Win11）</summary>
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_20 = 20;

        /// <summary>DWM 沉浸式暗色属性 ID（Win10 1809，旧版）</summary>
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_19 = 19;

        /// <summary>当前主体是否暗色（SDK 级状态，由宿主主题管理器同步）</summary>
        public static bool IsDark { get; private set; }

        /// <summary>
        /// 设置全局主题状态并刷新所有已打开窗口标题栏（宿主主题切换时调用，保证 tools 窗口即时跟随）。
        /// </summary>
        public static void SetDark(bool isDark)
        {
            IsDark = isDark;
            var app = Application.Current;
            if (app == null)
                return;

            foreach (Window window in app.Windows)
            {
                Apply(window);
            }
        }

        /// <summary>
        /// 对单个窗口应用当前主体主题的标题栏颜色（WindowStyle=None 的自绘窗口直接跳过）。
        /// DWM 失败（旧系统不支持）时忽略，不影响窗口正常使用。
        /// </summary>
        public static void Apply(Window window)
        {
            if (window == null || window.WindowStyle == WindowStyle.None)
                return;

            IntPtr hwnd;
            try
            {
                hwnd = new WindowInteropHelper(window).Handle;
            }
            catch (Exception)
            {
                return;
            }

            if (hwnd == IntPtr.Zero)
                return;

            int useDarkMode = IsDark ? 1 : 0;

            // 先试属性值 20（Win10 1903+ / Win11），E_INVALIDARG 则回退试 19（Win10 1809）
            if (!TryApplyAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_20, ref useDarkMode)
                && !TryApplyAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_19, ref useDarkMode))
            {
                // 当前系统不支持 DWM 暗色标题栏 —— 静默忽略（不影响功能）
            }
        }

        private static bool TryApplyAttribute(IntPtr hwnd, int attribute, ref int value)
        {
            int result = DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));
            if (result == 0)
                return true;

            // E_INVALIDARG = 该属性 ID 不被当前系统支持 → 返回 false 以尝试旧属性 ID；其它失败同样 false
            return false;
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
