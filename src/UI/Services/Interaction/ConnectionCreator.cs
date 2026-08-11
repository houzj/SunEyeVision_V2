using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Services.Connection;
using SunEyeVision.UI.Shell.Controls.Canvas;

using SunEyeVision.UI.Features.WorkflowUI;

using SunEyeVision.UI.Features.Shell;

namespace SunEyeVision.UI.Services.Interaction
{
    /// <summary>
    /// 工作流连接创建器 - 负责节点连接的创建逻辑
    /// </summary>
    public class WorkflowConnectionCreator
    {
        private readonly MainWindowViewModel? _viewModel;

        public WorkflowConnectionCreator(MainWindowViewModel? viewModel)
        {
            _viewModel = viewModel;
        }

        /// <summary>
        /// 创建节点连接（使用指定的目标端口）
        /// </summary>
        public WorkflowConnection? CreateConnectionWithSpecificPort(
            WorkflowNode sourceNode,
            WorkflowNode targetNode,
            string sourcePortName,
            string targetPortName,
            WorkflowTabViewModel? currentTab)
        {
            if (currentTab == null || currentTab.WorkflowConnections == null)
            {
                return null;
            }

            var connectionId = $"conn_{Guid.NewGuid().ToString("N")[..8]}";
            var newConnection = new WorkflowConnection(connectionId, sourceNode.Id, targetNode.Id);

            // 设置端口名称
            newConnection.SourcePort = sourcePortName;
            newConnection.TargetPort = targetPortName;

            // 获取源端口位置
            Point sourcePos = GetPortPosition(sourceNode, sourcePortName);

            // 获取目标端口位置
            Point targetPos = GetPortPosition(targetNode, targetPortName);

            newConnection.SourcePosition = sourcePos;
            newConnection.TargetPosition = targetPos;

            // 箭头位置和角度由 ConnectionPathCache 计算，这里先设置默认值
            newConnection.ArrowPosition = targetPos;  // 初始设置为目标端口位置
            newConnection.ArrowAngle = 0;

            // 通过 MainWindowViewModel 添加连接（记录日志）
            if (_viewModel != null)
            {
                _viewModel.AddConnectionToWorkflow(newConnection);
            }
            else
            {
                currentTab.WorkflowConnections.Add(newConnection);
            }

            return newConnection;
        }

        /// <summary>
        /// 创建节点连接（智能选择端口）
        /// </summary>
        public WorkflowConnection? CreateConnection(
            WorkflowNode sourceNode,
            WorkflowNode targetNode,
            string initialSourcePort,
            WorkflowTabViewModel? currentTab)
        {
            if (currentTab == null)
            {
                return null;
            }

            if (currentTab.WorkflowConnections == null)
            {
                return null;
            }

            var connectionId = $"conn_{Guid.NewGuid().ToString("N")[..8]}";
            var newConnection = new WorkflowConnection(connectionId, sourceNode.Id, targetNode.Id);

            // 智能选择连接点位置
            var (sourcePos, targetPos, finalSourcePort, finalTargetPort) =
                CalculateSmartPortPositions(sourceNode, targetNode, initialSourcePort);

            newConnection.SourcePort = finalSourcePort;
            newConnection.TargetPort = finalTargetPort;
            newConnection.SourcePosition = sourcePos;
            newConnection.TargetPosition = targetPos;

            // 箭头位置和角度由 ConnectionPathCache 计算，这里先设置默认值
            newConnection.ArrowPosition = targetPos;  // 初始设置为目标端口位置
            newConnection.ArrowAngle = 0;

            // 通过 MainWindowViewModel 添加连接（记录日志）
            if (_viewModel != null)
            {
                _viewModel.AddConnectionToWorkflow(newConnection);
            }
            else
            {
                currentTab.WorkflowConnections.Add(newConnection);
            }

            _viewModel!.StatusText = $"成功连接: {sourceNode.Name} -> {targetNode.Name}";

            return newConnection;
        }

        /// <summary>
        /// 计算智能端口位置
        /// </summary>
        private (Point sourcePos, Point targetPos, string sourcePort, string targetPort) 
            CalculateSmartPortPositions(WorkflowNode sourceNode, WorkflowNode targetNode, string initialSourcePort)
        {
            Point sourcePos, targetPos;
            string finalSourcePort, finalTargetPort;

            // 获取初始源端口位置
            sourcePos = GetPortPosition(sourceNode, initialSourcePort);

            // 选择目标端口（根据源端口方向和目标节点位置选择最近的端口）
            var deltaX = targetNode.Position.X - sourcePos.X;
            var deltaY = targetNode.Position.Y - sourcePos.Y;

            string direction = "";
            bool isVerticalDominant = initialSourcePort == "TopPort" || initialSourcePort == "BottomPort";

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
                        sourcePos = sourceNode.RightPortPosition;
                        targetPos = targetNode.LeftPortPosition;
                    }
                    else
                    {
                        finalSourcePort = "LeftPort";
                        finalTargetPort = "RightPort";
                        sourcePos = sourceNode.LeftPortPosition;
                        targetPos = targetNode.RightPortPosition;
                    }
                }
                else
                {
                    direction = "垂直（源端口主导）";
                    if (deltaY > 0)
                    {
                        finalSourcePort = "BottomPort";
                        finalTargetPort = "TopPort";
                        sourcePos = sourceNode.BottomPortPosition;
                        targetPos = targetNode.TopPortPosition;
                    }
                    else
                    {
                        finalSourcePort = "TopPort";
                        finalTargetPort = "BottomPort";
                        sourcePos = sourceNode.TopPortPosition;
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
                        sourcePos = sourceNode.BottomPortPosition;
                        targetPos = targetNode.TopPortPosition;
                    }
                    else
                    {
                        finalSourcePort = "TopPort";
                        finalTargetPort = "BottomPort";
                        sourcePos = sourceNode.TopPortPosition;
                        targetPos = targetNode.BottomPortPosition;
                    }
                }
                else
                {
                    direction = "水平（源端口主导）";
                    if (deltaX > 0)
                    {
                        finalSourcePort = "RightPort";
                        finalTargetPort = "LeftPort";
                        sourcePos = sourceNode.RightPortPosition;
                        targetPos = targetNode.LeftPortPosition;
                    }
                    else
                    {
                        finalSourcePort = "LeftPort";
                        finalTargetPort = "RightPort";
                        sourcePos = sourceNode.LeftPortPosition;
                        targetPos = targetNode.RightPortPosition;
                    }
                }
            }

            return (sourcePos, targetPos, finalSourcePort, finalTargetPort);
        }

        /// <summary>
        /// 验证端口位置是否正确
        /// </summary>
        private void ValidatePortPositions(
            WorkflowNode sourceNode, 
            WorkflowNode targetNode, 
            string finalSourcePort, 
            string finalTargetPort,
            Point sourcePos, 
            Point targetPos)
        {
            // 已删除详细验证日志，避免信息干扰
        }

        /// <summary>
        /// 检查连接是否已存在
        /// </summary>
        public bool ConnectionExists(WorkflowTabViewModel? currentTab, string sourceNodeId, string targetNodeId)
        {
            if (currentTab?.WorkflowConnections == null) return false;

            return currentTab.WorkflowConnections.Any(c => 
                c.SourceNodeId == sourceNodeId && 
                c.TargetNodeId == targetNodeId);
        }

        /// <summary>
        /// 检查是否为自连接
        /// </summary>
        public bool IsSelfConnection(string sourceNodeId, string targetNodeId)
        {
            return sourceNodeId == targetNodeId;
        }

        /// <summary>
        /// 获取指定端口的位置
        /// </summary>
        private static Point GetPortPosition(WorkflowNode node, string portName)
        {
            return portName switch
            {
                "TopPort" => node.TopPortPosition,
                "BottomPort" => node.BottomPortPosition,
                "LeftPort" => node.LeftPortPosition,
                "RightPort" => node.RightPortPosition,
                _ => node.Position // 默认返回节点中心
            };
        }

        /// <summary>
        /// 计算箭头角度（度）
        /// </summary>
        private static double CalculateArrowAngle(Point targetPortPos, WorkflowNode targetNode, string targetPortName)
        {
            // 箭头默认指向右方（0度），根据端口方向旋转
            return targetPortName switch
            {
                "TopPort" => 270,    // 指向上方
                "BottomPort" => 90,   // 指向下方
                "LeftPort" => 180,    // 指向左方
                "RightPort" => 0,     // 指向右方
                _ => 0
            };
        }
    }
}