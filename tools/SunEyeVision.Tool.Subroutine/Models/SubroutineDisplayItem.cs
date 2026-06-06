using System;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Tool.Subroutine.Models
{
    /// <summary>
    /// 子程序显示配置项（对齐 21001 图像显示模式）
    /// </summary>
    public class SubroutineDisplayItem : ObservableObject
    {
        private string _displayName = string.Empty;
        private SubroutineDisplayType _displayType = SubroutineDisplayType.Circle;
        private string _mappedSource = string.Empty;
        private AvailableDataSource? _boundDataSource;
        private bool _isAccumulated;
        private string _bindingSource = string.Empty;
        private string _bindingDisplayText = string.Empty;

        /// <summary>参数名称</summary>
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        /// <summary>显示类型</summary>
        public SubroutineDisplayType DisplayType
        {
            get => _displayType;
            set => SetProperty(ref _displayType, value);
        }

        /// <summary>映射源显示文本（关联关系）</summary>
        public string MappedSource
        {
            get => _mappedSource;
            set => SetProperty(ref _mappedSource, value);
        }

        /// <summary>绑定的数据源</summary>
        public AvailableDataSource? BoundDataSource
        {
            get => _boundDataSource;
            set
            {
                if (SetProperty(ref _boundDataSource, value))
                    MappedSource = value?.GetDisplayText() ?? string.Empty;
            }
        }

        /// <summary>是否累积显示</summary>
        public bool IsAccumulated
        {
            get => _isAccumulated;
            set => SetProperty(ref _isAccumulated, value);
        }

        /// <summary>参数类型（null 表示不限制，显示所有图形和图像数据源）</summary>
        public Type? ParamType => null;

        /// <summary>绑定源路径（格式：节点ID.属性名），对接 ParamBinding.BindingSource</summary>
        public string BindingSource
        {
            get => _bindingSource;
            set => SetProperty(ref _bindingSource, value);
        }

        /// <summary>绑定显示文本（格式：节点名.属性名），对接 ParamBinding.BindingDisplayText</summary>
        public string BindingDisplayText
        {
            get => _bindingDisplayText;
            set => SetProperty(ref _bindingDisplayText, value);
        }
    }

    /// <summary>
    /// 子程序显示类型（对齐 GraphicsType 枚举 + Text + Image）
    /// </summary>
    public enum SubroutineDisplayType
    {
        Point,
        Line,
        Rectangle,
        Circle,
        Arc,
        Polygon,
        Contour,
        Arrow,
        Cross,
        CoordinateSystem,
        Text,
        Image
    }
}
