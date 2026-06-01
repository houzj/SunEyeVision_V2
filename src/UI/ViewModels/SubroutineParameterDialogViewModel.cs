using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Commands;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.UI.ViewModels
{
    /// <summary>
    /// 参数映射项（用于对话框显示）
    /// </summary>
    public class ParameterMappingItem : ObservableObject
    {
        private string _parameterName = string.Empty;
        private string _dataType = "object";
        private string _sourceDisplay = "未设置";

        /// <summary>参数名称</summary>
        public string ParameterName
        {
            get => _parameterName;
            set => SetProperty(ref _parameterName, value);
        }

        /// <summary>数据类型</summary>
        public string DataType
        {
            get => _dataType;
            set => SetProperty(ref _dataType, value);
        }

        /// <summary>订阅源显示文本</summary>
        public string SourceDisplay
        {
            get => _sourceDisplay;
            set => SetProperty(ref _sourceDisplay, value);
        }
    }

    /// <summary>
    /// 子程序参数映射对话框的 ViewModel
    /// </summary>
    public class SubroutineParameterDialogViewModel : ViewModelBase
    {
        private readonly string _subroutineId;
        private readonly string _subroutineName;
        private bool _enableLoop;
        private int _maxIterations = 10;

        /// <summary>
        /// 对话框标题
        /// </summary>
        public string DialogTitle => $"{_subroutineName} - 子程序参数设置";

        /// <summary>
        /// 输入映射列表
        /// </summary>
        public ObservableCollection<ParameterMappingItem> InputMappings { get; }

        /// <summary>
        /// 输出映射列表
        /// </summary>
        public ObservableCollection<ParameterMappingItem> OutputMappings { get; }

        /// <summary>
        /// 启用循环执行
        /// </summary>
        public bool EnableLoop
        {
            get => _enableLoop;
            set => SetProperty(ref _enableLoop, value);
        }

        /// <summary>
        /// 最大迭代次数
        /// </summary>
        public int MaxIterations
        {
            get => _maxIterations;
            set => SetProperty(ref _maxIterations, value);
        }

        /// <summary>
        /// 添加输入映射命令
        /// </summary>
        public ICommand AddInputMappingCommand { get; }

        /// <summary>
        /// 添加输出映射命令
        /// </summary>
        public ICommand AddOutputMappingCommand { get; }

        public SubroutineParameterDialogViewModel(string subroutineId, string subroutineName)
        {
            _subroutineId = subroutineId ?? throw new ArgumentNullException(nameof(subroutineId));
            _subroutineName = subroutineName ?? throw new ArgumentNullException(nameof(subroutineName));

            InputMappings = new ObservableCollection<ParameterMappingItem>();
            OutputMappings = new ObservableCollection<ParameterMappingItem>();

            // 初始化示例数据（后续从子程序节点加载真实数据）
            InputMappings.Add(new ParameterMappingItem
            {
                ParameterName = "输入图像",
                DataType = "HImage",
                SourceDisplay = "上游节点"
            });

            OutputMappings.Add(new ParameterMappingItem
            {
                ParameterName = "输出掩码",
                DataType = "HRegion",
                SourceDisplay = "子程序输出"
            });

            AddInputMappingCommand = new RelayCommand(_ => AddInputMapping());
            AddOutputMappingCommand = new RelayCommand(_ => AddOutputMapping());
        }

        /// <summary>
        /// 添加输入映射
        /// </summary>
        public void AddInputMapping()
        {
            InputMappings.Add(new ParameterMappingItem
            {
                ParameterName = $"输入参数_{InputMappings.Count + 1}",
                DataType = "object",
                SourceDisplay = "未设置"
            });
        }

        /// <summary>
        /// 添加输出映射
        /// </summary>
        public void AddOutputMapping()
        {
            OutputMappings.Add(new ParameterMappingItem
            {
                ParameterName = $"输出参数_{OutputMappings.Count + 1}",
                DataType = "object",
                SourceDisplay = "子程序输出"
            });
        }

        /// <summary>
        /// 保存参数配置
        /// </summary>
        public void Save()
        {
            // TODO: 将参数映射写回 SubroutineNode 的数据
            // 当前为占位实现
        }
    }
}
