using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
internal static class S2DocumentEditingCases
{
    [STAThread] static int Main()
    {
        var helpers=Environment.GetEnvironmentVariable("S2_TEST_HELPERS")!;
        var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(helpers,"SunEyeVision.UI.Tests.dll"));
        SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.InitializeServices();
        SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.InitializeSolutionManager(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"workflow-test-solutions"));
        var selected = new Dictionary<string, string[]?> {
            ["DocumentHistoryCoordinatorTests"] = null,
            ["CanvasDocumentHistoryTests"] = null,
            ["RootConfigurationTests"] = null,
            ["RootTabStateTests"] = null,
            ["RootWorkflowCollectionTests"] = null,
            ["RootWorkspaceSettingsTests"] = null,
            ["VmCanvasAssemblyTests"] = null,
            ["VmConnectionPolicyTests"] = null,
            ["VmContainerHistoryTests"] = null,
            ["VmDeleteRepairTests"] = null,
            ["VmDeleteTransactionTests"] = null,
            ["VmFragmentCopyTests"] = null,
            ["VmGestureTests"] = null,
            ["VmGroupCreationTests"] = null,
            ["VmGroupWindowLifecycleTests"] = null,
            ["VmPathCalculatorTests"] = null,
            ["VmPlacementTests"] = null,
            ["VmRenameTests"] = null,
            ["VmSelectionLayoutTests"] = null,
            ["VmWireEditingTests"] = null,
            ["WorkflowLocationTests"] = null,
            ["WorkflowSearchTests"] = null,
            ["WorkflowTabButtonTests"] = null,
            ["WorkspaceStatusBarTests"] = null,
            ["WorkspaceTimingPresentationTests"] = null,
            ["ToolboxPresentationTests"] = null,
            ["GroupRunParameterConfigTests"] = null,
            ["ProductionEditorContextTests"] = null,
            ["NodeConfigurationEditHostTests"] = null,
        };
        var results=new List<object>();int failures=0;
        foreach(var selection in selected)
        {
            var type=assembly.GetType("SunEyeVision.UI.Tests."+selection.Key,true)!;
            var cases=new List<(MethodInfo Method,object?[] Arguments)>();
            foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).OrderBy(m=>m.Name))
            {
                var attrs=method.GetCustomAttributesData();
                if(attrs.Any(a=>a.AttributeType.FullName=="Xunit.FactAttribute")&&method.GetParameters().Length==0)
                    cases.Add((method,Array.Empty<object?>()));
                foreach(var inline in attrs.Where(a=>a.AttributeType.FullName=="Xunit.InlineDataAttribute"))
                {
                    var values=(IReadOnlyCollection<CustomAttributeTypedArgument>)inline.ConstructorArguments[0].Value!;
                    var args=values.Select((a,i)=>a.Value is not null&&method.GetParameters()[i].ParameterType.IsEnum?Enum.ToObject(method.GetParameters()[i].ParameterType,a.Value):a.Value).ToArray();
                    cases.Add((method,args));
                }
                foreach(var member in attrs.Where(a=>a.AttributeType.FullName=="Xunit.MemberDataAttribute"))
                {
                    var memberName=(string)member.ConstructorArguments[0].Value!;
                    var methodSource=type.GetMethod(memberName,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
                    var data=methodSource is not null?methodSource.Invoke(null,Array.Empty<object?>()):type.GetProperty(memberName,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null);
                    foreach(var row in (IEnumerable<object?[]>)data!)cases.Add((method,row));
                }
            }
            if(cases.Count==0)throw new Exception("检查组为空："+selection.Key);
            foreach(var test in cases)
            {
                object? instance=null;string name=type.Name+"."+test.Method.Name+"("+string.Join(",",test.Arguments)+")";
                try
                {
                    instance=Activator.CreateInstance(type)!;
                    var returned=test.Method.Invoke(instance,test.Arguments);
                    if(returned is Task task)task.GetAwaiter().GetResult();
                    Console.WriteLine("通过："+name);results.Add(new{name,passed=true});
                }
                catch(Exception error)
                {
                    failures++;var cause=error.InnerException??error;Console.WriteLine("失败："+name);Console.Error.WriteLine(cause);results.Add(new{name,passed=false,error=cause.ToString()});
                }
                finally{(instance as IDisposable)?.Dispose();}
            }
        }
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"related-report.json"),JsonSerializer.Serialize(new{scope="当前正式Release工作流与控件合并定向回归；Fact及InlineData，不代用户人工",count=results.Count,failures,results},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"统一基线检查：{results.Count-failures}通过，{failures}失败。");return failures==0?0:1;
    }
}
