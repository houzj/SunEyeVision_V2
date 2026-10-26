using SunEyeVision.Plugin.Contracts;
namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Finite Blob presentation roles; no WPF, live parameters, reflection or scheduling.</summary>
public static class BlobPresentation
{
 public static FieldLayout Layout(string key, IReadOnlyDictionary<string,string> ids)
 {
  var basic=key.StartsWith("input.")||key=="setting.pose-enabled";
  var result=key.StartsWith("presentation.");
  var page=basic?"基本参数":result?"结果显示":"运行参数";
  var section=basic?"ROI区域":key.StartsWith("presentation.judgments.")?"结果判断":key.StartsWith("presentation.graphics.")?"图像显示":key.StartsWith("presentation.texts.")?"文本显示":"运行参数";
  FieldCondition? condition=null;string? paired=null;
  var minimum=default(double?);var maximum=default(double?);var decimals=2;
  var advanced=false;var allowReference=!key.EndsWith("Enabled")&&!key.EndsWith("IsVisible")&&!key.EndsWith("IsBold");
  if(key is "setting.pose-enabled" or "setting.SegmentationMode")allowReference=false;
  string? Id(string logical)=>ids.TryGetValue(logical,out var id)?id:null;
  void When(string logical,object value){if(Id(logical) is {} id)condition=new(id,value);}
  if(key is "setting.Threshold" or "setting.UpperThreshold"){minimum=0;maximum=255;decimals=0;}
  if(key is "setting.Threshold" or "setting.Type")condition=new(ids["setting.SegmentationMode"],"None",true);
  if(key=="setting.UpperThreshold")When("setting.SegmentationMode", "DoubleThreshold");
  if(key.StartsWith("setting.Filter"))
  {
   section="Blob筛选";
   var family=key.Contains("Circularity")?"Circularity":key.Contains("Rectangularity")?"Rectangularity":"Area";
   When("setting.Is"+family+"FilterEnabled",true);
   minimum=0;maximum=family=="Area"?9999999:1;
   if(family=="Area")decimals=0;
   if(key.EndsWith("Min"))paired=Id(key[..^3]+"Max");
  }
  if(key is "setting.IsAreaFilterEnabled" or "setting.IsCircularityFilterEnabled" or "setting.IsRectangularityFilterEnabled" or "setting.SortMode" or "setting.IsBlobCountFilterEnabled" or "setting.BlobCountLimit")section="Blob筛选";
  if(key=="setting.BlobCountLimit"){When("setting.IsBlobCountFilterEnabled",true);minimum=1;maximum=1000000;decimals=0;}
  if(key.StartsWith("setting.Output"))section="输出";
  if(key.StartsWith("presentation.judgments."))
  {
   allowReference=false;var prefix=key[..key.LastIndexOf('.')];
   if(key.EndsWith(".Min")||key.EndsWith(".Max")){When(prefix+".Enabled",true);if(key.EndsWith(".Min"))paired=Id(prefix+".Max");}
   advanced=!key.Contains("output-not-empty")&&!key.Contains("blob-count")&&!key.Contains("total-area");
  }
  string? dialog=null,title=null;var primary=false;
  if(key.StartsWith("presentation.graphics."))
  {
   dialog=key[..key.IndexOf('.',"presentation.graphics.".Length)];primary=key.EndsWith(".IsVisible");
   title=dialog["presentation.graphics.".Length..] switch {"blobs"=>"Blob","centroids"=>"质心","contours"=>"轮廓","inspection-roi"=>"检测区域","mask-roi"=>"屏蔽区域",_=>throw new ArgumentException("Unknown declared Blob display group.")};
  }
  return new(page,section,condition,paired,advanced,minimum,maximum,decimals,allowReference,dialog,title,primary);
 }
}
