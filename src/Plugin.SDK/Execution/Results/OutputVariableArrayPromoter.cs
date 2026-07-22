using System;

namespace SunEyeVision.Plugin.SDK.Execution.Results
{
    /// <summary>
    /// 输出变量数组提升器（IO 边界治本）。
    /// 把标量运行期值统一包装成"长度 1 的数组"，消除"契约数组 / 实际单值"背离，
    /// 对齐 Halcon tuple 语义与 SdkTypeVocabulary 注释（单值 = 长度 1 的数组）。
    /// 图像不包装、已是数组的不包装、无值不造数组（rule-008 零兼容 / rule-015 报错优先）。
    /// </summary>
    internal static class OutputVariableArrayPromoter
    {
        public static object? Promote(object? runtimeValue, string variableType, Type clrPropertyType)
        {
            if (variableType == "Image") return runtimeValue;   // 图像不包装
            if (clrPropertyType.IsArray) return runtimeValue;   // 已是数组
            if (runtimeValue == null) return null;              // 无值不造数组

            var elemType = clrPropertyType;
            if (elemType.IsGenericType && elemType.GetGenericTypeDefinition() == typeof(Nullable<>))
                elemType = Nullable.GetUnderlyingType(elemType)!;

            var arr = Array.CreateInstance(elemType, 1);
            arr.SetValue(runtimeValue, 0);
            return arr;
        }
    }
}
