using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.UI.Services.ParameterSetting
{
    /// <summary>
    /// 图像数据源服务
    /// </summary>
    /// <remarks>
    /// 专门处理图像类型参数的数据源查询和过滤。
    /// 支持OpenCvSharp.Mat和其他图像类型的自动识别。
    /// 
    /// 核心功能：
    /// 1. 过滤图像类型数据源
    /// 2. 提供图像类型兼容性检查
    /// 3. 支持缩略图预览数据
    /// </remarks>
    public class ImageDataSourceService
    {
        private readonly IDataSourceQueryService _dataSourceQueryService;

        /// <summary>
        /// 创建图像数据源服务
        /// </summary>
        public ImageDataSourceService(IDataSourceQueryService dataSourceQueryService)
        {
            _dataSourceQueryService = dataSourceQueryService ?? throw new ArgumentNullException(nameof(dataSourceQueryService));
        }

        /// <summary>
        /// 获取图像类型的可用数据源
        /// </summary>
        /// <param name="nodeId">当前节点ID</param>
        /// <returns>图像类型数据源列表</returns>
        public List<AvailableDataSource> GetImageDataSources(string nodeId)
        {
            var allDataSources = _dataSourceQueryService.GetAvailableDataSources(nodeId);
            return allDataSources.Where(IsImageType).ToList();
        }

        /// <summary>
        /// 获取图像类型的可用数据源（按父节点分组）
        /// </summary>
        /// <param name="nodeId">当前节点ID</param>
        /// <returns>按父节点分组的图像数据源</returns>
        public List<ImageDataSourceGroup> GetImageDataSourcesGrouped(string nodeId)
        {
            var parentNodes = _dataSourceQueryService.GetParentNodes(nodeId);
            var groups = new List<ImageDataSourceGroup>();

            foreach (var parent in parentNodes)
            {
                var imageDataSources = parent.OutputProperties
                    .Where(IsImageType)
                    .ToList();

                if (imageDataSources.Count > 0)
                {
                    groups.Add(new ImageDataSourceGroup
                    {
                        NodeId = parent.NodeId,
                        NodeName = parent.NodeName,
                        NodeType = parent.NodeType,
                        ExecutionStatus = parent.ExecutionStatus,
                        HasExecuted = parent.HasExecuted,
                        DataSources = imageDataSources
                    });
                }
            }

            return groups;
        }

        /// <summary>
        /// 检查类型是否为图像类型（通过 Mapper 词汇表）
        /// </summary>
        public static bool IsImageType(Type type)
        {
            if (type == null) return false;
            return SdkTypeVocabulary.ToTypeVocabulary(type) == "Image";
        }

        /// <summary>
        /// 检查数据源是否为图像类型（通过 AvailableDataSource.VariableType）
        /// </summary>
        public static bool IsImageType(AvailableDataSource dataSource)
        {
            return dataSource?.VariableType == "Image";
        }

        /// <summary>
        /// 获取图像类型的显示名称
        /// </summary>
        public static string GetImageTypeDisplayName(Type type)
        {
            if (type == null)
                return "未知";

            if (type.Name == "IVisionImage")
                return "图像";

            // BitmapSource派生类
            if (type.Name == "BitmapSource" || type.Name == "BitmapImage")
                return "位图";

            return type.Name;
        }
    }

    /// <summary>
    /// 图像数据源分组
    /// </summary>
    public class ImageDataSourceGroup
    {
        /// <summary>
        /// 节点ID
        /// </summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>
        /// 节点名称
        /// </summary>
        public string NodeName { get; set; } = string.Empty;

        /// <summary>
        /// 节点类型
        /// </summary>
        public string NodeType { get; set; } = string.Empty;

        /// <summary>
        /// 执行状态
        /// </summary>
        public ExecutionStatus ExecutionStatus { get; set; }

        /// <summary>
        /// 是否已执行
        /// </summary>
        public bool HasExecuted { get; set; }

        /// <summary>
        /// 图像数据源列表
        /// </summary>
        public List<AvailableDataSource> DataSources { get; set; } = new();

        /// <summary>
        /// 数据源数量
        /// </summary>
        public int Count => DataSources.Count;

        /// <summary>
        /// 分组显示文本
        /// </summary>
        public string DisplayText => $"{NodeName} ({Count}个图像输出)";
    }
}
