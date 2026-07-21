using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Plugin.SDK.Models
{
    public static class SdkTypeVocabulary
    {
        /// <summary>
        /// 所有类型只注册数组版本（单值 = 长度为 1 的数组，对齐 Halcon tuple 语义）。
        /// 数值类型归并为规范键：int/int[]/float/float[] → long/long[]、double/double[]，
        /// 仅保留 7 个核心概念：整型/双精度/字符串/布尔/图像/RoI/变换矩阵。
        /// Region（算法区域，iconic 运行时数据）禁止进入工作流契约，仅活在算法工具内部，
        /// 由 RegionConverter 桥接为 RoI 外发（方案 D8）。
        /// 图像具体实现类型 OpenCvSharp.Mat 映射到 "Image"（词汇表唯一图像入口）；
        /// 下游 ToSystemType("Image") 仍返回 IVisionImage（过滤/绑定用的抽象类型）。
        /// </summary>
        private static readonly Dictionary<Type, string> TypeToString = new()
        {
            [typeof(long[])]          = "Integer",
            [typeof(double[])]        = "Double",
            [typeof(string[])]        = "String",
            [typeof(bool[])]          = "Boolean",
            [typeof(AffineMatrix[])]  = "Transform",
            [typeof(IVisionImage)]    = "Image",
            [typeof(Mat)]             = "Image",
            [typeof(ShapeParameters[])] = "RoI",
        };

        /// <summary>
        /// 字符串→CLR 类型反向映射。规范键映射到规范类型（数值归并为 long/double）。
        /// 保留历史存量类型名（"Xxx[]"、Float、Rectangle）以便旧方案数据可识别，
        /// 但 Byte 及 6 几何类型已删除，触发 NotSupportedException 可见（rule-015）。
        /// </summary>
        private static readonly Dictionary<string, Type> StringToType = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Integer"]   = typeof(long),
            ["String"]    = typeof(string),
            ["Boolean"]   = typeof(bool),
            ["Double"]    = typeof(double),
            ["Transform"] = typeof(AffineMatrix),
            ["Image"]     = typeof(IVisionImage),
            ["RoI"]       = typeof(ShapeParameters[]),
            // 存量兼容：历史 JSON 使用 "Xxx[]" 类型名（单值 = 长度 1 数组）。
            ["Integer[]"] = typeof(int[]),
            ["Double[]"]  = typeof(double[]),
            ["String[]"]  = typeof(string[]),
            ["Float[]"]   = typeof(float[]),
            ["Float"]     = typeof(float),
        };

        public static readonly Dictionary<string, string> ChineseNames = new()
        {
            ["Integer"]   = "整型",
            ["String"]    = "字符串",
            ["Boolean"]   = "布尔",
            ["Double"]    = "双精度",
            ["Image"]     = "图像",
            ["RoI"]       = "图形定义",
            ["Transform"] = "变换矩阵",
        };

        /// <summary>
        /// 数值类型规范归并：int→long、int[]→long[]、float→double、float[]→double[]，否则原样。
        /// </summary>
        private static Type NormalizeNumeric(Type t)
        {
            if (t == typeof(int)) return typeof(long);
            if (t == typeof(int[])) return typeof(long[]);
            if (t == typeof(float)) return typeof(double);
            if (t == typeof(float[])) return typeof(double[]);
            return t;
        }

        /// <summary>
        /// 获取类型对应的全局变量类型名称。
        /// 传入单值类型（如 double）自动提升为数组类型（如 "Double"）。
        /// 传入 int/float 等会先规范归并为 long/double（如 int → "Integer"）。
        /// 支持 List&lt;T&gt; → T[] 自动拆箱，以及 ShapeParameters[] → ShapeParameters 递归归并。
        /// </summary>
        public static string? ToTypeVocabulary(Type? type)
        {
            if (type == null) return null;

            type = NormalizeNumeric(type);

            // Step 1: 直查字典（最快路径）
            if (TypeToString.TryGetValue(type, out var result))
                return result;

            // Step 2: List<T> → T[] 拆箱后递归
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var arrayType = type.GetGenericArguments()[0].MakeArrayType();
                return ToTypeVocabulary(arrayType);
            }

            // Step 3: 数组类型 → 递归检查元素类型（如 ShapeParameters[] → ShapeParameters → "RoI"）
            if (type.IsArray && type.HasElementType)
            {
                var elementType = type.GetElementType();
                if (elementType != null)
                    return ToTypeVocabulary(elementType);
            }

            // Step 4: 自动提升：单值 T → T[] 查表
            if (!type.IsArray)
            {
                var arrayType = type.MakeArrayType();
                if (TypeToString.TryGetValue(arrayType, out result))
                    return result;
            }

            return null;
        }

        public static Type? ToSystemType(string? globalVarType)
            => globalVarType != null && StringToType.TryGetValue(globalVarType, out var result) ? result : null;

        public static string? GetChineseName(string? typeName)
            => typeName != null && ChineseNames.TryGetValue(typeName, out var name) ? name : typeName;

        public static List<TypeOption> GetAllTypeOptions()
            => ChineseNames.Select(kvp => new TypeOption(kvp.Value, kvp.Key)).ToList();
    }

    public class TypeOption
    {
        public string DisplayName { get; }
        public string Value { get; }
        public TypeOption(string displayName, string value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }
}
