using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.ViewModels
{
    /// <summary>
    /// 区域订阅子模式
    /// </summary>
    public enum RegionSubscribeSubMode
    {
        /// <summary>
        /// 订阅整个区域（固定区域，引用其他节点的区域输出）
        /// </summary>
        WholeRegion,

        /// <summary>
        /// 订阅几何参数（计算区域，每个参数可单独绑定）
        /// </summary>
        GeometricParameters
    }

    /// <summary>
    /// 参数面板视图模型（区域订阅统一驱动：整区域 + 几何参数双模式）
    /// </summary>
    public class ParameterPanelViewModel : ObservableObject
    {
        private readonly DataSourceQueryService? _dataProvider;
        private ShapeType _currentShapeType;
        private bool _isEditable = true;
        private ParamSettingItem? _selectedParameter;
        private RegionSubscribeSubMode _subscribeSubMode = RegionSubscribeSubMode.GeometricParameters;
        private ParameterSource? _wholeRegionSource;

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
        /// 订阅子模式（整区域 / 几何参数）
        /// </summary>
        public RegionSubscribeSubMode SubscribeSubMode
        {
            get => _subscribeSubMode;
            set
            {
                if (SetProperty(ref _subscribeSubMode, value))
                {
                    OnPropertyChanged(nameof(IsWholeRegionMode));
                    OnPropertyChanged(nameof(IsGeometricMode));
                }
            }
        }

        /// <summary>
        /// 是否为整区域订阅模式
        /// </summary>
        public bool IsWholeRegionMode => SubscribeSubMode == RegionSubscribeSubMode.WholeRegion;

        /// <summary>
        /// 是否为几何参数订阅模式
        /// </summary>
        public bool IsGeometricMode => SubscribeSubMode == RegionSubscribeSubMode.GeometricParameters;

        /// <summary>
        /// 形状下拉可选项（锁定 6 种：点/线/圆/矩形/旋转矩形/多边形）
        /// </summary>
        public IReadOnlyList<ShapeType> AvailableShapeTypes { get; } = new[]
        {
            ShapeType.Point,
            ShapeType.Line,
            ShapeType.Circle,
            ShapeType.Rectangle,
            ShapeType.RotatedRectangle,
            ShapeType.Polygon
        };

        /// <summary>
        /// 整区域绑定源（可能为 null 表示未订阅）
        /// </summary>
        public ParameterSource? WholeRegionSource
        {
            get => _wholeRegionSource;
            set
            {
                if (SetProperty(ref _wholeRegionSource, value))
                {
                    OnPropertyChanged(nameof(WholeRegionBindingPath));
                }
            }
        }

        /// <summary>
        /// 整区域绑定路径（供 ParamBinding.BindingSource 双向显示）
        /// </summary>
        public string? WholeRegionBindingPath
        {
            get
            {
                if (WholeRegionSource is NodeOutputSource nos && !string.IsNullOrEmpty(nos.NodeId))
                {
                    return string.IsNullOrEmpty(nos.PropertyPath) ? nos.NodeId : $"{nos.NodeId}.{nos.PropertyPath}";
                }
                return null;
            }
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

        /// <summary>
        /// 整区域选择变更事件（由宿主写 FixedRegion）
        /// </summary>
        public event EventHandler<WholeRegionSelectedEventArgs>? WholeRegionChanged;

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
            Parameters.Clear();

            var parameterDefs = GetParameterDefinitions(_currentShapeType);

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
        /// 获取参数定义（DataType 为 CLR 类型，与 ParamBinding.DataType 一致）
        /// </summary>
        private List<(string Name, string DisplayName, Type DataType)> GetParameterDefinitions(ShapeType shapeType)
        {
            return shapeType switch
            {
                ShapeType.Point => new List<(string, string, Type)>
                {
                    ("CenterX", "中心X", typeof(double)),
                    ("CenterY", "中心Y", typeof(double))
                },
                ShapeType.Line => new List<(string, string, Type)>
                {
                    ("StartX", "起点X", typeof(double)),
                    ("StartY", "起点Y", typeof(double)),
                    ("EndX", "终点X", typeof(double)),
                    ("EndY", "终点Y", typeof(double))
                },
                ShapeType.Circle => new List<(string, string, Type)>
                {
                    ("CenterX", "中心X", typeof(double)),
                    ("CenterY", "中心Y", typeof(double)),
                    ("Radius", "半径", typeof(double))
                },
                ShapeType.Rectangle => new List<(string, string, Type)>
                {
                    ("CenterX", "中心X", typeof(double)),
                    ("CenterY", "中心Y", typeof(double)),
                    ("Width", "宽度", typeof(double)),
                    ("Height", "高度", typeof(double))
                },
                ShapeType.RotatedRectangle => new List<(string, string, Type)>
                {
                    ("CenterX", "中心X", typeof(double)),
                    ("CenterY", "中心Y", typeof(double)),
                    ("Width", "宽度", typeof(double)),
                    ("Height", "高度", typeof(double)),
                    ("Angle", "角度", typeof(double))
                },
                ShapeType.Polygon => new List<(string, string, Type)>
                {
                    ("Points", "顶点", typeof(Point2D[]))
                },
                _ => new List<(string, string, Type)>()
            };
        }

        /// <summary>
        /// 绑定参数（打开选择器）
        /// </summary>
        private void BindParameter(ParamSettingItem? item)
        {
            if (item == null) return;
            SelectedParameter = item;
            // 实际绑定在View层通过 ParamBinding 树弹窗 / NodeSelector 完成
        }

        /// <summary>
        /// 清除绑定
        /// </summary>
        private void ClearBinding(ParamSettingItem? item)
        {
            if (item == null) return;

            item.Source = null;
            item.DisplayPath = string.Empty;
            item.BindingPath = string.Empty;
            item.CurrentValue = null;

            ParamSettingChanged?.Invoke(this, new ParamSettingChangedEventArgs(item.ParameterName, null));
        }

        /// <summary>
        /// 应用几何参数绑定选择结果
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
                // 边界：CLR Type → 字符串词表走 SdkTypeVocabulary 适配层（已知类型归一为规范键）；
                // 词表未收录的合法几何类型（如 Point2D[]，系统本就使用）回退到 CLR 全名用于持久化，
                // 非未支持类型的静默兜底（运行期过滤仍按 ParamBinding.DataType 的 Type 执行）
                DataType = SdkTypeVocabulary.ToTypeVocabulary(SelectedParameter.DataType)
                           ?? SelectedParameter.DataType.FullName
                           ?? "double"
            };

            SelectedParameter.Source = source;
            SelectedParameter.DisplayPath = selectedNode.DisplayPath;
            SelectedParameter.BindingPath = string.IsNullOrEmpty(selectedNode.PropertyPath)
                ? selectedNode.NodeId
                : $"{selectedNode.NodeId}.{selectedNode.PropertyPath}";

            // 订阅值变更
            SubscribeToValue(SelectedParameter, source);

            ParamSettingChanged?.Invoke(this, new ParamSettingChangedEventArgs(SelectedParameter.ParameterName, source));
        }

        /// <summary>
        /// 应用整区域绑定选择结果
        /// </summary>
        public void ApplyWholeRegionSelection(NodeOutputInfo selectedNode)
        {
            if (selectedNode == null) return;

            var source = new NodeOutputSource(
                selectedNode.NodeId,
                selectedNode.OutputName,
                null,
                selectedNode.PropertyPath)
            {
                DataType = "Region"
            };

            WholeRegionSource = source;
            WholeRegionChanged?.Invoke(this, new WholeRegionSelectedEventArgs(source));
        }

        /// <summary>
        /// 清除整区域绑定
        /// </summary>
        public void ClearWholeRegion()
        {
            WholeRegionSource = null;
            WholeRegionChanged?.Invoke(this, new WholeRegionSelectedEventArgs(null));
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
        /// 从区域定义加载绑定（覆盖 FixedRegion / ComputedRegion / ShapeParameters 三种）
        /// </summary>
        public void LoadFromRegionDefinition(RegionDefinition def)
        {
            switch (def)
            {
                case FixedRegion fixedRegion:
                    SubscribeSubMode = RegionSubscribeSubMode.WholeRegion;
                    WholeRegionSource = string.IsNullOrEmpty(fixedRegion.SourceNodeId)
                        ? null
                        : new NodeOutputSource(fixedRegion.SourceNodeId, fixedRegion.OutputName, fixedRegion.RegionIndex);
                    break;

                case ComputedRegion computedRegion:
                    SubscribeSubMode = RegionSubscribeSubMode.GeometricParameters;
                    CurrentShapeType = computedRegion.TargetShapeType;
                    foreach (var param in Parameters)
                    {
                        if (computedRegion.ParameterBindings.TryGetValue(param.ParameterName, out var source))
                        {
                            param.Source = source;
                            if (source is NodeOutputSource nodeSource && _dataProvider != null)
                            {
                                param.DisplayPath = $"{nodeSource.NodeId}.{nodeSource.PropertyPath}";
                                param.BindingPath = string.IsNullOrEmpty(nodeSource.PropertyPath)
                                    ? nodeSource.NodeId
                                    : $"{nodeSource.NodeId}.{nodeSource.PropertyPath}";
                                SubscribeToValue(param, nodeSource);
                            }
                        }
                    }
                    break;

                case ShapeParameters shapeDef:
                    SubscribeSubMode = RegionSubscribeSubMode.GeometricParameters;
                    CurrentShapeType = shapeDef.ShapeType;
                    break;

                default:
                    SubscribeSubMode = RegionSubscribeSubMode.GeometricParameters;
                    break;
            }
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

    /// <summary>
    /// 整区域选择变更事件参数
    /// </summary>
    public class WholeRegionSelectedEventArgs : EventArgs
    {
        public ParameterSource? Source { get; }

        public WholeRegionSelectedEventArgs(ParameterSource? source)
        {
            Source = source;
        }
    }
}
