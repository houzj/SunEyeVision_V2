using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.UI.Services.ParameterSetting;
using SunEyeVision.UI.Services.Thumbnail;

namespace SunEyeVision.UI.ViewModels
{
    /// <summary>
    /// 图像参数绑定ViewModel
    /// </summary>
    /// <remarks>
    /// 专门处理图像类型参数的绑定，提供：
    /// 1. 图像数据源过滤
    /// 2. 缩略图预览
    /// 3. 下拉选择器界面
    /// 
    /// 使用示例：
    /// <code>
    /// var viewModel = new ImageParamSettingViewModel(
    ///     "InputImage",
    ///     "输入图像",
    ///     dataSourceQueryService,
    ///     thumbnailLoader);
    /// 
    /// // 加载数据源
    /// viewModel.LoadImageDataSources(currentNodeId);
    /// 
    /// // 选择数据源
    /// viewModel.SelectedImageDataSource = viewModel.ImageDataSources.First();
    /// </code>
    /// </remarks>
    public class ImageParamSettingViewModel : ParamSettingViewModelBase
    {
        #region 字段

        private readonly IDataSourceQueryService _dataSourceQueryService;
        private readonly ImageDataSourceService _imageDataSourceService;
        private readonly SmartThumbnailLoader? _thumbnailLoader;
        private ParamSetting _setting;
        private BindingType _selectedBindingType;
        private AvailableDataSource? _selectedImageDataSource;
        private BitmapImage? _previewThumbnail;
        private bool _isLoadingThumbnail;
        private bool _isDataSourceLoading;
        private string _currentNodeId = string.Empty;

        #endregion

        #region 属性

        /// <summary>
        /// 参数名称
        /// </summary>
        public override string ParameterName => _setting.ParameterName;

        /// <summary>
        /// 参数显示名称
        /// </summary>
        public override string DisplayName { get; }

        /// <summary>
        /// 参数类型
        /// </summary>
        public Type ParameterType { get; }

        /// <summary>
        /// 参数类型名称
        /// </summary>
        public string TypeName => ImageDataSourceService.GetImageTypeDisplayName(ParameterType);

        /// <summary>
        /// 参数描述
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// 绑定类型
        /// </summary>
        public BindingType SelectedBindingType
        {
            get => _selectedBindingType;
            set
            {
                if (SetProperty(ref _selectedBindingType, value))
                {
                    _setting.BindingType = value;
                    OnPropertyChanged(nameof(IsConstantMode));
                    OnPropertyChanged(nameof(IsDynamicMode));
                    Validate();
                }
            }
        }

        /// <summary>
        /// 是否为常量模式
        /// </summary>
        public bool IsConstantMode => SelectedBindingType == BindingType.Constant;

        /// <summary>
        /// 是否为动态绑定模式
        /// </summary>
        public bool IsDynamicMode => SelectedBindingType == BindingType.Binding;

        /// <summary>
        /// 图像数据源分组列表
        /// </summary>
        public ObservableCollection<ImageDataSourceGroup> ImageDataSourceGroups { get; }

        /// <summary>
        /// 扁平化的图像数据源列表（用于下拉选择器）
        /// </summary>
        public ObservableCollection<AvailableDataSource> ImageDataSources { get; }

        /// <summary>
        /// 选中的图像数据源
        /// </summary>
        public AvailableDataSource? SelectedImageDataSource
        {
            get => _selectedImageDataSource;
            set
            {
                if (SetProperty(ref _selectedImageDataSource, value))
                {
                    if (value != null)
                    {
                        _setting.SourceNodeId = value.SourceNodeId;
                        _setting.SourceProperty = value.PropertyName;
                    }
                    else
                    {
                        _setting.SourceNodeId = null;
                        _setting.SourceProperty = null;
                    }

                    OnPropertyChanged(nameof(SelectedDataSourceDisplay));
                    OnPropertyChanged(nameof(HasSelection));
                    OnPropertyChanged(nameof(SelectionInfo));

                    // 异步加载预览缩略图
                    _ = LoadPreviewThumbnailAsync();

                    Validate();
                }
            }
        }

        /// <summary>
        /// 选中的数据源显示文本
        /// </summary>
        public string SelectedDataSourceDisplay
        {
            get
            {
                if (SelectedImageDataSource == null)
                    return "选择图像数据源...";

                return $"{SelectedImageDataSource.SourceNodeName} → {SelectedImageDataSource.DisplayName}";
            }
        }

        /// <summary>
        /// 是否有选中项
        /// </summary>
        public bool HasSelection => SelectedImageDataSource != null;

        /// <summary>
        /// 选择信息（用于Tooltip）
        /// </summary>
        public string SelectionInfo
        {
            get
            {
                if (SelectedImageDataSource == null)
                    return string.Empty;

                return $"节点: {SelectedImageDataSource.SourceNodeName}\n" +
                       $"属性: {SelectedImageDataSource.DisplayName}\n" +
                       $"类型: {SelectedImageDataSource.TypeName}";
            }
        }

        /// <summary>
        /// 预览缩略图
        /// </summary>
        public BitmapImage? PreviewThumbnail
        {
            get => _previewThumbnail;
            private set => SetProperty(ref _previewThumbnail, value);
        }

        /// <summary>
        /// 是否正在加载缩略图
        /// </summary>
        public bool IsLoadingThumbnail
        {
            get => _isLoadingThumbnail;
            private set => SetProperty(ref _isLoadingThumbnail, value);
        }

        /// <summary>
        /// 是否正在加载数据源
        /// </summary>
        public bool IsDataSourceLoading
        {
            get => _isDataSourceLoading;
            private set => SetProperty(ref _isDataSourceLoading, value);
        }

        /// <summary>
        /// 可用图像数据源数量
        /// </summary>
        public int AvailableImageCount => ImageDataSources.Count;

        /// <summary>
        /// 当前绑定配置
        /// </summary>
        public ParamSetting Binding => _setting;

        #endregion

        #region 命令

        /// <summary>
        /// 刷新数据源命令
        /// </summary>
        public ICommand RefreshCommand { get; }

        /// <summary>
        /// 清除选择命令
        /// </summary>
        public ICommand ClearSelectionCommand { get; }

        /// <summary>
        /// 切换绑定模式命令
        /// </summary>
        public ICommand ToggleBindingModeCommand { get; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建图像参数绑定ViewModel
        /// </summary>
        public ImageParamSettingViewModel(
            string parameterName,
            string displayName,
            IDataSourceQueryService dataSourceQueryService,
            SmartThumbnailLoader? thumbnailLoader = null,
            Type? parameterType = null,
            string? description = null)
        {
            DisplayName = displayName;
            Description = description;
            ParameterType = parameterType ?? typeof(object);
            _dataSourceQueryService = dataSourceQueryService ?? throw new ArgumentNullException(nameof(dataSourceQueryService));
            _thumbnailLoader = thumbnailLoader;

            _imageDataSourceService = new ImageDataSourceService(_dataSourceQueryService);

            _setting = new ParamSetting
            {
                ParameterName = parameterName,
                BindingType = BindingType.Binding,
                TargetType = ParameterType
            };

            _selectedBindingType = BindingType.Binding;

            ImageDataSourceGroups = new ObservableCollection<ImageDataSourceGroup>();
            ImageDataSources = new ObservableCollection<AvailableDataSource>();

            RefreshCommand = new RelayCommand(ExecuteRefresh);
            ClearSelectionCommand = new RelayCommand(ExecuteClearSelection, () => HasSelection);
            ToggleBindingModeCommand = new RelayCommand(ExecuteToggleBindingMode);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 加载图像数据源
        /// </summary>
        /// <param name="nodeId">当前节点ID</param>
        public void LoadImageDataSources(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return;

            _currentNodeId = nodeId;
            ExecuteRefresh();
        }

        /// <summary>
        /// 刷新数据源列表
        /// </summary>
        public void Refresh()
        {
            ExecuteRefresh();
        }

        /// <summary>
        /// 获取当前绑定配置
        /// </summary>
        public override ParamSetting GetSetting()
        {
            return _setting.Clone();
        }

        /// <summary>
        /// 应用绑定配置
        /// </summary>
        public void ApplySetting()
        {
            RaiseSettingChanged(_setting.Clone());
        }

        #endregion

        #region 私有方法

        private void ExecuteRefresh()
        {
            if (string.IsNullOrEmpty(_currentNodeId))
                return;

            IsDataSourceLoading = true;

            try
            {
                ImageDataSourceGroups.Clear();
                ImageDataSources.Clear();

                var groups = _imageDataSourceService.GetImageDataSourcesGrouped(_currentNodeId);

                foreach (var group in groups)
                {
                    ImageDataSourceGroups.Add(group);

                    foreach (var dataSource in group.DataSources)
                    {
                        ImageDataSources.Add(dataSource);
                    }
                }

                OnPropertyChanged(nameof(AvailableImageCount));

                // 如果当前选中的数据源不在列表中，清除选择
                if (SelectedImageDataSource != null)
                {
                    var stillExists = ImageDataSources.Contains(SelectedImageDataSource);

                    if (!stillExists)
                    {
                        SelectedImageDataSource = null;
                    }
                }
            }
            finally
            {
                IsDataSourceLoading = false;
            }
        }

        private void ExecuteClearSelection()
        {
            SelectedImageDataSource = null;
            PreviewThumbnail = null;
        }

        private void ExecuteToggleBindingMode()
        {
            SelectedBindingType = IsDynamicMode ? BindingType.Constant : BindingType.Binding;
        }

        private async Task LoadPreviewThumbnailAsync()
        {
            PreviewThumbnail = null;

            if (_thumbnailLoader == null || SelectedImageDataSource == null)
                return;

            // 尝试从CurrentValue获取文件路径
            var currentValue = SelectedImageDataSource.CurrentValue;
            string? filePath = null;

            // 检查CurrentValue是否为文件路径字符串
            if (currentValue is string path && System.IO.File.Exists(path))
            {
                filePath = path;
            }
            // 检查CurrentValue是否有FilePath属性
            else if (currentValue != null)
            {
                var filePathProp = currentValue.GetType().GetProperty("FilePath");
                if (filePathProp != null)
                {
                    filePath = filePathProp.GetValue(currentValue) as string;
                }
            }

            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                return;

            IsLoadingThumbnail = true;

            try
            {
                await Task.Run(() =>
                {
                    var thumbnail = _thumbnailLoader.LoadThumbnail(filePath, 64, isHighPriority: true);

                    if (thumbnail != null)
                    {
                        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        {
                            PreviewThumbnail = thumbnail;
                        });
                    }
                });
            }
            finally
            {
                IsLoadingThumbnail = false;
            }
        }

        private void Validate()
        {
            IsValid = true;
            ValidationMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(ParameterName))
            {
                IsValid = false;
                ValidationMessage = "参数名称不能为空";
                return;
            }

            if (IsDynamicMode)
            {
                if (string.IsNullOrWhiteSpace(_setting.SourceNodeId))
                {
                    IsValid = false;
                    ValidationMessage = "请选择图像数据源";
                    return;
                }

                if (string.IsNullOrWhiteSpace(_setting.SourceProperty))
                {
                    IsValid = false;
                    ValidationMessage = "请选择图像属性";
                    return;
                }
            }
        }

        #endregion
    }
}
