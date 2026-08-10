using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Commands;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Visualization;

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
        private readonly Action<SubroutineOutputChange>? _onOutputsChanged;
        private string _selectedInputTypeName = "Image";
        private string _selectedOutputTypeName = "Double";
        private SubroutineDisplayType _selectedDisplayType = SubroutineDisplayType.Circle;
        private bool _isDisposed;
        private bool _suppressNotifications;
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

        public List<TypeOption> AvailableTypes { get; } = SdkTypeVocabulary.GetAllTypeOptions();

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
            Action<SubroutineOutputChange>? onOutputsChanged = null)
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

            // ★ 订阅 Outputs 变更 → 通知主窗口增量更新模块结果树
            _parameters.Outputs.CollectionChanged += OnOutputsCollectionChanged;

            // ★ 子程序至少需要一个输入和一个输出（幂等补齐，补齐不应触发增量通知）
            _suppressNotifications = true;
            try
            {
                EnsureMinimumInputsOutputs();
            }
            finally
            {
                _suppressNotifications = false;
            }

            // ★ 补齐后统一订阅全部输出项的 PropertyChanged（名称/类型/分组修改时增量刷新）
            foreach (var output in _parameters.Outputs)
                output.PropertyChanged += OnOutputPropertyChanged;

            LoadAvailableSources();
        }

        /// <summary>
        /// 关闭窗口时清理：取消全部事件订阅，杜绝重复打开时幽灵订阅累积。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _parameters.Outputs.CollectionChanged -= OnOutputsCollectionChanged;
            foreach (var output in _parameters.Outputs)
                output.PropertyChanged -= OnOutputPropertyChanged;
        }

        /// <summary>
        /// 子程序至少保留一个输入/输出：集合为空时自动补齐默认项（幂等，重复打开不重复添加）。
        /// </summary>
        private void EnsureMinimumInputsOutputs()
        {
            if (Inputs.Count == 0)
            {
                Inputs.Add(new SubroutineInputConfig
                {
                    Name = "in_1",
                    BindingKey = "in_1",
                    TypeName = "Image"
                });
            }

            if (Outputs.Count == 0)
            {
                Outputs.Add(new SubroutineOutputConfig
                {
                    Name = "out_1",
                    TypeName = "Double"
                });
            }
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
            var initialName = $"in_{Inputs.Count + 1}";
            Inputs.Add(new SubroutineInputConfig
            {
                Name = initialName,
                BindingKey = initialName,
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
            {
                // 子程序至少保留一个输入
                if (Inputs.Count <= 1)
                {
                    MessageBox.Show("子程序至少需要保留一个输入参数。",
                        "无法删除", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Inputs.Remove(input);
            }
            else if (item is SubroutineOutputConfig output)
            {
                // 子程序至少保留一个输出
                if (Outputs.Count <= 1)
                {
                    MessageBox.Show("子程序至少需要保留一个输出参数。",
                        "无法删除", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Outputs.Remove(output);
            }
        }

        public void ExecuteRemoveDisplayItem(SubroutineDisplayItem? item)
        {
            if (item == null) return;
            DisplayItems.Remove(item);
        }

        /// <summary>
        /// Outputs 集合增删时触发回调，通知主窗口增量更新模块结果树
        /// </summary>
        private void OnOutputsCollectionChanged(object? sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (_isDisposed || _suppressNotifications) return;

            // 新加项 → 订阅 PropertyChanged，并通知 Added
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<SubroutineOutputConfig>())
                {
                    item.PropertyChanged += OnOutputPropertyChanged;
                    _onOutputsChanged?.Invoke(new SubroutineOutputChange(
                        SubroutineOutputChangeType.Added, item));
                }
            }

            // 移除项 → 取消订阅，并通知 Removed
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<SubroutineOutputConfig>())
                {
                    item.PropertyChanged -= OnOutputPropertyChanged;
                    _onOutputsChanged?.Invoke(new SubroutineOutputChange(
                        SubroutineOutputChangeType.Removed, item));
                }
            }
        }

        /// <summary>
        /// 输出项属性变更 → 按属性名分流增量通知。
        /// 只关心影响主窗口显示的属性（Name/TypeName/Group）；
        /// 订阅（BindingSource/IsAccumulated 等）不影响主窗口显示，零通知。
        /// </summary>
        private void OnOutputPropertyChanged(object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_isDisposed || sender is not SubroutineOutputConfig output) return;

            switch (e.PropertyName)
            {
                case nameof(SubroutineOutputConfig.Name):
                    _onOutputsChanged?.Invoke(new SubroutineOutputChange(
                        SubroutineOutputChangeType.Renamed, output, previousName: output.PreviousName));
                    break;
                case nameof(SubroutineOutputConfig.TypeName):
                    _onOutputsChanged?.Invoke(new SubroutineOutputChange(
                        SubroutineOutputChangeType.TypeChanged, output,
                        previousTypeName: output.PreviousTypeName));
                    break;
                case nameof(SubroutineOutputConfig.Group):
                    _onOutputsChanged?.Invoke(new SubroutineOutputChange(
                        SubroutineOutputChangeType.GroupChanged, output,
                        previousGroup: output.PreviousGroup));
                    break;
                default:
                    // Source/BindingSource/BindingDisplayText/IsAccumulated 等不影响主界面显示，忽略
                    break;
            }
        }

    }


}
