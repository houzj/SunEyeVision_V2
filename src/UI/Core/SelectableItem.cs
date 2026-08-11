using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.UI.ViewModels.Core
{
    /// <summary>
    /// 可选中项包装基类
    /// </summary>
    /// <remarks>
    /// 设计原则：
    /// 1. 符合标准视觉元件的原型设计（与 ImageInfo 类似的 IsSelected 模式）
    /// 2. 领域模型纯粹，不包含UI状态
    /// 3. 自动同步选中状态到父级ViewModel
    /// 4. 可复用于任何需要可选中UI的场景
    /// 
    /// 使用场景：
    /// - 解决方案配置对话框（SolutionItemViewModel）
    /// - 图像预览器（可统一使用此类）
    /// - 任何需要可选中列表的场景
    /// </remarks>
    /// <typeparam name="T">领域模型类型</typeparam>
    public class SelectableItem<T> : ObservableObject where T : class
    {
        private T _item;
        private bool _isSelected;
        private readonly Action<SelectableItem<T>, bool>? _onSelectionChanged;

        /// <summary>
        /// 领域模型实例
        /// </summary>
        public T Item
        {
            get => _item;
            protected set
            {
                if (SetProperty(ref _item, value, "领域模型"))
                {
                    OnPropertyChanged(nameof(Item));
                }
            }
        }

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value, "选中状态"))
                {
                    // 自动同步到父级ViewModel
                    _onSelectionChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="item">领域模型实例</param>
        /// <param name="onSelectionChanged">选中状态变化回调</param>
        public SelectableItem(T item, Action<SelectableItem<T>, bool>? onSelectionChanged = null)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _onSelectionChanged = onSelectionChanged;
        }

        /// <summary>
        /// 方便访问领域模型属性的辅助方法
        /// </summary>
        public override string ToString()
        {
            return _item?.ToString() ?? base.ToString();
        }
    }
}
