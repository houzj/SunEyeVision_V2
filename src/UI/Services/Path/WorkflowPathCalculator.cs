using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Services.Path
{
    /// <summary>
    /// 工作流路径计算器 - 负责连接线路径的计算和生成
    /// </summary>
    public class WorkflowPathCalculator
    {
        /// <summary>
        /// 计算智能直角折线路径
        /// </summary>
        public static List<Point> CalculateSmartPath(Point source, Point target, string sourcePort, string targetPort)
        {
            var pathPoints = new List<Point>();
            
            // 添加起点
            pathPoints.Add(source);

            double deltaX = target.X - source.X;
            double deltaY = target.Y - source.Y;

            // 判断是否需要中间点
            bool needsIntermediatePoint = true;
            
            // 如果源端口和目标端口在相同方向且距离较近，可以不需要中间点
            if ((sourcePort == "LeftPort" && targetPort == "RightPort" && deltaX < 0) ||
                (sourcePort == "RightPort" && targetPort == "LeftPort" && deltaX > 0) ||
                (sourcePort == "TopPort" && targetPort == "BottomPort" && deltaY < 0) ||
                (sourcePort == "BottomPort" && targetPort == "TopPort" && deltaY > 0))
            {
                needsIntermediatePoint = false;
            }

            if (needsIntermediatePoint)
            {
                // 根据源端口方向和相对位置选择中间点策略
                bool isVerticalSource = sourcePort == "TopPort" || sourcePort == "BottomPort";
                
                if (isVerticalSource)
                {
                    // 源端口是垂直方向
                    if (Math.Abs(deltaX) > 2 * Math.Abs(deltaY))
                    {
                        // 水平偏移远大于垂直偏移，使用水平主导的路径
                        pathPoints.Add(new Point(source.X + deltaX / 2, source.Y));
                        pathPoints.Add(new Point(source.X + deltaX / 2, target.Y));
                    }
                    else
                    {
                        // 垂直主导，使用垂直路径
                        pathPoints.Add(new Point(source.X, source.Y + deltaY / 2));
                        pathPoints.Add(new Point(target.X, source.Y + deltaY / 2));
                    }
                }
                else
                {
                    // 源端口是水平方向
                    if (Math.Abs(deltaY) > 2 * Math.Abs(deltaX))
                    {
                        // 垂直偏移远大于水平偏移，使用垂直主导的路径
                        pathPoints.Add(new Point(source.X, source.Y + deltaY / 2));
                        pathPoints.Add(new Point(target.X, source.Y + deltaY / 2));
                    }
                    else
                    {
                        // 水平主导，使用水平路径
                        pathPoints.Add(new Point(source.X + deltaX / 2, source.Y));
                        pathPoints.Add(new Point(source.X + deltaX / 2, target.Y));
                    }
                }
            }

            // 添加终点
            pathPoints = new List<Point>(pathPoints);
            pathPoints.Add(target);

            return pathPoints;
        }

        /// <summary>
        /// 计算箭头的旋转角度
        /// </summary>
        public static double CalculateArrowAngle(Point source, Point target)
        {
            double deltaX = target.X - source.X;
            double deltaY = target.Y - source.Y;
            
            // 计算角度（弧度）
            double angleRadians = Math.Atan2(deltaY, deltaX);
            
            // 转换为角度
            double angleDegrees = angleRadians * 180 / Math.PI;
            
            return angleDegrees;
        }

        /// <summary>
        /// 刷新所有连接路径
        /// </summary>
        public static void RefreshAllConnectionPaths(IEnumerable<WorkflowConnection> connections)
        {
            if (connections == null) return;

            // 触发所有连接的属性变化，强制刷新UI
            foreach (var connection in connections)
            {
                // 触发 SourcePosition 变化，导致转换器重新计算
                var oldPos = connection.SourcePosition;
                connection.SourcePosition = new System.Windows.Point(oldPos.X + 0.001, oldPos.Y);
                connection.SourcePosition = oldPos;
            }
        }
    }
}
