using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters
{
    /// <summary>
    /// 节点显示适配器工厂
    /// </summary>
    /// <remarks>
    /// 支持基于 Category 的适配器自动匹配和基于 algorithmType 的手动注册
    /// </remarks>
    public static class NodeDisplayAdapterFactory
    {
        private static readonly Dictionary<string, INodeDisplayAdapter> _algorithmTypeAdapters = new Dictionary<string, INodeDisplayAdapter>();
        private static readonly Dictionary<string, ICategoryDisplayAdapter> _categoryAdapters = new Dictionary<string, ICategoryDisplayAdapter>();
        private static readonly INodeDisplayAdapter _defaultAdapter = new DefaultNodeDisplayAdapter();
        private static bool _initialized = false;

        /// <summary>
        /// 静态构造函数，自动扫描并初始化分类适配器
        /// </summary>
        static NodeDisplayAdapterFactory()
        {
            InitializeCategoryAdapters();
        }

        /// <summary>
        /// 初始化分类适配器（自动扫描实现类）
        /// </summary>
        private static void InitializeCategoryAdapters()
        {
            try
            {
                // 获取当前程序集
                var assembly = Assembly.GetExecutingAssembly();

                // 扫描所有实现 ICategoryDisplayAdapter 的类型
                var adapterTypes = assembly.GetTypes()
                    .Where(t => typeof(ICategoryDisplayAdapter).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                // 为每个适配器类型创建实例并注册
                foreach (var adapterType in adapterTypes)
                {
                    try
                    {
                        var adapter = (ICategoryDisplayAdapter)Activator.CreateInstance(adapterType)!;
                        RegisterCategoryAdapter(adapter);
                    }
                    catch (Exception ex)
                    {
                        // 静默失败，继续扫描其他适配器
                    }
                }

                _initialized = true;
            }
            catch
            {
                // 初始化失败，不影响使用
            }
        }

        /// <summary>
        /// 注册分类显示适配器
        /// </summary>
        /// <param name="adapter">适配器实例</param>
        private static void RegisterCategoryAdapter(ICategoryDisplayAdapter adapter)
        {
            if (adapter?.SupportedCategories == null)
                return;

            foreach (var category in adapter.SupportedCategories)
            {
                if (!string.IsNullOrWhiteSpace(category))
                {
                    _categoryAdapters[category] = adapter;
                }
            }
        }

        /// <summary>
        /// 根据分类获取适配器
        /// </summary>
        /// <param name="category">分类名称</param>
        /// <returns>适配器实例，如果未找到则返回默认适配器</returns>
        public static INodeDisplayAdapter GetAdapterByCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return _defaultAdapter;

            return _categoryAdapters.TryGetValue(category, out var adapter) ? adapter : _defaultAdapter;
        }

        /// <summary>
        /// 根据算法类型获取适配器（优先使用 Category 匹配）
        /// </summary>
        /// <param name="algorithmType">算法类型（工具ID）</param>
        /// <returns>适配器实例，如果未找到则返回默认适配器</returns>
        public static INodeDisplayAdapter GetAdapter(string algorithmType)
        {
            if (string.IsNullOrWhiteSpace(algorithmType))
                return _defaultAdapter;

            // 优先使用基于 algorithmType 的手动注册
            if (_algorithmTypeAdapters.TryGetValue(algorithmType, out var manualAdapter))
                return manualAdapter;

            // 回退到基于 Category 的自动匹配
            try
            {
                var metadata = Plugin.Infrastructure.Managers.Tool.ToolRegistry.GetToolMetadata(algorithmType);
                if (metadata != null && !string.IsNullOrWhiteSpace(metadata.Category))
                {
                    return GetAdapterByCategory(metadata.Category);
                }
            }
            catch
            {
                // 静默失败，返回默认适配器
            }

            return _defaultAdapter;
        }

        /// <summary>
        /// 注册节点显示适配器（基于 algorithmType）
        /// </summary>
        /// <param name="algorithmType">算法类型</param>
        /// <param name="adapter">适配器实例</param>
        public static void RegisterAdapter(string algorithmType, INodeDisplayAdapter adapter)
        {
            if (string.IsNullOrWhiteSpace(algorithmType) || adapter == null)
                return;

            _algorithmTypeAdapters[algorithmType] = adapter;
        }

        /// <summary>
        /// 清空所有已注册的适配器
        /// </summary>
        public static void Clear()
        {
            _algorithmTypeAdapters.Clear();
            _categoryAdapters.Clear();
            InitializeCategoryAdapters();
        }
    }
}
