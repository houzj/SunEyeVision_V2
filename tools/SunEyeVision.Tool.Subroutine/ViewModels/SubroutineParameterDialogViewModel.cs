using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Commands;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Visualization;
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
        private readonly Action? _onOutputsChanged;
        private string _selectedInputTypeName = "Image";
        private string _selectedOutputTypeName = "Double";
        private SubroutineDisplayType _selectedDisplayType = SubroutineDisplayType.Circle;
        public string DialogTitle => $"{_subroutineName} - 子程序参数设置";

        /// <summary>直接暴露源参数的集合（共享引用，实时同步到 node.Parameters）</summary>
        public ObservableCollection<SubroutineInputConfig> Inputs => _parameters.Inputs;
        public ObservableCollection<SubroutineOutputConfig> Outputs => _parameters.Outputs;
        public ObservableCollection<SubroutineDisplayItem> DisplayItems => _parameters.DisplayConfigs;
        public SubroutineLoopConfig LoopConfig => _parameters.LoopConfig;

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

        public List<DisplayTypeOption> AvailableDisplayTypes { get; } = SubroutineDisplayTypeExtensions.GetAllOptions();

        /// <summary>循环设置：可用数据类型</summary>
        public List<string> AvailableLoopDataTypes { get; } = new() { "int", "double", "float", "string" };

        /// <summary>循环设置：比较运算符</summary>
        public List<string> ComparisonOperators { get; } = new() { ">", ">=", "<", "<=", "==", "!=" };

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
            SubroutineParameters parameters,
            Action? onOutputsChanged = null)
        {
            _subroutineId = subroutineId ?? throw new ArgumentNullException(nameof(subroutineId));
            _subroutineName = subroutineName ?? throw new ArgumentNullException(nameof(subroutineName));
            _nodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
            _dataSourceQueryService = dataSourceQueryService;
            _subroutineInternalSources = subroutineInternalSources;
            _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            _onOutputsChanged = onOutputsChanged;

            AddInputCommand = new RelayCommand(AddInput);
            AddOutputCommand = new RelayCommand(AddOutput);
            AddDisplayItemCommand = new RelayCommand(AddDisplayItem);
            RemoveItemCommand = new RelayCommand<object>(ExecuteRemoveItem);
            RemoveDisplayItemCommand = new RelayCommand<SubroutineDisplayItem>(ExecuteRemoveDisplayItem);

            // ★ 订阅 Outputs 变更 → 实时通知主窗口刷新模块结果树
            _parameters.Outputs.CollectionChanged += OnOutputsCollectionChanged;

            // ★ 订阅已有输出项的 PropertyChanged（名称/类型修改时刷新）
            foreach (var output in _parameters.Outputs)
                output.PropertyChanged += OnOutputPropertyChanged;

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
            var item = new SubroutineDisplayItem
            {
                DisplayName = SelectedDisplayType.GetChineseName(),
                DisplayType = SelectedDisplayType
            };

            // 根据类型初始化样式对象
            if (SelectedDisplayType == SubroutineDisplayType.Text)
            {
                item.TextStyle = new TextStyle
                {
                    Name = SelectedDisplayType.GetChineseName(),
                    FontSize = 12.0,
                    OkColor = System.Windows.Media.Color.FromRgb(0, 255, 0),
                    PositionX = 10,
                    PositionY = 10
                };
            }
            else if (SelectedDisplayType != SubroutineDisplayType.Image)
            {
                // 图形类型——更新默认 GraphicsStyle 的名称
                item.GraphicsStyle.Name = SelectedDisplayType.GetChineseName();
            }
            // Image 类型：不需要任何样式对象

            DisplayItems.Add(item);
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

        /// <summary>
        /// Outputs 集合增删时触发回调，通知主窗口实时刷新模块结果树
        /// </summary>
        private void OnOutputsCollectionChanged(object? sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // 新加项 → 订阅 PropertyChanged
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<SubroutineOutputConfig>())
                    item.PropertyChanged += OnOutputPropertyChanged;
            }

            // 移除项 → 取消订阅
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<SubroutineOutputConfig>())
                    item.PropertyChanged -= OnOutputPropertyChanged;
            }

            // 增删 → 触发回调
            _onOutputsChanged?.Invoke();
        }

        /// <summary>
        /// 输出项属性变更（如名称修改）→ 实时刷新模块结果树
        /// </summary>
        private void OnOutputPropertyChanged(object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            _onOutputsChanged?.Invoke();
        }

    }


}
