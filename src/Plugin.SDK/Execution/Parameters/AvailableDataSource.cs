using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 可用数据源模型
    /// </summary>
    /// <remarks>
    /// 表示一个可供绑定的数据源，通常是父节点的某个输出属性。
    /// 用于在参数绑定界面中展示可选的数据源列表。
    /// 
    /// 核心功能：
    /// 1. 描述数据源的基本信息
    /// 2. 支持类型过滤
    /// 3. 支持分组显示
    /// 
    /// 设计分层：
    /// - 身份层（SourceNodeId + PropertyName）：构造后不可变，用于唯一标识一个数据源，
    ///   相等比较基于此二层，确保 WPF 控件能正确匹配选中项
    /// - 展示层（SourceNodeName, DisplayName 等）：UI 显示辅助
    /// - 状态层（CurrentValue, HasExecuted 等）：随执行变化
    /// 
    /// 使用示例：
    /// <code>
    /// // 获取可用数据源列表
    /// var dataSources = dataQueryService.GetAvailableDataSources(nodeId, typeof(double));
    /// 
    /// // 显示在UI中
    /// foreach (var source in dataSources)
    /// {
    ///     // 绑定到属性面板显示: source.DisplayName, source.PropertyType, source.CurrentValue
    /// }
    /// </code>
    /// </remarks>
    [TypeConverter(typeof(AvailableDataSourceConverter))]
    public class AvailableDataSource : IEquatable<AvailableDataSource>
    {
        /// <summary>
        /// 源节点ID（身份字段，构造后不可变）
        /// </summary>
        public string SourceNodeId { get; init; } = string.Empty;

        /// <summary>
        /// 源节点名称
        /// </summary>
        public string SourceNodeName { get; set; } = string.Empty;

        /// <summary>
        /// 源节点类型
        /// </summary>
        public string? SourceNodeType { get; set; }

        /// <summary>
        /// 属性名称（身份字段，构造后不可变）
        /// </summary>
        /// <remarks>
        /// 结果对象中的属性路径。
        /// 示例: "Radius", "Center.X", "CircleFound.Radius"
        /// </remarks>
        public string PropertyName { get; init; } = string.Empty;

        /// <summary>
        /// 显示名称
        /// </summary>
        /// <remarks>
        /// 用于UI显示的友好名称。
        /// 示例: "半径", "圆心X坐标", "检测到的圆半径"
        /// </remarks>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 属性类型
        /// </summary>
        public Type PropertyType { get; set; } = typeof(object);

        /// <summary>
        /// 类型名称（用于显示）
        /// </summary>
        public string TypeName => PropertyType.Name;

        /// <summary>
        /// 完整类型名称
        /// </summary>
        public string FullTypeName => PropertyType.FullName ?? PropertyType.Name;

        /// <summary>
        /// 当前值
        /// </summary>
        /// <remarks>
        /// 如果节点已执行，则包含当前属性的值。
        /// 用于预览和验证。
        /// </remarks>
        public object? CurrentValue { get; set; }

        /// <summary>
        /// 值的字符串表示
        /// </summary>
        public string? ValueString => CurrentValue?.ToString();

        /// <summary>
        /// 单位
        /// </summary>
        public string? Unit { get; set; }

        /// <summary>
        /// 描述信息
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 分组名称
        /// </summary>
        /// <remarks>
        /// 用于在UI中分组显示数据源。
        /// 示例: "圆形检测", "边缘检测", "测量结果"
        /// </remarks>
        public string? GroupName { get; set; }

        /// <summary>
        /// 树节点（用于支持多层树形结构）
        /// </summary>
        /// <remarks>
        /// 关联的树节点对象，用于支持完整的树形结构导航。
        /// </remarks>
        public TreeNodeData? TreeNode { get; set; }

        /// <summary>
        /// 下拉显示文本（中文，用于界面显示）
        /// </summary>
        /// <remarks>
        /// 格式: "节点名.属性名"
        /// 示例: "图像阈值化4.输出图像", "图像载入1.宽度"
        /// </remarks>
        public string DropdownDisplayText
        {
            get
            {
                string displayName = DisplayName;
                if (PropertyType?.IsArray == true)
                    displayName += "[]";
                return $"{SourceNodeName}.{displayName}";
            }
        }

        /// <summary>
        /// 是否与目标类型兼容
        /// </summary>
        public bool IsCompatible { get; set; } = true;

        /// <summary>
        /// 兼容性说明
        /// </summary>
        /// <remarks>
        /// 如果不兼容，说明原因。
        /// </remarks>
        public string? CompatibilityNote { get; set; }

        /// <summary>
        /// 是否为设计时推断
        /// </summary>
        /// <remarks>
        /// 标识此数据源是否通过反射从 ResultType 推断而来，
        /// 而非从实际执行结果中提取。
        /// 
        /// 设计时推断的特点：
        /// - CurrentValue 为 null（未执行）
        /// - PropertyType 准确（从类型定义获取）
        /// - 在 UI 中可能显示为灰色或特殊标记
        /// </remarks>
        public bool IsDesignTime { get; set; } = false;

        /// <summary>
        /// 距离当前节点的距离（0=当前节点，1=直接父节点，以此类推）
        /// </summary>
        public int Distance { get; set; }

        /// <summary>
        /// 节点是否已执行
        /// </summary>
        public bool HasExecuted { get; set; } = true;

        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(SourceNodeId) && !string.IsNullOrEmpty(PropertyName);

        /// <summary>
        /// 创建绑定路径
        /// </summary>
        /// <returns>绑定路径字符串，数组类型追加 []</returns>
        public string GetBindingPath()
        {
            if (PropertyType?.IsArray == true)
                return $"{SourceNodeId}.{PropertyName}[]";
            return $"{SourceNodeId}.{PropertyName}";
        }

        /// <summary>
        /// 获取显示文本
        /// </summary>
        /// <returns>格式化的显示文本</returns>
        public string GetDisplayText()
        {
            var text = $"{SourceNodeName}.{DisplayName}";
            if (!string.IsNullOrEmpty(Unit))
                text += $" ({Unit})";
            return text;
        }

        /// <summary>
        /// 获取详细信息
        /// </summary>
        /// <returns>详细信息字符串</returns>
        public string GetDetailedInfo()
        {
            var info = $"{GetDisplayText()}\n";
            info += $"类型: {TypeName}\n";
            if (CurrentValue != null)
                info += $"当前值: {CurrentValue}\n";
            if (!string.IsNullOrEmpty(Description))
                info += $"说明: {Description}";
            return info.TrimEnd('\n');
        }

        /// <summary>
        /// 获取描述字符串
        /// </summary>
        public override string ToString()
        {
            return GetBindingPath();
        }

        /// <summary>
        /// 创建数据源
        /// </summary>
        public static AvailableDataSource Create(
            string sourceNodeId,
            string sourceNodeName,
            string propertyName,
            string displayName,
            Type propertyType,
            object? currentValue = null)
        {
            return new AvailableDataSource
            {
                SourceNodeId = sourceNodeId,
                SourceNodeName = sourceNodeName,
                PropertyName = propertyName,
                DisplayName = displayName,
                PropertyType = propertyType,
                CurrentValue = currentValue
            };
        }

        #region 值相等（基于身份字段）

        /// <summary>
        /// 基于身份字段（SourceNodeId + PropertyName）判断值相等
        /// </summary>
        /// <remarks>
        /// 不同创建点产生的 AvailableDataSource 实例可能引用不同，但身份相同（同一个数据源）。
        /// WPF 控件（如 ListBox.SelectedItem）依赖 Equals 匹配选中项，
        /// 实现值相等确保跨创建点的实例能被正确匹配。
        /// </remarks>
        public bool Equals(AvailableDataSource? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return SourceNodeId == other.SourceNodeId
                && PropertyName == other.PropertyName;
        }

        /// <summary>
        /// 基于身份字段判断值相等
        /// </summary>
        public override bool Equals(object? obj) => Equals(obj as AvailableDataSource);

        /// <summary>
        /// 基于身份字段计算哈希值
        /// </summary>
        /// <remarks>
        /// 身份字段（SourceNodeId、PropertyName）使用 init setter，构造后不可变，
        /// 因此 GetHashCode 在对象生命周期内保持稳定，可安全用于字典和集合。
        /// </remarks>
        public override int GetHashCode() => HashCode.Combine(SourceNodeId, PropertyName);

        /// <summary>
        /// 相等运算符
        /// </summary>
        public static bool operator ==(AvailableDataSource? left, AvailableDataSource? right)
            => left?.Equals(right) ?? right is null;

        /// <summary>
        /// 不等运算符
        /// </summary>
        public static bool operator !=(AvailableDataSource? left, AvailableDataSource? right)
            => !(left == right);

        #endregion
    }

    /// <summary>
    /// AvailableDataSource 类型转换器，支持 string ↔ AvailableDataSource 的 WPF 绑定转换。
    /// 格式：NodeId.PropertyName
    /// </summary>
    public class AvailableDataSourceConverter : TypeConverter
    {
        /// <summary>
        /// 判断是否可以从指定源类型转换为 AvailableDataSource
        /// </summary>
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        /// <summary>
        /// 从字符串转换（格式：NodeId.PropertyName）
        /// </summary>
        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string str && !string.IsNullOrEmpty(str))
            {
                var dotIndex = str.IndexOf('.');
                if (dotIndex > 0 && dotIndex < str.Length - 1)
                {
                    var nodeId = str[..dotIndex];
                    var propertyName = str[(dotIndex + 1)..];
                    // 去掉数组索引标记 []
                    if (propertyName.EndsWith("[]"))
                        propertyName = propertyName[..^2];
                    return new AvailableDataSource
                    {
                        SourceNodeId = nodeId,
                        PropertyName = propertyName,
                    };
                }
            }
            return base.ConvertFrom(context, culture, value);
        }

        /// <summary>
        /// 判断是否可以转换为指定目标类型
        /// </summary>
        public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        {
            return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is AvailableDataSource ds)
            {
                return ds.GetBindingPath();
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    /// <summary>
    /// 数据源分组
    /// </summary>
    /// <remarks>
    /// 用于在UI中分组显示数据源。
    /// </remarks>
    public class DataSourceGroup
    {
        /// <summary>
        /// 分组名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 分组显示顺序
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// 分组图标
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// 分组中的数据源
        /// </summary>
        public List<AvailableDataSource> DataSources { get; set; } = new List<AvailableDataSource>();

        /// <summary>
        /// 数据源数量
        /// </summary>
        public int Count => DataSources.Count;

        /// <summary>
        /// 添加数据源
        /// </summary>
        public void AddDataSource(AvailableDataSource dataSource)
        {
            dataSource.GroupName = Name;
            DataSources.Add(dataSource);
        }
    }
}
