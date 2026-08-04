using System;

namespace SunEyeVision.Plugin.SDK.Execution.Results
{
    /// <summary>
    /// 输出变量数组提升器（IO 边界治本）。
    /// 把标量运行期值统一包装成"长度 1 的数组"，消除"契约数组 / 实际单值"背离，
    /// 对齐 Halcon tuple 语义与 SdkTypeVocabulary 注释（单值 = 长度 1 的数组）。
    /// 判定基于运行时值形态而非目标类型：值为数组保持，值为单对象包成 T[1]，
    /// 从而兼容动态生产者（脚本/子程序）在中间环节被解包后的重新包装。
    /// 图像不包装、已是数组的不包装、无值不造数组（rule-008 零兼容 / rule-015 报错优先）。
    /// </summary>
    public static class OutputVariableArrayPromoter
    {
        public static object? Promote(object? runtimeValue, string variableType, Type clrPropertyType)
        {
            if (variableType == "Image") return runtimeValue;   // 图像不包装
            if (runtimeValue == null) return null;              // 无值不造数组
            if (runtimeValue is Array) return runtimeValue;     // 值形态优先：运行时值已是数组则保持

            var elemType = clrPropertyType.IsArray ? clrPropertyType.GetElementType()! : clrPropertyType;
            if (elemType.IsGenericType && elemType.GetGenericTypeDefinition() == typeof(Nullable<>))
                elemType = Nullable.GetUnderlyingType(elemType)!;

            var arr = Array.CreateInstance(elemType, 1);
            arr.SetValue(runtimeValue, 0);
            return arr;
        }
    }
}
