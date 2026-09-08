using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.ViewModels
{
    /// <summary>
    /// 订阅方式子模式：整对象订阅 / 几何参数拼装（双子互斥，与 Roi 三态之二对应）。
    /// </summary>
    public enum RoiSubscribeSubMode
    {
        /// <summary>订阅整个区域（写 Roi.Binding，绑定上游 ROI 容器/分组/单项）</summary>
        WholeRegion,

        /// <summary>订阅几何参数（写 Roi.ParameterBindings，键=RoiShape 扁平参数名，按 ComposeShapeType 拼装）</summary>
        GeometricParameters,
    }

    /// <summary>
    /// 订阅面板视图模型（双子契约）。
    /// 值源统一为 <see cref="ParamSetting"/>（对齐 Roi 单模型），取代历史 ParameterSource 值源。
    /// <list type="bullet">
    /// <item>整对象订阅：一个 ParamSetting（WholeRegion 行）→ Roi.Binding；</item>
    /// <item>几何参数拼装：形状下拉决定扁平参数行，每行一个 ParamSetting（键=RoiShape 扁平参数名）→ Roi.ParameterBindings + ComposeShapeType。</item>
    /// </list>
    /// </summary>
    public class ParameterPanelViewModel : ObservableObject
    {
        private readonly DataSourceQueryService? _dataProvider;
        private RoiSubscribeSubMode _subscribeSubMode = RoiSubscribeSubMode.WholeRegion;
        private ShapeType _currentShapeType = ShapeType.Rectangle;
        private ParamSetting? _wholeRegionSetting;
        private ParamSettingItem? _selectedParameter;

        /// <summary>参数绑定行（几何参数拼装模式；键 = RoiShape 扁平参数名）</summary>
        public ObservableCollection<ParamSettingItem> Parameters { get; } = new();

        /// <summary>订阅子模式（整对象 / 几何参数）</summary>
        public RoiSubscribeSubMode SubscribeSubMode
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

        /// <summary>是否为整对象订阅模式</summary>
        public bool IsWholeRegionMode => SubscribeSubMode == RoiSubscribeSubMode.WholeRegion;

        /// <summary>是否为几何参数拼装模式</summary>
        public bool IsGeometricMode => SubscribeSubMode == RoiSubscribeSubMode.GeometricParameters;

        /// <summary>形状下拉可选项（拼装目标形状；键集与 DeclaredShapeType 天然对齐）</summary>
        public IReadOnlyList<ShapeType> AvailableShapeTypes { get; } = new[]
        {
            ShapeType.Point,
            ShapeType.Line,
            ShapeType.Circle,
            ShapeType.Rectangle,
            ShapeType.RotatedRectangle,
        };

        /// <summary>
        /// 当前拼装目标形状。切换时重建参数行（防参数张冠李戴）。
        /// </summary>
        public ShapeType CurrentShapeType
        {
            get => _currentShapeType;
            set
            {
                if (SetProperty(ref _currentShapeType, value))
                {
                    UpdateParameterDefinitions();
                    ShapeTypeChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary>整对象订阅设置（null=未订阅）</summary>
        public ParamSetting? WholeRegionSetting
        {
            get => _wholeRegionSetting;
            set
            {
                if (SetProperty(ref _wholeRegionSetting, value))
                {
                    OnPropertyChanged(nameof(WholeRegionBindingPath));
                }
            }
        }

        /// <summary>整对象绑定路径（ParamBinding.BindingSource 双向显示）</summary>
        public string? WholeRegionBindingPath
            => WholeRegionSetting is { BindingType: BindingType.Binding } b
                ? (string.IsNullOrEmpty(b.SourceProperty) ? b.SourceNodeId : $"{b.SourceNodeId}.{b.SourceProperty}")
                : null;

        /// <summary>选中的参数行（用于几何参数绑定）</summary>
        public ParamSettingItem? SelectedParameter
        {
            get => _selectedParameter;
            set => SetProperty(ref _selectedParameter, value);
        }

        /// <summary>清除某参数行绑定命令</summary>
        public ICommand ClearBindingCommand { get; }

        /// <summary>整对象订阅变更事件（null=解除）</summary>
        public event EventHandler<ParamSetting?>? BindingChanged;

        /// <summary>几何参数绑定变更事件（parameterName=扁平参数名；setting=null=解除该键）</summary>
        public event EventHandler<ParameterSettingChangedEventArgs>? ParameterSettingChanged;

        /// <summary>拼装目标形状变更事件（宿主同步写 Roi.ComposeShapeType）</summary>
        public event EventHandler<ShapeType>? ShapeTypeChanged;

        public ParameterPanelViewModel(DataSourceQueryService? dataProvider = null)
        {
            _dataProvider = dataProvider;
            ClearBindingCommand = new RelayCommand<ParamSettingItem>(ClearBinding);
            UpdateParameterDefinitions();
        }

        /// <summary>
        /// 从选中的几何输入回显订阅状态（三态：Binding→整对象；ParameterBindings→几何参数并回填；本地→按本地形状进几何参数模板）。
        /// </summary>
        public void LoadFromRoi(Roi input)
        {
            if (input == null) return;

            if (input.Binding != null)
            {
                // 整对象订阅
                SubscribeSubMode = RoiSubscribeSubMode.WholeRegion;
                WholeRegionSetting = input.Binding;
                return;
            }

            if (input.ParameterBindings is { Count: > 0 })
            {
                // 参数拼装：按持久化形状出参数行，并回填已绑定的键
                SubscribeSubMode = RoiSubscribeSubMode.GeometricParameters;
                var shapeType = input.ComposeShapeType != ShapeType.None
                    ? input.ComposeShapeType
                    : input.DeclaredShapeType;
                if (shapeType == ShapeType.None)
                    shapeType = ShapeType.Rectangle;
                _currentShapeType = shapeType;
                OnPropertyChanged(nameof(CurrentShapeType));
                UpdateParameterDefinitions();
                foreach (var item in Parameters)
                {
                    if (input.ParameterBindings.TryGetValue(item.ParameterName, out var setting))
                    {
                        item.Setting = setting;
                        item.BindingPath = setting.BindingType == BindingType.Binding
                            ? (string.IsNullOrEmpty(setting.SourceProperty) ? setting.SourceNodeId! : $"{setting.SourceNodeId}.{setting.SourceProperty}")
                            : string.Empty;
                    }
                }
                return;
            }

            // 本地绘制：进入几何参数模板（形状沿用本地几何），参数行全空待绑
            SubscribeSubMode = RoiSubscribeSubMode.GeometricParameters;
            var localShape = input.LocalGeometry?.ShapeType ?? ShapeType.None;
            if (localShape is ShapeType.Point or ShapeType.Line or ShapeType.Circle
                or ShapeType.Rectangle or ShapeType.RotatedRectangle)
            {
                _currentShapeType = localShape;
            }
            else
            {
                _currentShapeType = ShapeType.Rectangle;
            }
            OnPropertyChanged(nameof(CurrentShapeType));
            UpdateParameterDefinitions();
        }

        #region 整对象订阅

        /// <summary>应用整对象订阅选择结果（NodeOutputInfo → ParamSetting）</summary>
        public void ApplyWholeRegionSelection(NodeOutputInfo selectedNode)
        {
            if (selectedNode == null) return;

            var setting = new ParamSetting
            {
                ParameterName = "WholeRegion",
                BindingType = BindingType.Binding,
                SourceNodeId = selectedNode.NodeId,
                SourceProperty = string.IsNullOrEmpty(selectedNode.PropertyPath)
                    ? selectedNode.OutputName
                    : selectedNode.PropertyPath,
            };
            WholeRegionSetting = setting;
            BindingChanged?.Invoke(this, setting);
        }

        /// <summary>清除整对象订阅</summary>
        public void ClearWholeRegion()
        {
            WholeRegionSetting = null;
            BindingChanged?.Invoke(this, null);
        }

        #endregion

        #region 几何参数拼装

        /// <summary>应用几何参数绑定选择结果（对 SelectedParameter 行）</summary>
        public void ApplyBindingSelection(NodeOutputInfo selectedNode)
        {
            var item = SelectedParameter;
            if (item == null || selectedNode == null) return;

            var setting = new ParamSetting
            {
                ParameterName = item.ParameterName,
                BindingType = BindingType.Binding,
                SourceNodeId = selectedNode.NodeId,
                SourceProperty = string.IsNullOrEmpty(selectedNode.PropertyPath)
                    ? selectedNode.OutputName
                    : selectedNode.PropertyPath,
            };
            item.Setting = setting;
            item.BindingPath = string.IsNullOrEmpty(setting.SourceProperty)
                ? setting.SourceNodeId!
                : $"{setting.SourceNodeId}.{setting.SourceProperty}";
            item.DisplayPath = selectedNode.DisplayPath;

            ParameterSettingChanged?.Invoke(this, new ParameterSettingChangedEventArgs(item.ParameterName, setting));
        }

        /// <summary>清除某参数行绑定</summary>
        private void ClearBinding(ParamSettingItem? item)
        {
            if (item == null) return;

            item.Setting = null;
            item.BindingPath = string.Empty;
            item.DisplayPath = string.Empty;
            item.CurrentValue = null;

            ParameterSettingChanged?.Invoke(this, new ParameterSettingChangedEventArgs(item.ParameterName, null));
        }

        #endregion

        /// <summary>按当前形状重建扁平参数行</summary>
        private void UpdateParameterDefinitions()
        {
            Parameters.Clear();

            foreach (var def in GetParameterDefinitions(_currentShapeType))
            {
                Parameters.Add(new ParamSettingItem
                {
                    ParameterName = def.Name,
                    DisplayName = def.DisplayName,
                    DataType = def.DataType,
                });
            }
        }

        /// <summary>形状 → 扁平参数定义（键 = RoiShape 扁平参数名，与拼装集/ComposeShapeType 对齐）</summary>
        private static List<(string Name, string DisplayName, Type DataType)> GetParameterDefinitions(ShapeType shapeType)
        {
            return shapeType switch
            {
                ShapeType.Point => new List<(string, string, Type)>
                {
                    ("CenterX", "中心X", typeof(double)),
                    ("CenterY", "中心Y", typeof(double)),
                },
                ShapeType.Line => new List<(string, string, Type)>
                {
                    ("StartX", "起点X", typeof(double)),
                    ("StartY", "起点Y", typeof(double)),
                    ("EndX", "终点X", typeof(double)),
                    ("EndY", "终点Y", typeof(double)),
                },
                ShapeType.Circle => new List<(string, string, Type)>
                {
                    ("CenterX", "中心X", typeof(double)),
                    ("CenterY", "中心Y", typeof(double)),
                    ("Radius", "半径", typeof(double)),
                },
                ShapeType.Rectangle => new List<(string, string, Type)>
                {
                    ("CenterX", "中心X", typeof(double)),
                    ("CenterY", "中心Y", typeof(double)),
                    ("Width", "宽度", typeof(double)),
                    ("Height", "高度", typeof(double)),
                },
                ShapeType.RotatedRectangle => new List<(string, string, Type)>
                {
                    ("CenterX", "中心X", typeof(double)),
                    ("CenterY", "中心Y", typeof(double)),
                    ("Width", "宽度", typeof(double)),
                    ("Height", "高度", typeof(double)),
                    ("Angle", "角度", typeof(double)),
                },
                _ => new List<(string, string, Type)>(),
            };
        }
    }

    /// <summary>
    /// 几何参数绑定变更事件参数（parameterName = RoiShape 扁平参数名）
    /// </summary>
    public class ParameterSettingChangedEventArgs : EventArgs
    {
        public string ParameterName { get; }
        public ParamSetting? Setting { get; }

        public ParameterSettingChangedEventArgs(string parameterName, ParamSetting? setting)
        {
            ParameterName = parameterName;
            Setting = setting;
        }
    }
}
