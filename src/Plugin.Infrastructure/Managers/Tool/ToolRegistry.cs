using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Core.Services.Serialization;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;

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
    public static partial class ToolRegistry
    {
        private static Dictionary<string, Type> _toolTypes = new();
        private static Dictionary<string, ToolMetadata> _metadataCache = new();
        private static readonly object _lock = new();
        private static long _registrationGeneration;
        public static SunEyeVision.Plugin.Infrastructure.Managers.Plugin.ToolDirectoryCatalog? DirectoryCatalog { get; private set; }
        private static Action<string>? _ensureDirectoryTool;
        public static void ConfigureDirectory(SunEyeVision.Plugin.Infrastructure.Managers.Plugin.ToolDirectoryCatalog catalog, Action<string> ensureLoaded)
        {
            lock(_lock) { DirectoryCatalog=catalog; _ensureDirectoryTool=ensureLoaded; }
        }
        private static void EnsureDirectoryTool(string id)
        {
            Action<string>? ensure;
            lock(_lock) { if(_toolTypes.ContainsKey(id))return; ensure=_ensureDirectoryTool; }
            ensure?.Invoke(id);
        }

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
        private static Dictionary<string, Dictionary<string, List<ToolPropertyMetadata>>> _typeCache = new();

        /// <summary>
        /// 注册工具类型（从 [Tool] 特性提取元数据）
        /// </summary>
        /// <param name="toolType">工具类型</param>
        public static void RegisterTool(Type toolType)
        {
            var prepared=PrepareRegistration(new[]{toolType});
            using var commit=prepared.EnterCommit();
            commit.Publish();
        }

        public static IPreparedToolRegistration PrepareRegistration(IReadOnlyList<Type> toolTypes,IReadOnlyList<ToolComponentRegistration>? components=null)
        {
            ArgumentNullException.ThrowIfNull(toolTypes);
            Dictionary<string,Type> types;
            Dictionary<string,ToolMetadata> metadata;
            Dictionary<string,Dictionary<string,List<ToolPropertyMetadata>>> properties;
            Dictionary<string,ToolComponentRegistration> runtimes,editors;
            long generation;
            lock(_lock)
            {
                generation=_registrationGeneration;
                runtimes=new(_runtimeComponents,StringComparer.Ordinal);editors=new(_editorComponents,StringComparer.Ordinal);
                types=new(_toolTypes);metadata=new(_metadataCache);
                properties=_typeCache.ToDictionary(item=>item.Key,item=>item.Value.ToDictionary(field=>field.Key,field=>field.Value.ToList()));
            }
            var candidates=new List<(Type Type,ToolAttribute Attribute)>();
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var type in toolTypes)
            {
                if(type is null||type.IsAbstract||!typeof(IToolPlugin).IsAssignableFrom(type))throw new ArgumentException("A concrete tool plugin type is required.");
                var attribute=type.GetCustomAttribute<ToolAttribute>()??throw new ArgumentException("The candidate has no Tool attribute.");
                if(string.IsNullOrWhiteSpace(attribute.Id)||!ids.Add(attribute.Id))throw new InvalidOperationException("Plugin assembly contains a missing or duplicate ToolId.");
                if(types.TryGetValue(attribute.Id,out var current))
                {
                    if(current!=type)throw new InvalidOperationException("ToolId is already owned by another type: "+attribute.Id);
                    continue;
                }
                candidates.Add((type,attribute));
            }
            var parameters=new List<KeyValuePair<Type,string>>();
            foreach(var candidate in candidates)
            {
                var plugin=(IToolPlugin)(Activator.CreateInstance(candidate.Type)??throw new InvalidOperationException("Tool construction returned null."));
                using var candidateLifetime=plugin as IDisposable;
                var parameterType=plugin.ParamsType;
                if(parameterType is null||parameterType.IsAbstract||!typeof(ToolParameters).IsAssignableFrom(parameterType))throw new InvalidOperationException("Invalid plugin parameter type.");
                foreach(var contract in candidate.Type.GetInterfaces().Where(type=>type.IsGenericType&&type.GetGenericTypeDefinition()==typeof(IToolPlugin<,>)))
                    if(contract.GetGenericArguments()[0]!=parameterType||contract.GetGenericArguments()[1]!=plugin.ResultType)throw new InvalidOperationException("Generic tool signature differs from its published parameter/result types.");
                if(plugin.ResultType is null||plugin.ResultType.IsAbstract||!typeof(ToolResults).IsAssignableFrom(plugin.ResultType))throw new InvalidOperationException("Invalid plugin result type.");
                var discriminator=parameterType==typeof(GenericToolParameters)?"Generic":parameterType.GetCustomAttribute<JsonDerivedTypeAttribute>()?.TypeDiscriminator?.ToString()??parameterType.Name;
                parameters.Add(new(parameterType,discriminator));
                var id=candidate.Attribute.Id;
                if(components is not null&&candidate.Attribute.RuntimeComponentId is { } required)
                {
                    var runtime=components.SingleOrDefault(c=>c.Metadata.ToolId==id&&c.Metadata.Role==SunEyeVision.Plugin.Contracts.ToolComponentRole.Runtime)
                        ?? throw new InvalidOperationException("Required runtime is missing: "+id);
                    if(runtime.Metadata.ComponentId!=required)throw new InvalidOperationException("Runtime component identity differs from the tool requirement.");
                    ValidateComponentEntry(runtime);runtimes.Add(id,runtime);
                    var editor=components.SingleOrDefault(c=>c.Metadata.ToolId==id&&c.Metadata.Role==SunEyeVision.Plugin.Contracts.ToolComponentRole.Editor);
                    if(editor is not null){ValidateComponentEntry(editor);editors.Add(id,editor);}
                }
                var fields=BuildPropertyMetadata(id,plugin);
                foreach(var field in fields)
                {
                    if(!properties.TryGetValue(field.Key,out var byTool)){byTool=new();properties.Add(field.Key,byTool);}
                    byTool.Add(id,field.Value);
                }
                var info=ToolMetadata.FromToolType(candidate.Type,id,candidate.Attribute.DisplayName,candidate.Attribute.Description,candidate.Attribute.Icon,candidate.Attribute.Category);
                info.DebugWindowStyle=candidate.Attribute.DebugWindowStyle;
                types.Add(id,candidate.Type);metadata.Add(id,info);
            }
            var parameterRegistration=ParameterTypeRegistry.PrepareRegistration(parameters);
            return new PreparedRegistration(generation,types,metadata,properties,parameterRegistration,candidates.Count>0,runtimes,editors);
        }

        private sealed class PreparedRegistration(long generation,Dictionary<string,Type> types,Dictionary<string,ToolMetadata> metadata,
            Dictionary<string,Dictionary<string,List<ToolPropertyMetadata>>> properties,ParameterTypeRegistry.PreparedRegistration parameters,bool changed,Dictionary<string,ToolComponentRegistration> runtimes,Dictionary<string,ToolComponentRegistration> editors) : IPreparedToolRegistration
        {
            public IToolCatalogCommitScope EnterCommit()=>new CommitScope(generation,types,metadata,properties,parameters,changed,runtimes,editors);
        }
        private sealed class CommitScope : IToolCatalogCommitScope
        {
            private readonly Dictionary<string,Type> _types;
            private readonly Dictionary<string,ToolMetadata> _metadata;
            private readonly Dictionary<string,Dictionary<string,List<ToolPropertyMetadata>>> _properties;
            private readonly ParameterTypeRegistry.CommitScope _parameters;
            private readonly bool _changed;
            private readonly Dictionary<string,ToolComponentRegistration> _runtimes,_editors;
            private bool _closed,_published;
            internal CommitScope(long generation,Dictionary<string,Type> types,Dictionary<string,ToolMetadata> metadata,
                Dictionary<string,Dictionary<string,List<ToolPropertyMetadata>>> properties,ParameterTypeRegistry.PreparedRegistration parameters,bool changed,Dictionary<string,ToolComponentRegistration> runtimes,Dictionary<string,ToolComponentRegistration> editors)
            {
                _types=types;_metadata=metadata;_properties=properties;_changed=changed;_runtimes=runtimes;_editors=editors;
                Monitor.Enter(_lock);
                try
                {
                    if(generation!=_registrationGeneration)throw new InvalidOperationException("Tool registration preparation is stale.");
                    _parameters=parameters.EnterCommit();
                }
                catch{Monitor.Exit(_lock);throw;}
            }
            public void Publish()
            {
                ObjectDisposedException.ThrowIf(_closed,this);
                if(_published)throw new InvalidOperationException("Tool registration was already published.");
                _parameters.Publish();
                if(_changed){_toolTypes=_types;_metadataCache=_metadata;_typeCache=_properties;_runtimeComponents=_runtimes;_editorComponents=_editors;_registrationGeneration++;}
                _published=true;
            }
            public void Dispose(){if(_closed)return;_closed=true;_parameters.Dispose();Monitor.Exit(_lock);}
        }
        public static ToolMetadata? GetToolMetadata(string toolId)
        {
            EnsureDirectoryTool(toolId);
            // Every visible type and its metadata are prepared together. Readers
            // never run reflection or attribute constructors inside the catalog gate.
            lock (_lock) return _metadataCache.TryGetValue(toolId,out var metadata)?metadata:null;
        }
        /// <summary>
        /// 获取所有工具元数据（带缓存）
        /// </summary>
        /// <returns>所有工具元数据列表</returns>
        public static List<ToolMetadata> GetAllToolMetadata()
        {
            lock (_lock) return DirectoryCatalog is { } directory
                ? directory.Tools.Select(t=>_metadataCache.TryGetValue(t.Id,out var loaded)?loaded:t.ToMetadata()).ToList()
                : _metadataCache.Values.ToList();
        }
        /// <summary>
        /// 获取指定分类的工具元数据
        /// </summary>
        /// <param name="category">分类名称</param>
        /// <returns>该分类下的工具元数据列表</returns>
        public static List<ToolMetadata> GetToolsByCategory(string category)
        {
            return GetAllToolMetadata().Where(m => m.Category == category).OrderBy(m => m.Order).ThenBy(m => m.Id, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// 创建工具实例
        /// </summary>
        /// <param name="toolId">工具ID</param>
        /// <returns>工具实例，未找到则返回null</returns>
        public static IToolPlugin? CreateToolInstance(string toolId)
        {
            EnsureDirectoryTool(toolId);
            Type? type;
            lock (_lock) _toolTypes.TryGetValue(toolId,out type);
            return type is null?null:Activator.CreateInstance(type) as IToolPlugin;
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
            return CreateEditorFactory(tool) is { HasDebugWindow: true } editorFactory
                ? editorFactory.CreateDebugWindow()
                : null;
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
                return _toolTypes.ContainsKey(toolId) || DirectoryCatalog?.ToolIds.Contains(toolId)==true;
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
                _runtimeComponents.Remove(toolId);_editorComponents.Remove(toolId);
                _registrationGeneration++;
                _metadataCache.Remove(toolId);
                foreach(var cache in _typeCache.Values)cache.Remove(toolId);
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
            lock(_lock){_typeCache.Clear();_registrationGeneration++;}
            
            VisionLogger.Instance.Log(LogLevel.Success, 
                "✅ 清除所有属性元数据缓存池", 
                "ToolRegistry");
        }
        
        /// <summary>
        /// 清除指定工具的缓存
        /// </summary>
        public static void ClearToolCache(string toolId)
        {
            lock(_lock)
            {
                foreach (var typeCache in _typeCache.Values)typeCache.Remove(toolId);
                _registrationGeneration++;
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
            EnsureDirectoryTool(toolId);
            if (propertyType == null)
                return new List<ToolPropertyMetadata>();
            
            var typeFullName = propertyType.FullName;
            if (string.IsNullOrEmpty(typeFullName))
                return new List<ToolPropertyMetadata>();
            
            lock(_lock)
            {
            if (_typeCache.TryGetValue(typeFullName, out var toolCache) &&
                toolCache.TryGetValue(toolId, out var properties))
            {
                return properties.ToList();
            }
            
            return new List<ToolPropertyMetadata>();
            }
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
            
            lock(_lock)
            {
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
        }

        /// <summary>
        /// 清除所有已注册的工具和缓存
        /// </summary>
        public static void ClearAll()
        {
            lock (_lock)
            {
                DirectoryCatalog=null; _ensureDirectoryTool=null;
                _toolTypes.Clear();
                _runtimeComponents.Clear();_editorComponents.Clear();
                _metadataCache.Clear();
                _typeCache.Clear();
                _registrationGeneration++;
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
                .OrderBy(c => ToolCategories.GetDefinition(c).Order)
                .ThenBy(c => c, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// 提取属性元数据到类型缓存（基于输出变量契约 SSOT，取代空实例反射）。
        /// 静态工具由框架自动派生 OutputVariables；动态工具（脚本/子程序）由实现类从 Parameters 注入。
        /// 未注册到 SdkTypeVocabulary 词汇表的变量类型直接报错提示并跳过（rule-015，不静默兜底）。
        /// </summary>
        /// <param name="toolId">工具ID</param>
        /// <param name="plugin">工具插件实例（已注册，持有 OutputVariables 契约）</param>
        private static Dictionary<string,List<ToolPropertyMetadata>> BuildPropertyMetadata(string toolId,IToolPlugin plugin)
        {
            var fields=new Dictionary<string,List<ToolPropertyMetadata>>(StringComparer.Ordinal);
            var variables=plugin.OutputVariables??throw new InvalidOperationException("Output declaration returned null: "+toolId);
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var variable in variables)
            {
                if(variable is null)throw new InvalidOperationException("Output declaration contains a null field.");
                if(variable.Kind!=OutputVariableKind.Data)continue;
                if(string.IsNullOrWhiteSpace(variable.Name)||!names.Add(variable.Name))throw new InvalidOperationException("Output declaration contains a missing or duplicate field name.");
                var declaredClr=variable.ValueDeclaration is not null?variable.DeclaredType:null;
                var type=declaredClr??SunEyeVision.Plugin.SDK.Models.SdkTypeVocabulary.ToSystemType(variable.VariableType);
                if(type?.FullName is not { } key)
                {
                    throw new InvalidOperationException($"工具 '{toolId}' 的输出变量 '{variable.Name}' 类型 '{variable.VariableType}' 没有正式 CLR 声明或已注册词汇。");
                }
                var metadata=new ToolPropertyMetadata
                {
                    PropertyName=variable.Name,DeclaredType=variable.DeclaredType,ValueDeclaration=variable.ValueDeclaration,AllowsNull=variable.AllowsNull,
                    IsCollection=variable.IsCollection,FixedCount=variable.FixedCount,FieldId=variable.FieldId,
                    DisplayName=string.IsNullOrEmpty(variable.DisplayName)?variable.Name:variable.DisplayName,GroupName=variable.Group,
                    PropertyType=variable.VariableType=="Image"?type:type.IsArray?type:type.MakeArrayType(),
                    IsGroupPlaceholder=variable.IsGroupPlaceholder,Description=null
                };
                if(!fields.TryGetValue(key,out var list)){list=new();fields.Add(key,list);}
                list.Add(metadata);
            }
            return fields;
        }
        public static ToolPropertyMetadata? GetPropertyMetadata(string toolId, string propertyName)
        {
            EnsureDirectoryTool(toolId);
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
            EnsureDirectoryTool(toolId);
            lock(_lock)return _typeCache.Values.Where(cache=>cache.ContainsKey(toolId)).SelectMany(cache=>cache[toolId]).ToList();
        }
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
