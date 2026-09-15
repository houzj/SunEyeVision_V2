using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace SunEyeVision.Plugin.SDK.UI.Themes
{
    /// <summary>
    /// 在 Visual Studio 设计器创建控件可视树之前装配 SDK 默认主题。
    /// 设计器不保证存在可供工具使用的 Application，因此控件根与可用的 Application
    /// 都建立相同的设计时资源作用域。运行时由宿主 AppResources + ThemeManager 管理。
    /// </summary>
    internal static class DesignTimeThemeLoader
    {
        internal const string GenericThemeUri =
            "pack://application:,,,/SunEyeVision.Plugin.SDK;component/themes/generic.xaml";

        internal static void EnsureApplicationResources(DependencyObject owner)
        {
            // 工具根通常会在 XAML 中声明自己的 Resources。该属性在基类构造之后
            // 才由 InitializeComponent 写入，会替换构造阶段已经修改过的字典。
            // 因此必须在 Initialized 再检查一次最终资源字典。
            if (owner is FrameworkElement pendingElement && !pendingElement.IsInitialized)
            {
                pendingElement.Initialized -= OnOwnerInitialized;
                pendingElement.Initialized += OnOwnerInitialized;
            }

            if (!IsInDesignMode(owner))
                return;

            if (owner is FrameworkElement frameworkElement)
                EnsureThemeIsLast(frameworkElement.Resources);

            var applicationResources = Application.Current?.Resources;
            if (applicationResources != null)
                EnsureThemeIsLast(applicationResources);
        }

        private static void OnOwnerInitialized(object? sender, EventArgs e)
        {
            if (sender is not FrameworkElement frameworkElement)
                return;

            frameworkElement.Initialized -= OnOwnerInitialized;
            EnsureApplicationResources(frameworkElement);
        }

        private static bool IsInDesignMode(DependencyObject owner)
        {
            return DesignerProperties.GetIsInDesignMode(owner)
                   || (bool)DesignerProperties.IsInDesignModeProperty
                       .GetMetadata(typeof(DependencyObject)).DefaultValue
                   || LicenseManager.UsageMode == LicenseUsageMode.Designtime
                   || IsVisualDesignerProcess();
        }

        private static bool IsVisualDesignerProcess()
        {
            var processName = Process.GetCurrentProcess().ProcessName;
            return processName.Contains("XDesProc", StringComparison.OrdinalIgnoreCase)
                   || processName.Contains("WpfSurface", StringComparison.OrdinalIgnoreCase)
                   || processName.Contains("DesignToolsServer", StringComparison.OrdinalIgnoreCase)
                   || processName.Contains("Blend", StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureThemeIsLast(ResourceDictionary resources)
        {
            var existing = resources.MergedDictionaries.FirstOrDefault(dictionary =>
                string.Equals(dictionary.Source?.OriginalString, GenericThemeUri,
                    StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                // 设计时固定由 SDK DefaultTheme 提供完整预览；移到末尾确保同一资源
                // 作用域中不存在外层暗色、内部亮色的混合解析结果。
                resources.MergedDictionaries.Remove(existing);
                resources.MergedDictionaries.Add(existing);
                return;
            }

            resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(GenericThemeUri, UriKind.Absolute)
            });
        }
    }
}
