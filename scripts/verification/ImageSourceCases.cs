using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

internal static class S2DocumentEditingCases
{
    [STAThread] static int Main()
    {
        var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(Environment.GetEnvironmentVariable("S2_TEST_HELPERS")!,"SunEyeVision.UI.Tests.dll"));
        var runtimeTests=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(Environment.GetEnvironmentVariable("S2_TEST_HELPERS")!,"SunEyeVision.ImageSource.Runtime.Tests.dll"));
        SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.InitializeServices();
        SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.InitializeSolutionManager(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"solutions"));
        var selected=new Dictionary<string,string[]?>
        {
            ["ImageSourceAcceptanceTests"]=null,
            ["ImageSourceRuntimeTests"]=null,
            ["ImageSourceDeclarationAndFormatTests"]=null,
            ["ImageSourceDebugInvocationTests"]=null,
            ["ResultPresentationModelTests"]=null,
            ["ResultPresentationPresenterTests"]=null,
            ["NativeResultCompositionTests"]=null,
            ["ImageSourceDesignCompositionTests"]=null,
            ["ImageSourceConfigurationHistoryTests"]=null,
            ["LocalImageConfigurationSnapshotTests"]=null,
            ["ImageSourceSavePersistenceTests"]=null,
            ["ImageSourceResolverTests"]=null,
            ["SolutionManagerUiTests"]=new[]{
                "Unchecked_activation_in_selection_mode_preserves_start_history_and_execution",
                "Selection_and_removal_preserve_current_image_identity_and_one_way_display",
                "Restoring_images_preserves_missing_references_selection_and_run_flags_without_rewriting_configuration",
                "Unresolved_relative_image_keeps_reference_and_can_be_resolved_without_current_directory_fallback",
                "Right_click_and_programmatic_selection_do_not_execute_left_and_keys_execute_once",
                "Adding_missing_or_duplicate_images_does_not_change_current_selection",
                "Import_cancel_before_acceptance_does_not_publish_or_change_selection",
                "Import_cancel_after_atomic_acceptance_keeps_the_complete_committed_batch",
                "Import_source_switch_during_commit_cannot_change_new_source_selection",
                "Manual_execution_does_not_advance_but_default_successful_execution_does",
                "Missing_images_can_be_cleared_and_empty_configuration_is_persisted",
                "Unloading_preview_does_not_clear_source_or_persisted_paths",
                "Replacing_and_removing_missing_reference_does_not_delete_real_file",
                "Clearing_releases_cached_image_and_cancels_pending_thumbnail",
                "Mode_edits_preserve_display_and_execution_cursor_but_explicit_choice_resets_it",
                "Continuous_sources_stop_at_last_valid_selected_entry_without_advancing_other_sources",
                "Same_start_reset_during_a_round_restarts_only_that_source_on_the_next_round",
                "Repeated_single_runs_keep_execution_cursor_without_a_preview_and_explicit_choice_resets_only_its_source",
                "Stale_preview_list_cannot_replace_a_published_source_or_receive_its_cursor",
                "Cancellation_while_frozen_input_is_in_flight_never_advances_a_source",
                "Solution_execution_captures_each_workflow_source_and_does_not_auto_advance",
                "Frozen_execution_ignores_other_preview_and_does_not_overwrite_a_new_user_selection",
                "Success_advances_each_source_and_missing_or_empty_selection_is_rejected_before_dispatch"}
        };
        var results=new List<object>();var failures=0;var theoryCount=0;var uiTheoryCount=0;
        foreach(var (name,filter) in selected)
        {
            var type=assembly.GetType("SunEyeVision.UI.Tests."+name)
                ??runtimeTests.GetType("SunEyeVision.ImageSource.Runtime.Tests."+name,true)!;
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
                    throw new Exception("图像源Theory未被展开："+method.Name);
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
            if(count==0)throw new Exception("图像源检查组为空："+name);
        }
        if(uiTheoryCount!=14)throw new Exception("图像源非法候选7项与调试拒绝7项应完整展开，实际"+uiTheoryCount);
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"image-source-report.json"),
            JsonSerializer.Serialize(new{scope="正式Release图像源编辑、历史、持久化、Runtime像素资源与正式调试、结果状态及同Reader层级分页回归；不代真实VS/VM或人工验收",count=results.Count,failures,theoryCount,uiTheoryCount,results},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"图像源检查：{results.Count-failures}通过，{failures}失败。");
        return failures==0?0:1;
    }
}
