using System;
using System.Windows;
using System.Windows.Media;

namespace SunEyeVision.UI.Shared
{
    /// <summary>
    /// 节点样式配置 - 完全解耦样式与逻辑
    /// 从ResourceDictionary统一管理样式参数
    /// </summary>
    public class NodeStyleConfig
    {
        private static readonly ResourceDictionary _resources;

        static NodeStyleConfig()
        {
            try
            {
                _resources = new ResourceDictionary
                {
                    Source = new Uri("/SunEyeVision.UI;component/Resources/Styles/NodeStyleResources.xaml",
                                    UriKind.Relative)
                };
            }
            catch (Exception ex)
            {
                // 降级到硬编码默认值
                _resources = new ResourceDictionary();
                _resources["NodeWidth"] = 160.0;
                _resources["NodeHeight"] = 40.0;
                _resources["PortSize"] = 10.0;
                _resources["PortMargin"] = 10.0;
                _resources["NodeCornerRadius"] = 6.0;
                _resources["NodeContentMargin"] = new Thickness(0, 0, 12, 0);
}
        }

        private T GetResource<T>(string key) where T : class
        {
            return _resources[key] as T;
        }

        private double GetDouble(string key)
        {
            return (double)_resources[key];
        }

        private Thickness GetThickness(string key)
        {
            return (Thickness)_resources[key];
        }

        /// <summary>
        /// 节点宽度（从资源读取）
        /// </summary>
        public double NodeWidth => GetDouble("NodeWidth");

        /// <summary>
        /// 节点高度（从资源读取）
        /// </summary>
        public double NodeHeight => GetDouble("NodeHeight");

        /// <summary>
        /// 端口大小（直径）（从资源读取）
        /// </summary>
        public double PortSize => GetDouble("PortSize");

        /// <summary>
        /// 端口外边距（端口距离节点的距离）（从资源读取）
        /// </summary>
        public double PortMargin => GetDouble("PortMargin");

        /// <summary>
        /// 节点圆角半径（从资源读取）
        /// </summary>
        public double CornerRadius => GetDouble("NodeCornerRadiusDouble");

        /// <summary>
        /// 节点内容边距（从资源读取）
        /// </summary>
        public double ContentMargin => GetThickness("NodeContentMargin").Left;

        /// <summary>
        /// 芯片厚度
        /// </summary>
        public double ChipThickness { get; set; } = 2;

        /// <summary>
        /// 验证配置的有效性
        /// </summary>
        public void Validate()
        {
            if (NodeWidth <= 0)
                throw new InvalidOperationException("节点宽度必须大于0");
            if (NodeHeight <= 0)
                throw new InvalidOperationException("节点高度必须大于0");
            if (PortSize <= 0)
                throw new InvalidOperationException("端口大小必须大于0");
            if (PortMargin < 0)
                throw new InvalidOperationException("端口外边距不能为负数");
        }

        /// <summary>
        /// 计算节点的边界矩形（Position为HitArea中心点）
        /// </summary>
        public Rect GetNodeRect(Point position)
        {
            return new Rect(
                position.X - NodeWidth / 2,
                position.Y - NodeHeight / 2,
                NodeWidth,
                NodeHeight
            );
        }

        /// <summary>
        /// 计算节点中心点（Position本身就是中心点）
        /// </summary>
        public Point GetNodeCenter(Point position)
        {
            return position;
        }

        /// <summary>
        /// 计算上端口中心位置
        /// </summary>
        public Point GetTopPortPosition(Point position)
        {
            return new Point(position.X, position.Y - NodeHeight / 2);
        }

        /// <summary>
        /// 计算下端口中心位置
        /// </summary>
        public Point GetBottomPortPosition(Point position)
        {
            return new Point(position.X, position.Y + NodeHeight / 2);
        }

        /// <summary>
        /// 计算左端口中心位置
        /// </summary>
        public Point GetLeftPortPosition(Point position)
        {
            return new Point(position.X - NodeWidth / 2, position.Y);
        }

        /// <summary>
        /// 计算右端口中心位置
        /// </summary>
        public Point GetRightPortPosition(Point position)
        {
            return new Point(position.X + NodeWidth / 2, position.Y);
        }
    }

    /// <summary>
    /// 预定义的节点样式（保留以提供快速样式切换）
    /// 注意：这些样式现在通过修改ResourceDictionary来实现
    /// </summary>
    public static class NodeStyles
    {
        /// <summary>
        /// 标准节点样式（当前默认，从资源读取）
        /// </summary>
        public static readonly NodeStyleConfig Standard = new NodeStyleConfig();

        /// <summary>
        /// 紧凑节点样式（小尺寸）
        /// 注意：需要通过ResourceDictionary切换主题或样式
        /// </summary>
        public static readonly NodeStyleConfig Compact = new NodeStyleConfig
        {
            // 紧凑样式暂不实现，等待主题系统完善
        };

        /// <summary>
        /// 大型节点样式（大尺寸）
        /// 注意：需要通过ResourceDictionary切换主题或样式
        /// </summary>
        public static readonly NodeStyleConfig Large = new NodeStyleConfig
        {
            // 大型样式暂不实现，等待主题系统完善
        };
    }
}
