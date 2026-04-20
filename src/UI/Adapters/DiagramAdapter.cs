using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AIStudio.Wpf.DiagramDesigner.ViewModels;
using AIStudio.Wpf.DiagramDesigner;
using SunEyeVision.UI.Models;
using SunEyeVision.UI.Adapters;

namespace SunEyeVision.UI.Adapters
{
    /// <summary>
    /// 图表适配器实现
    /// 将Workflow模型转换为AIStudio.Wpf.DiagramDesigner原生图表
    /// 使用 DiagramViewModel 和贝塞尔曲线连接
    /// </summary>
    public class DiagramAdapter : IDiagramAdapter
    {
        // 缓存映射：WorkflowNode -> native Node
        private readonly Dictionary<string, DesignerItemViewModelBase> _nodeMap = new Dictionary<string, DesignerItemViewModelBase>();

        // 缓存映射：WorkflowConnection -> native Connection
        private readonly Dictionary<string, ConnectionViewModel> _connectionMap = new Dictionary<string, ConnectionViewModel>();

        private static DateTime _lastLogTime = DateTime.Now;

        /// <summary>
        /// 带时间戳的调试日志
        /// </summary>
        private static void LogTimestamp(string tag, string message)
        {
            var now = DateTime.Now;
            var elapsed = (now - _lastLogTime).TotalMilliseconds;
            _lastLogTime = now;
}

        public DiagramAdapter()
        {
        }

        /// <summary>
        /// 创建原生节点（DefaultDesignerItemViewModel）
        /// </summary>
        public object CreateNode(WorkflowNode workflowNode)
        {
            // 缓存 WorkflowNode，稍后在 SyncNodes 中创建实际的 ViewModel
            _nodeMap[workflowNode.Id] = null!;

            return workflowNode; // 返回 WorkflowNode，稍后处理
        }

        /// <summary>
        /// 创建原生节点（内部方法）
        /// </summary>
        private DesignerItemViewModelBase CreateNodeInternal(WorkflowNode workflowNode, DiagramViewModel diagramViewModel)
        {
            LogTimestamp("Adapter", $"CreateNodeInternal: {workflowNode.Name}");

            // 创建 DefaultDesignerItemViewModel
            var nativeNode = new DefaultDesignerItemViewModel(diagramViewModel)
            {
                // 设置属性：Left 和 Top（不是 X 和 Y）
                Left = workflowNode.Position.X,
                Top = workflowNode.Position.Y,
                Text = workflowNode.Name
            };

            // 缓存映射
            _nodeMap[workflowNode.Id] = nativeNode;

            LogTimestamp("Adapter", $"CreateNodeInternal 完成");
            return nativeNode;
        }

        /// <summary>
        /// 创建原生节点（公共方法，供拖放使用）
        /// </summary>
        public DesignerItemViewModelBase CreateNativeNode(WorkflowNode workflowNode, DiagramViewModel diagramViewModel)
        {
            _lastLogTime = DateTime.Now; // 重置计时器
            return CreateNodeInternal(workflowNode, diagramViewModel);
        }

        /// <summary>
        /// 创建原生连接（ConnectionViewModel）
        /// 使用贝塞尔曲线连接
        /// </summary>
        public object CreateConnection(WorkflowConnection workflowConnection)
        {
            // 暂时返回 null，实际创建在 SyncConnections 中完成
            return null!;
        }

        /// <summary>
        /// 创建原生连接（内部方法）
        /// 使用贝塞尔曲线连接
        /// </summary>
        private ConnectionViewModel CreateConnectionInternal(WorkflowConnection workflowConnection, DiagramViewModel diagramViewModel)
        {
            // 查找源节点和目标节点
            if (!_nodeMap.TryGetValue(workflowConnection.SourceNodeId, out var sourceNode) || sourceNode == null)
            {
                throw new InvalidOperationException($"源节点未找到: {workflowConnection.SourceNodeId}");
            }

            if (!_nodeMap.TryGetValue(workflowConnection.TargetNodeId, out var targetNode) || targetNode == null)
            {
                throw new InvalidOperationException($"目标节点未找到: {workflowConnection.TargetNodeId}");
            }

            // 获取连接端口
            var sourceConnector = sourceNode.RightConnector;
            var targetConnector = targetNode.LeftConnector;

            if (sourceConnector == null || targetConnector == null)
            {
                throw new InvalidOperationException("连接端口为空");
            }

            // 创建 ConnectionViewModel，使用贝塞尔曲线
            var nativeConnection = new ConnectionViewModel(
                diagramViewModel,
                sourceConnector,
                targetConnector,
                DrawMode.ConnectingLineSmooth,  // 贝塞尔曲线！
                RouterMode.RouterNormal
            );

            // 添加连接标签（可选）
            nativeConnection.AddLabel(workflowConnection.Id);

            // 缓存映射
            _connectionMap[workflowConnection.Id] = nativeConnection;

            return nativeConnection;
        }

        /// <summary>
        /// 同步节点到原生图表
        /// 使用 DiagramViewModel.Add() 方法
        /// </summary>
        public void SyncNodes(IEnumerable<WorkflowNode> nodes, object nativeDiagram)
        {
            _lastLogTime = DateTime.Now; // 重置计时器
            LogTimestamp("Adapter", "SyncNodes 开始");

            try
            {
                // 检查传入的 nativeDiagram 是否为 DiagramViewModel
                if (nativeDiagram is not DiagramViewModel diagramViewModel)
                {
                    throw new InvalidOperationException("nativeDiagram 必须是 DiagramViewModel 类型");
                }

                // 清空现有元素
                var startTime = DateTime.Now;
                diagramViewModel.Items.Clear();
                _nodeMap.Clear();
                _connectionMap.Clear();
                LogTimestamp("Adapter", $"清空集合耗时: {(DateTime.Now - startTime).TotalMilliseconds:F0}ms");

                // 添加新节点
                int count = 0;
                foreach (var node in nodes)
                {
                    startTime = DateTime.Now;
                    var nativeNode = CreateNodeInternal(node, diagramViewModel);
                    diagramViewModel.Add(nativeNode);
                    count++;
                    LogTimestamp("Adapter", $"  节点 {count}: {(DateTime.Now - startTime).TotalMilliseconds:F0}ms");
                }

                LogTimestamp("Adapter", $"SyncNodes 完成, 共 {count} 个节点");
            }
            catch (Exception ex)
            {
                LogTimestamp("Adapter", $"SyncNodes 异常: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 同步连接到原生图表
        /// 使用 DiagramViewModel.Add() 方法
        /// </summary>
        public void SyncConnections(IEnumerable<WorkflowConnection> connections, object nativeDiagram)
        {
            try
            {
                // 检查传入的 nativeDiagram 是否为 DiagramViewModel
                if (nativeDiagram is not DiagramViewModel diagramViewModel)
                {
                    throw new InvalidOperationException("nativeDiagram 必须是 DiagramViewModel 类型");
                }

                // 添加新连接
                int successCount = 0;
                foreach (var connection in connections)
                {
                    try
                    {
                        var nativeConnection = CreateConnectionInternal(connection, diagramViewModel);
                        diagramViewModel.Add(nativeConnection);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        // 忽略单个连接创建失败
                    }
                }

    
            }
            catch (Exception ex)
            {


                throw;
            }
        }

        /// <summary>
        /// 添加节点到原生图表
        /// 使用 DiagramViewModel.Add() 方法
        /// </summary>
        public void AddNode(object nativeNode, object nativeDiagram)
        {
            try
            {
                if (nativeDiagram is not DiagramViewModel diagramViewModel)
                    return;

                diagramViewModel.Add(nativeNode);
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 添加连接到原生图表
        /// 使用 DiagramViewModel.Add() 方法
        /// </summary>
        public void AddConnection(object nativeConnection, object nativeDiagram)
        {
            try
            {
                if (nativeDiagram is not DiagramViewModel diagramViewModel)
                    return;

                diagramViewModel.Add(nativeConnection);
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 移除节点
        /// 使用 DiagramViewModel.Remove() 方法
        /// </summary>
        public void RemoveNode(object nativeNode, object nativeDiagram)
        {
            try
            {
                if (nativeDiagram is not DiagramViewModel diagramViewModel)
                    return;

                if (nativeNode is SelectableDesignerItemViewModelBase item)
                {
                    diagramViewModel.Items.Remove(item);
                }
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 移除连接
        /// 使用 DiagramViewModel.Remove() 方法
        /// </summary>
        public void RemoveConnection(object nativeConnection, object nativeDiagram)
        {
            try
            {
                if (nativeDiagram is not DiagramViewModel diagramViewModel)
                    return;

                if (nativeConnection is SelectableDesignerItemViewModelBase item)
                {
                    diagramViewModel.Items.Remove(item);
                }
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 设置贝塞尔曲线样式（原生库默认使用贝塞尔曲线）
        /// </summary>
        /// <param name="nativeConnection">原生连接对象</param>
        public void SetBezierCurveStyle(object nativeConnection)
        {
            // AIStudio.Wpf.DiagramDesigner原生库默认使用贝塞尔曲线
            // 在 CreateConnectionInternal 中已设置 DrawMode.ConnectingLineSmooth
            // 此方法为接口实现预留

        }
    }
}
