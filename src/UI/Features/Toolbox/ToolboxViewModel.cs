using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK;
using SunEyeVision.UI.Models;
using SunEyeVision.UI.Services.Toolbox;

namespace SunEyeVision.UI.ViewModels
{
    /// <summary>
    /// 工具箱视图模型 - 支持动态插件加载和双模显示（优化版）
    /// </summary>
    public class ToolboxViewModel : ViewModelBase
    {
        private string _selectedCategory;
        private bool _isCompactMode = true;
        private bool _isCompactModePopupOpen = false;
        private double _popupVerticalOffset = 0;
        private readonly ToolboxToolCacheManager _toolCacheManager;

        public ObservableCollection<ToolCategory> Categories { get; }
        public ObservableCollection<ToolItem> AllTools { get; }
        public ObservableCollection<ToolItem> SelectedCategoryTools { get; }

        /// <summary>
        /// 是否为紧凑模式（true: 紧凑侧边栏模式, false: 传统展开模式）
        /// </summary>
        public bool IsCompactMode
        {
            get => _isCompactMode;
            set
            {
                if (SetProperty(ref _isCompactMode, value))
                {
                    // 切换模式时清空选中分类并关闭 Popup
                    SelectedCategory = null;
                    IsCompactModePopupOpen = false;
                    OnPropertyChanged(nameof(DisplayModeIcon));
                    OnPropertyChanged(nameof(DisplayModeTooltip));
                }
            }
        }

        /// <summary>
        /// 紧凑模式下的 Popup 是否打开
        /// </summary>
        public bool IsCompactModePopupOpen
        {
            get => _isCompactModePopupOpen;
            set => SetProperty(ref _isCompactModePopupOpen, value);
        }

        /// <summary>
        /// Popup的垂直偏移量（相对于PlacementTarget）
        /// </summary>
        public double PopupVerticalOffset
        {
            get => _popupVerticalOffset;
            set => SetProperty(ref _popupVerticalOffset, value);
        }

        /// <summary>
        /// 设置Popup的垂直偏移量
        /// </summary>
        public void SetPopupVerticalOffset(double offset)
        {
            PopupVerticalOffset = offset;
        }

        /// <summary>
        /// 显示模式图标（三角箭头）
        /// </summary>
        public string DisplayModeIcon
        {
            get => IsCompactMode ? "◀" : "▶";
        }

        /// <summary>
        /// 显示模式提示
        /// </summary>
        public string DisplayModeTooltip
        {
            get => IsCompactMode ? "切换到展开模式" : "切换到紧凑模式";
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    UpdateSelectedCategoryTools();
                    UpdateCategorySelection();
                    OnPropertyChanged(nameof(SelectedCategoryIcon));

                    // 紧凑模式：控制 Popup 打开/关闭
                    if (IsCompactMode && !string.IsNullOrEmpty(value))
                    {
                        IsCompactModePopupOpen = true;
                    }
                    else
                    {
                        IsCompactModePopupOpen = false;
                    }
                }
            }
        }

        public string SelectedCategoryIcon
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SelectedCategory))
                    return "";
                var category = Categories.FirstOrDefault(c => c.Name == SelectedCategory);
                return category?.Icon ?? "📦";
            }
        }

        public ICommand ToggleCategoryCommand { get; }
        public ICommand UseToolCommand { get; }
        public ICommand ClearSelectionCommand { get; }
        public ICommand ToggleDisplayModeCommand { get; }

        public ToolboxViewModel()
        {
            Categories = new ObservableCollection<ToolCategory>();
            AllTools = new ObservableCollection<ToolItem>();
            SelectedCategoryTools = new ObservableCollection<ToolItem>();
            _selectedCategory = null;
            _toolCacheManager = new ToolboxToolCacheManager(AllTools);

            ToggleCategoryCommand = new RelayCommand<ToolCategory>(ExecuteToggleCategory);
            UseToolCommand = new RelayCommand<ToolItem>(ExecuteUseTool);
            ClearSelectionCommand = new RelayCommand(ExecuteClearSelection);
            ToggleDisplayModeCommand = new RelayCommand(ExecuteToggleDisplayMode);

            InitializeFromPlugins();

            // 监听分类工具变化，如果没有工具则关闭Popup
            SelectedCategoryTools.CollectionChanged += (s, e) =>
            {
                if (IsCompactMode && SelectedCategoryTools.Count == 0)
                {
                    IsCompactModePopupOpen = false;
                }
            };
        }

        /// <summary>
        /// 从插件初始化工具箱
        /// </summary>
        private void InitializeFromPlugins()
        {
            // 清空现有数据
            Categories.Clear();
            AllTools.Clear();

            // 注意：插件已在 App.OnStartup 中由 PluginManager 加载并注册到 ToolRegistry
            // 这里只需要从 ToolRegistry 加载工具即可
            LoadToolsFromRegistry();

            // 更新分类的工具数量
            UpdateCategoryToolCounts();
        }

        /// <summary>
        /// 从工具注册中心加载工具
        /// </summary>
        private void LoadToolsFromRegistry()
        {
            var categories = ToolRegistry.GetAllCategories();
            foreach (var category in categories)
            {
                var categoryIcon = GetCategoryIcon(category);
                var categoryDesc = GetCategoryDescription(category);
                Categories.Add(new ToolCategory(category, categoryIcon, categoryDesc, 0, false));
            }

            var tools = ToolRegistry.GetAllToolMetadata();
            foreach (var tool in tools)
            {
                var toolItem = new ToolItem(
                    tool.DisplayName,
                    tool.Category,
                    tool.Icon,
                    tool.Description,
                    tool.ToolType?.Name,
                    false // HasDebugInterface 已删除，默认为 false
                );
                // 设置真正的工具ID
                toolItem.ToolId = tool.Id;
                AllTools.Add(toolItem);
            }
        }

        /// <summary>
        /// 获取分类图标
        /// </summary>
        private string GetCategoryIcon(string category)
        {
            return category switch
            {
                "采集" => "📷",
                "定位" => "🎯",
                "图像处理" => "🖼️",
                "识别" => "🔍",
                "测量" => "📏",
                "工作流控制" => "🔄",
                _ => "📦"
            };
        }

        /// <summary>
        /// 获取分类描述
        /// </summary>
        private string GetCategoryDescription(string category)
        {
            return category + "相关工具";
        }

        private void UpdateCategoryToolCounts()
        {
            foreach (var category in Categories)
            {
                category.ToolCount = AllTools.Count(t => t.Category == category.Name);
                // 为每个分类设置工具列表
                var tools = AllTools.Where(t => t.Category == category.Name).ToList();
                category.Tools = new System.Collections.ObjectModel.ObservableCollection<ToolItem>(tools);
            }
        }

        /// <summary>
        /// 更新选中分类的工具列表（使用缓存优化）
        /// </summary>
        private void UpdateSelectedCategoryTools()
        {
            SelectedCategoryTools.Clear();

            if (string.IsNullOrWhiteSpace(SelectedCategory))
            {
                return;
            }

            // 使用缓存管理器获取工具列表
            var cachedTools = _toolCacheManager.GetToolsByCategory(SelectedCategory);
            foreach (var tool in cachedTools)
            {
                SelectedCategoryTools.Add(tool);
            }
        }

        /// <summary>
        /// 更新分类选中状态
        /// </summary>
        private void UpdateCategorySelection()
        {
            foreach (var category in Categories)
            {
                category.IsSelected = (category.Name == SelectedCategory);
            }
        }

        private void ExecuteToggleCategory(ToolCategory category)
        {
            if (SelectedCategory == category.Name)
            {
                SelectedCategory = null; // 取消选择
            }
            else
            {
                SelectedCategory = category.Name; // 选择新分类
            }
        }

        private void ExecuteUseTool(ToolItem tool)
        {
            // TODO: 实现工具使用事件
        }

        private void ExecuteClearSelection()
        {
            SelectedCategory = null;
        }

        /// <summary>
        /// 切换显示模式
        /// </summary>
        private void ExecuteToggleDisplayMode()
        {
            IsCompactMode = !IsCompactMode;
        }
    }
}
