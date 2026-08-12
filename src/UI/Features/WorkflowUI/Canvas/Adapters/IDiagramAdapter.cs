using System.Collections.Generic;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters
{
    /// <summary>
    /// 图表适配器接口。
    /// 用于在Workflow模型和AIStudio.Wpf.DiagramDesigner原生图表之间进行适配
    /// 使用贝塞尔曲线连接算法。
    /// </summary>
    public interface IDiagramAdapter
    {
        /// <summary>
        /// 创建原生节点
        /// </summary>
        object CreateNode(WorkflowNode workflowNode);

        /// <summary>
        /// 创建原生连接
        /// </summary>
        object CreateConnection(WorkflowConnection workflowConnection);

        /// <summary>
        /// 同步节点到原生图表。
        /// </summary>
        void SyncNodes(IEnumerable<WorkflowNode> nodes, object nativeDiagram);

        /// <summary>
        /// 同步连接到原生图表。
        /// </summary>
        void SyncConnections(IEnumerable<WorkflowConnection> connections, object nativeDiagram);

        /// <summary>
        /// 设置贝塞尔曲线样式。
        /// </summary>
        void SetBezierCurveStyle(object nativeConnection);

        /// <summary>
        /// 添加节点到原生图表。
        /// </summary>
        void AddNode(object nativeNode, object nativeDiagram);

        /// <summary>
        /// 添加连接到原生图表。
        /// </summary>
        void AddConnection(object nativeConnection, object nativeDiagram);

        /// <summary>
        /// 移除节点
        /// </summary>
        void RemoveNode(object nativeNode, object nativeDiagram);

        /// <summary>
        /// 移除连接
        /// </summary>
        void RemoveConnection(object nativeConnection, object nativeDiagram);
    }
}
