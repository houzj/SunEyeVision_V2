using System.Linq;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 图像源引用 - 可序列化的精简模型，仅存储身份和展示信息
    /// </summary>
    /// <remarks>
    /// 对应 AvailableDataSource 的身份层+展示层：
    /// - 身份层：SourceNodeId + PropertyName（唯一标识，用于加载后从 AvailableDataSources 匹配恢复）
    /// - 展示层：SourceNodeName + DisplayName（UI 显示文本，用于 ImageSourceSelector 显示）
    /// 
    /// 不包含运行时数据（CurrentValue、PropertyType、TreeNode 等），这些在加载后从 AvailableDataSources 恢复。
    /// 
    /// 设计原因：AvailableDataSource 包含运行时数据（Mat 图像、TreeNode 等）无法序列化，
    /// 但用户选择的图像源需要在保存/加载后保持不变。
    /// ImageSourceReference 作为可序列化的中间层解决这个问题。
    /// </remarks>
    public class ImageSourceReference
    {
        /// <summary>
        /// 源节点ID（身份层）
        /// </summary>
        public string SourceNodeId { get; set; } = string.Empty;

        /// <summary>
        /// 源属性名称（身份层）
        /// </summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>
        /// 源节点显示名称（展示层）
        /// </summary>
        public string SourceNodeName { get; set; } = string.Empty;

        /// <summary>
        /// 源属性显示名称（展示层）
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 无参构造函数（序列化需要）
        /// </summary>
        public ImageSourceReference() { }

        /// <summary>
        /// 构造函数
        /// </summary>
        public ImageSourceReference(string sourceNodeId, string propertyName, string sourceNodeName, string displayName)
        {
            SourceNodeId = sourceNodeId;
            PropertyName = propertyName;
            SourceNodeName = sourceNodeName;
            DisplayName = displayName;
        }

        /// <summary>
        /// 从 AvailableDataSource 创建引用
        /// </summary>
        /// <param name="dataSource">完整的数据源对象</param>
        /// <returns>可序列化的引用对象，如果 dataSource 为 null 则返回 null</returns>
        public static ImageSourceReference? FromDataSource(AvailableDataSource? dataSource)
        {
            if (dataSource == null) return null;
            return new ImageSourceReference(
                dataSource.SourceNodeId,
                dataSource.PropertyName,
                dataSource.SourceNodeName,
                dataSource.DisplayName);
        }

        /// <summary>
        /// 从 AvailableDataSources 列表中匹配恢复完整的 AvailableDataSource
        /// </summary>
        /// <param name="dataSources">当前可用的数据源列表</param>
        /// <returns>匹配到的数据源，未找到则返回 null</returns>
        public AvailableDataSource? MatchDataSource(System.Collections.Generic.IEnumerable<AvailableDataSource>? dataSources)
        {
            if (dataSources == null) return null;
            return dataSources.FirstOrDefault(ds =>
                ds.SourceNodeId == SourceNodeId &&
                ds.PropertyName == PropertyName);
        }

        /// <summary>
        /// 显示文本（与 AvailableDataSource.DropdownDisplayText 格式一致）
        /// </summary>
        public string DropdownDisplayText => $"{SourceNodeName}.{DisplayName}";
    }
}
