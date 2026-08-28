using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Commands;
using SunEyeVision.Plugin.SDK.UI.Controls.Common.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Common.Views
{
    /// <summary>
    /// 通用数据驱动工具栏 - 类型无关的工具选择栏
    /// </summary>
    /// <remarks>
    /// 通过 ItemsSource 提供 ToolItem 集合，SelectedValue 承载选中的业务枚举值（object?）。
    /// Toggle 行为内建：点击已选中项 → 取消选中（SelectedValue 置 null）。
    /// 复用此控件的场景：
    ///   形状选择（区域编辑器）、卡尺引导几何选择（边缘检测）、未来任意工具类型选择。
    /// </remarks>
    public partial class ToolBarControl : UserControl
    {
        #region 依赖属性

        /// <summary>
        /// 工具项集合（数据驱动）
        /// </summary>
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource),
                typeof(IEnumerable<ToolItem>),
                typeof(ToolBarControl),
                new PropertyMetadata(null, OnItemsSourceChanged));

        /// <summary>
        /// 工具项集合
        /// </summary>
        public IEnumerable<ToolItem>? ItemsSource
        {
            get => (IEnumerable<ToolItem>?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        /// <summary>
        /// 当前选中的业务值（object?，null=未选中），双向绑定
        /// </summary>
        public static readonly DependencyProperty SelectedValueProperty =
            DependencyProperty.Register(nameof(SelectedValue),
                typeof(object),
                typeof(ToolBarControl),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedValueChanged));

        /// <summary>
        /// 当前选中的业务值（object?，null=未选中），双向绑定
        /// </summary>
        public object? SelectedValue
        {
            get => GetValue(SelectedValueProperty);
            set => SetValue(SelectedValueProperty, value);
        }

        #endregion

        #region 命令

        /// <summary>
        /// 工具选择命令（绑定到每个按钮的 Command，Toggle 行为内建）
        /// </summary>
        public ICommand SelectToolCommand { get; }

        #endregion

        #region 事件

        /// <summary>
        /// 工具选择变更事件
        /// 参数为选中的业务值（object?），null 表示取消选中（回到选择模式）
        /// </summary>
        public event EventHandler<object?>? SelectionChanged;

        #endregion

        public ToolBarControl()
        {
            InitializeComponent();
            SelectToolCommand = new RelayCommand<ToolItem>(OnToolButtonClick);
        }

        /// <summary>
        /// 通过代码设置选中的业务值（不触发 SelectionChanged 事件）
        /// </summary>
        public void SetSelectedValue(object? value)
        {
            // 直接设置 DP 走 OnSelectedValueChanged，但需避免事件递归（用标志位）
            _syncingSelection = true;
            SelectedValue = value;
            ApplySelection(value);
            _syncingSelection = false;
        }

        /// <summary>
        /// 清除所有选中状态（不触发 SelectionChanged 事件）
        /// </summary>
        public void ClearSelection()
        {
            SetSelectedValue(null);
        }

        #region 内部逻辑

        private bool _syncingSelection;

        /// <summary>
        /// 工具按钮点击处理：Toggle 行为
        /// </summary>
        private void OnToolButtonClick(ToolItem? clickedItem)
        {
            if (clickedItem == null || !clickedItem.IsEnabled) return;

            // 点击已选中的类型 → 取消选中
            if (clickedItem.IsSelected)
            {
                SetSelectedValueInternal(null);
                SelectionChanged?.Invoke(this, null);
                return;
            }

            // 选中新类型
            SetSelectedValueInternal(clickedItem.Value);
            SelectionChanged?.Invoke(this, clickedItem.Value);
        }

        /// <summary>
        /// 更新选中状态并同步 SelectedValue（不触发 SelectionChanged，避免递归）
        /// </summary>
        private void SetSelectedValueInternal(object? value)
        {
            _syncingSelection = true;
            SelectedValue = value;
            ApplySelection(value);
            _syncingSelection = false;
        }

        /// <summary>
        /// 将选中态应用到所有工具项
        /// </summary>
        private void ApplySelection(object? value)
        {
            if (ItemsSource == null) return;
            foreach (var item in ItemsSource)
            {
                item.IsSelected = ReferenceEquals(item.Value, value) ||
                                  (item.Value != null && item.Value.Equals(value));
            }
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ToolBarControl)d;
            // 集合变化后刷新选中态
            control.ApplySelection(control.SelectedValue);
        }

        private static void OnSelectedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ToolBarControl)d;
            // 双向绑定时外部修改 SelectedValue → 同步 UI 选中态（但不触发 SelectionChanged，避免递归）
            if (!control._syncingSelection)
            {
                control.ApplySelection(e.NewValue);
            }
        }

        #endregion
    }
}
