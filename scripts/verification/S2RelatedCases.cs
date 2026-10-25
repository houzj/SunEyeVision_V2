using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

internal static class S2DocumentEditingCases
{
    static int Main()
    {
        var helpers = Environment.GetEnvironmentVariable("S2_TEST_HELPERS")!;
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(helpers, "SunEyeVision.UI.Tests.dll"));
        var selected = new Dictionary<string, string[]?> {
            ["WorkflowDocumentPreparationTests"] = null,
            ["ProductionEditorContextTests"] = null,
            ["ImageThresholdProductionEditorTests"] = null,
            ["GlobalVariableConfigurationEditHostTests"] = null,
            ["ContinuousRunStopNotificationTests"] = null,
            ["SolutionManagerUiTests"] = new[] {
                "Continuous_sources_stop_at_last_valid_selected_entry_without_advancing_other_sources",
                "Cancellation_while_tool_is_in_flight_never_advances_a_source",
                "Solution_execution_captures_each_workflow_source_and_does_not_auto_advance",
                "Frozen_execution_ignores_other_preview_and_does_not_overwrite_a_new_user_selection",
                "Success_advances_each_source_and_missing_or_empty_selection_is_rejected_before_dispatch" },
            ["CommunicationAutomationTests"] = new[] {
                "Single_module_uses_a_parameter_snapshot_and_rejects_missing_upstream_results",
                "Communication_mapping_overrides_only_execution_copy_and_does_not_leak_to_next_run",
                "Full_workflow_mapping_overrides_execution_binding_without_changing_design_binding",
                "Failed_workflow_does_not_publish_success_completion",
                "Manual_run_lease_also_rejects_communication_operations",
                "Solution_admission_blocks_other_manual_runs_and_releases_all_snapshots",
                "Repeated_communication_nodes_share_admission_snapshot_and_failure_releases_it",
                "Continuous_apply_during_first_node_only_changes_the_next_whole_round" }
        };
        var results = new List<object>(); int failures = 0;
        foreach (var selection in selected)
        {
            var type = assembly.GetType("SunEyeVision.UI.Tests." + selection.Key, true)!;
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
