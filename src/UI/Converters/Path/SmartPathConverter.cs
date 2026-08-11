using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Connection;

namespace SunEyeVision.UI.Converters.Path
{
    /// <summary>
    /// 智能路径转换器 - 将 WorkflowConnection 转换为 Path Data
    /// 使用 ConnectionPathCache 统一计算路径
    /// </summary>
    public class SmartPathConverter : IValueConverter
    {
        /// <summary>
        /// 节点集合（静态）
        /// </summary>
        public static ObservableCollection<WorkflowNode>? Nodes { get; set; }

        /// <summary>
        /// 连接集合（静态）
        /// </summary>
        public static ObservableCollection<WorkflowConnection>? Connections { get; set; }

        /// <summary>
        /// 连接线路径缓存（静态）
        /// </summary>
        public static ConnectionPathCache? PathCache { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not WorkflowConnection connection)
            {
                return string.Empty;
            }

            // 统一使用 PathCache 计算路径
            if (PathCache == null)
            {
                return string.Empty;
            }

            try
            {
                return PathCache.GetPathData(connection) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
