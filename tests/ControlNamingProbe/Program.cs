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
 var blobContext=SunEyeVision.Tool.BlobAnalysis.Wpf.Design.BlobDesignData.CreateParameterContext();
 var blobSchema=new SunEyeVision.Tool.BlobAnalysis.Runtime.BlobTool().Describe();
 var input=new SunEyeVision.Plugin.Wpf.Presenters.Parameters.DeclaredInputFieldsView(blobSchema,_=>new SunEyeVision.Plugin.Contracts.UnsetSetting(),(_,_)=>{},SunEyeVision.Plugin.Wpf.Adapters.References.ReferenceFieldOptions.Empty);
 var blobOptions=new SunEyeVision.Plugin.Wpf.Presenters.Parameters.ToolSettingsViewOptions(ReferenceOptions:SunEyeVision.Plugin.Wpf.Adapters.References.ReferenceFieldOptions.Empty,InputSection:blobSchema.Presentation!.Fields.First().Layout!.Page,InputContent:input);
 using(var blob=new SunEyeVision.Tool.BlobAnalysis.Wpf.Views.BlobParameterEditor(blobContext.Draft,blobOptions))
 { blob.Measure(new Size(680,800)); blob.Arrange(new Rect(0,0,680,800)); }
 var sourceContext=SunEyeVision.Tool.ImageSource.Wpf.Design.ImageSourceDesignData.CreateParameterContext();
 using(var source=new SunEyeVision.Tool.ImageSource.Wpf.Views.ImageSourceParameterEditor(sourceContext.Draft,sourceContext.Options))
 { source.Measure(new Size(680,800)); source.Arrange(new Rect(0,0,680,800)); }
 Console.WriteLine("PASS: real Blob and image source editor assembly with design inputs");
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
 Console.WriteLine("PASS: real slider, result viewport and embedded cursor resources");return 0;
 } catch(Exception e){Console.Error.WriteLine(e);return 1;} }
}
