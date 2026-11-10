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

        string error="",restored="";
        var history=solutions.Session!.History;
        var host=solutions.Session.Editing.Host;
        var initialBytes=File.ReadAllBytes(path);
        var first=Open();var control=Control(first);
        var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(control)!;
        var tabs=Logical(control).OfType<TabControl>().Single();
        var inputs=Logical(control).OfType<DeclaredInputFieldsView>().Single();
        var image=inputs.Fields.Single(f=>f.FieldId==BlobTool.ImageInput);
        var combo=(ComboBox)image.InputElement!;
        combo.SelectedItem=combo.Items.OfType<ComboBoxItem>().Single(item=>item.Tag is ReferenceSetting r&&r.Reference.NodeId==source.Id&&r.Reference.PortId=="output.image"&&r.Reference.MemberFieldPath.SequenceEqual(new[]{"data"}));Pump();
        Case("真实受限窗口默认全有效域无屏蔽，图像来源接纳可捕获",()=>
        {
            Require(first.IsVisible&&image.IsLoaded&&combo.IsVisible,"真实图像输入没有加载");
            Require(control.TryCapture(out error),"默认生产设置被阻断："+error);
            foreach(var id in new[]{BlobTool.InspectionInput,BlobTool.MaskInput})
            {var field=inputs.Fields.Single(f=>f.FieldId==id);Require(!field.IsVisible&&field.Setting is UnsetSetting,"默认像素Region不应进入原几何区域布局或被造值："+id);}
        });
        Case("原S1模式与形状交互保留，偏离基线拒绝且恢复可捕获",()=>
        {
            var roi=Logical(control).OfType<SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeRoiUiDraftControls>().Single();
            var radios=roi.Children.OfType<FrameworkElement>().Where(child=>child.GetType().Name=="NativeIntegerRadioPresenter").ToArray();
            foreach(var radio in radios)
            {
                Require(radio.IsEnabled&&SelectRadio(radio,1L),"原模式不可切换");Pump();
                Require(roi.HasUiChanges&&!control.TryCapture(out error)&&error.Contains("界面设置"),"未接入模式被静默接纳");
                Require(SelectRadio(radio,0L)&&control.TryCapture(out error),"恢复模式不能捕获："+error);
            }
            FrameworkElement Part(string name)=>(FrameworkElement)roi.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(roi)!;
            void Visible(string name,bool expected)=>Require(Part(name).Visibility==(expected?Visibility.Visible:Visibility.Collapsed),"原四态显隐不符："+name);
            Require(radios.Length==4,"ROI模式组数不符");
            Visible("_detectionShapes",true);Visible("_detectionSource",false);Visible("_imageInput",false);Visible("_maskShapes",true);Visible("_maskSource",false);
            Require(SelectRadio(radios[1],1L),"检测继承不可切换");Pump();Visible("_detectionShapes",false);Visible("_detectionSource",true);Visible("_inheritRadio",true);
            Require(SelectRadio(radios[2],2L),"圆环继承不可切换");Pump();Visible("_detectionSource",false);Visible("_circleSource",true);
            Require(SelectRadio(radios[2],1L),"矩形参数继承不可切换");Pump();Visible("_rectangleParameters",true);Visible("_circleSource",false);
            Require(SelectRadio(radios[2],3L),"圆环参数继承不可切换");Pump();Visible("_rectangleParameters",false);Visible("_circleParameters",true);
            Require(SelectRadio(radios[3],1L),"屏蔽继承不可切换");Pump();Visible("_maskShapes",false);Visible("_maskSource",true);
            Require(SelectRadio(radios[0],1L),"图像模式不可切换");Pump();Visible("_imageInput",true);Visible("_detectionSource",false);Visible("_circleSource",false);Visible("_rectangleParameters",false);Visible("_circleParameters",false);Visible("_maskSource",false);Visible("_maskRadio",false);
            foreach(var radio in radios)Require(SelectRadio(radio,0L),"四态恢复失败");Pump();Require(!roi.HasUiChanges&&control.TryCapture(out restored),"四态恢复不能捕获："+restored);
            var editors=roi.Children.OfType<SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeRoiEditor>().ToArray();
            Require(editors.Length==2&&editors.All(editor=>editor.NativeView.IsEnabled),"两组真实形状入口未保留");
            var shape=editors[0].NativeView;
            Require(context.DraftNode.RuntimeConfiguration!.Fields.Read(SunEyeVision.Tool.BlobAnalysis.Runtime.BlobLocalRoi.InputId) is UnsetSetting,"无帧夹具不应已有本地几何");
            var command=(ICommand)shape.DataContext.GetType().GetProperty("ValueChangedCommand")!.GetValue(shape.DataContext)!;
            command.Execute(SunEyeVision.Plugin.Wpf.Native.Dependencies.ParameterSchema.ParamTab.RoiType.Box);Pump();
            Require(context.DraftNode.RuntimeConfiguration!.Fields.Read(SunEyeVision.Tool.BlobAnalysis.Runtime.BlobLocalRoi.InputId) is UnsetSetting,"无真实匹配帧仍写入几何");
            Require(!roi.HasUiChanges&&control.TryCapture(out restored),"无帧拒绝创建后破坏原基线："+restored);
            command.Execute(SunEyeVision.Plugin.Wpf.Native.Dependencies.ParameterSchema.ParamTab.RoiType.FullScreen);Pump();
            Require(!roi.HasUiChanges&&control.TryCapture(out restored),"全域基线不能捕获："+restored);        });
        Case("原图文眼睛颜色文本及样式pending均跟踪，不把默认眼睛当已绘制",()=>
        {
            tabs.SelectedIndex=2;first.UpdateLayout();Pump();
            var display=Logical(control).OfType<SunEyeVision.Plugin.Wpf.Presenters.Display.NativeDisplayUiDraftRows>().Single();
            Require(display.IsEnabled&&display.ToolTip is string tip&&tip.Contains("仅预览")&&tip.Contains("未接入"),"默认图文缺少未接入标识");
            var graphic=Logical(display).First(item=>item.GetType().Name=="NativeGraphicStylePresenter");
            var eye=Visual(graphic).OfType<ToggleButton>().First(button=>button.IsVisible);
            var initial=eye.IsChecked;eye.SetCurrentValue(ToggleButton.IsCheckedProperty,initial!=true);eye.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Pump();
            Require(display.HasUiChanges&&!control.TryCapture(out _),"眼睛修改未阻断");
            eye.SetCurrentValue(ToggleButton.IsCheckedProperty,initial);eye.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Pump();
            Require(!display.HasUiChanges,"眼睛恢复仍脏");
            var colors=Logical(display).OfType<SunEyeVision.Plugin.Wpf.Presenters.Display.NativeColorPresenter>().ToArray();
            foreach(var color in colors){var original=color.SelectedColor;color.SelectedColor=Colors.Blue;Pump();Require(display.HasUiChanges&&!control.TryCapture(out _),"文本颜色漏跟踪");color.SelectedColor=original;Pump();Require(!display.HasUiChanges,"颜色同值恢复仍脏");}
            var content=Logical(display).OfType<DeclaredFieldRow>().Single(field=>field.FieldId=="unavailable.text.content");
            Require(content.TrySetConstant("preview"),"原内容不可编辑");Require(display.HasUiChanges&&!control.TryCapture(out _),"文本变更漏跟踪");
            Require(content.TrySetConstant("")&&!display.HasUiChanges,"文本恢复仍脏");
            var popup=Visual(graphic).OfType<AppsPopup>().First();popup.IsDropDownOpen=true;first.UpdateLayout();Pump();
            var part=(Popup)popup.Template.FindName("PART_Popup",popup)!;Require(part.IsOpen&&part.Child is FrameworkElement,"原样式Popup未真实打开");
            var surface=(DependencyObject)part.Child;var opacity=Visual(surface).OfType<FrameworkElement>().Single(item=>Equals(item.Tag,"NativeGraphicOpacity"));
            var text=Visual(opacity).OfType<TextBox>().First(item=>item.Name=="ValueText");var old=text.Text;
            var trace=new List<object>();
            void Trace(string phase)
            {
                object? Read(object target,string name)=>target.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(target);
                var numbers=(System.Collections.IDictionary)graphic.GetType().GetField("_numbers",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(graphic)!;
                var states=(System.Collections.IDictionary)graphic.GetType().GetField("_states",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(graphic)!;
                trace.Add(new{phase,popup.IsDropDownOpen,part.IsOpen,text.Text,text.IsKeyboardFocused,display.HasUiChanges,
                    pending=numbers.Keys.Cast<object>().Select(key=>{var number=numbers[key]!;var commit=number.GetType().GetField("_commit",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(number);return new{id=key,hasPending=Read(number,"HasPendingEdits"),raw=commit is null?null:Read(commit,"Pending")?.ToString()};}).ToArray(),
                    states=states.Keys.Cast<object>().Select(key=>new{id=key,value=Read(states[key]!,"Accepted")?.ToString(),editError=Read(states[key]!,"EditError"),sourceError=Read(states[key]!,"SourceError")}).ToArray()});
                File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"style-pending-trace.json"),System.Text.Json.JsonSerializer.Serialize(trace,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            }
            Trace("open-baseline");var focused=text.Focus();Trace("focus-result-"+focused);
            text.Text="invalid";Pump();Trace("invalid-open");
            Require(display.HasUiChanges&&!control.TryCapture(out _),"样式未提交非法文本漏跟踪");
            popup.IsDropDownOpen=false;Pump();Trace("invalid-closed");Require(display.HasUiChanges,"关闭丢弃非法原文");
            popup.IsDropDownOpen=true;first.UpdateLayout();Pump();Trace("invalid-reopened");
            text.Text=old;Pump();Trace("baseline-raw-restored");
            text.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,Environment.TickCount,text,popup){RoutedEvent=Keyboard.LostKeyboardFocusEvent});Pump();Trace("native-lost-focus-processed");
            popup.IsDropDownOpen=false;Pump();Trace("valid-closed");
            Require(!display.HasUiChanges&&control.TryCapture(out error),"样式恢复仍不能接纳："+error);
            tabs.SelectedIndex=0;Pump();
        });
        var fields=Logical(control).OfType<DeclaredFieldsView>().Single();
        Case("正式位置开关即时显隐与准入一致，原值可恢复",()=>
        {
            var position=fields.Fields.Single(f=>f.FieldId==SettingId("IsPositionCorrectionEnabled"));
            Require(position.TrySetConstant(true),"正式位置提案没有进入私有草稿");Pump();
            Require(!control.TryCapture(out error)&&error.Contains("位置"),"不支持位置被接纳");
            Require(position.TrySetConstant(false)&&control.TryCapture(out error),"恢复默认位置仍不能捕获："+error);Pump();
        });
        tabs.SelectedIndex=1;Pump();
        Case("原输出行布局保留，隐藏输出非默认拒绝且原值不清除",()=>
        {
            var gray=fields.Fields.Single(f=>f.FieldId==SettingId("OutputGrayImage"));var boundary=fields.Fields.Single(f=>f.FieldId==SettingId("OutputBoundary"));
            Require(!gray.View.IsVisible&&!boundary.View.IsVisible,"新增非原厂输出行");
            Require(gray.TrySetConstant(false)&&!control.TryCapture(out error)&&error.Contains("输出"),"灰度非默认未拒绝");Require(gray.Setting is ConstantSetting {Value:false},"灰度被自动清除");
            Require(gray.TrySetConstant(true)&&boundary.TrySetConstant(true)&&!control.TryCapture(out error)&&error.Contains("输出"),"边界非默认未拒绝");Require(boundary.Setting is ConstantSetting {Value:true},"边界被自动清除");
            Require(boundary.TrySetConstant(false)&&control.TryCapture(out error),"恢复默认输出仍不能捕获："+error);
        });        Case("真实窗口编辑调试取消隔离，关闭退役",()=>
        {
            var before=node.RuntimeConfiguration;var revision=history.Position;
            Require(fields.Fields.Single(f=>f.FieldId==threshold).TrySetConstant(111L),"阈值控件没有接纳");
            Require(ReferenceEquals(before,node.RuntimeConfiguration)&&history.Position==revision,"私有编辑污染正式历史");
            Capture(first,"supported-runtime.png");first.Close();Pump();
            Require(!context.IsValid&&ReferenceEquals(before,node.RuntimeConfiguration)&&File.ReadAllBytes(path).SequenceEqual(initialBytes),"取消/关闭写入正式状态或文件");
            var rejected=false;try{rejected=!control.TryCommitEditor(out _);}catch(ObjectDisposedException){rejected=true;}Require(rejected,"旧控件关闭后仍接纳");
        });
        var saved=Open();control=Control(saved);inputs=Logical(control).OfType<DeclaredInputFieldsView>().Single();
        combo=(ComboBox)inputs.Fields.Single(f=>f.FieldId==BlobTool.ImageInput).InputElement!;
        combo.SelectedItem=combo.Items.OfType<ComboBoxItem>().Single(item=>item.Tag is ReferenceSetting r&&r.Reference.NodeId==source.Id&&r.Reference.PortId=="output.image"&&r.Reference.MemberFieldPath.SequenceEqual(new[]{"data"}));Pump();
        fields=Logical(control).OfType<DeclaredFieldsView>().Single();
        Require(fields.Fields.Single(f=>f.FieldId==threshold).TrySetConstant(111L),"重新打开阈值不可编辑");
        var historyBefore=history.Position;
        Case("真实窗口确认历史复制保存重开",()=>
        {
            Require(saved.TryConfirm(out error),"真实确认仍被阻断："+error);Pump();if(saved.IsVisible)saved.Close();Pump();
            Require(Formal()==111&&history.Position==historyBefore+1,"正式确认/历史未接纳");
            history.Undo();Require(Formal()==100,"撤销未恢复默认");history.Redo();Require(Formal()==111,"重做未恢复确认");
            var copied=node.RuntimeConfiguration!.Copy();Require(((BlobSettings)copied.CaptureSettings()).Threshold==111&&copied.Fields.Read(BlobTool.ImageInput) is ReferenceSetting,"配置复制丢字段/引用");
            Require(solutions.SaveCurrentDocument(path).FileWritten,"真实保存失败");var reopened=new SolutionRepository().LoadCandidate(path).Candidate??throw new Exception("保存重开失败");
            Require(((BlobSettings)reopened.Workflows.Single(w=>w.Id==vm.WorkflowTabViewModel.SelectedTab!.RootWorkflowId).Nodes.Single(n=>n.Id==node.Id).RuntimeConfiguration!.CaptureSettings()).Threshold==111,"重开字段不符");
        });
        Case("真实工作流执行及本窗口私有调试",()=>
        {
            var task=vm.RunSelectedImageAsync();PumpUntil(()=>task.IsCompleted);task.GetAwaiter().GetResult();
            Require(Context().GetNodeResult(node.Id)?.Result is SunEyeVision.Plugin.Runtime.DeclaredOutputSnapshot {IsSuccess:true},"真实工作流没有发布Blob成功结果");
            var debug=Open();var current=Control(debug);var formal=node.RuntimeConfiguration;
            var run=current.RunDebugAsync();PumpUntil(()=>run.IsCompleted);run.GetAwaiter().GetResult();
            Require(ReferenceEquals(formal,node.RuntimeConfiguration),"私有调试污染正式设置");debug.Close();Pump();
        });
        void RejectConfiguration(string name,RuntimeToolConfiguration configuration,string expected)
        {
            var draft=host.BeginEdit(vm.WorkflowTabViewModel.SelectedTab!.RootWorkflowId,node.Id);draft.Node.RuntimeConfiguration=configuration;host.Commit(draft);
            var before=node.RuntimeConfiguration;var position=history.Position;var bytes=File.ReadAllBytes(path);
            var window=Open();var current=Control(window);
            Require(!current.TryCapture(out error)&&error.Contains(expected),name+"没有明确拒绝："+error);
            Require(!window.TryConfirm(out error)&&window.IsVisible,name+"错误确认/关闭");window.Close();Pump();
            Require(ReferenceEquals(before,node.RuntimeConfiguration)&&history.Position==position&&bytes.SequenceEqual(File.ReadAllBytes(path)),name+"拒绝后改值或写盘");
        }
        var flowId=vm.WorkflowTabViewModel.SelectedTab!.RootWorkflowId;
        var supported=node.RuntimeConfiguration!;
        var producer=vm.WorkflowTabViewModel.SelectedTab.CreateNode(BlobTool.Id);vm.AddNodeToWorkflow(producer);
        Require(vm.AddConnectionToWorkflow(new WorkflowConnection{Id=Guid.NewGuid().ToString("N"),SourceNodeId=source.Id,TargetNodeId=producer.Id}),"源到ROI生产者连线失败");
        Require(vm.AddConnectionToWorkflow(new WorkflowConnection{Id=Guid.NewGuid().ToString("N"),SourceNodeId=producer.Id,TargetNodeId=node.Id}),"ROI生产者到消费者连线失败");
        foreach(var (detection,mask) in new[]{(true,false),(false,true),(true,true)})
            Case("已有正式像素Region保值拒绝而不冒充几何继承 "+detection+"/"+mask,()=>
            {
                var region=new ReferenceSetting(new OutputReference(producer.Id,BlobSchema.RegionPort,TypeRef.ResourceOf(ResourceKind.Region),scopeId:flowId));
                var changes=new Dictionary<string,ParameterSetting>();if(detection)changes[BlobTool.InspectionInput]=region;if(mask)changes[BlobTool.MaskInput]=region;
                RejectConfiguration("像素Region",supported.WithFieldSettings(changes),"像素区域引用");
            });
        Case("已有隐藏输出非默认配置重开保值拒绝",()=>
        {
            RejectConfiguration("灰度关闭",supported.WithFieldValues(new Dictionary<string,object?>{{SettingId("OutputGrayImage"),false}}),"输出设置");
            RejectConfiguration("边界开启",supported.WithFieldValues(new Dictionary<string,object?>{{SettingId("OutputBoundary"),true}}),"输出设置");
        });
        Case("已有启用正式判定保值并明确拒绝",()=>
        {
            var enabled=new BlobTool().SettingsSchema.Presentation!.Fields.First(f=>f.Group=="结果判定"&&new BlobTool().SettingsSchema.Inputs.Single(p=>p.Id==f.FieldId).Type==TypeRef.ScalarOf(ScalarKind.Boolean));
            RejectConfiguration("判定",supported.WithFieldValues(new Dictionary<string,object?>{{enabled.FieldId,true}}),"结果判定");
        });
        Case("已有合法Affine值保持且可用原可选引用清除后确认",()=>
        {
            var affine=new RecordValue(SpatialDeclarations.Affine2D,new Dictionary<string,object?>{{"m11",1d},{"m12",0d},{"m21",0d},{"m22",1d},{"offset-x",0d},{"offset-y",0d},{"from","image:input"},{"to","image:input"}});
            var edit=host.BeginEdit(vm.WorkflowTabViewModel.SelectedTab!.RootWorkflowId,node.Id);edit.Node.RuntimeConfiguration=supported.WithFieldSettings(new Dictionary<string,ParameterSetting>{{BlobTool.PoseInput,new ConstantSetting(affine)}});host.Commit(edit);
            var formal=node.RuntimeConfiguration;var window=Open();var current=Control(window);var pose=Logical(current).OfType<DeclaredInputFieldsView>().Single().Fields.Single(field=>field.FieldId==BlobTool.PoseInput);
            Require(pose.IsVisible&&pose.IsEnabled&&!current.TryCapture(out error)&&!string.IsNullOrWhiteSpace(error),"已有合法pose未显示或未拒绝");
            SelectSource(pose,null);Pump();Require(pose.Setting is UnsetSetting&&current.TryCapture(out error),"原清除来源不能修复："+error);
            Require(ReferenceEquals(formal,node.RuntimeConfiguration),"清除私有pose提前写正式状态");window.Close();Pump();Require(node.RuntimeConfiguration!.Fields.Read(BlobTool.PoseInput) is ConstantSetting,"取消清除了用户pose");
            window=Open();current=Control(window);pose=Logical(current).OfType<DeclaredInputFieldsView>().Single().Fields.Single(field=>field.FieldId==BlobTool.PoseInput);SelectSource(pose,null);Pump();Require(window.TryConfirm(out error),"用户主动清除不能确认："+error);if(window.IsVisible)window.Close();Pump();Require(node.RuntimeConfiguration!.Fields.Read(BlobTool.PoseInput) is UnsetSetting,"确认清除未进入正式字段");
        });
        var restore=host.BeginEdit(vm.WorkflowTabViewModel.SelectedTab!.RootWorkflowId,node.Id);restore.Node.RuntimeConfiguration=supported;host.Commit(restore);
        shell.Close();dialogs.Stop();Pump();
        Console.WriteLine("通过：Blob受限正式窗口 "+_passed+"组。Region正向保存执行未开放；同帧/错帧后台能力另轨，不计窗口闭环。");
        return 0;
    }
    private static bool SelectRadio(FrameworkElement radio,long value)=>(bool)radio.GetType().GetMethod("TrySetConstant")!.Invoke(radio,new object[]{value})!;
    private static void SelectSource(DeclaredFieldRow field,OutputReference? expected)
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
