using System;
using System.ComponentModel;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Roi.Logic
{
    /// <summary>
    /// 数据类型转换器 - 处理不同数据类型之间的转换
    /// </summary>
    public static class DataTypeConverter
    {
        /// <summary>
        /// 转换值到目标类型
        /// </summary>
        public static T? ConvertTo<T>(object? value)
        {
            if (value == null)
                return default;

            try
            {
                var targetType = typeof(T);

                // 如果已经是目标类型
                if (targetType.IsAssignableFrom(value.GetType()))
                {
                    return (T)value;
                }

                // 使用TypeConverter转换
                var converter = TypeDescriptor.GetConverter(targetType);
                if (converter.CanConvertFrom(value.GetType()))
                {
                    return (T?)converter.ConvertFrom(value);
                }

                // 尝试直接转换
                return (T?)Convert.ChangeType(value, targetType);
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// 转换值到目标类型
        /// </summary>
        public static object? ConvertTo(object? value, Type targetType)
        {
            if (value == null)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            try
            {
                // 如果已经是目标类型
                if (targetType.IsAssignableFrom(value.GetType()))
                {
                    return value;
                }

                // 使用TypeConverter转换
                var converter = TypeDescriptor.GetConverter(targetType);
                if (converter.CanConvertFrom(value.GetType()))
                {
                    return converter.ConvertFrom(value);
                }

                // 尝试直接转换
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }
        }

        /// <summary>
        /// 验证值是否符合目标类型
        /// </summary>
        public static bool ValidateType(object? value, Type targetType)
        {
            if (value == null)
                return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;

            try
            {
                // 检查是否可以直接赋值
                if (targetType.IsAssignableFrom(value.GetType()))
                    return true;

                // 尝试转换
                var converter = TypeDescriptor.GetConverter(targetType);
                if (converter.CanConvertFrom(value.GetType()))
                {
                    converter.ConvertFrom(value);
                    return true;
                }

                // 尝试直接转换
                Convert.ChangeType(value, targetType);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取类型的友好名称
        /// </summary>
        public static string GetTypeFriendlyName(Type type)
        {
            if (type == null)
                return "Unknown";

            // 处理Nullable类型
            var underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
            {
                return $"{GetTypeFriendlyName(underlyingType)}?";
            }

            // 常见类型的友好名称
            var friendlyNames = new System.Collections.Generic.Dictionary<Type, string>
            {
                { typeof(int), "int" },
                { typeof(long), "long" },
                { typeof(short), "short" },
                { typeof(byte), "byte" },
                { typeof(float), "float" },
                { typeof(double), "double" },
                { typeof(decimal), "decimal" },
                { typeof(bool), "bool" },
                { typeof(string), "string" },
                { typeof(char), "char" },
                { typeof(object), "object" }
            };

            if (friendlyNames.TryGetValue(type, out var friendlyName))
            {
                return friendlyName;
            }

            return type.Name;
        }

        /// <summary>
        /// 判断类型是否为数值类型
        /// </summary>
        public static bool IsNumericType(Type type)
        {
            if (type == null)
                return false;

            var numericTypes = new[]
            {
                typeof(int), typeof(long), typeof(short), typeof(byte),
                typeof(float), typeof(double), typeof(decimal),
                typeof(uint), typeof(ulong), typeof(ushort), typeof(sbyte)
            };

            // 处理Nullable类型
            var underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
            {
                type = underlyingType;
            }

            return Array.IndexOf(numericTypes, type) >= 0;
        }

        /// <summary>
        /// 判断两个类型是否兼容
        /// </summary>
        public static bool AreTypesCompatible(Type sourceType, Type targetType)
        {
            if (sourceType == null || targetType == null)
                return false;

            // 完全相同
            if (sourceType == targetType)
                return true;

            // 目标类型是object
            if (targetType == typeof(object))
                return true;

            // 数值类型之间兼容
            if (IsNumericType(sourceType) && IsNumericType(targetType))
                return true;

            // 继承关系
            if (targetType.IsAssignableFrom(sourceType))
                return true;

            // Nullable类型处理
            var sourceUnderlying = Nullable.GetUnderlyingType(sourceType);
            var targetUnderlying = Nullable.GetUnderlyingType(targetType);

            if (sourceUnderlying != null && targetUnderlying != null)
            {
                return AreTypesCompatible(sourceUnderlying, targetUnderlying);
            }

            if (sourceUnderlying != null)
            {
                return AreTypesCompatible(sourceUnderlying, targetType);
            }

            if (targetUnderlying != null)
            {
                return AreTypesCompatible(sourceType, targetUnderlying);
            }

            return false;
        }
    }
}
