using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.ViewModels;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Views
{
    /// <summary>
    /// 区域订阅面板
    /// </summary>
    public partial class RegionSubscribePanel : UserControl
    {
        /// <summary>
        /// AvailableDataSources 依赖属性（由父级 RegionEditorControl 透传）
        /// </summary>
        public static readonly DependencyProperty AvailableDataSourcesProperty =
            DependencyProperty.Register(
                nameof(AvailableDataSources),
                typeof(System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>),
                typeof(RegionSubscribePanel),
                new PropertyMetadata(null));

        /// <summary>
        /// 可用数据源集合
        /// </summary>
        public System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>? AvailableDataSources
        {
            get => (System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>?)GetValue(AvailableDataSourcesProperty);
            set => SetValue(AvailableDataSourcesProperty, value);
        }

        public RegionSubscribePanel()
        {
            InitializeComponent();
            AddHandler(ParamBinding.BindingSourceSelectedEvent, new RoutedEventHandler(OnBindingSourceSelected));
        }

        private ParameterPanelViewModel? ViewModel => DataContext as ParameterPanelViewModel;

        #region 模式切换

        private void WholeRegionRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.SubscribeSubMode = RegionSubscribeSubMode.WholeRegion;
        }

        private void GeometricRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.SubscribeSubMode = RegionSubscribeSubMode.GeometricParameters;
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

            var parts = bindingSource.Split('.');
            var nodeId = parts[0];
            var propertyFullName = parts.Length > 1 ? parts[1] : string.Empty;

            // 去掉 [N] 数组索引，仅用属性名反查
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
