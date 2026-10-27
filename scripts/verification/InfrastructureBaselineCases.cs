using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

internal static class S2DocumentEditingCases
{
    static int Main()
    {
        var helpers = Environment.GetEnvironmentVariable("S2_TEST_HELPERS")!;
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(helpers, "SunEyeVision.Plugin.Infrastructure.Tests.dll"));
        var selected = new Dictionary<string, string[]?> {
            ["ToolDirectoryCatalogTests"] = null,
        };
        var results = new List<object>(); int failures = 0;
        foreach (var selection in selected)
        {
            var type = assembly.GetType("SunEyeVision.Plugin.Infrastructure.Tests." + selection.Key, true)!;
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetParameters().Length == 0 && m.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "Xunit.FactAttribute")
                    && (selection.Value is null || selection.Value.Contains(m.Name))).OrderBy(m => m.Name).ToArray();
            if (selection.Value is not null && methods.Length != selection.Value.Length)
                throw new Exception("未找到全部指定用例：" + selection.Key);
            if (methods.Length == 0) throw new Exception("检查组为空：" + selection.Key);
            foreach (var method in methods)
            {
                object? instance = null;
                var started = DateTimeOffset.Now;
                try
                {
                    instance = Activator.CreateInstance(type)!;
                    var returned = method.Invoke(instance, null);
                    if (returned is Task task) task.GetAwaiter().GetResult();
                    Console.WriteLine("通过：" + type.Name + "." + method.Name);
                    results.Add(new { name = type.Name + "." + method.Name, passed = true, started });
                }
                catch (Exception error)
                {
                    failures++;
                    var cause = error.InnerException ?? error;
                    Console.WriteLine("失败：" + type.Name + "." + method.Name);
                    Console.Error.WriteLine(cause);
                    results.Add(new { name = type.Name + "." + method.Name, passed = false, started, error = cause.ToString() });
                }
                finally { (instance as IDisposable)?.Dispose(); }
            }
        }
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!, "related-report.json"),
            JsonSerializer.Serialize(new { scope = "现有UI/变量/运行入口针对正式Release的关联自动回归；由实施者执行", count = results.Count, failures, results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"S2关联检查：{results.Count - failures}通过，{failures}失败。");
        return failures == 0 ? 0 : 1;
    }
}
