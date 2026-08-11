using System.Windows;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Services.Canvas
{
    /// <summary>
    /// 画布配置 - 集中管理画布的配置参数
    /// </summary>
    public static class CanvasConfig
    {
        /// <summary>
        /// 节点配置
        /// </summary>
        public static class Node
        {
            /// <summary>
            /// 节点默认宽度
            /// </summary>
            public const double DefaultWidth = 120;

            /// <summary>
            /// 节点默认高度
            /// </summary>
            public const double DefaultHeight = 80;

            /// <summary>
            /// 节点最小宽度
            /// </summary>
            public const double MinWidth = 80;

            /// <summary>
            /// 节点最小高度
            /// </summary>
            public const double MinHeight = 60;

            /// <summary>
            /// 节点最大宽度
            /// </summary>
            public const double MaxWidth = 300;

            /// <summary>
            /// 节点最大高度
            /// </summary>
            public const double MaxHeight = 200;

            /// <summary>
            /// 节点圆角半径
            /// </summary>
            public const double CornerRadius = 5;

            /// <summary>
            /// 节点阴影模糊半径
            /// </summary>
            public const double ShadowBlurRadius = 5;

            /// <summary>
            /// 节点阴影偏移
            /// </summary>
            public static readonly Vector ShadowOffset = new Vector(2, 2);

            /// <summary>
            /// 节点选中边框粗细
            /// </summary>
            public const double SelectionBorderThickness = 2;

            /// <summary>
            /// 节点默认背景色
            /// </summary>
            public static readonly string DefaultBackgroundColor = "#FFFFFF";

            /// <summary>
            /// 节点选中背景色
            /// </summary>
            public static readonly string SelectedBackgroundColor = "#E3F2FD";

            /// <summary>
            /// 节点悬停背景色
            /// </summary>
            public static readonly string HoverBackgroundColor = "#F5F5F5";
        }

        /// <summary>
        /// 端口配置
        /// </summary>
        public static class Port
        {
            /// <summary>
            /// 端口半径
            /// </summary>
            public const double Radius = 6;

            /// <summary>
            /// 端口默认颜色
            /// </summary>
            public static readonly string DefaultColor = "#90CAF9";

            /// <summary>
            /// 端口高亮颜色
            /// </summary>
            public static readonly string HighlightColor = "#42A5F5";

            /// <summary>
            /// 端口悬停颜色
            /// </summary>
            public static readonly string HoverColor = "#64B5F6";

            /// <summary>
            /// 端口到节点边缘的距离
            /// </summary>
            public const double DistanceToEdge = 0;

            /// <summary>
            /// 端口命中测试距离
            /// </summary>
            public const double PortHitTestDistance = 15;
            public const double EdgeDistance = 0;

            /// <summary>
            /// 端口可见性
            /// </summary>
            public const bool IsVisible = true;

            /// <summary>
            /// 活动端口颜色
            /// </summary>
            public static readonly string ActivePortColor = "#FF9800";

            /// <summary>
            /// 端口悬停大小
            /// </summary>
            public const double PortHoverSize = 2;
        }

        /// <summary>
        /// 连接线配置
        /// </summary>
        public static class Connection
        {
            /// <summary>
            /// 连接线默认粗细
            /// </summary>
            public const double DefaultThickness = 2;

            /// <summary>
            /// 连接线选中粗细
            /// </summary>
            public const double SelectedThickness = 3;

            /// <summary>
            /// 连接线默认颜色
            /// </summary>
            public static readonly string DefaultColor = "#90CAF9";

            /// <summary>
            /// 连接线选中颜色
            /// </summary>
            public static readonly string SelectedColor = "#42A5F5";

            /// <summary>
            /// 连接线悬停颜色
            /// </summary>
            public static readonly string HoverColor = "#64B5F6";

            /// <summary>
            /// 箭头大小
            /// </summary>
            public const double ArrowSize = 10;

            /// <summary>
            /// 箭头颜色
            /// </summary>
            public static readonly string ArrowColor = "#90CAF9";

            /// <summary>
            /// 路径拐点圆角半径
            /// </summary>
            public const double CornerRadius = 0;

            /// <summary>
            /// 中间点大小
            /// </summary>
            public const double MidpointSize = 6;

            /// <summary>
            /// 中间点颜色
            /// </summary>
            public static readonly string MidpointColor = "#90CAF9";
        }

        /// <summary>
        /// 框选配置
        /// </summary>
        public static class Selection
        {
            /// <summary>
            /// 框选矩形边框粗细
            /// </summary>
            public const double BorderThickness = 1;

            /// <summary>
            /// 框选矩形边框颜色
            /// </summary>
            public static readonly string BorderColor = "#42A5F5";

            /// <summary>
            /// 框选矩形填充颜色
            /// </summary>
            public static readonly string FillColor = "#E3F2FD";

            /// <summary>
            /// 框选矩形透明度
            /// </summary>
            public const double FillOpacity = 0.3;

            /// <summary>
            /// 最小框选区域大小
            /// </summary>
            public const double MinSelectionSize = 5;
        }

        /// <summary>
        /// 拖拽配置
        /// </summary>
        public static class Drag
        {
            /// <summary>
            /// 拖拽阈值（像素）
            /// </summary>
            public const double Threshold = 3;

            /// <summary>
            /// 拖拽时节点透明度
            /// </summary>
            public const double NodeOpacity = 0.8;

            /// <summary>
            /// 是否启用吸附功能
            /// </summary>
            public const bool EnableSnap = false;

            /// <summary>
            /// 吸附网格大小
            /// </summary>
            public const double SnapGridSize = 10;

            /// <summary>
            /// 吸附距离
            /// </summary>
            public const double SnapDistance = 10;

            /// <summary>
            /// 网格大小（别名）
            /// </summary>
            public const double GridSize = SnapGridSize;
        }

        /// <summary>
        /// 缩放配置
        /// </summary>
        public static class Zoom
        {
            /// <summary>
            /// 最小缩放比例
            /// </summary>
            public const double MinScale = 0.1;

            /// <summary>
            /// 最大缩放比例
            /// </summary>
            public const double MaxScale = 5.0;

            /// <summary>
            /// 默认缩放比例
            /// </summary>
            public const double DefaultScale = 1.0;

            /// <summary>
            /// 缩放步长
            /// </summary>
            public const double Step = 0.1;

            /// <summary>
            /// 是否启用鼠标滚轮缩放
            /// </summary>
            public const bool EnableMouseWheelZoom = true;

            /// <summary>
            /// 缩放中心点
            /// </summary>
            public static readonly Point CenterPoint = new Point(0, 0);
        }

        /// <summary>
        /// 平移配置
        /// </summary>
        public static class Pan
        {
            /// <summary>
            /// 是否启用平移
            /// </summary>
            public const bool Enabled = true;

            /// <summary>
            /// 平移速度
            /// </summary>
            public const double Speed = 1.0;

            /// <summary>
            /// 是否启用空格键平移
            /// </summary>
            public const bool EnableSpaceKeyPan = true;

            /// <summary>
            /// 是否启用中键平移
            /// </summary>
            public const bool EnableMiddleButtonPan = true;
        }

        /// <summary>
        /// 缓存配置
        /// </summary>
        public static class Cache
        {
            /// <summary>
            /// 路径缓存最大大小
            /// </summary>
            public const int MaxPathCacheSize = 1000;

            /// <summary>
            /// 是否启用路径缓存
            /// </summary>
            public const bool EnablePathCache = true;

            /// <summary>
            /// 是否启用空间索引
            /// </summary>
            public const bool EnableSpatialIndex = true;

            /// <summary>
            /// 空间索引网格大小
            /// </summary>
            public const double SpatialIndexGridSize = 100;
        }

        /// <summary>
        /// 性能配置
        /// </summary>
        public static class Performance
        {
            /// <summary>
            /// 是否启用虚拟化
            /// </summary>
            public const bool EnableVirtualization = false;

            /// <summary>
            /// 可见区域外缓冲区大小
            /// </summary>
            public const double VisibleBuffer = 100;

            /// <summary>
            /// 最大渲染节点数
            /// </summary>
            public const int MaxRenderedNodes = 1000;

            /// <summary>
            /// 最大渲染连接数
            /// </summary>
            public const int MaxRenderedConnections = 2000;

            /// <summary>
            /// 路径计算节流时间（毫秒）
            /// </summary>
            public const int PathCalculationThrottle = 16;
        }

        /// <summary>
        /// 调试配置
        /// </summary>
        public static class Debug
        {
            /// <summary>
            /// 是否启用调试日志
            /// </summary>
            public const bool EnableLogging = false;

            /// <summary>
            /// 是否显示边界矩形
            /// </summary>
            public const bool ShowBoundingRect = false;

            /// <summary>
            /// 是否显示端口位置
            /// </summary>
            public const bool ShowPortPositions = false;

            /// <summary>
            /// 是否显示连接线信息
            /// </summary>
            public const bool ShowConnectionInfo = false;
        }

        // 向后兼容的属性（从NodeStyleConfig读取）
        public static double NodeWidth => NodeStyles.Standard.NodeWidth;
        public static double NodeHeight => NodeStyles.Standard.NodeHeight;
    }
}
