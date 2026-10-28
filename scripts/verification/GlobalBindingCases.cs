using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

internal static class S2DocumentEditingCases
{
    [STAThread] static int Main()
    {
        var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(Environment.GetEnvironmentVariable("S2_TEST_HELPERS")!,"SunEyeVision.UI.Tests.dll"));
        var sdkTests=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(Environment.GetEnvironmentVariable("S2_TEST_HELPERS")!,"SunEyeVision.Plugin.SDK.Tests.dll"));
        SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.InitializeServices();
        SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.InitializeSolutionManager(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"solutions"));
        var selected=new Dictionary<string,string[]?>
        {
            ["GlobalProducerBindingRulesTests"]=null,
            ["WorkflowGlobalProductionTests"]=null,
            ["WorkflowDocumentPreparationTests"]=null,
            ["ResultVariableBindingProductionTests"]=null,
            ["BindingAdaptationB2Tests"]=null,
            ["BindingConfigurationStoreTests"]=null,
            ["BindingWorkflowExecutionTests"]=null,
            ["OutputBindingChangeTests"]=null,
            ["GlobalVariableConfigurationEditHostTests"]=null,
            ["NodeConfigurationEditHostTests"]=null,
            ["CommunicationReceiveDataToolTests"]=null,
            ["CommunicationReceiveConsumptionTests"]=null,
            ["CommunicationSendDataToolTests"]=null,
            ["CommunicationAutomationTests"]=null,
            ["SolutionExecutionDependencyGraphTests"]=null,
        };
        var results=new List<object>();var failures=0;var theoryCount=0;var uiTheoryCount=0;
        foreach(var (name,filter) in selected)
        {
            var type=assembly.GetType("SunEyeVision.UI.Tests."+name)
                ??sdkTests.GetType("SunEyeVision.Plugin.SDK.Tests."+name,true)!;
            var methods=type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)
                .Where(method=>filter is null||filter.Contains(method.Name)).OrderBy(method=>method.Name).ToArray();
            if(filter is not null&&methods.Length!=filter.Length)throw new Exception("指定图像源用例缺失："+name);
            var count=0;
            foreach(var method in methods)
            {
                var attributes=method.GetCustomAttributesData();
                var arguments=new List<object?[]>();
                if(attributes.Any(attribute=>attribute.AttributeType.FullName=="Xunit.FactAttribute"))arguments.Add(Array.Empty<object?>());
                foreach(var inline in attributes.Where(attribute=>attribute.AttributeType.FullName=="Xunit.InlineDataAttribute"))
                {
                    var values=(IReadOnlyCollection<CustomAttributeTypedArgument>)inline.ConstructorArguments[0].Value!;
                    arguments.Add(values.Select(value=>value.Value).ToArray());theoryCount++;
                    if(name is "ImageSourceConfigurationHistoryTests" or "ImageSourceDebugInvocationTests")uiTheoryCount++;
                }
                if(attributes.Any(attribute=>attribute.AttributeType.FullName=="Xunit.TheoryAttribute")&&arguments.Count==0)
                    throw new Exception("全局绑定Theory未被展开："+method.Name);
                foreach(var args in arguments)
                {
                    count++;object? instance=null;var label=name+"."+method.Name+"("+string.Join(",",args)+")";
                    try
                    {
                        instance=Activator.CreateInstance(type)!;
                        if(method.Invoke(instance,args) is Task task)task.GetAwaiter().GetResult();
                        results.Add(new{name=label,passed=true});Console.WriteLine("通过："+label);
                    }
                    catch(Exception error)
                    {
                        failures++;var cause=error.InnerException??error;
                        results.Add(new{name=label,passed=false,error=cause.ToString()});Console.Error.WriteLine("失败："+label+"\n"+cause);
                    }
                    finally{(instance as IDisposable)?.Dispose();}
                }
            }
            if(count==0)throw new Exception("全局绑定检查组为空："+name);
        }
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"global-binding-report.json"),
            JsonSerializer.Serialize(new{scope="正式Release类型生产者身份、UI绑定事务、历史保存、同流跨流与通信、取消排空及错运行拒绝；数组通信和资源存储明确未接入，不代VS/VM及人工验收",count=results.Count,failures,theoryCount,uiTheoryCount,results},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"全局绑定检查：{results.Count-failures}通过，{failures}失败。");
        return failures==0?0:1;
    }
}
