using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.ViewModels
{
    /// <summary>
    /// 参数面板视图模型
    /// </summary>
    public class ParameterPanelViewModel : ObservableObject
    {
        private readonly DataSourceQueryService? _dataProvider;
        private ShapeType _currentShapeType;
        private bool _isEditable = true;
        private ParamSettingItem? _selectedParameter;

        /// <summary>
        /// 参数绑定项列表
        /// </summary>
        public ObservableCollection<ParamSettingItem> Parameters { get; } = new();

        /// <summary>
        /// 当前形状类型
        /// </summary>
        public ShapeType CurrentShapeType
        {
            get => _currentShapeType;
            set
            {
                if (SetProperty(ref _currentShapeType, value))
                {
                    UpdateParameterDefinitions();
                }
            }
        }

        /// <summary>
        /// 是否可编辑
        /// </summary>
        public bool IsEditable
        {
            get => _isEditable;
            set => SetProperty(ref _isEditable, value);
        }

        /// <summary>
        /// 选中的参数（用于打开绑定选择器）
        /// </summary>
        public ParamSettingItem? SelectedParameter
        {
            get => _selectedParameter;
            set => SetProperty(ref _selectedParameter, value);
        }

        /// <summary>
        /// 命令
        /// </summary>
        public ICommand BindParameterCommand { get; }
        public ICommand ClearBindingCommand { get; }

        /// <summary>
        /// 参数绑定变更事件
        /// </summary>
        public event EventHandler<ParamSettingChangedEventArgs>? ParamSettingChanged;

        public ParameterPanelViewModel(DataSourceQueryService? dataProvider = null)
        {
            _dataProvider = dataProvider;

            BindParameterCommand = new RelayCommand<ParamSettingItem>(BindParameter);
            ClearBindingCommand = new RelayCommand<ParamSettingItem>(ClearBinding);
        }

        /// <summary>
        /// 更新参数定义（根据形状类型）
        /// </summary>
        private void UpdateParameterDefinitions()
        {
            // 清空参数列表
            Parameters.Clear();

            // 根据形状类型定义参数
            var parameterDefs = GetParameterDefinitions(_currentShapeType);

            // 添加参数项
            foreach (var def in parameterDefs)
            {
                Parameters.Add(new ParamSettingItem
                {
                    ParameterName = def.Name,
                    DisplayName = def.DisplayName,
                    DataType = def.DataType
                });
            }
        }

        /// <summary>
        /// 获取参数定义
        /// </summary>
        private List<(string Name, string DisplayName, string DataType)> GetParameterDefinitions(ShapeType shapeType)
        {
            return shapeType switch
            {
                ShapeType.Point => new List<(string, string, string)>
                {
                    ("CenterX", "中心X", "double"),
                    ("CenterY", "中心Y", "double")
                },
                ShapeType.Line => new List<(string, string, string)>
                {
                    ("StartX", "起点X", "double"),
                    ("StartY", "起点Y", "double"),
                    ("EndX", "终点X", "double"),
                    ("EndY", "终点Y", "double")
                },
                ShapeType.Circle => new List<(string, string, string)>
                {
                    ("CenterX", "中心X", "double"),
                    ("CenterY", "中心Y", "double"),
                    ("Radius", "半径", "double")
                },
                ShapeType.Rectangle => new List<(string, string, string)>
                {
                    ("CenterX", "中心X", "double"),
                    ("CenterY", "中心Y", "double"),
                    ("Width", "宽度", "double"),
                    ("Height", "高度", "double")
                },
                ShapeType.RotatedRectangle => new List<(string, string, string)>
                {
                    ("CenterX", "中心X", "double"),
                    ("CenterY", "中心Y", "double"),
                    ("Width", "宽度", "double"),
                    ("Height", "高度", "double"),
                    ("Angle", "角度", "double")
                },
                ShapeType.Polygon => new List<(string, string, string)>
                {
                    ("Points", "顶点", "Point2D[]")
                },
                _ => new List<(string, string, string)>()
            };
        }

        /// <summary>
        /// 绑定参数（打开选择器）
        /// </summary>
        private void BindParameter(ParamSettingItem? item)
        {
            if (item == null) return;
            SelectedParameter = item;
            // 实际绑定在View层通过NodeSelectorPopup完成
        }

        /// <summary>
        /// 清除绑定
        /// </summary>
        private void ClearBinding(ParamSettingItem? item)
        {
            if (item == null) return;

            item.Source = null;
            item.DisplayPath = string.Empty;
            item.CurrentValue = null;

            ParamSettingChanged?.Invoke(this, new ParamSettingChangedEventArgs(item.ParameterName, null));
        }

        /// <summary>
        /// 应用绑定选择结果
        /// </summary>
        public void ApplyBindingSelection(NodeOutputInfo selectedNode)
        {
            if (SelectedParameter == null) return;

            var source = new NodeOutputSource(
                selectedNode.NodeId,
                selectedNode.OutputName,
                null,
                selectedNode.PropertyPath)
            {
                DataType = SelectedParameter.DataType
            };

            SelectedParameter.Source = source;
            SelectedParameter.DisplayPath = selectedNode.DisplayPath;

            // 订阅值变更
            SubscribeToValue(SelectedParameter, source);

            ParamSettingChanged?.Invoke(this, new ParamSettingChangedEventArgs(SelectedParameter.ParameterName, source));
        }

        /// <summary>
        /// 订阅值变更（通过 GetPropertyValue 实时查询，DSQS 已无本地缓存/订阅机制）
        /// </summary>
        private void SubscribeToValue(ParamSettingItem item, NodeOutputSource source)
        {
            if (_dataProvider == null) return;

            // 实时查询 SSOT（DSQS 通过 _ssotReader 委托读取 WorkflowContext）
            item.CurrentValue = _dataProvider.GetPropertyValue(source.NodeId, source.PropertyPath);
        }

        /// <summary>
        /// 从ComputedRegion加载绑定
        /// </summary>
        public void LoadFromComputedRegion(ComputedRegion computedRegion)
        {
            CurrentShapeType = computedRegion.TargetShapeType;

            foreach (var param in Parameters)
            {
                if (computedRegion.ParameterBindings.TryGetValue(param.ParameterName, out var source))
                {
                    param.Source = source;

                    if (source is NodeOutputSource nodeSource && _dataProvider != null)
                    {
                        param.DisplayPath = $"{nodeSource.NodeId}.{nodeSource.PropertyPath}";

                        SubscribeToValue(param, nodeSource);
                    }
                }
            }
        }

        /// <summary>
        /// 导出到ComputedRegion
        /// </summary>
        public ComputedRegion ExportToComputedRegion()
        {
            var result = new ComputedRegion
            {
                TargetShapeType = CurrentShapeType
            };

            foreach (var param in Parameters)
            {
                if (param.Source != null)
                {
                    result.SetParameterBinding(param.ParameterName, param.Source);
                }
            }

            return result;
        }

    }


    /// <summary>
    /// 参数绑定变更事件参数
    /// </summary>
    public class ParamSettingChangedEventArgs : EventArgs
    {
        public string ParameterName { get; }
        public ParameterSource? NewSource { get; }

        public ParamSettingChangedEventArgs(string parameterName, ParameterSource? newSource)
        {
            ParameterName = parameterName;
            NewSource = newSource;
        }
    }
}
