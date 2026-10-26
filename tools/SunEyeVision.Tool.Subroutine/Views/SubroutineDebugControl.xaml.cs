using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Tool.Subroutine.ViewModels;

namespace SunEyeVision.Tool.Subroutine.Views
{
    /// <summary>
    /// 子程序调试控件 — 包装参数设置的 TabControl 内容，由 DefaultDebugWindow 包裹弹出。
    /// 点击设置按钮时创建此控件 + DefaultDebugWindow，替代旧的独立对话框。
    /// </summary>
    public partial class SubroutineDebugControl : UserControl, ISubroutineParameterEditor
    {
        /// <summary>ViewModel 引用（保持对象存活）</summary>
        public SubroutineParameterDialogViewModel ViewModel { get; private set; } = null!;
        private SubroutineIoPicker? _activePicker;
        public bool HasPendingEdits => _activePicker?.SelectedSources.Count > 0;
        public bool TryPrepare(out string error)
        {
            error = _activePicker is null ? "" : "请先完成或取消批量选择。";
            return _activePicker is null;
        }
        public void Deactivate()
        {
            _activePicker?.ForceClose();
            ViewModel?.Dispose();
        }
        private readonly string _subroutineId,_subroutineName,_nodeId;
        private readonly List<AvailableDataSource>? _internalSources;
        private readonly Action<SubroutineOutputChange>? _onOutputsChanged;

        public SubroutineDebugControl(
            string subroutineId,
            string subroutineName,
            string nodeId,
            IDataSourceQueryService? dataSourceQueryService,
            List<AvailableDataSource>? subroutineInternalSources,
            SubroutineParameters parameters,
            Action<SubroutineOutputChange>? onOutputsChanged = null)
        {
            InitializeComponent();

            _subroutineId=subroutineId;_subroutineName=subroutineName;_nodeId=nodeId;
            _internalSources=subroutineInternalSources;_onOutputsChanged=onOutputsChanged;
            BindParameters(parameters,dataSourceQueryService);
        }
        public void BindParameters(SubroutineParameters parameters,IDataSourceQueryService? queryService)
        {
            Dispatcher.VerifyAccess();
            if (!TryPrepare(out var error)) throw new InvalidOperationException(error);
            ViewModel?.Dispose();
            ViewModel=new SubroutineParameterDialogViewModel(_subroutineId,_subroutineName,_nodeId,queryService,_internalSources,parameters,_onOutputsChanged);
            ViewModel.PickSources=inputs=>
            {
                var owner=Window.GetWindow(this)??throw new InvalidOperationException("批量选择需要已显示的Group窗口。");
                var picker=new SubroutineIoPicker(owner,inputs?ViewModel.AvailableSources:ViewModel.SubroutineInternalSources);
                _activePicker=picker;
                try { return picker.ShowDialog()==true?new SubroutineIoSelection(picker.SelectedSources,picker.IncludeModuleName):null; }
                finally { _activePicker=null; }
            };
            DataContext=ViewModel;
            if(RunParameterContent.Content is ISubroutineRunParameterEditor run)run.BindParameters(parameters);
        }
        public void SetRunParameterEditor(UserControl editor)
        {
            Dispatcher.VerifyAccess();
            if(RunParameterContent.Content is IDisposable previous)previous.Dispose();
            RunParameterContent.Content=editor;
        }
        private void OnIoBindingChanged(object sender,RoutedEventArgs e)
        {
            if(sender is not SunEyeVision.Plugin.SDK.UI.Controls.ParamBinding binding)return;
            binding.GetBindingExpression(SunEyeVision.Plugin.SDK.UI.Controls.ParamBinding.BindingSourceProperty)?.UpdateSource();
            var selected=e is SunEyeVision.Plugin.SDK.UI.Controls.ParamBindingSourceEventArgs picked?picked.DataSource:binding.ResolveSelectedDataSource();
            var source=selected is null?null:SubroutineParameterDialogViewModel.CopySource(selected);
            if(binding.DataContext is SubroutineInputConfig input){input.Source=source;input.BindingSource=binding.BindingSource;input.BindingDisplayText=source?.DropdownDisplayText??"";}
            else if(binding.DataContext is SubroutineOutputConfig output){output.Source=source;output.BindingSource=binding.BindingSource;output.BindingDisplayText=source?.DropdownDisplayText??"";}
        }
        private void OnLoopBindingChanged(object sender,RoutedEventArgs e)
        {
            if(sender is not SunEyeVision.Plugin.SDK.UI.Controls.ParamBinding binding||ViewModel is null)return;
            binding.GetBindingExpression(SunEyeVision.Plugin.SDK.UI.Controls.ParamBinding.BindingSourceProperty)?.UpdateSource();
            var selectedSource=e is SunEyeVision.Plugin.SDK.UI.Controls.ParamBindingSourceEventArgs selected?selected.DataSource:binding.ResolveSelectedDataSource();
            var source=ImageSourceReference.FromDataSource(selectedSource);
            if(Equals(binding.Tag,"SourceCompareBinding")){ViewModel.LoopConfig.SourceCompareReference=source;ViewModel.LoopConfig.SourceCompareBindingDisplayText=selectedSource?.DropdownDisplayText??"";}
            else if(Equals(binding.Tag,"TargetCompareBinding")){ViewModel.LoopConfig.TargetCompareReference=source;ViewModel.LoopConfig.TargetCompareBindingDisplayText=selectedSource?.DropdownDisplayText??"";}
        }
        public void Dispose()
        {
            Deactivate();
            if(RunParameterContent.Content is IDisposable run)run.Dispose();
            RunParameterContent.Content=null;
        }
    }
}
