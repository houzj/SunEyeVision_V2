using System.Windows.Media;

namespace SunEyeVision.Plugin.SDK.UI.Constants
{
    /// <summary>
    /// 项目统一颜色常量 - 单一来源
    /// </summary>
    /// <remarks>
    /// 此类是项目所有颜色的唯一定义来源：
    /// - C# 代码直接使用常量
    /// - XAML 通过 x:Static 引用常量
    /// - 修改此处，所有地方同步更新
    /// 
    /// 规则：禁止在其他地方定义颜色值！
    /// 
    /// 颜色体系：
    /// - Primary: #0066CC（主色调，用于按钮、选中、重点）
    /// - Secondary: #00CC99（次要色，用于成功、次要操作）
    /// - Warning: #FF9900（警告色）
    /// - Error: #FF3333（错误色）
    /// - Success: #00CC99（成功色）
    ///
    /// 说明：SDK 控件已统一改用 Theme 前缀语义 key（见 DefaultTheme.xaml），
    /// 本类仍作为 SDK 兜底默认主题（DefaultTheme.xaml）的颜色值单一来源。
    /// </remarks>
    public static class ColorConstants
    {
        #region 主色调系统

        /// <summary>
        /// 主色调 - 蓝色（按钮、选中、重点）
        /// </summary>
        public const string PrimaryHex = "#0066CC";
        public static readonly Color Primary = Color.FromRgb(0, 102, 204);
        public static readonly SolidColorBrush PrimaryBrush = CreateFrozenBrush(Primary);

        /// <summary>
        /// 主色调悬停 - 深蓝色
        /// </summary>
        public const string PrimaryHoverHex = "#0055AA";
        public static readonly Color PrimaryHover = Color.FromRgb(0, 85, 170);
        public static readonly SolidColorBrush PrimaryHoverBrush = CreateFrozenBrush(PrimaryHover);

        /// <summary>
        /// 主色调按下 - 更深蓝色
        /// </summary>
        public const string PrimaryPressedHex = "#004488";
        public static readonly Color PrimaryPressed = Color.FromRgb(0, 68, 136);
        public static readonly SolidColorBrush PrimaryPressedBrush = CreateFrozenBrush(PrimaryPressed);

        #endregion

        #region 功能色系统

        /// <summary>
        /// 次要色 - 绿色（成功、次要）
        /// </summary>
        public const string SecondaryHex = "#00CC99";
        public static readonly Color Secondary = Color.FromRgb(0, 204, 153);
        public static readonly SolidColorBrush SecondaryBrush = CreateFrozenBrush(Secondary);

        /// <summary>
        /// 警告色 - 橙色
        /// </summary>
        public const string WarningHex = "#FF9900";
        public static readonly Color Warning = Color.FromRgb(255, 153, 0);
        public static readonly SolidColorBrush WarningBrush = CreateFrozenBrush(Warning);

        /// <summary>
        /// 错误色 - 红色
        /// </summary>
        public const string ErrorHex = "#FF3333";
        public static readonly Color Error = Color.FromRgb(255, 51, 51);
        public static readonly SolidColorBrush ErrorBrush = CreateFrozenBrush(Error);

        /// <summary>
        /// 成功色 - 绿色
        /// </summary>
        public const string SuccessHex = "#00CC99";
        public static readonly Color Success = Color.FromRgb(0, 204, 153);
        public static readonly SolidColorBrush SuccessBrush = CreateFrozenBrush(Success);

        /// <summary>
        /// 信息色 - 蓝色
        /// </summary>
        public const string InfoHex = "#0066CC";
        public static readonly Color Info = Color.FromRgb(0, 102, 204);
        public static readonly SolidColorBrush InfoBrush = CreateFrozenBrush(Info);

        #endregion

        #region 中性色系统

        /// <summary>
        /// 文本主色 - 深灰色
        /// </summary>
        public const string TextPrimaryHex = "#333333";
        public static readonly Color TextPrimary = Color.FromRgb(51, 51, 51);
        public static readonly SolidColorBrush TextPrimaryBrush = CreateFrozenBrush(TextPrimary);

        /// <summary>
        /// 文本次要色 - 中灰色
        /// </summary>
        public const string TextSecondaryHex = "#666666";
        public static readonly Color TextSecondary = Color.FromRgb(102, 102, 102);
        public static readonly SolidColorBrush TextSecondaryBrush = CreateFrozenBrush(TextSecondary);

        /// <summary>
        /// 文本禁用色 - 浅灰色
        /// </summary>
        public const string TextDisabledHex = "#999999";
        public static readonly Color TextDisabled = Color.FromRgb(153, 153, 153);
        public static readonly SolidColorBrush TextDisabledBrush = CreateFrozenBrush(TextDisabled);

        /// <summary>
        /// 边框色 - 浅灰色
        /// </summary>
        public const string BorderHex = "#CCCCCC";
        public static readonly Color Border = Color.FromRgb(204, 204, 204);
        public static readonly SolidColorBrush BorderBrush = CreateFrozenBrush(Border);

        /// <summary>
        /// 边框浅色 - 更浅灰色
        /// </summary>
        public const string BorderLightHex = "#DDDDDD";
        public static readonly Color BorderLight = Color.FromRgb(221, 221, 221);
        public static readonly SolidColorBrush BorderLightBrush = CreateFrozenBrush(BorderLight);

        /// <summary>
        /// 背景色 - 白色
        /// </summary>
        public const string BackgroundHex = "#FFFFFF";
        public static readonly Color Background = Color.FromRgb(255, 255, 255);
        public static readonly SolidColorBrush BackgroundBrush = CreateFrozenBrush(Background);

        /// <summary>
        /// 背景灰色 - 浅灰色背景
        /// </summary>
        public const string BackgroundGrayHex = "#F8F8F8";
        public static readonly Color BackgroundGray = Color.FromRgb(248, 248, 248);
        public static readonly SolidColorBrush BackgroundGrayBrush = CreateFrozenBrush(BackgroundGray);

        /// <summary>
        /// 背景深灰色 - 深色背景
        /// </summary>
        public const string BackgroundDarkHex = "#F0F0F0";
        public static readonly Color BackgroundDark = Color.FromRgb(240, 240, 240);
        public static readonly SolidColorBrush BackgroundDarkBrush = CreateFrozenBrush(BackgroundDark);

        /// <summary>
        /// 背景悬停色 - 浅灰色（鼠标悬停）
        /// </summary>
        public const string BackgroundHoverHex = "#F5F5F5";
        public static readonly Color BackgroundHover = Color.FromRgb(245, 245, 245);
        public static readonly SolidColorBrush BackgroundHoverBrush = CreateFrozenBrush(BackgroundHover);

        /// <summary>
        /// 背景选中色 - 浅蓝色（选中项）
        /// </summary>
        public const string BackgroundSelectedHex = "#E6F7FF";
        public static readonly Color BackgroundSelected = Color.FromRgb(230, 247, 255);
        public static readonly SolidColorBrush BackgroundSelectedBrush = CreateFrozenBrush(BackgroundSelected);

        /// <summary>
        /// 背景悬停浅蓝色 - 鼠标悬停选中项
        /// </summary>
        public const string BackgroundHoverSelectedHex = "#F0F7FF";
        public static readonly Color BackgroundHoverSelected = Color.FromRgb(240, 247, 255);
        public static readonly SolidColorBrush BackgroundHoverSelectedBrush = CreateFrozenBrush(BackgroundHoverSelected);

        /// <summary>
        /// 轨道背景色 - 灰色（滑块轨道）
        /// </summary>
        public const string TrackBackgroundHex = "#E0E0E0";
        public static readonly Color TrackBackground = Color.FromRgb(224, 224, 224);
        public static readonly SolidColorBrush TrackBackgroundBrush = CreateFrozenBrush(TrackBackground);

        /// <summary>
        /// 深色主题控件背景 - 深灰色（深色主题）
        /// </summary>
        public const string DarkThemeControlBackgroundHex = "#555555";
        public static readonly Color DarkThemeControlBackground = Color.FromRgb(85, 85, 85);
        public static readonly SolidColorBrush DarkThemeControlBackgroundBrush = CreateFrozenBrush(DarkThemeControlBackground);

        /// <summary>
        /// 橙色 - 开关开启状态
        /// </summary>
        public const string SwitchOnBackgroundHex = "#FF7F00";
        public static readonly Color SwitchOnBackground = Color.FromRgb(255, 127, 0);
        public static readonly SolidColorBrush SwitchOnBackgroundBrush = CreateFrozenBrush(SwitchOnBackground);

        #endregion

        #region 辅助方法

        /// <summary>
        /// 创建冻结的画笔（性能优化）
        /// </summary>
        /// <param name="color">颜色值</param>
        /// <returns>冻结的画笔</returns>
        /// <remarks>
        /// 冻结画笔可以提高性能，避免重复创建。
        /// 冻结后的画笔不能修改，但可以跨线程共享。
        /// </remarks>
        private static SolidColorBrush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        #endregion
    }
}
