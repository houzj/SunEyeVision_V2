using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.ViewModels;

namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Views
{
    /// <summary>
    /// 区域订阅面板
    /// </summary>
    public partial class RoiSubscribePanel : UserControl
    {
        /// <summary>
        /// AvailableDataSources 依赖属性（由父级 RoiEditorControl 透传）
        /// </summary>
        public static readonly DependencyProperty AvailableDataSourcesProperty =
            DependencyProperty.Register(
                nameof(AvailableDataSources),
                typeof(System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>),
                typeof(RoiSubscribePanel),
                new PropertyMetadata(null));

        /// <summary>
        /// 可用数据源集合
        /// </summary>
        public System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>? AvailableDataSources
        {
            get => (System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>?)GetValue(AvailableDataSourcesProperty);
            set => SetValue(AvailableDataSourcesProperty, value);
        }

        public RoiSubscribePanel()
        {
            InitializeComponent();
            AddHandler(ParamBinding.BindingSourceSelectedEvent, new RoutedEventHandler(OnBindingSourceSelected));
            DataContextChanged += OnPanelDataContextChanged;

            // 整对象订阅行绑定目标 = ROI 契约（RoiShape[]，词目 ROI）：
            // 传 DataType 后，ParamBinding 绑定树按词目定型（ArrayTreeFormatter.BuildRoiTypedChildren）：
            // ROI 目标 → 检测区图形[] → 整体 / 各类型集合叶（点谁绑谁）；
            // 字段级绑定（圆心X/半径…）自动出现在「订阅几何参数」行的 double 参数树中（类型容器→字段叶）。
            // （RoiShape[] 为数组类型无法用 XAML x:Type 表达，故在 code-behind 设置。）
            WholeRegionBinding.DataType = typeof(RoiShape[]);
        }

        /// <summary>
        /// 区域源行显示由 VM 模型显式驱动（与参数行一致、不依赖隐性 OneWay 绑定是否生效）：
        /// 订阅面板持有区域行控件引用，监听 VM 的区域源/子模式变化，强制把区域行文本同步为模型状态
        /// （有绑定 → 翻译显示；模式切走/清除 → 清空），杜绝"模型已清、控件残留旧文本"的 UI/数据漂移。
        /// </summary>
        private void OnPanelDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ParameterPanelViewModel oldVm) oldVm.PropertyChanged -= OnVmPropertyChanged;
            if (e.NewValue is ParameterPanelViewModel newVm)
            {
                newVm.PropertyChanged += OnVmPropertyChanged;
                SyncWholeRegionDisplay();
            }
        }

        private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ParameterPanelViewModel.WholeRegionBindingPath)
                or nameof(ParameterPanelViewModel.WholeRegionSetting)
                or nameof(ParameterPanelViewModel.IsWholeRegionMode)
                or nameof(ParameterPanelViewModel.IsGeometricMode))
            {
                SyncWholeRegionDisplay();
            }
        }

        private void SyncWholeRegionDisplay()
        {
            var vm = ViewModel;
            if (vm == null) return;
            WholeRegionBinding.SyncDisplay(vm.WholeRegionBindingPath);
        }

        private ParameterPanelViewModel? ViewModel => DataContext as ParameterPanelViewModel;

        #region 模式切换

        private void WholeRegionRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.SubscribeSubMode = RoiSubscribeSubMode.WholeRegion;
        }

        private void GeometricRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.SubscribeSubMode = RoiSubscribeSubMode.GeometricParameters;
        }

        #endregion

        #region 绑定源选择处理

        private void OnBindingSourceSelected(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not ParamBinding pb) return;
            var vm = ViewModel;
            if (vm == null) return;

            var bindingSource = pb.BindingSource;

            if (pb.ParameterName == "WholeRegion")
            {
                if (string.IsNullOrEmpty(bindingSource))
                {
                    vm.ClearWholeRegion();
                }
                else
                {
                    var info = ResolveNodeOutputInfo(bindingSource);
                    if (info != null) vm.ApplyWholeRegionSelection(info);
                }
                return;
            }

            // 几何参数
            var item = vm.Parameters.FirstOrDefault(p => p.ParameterName == pb.ParameterName);
            if (item == null) return;

            if (string.IsNullOrEmpty(bindingSource))
            {
                vm.ClearBindingCommand.Execute(item);
            }
            else
            {
                var info = ResolveNodeOutputInfo(bindingSource);
                if (info != null)
                {
                    vm.SelectedParameter = item;
                    vm.ApplyBindingSelection(info);
                }
            }
        }

        /// <summary>
        /// 由 ParamBinding 的 BindingSource 字符串反查构造 NodeOutputInfo
        /// </summary>
        private NodeOutputInfo? ResolveNodeOutputInfo(string bindingSource)
        {
            if (string.IsNullOrEmpty(bindingSource)) return null;

            // 全局变量：__GlobalVars__.变量名
            if (bindingSource.StartsWith("__GlobalVars__."))
            {
                var varName = bindingSource.Substring("__GlobalVars__.".Length);
                return new NodeOutputInfo
                {
                    NodeId = "__GlobalVars__",
                    OutputName = varName,
                    PropertyPath = varName
                };
            }

            // nodeId（GUID）不含 '.'；其后整段都是属性路径（可含 [Type=X][0].Geometry.字段 多点），
            // 不能用 Split('.') 只取第二段，否则字段下钻路径（如 .Geometry.Center.X）会被切丢，
            // 导致持久化的绑定源不完整、重开窗回显找不到树节点而回退原文。
            var dotIndex = bindingSource.IndexOf('.');
            if (dotIndex <= 0 || dotIndex == bindingSource.Length - 1)
                return null;
            var nodeId = bindingSource[..dotIndex];
            var propertyFullName = bindingSource[(dotIndex + 1)..];

            // 去掉 [N] 数组索引，仅用属性名反查（属性全名可能含 [Type=X][0].Geometry.字段，不能整段反查）
            var propertyName = propertyFullName;
            var bracket = propertyName.IndexOf('[');
            if (bracket >= 0) propertyName = propertyName.Substring(0, bracket);

            var ds = AvailableDataSources?
                .FirstOrDefault(d => d.SourceNodeId == nodeId && d.PropertyName == propertyName);
            if (ds == null) return null;

            return new NodeOutputInfo
            {
                NodeId = ds.SourceNodeId,
                NodeName = ds.SourceNodeName,
                OutputName = ds.PropertyName,
                // 保留 [Type=]/[i] 后缀作为运行期解析路径：经 GetPropertyValue → OutputVariableResolver(SSOT) 按图形类型/单元素过滤
                PropertyPath = propertyFullName
            };
        }

        #endregion
    }
}
