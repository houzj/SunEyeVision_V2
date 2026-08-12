namespace SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters
{
    /// <summary>
    /// 分类显示适配器接口
    /// </summary>
    /// <remarks>
    /// 允许适配器声明支持的分类，实现自动扫描和匹配
    /// </remarks>
    public interface ICategoryDisplayAdapter : INodeDisplayAdapter
    {
        /// <summary>
        /// 支持的分类列表
        /// </summary>
        string[] SupportedCategories { get; }
    }
}
