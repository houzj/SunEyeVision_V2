using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Adapters;

namespace SunEyeVision.UI.Services.Rendering
{
    /// <summary>
    /// 节点转换器缓存 - 避免重复创建SolidColorBrush
    /// 预定义常用颜色并Freeze，提升渲染性能
    /// </summary>
    public static class CachedBrushes
    {
        // 预定义常用节点背景色（已Freeze，线程安全）
        private static readonly SolidColorBrush _whiteBrush;
        private static readonly SolidColorBrush _lightBlueBrush;
        private static readonly SolidColorBrush _lightGreenBrush;
        private static readonly SolidColorBrush _lightYellowBrush;
        private static readonly SolidColorBrush _lightOrangeBrush;
        private static readonly SolidColorBrush _lightPurpleBrush;
        private static readonly SolidColorBrush _lightRedBrush;

        // 预定义常用节点边框色
        private static readonly SolidColorBrush _orangeBorderBrush;
        private static readonly SolidColorBrush _blueBorderBrush;
        private static readonly SolidColorBrush _grayBorderBrush;
        private static readonly SolidColorBrush _crimsonBorderBrush;
        private static readonly SolidColorBrush _greenBorderBrush;
        private static readonly SolidColorBrush _purpleBorderBrush;

        // 动态缓存（用于非常用颜色）
        private static readonly ConcurrentDictionary<Color, SolidColorBrush> _brushCache = new();

        static CachedBrushes()
        {
            // 初始化并Freeze所有预定义画刷
            _whiteBrush = CreateFrozenBrush(Colors.White);
            _lightBlueBrush = CreateFrozenBrush(Color.FromRgb(230, 242, 255)); // #E6F2FF
            _lightGreenBrush = CreateFrozenBrush(Color.FromRgb(230, 255, 230)); // #E6FFE6
            _lightYellowBrush = CreateFrozenBrush(Color.FromRgb(255, 250, 230)); // #FFFAE6
            _lightOrangeBrush = CreateFrozenBrush(Color.FromRgb(255, 245, 230)); // #FFF5E6
            _lightPurpleBrush = CreateFrozenBrush(Color.FromRgb(245, 240, 255)); // #F5F0FF
            _lightRedBrush = CreateFrozenBrush(Color.FromRgb(255, 240, 240)); // 淡红色背景

            _orangeBorderBrush = CreateFrozenBrush(Color.FromRgb(255, 149, 0)); // #ff9500
            _blueBorderBrush = CreateFrozenBrush(Color.FromRgb(0, 102, 204)); // #0066CC
            _grayBorderBrush = CreateFrozenBrush(Color.FromRgb(200, 200, 200)); // #C8C8C8
            _crimsonBorderBrush = CreateFrozenBrush(Color.FromRgb(220, 20, 60)); // 猩红
            _greenBorderBrush = CreateFrozenBrush(Color.FromRgb(34, 139, 34)); // 森林绿
            _purpleBorderBrush = CreateFrozenBrush(Color.FromRgb(128, 0, 128)); // 紫色
        }

        private static SolidColorBrush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// 获取或创建缓存的SolidColorBrush
        /// </summary>
        public static SolidColorBrush GetOrCreateBrush(Color color)
        {
            return _brushCache.GetOrAdd(color, c =>
            {
                var brush = new SolidColorBrush(c);
                brush.Freeze();
                return brush;
            });
        }

        // 预定义画刷访问器
        public static SolidColorBrush White => _whiteBrush;
        public static SolidColorBrush LightBlue => _lightBlueBrush;
        public static SolidColorBrush LightGreen => _lightGreenBrush;
        public static SolidColorBrush LightYellow => _lightYellowBrush;
        public static SolidColorBrush LightOrange => _lightOrangeBrush;
        public static SolidColorBrush LightPurple => _lightPurpleBrush;
        public static SolidColorBrush LightRed => _lightRedBrush;
        public static SolidColorBrush OrangeBorder => _orangeBorderBrush;
        public static SolidColorBrush BlueBorder => _blueBorderBrush;
        public static SolidColorBrush GrayBorder => _grayBorderBrush;
        public static SolidColorBrush CrimsonBorder => _crimsonBorderBrush;
        public static SolidColorBrush GreenBorder => _greenBorderBrush;
        public static SolidColorBrush PurpleBorder => _purpleBorderBrush;
    }

    /// <summary>
    /// 缓存版节点背景颜色转换器 - 使用适配器获取背景色，然后使用缓存画刷
    /// </summary>
    public class CachedNodeBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WorkflowNode node)
            {
                var adapter = NodeDisplayAdapterFactory.GetAdapter(node.ToolType);
                var color = adapter.GetBackgroundColor(node);
                return CachedBrushes.GetOrCreateBrush(color);
            }
            return CachedBrushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 缓存版节点边框颜色转换器：未选中 → 灰；选中 → 橙。
    /// 遵循视觉软件行业惯例，默认灰色边框、选中橙色高亮，与虚线选中装饰层一致。
    /// 绑定源为节点 IsSelected（bool），属性变化自动触发重求值。
    /// </summary>
    public class CachedNodeBorderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool isSelected && isSelected
                ? (Brush)CachedBrushes.OrangeBorder
                : (Brush)CachedBrushes.GrayBorder;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("CachedNodeBorderConverter 不支持反向转换。");
        }
    }
}
