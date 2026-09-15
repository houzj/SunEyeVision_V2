namespace SunEyeVision.Plugin.SDK.UI.Themes
{
    /// <summary>
    /// SDK 层语义色板 Key 常量（避免魔法字符串）。
    /// 字符串与 UI 层 ThemeKeys（SunEyeVision.UI.Themes.Base）完全一致，
    /// 使 SDK 控件在 UI 宿主内可通过 DynamicResource 命中 UI 主题字典（Application.Resources 置顶注入）。
    /// SDK 不能引用 UI 程序集（循环依赖），故在 SDK 层自建等值常量。
    /// 每个 Key 在 SDK 兜底主题 DefaultTheme.xaml 中必须定义（亮色值来自 ColorConstants）。
    /// </summary>
    public static class SdkThemeKeys
    {
        // ============ 品牌主色 ============
        /// <summary>品牌主色 / 强调色（Color 层），对应 UI ThemeKeys.AccentColor</summary>
        public const string AccentColor = "ThemeAccentColor";
        /// <summary>品牌主色 / 强调色（Brush 层），对应 UI ThemeKeys.AccentBrush</summary>
        public const string AccentBrush = "ThemeAccentBrush";

        // ============ 顶栏 / 标题栏 ============
        /// <summary>标题栏背景，对应 UI ThemeKeys.TitleBarBackgroundBrush</summary>
        public const string TitleBarBackgroundBrush = "ThemeTitleBarBackgroundBrush";
        /// <summary>标题栏文字，对应 UI ThemeKeys.TitleBarTextBrush</summary>
        public const string TitleBarTextBrush = "ThemeTitleBarTextBrush";
        /// <summary>标题栏按钮悬停背景，对应 UI ThemeKeys.TitleBarButtonHoverBrush</summary>
        public const string TitleBarButtonHoverBrush = "ThemeTitleBarButtonHoverBrush";

        // ============ 控件底色 ============
        /// <summary>控件悬停背景，对应 UI ThemeKeys.ControlHoverBackgroundBrush</summary>
        public const string ControlHoverBackgroundBrush = "ThemeControlHoverBackgroundBrush";
        /// <summary>控件按下 / 选中背景，对应 UI ThemeKeys.ControlSelectedBackgroundBrush</summary>
        public const string ControlSelectedBackgroundBrush = "ThemeControlSelectedBackgroundBrush";
        /// <summary>控件默认背景（按钮/输入框等），对应 UI ThemeKeys.ControlBackgroundBrush</summary>
        public const string ControlBackgroundBrush = "ThemeControlBackgroundBrush";

        // ============ 按钮（SDK 统一悬停） ============
        /// <summary>按钮默认背景，对应 UI ThemeKeys.ButtonBackgroundBrush</summary>
        public const string ButtonBackgroundBrush = "ThemeButtonBackgroundBrush";
        /// <summary>按钮悬停背景，对应 UI ThemeKeys.ButtonHoverBackgroundBrush</summary>
        public const string ButtonHoverBackgroundBrush = "ThemeButtonHoverBackgroundBrush";
        /// <summary>按钮按下背景，对应 UI ThemeKeys.ButtonPressedBackgroundBrush</summary>
        public const string ButtonPressedBackgroundBrush = "ThemeButtonPressedBackgroundBrush";
        /// <summary>按钮边框，对应 UI ThemeKeys.ButtonBorderBrush</summary>
        public const string ButtonBorderBrush = "ThemeButtonBorderBrush";
        /// <summary>按钮文字，对应 UI ThemeKeys.ButtonTextBrush</summary>
        public const string ButtonTextBrush = "ThemeButtonTextBrush";
        /// <summary>按钮禁用背景，对应 UI ThemeKeys.ButtonDisabledBackgroundBrush</summary>
        public const string ButtonDisabledBackgroundBrush = "ThemeButtonDisabledBackgroundBrush";

        // ============ 文字 ============
        /// <summary>主文字，对应 UI ThemeKeys.TextPrimaryBrush</summary>
        public const string TextPrimaryBrush = "ThemeTextPrimaryBrush";
        /// <summary>次要文字，对应 UI ThemeKeys.TextSecondaryBrush</summary>
        public const string TextSecondaryBrush = "ThemeTextSecondaryBrush";
        /// <summary>禁用文字，对应 UI ThemeKeys.TextDisabledBrush</summary>
        public const string TextDisabledBrush = "ThemeTextDisabledBrush";

        // ============ 边框 / 分隔 ============
        /// <summary>常规边框，对应 UI ThemeKeys.BorderBrush</summary>
        public const string BorderBrush = "ThemeBorderBrush";
        /// <summary>分隔线（Brush 层），对应 UI ThemeKeys.DividerBrush</summary>
        public const string DividerBrush = "ThemeDividerBrush";

        // ============ 面板 / 背景 ============
        /// <summary>右侧属性面板 / 常规面板背景，对应 UI ThemeKeys.PanelBackgroundBrush</summary>
        public const string PanelBackgroundBrush = "ThemePanelBackgroundBrush";
        /// <summary>面板标题栏背景，对应 UI ThemeKeys.PanelHeaderBackgroundBrush</summary>
        public const string PanelHeaderBackgroundBrush = "ThemePanelHeaderBackgroundBrush";

        // ============ 工具栏 / 输入控件 / 状态栏（主题管控扩展） ============
        /// <summary>面板 / 工具栏背景，对应 UI ThemeKeys.ToolBarBackgroundBrush</summary>
        public const string ToolBarBackgroundBrush = "ThemeToolBarBackgroundBrush";
        /// <summary>输入控件背景（文本框/下拉框），对应 UI ThemeKeys.InputBackgroundBrush</summary>
        public const string InputBackgroundBrush = "ThemeInputBackgroundBrush";
        /// <summary>输入控件边框，对应 UI ThemeKeys.InputBorderBrush</summary>
        public const string InputBorderBrush = "ThemeInputBorderBrush";
        /// <summary>状态栏背景，对应 UI ThemeKeys.StatusBarBackgroundBrush</summary>
        public const string StatusBarBackgroundBrush = "ThemeStatusBarBackgroundBrush";
        /// <summary>状态栏边框，对应 UI ThemeKeys.StatusBarBorderBrush</summary>
        public const string StatusBarBorderBrush = "ThemeStatusBarBorderBrush";
        /// <summary>状态文字，对应 UI ThemeKeys.StatusBarTextBrush</summary>
        public const string StatusBarTextBrush = "ThemeStatusBarTextBrush";
        /// <summary>右键菜单背景，对应 UI ThemeKeys.ContextMenuBackgroundBrush</summary>
        public const string ContextMenuBackgroundBrush = "ThemeContextMenuBackgroundBrush";
        /// <summary>深底上的文字（选中/强调，常为白），对应 UI ThemeKeys.TextOnDarkBrush</summary>
        public const string TextOnDarkBrush = "ThemeTextOnDarkBrush";
        /// <summary>图像区透明背景，对应 UI ThemeKeys.CheckerboardBackgroundBrush</summary>
        public const string CheckerboardBackgroundBrush = "ThemeCheckerboardBackgroundBrush";
        /// <summary>图像/模板预览区域背景，对应 UI ThemeKeys.ImageAreaBackgroundBrush</summary>
        public const string ImageAreaBackgroundBrush = "ThemeImageAreaBackgroundBrush";

        // ============ 表格（工具结果面板） ============
        /// <summary>表格行底，对应 UI ThemeKeys.TableRowBackgroundBrush</summary>
        public const string TableRowBackgroundBrush = "ThemeTableRowBackgroundBrush";
        /// <summary>表格行悬停底 / 交替行底，对应 UI ThemeKeys.TableRowHoverBackgroundBrush</summary>
        public const string TableRowHoverBackgroundBrush = "ThemeTableRowHoverBackgroundBrush";
        /// <summary>表格选中行背景，对应 UI ThemeKeys.TableRowSelectedBackgroundBrush</summary>
        public const string TableRowSelectedBackgroundBrush = "ThemeTableRowSelectedBackgroundBrush";
        /// <summary>表格表头底，对应 UI ThemeKeys.TableHeaderBackgroundBrush</summary>
        public const string TableHeaderBackgroundBrush = "ThemeTableHeaderBackgroundBrush";
        /// <summary>表格表头文字，对应 UI ThemeKeys.TableHeaderTextBrush</summary>
        public const string TableHeaderTextBrush = "ThemeTableHeaderTextBrush";
        /// <summary>表格网格线，对应 UI ThemeKeys.TableGridLineBrush</summary>
        public const string TableGridLineBrush = "ThemeTableGridLineBrush";

        // ============ 品牌 / 状态色（工具结果状态） ============
        /// <summary>成功 / 就绪状态，对应 UI ThemeKeys.SuccessBrush</summary>
        public const string SuccessBrush = "ThemeSuccessBrush";
        /// <summary>警告状态，对应 UI ThemeKeys.WarningBrush</summary>
        public const string WarningBrush = "ThemeWarningBrush";
        /// <summary>错误 / 运行中状态，对应 UI ThemeKeys.ErrorBrush</summary>
        public const string ErrorBrush = "ThemeErrorBrush";

        // ============ 数据分析序列色 ============
        /// <summary>数据分析序列 1（主指标）</summary>
        public const string DataSeries1Brush = "ThemeDataSeries1Brush";
        /// <summary>数据分析序列 2</summary>
        public const string DataSeries2Brush = "ThemeDataSeries2Brush";
        /// <summary>数据分析序列 3</summary>
        public const string DataSeries3Brush = "ThemeDataSeries3Brush";
        /// <summary>数据分析序列 4</summary>
        public const string DataSeries4Brush = "ThemeDataSeries4Brush";
        /// <summary>数据分析序列 5</summary>
        public const string DataSeries5Brush = "ThemeDataSeries5Brush";
        /// <summary>数据分析辅助说明</summary>
        public const string DataSeriesMutedBrush = "ThemeDataSeriesMutedBrush";

        // ============ SDK 控件补充（批次3，SDK 样式库 Token 化） ============
        /// <summary>滑块/范围滑块轨道背景，对应 UI ThemeKeys.SliderTrackBackgroundBrush</summary>
        public const string SliderTrackBackgroundBrush = "ThemeSliderTrackBackgroundBrush";
        /// <summary>开关控件关闭态背景，对应 UI ThemeKeys.ToggleOffBackgroundBrush</summary>
        public const string ToggleOffBackgroundBrush = "ThemeToggleOffBackgroundBrush";
        /// <summary>成功状态浅底色（类型匹配标签），对应 UI ThemeKeys.SuccessLightBackgroundBrush</summary>
        public const string SuccessLightBackgroundBrush = "ThemeSuccessLightBackgroundBrush";

        // ============ 滚动条（标准控件，激活 UI 已定义但无人消费的死 token） ============
        /// <summary>滚动条轨道背景，对应 UI ThemeKeys.ScrollBarBackgroundBrush</summary>
        public const string ScrollBarBackgroundBrush = "ThemeScrollBarBackgroundBrush";
        /// <summary>滚动条滑块（默认态），对应 UI ThemeKeys.ScrollBarThumbBrush</summary>
        public const string ScrollBarThumbBrush = "ThemeScrollBarThumbBrush";
        /// <summary>滚动条滑块（悬停态），对应 UI ThemeKeys.ScrollBarThumbHoverBrush</summary>
        public const string ScrollBarThumbHoverBrush = "ThemeScrollBarThumbHoverBrush";
        /// <summary>滚动条滑块（按下态），对应 UI ThemeKeys.ScrollBarThumbPressedBrush</summary>
        public const string ScrollBarThumbPressedBrush = "ThemeScrollBarThumbPressedBrush";
    }
}
