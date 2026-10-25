using System.IO;
using System.Windows;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Workflow;

internal static class S2DocumentEditingCases
{
    [STAThread] private static int Main()
    {
        try
        {
            if(Application.Current is null)new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;
            var manager=new PluginManager();manager.LoadPlugins(Path.Combine(product,"plugins"));
            manager.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(tool=>tool.Id));
            var engine=new WorkflowEngine(new CheckLogger());var workflow=engine.CreateWorkflow("binding-flow","声明绑定");
            WorkflowNodeBase Node(string id,int threshold)
            {
                var parameters=ToolRegistry.CreateParameters("BlobAnalysis");
                void Set(string key,object value)=>parameters.GetType().GetProperty(key)!.SetValue(parameters,value);
                Set("Threshold",threshold);Set("IsAreaFilterEnabled",false);Set("IsCircularityFilterEnabled",false);
                Set("IsRectangularityFilterEnabled",false);Set("IsBlobCountFilterEnabled",false);
                ((IConfigurationFieldProvider)parameters).GetConfigurationFields().Single(field=>field.StoragePath=="SortMode").Write("None");
                var node=new WorkflowNodeBase(id,id,id,"BlobAnalysis"){Parameters=parameters};workflow.Nodes.Add(node);return node;
            }
            var source=Node("source",100);var index=Node("index",1);var target=Node("target",220);
            var fields=((IConfigurationFieldProvider)target.Parameters).GetConfigurationFields();
            ConfigurationFieldAccessor Field(string path)=>fields.Single(field=>field.StoragePath==path);
            Port Output(string name)=>BindingClrAdapter.DescribeOutput(((IOutputContractProvider)source.Parameters).GetOutputContract().Single(output=>output.Name==name));
            void Bind(WorkflowNodeBase node,string path,OutputReference reference)=>node.ParamSettings.SetSetting(new ParamSetting {
                ParameterName=path,BindingType=BindingType.Binding,ReferenceExpression=BindingReferenceSerializer.Serialize(reference),
                SourceNodeId=reference.NodeId,SourceWorkflowId=workflow.Id,SourceProperty=reference.PortId});
            OutputReference Ref(string name,BindingSelection? selection=null,IReadOnlyList<string>? members=null)
            {var port=Output(name);return new(source.Id,port.Id,port.Type,members,selection,workflow.Id,port.Storage);}
            Bind(target,"Threshold",Ref("ThresholdMinUsed"));
            Field("IsAreaFilterEnabled").Write(true);
            Field("ResultPresentation.Judgments[Key=total-area].Enabled").Write(true);
            Field("ResultPresentation.Judgments[Key=total-area].Min").Write(0d);
            Field("ResultPresentation.Texts[Key=area].Style.PositionX").Write(99d);
            Bind(target,"ResultPresentation.Texts[Key=area].Style.PositionX",Ref("AverageArea"));
            var records=Output("FilteredBlobs");
            var recordCandidate=new DeclaredReferenceCandidate("Blob记录 / 面积",source.Id,workflow.Id,records,new[]{"area"});
            var picker=new ReferenceSelectionControl(new[]{recordCandidate},_=>null){SelectedCandidate=recordCandidate,SelectionMode=1,IndexText="1"};
            Require(picker.TryGetReference(out var chosen),"显式选择记录元素失败");
            Bind(target,"FilterAreaMin",chosen!);
            picker.IndexText="-1";Require(!picker.TryGetReference(out _),"序号选择允许负数");
            picker.IndexText="1.5";Require(!picker.TryGetReference(out _),"序号选择允许小数");
            Console.WriteLine("通过：声明记录成员与显式元素选择；非法序号保留并拒绝");

            var bytes=new byte[12*12];for(int y=1;y<4;y++)for(int x=1;x<4;x++)bytes[y*12+x]=160;
            for(int y=7;y<11;y++)for(int x=7;x<11;x++)bytes[y*12+x]=160;
            using var image=HalImage.CreateFromBytes(bytes,12,12,1);
            var executor=new WorkflowExecutionEngine(engine,manager,new CheckLogger());
            void Verify(ExecutionResult execution)
            {
                Require(execution.Success,string.Join(";",execution.Errors));
                var result=execution.NodeResults[target.Id].Result!;
                Require(Convert.ToInt32(result.GetType().GetProperty("ThresholdMinUsed")!.GetValue(result))==100,"声明阈值绑定未执行");
                Require(Convert.ToInt32(result.GetType().GetProperty("FilteredBlobCount")!.GetValue(result))==1
                    &&Convert.ToDouble(result.GetType().GetProperty("FilteredTotalArea")!.GetValue(result))==16,"记录面积投影没有进入筛选");
                Require(result.IsPass,"嵌套判定字段未应用本次绑定值");
                var texts=result.Display.Items.OfType<SunEyeVision.Plugin.SDK.Models.Visualization.TextDisplayItem>().SelectMany(item=>item.Texts).ToArray();
                Require(texts.Length==1&&texts.All(text=>text.X==12.5),"嵌套文字位置没有使用本次绑定值");
            }
            using(var run=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(workflow.Id,image)).GetAwaiter().GetResult())Verify(run);
            Require(Convert.ToInt32(target.Parameters.GetType().GetProperty("Threshold")!.GetValue(target.Parameters))==220
                &&Equals(Field("ResultPresentation.Texts[Key=area].Style.PositionX").Read(),99d),"运行值写回了正式配置");
            Console.WriteLine("通过：实际Workflow/HALCON按稳定声明绑定阈值、记录成员及嵌套文字位置；配置未被运行值污染");

            var indexPort=Output("ThresholdMinUsed");
            var indexRef=new OutputReference(index.Id,indexPort.Id,indexPort.Type,scopeId:workflow.Id,storage:indexPort.Storage);
            Bind(target,"FilterAreaMin",Ref("FilteredBlobs",new BindingSelection.AtField(indexRef),new[]{"area"}));
            using(var run=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(workflow.Id,image)).GetAwaiter().GetResult())Verify(run);
            index.Parameters.GetType().GetProperty("Threshold")!.SetValue(index.Parameters,4);
            using(var bad=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(workflow.Id,image)).GetAwaiter().GetResult())
                Require(!bad.Success,"动态序号越界使用了上一轮值");
            index.Parameters.GetType().GetProperty("Threshold")!.SetValue(index.Parameters,1);
            Console.WriteLine("通过：绑定序号加入真实依赖并按本轮值执行，越界不使用旧结果");

            var folder=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"save-reopen");Directory.CreateDirectory(folder);
            PluginLoader.MarkAsLoaded();var solutions=new SolutionManager(folder);var document=solutions.CreateUntitledSolution();
            var saveService=new SunEyeVision.UI.Services.Solutions.SolutionApplicationService(solutions);
            document.Workflows.Clear();document.Workflows.Add(workflow);var file=Path.Combine(folder,"s4-binding.solution");
            Require(saveService.Save(file,()=>solutions.Session!.Editing.AcceptAll()).FileWritten,"实际保存失败："+solutions.LastError);
            var reopened=solutions.LoadSolutionOnly(file)??throw new Exception(solutions.LastError);
            var reopenedFlow=reopened.Workflows.Single();
            var nested=reopenedFlow.Nodes.Single(node=>node.Id==target.Id).ParamSettings.GetSetting("ResultPresentation.Texts[Key=area].Style.PositionX")!;
            Require(nested.ReferenceExpression is not null&&BindingReferenceSerializer.Deserialize(nested.ReferenceExpression).PortId==Output("AverageArea").Id,"重开丢失嵌套字段正式引用");
            var reopenedEngine=new WorkflowEngine(new CheckLogger());reopenedEngine.RegisterWorkflow(reopenedFlow);
            var reopenedExecutor=new WorkflowExecutionEngine(reopenedEngine,manager,new CheckLogger());
            using(var run=reopenedExecutor.ExecuteWorkflowAsync(new WorkflowRunRequest(reopenedFlow.Id,image)).GetAwaiter().GetResult())Verify(run);
            Console.WriteLine("通过：真实保存重开保留稳定声明、嵌套字段和动态序号，再执行一致");

            var fallback=new SunEyeVision.UI.Services.Editing.DeclaredToolConfigurationControl(ToolRegistry.CreateToolInstance("BlobAnalysis")!);
            var query=new DataSourceQueryService {WorkflowId=workflow.Id,BindingCandidateNodeIds=_=>new[]{source.Id},
                FormalOutputContractReader=id=>((IOutputContractProvider)workflow.Nodes.Single(node=>node.Id==id).Parameters).GetOutputContract()};
            void BindDraft(WorkflowNodeBase draft){fallback.SetCurrentNode(draft);fallback.SetParameters(draft.Parameters);fallback.SetDataProvider(query);}
            using var context=new SunEyeVision.UI.Services.Editing.ProductionEditorContext(solutions.Session!.Editing.Host,workflow.Id,target,()=>true,BindDraft,documentEdits:solutions.Session.Editing);
            BindDraft(context.DraftNode);SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.AttachDraftContext(fallback,context);
            typeof(SunEyeVision.UI.Services.Editing.ProductionEditorContext).GetProperty("PrepareView",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!
                .SetValue(context,(Func<string?>)(()=>SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.TryPrepare(fallback,out var error)?null:error));
            typeof(SunEyeVision.UI.Services.Editing.ProductionEditorContext).GetProperty("HasPendingViewEdits",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!
                .SetValue(context,(Func<bool>)(()=>SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.HasPendingEdits(fallback)));
            var thresholdField=((IConfigurationFieldProvider)context.DraftNode.Parameters).GetConfigurationFields().Single(field=>field.StoragePath=="Threshold");
            var fieldView=((DeclaredFieldsView)fallback.Content).Fields.Single(field=>field.FieldId==thresholdField.Port.Id);
            var modalChosen=false;
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(()=>
            {
                var window=Application.Current.Windows.Cast<Window>().Single(window=>window.Title=="选择绑定来源");
                var selector=Descendants(window).OfType<ReferenceSelectionControl>().Single();
                var list=Descendants(selector).OfType<System.Windows.Controls.ListBox>().Single();
                selector.SelectedCandidate=list.Items.Cast<DeclaredReferenceCandidate>().Single(candidate=>candidate.Output.Id==Output("FilteredBlobCount").Id&&candidate.MemberPath.Count==0);
                Descendants(window).OfType<System.Windows.Controls.Button>().Single(button=>Equals(button.Content,"确定"))
                    .RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));modalChosen=true;
            }));
            Descendants(fieldView).OfType<System.Windows.Controls.Button>().Single(button=>Equals(button.Content,"绑定"))
                .RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Require(modalChosen&&context.TryCommitEditor(out var commitError),"真实来源窗口未确认到当前草稿范围");
            Require(BindingReferenceSerializer.Deserialize(target.ParamSettings.GetSetting("Threshold")!.ReferenceExpression!).PortId==Output("FilteredBlobCount").Id,"窗口选择没有成为正式字段引用");
            Require(saveService.Save(file,()=>solutions.Session!.Editing.AcceptAll()).FileWritten,"窗口确认后的绑定无法保存："+solutions.LastError);
            Console.WriteLine("通过：真实宿主来源窗口按声明选择，确定只写当前草稿，经范围确认后可保存");
            SourceSelectionField ImageSelector()=>Descendants(fallback).OfType<SourceSelectionField>().Single(view=>Equals(view.Tag,
                ((IConfigurationFieldProvider)context.DraftNode.Parameters).ConfigurationSchema!.Inputs.Single(port=>port.Type==TypeRef.ResourceOf(ResourceKind.Image)).Id));
            var imageOutput=((IOutputContractProvider)source.Parameters).GetOutputContract().Single(output=>output.Name=="GrayImage");
            var imageIdentity=source.Id+"/"+BindingClrAdapter.OutputPortId(imageOutput);
            ImageSelector().Select(source.Id+"/"+Output("FilteredBlobCount").Id);
            Require(!ImageSelector().ApplySelection()&&((MeasureGuideParameters)context.DraftNode.Parameters).ImageSourceRef is null,"数值字段误作图像来源或失败修改了草稿");
            ImageSelector().Select(imageIdentity);
            Require(ImageSelector().ApplySelection(),"实际声明图像无法选择："+ImageSelector().Error);
            Require(((MeasureGuideParameters)target.Parameters).ImageSourceRef is null,"选择来源提前污染正式节点");
            Require(context.TryCommitEditor(out var sourceError),sourceError);
            Require(((MeasureGuideParameters)target.Parameters).ImageSourceRef?.SourceFieldId==imageOutput.FieldId,"稳定图像字段ID未发布");
            using(var run=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(workflow.Id,image)).GetAwaiter().GetResult())
                Require(run.Success,string.Join(";",run.Errors));
            Require(saveService.Save(file,()=>solutions.Session!.Editing.AcceptAll()).FileWritten,"图像来源实际保存失败："+solutions.LastError);
            var sourceReopen=solutions.LoadSolutionOnly(file)!.Workflows.Single().Nodes.Single(node=>node.Id==target.Id);
            Require(((MeasureGuideParameters)sourceReopen.Parameters).ImageSourceRef?.SourceFieldId==imageOutput.FieldId,"保存重开丢失图像来源身份");
            Console.WriteLine("通过：图像来源只列正式图像声明，私有草稿确认、真实Workflow/HALCON消费及保存重开保留稳定身份");

            // The current SDK pose source adapter does not fabricate canonical affine frame labels.
            var poseId=Guid.Parse("a4210000-0000-4000-8000-000000000001");
            var poseHost=new SunEyeVision.UI.Services.Editing.DeclaredToolConfigurationControl(ToolRegistry.CreateToolInstance("BlobAnalysis")!);
            var poseParameters=ToolRegistry.CreateParameters("BlobAnalysis");poseHost.SetCurrentNode(new WorkflowNodeBase("pose-target","pose-target","pose-target","BlobAnalysis"){Parameters=poseParameters});poseHost.SetParameters(poseParameters);
            var poseDeclaration=new OutputVariable{Name="Poses",FieldId=poseId,DeclaredType=typeof(AffineMatrix[]),VariableType="Transform",IsCollection=true};
            poseHost.SetDataProvider(new DataSourceQueryService{WorkflowId=workflow.Id,BindingCandidateNodeIds=_=>new[]{"pose-source"},FormalOutputContractReader=_=>new[]{poseDeclaration}});
            var posePort=((IConfigurationFieldProvider)poseParameters).ConfigurationSchema!.Inputs.Single(port=>port.Type==SpatialDeclarations.Affine2D);
            var poseSelector=Descendants(poseHost).OfType<SourceSelectionField>().Single(view=>Equals(view.Tag,posePort.Id));
            var poseIdentity="pose-source/"+BindingClrAdapter.OutputPortId(poseDeclaration);
            poseSelector.Select(poseIdentity,"-1");Require(!poseSelector.ApplySelection()&&poseHost.HasPendingEdits,"矩阵集合接受负序号或丢失未应用状态");
            poseSelector.Select(poseIdentity,"0");Require(poseSelector.ApplySelection(),poseSelector.Error);
            poseSelector.Select(poseIdentity,"1");Require(poseSelector.ApplySelection(),poseSelector.Error);
            var poseGuide=(MeasureGuideParameters)poseParameters;
            Require(poseGuide.PositionCorrectionSource?.PropertyName=="Poses[1]"&&poseGuide.PositionCorrectionBindingSource?.GetBindingPath()=="pose-source.Poses[1]","同一输出换序号仍保留旧运行时来源");
            Require(poseSelector.ClearSource()&&poseGuide.PositionCorrectionSource is null&&poseGuide.PositionCorrectionBindingSource is null,"清除矩阵来源留下旧引用");
            Console.WriteLine("通过：现行SDK位置来源显式选序号、拒绝非法原文、更换同源序号与清除；同帧与真实位置消费另由输入边界轨验证");
            var regionGroup=((IConfigurationFieldProvider)context.DraftNode.Parameters).ConfigurationSchema!.Presentation!.Regions.Single(group=>group.Role=="inspection");
            var regionSelector=Descendants(fallback).OfType<SourceSelectionField>().Single(view=>Equals(view.Tag,regionGroup.RegionSourceFieldId));
            var regionOutput=((IOutputContractProvider)source.Parameters).GetOutputContract().Single(output=>output.Name=="Region");
            regionSelector.Select(source.Id+"/"+BindingClrAdapter.OutputPortId(regionOutput));
            Require(regionSelector.ApplySelection(),regionSelector.Error);
            var draftMode=((IConfigurationFieldProvider)context.DraftNode.Parameters).GetConfigurationFields().Single(field=>field.StoragePath=="InspectionMode");
            draftMode.Write("InheritedRegion");fallback.OnExternalResultArrived();
            Require(context.TryCommitEditor(out var regionError),regionError);
            Require(target.ParamSettings.GetSetting("InspectionRegionInput")?.SourceFieldId==regionOutput.FieldId,"像素Region未存稳定引用");
            using(var run=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(workflow.Id,image)).GetAwaiter().GetResult())
                Require(run.Success&&Convert.ToInt32(run.NodeResults[target.Id].Result!.GetType().GetProperty("FilteredBlobCount")!.GetValue(run.NodeResults[target.Id].Result))==1,string.Join(";",run.Errors));
            Require(saveService.Save(file,()=>solutions.Session!.Editing.AcceptAll()).FileWritten,"Region来源实际保存失败："+solutions.LastError);
            var regionReopen=solutions.LoadSolutionOnly(file)!.Workflows.Single().Nodes.Single(node=>node.Id==target.Id);
            Require(regionReopen.ParamSettings.GetSetting("InspectionRegionInput")?.SourceFieldId==regionOutput.FieldId,"Region来源重开丢失身份");
            Console.WriteLine("通过：实际像素Region来源绑定经确认、同帧Workflow/HALCON消费及保存重开；不复制资源到设置");

            var geometryOutput=new OutputVariable{Name="Shapes",FieldId=Guid.Parse("a4210000-0000-4000-8000-000000000002"),DeclaredType=typeof(RoiShape[]),VariableType="ROI",IsCollection=true};
            poseHost.SetDataProvider(new DataSourceQueryService{WorkflowId=workflow.Id,BindingCandidateNodeIds=_=>new[]{"geometry-source"},FormalOutputContractReader=_=>new[]{geometryOutput}});
            var geometryParameters=(RoiParameters)poseParameters;
            var local=Roi.CreateLocal("本地底稿",new SunEyeVision.Plugin.SDK.Models.Geometry.Circle2d(4,4,2));geometryParameters.InspectionRois.Rois.Add(local);
            var geometryGroup=((IConfigurationFieldProvider)poseParameters).ConfigurationSchema!.Presentation!.Regions.Single(group=>group.Role=="inspection");
            var geometrySelector=Descendants(poseHost).OfType<SourceSelectionField>().Single(view=>Equals(view.Tag,geometryGroup.GeometrySourceFieldId));
            geometrySelector.Select("geometry-source/"+BindingClrAdapter.OutputPortId(geometryOutput));Require(geometrySelector.ApplySelection(),geometrySelector.Error);
            var subscribed=geometryParameters.InspectionRois.Rois.Single(item=>item.IsSubscribed);
            Require(ReferenceEquals(geometryParameters.InspectionRois.Rois.Single(item=>item.IsLocal),local)&&subscribed.Binding?.SourceFieldId==geometryOutput.FieldId,"几何订阅覆盖本地底稿或丢失字段ID");
            var declaredInput=BindingInputCatalog.Describe(poseParameters).Single(input=>input.Key=="InspectionRois:"+subscribed.Id);
            Require(declaredInput.ReadFieldId?.Invoke()==geometryOutput.FieldId,"几何订阅输入目录遗漏稳定字段身份");
            Require(geometrySelector.ClearSource()&&geometryParameters.InspectionRois.Rois.Count==1&&ReferenceEquals(geometryParameters.InspectionRois.Rois[0],local),"清除订阅删除了本地底稿");
            Console.WriteLine("通过：几何来源只接正式ROI集合，独立订阅项保留稳定身份；清除只删除该项，本地底稿不受影响");
            var original=File.ReadAllBytes(file);
            var minField=((IConfigurationFieldProvider)context.DraftNode.Parameters).GetConfigurationFields().Single(field=>field.StoragePath=="FilterAreaMax");
            var invalidText=(System.Windows.Controls.TextBox)((DeclaredFieldsView)fallback.Content).Fields.Single(field=>field.FieldId==minField.Port.Id).InputElement!;
            invalidText.Text="invalid";
            Require(fallback.HasPendingEdits&&!saveService.Save(file,()=>solutions.Session!.Editing.AcceptAll()).FileWritten
                &&invalidText.Text=="invalid"&&original.SequenceEqual(File.ReadAllBytes(file)),"隐藏的非法文本被保存或丢弃，或原文件被覆盖");
            Console.WriteLine("通过：默认页非法原文进入正式保存准备；拒绝保存并保留原文件和原文");
            Console.WriteLine("S4声明绑定：10组通过；完整窗口组装及整链仍待核收。");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach(var item in LogicalTreeHelper.GetChildren(parent))if(item is DependencyObject child)
        {yield return child;foreach(var descendant in Descendants(child))yield return descendant;}
    }
    private sealed class CheckLogger:ILogger
    {
        public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All){ }
        public ILogger ForSource(string source)=>this;
    }
}
