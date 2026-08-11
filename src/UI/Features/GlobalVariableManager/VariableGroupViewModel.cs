using System.Collections.ObjectModel;
using System.Linq;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Workflow;

using SunEyeVision.UI.Core;

namespace SunEyeVision.UI.Features.GlobalVariableManager
{
    /// <summary>
    /// 变量分组视图模型
    /// </summary>
    /// <remarks>
    /// 用于在UI中显示和管理变量分组。
    /// 每个分组包含一组全局变量。
    /// 
    /// 设计原则（rule-004: 方案设计要求）：
    /// - 支持多工位/多相机场景的分组管理
    /// - 与VisionMaster风格保持一致
    /// </remarks>
    public class VariableGroupViewModel : ViewModelBase
    {
        private string _name;
        private bool _isSelected;
        private int _variableCount;

        /// <summary>
        /// 分组名称
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value, "分组名称"))
                {
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// 变量数量
        /// </summary>
        public int VariableCount
        {
            get => _variableCount;
            set
            {
                if (SetProperty(ref _variableCount, value))
                {
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        /// <summary>
        /// 分组中的变量列表
        /// </summary>
        public ObservableCollection<GlobalVariable> Variables { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public VariableGroupViewModel(string name)
        {
            _name = name;
            Variables = new ObservableCollection<GlobalVariable>();
        }

        /// <summary>
        /// 添加变量
        /// </summary>
        public void AddVariable(GlobalVariable variable)
        {
            Variables.Add(variable);
            VariableCount = Variables.Count;
        }

        /// <summary>
        /// 移除变量
        /// </summary>
        public void RemoveVariable(GlobalVariable variable)
        {
            Variables.Remove(variable);
            VariableCount = Variables.Count;
        }

        /// <summary>
        /// 更新变量数量
        /// </summary>
        public void UpdateVariableCount()
        {
            VariableCount = Variables.Count;
        }

        /// <summary>
        /// 显示名称（包含数量）
        /// </summary>
        public string DisplayName => $"{Name} ({VariableCount})";

        // ===== 内联编辑模式 =====

        private bool _isEditing;
        private string _editingText = "";

        /// <summary>是否处于内联编辑模式（控制 Tab 变为 TextBox）</summary>
        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        /// <summary>编辑中的文本（绑定到 TextBox.Text）</summary>
        public string EditingText
        {
            get => _editingText;
            set => SetProperty(ref _editingText, value);
        }

        /// <summary>开始编辑：将当前名称复制到编辑文本</summary>
        public void BeginEdit()
        {
            EditingText = Name;
            IsEditing = true;
        }

        /// <summary>确认编辑</summary>
        public void CommitEdit() => IsEditing = false;

        /// <summary>取消编辑：还原名称</summary>
        public void CancelEdit() { IsEditing = false; EditingText = ""; }
    }
}
