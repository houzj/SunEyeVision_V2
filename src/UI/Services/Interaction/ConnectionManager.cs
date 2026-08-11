using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Services.Connection;
using SunEyeVision.UI.Shell.Controls.Canvas;

using SunEyeVision.UI.Features.WorkflowUI;

using SunEyeVision.UI.Features.Shell;

namespace SunEyeVision.UI.Services.Interaction
{
    /// <summary>
    /// 工作流连接管理器
    /// 负责节点之间的连接创建、管理和路径计算
    /// </summary>
    public class WorkflowConnectionManager
    {
        private readonly WorkflowCanvasControl _canvasControl;
        private readonly MainWindowViewModel? _viewModel;
        private ConnectionPathCache? _connectionPathCache;

        public WorkflowConnectionManager(WorkflowCanvasControl canvasControl, MainWindowViewModel? viewModel)
        {
            _canvasControl = canvasControl;
            _viewModel = viewModel;
        }

        /// <summary>
        /// 设置连接路径缓存
        /// </summary>
        public void SetConnectionPathCache(ConnectionPathCache cache)
        {
            _connectionPathCache = cache;
        }

        /// <summary>
        /// 刷新所有连接的路径（触发重新计算）
        /// </summary>
        public void RefreshAllConnectionPaths()
        {
            try
            {
                var currentWorkflowTab = GetCurrentWorkflowTab();
                if (currentWorkflowTab == null)
                {
                    return;
                }

                // 标记所有缓存为脏数据
                if (_connectionPathCache != null)
                {
                    _connectionPathCache.MarkAllDirty();
                }

                // 触发所有连接的属性变化，强制刷新UI
                foreach (var connection in currentWorkflowTab.WorkflowConnections)
                {
                    try
                    {
                        if (connection == null)
                        {
                            continue;
                        }

                        // 触发 SourcePosition 变化，导致转换器重新计算
                        var oldPos = connection.SourcePosition;
                        connection.SourcePosition = new Point(oldPos.X + 0.001, oldPos.Y);
                        connection.SourcePosition = oldPos;
                    }
                    catch (Exception ex)
                    {
}
                }
            }
            catch (Exception ex)
            {
}
        }

        /// <summary>
        /// 创建节点连接（使用指定的目标端口）
        /// </summary>
        public void CreateConnectionWithSpecificPort(WorkflowNode sourceNode, WorkflowNode targetNode, string targetPortName, string? sourcePortName)
        {
            var selectedTab = GetCurrentWorkflowTab();
            if (selectedTab == null || selectedTab.WorkflowConnections == null)
            {
                return;
            }

            var connectionId = $"conn_{Guid.NewGuid().ToString("N")[..8]}";
            var newConnection = new WorkflowConnection(connectionId, sourceNode.Id, targetNode.Id);

            // 设置源端口名
            newConnection.SourcePort = sourcePortName ?? "RightPort";

            // 获取源端口位置
            Point sourcePos;
            switch (sourcePortName)
            {
                case "TopPort":
                    sourcePos = sourceNode.TopPortPosition;
                    break;
                case "BottomPort":
                    sourcePos = sourceNode.BottomPortPosition;
                    break;
                case "LeftPort":
                    sourcePos = sourceNode.LeftPortPosition;
                    break;
                case "RightPort":
                    sourcePos = sourceNode.RightPortPosition;
                    break;
                default:
                    sourcePos = sourceNode.RightPortPosition;
                    break;
            }

            // 获取目标端口位置（使用用户指定的端口）
            Point targetPos;
            switch (targetPortName)
            {
                case "TopPort":
                    targetPos = targetNode.TopPortPosition;
                    break;
                case "BottomPort":
                    targetPos = targetNode.BottomPortPosition;
                    break;
                case "LeftPort":
                    targetPos = targetNode.LeftPortPosition;
                    break;
                case "RightPort":
                    targetPos = targetNode.RightPortPosition;
                    break;
                default:
                    targetPos = targetNode.LeftPortPosition;
                    break;
            }

            newConnection.SourcePosition = sourcePos;
            newConnection.TargetPosition = targetPos;

            // 通过 MainWindowViewModel 添加连接（记录日志）
            if (_viewModel != null)
            {
                _viewModel.AddConnectionToWorkflow(newConnection);
            }
            else
            {
                selectedTab.WorkflowConnections.Add(newConnection);
            }
        }

        /// <summary>
        /// 创建节点连接
        /// </summary>
        public void CreateConnection(WorkflowNode sourceNode, WorkflowNode targetNode, string? sourcePortName)
        {
            var selectedTab = GetCurrentWorkflowTab();
            if (selectedTab == null)
            {
                return;
            }

            if (selectedTab.WorkflowConnections == null)
            {
                return;
            }

            var connectionId = $"connId_{Guid.NewGuid().ToString("N")[..8]}";
            var newConnection = new WorkflowConnection(connectionId, sourceNode.Id, targetNode.Id);
            string finalSourcePort, finalTargetPort;

            // 使用记录的源端口
            string initialSourcePort = sourcePortName ?? "RightPort";
            Point sourcePos;
            switch (initialSourcePort)
            {
                case "TopPort":
                    sourcePos = sourceNode.TopPortPosition;
                    break;
                case "BottomPort":
                    sourcePos = sourceNode.BottomPortPosition;
                    break;
                case "LeftPort":
                    sourcePos = sourceNode.LeftPortPosition;
                    break;
                case "RightPort":
                    sourcePos = sourceNode.RightPortPosition;
                    break;
                default:
                    sourcePos = sourceNode.RightPortPosition;
                    break;
            }

            // 选择目标端口（根据源端口方向和目标节点位置选择最近的端口）
            var deltaX = targetNode.Position.X - sourcePos.X;
            var deltaY = targetNode.Position.Y - sourcePos.Y;

            string direction = "";
            bool isVerticalDominant = initialSourcePort == "TopPort" || initialSourcePort == "BottomPort";

            Point targetPos;
            if (isVerticalDominant)
            {
                // 源端口是垂直方向（Top/Bottom），优先选择垂直方向的目标端口
                bool horizontalDominant = Math.Abs(deltaX) > 2 * Math.Abs(deltaY);

                if (horizontalDominant)
                {
                    direction = "水平（源垂直但水平偏移过大）";
                    if (deltaX > 0)
                    {
                        finalSourcePort = "RightPort";
                        finalTargetPort = "LeftPort";
                        targetPos = targetNode.LeftPortPosition;
                    }
                    else
                    {
                        finalSourcePort = "LeftPort";
                        finalTargetPort = "RightPort";
                        targetPos = targetNode.RightPortPosition;
                    }
                }
                else
                {
                    direction = "垂直主导";
                    if (deltaY > 0)
                    {
                        finalSourcePort = "BottomPort";
                        finalTargetPort = "TopPort";
                        targetPos = targetNode.TopPortPosition;
                    }
                    else
                    {
                        finalSourcePort = "TopPort";
                        finalTargetPort = "BottomPort";
                        targetPos = targetNode.BottomPortPosition;
                    }
                }
            }
            else
            {
                // 源端口是水平方向（Left/Right），优先选择水平方向的目标端口
                bool verticalDominant = Math.Abs(deltaY) > 2 * Math.Abs(deltaX);

                if (verticalDominant)
                {
                    direction = "垂直（源水平但垂直偏移过大）";
                    if (deltaY > 0)
                    {
                        finalSourcePort = "BottomPort";
                        finalTargetPort = "TopPort";
                        targetPos = targetNode.TopPortPosition;
                    }
                    else
                    {
                        finalSourcePort = "TopPort";
                        finalTargetPort = "BottomPort";
                        targetPos = targetNode.BottomPortPosition;
                    }
                }
                else
                {
                    direction = "水平主导";
                    if (deltaX > 0)
                    {
                        finalSourcePort = "RightPort";
                        finalTargetPort = "LeftPort";
                        targetPos = targetNode.LeftPortPosition;
                    }
                    else
                    {
                        finalSourcePort = "LeftPort";
                        finalTargetPort = "RightPort";
                        targetPos = targetNode.RightPortPosition;
                    }
                }
            }

            // 设置连接属性
            newConnection.SourcePort = finalSourcePort;
            newConnection.TargetPort = finalTargetPort;
            newConnection.SourcePosition = sourcePos;
            newConnection.TargetPosition = targetPos;

            // 通过 MainWindowViewModel 添加连接（记录日志）
            if (_viewModel != null)
            {
                _viewModel.AddConnectionToWorkflow(newConnection);
            }
            else
            {
                selectedTab.WorkflowConnections.Add(newConnection);
            }

            if (_viewModel != null)
            {
                _viewModel.StatusText = $"成功连接: {sourceNode.Name} -> {targetNode.Name}";
            }
        }

        private WorkflowTabViewModel? GetCurrentWorkflowTab()
        {
            return _viewModel?.WorkflowTabViewModel?.SelectedTab;
        }
    }
}
