using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SunEyeVision.Core.Services.Serialization
{
    /// <summary>
    /// JSON序列化选项统一配置
    /// </summary>
    /// <remarks>
    /// 提供统一的JsonSerializerOptions配置，确保全项目序列化行为一致。
    /// 遵循以下原则：
    /// 1. Single Source of Truth: 所有配置集中管理
    /// 2. DRY: 避免重复配置
    /// 3. 一致性: 序列化和反序列化使用相同配置
    /// 
    /// 重要变更（rule-008: 原型设计期代码纯净原则）：
    /// - 优先使用 ParameterTypeRegistry.SerializationOptions
    /// - 该选项包含自动配置的多态序列化支持
    /// - 无需手动配置 JsonPolymorphic 特性
    /// </remarks>
    public static class JsonSerializationOptions
    {
        private static JsonSerializerOptions? _compact;
        private static JsonSerializerOptions? _compactSource;
        private static readonly object _compactLock = new();

        /// <summary>
        /// 默认选项：用于大多数场景（包含多态序列化支持）
        /// </summary>
        /// <remarks>
        /// 配置：
        /// - WriteIndented = true: 格式化输出，便于人工阅读
        /// - PropertyNamingPolicy = null: 属性名使用PascalCase（符合视觉软件行业标准）
        /// - DefaultIgnoreCondition = WhenWritingNull: 忽略null值
        /// - Encoder = UnsafeRelaxedJsonEscaping: 放宽转义规则，支持中文
        /// - PropertyNameCaseInsensitive = true: 反序列化时属性名大小写不敏感
        /// - 自动多态序列化：通过 ParameterTypeRegistry 自动注册所有 ToolParameters 派生类型
        /// </remarks>
        public static JsonSerializerOptions Default => ParameterTypeRegistry.SerializationOptions;

        /// <summary>
        /// 紧凑选项：用于网络传输或存储空间敏感场景（包含多态序列化支持）
        /// </summary>
        /// <remarks>
        /// 配置：
        /// - WriteIndented = false: 紧凑输出，减少文件大小
        /// - PropertyNamingPolicy = null: 属性名使用PascalCase（符合视觉软件行业标准）
        /// - DefaultIgnoreCondition = WhenWritingNull: 忽略null值
        /// - Encoder = UnsafeRelaxedJsonEscaping: 放宽转义规则，支持中文
        /// - PropertyNameCaseInsensitive = true: 反序列化时属性名大小写不敏感
        /// - 自动多态序列化：通过 ParameterTypeRegistry 自动注册所有 ToolParameters 派生类型
        /// 
        /// 注意：TypeInfoResolver 必须手动设置，因为复制构造函数不会复制它
        /// </remarks>
        public static JsonSerializerOptions Compact
        {
            get
            {
                var source=ParameterTypeRegistry.SerializationOptions;
                lock(_compactLock)
                {
                    if(!ReferenceEquals(_compactSource,source))
                    {
                        _compact=new JsonSerializerOptions(source){WriteIndented=false};
                        _compactSource=source;
                    }
                    return _compact!;
                }
            }
        }
    }
}
