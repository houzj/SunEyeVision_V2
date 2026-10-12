using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Models
{
    public static class SdkTypeVocabulary
    {
        /// <summary>
        /// 所有类型只注册数组版本（单值 = 长度为 1 的数组，对齐 Halcon tuple 语义）。
        /// 数值类型归并为规范键：int/int[]/float/float[] → long/long[]、double/double[]。
        /// 核心概念：整型/双精度/字符串/布尔/图像/ROI/变换矩阵 + 几何图形
        /// （Circle/RotatedRect/Point/Polygon/Line/Annulus/Arc）。
        /// ROI（几何输入/图形定义，契约词目 ROI ↔ <see cref="RoiShape"/>[]）：
        /// 承载工具外发的几何定义（检测区/屏蔽区/测量几何），下游可整体/按类型/逐项/字段绑定。
        /// Region（算法像素区域，iconic 运行时数据）禁止进入工作流契约，仅活在算法工具内部，
        /// 由 RegionConverter 桥接为 RoiShape 外发。
        /// 图像词目以接口 IVisionImage 为唯一入口；算法库具体实现类型
        /// （如 OpenCvSharp.Mat、HalconDotNet.HImage）由各自引擎启动时经
        /// RegisterType 注册到 "Image"（契约 JSON 不变，下游 ToSystemType("Image")
        /// 仍返回 IVisionImage，过滤/绑定用的抽象类型）。
        /// 几何图形（Circle2d/RotatedRect2d/Point2d/Polygon2d/Line2d/Annulus2d/Arc2d）作为算法产物
        /// 输出进契约，供下游绑定/下钻属性。
        /// </summary>
        private static readonly Dictionary<Type, string> TypeToString = new()
        {
            [typeof(long[])]            = "Integer",
            [typeof(double[])]          = "Double",
            [typeof(string[])]          = "String",
            [typeof(bool[])]            = "Boolean",
            [typeof(Communication.BinaryPayload[])] = "Binary",
            [typeof(AffineMatrix[])]    = "Transform",
            [typeof(IVisionImage)]      = "Image",
            [typeof(RoiShape[])]        = "ROI",
            [typeof(Circle2d[])]        = "Circle",
            [typeof(RotatedRect2d[])]   = "RotatedRect",
            [typeof(SunEyeVision.Plugin.SDK.Models.Geometry.Point2d[])] = "Point",
            [typeof(Polygon2d[])]       = "Polygon",
            [typeof(Line2d[])]          = "Line",
            [typeof(Annulus2d[])]       = "Annulus",
            [typeof(Arc2d[])]           = "Arc",
        };

        /// <summary>
        /// 动态注册表：算法库（CvEngine/HalAdapter）启动时把引擎具体图像类型
        /// （如 typeof(Mat)/typeof(HImage)）注册到契约词目 "Image"。
        /// 静态 TypeToString 保持只读契约（零引擎依赖），动态注册不污染静态表。
        /// </summary>
        private static readonly ConcurrentDictionary<Type, string> RegisteredTypeToString = new();

        /// <summary>
        /// 动态注册类型→词目映射（如 CvEngine 启动注册 typeof(Mat) → "Image"）。
        /// 仅允许注册到契约 StringToType 已收录的词目；重复注册幂等覆盖（同类型同词目）。
        /// 新增词目应视为契约演进（需同步 StringToType/ChineseNames），不允许经注册表静默引入。
        /// </summary>
        public static void RegisterType(Type type, string typeName)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (string.IsNullOrWhiteSpace(typeName)) throw new ArgumentException("typeName 不能为空", nameof(typeName));

            // 词目必须是契约反向表已收录的规范键（rule-015：不允许正向能解析、反向解析不了的"单行道"词目）。
            if (!StringToType.ContainsKey(typeName))
                throw new ArgumentException(
                    $"词目 '{typeName}' 未收录于 SdkTypeVocabulary 契约反向表，禁止经 RegisterType 静默注册。", nameof(typeName));

            type = NormalizeNumeric(type);
            RegisteredTypeToString[type] = typeName;
        }

        /// <summary>
        /// 查询类型对应词目：先静态契约表，再动态注册表。
        /// </summary>
        private static bool TryGetTypeName(Type type, out string? name)
        {
            if (TypeToString.TryGetValue(type, out name))
                return true;
            return RegisteredTypeToString.TryGetValue(type, out name);
        }

        /// <summary>
        /// 字符串→CLR 类型反向映射。规范键映射到规范类型（数值归并为 long/double）。
        /// 保留历史存量类型名（"Xxx[]"、Float、Rectangle）以便旧方案数据可识别，
        /// 但 Byte 已删除，触发 NotSupportedException 可见（rule-015）。
        /// </summary>
        private static readonly Dictionary<string, Type> StringToType = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Integer"]   = typeof(long),
            ["String"]    = typeof(string),
            ["Boolean"]   = typeof(bool),
            ["Binary"]    = typeof(Communication.BinaryPayload),
            ["Double"]    = typeof(double),
            ["Transform"] = typeof(AffineMatrix),
            ["Image"]     = typeof(IVisionImage),
            ["ROI"]       = typeof(RoiShape[]),
            ["Circle"]        = typeof(Circle2d[]),
            ["RotatedRect"]   = typeof(RotatedRect2d[]),
            ["Point"]         = typeof(SunEyeVision.Plugin.SDK.Models.Geometry.Point2d[]),
            ["Polygon"]       = typeof(Polygon2d[]),
            ["Line"]          = typeof(Line2d[]),
            ["Annulus"]       = typeof(Annulus2d[]),
            ["Arc"]           = typeof(Arc2d[]),
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
            ["Binary"]    = "字节序列",
            ["Double"]    = "双精度",
            ["Image"]     = "图像",
            ["ROI"]       = "图形",
            ["Transform"] = "变换矩阵",
            ["Circle"]        = "圆",
            ["RotatedRect"]   = "旋转矩形",
            ["Point"]         = "点",
            ["Polygon"]       = "多边形",
            ["Line"]          = "直线",
            ["Annulus"]       = "圆环",
            ["Arc"]           = "圆弧",
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

            // Step 1: 直查字典（静态契约表 + 动态注册表）
            if (TryGetTypeName(type, out var result))
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
                if (TryGetTypeName(arrayType, out result))
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
