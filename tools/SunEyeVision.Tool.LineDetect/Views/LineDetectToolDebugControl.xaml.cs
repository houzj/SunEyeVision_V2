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

namespace SunEyeVision.Tool.LineDetect.Views
{
    /// <summary>
    /// 直线查找工具调试控件
    /// </summary>
    /// <remarks>
    /// 3 Tab 布局（测量工具范式）：
    /// - Tab1 基本参数：引导直线 + 卡尺几何参数（GuideEditor，锁定 Line 类型）
    /// - Tab2 运行参数：边缘检测参数 + 拟合筛选（Expander 分组）
    /// - Tab3 结果显示：拟合结果 + RMSE + 边缘点数
    ///
    /// 引导几何绘制/编辑交互由 SDK 通用控件 MeasurementEditorManagerControl 承载，
    /// 本工具只负责组装 + 参数同步。
    /// </remarks>
    public partial class LineDetectToolDebugControl : ToolDebugControlBase
    {
        /// <summary>正在从编辑器同步卡尺参数（防止 参数→编辑器→参数 回写死循环）</summary>
        private bool _syncingCaliberFromEditor;

        /// <summary>卡尺编辑器事件是否已订阅（幂等标志，构造 + Loaded 只订阅一次）</summary>
        private bool _caliberHooked;

        #region Properties

        public static readonly DependencyProperty ParametersProperty =
            DependencyProperty.Register(
                nameof(Parameters),
                typeof(LineDetectParameters),
                typeof(LineDetectToolDebugControl),
                new PropertyMetadata(null));

        public LineDetectParameters Parameters
        {
            get => (LineDetectParameters)GetValue(ParametersProperty);
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

        public LineDetectToolDebugControl()
        {
            InitializeComponent();
            Parameters = new LineDetectParameters();
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
        /// 测量卡尺引导编辑器（锁定直线类型，屏蔽区域由 Manager 编排）
        /// </summary>
        private MeasureEditorControl GuideEditor => MeasurementManager.GuideEditor;

        /// <summary>
        /// 订阅引导几何交互宿主的事件 + 参数联动
        /// </summary>
        private void HookCaliberEditor()
        {
            if (GuideEditor == null)
            {
                VisionLogger.Instance.Log(LogLevel.Warning, "[Diag] HookCaliberEditor GuideEditor=null 订阅失败", "LineDetectToolDebugControl");
                return;
            }
            if (_caliberHooked) return;
            _caliberHooked = true;
            GuideEditor.GuideGeometryChanged += OnGuideGeometryChanged;
            GuideEditor.CaliberParamsChanged += OnCaliberParamsChanged;
            Parameters.PropertyChanged += OnParameterChanged;
            VisionLogger.Instance.Log(LogLevel.Info, "[Diag] HookCaliberEditor 订阅成功", "LineDetectToolDebugControl");
        }

        /// <summary>
        /// 卡尺参数变更 → 实时刷新测量带渲染
        /// </summary>
        private void OnParameterChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (Parameters == null || GuideEditor == null) return;

            // 参数面板编辑起终点时，回填同一个公共测量编辑器。
            // SetGuide 不触发 GuideGeometryChanged，因此不会反向覆盖刚编辑的参数。
            if (e.PropertyName is nameof(LineDetectParameters.LineStartPoint)
                or nameof(LineDetectParameters.LineEndPoint))
            {
                var start = Parameters.LineStartPoint;
                var end = Parameters.LineEndPoint;
                if ((end - start).Length > 1e-6)
                    GuideEditor.SetGuide(Line2d.FromTwoPoints(start, end), start, end);
                return;
            }

            if (e.PropertyName == nameof(LineDetectParameters.MeasureLength1) ||
                e.PropertyName == nameof(LineDetectParameters.MeasureLength2) ||
                e.PropertyName == nameof(LineDetectParameters.NumMeasures))
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[Diag] OnParameterChanged prop={e.PropertyName} L1={Parameters.MeasureLength1:F2} L2={Parameters.MeasureLength2:F2} N={Parameters.NumMeasures}",
                    "LineDetectToolDebugControl");
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
                "LineDetectToolDebugControl");
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
            if (e.GeometryType == MeasureGeometryType.Line && model.EditingLine.HasValue)
            {
                Parameters.LineStartPoint = model.LineStartPoint;
                Parameters.LineEndPoint = model.LineEndPoint;
                Parameters.LogInfo(
                    $"引导直线已绘制: 起点({model.LineStartPoint.X:F1}, {model.LineStartPoint.Y:F1})");
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

            var lineLen = (Parameters.LineEndPoint - Parameters.LineStartPoint).Length;
            if (lineLen > 1e-6)
            {
                // ★ 修复持久化：显式传入端点，避免 SetLine 用默认 200px 长度推导导致端点与保存值不一致
                GuideEditor.SetGuide(
                    Line2d.FromTwoPoints(Parameters.LineStartPoint, Parameters.LineEndPoint),
                    Parameters.LineStartPoint,
                    Parameters.LineEndPoint);
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
            if (parameters is not LineDetectParameters lineParams)
            {
                throw new InvalidOperationException(
                    $"直线查找节点参数类型异常（期望 LineDetectParameters，实际 {parameters?.GetType().Name ?? "null"}）。");
            }
            Parameters = lineParams;

            // 屏蔽区域：绑定到 MeasurementEditorManager
            if (MeasurementManager != null)
            {
                MeasurementManager.MaskRoisSource = Parameters.MaskRois.Rois;
            }

            // ★ 修复持久化：打开调试窗口即回填引导几何 + 卡尺参数到编辑器
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
                // 恢复序列化的图像源/位置修正绑定源（加载后重建运行时数据）
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
            var tool = new LineDetectTool();
            var result = ExecuteWithLiveContext(tool, Parameters);

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
            // 锁定引导类型为直线（工具固定为直线查找，不自动进入绘制模式）
            MeasurementManager?.GuideEditor.LockGeometryType(MeasureGeometryType.Line);
            // 工具栏只显示直线卡尺（隐藏圆/圆弧卡尺）
            MeasurementManager?.GuideEditor.SetAllowedGeometryTypes(MeasureGeometryType.Line);
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
        private void UpdateResultDisplay(LineDetectResults result)
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
