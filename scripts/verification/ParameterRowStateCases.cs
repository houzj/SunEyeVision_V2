using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Controls.References.Primitives;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Core;
using Numbers=SunEyeVision.Plugin.Wpf.Native.Numeric.Arranged;

internal static class ParameterRowStateCases
{
 static Window owner=null!;static StackPanel host=null!;static int passed;
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static object F(object value,string name)=>value.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(value)!;
 static T P<T>(object value,string name)=>(T)value.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(value)!;
 static object? Call(object value,string name,params object?[] args){try{return value.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(value,args);}catch(TargetInvocationException error){throw error.InnerException!;}}
 static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
 static IEnumerable<T> Visual<T>(DependencyObject root)where T:DependencyObject
 {if(root is T value)yield return value;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Visual<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
 static IEnumerable<object> Nodes(object values){foreach(var node in (IEnumerable)values){yield return node;foreach(var child in Nodes(P<object>(node,"EnumEntrysA")))yield return child;}}
 static void Scenario(bool floating,bool paired)
 {
  var type=floating?DataType.Number:DataType.Integer;
  object Value(long value)=>floating?(object)(double)value:value;
  var modes=FieldEditModes.Constant|FieldEditModes.Reference;
  var declarations=new List<(Port Port,FieldPresentation Presentation,ParameterSetting Value)>
  { (new("lo",type,Required:true,EditModes:modes),new("lo","低阈值","g",0,FieldEditorKind.Scalar){Layout=new("参数","组",Minimum:0,Maximum:255,DecimalPlaces:floating?2:0,PairWith:paired?"hi":null)},new ConstantSetting(Value(7))) };
  if(paired)declarations.Add((new("hi",type,Required:true,EditModes:modes),new("hi","高阈值","g",1,FieldEditorKind.Scalar){Layout=new("参数","组",Minimum:0,Maximum:255,DecimalPlaces:floating?2:0)},new ConstantSetting(Value(30))));
  var a=new DeclaredReferenceCandidate("1 源A.值","1","flow",new Port("value",type),Array.Empty<string>());
  var b=new DeclaredReferenceCandidate("2 源B.值","2","flow",new Port("value",type),Array.Empty<string>());
  var reject=false;var writes=0;
  var options=new ReferenceFieldOptions(_=>new(new[]{a,b},Array.Empty<DeclaredReferenceCandidate>()),ValidateProposal:(_,_)=>reject?"host-rejected":null,
   ReadCurrentValue:p=>new ConstantSetting(Value(p.Id=="hi"?30:7)));
  using var fields=new DeclaredFieldsView(declarations,_=>writes++,options);
  host.Children.Clear();host.Children.Add(fields);owner.UpdateLayout();Pump();
  var list=Visual<ParamListView>(fields).Single();var row=list.Items.Cast<NativeParameterRow>().Single();
  var content=Visual<ParamUserControl>(list).Single();var border=(Border)VisualTreeHelper.GetParent(content);
  var view=fields.Fields.Single(field=>field.FieldId=="lo").View;
  object State(string id)=>((IEnumerable)F(fields,"_fieldStates")).Cast<object>().Single(value=>P<Port>(value,"Port").Id==id);
  var state=State("lo");
  object Session()=>paired?((Array)F(view,"_references")).GetValue(0)!:F(F(F(view,"_numeric"),"_coordinator"),"_reference");
  Color Color()=>((SolidColorBrush)border.BorderBrush).Color;
  void Expect(NativeParameterItemState expected,Color color,string reason)
  {Pump();Check(row.MyItemState==expected&&Color()==color,reason+": "+row.MyItemState+"/"+Color());}
  ToggleButton Mode(string side)=>Visual<ToggleButton>(view).Single(toggle=>BindingOperations.GetBinding(toggle,ToggleButton.IsCheckedProperty)?.Path?.Path==side);
  void Switch(bool subscribe,string side="IsSubscribeLeft")
  {var button=Mode(side);Check(button.IsVisible&&button.IsEnabled,"实际模式按钮不可用");button.SetCurrentValue(ToggleButton.IsCheckedProperty,subscribe);button.GetBindingExpression(ToggleButton.IsCheckedProperty)!.UpdateSource();button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Pump();}
  void Choose(string id)
  {var session=Session();Call(session,"Refresh");var node=Nodes(P<object>(session,"SourceNodes")).Single(node=>P<DeclaredReferenceCandidate?>(node,"Candidate")?.NodeId==id);P<ICommand>(session,"ClickCommand").Execute(node);Pump();}
  Expect(NativeParameterItemState.Normal,Colors.Transparent,"初始合法常量不应标错");
  Switch(true);
  Check(P<bool>(state,"IsReferenceMode")&&P<bool>(state,"MissingReference")&&fields.HasInvalidActiveFields,"等待态丢失必要来源校验");
  Check(writes==0&&fields.Fields.Single(field=>field.FieldId=="lo").Setting is ConstantSetting,"切模式提前写入正式参数");
  Expect(NativeParameterItemState.Normal,Colors.Transparent,"刚切绑定且未选源不应投影写错误红框");
  var text=Visual<DeleteTextBox>(view).Single(editor=>ReferenceEquals(editor.DataContext,Session()));Keyboard.Focus(text);Pump();
  Check(text.IsKeyboardFocused,"没有取得实际WPF焦点");row.ParamItemSelectedCommand.Execute(null);
  Expect(NativeParameterItemState.Normal,Colors.Transparent,"焦点或选中不应冒充整行写错误");
  Choose("1");Check(fields.Fields.Single(field=>field.FieldId=="lo").Setting is ReferenceSetting&&!fields.HasInvalidActiveFields,"选源后未正常接纳");
  Expect(NativeParameterItemState.Normal,Colors.Transparent,"合法来源仍标错");
  Call(state,"SetSourceError","read-failed");Expect(NativeParameterItemState.ReadError,Colors.Yellow,"真实来源错误必须保黄框");
  reject=true;var before=fields.Fields.Single(field=>field.FieldId=="lo").Setting;var priorWrites=writes;Choose("2");
  Check(ReferenceEquals(before,fields.Fields.Single(field=>field.FieldId=="lo").Setting)&&writes==priorWrites&&fields.HasInvalidActiveFields,"失败选源改写已接纳值或放宽门禁");
  Expect(NativeParameterItemState.WriteError,Colors.Red,"真实编辑拒绝必须保红框优先级");
  Call(state,"SetEditError","");Expect(NativeParameterItemState.ReadError,Colors.Yellow,"清写错不能抹掉读错");
  reject=false;Choose("2");Call(state,"SetSourceError","");
  Expect(NativeParameterItemState.Normal,Colors.Transparent,"重新合法选源不能清理错误框");
  Switch(false);var access=fields.Fields.Single(field=>field.FieldId=="lo");Check(access.TrySetConstant(Value(8)),"恢复常量未成功");Pump();
  Expect(NativeParameterItemState.Normal,Colors.Transparent,"恢复常量仍留错误框");
  var input=(TextBox)access.InputElement!;Call(state,"SetSourceError","read-failed");
  input.Text="invalid-number";Pump();
  Check(access.HasInvalidText,"无效数字文本不再校验");Expect(NativeParameterItemState.WriteError,Colors.Red,"无效文本必须保留红框");
  input.Text="9";Pump();Expect(NativeParameterItemState.ReadError,Colors.Yellow,"原始文本修复不能抹掉独立来源错误");
  Check(access.TryCommitPending(),"合法文本不能提交");Pump();
  Expect(NativeParameterItemState.Normal,Colors.Transparent,"合法提交后错误框未恢复");
  if(paired)
  {
   Switch(true,"IsSubscribeRight");var right=State("hi");
   Check(P<bool>(right,"MissingReference")&&fields.HasInvalidActiveFields,"配对右端等待态未保确认门禁");
   Expect(NativeParameterItemState.Normal,Colors.Transparent,"配对右端等待态错误汇总成红框");
   Call(right,"SetEditError","right-write-failed");Expect(NativeParameterItemState.WriteError,Colors.Red,"配对右端真实错误被隐藏");
   Call(right,"SetEditError","");Expect(NativeParameterItemState.Normal,Colors.Transparent,"右端清错后等待态未恢复");
  }
  host.Children.Clear();Pump();passed++;Console.WriteLine("PASS "+(paired?"paired":"single")+"-"+(floating?"float":"integer")+" 等待选源/焦点/真实读写错误/原始文本/恢复整链");
 }
 static int Run()
 {
  new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};owner=new Window{Content=host=new StackPanel(),Width=850,Height=650,ShowActivated=false,ShowInTaskbar=false};owner.Resources.MergedDictionaries.Add(EditorResources.Create());owner.Show();Pump();
  try{Scenario(false,false);Scenario(true,false);Scenario(false,true);Console.WriteLine($"参数行状态{passed}组通过");return 0;}
  finally{owner.Close();Application.Current.Shutdown();}
 }
 static IntPtr desktop;
 [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,int flags,uint access,IntPtr security);
 [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]static extern bool SetThreadDesktop(IntPtr value);
 [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]static extern bool CloseDesktop(IntPtr value);
 [STAThread]public static int Main()
 {
  var result=1;var thread=new Thread(()=>{try{desktop=CreateDesktop("SunEyeVision-ParameterRow-"+Guid.NewGuid().ToString("N"),IntPtr.Zero,IntPtr.Zero,0,0x000F01FF,IntPtr.Zero);if(desktop==IntPtr.Zero||!SetThreadDesktop(desktop))throw new System.ComponentModel.Win32Exception();result=Run();}catch(Exception error){Console.Error.WriteLine(error);}});
  thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(desktop!=IntPtr.Zero)CloseDesktop(desktop);return result;
 }
}
