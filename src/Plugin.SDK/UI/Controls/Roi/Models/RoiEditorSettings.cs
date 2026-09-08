using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models
{
    /// <summary>
    /// 区域编辑器显示设置
    /// </summary>
    public class RoiEditorSettings : ObservableObject
    {
        #region 私有字段

        private double _labelFontSize = 12;
        private Color _labelForeground = Colors.Blue;
        private Color _labelBackground = Color.FromArgb(180, 255, 255, 255);
        private double _defaultStrokeThickness = 2;
        private double _selectedStrokeThickness = 2;
        private string _labelFontFamily = "Microsoft YaHei";
        private bool _showLabelOnPreview = true;
        private bool _showLabelOnEdit = true;
        private double _labelOffset = 8;
        private double _handleSize = 60;
        private double _hitTolerance = 0;
        private Color _selectionColor = Colors.Blue;
        private Color _previewColor = Colors.Red;
        private double _previewOpacity = 0.3;
        private double _defaultOpacity = 0.3;

        #endregion

        #region 属性

        /// <summary>
        /// 标签字体大小
        /// </summary>
        public double LabelFontSize
        {
            get => _labelFontSize;
            set => SetProperty(ref _labelFontSize, value);
        }

        /// <summary>
        /// 标签前景色
        /// </summary>
        public Color LabelForeground
        {
            get => _labelForeground;
            set => SetProperty(ref _labelForeground, value);
        }

        /// <summary>
        /// 标签背景色
        /// </summary>
        public Color LabelBackground
        {
            get => _labelBackground;
            set => SetProperty(ref _labelBackground, value);
        }

        /// <summary>
        /// 默认边框线宽
        /// </summary>
        public double DefaultStrokeThickness
        {
            get => _defaultStrokeThickness;
            set => SetProperty(ref _defaultStrokeThickness, value);
        }

        /// <summary>
        /// 选中时边框线宽
        /// </summary>
        public double SelectedStrokeThickness
        {
            get => _selectedStrokeThickness;
            set => SetProperty(ref _selectedStrokeThickness, value);
        }

        /// <summary>
        /// 标签字体族
        /// </summary>
        public string LabelFontFamily
        {
            get => _labelFontFamily;
            set => SetProperty(ref _labelFontFamily, value);
        }

        /// <summary>
        /// 预览时是否显示标签
        /// </summary>
        public bool ShowLabelOnPreview
        {
            get => _showLabelOnPreview;
            set => SetProperty(ref _showLabelOnPreview, value);
        }

        /// <summary>
        /// 编辑时是否显示标签
        /// </summary>
        public bool ShowLabelOnEdit
        {
            get => _showLabelOnEdit;
            set => SetProperty(ref _showLabelOnEdit, value);
        }

        /// <summary>
        /// 标签与区域的间距（像素）
        /// </summary>
        public double LabelOffset
        {
            get => _labelOffset;
            set => SetProperty(ref _labelOffset, value);
        }

        /// <summary>
        /// 手柄大小
        /// </summary>
        public double HandleSize
        {
            get => _handleSize;
            set => SetProperty(ref _handleSize, value);
        }

        /// <summary>
        /// 命中测试容差
        /// </summary>
        public double HitTolerance
        {
            get => _hitTolerance;
            set => SetProperty(ref _hitTolerance, value);
        }

        /// <summary>
        /// 选中颜色
        /// </summary>
        public Color SelectionColor
        {
            get => _selectionColor;
            set => SetProperty(ref _selectionColor, value);
        }

        /// <summary>
        /// 选中颜色（别名）
        /// </summary>
        public Color SelectedColor
        {
            get => _selectionColor;
            set => SetProperty(ref _selectionColor, value);
        }

        /// <summary>
        /// 预览颜色
        /// </summary>
        public Color PreviewColor
        {
            get => _previewColor;
            set => SetProperty(ref _previewColor, value);
        }

        /// <summary>
        /// 默认边框厚度（别名）
        /// </summary>
        public double DefaultBorderThickness => _defaultStrokeThickness;

        /// <summary>
        /// 选中边框厚度（别名）
        /// </summary>
        public double SelectedBorderThickness => _selectedStrokeThickness;

        /// <summary>
        /// 预览透明度
        /// </summary>
        public double PreviewOpacity
        {
            get => _previewOpacity;
            set => SetProperty(ref _previewOpacity, value);
        }

        /// <summary>
        /// 默认透明度
        /// </summary>
        public double DefaultOpacity
        {
            get => _defaultOpacity;
            set => SetProperty(ref _defaultOpacity, value);
        }

        /// <summary>
        /// 获取默认设置实例
        /// </summary>
        public static RoiEditorSettings Default { get; } = new RoiEditorSettings();

        #endregion

        #region 预设方案

        /// <summary>
        /// 创建高对比度设置
        /// </summary>
        public static RoiEditorSettings HighContrast()
        {
            return new RoiEditorSettings
            {
                LabelFontSize = 14,
                DefaultStrokeThickness = 1,
                SelectedStrokeThickness = 1,
                HandleSize = 70,
                SelectionColor = Colors.Cyan,
                PreviewColor = Colors.Magenta
            };
        }

        /// <summary>
        /// 创建紧凑设置
        /// </summary>
        public static RoiEditorSettings Compact()
        {
            return new RoiEditorSettings
            {
                LabelFontSize = 10,
                DefaultStrokeThickness = 10,
                SelectedStrokeThickness = 11,
                HandleSize = 8,
                LabelOffset = 4
            };
        }

        #endregion
    }
}
