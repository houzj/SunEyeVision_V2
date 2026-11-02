using System.Collections;
using HalconDotNet;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Tool.ImageSource.Runtime;
using System.Windows.Threading;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Shell.Controls.Shell;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Workflow;

internal static class S2DocumentEditingCases
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Get(object value,string name)=>value.GetType().GetProperty(name,Flags)?.GetValue(value)??value.GetType().GetField(name,Flags)?.GetValue(value)??throw new Exception("缺少真实宿主成员："+name);
 static object? Field(object value,string name)
 {for(var type=value.GetType();type is not null;type=type.BaseType)if(type.GetField(name,Flags|BindingFlags.DeclaredOnly) is {} field)return field.GetValue(value);throw new Exception("缺少真实图层字段："+name);}
 static object[] LayerItems(object layer)=>Field(layer,"_prepared") is {} prepared?(object[])Get(prepared,"Items"):Array.Empty<object>();
 static void Call(object value,string name,params object?[] args)=>value.GetType().GetMethod(name,Flags)!.Invoke(value,args);
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
 static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
 static void Until(Func<bool> ready){var timer=Stopwatch.StartNew();while(!ready()){if(timer.Elapsed>TimeSpan.FromSeconds(20))throw new TimeoutException("真实结果宿主未就绪");Pump();Thread.Yield();}}
 static IEnumerable<T> Visuals<T>(DependencyObject root)where T:DependencyObject{for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T item)yield return item;foreach(var nested in Visuals<T>(child))yield return nested;}}
 [STAThread] static int Main()
 {
  try{VerifyColumns();VerifyCapture();Run();Console.WriteLine("通过：真实Blob表→正式publication→ResultWorkspacePresenter同帧选择、重跑及旧事件拒绝及快照/包装清理及声明占位，4组。");return 0;}
  catch(Exception error){Console.Error.WriteLine(error);return 1;}
 }
 static void VerifyColumns()
 {
  var tool=new BlobTool();var projection=tool.ResultProjection;var keys=new[]{"label","area","perimeter","centroid-x","centroid-y","angle","long-axis","short-axis","circularity","rectangularity"};
  Require(projection.Columns.Select(column=>column.Key).SequenceEqual(keys),"Blob表未按原序号加九项特征排列");
  var placeholders=projection.Columns.Where(column=>column.Value is null).ToArray();Require(placeholders.Select(column=>column.Key).SequenceEqual(new[]{"angle","long-axis","short-axis"})&&placeholders.All(column=>!string.IsNullOrWhiteSpace(column.UnavailableReason)),"未证三项未明确声明展示占位");
  var imagePorts=tool.Describe().Outputs.Where(port=>port.Type==TypeRef.ResourceOf(ResourceKind.Image)).Select(port=>port.Id).ToArray();Require(imagePorts.SequenceEqual(new[]{BlobSchema.Field(5),BlobSchema.Field(6)}),"存在未实现的Blob绘制图像输出");
  var display=tool.Describe().OutputDisplay;Require(display.Single(node=>node.FieldId==BlobSchema.Field(5)).DisplayName=="灰度图像"&&!display.Any(node=>node.DisplayName=="Blob图像"),"灰度输出仍冒称Blob绘制图像");
  var fields=tool.ResultSchema.Outputs.Single(port=>port.Id==BlobSchema.BlobsPort).Type.Element!.Fields;Require(!fields.Any(field=>placeholders.Any(column=>column.Key==field.Id)),"占位泄漏为真实输出字段");
  Require(!tool.ManualReferenceSources.Any(source=>source.MemberPath?.Any(member=>placeholders.Any(column=>column.Key==member))==true),"占位泄漏为可订阅来源");
  ResultProjectionDescriptor Make(ResultColumnDescriptor column,ResultProjectionKind kind=ResultProjectionKind.StandardTable)=>new(kind,ResultCardinality.Many,new[]{column},projection.Rows,projection.ElementIdentityFieldId);
  void Reject(Func<object> create){var rejected=false;try{create();}catch(ArgumentException){rejected=true;}Require(rejected,"非法展示占位合同被接纳");}
  var path=new ResultFieldPath(BlobSchema.BlobsPort,new[]{"area"});
  foreach(var column in new[]{new ResultColumnDescriptor("bad","未实现",null),new ResultColumnDescriptor("bad","未实现",null,UnavailableReason:" "),new ResultColumnDescriptor("bad","未实现",path,UnavailableReason:"尚未实现"),new ResultColumnDescriptor("bad","未实现",null,Geometry:path,UnavailableReason:"尚未实现"),new ResultColumnDescriptor("bad","未实现",null,ValidWhen:path,UnavailableReason:"尚未实现"),new ResultColumnDescriptor("bad","未实现",null,DecimalPlaces:2,UnavailableReason:"尚未实现"),new ResultColumnDescriptor("bad","未实现",null,Unit:null!,UnavailableReason:"尚未实现"),new ResultColumnDescriptor("bad","面积",path,Unit:null!)})Reject(()=>Make(column));
  Reject(()=>Make(placeholders[0],ResultProjectionKind.PropertyList));Require(Make(new("bad","未实现",new(BlobSchema.BlobsPort,new[]{"angle"}))).Validate(tool.ResultSchema).Count>0,"假占位引用未知字段绕过声明校验");
  Console.WriteLine("通过：原十列顺序、六真实特征、三展示占位与非法声明及来源隔离。");
 }
 static void VerifyCapture()
 {
  using var native=new HImage();native.GenImageConst("byte",8,8);using var region=new HRegion();region.GenRegionRuns(new HTuple(new[]{1,2,3}),new HTuple(new[]{1,1,1}),new HTuple(new[]{1,2,3}));using var reduced=native.ReduceDomain(region);
  using var resources=new FrozenOutputResources();var frame=new ImageFrameAddress(Guid.NewGuid(),"capture-domain");var coordinates=BlobSchema.ImageFrame;
  using var original=HalImageSnapshotSource.Capture(reduced,frame);var image=HalImageSnapshotSource.Capture(reduced,frame,coordinates,resources);
  Require(image.Frame==frame&&image.EffectiveDomain.Frame==coordinates&&image.Layout==original.Layout&&image.CopyPixels().SequenceEqual(original.CopyPixels()),"显式域捕获改变物理帧、尺寸或像素");
  using(var reader=image.EffectiveDomain.AcquireReader())Require(reader.RunCount==3&&reader.ReadRuns(0,4096).Span.SequenceEqual(new[]{new RegionRun(1,1,1),new RegionRun(2,1,2),new RegionRun(3,1,3)}),"非矩形Domain runs未原样保留");
  foreach(var invalid in new[]{new CoordinateFrame("model",CoordinateDomain.Model,CoordinateUnit.Pixel),new CoordinateFrame("millimeter",CoordinateDomain.Image,CoordinateUnit.Millimeter)})
  {var rejected=false;try{HalImageSnapshotSource.Capture(reduced,frame,invalid,resources);}catch(ArgumentException){rejected=true;}Require(rejected,"显式图像捕获接纳非法坐标域");}
  Console.WriteLine("通过：显式坐标捕获保真实像素、非矩形Domain并拒绝非法域，默认来源域保留。");
 }
 static void VerifyWrapper(IResultPublication publication)
 {
  var wrapper=ToolRegistry.CreateCurrentResultView(BlobTool.Id)!;var events=0;ResultSelection? last=null;object? sender=null;
  wrapper.SelectionHighlightChanged+=(source,selection)=>{sender=source;last=selection;events++;};wrapper.Present(publication);var host=new Window{Content=wrapper.View,Width=700,Height=350,ShowActivated=false,ShowInTaskbar=false};host.Show();Pump();var grid=Visuals<DataGrid>(wrapper.View).Single();grid.SelectedItem=grid.Items[0];
  Require(ReferenceEquals(sender,wrapper)&&last is {Targets.Count:1}&&last.Publication==publication.Stamp,"包装选中未保身份或Stamp");wrapper.Clear();Require(ReferenceEquals(sender,wrapper)&&last is {ElementIdentity:null,Targets.Count:0}&&last.Publication==publication.Stamp,"Clear未转发原Stamp空选择");
  wrapper.Present(publication);grid.SelectedItem=grid.Items[0];var count=events;wrapper.Dispose();Require(events==count+1&&last is {ElementIdentity:null,Targets.Count:0}&&ReferenceEquals(sender,wrapper),"Dispose遗漏最终空选择");wrapper.Dispose();Require(events==count+1,"重复释放重复通知");host.Content=null;host.Close();
  var rejected=false;try{wrapper.Present(publication);}catch(ObjectDisposedException){rejected=true;}Require(rejected,"释放后包装仍可呈现");
  var faulty=ToolRegistry.CreateCurrentResultView(BlobTool.Id)!;faulty.Present(publication);faulty.SelectionHighlightChanged+=(_,_)=>throw new InvalidOperationException("预期观察者异常");var failed=false;try{faulty.Dispose();}catch(AggregateException){failed=true;}Require(failed,"释放观察者异常被吞");faulty.Dispose();
  Require(faulty.GetType().GetField("SelectionHighlightChanged",Flags)!.GetValue(faulty) is null,"异常释放保留包装观察者");Console.WriteLine("通过：Blob包装sender、Clear/Dispose空选择及异常退订。");
 }
 static void Run()
 {
  var app=Application.Current??new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  foreach(var resourcePath in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/CommonStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.UI;component/"+resourcePath,UriKind.Relative)});
  Require(PluginLoader.LoadPlugins(),"正式插件未接纳");ServiceInitializer.InitializeServices();
  var managerField=typeof(ServiceInitializer).GetField("_solutionManager",BindingFlags.Static|BindingFlags.NonPublic)!;var previous=managerField.GetValue(null);
  var document=new Solution{Name="Blob真实结果宿主联动"};var engine=new WorkflowEngine(new CheckLogger());var flow=engine.CreateWorkflow(Guid.NewGuid().ToString("N"),"Blob结果联动");document.Workflows.Add(flow);
  var node=new SunEyeVision.UI.Shared.WorkflowNode("blob","Blob","Blob",BlobTool.Id){RuntimeConfiguration=RuntimeToolConfiguration.Create(BlobTool.Id).WithSettings(new BlobSettings{Threshold=100,IsAreaFilterEnabled=false,OutputGrayImage=true,OutputBinaryImage=true})};flow.Nodes.Add(node);
  var path=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"source.png");
  var source=new SunEyeVision.UI.Shared.WorkflowNode("source","图像源","图像源",ImageSourceTool.Id){RuntimeConfiguration=RuntimeToolConfiguration.Create(ImageSourceTool.Id).WithSettings(new ImageSourceSettings{FileReferences=new[]{path},StartIndex=0})};
  flow.Nodes.Insert(0,source);flow.Connections.Add(new WorkflowConnection("source-to-blob",source.Id,node.Id));
  node.RuntimeConfiguration=node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{BlobTool.ImageInput,new ReferenceSetting(new OutputReference(source.Id,ImageSourceOutput.Image,ImageSourceOutput.ImageType,new[]{ImageSourceOutput.ImageData},scopeId:flow.Id))}});
  MainWindowViewModel? main=null;ImageResultControl? view=null;Window? window=null;WorkflowContext? context=null;var runs=new List<ExecutionResult>();var borrowed=new List<IDisposable>();IReadOnlyImage? retainedImage=null;
  try
  {
   var manager=new SolutionManager(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"solution-manager"));managerField.SetValue(null,manager);manager.SetCurrentSolution(document,false);
   main=new MainWindowViewModel();main.SelectedNode=node;node.IsSelected=true;
   var formal=main.CreateDataSourceQueryService(flow.Id).FormalOutputContractReader!.Invoke(node.Id);Require(formal.Single(field=>field.PortId==BlobSchema.Field(5)).DisplayName=="灰度图像"&&!formal.Any(field=>field.DisplayName=="Blob图像"),"自动来源/引用树的真实正式目录仍冒称Blob图像");context=(WorkflowContext)Get(main,"CurrentWorkflowContext");var results=(NodeResultManager)Get(main,"_nodeResultManager");
   view=new ImageResultControl{DataContext=main.ImageResult};view.Initialize(main);window=new Window{Content=view,Width=1000,Height=850,ShowActivated=false,ShowInTaskbar=false};window.Show();Pump();
   var shell=(ResultWorkspaceShell)view.FindName("Workspace");shell.IsDoubleImage=true;var presenter=Get(view,"_workspacePresenter");var output=Get(presenter,"OutputPane");var viewport=(ImageViewBox)shell.OutputViewport;
   var executor=new WorkflowExecutionEngine(engine,new PluginManager(),new CheckLogger());
   DeclaredOutputSnapshot Publish(int offset,bool empty=false,bool multiple=false)
   {
    var pixels=new byte[256];if(!empty)for(var y=2;y<10;y++)for(var x=2+offset;x<10+offset;x++)pixels[y*16+x]=200;if(multiple)for(var y=12;y<14;y++)for(var x=1;x<3;x++)pixels[y*16+x]=200;
    var bitmap=BitmapSource.Create(16,16,96,96,PixelFormats.Gray8,null,pixels,16);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);
    using var image=HalImage.CreateFromBytes(pixels,16,16,1);
    var task=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,image,sourceInputs:new Dictionary<string,ImageSourceRunInput>{{source.Id,new ImageSourceRunInput(path,offset+1){LocalReadAdmission=_=>path}}}){Configuration=WorkflowRunConfiguration.Capture(new[]{flow}),ExecutionOwner=document.ExecutionOwner,ResourcePaths=SunEyeVision.Workflow.Persistence.SolutionResourceResolver.For(document)});Until(()=>task.IsCompleted);var run=task.GetAwaiter().GetResult();runs.Add(run);
    Require(run.Success,"真实Blob执行失败："+string.Join(";",run.Errors??new()));var snapshot=(DeclaredOutputSnapshot)run.NodeResults[node.Id].Result!;
    Require(snapshot.IsSuccess&&snapshot.SourceWorkflowId==flow.Id,"Blob没有本流程正式快照");Require(snapshot.Fields[BlobSchema.Field(5)].DisplayName=="灰度图像","真实快照/引用树输出元数据仍冒称Blob图像");
    using(var reader=snapshot.AcquireReader())
    {
     var gray=reader.Values!.Get<IReadOnlyImage>(BlobSchema.Field(5));var binary=reader.Values.Get<IReadOnlyImage>(BlobSchema.Field(6));
     Require(gray.Frame==binary.Frame&&gray.EffectiveDomain.Frame==BlobSchema.ImageFrame&&binary.EffectiveDomain.Frame==BlobSchema.ImageFrame,"灰/二值输出物理帧或声明坐标域不一致");
     Require(gray.Width==16&&gray.Height==16&&gray.CopyPixels().SequenceEqual(pixels)&&binary.CopyPixels().SequenceEqual(pixels.Select(value=>(byte)(value>=100?255:0))),"灰/二值输出改变实际像素或尺寸");
     using var grayDomain=gray.EffectiveDomain.AcquireReader();using var binaryDomain=binary.EffectiveDomain.AcquireReader();var expected=Enumerable.Range(0,16).Select(row=>new RegionRun(row,0,15)).ToArray();
     Require(grayDomain.ReadRuns(0,4096).Span.SequenceEqual(expected)&&binaryDomain.ReadRuns(0,4096).Span.SequenceEqual(expected),"灰/二值输出没有保留真实Domain runs");
    }results.UpdateNodeResult(source,(DeclaredOutputSnapshot)run.NodeResults[source.Id].Result!,false,context);results.UpdateNodeResult(node,snapshot,true,context);Pump();return snapshot;
   }
   var pane=Get(presenter,"_output");var selectionLayer=Get(pane,"Selection");
   object[] Shapes()=>((IEnumerable?)viewport.ShapeList)?.Cast<object>().ToArray()??Array.Empty<object>();
   bool Contains(object[] values,object item)=>values.Any(value=>ReferenceEquals(value,item));
   object? DisplayScene()=>Field(pane,"Display");
   object[] DisplayItems()=>DisplayScene() is {} scene&&(Field(Get(scene,"Layer"),"_prepared") is {} prepared)?(object[])Get(prepared,"PresentationItems"):Array.Empty<object>();
   object[] SelectionItems()=>LayerItems(selectionLayer);
   IResultPublication Publication()=>(IResultPublication)Get(main,"CurrentResultPublication");
   IToolResultView Table()=>(IToolResultView)Get(main,"_activeResultView");
   void SelectImage(string? port=null,string? selectedNode=null)
   {
    Until(()=>shell.OutputItems.Cast<object>().Any());
    var choice=shell.OutputItems.Cast<object>().Single(item=>Get(item,"Reference") is OutputReference reference&&reference.NodeId==(selectedNode??node.Id)&&reference.PortId==(port??BlobSchema.Field(5)));if(port is null||port==BlobSchema.Field(5))Require(Get(choice,"DisplayName").ToString()!.Contains("灰度图像")&&!Get(choice,"DisplayName").ToString()!.Contains("Blob图像"),"结果图像选择仍冒称Blob绘制图像");shell.SelectedOutputItem=choice;Pump();
   }
   ResultSelection SelectRow(DeclaredOutputSnapshot snapshot)
   {
    var table=Table();Require(table.GetType().Name=="BlobResultView","没有使用真实Blob结果视图");var publication=Publication();Require(publication.Stamp.Invocation==snapshot.Identity,"publication不是同次Blob执行");
    var grid=Visuals<DataGrid>(table.View).Single();Require(grid.Items.Count==1&&grid.Columns.Count==10,"真实Blob表行列不符");
    Require(!grid.CanUserSortColumns&&!grid.CanUserReorderColumns,"占位改变原表排序/重排门禁");
    var cell=grid.Items[0].GetType().GetProperty("Item")!;foreach(var key in new[]{"angle","long-axis","short-axis"})Require(Equals(cell.GetValue(grid.Items[0],new object[]{key}),"未实现"),"未证特征显示伪数值");
    using(var reader=snapshot.AcquireReader()){var record=(RecordValue)reader.Values!.Get<SequenceValue>(BlobSchema.BlobsPort)[0]!;foreach(var key in new[]{"area","perimeter","centroid-x","centroid-y","circularity","rectangularity"})Require(Equals(cell.GetValue(grid.Items[0],new object[]{key}),record.Get<double>(key)),"已有真实特征被占位替换："+key);}
    var permanent=DisplayScene();var permanentItems=DisplayItems();var semantic=permanent is null?Array.Empty<object>():LayerItems(Get(permanent,"Layer"));Require(permanent is not null&&semantic.OfType<SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface.IRectangle>().Count()==grid.Items.Count&&semantic.Any(item=>item.GetType().Name.Contains("MultiText",StringComparison.Ordinal)),"真实夹具缺同次常驻结果矩形/文字");
    ResultSelection? selected=null;object? senderIdentity=null;EventHandler<ResultSelection> capture=(sender,value)=>{senderIdentity=sender;selected=value;};table.SelectionHighlightChanged+=capture;
    try{grid.SelectedItem=null;grid.SelectedItem=grid.Items[0];Pump();}finally{table.SelectionHighlightChanged-=capture;}
    Require(ReferenceEquals(senderIdentity,table),"真实Blob包装事件sender不是活动BlobResultView，而是"+senderIdentity?.GetType().Name);
    Require(selected is {Targets.Count:1}&&selected.Publication==publication.Stamp,"真实表事件未携带同次Stamp和几何");
    var receipt=(SunEyeVision.Plugin.Wpf.Presenters.Display.NativeResultFrameReceipt)Get(output,"CurrentFrame");Require(selected!.Targets[0].ImageFrame==receipt.Frame&&selected.Targets[0].Coordinates==receipt.Coordinates,"表事件与显示物理帧/域不匹配：target="+selected.Targets[0].ImageFrame+"/"+selected.Targets[0].Coordinates+"；viewport="+receipt.Frame+"/"+receipt.Coordinates);
    var highlight=SelectionItems();var combined=Shapes();Require(highlight.Length==1&&Equals(Field(selectionLayer,"_receipt"),receipt)&&highlight.All(item=>Contains(combined,item)),"真实订阅没有把表选择传到独立同帧高亮层");Require(ReferenceEquals(DisplayScene(),permanent)&&permanentItems.All(item=>Contains(combined,item)),"选择覆盖常驻结果矩形或文字");return selected;
   }
   var first=Publish(0);Until(()=>Get(main,"_activeResultView") is IToolResultView);SelectImage();var oldPublication=Publication();VerifyWrapper(oldPublication);var oldSelection=SelectRow(first);var oldFrame=oldSelection.Targets[0].ImageFrame;
   var oldLease=(IDisposable)output.GetType().GetMethod("BorrowCurrentImage",Flags)!.Invoke(output,null)!;borrowed.Add(oldLease);retainedImage=(IReadOnlyImage)Get(oldLease,"Image");Require((bool)Get(oldLease,"IsCurrent"),"当前图像租约未建立");
   SelectImage(BlobSchema.Field(6));SelectRow(first);
   SelectImage(ImageSourceOutput.Image,source.Id);var rawReceipt=(SunEyeVision.Plugin.Wpf.Presenters.Display.NativeResultFrameReceipt)Get(output,"CurrentFrame");
   Require(rawReceipt.Frame==oldFrame&&rawReceipt.Coordinates!=oldSelection.Targets[0].Coordinates,"真实原始上游图像没有保留独立实际域");
   var rawGrid=Visuals<DataGrid>(Table().View).Single();rawGrid.SelectedItem=null;rawGrid.SelectedItem=rawGrid.Items[0];Pump();Require(viewport.ShapeList is null||((IList)viewport.ShapeList).Count==0,"缺输入槽到实际域证明时错误绘制原始上游图像");
   SelectImage();SelectRow(first);
   var second=Publish(1);SelectImage();var currentPublication=Publication();
   Require(first.Identity!=second.Identity&&oldPublication.Stamp!=currentPublication.Stamp&&!oldPublication.IsCurrent(),"重跑沿用旧RunIdentity或publication");
   var currentSelection=SelectRow(second);Require(currentSelection.Targets[0].ImageFrame!=oldFrame,"两轮真实运行沿用同一物理Frame");
   Require(!(bool)Get(oldLease,"IsCurrent"),"重跑后旧图像租约仍可发布");var layer=viewport.ShapeList;var shape=((IList)layer!)[0];
   // Re-deliver the captured real event through the production handler, never invent a selection payload.
   Call(main,"OnDeclaredSelectionHighlightChanged",Table(),oldSelection);Pump();Require(ReferenceEquals(layer,viewport.ShapeList)&&ReferenceEquals(shape,((IList)viewport.ShapeList!)[0]),"旧Stamp选择覆盖新高亮");
   var selectedItems=SelectionItems();var permanentBeforeClear=DisplayScene();var permanentItemsBeforeClear=DisplayItems();
   main.ClearResultHighlight();Pump();var combinedAfterClear=Shapes();Require(SelectionItems().Length==0&&selectedItems.All(item=>!Contains(combinedAfterClear,item)),"真实清空高亮未生效");
   Require(ReferenceEquals(DisplayScene(),permanentBeforeClear)&&permanentItemsBeforeClear.All(item=>Contains(combinedAfterClear,item)),"清高亮删除常驻图文");
   var emptySnapshot=Publish(0,true);SelectImage();var emptyGrid=Visuals<DataGrid>(Table().View).Single();Require(emptyGrid.Items.Count==0&&emptyGrid.Columns.Count==10,"空结果制造占位假行或丢表头");
   Require(emptyGrid.Columns.Cast<DataGridColumn>().Where(column=>column.Header?.ToString()?.Contains("未实现")==true).Count()==3&&emptyGrid.ToolTip?.ToString()?.Contains("空结果不代表")==true,"空结果表未明确保留三项未实现说明");
   foreach(var order in new[]{BlobSortMode.SortModeAscend,BlobSortMode.SortModeDecend})
   {
    node.RuntimeConfiguration=node.RuntimeConfiguration!.WithSettings(new BlobSettings{Threshold=100,IsAreaFilterEnabled=false,OutputGrayImage=true,OutputBinaryImage=true,SortMode=order});
    var sorted=Publish(0,multiple:true);SelectImage();var sortedView=Table();var sortedGrid=Visuals<DataGrid>(sortedView.View).Single();Require(sortedGrid.Items.Count==2,"排序样本未得到两个真实Blob");
    using var reader=sorted.AcquireReader();var records=reader.Values!.Get<SequenceValue>(BlobSchema.BlobsPort).Cast<RecordValue>().ToArray();var areas=records.Select(record=>record.Get<double>("area")).ToArray();Require(areas.SequenceEqual(order==BlobSortMode.SortModeAscend?new[]{4d,64d}:new[]{64d,4d}),"真实反序排序未生效");
    for(var index=0;index<2;index++)
    {
     ResultSelection? selected=null;EventHandler<ResultSelection> capture=(_,value)=>selected=value;sortedView.SelectionHighlightChanged+=capture;try{sortedGrid.SelectedItem=null;sortedGrid.SelectedItem=sortedGrid.Items[index];Pump();}finally{sortedView.SelectionHighlightChanged-=capture;}
     var record=records[index];Require(selected is {Targets.Count:1}&&Equals(selected.ElementIdentity,record.Get<long>("element-id"))&&selected.Publication==Publication().Stamp,"排序后选择使用行号替代真实element-id或Stamp");
     var vertices=record.Get<SequenceValue>("rotated-bounds").Cast<PointGeometry>().Select(point=>point.Point).ToArray();Require(selected!.Targets[0].Geometry.Single() is PolygonGeometry polygon&&polygon.Vertices.SequenceEqual(vertices),"排序后的事件几何不属于同一记录");
     var highlights=SelectionItems();Require(highlights.Length==1&&highlights.All(item=>Contains(Shapes(),item))&&DisplayItems().All(item=>Contains(Shapes(),item)),"排序选择未在独立层呈现或覆盖常驻图文");var rendered=((IEnumerable<Point>)Get(highlights.Single(),"Points")).Select(point=>new Point2(point.X,point.Y));Require(rendered.SequenceEqual(vertices),"排序后实际选择层高亮与选中记录错配");
    }
   }
   view.Shutdown();Require(!(bool)Get(oldLease,"IsCurrent"),"工作区关闭复活旧租约");oldLease.Dispose();oldLease.Dispose();borrowed.Clear();
   Console.WriteLine("回执：run1="+first.Identity+"；run2="+second.Identity+"；frame1="+oldFrame+"；frame2="+currentSelection.Targets[0].ImageFrame);
  }
  finally
  {
   foreach(var lease in borrowed)lease.Dispose();view?.Shutdown();if(window is not null){window.Content=null;window.Close();}if(main is not null){Call(main,"CloseResultPresentation");Call(main,"ReleaseDocumentHost");}context?.ClearNodeResults();foreach(var run in runs)run.Dispose();managerField.SetValue(null,previous);
  }
  Require(retainedImage is not null,"没有捕获真实灰度资源以核释放");var released=false;try{using var pixels=retainedImage!.AcquirePixels();}catch(ObjectDisposedException){released=true;}Require(released,"所有真实发布/图像租约/运行owner释放后仍可借阅旧灰度资源");
 }
 sealed class CheckLogger:ILogger{public ILogger ForSource(string source)=>this;public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All){if(level==LogLevel.Error)Console.Error.WriteLine(message+" "+exception);}}
}
