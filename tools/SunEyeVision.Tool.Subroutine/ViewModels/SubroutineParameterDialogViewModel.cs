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
    public sealed record SubroutineIoSelection(IReadOnlyList<AvailableDataSource> Sources,bool IncludeModuleName);
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
        public string DialogTitle => $"{_subroutineName} - 子程序参数设置";

        /// <summary>直接暴露源参数的集合（共享引用，实时同步到 node.Parameters）</summary>
        public ObservableCollection<SubroutineInputConfig> Inputs => _parameters.Inputs;
        public ObservableCollection<SubroutineOutputConfig> Outputs => _parameters.Outputs;
        public ObservableCollection<SubroutineDisplayItem> DisplayItems => _parameters.DisplayConfigs;
        public SubroutineLoopConfig LoopConfig => _parameters.LoopConfig;
        public Type LoopIndexType=>typeof(long);
        public Type LoopComparisonType=>LoopConfig.DataType switch{"int"=>typeof(long),"float"=>typeof(double),"string"=>typeof(string),_=>throw new InvalidOperationException("循环比较类型无效。")};
        public ObservableCollection<AvailableDataSource> LoopComparisonSources{get;}=new();
        private void LoopChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
        {if(e.PropertyName==nameof(SubroutineLoopConfig.DataType))OnPropertyChanged(nameof(LoopComparisonType));}

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

        public const string SourceImportType="SourceImport";
        public Func<bool,SubroutineIoSelection?>? PickSources{get;set;}
        public List<TypeOption> AvailableTypes { get; } = SdkTypeVocabulary.GetAllTypeOptions().Concat(new[]{new TypeOption("批量选择",SourceImportType)}).ToList();

        public List<DisplayTypeOption> AvailableDisplayTypes { get; } = SubroutineDisplayTypeExtensions.GetAllOptions();

        /// <summary>循环设置：可用数据类型</summary>
        public List<string> AvailableLoopDataTypes { get; } = new() { "int", "float", "string" };

        /// <summary>循环设置：比较运算符</summary>
        public List<string> ComparisonOperators { get; } = new() { ">", "<", "==", "!=", ">=", "<=" };

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
            LoopConfig.PropertyChanged+=LoopChanged;
            _onOutputsChanged = onOutputsChanged;

            AddInputCommand = new RelayCommand(AddInput,()=>!_isDisposed&&Inputs.Count<SubroutineIoRules.MaximumItems);
            AddOutputCommand = new RelayCommand(AddOutput,()=>!_isDisposed&&Outputs.Count<SubroutineIoRules.MaximumItems);
            AddDisplayItemCommand = new RelayCommand(AddDisplayItem);
            RemoveItemCommand = new RelayCommand<object>(ExecuteRemoveItem);
            RemoveDisplayItemCommand = new RelayCommand<SubroutineDisplayItem>(ExecuteRemoveDisplayItem);

            // ★ 订阅 Outputs 变更 → 通知主窗口增量更新模块结果树
            _parameters.Outputs.CollectionChanged += OnOutputsCollectionChanged;

            // Opening a Group observes its configured IO; it must not create rows.
            foreach (var output in _parameters.Outputs)
                output.PropertyChanged += OnOutputPropertyChanged;

            LoadAvailableSources();
            foreach(var source in AvailableSources.Concat(SubroutineInternalSources).Distinct())LoopComparisonSources.Add(source);
        }

        /// <summary>
        /// 关闭窗口时清理：取消全部事件订阅，杜绝重复打开时幽灵订阅累积。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;PickSources=null;
            _parameters.Outputs.CollectionChanged -= OnOutputsCollectionChanged;
            LoopConfig.PropertyChanged-=LoopChanged;
            foreach (var output in _parameters.Outputs)
                output.PropertyChanged -= OnOutputPropertyChanged;
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
            if(!AddInputCommand.CanExecute(null))return;
            if(SelectedInputTypeName==SourceImportType){PickAndImport(true);return;}
            var initialName = SubroutineIoRules.UniqueName("in",Inputs.Select(i=>i.Name).Concat(Outputs.Select(o=>o.Name)));
            Inputs.Add(new SubroutineInputConfig
            {
                Name = initialName,
                BindingKey = initialName,
                TypeName = SelectedInputTypeName
            });
        }

        private void AddOutput()
        {
            if(!AddOutputCommand.CanExecute(null))return;
            if(SelectedOutputTypeName==SourceImportType){PickAndImport(false);return;}
            Outputs.Add(new SubroutineOutputConfig
            {
                Name = SubroutineIoRules.UniqueName("out",Inputs.Select(i=>i.Name).Concat(Outputs.Select(o=>o.Name))),
                TypeName = SelectedOutputTypeName
            });
        }

        private void PickAndImport(bool inputs)
        {
            var picked=PickSources?.Invoke(inputs);if(picked is null||_isDisposed)return;
            try{ImportSources(inputs,picked.Sources,picked.IncludeModuleName);}
            catch(InvalidOperationException ex){MessageBox.Show(ex.Message,"批量选择",MessageBoxButton.OK,MessageBoxImage.Warning);}
        }
        internal static AvailableDataSource CopySource(AvailableDataSource source)=>new()
        {
            SourceWorkflowId=source.SourceWorkflowId,SourceNodeId=source.SourceNodeId,SourceFieldId=source.SourceFieldId,
            SourceNodeName=source.SourceNodeName,SourceNodeType=source.SourceNodeType,PropertyName=source.PropertyName,
            BindingSuffix=source.BindingSuffix,DisplayName=source.DisplayName,PropertyType=source.PropertyType,
            VariableType=source.VariableType,Description=source.Description,Unit=source.Unit,GroupName=source.GroupName
        };
        public void ImportSources(bool inputs,IEnumerable<AvailableDataSource> selection,bool includeModuleName)
        {
            ObjectDisposedException.ThrowIf(_isDisposed,this);
            static (string?,string,Guid?,string) Key(AvailableDataSource s)=>(s.SourceWorkflowId,s.SourceNodeId,s.SourceFieldId,s.GetBindingPath());
            var available=inputs?AvailableSources:SubroutineInternalSources;var selected=selection.DistinctBy(Key).ToArray();
            if((inputs?Inputs.Count:Outputs.Count)+selected.Length>SubroutineIoRules.MaximumItems)throw new InvalidOperationException("批选超过IO数量上限，未添加任何项。");
            var names=Inputs.Select(i=>i.Name).Concat(Outputs.Select(o=>o.Name)).ToHashSet(StringComparer.Ordinal);
            var rows=new List<(string Name,string Type,AvailableDataSource Source)>();
            foreach(var source in selected)
            {
                if(!source.IsValid||!available.Any(a=>Key(a)==Key(source)))throw new InvalidOperationException("批选来源不属于当前输入或输出作用域。");
                var type=SdkTypeVocabulary.ToTypeVocabulary(source.PropertyType)??throw new InvalidOperationException("批选来源类型不受支持："+source.DisplayName);
                var label=string.IsNullOrWhiteSpace(source.DisplayName)?source.PropertyName:source.DisplayName;
                var suggestion=SubroutineIoRules.SuggestName((includeModuleName?source.SourceNodeName+"_":"")+label,inputs?"in":"out");
                var name=SubroutineIoRules.UniqueName(suggestion,names);names.Add(name);rows.Add((name,type,CopySource(source)));
            }
            var added=new List<object>();
            try
            {
                foreach(var row in rows)
                    if(inputs){var item=new SubroutineInputConfig{Name=row.Name,BindingKey=row.Name,TypeName=row.Type,Source=row.Source,BindingSource=row.Source.GetBindingPath(),BindingDisplayText=row.Source.DropdownDisplayText};added.Add(item);Inputs.Add(item);}
                    else{var item=new SubroutineOutputConfig{Name=row.Name,TypeName=row.Type,Source=row.Source,BindingSource=row.Source.GetBindingPath(),BindingDisplayText=row.Source.DropdownDisplayText};added.Add(item);Outputs.Add(item);}
            }
            catch{foreach(var item in added)if(item is SubroutineInputConfig input)Inputs.Remove(input);else Outputs.Remove((SubroutineOutputConfig)item);throw;}
        }

        private void AddDisplayItem()
        {
            var prefix=SelectedDisplayType.GetChineseName();
            var names=DisplayItems.Select(d=>d.DisplayName).ToHashSet(StringComparer.Ordinal);
            var name=prefix;
            for(var suffix=0;names.Contains(name);suffix++)name=prefix+suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var item = new SubroutineDisplayItem
            {
                DisplayName = name,
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
                Inputs.Remove(input);
            }
            else if (item is SubroutineOutputConfig output)
            {
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
            if (_isDisposed) return;

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
