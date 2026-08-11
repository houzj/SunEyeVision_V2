using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Adapters;

namespace SunEyeVision.UI.Adapters
{
    /// <summary>
    /// 节点显示适配器接口，用于解耦节点显示逻辑与业务逻辑
    /// </summary>
    public interface INodeDisplayAdapter
    {
        /// <summary>
        /// 获取节点显示文本
        /// </summary>
        string GetDisplayText(WorkflowNode node);

        /// <summary>
        /// 获取节点图标
        /// </summary>
        string GetIcon(WorkflowNode node);

        /// <summary>
        /// 获取节点背景颜色
        /// </summary>
        Color GetBackgroundColor(WorkflowNode node);

        /// <summary>
        /// 获取节点边框颜色
        /// </summary>
        Color GetBorderColor(WorkflowNode node);
    }
}
