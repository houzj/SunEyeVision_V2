using System;
using System.Collections.Generic;

namespace SunEyeVision.Plugin.Editing.Wpf.Execution.Parameters
{
    /// <summary>
    /// 属性元数据提供者接口
    /// </summary>
    /// <remarks>
    /// 用于在设计时和运行时提供工具输出属性的元数据。
    /// 
    /// 依赖倒置原则：
    /// - SDK 层定义此接口（依赖抽象而非具体实现）
    /// - Infrastructure 层实现此接口（通过 ToolRegistry）
    /// - UI 层注入具体实现（构造函数注入）
    /// 
    /// 优化方案（2026-04）：
    /// - 按具体类型查询，无需分类
    /// - 直接精确匹配类型（typeof(Point[]), typeof(Mat) 等）
    /// - 符合视觉软件特点（OpenCvSharp 主要使用数组）
    /// </remarks>
    public interface IPropertyMetadataProvider
    {
        /// <summary>
        /// 获取所有属性元数据
        /// </summary>
        /// <param name="toolType">工具类型名称或ID</param>
        /// <returns>属性元数据列表，未找到返回 null</returns>
        List<ToolPropertyMetadata>? GetAllPropertyMetadata(string toolType);
        
        /// <summary>
        /// 按类型获取属性元数据（优化方案）
        /// </summary>
        /// <param name="toolType">工具类型名称或ID</param>
        /// <param name="propertyType">属性类型（如 typeof(Point[]), typeof(Mat) 等）</param>
        /// <returns>属性元数据列表</returns>
        /// <remarks>
        /// 示例：
        /// <code>
        /// // 查询 Point[] 类型的属性
        /// var props = provider.GetPropertyMetadataByType("contour", typeof(Point[]));
        /// // 返回：[ { PropertyName: "Contours", PropertyType: typeof(Point[]) } ]
        /// </code>
        /// </remarks>
        List<ToolPropertyMetadata> GetPropertyMetadataByType(string toolType, Type propertyType);
    }
}
