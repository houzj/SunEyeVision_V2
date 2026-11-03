using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using Numbers=SunEyeVision.Plugin.Wpf.Native.Numeric.Arranged;
internal static class C06NumericRangeCases
{
 static object F(object o,string n)=>o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(o)!;
 static T P<T>(object o,string n)=>(T)o.GetType().GetProperty(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(o)!;
 static object? Call(object o,string n,params object?[] args){try{return o.GetType().GetMethods(BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance).Single(m=>m.Name==n&&m.GetParameters().Length==args.Length).Invoke(o,args);}catch(TargetInvocationException error){throw error.InnerException!;}}
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static StackPanel host=null!;static Window window=null!;static int passed;
 static void Pump(){var f=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>f.Continue=false));Dispatcher.PushFrame(f);}
 static void Case(string name,Action action){host.Children.Clear();action();passed++;Console.WriteLine("PASS "+name);}
 static void Show(FrameworkElement view){host.Children.Clear();host.Children.Add(view);window.UpdateLayout();Pump();}
 sealed class Fixture:IDisposable
 {
  internal readonly DeclaredSettingsView View;internal readonly DeclaredFieldAccess Low,High;internal readonly object Range;internal readonly object[] States;internal readonly Numbers.SliderArrangeBase Native;
  internal Fixture(bool integer,ParameterSetting low,ParameterSetting high,ReferenceFieldOptions? refs=null,Func<FieldGroupCandidate,string?>? validate=null,double min=0,double max=100,double increment=1,int digits=2,bool reverse=false,bool disabledBeforeLoad=false)
  {
   var type=integer?(TypeRef)DataType.Integer:DataType.Number;var l=new Port("lo",type,Storage:integer?NumericStorage.Int32:null,EditModes:FieldEditModes.Constant|FieldEditModes.Reference);var h=l with{Id="hi"};
   var lp=new FieldPresentation("lo","下限","G",0,FieldEditorKind.Scalar){Layout=new("A","G",Minimum:min,Maximum:max,Increment:increment,DecimalPlaces:digits,PairWith:"hi")};var hp=new FieldPresentation("hi","上限","G",1,FieldEditorKind.Scalar){Layout=lp.Layout! with{PairWith=null}};
   var ports=reverse?new[]{h,l}:new[]{l,h};var fields=reverse?new[]{hp,lp}:new[]{lp,hp};var schema=new ToolSchema(ports,Array.Empty<Port>(),new ToolPresentation(fields));
   var draft=new FieldSettingsDraft(schema,new FieldSettings(ports,new Dictionary<string,ParameterSetting>{{"lo",low},{"hi",high}}));
   View=new(draft,new(ReferenceOptions:refs??ReferenceFieldOptions.Empty,ValidateComposition:validate));if(disabledBeforeLoad)View.IsEnabled=false;Show(View);
   var root=(DeclaredFieldsView)View.Content;Low=root.Fields.Single(f=>f.FieldId=="lo");High=root.Fields.Single(f=>f.FieldId=="hi");Range=Low.View;States=(object[])F(Range,"_states");Native=P<Numbers.SliderArrangeBase>(Range,"NativeView");
  }
  internal object Model(int i)=>P<object>(States[i],"NumericModel");internal TextBox Text(int i)=>(TextBox)(i==0?Low:High).InputElement!;
  public void Dispose()=>View.Dispose();
 }
 static ReferenceSetting Ref(bool integer=false)=>new(new OutputReference("producer","value",integer?(TypeRef)DataType.Integer:DataType.Number));
 static NumericModelReadResult Read(Fixture f,string id)=>f.View.ReadNumericModels(NumericModelReadScope.Field,id).Single();
 static void Flush(Fixture f)
 {
  var leaves=((Array)F(f.Range,"_leaves")).Cast<Numbers.NumericControl>().ToArray();
  foreach(var leaf in leaves){var text=(TextBox)leaf.FindName("ValueText");var bindings=new[]{leaf.GetBindingExpression(Numbers.NumericControl.TextProperty)!,text.GetBindingExpression(TextBox.TextProperty)!};var dirty=bindings.Where(b=>b.IsDirty).ToArray();Check(dirty.All(b=>b.ValidateWithoutUpdate()),"隐藏槽dirty校验失败");foreach(var b in dirty)b.UpdateSource();Check(!Validation.GetHasError(leaf)&&!Validation.GetHasError(text),"隐藏槽遗留WPF错误");}
 }
 static int Run()
 {
  new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};window=new Window{Content=host=new StackPanel(),Width=800,Height=850,ShowActivated=false,ShowInTaskbar=false};window.Resources.MergedDictionaries.Add(EditorResources.Create());window.Show();Pump();
  try
  {
   Case("原typed0初始化位与浮点同舍入通知",()=>{
    using(var f=new Fixture(true,new ConstantSetting(0L),Ref(true))){Check(f.Native.IsNeedCheckExtremumByXML&&f.Native.SliderType==Numbers.SliderArrangeType.between,"整数原模板属性错误");Check(P<bool>(f.Model(0),"HasValue")&&!P<bool>(f.Model(0),"IsInitValue"),"已知0被当原已初始化");Check(!P<bool>(f.Model(1),"HasValue")&&!P<bool>(f.Model(1),"IsInitValue")&&P<object>(f.Model(1),"CurValue") is 0L,"未知typed0被当已读");}
    using(var direct=new Fixture(false,new ConstantSetting(0d),new ConstantSetting(10d))){Check(!P<bool>(direct.Model(0),"IsInitValue"),"初0标志错误");Check(direct.Low.TrySetConstant(.004)&&P<bool>(direct.Model(0),"IsInitValue")&&P<double>(direct.Model(0),"CurValue")==0,"程序原raw在舍入前被丢弃");}
    var armed=false;var reads=0;using var g=new Fixture(false,Ref(),Ref(),ReferenceFieldOptions.Empty with{ReadCurrentValue=p=>{reads++;return armed?new(p.Id=="lo"?.004:1d):null;}});Check(!g.Native.IsNeedCheckExtremumByXML&&g.Native.SliderType==Numbers.SliderArrangeType.between&&((Numbers.SliderArrangeFloat)g.Native).NeedSlider,"浮点原模板默认属性错误");armed=true;reads=0;var before=P<long>(g.Model(0),"Version");Check(Read(g,"lo").Status==NumericModelReadStatus.Updated&&P<double>(g.Model(0),"CurValue")==0&&P<bool>(g.Model(0),"IsInitValue")&&P<long>(g.Model(0),"Version")>before,"舍入同0丢原setter初始化/通知");Check(reads==2&&!P<bool>(g.Model(1),"HasValue"),"临时右读安装模型");
   });
   Case("真实TwoWay先改本地setter再单端正式接纳",()=>{
    using var f=new Fixture(true,new ConstantSetting(1L),new ConstantSetting(29L),min:1,max:30,increment:4);
    f.Native.StartValue=8;f.Native.GetBindingExpression(Numbers.SliderArrangeBase.StartValueProperty)!.UpdateSource();
    Check(P<long>(f.Model(0),"CurValue")==5&&f.Low.Setting==new ConstantSetting(1L),"未按原setter先改本地或提前写正式值");Call(f.Native,"DoStartValueChange");Pump();Check(f.Low.Setting==new ConstantSetting(5L)&&f.High.Setting==new ConstantSetting(29L),"原端点事件未提交单端格点");
   });
   Case("无编辑原事件保持普通模型与正式值分离",()=>{
    var armed=false;double current=2;using var f=new Fixture(false,new ConstantSetting(1d),new ConstantSetting(10d),ReferenceFieldOptions.Empty with{ReadCurrentValue=p=>armed?new(p.Id=="lo"?current:10d):null});armed=true;Check(Read(f,"lo").Status==NumericModelReadStatus.Updated&&!f.Low.HasPendingEdits,"普通模型投影误作用户raw");var revision=f.View.Draft!.Revision;Call(f.Native,"DoStartValueChange");Pump();Check(f.Low.Setting==new ConstantSetting(1d)&&P<double>(f.Model(0),"CurValue")==2&&f.View.Draft.Revision==revision,"无编辑原事件把正式值投回模型或写回读值");
    f.Text(0).Text="keep-bad-raw";Pump();current=3;Check(Read(f,"lo").Status==NumericModelReadStatus.Updated&&f.Text(0).Text=="keep-bad-raw"&&f.Low.HasPendingEdits&&f.Low.HasInvalidText&&P<double>(f.Model(0),"CurValue")==3,"TwoWay普通投影覆盖已有坏raw");
   });
   Case("原左右Check非对称与临时parse和异常分支",()=>{
    foreach(var peer in new object?[]{10d,"bad",null,new InvalidOperationException("read-failed")})
    {
     var armed=false;var order=new List<string>();using var f=new Fixture(false,Ref(),Ref(),ReferenceFieldOptions.Empty with{ReadCurrentValue=p=>{order.Add(p.Id);if(!armed)return null;if(p.Id=="lo")return new(5d);if(peer is Exception e)throw e;return peer is null?null:new(peer);}});armed=true;order.Clear();var version=P<long>(f.Model(1),"Version");var result=Read(f,"lo");Check(result.Status==(peer is string?NumericModelReadStatus.Failed:NumericModelReadStatus.Updated),"左原临时比较分支不符");Check(order.SequenceEqual(new[]{"lo","hi"})&&!P<bool>(f.Model(1),"HasValue")&&P<long>(f.Model(1),"Version")==version,"临时比较被当右Field回读");
    }
    var active=false;double l=5,r=10;var calls=new List<string>();using var g=new Fixture(false,Ref(),Ref(),ReferenceFieldOptions.Empty with{ReadCurrentValue=p=>{calls.Add(p.Id);return active?new(p.Id=="lo"?l:r):null;}});active=true;Read(g,"lo");Read(g,"hi");calls.Clear();Read(g,"lo");Check(calls.SequenceEqual(new[]{"lo"}),"右已init仍普通回读");r=2;Check(Read(g,"hi").Status==NumericModelReadStatus.Failed&&P<double>(g.Model(1),"CurValue")==10,"右Check未使用左CurValue或失败改模型");
   });
   Case("Field页All原左后右且无正式Dirty",()=>{
    var armed=false;var calls=new List<string>();using var f=new Fixture(false,Ref(),Ref(),ReferenceFieldOptions.Empty with{ReadCurrentValue=p=>{calls.Add(p.Id);return armed?new(p.Id=="lo"?1d:10d):null;}},reverse:true);armed=true;calls.Clear();var revision=f.View.Draft!.Revision;var result=f.View.ReadNumericModels(NumericModelReadScope.All);Check(result.Select(r=>r.FieldId).SequenceEqual(new[]{"lo","hi"})&&calls.SequenceEqual(new[]{"lo","hi","hi"}),"All未按ValueBettween左后右含临时依赖");Check(f.View.Draft.Revision==revision&&f.Low.Setting is ReferenceSetting&&f.High.Setting is ReferenceSetting,"回读写正式值");calls.Clear();Check(f.View.ReadNumericModels(NumericModelReadScope.CurrentPage).Count==2&&calls.SequenceEqual(new[]{"lo","hi"}),"页scope漏两端或重复初始化");
   });
   Case("原raw绝对范围与完整组一次提案拒绝回滚",()=>{
    var reject=false;using var f=new Fixture(false,new ConstantSetting(1d),new ConstantSetting(10d),validate:_=>reject?"group-rejected":null,increment:.01);
    f.Text(0).Text="12";f.Text(1).Text="20";Pump();var before=f.View.Draft!.Revision;Check((bool)Call(f.Range,"TryCommitPending",false)!&&f.View.Draft.Revision==before+1,"完整两端未一次接纳");Check(f.Low.Setting==new ConstantSetting(12d)&&f.High.Setting==new ConstantSetting(20d),"组结果不完整");
    reject=true;f.Text(0).Text="13";f.Text(1).Text="30";Pump();before=f.View.Draft.Revision;Check(!(bool)Call(f.Range,"TryCommitPending",false)!&&f.View.Draft.Revision==before&&f.Low.Setting==new ConstantSetting(12d)&&f.High.Setting==new ConstantSetting(20d),"拒绝先写正式或丢原子性");Check(f.Text(0).Text=="13"&&f.Text(1).Text=="30","拒绝丢pending raw");
    f.Text(0).Text="100.004";Check(!(bool)Call(f.Range,"TryCommitPending",false)!&&f.Text(0).Text=="100.004","raw先舍入掩盖绝对越界");
   });
   Case("原生popup两未知槽与无值解绑坏raw",()=>{
    foreach(var knownLeft in new[]{false,true})
    {
     var armed=false;var ordinary=0;using var life=new Fixture(true,Ref(true),Ref(true),ReferenceFieldOptions.Empty with{ReadCurrentValue=p=>{ordinary++;return armed&&p.Id=="lo"?new(15L):null;}},min:10,max:100);
     if(knownLeft){armed=true;Check(Read(life,"lo").Status==NumericModelReadStatus.Updated,"known引用准备失败");Pump();Check(P<long>(life.Model(0),"CurValue")==15,"普通投影原Slider队列改写known模型");}ordinary=0;
     var popup=(ComboBox)life.Native.FindName("CBox");var versions=life.States.Select(state=>P<long>(P<object>(state,"NumericModel"),"Version")).ToArray();
     void Verify(string state)
     {
      Flush(life);Check(ordinary==0&&!P<bool>(life.Model(1),"HasValue")&&life.Low.Setting is ReferenceSetting&&life.High.Setting is ReferenceSetting,state+"清理改回读/unknown/正式引用");
      Check(Enumerable.Range(0,2).All(i=>P<long>(life.Model(i),"Version")==versions[i]),state+"owned清理改变模型版本");
      if(knownLeft)Check(P<long>(life.Model(0),"CurValue")==15,state+"known模型被旧Slider改0");else Check(!P<bool>(life.Model(0),"HasValue"),state+"unknown被安装");
     }
     foreach(var state in new[]{"disable","hide","program-close","unload"})
     {
      popup.IsDropDownOpen=true;Pump();Check(popup.IsDropDownOpen,state+"原生popup未实际打开");
      if(state=="disable")life.View.IsEnabled=false;else if(state=="hide")life.View.Visibility=Visibility.Collapsed;else if(state=="program-close")Call(life.Range,"ClosePopups");else host.Children.Clear();
      Pump();Check(!popup.IsDropDownOpen,state+"原生popup未关闭");Verify(state);
      if(state=="disable")life.View.IsEnabled=true;else if(state=="hide")life.View.Visibility=Visibility.Visible;else if(state=="unload")host.Children.Add(life.View);
      window.UpdateLayout();Pump();Verify(state+"-resume");
     }
    }

    var reads=0;using var f=new Fixture(true,Ref(true),Ref(true),ReferenceFieldOptions.Empty with{ReadCurrentValue=_=>{reads++;return null;}},min:10,max:100);reads=0;var box=(ComboBox)f.Native.FindName("CBox");Check(box.IsVisible&&box.IsEnabled,"原生CBox不可打开");box.IsDropDownOpen=true;Pump();Check(box.IsDropDownOpen,"未真实开popup");box.IsDropDownOpen=false;Pump();Flush(f);Check(!P<bool>(f.Model(0),"HasValue")&&!P<bool>(f.Model(1),"HasValue")&&reads==0,"未知槽物理值被安装或回读");
    f.Native.IsSubscribeLeft=false;Call(f.Native,"DoLeftTypeChange");Pump();Check(f.Low.Setting is ReferenceSetting&&f.Text(0).Text==string.Empty&&f.Low.HasPendingEdits&&f.Low.HasInvalidText&&reads==1,"无值解绑未保护正式引用");f.Text(0).Text="bad-raw";Call(f.Range,"ReplayAccepted");Pump();Check(f.Text(0).Text=="bad-raw"&&f.Low.HasInvalidText,"重投影清实际坏raw");
   });
   Case("原Dispatcher当前对端时序与owner退役",()=>{
    using(var disabled=new Fixture(false,new ConstantSetting(3d),new ConstantSetting(8d),disabledBeforeLoad:true))
    {
     Call(disabled.Native,"SL_Bat1_ValueChanged",disabled.Native,new RoutedPropertyChangedEventArgs<double>(3,12));Pump();Check(disabled.Native.StartValue==3,"disabled-before-load激活了旧队列");
     disabled.View.Visibility=Visibility.Collapsed;Pump();disabled.View.Visibility=Visibility.Visible;Pump();Call(disabled.Native,"SL_Bat1_ValueChanged",disabled.Native,new RoutedPropertyChangedEventArgs<double>(3,12));Pump();Check(disabled.Native.StartValue==3,"disabled-hide-show激活了队列");
     disabled.View.IsEnabled=true;Pump();Call(disabled.Native,"SL_Bat1_ValueChanged",disabled.Native,new RoutedPropertyChangedEventArgs<double>(3,12));Pump();Check(disabled.Native.StartValue==8,"重新enable后丢原有效事件");
    }
    using var f=new Fixture(false,new ConstantSetting(3d),new ConstantSetting(8d));
    Call(f.Native,"SL_Bat1_ValueChanged",f.Native,new RoutedPropertyChangedEventArgs<double>(3,12));Pump();Check(f.Native.StartValue==8,"非原proposed相等门禁丢原回调");
    Call(f.Native,"SL_Bat1_ValueChanged",f.Native,new RoutedPropertyChangedEventArgs<double>(8,20));((FrameworkElement)f.Range).IsEnabled=false;Pump();Check(f.Native.StartValue==8,"禁用后旧排队回调仍改本地模型");((FrameworkElement)f.Range).IsEnabled=true;Pump();
    Call(f.Native,"SL_Bat1_ValueChanged",f.Native,new RoutedPropertyChangedEventArgs<double>(8,20));Call(f.Native,"RetireBorrowedInteractions");Pump();Check(f.Native.StartValue==8,"退役回调仍写DP");var old=f.Native;f.View.Dispose();var retiredValue=old.StartValue;Call(old,"SL_Bat1_ValueChanged",old,new RoutedPropertyChangedEventArgs<double>(8,30));Pump();Check(old.StartValue==retiredValue,"Dispose复活原排队事件");
   });
   Case("临时普通读重入换根与显式回读退役",()=>{
    Fixture? target=null;var armed=false;using var f=target=new Fixture(false,Ref(),Ref(),ReferenceFieldOptions.Empty with{ReadCurrentValue=p=>{if(armed&&p.Id=="hi"){target!.View.Dispose();return new(10d);}return armed?new(5d):null;}});armed=true;Check(Read(f,"lo").Status==NumericModelReadStatus.Retired,"临时右读Dispose后继续fallback或安装旧模型");
   });
   Console.WriteLine($"C06公共{passed}组通过；原双端整链候选，不替代宿主/正式/人工。");return 0;
  }
  finally{window.Close();Application.Current.Shutdown();}
 }
 static IntPtr desktop;
 [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,int flags,uint access,IntPtr security);
 [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]static extern bool SetThreadDesktop(IntPtr value);
 [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]static extern bool CloseDesktop(IntPtr value);
 [STAThread]public static int Main()
 {
  var result=1;var thread=new Thread(()=>{
   try{desktop=CreateDesktop("SunEyeVision-C06-"+Guid.NewGuid().ToString("N"),IntPtr.Zero,IntPtr.Zero,0,0x000F01FF,IntPtr.Zero);
    if(desktop==IntPtr.Zero||!SetThreadDesktop(desktop))throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(),"独立隐藏桌面不可用；不回退到用户桌面。");
    Console.WriteLine("C06隐藏桌面已建立，不切换用户桌面。");result=Run();}
   catch(Exception e){Console.Error.WriteLine(e);}});
  thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(desktop!=IntPtr.Zero)CloseDesktop(desktop);return result;
 }
}
