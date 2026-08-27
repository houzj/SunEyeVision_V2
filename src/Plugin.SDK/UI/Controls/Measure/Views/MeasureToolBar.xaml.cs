using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Commands;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Views
{
    /// <summary>
    /// 测量工具栏控件 - 数据驱动的检测引导几何选择栏
    /// 复用 ShapeToolBar 的数据驱动模式（图标 + 名称 + 快捷键 + 选中态）
    /// 用于边缘检测等工具的引导几何类型选择（直线/圆/圆弧）
    /// </summary>
    public partial class MeasureToolBar : UserControl
    {
        #region 依赖属性

        /// <summary>
        /// 测量工具项集合
        /// </summary>
        public static readonly DependencyProperty MeasureToolsProperty =
            DependencyProperty.Register(nameof(MeasureTools),
                typeof(ObservableCollection<MeasureToolItem>),
                typeof(MeasureToolBar),
                new PropertyMetadata(null));

        public ObservableCollection<MeasureToolItem>? MeasureTools
        {
            get => (ObservableCollection<MeasureToolItem>?)GetValue(MeasureToolsProperty);
            set => SetValue(MeasureToolsProperty, value);
        }

        #endregion

        #region 命令

        /// <summary>
        /// 几何类型选择命令（绑定到每个按钮的 Command）
        /// </summary>
        public ICommand SelectMeasureCommand { get; }

        #endregion

        #region 事件

        /// <summary>
        /// 几何类型选择变更事件
        /// 参数为选中的 MeasureGeometryType，null 表示取消选中（回到选择模式）
        /// </summary>
        public event EventHandler<MeasureGeometryType?>? MeasureSelectionChanged;

        #endregion

        public MeasureToolBar()
        {
            InitializeComponent();
            SelectMeasureCommand = new RelayCommand<MeasureToolItem>(OnMeasureButtonClick);
        }

        /// <summary>
        /// 通过代码设置选中的几何类型（不触发 MeasureSelectionChanged 事件）
        /// </summary>
        public void SetSelectedType(MeasureGeometryType? geometryType)
        {
            if (MeasureTools == null) return;

            foreach (var item in MeasureTools)
            {
                item.IsSelected = item.GeometryType == geometryType;
            }
        }

        /// <summary>
        /// 清除所有选中状态
        /// </summary>
        public void ClearSelection()
        {
            SetSelectedType(null);
        }

        /// <summary>
        /// 几何类型按钮点击处理
        /// </summary>
        private void OnMeasureButtonClick(MeasureToolItem? clickedItem)
        {
            if (clickedItem == null) return;

            // Toggle 行为：点击已选中的类型 → 取消选中
            if (clickedItem.IsSelected)
            {
                ClearSelection();
                MeasureSelectionChanged?.Invoke(this, null);
                return;
            }

            // 选中新类型
            ClearSelection();
            clickedItem.IsSelected = true;
            MeasureSelectionChanged?.Invoke(this, clickedItem.GeometryType);
        }
    }
}
