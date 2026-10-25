using System.IO;
using System.Windows;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Workflow;

internal static class S2DocumentEditingCases
{
    [STAThread] private static int Main()
    {
        try
        {
            bool expectEditor=Environment.GetEnvironmentVariable("S3_EXPECT_EDITOR")!="0";
            var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;
            var manager=new PluginManager();
            if(Environment.GetEnvironmentVariable("S3_USE_GLOBAL_LOADER")=="1")
            {
                var admitted=PluginLoader.LoadPlugins(Path.Combine(product,"plugins"));
                if(Environment.GetEnvironmentVariable("S3_EXPECT_REJECTION")=="1")
                {
                    Require(!admitted&&!PluginLoader.IsLoaded,"全局装载器将失败标为已完成");
                    Console.WriteLine("通过：正式全局装载器拒绝不完整组件且不设置已完成");return 0;
                }
                Require(admitted&&PluginLoader.IsLoaded,"全局装载器未接纳完整组件");
            }
            else
            {
                manager.LoadPlugins(Path.Combine(product,"plugins"));
                manager.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(x=>x.Id));
            }
            var components=ToolRegistry.GetComponentMetadata();
            Require(components.Count(c=>c.ToolId=="BlobAnalysis"&&c.Role==ToolComponentRole.Runtime)==1,"Blob没有唯一Runtime");
            Require(components.Count(c=>c.ToolId=="BlobAnalysis"&&c.Role==ToolComponentRole.Editor)==(expectEditor?1:0),"可选Editor登记不符");
            Console.WriteLine("通过：正式PluginManager发现/准入/注册，单ToolId及组件角色正确");
            var parameters=ToolRegistry.CreateParameters("BlobAnalysis");
            void Set(string name,object value)=>parameters.GetType().GetProperty(name)!.SetValue(parameters,value);
            Set("Threshold",100);Set("IsAreaFilterEnabled",false);Set("IsBlobCountFilterEnabled",false);Set("OutputBlobImage",true);
            var node=new WorkflowNodeBase("blob","blob","Blob","BlobAnalysis"){Parameters=parameters};
            var tool=node.CreateInstance()??throw new Exception("节点无法创建工具");
            var editor=ToolRegistry.CreateEditorFactory(tool);
            Require((editor is not null)==expectEditor,"实际节点编辑器解析错误");
            if(editor is not null)
            {
                if(Application.Current is null){var app=new SunEyeVision.UI.App();app.InitializeComponent();}
                var debug=editor.CreateDebugControl(tool);var data=editor.CreateDataAnalysisControl();
                Require(debug is not null&&data is not null&&debug.GetType().Assembly.GetName().Name=="SunEyeVision.Tool.BlobAnalysis.Wpf","真实BAML控件没有来自独立Wpf组件");
                Console.WriteLine("通过：共享契约与真实BAML控件实例化，编辑器来自独立Wpf组件");
            }
            else Console.WriteLine("通过：缺少Wpf时节点明确无专用编辑器，保留算法入口");
            using var image=HalImage.CreateFromBytes(Enumerable.Repeat((byte)200,64).ToArray(),8,8,1);
            var engine=new WorkflowEngine(new CheckLogger());var flow=engine.CreateWorkflow("S3-component-flow","组件验证");flow.Nodes.Add(node);
            var executor=new WorkflowExecutionEngine(engine,manager,new CheckLogger());
            using var execution=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,image)).GetAwaiter().GetResult();
            Require(execution.Success&&execution.NodeResults.TryGetValue("blob",out var nodeResult),"正式工作流失败："+string.Join(";",execution.Errors??new()));
            var result=execution.NodeResults["blob"].Result??throw new Exception("工作流没有Blob结果");
            Require(result.IsSuccess&&Convert.ToInt32(result.GetType().GetProperty("FilteredBlobCount")!.GetValue(result))==1&&Convert.ToDouble(result.GetType().GetProperty("FilteredTotalArea")!.GetValue(result))==64,"真实节点结果不符");
            Require(execution.DeclaredOutputs.Count==1,"正式执行未发布声明结果");
            Console.WriteLine("通过：正式节点与WorkflowExecutionEngine真实HALCON执行、声明结果发布，Blob面积64");
            Console.WriteLine($"S3组件消费：3组通过；Editor={(expectEditor?"存在":"缺席")}。不是完整新UI或整个S3验收。");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
    private sealed class CheckLogger:ILogger
    {
        public ILogger ForSource(string source)=>this;
        public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All)
        {if(level==LogLevel.Error)Console.Error.WriteLine(message+" "+exception);}
    }
}
