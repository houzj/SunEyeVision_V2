using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

internal static class C05NumericModelCases
{
 static object F(object o,string n)=>o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(o)!;
 static T P<T>(object o,string n)=>(T)o.GetType().GetProperty(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(o)!;
 static object? Call(object o,string n,params object?[] a){try{return o.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Single(m=>m.Name==n&&m.GetParameters().Length==a.Length).Invoke(o,a);}catch(TargetInvocationException e){throw e.InnerException!;}}
 static object State(DeclaredFieldEditor e)=>F(e,"_state");static object Model(DeclaredFieldEditor e)=>P<object>(State(e),"NumericModel");
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static StackPanel host=null!;static Window owner=null!;static int passed;
 static void Pump(){var f=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>f.Continue=false));Dispatcher.PushFrame(f);}
 static void Show(FrameworkElement v){host.Children.Clear();host.Children.Add(v);owner.UpdateLayout();Pump();}
 static void Case(string n,Action run){host.Children.Clear();run();passed++;Console.WriteLine("PASS "+n);}
 static Port Int(string id="n")=>new(id,DataType.Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant|FieldEditModes.Reference);
 static FieldPresentation Layout(Port p,string page="A",double min=-100,double max=100,double inc=1,int digits=2,string? pair=null)=>new(p.Id,p.Id,page,0,FieldEditorKind.Scalar){Layout=new(page,page,Minimum:min,Maximum:max,Increment:inc,DecimalPlaces:digits,PairWith:pair)};
 static DeclaredFieldEditor Editor(Port p,ParameterSetting value,FieldPresentation? layout=null,ReferenceFieldOptions? refs=null)=>new(p,p.Id,value,_=>{},refs,presentation:layout);
 static NumericModelReadResult Read(DeclaredFieldEditor e)=>(NumericModelReadResult)Call(State(e),"ReadNumericModel",(object?)null)!;
 static DeclaredSettingsView View((Port Port,FieldPresentation Presentation,ParameterSetting Value)[] fields,ReferenceFieldOptions refs,ToolSettingsViewOptions? options=null)
 {
  var schema=new ToolSchema(fields.Select(x=>x.Port),Array.Empty<Port>(),new ToolPresentation(fields.Select(x=>x.Presentation)));
  var draft=new FieldSettingsDraft(schema,new FieldSettings(schema.Inputs,fields.ToDictionary(x=>x.Port.Id,x=>x.Value)));
  return new(draft,(options??new()) with{ReferenceOptions=refs});
 }
 static int Run()
 {
  new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};owner=new Window{Content=host=new StackPanel(),Width=650,Height=850,Left=-20000,Top=-20000,ShowActivated=false,ShowInTaskbar=false};owner.Resources.MergedDictionaries.Add(EditorResources.Create());owner.Show();Pump();
  try
  {
   Case("整数Min相对格点与跨符号Int32安全适配",()=>{
    foreach(var (min,max,inc,input,expected) in new[]{(1d,30d,4d,8L,5L),(-10d,20d,3d,-2L,-4L),((double)int.MinValue,(double)int.MaxValue,1d,0L,0L),((double)int.MinValue,(double)int.MaxValue,3d,0L,-2L),((double)int.MinValue,(double)int.MaxValue,3d,(long)int.MaxValue,(long)int.MaxValue)})
    {var p=Int();using var e=Editor(p,new ConstantSetting((long)min),Layout(p,min:min,max:max,inc:inc));Show(e);Check(e.TrySetConstant(input),"合法格点未接纳");Check(((ConstantSetting)e.Setting).Value is long n&&n==expected,"整数格点错误");}
    foreach(var inc in new[]{0d,-1d,.5d,double.NaN}){var p=Int();using var e=Editor(p,new ConstantSetting(5L),Layout(p,min:1,max:20,inc:inc));Show(e);Check(!e.TrySetConstant(8L)&&((ConstantSetting)e.Setting).Value is 5L,"非法Inc写入原值");}
   });
   Case("double到偶数及0至9位模型投影不写正式值",()=>{
    foreach(var digits in Enumerable.Range(0,10))
    {var p=new Port("f",DataType.Number,EditModes:FieldEditModes.Constant|FieldEditModes.Reference);var reads=0;double value=1.25;var refs=ReferenceFieldOptions.Empty with{ReadCurrentValue=_=>{reads++;return new(value);}};using var e=Editor(p,new ConstantSetting(0d),Layout(p,inc:.1,digits:digits),refs);Show(e);var changes=0;e.Changed+=()=>changes++;var rev=P<long>(Model(e),"Version");Check(Read(e).Status==NumericModelReadStatus.Updated,"read未更新");Check(P<double>(Model(e),"Value")==Math.Round(value,digits,MidpointRounding.ToEven),"ToEven错误");Check(e.Setting==new ConstantSetting(0d)&&changes==0&&reads==1,"普通读污染正式状态");value=-1.25;Read(e);Check(P<double>(Model(e),"Value")==Math.Round(value,digits,MidpointRounding.ToEven),"负中点错误");}
    var stored=new Port("float32",DataType.Number,Storage:NumericStorage.Float32);using(var e=Editor(stored,new ConstantSetting(0d),Layout(stored,digits:2))){Show(e);Check(e.TrySetConstant(1.225),"Float32显式输入失败");Check(((ConstantSetting)e.Setting).Value is double actual&&actual==(double)(float)Math.Round(1.225,2,MidpointRounding.ToEven)&&P<double>(Model(e),"Value")==Math.Round(1.225,2,MidpointRounding.ToEven),"Float32存储提前舍入污染double模型");}
    var fp=new Port("same",DataType.Number);using var same=Editor(fp,new ConstantSetting(1.2),Layout(fp,digits:1),ReferenceFieldOptions.Empty with{ReadCurrentValue=_=>new(1.25)});Show(same);var before=P<long>(Model(same),"Version");Check(Read(same).Status==NumericModelReadStatus.Updated&&P<long>(Model(same),"Version")>before,"舍入同值丢通知版本");
   });
   Case("引用独立模型显式回读及历史getter不隐式读取",()=>{
    var p=Int();var identity=new ReferenceSetting(new OutputReference("producer","value",DataType.Integer));var reads=0;var refs=ReferenceFieldOptions.Empty with{ReadCurrentValue=_=>{reads++;return new(17L);}};using var e=Editor(p,identity,Layout(p),refs);Show(e);Check(!P<bool>(Model(e),"HasValue"),"未知引用补默认");Call(State(e),"CaptureAccepted");P<object?>(State(e),"ConstantBackup");Check(reads==0,"getter/历史隐式read");var changes=0;e.Changed+=()=>changes++;Check(Read(e).Status==NumericModelReadStatus.Updated&&P<long>(Model(e),"Value")==17L,"引用模型read");Check(e.Setting==identity&&changes==0&&reads==1,"引用read污染身份");var numeric=F(e,"_numeric");var coordinator=F(numeric,"_coordinator");var native=F(coordinator,"_arrange");Check(P<double>(native,"StartValue")==17,"隐藏引用端未投影模型");Check((bool)Call(State(e),"TryUseConstant")!&&((ConstantSetting)e.Setting).Value is 17L&&reads==2,"解绑未显式回读");
   });
   Case("无值异常解绑保正式引用且允许明确新常量",()=>{
    foreach(var fail in new[]{false,true}){var p=Int();var identity=new ReferenceSetting(new OutputReference("producer","value",DataType.Integer));var refs=ReferenceFieldOptions.Empty with{ReadCurrentValue=_=>fail?throw new InvalidOperationException("ordinary-read-failed"):null};using var e=Editor(p,identity,Layout(p),refs);Show(e);Check(Read(e).Status==(fail?NumericModelReadStatus.Failed:NumericModelReadStatus.Unavailable),"读失败状态");Check(!(bool)Call(State(e),"TryUseConstant")!&&e.Setting==identity&&!P<bool>(State(e),"IsReferenceMode"),"失败解绑破坏引用或锁死输入");Check(e.HasInvalidText,"未接纳输入态无门禁");Check(e.TrySetConstant(9L)&&e.Setting==new ConstantSetting(9L),"失败后无法明确输入");}
   });
   Case("raw跨回读隐藏与重建保留模型及旧叶退役",()=>{
    var p=Int();using var e=Editor(p,new ConstantSetting(3L),Layout(p),ReferenceFieldOptions.Empty with{ReadCurrentValue=_=>new(11L)});Show(e);var text=(TextBox)e.InputElement!;text.Text="raw-invalid";Pump();Check(Read(e).Status==NumericModelReadStatus.Updated&&text.Text=="raw-invalid"&&e.HasPendingEdits,"read覆盖raw");e.RetireInteractions();e.Visibility=Visibility.Collapsed;Pump();e.Visibility=Visibility.Visible;Pump();Check(text.Text=="raw-invalid","隐藏丢raw");var model=Model(e);e.TrySetConstant(4L);Call(e,"Render");Pump();Check(ReferenceEquals(model,Model(e)),"同state重建丢模型");text.Text="99";Pump();Check(e.Setting==new ConstantSetting(4L),"旧TextBox回调污染");
   });
   Case("声明页精确scope与C06和非数值排除",()=>{
    var a=Int("a");var b=Int("b");var low=Int("lo");var high=Int("hi");var count=new Dictionary<string,int>();var refs=ReferenceFieldOptions.Empty with{ReadCurrentValue=p=>{count[p.Id]=count.GetValueOrDefault(p.Id)+1;return new(5L);}};
    var fields=new[]{(a,Layout(a,"A"), (ParameterSetting)new ConstantSetting(1L)),(b,Layout(b,"B"),new ConstantSetting(2L)),(low,Layout(low,"A",pair:"hi"),new ConstantSetting(1L)),(high,Layout(high,"A"),new ConstantSetting(10L))};using var v=View(fields,refs,new(AdditionalPageContents:new Dictionary<string,FrameworkElement>{{"A",new Border()}}));Show(v);count.Clear();var r=v.ReadNumericModels(NumericModelReadScope.All);Check(r.Count==2&&count.Count==2,"All误读C06");count.Clear();v.SelectedPageIndex=1;r=v.ReadNumericModels(NumericModelReadScope.CurrentPage);Check(r.Count==1&&r[0].FieldId=="b"&&count.Keys.Single()=="b","声明页scope错误");Check(v.ReadNumericModels(NumericModelReadScope.Field,"lo").Single().Status==NumericModelReadStatus.NotApplicable,"C06未排除");Check(v.Draft!.Revision==0,"read写入draft");
   });
   Case("实际输入数值sequence同owner聚合保正式形状",()=>{
    var p=new Port("input.numeric",TypeRef.SequenceOf(DataType.Number),EditModes:FieldEditModes.Constant|FieldEditModes.Reference);ParameterSetting accepted=new ReferenceSetting(new OutputReference("source","sequence",p.Type));double[] sequenceValue=new[]{3.5};var schema=new ToolSchema(new[]{p},Array.Empty<Port>(),new ToolPresentation(Array.Empty<FieldPresentation>()));var refs=ReferenceFieldOptions.Empty with{ReadCurrentValue=port=>port.Id==p.Id?new(sequenceValue):new(7L)};using var input=new DeclaredInputFieldsView(schema,_=>accepted,(_,s)=>accepted=s,refs);var e=input.ComposeInputEditor(p.Id,"输入",true);var a=Int("a");var presentation=new FieldPresentation(a.Id,a.Id,"输入分组",0,FieldEditorKind.Scalar){Layout=new("A","输入分组",Minimum:-100,Maximum:100,Increment:1,DecimalPlaces:0)};using var v=View(new[]{(a,presentation, (ParameterSetting)new ConstantSetting(1L))},refs,new(InputContent:input,InputSection:"A"));Show(v);var r=v.ReadNumericModels(NumericModelReadScope.All);Check(r.Count==2&&P<double>(Model(e),"Value")==3.5,"输入sequence未聚合为scalar模型");Check(accepted is ReferenceSetting,"read破坏正式sequence引用");foreach(var bad in new[]{Array.Empty<double>(),new[]{1d,2d}}){sequenceValue=bad;Check(v.ReadNumericModels(NumericModelReadScope.Field,p.Id).Single().Status==NumericModelReadStatus.Failed&&P<double>(Model(e),"Value")==3.5,"零/多元素未保护或覆盖已知模型");}sequenceValue=new[]{3.5};Check(v.ReadNumericModels(NumericModelReadScope.CurrentPage).Count==2,"输入页归属错误");Check((bool)Call(State(e),"TryUseConstant")!||e.TrySetConstant(4.5),"sequence常量提案失败");Check(e.TrySetConstant(4.5)&&accepted is ConstantSetting s&&s.Value is not double,"sequence提案变scalar");
   });
   Case("图形popup未开注册且打开投影与ButtonSelecter排除",()=>{
    var ids=new[]{"g.visible","g.ok","g.ng","g.opacity","g.width"};var types=new[]{(TypeRef)DataType.Boolean,PresentationDeclarations.Rgba,PresentationDeclarations.Rgba,(TypeRef)DataType.Number,(TypeRef)DataType.Number};
    var initial=new object[]{true,PresentationDeclarations.Color(102,255,0),PresentationDeclarations.Color(255,0,0),1d,2d};
    var fields=ids.Select((id,i)=>(new Port(id,types[i],EditModes:FieldEditModes.Constant),new FieldPresentation(id,id,"B",i,i is 1 or 2?FieldEditorKind.Record:FieldEditorKind.Scalar){Layout=new("B","B",DialogGroup:"g",DialogTitle:"g",DialogPrimary:i==0,Minimum:i==3?0:i==4?1:null,Maximum:i==3?1:i==4?10:null,Increment:i==4?1:.01,DecimalPlaces:2)},(ParameterSetting)new ConstantSetting(initial[i]))).ToList();
    var x=new Port("x",DataType.Number,EditModes:FieldEditModes.Constant|FieldEditModes.Reference);fields.Add((x,new FieldPresentation("x","x","B",5,FieldEditorKind.Scalar){Layout=new("B","B")},new ConstantSetting(0d)));
    var opacity=.25;var reads=new List<string>();var refs=ReferenceFieldOptions.Empty with{ReadCurrentValue=p=>{reads.Add(p.Id);return p.Id==ids[3]?new(opacity):p.Id==ids[4]?new(3d):null;}};
    using var v=View(fields.ToArray(),refs,new(GraphicDisplays:new[]{new SunEyeVision.Plugin.Wpf.Fields.NativeGraphicDisplayOptions("g",ids[0],ids[1],ids[2],ids[3],ids[4])},InlineConstants:new HashSet<string>{"x"}));
    Check(reads.SequenceEqual(new[]{ids[3],ids[4]}),"初始化未登记图形C05或误读ButtonSelecter");
    Check(v.ReadNumericModels(NumericModelReadScope.Field,"x").Single().Status==NumericModelReadStatus.NotApplicable,"ButtonSelecter误入C05");Show(v);
    var root=F(v,"_fields");var access=((IEnumerable<DeclaredFieldAccess>)P<IReadOnlyList<DeclaredFieldAccess>>(root,"Fields")).Single(f=>f.FieldId==ids[3]);var style=access.View;
    Call(style,"Open");Pump();var text=(TextBox)Call(style,"Input",ids[3])!;Check(double.Parse(text.Text,System.Globalization.CultureInfo.InvariantCulture)==.25,"popup未投影预读模型");opacity=.75;v.ReadNumericModels(NumericModelReadScope.Field,ids[3]);Pump();Check(double.Parse(text.Text,System.Globalization.CultureInfo.InvariantCulture)==.75&&v.Draft!.Revision==0,"打开图形回读未投影或污染草稿");
   });
   Case("重绑与关闭回读拒旧根不写新模型",()=>{
    var p=Int();DeclaredSettingsView? view=null;var first=true;var armed=false;var refs=ReferenceFieldOptions.Empty with{ReadCurrentValue=_=>{if(armed&&first){first=false;var replacement=View(new[]{(p,Layout(p),(ParameterSetting)new ConstantSetting(22L))},ReferenceFieldOptions.Empty);view!.Bind(replacement.Draft!);replacement.Dispose();}return new(66L);}};using var v=view=View(new[]{(p,Layout(p),(ParameterSetting)new ConstantSetting(1L))},refs);Show(v);armed=true;var result=v.ReadNumericModels(NumericModelReadScope.All);Check(result.Any(r=>r.Status==NumericModelReadStatus.Retired)&&v.CaptureAcceptedFields().Read(p.Id)==new ConstantSetting(22L),"迟到read写新根");v.Dispose();Check(v.ReadNumericModels(NumericModelReadScope.All).Single().Status==NumericModelReadStatus.Retired,"关闭未退役");
   });
   Console.WriteLine($"C05公共{passed}组通过；仅本批数值模型，不扩C06/宿主生产验收。");return 0;
  }
  finally{owner.Close();Application.Current.Shutdown();}
 }
 [STAThread]public static int Main(){try{return Run();}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
