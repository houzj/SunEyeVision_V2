using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Core.Services.Serialization
{
    /// <summary>
    /// 参数类型注册中心
    /// </summary>
    /// <remarks>
    /// 多态序列化配置 - 动态注册插件派生类型。
    /// 
    /// 设计原则（rule-008: 原型设计期代码纯净原则）：
    /// - 不考虑向后兼容，直接使用最优方案
    /// 
    /// 设计原则（rule-010: 方案系统实现规范）：
    /// - 优先使用 System.Text.Json 原生多态序列化
    /// - 使用 DefaultJsonTypeInfoResolver 动态注册派生类型
    /// - 派生类的 [JsonDerivedType] 特性作为元数据被扫描注册
    /// 
    /// 设计原则（rule-012: 参数系统约束条件）：
    /// - 保持工具注册机制不变
    /// - 修复参数类型注册逻辑
    /// 
    /// 双层机制说明：
    /// 1. 基类特性（ToolParameters）：
    ///    - [JsonPolymorphic] 提供多态配置入口（必需）
    ///    - [JsonDerivedType(typeof(GenericToolParameters), "Generic")] 注册已知派生类
    /// 
    /// 2. 动态注册（此类实现）：
    ///    - 使用 DefaultJsonTypeInfoResolver + 修饰器模式
    ///    - 在运行时扫描并注册插件派生类型
    ///    - 支持插件架构（基类编译时不知道插件派生类）
    /// 
    /// 主动注册模式：
    /// - ToolRegistry.RegisterTool 触发 ParameterTypeRegistry.RegisterParameterType
    /// - 确保在反序列化之前，所有参数类型已注册到多态配置中
    /// - 避免修饰器延迟执行导致的初始化时序问题
    /// 
    /// 修复记录（2026-03-26）：
    /// - 问题：WithAddedModifier() 返回 IJsonTypeInfoResolver 而非 DefaultJsonTypeInfoResolver
    /// - 修复：将 _typeResolver 类型改为 IJsonTypeInfoResolver，移除强制转换
    /// </remarks>
    public static class ParameterTypeRegistry
    {
        private static JsonSerializerOptions? _serializationOptions;
        private static JsonPolymorphismOptions? _polymorphismOptions;
        
        /// <summary>
        /// 类型解析器（使用接口类型，因为 WithAddedModifier 返回 IJsonTypeInfoResolver）
        /// </summary>
        private static IJsonTypeInfoResolver? _typeResolver;
        
        private static bool _isInitialized = false;
        private static readonly object _lock = new();
        
        // 已注册的类型集合（用于去重）
        private static readonly HashSet<Type> _registeredTypes = new();

        /// <summary>
        /// 序列化选项（包含多态配置）
        /// </summary>
        public static JsonSerializerOptions SerializationOptions
        {
            get
            {
                EnsureInitialized();
                return _serializationOptions!;
            }
        }

        /// <summary>
        /// 确保类型注册已完成
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_isInitialized && _serializationOptions != null)
            {
                return;
            }

            lock (_lock)
            {
                if (_isInitialized && _serializationOptions != null)
                {
                    return;
                }

                try
                {
                    InitializeSerializationOptions();
                    _isInitialized = true;
                    
                    VisionLogger.Instance.Log(LogLevel.Success,
                        $"参数类型注册系统初始化完成，已注册类型数: {_registeredTypes.Count}",
                        "ParameterTypeRegistry");
                }
                catch (Exception ex)
                {
                    VisionLogger.Instance.Log(LogLevel.Fatal,
                        $"参数类型注册系统初始化失败: {ex.GetType().Name} - {ex.Message}",
                        "ParameterTypeRegistry", ex);

                    throw new InvalidOperationException(
                        $"ParameterTypeRegistry 初始化失败，无法继续。原因: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 初始化序列化选项（创建空的多态配置）
        /// </summary>
        private static void InitializeSerializationOptions()
        {
            // 创建多态配置（初始只包含 GenericToolParameters）
            _polymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "$type",
                IgnoreUnrecognizedTypeDiscriminators = false
            };

            // 注册 GenericToolParameters 作为默认回退类型
            _polymorphismOptions.DerivedTypes.Add(
                new JsonDerivedType(typeof(GenericToolParameters), "Generic"));
            _registeredTypes.Add(typeof(GenericToolParameters));

            // 创建类型解析器
            var defaultResolver = new DefaultJsonTypeInfoResolver();

            // 添加类型修饰器，将多态配置应用到 ToolParameters 类型
            _typeResolver = defaultResolver.WithAddedModifier(ApplyPolymorphismOptions);

            _serializationOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null,  // PascalCase（符合视觉软件行业标准）
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNameCaseInsensitive = true,
                IncludeFields = true,
                TypeInfoResolver = _typeResolver
            };
        }

        /// <summary>
        /// 将多态配置应用到 ToolParameters 类型
        /// </summary>
        private static void ApplyPolymorphismOptions(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Type != typeof(ToolParameters))
                return;

            // JsonPolymorphismOptions belongs to exactly one JsonTypeInfo. Workflow
            // copies the serializer options, so each resolver call needs its own
            // metadata rather than assigning the registration catalog itself.
            lock (_lock)
            {
                var catalog = _polymorphismOptions!;
                var snapshot = new JsonPolymorphismOptions
                {
                    TypeDiscriminatorPropertyName = catalog.TypeDiscriminatorPropertyName,
                    IgnoreUnrecognizedTypeDiscriminators = catalog.IgnoreUnrecognizedTypeDiscriminators,
                    UnknownDerivedTypeHandling = catalog.UnknownDerivedTypeHandling
                };
                foreach (var derived in catalog.DerivedTypes) snapshot.DerivedTypes.Add(derived);
                typeInfo.PolymorphismOptions = snapshot;
            }
        }

        /// <summary>
        /// 注册参数类型（由 ToolRegistry.RegisterTool 调用）
        /// </summary>
        /// <param name="parameterType">参数类型（ToolParameters 的派生类）</param>
        /// <param name="typeDiscriminator">类型鉴别器（从 [JsonDerivedType] 特性提取）</param>
        public static void RegisterParameterType(Type parameterType, string typeDiscriminator)
        {
            if (parameterType == null)
            {
                VisionLogger.Instance.Log(LogLevel.Warning,
                    "参数类型注册失败: 类型为 null",
                    "ParameterTypeRegistry");
                return;
            }

            if (!typeof(ToolParameters).IsAssignableFrom(parameterType))
            {
                VisionLogger.Instance.Log(LogLevel.Warning,
                    $"参数类型注册失败: {parameterType.Name} 不是 ToolParameters 的派生类",
                    "ParameterTypeRegistry");
                return;
            }

            if (parameterType == typeof(GenericToolParameters))
            {
                // GenericToolParameters 已在初始化时注册
                return;
            }

            EnsureInitialized();

            lock (_lock)
            {
                // 检查是否已注册
                if (_registeredTypes.Contains(parameterType))
                {
                    return;
                }

                // 添加到多态配置
                _polymorphismOptions!.DerivedTypes.Add(
                    new JsonDerivedType(parameterType, typeDiscriminator));

                // 记录已注册
                _registeredTypes.Add(parameterType);

                VisionLogger.Instance.Log(LogLevel.Debug,
                    $"参数类型已注册: {parameterType.Name} → '{typeDiscriminator}'",
                    "ParameterTypeRegistry");
            }
        }

        /// <summary>
        /// 批量注册参数类型（从程序集扫描）
        /// </summary>
        /// <remarks>
        /// 用于兼容旧代码，支持从程序集扫描注册所有派生类型。
        /// 但推荐使用主动注册模式（RegisterParameterType）。
        /// </remarks>
        public static void ScanAndRegisterFromCurrentDomain()
        {
            EnsureInitialized();

            int registeredCount = 0;
            int scannedAssemblyCount = 0;
            int errorAssemblyCount = 0;

            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            VisionLogger.Instance.Log(LogLevel.Info,
                $"开始扫描程序集: 共 {allAssemblies.Length} 个程序集",
                "ParameterTypeRegistry");

            foreach (var assembly in allAssemblies)
            {
                scannedAssemblyCount++;
                var assemblyName = assembly.GetName().Name ?? "Unknown";

                try
                {
                    var allTypes = assembly.GetTypes();
                    var derivedTypes = allTypes
                        .Where(t => t.IsClass && !t.IsAbstract)
                        .Where(t => typeof(ToolParameters).IsAssignableFrom(t))
                        .Where(t => t != typeof(GenericToolParameters))
                        .ToList();

                    if (derivedTypes.Any())
                    {
                        VisionLogger.Instance.Log(LogLevel.Info,
                            $"程序集 [{assemblyName}] 包含 {derivedTypes.Count} 个 ToolParameters 派生类",
                            "ParameterTypeRegistry");
                    }

                    foreach (var derivedType in derivedTypes)
                    {
                        // 从 [JsonDerivedType] 特性获取类型标识符
                        var jsonDerivedAttr = derivedType.GetCustomAttribute<JsonDerivedTypeAttribute>();
                        if (jsonDerivedAttr != null)
                        {
                            string typeDiscriminator = jsonDerivedAttr.TypeDiscriminator?.ToString() ?? derivedType.Name;
                            RegisterParameterType(derivedType, typeDiscriminator);
                            registeredCount++;
                        }
                        else
                        {
                            VisionLogger.Instance.Log(LogLevel.Warning,
                                $"派生类 {derivedType.FullName} 缺少 [JsonDerivedType] 特性，无法注册",
                                "ParameterTypeRegistry");
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    errorAssemblyCount++;
                    var loaderErrors = string.Join(", ", ex.LoaderExceptions.Select(e => e?.Message));
                    VisionLogger.Instance.Log(LogLevel.Warning,
                        $"程序集 [{assemblyName}] 类型加载失败: {loaderErrors}",
                        "ParameterTypeRegistry");
                }
                catch (Exception ex)
                {
                    errorAssemblyCount++;
                    VisionLogger.Instance.Log(LogLevel.Warning,
                        $"程序集 [{assemblyName}] 扫描失败: {ex.Message}",
                        "ParameterTypeRegistry");
                }
            }

            VisionLogger.Instance.Log(LogLevel.Info,
                $"程序集扫描完成: 扫描 {scannedAssemblyCount} 个，错误 {errorAssemblyCount} 个，注册 {registeredCount} 个类型",
                "ParameterTypeRegistry");
        }

        /// <summary>
        /// 获取已注册的类型数量
        /// </summary>
        public static int GetRegisteredTypeCount()
        {
            lock (_lock)
            {
                return _registeredTypes.Count;
            }
        }

        /// <summary>
        /// 获取诊断信息（用于验证修复有效性）
        /// </summary>
        public static string GetDiagnosticInfo()
        {
            lock (_lock)
            {
                var derivedTypesList = _polymorphismOptions != null
                    ? string.Join(", ", _polymorphismOptions.DerivedTypes.Select(dt => 
                        $"{dt.DerivedType.Name}({dt.TypeDiscriminator})"))
                    : "null";

                return $"[ParameterTypeRegistry 诊断报告]\n" +
                       $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                       $"初始化状态: {(_isInitialized ? "✅ 已初始化" : "❌ 未初始化")}\n" +
                       $"序列化选项: {(_serializationOptions != null ? "✅ 已创建" : "❌ 未创建")}\n" +
                       $"类型解析器: {(_typeResolver != null ? $"✅ {_typeResolver.GetType().Name}" : "❌ 未创建")}\n" +
                       $"多态配置: {(_polymorphismOptions != null ? "✅ 已创建" : "❌ 未创建")}\n" +
                       $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                       $"已注册类型数: {_registeredTypes.Count}\n" +
                       $"派生类型列表: {derivedTypesList}\n" +
                       $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
            }
        }

        /// <summary>
        /// 清除所有注册的类型（用于测试）
        /// </summary>
        public static void ClearRegistrations()
        {
            lock (_lock)
            {
                _polymorphismOptions?.DerivedTypes.Clear();
                _registeredTypes.Clear();

                // 重新注册 GenericToolParameters
                if (_polymorphismOptions != null)
                {
                    _polymorphismOptions.DerivedTypes.Add(
                        new JsonDerivedType(typeof(GenericToolParameters), "Generic"));
                    _registeredTypes.Add(typeof(GenericToolParameters));
                }

                VisionLogger.Instance.Log(LogLevel.Info,
                    "参数类型注册已清除",
                    "ParameterTypeRegistry");
            }
        }

    }
}
