using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Commands;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Tool.Subroutine.Models;
using SunEyeVision.Workflow;

namespace SunEyeVision.Tool.Subroutine.ViewModels
{
    public class SubroutineParameterDialogViewModel : ObservableObject
    {
        private readonly string _subroutineId;
        private readonly string _subroutineName;
        private readonly string _nodeId;
        private readonly IDataSourceQueryService? _dataSourceQueryService;
        private readonly List<AvailableDataSource>? _subroutineInternalSources;
        private readonly SubroutineParameters _parameters;
        private string _selectedInputTypeName = "Image";
        private string _selectedOutputTypeName = "Float";
        private SubroutineDisplayType _selectedDisplayType = SubroutineDisplayType.Circle;
        private System.Windows.Media.ImageSource? _previewImage;

        public string DialogTitle => $"{_subroutineName} - 子程序参数设置";

        /// <summary>直接暴露源参数的集合（共享引用，实时同步到 node.Parameters）</summary>
        public ObservableCollection<SubroutineInputConfig> Inputs => _parameters.Inputs;
        public ObservableCollection<SubroutineOutputConfig> Outputs => _parameters.Outputs;
        public ObservableCollection<SubroutineDisplayItem> DisplayItems => _parameters.DisplayConfigs;
        public SubroutineLoopConfig LoopConfig => _parameters.LoopConfig;

        public ObservableCollection<OutputResultItem> OutputResults { get; } = new();

        public ObservableCollection<AvailableDataSource> AvailableSources { get; } = new();

        /// <summary>子程序内部所有节点的数据源（输出设置/显示设置的订阅源）</summary>
        public ObservableCollection<AvailableDataSource> SubroutineInternalSources { get; } = new();

        public string SelectedInputTypeName
        {
            get => _selectedInputTypeName;
            set => SetProperty(ref _selectedInputTypeName, value);
        }

        public string SelectedOutputTypeName
        {
            get => _selectedOutputTypeName;
            set => SetProperty(ref _selectedOutputTypeName, value);
        }

        public SubroutineDisplayType SelectedDisplayType
        {
            get => _selectedDisplayType;
            set => SetProperty(ref _selectedDisplayType, value);
        }

        public List<TypeOption> AvailableTypes { get; } = GlobalVariableTypeMapper.GetAllTypeOptions();

        public List<SubroutineDisplayType> AvailableDisplayTypes { get; }

        public System.Windows.Media.ImageSource? PreviewImage
        {
            get => _previewImage;
            set => SetProperty(ref _previewImage, value);
        }

        public ICommand AddInputCommand { get; }
        public ICommand AddOutputCommand { get; }
        public ICommand AddDisplayItemCommand { get; }
        public ICommand RemoveItemCommand { get; }
        public ICommand RemoveDisplayItemCommand { get; }

        public SubroutineParameterDialogViewModel(
            string subroutineId,
            string subroutineName,
            string nodeId,
            IDataSourceQueryService? dataSourceQueryService,
            List<AvailableDataSource>? subroutineInternalSources,
            SubroutineParameters parameters)
        {
            _subroutineId = subroutineId ?? throw new ArgumentNullException(nameof(subroutineId));
            _subroutineName = subroutineName ?? throw new ArgumentNullException(nameof(subroutineName));
            _nodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
            _dataSourceQueryService = dataSourceQueryService;
            _subroutineInternalSources = subroutineInternalSources;
            _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));

            AvailableDisplayTypes = Enum.GetValues<SubroutineDisplayType>().ToList();

            AddInputCommand = new RelayCommand(AddInput);
            AddOutputCommand = new RelayCommand(AddOutput);
            AddDisplayItemCommand = new RelayCommand(AddDisplayItem);
            RemoveItemCommand = new RelayCommand<object>(ExecuteRemoveItem);
            RemoveDisplayItemCommand = new RelayCommand<SubroutineDisplayItem>(ExecuteRemoveDisplayItem);

            LoadAvailableSources();
        }

        private void LoadAvailableSources()
        {
            // 输入源：父工作流上游节点（GetAvailableDataSources，和其他节点一致）
            if (_dataSourceQueryService != null)
            {
                var sources = _dataSourceQueryService.GetAvailableDataSources(_nodeId);
                foreach (var source in sources)
                {
                    if (source.IsValid)
                        AvailableSources.Add(source);
                }
            }

            // 输出/显示源：预计算的子程序内部节点数据
            if (_subroutineInternalSources != null)
            {
                foreach (var source in _subroutineInternalSources)
                {
                    if (source.IsValid)
                        SubroutineInternalSources.Add(source);
                }
            }
        }

        private void AddInput()
        {
            Inputs.Add(new SubroutineInputConfig
            {
                Name = $"in_{Inputs.Count + 1}",
                TypeName = SelectedInputTypeName
            });
        }

        private void AddOutput()
        {
            Outputs.Add(new SubroutineOutputConfig
            {
                Name = $"out_{Outputs.Count + 1}",
                TypeName = SelectedOutputTypeName
            });
        }

        private void AddDisplayItem()
        {
            DisplayItems.Add(new SubroutineDisplayItem
            {
                DisplayName = SelectedDisplayType.ToString(),
                DisplayType = SelectedDisplayType
            });
        }

        private void ExecuteRemoveItem(object? item)
        {
            if (item == null) return;
            if (item is SubroutineInputConfig input)
                Inputs.Remove(input);
            else if (item is SubroutineOutputConfig output)
                Outputs.Remove(output);
        }

        public void ExecuteRemoveDisplayItem(SubroutineDisplayItem? item)
        {
            if (item == null) return;
            DisplayItems.Remove(item);
        }

    }

    public class OutputResultItem : ObservableObject
    {
        private string _name = string.Empty;
        private string _value = string.Empty;
        private string _status = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }
    }
}
