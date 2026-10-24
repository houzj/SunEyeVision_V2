using System;
using System.Text.Json;
using SunEyeVision.Core.Services.Serialization;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// Workflow 层的序列化配置
    /// </summary>
    /// <remarks>
    /// 职责：管理 Workflow 层的 JSON 序列化配置
    /// 
    /// 设计原则：
    /// 1. 单一职责：仅管理 Workflow 层的序列化配置
    /// 2. 依赖正确：依赖 Core 层的基础配置，不修改 Core 层
    /// 3. 线程安全：使用锁和静态字段缓存，避免并发问题
    /// 4. 性能优化：配置对象复用，避免重复创建
    /// 
    /// 架构说明：
    /// - 继承 Core 层的基础配置（PascalCase、UnsafeRelaxedJsonEscaping 等）
    /// - 添加 Workflow 层特有的转换器（WorkflowJsonConverter）
    /// - Workflow 层代码使用此配置，不使用 Core 层的配置
    /// </remarks>
    public static class WorkflowSerializationOptions
    {
        private static JsonSerializerOptions? _default;
        private static JsonSerializerOptions? _defaultSource;
        private static readonly object _lock = new object();

        /// <summary>
        /// 默认选项：包含 Workflow 层的转换器
        /// </summary>
        /// <remarks>
        /// 配置继承自 Core.JsonSerializationOptions.Default：
        /// - WriteIndented = true: 格式化输出
        /// - PropertyNamingPolicy = null: PascalCase（符合视觉软件行业标准）
        /// - DefaultIgnoreCondition = WhenWritingNull: 忽略 null 值
        /// - Encoder = UnsafeRelaxedJsonEscaping: 支持中文
        /// - PropertyNameCaseInsensitive = true: 反序列化时大小写不敏感
        /// 
        /// 额外配置：
        /// - WorkflowJsonConverter: Workflow 对象的序列化/反序列化
        /// </remarks>
        public static JsonSerializerOptions Default
        {
            get
            {
                var source=ParameterTypeRegistry.SerializationOptions;
                lock(_lock)
                {
                    if(!ReferenceEquals(_defaultSource,source))
                    {
                        var options=new JsonSerializerOptions(source){WriteIndented=true};
                        options.Converters.Add(new WorkflowJsonConverter());
                        _default=options;_defaultSource=source;
                    }
                    return _default!;
                }
            }
        }

        private static JsonSerializerOptions? _compact;
        private static JsonSerializerOptions? _compactSource;
        private static readonly object _compactLock = new object();

        /// <summary>
        /// 紧凑选项：用于需要紧凑输出的场景
        /// </summary>
        /// <remarks>
        /// 配置继承自 Core.JsonSerializationOptions.Compact：
        /// - WriteIndented = false: 紧凑输出，减少文件大小
        /// - 其他配置与 Default 相同
        /// 
        /// 额外配置：
        /// - WorkflowJsonConverter: Workflow 对象的序列化/反序列化
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
                        var options=new JsonSerializerOptions(source){WriteIndented=false};
                        options.Converters.Add(new WorkflowJsonConverter());
                        _compact=options;_compactSource=source;
                    }
                    return _compact!;
                }
            }
        }
    }
}
