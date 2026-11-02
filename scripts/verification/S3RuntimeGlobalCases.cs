using System.IO;
using System.Reflection;
using System.Text.Json;
using SunEyeVision.UI.Tests;

internal static class S2DocumentEditingCases
{
    [STAThread] private static int Main()
    {
        var results=new List<object>();var failed=0;
        var type=typeof(WorkflowGlobalProductionTests);
        foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)
            .Where(method=>method.GetCustomAttributesData().Any(a=>a.AttributeType.FullName=="Xunit.FactAttribute"))
            .OrderBy(method=>method.Name))
        {
            try
            {
                if(method.Invoke(new WorkflowGlobalProductionTests(),null) is Task task)task.GetAwaiter().GetResult();
                results.Add(new{name=method.Name,passed=true});Console.WriteLine("通过："+method.Name);
            }
            catch(Exception error)
            {
                failed++;var cause=error.InnerException??error;
                results.Add(new{name=method.Name,passed=false,error=cause.ToString()});Console.Error.WriteLine("失败："+method.Name+"\n"+cause);
            }
        }
        if(results.Count==0)throw new Exception("运行变量生产检查集合为空。");
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"runtime-global-report.json"),
            JsonSerializer.Serialize(new{scope="正式Release运行变量槽、生产调度、通讯身份及生命周期；不代表B08变量UI或触发文本已接入，不代完整旧测试/VS/人工",count=results.Count,failed,results},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"运行变量生产：{results.Count-failed}通过，{failed}失败。");return failed==0?0:1;
    }
}
