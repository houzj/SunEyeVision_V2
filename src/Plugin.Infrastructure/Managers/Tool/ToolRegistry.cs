using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Core.Services.Serialization;

namespace SunEyeVision.Plugin.Infrastructure.Managers.Tool
{
    /// <summary>
    /// 工具注册中心 - 管理所有已注册的工具
    /// </summary>
    /// <remarks>
    /// 优化后的设计：
    /// - 只管理工具类型字典（简化为1个字典）
    /// - 元数据按需创建（延迟创建，减少内存占用）
    /// - 从 [Tool] 特性自动提取基本信息
    /// </remarks>
    public static class ToolRegistry
    {
        private static readonly Dictionary<string, Type> _toolTypes = new();
        private static readonly Dictionary<string, ToolMetadata> _metadataCache = new();
        private static readonly object _lock = new();

        // ========================================
        // 缓存结构：基于具体类型的缓存池（优化方案）
        // ========================================
        
        /// <summary>
        /// 类型缓存池（基于具体类型）
        /// </summary>
        /// <remarks>
        /// 优化方案：按具体类型缓存，无需分类
        /// - Point[] → 缓存到 "OpenCvSharp.Point[]"
        /// - Mat     → 缓存到 "OpenCvSharp.Mat"
        /// - double[] → 缓存到 "System.Double[]"
        /// 
        /// 优点：
        /// 1. 查询效率高（直接索引，无需遍历）
        /// 2. 类型精确匹配（不会误判）
        /// 3. 符合视觉软件特点（OpenCvSharp API 主要使用数组）
        /// </remarks>
        private static readonly Dictionary<string, Dictionary<string, List<ToolPropertyMetadata>>> _typeCache = new();

        /// <summary>
        /// 注册工具类型（从 [Tool] 特性提取元数据）
        /// </summary>
        /// <param name="toolType">工具类型</param>
        public static void RegisterTool(Type toolType)
        {
            if (toolType == null)
                throw new ArgumentNullException(nameof(toolType));

            var attr = toolType.GetCustomAttribute<ToolAttribute>();
            if (attr == null)
                throw new ArgumentException($"类型 {toolType.Name} 缺少 [Tool] 特性");

            lock (_lock)
            {
                _toolTypes[attr.Id] = toolType;
                
                // 提取参数类型信息（用于诊断）
                Type? paramsType = null;
                Type? resultType = null;
                foreach (var iface in toolType.GetInterfaces())
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IToolPlugin<,>))
                    {
                        var genericArgs = iface.GetGenericArguments();
                        if (genericArgs.Length >= 2)
                        {
                            paramsType = genericArgs[0];
                            resultType = genericArgs[1];
                            break;
                        }
                    }
                }
                
                // 主动注册参数类型到多态序列化配置
                if (paramsType != null && typeof(ToolParameters).IsAssignableFrom(paramsType))
                {
                    RegisterParameterTypeToRegistry(paramsType);
                }
                
                // ✅ 提取并缓存属性元数据
                ExtractPropertyMetadata(attr.Id, resultType);
                
                VisionLogger.Instance.Log(LogLevel.Success,
                    $"工具注册成功: {attr.DisplayName} (Id={attr.Id}, ParamsType={paramsType?.Name ?? "未指定"}, ResultType={resultType?.Name ?? "未指定"})",
                    "ToolRegistry");
            }
        }

        /// <summary>
        /// 注册参数类型到 ParameterTypeRegistry
        /// </summary>
        /// <param name="paramsType">参数类型</param>
        private static void RegisterParameterTypeToRegistry(Type paramsType)
        {
            try
            {
                // 从 [JsonDerivedType] 特性提取类型鉴别器
                var jsonDerivedAttr = paramsType.GetCustomAttribute<JsonDerivedTypeAttribute>();
                string typeDiscriminator = jsonDerivedAttr?.TypeDiscriminator?.ToString() ?? paramsType.Name;
                
                ParameterTypeRegistry.RegisterParameterType(paramsType, typeDiscriminator);
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Warning,
                    $"参数类型注册失败: {paramsType.Name}, 错误: {ex.Message}",
                    "ToolRegistry");
            }
        }

        /// <summary>
        /// 获取工具元数据（带缓存）
        /// </summary>
        /// <param name="toolId">工具ID</param>
        /// <returns>工具元数据，未找到则返回null</returns>
        public static ToolMetadata? GetToolMetadata(string toolId)
        {
            lock (_lock)
            {
                if (!_toolTypes.TryGetValue(toolId, out var toolType))
                    return null;

                var attr = toolType.GetCustomAttribute<ToolAttribute>();
                if (attr == null)
                    return null;

                if (_metadataCache.TryGetValue(toolId, out var cachedMetadata))
                    return cachedMetadata;

                var metadata = ToolMetadata.FromToolType(
                    toolType,
                    attr.Id,
                    attr.DisplayName,
                    attr.Description,
                    attr.Icon,
                    attr.Category
                );

                // 提取 DebugWindowStyle 属性
                metadata.DebugWindowStyle = attr.DebugWindowStyle;

                _metadataCache[toolId] = metadata;
                return metadata;
            }
        }

        /// <summary>
        /// 获取所有工具元数据（带缓存）
        /// </summary>
        /// <returns>所有工具元数据列表</returns>
        public static List<ToolMetadata> GetAllToolMetadata()
        {
            lock (_lock)
            {
                return _toolTypes.Values.Select(toolType =>
                {
                    var attr = toolType.GetCustomAttribute<ToolAttribute>();
                    if (attr == null)
                        return null;

                    var toolId = attr.Id;
                    if (_metadataCache.TryGetValue(toolId, out var cachedMetadata))
                        return cachedMetadata;

                    var metadata = ToolMetadata.FromToolType(
                        toolType,
                        attr.Id,
                        attr.DisplayName,
                        attr.Description,
                        attr.Icon,
                        attr.Category
                    );

                    _metadataCache[toolId] = metadata;
                    return metadata;
                }).Where(m => m != null).ToList()!;
            }
        }

        /// <summary>
        /// 获取指定分类的工具元数据
        /// </summary>
        /// <param name="category">分类名称</param>
        /// <returns>该分类下的工具元数据列表</returns>
        public static List<ToolMetadata> GetToolsByCategory(string category)
        {
            return GetAllToolMetadata().Where(m => m.Category == category).ToList();
        }

        /// <summary>
        /// 创建工具实例
        /// </summary>
        /// <param name="toolId">工具ID</param>
        /// <returns>工具实例，未找到则返回null</returns>
        public static IToolPlugin? CreateToolInstance(string toolId)
        {
            lock (_lock)
            {
                if (_toolTypes.TryGetValue(toolId, out var type))
                {
                    return Activator.CreateInstance(type) as IToolPlugin;
                }
                return null;
            }
        }

        /// <summary>
        /// 创建参数实例
        /// </summary>
        /// <param name="toolId">工具ID</param>
        /// <returns>参数实例</returns>
        public static ToolParameters CreateParameters(string toolId)
        {
            VisionLogger.Instance.Log(LogLevel.Info, $"[ToolRegistry.CreateParameters] 开始创建参数实例 - toolId={toolId}", "ToolRegistry");

            var tool = CreateToolInstance(toolId);
            if (tool == null)
                throw new InvalidOperationException($"工具实例创建失败: {toolId}");

            if (tool.ParamsType == null)
                throw new InvalidOperationException($"工具 {toolId} 的参数类型为null，请检查工具注册");

            try
            {
                var paramsInstance = (ToolParameters)Activator.CreateInstance(tool.ParamsType)!;
                VisionLogger.Instance.Log(LogLevel.Success, $"[ToolRegistry.CreateParameters] 参数创建成功 - 类型={paramsInstance.GetType().FullName}", "ToolRegistry");
                return paramsInstance;
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Fatal, $"❌ [ToolRegistry.CreateParameters] 参数创建失败 - toolId={toolId}, ParamsType={tool.ParamsType.FullName}, 错误={ex.Message}", "ToolRegistry", ex);
                throw new InvalidOperationException($"参数创建失败: toolId={toolId}, ParamsType={tool.ParamsType.FullName}", ex);
            }
        }

        /// <summary>
        /// 创建调试窗口
        /// </summary>
        /// <param name="toolId">工具ID</param>
        /// <returns>调试窗口实例</returns>
        public static System.Windows.Window? CreateDebugWindow(string toolId)
        {
            var tool = CreateToolInstance(toolId);
            return tool?.HasDebugWindow == true ? tool.CreateDebugWindow() : null;
        }

        /// <summary>
        /// 检查工具是否存在
        /// </summary>
        /// <param name="toolId">工具ID</param>
        /// <returns>是否存在</returns>
        public static bool ToolExists(string toolId)
        {
            lock (_lock)
            {
                return _toolTypes.ContainsKey(toolId);
            }
        }

        /// <summary>
        /// 注销工具
        /// </summary>
        /// <param name="toolId">工具ID</param>
        /// <returns>是否注销成功</returns>
        public static bool UnregisterTool(string toolId)
        {
            lock (_lock)
            {
                var removed = _toolTypes.Remove(toolId);
                _metadataCache.Remove(toolId);
                ClearToolCache(toolId);
                return removed;
            }
        }
        
        // ========================================
        // 缓存清理方法
        // ========================================
        
        /// <summary>
        /// 清除所有缓存池
        /// </summary>
        public static void ClearAllCaches()
        {
            _typeCache.Clear();
            
            VisionLogger.Instance.Log(LogLevel.Success, 
                "✅ 清除所有属性元数据缓存池", 
                "ToolRegistry");
        }
        
        /// <summary>
        /// 清除指定工具的缓存
        /// </summary>
        public static void ClearToolCache(string toolId)
        {
            foreach (var typeCache in _typeCache.Values)
            {
                typeCache.Remove(toolId);
            }
            
            VisionLogger.Instance.Log(LogLevel.Success, 
                $"✅ 清除工具缓存: {toolId}", 
                "ToolRegistry");
        }
        
        // ========================================
        // 按类型查询方法（优化方案）
        // ========================================
        
        /// <summary>
        /// 按具体类型获取属性元数据
        /// </summary>
        /// <param name="toolId">工具ID</param>
        /// <param name="propertyType">属性类型（如 typeof(Point[]), typeof(Mat) 等）</param>
        /// <returns>属性元数据列表</returns>
        /// <remarks>
        /// 示例：
        /// <code>
        /// // 查询 Point[] 类型的属性
        /// var props = ToolRegistry.GetMetadataByType("contour", typeof(Point[]));
        /// // 返回：[ { PropertyName: "Contours", PropertyType: typeof(Point[]) } ]
        /// </code>
        /// </remarks>
        public static List<ToolPropertyMetadata> GetMetadataByType(
            string toolId, 
            Type propertyType)
        {
            if (propertyType == null)
                return new List<ToolPropertyMetadata>();
            
            var typeFullName = propertyType.FullName;
            if (string.IsNullOrEmpty(typeFullName))
                return new List<ToolPropertyMetadata>();
            
            if (_typeCache.TryGetValue(typeFullName, out var toolCache) &&
                toolCache.TryGetValue(toolId, out var properties))
            {
                return properties;
            }
            
            return new List<ToolPropertyMetadata>();
        }

        /// <summary>
        /// 获取指定类型的所有工具属性元数据
        /// </summary>
        /// <param name="propertyType">属性类型</param>
        /// <returns>所有工具的该类型属性列表</returns>
        public static List<ToolPropertyMetadata> GetMetadataByType(Type propertyType)
        {
            if (propertyType == null)
                return new List<ToolPropertyMetadata>();
            
            var typeFullName = propertyType.FullName;
            if (string.IsNullOrEmpty(typeFullName))
                return new List<ToolPropertyMetadata>();
            
            if (_typeCache.TryGetValue(typeFullName, out var toolCache))
            {
                var allProperties = new List<ToolPropertyMetadata>();
                foreach (var properties in toolCache.Values)
                {
                    allProperties.AddRange(properties);
                }
                return allProperties;
            }
            
            return new List<ToolPropertyMetadata>();
        }

        /// <summary>
        /// 清除所有已注册的工具和缓存
        /// </summary>
        public static void ClearAll()
        {
            lock (_lock)
            {
                _toolTypes.Clear();
                _metadataCache.Clear();
                ClearAllCaches();
            }
        }

        /// <summary>
        /// 获取已注册的工具数量
        /// </summary>
        /// <returns>工具数量</returns>
        public static int GetToolCount()
        {
            lock (_lock)
            {
                return _toolTypes.Count;
            }
        }

        /// <summary>
        /// 获取所有分类
        /// </summary>
        /// <returns>分类列表</returns>
        public static List<string> GetAllCategories()
        {
            return GetAllToolMetadata()
                .Select(m => m.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        /// <summary>
        /// 提取属性元数据到类型缓存（优化方案）
        /// </summary>
        /// <param name="toolId">工具ID</param>
        /// <param name="resultType">结果类型</param>
        private static void ExtractPropertyMetadata(string toolId, Type? resultType)
        {
            if (resultType == null || !typeof(ToolResults).IsAssignableFrom(resultType))
            {
                return;
            }

            try
            {
                // 创建临时空实例（仅用于提取元数据）
                ToolResults? tempResult;
                try
                {
                    tempResult = Activator.CreateInstance(resultType) as ToolResults;
                }
                catch (Exception ex)
                {
                    VisionLogger.Instance.Log(LogLevel.Warning, 
                        $"创建临时实例失败: {toolId}, 错误: {ex.Message}", 
                        "ToolRegistry");
                    return;
                }
                
                if (tempResult == null)
                {
                    return;
                }
                
                // 遍历所有公共属性
                var properties = resultType.GetProperties(
                    BindingFlags.Public |
                    BindingFlags.Instance);
                
                // 一次性获取属性映射表（性能优化）
                var propertyMap = tempResult.GetPropertyMap();
                
                int validPropertyCount = 0;
                
                foreach (var prop in properties)
                {
                    // 跳过索引属性
                    if (prop.GetIndexParameters().Length > 0)
                        continue;
                    
                    // 跳过基类 ToolResults 的属性（执行耗时 ExecutionTimeMs 除外，对所有工具可见）
                    if (prop.DeclaringType == typeof(ToolResults))
                    {
                        if (prop.Name == "ExecutionTimeMs")
                        {
                            // 执行耗时所有工具通用，保留用于性能监控
                            // 因工具可能重写 GetPropertyMap() 而不包含基类属性，此处兜底设置中文显示名
                        }
                        else
                        {
                            continue;
                        }
                    }
                    
                    // 跳过特殊属性
                    if (IsSpecialProperty(prop.Name))
                        continue;
                    
                    // 从属性映射表获取 DisplayName 和 GroupName
                    var mapEntry = propertyMap.FirstOrDefault(e => e.PropertyName == prop.Name);
                    
                    // 执行耗时统一使用中文显示名（即使工具的 GetPropertyMap() 未包含它也生效）
                    var displayName = prop.Name == "ExecutionTimeMs"
                        ? "执行耗时"
                        : mapEntry?.DisplayName ?? GetDisplayName(prop.Name);
                    var groupName = prop.Name == "ExecutionTimeMs"
                        ? "执行信息"
                        : mapEntry?.GroupName;
                    
                    // 创建元数据
                    var metadata = new ToolPropertyMetadata
                    {
                        PropertyName = prop.Name,
                        DisplayName = displayName,
                        GroupName = groupName,
                        PropertyType = prop.PropertyType,
                        Description = GetPropertyDescription(prop),
                    };
                    
                    // 获取类型全名（作为缓存Key）
                    var typeFullName = prop.PropertyType.FullName;
                    if (string.IsNullOrEmpty(typeFullName))
                    {
                        continue;
                    }
                    
                    // 添加到类型缓存（关键优化：直接按具体类型缓存）
                    if (!_typeCache.ContainsKey(typeFullName))
                    {
                        _typeCache[typeFullName] = new Dictionary<string, List<ToolPropertyMetadata>>();
                    }
                    
                    if (!_typeCache[typeFullName].ContainsKey(toolId))
                    {
                        _typeCache[typeFullName][toolId] = new List<ToolPropertyMetadata>();
                    }
                    
                    _typeCache[typeFullName][toolId].Add(metadata);
                    
                    validPropertyCount++;
                    
                    VisionLogger.Instance.Log(LogLevel.Debug,
                        $"属性元数据已提取: {toolId}.{metadata.PropertyName} ({prop.PropertyType.Name})",
                        "ToolRegistry");
                }
                
                VisionLogger.Instance.Log(LogLevel.Success,
                    $"属性元数据提取完成: {toolId}, 共 {validPropertyCount} 个属性",
                    "ToolRegistry");
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Warning,
                    $"提取属性元数据失败: {toolId}, 错误: {ex.Message}",
                    "ToolRegistry");
            }
        }

        /// <summary>
        /// 获取属性元数据（从类型缓存）
        /// </summary>
        /// <param name="toolId">工具ID</param>
        /// <param name="propertyName">属性名</param>
        /// <returns>属性元数据，未找到返回 null</returns>
        public static ToolPropertyMetadata? GetPropertyMetadata(string toolId, string propertyName)
        {
            lock (_lock)
            {
                // 从所有类型缓存中查找属性
                foreach (var typeCache in _typeCache.Values)
                {
                    if (typeCache.TryGetValue(toolId, out var propertyList))
                    {
                        var metadata = propertyList.FirstOrDefault(m => m.PropertyName == propertyName);
                        if (metadata != null)
                        {
                            return metadata;
                        }
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// 获取所有属性元数据（合并所有类型）
        /// </summary>
        /// <param name="toolId">工具ID</param>
        /// <returns>属性元数据列表，未找到返回空列表</returns>
        public static List<ToolPropertyMetadata> GetAllPropertyMetadata(string toolId)
        {
            var allMetadata = new List<ToolPropertyMetadata>();

            foreach (var typeCache in _typeCache.Values)
            {
                if (typeCache.TryGetValue(toolId, out var properties))
                {
                    allMetadata.AddRange(properties);
                }
            }

            // ★ 诊断：查询结果为空时输出缓存状态
            if (allMetadata.Count == 0)
            {
                var registeredKeys = new HashSet<string>();
                foreach (var kvp in _typeCache)
                {
                    foreach (var key in kvp.Value.Keys)
                        registeredKeys.Add(key);
                }

                VisionLogger.Instance.Log(LogLevel.Warning,
                    $"[诊断:GetAllPropertyMetadata] toolId='{toolId}' 查询结果为空 | " +
                    $"_typeCache.Count={_typeCache.Count} | " +
                    $"已注册的 toolId 列表: [{string.Join(", ", registeredKeys.OrderBy(k => k))}]",
                    "ToolRegistry");
            }

            return allMetadata;
        }

        /// <summary>
        /// 判断是否为特殊属性
        /// </summary>
        private static bool IsSpecialProperty(string propertyName)
        {
            return propertyName == "Status" ||
                   propertyName == "ErrorMessage" ||
                   propertyName == "Timestamp" ||
                   propertyName == "ToolName" ||
                   propertyName == "ToolId";
        }

        /// <summary>
        /// 获取属性显示名称
        /// </summary>
        private static string GetDisplayName(string propertyName)
        {
            return propertyName;
        }

        /// <summary>
        /// 获取属性描述
        /// </summary>
        private static string? GetPropertyDescription(PropertyInfo prop)
        {
            // 可以从特性中读取描述
            return null;
        }
    }
}
