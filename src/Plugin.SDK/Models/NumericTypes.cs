using System;

namespace SunEyeVision.Plugin.SDK.Models;

/// <summary>
/// 数字类型工具类 — 集中所有"是否为数字类型"的判断逻辑
/// </summary>
/// <remarks>
/// 单一来源原则：UI 筛选（ParamBindingBase）、绑定执行（ScriptBindingResolver）
/// 等所有"是否数字类型"判断都走此静态方法，避免定义散落。
/// </remarks>
public static class NumericTypes
{
    /// <summary>
    /// 判断类型是否为数值类型（int/long/float/double/decimal/short/byte/uint/ulong/ushort/sbyte）
    /// </summary>
    public static bool IsNumericType(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(float)
        || type == typeof(double) || type == typeof(decimal)
        || type == typeof(short) || type == typeof(byte)
        || type == typeof(uint) || type == typeof(ulong)
        || type == typeof(ushort) || type == typeof(sbyte);
}
