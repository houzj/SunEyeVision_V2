using System.Windows;
using SunEyeVision.Plugin.SDK.UI.Windows;
using SunEyeVision.Tool.Script.Views;

namespace SunEyeVision.Tool.Script
{
    /// <summary>
    /// 脚本插件 — 创建 DefaultDebugWindow 包裹 ScriptDebugControl。
    /// </summary>
    public static class ScriptPlugin
    {
        /// <summary>
        /// 打开脚本调试窗口
        /// </summary>
        public static DefaultDebugWindow ShowDebugWindow(Window owner)
        {
            var control = new ScriptDebugControl();
            var debugWindow = new DefaultDebugWindow(control)
            {
                Owner = owner
            };
            return debugWindow;
        }
    }
}