using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.ComponentModel;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;

internal static class BlobUiCleanupCases
{
    static object Field(object o,string n)=>o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(o)!;
    static bool Disposed(object o)=>o.GetType().GetProperty("IsDisposed",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance) is {} p?(bool)p.GetValue(o)!:(bool)Field(o,"_disposed");
    static object[] Items(object o,string n)=>((IEnumerable)Field(o,n)).Cast<object>().ToArray();
    static void Check(bool b,string m){if(!b)throw new Exception(m);}
    static void Pump(){var f=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>f.Continue=false));Dispatcher.PushFrame(f);}
    static Window owner=null!;static StackPanel host=null!;static int passed;
    static void Case(string label,Action run){host.Children.Clear();run();passed++;Console.WriteLine("PASS "+label);}
    static void Show(FrameworkElement view){host.Children.Add(view);owner.UpdateLayout();Pump();}
    static DeclaredFieldEditor Editor(string id)=>new(new Port(id,DataType.Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant),id,new ConstantSetting(1L),_=>{});
    static Exception Capture(Action action){try{action();}catch(Exception e){return e;}throw new Exception("预期真实故障未触发");}
    static Exception[] Errors(Exception e)=>e is AggregateException a?a.Flatten().InnerExceptions.ToArray():new[]{e};
    static void RadioFailure(object radio,Exception failure,Action? reenter=null)
    {
        var native=(FrameworkElement)radio.GetType().GetProperty("NativeView")!.GetValue(radio)!;
        native.IsEnabledChanged+=(_,_)=>{if(!native.IsEnabled){reenter?.Invoke();throw failure;}};
    }
    sealed class DetachFailure(Exception failure,Action? reenter=null):FrameworkElement
    {
        public bool Attempted;
        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {base.OnVisualParentChanged(oldParent);if(oldParent is not null){Attempted=true;reenter?.Invoke();throw failure;}}
    }
    sealed class FailingParameters(DeclaredFieldEditor[] values,Action beforeFailure,Exception failure):IReadOnlyList<DeclaredFieldEditor>
    {
        public int Count=>values.Length;public DeclaredFieldEditor this[int i]=>values[i];
        public IEnumerator<DeclaredFieldEditor> GetEnumerator(){yield return values[0];beforeFailure();throw failure;}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    static int Run()
    {
        new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};owner=new Window{Content=host=new StackPanel(),Width=600,Height=800,Left=-20000,Top=-20000,ShowActivated=false,ShowInTaskbar=false};owner.Resources.MergedDictionaries.Add(EditorResources.Create());owner.Show();Pump();
        try
        {
            Case("Assist清空与radio故障后全部owned释放并保borrowed",()=>{
                using var information=Editor("assist-information");var view=new NativeAssistUiDraftControls(information);Show(view);
                var rows=Items(view,"_rows").Select(row=>row.GetType().GetField("Item1")!.GetValue(row)!).ToArray();var radio=Field(view,"_radio");var state=Field(view,"_mode");
                var detachError=new InvalidOperationException("assist-detach");var radioError=new InvalidOperationException("assist-radio");var detach=new DetachFailure(detachError,view.Dispose);view.Children.Add(detach);RadioFailure(radio,radioError,view.Dispose);
                var error=Capture(view.Dispose);var errors=Errors(error);Check(errors.Contains(detachError)&&errors.Contains(radioError),"Assist原故障丢失");Check(detach.Attempted&&rows.All(Disposed)&&Disposed(radio)&&Disposed(state),"Assist后续owned跳过");Check(!Disposed(information)&&information.TrySetConstant(2L),"Assist销毁借入字段");view.Dispose();Check(Disposed(view),"Assist重入复活");
            });
            Case("ROI三个panel与多radio故障仍完整释放",()=>{
                var view=new NativeRoiUiDraftControls();Show(view);var editors=Items(view,"_inheritEditors");var radios=Items(view,"_radios");var states=Items(view,"_states");
                var failures=Enumerable.Range(0,5).Select(i=>new InvalidOperationException("roi-cleanup-"+i)).ToArray();var detaches=new[]{new DetachFailure(failures[0],view.Dispose),new DetachFailure(failures[1]),new DetachFailure(failures[2])};
                view.Children.Add(detaches[0]);((Panel)Field(view,"_rectangleParameters")).Children.Add(detaches[1]);((Panel)Field(view,"_circleParameters")).Children.Add(detaches[2]);RadioFailure(radios[0],failures[3],view.Dispose);RadioFailure(radios[1],failures[4]);
                var errors=Errors(Capture(view.Dispose));Check(failures.All(errors.Contains),"ROI未汇总所有原故障");Check(detaches.All(d=>d.Attempted),"ROI后续panel跳过");Check(editors.All(Disposed)&&radios.All(Disposed)&&states.All(Disposed),"ROI后续owned跳过");view.Dispose();Check(Disposed(view),"ROI重入复活");
            });
            Case("ROI部分构造失败保原异常和清理异常及借入所有权",()=>{
                using var rectangle=Editor("rectangle-source");using var annulus=Editor("annulus-source");var rect=Enumerable.Range(0,5).Select(i=>Editor("rectangle-"+i)).ToArray();var ring=Enumerable.Range(0,6).Select(i=>Editor("annulus-"+i)).ToArray();
                NativeRoiUiDraftControls? partial=null;var original=new InvalidOperationException("roi-constructor-original");var cleanup=new InvalidOperationException("roi-constructor-cleanup");
                var values=new FailingParameters(rect,()=>{partial=(NativeRoiUiDraftControls)annulus.Parent;RadioFailure(Items(partial,"_radios")[0],cleanup,partial.Dispose);},original);
                var binding=new NativeRoiInheritanceBinding(null,_=>{},rectangle,annulus,values,ring);
                var errors=Errors(Capture(()=>new NativeRoiUiDraftControls(false,binding)));
                Check(errors.Contains(original)&&errors.Contains(cleanup),"构造原异常或清理异常丢失");Check(partial is not null&&Disposed(partial),"部分实例未退役");Check(Items(partial!,"_inheritEditors").All(Disposed)&&Items(partial!,"_radios").All(Disposed)&&Items(partial!,"_states").All(Disposed),"部分构造owned漏清理");
                Check(rect.Append(rectangle).Append(annulus).Concat(ring).All(e=>!Disposed(e)),"构造失败销毁borrowed");Check(rect[0].Parent is null&&annulus.Parent is null,"构造失败未解除borrowed父级");foreach(var e in rect.Concat(ring))e.Dispose();
            });
            Case("Assist构造原异常保留且借入字段仍可复用",()=>{
                using var information=Editor("assist-constructor");var label=(TextBlock)Field(information,"_label");var descriptor=DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty,typeof(TextBlock));var original=new InvalidOperationException("assist-constructor-original");EventHandler fail=(_,_)=>throw original;descriptor.AddValueChanged(label,fail);
                Exception error;try{error=Capture(()=>new NativeAssistUiDraftControls(information));}finally{descriptor.RemoveValueChanged(label,fail);}
                Check(Errors(error).Contains(original),"Assist构造原异常丢失");Check(!Disposed(information)&&information.Parent is null&&information.TrySetConstant(3L),"Assist失败销毁borrowed");using var retry=new NativeAssistUiDraftControls(information);retry.Dispose();Check(!Disposed(information),"Assist重建销毁borrowed");
            });
            Console.WriteLine($"公共清理{passed}组通过；本批新增故障验证，不复用历史八组成绩。");return 0;
        }
        finally{owner.Close();Application.Current.Shutdown();}
    }
    [STAThread]public static int Main(){try{return Run();}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
