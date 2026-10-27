using System.Windows;
using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Path;

namespace SunEyeVision.UI.Services.Path
{
    /// <summary>
    /// 路径计算器接口 - 定义正交折线路径计算的契约
    /// </summary>
    public interface IPathCalculator
    {
        /// <summary>新建连线预览：鼠标为自由终点，源节点边界必须取实际模板尺寸。</summary>
        Point[] CalculatePreviewPath(Point sourcePosition, Point pointer, PortDirection sourceDirection, Rect sourceNodeRect);

        /// <summary>
        /// 计算正交折线路径点集合（增强方法，带节点信息）
        /// </summary>
        /// <param name="sourcePosition">源端口位置</param>
        /// <param name="targetPosition">目标端口位置</param>
        /// <param name="sourceDirection">源端口方向</param>
        /// <param name="targetDirection">目标端口方向</param>
        /// <param name="sourceNodeRect">源节点边界矩形</param>
        /// <param name="targetNodeRect">目标节点边界矩形</param>
        /// <param name="allNodeRects">所有节点的边界矩形（用于碰撞检测）</param>
        /// <returns>路径点集合（包括起点和终点）</returns>
        Point[] CalculateOrthogonalPath(
            Point sourcePosition,
            Point targetPosition,
            PortDirection sourceDirection,
            PortDirection targetDirection,
            Rect sourceNodeRect,
            Rect targetNodeRect,
            params Rect[] allNodeRects);

        /// <summary>
        /// 根据路径点创建路径几何
        /// </summary>
        /// <param name="pathPoints">路径点集合</param>
        /// <returns>PathGeometry对象</returns>
        PathGeometry CreatePathGeometry(Point[] pathPoints);

        /// <summary>
        /// 计算箭头位置和角度
        /// </summary>
        /// <param name="pathPoints">路径点集合</param>
        /// <param name="targetPosition">目标端口位置（箭头尖端应该到达的位置）</param>
        /// <param name="targetDirection">目标端口方向，决定箭头的固定角度</param>
        /// <returns>箭头位置和角度（角度为度数）</returns>
        (Point position, double angle) CalculateArrow(Point[] pathPoints, Point targetPosition, PortDirection targetDirection);
    }

    /// <summary>
    /// 端口方向枚举
    /// </summary>
    public enum PortDirection
    {
        /// <summary>
        /// 上方端口
        /// </summary>
        Top,

        /// <summary>
        /// 下方端口
        /// </summary>
        Bottom,

        /// <summary>
        /// 左侧端口
        /// </summary>
        Left,

        /// <summary>
        /// 右侧端口
        /// </summary>
        Right
    }

    /// <summary>
    /// 端口方向扩展方法
    /// </summary>
    public static class PortDirectionExtensions
    {
        /// <summary>
        /// 根据端口名称获取端口方向
        /// </summary>
        public static PortDirection FromPortName(string portName)
        {
            return portName?.ToLower() switch
            {
                "top" or "topport" => PortDirection.Top,
                "bottom" or "bottomport" => PortDirection.Bottom,
                "left" or "leftport" => PortDirection.Left,
                "right" or "rightport" => PortDirection.Right,
                _ => PortDirection.Right // 默认为右侧
            };
        }

        /// <summary>
        /// 获取端口的水平移动方向（1为正向，-1为反向，0为无移动）
        /// </summary>
        public static int HorizontalMove(this PortDirection direction)
        {
            return direction switch
            {
                PortDirection.Right => 1,
                PortDirection.Left => -1,
                _ => 0
            };
        }

        /// <summary>
        /// 获取端口的垂直移动方向（1为正向，-1为反向，0为无移动）
        /// </summary>
        public static int VerticalMove(this PortDirection direction)
        {
            return direction switch
            {
                PortDirection.Bottom => 1,
                PortDirection.Top => -1,
                _ => 0
            };
        }

        /// <summary>
        /// 获取端口的主方向（水平或垂直）
        /// </summary>
        public static bool IsHorizontal(this PortDirection direction)
        {
            return direction == PortDirection.Left || direction == PortDirection.Right;
        }

        /// <summary>
        /// 获取端口的主方向（水平或垂直）
        /// </summary>
        public static bool IsVertical(this PortDirection direction)
        {
            return direction == PortDirection.Top || direction == PortDirection.Bottom;
        }
    }
}
