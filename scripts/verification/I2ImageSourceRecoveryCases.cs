using System.Reflection;
using System.Text.Json;
using System.IO;

internal static class S2DocumentEditingCases
{
    [STAThread] private static int Main()
    {
        var assembly=typeof(S2DocumentEditingCases).Assembly;
        var probeAssembly=typeof(SunEyeVision.TestFixtures.WorkspaceProbeDefinition).Assembly;
        if(probeAssembly.GetReferencedAssemblies().Any(a=>a.Name is "SunEyeVision.UI" or "SunEyeVision.Plugin.SDK" or "PresentationFramework" or "PresentationCore" or "WindowsBase"))
            throw new InvalidOperationException("结果消费者测试声明不能依赖UI/SDK/WPF。");
        var failedSource=Path.Combine(Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!,"../../../docs/collaboration/evidence/I2_RESULT_CHAIN_FAILED_CASES_20261029.json");
        using var original=JsonDocument.Parse(File.ReadAllText(failedSource));
        var originals=original.RootElement.GetProperty("cases").EnumerateArray().Select(c=>c.GetProperty("originalName").GetString()!).ToHashSet(StringComparer.Ordinal);
        var results=new List<object>();int failures=0,covered=0;
        foreach(var name in new[]{"LocalResultImagePreviewTests","ImageSourceDesignCompositionTests","SolutionManagerUiTests","ResultWorkspaceProductionTests","ImageSourceAcceptanceTests","CameraStreamOwnershipTests","CameraWorkflowFrameFeedTests"})
        {
            var type=assembly.GetType("SunEyeVision.UI.Tests."+name,true)!;
            foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).OrderBy(m=>m.Name))
            {
                if(name=="SolutionManagerUiTests"&&!originals.Contains(name+"."+method.Name+"()"))continue;
                var args=new List<object?[]>();
                var attributes=method.GetCustomAttributesData();
                if(attributes.Any(a=>a.AttributeType.FullName=="Xunit.FactAttribute"))args.Add(Array.Empty<object?>());
                foreach(var inline in attributes.Where(a=>a.AttributeType.FullName=="Xunit.InlineDataAttribute"))
                    args.Add(((IReadOnlyCollection<CustomAttributeTypedArgument>)inline.ConstructorArguments[0].Value!).Select(a=>a.Value).ToArray());
                foreach(var values in args)
                {
                    var label=name+"."+method.Name+"("+string.Join(",",values)+")";
                    bool wasFailed=originals.Contains(label);if(wasFailed)covered++;
                    try
                    {
                        var instance=Activator.CreateInstance(type)!;
                        try{if(method.Invoke(instance,values) is Task task)task.GetAwaiter().GetResult();}
                        finally{(instance as IDisposable)?.Dispose();}
                        results.Add(new{name=label,passed=true,originalFailure=wasFailed});Console.WriteLine("PASS "+label);
                    }
                    catch(Exception error)
                    {
                        failures++;var cause=error.InnerException??error;
                        results.Add(new{name=label,passed=false,originalFailure=wasFailed,error=cause.ToString()});Console.WriteLine("FAIL "+label+"\n"+cause);
                    }
                }
            }
        }
        // Four design theories are serialized with Boolean.ToString in the original evidence.
        if(covered!=21)throw new InvalidOperationException("本批必须逐项承接21个原失败，实际："+covered);
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"image-source-recovery-report.json"),
            JsonSerializer.Serialize(new{count=results.Count,failures,originalFailuresCovered=covered,
                testAssemblyPath=assembly.Location,testAssemblySha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(assembly.Location))),
                runtimeProbeAssemblyPath=probeAssembly.Location,runtimeProbeAssemblySha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(probeAssembly.Location))),
                scope="原21失败及同组本地解码/结果工作区用例；原249整体未重跑。直接编译原用例、正式产品引用及共享实际Dispatcher。",results},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"图像源失败承接：{results.Count-failures}通过，{failures}失败；覆盖原失败{covered}项。");
        return failures==0?0:1;
    }
}
