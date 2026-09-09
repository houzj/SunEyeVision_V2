using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
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
        private Roi? _targetRoi;

        /// <summary>参数绑定行（几何参数拼装模式；键 = RoiShape 扁平参数名）</summary>
        public ObservableCollection<ParamSettingItem> Parameters { get; } = new();

        /// <summary>当前订阅面板编辑的目标 Roi（用于 VM 把订阅配置写回正确的 Roi）</summary>
        public Roi? TargetRoi
        {
            get => _targetRoi;
            private set => _targetRoi = value;
        }

        /// <summary>
        /// 订阅子模式（区域订阅 / 参数订阅）。
        /// 架构语义：子模式是订阅主模式下的【纯视图切换】——仅控制"区域源"或"几何参数"两组显示，
        /// 【绝不因此清空/重载绑定数据】，也不反向影响 Roi 模型（LoadFromRoi 只由
        /// SelectedRoi 变化 / 主模式切换（订阅↔绘制）触发；主模式切走订阅时才清数据）。
        /// </summary>
        public RoiSubscribeSubMode SubscribeSubMode
        {
            get => _subscribeSubMode;
            set
            {
                if (SetProperty(ref _subscribeSubMode, value))
                {
                    OnPropertyChanged(nameof(IsWholeRegionMode));
                    OnPropertyChanged(nameof(IsGeometricMode));
                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"[SubscribeSubMode] 切换子模式 {_subscribeSubMode} | targetRoi={_targetRoi?.Name ?? "null"}(Source={_targetRoi?.Source.ToString() ?? "-"}|Binding={( _targetRoi?.Binding != null ? "Y" : "N")}|ParamBindings={_targetRoi?.ParameterBindings?.Count ?? -1})",
                        "RoiSubscribePanel");
                    // 子模式切换只做两件事，绝不改动任何订阅配置：
                    // 1) 激活：切到某订阅方式且该 Roi 已有对应配置时，把 Source 激活为该方式（运行时按它解析）；
                    //    无对应配置则不写 Source（等用户在对应子模式配好绑定后由 On*Changed 激活）。
                    // 2) 回灌视图缓存：LoadFromRoi(syncSubMode:false) 把区域行/参数行缓存与模型对齐，
                    //    但不改 radio（不回弹）。
                    if (_targetRoi is { } roi)
                    {
                        if (_subscribeSubMode == RoiSubscribeSubMode.WholeRegion
                            && roi.Binding != null
                            && roi.Source != RoiGeometrySource.WholeRegionSub)
                        {
                            roi.Source = RoiGeometrySource.WholeRegionSub;
                        }
                        else if (_subscribeSubMode == RoiSubscribeSubMode.GeometricParameters
                                 && roi.ParameterBindings is { Count: > 0 }
                                 && roi.Source != RoiGeometrySource.GeometricSub)
                        {
                            roi.Source = RoiGeometrySource.GeometricSub;
                        }
                        LoadFromRoi(roi, syncSubMode: false);
                    }
                }
            }
        }

        /// <summary>是否为整对象订阅模式（可写：TwoWay 绑定 RadioButton.IsChecked；setter 切 SubscribeSubMode）</summary>
        public bool IsWholeRegionMode
        {
            get => SubscribeSubMode == RoiSubscribeSubMode.WholeRegion;
            set
            {
                if (value && SubscribeSubMode != RoiSubscribeSubMode.WholeRegion)
                    SubscribeSubMode = RoiSubscribeSubMode.WholeRegion;
            }
        }

        /// <summary>是否为几何参数拼装模式（可写：TwoWay 绑定 RadioButton.IsChecked；setter 切 SubscribeSubMode）</summary>
        public bool IsGeometricMode
        {
            get => SubscribeSubMode == RoiSubscribeSubMode.GeometricParameters;
            set
            {
                if (value && SubscribeSubMode != RoiSubscribeSubMode.GeometricParameters)
                    SubscribeSubMode = RoiSubscribeSubMode.GeometricParameters;
            }
        }

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

        /// <summary>整对象绑定路径（只读计算属性，仅用于 ParamBinding.BindingSource 单向显示；回写走 BindingSourceSelectedEvent）</summary>
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
        /// 从选中的几何输入回显订阅状态，按 <see cref="Roi.Source"/> 激活的来源分支
        /// （WholeRegionSub→区域订阅；GeometricSub→参数订阅并回填；Drawing→本地，仅出空参数行）。
        /// 调用时机仅限【选中 Roi 变化】与【主模式切换（订阅↔绘制，CurrentMode setter 触发）】；
        /// 订阅子模式 radio 切换只切视图/激活 Source，不清数据。
        /// </summary>
        /// <remarks>
        /// input==null 视为"无选中 Roi"：仍须清空 WholeRegionSetting 与重建空参数行，
        /// 避免从绘制切回订阅时（SelectedRoi 可能为 null）缓存残留造成"看有绑、实际没绑"。
        /// <param name="syncSubMode">true = 选中 Roi 变化/主模式切换（按 <see cref="Roi.Source"/> 同步 radio）；
        /// false = 用户刚切子模式 radio（此时 radio 已是用户意图，只回灌视图缓存、绝不反向改 radio）。</param>
        /// 本地绘制（Source=Drawing）分支不覆盖 SubscribeSubMode（保留用户当前子模式选择）。
        /// </remarks>
        public void LoadFromRoi(Roi? input, bool syncSubMode = false)
        {
            // 记录订阅面板编辑的目标 Roi，供 VM 把订阅配置写回正确位置
            _targetRoi = input;

            VisionLogger.Instance.Log(LogLevel.Info,
                $"[LoadFromRoi] 回显输入 roi={input?.Name ?? "null"} | Source={input?.Source.ToString() ?? "null"} | syncSubMode={syncSubMode} | SubscribeSubMode={_subscribeSubMode}",
                "RoiSubscribePanel");

            if (input == null)
            {
                WholeRegionSetting = null;
                UpdateParameterDefinitions();
                return;
            }

            // radio 同步（仅选中变化/主模式切换）：按激活来源设置子模式；用户切 radio 时不走这里
            //（radio 已是用户意图，syncSubMode=false 只回灌视图、绝不反向改 radio）。
            // Drawing（无订阅/刚切绘制清空）= "新节点刚打开订阅模式"的等价态：子模式重置为默认"按区域订阅"，
            // 两个子模式绑定均已清空（区域行 null、参数行空），与用户语义一致。
            if (syncSubMode)
            {
                SubscribeSubMode = input.Source switch
                {
                    RoiGeometrySource.WholeRegionSub => RoiSubscribeSubMode.WholeRegion,
                    RoiGeometrySource.GeometricSub => RoiSubscribeSubMode.GeometricParameters,
                    _ => RoiSubscribeSubMode.WholeRegion, // Drawing：重置为默认"区域订阅"
                };
            }

            // 视图缓存按【激活来源】显示对应数据；非激活来源的并存/错位数据一律不显示：
            // - 区域源行仅在 Source=WholeRegionSub 时显示模型 Binding；Drawing/参数订阅时必空
            //   （杜绝"Source 已是本地/参数但旧区域绑定仍显示"，以及绘制切回订阅后残留订阅行）；
            // - 参数行仅在 Source=GeometricSub 时按 ParameterBindings 回填；其它来源为空行（供新配）。
            // 并存配置（另一侧）在切到对应子模式 radio 时由 SubscribeSubMode setter 激活 Source 后重新回灌。
            WholeRegionSetting = input.Source == RoiGeometrySource.WholeRegionSub
                ? input.Binding
                : null;

            // 参数行形状模板：激活参数订阅用持久化 Compose/Declared 形状；其余（区域/绘制）按本地形状提供占位行。
            var shapeType = input.Source == RoiGeometrySource.GeometricSub
                ? (input.ComposeShapeType != ShapeType.None ? input.ComposeShapeType : input.DeclaredShapeType)
                : (input.LocalGeometry?.ShapeType ?? ShapeType.None);
            if (shapeType is not (ShapeType.Point or ShapeType.Line or ShapeType.Circle
                    or ShapeType.Rectangle or ShapeType.RotatedRectangle))
                shapeType = ShapeType.Rectangle;
            _currentShapeType = shapeType;
            OnPropertyChanged(nameof(CurrentShapeType));
            UpdateParameterDefinitions();

            if (input.Source == RoiGeometrySource.GeometricSub && input.ParameterBindings != null)
            {
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
            }
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
