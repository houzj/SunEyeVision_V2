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
            ViewModel?.Dispose();
            ViewModel=new SubroutineParameterDialogViewModel(_subroutineId,_subroutineName,_nodeId,queryService,_internalSources,parameters,_onOutputsChanged);
            DataContext=ViewModel;
            if(RunParameterContent.Content is ISubroutineRunParameterEditor run)run.BindParameters(parameters);
        }
        public void SetRunParameterEditor(UserControl editor)
        {
            Dispatcher.VerifyAccess();
            if(RunParameterContent.Content is IDisposable previous)previous.Dispose();
            RunParameterContent.Content=editor;
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
            ViewModel?.Dispose();
            if(RunParameterContent.Content is IDisposable run)run.Dispose();
            RunParameterContent.Content=null;
        }
    }
}
