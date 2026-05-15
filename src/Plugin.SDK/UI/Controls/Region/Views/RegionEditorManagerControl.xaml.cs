using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Views
{
    /// <summary>
    /// 区域编辑器管理器 - 管理两个 RegionEditorControl 实例
    /// 使用 HalconImageControl 的 DrawingObject 进行 ROI 交互
    /// </summary>
    public partial class RegionEditorManagerControl : UserControl
    {
        private HalconImageControl? _mainImageControl;

        public RegionEditorManagerControl()
        {
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            InitializeComponent();

            inspectionsEditor.RenderRequested += OnEditorRenderRequested;
            masksEditor.RenderRequested += OnEditorRenderRequested;
            inspectionsEditor.ToolActivated += () => OnEditorToolActivated(inspectionsEditor);
            masksEditor.ToolActivated += () => OnEditorToolActivated(masksEditor);
        }

        #region Dependency Properties

        public static readonly DependencyProperty InspectionRegionsSourceProperty =
            DependencyProperty.Register(nameof(InspectionRegionsSource),
                typeof(ObservableCollection<RegionData>),
                typeof(RegionEditorManagerControl),
                new PropertyMetadata(null, OnInspectionSourceChanged));

        public ObservableCollection<RegionData>? InspectionRegionsSource
        {
            get => (ObservableCollection<RegionData>?)GetValue(InspectionRegionsSourceProperty);
            set => SetValue(InspectionRegionsSourceProperty, value);
        }

        public static readonly DependencyProperty MaskRegionsSourceProperty =
            DependencyProperty.Register(nameof(MaskRegionsSource),
                typeof(ObservableCollection<RegionData>),
                typeof(RegionEditorManagerControl),
                new PropertyMetadata(null, OnMaskSourceChanged));

        public ObservableCollection<RegionData>? MaskRegionsSource
        {
            get => (ObservableCollection<RegionData>?)GetValue(MaskRegionsSourceProperty);
            set => SetValue(MaskRegionsSourceProperty, value);
        }

        public static readonly DependencyProperty AvailableDataSourcesProperty =
            DependencyProperty.Register(nameof(AvailableDataSources),
                typeof(ObservableCollection<AvailableDataSource>),
                typeof(RegionEditorManagerControl),
                new PropertyMetadata(null, OnAvailableDataSourcesChanged));

        public ObservableCollection<AvailableDataSource>? AvailableDataSources
        {
            get => (ObservableCollection<AvailableDataSource>?)GetValue(AvailableDataSourcesProperty);
            set => SetValue(AvailableDataSourcesProperty, value);
        }

        #endregion

        #region Property Change Callbacks

        private static void OnInspectionSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RegionEditorManagerControl control && e.NewValue is ObservableCollection<RegionData> source)
            {
                control.inspectionsEditor?.SetRegionsSource(source);
            }
        }

        private static void OnMaskSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RegionEditorManagerControl control && e.NewValue is ObservableCollection<RegionData> source)
            {
                control.masksEditor?.SetRegionsSource(source);
            }
        }

        private static void OnAvailableDataSourcesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RegionEditorManagerControl control && e.NewValue is ObservableCollection<AvailableDataSource> sources)
            {
                control.inspectionsEditor.AvailableDataSources = sources;
                control.masksEditor.AvailableDataSources = sources;
            }
        }

        #endregion

        #region Public Methods

        public void SetMainImageControl(HalconImageControl? imageControl)
        {
            if (_mainImageControl == imageControl)
                return;

            _mainImageControl = imageControl;

            if (_mainImageControl != null)
            {
                inspectionsEditor.SetHalconImageControl(_mainImageControl);
                masksEditor.SetHalconImageControl(_mainImageControl);

                PluginLogger.Info("[RegionEditorManager] HalconImageControl set", "RegionEditorManager");
            }
        }

        public void ClearMainOverlay()
        {
            inspectionsEditor?.SetHalconImageControl(null);
            masksEditor?.SetHalconImageControl(null);
            _mainImageControl = null;
        }

        #endregion

        #region Event Handlers

        private void OnEditorRenderRequested()
        {
            // DrawingObject 由 HalconImageControl 管理，无需额外渲染
        }

        private void OnEditorToolActivated(RegionEditorControl activatedEditor)
        {
            if (activatedEditor == inspectionsEditor)
            {
                masksEditor.EnterIdleMode();
            }
            else
            {
                inspectionsEditor.EnterIdleMode();
            }
        }

        #endregion
    }
}
