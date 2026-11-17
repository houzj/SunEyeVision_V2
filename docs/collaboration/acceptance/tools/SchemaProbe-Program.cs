using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

var root = @"d:\MyWork\SunEyeVision_V2\Release\SunEyeVision_v1.0.0\SunEyeVision";
AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
{
    var n = new AssemblyName(e.Name).Name + ".dll";
    var p = Path.Combine(root, n);
    return File.Exists(p) ? Assembly.LoadFrom(p) : null;
};

var tools = new[]
{
    ("FrameMean", "SunEyeVision.Tool.FrameMean.Runtime", "SunEyeVision.Tool.FrameMean.Runtime.FrameMeanTool"),
    ("ColorExtract", "SunEyeVision.Tool.ColorExtract.Runtime", "SunEyeVision.Tool.ColorExtract.Runtime.ColorExtractTool"),
    ("DivideImage", "SunEyeVision.Tool.DivideImage.Runtime", "SunEyeVision.Tool.DivideImage.Runtime.DivideImageTool"),
    ("IntensityMeasure", "SunEyeVision.Tool.IntensityMeasure.Runtime", "SunEyeVision.Tool.IntensityMeasure.Runtime.IntensityMeasureTool"),
    ("PixelCount", "SunEyeVision.Tool.PixelCount.Runtime", "SunEyeVision.Tool.PixelCount.Runtime.PixelCountTool"),
    ("If", "SunEyeVision.Tool.If.Runtime", "SunEyeVision.Tool.If.Runtime.IfTool"),
    ("BlobLabels", "SunEyeVision.Tool.BlobLabels.Runtime", "SunEyeVision.Tool.BlobLabels.Runtime.BlobLabelsTool"),
    ("MarkFind", "SunEyeVision.Tool.MarkFind.Runtime", "SunEyeVision.Tool.MarkFind.Runtime.MarkFindTool"),
    ("ContourMatch", "SunEyeVision.Tool.ContourMatch.Runtime", "SunEyeVision.Tool.ContourMatch.Runtime.ContourMatchTool"),
    ("GrayMatch", "SunEyeVision.Tool.GrayMatch.Runtime", "SunEyeVision.Tool.GrayMatch.Runtime.GrayMatchTool"),
    ("ImageLoad", "SunEyeVision.Tool.ImageSource.Runtime", "SunEyeVision.Tool.ImageSource.Runtime.ImageSourceTool"),
    ("BlobAnalysis", "SunEyeVision.Tool.BlobAnalysis.Runtime", "SunEyeVision.Tool.BlobAnalysis.Runtime.BlobTool")
};

var result = new Dictionary<string, object>();
foreach (var (toolId, asmName, typeName) in tools)
{
    var entry = new Dictionary<string, object>();
    try
    {
        var asm = Assembly.LoadFrom(Path.Combine(root, asmName + ".dll"));
        var type = asm.GetType(typeName) ?? throw new InvalidOperationException("类型不存在: " + typeName);
        var instance = Activator.CreateInstance(type)!;
        var schema = type.GetProperty("SettingsSchema", BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);
        var fields = new List<Dictionary<string, object?>>();
        if (schema != null)
        {
            var pres = schema.GetType().GetProperty("Presentation")!.GetValue(schema);
            if (pres != null)
            {
                var list = pres.GetType().GetProperty("Fields")!.GetValue(pres) as IEnumerable;
                if (list != null)
                {
                    foreach (var f in list)
                    {
                        var layout = f.GetType().GetProperty("Layout")?.GetValue(f);
                        var choices = f.GetType().GetProperty("ChoiceKeys")?.GetValue(f) as IEnumerable;
                        var choiceText = choices == null ? "" : string.Join("|", choices.Cast<object>().Select(o => o?.ToString()));
                        fields.Add(new Dictionary<string, object?>
                        {
                            ["id"] = f.GetType().GetProperty("FieldId")?.GetValue(f)?.ToString(),
                            ["label"] = f.GetType().GetProperty("DisplayName")?.GetValue(f)?.ToString(),
                            ["editor"] = f.GetType().GetProperty("Editor")?.GetValue(f)?.ToString(),
                            ["choices"] = choiceText,
                            ["page"] = layout?.GetType().GetProperty("Page")?.GetValue(layout)?.ToString(),
                            ["section"] = layout?.GetType().GetProperty("Section")?.GetValue(layout)?.ToString(),
                            ["min"] = layout?.GetType().GetProperty("Minimum")?.GetValue(layout)?.ToString(),
                            ["max"] = layout?.GetType().GetProperty("Maximum")?.GetValue(layout)?.ToString(),
                            ["increment"] = layout?.GetType().GetProperty("Increment")?.GetValue(layout)?.ToString(),
                            ["decimals"] = layout?.GetType().GetProperty("DecimalPlaces")?.GetValue(layout)?.ToString(),
                            ["advanced"] = layout?.GetType().GetProperty("Advanced")?.GetValue(layout)?.ToString()
                        });
                    }
                }
            }
        }

        var defaults = new Dictionary<string, object?>();
        var defaultsMethod = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "Defaults" && m.GetParameters().Length == 0);
        if (defaultsMethod != null)
        {
            try
            {
                var values = defaultsMethod.Invoke(instance, null);
                var dict = values?.GetType().GetProperty("Values")?.GetValue(values) as IEnumerable;
                if (dict != null)
                {
                    foreach (var kv in dict)
                    {
                        var key = kv.GetType().GetProperty("Key")!.GetValue(kv)?.ToString();
                        var val = kv.GetType().GetProperty("Value")!.GetValue(kv);
                        if (key != null) defaults[key] = val?.ToString();
                    }
                }
            }
            catch (Exception ex) { defaults["__error"] = ex.InnerException?.Message ?? ex.Message; }
        }

        entry["fields"] = fields;
        entry["defaults"] = defaults;
        entry["ok"] = true;
    }
    catch (Exception ex)
    {
        entry["ok"] = false;
        entry["error"] = ex.InnerException?.Message ?? ex.Message;
    }
    result[toolId] = entry;
}

var outPath = @"d:\MyWork\SunEyeVision_V2\docs\collaboration\acceptance\our-schemas.json";
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
File.WriteAllText(outPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }), new System.Text.UTF8Encoding(false));
Console.WriteLine("已输出: " + outPath);
foreach (var kv in result)
{
    var e = (Dictionary<string, object>)kv.Value;
    var ok = (bool)e["ok"]!;
    var count = ok ? ((List<Dictionary<string, object?>>)e["fields"]!).Count : -1;
    Console.WriteLine($"{kv.Key,-18} ok={ok} 字段数={count} {(ok ? "" : e["error"])}");
}
