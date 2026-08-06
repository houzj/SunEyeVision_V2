using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.ViewModels;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Roi.Views
{
    /// <summary>
    /// 形状工具栏控件 - 数据驱动的形状工具选择栏
    /// 所有形状的数据（图标、名称、可见性、快捷键）由 ShapeToolItem 集合驱动
    /// 每个形状的可见性/启用状态可单独控制
    /// </summary>
    public partial class ShapeToolBar : UserControl
    {
        #region 依赖属性

        /// <summary>
        /// 形状工具项集合
        /// </summary>
        public static readonly DependencyProperty ShapeToolsProperty =
            DependencyProperty.Register(nameof(ShapeTools),
                typeof(ObservableCollection<ShapeToolItem>),
                typeof(ShapeToolBar),
                new PropertyMetadata(null));

        public ObservableCollection<ShapeToolItem>? ShapeTools
        {
            get => (ObservableCollection<ShapeToolItem>?)GetValue(ShapeToolsProperty);
            set => SetValue(ShapeToolsProperty, value);
        }

        #endregion

        #region 命令

        /// <summary>
        /// 形状选择命令（绑定到每个形状按钮的 Command）
        /// </summary>
        public ICommand SelectShapeCommand { get; }

        #endregion

        #region 事件

        /// <summary>
        /// 形状选择变更事件
        /// 参数为选中的 ShapeType，null 表示取消选中（回到选择模式）
        /// </summary>
        public event EventHandler<ShapeType?>? ShapeSelectionChanged;

        #endregion

        public ShapeToolBar()
        {
            InitializeComponent();
            SelectShapeCommand = new RelayCommand<ShapeToolItem>(OnShapeButtonClick);
        }

        /// <summary>
        /// 通过代码设置选中的形状（不触发 ShapeSelectionChanged 事件）
        /// 用于键盘快捷键或外部 API 调用
        /// </summary>
        public void SetSelectedShape(ShapeType? shapeType)
        {
            if (ShapeTools == null) return;

            foreach (var item in ShapeTools)
            {
                item.IsSelected = item.ShapeType == shapeType;
            }
        }

        /// <summary>
        /// 清除所有选中状态
        /// </summary>
        public void ClearSelection()
        {
            SetSelectedShape(null);
        }

        /// <summary>
        /// 形状按钮点击处理
        /// </summary>
        private void OnShapeButtonClick(ShapeToolItem? clickedItem)
        {
            if (clickedItem == null) return;

            // Toggle 行为：点击已选中的形状 → 取消选中
            if (clickedItem.IsSelected)
            {
                ClearSelection();
                ShapeSelectionChanged?.Invoke(this, null);
                return;
            }

            // 选中新形状
            ClearSelection();
            clickedItem.IsSelected = true;
            ShapeSelectionChanged?.Invoke(this, clickedItem.ShapeType);
        }
    }
}
