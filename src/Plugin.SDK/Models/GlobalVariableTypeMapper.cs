using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Plugin.SDK.Models
{
    public static class GlobalVariableTypeMapper
    {
        private static readonly Dictionary<Type, string> TypeToString = new()
        {
            [typeof(int)]    = "Integer",
            [typeof(string)] = "String",
            [typeof(bool)]   = "Boolean",
            [typeof(double)] = "Double",
            [typeof(byte)]   = "Byte",
            [typeof(float)]  = "Float",
            [typeof(int[])]    = "Integer[]",
            [typeof(double[])] = "Double[]",
            [typeof(float[])]  = "Float[]",
            [typeof(string[])] = "String[]",
            [typeof(System.Windows.Point)] = "Point",
            [typeof(OpenCvSharp.Point)]    = "Point",
            [typeof(Point2D)]              = "Point",
            [typeof(System.Windows.Rect)]  = "Rectangle",
            [typeof(OpenCvSharp.Rect)]     = "Rectangle",
            [typeof(System.Windows.Size)]  = "Size",
            [typeof(OpenCvSharp.Size)]     = "Size",
            [typeof(IVisionImage)] = "Image",
            [typeof(IVisionRegion)] = "Region",
            [typeof(Circle2d)]      = "Circle",
            [typeof(Line2d)]        = "Line",
            [typeof(Annulus2d)]     = "Annulus",
            [typeof(ShapeParameters)] = "Rectangle",
        };

        private static readonly Dictionary<string, Type> StringToType =
            TypeToString.GroupBy(kvp => kvp.Value)
                .ToDictionary(g => g.Key, g => g.First().Key, StringComparer.OrdinalIgnoreCase);

        public static readonly Dictionary<string, string> ChineseNames = new()
        {
            ["Integer"]   = "\u6574\u6570",
            ["String"]    = "\u5b57\u7b26\u4e32",
            ["Boolean"]   = "\u5e03\u5c14",
            ["Double"]    = "\u53cc\u7cbe\u5ea6",
            ["Byte"]      = "\u5b57\u8282",
            ["Point"]     = "\u70b9",
            ["Rectangle"] = "\u77e9\u5f62",
            ["Size"]      = "\u5c3a\u5bf8",
            ["Image"]     = "\u56fe\u50cf",
            ["Region"]    = "\u533a\u57df",
            ["Circle"]    = "\u5706",
            ["Line"]      = "\u76f4\u7ebf",
            ["RotatedRect"] = "\u65cb\u8f6c\u77e9\u5f62",
            ["Polygon"]   = "\u591a\u8fb9\u5f62",
            ["Annulus"]   = "\u5706\u73af",
            ["Float"]     = "\u5355\u7cbe\u5ea6",
            ["Integer[]"] = "\u6574\u578b\u6570\u7ec4",
            ["Double[]"]  = "\u53cc\u7cbe\u5ea6\u6570\u7ec4",
            ["Float[]"]  = "\u5355\u7cbe\u5ea6\u6570\u7ec4",
            ["String[]"]  = "\u5b57\u7b26\u4e32\u6570\u7ec4",
        };

        public static string? ToGlobalVarType(Type? type)
            => type != null && TypeToString.TryGetValue(type, out var result) ? result : null;

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
