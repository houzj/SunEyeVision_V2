using SunEyeVision.Plugin.Editing.Wpf;
using SunEyeVision.Plugin.Runtime;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Windows;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using AppsPopup=SunEyeVision.Plugin.Wpf.Controls.Popups.PopupButton;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Workflow;

internal static class S2DocumentEditingCases
{
    private static int _passed;
    [STAThread] private static int Main()
    {
        try{return Run();}catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static int Run()
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        SunEyeVision.Plugin.SDK.Vision.Hosting.VisionImageFactoryLocator.Current=new SunEyeVision.HalAdapter.HalImageFactory();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/NodeStyleResources.xaml","Resources/Styles/ConnectionStyleResources.xaml","Resources/Styles/CanvasStyleResources.xaml","Resources/Styles/CommonStyleResources.xaml","Resources/Styles/MenuStyleResources.xaml","Resources/Styles/ToolTipStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/SunEyeVision.UI;component/"+resource)});
        var dialogs=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
        dialogs.Tick+=(_,_)=>{foreach(var dialog in app.Windows.OfType<Window>().Where(w=>w.GetType().Name.Contains("MessageDialog")).ToArray())
            {Console.Error.WriteLine("产品提示："+string.Join(" | ",Logical(dialog).OfType<TextBlock>().Select(t=>t.Text)));dialog.Close();}};dialogs.Start();
        var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;var plugins=new PluginManager();plugins.LoadPlugins();
        plugins.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(m=>m.Id));PluginLoader.MarkAsLoaded();
        var folder=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"document");Directory.CreateDirectory(folder);
        ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(folder);
        var solutions=ServiceInitializer.SolutionManager;var vm=new MainWindowViewModel();solutions.CreateUntitledSolution();
        if(vm.WorkflowTabViewModel.SelectedTab is null)vm.NewWorkflowCommand.Execute(null);
        var resultHost=new ContentControl();resultHost.SetBinding(ContentControl.ContentProperty,new System.Windows.Data.Binding(nameof(MainWindowViewModel.SelectedNodeDataAnalysisControl)){Source=vm});
        var shell=new Window{Width=620,Height=400,Content=resultHost,Left=-10000,ShowActivated=false,ShowInTaskbar=false};app.MainWindow=shell;shell.Show();
        var node=vm.WorkflowTabViewModel.SelectedTab!.CreateNode(BlobTool.Id);vm.AddNodeToWorkflow(node);
        node.DebugWindowStyle=DebugWindowStyle.Custom;
        var threshold=BindingId("LowThreshold");
        const string modeId="field:8ca20000000040008000000000000001";
        DefaultDebugWindow Open()=>OpenFor(node);
        DefaultDebugWindow OpenFor(WorkflowNodeBase target)
        {
            vm.OpenDebugWindowCommand.Execute(target);Pump();
            var window=app.Windows.OfType<DefaultDebugWindow>().SingleOrDefault(w=>w.IsVisible)??throw new Exception("实际打开命令没有创建Runtime窗口");
            window.UpdateLayout();Pump();return window;
        }
        RuntimeNodeConfigurationControl Control(DefaultDebugWindow window)=>(RuntimeNodeConfigurationControl)((ContentControl)window.FindName("ContentHost")).Content;
        TextBox Threshold(DefaultDebugWindow window)
        {
            var control=Control(window);Logical(control).OfType<TabControl>().Single().SelectedIndex=1;window.UpdateLayout();Pump();
            return (TextBox)Logical(control).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==threshold).InputElement!;
        }
        ReferenceSetting? ImageBinding(WorkflowNodeBase value)=>value.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) as ReferenceSetting;
        int Formal()=>(int)((BlobSettings)node.RuntimeConfiguration!.CaptureSettings()).Threshold;
        WorkflowContext Context()=>(WorkflowContext)typeof(MainWindowViewModel).GetProperty("CurrentWorkflowContext",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vm)!;
        var path=Path.Combine(folder,"runtime-editor.solution");
        Require(solutions.SaveCurrentDocument(path).FileWritten,"初始化测试文件位置失败");
            var source=vm.WorkflowTabViewModel.SelectedTab!.CreateNode("ImageLoad");
            var imageFile=Path.Combine(folder,"input.png");var png=new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(8,8,96,96,PixelFormats.Gray8,null,Enumerable.Repeat((byte)200,64).ToArray(),8)));
            using(var output=File.Create(imageFile))png.Save(output);
            Require(source.RuntimeConfiguration?.ToolId=="ImageLoad","图像源UI工厂未创建实际Runtime配置");
            var sourcePolicy=ImageSourcePolicy.Read(source)??throw new Exception("图像源没有正式字段投影");
            sourcePolicy.FilePaths=new(){imageFile};sourcePolicy.CurrentIndex=0;sourcePolicy.ForRunPaths=new(){imageFile};sourcePolicy.SNInitialValue=1;sourcePolicy.ApplyTo(source);
            vm.AddNodeToWorkflow(source);

        GlobalVariable? uiVariable=null;
        {
            var host=solutions.Session!.Editing.Host;var variables=host.BeginVariableEdit();
            uiVariable=new GlobalVariable("验收阈值",77d,"Double","验收参数");
            variables.Configuration.GlobalVariables.Add(uiVariable);
            host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),variables);
        }
        var first=Open();
        {
            var formalOpening=node.RuntimeConfiguration!;
            var savedOpening=File.ReadAllBytes(path);
            var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(first))!;
            var tabs=Logical(Control(first)).OfType<TabControl>().Single();
            var input=Logical(Control(first)).OfType<DeclaredFieldEditor>().Single(field=>field.FieldId==BlobTool.ImageInput);
            var combo=(ComboBox)input.InputElement!;
            Case("真实MainWindow打开基本页与正式来源下拉",()=>
            {
                Require(tabs.SelectedIndex==0&&combo.IsLoaded&&combo.IsVisible,"实际参数窗未从基本页打开或来源控件未加载");
                Require(node.RuntimeConfiguration?.SettingsType==typeof(BlobSettings)&&first.EditorControl is null
                    &&Control(first).GetType()==typeof(RuntimeNodeConfigurationControl),"窗口借用SDK Blob参数或未保留Runtime配置");
                Require(Logical(Control(first)).OfType<DeclaredInputFieldsView>().Single().Fields.Count==4,"四个真实输入未进入窗口");
                Require(solutions.Session!.Editing.RegisteredScopeCount==1,"实际窗口未注册唯一编辑范围");
                Require(combo.Items.OfType<ComboBoxItem>().Any(item=>item.Tag is ReferenceSetting reference
                    &&reference.Reference.NodeId==source.Id&&reference.Reference.PortId=="output.image"),"正式图像源输出没有进入真实下拉");
                Capture(first,"runtime-editor-ui-stage.png");
            });
            Case("来源选择仅写私有字段且展示中文声明",()=>
            {
                combo.IsDropDownOpen=true;Pump();
                var choice=combo.Items.OfType<ComboBoxItem>().Single(item=>item.Tag is ReferenceSetting reference
                    &&reference.Reference.NodeId==source.Id&&reference.Reference.PortId=="output.image");
                combo.SelectedItem=choice;combo.IsDropDownOpen=false;Pump();
                Require(context.DraftNode.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) is ReferenceSetting selected
                    &&selected.Reference.NodeId==source.Id&&selected.Reference.PortId=="output.image"
                    &&node.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) is UnsetSetting,"真实来源选择未写私有草稿或污染正式配置");
                Require(choice.Content?.ToString()?.Contains("图像",StringComparison.Ordinal)==true
                    &&choice.Content?.ToString()?.Contains("output.image",StringComparison.Ordinal)!=true,"来源下拉没有消费正式中文显示声明");
            });
        Case("图像下拉拒绝真实自引用并恢复原草稿，刷新不改引用，修正后可继续",()=>
        {
            var input=Logical(Control(first)).OfType<DeclaredFieldEditor>().Single(field=>field.FieldId==BlobTool.ImageInput);
            var combo=(ComboBox)input.InputElement!;var priorItem=(ComboBoxItem)combo.SelectedItem;
            var reference=((ReferenceSetting)priorItem.Tag).Reference;
            var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(first))!;
            var expression=ImageBinding(context.DraftNode)!;
            var invalid=new ComboBoxItem{Content="自引用拒绝检查",Tag=new ReferenceSetting(new OutputReference(node.Id,BlobSchema.Field(5),
                TypeRef.ResourceOf(ResourceKind.Image),scopeId:reference.ScopeId))};
            combo.Items.Add(invalid);combo.SelectedItem=invalid;Pump();
            Require(input.HasInvalidText&&ReferenceEquals(combo.SelectedItem,priorItem)
                &&Equals(ImageBinding(context.DraftNode),expression),
                "自引用未被生产合同拒绝或污染了原来源");
            combo.Items.Remove(invalid);
            Require(!combo.Items.OfType<ComboBoxItem>().Any(item=>item.Tag is UnsetSetting),"必填图像来源不应提供清除项");
            combo.IsDropDownOpen=true;Pump();
            var validItem=combo.Items.OfType<ComboBoxItem>().Single(item=>item.Tag is ReferenceSetting candidate&&candidate.Reference.NodeId==reference.NodeId
                &&candidate.Reference.PortId==reference.PortId&&candidate.Reference.ScopeId==reference.ScopeId&&candidate.Reference.MemberFieldPath.SequenceEqual(reference.MemberFieldPath));
            validItem.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left)
                {RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent});Pump();
            Require(!input.HasInvalidText,"修正来源后仍无法继续");
            combo.IsDropDownOpen=true;Pump();combo.IsDropDownOpen=false;
            Require(Equals(ImageBinding(context.DraftNode),expression)
                &&node.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) is UnsetSetting,"刷新候选写入了草稿或正式节点");
        });
            Case("未修改数值或未接入项时整窗仍明确拒绝调试确认保存",()=>
            {
                var rejected=false;var execution=Control(first).RunDebugAsync();PumpUntil(()=>execution.IsCompleted);
                try{execution.GetAwaiter().GetResult();}catch(InvalidOperationException failure)when(IsUnavailableBoundary(failure.Message)){rejected=true;}
                Require(rejected&&context.DebugOwner.ActiveRuns==0,"默认页面进入未闭合的算法调试");
                Require(!first.TryConfirm(out var boundary)&&IsUnavailableBoundary(boundary)&&first.IsVisible,"默认页面绕过整窗保护确认");
                vm.SaveCurrentSolutionCommand.Execute(null);Pump();
                Require(savedOpening.SequenceEqual(File.ReadAllBytes(path))&&ReferenceEquals(formalOpening,node.RuntimeConfiguration),"默认页面绕过整窗保护保存或发布");
            });
            Case("真实九参数控件加载、规范默认及正式配置隔离",()=>
            {
                Threshold(first);var fields=Logical(Control(first)).OfType<DeclaredFieldsView>().Single();
                Require(fields.Fields.Select(f=>f.FieldId).Distinct().Count()==55,"真实Blob设置字段不是55项");
                var names=new[]{"LowThreshold","HightThreshold","FindNum","MinArea","MaxArea","MinCircularity","MaxCircularity","MinRectangularity","MaxRectangularity"};
                object[] expected={100L,150L,100L,10L,999999999L,.1,1d,.1,1d};
                for(var i=0;i<names.Length;i++)
                {
                    var field=fields.Fields.Single(f=>f.FieldId==BindingId(names[i]));
                    Require(field.InputElement is TextBox,"九参数没有实际数值编辑器："+names[i]);
                    Require(field.Setting is ConstantSetting constant&&Equals(constant.Value,expected[i]),"九参数控件默认错误："+names[i]);
                }
                foreach(var advanced in Visual(Control(first)).OfType<NativeAdvancedPresenter>())advanced.IsExpanded=true;
                var production=fields.Fields;var sortFeature=production.Single(field=>field.FieldId==SettingId("SortFeature"));var choices=(ComboBox)sortFeature.InputElement!;
                first.UpdateLayout();Pump();
                var advancedOrder=new[]{"ui-only-run.centroid-bias","ui-only-run.axis-ratio",SettingId("SortFeature"),SettingId("SortMode"),SettingId("Connectivity")};
                var actualEditors=Logical(Control(first)).OfType<DeclaredFieldEditor>().ToArray();
                var positions=advancedOrder.Select(id=>actualEditors.Single(field=>field.FieldId==id).TransformToAncestor(first).Transform(new Point()).Y).ToArray();
                Require(positions.All(double.IsFinite)&&positions.Zip(positions.Skip(1),(left,right)=>left<right).All(value=>value),"真实高级区行序未保持质心偏移/轴比/排序特征/方向/连通："+string.Join(",",positions));
                var judgment=Logical(Control(first)).OfType<NativeJudgmentUiDraftControls>().Single();
                Require(!judgment.IsEnabled&&judgment.ToolTip is string&&ToolTipService.GetShowOnDisabled(judgment),"禁用判定组合未保留可用的未实现提示");
                Require(choices.Items.Count==13&&choices.Items.Cast<DeclaredChoiceDisplayItem>().Count(item=>item.IsAvailable)==6,"原13项排序未保留或未实现项被开放");
                Require(sortFeature.TrySetConstant("SortFeaturePerimeter")&&production.Single(field=>field.FieldId==SettingId("SortMode")).TrySetConstant("SortModeAscend")&&production.Single(field=>field.FieldId==SettingId("Connectivity")).TrySetConstant("Connected_4")&&production.Single(field=>field.FieldId==SettingId("OutputBinaryImage")).TrySetConstant(false),"正式排序/连通/二值开关未接纳同一草稿");
                Require(!Logical(Control(first)).OfType<DeclaredFieldEditor>().Any(field=>field.FieldId is "ui-only-run.sort-feature" or "ui-only-run.sort-mode" or "ui-only-run.connectivity" or "ui-only-run.binary-output"),"正式字段仍被重复UI-only覆盖");

                foreach(var enabledName in new[]{"IsCircularityFilterEnabled","IsRectangularityFilterEnabled"})
                    Require(fields.Fields.Single(f=>f.FieldId==SettingId(enabledName)).TrySetConstant(true),"真实过滤开关不可接纳");
                first.UpdateLayout();Pump();
                foreach(var name in names.Where(value=>value!="HightThreshold"))
                    Require(fields.Fields.Single(f=>f.FieldId==BindingId(name)).InputElement is {IsLoaded:true},"激活后九参数编辑器未加载："+name);
                Require(!fields.Fields.Single(f=>f.FieldId==BindingId("HightThreshold")).View.IsVisible,"单阈值模式没有隐藏上阈值");
                Require(!Visual(Control(first)).OfType<FrameworkElement>().Any(e=>e.GetType().Name=="NativeThresholdUiDraftControls"&&e.IsVisible),"页面仍展示旧阈值占位");
                Require(fields.Fields.Single(f=>f.FieldId==threshold).TrySetConstant(77L),"真实阈值控件拒绝合法整数");Pump();
                Require(fields.Fields.Single(f=>f.FieldId==threshold).Setting is ConstantSetting{Value:77L}&&Formal()==100,"真实阈值编辑未保留私有值或污染正式值");
                Capture(first,"runtime-editor-nine-fields.png");
            });
            Case("六模式真实Choice只开放单阈值，禁项程序选择也不写入字段",()=>
            {
                var field=Logical(Control(first)).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==modeId);
                var choice=field.InputElement as ComboBox??throw new Exception("模式未使用实际Choice控件");
                Require(choice.IsLoaded&&choice.IsVisible&&choice.Items.Count==6&&Equals(choice.SelectedValue,"SingleThreshold"),"模式没有六个实际选项或默认单阈值");
                choice.IsDropDownOpen=true;Pump();
                var items=choice.Items.Cast<object>().ToArray();var keys=items.Select(item=>ChoiceKey(choice,item)).ToArray();var enabled=0;
                Require(keys.SequenceEqual(new[]{"BinaryImage","SingleThreshold","DoubleThreshold","AutoThreshold","FixedSoftThreshold","RelativeSoftThreshold"}),"六Choice缺失正式模式身份或包含重复项");
                foreach(var item in items)
                {
                    var key=ChoiceKey(choice,item);var container=item as ComboBoxItem??choice.ItemContainerGenerator.ContainerFromItem(item) as ComboBoxItem;
                    Require(container is not null,"实际Choice下拉项未生成容器："+key);
                    Require(container!.IsEnabled==(key=="SingleThreshold"),"模式可用状态错误："+key);if(container.IsEnabled)enabled++;
                }
                Require(enabled==1,"模式可选集合不是仅单阈值");
                var disabledKeys=items.Select(item=>ChoiceKey(choice,item)).Where(key=>key!="SingleThreshold").ToArray();
                foreach(var key in disabledKeys)
                {
                    choice=(ComboBox)field.InputElement!;choice.IsDropDownOpen=true;Pump();
                    choice.SelectedItem=choice.Items.Cast<object>().Single(item=>ChoiceKey(choice,item)==key);Pump();
                    Require(field.Setting is ConstantSetting{Value:"SingleThreshold"}&&Equals(choice.SelectedValue,"SingleThreshold"),"禁用模式通过程序选择进入草稿："+key);
                    Require(field.TrySetConstant("SingleThreshold"),"拒绝模式后无法恢复可用值");
                }
                choice=(ComboBox)field.InputElement!;choice.IsDropDownOpen=false;Pump();
                Require(ReferenceEquals(node.RuntimeConfiguration,formalOpening),"模式试选修改正式配置");
                Capture(first,"runtime-editor-six-modes.png");
            });
            Case("未接入占位项全部禁用且提供说明，不能冒充正式参数",()=>
            {
                var placeholders=Logical(Control(first)).OfType<DeclaredFieldEditor>().Where(f=>f.FieldId.StartsWith("ui-only-run.",StringComparison.Ordinal)).ToArray();
                Require(placeholders.Length>0&&placeholders.All(field=>!field.IsEnabled&&field.ToolTip is string),"未接入项仍可编辑或缺少说明");
                Require(solutions.Session!.Editing.Host.GetVariableNames(solutions.CurrentSolution!.Id).ContainsKey(uiVariable!.Id),"真实变量候选准备丢失");
                tabs.SelectedIndex=0;Pump();
            });
            Case("合法九参数编辑仍阻止整窗调试与确定，不取得算法运行租约",()=>
            {
                var before=Context().GetNodeResult(node.Id)?.Result;
                var task=Control(first).RunDebugAsync();PumpUntil(()=>task.IsCompleted);
                var rejected=false;
                try{task.GetAwaiter().GetResult();}
                catch(InvalidOperationException failure) when(IsUnavailableBoundary(failure.Message)){rejected=true;}
                Require(rejected&&context.DebugOwner.ActiveRuns==0&&ReferenceEquals(before,Context().GetNodeResult(node.Id)?.Result),
                    "纯UI调试未明确拒绝、遗留运行租约或修改正式结果");
                Require(!first.TryConfirm(out var error)&&IsUnavailableBoundary(error)
                    &&first.IsVisible&&ReferenceEquals(node.RuntimeConfiguration,formalOpening),"纯UI确定未拒绝或提前关闭/发布");
            });
            Case("未接入界面状态阻断保存，磁盘与正式字段保持不变",()=>
            {
                vm.SaveCurrentSolutionCommand.Execute(null);Pump();
                Require(savedOpening.SequenceEqual(File.ReadAllBytes(path))
                    &&ReferenceEquals(node.RuntimeConfiguration,formalOpening),"未接入项修改写入文件或正式配置");
            });
            Case("非法数值未失焦及隐藏后仍不能保存确认，原文保留至取消",()=>
            {
                var text=Threshold(first);text.Focus();text.Text="invalid";Pump();
                var field=Logical(Control(first)).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==threshold);
                Require(!field.TryCommitPending()&&field.HasInvalidText&&text.Text=="invalid","非法数值没有保留明确错误及原文");
                var disk=File.ReadAllBytes(path);first.Hide();vm.SaveCurrentSolutionCommand.Execute(null);Pump();
                Require(disk.SequenceEqual(File.ReadAllBytes(path))&&!first.TryConfirm(out _)&&ReferenceEquals(formalOpening,node.RuntimeConfiguration),"隐藏非法输入被保存确认或污染正式配置");
                first.Show();Pump();
            });
            var retiredDraft=context.DraftNode;var retiredConfiguration=retiredDraft.RuntimeConfiguration;
            first.Close();Pump();
            var reopened=Open();var reopenedContext=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(Control(reopened))!;
            Case("关闭取消重开回读正式配置且旧来源控件不能写新会话",()=>
            {
                Require(!context.IsValid&&solutions.Session!.Editing.RegisteredScopeCount==1
                    &&reopenedContext.DraftNode.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) is UnsetSetting,"旧范围未退役或重开继承了已取消的来源");
                combo.SelectedItem=combo.Items.OfType<ComboBoxItem>().FirstOrDefault(item=>item.Tag is ReferenceSetting reference
                    &&reference.Reference.NodeId==source.Id&&reference.Reference.PortId=="output.gray-image");Pump();
                Require(ReferenceEquals(retiredDraft.RuntimeConfiguration,retiredConfiguration)
                    &&reopenedContext.DraftNode.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput) is UnsetSetting
                    &&ReferenceEquals(node.RuntimeConfiguration,formalOpening),"退役来源回调写入旧草稿、新范围或正式配置");
                Require(Logical(Control(reopened)).OfType<TabControl>().Single().SelectedIndex==0,"重开未回到基本参数页");
            });
            reopened.Close();Pump();
            Require(solutions.Session!.Editing.RegisteredScopeCount==0,"真实窗口全部关闭后编辑范围未清空");
            var modeBaselinePath=Path.Combine(folder,"mode-window-baseline.solution");
            Require(new SolutionRepository().Save(solutions.CurrentSolution!,modeBaselinePath),"准备真实旧模式窗口基线失败");
            var modeBaseline=File.ReadAllText(modeBaselinePath);
            var modeScope=solutions.CurrentSolution!.Workflows.Single(flow=>flow.Nodes.Any(value=>value.Id==node.Id)).Id;
            foreach(var oldMode in new[]{BlobSegmentationMode.None,BlobSegmentationMode.DoubleThreshold})
            Case("旧模式窗口原值与整窗保护，私有修正关闭重开："+oldMode,()=>
            {
                var legacy=System.Text.Json.Nodes.JsonNode.Parse(modeBaseline)!;
                var modeConfiguration=formalOpening.WithSettings(new BlobSettings{SegmentationMode=oldMode}).WithFieldSettings(
                    new Dictionary<string,ParameterSetting>{{BlobTool.ImageInput,new ReferenceSetting(new OutputReference(source.Id,SunEyeVision.Tool.ImageSource.Runtime.ImageSourceOutput.Image,SunEyeVision.Tool.ImageSource.Runtime.ImageSourceOutput.ImageType,new[]{SunEyeVision.Tool.ImageSource.Runtime.ImageSourceOutput.ImageData},scopeId:modeScope))}});
                var serialized=System.Text.Json.JsonSerializer.SerializeToNode(modeConfiguration);
                var jsonNode=legacy["Workflows"]!.AsArray().SelectMany(flow=>flow!["Nodes"]!.AsArray()).Single(value=>value!["Id"]!.GetValue<string>()==node.Id)!;
                jsonNode["RuntimeConfiguration"]=serialized;
                var oldPath=Path.Combine(folder,"old-mode-"+oldMode+".solution");File.WriteAllText(oldPath,legacy.ToJsonString());
                var load=new SolutionRepository().LoadCandidate(oldPath);var loaded=load.Candidate??throw new Exception("旧模式真实文件读取失败");
                solutions.SetCurrentSolution(loaded,registerInCatalog:false);Pump();
                var target=loaded.Workflows.SelectMany(flow=>flow.Nodes).Single(value=>value.Id==node.Id);
                var window=OpenFor(target);var control=Control(window);var owner=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(control)!;
                Logical(control).OfType<TabControl>().Single().SelectedIndex=1;window.UpdateLayout();Pump();
                var field=Logical(control).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==modeId);
                var choices=field.InputElement as ComboBox??throw new Exception("旧模式没有真实Choice");
                Require(field.Setting is ConstantSetting oldSetting&&Equals(oldSetting.Value,oldMode.ToString())&&Equals(choices.SelectedValue,oldMode.ToString()),"打开旧模式被默认值覆盖");
                var formal=target.RuntimeConfiguration;var disk=File.ReadAllBytes(oldPath);
                Require(!window.TryConfirm(out var modeError)&&IsUnavailableBoundary(modeError)&&window.IsVisible,"旧未就绪模式在窗口确认通过");
                var rejected=false;var execution=control.RunDebugAsync();PumpUntil(()=>execution.IsCompleted);
                try{execution.GetAwaiter().GetResult();}catch(InvalidOperationException failure)when(IsUnavailableBoundary(failure.Message)){rejected=true;}
                Require(rejected&&owner.DebugOwner.ActiveRuns==0,"旧未就绪模式进入调试或遗留租约");
                vm.SaveCurrentSolutionCommand.Execute(null);Pump();
                Require(File.ReadAllBytes(oldPath).SequenceEqual(disk)&&ReferenceEquals(formal,target.RuntimeConfiguration),"旧模式窗口保存覆盖磁盘或正式配置");
                Capture(window,"runtime-editor-old-mode-"+oldMode+".png");
                var borrowedField=field;var borrowedOwner=owner;
                control=Control(window);owner=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(control)!;
                Logical(control).OfType<TabControl>().Single().SelectedIndex=1;window.UpdateLayout();Pump();
                field=Logical(control).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==modeId);
                Console.WriteLine("旧模式修正前诊断：mode="+oldMode+" fieldSame="+ReferenceEquals(field,borrowedField)+" ownerSame="+ReferenceEquals(owner,borrowedOwner)
                    +" ownerValid="+owner.IsValid+" fieldLoaded="+field.View.IsLoaded+" inputLoaded="+field.InputElement?.IsLoaded+" error="+field.Error);
                Require(field.TrySetConstant("SingleThreshold"),"旧模式当前活跃Choice修正被拒绝："+field.Error);Pump();
                Require(field.Setting is ConstantSetting{Value:"SingleThreshold"}&&((BlobSettings)target.RuntimeConfiguration!.CaptureSettings()).SegmentationMode==oldMode,"修正Choice未进入私有值或提前发布");
                window.Close();Pump();
                Require(!owner.IsValid&&solutions.Session!.Editing.RegisteredScopeCount==0,"旧模式窗口关闭没有退役范围");
                var again=OpenFor(target);Logical(Control(again)).OfType<TabControl>().Single().SelectedIndex=1;again.UpdateLayout();Pump();
                Require(Logical(Control(again)).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==modeId).Setting is ConstantSetting prior&&Equals(prior.Value,oldMode.ToString()),"取消修正后重开未回读正式旧模式");
                again.Close();Pump();
            });
            shell.Close();dialogs.Stop();
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"ui-stage-report.json"),
                System.Text.Json.JsonSerializer.Serialize(new{stage="B08Window",track=Environment.GetEnvironmentVariable("S4_EDITOR_TRACK"),passed=_passed,fullFunctionalAcceptance=false,
                    remaining=new[]{"ROI/显示未接入项完整功能","实际VS与人工验收"},
                    blockedPositiveCases=new[]{"成功捕获过程同步重绑的旧结果隔离","保存接纳并重绑后旧来源控件迟到回调","未失焦数值经整窗确认保存重开","真实窗口调试与连续运行停止","私有调试预览结果表及失败清空","整窗成功确认发布后释放范围"},
                    blockedReason="Blob整窗ROI/显示尚未闭合；不得绕过保护执行旧正向用例",actualVisualStudioDesigner=false,isolatedModeAdmissionProvenByThisUiTrack=false}));
            Console.WriteLine($"B08真实编辑窗口UI链：{_passed}组通过；fullFunctionalAcceptance=false；原正向窗口项在报告中保留为阻塞，未运行Blob算法。");
            return 0;
        }
    }
    private static string SettingId(string name)=>new BlobTool().SettingsSchema.Presentation!.Fields.Single(field=>field.DisplayName==BlobParameterDefinition.Instance.Label(name)).FieldId;
    private static string BindingId(string name)=>((IToolParameterBindingNames)new BlobTool()).ParameterBindingNames.Single(pair=>pair.Value==name).Key;
    private static string ChoiceKey(ComboBox choice,object item)
    {
        Require(choice.SelectedValuePath==nameof(DeclaredChoiceDisplayItem.FormalKey),"Choice没有使用正式键绑定");
        return item is DeclaredChoiceDisplayItem display?display.DisplayId:throw new Exception("模式未使用正式公开展示快照");
    }
    private static bool IsUnavailableBoundary(string error)=>error.Contains("界面试用",StringComparison.Ordinal)||error.Contains("未接入",StringComparison.Ordinal)||error.Contains("未闭合",StringComparison.Ordinal)||error.Contains("未就绪",StringComparison.Ordinal)||error.Contains("未完成",StringComparison.Ordinal);
    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {
        var seen=new HashSet<DependencyObject>();var pending=new Stack<DependencyObject>();pending.Push(root);
        while(pending.Count>0)
        {
            var node=pending.Pop();if(!seen.Add(node))continue;yield return node;
            foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())pending.Push(child);
            if(node is SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation.ParamListView list)
                foreach(var row in list.Items.OfType<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow>())if(row.View is {} view)pending.Push(view);
        }
    }
    private static void Pump()
    {var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    private static void PumpUntil(Func<bool> complete)
    {var until=DateTime.UtcNow.AddSeconds(30);while(!complete()){if(DateTime.UtcNow>until)throw new TimeoutException("真实窗口/工作流操作未完成");Pump();Thread.Sleep(5);}Pump();}
    private static void Capture(FrameworkElement view,string name)
    {
        view.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth),(int)Math.Ceiling(view.ActualHeight),96,96,PixelFormats.Pbgra32);
        var images=Visual(view).OfType<Image>().Select(i=>new {Source=i.Source?.ToString(),Width=i.ActualWidth,Height=i.ActualHeight,
            PixelWidth=(i.Source as BitmapSource)?.PixelWidth,PixelHeight=(i.Source as BitmapSource)?.PixelHeight});
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,name+".images.json"),System.Text.Json.JsonSerializer.Serialize(images));
        bitmap.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,name));png.Save(file);
    }
    private static IEnumerable<DependencyObject> Visual(DependencyObject root)
    {yield return root;for(var n=0;n<VisualTreeHelper.GetChildrenCount(root);n++)foreach(var child in Visual(VisualTreeHelper.GetChild(root,n)))yield return child;}
    private static void Case(string name,Action action){action();_passed++;Console.WriteLine("通过："+name);}
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
}
