using System.Reflection;
using System.Text.Json;
using System.IO;

internal static class S2DocumentEditingCases
{
    private static void CheckReentrantAdmission()
    {
        foreach (var replace in new[] { false, true })
        {
            var owner = new SunEyeVision.Plugin.Runtime.ToolExecutionOwner();
            var run = Guid.NewGuid();
            SunEyeVision.Plugin.Runtime.ToolExecutionIdentity? winner = null;
            var original = owner.Begin("flow/source", run, "schema", admission: () =>
            {
                if (replace) winner = owner.Begin("flow/source", run, "schema");
                else owner.RetireNode("flow/source");
                return true;
            });
            var published = false;
            if (original.TryPublish(() => published = true) || published || (replace && !winner!.IsCurrent))
                throw new InvalidOperationException("重入准入后旧身份错误发布，或替换身份失效。");
        }
    }
    [STAThread] private static int Main()
    {
        CheckReentrantAdmission();
        var assembly=typeof(S2DocumentEditingCases).Assembly;
        var probeAssembly=typeof(SunEyeVision.TestFixtures.WorkspaceProbeDefinition).Assembly;
        if(probeAssembly.GetReferencedAssemblies().Any(a=>a.Name is "SunEyeVision.UI" or "SunEyeVision.Plugin.SDK" or "PresentationFramework" or "PresentationCore" or "WindowsBase"))
            throw new InvalidOperationException("结果消费者测试声明不能依赖UI/SDK/WPF。");
        var failedSource=Path.Combine(Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!,"../../../docs/collaboration/evidence/I2_RESULT_CHAIN_FAILED_CASES_20261029.json");
        using var original=JsonDocument.Parse(File.ReadAllText(failedSource));
        var originals=original.RootElement.GetProperty("cases").EnumerateArray().Select(c=>c.GetProperty("originalName").GetString()!).ToHashSet(StringComparer.Ordinal);
        var auditSource=Path.Combine(Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!,"../../../docs/collaboration/evidence/D07_FULL_IMAGESOURCE_TRACK_MIGRATION_AUDIT_20261029.json");
        using var audit=JsonDocument.Parse(File.ReadAllText(auditSource));
        var originalGroups=new[]{"ImageSourceConfigurationHistoryTests","ImageSourceSavePersistenceTests","LocalImageConfigurationSnapshotTests","ImageSourceResolverTests","ImageSourceRuntimeTests","ImageSourceDeclarationAndFormatTests","ImageSourceDebugInvocationTests","ResultPresentationModelTests","ResultPresentationPresenterTests","ResultImagePaneTests","ResultViewportFullscreenScopeTests","NativeResultCompositionTests","RootResultImagePreviewTests","ImageSourceAcceptanceTests","LocalResultImagePreviewTests","ImageSourceDesignCompositionTests","ResultWorkspaceProductionTests","SolutionManagerUiTests"}.ToHashSet(StringComparer.Ordinal);
        var expectedOriginalCases=audit.RootElement.GetProperty("groups").EnumerateArray()
            .Where(g=>originalGroups.Contains(g.GetProperty("class").GetString()!))
            .SelectMany(g=>g.GetProperty("cases").EnumerateArray().Select(c=>c.GetProperty("name").GetString()!)).ToHashSet(StringComparer.Ordinal);
        if(expectedOriginalCases.Count!=222)throw new InvalidOperationException("原配置、接纳、运行、调试、预览、设计组合、工作区、结果模型、展示器与视口清单必须包含222项。");
        // These six historical cases asserted camera format conversion. The current
        // camera contract preserves the physical frame format; retain the audit and
        // explicitly record replacements instead of reporting the old assertions as run.
        var cameraReplacements=(from format in new[]{"original","mono8","rgb24"}
            from pair in new[]{
                (Old:"Color_input_converts_real_pixels_without_changing_device_metadata_or_actual_domain",New:"Camera_color_input_ignores_local_format_without_changing_metadata_or_domain"),
                (Old:"Gray_input_supports_all_three_formats_and_rgb_replication_preserves_actual_domain",New:"Camera_gray_input_ignores_local_format_and_preserves_actual_domain")}
            select (Old:$"ImageSourceDeclarationAndFormatTests.{pair.Old}({format})",
                New:$"ImageSourceDeclarationAndFormatTests.{pair.New}({format})")).ToArray();
        if(cameraReplacements.Length!=6||cameraReplacements.Any(item=>!expectedOriginalCases.Contains(item.Old)||originals.Contains(item.Old)))
            throw new InvalidOperationException("相机历史案例映射与原验收清单不一致。");
        var currentRequiredCases=expectedOriginalCases.Except(cameraReplacements.Select(item=>item.Old),StringComparer.Ordinal)
            .Concat(cameraReplacements.Select(item=>item.New)).ToHashSet(StringComparer.Ordinal);
        var sequenceCases=new[]{
            "LocalImageSequenceTests.ReadAcceptanceAdvancesOnlyThatSourceAndIsIdempotentForTheRequest()",
            "LocalImageSequenceTests.FinishedRoundRetainsConsumedPositionAndStopsAtTheActualLastEntry()",
            "LocalImageSequenceTests.CancelledAndStaleReadTicketsCannotChangeTheSequence()",
            "LocalImageSequenceTests.CancellationAfterAcceptanceKeepsTheConsumedPosition()",
            "LocalImageSequenceTests.ManualNavigationWrapsOnceWithoutApplyingAutomaticAdvanceOrStop()",
            "LocalImageSequenceTests.UnselectedCanStartAtFirstButInvalidExplicitSelectionCannotReplaceAnActiveSession()",
            "LocalImageSequenceTests.SequencesFreezeEntriesAndAdvanceAllSourcesOfOnlyTheCompletedWorkflow()",
            "LocalImageSequenceTests.FailureAndManualExecutionRetainCurrentEntryAndStaleGenerationsAreRejected()",
            "LocalImageSequenceTests.LastEntryUsesFrozenFilteredSequenceAndStopsBeforeAnySourceAdvances()",
            "LocalImageSequenceTests.WrongSourceIdentityCannotAdvanceAndWrapUsesEntryIdentityRatherThanPath()"
        }.ToHashSet(StringComparer.Ordinal);
        var explicitSourceCase="SolutionManagerUiTests.Explicit_source_choice_only_suppresses_that_sources_automatic_advance()";
        var navigationCase="SolutionManagerUiTests.NavigationRunsRootSourcesOnceAndWrapsWithoutExtraAutomaticAdvance()";
        var replacedRootCase="SolutionManagerUiTests.ReplacedRootWithSameIdCannotRunAfterOldOwnerCursorResetFails()";
        var cleanupFailureCase="SolutionManagerUiTests.Continuous_run_drains_execution_when_start_and_error_observers_fail()";
        var settlementFailureCase="SolutionManagerUiTests.Continuous_run_drains_execution_when_round_settlement_and_error_observers_fail()";
        var missingFrameCase="SolutionManagerUiTests.Missing_local_image_is_consumed_and_the_next_single_run_advances()";
        var localFormatCases=(from gray in new[]{false,true} from format in new[]{"original","mono8","rgb24"}
            select $"ImageSourceDeclarationAndFormatTests.Local_file_formats_preserve_pixels_frame_and_decoded_domain({gray},{format})")
            .ToHashSet(StringComparer.Ordinal);
        var grayFormulaCase="ImageSourceDeclarationAndFormatTests.Camera_rgb_gray_conversion_matches_the_observed_vm_integer_formula()";
        var addedImportCases=new[]{
            "SolutionManagerUiTests.Manual_folder_import_expands_one_level_in_vm_entry_order_as_one_edit()",
            "SolutionManagerUiTests.Folder_import_failure_after_an_earlier_directory_does_not_accept_a_partial_batch()",
            "SolutionManagerUiTests.Import_reentrant_selection_keeps_formal_batch_without_reporting_the_old_projection()"
        }.ToHashSet(StringComparer.Ordinal);
        var addedStopMethods=new[]{"Single_and_all_stop_do_not_hold_execution_lock_during_cancellation",
            "Stop_all_requests_cancellation_for_remaining_runs_when_one_callback_fails"}.ToHashSet(StringComparer.Ordinal);
        var addedStopCases=new[]{
            "SolutionTransitionTests.Single_and_all_stop_do_not_hold_execution_lock_during_cancellation(False)",
            "SolutionTransitionTests.Single_and_all_stop_do_not_hold_execution_lock_during_cancellation(True)",
            "SolutionTransitionTests.Stop_all_requests_cancellation_for_remaining_runs_when_one_callback_fails()"
        }.ToHashSet(StringComparer.Ordinal);
        var executed=new HashSet<string>(StringComparer.Ordinal);
        var diagnosticCase=Environment.GetEnvironmentVariable("S2_DIAGNOSTIC_CASE");
        if(!string.IsNullOrEmpty(diagnosticCase)&&diagnosticCase!="ResultWorkspaceProductionTests.Real_result_host_keeps_the_horizontal_scrollbar_accessible_for_1051_images_and_releases_the_session()")
            throw new InvalidOperationException("未批准的单例诊断入口。");
        var results=new List<object>();int failures=0,covered=0;
        foreach(var name in new[]{"LocalResultImagePreviewTests","ImageSourceDesignCompositionTests","SolutionManagerUiTests","ResultWorkspaceProductionTests","ResultPresentationModelTests","ResultPresentationPresenterTests","ResultImagePaneTests","ResultViewportFullscreenScopeTests","NativeResultCompositionTests","RootResultImagePreviewTests","ImageSourceAcceptanceTests","ImageSourceRuntimeTests","ImageSourceDeclarationAndFormatTests","ImageSourceDebugInvocationTests","ImageSourceConfigurationHistoryTests","DocumentHistoryCoordinatorTests","ImageSourceSavePersistenceTests","LocalImageConfigurationSnapshotTests","LocalImageSequenceTests","ImageSourceResolverTests","CameraStreamOwnershipTests","CameraWorkflowFrameFeedTests","SolutionTransitionTests"})
        {
            var type=assembly.GetType("SunEyeVision.UI.Tests."+name)
                ?? assembly.GetType("SunEyeVision.ImageSource.Runtime.Tests."+name,true)!;
            foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).OrderBy(m=>m.Name))
            {
                if(name=="SolutionManagerUiTests"&&method.Name!="Source_items_reentry_stops_refresh_after_release_or_owner_change"&&method.Name!="First_button_uses_original_auto_switch_reset_position"&&!originals.Contains(name+"."+method.Name+"()")&&!expectedOriginalCases.Contains(name+"."+method.Name+"()")
                    &&!addedImportCases.Contains(name+"."+method.Name+"()")&&name+"."+method.Name+"()"!=explicitSourceCase&&name+"."+method.Name+"()"!=navigationCase&&name+"."+method.Name+"()"!=replacedRootCase&&name+"."+method.Name+"()"!=cleanupFailureCase&&name+"."+method.Name+"()"!=settlementFailureCase&&name+"."+method.Name+"()"!=missingFrameCase)continue;
                if(name=="SolutionTransitionTests"&&!addedStopMethods.Contains(method.Name))continue;
                var args=new List<object?[]>();
                var attributes=method.GetCustomAttributesData();
                if(attributes.Any(a=>a.AttributeType.FullName=="Xunit.FactAttribute"))args.Add(Array.Empty<object?>());
                foreach(var inline in attributes.Where(a=>a.AttributeType.FullName=="Xunit.InlineDataAttribute"))
                    args.Add(((IReadOnlyCollection<CustomAttributeTypedArgument>)inline.ConstructorArguments[0].Value!).Select(a=>a.Value).ToArray());
                foreach(var values in args)
                {
                    var label=name+"."+method.Name+"("+string.Join(",",values)+")";
                    if(!string.IsNullOrEmpty(diagnosticCase)&&label!=diagnosticCase)continue;
                    if(!executed.Add(label))throw new InvalidOperationException("重复图像源用例："+label);
                    bool wasFailed=originals.Contains(label);if(wasFailed)covered++;
                    Console.WriteLine("START "+label);Console.Out.Flush();
                    try
                    {
                        var instance=Activator.CreateInstance(type)!;
                        try{if(method.Invoke(instance,values) is Task task)task.GetAwaiter().GetResult();}
                        finally{(instance as IDisposable)?.Dispose();}
                        results.Add(new{name=label,passed=true,originalFailure=wasFailed});Console.WriteLine("PASS "+label);Console.Out.Flush();
                    }
                    catch(Exception error)
                    {
                        failures++;var cause=error.InnerException??error;
                        results.Add(new{name=label,passed=false,originalFailure=wasFailed,error=cause.ToString()});Console.WriteLine("FAIL "+label+"\n"+cause);Console.Out.Flush();
                    }
                }
            }
        }
        if(!string.IsNullOrEmpty(diagnosticCase))
        {
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"image-source-single-case-diagnostic.json"),
                JsonSerializer.Serialize(new{diagnosticOnly=true,fullAcceptance=false,count=results.Count,failures,results},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("仅单例诊断，不构成完整回归通过。");Console.Out.Flush();
            return 2;
        }
        // Four design theories are serialized with Boolean.ToString in the original evidence.
        foreach(var firstCase in new[]{"False,False,0","True,False,2","False,True,1","True,True,2"})
            if(!executed.Contains("SolutionManagerUiTests.First_button_uses_original_auto_switch_reset_position("+firstCase+")"))
                throw new InvalidOperationException("S1原件首图组合未执行："+firstCase);
        if(covered!=21)throw new InvalidOperationException("本批必须逐项承接21个原失败，实际："+covered);
        var missing=currentRequiredCases.Except(executed,StringComparer.Ordinal).OrderBy(x=>x).ToArray();
        if(missing.Length!=0)throw new InvalidOperationException("原配置、运行、调试、结果模型、展示器或视口用例缺失："+string.Join("；",missing));
        if(!executed.Contains(explicitSourceCase))throw new InvalidOperationException("显式选图来源隔离回归用例缺失。");
        if(!executed.Contains(navigationCase))throw new InvalidOperationException("根流程图像导航回归用例缺失。");
        if(!executed.Contains(replacedRootCase))throw new InvalidOperationException("根流程替换游标阻断回归用例缺失。");
        if(!executed.Contains(cleanupFailureCase))throw new InvalidOperationException("连续运行清理异常回归用例缺失。");
        if(!executed.Contains(settlementFailureCase))throw new InvalidOperationException("图像读取轮次结算异常回归用例缺失。");
        if(!executed.Contains(missingFrameCase))throw new InvalidOperationException("正式单步缺图后续读取回归用例缺失。");
        var missingLocalFormats=localFormatCases.Except(executed,StringComparer.Ordinal).OrderBy(x=>x).ToArray();
        if(missingLocalFormats.Length!=0)throw new InvalidOperationException("本地图像格式回归用例缺失："+string.Join("；",missingLocalFormats));
        if(!executed.Contains(grayFormulaCase))throw new InvalidOperationException("VM 灰度公式回归用例缺失。");
        var missingSequence=sequenceCases.Except(executed,StringComparer.Ordinal).OrderBy(x=>x).ToArray();
        if(missingSequence.Length!=0)throw new InvalidOperationException("图像读取序列回归用例缺失："+string.Join("；",missingSequence));
        var missingImport=addedImportCases.Except(executed,StringComparer.Ordinal).OrderBy(x=>x).ToArray();
        if(missingImport.Length!=0)throw new InvalidOperationException("手动文件夹导入回归用例缺失："+string.Join("；",missingImport));
        var missingStop=addedStopCases.Except(executed,StringComparer.Ordinal).OrderBy(x=>x).ToArray();
        if(missingStop.Length!=0)throw new InvalidOperationException("取消锁序回归用例缺失："+string.Join("；",missingStop));
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"image-source-recovery-report.json"),
            JsonSerializer.Serialize(new{count=results.Count,failures,originalFailuresCovered=covered,
                testAssemblyPath=assembly.Location,testAssemblySha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(assembly.Location))),
                runtimeProbeAssemblyPath=probeAssembly.Location,runtimeProbeAssemblySha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(probeAssembly.Location))),
                originalAuditedCasesCovered=expectedOriginalCases.Count-cameraReplacements.Length,
                retiredCameraCases=cameraReplacements.Select(item=>new { historical=item.Old,replacement=item.New,reason="相机保留原始格式；本地格式转换单独验证" }).ToArray(),
                scope="原21失败、原审计216项及6项相机行为替代、配置/保存/历史/接纳/来源、图像源运行/调试/格式、真实本地预览、设计组合与结果工作区、结果模型/展示器及结果视口/根窗口和相关相机与工作区用例；历史相机转换语义已退役，不能计作原断言通过。展示器与调试夹具已切现行快照，历史展示断言不代表历史存储产品已实现。原249整体未重跑。直接编译测试源码、正式产品引用及共享实际Dispatcher。",results},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"图像源失败承接：{results.Count-failures}通过，{failures}失败；覆盖原失败{covered}项。");
        return failures==0?0:1;
    }
}
