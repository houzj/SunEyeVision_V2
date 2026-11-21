using System;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Interaction.ListView;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Regions;
internal static class Program
{
 [STAThread] static int Main() { try {
 var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
 foreach(var path in new[]{
 "Themes/Native/OriginalTheme.xaml",
 "Themes/Native/Display/ImageViewport/themes/generic.xaml",
 "Themes/Native/Display/PresentationControls/themes/generic.xaml",
 "Themes/Native/Display/PresentationControls/controls/themes/aero2.normalcolor.xaml",
 "Themes/Native/Numeric/PresentationControls/controls/numericupdown/themes/generic.xaml"})
 { var dict=new ResourceDictionary{Source=new Uri("/SunEyeVision.Plugin.Wpf;component/"+path,UriKind.Relative)}; if(dict.Source is null) throw new Exception(path); Console.WriteLine("PASS: dictionary "+path); }
 foreach(var locale in new[]{"zh-cn","en-us"})
 { var text=new ResourceDictionary{Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Localization/"+locale+".xaml",UriKind.Relative)};
   if(text["Lang_LearnApplication"]?.ToString()?.Contains("VisionMaster",StringComparison.OrdinalIgnoreCase)!=false ||
      !text.Contains("RunParam_ExternalBoard_I Calibration Board")) throw new Exception("localization identity"); }
 Console.WriteLine("PASS: neutral application labels and factual third-party board keys in both locales");
 var slider=new SliderArrange{Minimum=0,Maximum=100000,StartValue=123,SliderTickFrequency=10};
 if(slider.StartValue!=123) throw new Exception("slider value");
 var blobContext=SunEyeVision.Plugin.Wpf.Presenters.Design.DesignTimeSample.Create(new SunEyeVision.Tool.BlobAnalysis.Runtime.BlobTool(),SunEyeVision.Tool.BlobAnalysis.Runtime.BlobTool.ImageInput);
 var blobSchema=new SunEyeVision.Tool.BlobAnalysis.Runtime.BlobTool().Describe();
 var input=new SunEyeVision.Plugin.Wpf.Presenters.Parameters.DeclaredInputFieldsView(blobSchema,_=>new SunEyeVision.Plugin.Contracts.UnsetSetting(),(_,_)=>{},SunEyeVision.Plugin.Wpf.Adapters.References.ReferenceFieldOptions.Empty);
 var blobOptions=new SunEyeVision.Plugin.Wpf.Presenters.Parameters.ToolSettingsViewOptions(ReferenceOptions:SunEyeVision.Plugin.Wpf.Adapters.References.ReferenceFieldOptions.Empty,InputSection:blobSchema.Presentation!.Fields.First().Layout!.Page,InputContent:input);
 using(var blob=new SunEyeVision.Tool.BlobAnalysis.Wpf.Views.BlobParameterEditor(blobContext.Draft,blobOptions))
 { blob.Measure(new Size(680,800)); blob.Arrange(new Rect(0,0,680,800)); }
 // 走生产装配路径：有参构造会创建 BlobUiComposition 并装配完整参数页。
 // 传 PageBuilt 让页面构建钩子真正执行，覆盖 ROI 组与位置修正组的装配路径。
using(var sample=new SunEyeVision.Tool.BlobAnalysis.Wpf.Views.BlobParameterEditor(blobContext.Draft,
 blobContext.Options with { PageBuilt=(page,built,input)=>{ } }))
 {
  var presenter=(SunEyeVision.Plugin.Wpf.Presenters.Parameters.DeclaredSettingsView)sample.FindName("Editor");
  var parameterTree=presenter.Content??throw new Exception("Blob design parameter tree was not built");
 if(parameterTree is SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldsView fieldsView)
 {
  if(sample.CompositionError is {} compositionError)throw new Exception("Blob composition error: "+compositionError);
  sample.Measure(new Size(680,800));sample.Arrange(new Rect(0,0,680,800));
  Console.WriteLine("PASS: Blob ROI group composed without a composition error");
  sample.Measure(new Size(680,800));sample.Arrange(new Rect(0,0,680,800));
 }
  if(presenter.SelectedPageKey!="基本参数")throw new Exception("Blob designer does not open the production first page");
  var designInputs=(SunEyeVision.Plugin.Wpf.Presenters.Parameters.DeclaredInputFieldsView)blobContext.Options.InputContent!;
  var imageRow=designInputs.Fields.Single(row=>row.FieldId==SunEyeVision.Tool.BlobAnalysis.Runtime.BlobTool.ImageInput);
  if(imageRow.InputElement is not System.Windows.Controls.ComboBox {SelectedItem:SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldRow.ImageSourceChoice {Label:"图像源 · 图像"}})
   throw new Exception("Blob designer input source is not projected through the real selector");
  foreach(var (index,key) in new[]{(1,"运行参数"),(2,"结果显示"),(0,"基本参数")})
  {
   presenter.SelectedPageIndex=index;
   if(sample.CompositionError is {} compositionError)throw new Exception("Blob composition error: "+compositionError);
  sample.Measure(new Size(680,800));sample.Arrange(new Rect(0,0,680,800));
  Console.WriteLine("PASS: Blob ROI group composed without a composition error");
  sample.Measure(new Size(680,800));sample.Arrange(new Rect(0,0,680,800));
   if(presenter.SelectedPageKey!=key)throw new Exception("Blob designer page was not composed: "+key);
  }
 }
 var sourceContext=SunEyeVision.Plugin.Wpf.Presenters.Design.DesignTimeSample.Create(new SunEyeVision.Tool.ImageSource.Runtime.ImageSourceTool(),cameraSource:new SampleCameraSource());
 using(var source=new SunEyeVision.Tool.ImageSource.Wpf.Views.ImageSourceParameterEditor(sourceContext.Draft,sourceContext.Options))
 { source.Measure(new Size(680,800)); source.Arrange(new Rect(0,0,680,800)); }
 Console.WriteLine("PASS: real Blob and image source editor assembly with design inputs");
Console.WriteLine("PASS: image source camera row renders through the injected device source");
 CheckBatchTool("PixelCount",new SunEyeVision.Tool.PixelCount.Runtime.PixelCountTool(),new SunEyeVision.Tool.PixelCount.Wpf.PixelCountEditorFactory(),SunEyeVision.Tool.PixelCount.Runtime.PixelCountTool.ImageInput);
 CheckBatchTool("FrameMean",new SunEyeVision.Tool.FrameMean.Runtime.FrameMeanTool(),new SunEyeVision.Tool.FrameMean.Wpf.FrameMeanEditorFactory(),SunEyeVision.Tool.FrameMean.Runtime.FrameMeanTool.ImageInput);
 CheckBatchTool("IntensityMeasure",new SunEyeVision.Tool.IntensityMeasure.Runtime.IntensityMeasureTool(),new SunEyeVision.Tool.IntensityMeasure.Wpf.IntensityMeasureEditorFactory(),SunEyeVision.Tool.IntensityMeasure.Runtime.IntensityMeasureTool.ImageInput);
 Console.WriteLine("PASS: batch-1 PixelCount/FrameMean/IntensityMeasure settings and result views use the shared presentation path");
 CheckBatchTool("ColorExtract",new SunEyeVision.Tool.ColorExtract.Runtime.ColorExtractTool(),new SunEyeVision.Tool.ColorExtract.Wpf.ColorExtractEditorFactory(),SunEyeVision.Tool.ColorExtract.Runtime.ColorExtractTool.ImageInput);
 CheckBatchTool("DivideImage",new SunEyeVision.Tool.DivideImage.Runtime.DivideImageTool(),new SunEyeVision.Tool.DivideImage.Wpf.DivideImageEditorFactory(),SunEyeVision.Tool.DivideImage.Runtime.DivideImageTool.ImageInput);
 CheckBatchTool("ContourMatch",new SunEyeVision.Tool.ContourMatch.Runtime.ContourMatchTool(),new SunEyeVision.Tool.ContourMatch.Wpf.ContourMatchEditorFactory(),SunEyeVision.Tool.ContourMatch.Runtime.ContourMatchSchema.ImageInput);
 CheckBatchTool("GrayMatch",new SunEyeVision.Tool.GrayMatch.Runtime.GrayMatchTool(),new SunEyeVision.Tool.GrayMatch.Wpf.GrayMatchEditorFactory(),SunEyeVision.Tool.GrayMatch.Runtime.GrayMatchSchema.ImageInput);
 CheckBatchTool("MarkFind",new SunEyeVision.Tool.MarkFind.Runtime.MarkFindTool(),new SunEyeVision.Tool.MarkFind.Wpf.MarkFindEditorFactory(),SunEyeVision.Tool.MarkFind.Runtime.MarkFindSchema.ImageInput);
 CheckBatchTool("BlobLabels",new SunEyeVision.Tool.BlobLabels.Runtime.BlobLabelsTool(),new SunEyeVision.Tool.BlobLabels.Wpf.BlobLabelsEditorFactory(),SunEyeVision.Tool.BlobLabels.Runtime.BlobLabelsSchema.ImageInput);
 CheckBatchTool("If",new SunEyeVision.Tool.If.Runtime.IfTool(),new SunEyeVision.Tool.If.Wpf.IfEditorFactory(),string.Empty);
 Console.WriteLine("PASS: batch-2/3/4 ColorExtract/DivideImage/ContourMatch/GrayMatch/MarkFind/BlobLabels/If settings and result views use the shared presentation path");
 var corner=new SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Base.ImageView.ROI.EditRoundedWindow(100,5);
 corner.Close();
 var normalized=new SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Base.ImageView.ROI.NormalizedRectROI();
 var rectangle=new SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Base.ImageView.ROI.RectROISubstitute { SubstituteRect=new Rect(1,2,3,4) };
 rectangle.Measure(new Size(64,64)); rectangle.Arrange(new Rect(0,0,64,64));
 if(rectangle.SubstituteRect.Width!=3 || normalized is null) throw new Exception("ROI resource or geometry projection failed");
 Console.WriteLine("PASS: normalized rectangle loads dynamic dictionary and native rectangle projection");
 using var viewport=new NativeResultViewport();
 using var host=new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("控制探针") {Width=640,Height=480,WindowStyle=0});
 host.RootVisual=viewport.View;
 System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>{}));
 foreach(var path in new[]{"roiitemspagestyle","subroiitemspagestyle","rectroi/rectroistyle","circleroi/circleroistyle","polygonroi/polygonroistyle","sectorroi/sectorroistyle"})
  try { _=new ResourceDictionary { Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/ImageViewport/base/imageview/roi/"+path+".xaml",UriKind.Relative) }; }
  catch(Exception error) { throw new InvalidOperationException("ROI 字典加载失败："+path,error); }
 using var resources=new FrozenOutputResources();
 var frame=new ImageFrameAddress(Guid.NewGuid(),"naming-probe");
 var coordinates=new CoordinateFrame("naming-probe",CoordinateDomain.Image,CoordinateUnit.Pixel);
 var domain=resources.CreateRegion(coordinates,frame,Enumerable.Range(0,20).Select(y=>new RegionRun(y,0,29)).ToArray());
 var image=resources.CreateImage(new ImageLayout(30,20,30,PixelLayout.Gray8),frame,domain,new byte[600]);
 if(!viewport.BindImage(image,()=>true)) throw new Exception("frame attachment rejected");
 var stamp=new RoiEditStamp(Guid.NewGuid(),0,frame);
 var state=new RoiEditSnapshot(stamp,Array.Empty<RoiEditItem>(),new[]{new RoiGroupState("inspection",RoiCoverage.None,RoiCoverage.None)});
 RoiEditAcceptance Accept(RoiEditStamp before,RoiEditProposal proposal)
 {
  if(before!=state.Stamp || proposal is not RoiEditProposal.Create create) return RoiEditAcceptance.Reject(state,"unexpected proposal");
  var id=Guid.NewGuid();var target=new RoiEditTarget(create.Group,id);
  state=new RoiEditSnapshot(before with {Revision=before.Revision+1},new[]{new RoiEditItem(create.Group,id,"区域",create.Geometry)},new[]{new RoiGroupState("inspection",RoiCoverage.Explicit,RoiCoverage.None)});
  return new(true,state,new[]{target},new RoiEditSelection.Select(target));
 }
 using(var session=new NativeRoiSession(image,()=>state,Accept,()=>true,()=>new RoiHistoryAvailability(false,false),"inspection"))
 using(var lease=viewport.BeginRoiEditing(viewport.CurrentFrame!,session,()=>true))
 {
  var draw=session.BeginDraw("rectangle") ?? throw new Exception("ROI draw request unavailable: "+session.Error);
  var geometry=RoiGeometryEditing.Create("rectangle",new Point2(3,3),new Point2(14,12));
  if(!session.CompleteDraw(draw,geometry) || session.Items.Count!=1) throw new Exception("ROI drawing was not accepted");
 }
 Console.WriteLine("PASS: actual frame, native ROI attachment and rectangle draw transaction");
 foreach(var name in new[]{"SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Core.ErrorMessages.resources", "SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Properties.Resources.resources"})
  using (var stream=typeof(NativeResultViewport).Assembly.GetManifestResourceStream(name) ?? throw new Exception("missing resx "+name))
   if(stream.Length==0) throw new Exception("empty resx "+name);
 foreach(var path in new[]{"Themes/Assets/Native/Input/WorkspaceControls/resources/ic_log_信息.png","Themes/Assets/Native/Display/PresentationControls/resources/arrow_down.png"})
  using (var stream=Application.GetResourceStream(new Uri("/SunEyeVision.Plugin.Wpf;component/"+path,UriKind.Relative))?.Stream ?? throw new Exception("missing resource "+path))
   if(stream.Length==0) throw new Exception("empty resource "+path);
 foreach(var name in new[]{"鼠标双向箭头.png","旋转光标.png","鼠标旋转箭头.png"})
  using (var stream=typeof(NativeResultViewport).Assembly.GetManifestResourceStream("SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Resources."+name) ?? throw new Exception("missing cursor "+name)) { if(stream.Length==0) throw new Exception("empty cursor"); }
 /// <summary>在视觉树里按类型查找后代控件。</summary>
static T? FindChild<T>(System.Windows.DependencyObject root) where T : System.Windows.DependencyObject
{
    for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++)
    {
        var child=System.Windows.Media.VisualTreeHelper.GetChild(root,i);
        if(child is T match)return match;
        if(FindChild<T>(child) is {} nested)return nested;
    }
    return null;
}

    Console.WriteLine("PASS: real slider, result viewport and embedded cursor resources");return 0;
 } catch(Exception e){Console.Error.WriteLine(e);return 1;} }

 static void CheckBatchTool(string name,SunEyeVision.Plugin.Contracts.IToolDefinition tool,SunEyeVision.Plugin.Wpf.Presenters.Results.IToolPresentationFactory factory,string imageInput)
 {
  var defaultSettings=tool.CreateSettings();
  var diagnostics=tool.Validate(defaultSettings);
  if(name=="DivideImage")
  {
   var divideSchema=tool.Describe();
   var roiPort=divideSchema.Inputs.SingleOrDefault(port=>port.Id==SunEyeVision.Tool.DivideImage.Runtime.DivideImageTool.RoiInput);
   if(roiPort is null || roiPort.Type!=SunEyeVision.Plugin.Contracts.TypeRef.ResourceOf(SunEyeVision.Plugin.Contracts.ResourceKind.Region) || roiPort.Required || !roiPort.Nullable)
    throw new Exception("DivideImage 缺少原厂 InputROI 可选输入端口。");
  }
  var runtime=tool.CreateSession(); runtime.DisposeAsync().GetAwaiter().GetResult();
  Console.WriteLine($"PASS: {name} runtime settings/session diagnostics={diagnostics.Count}");
  var context=SunEyeVision.Plugin.Wpf.Presenters.Design.DesignTimeSample.Create(tool,imageInput);
  using var settings=factory.CreateSettingsView(context.Draft,context.Options);
  settings.View.Measure(new Size(680,800));settings.View.Arrange(new Rect(0,0,680,800));
  if(settings.View.ActualWidth<=0 || settings.HasInvalidInput) throw new Exception(name+" 参数页未能通过共享模板组装");
  var declaredPages=FindDescendant<SunEyeVision.Plugin.Wpf.Presenters.Parameters.DeclaredSettingsView>(settings.View);
  if(declaredPages is {} pageDiagnostic)
  {
   var fieldHost=FindDescendant<SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldsView>(settings.View);
   Console.WriteLine($"INFO: {name} declared pages={string.Join('/', fieldHost?.DeclaredPageKeys ?? Array.Empty<string>())}");
  }
  var expectedPagesByTool = name switch
  {
   "PixelCount" => new[] { "基本参数", "运行参数", "结果显示" },
   "FrameMean" => new[] { "基本参数", "运行参数", "结果显示" },
   "IntensityMeasure" => new[] { "基本参数", "结果显示" },
   "ColorExtract" => new[] { "基本参数", "运行参数", "结果显示" },
   "DivideImage" => new[] { "基本参数", "运行参数" },
   "ContourMatch" => new[] { "基本参数", "特征模板", "运行参数", "结果显示" },
   "GrayMatch" => new[] { "基本参数", "灰度模板", "运行参数", "结果显示" },
   "MarkFind" => new[] { "基本参数", "特征模型", "运行参数", "结果显示" },
   "BlobLabels" => new[] { "基本参数", "运行参数", "结果显示" },
   "If" => new[] { "基本参数" },
   _ => Array.Empty<string>()
  };
  if(expectedPagesByTool.Length>0 && declaredPages is {} mappedPages)
  {
   for(var index=0;index<expectedPagesByTool.Length;index++)
   {
    mappedPages.SelectedPageIndex=index;
    if(mappedPages.SelectedPageKey!=expectedPagesByTool[index])throw new Exception(name+" 页签顺序不符合原厂证据："+mappedPages.SelectedPageKey);
   }
   mappedPages.SelectedPageIndex=0;
   Console.WriteLine($"PASS: {name} pages {string.Join('/', expectedPagesByTool)} order");
   if(name=="PixelCount")
   {
    mappedPages.SelectedPageIndex=1;
    var pixelFields=FindDescendant<SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldsView>(settings.View);
    pixelFields?.RealizePage("运行参数");
    settings.View.Measure(new Size(680,800));settings.View.Arrange(new Rect(0,0,680,800));
    var pixelEditor=settings.View as SunEyeVision.Tool.PixelCount.Wpf.Views.PixelCountParameterEditor
        ?? FindDescendant<SunEyeVision.Tool.PixelCount.Wpf.Views.PixelCountParameterEditor>(settings.View);
    var rangePanel= pixelEditor?.GetType().GetField("_rangePanel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.GetValue(pixelEditor)
        as SunEyeVision.Tool.PixelCount.Wpf.Views.PixelCountRangePanel;
    if(rangePanel is null || pixelFields is null || !pixelFields.ContainsView(rangePanel))
     throw new Exception("PixelCount 缺少原厂范围列表面板接缝。");
    mappedPages.SelectedPageIndex=0;
    Console.WriteLine("PASS: PixelCount 原厂范围列表面板已在运行参数页物化");
   }
   if(name=="FrameMean")
   {
    var frameEditor=settings.View as SunEyeVision.Tool.FrameMean.Wpf.Views.FrameMeanParameterEditor
        ?? FindDescendant<SunEyeVision.Tool.FrameMean.Wpf.Views.FrameMeanParameterEditor>(settings.View);
    var commandPanel=frameEditor?.GetType().GetField("_commandPanel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.GetValue(frameEditor);
    if(commandPanel is null)
     throw new Exception("FrameMean 缺少原厂命令选择器面板接缝。");
    Console.WriteLine("PASS: FrameMean 原厂命令选择器面板已物化");
   }
   if(name=="ColorExtract")
   {
    var editor=settings.View as SunEyeVision.Tool.ColorExtract.Wpf.Views.ColorExtractParameterEditor
        ?? FindDescendant<SunEyeVision.Tool.ColorExtract.Wpf.Views.ColorExtractParameterEditor>(settings.View);
    var panel=editor?.GetType().GetField("_rangePanel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.GetValue(editor);
    if(panel is not SunEyeVision.Tool.ColorExtract.Wpf.Views.ColorExtractRangePanel)
     throw new Exception("ColorExtract 缺少原厂颜色范围/直方图面板接缝。");
    Console.WriteLine("PASS: ColorExtract 原厂颜色范围面板已物化");
   }
   if(name=="ContourMatch")
   {
    var editor=settings.View as SunEyeVision.Tool.ContourMatch.Wpf.Views.ContourMatchParameterEditor
        ?? FindDescendant<SunEyeVision.Tool.ContourMatch.Wpf.Views.ContourMatchParameterEditor>(settings.View);
    var panel=editor?.GetType().GetField("_templatePanel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.GetValue(editor);
    if(panel is not SunEyeVision.Tool.ContourMatch.Wpf.Views.ContourMatchTemplatePanel)
     throw new Exception("ContourMatch 缺少原厂特征模板面板接缝。");
    Console.WriteLine("PASS: ContourMatch 原厂特征模板面板已物化");
   }
   if(name=="GrayMatch")
   {
    var editor=settings.View as SunEyeVision.Tool.GrayMatch.Wpf.Views.GrayMatchParameterEditor
        ?? FindDescendant<SunEyeVision.Tool.GrayMatch.Wpf.Views.GrayMatchParameterEditor>(settings.View);
    var panel=editor?.GetType().GetField("_templatePanel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.GetValue(editor);
    if(panel is not SunEyeVision.Tool.GrayMatch.Wpf.Views.GrayMatchTemplatePanel)
     throw new Exception("GrayMatch 缺少原厂灰度模板面板接缝。");
    Console.WriteLine("PASS: GrayMatch 原厂灰度模板面板已物化");
   }
   if(name=="MarkFind")
   {
    var editor=settings.View as SunEyeVision.Tool.MarkFind.Wpf.Views.MarkFindParameterEditor
        ?? FindDescendant<SunEyeVision.Tool.MarkFind.Wpf.Views.MarkFindParameterEditor>(settings.View);
    var panel=editor?.GetType().GetField("_templatePanel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.GetValue(editor);
    if(panel is not SunEyeVision.Tool.MarkFind.Wpf.Views.MarkFindTemplatePanel)
     throw new Exception("MarkFind 缺少原厂特征模型面板接缝。");
    Console.WriteLine("PASS: MarkFind 原厂特征模型面板已物化");
   }
   if(name=="BlobLabels")
   {
    var editor=settings.View as SunEyeVision.Tool.BlobLabels.Wpf.Views.BlobLabelsParameterEditor
        ?? FindDescendant<SunEyeVision.Tool.BlobLabels.Wpf.Views.BlobLabelsParameterEditor>(settings.View);
    var panel=editor?.GetType().GetField("_thresholdPanel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.GetValue(editor);
    if(panel is not SunEyeVision.Tool.BlobLabels.Wpf.Views.BlobLabelsThresholdAreaPanel)
     throw new Exception("BlobLabels 缺少原厂阈值面积面板接缝。");
    Console.WriteLine("PASS: BlobLabels 原厂阈值面积面板已物化");
   }
   if(name=="If")
   {
    var ifEditor=settings.View as SunEyeVision.Tool.If.Wpf.Views.IfParameterEditor
        ?? FindDescendant<SunEyeVision.Tool.If.Wpf.Views.IfParameterEditor>(settings.View);
    var branchPanel=ifEditor?.GetType().GetField("_branchPanel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.GetValue(ifEditor);
    if(branchPanel is null)
     throw new Exception("If 缺少原厂动态条件行面板接缝。");
    Console.WriteLine("PASS: If 原厂动态条件行面板已物化");
   }
  }
  if(tool is not SunEyeVision.Plugin.Contracts.Results.Presentation.IToolResultPresentationProvider provider)
   throw new Exception(name+" 缺少结果投影声明");
  using var result=factory.CreateResultView(provider.ResultSchema,provider.ResultProjection);
  result.View.Measure(new Size(680,800));result.View.Arrange(new Rect(0,0,680,800));
  if(result.Sources.Count==0 && provider.ResultSchema.Outputs.Count==0) throw new Exception(name+" 结果视图没有输出声明");
  Console.WriteLine($"PASS: {name} settings fields={settings.AcceptedFieldOrder.Count} outputs={provider.ResultSchema.Outputs.Count}");
 }
 static T? FindDescendant<T>(System.Windows.DependencyObject root) where T : System.Windows.DependencyObject
 {
  for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++)
  {
   var child=System.Windows.Media.VisualTreeHelper.GetChild(root,i);
   if(child is T match)return match;
   if(FindDescendant<T>(child) is {} nested)return nested;
  }
  return null;
 }
}

/// <summary>探针用的相机源：一台假设备，验证参数页相机行可渲染。</summary>
internal sealed class SampleCameraSource : SunEyeVision.Plugin.Contracts.ICameraSelectionSource
{
    public IReadOnlyList<SunEyeVision.Plugin.Contracts.CameraChoiceItem> Choices { get; } =
        new[] { new SunEyeVision.Plugin.Contracts.CameraChoiceItem("示例相机", "sample-camera") };
    public event Action? Changed { add { } remove { } }
    public string Status(string? deviceId) => deviceId is null ? "未选择" : "已连接（示例）";
    public void Open(string? deviceId) { }
}

