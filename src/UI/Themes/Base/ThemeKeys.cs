namespace SunEyeVision.UI.Themes.Base
{
    /// <summary>
    /// 语义色板 Key 常量集中定义（避免魔法字符串）。
    /// 每个语义 Key 在 Light/Dark 主题字典中都必须成对定义：
    /// - 颜色 Key（如 <see cref="WindowBackground"/>）：Color 资源
    /// - 笔刷 Key（如 <see cref="WindowBackgroundBrush"/>）：SolidColorBrush 资源
    /// 按"窗口/面板底、控件底、文字、边框分隔、品牌状态、画布专属"6 组组织。
    /// </summary>
    public static class ThemeKeys
    {
        // ============ 窗口 / 面板底色 ============
        /// <summary>主窗口背景</summary>
        public const string WindowBackground = "ThemeWindowBackground";
        public const string WindowBackgroundBrush = "ThemeWindowBackgroundBrush";
        /// <summary>顶栏 / 标题栏背景</summary>
        public const string TitleBarBackground = "ThemeTitleBarBackground";
        public const string TitleBarBackgroundBrush = "ThemeTitleBarBackgroundBrush";
        /// <summary>右侧属性面板 / 常规面板背景</summary>
        public const string PanelBackground = "ThemePanelBackground";
        public const string PanelBackgroundBrush = "ThemePanelBackgroundBrush";
        /// <summary>内容区 / 工作区背景（画布外）</summary>
        public const string WorkspaceBackground = "ThemeWorkspaceBackground";
        public const string WorkspaceBackgroundBrush = "ThemeWorkspaceBackgroundBrush";

        // ============ 控件底色 ============
        /// <summary>控件默认背景（按钮/输入框等）</summary>
        public const string ControlBackground = "ThemeControlBackground";
        public const string ControlBackgroundBrush = "ThemeControlBackgroundBrush";
        /// <summary>控件悬停背景</summary>
        public const string ControlHoverBackground = "ThemeControlHoverBackground";
        public const string ControlHoverBackgroundBrush = "ThemeControlHoverBackgroundBrush";
        /// <summary>控件按下 / 选中背景</summary>
        public const string ControlSelectedBackground = "ThemeControlSelectedBackground";
        public const string ControlSelectedBackgroundBrush = "ThemeControlSelectedBackgroundBrush";

        // ============ 文字 ============
        /// <summary>主文字</summary>
        public const string TextPrimary = "ThemeTextPrimary";
        public const string TextPrimaryBrush = "ThemeTextPrimaryBrush";
        /// <summary>次要文字</summary>
        public const string TextSecondary = "ThemeTextSecondary";
        public const string TextSecondaryBrush = "ThemeTextSecondaryBrush";
        /// <summary>标题栏 / 顶栏文字（常为白色）</summary>
        public const string TextOnDark = "ThemeTextOnDark";
        public const string TextOnDarkBrush = "ThemeTextOnDarkBrush";
        /// <summary>禁用文字</summary>
        public const string TextDisabled = "ThemeTextDisabled";
        public const string TextDisabledBrush = "ThemeTextDisabledBrush";

        // ============ 边框 / 分隔 ============
        /// <summary>常规边框</summary>
        public const string BorderBrushKey = "ThemeBorder";
        public const string BorderBrush = "ThemeBorderBrush";
        /// <summary>浅色分隔线（Color 层 Key）</summary>
        public const string DividerBrush = "ThemeDividerBrush";
        public const string DividerBrushBrush = "ThemeDividerBrushBrush";

        // ============ 品牌 / 状态 ============
        /// <summary>品牌主色（沿用 #0066CC，Color 层 Key）</summary>
        public const string AccentColor = "ThemeAccentColor";
        public const string AccentBrush = "ThemeAccentBrush";
        /// <summary>成功 / 就绪状态（Color 层 Key）</summary>
        public const string SuccessColor = "ThemeSuccessColor";
        public const string SuccessBrush = "ThemeSuccessBrush";
        /// <summary>警告状态（Color 层 Key）</summary>
        public const string WarningColor = "ThemeWarningColor";
        public const string WarningBrush = "ThemeWarningBrush";
        /// <summary>错误 / 运行中状态（Color 层 Key）</summary>
        public const string ErrorColor = "ThemeErrorColor";
        public const string ErrorBrush = "ThemeErrorBrush";

        // ============ 画布专属 ============
        /// <summary>画布背景</summary>
        public const string CanvasBackground = "ThemeCanvasBackground";
        public const string CanvasBackgroundBrush = "ThemeCanvasBackgroundBrush";
        /// <summary>画布网格主线条</summary>
        public const string CanvasGridMajor = "ThemeCanvasGridMajor";
        public const string CanvasGridMajorBrush = "ThemeCanvasGridMajorBrush";
        /// <summary>画布网格次线条</summary>
        public const string CanvasGridMinor = "ThemeCanvasGridMinor";
        public const string CanvasGridMinorBrush = "ThemeCanvasGridMinorBrush";
        /// <summary>画布节点边框</summary>
        public const string CanvasNodeBorder = "ThemeCanvasNodeBorder";
        public const string CanvasNodeBorderBrush = "ThemeCanvasNodeBorderBrush";
        /// <summary>
        /// 画布棋盘格背景（整体 DrawingBrush，内部用 StaticResource 引用同字典笔刷，
        /// 随主题整体替换；DynamicResource 只保留在使用处的元素属性层，避免 Drawing 冻结导致内部引用失效）
        /// </summary>
        public const string CheckerboardBackgroundBrush = "ThemeCheckerboardBackgroundBrush";

        // ============ 图像区 / 缩略图（批次2） ============
        /// <summary>图像显示区背景</summary>
        public const string ImageAreaBackground = "ThemeImageAreaBackground";
        public const string ImageAreaBackgroundBrush = "ThemeImageAreaBackgroundBrush";
        /// <summary>图像缩略图底</summary>
        public const string ThumbnailBackground = "ThemeThumbnailBackground";
        public const string ThumbnailBackgroundBrush = "ThemeThumbnailBackgroundBrush";
        /// <summary>图像缩略图边框</summary>
        public const string ThumbnailBorder = "ThemeThumbnailBorder";
        public const string ThumbnailBorderBrush = "ThemeThumbnailBorderBrush";
        /// <summary>图像缩略图选中 / 运行态边框</summary>
        public const string ThumbnailSelectedBorder = "ThemeThumbnailSelectedBorder";
        public const string ThumbnailSelectedBorderBrush = "ThemeThumbnailSelectedBorderBrush";

        // ============ 表格（批次2） ============
        /// <summary>表格行底</summary>
        public const string TableRowBackground = "ThemeTableRowBackground";
        public const string TableRowBackgroundBrush = "ThemeTableRowBackgroundBrush";
        /// <summary>表格行悬停底</summary>
        public const string TableRowHoverBackground = "ThemeTableRowHoverBackground";
        public const string TableRowHoverBackgroundBrush = "ThemeTableRowHoverBackgroundBrush";
        /// <summary>表格行选中底</summary>
        public const string TableRowSelectedBackground = "ThemeTableRowSelectedBackground";
        public const string TableRowSelectedBackgroundBrush = "ThemeTableRowSelectedBackgroundBrush";
        /// <summary>表格表头底</summary>
        public const string TableHeaderBackground = "ThemeTableHeaderBackground";
        public const string TableHeaderBackgroundBrush = "ThemeTableHeaderBackgroundBrush";
        /// <summary>表格表头文字</summary>
        public const string TableHeaderText = "ThemeTableHeaderText";
        public const string TableHeaderTextBrush = "ThemeTableHeaderTextBrush";
        /// <summary>表格网格线</summary>
        public const string TableGridLine = "ThemeTableGridLine";
        public const string TableGridLineBrush = "ThemeTableGridLineBrush";

        // ============ 工具栏 / 菜单 / 面板表头（批次2） ============
        /// <summary>面板 / 工具栏背景</summary>
        public const string ToolBarBackground = "ThemeToolBarBackground";
        public const string ToolBarBackgroundBrush = "ThemeToolBarBackgroundBrush";
        /// <summary>右键菜单背景</summary>
        public const string ContextMenuBackground = "ThemeContextMenuBackground";
        public const string ContextMenuBackgroundBrush = "ThemeContextMenuBackgroundBrush";
        /// <summary>右键菜单项悬停背景</summary>
        public const string ContextMenuItemHover = "ThemeContextMenuItemHover";
        public const string ContextMenuItemHoverBrush = "ThemeContextMenuItemHoverBrush";
        /// <summary>面板表头背景</summary>
        public const string PanelHeaderBackground = "ThemePanelHeaderBackground";
        public const string PanelHeaderBackgroundBrush = "ThemePanelHeaderBackgroundBrush";

        // ============ 输入控件（批次2，参数模板） ============
        /// <summary>输入控件背景（文本框/下拉框）</summary>
        public const string InputBackground = "ThemeInputBackground";
        public const string InputBackgroundBrush = "ThemeInputBackgroundBrush";
        /// <summary>输入控件边框</summary>
        public const string InputBorder = "ThemeInputBorder";
        public const string InputBorderBrush = "ThemeInputBorderBrush";

        // ============ 滚动条（批次2） ============
        /// <summary>滚动条滑轨背景</summary>
        public const string ScrollBarBackground = "ThemeScrollBarBackground";
        public const string ScrollBarBackgroundBrush = "ThemeScrollBarBackgroundBrush";
        /// <summary>滚动条滑块（默认）</summary>
        public const string ScrollBarThumb = "ThemeScrollBarThumb";
        public const string ScrollBarThumbBrush = "ThemeScrollBarThumbBrush";
        /// <summary>滚动条滑块（悬停）</summary>
        public const string ScrollBarThumbHover = "ThemeScrollBarThumbHover";
        public const string ScrollBarThumbHoverBrush = "ThemeScrollBarThumbHoverBrush";
        /// <summary>滚动条滑块（按下）</summary>
        public const string ScrollBarThumbPressed = "ThemeScrollBarThumbPressed";
        public const string ScrollBarThumbPressedBrush = "ThemeScrollBarThumbPressedBrush";

        // ============ 状态栏 ============
        /// <summary>状态栏背景</summary>
        public const string StatusBarBackground = "ThemeStatusBarBackground";
        public const string StatusBarBackgroundBrush = "ThemeStatusBarBackgroundBrush";
        /// <summary>状态栏边框</summary>
        public const string StatusBarBorder = "ThemeStatusBarBorder";
        public const string StatusBarBorderBrush = "ThemeStatusBarBorderBrush";
        /// <summary>状态文字 / 状态点（就绪、相机连接等成功态）</summary>
        public const string StatusBarText = "ThemeStatusBarText";
        public const string StatusBarTextBrush = "ThemeStatusBarTextBrush";

        // ============ 顶栏 / 标题栏 ============
        /// <summary>顶栏图标 / 常规按钮前景</summary>
        public const string TitleBarIcon = "ThemeTitleBarIcon";
        public const string TitleBarIconBrush = "ThemeTitleBarIconBrush";
        /// <summary>顶栏图标悬停前景</summary>
        public const string TitleBarIconHover = "ThemeTitleBarIconHover";
        public const string TitleBarIconHoverBrush = "ThemeTitleBarIconHoverBrush";
        /// <summary>顶栏按钮悬停背景</summary>
        public const string TitleBarButtonHover = "ThemeTitleBarButtonHover";
        public const string TitleBarButtonHoverBrush = "ThemeTitleBarButtonHoverBrush";
        /// <summary>顶栏分割线 / 分隔条</summary>
        public const string TitleBarDivider = "ThemeTitleBarDivider";
        public const string TitleBarDividerBrush = "ThemeTitleBarDividerBrush";
        /// <summary>顶栏项目信息胶囊背景</summary>
        public const string TitleBarProjectInfoBg = "ThemeTitleBarProjectInfoBg";
        public const string TitleBarProjectInfoBgBrush = "ThemeTitleBarProjectInfoBgBrush";
        /// <summary>顶栏次要文字（如"工作流:"标签）</summary>
        public const string TitleBarTextSecondary = "ThemeTitleBarTextSecondary";
        public const string TitleBarTextSecondaryBrush = "ThemeTitleBarTextSecondaryBrush";
        /// <summary>顶栏主文字（Logo 名 / 项目名 / 关闭按钮等，随主题亮深字暗浅字）</summary>
        public const string TitleBarText = "ThemeTitleBarText";
        public const string TitleBarTextBrush = "ThemeTitleBarTextBrush";

        // ============ 工作区 Tab 标签条 ============
        /// <summary>Tab 标签条背景</summary>
        public const string TabStripBackground = "ThemeTabStripBackground";
        public const string TabStripBackgroundBrush = "ThemeTabStripBackgroundBrush";
        /// <summary>Tab 标签项背景</summary>
        public const string TabItemBackground = "ThemeTabItemBackground";
        public const string TabItemBackgroundBrush = "ThemeTabItemBackgroundBrush";
        /// <summary>Tab 标签项悬停背景</summary>
        public const string TabItemHover = "ThemeTabItemHover";
        public const string TabItemHoverBrush = "ThemeTabItemHoverBrush";
        /// <summary>Tab 标签项边框</summary>
        public const string TabItemBorder = "ThemeTabItemBorder";
        public const string TabItemBorderBrush = "ThemeTabItemBorderBrush";
        /// <summary>Tab 标签项文字</summary>
        public const string TabItemText = "ThemeTabItemText";
        public const string TabItemTextBrush = "ThemeTabItemTextBrush";

        // ============ 分割器 ============
        /// <summary>面板分割器背景</summary>
        public const string SplitterBackground = "ThemeSplitterBackground";
        public const string SplitterBackgroundBrush = "ThemeSplitterBackgroundBrush";
        /// <summary>面板分割器悬停背景</summary>
        public const string SplitterHover = "ThemeSplitterHover";
        public const string SplitterHoverBrush = "ThemeSplitterHoverBrush";

        // ============ 工具箱 ============
        /// <summary>工具箱背景</summary>
        public const string ToolboxBackground = "ThemeToolboxBackground";
        public const string ToolboxBackgroundBrush = "ThemeToolboxBackgroundBrush";
        /// <summary>工具箱分类标题背景</summary>
        public const string ToolboxCategoryBg = "ThemeToolboxCategoryBg";
        public const string ToolboxCategoryBgBrush = "ThemeToolboxCategoryBgBrush";
        /// <summary>工具箱分类 / 工具悬停背景</summary>
        public const string ToolboxCategoryHover = "ThemeToolboxCategoryHover";
        public const string ToolboxCategoryHoverBrush = "ThemeToolboxCategoryHoverBrush";
        /// <summary>工具箱工具项背景</summary>
        public const string ToolboxItemBg = "ThemeToolboxItemBg";
        public const string ToolboxItemBgBrush = "ThemeToolboxItemBgBrush";
        /// <summary>工具箱工具项边框</summary>
        public const string ToolboxItemBorder = "ThemeToolboxItemBorder";
        public const string ToolboxItemBorderBrush = "ThemeToolboxItemBorderBrush";
        /// <summary>工具箱工具项悬停背景</summary>
        public const string ToolboxItemHover = "ThemeToolboxItemHover";
        public const string ToolboxItemHoverBrush = "ThemeToolboxItemHoverBrush";
        /// <summary>工具箱主文字（工具名 / 分类名）</summary>
        public const string ToolboxTextPrimary = "ThemeToolboxTextPrimary";
        public const string ToolboxTextPrimaryBrush = "ThemeToolboxTextPrimaryBrush";
        /// <summary>工具箱次要文字（折叠图标等）</summary>
        public const string ToolboxTextSecondary = "ThemeToolboxTextSecondary";
        public const string ToolboxTextSecondaryBrush = "ThemeToolboxTextSecondaryBrush";
        /// <summary>工具箱次要文字弱化（数量等）</summary>
        public const string ToolboxTextTertiary = "ThemeToolboxTextTertiary";
        public const string ToolboxTextTertiaryBrush = "ThemeToolboxTextTertiaryBrush";
        /// <summary>工具箱切换模式按钮背景</summary>
        public const string ToolboxToggleBg = "ThemeToolboxToggleBg";
        public const string ToolboxToggleBgBrush = "ThemeToolboxToggleBgBrush";
        /// <summary>工具箱切换模式按钮边框</summary>
        public const string ToolboxToggleBorder = "ThemeToolboxToggleBorder";
        public const string ToolboxToggleBorderBrush = "ThemeToolboxToggleBorderBrush";
        /// <summary>工具箱计数红点</summary>
        public const string ToolboxBadge = "ThemeToolboxBadge";
        public const string ToolboxBadgeBrush = "ThemeToolboxBadgeBrush";
    }
}
