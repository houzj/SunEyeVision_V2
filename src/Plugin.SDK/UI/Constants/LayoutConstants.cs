namespace SunEyeVision.Plugin.SDK.UI.Constants
{
    /// <summary>
    /// 项目统一布局常量 - 单一来源
    /// </summary>
    /// <remarks>
    /// 此类是项目所有布局值的唯一定义来源：
    /// - C# 代码直接使用常量
    /// - XAML 通过 x:Static 引用常量
    /// - 修改此处，所有地方同步更新
    /// 
    /// 规则：禁止在其他地方定义布局值！
    /// 
    /// 布局体系：
    /// - 控件高度：24px / 28px / 32px / 36px
    /// - 控件宽度：60px / 80px / 100px / 120px
    /// - 间距：4px / 8px / 12px / 16px / 24px
    /// - 圆角：2px / 4px / 8px
    /// </remarks>
    public static class LayoutConstants
    {
        #region 控件高度

        /// <summary>
        /// 控件高度 - 小号（24px）
        /// </summary>
        public const double ControlHeightSmall = 24;

        /// <summary>
        /// 控件高度 - 中号（28px）
        /// </summary>
        public const double ControlHeightMedium = 28;

        /// <summary>
        /// 控件高度 - 大号（32px）
        /// </summary>
        public const double ControlHeightLarge = 32;

        /// <summary>
        /// 控件高度 - 超大号（36px）
        /// </summary>
        public const double ControlHeightXLarge = 36;

        #endregion

        #region 控件宽度

        /// <summary>
        /// 控件最小宽度 - 小号（60px）
        /// </summary>
        public const double ControlMinWidthSmall = 60;

        /// <summary>
        /// 控件最小宽度 - 中号（80px）
        /// </summary>
        public const double ControlMinWidthMedium = 80;

        /// <summary>
        /// 控件最小宽度 - 大号（100px）
        /// </summary>
        public const double ControlMinWidthLarge = 100;

        /// <summary>
        /// 控件最小宽度 - 超大号（120px）
        /// </summary>
        public const double ControlMinWidthXLarge = 120;

        #endregion

        #region 间距

        /// <summary>
        /// 间距 - 超小（4px）
        /// </summary>
        public const double SpacingXSmall = 4;

        /// <summary>
        /// 间距 - 极小（2px）
        /// </summary>
        public const double SpacingXXSmall = 2;

        /// <summary>
        /// 间距 - 小（8px）
        /// </summary>
        public const double SpacingSmall = 8;

        /// <summary>
        /// 间距 - 中（12px）
        /// </summary>
        public const double SpacingMedium = 12;

        /// <summary>
        /// 间距 - 大（16px）
        /// </summary>
        public const double SpacingLarge = 16;

        /// <summary>
        /// 间距 - 超大（24px）
        /// </summary>
        public const double SpacingXLarge = 24;

        #endregion

        #region 内边距（Padding）

        /// <summary>
        /// 内边距 - 紧凑（4px）
        /// </summary>
        public const double PaddingCompact = 4;

        /// <summary>
        /// 内边距 - 标准（8px）
        /// </summary>
        public const double PaddingStandard = 8;

        /// <summary>
        /// 内边距 - 宽松（12px）
        /// </summary>
        public const double PaddingLoose = 12;

        /// <summary>
        /// 内边距 - 超宽松（16px）
        /// </summary>
        public const double PaddingXLoose = 16;

        /// <summary>
        /// 按钮内边距 - 水平12px，垂直4px
        /// </summary>
        public static readonly System.Windows.Thickness ButtonPadding = new System.Windows.Thickness(SpacingMedium, PaddingCompact, SpacingMedium, PaddingCompact);

        /// <summary>
        /// 卡片内边距 - 水平8px，垂直4px
        /// </summary>
        public static readonly System.Windows.Thickness CardPadding = new System.Windows.Thickness(PaddingStandard, PaddingCompact, PaddingStandard, PaddingCompact);

        /// <summary>
        /// 左边距 - 极小（2px）
        /// </summary>
        public static readonly System.Windows.Thickness MarginLeftXXSmall = new System.Windows.Thickness(SpacingXXSmall, 0, 0, 0);

        /// <summary>
        /// 上边距 - 极小（2px）
        /// </summary>
        public static readonly System.Windows.Thickness MarginTopXXSmall = new System.Windows.Thickness(0, SpacingXXSmall, 0, 0);

        /// <summary>
        /// 垂直内边距 - 极小（上下各2px）
        /// </summary>
        public static readonly System.Windows.Thickness PaddingVerticalXXSmall = new System.Windows.Thickness(0, SpacingXXSmall, 0, SpacingXXSmall);

        /// <summary>
        /// 左边距 - 小（4px）
        /// </summary>
        public static readonly System.Windows.Thickness MarginLeftXSmall = new System.Windows.Thickness(SpacingXSmall, 0, 0, 0);

        /// <summary>
        /// 左边距 - 标准（8px）
        /// </summary>
        public static readonly System.Windows.Thickness MarginLeftSmall = new System.Windows.Thickness(SpacingSmall, 0, 0, 0);

        /// <summary>
        /// 右边距 - 标准（8px）
        /// </summary>
        public static readonly System.Windows.Thickness MarginRightSmall = new System.Windows.Thickness(0, 0, SpacingSmall, 0);

        /// <summary>
        /// 右边距 - 中（12px）
        /// </summary>
        public static readonly System.Windows.Thickness MarginRightMedium = new System.Windows.Thickness(0, 0, SpacingMedium, 0);

        /// <summary>
        /// 上边距 - 小（4px）
        /// </summary>
        public static readonly System.Windows.Thickness MarginTopXSmall = new System.Windows.Thickness(0, SpacingXSmall, 0, 0);

        /// <summary>
        /// 上边距 - 中（12px）
        /// </summary>
        public static readonly System.Windows.Thickness MarginTopMedium = new System.Windows.Thickness(0, SpacingMedium, 0, 0);

        /// <summary>
        /// 下边距 - 标准（8px）
        /// </summary>
        public static readonly System.Windows.Thickness MarginBottomSmall = new System.Windows.Thickness(0, 0, 0, SpacingSmall);

        /// <summary>
        /// 下边距 - 中（12px）
        /// </summary>
        public static readonly System.Windows.Thickness MarginBottomMedium = new System.Windows.Thickness(0, 0, 0, SpacingMedium);

        /// <summary>
        /// 水平内边距 - 标准（左右各8px）
        /// </summary>
        public static readonly System.Windows.Thickness PaddingHorizontalStandard = new System.Windows.Thickness(PaddingStandard, 0, PaddingStandard, 0);

        /// <summary>
        /// 水平内边距 - 小（左右各8px）
        /// </summary>
        public static readonly System.Windows.Thickness PaddingHorizontalSmall = new System.Windows.Thickness(SpacingSmall, 0, SpacingSmall, 0);

        /// <summary>
        /// 标题内边距 - 水平12px，垂直4px
        /// </summary>
        public static readonly System.Windows.Thickness TitlePadding = new System.Windows.Thickness(SpacingMedium, PaddingCompact, SpacingMedium, PaddingCompact);

        /// <summary>
        /// 分组内边距 - 水平16px，垂直4px
        /// </summary>
        public static readonly System.Windows.Thickness GroupPadding = new System.Windows.Thickness(SpacingLarge, PaddingCompact, SpacingLarge, PaddingCompact);

        /// <summary>
        /// 列表项内边距 - 水平8px，垂直4px
        /// </summary>
        public static readonly System.Windows.Thickness ListItemPadding = new System.Windows.Thickness(SpacingSmall, PaddingCompact, SpacingSmall, PaddingCompact);

        /// <summary>
        /// 内容区域内边距 - 左右12px，上4px，下12px
        /// </summary>
        public static readonly System.Windows.Thickness ContentAreaPadding = new System.Windows.Thickness(SpacingMedium, PaddingCompact, SpacingMedium, SpacingMedium);

        /// <summary>
        /// 边框厚度 - 左右下有边框（左1px，上0，右1px，下1px）
        /// </summary>
        public static readonly System.Windows.Thickness BorderLeftRightBottom = new System.Windows.Thickness(BorderThicknessThin, 0, BorderThicknessThin, BorderThicknessThin);

        /// <summary>
        /// 边框厚度 - 仅下边框（左0，上0，右0，下1.5px）
        /// </summary>
        public static readonly System.Windows.Thickness BorderBottomOnly = new System.Windows.Thickness(0, 0, 0, BorderThicknessStandard);

        /// <summary>
        /// 圆角 - 右侧圆角（左上0，右上4px，右下4px，左下0）
        /// </summary>
        public static readonly System.Windows.CornerRadius CornerRadiusRight = new System.Windows.CornerRadius(0, CornerRadiusMedium, CornerRadiusMedium, 0);

        #endregion

        #region 圆角

        /// <summary>
        /// 圆角 - 小（2px）
        /// </summary>
        public const double CornerRadiusSmall = 2;

        /// <summary>
        /// 圆角 - 中（4px）
        /// </summary>
        public const double CornerRadiusMedium = 4;

        /// <summary>
        /// 圆角 - 大（8px）
        /// </summary>
        public const double CornerRadiusLarge = 8;

        #endregion

        #region 边框

        /// <summary>
        /// 边框宽度 - 细（1px）
        /// </summary>
        public const double BorderThicknessThin = 1;

        /// <summary>
        /// 边框宽度 - 标准（1.5px）
        /// </summary>
        public const double BorderThicknessStandard = 1.5;

        /// <summary>
        /// 边框宽度 - 粗（2px）
        /// </summary>
        public const double BorderThicknessThick = 2;

        #endregion

        #region 图标

        /// <summary>
        /// 图标尺寸 - 小（12px）
        /// </summary>
        public const double IconSizeSmall = 12;

        /// <summary>
        /// 图标尺寸 - 中（16px）
        /// </summary>
        public const double IconSizeMedium = 16;

        /// <summary>
        /// 图标尺寸 - 大（20px）
        /// </summary>
        public const double IconSizeLarge = 20;

        /// <summary>
        /// 图标尺寸 - 超大（24px）
        /// </summary>
        public const double IconSizeXLarge = 24;

        /// <summary>
        /// 按钮图标尺寸（18px）
        /// </summary>
        public const double ButtonIconSize = 18;

        /// <summary>
        /// 按钮图标尺寸 - 小（13px）
        /// </summary>
        public const double ButtonIconSizeSmall = 13;

        /// <summary>
        /// 缩略图宽度 - 大（48px）
        /// </summary>
        public const double ThumbnailWidthLarge = 48;

        /// <summary>
        /// 缩略图高度 - 中（36px）
        /// </summary>
        public const double ThumbnailHeightMedium = 36;

        /// <summary>
        /// 弹出面板最大高度（300px）
        /// </summary>
        public const double PopupMaxHeight = 300;

        /// <summary>
        /// 列表最大高度（250px）
        /// </summary>
        public const double ListMaxHeight = 250;

        #endregion
    }
}
