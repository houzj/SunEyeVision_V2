using System.IO;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Native.Display.ImageView;
using C=SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

internal static partial class S2DocumentEditingCases
{
    static readonly Type Exporter=typeof(ImageViewBox).Assembly.GetType("SunEyeVision.Plugin.Wpf.Native.Common.NativeImageExport",true)!;
    static object ExportCall(string name,params object?[] args)
    {try{return Exporter.GetMethod(name,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(null,args)!;}catch(TargetInvocationException e){throw e.InnerException!;}}
    static BitmapSource ExportDraw(params object[] shapes)
    {var bitmap=BitmapSource.Create(64,64,96,96,PixelFormats.Gray8,null,new byte[4096],64);bitmap.Freeze();return (BitmapSource)ExportCall("PrepareRenderedBitmap",bitmap,shapes,new System.Windows.Controls.Grid());}
    static byte[] ExportPixels(BitmapSource source)
    {var bitmap=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);var bytes=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(bytes,bitmap.PixelWidth*4,0);return bytes;}
    static BitmapSource ExportDecode(string path)=>BitmapDecoder.Create(new Uri(path),BitmapCreateOptions.IgnoreImageCache|BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
    static bool ExportInk(BitmapSource source)=>ExportPixels(source).Where((v,i)=>i%4!=3).Any(v=>v!=0);
    static void ExportReject(Action action,string text="")
    {try{action();throw new Exception("未拒绝受保护路径");}catch(NotSupportedException e){Require(e.Message.Contains(text),"错误保护："+e.Message);}}
    static void VerifyNativeExportWhole(NativeResultViewport production,Func<object> scene,Func<object> publication,Action rerun,
        string solution,Func<object> fields,Func<int> history,Action hideProductionText)
    {
        var folder=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"native-export");Directory.CreateDirectory(folder);
        var productionView=(ImageViewBox)production.View;
        static void Set(ImageViewBox view,string name,object? value)=>view.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(view,value);
        Exception? Menu(ImageViewBox view,bool render,string name,Action? duringPicker=null,bool cancel=false)
        {
            Exception? error=null;int selected=0;var path=Path.Combine(folder,name);
            Set(view,"NativeSelectExportFile",(Func<SaveFileDialog,bool?>)(dialog=>{selected++;Require(dialog.OverwritePrompt,"丢失覆盖确认");dialog.FileName=path;duringPicker?.Invoke();return !cancel;}));
            Set(view,"NativeReportExportError",(Action<Exception>)(e=>error=e));
            try{(render?view.SaveRenderImageCommand:view.SaveOriginalImageCommand).Execute(null);}
            finally{Set(view,"NativeSelectExportFile",null);Set(view,"NativeReportExportError",null);}
            if(error is null)Require(selected==1,"未经过真实菜单文件选择边界");return error;
        }
        Case("图像导出 真实Blob发布可见文字原菜单受保护且无文件选择或副作用",()=>{
            var oldFields=fields();var oldHistory=history();var bytes=File.ReadAllBytes(solution);var oldPublication=publication();Require(scene()!=null,"真实场景缺失");
            var error=Menu(productionView,true,"text-must-not-exist.png");
            Require(error is NotSupportedException&&error.Message.Contains("PutTextZh")&&!File.Exists(Path.Combine(folder,"text-must-not-exist.png")),"可见字被假导出或部分写入");
            Require(ReferenceEquals(oldFields,fields())&&history()==oldHistory&&ReferenceEquals(publication(),oldPublication)&&bytes.SequenceEqual(File.ReadAllBytes(solution)),"导出改变正式配置/历史/运行/方案");
        });
        hideProductionText();Pump();var currentPublication=publication();
        Case("图像导出 真实Blob菜单图形PNG编码解码与冻结后重跑取消覆盖",()=>{
            var oldFields=fields();var oldHistory=history();var bytes=File.ReadAllBytes(solution);var before=production.CurrentFrame!;
            var expected=(BitmapSource)typeof(NativeResultViewport).GetMethod("PrepareExport",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(production,new object[]{true})!;
            Require(expected.Format==PixelFormats.Bgr24,"灰度结果图没有扩为BGR，误用显示BGRA");
            Require(Menu(productionView,true,"blob-render.png",rerun)==null,"真实Blob菜单导出失败");
            var decoded=ExportDecode(Path.Combine(folder,"blob-render.png"));Require(ExportPixels(decoded).SequenceEqual(ExportPixels(expected)),"文件选择重跑后保存了新帧或图层");
            Require(production.CurrentFrame!.Frame!=before.Frame,"夹具未重跑换帧");
            Require(Menu(productionView,false,"cancel.png",cancel:true)==null&&!File.Exists(Path.Combine(folder,"cancel.png")),"取消写文件");
            File.WriteAllText(Path.Combine(folder,"overwrite.png"),"old");Require(Menu(productionView,false,"overwrite.png")==null&&ExportDecode(Path.Combine(folder,"overwrite.png")).PixelWidth==64,"覆盖未接真实编码");
            Require(ReferenceEquals(fields(),oldFields)&&history()==oldHistory&&bytes.SequenceEqual(File.ReadAllBytes(solution))&&!Directory.EnumerateFiles(folder,"*.tmp").Any(),"导出改配置历史方案或残留临时文件");
        });
        foreach(var layout in Enum.GetValues<PixelLayout>())Case("图像导出 菜单原图与结果保真实布局alpha及Domain外像素 "+layout,()=>{
            using var resources=new FrozenOutputResources();var frame=new ImageFrameAddress(Guid.NewGuid(),"export-contract");var coordinates=new CoordinateFrame("export-contract",CoordinateDomain.Image,CoordinateUnit.Pixel);
            var domain=resources.CreateRegion(coordinates,frame,new[]{new RegionRun(0,0,2)});int channels=layout==PixelLayout.Gray8?1:layout==PixelLayout.Bgr24?3:4;
            var data=Enumerable.Range(0,6*channels).Select(i=>(byte)(10+i*3)).ToArray();if(channels==4)for(int i=0;i<6;i++)data[i*4+3]=(byte)(40+i*20);
            var image=resources.CreateImage(new ImageLayout(3,2,3*channels,layout),frame,domain,data);using var viewport=new NativeResultViewport();Require(viewport.BindImage(image,()=>true),"未绑定真实资源");
            var view=(ImageViewBox)viewport.View;Require(Menu(view,false,"raw-"+layout+".png")==null,"菜单原图失败");
            var stored=ExportDecode(Path.Combine(folder,"raw-"+layout+".png"));var converted=new FormatConvertedBitmap(stored,layout==PixelLayout.Gray8?PixelFormats.Gray8:layout==PixelLayout.Bgr24?PixelFormats.Bgr24:PixelFormats.Bgra32,null,0);var actual=new byte[data.Length];converted.CopyPixels(actual,3*channels,0);Require(actual.SequenceEqual(data),"原通道/alpha/Domain外像素改变");
            Require(Menu(view,true,"render-"+layout+".png")==null,"菜单结果图失败");Require(ExportPixels(ExportDecode(Path.Combine(folder,"render-"+layout+".png"))).SequenceEqual(ExportPixels(stored)),"无图形结果改原像素");
            Require(image.CopyPixels().SequenceEqual(data),"导出污染原缓冲");
        });
        Case("图像导出 PNG16保持fallback位深且BMPJPEG不静默降位",()=>{
            var original=new ExportDepthImage();var bitmap=(BitmapSource)ExportCall("PrepareRaw",original);var path=Path.Combine(folder,"depth.png");ExportCall("SavePrepared",path,bitmap);var decoded=ExportDecode(path);var actual=new byte[4];decoded.CopyPixels(actual,4,0);Require(decoded.Format==PixelFormats.Gray16&&actual.SequenceEqual(original.Buffer),"16bit值改变");
            ExportReject(()=>ExportCall("PrepareRendered",original,new object[]{new ExportCircle()},new System.Windows.Controls.Grid()),"16位");
            foreach(var extension in new[]{".bmp",".jpg"})ExportReject(()=>ExportCall("SavePrepared",Path.Combine(folder,"depth"+extension),bitmap),"16位");
        });
        Case("图像导出 fallback平面RGB及有界共享映射头长度偏移释放",()=>{
            var planar=new ExportMappedImage{Width=2,Height=1,PixelFormat="Rgb24_P3",Buffer=new byte[]{10,40,20,50,30,60}};
            var bitmap=(BitmapSource)ExportCall("PrepareRaw",planar);var rgb=new FormatConvertedBitmap(bitmap,PixelFormats.Rgb24,null,0);var values=new byte[6];rgb.CopyPixels(values,6,0);Require(values.SequenceEqual(new byte[]{10,20,30,40,50,60})&&planar.Buffer.SequenceEqual(new byte[]{10,40,20,50,30,60}),"平面RGB未按原重排或改借入源");
            var planarFile=Path.Combine(folder,"planar.png");ExportCall("SavePrepared",planarFile,bitmap);var planarDecoded=new FormatConvertedBitmap(ExportDecode(planarFile),PixelFormats.Rgb24,null,0);var planarValues=new byte[6];planarDecoded.CopyPixels(planarValues,6,0);Require(planarValues.SequenceEqual(new byte[]{10,20,30,40,50,60}),"平面RGB编码通道变化");
            foreach(var channels in new[]{1,3,4})
            {
                var name="SunEyeVision-export-"+Guid.NewGuid().ToString("N");BitmapSource copied;
                using(var mapping=System.IO.MemoryMappedFiles.MemoryMappedFile.CreateNew(name,128))
                using(var view=mapping.CreateViewAccessor())
                {
                    var pixels=Enumerable.Range(0,2*channels).Select(i=>(byte)(i+10)).ToArray();var header=new byte[16];BitConverter.GetBytes(pixels.Length).CopyTo(header,0);BitConverter.GetBytes(918273).CopyTo(header,4);BitConverter.GetBytes((short)32).CopyTo(header,14);view.WriteArray(0,header,0,16);view.WriteArray(32,pixels,0,pixels.Length);
                    var image=new ExportMappedImage{Width=2,Height=1,PixelFormat=channels==1?"Gray8":channels==3?"Bgr24":"Bgra32",MemoryAddress=name,Buffer=null!};
                    copied=(BitmapSource)ExportCall("PrepareRaw",image);var actual=new byte[pixels.Length];copied.CopyPixels(actual,pixels.Length,0);Require(actual.SequenceEqual(pixels)&&image.Buffer is null,"32位头长度/通道/高DWORD或源回写错误");
                    foreach(var offset in new short[]{-1,0,15,32767}){BitConverter.GetBytes(offset).CopyTo(header,14);view.WriteArray(0,header,0,16);try{ExportCall("PrepareRaw",image);throw new Exception("非法映射偏移未拒绝");}catch(System.IO.InvalidDataException){}}
                    BitConverter.GetBytes((short)32).CopyTo(header,14);header[8]=1;view.WriteArray(0,header,0,16);try{ExportCall("PrepareRaw",image);throw new Exception("忙映射未拒绝");}catch(System.IO.InvalidDataException){}
                }
                Require(copied.IsFrozen,"映射拷贝未冻结");try{using var leaked=System.IO.MemoryMappedFiles.MemoryMappedFile.OpenExisting(name);throw new Exception("导出泄漏共享映射句柄");}catch(FileNotFoundException){}
            }
        });
        Case("图像导出 默认JPEG与BMP原菜单及透明保护和写入失败清理",()=>{
            using var resources=new FrozenOutputResources();using var viewport=new NativeResultViewport();var image=resources.CreateGray8(16,8,Enumerable.Repeat((byte)99,128).ToArray());Require(viewport.BindImage(image,()=>true),"未绑定");var view=(ImageViewBox)viewport.View;
            foreach(var extension in new[]{".bmp",".jpg",".jpeg"}){Require(Menu(view,false,"codec"+extension)==null,"原菜单编码失败");var pixels=ExportPixels(ExportDecode(Path.Combine(folder,"codec"+extension)));Require(pixels.Where((v,i)=>i%4!=3).All(v=>Math.Abs(v-99)<=1),"默认编码像素异常");}
            var alpha=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{3,7,11,128},4);alpha.Freeze();foreach(var ext in new[]{".jpg",".bmp"})ExportReject(()=>ExportCall("SavePrepared",Path.Combine(folder,"alpha"+ext),alpha),"透明");
            var opaque=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{3,7,11,255},4);opaque.Freeze();ExportCall("SavePrepared",Path.Combine(folder,"opaque.bmp"),opaque);Require(ExportPixels(ExportDecode(Path.Combine(folder,"opaque.bmp"))).SequenceEqual(new byte[]{3,7,11,255}),"全不透明BGRA显式转BGR失败");
            var target=Path.Combine(folder,"locked.png");File.WriteAllText(target,"keep");using(var locked=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.None)){var failure=Menu(view,false,"locked.png");Require(failure is IOException or UnauthorizedAccessException,"写入失败未报告文件异常："+failure);}
            Require(File.ReadAllText(target)=="keep"&&!Directory.EnumerateFiles(folder,"*.tmp").Any(),"失败损坏旧文件或残留临时文件");
        });
        Case("图像导出 模态选择内换帧清理关闭不借reader或可变模型",()=>{
            using var resources=new FrozenOutputResources();using var viewport=new NativeResultViewport();var a=resources.CreateGray8(4,3,Enumerable.Repeat((byte)17,12).ToArray());var b=resources.CreateGray8(2,1,new byte[]{80,81});Require(viewport.BindImage(a,()=>true),"未绑定A");var view=(ImageViewBox)viewport.View;
            Require(Menu(view,false,"modal-retired.png",()=>{Require(viewport.BindImage(b,()=>true),"未绑定B");viewport.Clear();viewport.Dispose();resources.Dispose();})==null,"模态前快照仍借已退役资源");Require(ExportPixels(ExportDecode(Path.Combine(folder,"modal-retired.png"))).Where((v,i)=>i%4!=3).All(v=>v==17),"关闭后保存了新帧或失效资源");
        });
        Case("图像导出 原右键只过滤顶层可见ShapeList不加入ROI附件",()=>{
            var view=new ImageViewBox{ImageDataSource=new ExportMappedImage{Width=64,Height=64,Buffer=new byte[4096]}};
            view.ShapeList=new ObservableCollection<object>{new ExportPolygon{IsVisible=false}};
            Require(Menu(view,true,"hidden-top.png")==null&&!ExportInk(ExportDecode(Path.Combine(folder,"hidden-top.png"))),"顶层不可见仍导出");
            view.ShapeList=new ObservableCollection<object>{Tuple.Create("group",(object)new List<object>{new ExportPolygon{IsVisible=false}})};
            Require(Menu(view,true,"visible-tuple.png")==null&&ExportInk(ExportDecode(Path.Combine(folder,"visible-tuple.png"))),"Tuple内部错误增加可见性过滤");
            view.ShapeList=new ObservableCollection<object>();view.ROIList=new ObservableCollection<object>{new ExportPolygon()};
            Require(Menu(view,true,"no-roi.png")==null&&!ExportInk(ExportDecode(Path.Combine(folder,"no-roi.png"))),"ROI编辑附件错误混入导出");
            view.ImageDataSource=null;view.ShapeList=null;view.ROIList=null;
        });
        VerifyExportShapeFamily(folder);
        var runtimes=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name=="OpenCvSharp").Select(a=>new {path=a.Location,version=a.GetName().Version?.ToString(),sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a.Location)))}).ToArray();
        var natives=System.Diagnostics.Process.GetCurrentProcess().Modules.Cast<System.Diagnostics.ProcessModule>().Where(m=>m.ModuleName.Contains("OpenCvSharpExtern",StringComparison.OrdinalIgnoreCase)).Select(m=>new {path=m.FileName,version=m.FileVersionInfo.FileVersion,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(m.FileName)))}).ToArray();
        Require(runtimes.Length==1&&natives.Length==1,"未记录实际OpenCV托管与native载荷");File.WriteAllText(Path.Combine(folder,"opencv-runtime.json"),System.Text.Json.JsonSerializer.Serialize(new {managed=runtimes,native=natives,encoding="right-click ImEncode without explicit quality",difference="4.13.0.20260222 host package, not vendor native pixel/codec equivalence"}));
    }
    static void VerifyExportShapeFamily(string folder)
    {
        Case("图像导出 原分派Tuple内保序非零opacity实色fill及未知类型忽略",()=>{
            var polygon=new ExportPolygon{Fill="Lime",Stroke="Red",Opacity=.1,IsVisible=false};var tuple=Tuple.Create("batch",(object)new List<object>{polygon,new object()});var bitmap=ExportDraw(tuple);var bytes=ExportPixels(bitmap);var offset=(20*64+20)*4;Require(bytes[offset]==0&&bytes[offset+1]==255&&bytes[offset+2]==0,"非零opacity被alpha混合或Tuple内部被visibility误过滤");
            Require(!ExportInk(ExportDraw(new ExportPolygon{Opacity=0},new object())),"opacity0或未知类型不按原排除");
            var bgraBytes=Enumerable.Range(0,4096).SelectMany(_=>new byte[]{11,22,33,128}).ToArray();var bgra=BitmapSource.Create(64,64,96,96,PixelFormats.Bgra32,null,bgraBytes,256);bgra.Freeze();
            var painted=(BitmapSource)ExportCall("PrepareRenderedBitmap",bgra,new object[]{new ExportPoint{Opacity=.1}},new System.Windows.Controls.Grid());var paintedBytes=ExportPixels(painted);
            Require(paintedBytes.Take(4).SequenceEqual(new byte[]{11,22,33,128})&&paintedBytes.Skip((20*64+20)*4).Take(4).SequenceEqual(new byte[]{0,255,0,255}),"BGRA未绘像素变色或实色图形清了alpha");
        });
        Case("图像导出 全批圆椭圆正负扇环旋转及退化矩形线贯穿点十字箭头折线",()=>{
            object[] shapes={new ExportCircle(),new ExportEllipse(),new ExportArc{AngleRange=100},new ExportArc{AngleRange=-100},new ExportArc{AngleRange=360},new ExportRect{Angle=30},new ExportRect{Angle=-30},new ExportRect{Width=0},new ExportRect{Width=0,Height=0},new ExportLine(),new ExportLine{IsThroughLine=true},new ExportPoint(),new ExportCross(),new ExportArrow(),new ExportPolyline()};
            foreach(var shape in shapes)Require(ExportInk(ExportDraw(shape)),"原几何未绘制 "+shape.GetType().Name);
            var cross=ExportPixels(ExportDraw(new ExportCross()));Require(cross[(20*64)*4+1]>0&&cross[(63*64+20)*4+1]>0,"cross未贯穿图像");var point=ExportPixels(ExportDraw(new ExportPoint()));Require(point[(20*64)*4+1]==0,"point被错误画成贯穿cross");
            foreach(var angle in new[]{30d,-30d})
            {
                var rotated=ExportPixels(ExportDraw(new ExportRect{Width=28,Height=6,Angle=angle}));double covariance=0;
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)if(rotated[(y*64+x)*4+1]>0)covariance+=(x-20)*(y-20);
                Require(Math.Sign(covariance)==Math.Sign(angle),"正负角矩形主方向相反或角度丢失");
            }
            var positive=ExportPixels(ExportDraw(new ExportArc{StartAngle=0,AngleRange=90}));var negative=ExportPixels(ExportDraw(new ExportArc{StartAngle=0,AngleRange=-90}));
            Require(positive[(36*64+25)*4+1]>0&&positive[(14*64+25)*4+1]==0&&negative[(14*64+25)*4+1]>0&&negative[(36*64+25)*4+1]==0&&positive[(25*64+25)*4+1]==0&&negative[(25*64+25)*4+1]==0,"正负90度扇环方向或中心孔错误");
            Require(ExportPixels(ExportDraw(new ExportLine{StartPoint=new(10,20),EndPoint=new(30,20),StrokeThickness=1.5})).SequenceEqual(ExportPixels(ExportDraw(new ExportLine{StartPoint=new(10,20),EndPoint=new(30,20),StrokeThickness=2}))),"小数线宽未遵循原最少2整数规则");
            Require(ExportPixels(ExportDraw(new ExportLine{StartPoint=new(-5.9,5.9),EndPoint=new(10.9,20.9)})).SequenceEqual(ExportPixels(ExportDraw(new ExportLine{StartPoint=new(-5,5),EndPoint=new(10,20)}))),"原Point(double)正负坐标未向零截断");
            Require(ExportPixels(ExportDraw(new ExportEllipse{MajorRadius=10.9,MinorRadius=5.9})).SequenceEqual(ExportPixels(ExportDraw(new ExportEllipse{MajorRadius=10,MinorRadius=5}))),"原Size(double)未向零截断");
            foreach(var pair in new[]{(0.9,0d),(20.9,20d)})Require(ExportPixels(ExportDraw(new ExportRect{Width=pair.Item1,Height=8.9,Angle=30})).SequenceEqual(ExportPixels(ExportDraw(new ExportRect{Width=pair.Item2,Height=8,Angle=30}))),"原矩形Size2f(double)宽高未先截整");
            Require(!ExportPixels(ExportDraw(new ExportRect{CenterPoint=new(20.9,20.9),Width=20,Height=8,Angle=30})).SequenceEqual(ExportPixels(ExportDraw(new ExportRect{CenterPoint=new(20,20),Width=20,Height=8,Angle=30}))),"矩形中心被误截整而不是float保小数");
            Require(!ExportInk(ExportDraw(new ExportLine{StartPoint=new(5,5),EndPoint=new(5,5),IsThroughLine=true})),"退化贯穿线应按原忽略");
            try{ExportDraw(new ExportRect{Width=double.NaN});throw new Exception("未拒非法尺寸");}catch(ArgumentException){}
        });
        Case("图像导出 IMultiPoint父opacity0子项16字节布局ScoreIndex多点与残尾",()=>{
            var bytes=new List<byte>();foreach(var item in new[]{(10f,10f,0f,123),(20f,20f,1f,456),(30f,30f,2f,789)}){bytes.AddRange(BitConverter.GetBytes(item.Item1));bytes.AddRange(BitConverter.GetBytes(item.Item2));bytes.AddRange(BitConverter.GetBytes(item.Item3));bytes.AddRange(BitConverter.GetBytes(item.Item4));}bytes.AddRange(new byte[]{7,8,9});
            var multi=new ExportMulti{Opacity=0};multi.OutlinePoints.Add(new ExportOutline{OutlineData=bytes.ToArray()});multi.MicroPoints.Add(new ExportMicro{Point=new(40,40)});multi.Rectangles.Add(new ExportRect{CenterPoint=new(50,50),Width=4,Height=4,Opacity=0});var pixels=ExportPixels(ExportDraw(multi));
            byte[] At(int x,int y)=>pixels.Skip((y*64+x)*4).Take(3).ToArray();Require(At(10,9).SequenceEqual(new byte[]{0,255,0})&&At(20,19).SequenceEqual(new byte[]{0,255,255})&&At(30,29).SequenceEqual(new byte[]{0,0,255}),"结构步长/Score颜色/Index误消费");Require(At(40,39)[1]==255&&At(48,48)[1]==255,"微点或子矩形被父/子opacity错误过滤");
        });
        Case("图像导出 文字同一分派边界内部opacity空串错误越界等于边界坐标角度保护",()=>{
            ExportMultiText Text(string value,double x=20,double y=20,double childOpacity=1)=>new(){Texts=new(){new ExportText{Content=value,Position=new(x,y),Opacity=childOpacity}}};
            foreach(var shape in new object[]{Text("hello"),Text("hello",64,64),Text("hello",20,20,0),new ExportCoordinate{VectorX=new(1,0)},new ExportAngle{Intersection=new(1000,1000)}})ExportReject(()=>ExportDraw(shape),"PutTextZh");
            foreach(var shape in new object[]{Text(""),Text("模块执行异常"),Text("skip",65,20),new ExportMultiText{Opacity=0,Texts=new(){new ExportText{Content="hidden"}}},new ExportCoordinate(),new ExportCoordinate{VectorX=new(1e-60,0)}})Require(!ExportInk(ExportDraw(shape)),"原文字排除/零轴被误拒或误绘");
        });
    }
    class ExportBase:C.IShape {public string Stroke{get;set;}="Lime";public string Fill{get;set;}="";public double StrokeThickness{get;set;}=2;public double Opacity{get;set;}=1;public string ToolTip=>"export fixture";}
    class ExportCircle:ExportBase,C.ICircle{public Point CenterPoint=>new(20,20);public double Radius=>8;}
    class ExportEllipse:ExportBase,C.IEllipse{public Point CenterPoint=>new(20,20);public double MajorRadius{get;set;}=10;public double MinorRadius{get;set;}=5;public double Angle=>30;}
    class ExportArc:ExportBase,C.IAnnularSector{public Point CenterPoint=>new(25,25);public double OuterRadius=>14;public double InnerRadius=>8;public double StartAngle{get;set;}=30;public double AngleRange{get;set;}=100;}
    class ExportRect:ExportBase,C.IRectangle{public Point CenterPoint{get;set;}=new(20,20);public double Width{get;set;}=12;public double Height{get;set;}=10;public double Angle{get;set;}=0;public double SkewAngleX=>0;public double SkewAngleY=>0;}
    class ExportLine:ExportBase,C.ILine{public Point StartPoint{get;set;}=new(10,10);public Point EndPoint{get;set;}=new(30,30);public bool IsThroughLine{get;set;}}
    class ExportPoint:ExportBase,C.IPoint{public Point Point=>new(20,20);}
    class ExportMicro:ExportBase,C.IMicroPoint{public Point Point{get;set;}=new(20,20);}
    class ExportCross:ExportBase,C.ICross{public Point CenterPoint=>new(20,20);}
    class ExportArrow:ExportBase,C.IArrowPath{public List<Point> Points{get;}=new(){new(10,10),new(20,20),new(30,10)};}
    class ExportPolygon:ExportBase,C.IPolygon,C.IIsVisible{public List<Point> Points{get;}=new(){new(10,10),new(30,10),new(30,30),new(10,30)};public bool IsVisible{get;set;}=true;}
    class ExportPolyline:ExportBase,C.IPolyline{public List<Point> PolylinePointList{get;}=new(){new(10,10),new(20,20),new(30,10)};}
    class ExportMulti:ExportBase,C.IMultiPoint{public ObservableCollection<C.IOutlinePoint> OutlinePoints{get;}=new();public ObservableCollection<C.IMicroPoint> MicroPoints{get;}=new();public ObservableCollection<C.IRectangle> Rectangles{get;}=new();}
    class ExportOutline:ExportBase,C.IOutlinePoint{public byte[] OutlineData{get;set;}=Array.Empty<byte>();public string NgColor=>"Blue";}
    class ExportText:ExportBase,C.IText{public string Content{get;set;}="";public Point Position{get;set;}=new(20,20);public int FontSize=>12;}
    class ExportMultiText:ExportBase,C.IMultiText{public List<C.IText> Texts{get;set;}=new();}
    class ExportCoordinate:ExportBase,C.ICoordinateShape{public Point CenterPoint=>new(20,20);public Point VectorX{get;set;}=new();public Point VectorY{get;set;}=new();}
    class ExportAngle:ExportBase,C.IAngleShape{public Point InitialLineStartPoint=>new(0,0);public Point InitialLineEndPoint=>new(1,0);public Point TerminalLineStartPoint=>new(0,0);public Point TerminalLineEndPoint=>new(1,1);public Point Intersection{get;set;}=new();public double Angle=>45;public C.IAngleShape GetAngleShape(Point a,Point b,Point c,Point d,Point e,double angle)=>this;}
    class ExportMappedImage:C.IImageData
    {public int Width{get;set;}=2;public int Height{get;set;}=1;public byte[] Buffer{get;set;}=null!;public string MemoryAddress{get;set;}="";public string PixelFormat{get;set;}="Gray8";public void SetPixelFormat(string value)=>PixelFormat=value;}
    class ExportDepthImage:C.IImageData,C.IRangeImageData
    {public int Width=>2;public int Height=>1;public byte[] Buffer{get;set;}=new byte[]{0,0,255,127};public string MemoryAddress=>"";public string PixelFormat=>"Gray16";public void SetPixelFormat(string f){}public byte[] RangeBuffer{get=>Buffer;set=>Buffer=value;}public uint DetailedPixelFormat{get;set;}=17825976u;public int Xoffset{get;set;}public int Yoffset{get;set;}public int Zoffset{get;set;}public int Xscale{get;set;}public int Yscale{get;set;}public int Zscale{get;set;}}
}
