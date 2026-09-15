using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace SunEyeVision.Plugin.SDK.UI.Themes
{
    /// <summary>
    /// 仅在 XAML 设计器中加载 SDK 默认主题的资源字典。
    /// 运行时该字典保持为空，由宿主 Application 的 Light/Dark 主题负责供给资源。
    /// </summary>
    public sealed class DesignTimeThemeResourceDictionary : ResourceDictionary
    {
        public DesignTimeThemeResourceDictionary()
        {
            if (IsDesignEnvironment())
                Source = new Uri(DesignTimeThemeLoader.GenericThemeUri, UriKind.Absolute);
        }

        private static bool IsDesignEnvironment()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject())
                || (bool)DesignerProperties.IsInDesignModeProperty
                    .GetMetadata(typeof(DependencyObject)).DefaultValue
                || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return true;

            // WPF 应用正常创建工具面板时必定已有 Application。设计器与独立预览宿主
            // 可能没有 Application；此时加载 SDK 兜底主题正是期望行为。
            if (Application.Current == null)
                return true;

            var processName = Process.GetCurrentProcess().ProcessName;
            return processName.Contains("devenv", StringComparison.OrdinalIgnoreCase)
                   || processName.Contains("XDesProc", StringComparison.OrdinalIgnoreCase)
                   || processName.Contains("WpfSurface", StringComparison.OrdinalIgnoreCase)
                   || processName.Contains("DesignToolsServer", StringComparison.OrdinalIgnoreCase)
                   || processName.Contains("Blend", StringComparison.OrdinalIgnoreCase);
        }
    }
}
