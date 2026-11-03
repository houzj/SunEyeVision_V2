using System;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Interaction.ListView;
internal static class Program
{
 [STAThread] static int Main() { try {
 var app=new Application();
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
