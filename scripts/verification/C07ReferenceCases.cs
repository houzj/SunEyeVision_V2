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
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Controls.References.Primitives;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using NativePopup=SunEyeVision.Plugin.Wpf.Controls.Popups.PopupButton;
internal static class C07ReferenceCases
{
 static object? F(object o,string n)=>o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(o);
 static T P<T>(object o,string n)=>(T)o.GetType().GetProperty(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(o)!;
 static object? Call(object o,string n,params object?[] a){try{return o.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Single(m=>m.Name==n&&m.GetParameters().Length==a.Length).Invoke(o,a);}catch(TargetInvocationException e){throw e.InnerException!;}}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static Window owner=null!;static StackPanel host=null!;static int passed;
 static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
 static void Show(FrameworkElement value){host.Children.Clear();host.Children.Add(value);owner.UpdateLayout();Pump();}
 static void Case(string name,Action run){host.Children.Clear();run();passed++;Console.WriteLine("PASS "+name);}
 static IEnumerable<T> Visual<T>(DependencyObject root){if(root is T found)yield return found;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var item in Visual<T>(VisualTreeHelper.GetChild(root,i)))yield return item;}
 static IEnumerable<object> Nodes(object list){foreach(var node in (IEnumerable)list){yield return node;foreach(var child in Nodes(P<object>(node,"EnumEntrysA")))yield return child;}}
 static DeclaredReferenceCandidate Candidate(string id,string label,TypeRef? type=null)=>new(label,id,"flow",new Port("value",type??DataType.Number),Array.Empty<string>()){NodeDisplayName=id+" 节点",OutputDisplayName="值"};
 static readonly TypeRef Sequence=TypeRef.SequenceOf(DataType.Number,CountConstraint.Exactly(3));
 static readonly DeclaredReferenceCandidate A=Candidate("11","11 A.值",Sequence),B=Candidate("12","12 B.值",Sequence),Scalar=Candidate("13","13 Scalar.值"),Index=Candidate("14","14 Index.序号",DataType.Integer);
 static VariableSelectionItem Variable(string id,TypeRef type)=>new(id,"参数",1,"","",new VariableReference("solution",id,type));
 sealed class Fixture:IDisposable
 {
  internal readonly DeclaredFieldEditor Editor;internal readonly object Session;internal readonly DeleteTextBox Text;internal ParameterSetting Accepted;internal int Writes,Reads;internal bool Reject;internal Action? OnRead;internal VariableSelectionRequest? Request;
  internal DeclaredReferenceCandidate[] Sources={A,B,Scalar};internal VariableSelectionItem[] Variables={Variable("数组",Sequence),Variable("单值",DataType.Number),Variable("序号",DataType.Integer)};
  internal Fixture(ParameterSetting initial,bool editable=false,bool captureVariables=false)
  {
   Accepted=initial;var options=new ReferenceFieldOptions(_=>{Reads++;OnRead?.Invoke();return new(Sources,new[]{Index},Variables);},ValidateProposal:(_,_)=>Reject?"host-rejected":null,EditableIndex:editable,SelectVariable:captureVariables?r=>Request=r:null);
   Editor=new(new Port("n",DataType.Number,Required:false,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),"数值",initial,v=>{Accepted=v;Writes++;},options,formatReference:r=>Sources.Concat(new[]{Index}).FirstOrDefault(c=>c.NodeId==r.NodeId)?.Label??r.NodeId+".missing",presentation:new("n","数值","A",0,FieldEditorKind.Scalar){Layout=new("A","A",Minimum:0,Maximum:100,DecimalPlaces:2)});
   Show(Editor);Session=F(F(F(Editor,"_numeric")!,"_coordinator")!,"_reference")!;Call(Session,"Refresh");Pump();Text=Visual<DeleteTextBox>(Editor).Single(t=>ReferenceEquals(t.DataContext,Session));
  }
  internal string Display=>P<string>(Session,"CurValueA");
  internal void Command(string name,object? value=null)=>P<ICommand>(Session,name).Execute(value);
  internal object Source(string id)=>Nodes(P<object>(Session,"SourceNodes")).Single(n=>P<DeclaredReferenceCandidate?>(n,"Candidate")?.NodeId==id);
  internal NativePopup Popup(bool index)=>Visual<NativePopup>(Editor).Single(p=>ReferenceEquals(p.DataContext,Session)&&p.Name==(index?"et":"rt"));
  internal TreeViewItem OpenNode(object node,bool index=false)
  {
   var popup=Popup(index);popup.IsDropDownOpen=true;Pump();var part=(Popup)popup.Template.FindName("PART_Popup",popup)!;Check(part.IsOpen&&part.Child is not null,"真实原生Popup未打开");var tree=Visual<SubTreeView>(part.Child).Single();tree.UpdateLayout();Pump();var native=(TreeView)tree.FindName("treeView");
   return (TreeViewItem?)typeof(SubTreeView).GetMethod("FindTreeViewItem",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new[]{(object)native,node})??throw new Exception("原树未生成目标容器");
  }
  internal void Choose(string id)
  {
   var popup=Popup(false);popup.IsDropDownOpen=true;Pump();var node=Source(id);var container=OpenNode(node);container.IsSelected=true;Keyboard.Focus(container);container.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=Control.MouseDoubleClickEvent});Pump();
  }
  internal void Raw(string value,bool transfer=true){Text.Text=value;if(transfer)Text.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();Pump();}
  public void Dispose(){Editor.Dispose();Pump();}
 }
 static int Run()
 {
  new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};owner=new Window{Content=host=new StackPanel(),Width=800,Height=750,ShowActivated=false,ShowInTaskbar=false};owner.Resources.MergedDictionaries.Add(EditorResources.Create());owner.Show();Pump();
  try
  {
   Case("普通scalar原文字方括号与真实只读焦点",()=>{
    using var f=new Fixture(new ReferenceSetting(Scalar.Reference(BindingSelection.All)));Check(f.Display==Scalar.Label+"[]"&&f.Text.Text==f.Display&&f.Text.IsReadOnly,"普通scalar省[]、多空格或非原只读");Check(Nodes(P<object>(f.Session,"SourceNodes")).Any(n=>P<DeclaredReferenceCandidate?>(n,"Candidate")?.NodeId==A.NodeId),"只读错误隐藏了序列资格");
    Keyboard.Focus(f.Text);Check(f.Text.IsKeyboardFocused,"隔离桌面未取得真实WPF焦点，不伪造");var reads=f.Reads;f.Text.SelectionStart=f.Text.Text.Length-1;Pump();Check(!P<bool>(f.Session,"IsTextDropDownOpen")&&f.Reads==reads,"普通ValueBase只读仍开索引Popup");
   });
   Case("原生树切源保固定动态后缀且scalar明确拒绝",()=>{
    foreach(var selection in new BindingSelection[]{new BindingSelection.AtConstant(1),new BindingSelection.AtField(Index.Reference(BindingSelection.All))})
    {using var f=new Fixture(new ReferenceSetting(A.Reference(selection)));f.Choose(B.NodeId);Check(f.Accepted is ReferenceSetting selected&&selected.Reference.NodeId==B.NodeId&&Equals(selected.Reference.Selection,selection)&&f.Writes==1,"树切源丢原索引");Check(!f.Display.Contains(" [",StringComparison.Ordinal)&&f.Display.StartsWith(B.Label+"[",StringComparison.Ordinal),"原后缀前多空格");f.Choose(Scalar.NodeId);Check(f.Accepted is ReferenceSetting retained&&retained.Reference.NodeId==B.NodeId&&Equals(retained.Reference.Selection,selection)&&f.Writes==1&&f.Editor.HasInvalidText,"scalar索引被静默ALL/误接纳或未保护");}
   });
   Case("已接纳后的观察者异常不能伪报拒绝",()=>{
    using var f=new Fixture(new ReferenceSetting(A.Reference(new BindingSelection.AtConstant(1))));Action failure=()=>throw new InvalidOperationException("accepted-observer-failure");f.Editor.Changed+=failure;var propagated=false;
    try{f.Command("ClickCommand",f.Source(B.NodeId));}catch(InvalidOperationException error){propagated=error.Message=="accepted-observer-failure";}finally{f.Editor.Changed-=failure;}
    Check(propagated&&f.Writes==1&&f.Accepted is ReferenceSetting{Reference.NodeId:"12"}&&!f.Editor.HasInvalidText,"已接纳异常被外wrapper吞掉或新Accepted被写error");
   });
   Case("组合真实可编辑焦点两Popup及无效raw切源保留",()=>{
    using var f=new Fixture(new ReferenceSetting(A.Reference(new BindingSelection.AtConstant(1))),true);Check(!f.Text.IsReadOnly,"组合EditableIndex能力丢失");Keyboard.Focus(f.Text);Check(f.Text.IsKeyboardFocused,"未取得组合真实WPF焦点");f.Text.SelectionStart=f.Text.Text.Length-1;Pump();Check(P<bool>(f.Session,"IsTextDropDownOpen"),"原焦点条件未开索引Popup");var node=Nodes(P<object>(f.Session,"IndexNodes")).Single(n=>P<DeclaredReferenceCandidate?>(n,"Candidate")?.NodeId==Index.NodeId);var item=f.OpenNode(node,true);item.IsSelected=true;Keyboard.Focus(item);item.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=Control.MouseDoubleClickEvent});Pump();Check(f.Accepted is ReferenceSetting{Reference.Selection:BindingSelection.AtField},"真实索引树未产生结构身份");
    f.Raw(A.Label+"[bad]");var before=f.Accepted;f.Choose(B.NodeId);Check(ReferenceEquals(f.Accepted,before)&&f.Display==B.Label+"[bad]"&&f.Editor.HasInvalidText,"切源丢invalid后缀或半写正式");f.Raw(B.Label+"[2]");Check(f.Accepted is ReferenceSetting{Reference.NodeId:"12",Reference.Selection:BindingSelection.AtConstant{Index:2}}&&!f.Editor.HasInvalidText,"组合原文修复不能单次接纳");
   });
   Case("变量保后缀scalar构造拒绝与旧请求退出",()=>{
    using var f=new Fixture(new ReferenceSetting(A.Reference(new BindingSelection.AtConstant(1))),true,true);
    void Open(){f.Command("ClickCommand",Nodes(P<object>(f.Session,"SourceNodes")).Single(n=>P<bool>(n,"VariableManager")));Check(f.Request?.IsValid==true,"变量请求未产生");}
    Open();var seq=f.Request!.Items.Single(i=>i.Reference.VariableId=="数组");Check(f.Request.TrySelect(seq)&&f.Accepted is VariableSetting{Reference.Selection:BindingSelection.AtConstant{Index:1}},"变量切源丢索引");var previous=f.Accepted;Open();var scalar=f.Request!.Items.Single(i=>i.Reference.VariableId=="单值");Check(!f.Request.TrySelect(scalar)&&ReferenceEquals(f.Accepted,previous)&&f.Editor.HasInvalidText,"变量scalar构造未受保护或静默ALL");Open();var old=f.Request!;Call(f.Session,"Refresh");Check(!old.IsValid&&!old.TrySelect(seq),"目录刷新未退役变量请求");
   });
   Case("更名严格raw前缀同名新ID与类型刷新不重绑",()=>{
    using var f=new Fixture(new ReferenceSetting(A.Reference(new BindingSelection.AtConstant(1))),true);f.Raw(A.Label+"[2]",false);f.Sources=new[]{A with{Label="11 renamed.值"},B,Scalar};Call(f.Session,"Refresh");Pump();Check(f.Display==A.Label+"[2]"&&!f.Editor.TryCommitPending()&&f.Accepted is ReferenceSetting{Reference.Selection:BindingSelection.AtConstant{Index:1}},"rename刷新清raw或按caption重绑");f.Choose(A.NodeId);Check(f.Display=="11 renamed.值[2]"&&f.Accepted is ReferenceSetting{Reference.NodeId:"11",Reference.Selection:BindingSelection.AtConstant{Index:2}},"重新选源未按原后缀恢复");var old=f.Accepted;f.Sources=new[]{B with{Label="11 renamed.值"},Scalar};Call(f.Session,"Refresh");Check(ReferenceEquals(f.Accepted,old)&&f.Editor.HasInvalidText,"同名新ID劫持原身份");
    f.Sources=new[]{A with{Output=new Port("value",TypeRef.SequenceOf(DataType.Number,CountConstraint.Exactly(2)))},B,Scalar};Call(f.Session,"Refresh");Check(f.Accepted is ReferenceSetting{Reference.NodeId:"11"} changed&&changed.Reference.Type.Count.Maximum==3&&f.Editor.HasInvalidText,"同ID类型刷新改正式声明或假成功");
    f.OnRead=()=>f.Editor.Dispose();Call(f.Session,"Refresh");Check(f.Writes==1,"旧刷新退出仍写新owner");
   });
   Case("图像父组合显示与resource成员payload刷新保留",()=>{
    var image=TypeRef.ResourceOf(ResourceKind.Image);var record=TypeRef.RecordOf("frame",new[]{new Port("data",image),new Port("width",DataType.Integer)});var current=Candidate("21","21 Camera.ImageGroup",record) with{MemberPath=new[]{"data"},MemberDisplayNames=new[]{"Pixels"}};var sources=new[]{current,Candidate("21","21 Camera.Width",record) with{MemberPath=new[]{"width"},MemberDisplayNames=new[]{"Width"}}};var writes=0;ParameterSetting accepted=new ReferenceSetting(current.Reference(BindingSelection.All));using var field=new DeclaredFieldEditor(new Port("image",image,Required:false,EditModes:FieldEditModes.Reference),"图像",accepted,value=>{accepted=value;writes++;},ReferenceFieldOptions.Empty,formatReference:_=>current.Label,allowUnboundReference:true);field.UseImageSourceSelector(()=>sources);Show(field);var combo=(ComboBox)field.InputElement!;Check(combo.Items.OfType<ComboBoxItem>().Count(i=>i.Tag is ReferenceSetting)==1&&((ComboBoxItem)combo.SelectedItem).Content!.ToString()==current.Label,"图像父Label未直用或混入width叶");
    current=current with{Label="21 renamed.ImageGroup"};sources=new[]{current};combo.IsDropDownOpen=true;Pump();Check(((ComboBoxItem)combo.SelectedItem).Content!.ToString()==current.Label&&writes==0&&accepted is ReferenceSetting r&&r.Reference.MemberFieldPath.SequenceEqual(new[]{"data"}),"图像rename刷新改payload/写提案");combo.IsDropDownOpen=false;sources=Array.Empty<DeclaredReferenceCandidate>();combo.IsDropDownOpen=true;Pump();Check(combo.SelectedItem is ComboBoxItem{IsEnabled:false}&&writes==0&&field.HasInvalidText,"图像来源消失被清正式或假成功");
   });
   Case("既有范围非视觉参数命令完整事务与拒绝重入退役",()=>{
    var ports=new[]{new Port("lo",DataType.Number,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),new Port("hi",DataType.Number,EditModes:FieldEditModes.Constant|FieldEditModes.Reference)};var fields=new[]{new FieldPresentation("lo","范围","A",0,FieldEditorKind.Scalar){Layout=new("A","A",Minimum:0,Maximum:100,PairWith:"hi")},new FieldPresentation("hi","上限","A",1,FieldEditorKind.Scalar){Layout=new("A","A",Minimum:0,Maximum:100)}};var schema=new ToolSchema(ports,Array.Empty<Port>(),new ToolPresentation(fields));
    FieldSettingsDraft Draft()=>new(schema,new FieldSettings(ports,new Dictionary<string,ParameterSetting>{{"lo",new ConstantSetting(1d)},{"hi",new ConstantSetting(10d)}}));DeclaredSettingsView? view=null;int mode=0;bool nested=true;var references=new ReferenceFieldOptions(_=>new(new[]{A,B},new[]{Index}),ValidateProposal:(_,_)=>{if(mode==2)nested=view!.TrySetFields(new Dictionary<string,ParameterSetting>{{"lo",new ReferenceSetting(B.Reference(new BindingSelection.AtConstant(1)))}});if(mode==3)view!.Dispose();return mode is 1 or 2?"host-rejected":null;});var options=new ToolSettingsViewOptions(ReferenceOptions:references);using var v=view=new DeclaredSettingsView(Draft(),options);Show(v);
    Check(v.TrySetFields(new Dictionary<string,ParameterSetting>{{"lo",new ReferenceSetting(A.Reference(new BindingSelection.AtConstant(1)))}}),"范围单reference参数命令未接原事务");var variable=new VariableSetting(new VariableReference("solution","array",Sequence,new BindingSelection.AtConstant(2)));Check(v.TrySetFields(new Dictionary<string,ParameterSetting>{{"hi",variable}}),"范围variable参数命令未接事务");Check(v.CaptureAcceptedFields().Read("hi") is VariableSetting{Reference.VariableId:"array",Reference.Selection:BindingSelection.AtConstant{Index:2}},"变量身份未保留");
    var draft=Draft();v.Bind(draft,options);Show(v);var root=(DeclaredFieldsView)v.Content;var low=(TextBox)root.Fields.Single(f=>f.FieldId=="lo").InputElement!;var high=(TextBox)root.Fields.Single(f=>f.FieldId=="hi").InputElement!;void Trace(string phase,bool? result=null){var range=root.Fields.Single(f=>f.FieldId=="lo").View;var commits=(object[])F(range,"_commits")!;Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{phase,result,mode,revision=draft.Revision,low=low.Text,high=high.Text,lowVisible=low.IsVisible,highVisible=high.IsVisible,lowLoaded=low.IsLoaded,highLoaded=high.IsLoaded,active=F(range,"_active"),hidden=F(range,"_presentationHidden"),pending=commits.Select(c=>P<bool>(c,"IsDirty")).ToArray(),suspended=commits.Select(c=>P<bool>(c,"IsSuspended")).ToArray(),current=commits.Select(c=>Call(c,"Current")).ToArray(),settings=root.Fields.Select(f=>new{f.FieldId,setting=f.Setting.ToString(),f.HasPendingEdits,f.HasInvalidText,f.Error}).ToArray()}));}
    Trace("before-raw");low.Text="bad-left";high.Text="bad-right";Pump();Trace("after-raw");foreach(var reject in new[]{1,2}){mode=reject;Trace("before-command");var result=v.TrySetFields(new Dictionary<string,ParameterSetting>{{"lo",new ReferenceSetting(B.Reference(new BindingSelection.AtConstant(1)))}});Trace("after-command",result);Check(!result&&draft.Revision==0&&low.Text=="bad-left"&&high.Text=="bad-right","拒绝/重入改变两端raw或半写");}Check(!nested,"资格/范围事务允许重入");mode=3;Check(!v.TrySetFields(new Dictionary<string,ParameterSetting>{{"lo",new ReferenceSetting(B.Reference(new BindingSelection.AtConstant(1)))}})&&draft.Revision==0,"退役后仍写原draft");
   });
   Case("原展开窗口借用owner与隐藏后旧请求不再写",()=>{
    using var f=new Fixture(new ReferenceSetting(A.Reference(new BindingSelection.AtConstant(1))),true);f.Command("PopSubscribeWindowCommand");Pump();var request=(ReferenceExpansionRequest)F(f.Session,"_expansion")!;Check(request.IsValid,"原展开请求无效");var expanded=Application.Current.Windows.OfType<Window>().Single(w=>!ReferenceEquals(w,owner)&&w.IsVisible);Check(ReferenceEquals(expanded.Owner,owner),"展开窗脱离实际owner");var old=f.Source(B.NodeId);f.Editor.Visibility=Visibility.Collapsed;Pump();Check(!request.IsValid&&!expanded.IsVisible,"隐藏未退出原展开窗/请求");request.ClickCommand.Execute(old);Check(f.Writes==0,"旧展开请求写字段");f.Editor.Visibility=Visibility.Visible;Pump();f.Editor.Dispose();request.ClickCommand.Execute(old);Check(f.Writes==0,"Dispose复活旧来源请求");
   });
   Console.WriteLine($"C07公共{passed}组通过；真实WPF公共引用整链候选，正式宿主及人工另记。");return 0;
  }
  finally{owner.Close();Application.Current.Shutdown();}
 }
 static IntPtr desktop;
 [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,int flags,uint access,IntPtr security);
 [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]static extern bool SetThreadDesktop(IntPtr value);
 [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]static extern bool CloseDesktop(IntPtr value);
 [STAThread]public static int Main()
 {
  var result=1;var thread=new Thread(()=>{
   try{desktop=CreateDesktop("SunEyeVision-C07-"+Guid.NewGuid().ToString("N"),IntPtr.Zero,IntPtr.Zero,0,0x000F01FF,IntPtr.Zero);
    if(desktop==IntPtr.Zero||!SetThreadDesktop(desktop))throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(),"独立隐藏桌面不可用；不回退到用户桌面。");
    Console.WriteLine("C07隐藏桌面已建立，不切换用户桌面。");result=Run();}
   catch(Exception e){Console.Error.WriteLine(e);}});
  thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(desktop!=IntPtr.Zero)CloseDesktop(desktop);return result;
 }
}
