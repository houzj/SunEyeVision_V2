using System;
using System.Collections.ObjectModel;
using System.Linq;
using SunEyeVision.UI.Shared;

using SunEyeVision.UI.Features.Toolbox;

namespace SunEyeVision.UI.Services.Toolbox
{
    /// <summary>
    /// 工具缓存管理器 - 缓存分类工具列表，避免频繁重新加载
    /// </summary>
    public class ToolboxToolCacheManager
    {
        private readonly ObservableCollection<ToolItem> _allTools;
        private readonly System.Collections.Generic.Dictionary<string, ObservableCollection<ToolItem>> _toolCache;

        public ToolboxToolCacheManager(ObservableCollection<ToolItem> allTools)
        {
            _allTools = allTools;
            _toolCache = new System.Collections.Generic.Dictionary<string, ObservableCollection<ToolItem>>();

            // 预缓存所有分组
            PreCacheCategories();
        }

        /// <summary>
        /// 预缓存所有分类的工具列表
        /// </summary>
        private void PreCacheCategories()
        {
            var categories = _allTools.Select(t => t.Category).Distinct();
            foreach (var category in categories)
            {
                GetToolsByCategory(category);
            }
        }

        /// <summary>
        /// 获取指定分类的工具列表（带缓存）
        /// </summary>
        public ObservableCollection<ToolItem> GetToolsByCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return new ObservableCollection<ToolItem>();
            }

            // 如果缓存中存在，直接返回
            if (_toolCache.TryGetValue(category, out var cachedTools))
            {
                return cachedTools;
            }

            // 从AllTools中过滤并缓存
            var tools = new ObservableCollection<ToolItem>(
                _allTools.Where(t => t.Category == category)
            );

            _toolCache[category] = tools;

            return tools;
        }

        /// <summary>
        /// 清除指定分类的缓存
        /// </summary>
        public void ClearCache(string category)
        {
            if (_toolCache.ContainsKey(category))
            {
                _toolCache.Remove(category);
            }
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void ClearAllCache()
        {
            _toolCache.Clear();
        }

        /// <summary>
        /// 重新构建指定分类的缓存
        /// </summary>
        public void RebuildCache(string category)
        {
            ClearCache(category);
            GetToolsByCategory(category);
        }

        /// <summary>
        /// 重新构建所有缓存
        /// </summary>
        public void RebuildAllCache()
        {
            ClearAllCache();
            PreCacheCategories();
        }

        /// <summary>
        /// 检查缓存中是否包含指定分类
        /// </summary>
        public bool HasCache(string category)
        {
            return _toolCache.ContainsKey(category);
        }

        /// <summary>
        /// 获取缓存的分类数据
        /// </summary>
        public int CachedCategoryCount => _toolCache.Count;

        /// <summary>
        /// 刷新AllTools后的自动更新
        /// </summary>
        public void RefreshFromAllTools()
        {
            RebuildAllCache();
        }
    }
}
