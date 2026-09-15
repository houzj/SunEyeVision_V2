using System.Collections.ObjectModel;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.Script.Models
{
    /// <summary>
    /// 监视模式树形节点 — 根节点=变量名(类型)，叶节点=值
    /// 继承 ObservableObject 支持 WPF 数据绑定刷新
    /// </summary>
    public class MonitorNode : ObservableObject
    {
        private string _name = string.Empty;
        private string _typeDisplay = string.Empty;
        private string _displayValue = "无结果";
        private bool _hasValue;
        private bool _hasBinding;
        private string _bindingDisplayText = string.Empty;
        private ObservableCollection<MonitorNode>? _children;
        private bool _isExpanded;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string TypeDisplay
        {
            get => _typeDisplay;
            set => SetProperty(ref _typeDisplay, value);
        }

        public string DisplayValue
        {
            get => _displayValue;
            set => SetProperty(ref _displayValue, value);
        }

        public bool HasValue
        {
            get => _hasValue;
            set => SetProperty(ref _hasValue, value);
        }

        /// <summary>是否有绑定源（仅输入变量）</summary>
        public bool HasBinding
        {
            get => _hasBinding;
            set => SetProperty(ref _hasBinding, value);
        }

        /// <summary>绑定源显示文本（格式：节点名.属性名）</summary>
        public string BindingDisplayText
        {
            get => _bindingDisplayText;
            set => SetProperty(ref _bindingDisplayText, value);
        }

        /// <summary>子节点集合（懒加载，仅数组/集合类型有值）</summary>
        public ObservableCollection<MonitorNode>? Children
        {
            get => _children;
            private set
            {
                SetProperty(ref _children, value);
                OnPropertyChanged(nameof(HasChildren));
            }
        }

        /// <summary>是否有子节点（控制 TreeView 展开箭头显示）</summary>
        public bool HasChildren => _children is { Count: > 0 };

        /// <summary>树节点是否展开（默认展开）</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>添加子节点（懒加载创建 Children 集合）</summary>
        public void AddChild(MonitorNode child)
        {
            bool wasEmpty = _children == null || _children.Count == 0;
            if (_children == null)
                Children = new ObservableCollection<MonitorNode>();
            _children!.Add(child);
            if (wasEmpty && _children.Count == 1)
                OnPropertyChanged(nameof(HasChildren));
        }

        /// <summary>清除所有子节点</summary>
        public void ClearChildren()
        {
            if (_children != null && _children.Count > 0)
            {
                _children.Clear();
                OnPropertyChanged(nameof(HasChildren));
            }
        }

        /// <summary>清除自身和所有子节点的值</summary>
        public void ClearValuesRecursive()
        {
            _displayValue = "无结果";
            OnPropertyChanged(nameof(DisplayValue));
            _hasValue = false;
            OnPropertyChanged(nameof(HasValue));
            ClearChildren();
        }
    }
}
