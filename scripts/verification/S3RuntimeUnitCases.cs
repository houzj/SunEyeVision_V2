using System.Reflection;
internal static class S2DocumentEditingCases
{
    [STAThread] private static int Main()
    {
        try
        {
            var assembly=Assembly.Load("SunEyeVision.BlobAnalysis.Runtime.Tests");var count=0;
            foreach(var (type,method,arguments) in new (string,string,object?[])[]{
                ("BlobAnalysisParameterContractTests","Contract_declares_neutral_inputs_and_XML_settings",[]),
                ("BlobAnalysisParameterContractTests","Validation_rejects_reversed_threshold_range",[]),
                ("ToolResultsTypeSanityTests","BlobOutput_NoConcreteTypeLeak",[]),
                ("ToolResultsTypeSanityTests","RuntimeBlobMeasurement_NoConcreteTypeLeak",[]),
                ("DeclaredBlobResultTests","Sorted_filtered_boundary_records_keep_native_element_identity",[]),
                ("DeclaredBlobResultTests","Explicit_missing_image_is_not_replaced_by_root_input",[]),
                ("DeclaredBlobResultTests","Native_workflow_publishes_records_and_independent_reader_leases",[0]),
                ("DeclaredBlobResultTests","Native_workflow_publishes_records_and_independent_reader_leases",[1]),
                ("DeclaredBlobResultTests","Native_workflow_publishes_records_and_independent_reader_leases",[3])})
            {
                var testType=assembly.GetType("SunEyeVision.Tool.BlobAnalysis.Tests."+type,true)!;
                var result=testType.GetMethod(method)!.Invoke(Activator.CreateInstance(testType),arguments);
                if(result is Task task)task.WaitAsync(TimeSpan.FromSeconds(45)).GetAwaiter().GetResult();
                count++;Console.WriteLine("通过：正式Release依赖执行单元测试 "+type+"."+method+" "+string.Join(",",arguments));
            }
            Console.WriteLine($"S3 Runtime单元测试：{count}组通过；产品依赖从正式Release加载，测试断言来自现行测试程序集。");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e is TargetInvocationException?e.InnerException:e);return 1;}
    }
}
