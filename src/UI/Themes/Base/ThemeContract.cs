using System;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace SunEyeVision.UI.Themes.Base
{
    /// <summary>
    /// 唯一主题字典的语义键契约校验。
    /// VM 原件只有一套固定主题，不提供运行时切换；主题字典不再是"可切换资源"，
    /// 但键缺失仍会让控件静默回退到默认色，因此启动时必须逐键校验并直接报错。
    /// </summary>
    internal static class ThemeContract
    {
        internal static void Validate(ResourceDictionary theme)
        {
            ArgumentNullException.ThrowIfNull(theme);
            var missing = typeof(ThemeKeys)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue()!)
                .Where(key => !theme.Contains(key))
                .ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    $"主题 key 契约校验失败，以下 key 未在当前主题字典 {theme.Source} 中定义，部分控件将显示错误的颜色: "
                    + string.Join(", ", missing));
            }
        }
    }
}
