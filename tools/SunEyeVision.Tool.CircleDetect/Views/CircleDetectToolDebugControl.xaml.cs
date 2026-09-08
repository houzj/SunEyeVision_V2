using System;
using System.ComponentModel;
using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Views;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Tool.CircleDetect.Views
{
    /// <summary>
    /// 圆查找工具调试控件
    /// </summary>
    /// <remarks>
    /// 3 Tab 布局（对齐 EdgeDetect）：
    /// - Tab1 基本参数：引导圆（GuideEditor，锁定 Circle 类型）
    /// - Tab2 运行参数：卡尺参数 + 圆弧参数 + 拟合筛选
    /// - Tab3 结果显示：拟合结果 + RMSE + 边缘点数
    ///
    /// 引导几何绘制/编辑交互由 SDK 通用控件 MeasurementEditorManagerControl 承载，
    /// 本工具只负责组装 + 参数同步。
    /// </remarks>
    public partial class CircleDetectToolDebugControl : ToolDebugControlBase
    {
        /// <summary>正在从编辑器同步卡尺参数（防止 参数→编辑器→参数 回写死循环）</summary>
        private bool _syncingCaliberFromEditor;

        /// <summary>卡尺编辑器事件是否已订阅（幂等标志，构造 + Loaded 只订阅一次）</summary>
        private bool _caliberHooked;

        #region Properties

        public static readonly DependencyProperty ParametersProperty =
            DependencyProperty.Register(
                nameof(Parameters),
                typeof(CircleDetectParameters),
                typeof(CircleDetectToolDebugControl),
                new PropertyMetadata(null));

        public CircleDetectParameters Parameters
        {
            get => (CircleDetectParameters)GetValue(ParametersProperty);
            set => SetValue(ParametersProperty, value);
        }

        /// <summary>结果显示文本</summary>
        public string ResultText
        {
            get => _resultText;
            set { _resultText = value; OnPropertyChanged(nameof(ResultText)); }
        }
        private string _resultText = "尚未执行";

        /// <summary>拟合残差显示</summary>
        public string ResultRMSE
        {
            get => _resultRMSE;
            set { _resultRMSE = value; OnPropertyChanged(nameof(ResultRMSE)); }
        }
        private string _resultRMSE = "--";

        /// <summary>边缘点数显示</summary>
        public string ResultEdgePointCount
        {
            get => _resultEdgePointCount;
            set { _resultEdgePointCount = value; OnPropertyChanged(nameof(ResultEdgePointCount)); }
        }
        private string _resultEdgePointCount = "--";

        #endregion

        #region Constructor

        public CircleDetectToolDebugControl()
        {
            InitializeComponent();
            Parameters = new CircleDetectParameters();
            // 构造时先尝试订阅（XAML 命名元素可能已就绪）；若未就绪由 Loaded 补订阅
            HookCaliberEditor();

            // 窗口关闭时清理主图像测量叠加层（对齐 Threshold/Blob 范式）
            Loaded += (s, e) =>
            {
                // 确保 XAML 完全加载后订阅成功（防止构造函数时序导致事件漏订阅）
                HookCaliberEditor();
                var window = System.Windows.Window.GetWindow(this);
                if (window != null)
                {
                    window.Closing += OnWindowClosing;
                }
            };
        }

        #endregion

        #region 资源清理

        /// <summary>
        /// 清理主图像测量引导叠加层（关闭调试窗口时防止残留）
        /// </summary>
        public void Cleanup()
        {
            MeasurementManager?.ClearMainOverlay();
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            Cleanup();
        }

        #endregion

        #region GuideEditor 集成

        /// <summary>
        /// 测量卡尺引导编辑器（锁定圆类型，屏蔽区域由 Manager 编排）
        /// </summary>
        private MeasureEditorControl GuideEditor => MeasurementManager.GuideEditor;

        /// <summary>
        /// 订阅引导几何交互宿主的事件 + 参数联动（幂等：构造 + Loaded 双调用只订阅一次）
        /// </summary>
        private void HookCaliberEditor()
        {
            if (GuideEditor == null)
            {
                VisionLogger.Instance.Log(LogLevel.Warning, "[Diag] HookCaliberEditor GuideEditor=null 订阅失败", "CircleDetectToolDebugControl");
                return;
            }
            if (_caliberHooked) return;
            _caliberHooked = true;
            GuideEditor.GuideGeometryChanged += OnGuideGeometryChanged;
            GuideEditor.CaliberParamsChanged += OnCaliberParamsChanged;
            Parameters.PropertyChanged += OnParameterChanged;
            VisionLogger.Instance.Log(LogLevel.Info, "[Diag] HookCaliberEditor 订阅成功", "CircleDetectToolDebugControl");
        }

        /// <summary>
        /// 卡尺参数变更 → 实时刷新测量带渲染
        /// </summary>
        private void OnParameterChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (Parameters == null || GuideEditor == null) return;
            if (e.PropertyName == nameof(CircleDetectParameters.MeasureLength1) ||
                e.PropertyName == nameof(CircleDetectParameters.MeasureLength2) ||
                e.PropertyName == nameof(CircleDetectParameters.NumMeasures))
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[Diag] OnParameterChanged prop={e.PropertyName} L1={Parameters.MeasureLength1:F2} L2={Parameters.MeasureLength2:F2} N={Parameters.NumMeasures}",
                    "CircleDetectToolDebugControl");
                _syncingCaliberFromEditor = true;
                try
                {
                    GuideEditor.SetCaliberParams(
                        Parameters.MeasureLength1 * 2,
                        Parameters.MeasureLength2 * 2,
                        Parameters.NumMeasures);
                }
                finally
                {
                    _syncingCaliberFromEditor = false;
                }
            }
        }

        /// <summary>
        /// 卡尺参数变更（主卡尺手柄/面板编辑）→ 回写工具参数
        /// </summary>
        private void OnCaliberParamsChanged(object? sender, CaliberParamsChangedEventArgs e)
        {
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[Diag] OnCaliberParamsChanged W={e.Width:F2} L={e.Length:F2} C={e.Count} syncing={_syncingCaliberFromEditor}",
                "CircleDetectToolDebugControl");
            if (Parameters == null || _syncingCaliberFromEditor) return;
            Parameters.MeasureLength1 = e.Width / 2;
            Parameters.MeasureLength2 = e.Length / 2;
            Parameters.NumMeasures = e.Count;
        }

        /// <summary>
        /// 引导几何变更 → 同步到工具参数
        /// </summary>
        private void OnGuideGeometryChanged(object? sender, GuideGeometryChangedEventArgs e)
        {
            if (Parameters == null) return;

            var model = e.Model;
            if (e.GeometryType == MeasureGeometryType.Circle && model.EditingCircle.HasValue)
            {
                // ★ 几何驱动：画圆 = 整圆检测（强制 IsArc=false，消除手动开关与几何脱节）
                Parameters.IsArc = false;
                Parameters.GuideCircle = model.EditingCircle.Value;
                Parameters.StartAngle = 0.0;
                Parameters.EndAngle = 2.0 * Math.PI;
                Parameters.LogInfo(
                    $"引导圆已绘制: 圆心({model.EditingCircle.Value.Center.X:F1}, {model.EditingCircle.Value.Center.Y:F1})");
            }

            // 圆弧：完整回写几何（圆心/半径/起始角/终止角/大弧/方向），
            //   并自动置 IsArc=true（编辑器引导为弧即圆弧模式）
            if (model.EditingArc.HasValue)
            {
                var arc = model.EditingArc.Value;
                Parameters.IsArc = true;
                Parameters.GuideCircle = new Circle2d(arc.Center, arc.Radius);
                Parameters.StartAngle = arc.StartAngle;
                Parameters.EndAngle = arc.EndAngle;
                Parameters.IsMajorArc = arc.IsMajorArc;
                Parameters.IsClockwise = arc.IsClockwise;
                Parameters.LogInfo(
                    $"引导圆弧已绘制: 圆心({arc.Center.X:F1}, {arc.Center.Y:F1}) 半径({arc.Radius:F1}) 角度({arc.StartAngle:F2}~{arc.EndAngle:F2})");
            }

            // 绘制流程完成 → 清除工具栏选中，回到选择模式
            if (e.IsDrawing)
            {
                GuideEditor.ClearCaliberSelection();
            }
        }

        /// <summary>
        /// 渲染初始引导几何（从参数回填到编辑器）
        /// </summary>
        private void RestoreGuideFromParameters()
        {
            if (GuideEditor == null || Parameters == null) return;

            // ★ 修复圆弧持久化：圆弧模式回填 Arc2d（SetGuide 自动同步 Arc 交互策略），
            //   否则编辑器只显示整圆、角度/方向/大弧全部丢失
            if (Parameters.IsArc && Parameters.GuideCircle.HasValue)
            {
                var c = Parameters.GuideCircle.Value;
                GuideEditor.SetGuide(new Arc2d(
                    c.Center, c.Radius,
                    Parameters.StartAngle, Parameters.EndAngle,
                    Parameters.IsMajorArc));
            }
            else if (Parameters.GuideCircle.HasValue)
            {
                GuideEditor.SetGuide(Parameters.GuideCircle.Value);
            }

            // ★ 修复持久化：卡尺参数从 Parameters 回填到编辑器（宽度/长度/数量）
            //   SetCaliberParams 会触发 CaliberParamsChanged，用同步标志避免回写循环
            _syncingCaliberFromEditor = true;
            try
            {
                GuideEditor.SetCaliberParams(
                    Parameters.MeasureLength1 * 2,
                    Parameters.MeasureLength2 * 2,
                    Parameters.NumMeasures);
            }
            finally
            {
                _syncingCaliberFromEditor = false;
            }
        }

        #endregion

        #region Overrides

        /// <summary>数据提供者（取图像 / 查询位置修正矩阵）</summary>
        private DataSourceQueryService? _dataProvider;

        public override void SetParameters(ToolParameters parameters)
        {
            base.SetParameters(parameters);
            // 零兼容（rule-008/rule-015）：参数类型不符直接报错
            if (parameters is not CircleDetectParameters circleParams)
            {
                throw new InvalidOperationException(
                    $"圆查找节点参数类型异常（期望 CircleDetectParameters，实际 {parameters?.GetType().Name ?? "null"}）。");
            }
            Parameters = circleParams;

            // 屏蔽区域：绑定到 MeasurementEditorManager
            if (MeasurementManager != null)
            {
                MeasurementManager.MaskRoisSource = Parameters.MaskRois.Rois;
            }

            // ★ 修复持久化：打开调试窗口即回填引导几何 + 卡尺参数到编辑器
            //   （原实现只在 SetMainImageControl（窗口显示后）调用且不回填卡尺，
            //    导致关闭重开时编辑器显示默认值）。
            RestoreGuideFromParameters();

            // ★ 同步 ComboBox 选中（反序列化时按已保存值显示）
            SyncEnumsFromParameters();
        }

        public override void SetDataProvider(object dataProvider)
        {
            base.SetDataProvider(dataProvider);
            if (dataProvider is DataSourceQueryService queryService)
            {
                _dataProvider = queryService;
                Parameters?.RestoreImageSource(AvailableDataSources);
                Parameters?.RestorePositionCorrectionSource(AvailableDataSources);
                UpdatePositionCorrectionMatrix();
            }
        }

        protected override object ExecuteTool()
        {
            if (Parameters == null)
                throw new InvalidOperationException("Parameters not initialized.");

            var imageSource = Parameters?.ImageSource;
            if (imageSource == null)
                throw new InvalidOperationException("请选择输入图像源");
            if (_dataProvider == null)
                throw new InvalidOperationException("数据提供者未初始化");
            if (!_dataProvider.HasNodeExecuted(imageSource.SourceNodeId))
                throw new InvalidOperationException(
                    $"当前工具缺少输入图像：图像源 '{imageSource.SourceNodeId}.{imageSource.PropertyName}' 没有值");

            var image = _dataProvider.GetPropertyValue(imageSource.SourceNodeId, imageSource.PropertyName) as IVisionImage;
            if (image == null || image.IsEmpty)
                throw new InvalidOperationException("无法获取图像数据或图像为空");

            Parameters!.ResolvedInputImage = image;
            var tool = new CircleDetectTool();
            var result = tool.Run(Parameters);

            // 执行完成后刷新位置修正矩阵到编辑器
            UpdatePositionCorrectionMatrix();
            UpdateResultDisplay(result);
            return result;
        }

        protected override bool CanExecuteTool()
        {
            return Parameters != null && Parameters.ImageSource != null && _dataProvider != null;
        }

        /// <summary>
        /// 从 DataSourceQueryService 实时查询位置修正矩阵并设置到编辑器
        /// （交互层与结果层同一数据源，保证显示与算法一致）
        /// </summary>
        private void UpdatePositionCorrectionMatrix()
        {
            if (Parameters?.IsPositionCorrectionEnabled == true && _dataProvider != null
                && Parameters.PositionCorrectionBindingSource != null)
            {
                var source = Parameters.PositionCorrectionBindingSource;
                var matrix = _dataProvider.GetPropertyValue(source.SourceNodeId, source.PropertyName) as AffineMatrix;
                if (matrix != null)
                {
                    MeasurementManager?.SetPositionCorrectionMatrix(matrix);
                    return;
                }
            }
            if (_dataProvider != null)
            {
                MeasurementManager?.SetPositionCorrectionMatrix(null);
            }
        }

        /// <summary>
        /// 外部结果到达 - 工作流执行完成后刷新位置修正矩阵
        /// </summary>
        public override void OnExternalResultArrived()
        {
            UpdatePositionCorrectionMatrix();
        }

        /// <summary>
        /// 设置主界面图像控件（委托给 MeasurementEditorManager 承载交互）
        /// </summary>
        public override void SetMainImageControl(ImageControl? imageControl)
        {
            base.SetMainImageControl(imageControl);
            MeasurementManager?.SetMainImageControl(imageControl);
            // 锁定引导类型为圆（工具固定为圆查找，不自动进入绘制模式）
            MeasurementManager?.GuideEditor.LockGeometryType(MeasureGeometryType.Circle);
            // 工具栏只显示圆/圆弧卡尺（隐藏直线卡尺）
            MeasurementManager?.GuideEditor.SetAllowedGeometryTypes(
                MeasureGeometryType.Circle, MeasureGeometryType.Arc);
            RestoreGuideFromParameters();
        }

        #endregion

        #region 枚举下拉处理

        /// <summary>
        /// 从 Parameters 同步 ComboBox 选中（反序列化时按已保存值显示）
        /// </summary>
        private void SyncEnumsFromParameters()
        {
            if (TransitionCombo != null)
            {
                TransitionCombo.SelectedIndex = Parameters.Transition switch
                {
                    EdgeTransition.Positive => 1,
                    EdgeTransition.Negative => 2,
                    _ => 0,
                };
            }
            if (SelectCombo != null)
            {
                SelectCombo.SelectedIndex = Parameters.Select switch
                {
                    EdgeSelect.First => 1,
                    EdgeSelect.Last => 2,
                    _ => 0,
                };
            }
        }

        /// <summary>
        /// 边缘极性选择变更
        /// </summary>
        private void TransitionCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (Parameters == null || TransitionCombo == null) return;
            Parameters.Transition = TransitionCombo.SelectedIndex switch
            {
                1 => EdgeTransition.Positive,
                2 => EdgeTransition.Negative,
                _ => EdgeTransition.All,
            };
        }

        /// <summary>
        /// 选取模式选择变更
        /// </summary>
        private void SelectCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (Parameters == null || SelectCombo == null) return;
            Parameters.Select = SelectCombo.SelectedIndex switch
            {
                1 => EdgeSelect.First,
                2 => EdgeSelect.Last,
                _ => EdgeSelect.All,
            };
        }

        #endregion

        #region 结果更新

        /// <summary>
        /// 更新结果显示
        /// </summary>
        private void UpdateResultDisplay(CircleDetectResults result)
        {
            if (result == null) return;

            if (result.IsSuccess)
            {
                ResultText = result.GeometryDescription;
                ResultRMSE = $"{result.RMSE:F3} px";
                ResultEdgePointCount = $"{result.EdgePointCount}";
            }
            else
            {
                ResultText = result.ErrorMessage ?? "执行失败";
                ResultRMSE = "--";
                ResultEdgePointCount = "--";
            }
        }

        #endregion
    }
}
