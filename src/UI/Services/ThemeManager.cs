using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Themes;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services
{
    /// <summary>
    /// 应用主题（当前仅亮/暗两套）
    /// </summary>
    public enum AppTheme
    {
        Light,
        Dark
    }

    /// <summary>
    /// 主题管理器：负责运行时切换语义色板字典、广播切换事件、并持久化用户选择。
    /// 切换机制：从 Application.Resources.MergedDictionaries 移除旧主题字典、
    /// 追加新主题字典到集合末尾（最高优先级），使 DynamicResource/FindResource 优先命中语义 Key。
    /// 说明：WPF MergedDictionaries 查找为"顺序遍历 + 后定义者胜出"，集合越靠后优先级越高。
    /// 必须追加到末尾，否则 SDK DefaultTheme.xaml（经 AppResources 合入）的固定亮色值会遮蔽主题字典，
    /// 导致 ImageControl 工具栏/状态栏等被 DefaultTheme 定义了同名 Key 的控件不随黑白主题切换。
    /// </summary>
    public sealed class ThemeManager
    {
        public const string PreferenceKey = "AppTheme";

        private static readonly Uri LightThemeUri =
            new Uri("pack://application:,,,/SunEyeVision.UI;component/Themes/Light/LightTheme.xaml", UriKind.Absolute);
        private static readonly Uri DarkThemeUri =
            new Uri("pack://application:,,,/SunEyeVision.UI;component/Themes/Dark/DarkTheme.xaml", UriKind.Absolute);

        private readonly SolutionSettings _settings;
        private readonly VisionLogger _logger;
        private AppTheme _currentTheme;

        /// <summary>主题切换事件（参数为切换后的主题）</summary>
        public event Action<AppTheme>? ThemeChanged;

        /// <summary>当前主题</summary>
        public AppTheme CurrentTheme => _currentTheme;

        /// <summary>
        /// 创建主题管理器。
        /// </summary>
        /// <param name="settings">用户偏好设置（用于持久化主题选择）</param>
        public ThemeManager(SolutionSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = VisionLogger.Instance;
            _currentTheme = AppTheme.Light;
        }

        /// <summary>
        /// 初始化主题：读回持久化的主题选择并应用。
        /// </summary>
        public void Initialize()
        {
            var saved = _settings.GetPreference<string>(PreferenceKey, null);
            if (Enum.TryParse(saved, ignoreCase: true, out AppTheme theme) && Enum.IsDefined(typeof(AppTheme), theme))
            {
                _currentTheme = theme;
            }
            else
            {
                if (!string.IsNullOrEmpty(saved))
                {
                    _logger.Log(LogLevel.Warning, $"持久化主题值无法识别，回退到默认主题: {saved}", "ThemeManager");
                }
                _currentTheme = AppTheme.Light;
            }

            ApplyThemeDictionary(_currentTheme);
            _logger.Log(LogLevel.Info, $"主题系统初始化完成，当前主题: {_currentTheme}", "ThemeManager");
        }

        /// <summary>
        /// 应用指定主题：切换资源字典、广播事件、持久化用户选择。
        /// </summary>
        public void ApplyTheme(AppTheme theme)
        {
            if (theme == _currentTheme)
            {
                return;
            }

            _currentTheme = theme;
            ApplyThemeDictionary(theme);

            // 持久化用户选择
            try
            {
                _settings.SetPreference(PreferenceKey, theme.ToString());
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"主题偏好持久化失败: {ex.Message}", "ThemeManager", ex);
            }

            _logger.Log(LogLevel.Info, $"主题切换为: {theme}", "ThemeManager");
            ThemeChanged?.Invoke(theme);
        }

        /// <summary>
        /// 切换到另一个主题（亮↔暗互切），并返回切换后的主题。
        /// </summary>
        public AppTheme ToggleTheme()
        {
            var next = _currentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
            ApplyTheme(next);
            return next;
        }

        /// <summary>
        /// 获取主题对应的语义色板资源字典。
        /// </summary>
        private static ResourceDictionary CreateThemeDictionary(AppTheme theme)
        {
            var uri = theme == AppTheme.Light ? LightThemeUri : DarkThemeUri;
            return new ResourceDictionary { Source = uri };
        }

        /// <summary>
        /// 替换 MergedDictionaries 中的主题字典。
        /// 主题字典追加到集合末尾（最高优先级），覆盖 AppResources 合入的 SDK DefaultTheme.xaml 兜底值。
        /// 追加前先做 SDK key 契约校验，确保每个 SdkThemeKeys 常量在主题字典中都有定义，
        /// 防止 SDK 控件因 key 缺失静默回退到 DefaultTheme 兜底值（违反 rule-015）。
        /// </summary>
        private void ApplyThemeDictionary(AppTheme theme)
        {
            var resources = Application.Current?.Resources;
            if (resources == null)
            {
                _logger.Log(LogLevel.Warning, "Application.Current.Resources 为空，主题字典未加载", "ThemeManager");
                return;
            }

            var merged = resources.MergedDictionaries;

            // 1. 移除旧主题字典（通过 Source 匹配）
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var dict = merged[i];
                if (dict.Source != null && IsThemeUri(dict.Source))
                {
                    merged.RemoveAt(i);
                }
            }

            // 2. 创建新主题字典并校验 SDK key 契约
            var themeDict = CreateThemeDictionary(theme);
            ValidateSdkThemeKeys(themeDict);

            // 3. 追加到末尾（最高优先级），覆盖 AppResources 合入的 SDK DefaultTheme 兜底值
            merged.Add(themeDict);
        }

        /// <summary>
        /// 校验 SDK 语义 key 契约：遍历 SdkThemeKeys 全部常量，确认每个 key 在主题字典中都有定义。
        /// 若缺失则抛异常（rule-015 报错优先，不静默回退到 DefaultTheme 兜底值）。
        /// </summary>
        private static void ValidateSdkThemeKeys(ResourceDictionary themeDict)
        {
            var missing = typeof(SdkThemeKeys)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue()!)
                .Where(key => !themeDict.Contains(key))
                .ToArray();

            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    $"SDK 主题 key 契约校验失败，以下 key 未在当前主题字典 {themeDict.Source} 中定义，SDK 控件将静默回退到 DefaultTheme 兜底值: "
                    + string.Join(", ", missing)
                    + "。请检查 SdkThemeKeys 与 UI 主题字典（ThemeKeys + Light/DarkTheme）的一致性。");
            }
        }

        /// <summary>
        /// 判断给定 URI 是否为内置主题字典。
        /// </summary>
        private static bool IsThemeUri(Uri source)
        {
            return source == LightThemeUri || source == DarkThemeUri
                || source.OriginalString.EndsWith("Themes/Light/LightTheme.xaml", StringComparison.OrdinalIgnoreCase)
                || source.OriginalString.EndsWith("Themes/Dark/DarkTheme.xaml", StringComparison.OrdinalIgnoreCase);
        }
    }
}
