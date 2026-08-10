using System;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Models
{
    /// <summary>子程序输出变更类型（增量更新协议）</summary>
    public enum SubroutineOutputChangeType
    {
        /// <summary>名称变更（需在结果树/图像下拉原位改名）</summary>
        Renamed,
        /// <summary>类型变更（需跨分组移动结果树叶子 + 增删图像下拉条目）</summary>
        TypeChanged,
        /// <summary>分组变更（需在结果树移动分组）</summary>
        GroupChanged,
        /// <summary>新增输出项</summary>
        Added,
        /// <summary>移除输出项</summary>
        Removed,
    }

    /// <summary>子程序输出变更通知（携带变更项与旧值，供主窗口做增量局部更新）</summary>
    public sealed class SubroutineOutputChange
    {
        public SubroutineOutputChangeType ChangeType { get; }
        public SubroutineOutputConfig Output { get; }
        /// <summary>改名前的旧名称（Renamed 时使用）</summary>
        public string PreviousName { get; }
        /// <summary>类型变更前的旧类型（TypeChanged 时使用）</summary>
        public string PreviousTypeName { get; }
        /// <summary>分组变更前的旧分组（GroupChanged 时使用）</summary>
        public string? PreviousGroup { get; }

        public SubroutineOutputChange(
            SubroutineOutputChangeType changeType,
            SubroutineOutputConfig output,
            string previousName = "",
            string previousTypeName = "",
            string? previousGroup = null)
        {
            ChangeType = changeType;
            Output = output;
            PreviousName = previousName;
            PreviousTypeName = previousTypeName;
            PreviousGroup = previousGroup;
        }
    }

    public class SubroutineOutputConfig : ObservableObject
    {
        private string _name = string.Empty;
        private string _typeName = "Float";
        private string? _group;
        private AvailableDataSource? _source;
        private bool _isAccumulated;
        private string _bindingSource = string.Empty;
        private string _bindingDisplayText = string.Empty;
        // 变更前的旧值标记（仅用于增量更新协议，不参与序列化）
        private string _previousName = string.Empty;
        private string _previousTypeName = "Float";
        private string? _previousGroup;

        /// <summary>本次变更前的旧名称（内部标记，供增量更新定位）</summary>
        [JsonIgnore]
        public string PreviousName => _previousName;

        /// <summary>本次变更前的旧类型（内部标记，供增量更新定位）</summary>
        [JsonIgnore]
        public string PreviousTypeName => _previousTypeName;

        /// <summary>本次变更前的旧分组（内部标记，供增量更新定位）</summary>
        [JsonIgnore]
        public string? PreviousGroup => _previousGroup;

        public string Name
        {
            get => _name;
            set
            {
                _previousName = _name;
                if (SetProperty(ref _name, value))
                    OnPropertyChanged(nameof(SourceDisplayText));
            }
        }

        /// <summary>分组（用于树形分组，null 表示无分组）</summary>
        public string? Group
        {
            get => _group;
            set
            {
                _previousGroup = _group;
                SetProperty(ref _group, value);
            }
        }

        public string TypeName
        {
            get => _typeName;
            set
            {
                _previousTypeName = _typeName;
                if (SetProperty(ref _typeName, value))
                    OnPropertyChanged(nameof(ParamType));
            }
        }

        [JsonIgnore]
        public Type? ParamType => SdkTypeVocabulary.ToSystemType(TypeName);

        public AvailableDataSource? Source
        {
            get => _source;
            set
            {
                if (SetProperty(ref _source, value))
                {
                    if (value != null && !string.IsNullOrEmpty(value.TypeName))
                        TypeName = value.TypeName;
                }
            }
        }

        public bool IsAccumulated
        {
            get => _isAccumulated;
            set => SetProperty(ref _isAccumulated, value);
        }

        public string SourceDisplayText => Source?.DropdownDisplayText ?? string.Empty;

        public string BindingSource
        {
            get => _bindingSource;
            set => SetProperty(ref _bindingSource, value);
        }

        public string BindingDisplayText
        {
            get => _bindingDisplayText;
            set => SetProperty(ref _bindingDisplayText, value);
        }
    }
}
