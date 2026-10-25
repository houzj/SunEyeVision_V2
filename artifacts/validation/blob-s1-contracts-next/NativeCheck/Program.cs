using System.IO;
using System.Text.Json;
using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;

var rows=new List<object>();
var r=Enumerable.Repeat((byte)100,35).ToArray();var g=Enumerable.Repeat((byte)150,35).ToArray();var b=Enumerable.Repeat((byte)200,35).ToArray();
using var red=HalImage.CreateFromBytes(r,7,5,1);using var green=HalImage.CreateFromBytes(g,7,5,1);using var blue=HalImage.CreateFromBytes(b,7,5,1);
using var rgb=red.Unwrap().Compose3(green.Unwrap(),blue.Unwrap());
using var expectedNative=rgb.Rgb1ToGray();using var expected=new HalImage(expectedNative.CopyImage());var expectedBytes=expected.GetPixelData();
if(expectedBytes[0] is 100 or 150 or 200)throw new Exception("Non-equal RGB fixture did not exercise luminance conversion");
foreach(var mode in new[]{"full","reduced","empty"})foreach(byte opacity in new byte[]{0,255})
{
 if(args.Contains("domain-only")&&mode=="full")continue;
 try {
  var alphaBytes=Enumerable.Repeat(opacity,35).ToArray();using var alpha=HalImage.CreateFromBytes(alphaBytes,7,5,1);using var rgba=rgb.AppendChannel(alpha.Unwrap());using var domain=new HRegion();
  if(mode=="empty")domain.GenEmptyRegion();else if(mode=="reduced")domain.GenRectangle1(1d,2d,2d,4d);else domain.GenRectangle1(0d,0d,4d,6d);
  using var reduced=rgba.ReduceDomain(domain);using var input=new HalImage(reduced.CopyImage());
  var frame=new ImageFrameAddress(Guid.NewGuid(),"native-helper-fixture");using var before=input.CaptureSnapshot(frame);var original=before.CopyPixels();
  using var converted=HalImage.CreateGrayFromColor(input);using var gray=converted.CaptureSnapshot(new ImageFrameAddress(Guid.NewGuid(),"native-gray-fixture"));
  using var grayDomain=gray.EffectiveDomain.AcquireReader();using var originalDomain=before.EffectiveDomain.AcquireReader();
  if(grayDomain.RunCount!=originalDomain.RunCount||!grayDomain.ReadRuns(0,4096).Span.SequenceEqual(originalDomain.ReadRuns(0,4096).Span))throw new Exception("Conversion changed actual Domain");
  // HALCON evaluates rgb1_to_gray on its actual Domain. Bytes outside that Domain are not computed luminance.
  var grayBytes=gray.CopyPixels();
  foreach(var run in grayDomain.ReadRuns(0,4096).Span)
   for(int column=run.FirstColumn;column<=run.LastColumnInclusive;column++)
    if(grayBytes[run.Row*7+column]!=expectedBytes[run.Row*7+column])throw new Exception("RGBA in-domain luminance differs from identical RGB");
  using var after=input.CaptureSnapshot(frame);if(!original.SequenceEqual(after.CopyPixels()))throw new Exception("Conversion changed source pixels/alpha");
  for(int index=0;index<35;index++)if(original[index*4+3]!=opacity)throw new Exception("RGBA snapshot did not preserve alpha");
  rows.Add(new{mode,opacity,passed=true,grayValue=expectedBytes[0],domainRuns=grayDomain.RunCount});
 }catch(Exception fault){rows.Add(new{mode,opacity,passed=false,error=fault.ToString()});}
}
Console.WriteLine(JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
return rows.All(row=>JsonSerializer.SerializeToElement(row).GetProperty("passed").GetBoolean())?0:1;
