using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Runtime;
using System.IO;
using System.Reflection;
using System.Windows.Data;
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
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell;
using SunEyeVision.UI.Shell.Controls.Shell;

internal static class S2DocumentEditingCases
{
    private static int _passed;
    [STAThread] private static int Main()
    {
        var result=1;
        var thread=new Thread(()=>
        {
            try{EnterHiddenDesktop();result=Run();}catch(Exception error){Console.Error.WriteLine(error);}
            finally{if(_desktop!=IntPtr.Zero)CloseDesktop(_desktop);}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();return result;
    }
    private static IntPtr _desktop;
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]
    private static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,int flags,uint access,IntPtr security);
    [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]private static extern bool SetThreadDesktop(IntPtr desktop);
    [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]private static extern bool CloseDesktop(IntPtr desktop);
    private static void EnterHiddenDesktop()
    {
        // Never switch the user's desktop. All owned WPF HWNDs remain on this isolated desktop.
        _desktop=CreateDesktop("SunEyeVision-B08-"+Guid.NewGuid().ToString("N"),IntPtr.Zero,IntPtr.Zero,0,0x000F01FF,IntPtr.Zero);
        if(_desktop==IntPtr.Zero||!SetThreadDesktop(_desktop))throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(),"独立隐藏桌面不可用；不回退到用户桌面。");
        Console.WriteLine("B08隐藏桌面已建立，不切换用户桌面。");
    }
    private static int Run()
    {
        Console.WriteLine("Display schema fields="+BlobResultDisplay.Schema.Inputs.Count);
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
        var scope=Environment.GetEnvironmentVariable("S4_BLOB_DISPLAY_SCOPE")??"All";
        Require(scope is "All" or "Core" or "Window" or "Roi","未知显示专项");
        var resultView=new ImageResultControl{DataContext=vm.ImageResult};resultView.Initialize(vm);
        var shell=new Window{Width=1000,Height=850,Content=resultView,ShowActivated=false,ShowInTaskbar=false};app.MainWindow=shell;shell.Show();Pump();
        var node=vm.WorkflowTabViewModel.SelectedTab!.CreateNode(BlobTool.Id);vm.AddNodeToWorkflow(node);node.DebugWindowStyle=DebugWindowStyle.Custom;
        var source=vm.WorkflowTabViewModel.SelectedTab.CreateNode("ImageLoad");
        var imageFile=Path.Combine(folder,"input.png");var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(64,64,96,96,PixelFormats.Gray8,null,Pixels(),64)));using(var file=File.Create(imageFile))png.Save(file);
        var policy=ImageSourcePolicy.Read(source)!;policy.FilePaths=new(){imageFile};policy.CurrentIndex=0;policy.ForRunPaths=new(){imageFile};policy.SNInitialValue=1;policy.ApplyTo(source);vm.AddNodeToWorkflow(source);
        Require(vm.AddConnectionToWorkflow(new WorkflowConnection{Id=Guid.NewGuid().ToString("N"),SourceNodeId=source.Id,TargetNodeId=node.Id}),"真实图像连线失败");
        var inputReference=((ReferenceSetting)node.RuntimeConfiguration!.Fields.Read(BlobTool.ImageInput)).Reference;var key=BindingReferenceSerializer.Serialize(inputReference);
        var flowId=vm.WorkflowTabViewModel.SelectedTab.RootWorkflowId;var host=solutions.Session!.Editing.Host;var history=solutions.Session.History;
        vm.SelectedNode=node;node.IsSelected=true;Pump();
        var workspace=(ResultWorkspaceShell)resultView.FindName("Workspace");workspace.IsDoubleImage=false;Pump();
        DefaultDebugWindow Open()
        {vm.OpenDebugWindowCommand.Execute(node);Pump();var window=app.Windows.OfType<DefaultDebugWindow>().Single(w=>w.IsVisible);window.UpdateLayout();Pump();return window;}
        RuntimeNodeConfigurationControl Control(DefaultDebugWindow window)=>(RuntimeNodeConfigurationControl)((ContentControl)window.FindName("ContentHost")).Content;
        object Owner(RuntimeNodeConfigurationControl control)=>Get(control.Content,"_roiOwner");
        NativeRoiSession Session(object owner,string group)=>(NativeRoiSession)((System.Collections.IDictionary)Get(owner,"_sessions"))[group]!;
        NativeRoiEditor Editor(object owner,string group)=>(NativeRoiEditor)((System.Collections.IDictionary)Get(owner,"_editors"))[group]!;
        void SelectInput(bool doubleImage=false)
        {
            workspace.IsDoubleImage=doubleImage;Pump();var items=(System.Collections.IEnumerable)(doubleImage?workspace.InputItems:workspace.OutputItems);
            var item=items.Cast<object>().Single(item=>(string)Get(item,"Key")==key);
            if(doubleImage)workspace.SelectedInputItem=item;else workspace.SelectedOutputItem=item;Pump();
        }
        var drawTrace=new List<object>();
        void Draw(object owner,string group,string kind,bool expected=true)
        {
            var editor=Editor(owner,group);var context=editor.NativeView.DataContext;
            var type=kind switch {"rectangle"=>SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box,"annulus"=>SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.SectorRing,"sector"=>SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Sector,_=>SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Polygon};
            ((ICommand)Get(context,"ValueChangedCommand")).Execute(type);Pump();
            var session=Session(owner,group);var request=session.ActiveDraw??throw new Exception("原工具箱未进入实际视口绘制："+Get(owner,"Error"));
            var attachment=(SunEyeVision.Plugin.Wpf.Presenters.Display.INativeRoiEditLease)Get(owner,"_interaction");
            void TraceDraw(string phase)
            {
                drawTrace.Add(new{group,kind,phase,activeDraw=session.ActiveDraw?.Id,attachment.HasPendingGesture,revision=(long)Get(owner,"Revision")});
                File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"roi-draw-trace.json"),System.Text.Json.JsonSerializer.Serialize(drawTrace,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            }
            TraceDraw("before-actual-completion");
            var completionType=typeof(NativeRoiEditor).Assembly.GetType("SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeRoiDrawCompletion")!;
            var completion=Activator.CreateInstance(completionType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{request,NativeShape(kind)},null)!;
            var completed=(bool)attachment.GetType().GetMethod("CompleteDrawing",Flags)!.Invoke(attachment,new[]{completion})!;Pump();TraceDraw("after-actual-completion");
            Require(completed==expected&&session.ActiveDraw is null&&!attachment.HasPendingGesture,"原附件完成链未接纳/退役手势："+group+"/"+kind+" "+session.Error);
        }

        var flow=solutions.CurrentSolution!.Workflows.Single(flow=>flow.Id==flowId);
        string Display(string group,string role)=>BlobResultDisplay.Field(group,role);
        var displaySchema=node.RuntimeConfiguration!.DisplaySettingsSchema;
        string FieldValue(string property)=>BlobParameterDefinition.Instance.SettingsFieldId(property);
        SunEyeVision.Plugin.Wpf.Presenters.Results.IResultPublication Publication()=>(SunEyeVision.Plugin.Wpf.Presenters.Results.IResultPublication)Get(vm,"CurrentResultPublication");
        var presenter=Get(resultView,"_workspacePresenter");var outputPane=Get(presenter,"_output");
        var viewport=(SunEyeVision.Plugin.Wpf.Presenters.Display.NativeResultViewport)Get(Get(presenter,"OutputPane"),"Viewport");
        void SelectBlob(bool binary=false)
        {
            workspace.IsDoubleImage=false;Pump();var id=BlobSchema.Field(binary?6:5);
            var item=workspace.OutputItems.Cast<object>().Single(item=>Get(item,"Reference") is OutputReference reference&&reference.NodeId==node.Id&&reference.PortId==id);
            workspace.SelectedOutputItem=item;Pump();
        }
        object Scene(){try{PumpUntil(()=>Maybe(outputPane,"Display") is not null);}catch{throw new Exception("真实结果常驻场景未安装");}return Get(outputPane,"Display");}
        object[] Items(object scene)=>(object[])Get(Get(Get(scene,"Layer"),"_prepared"),"Items");
        object[] Geometry(object scene)=>Items(scene).Where(item=>Maybe(item,"Texts") is null&&Maybe(item,"StrokeThickness") is not null).ToArray();
        IEnumerable<object> Words(object scene)=>Items(scene).Where(item=>Maybe(item,"Texts") is not null).SelectMany(item=>((System.Collections.IEnumerable)Get(item,"Texts")).Cast<object>());
        void Run(){var run=vm.RunSelectedImageAsync();PumpUntil(()=>run.IsCompleted);run.GetAwaiter().GetResult();Pump();SelectBlob();}
        void SetFormal(string id,object value){var edit=host.BeginEdit(flowId,node.Id);edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{id,value}});host.Commit(edit);Pump();}
        if(scope is "All" or "Core")
        {
            Case("正式显示声明与算法阶段隔离，常量模式及默认只创建一次",()=>{
                var config=node.RuntimeConfiguration!;Require(config.SettingsSchema.Inputs.Count==55&&config.DisplaySettingsSchema.Inputs.Count==28&&config.EditorSettingsSchema.Inputs.Count==83,"算法/显示编辑声明边界错误");
                Require(displaySchema.Inputs.All(port=>port.EditModes==FieldEditModes.Constant),"显示可误订阅");
                var execution=((System.Collections.IEnumerable)config.GetType().GetMethod("CaptureExecutionFields",Flags)!.Invoke(config,null)!).Cast<object>().Select(pair=>(string)Get(pair,"Key")).ToArray();Require(!execution.Intersect(displaySchema.Inputs.Select(port=>port.Id)).Any()&&config.ParameterBindingOrder.All(id=>!config.IsDisplayField(id)),"显示进入执行或参数写入顺序");
                var appearance=config.CaptureResultDisplay()!;Require(appearance.Graphic.Ok.Red==102&&appearance.Graphic.Width==2&&appearance.Text.Content=="Blob Area:{0}"&&appearance.Text.X==0&&appearance.Text.FontSize==10,"原feature默认错误");
                var other=RuntimeToolConfiguration.Create("ImageLoad");Require(other.DisplaySettingsSchema.Inputs.Count==0&&other.EditorSettingsSchema.Fingerprint==other.SettingsSchema.Fingerprint,"无显示provider工具行为改变");
            });
            Case("显示只在完整编辑集合接纳，算法WithSettings保持正式样式",()=>{
                var original=node.RuntimeConfiguration!;var changed=original.WithFieldValues(new Dictionary<string,object?>{{Display(BlobResultDisplay.Graphic,"width"),4d}});
                var algorithm=changed.WithSettings(new BlobSettings{Threshold=121});Require(algorithm.CaptureResultDisplay()!.Graphic.Width==4&&((BlobSettings)algorithm.CaptureSettings()).Threshold==121,"算法更新覆盖显示");
                var editor=new SunEyeVision.Plugin.Runtime.ToolSettingsEditSession(new BlobTool(),algorithm.Fields.Select(algorithm.EditorSettingsSchema.Inputs));Require(editor.Schema.Inputs.Count==83&&editor.Schema.Inputs.Select(port=>port.Id).ToHashSet(StringComparer.Ordinal).SetEquals(algorithm.EditorSettingsSchema.Inputs.Select(port=>port.Id)),"编辑集合丢显示");
                var full=algorithm.WithSettingsFields(editor.CaptureValidatedFields());Require(full.Fields.Read(BlobTool.ImageInput) is ReferenceSetting&&full.CaptureResultDisplay()!.Graphic.Width==4,"编辑替换覆盖图像输入/样式");
                var rejected=false;try{_=algorithm.WithSettingsFields(algorithm.Fields.Select(algorithm.SettingsSchema.Inputs));}catch(ArgumentException error){rejected=error.Message.Contains("display.")||error.Message.Contains("required");}Require(rejected,"算法only快照误当完整编辑集合");
                var bad=false;try{_=changed.WithFieldSettings(new Dictionary<string,ParameterSetting>{{Display(BlobResultDisplay.Text,"x"),new ReferenceSetting(new OutputReference(source.Id,"image.width",DataType.Integer,scopeId:flowId))}});}catch(ArgumentException){bad=true;}Require(bad,"动态位置误入算法引用");
            });
            Case("保存与复制保显示，加载缺字段不补默认，非法参数仍拒绝",()=>{
                var config=node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{Display(BlobResultDisplay.Graphic,"width"),5d},{Display(BlobResultDisplay.Text,"content"),"A={0:F1}"}});
                var json=System.Text.Json.JsonSerializer.Serialize(config);var restored=System.Text.Json.JsonSerializer.Deserialize<RuntimeToolConfiguration>(json)!;Require(Equals(restored.CaptureResultDisplay(),config.CaptureResultDisplay())&&Equals(config.Copy().CaptureResultDisplay(),config.CaptureResultDisplay()),"复制/加载覆盖保存样式");
                var root=System.Text.Json.Nodes.JsonNode.Parse(json)!;root["Fields"]!.AsObject().Remove(Display(BlobResultDisplay.Graphic,"width"));var missing=false;try{_=System.Text.Json.JsonSerializer.Deserialize<RuntimeToolConfiguration>(root.ToJsonString());}catch(Exception error){missing=error.ToString().Contains("display.");}Require(missing,"重开静默补显示默认");
                var invalid=false;try{_=config.WithFieldValues(new Dictionary<string,object?>{{Display(BlobResultDisplay.Graphic,"opacity"),1.1d}});}catch(ArgumentException){invalid=true;}Require(invalid,"非法显示透明度通过");
                invalid=false;try{_=config.WithFieldValues(new Dictionary<string,object?>{{FieldValue(nameof(BlobSettings.FilterAreaMin)),100L},{FieldValue(nameof(BlobSettings.FilterAreaMax)),10L}});}catch(ArgumentException){invalid=true;}Require(invalid,"显示合同绕过算法跨字段校验");
            });
            Case("显示失效签名仅豁免纯样式，混合算法和休眠输入仍失效",()=>{
                var edit=host.BeginEdit(flowId,node.Id);edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{Display(BlobResultDisplay.Graphic,"width"),3d}});var style=host.Commit(edit);Require(style.InvalidatedNodes.Count==0,"纯样式使执行结果失效");history.Undo();Pump();
                edit=host.BeginEdit(flowId,node.Id);edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{Display(BlobResultDisplay.Graphic,"width"),3d},{FieldValue(nameof(BlobSettings.Threshold)),120L}});var mixed=host.Commit(edit);Require(mixed.InvalidatedNodes.Any(id=>id.NodeId==node.Id),"算法+样式被错误豁免");history.Undo();Pump();
            });
            Case("受控面积格式、未知中文token明确拒绝而非静默替换",()=>{
                BlobResultDisplay.ValidateContent(new string('a',40)+"{0:F2}");Require(string.Format(System.Globalization.CultureInfo.InvariantCulture,"A={0:F1}",25d)=="A=25.0","独立格式oracle错误");
                foreach(var raw in new[]{"Blob面积:{面积}","{1}","{0,1000000000}"}){var rejected=false;try{BlobResultDisplay.ValidateContent(raw);}catch(ArgumentException){rejected=true;}Require(rejected,"不支持内容被伪接纳："+raw);}
            });
        }
        Run();var path=Path.Combine(folder,"display.solution");Require(solutions.SaveCurrentDocument(path).FileWritten,"正式夹具保存失败");
        if((scope is "All" or "Window")&&Environment.GetEnvironmentVariable("S4_BLOB_DISPLAY_CASE")!="多节点纯样式")
        {
            var publication=Publication();var originalScene=Scene();
            Case("同次两个真实Blob常驻四角和Area文本进入原图层",()=>{
                using var values=publication.Borrow();var rows=values.Values!.Read(BlobSchema.BlobsPort);Require(rows.Count==2,"敏感图未产生两个真实对象");Require(Enumerable.Range(0,rows.Count).Select(i=>rows.GetElement(i).GetField("area").GetScalar<double>()).Order().SequenceEqual(new[]{25d,64d}),"真实区域面积不符独立像素oracle");
                Require((int)Get(Get(Get(originalScene,"Layer"),"_prepared"),"Count")==4&&Words(originalScene).Select(word=>(string)Get(word,"Content")).Order().SequenceEqual(new[]{"Blob Area:25","Blob Area:64"}),"未实际投影两个边框及两个Area文字");
                Console.WriteLine("Scene styles="+string.Join(";",Items(originalScene).Select(item=>item.GetType().FullName+"/stroke="+Maybe(item,"StrokeThickness"))));
                Require(Items(originalScene).Count(item=>Maybe(item,"StrokeThickness") is not null&&Maybe(item,"Texts") is null)==2,"未产生两条实际几何投影");
                Require(Items(originalScene).Where(item=>Maybe(item,"StrokeThickness") is not null&&Maybe(item,"Texts") is null).All(item=>(double)Get(item,"StrokeThickness")==2),"原边框样式未消费");Capture(viewport.View,"display-default.png");
            });
            var window=Open();var control=Control(window);var editor=(DeclaredSettingsView)Get(control.Content,"Editor");SelectResultPage(editor);window.UpdateLayout();Pump();
            DeclaredFieldsView Fields()=>(DeclaredFieldsView)((DeclaredSettingsView)Get(control.Content,"Editor")).Content;
            DeclaredFieldAccess Field(string group,string role)=>Fields().Fields.Single(field=>field.FieldId==Display(group,role));
            Case("原九行七文本同组装，七项缺生产值禁用且不伪功能",()=>{
                Require(Fields().Fields.Count(field=>field.FieldId.StartsWith("display.text.",StringComparison.Ordinal))==8,"文本眼睛和七角色缺失");
                var tabs=Visual(editor).OfType<TabControl>().Single();Require(tabs.Items.Cast<object>().Select(o=>(string)Get(o,"Header")).SequenceEqual(new[]{"基本参数","运行参数","结果显示"}),"原三页顺序改变");
                var page=((System.Collections.IDictionary)Get(Fields(),"_groups"))["结果显示"]!;
                var rows=((System.Collections.IEnumerable)Get(page,"Rows")).Cast<object>().ToArray();
                var groupOrder=rows.Select(row=>(string)Get(Get(row,"GroupName"),"NameA")).Distinct().ToArray();
                Console.WriteLine("Result groups="+string.Join(";",groupOrder));
                Require(groupOrder.SequenceEqual(new[]{"结果判定","图像显示","文本显示"}),"原结果页三组顺序改变");
                Console.WriteLine("Result row slots="+string.Join(";",rows.Select(row=>Get(Get(row,"GroupName"),"NameA")+":"+Maybe(Get(row,"View"),"DisplayNameA")+":"+Get(row,"View").GetType().FullName)));
                var unavailable=Get(control.Content,"_displayUi");var contents=((System.Collections.IEnumerable)Get(unavailable,"_contents")).Cast<object>().OfType<FrameworkElement>().ToArray();Require(contents.Length==4&&contents.All(view=>!view.IsEnabled&&view.ToolTip is not null),"无生产值显示项可误编辑");
                var actualRows=rows.Where(row=>Equals(Get(Get(row,"GroupName"),"NameA"),"图像显示")).Select(row=>Get(row,"View")).ToArray();
                var expectedRows=new object[]{Field(BlobResultDisplay.Graphic,"visible").View,contents[0],Field(BlobResultDisplay.Inspection,"visible").View,contents[1],contents[2],contents[3],Field(BlobResultDisplay.Text,"visible").View,Field(BlobResultDisplay.Mask,"visible").View,Field(BlobResultDisplay.Annulus,"visible").View};
                Require(actualRows.Length==9&&actualRows.Zip(expectedRows).All(pair=>ReferenceEquals(pair.First,pair.Second)),"原九行槽位顺序改变（含屏蔽→扇环）");
                Require((string)Get(Field(BlobResultDisplay.Graphic,"visible").View,"DisplayNameA")=="Blob"&&(string)Get(Field(BlobResultDisplay.Text,"visible").View,"DisplayNameA")=="文本"&&(bool)Get(Field(BlobResultDisplay.Text,"visible").View,"PanVisibleA")==false,"未复用原图形/文本眼睛模板");
                Console.WriteLine("Page="+editor.SelectedPageIndex+" tabs="+string.Join(";",Visual(editor).OfType<TabControl>().Select(t=>t.SelectedIndex+":"+string.Join(",",t.Items.Cast<object>().Select(o=>Maybe(o,"Header"))))));
                Console.WriteLine("XY visibility="+Field(BlobResultDisplay.Text,"x").View.IsVisible+" loaded="+Field(BlobResultDisplay.Text,"x").View.IsLoaded+" enabled="+Field(BlobResultDisplay.Text,"x").View.IsEnabled+" parent="+LogicalTreeHelper.GetParent(Field(BlobResultDisplay.Text,"x").View)?.GetType().FullName);Capture(window,"display-layout-diagnostic.png");
                Console.WriteLine("XY input="+Field(BlobResultDisplay.Text,"x").InputElement?.GetType().FullName+" view="+Field(BlobResultDisplay.Text,"x").View.GetType().FullName+" tree="+string.Join(";",Visual(Field(BlobResultDisplay.Text,"x").View).Select(e=>e.GetType().FullName)));
                Require(Visual(Field(BlobResultDisplay.Text,"x").View).OfType<SunEyeVision.Plugin.Wpf.Controls.References.Primitives.DeleteTextBox>().Any(),"固定位置未复用原同槽文本");
            });
            Case("有效草稿即时样式不运行，原眼睛双向且不改正式配置",()=>{
                var visible=Field(BlobResultDisplay.Graphic,"visible");var eye=Visual(visible.View).OfType<ToggleButton>().Single(item=>Equals(item.Tag,"NativeGraphicVisibility"));eye.SetCurrentValue(ToggleButton.IsCheckedProperty,false);eye.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();Console.WriteLine("Eye setting="+visible.Setting+" scene="+string.Join(";",Items(Scene()).Select(item=>item.GetType().FullName))+" words="+Words(Scene()).Count());Require(visible.Setting is ConstantSetting{Value:false}&&Words(Scene()).Count()==2&&Items(Scene()).All(item=>Maybe(item,"Texts") is not null),"原眼睛未驱动真实草稿/场景");
                Require(node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Visible&&ReferenceEquals(publication,Publication())&&publication.IsCurrent(),"预览改正式配置或新建运行结果");
                Require(visible.TrySetConstant(true),visible.Error);var width=Field(BlobResultDisplay.Graphic,"width");Require(width.TrySetConstant(4d),width.Error);Pump();Require(Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==4)&&node.RuntimeConfiguration.CaptureResultDisplay()!.Graphic.Width==2,"样式未消费或写正式");
            });
            Case("真实文本七字段完整投影，未知token拒绝且不半发布",()=>{
                var textFields=Fields().Fields.Where(field=>field.FieldId.StartsWith("display.text.",StringComparison.Ordinal)).ToArray();
                var baseline=textFields.ToDictionary(field=>field.FieldId,field=>((ConstantSetting)field.Setting).Value);
                var values=new Dictionary<string,object?>{{"content","A={0:F1}"},{"ok",PresentationDeclarations.Color(1,2,3)},{"ng",PresentationDeclarations.Color(4,5,6)},{"size",14d},{"opacity",.5d},{"x",7d},{"y",9d}};
                foreach(var pair in values)Require(Field(BlobResultDisplay.Text,pair.Key).TrySetConstant(pair.Value!),"文本字段未接纳："+pair.Key);Pump();
                var words=Words(Scene()).ToArray();Require(words.Length==2&&words.Select(word=>(string)Get(word,"Content")).Order().SequenceEqual(new[]{"A=25.0","A=64.0"})
                    &&words.All(word=>(int)Get(word,"FontSize")==14&&(Point)Get(word,"Position")==new Point(7,9)&&(double)Get(word,"Opacity")==.5&&(string)Get(word,"Stroke")=="#FF010203"),"文本七字段未进入真实原投影");
                Require(!Field(BlobResultDisplay.Text,"content").TrySetConstant("Blob面积:{面积}"),"未知中文token被伪接纳");Pump();Require(Words(Scene()).Select(word=>(string)Get(word,"Content")).Order().SequenceEqual(new[]{"A=25.0","A=64.0"}),"非法文本半发布");
                foreach(var field in textFields)Require(field.TrySetConstant(baseline[field.FieldId]!),field.Error);Pump();Require(publication.IsCurrent()&&node.RuntimeConfiguration!.CaptureResultDisplay()!.Text.Content=="Blob Area:{0}","文本草稿改正式或运行");
            });
            Case("原样式Popup非法pending关闭重开保原文，恢复后完整捕获",()=>{
                var graphic=Field(BlobResultDisplay.Graphic,"visible").View;
                var popup=Visual(graphic).OfType<AppsPopup>().Single();popup.IsDropDownOpen=true;window.UpdateLayout();Pump();
                var part=(Popup)popup.Template.FindName("PART_Popup",popup)!;Require(part.IsOpen&&part.Child is FrameworkElement,"原样式弹层未打开");
                TextBox PendingText(){var numbers=(System.Collections.IDictionary)Get(graphic,"_numbers");var text=(TextBox)Get(numbers[Display(BlobResultDisplay.Graphic,"opacity")]!,"TextEditor");Require(Visual(part.Child!).Any(item=>ReferenceEquals(item,text))&&text.Name=="ValueText","待提交文本未绑定原透明度数值角色");return text;}
                var text=PendingText();var old=text.Text;text.SetCurrentValue(TextBox.TextProperty,"bad-opacity");Pump();
                Require(control.HasPendingEdits&&!control.TryCapture(out _),"非法原文未阻断真实宿主捕获");
                Require(node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Opacity==1&&publication.IsCurrent(),"非法原文写正式或执行");
                popup.IsDropDownOpen=false;Pump();popup.IsDropDownOpen=true;window.UpdateLayout();Pump();text=PendingText();Require(text.Text=="bad-opacity"&&!control.TryCapture(out _),"关闭重开清掉非法pending");
                text.SetCurrentValue(TextBox.TextProperty,old);Require(Field(BlobResultDisplay.Graphic,"opacity").TryCommitPending(),"显式原字段提交未恢复");popup.IsDropDownOpen=false;Pump();Require(control.TryCapture(out var error),error);
            });
            Case("真实Host确认后同publication保留，UndoRedo只刷新正式样式",()=>{
                var context=ToolEditorCommit.GetDraftContext(control)!;var args=new object?[]{null};Require((bool)context.GetType().GetMethod("TryCommitEditor",Flags)!.Invoke(context,args)!,"正式确认拒绝："+args[0]);Pump();
                Require(node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width==4&&publication.IsCurrent()&&ReferenceEquals(publication,Publication()),"正式样式确认使结果失效");editor=(DeclaredSettingsView)Get(control.Content,"Editor");SelectResultPage(editor);Require(Field(BlobResultDisplay.Graphic,"width").TrySetConstant(6d),"同窗Apply新草稿不可编辑");Pump();Console.WriteLine("ApplyNext draft="+((ConstantSetting)Field(BlobResultDisplay.Graphic,"width").Setting).Value+" formal="+node.RuntimeConfiguration.CaptureResultDisplay()!.Graphic.Width+" scene="+string.Join(",",Geometry(Scene()).Select(item=>Get(item,"StrokeThickness"))));Require(Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==6),"同窗Apply新代未预览");window.Close();Pump();history.Undo();Pump();Console.WriteLine("StyleUndo formalWidth="+node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width+" publicationCurrent="+publication.IsCurrent()+" publicationSame="+ReferenceEquals(publication,Publication())+" history="+history.Position+" sceneWidths="+string.Join(",",Geometry(Scene()).Select(item=>Get(item,"StrokeThickness"))));Require(publication.IsCurrent()&&Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==2),"样式Undo失效publication或未更新");history.Redo();Pump();Require(publication.IsCurrent()&&Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==4),"样式Redo未刷新");
            });
            window=Open();control=Control(window);editor=(DeclaredSettingsView)Get(control.Content,"Editor");SelectResultPage(editor);window.UpdateLayout();Pump();
            Case("取消读最新正式值，不恢复开窗旧样式",()=>{
                var width=Field(BlobResultDisplay.Graphic,"width");Require(width.TrySetConstant(6d),width.Error);Pump();_=Scene();SetFormal(Display(BlobResultDisplay.Graphic,"width"),3d);window.Close();Pump();Require(publication.IsCurrent()&&node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width==3&&Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==3),"取消覆盖较新的正式样式");
            });
            Case("无修改旧窗口token不覆盖打开期间UndoRedo后的正式样式",()=>{
                var prior=node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width;SetFormal(Display(BlobResultDisplay.Graphic,"width"),5d);
                window=Open();control=Control(window);editor=(DeclaredSettingsView)Get(control.Content,"Editor");SelectResultPage(editor);
                history.Undo();Pump();Console.WriteLine("OpenUndo formal="+node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width+" scene="+string.Join(",",Geometry(Scene()).Select(item=>Get(item,"StrokeThickness")))+" draft="+((ConstantSetting)Field(BlobResultDisplay.Graphic,"width").Setting).Value+" contextValid="+Get(ToolEditorCommit.GetDraftContext(control)!,"IsValid")+" pending="+control.HasPendingEdits);Require(publication.IsCurrent()&&node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width==prior&&Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==prior),"旧窗口token覆盖Undo正式值");
                history.Redo();Pump();Require(publication.IsCurrent()&&node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width==5&&Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==5),"开窗Redo未投影最新正式值");window.Close();Pump();history.Undo();Pump();
            });
            Case("有合法未确认草稿时正式历史变更退役旧token而非写回旧草稿",()=>{
                window=Open();control=Control(window);editor=(DeclaredSettingsView)Get(control.Content,"Editor");SelectResultPage(editor);
                var draftContext=ToolEditorCommit.GetDraftContext(control)!;Require(Field(BlobResultDisplay.Graphic,"width").TrySetConstant(6d),"未接纳未确认样式草稿");Pump();Require(Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==6),"合法草稿未预览");
                history.Redo();Pump();Require(publication.IsCurrent()&&node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width==5&&Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==5),"旧未提交token覆盖正式历史");
                Require(Field(BlobResultDisplay.Graphic,"width").Setting is ConstantSetting{Value:6d},"历史刷新丢未确认草稿");var commitArguments=new object?[]{null};var commitAccepted=(bool)draftContext.GetType().GetMethod("TryCommitEditor")!.Invoke(draftContext,commitArguments)!;Console.WriteLine("Stale display commit="+commitAccepted+" error="+commitArguments[0]);Require(!commitAccepted&&node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width==5,"旧草稿覆盖正式历史");window.Close();Pump();history.Undo();Pump();
            });
            Case("纯文字仍严核实际物理Frame与Domain，错图清整景",()=>{
                SetFormal(Display(BlobResultDisplay.Graphic,"visible"),false);Pump();Require(Words(Scene()).Count()==2,"纯文字未实际显示");SelectInput();Pump();Require(Maybe(outputPane,"Display") is null,"文字在不匹配上游坐标域上误显示");SelectBlob();Pump();Require(Words(Scene()).Count()==2,"选回真实Blob图像未恢复文字");SetFormal(Display(BlobResultDisplay.Graphic,"visible"),true);Pump();_=Scene();
            });
            Case("保存重开及复制读取同一正式显示字段，不重置样式",()=>{
                Require(solutions.SaveCurrentDocument(path).FileWritten,"显示保存失败");var copy=new SolutionRepository().LoadCandidate(path).Candidate!;try{var copied=copy.Workflows.Single(flow=>flow.Id==flowId).Nodes.Single(item=>item.Id==node.Id).RuntimeConfiguration!;Require(Equals(copied.CaptureResultDisplay(),node.RuntimeConfiguration!.CaptureResultDisplay()),"保存重开替换正式显示值");Require(Equals(node.Clone().RuntimeConfiguration!.CaptureResultDisplay(),copied.CaptureResultDisplay()),"节点复制丢显示");}finally{copy.ExecutionOwner.Retire();}
            });
            Case("失败BindDraft不发布新显示token，成功绑定真实新草稿后恢复",()=>{
                window=Open();control=Control(window);editor=(DeclaredSettingsView)Get(control.Content,"Editor");SelectResultPage(editor);
                var context=ToolEditorCommit.GetDraftContext(control)!;var scopeOwner=Get(vm,"_displayPreview");var oldToken=Get(scopeOwner,"_token");var invalid=node.Clone();invalid.RuntimeConfiguration=null;var failed=false;
                try{control.BindDraft(invalid);}catch(ArgumentException){failed=true;}Pump();Require(failed&&ReferenceEquals(Get(scopeOwner,"_token"),oldToken),"失败绑定发布新token");
                control.BindDraft((WorkflowNodeBase)Get(context,"DraftNode"));Pump();Require(!ReferenceEquals(Get(scopeOwner,"_token"),oldToken)&&Geometry(Scene()).Length==2,"成功真实草稿绑定未建立新代");window.Close();Pump();
            });
            Case("同schema同物理输入的另一节点不借旧节点publication样式",()=>{
                var old=Scene();var second=vm.WorkflowTabViewModel.SelectedTab!.CreateNode(BlobTool.Id);vm.AddNodeToWorkflow(second);vm.SelectedNode=second;Pump();var captured=typeof(MainWindowViewModel).GetMethod("CaptureCurrentResultDisplay",Flags)!.Invoke(vm,new object[]{publication});Require(captured is null&&Maybe(outputPane,"Display") is null,"另一节点借用旧publication");vm.SelectedNode=node;Pump();SelectBlob();_=Scene();Require(Maybe(old,"Disposed") is true,"旧节点scene未退役");
            });
            Case("同尺寸新publication换帧退役旧reader及场景",()=>{
                var old=Scene();var oldPublication=Publication();var oldFrame=viewport.CurrentFrame!.Frame;Run();var current=Scene();Require(!ReferenceEquals(oldPublication,Publication())&&!oldPublication.IsCurrent()&&(bool)Get(old,"Disposed")&&viewport.CurrentFrame!.Frame!=oldFrame&&!ReferenceEquals(old,current),"重跑复用旧运行权威/scene");Capture(viewport.View,"display-rerun.png");
            });
        }
        if(scope is "All" or "Window")
        {
            Case("多节点纯样式事务及UndoRedo保结果，算法改变与Undo不复活旧publication",()=>{
                void Phase(string phase)=>File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"display-progress.log"),DateTime.UtcNow.ToString("O")+" MULTI "+phase+Environment.NewLine);Phase("before-window-close");foreach(var existingWindow in app.Windows.OfType<DefaultDebugWindow>().ToArray())existingWindow.Close();Pump();Phase("after-window-close");var current=Publication();var peer=vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes.FirstOrDefault(item=>item.Id!=node.Id&&item.RuntimeConfiguration?.ToolId==BlobTool.Id);if(peer is null){peer=vm.WorkflowTabViewModel.SelectedTab.CreateNode(BlobTool.Id);vm.AddNodeToWorkflow(peer);}
                Phase("peer-ready");var before=node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width;
                var first=host.BeginEdit(flowId,node.Id);var second=host.BeginEdit(flowId,peer.Id);
                first.Node.RuntimeConfiguration=first.Node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{Display(BlobResultDisplay.Graphic,"width"),7d}});
                second.Node.RuntimeConfiguration=second.Node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{Display(BlobResultDisplay.Graphic,"width"),8d}});
                Phase("before-style-commit");var committed=host.CommitBatch(new[]{first,second},null);Phase("after-style-commit");Pump();Require(committed.InvalidatedNodes.Count==0&&current.IsCurrent()&&Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==7),"多节点纯显示事务失效或未刷新");
                history.Undo();Pump();Require(current.IsCurrent()&&node.RuntimeConfiguration!.CaptureResultDisplay()!.Graphic.Width==before&&Geometry(Scene()).Length==2&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==before),"多节点显示Undo错误");history.Redo();Pump();Require(current.IsCurrent()&&Geometry(Scene()).All(item=>(double)Get(item,"StrokeThickness")==7),"多节点显示Redo错误");
                Phase("before-algorithm-window");var algorithmWindow=Open();var algorithmControl=Control(algorithmWindow);var algorithmEditor=(DeclaredSettingsView)Get(algorithmControl.Content,"Editor");
                var algorithmTabs=Visual(algorithmEditor).OfType<TabControl>().Single();algorithmTabs.SelectedItem=algorithmTabs.Items.Cast<TabItem>().Single(item=>Equals(item.Header,"运行参数"));algorithmWindow.UpdateLayout();Pump();
                var threshold=((DeclaredFieldsView)algorithmEditor.Content).Fields.Single(field=>field.FieldId==SettingId(nameof(BlobSettings.Threshold)));Require(threshold.TrySetConstant(201L),threshold.Error);Require(algorithmControl.TryCapture(out var captureError),captureError);
                var algorithmContext=ToolEditorCommit.GetDraftContext(algorithmControl)!;var commitArgs=new object?[]{null};Require((bool)algorithmContext.GetType().GetMethod("TryCommitEditor")!.Invoke(algorithmContext,commitArgs)!,commitArgs[0]?.ToString()??"真实算法确认失败");Pump();
                var actualResult=((WorkflowContext)Get(vm,"CurrentWorkflowContext")).GetNodeResult(node.Id)?.Result;Console.WriteLine("ProductionAlgorithm cachedResult="+(actualResult is null?"null":"present")+" cachedCurrent="+actualResult?.ExecutionIdentity?.IsCurrent+" publicationCurrent="+current.IsCurrent()+" pane="+(Maybe(outputPane,"Display") is null?"null":"present"));
                Require(!current.IsCurrent(),"正式算法确认后旧publication仍current");Require(Maybe(outputPane,"Display") is null,"正式算法确认后旧图层未撤");algorithmWindow.Close();Pump();
                history.Undo();Pump();Require(!current.IsCurrent()&&Maybe(outputPane,"Display") is null,"算法Undo复活旧运行结果");
            });
        }
        if(scope is "All" or "Roi")
        {
            ResultGeometrySnapshot Frozen() => ((IToolResultGeometryOutput)Get(Get(vm,"_resultPublicationSnapshot"),"Output")).ResultGeometry;
            RecordValue Rectangle(double x,double y,double width,double height)=>new(BlobLocalRoi.ShapeType,new Dictionary<string,object?>{{"kind","rectangle"},{"center",BlobLocalRoi.Point(x,y)},{"width",width},{"height",height},{"angle-degrees",0d}});
            RecordValue Ring(double start,double end,double inner=3)=>new(BlobLocalRoi.ShapeType,new Dictionary<string,object?>{{"kind","annulus"},{"center",BlobLocalRoi.Point(20,20)},{"inner-radius",inner},{"outer-radius",10d},{"start-radians",start},{"end-radians",end}});
            RecordValue Row(RecordValue shape,bool enabled=true)=>BlobLocalRoi.Item(Guid.NewGuid(),"同次几何",shape,enabled);
            void Roi(RecordValue inspection,RecordValue? mask=null){SetFormal(BlobLocalRoi.InputId,BlobLocalRoi.Root(inspection,mask??BlobLocalRoi.Group(BlobLocalRoi.None,Array.Empty<RecordValue>())));Run();}
            ResultGeometryGroup Group(ResultGeometryRole role)=>Frozen().Groups.Single(group=>group.Role==role);
            object[] RegionShapes()=>Geometry(Scene()).Where(item=>item.GetType().Name is "RectangleProjection" or "Annular" or "PolygonProjection").ToArray();
            Case("几何快照深冻结容器且拒绝错误声明和非有限参数",()=>{
                var shapes=new List<RecordValue>{BlobRoiDisplayProjection.Shape(Rectangle(9,9,14,14))};var groups=new List<ResultGeometryGroup>{new(ResultGeometryRole.Inspection,ResultGeometryCoverage.Explicit,shapes)};
                var copy=new ResultGeometrySnapshot(Frozen().SourceFrame,RoiDeclarations.ImageFrame,true,groups);shapes.Clear();groups.Clear();Require(copy.Groups.Count==1&&copy.Groups[0].Shapes.Count==1,"快照共享了可写容器");
                var rejected=false;try{_=new ResultGeometryGroup(ResultGeometryRole.Inspection,ResultGeometryCoverage.Explicit,new[]{Rectangle(9,9,14,14)});}catch(ArgumentException){rejected=true;}Require(rejected,"快照接纳私有/错误形状声明");
                rejected=false;try{var fields=copy.Groups[0].Shapes[0].CopyFields().ToDictionary(pair=>pair.Key,pair=>pair.Value);fields["angle-degrees"]=double.NaN;_=new ResultGeometryGroup(ResultGeometryRole.Inspection,ResultGeometryCoverage.Explicit,new[]{new RecordValue(RoiDeclarations.Shape,fields)});}catch(ArgumentException){rejected=true;}Require(rejected,"快照接纳非有限几何");
            });
            Case("同次全域与无区域不伪造框，空结果仍保覆盖身份",()=>{
                Roi(BlobLocalRoi.Group(BlobLocalRoi.FullDomain,Array.Empty<RecordValue>()));
                Require(Group(ResultGeometryRole.Inspection).Coverage==ResultGeometryCoverage.FullDomain&&Frozen().Groups.All(group=>group.Shapes.Count==0),"全域被伪造名义框");
                Roi(BlobLocalRoi.Group(BlobLocalRoi.None,Array.Empty<RecordValue>()));
                Require(Group(ResultGeometryRole.Inspection).Coverage==ResultGeometryCoverage.None&&Geometry(Scene()).Length==0&&!Words(Scene()).Any(),"无检测区域产生虚假结果或几何");
            });
            Case("实际矩形与disabled按本次冻结值展示，后改草稿不替换结果",()=>{
                Roi(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(Rectangle(9,9,14,14)),Row(Rectangle(32,32,8,8),false)}));
                var snapshot=Frozen();Require(Group(ResultGeometryRole.Inspection).Shapes.Count==1,"禁用形状伪装本次采用");
                var rectangles=Geometry(Scene()).Where(item=>item.GetType().Name=="RectangleProjection").ToArray();Require(rectangles.Length==1&&(double)Get(rectangles[0],"Width")==14,"真实检测框未进入原renderer");
                var window=Open();var control=Control(window);var draft=ToolEditorCommit.GetDraftContext(control)!;
                var draftNode=(WorkflowNodeBase)Get(draft,"DraftNode");draftNode.RuntimeConfiguration=draftNode.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{BlobLocalRoi.InputId,BlobLocalRoi.Empty}});Pump();
                Require(ReferenceEquals(snapshot,Frozen())&&Group(ResultGeometryRole.Inspection).Shapes.Count==1,"显示读了后改草稿");window.Close();Pump();
            });
            Case("多形屏蔽实际名义几何及原橙色Fill，零Blob不丢检测层",()=>{
                var polygon=new RecordValue(BlobLocalRoi.ShapeType,new Dictionary<string,object?>{{"kind","polygon"},{"vertices",new SequenceValue(BlobLocalRoi.ShapeType.Fields.Single(field=>field.Id=="vertices").Type,new object?[]{BlobLocalRoi.Point(1,1),BlobLocalRoi.Point(3,1),BlobLocalRoi.Point(2,3)})}});
                var sectorFields=Ring(0,Math.PI/2,0).CopyFields().ToDictionary(pair=>pair.Key,pair=>pair.Value);sectorFields["kind"]="sector";var sector=new RecordValue(BlobLocalRoi.ShapeType,sectorFields);
                Roi(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(Rectangle(50,50,6,6))}),BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(polygon),Row(Rectangle(10,40,4,4)),Row(Ring(0,2*Math.PI)),Row(sector)}));
                Require(Group(ResultGeometryRole.Mask).Shapes.Count==4,"多形屏蔽被收窄");
                var items=Geometry(Scene());Require(items.Length==5&&!Words(Scene()).Any(),"零Blob时实际几何丢失或伪造结果文本");
                Require(items.Count(item=>Equals(Get(item,"Fill"),"#FFFF5400"))==4,"屏蔽填充未走原IShape.Fill");Capture(shell,"roi-multiple-mask-zero-result.png");
            });
            Case("检测多边形与扇形名义投影保实际kind，不伪造矩形",()=>{
                var triangle=new RecordValue(BlobLocalRoi.ShapeType,new Dictionary<string,object?>{{"kind","polygon"},{"vertices",new SequenceValue(BlobLocalRoi.ShapeType.Fields.Single(field=>field.Id=="vertices").Type,new object?[]{BlobLocalRoi.Point(5,5),BlobLocalRoi.Point(12,5),BlobLocalRoi.Point(5,12)})}});
                Roi(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(triangle)}));Require(Group(ResultGeometryRole.Inspection).Shapes.Single().Get<string>("kind")=="polygon"&&!Geometry(Scene()).Any(item=>item.GetType().Name=="RectangleProjection"),"多边形检测伪造矩形");
                var fields=Ring(0,Math.PI/2,0).CopyFields().ToDictionary(pair=>pair.Key,pair=>pair.Value);fields["kind"]="sector";
                Roi(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(new RecordValue(BlobLocalRoi.ShapeType,fields))}));Require(Group(ResultGeometryRole.Annulus).Shapes.Single().Get<string>("kind")=="sector"&&Geometry(Scene()).Any(item=>item.GetType().Name=="Annular"),"扇形kind或原扇环renderer丢失");
            });
            Case("正负扇环及完整环复用原接口，内半径零与孔洞不混",()=>{
                foreach(var values in new[]{(Start:170d,End:150d,Inner:3d),(Start:150d,End:170d,Inner:3d),(Start:0d,End:360d,Inner:0d)})
                {
                    Roi(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(Ring(values.Start*Math.PI/180,values.End*Math.PI/180,values.Inner))}));
                    var ring=Geometry(Scene()).Single(item=>item.GetType().Name=="Annular");
                    Require(Math.Abs((double)Get(ring,"StartAngle")-Math.Min(values.Start,values.End))<1e-9&&Math.Abs((double)Get(ring,"AngleRange")-Math.Abs(values.End-values.Start))<1e-9&&(double)Get(ring,"InnerRadius")==values.Inner,"原扇环参数未保持同弧域");
                    Require(Group(ResultGeometryRole.Annulus).Shapes.Count==1&&Group(ResultGeometryRole.Inspection).Shapes.Count==0,"扇环被误画为矩形");
                    var original=SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape.AnnularSectorShape.GetAnnularGeometry((SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface.IAnnularSector)ring,64,64,1,1);
                    var field=original.GetType().BaseType!.GetField("geometry",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);var drawn=(System.Windows.Media.Geometry)field!.GetValue(original)!;
                    Point At(double radius,double angle)=>new(20+radius*Math.Cos(angle*Math.PI/180),20+radius*Math.Sin(angle*Math.PI/180));
                    if(values.End!=360)Require(drawn.FillContains(At(6,160))&&!drawn.FillContains(At(6,100))&&!drawn.FillContains(At(1,160)),"原renderer正负扇环实际弧域或孔洞不符");
                    else Require(drawn.FillContains(At(6,90))&&drawn.FillContains(At(1,180))&&!drawn.FillContains(At(11,90)),"原renderer整圆/零内半径实际区域不符");Capture(shell,"roi-ring-"+values.Start+"-"+values.End+".png");
                }
            });
            Case("三角色真实WPF眼睛样式确认取消UndoRedo只刷新不重跑",()=>{
                var polygon=new RecordValue(BlobLocalRoi.ShapeType,new Dictionary<string,object?>{{"kind","polygon"},{"vertices",new SequenceValue(BlobLocalRoi.ShapeType.Fields.Single(field=>field.Id=="vertices").Type,new object?[]{BlobLocalRoi.Point(1,40),BlobLocalRoi.Point(5,40),BlobLocalRoi.Point(2,44)})}});
                Roi(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(Rectangle(9,9,14,14)),Row(Ring(0,Math.PI))}),BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(polygon)}));
                var publication=Publication();var snapshot=Frozen();
                object[] Members(ResultGeometryRole role)=>Geometry(Scene()).Where(item=>role switch {ResultGeometryRole.Inspection=>item.GetType().Name=="RectangleProjection",ResultGeometryRole.Mask=>Equals(Maybe(item,"Fill"),"#FFFF5400"),_=>item.GetType().Name=="Annular"}).ToArray();
                var roles=new[]{(Role:ResultGeometryRole.Inspection,Id:BlobResultDisplay.Inspection),(Role:ResultGeometryRole.Mask,Id:BlobResultDisplay.Mask),(Role:ResultGeometryRole.Annulus,Id:BlobResultDisplay.Annulus)};
                for(var i=0;i<roles.Length;i++)
                {
                    var (role,id)=roles[i];var window=Open();var control=Control(window);var editor=(DeclaredSettingsView)Get(control.Content,"Editor");SelectResultPage(editor);window.UpdateLayout();Pump();
                    DeclaredFieldAccess Field(string name)=>((DeclaredFieldsView)editor.Content).Fields.Single(field=>field.FieldId==Display(id,name));
                    var eye=Visual(Field("visible").View).OfType<ToggleButton>().Single(button=>Equals(button.Tag,"NativeGraphicVisibility"));eye.SetCurrentValue(ToggleButton.IsCheckedProperty,false);eye.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
                    Require(Members(role).Length==0&&publication.IsCurrent()&&ReferenceEquals(snapshot,Frozen()),"原眼睛未隐藏对应角色或引发新执行："+role);
                    Require(Field("visible").TrySetConstant(true),Field("visible").Error);
                    var width=3d+i;var opacity=new[]{.4,.5,.6}[i];var ok=PresentationDeclarations.Color((byte)(11+i),(byte)(21+i),(byte)(31+i));var ng=PresentationDeclarations.Color((byte)(151+i),(byte)(161+i),(byte)(171+i));
                    Require(Field("width").TrySetConstant(width)&&Field("opacity").TrySetConstant(opacity)&&Field("ok").TrySetConstant(ok)&&Field("ng").TrySetConstant(ng),"真实样式字段提案被拒绝："+role);Pump();
                    var expected=System.Windows.Media.Color.FromRgb((byte)(11+i),(byte)(21+i),(byte)(31+i)).ToString();Require(Members(role).Length==1&&Members(role).All(item=>(double)Get(item,"StrokeThickness")==width&&Math.Abs((double)Get(item,"Opacity")-opacity)<1e-12&&Equals(Get(item,"Stroke"),expected)),"草稿样式未投影到实际角色："+role);
                    Require(control.TryCapture(out var error),error);var context=ToolEditorCommit.GetDraftContext(control)!;var args=new object?[]{null};Require((bool)context.GetType().GetMethod("TryCommitEditor")!.Invoke(context,args)!,args[0]?.ToString()??"真实确认失败");window.Close();Pump();
                    Require(publication.IsCurrent()&&ReferenceEquals(snapshot,Frozen()),"确认仅样式却替换结果："+role);history.Undo();Pump();Require(Members(role).Length==1&&Members(role).All(item=>(double)Get(item,"StrokeThickness")==2),"样式Undo未恢复对应角色："+role);history.Redo();Pump();Require(Members(role).All(item=>(double)Get(item,"StrokeThickness")==width&&Equals(Get(item,"Stroke"),expected)),"样式Redo未恢复对应角色："+role);
                    var cancel=Open();var cancelControl=Control(cancel);var cancelEditor=(DeclaredSettingsView)Get(cancelControl.Content,"Editor");SelectResultPage(cancelEditor);var cancelField=((DeclaredFieldsView)cancelEditor.Content).Fields.Single(field=>field.FieldId==Display(id,"width"));Require(cancelField.TrySetConstant(9d),cancelField.Error);Pump();Require(Members(role).All(item=>(double)Get(item,"StrokeThickness")==9),"取消前合法草稿未实际预览");cancel.Close();Pump();Require(Members(role).All(item=>(double)Get(item,"StrokeThickness")==width)&&publication.IsCurrent()&&ReferenceEquals(snapshot,Frozen()),"取消未恢复最新正式样式："+role);
                }
            });
            Case("三角色样式和实际区域保存重开执行，复制不丢名义几何定义",()=>{
                Roi(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(Rectangle(9,9,14,14))}),BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(Rectangle(10,10,2,2))}));
                SetFormal(Display(BlobResultDisplay.Mask,"width"),4d);var before=node.RuntimeConfiguration!;var formalSnapshot=Frozen();
                Require(solutions.SaveCurrentDocument(path).FileWritten,"同次几何配置保存失败");var bytes=File.ReadAllBytes(path);
                var loaded=new SolutionRepository().LoadCandidate(path).Candidate??throw new Exception("重开失败");var loadedFlow=loaded.Workflows.Single(f=>f.Id==flowId);var restored=loadedFlow.Nodes.Single(n=>n.Id==node.Id);
                Require(Equals(restored.RuntimeConfiguration!.CaptureResultDisplay(),before.CaptureResultDisplay())&&Equals(before.Copy().CaptureResultDisplay(),before.CaptureResultDisplay()),"三角色样式保存/复制丢失");
                var engine=new WorkflowEngine(SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);engine.RegisterWorkflow(loadedFlow);var ownHost=new NodeConfigurationEditHost(loaded);var published=WorkflowPublishedConfiguration.Capture(ownHost.Capture(),loaded,ownHost);
                using var globals=new WorkflowGlobalRunValues(loaded.Id,published.GlobalDefinitions,published.GlobalDefinitions.Select(v=>new KeyValuePair<string,object?>(v.Id,v.Value)));
                var executor=new WorkflowExecutionEngine(engine,new PluginManager(),SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);
                var task=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(loadedFlow.Id,sourceInputs:new Dictionary<string,ImageSourceRunInput>{{source.Id,new ImageSourceRunInput(imageFile,1){LocalReadAdmission=_=>imageFile}}}){Configuration=published.Configuration,ExecutionOwner=published.ExecutionOwner,ProducedGlobals=globals,ParameterValues=published.ParameterValues,ResourcePaths=published.ResourcePaths});PumpUntil(()=>task.IsCompleted);
                using(var result=task.GetAwaiter().GetResult())
                {
                    Require(result.Success&&result.NodeResults[node.Id].Result is DeclaredOutputSnapshot,"真实重开执行失败");
                    var output=(BlobOutput)((DeclaredOutputSnapshot)result.NodeResults[node.Id].Result!).Output!;
                    Require(output.ResultGeometry.Groups.Single(group=>group.Role==ResultGeometryRole.Inspection).Shapes.Count==1&&output.ResultGeometry.Groups.Single(group=>group.Role==ResultGeometryRole.Mask).Shapes.Count==1,"重开执行未携带本次实际检测/屏蔽");
                    Require(output.ResultGeometry.SourceFrame==output.Region.SourceFrame&&output.ResultGeometry.Coordinates==output.Region.Frame,"重开名义几何与实际结果资源帧不一致");
                    Require(output.TotalArea==55,"实际屏蔽未裁掉9像素或检测串入另一个Blob");
                }
                loaded.ExecutionOwner.Retire();var window=Open();window.Close();Pump();Require(bytes.SequenceEqual(File.ReadAllBytes(path))&&ReferenceEquals(formalSnapshot,Frozen()),"取消改写文件或替换实际几何");
            });
            Case("三角色总业务NG颜色来自同次状态，独立于逐Blob通过色",()=>{
                var polygon=new RecordValue(BlobLocalRoi.ShapeType,new Dictionary<string,object?>{{"kind","polygon"},{"vertices",new SequenceValue(BlobLocalRoi.ShapeType.Fields.Single(field=>field.Id=="vertices").Type,new object?[]{BlobLocalRoi.Point(1,40),BlobLocalRoi.Point(5,40),BlobLocalRoi.Point(2,44)})}});
                Roi(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(Rectangle(9,9,14,14)),Row(Ring(0,Math.PI))}),BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{Row(polygon)}));
                var draft=host.BeginEdit(flowId,node.Id);var settings=(BlobSettings)draft.Node.RuntimeConfiguration!.CaptureSettings();draft.Node.RuntimeConfiguration=draft.Node.RuntimeConfiguration.WithSettings(settings with {Judgments=settings.Judgments.Select(rule=>rule.Key=="blob-count"?rule with {Enabled=true,Minimum=99,Maximum=100}:rule).ToArray()});host.Commit(draft);Run();
                Require(!Frozen().Passed,"同次总判定未实际NG");
                var styles=new[]{(Role:ResultGeometryRole.Inspection,Style:node.RuntimeConfiguration!.CaptureResultDisplay()!.Inspection!),(Role:ResultGeometryRole.Mask,Style:node.RuntimeConfiguration!.CaptureResultDisplay()!.Mask!),(Role:ResultGeometryRole.Annulus,Style:node.RuntimeConfiguration!.CaptureResultDisplay()!.Annulus!)};
                foreach(var (role,style) in styles)
                {
                    var expected=System.Windows.Media.Color.FromArgb(style.Ng.Alpha,style.Ng.Red,style.Ng.Green,style.Ng.Blue).ToString();var members=Geometry(Scene()).Where(item=>role switch{ResultGeometryRole.Inspection=>item.GetType().Name=="RectangleProjection",ResultGeometryRole.Mask=>Equals(Maybe(item,"Fill"),"#FFFF5400"),_=>item.GetType().Name=="Annular"}).ToArray();Require(members.Length==1&&members.All(item=>Equals(Get(item,"Stroke"),expected)),"总NG未投影对应角色色："+role);
                }
                using var values=Publication().Borrow();var rows=values.Values!.Read(BlobSchema.BlobsPort);Require(rows.Count>0&&Enumerable.Range(0,rows.Count).All(index=>rows.GetElement(index).GetField("judgment-passed").GetScalar<bool>()),"未形成总NG但逐Blob通过的敏感样本");history.Undo();Pump();Run();
            });
            Case("新几何同物理帧严格验证，错图与新publication退役旧整景",()=>{
                var old=Scene();var oldSnapshot=Frozen();SelectInput();Pump();Require(Maybe(outputPane,"Display") is null&&(bool)Get(old,"Disposed"),"不同坐标域上游图仍显示Blob结果几何或泄漏旧scene租约");SelectBlob();var restored=Scene();Require(!ReferenceEquals(old,restored)&&ReferenceEquals(oldSnapshot,Frozen()),"选回未恢复当前结果");
                Run();Require(!ReferenceEquals(restored,Scene())&&!ReferenceEquals(oldSnapshot,Frozen()),"新publication未退役旧场景");
            });
        }
        shell.Close();app.Shutdown();Console.WriteLine("Blob正式图文组合："+_passed+"组通过；仅具真实生产值项目开通，非VM像素/语言/物理鼠标等价。");return 0;
    }
    private static void SelectResultPage(DeclaredSettingsView editor)
    {
        editor.UpdateLayout();Pump();var tabs=Visual(editor).OfType<TabControl>().Single();
        var header=tabs.Items.Cast<TabItem>().Single(item=>Equals(item.Header,"结果显示"));
        tabs.SelectedItem=header;editor.UpdateLayout();Pump();
        Require(ReferenceEquals(tabs.SelectedItem,header)&&Equals(header.Header,"结果显示"),"未实际选中结果显示页");
    }
    private static byte[] Pixels(){var pixels=new byte[4096];for(var y=5;y<13;y++)for(var x=5;x<13;x++)pixels[y*64+x]=200;for(var y=30;y<35;y++)for(var x=30;x<35;x++)pixels[y*64+x]=200;return pixels;}
    private sealed class ActionCommand(Action<object?> action):ICommand {public bool CanExecute(object? value)=>true;public void Execute(object? value)=>action(value);public event EventHandler? CanExecuteChanged {add{}remove{}}}
    private sealed class Spy(bool fail):IDisposable {public int Count;public void Dispose(){Count++;if(fail)throw new InvalidOperationException("reader-release-marker");}}
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object? Maybe(object value,string name){for(var type=value.GetType();type is not null;type=type.BaseType){var property=type.GetProperty(name,Flags|BindingFlags.DeclaredOnly);if(property is not null)return property.GetValue(value);var field=type.GetField(name,Flags|BindingFlags.DeclaredOnly);if(field is not null)return field.GetValue(value);}return null;}
    static object Get(object value,string name)=>Maybe(value,name)??throw new Exception("缺真实成员："+name);
    static RecordValue NativeShape(string kind,double angle=0,double start=0,double sweep=Math.PI/2)
    {
        if(kind=="polygon")return RoiGeometryEditing.Create(kind,new(0,0),new(0,0),new[]{new Point2(4,5),new Point2(12,5),new Point2(4,19)});
        var values=RoiGeometryEditing.Values(RoiGeometryEditing.Create(kind=="sector"?"annulus":kind,new(14,18),new(26,22)));
        if(kind=="rectangle"){values["angle-degrees"]=angle;}
        else{values["kind"]=kind;values["center"]=RoiGeometryEditing.Point(new(20,20));values["inner-radius"]=3d;values["outer-radius"]=10d;values["start-radians"]=start;values["end-radians"]=start+sweep;}
        return RoiGeometryEditing.Checked(values);
    }
    static RecordValue Stored(RecordValue shape)=>(RecordValue)typeof(SunEyeVision.Tool.BlobAnalysis.Wpf.Views.BlobParameterEditor).Assembly.GetType("SunEyeVision.Tool.BlobAnalysis.Wpf.Views.BlobLocalRoiProjection")!.GetMethod("FromNative",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object?[]{shape,null})!;
    static RecordValue Root(RecordValue shape)=>BlobLocalRoi.Root(BlobLocalRoi.Group(BlobLocalRoi.Explicit,new[]{BlobLocalRoi.Item(Guid.NewGuid(),"test",Stored(shape))}),BlobLocalRoi.Group(BlobLocalRoi.None,Array.Empty<RecordValue>()));
    static bool Contains(IReadOnlyRegion region,int x,int y)
    {using var read=region.AcquireReader();for(var i=0;i<read.RunCount;){var runs=read.ReadRuns(i,Math.Min(4096,read.RunCount-i));foreach(var run in runs.Span)if(run.Row==y&&x>=run.FirstColumn&&x<=run.LastColumnInclusive)return true;i+=runs.Length;}return false;}
    private static bool SelectRadio(FrameworkElement radio,long value)=>(bool)radio.GetType().GetMethod("TrySetConstant")!.Invoke(radio,new object[]{value})!;
    private static void SelectSource(DeclaredFieldEditor field,OutputReference? expected)
    {
        var popup=Logical(field).Concat(Visual(field)).OfType<AppsPopup>().First(item=>item.IsVisible);popup.IsDropDownOpen=true;popup.UpdateLayout();Pump();
        var session=popup.DataContext;((ICommand)session.GetType().GetProperty("DropDownCommand")!.GetValue(session)!).Execute(null);Pump();
        var roots=(System.Collections.IEnumerable)session.GetType().GetProperty("SourceNodes")!.GetValue(session)!;
        IEnumerable<object> Walk(System.Collections.IEnumerable nodes)
        {foreach(var node in nodes){yield return node;foreach(var child in Walk((System.Collections.IEnumerable)node.GetType().GetProperty("EnumEntrysA")!.GetValue(node)!))yield return child;}}
        var item=Walk(roots).Single(node=>
        {
            if(expected is null)return (bool)node.GetType().GetProperty("Clear")!.GetValue(node)!;
            var candidate=node.GetType().GetProperty("Candidate")!.GetValue(node);if(candidate is null)return false;
            var reference=(OutputReference)candidate.GetType().GetMethod("Reference")!.Invoke(candidate,new object[]{BindingSelection.All})!;
            return reference.NodeId==expected.NodeId&&reference.PortId==expected.PortId&&reference.MemberFieldPath.SequenceEqual(expected.MemberFieldPath);
        });        ((ICommand)session.GetType().GetProperty("ClickCommand")!.GetValue(session)!).Execute(item);popup.IsDropDownOpen=false;Pump();
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
            if(node is SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamListView list)
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
    private static void Case(string name,Action action){var filter=Environment.GetEnvironmentVariable("S4_BLOB_DISPLAY_CASE");if(!string.IsNullOrEmpty(filter)&&!name.Contains(filter,StringComparison.Ordinal))return;var log=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"display-progress.log");File.AppendAllText(log,DateTime.UtcNow.ToString("O")+" BEGIN "+name+Environment.NewLine);try{action();_passed++;Console.WriteLine("通过："+name);File.AppendAllText(log,DateTime.UtcNow.ToString("O")+" PASS "+name+Environment.NewLine);}catch(Exception error){File.AppendAllText(log,DateTime.UtcNow.ToString("O")+" FAIL "+name+Environment.NewLine+error+Environment.NewLine);throw;}}
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
}
