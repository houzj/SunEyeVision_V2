using System.Reflection;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;
using SunEyeVision.Plugin.Wpf.Presenters.Display;

internal static class BlobUiComponentCases
{
    static object Field(object o,string n)=>o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(o)!;
    static object? Call(object o,string n,params object?[] a)=>o.GetType().GetMethod(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(o,a);
    static T Prop<T>(object o,string n)=>(T)o.GetType().GetProperty(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(o)!;
    static void Check(bool b,string m){if(!b)throw new Exception(m);}
    static void Pump(){var f=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>f.Continue=false));Dispatcher.PushFrame(f);}
    static StackPanel host=null!;static Window owner=null!;static int passed;
    static void Case(string label,Action run){host.Children.Clear();run();passed++;Console.WriteLine("PASS "+label);}
    static void Show(FrameworkElement view){host.Children.Add(view);owner.UpdateLayout();Pump();owner.UpdateLayout();Pump();}
    static List<DeclaredFieldEditor> Editors(object o)=>(List<DeclaredFieldEditor>)Field(o,"_editors");
    static double Number(string raw)=>raw.StartsWith("0x",StringComparison.OrdinalIgnoreCase)?Convert.ToInt64(raw[2..],16):double.Parse(raw,System.Globalization.CultureInfo.InvariantCulture);
    static void CheckOriginalRanges(NativeUiDraftRange[] ranges)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_SOURCE_ROOT")!,"docs/collaboration/evidence/BLOB_WHOLE_TOOL_VM_20261031/原厂参数资源索引.json")));
        var original=doc.RootElement.GetProperty("items").EnumerateArray().Where(i=>i.GetProperty("tab").GetString()!.Contains("Result")&&i.GetProperty("kind").GetString()!.EndsWith("Bettween")).Select(i=>XElement.Parse(i.GetProperty("xml").GetString()!)).ToArray();
        Check(original.Length==15,"原证非15范围");
        for(var i=0;i<15;i++)
        {
            var xml=original[i];var leaves=xml.Elements().Where(e=>e.Name.LocalName is "Integers" or "Floats").Single().Elements().ToArray();
            var presenter=Field(ranges[i],"_range");var native=Prop<object>(presenter,"NativeView");
            for(var j=0;j<2;j++)
            {
                var leaf=leaves[j];var suffix=j==0?"Left":"Right";
                Check(Prop<double>(native,"Minimum"+suffix)==Number(leaf.Element("MinValue")!.Value),$"{i}/{j}原min");
                Check(Prop<double>(native,"Maximum"+suffix)==Number(leaf.Element("MaxValue")!.Value),$"{i}/{j}原max");
                var state=Field(ranges[i],j==0?"_lower":"_upper");var setting=Prop<ParameterSetting>(state,"Accepted");
                Check(Convert.ToDouble(((ConstantSetting)setting).Value)==Number(leaf.Element("DefaultValue")!.Value),$"{i}/{j}原默认");
                Check(Prop<double>(native,"SliderTickFrequency")==Number(leaf.Element("IncValue")!.Value),$"{i}/{j}原增量");
            }
            if(xml.Name.LocalName=="FloatBettween")Check(Prop<int>(native,"DecimalDigits")==2,$"{i}原默认精度");
        }
    }
    static int Run()
    {
        new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        owner=new Window{Content=host=new StackPanel(),Left=-20000,Top=-20000,Width=620,Height=900,ShowActivated=false,ShowInTaskbar=false};
        owner.Resources.MergedDictionaries.Add(EditorResources.Create());owner.Show();Pump();
        try
        {
            Case("六原key显隐及恢复基线",()=>{
                using var view=new NativeThresholdUiDraftControls();Show(view);Check(!view.HasUiChanges,"新阈值组误脏");
                var rows=Editors(view);var count=0;view.Changed+=()=>count++;
                var modes=new[]{("BinaryImage",1),("SingleThreshold",2),("DoubleThreshold",2),("AutoThreshold",1),("FixedSoftThreshold",3),("RelativeSoftThreshold",6)};
                foreach(var (mode,n) in modes){Check(view.TrySelectMode(mode),mode);Pump();Check(view.SelectedMode==mode,"选择失败");var visible=rows.Count(r=>r.Visibility==Visibility.Visible)-1+(((FrameworkElement)Field(view,"_softRange")).Visibility==Visibility.Visible?1:0);Check(visible==n,$"{mode}显隐{visible}/{n}");}
                Check(view.HasUiChanges&&count>0,"未观测变化");Check(view.TrySelectMode("SingleThreshold"),"恢复Single");Pump();Check(!view.HasUiChanges,"模式恢复后误脏");
                view.RetireInteractions();Check(!view.HasUiChanges,"退役产生变化");
            });
            Case("外部Single只读投影及seed原文重载保护",()=>{
                using var view=new NativeThresholdUiDraftControls(true);view.ConfigureSingleProjection(()=> (new ConstantSetting("dark"),new ConstantSetting(123L),"raw-seed"));Show(view);
                var low=Editors(view).Single(e=>e.FieldId.EndsWith(".low"));Check(!view.HasUiChanges,"seed原pending误脏");Check(low.Visibility==Visibility.Collapsed&&view.SingleVisibilitySource.Visibility==Visibility.Visible,"外部Single显隐");
                Check(view.TrySelectMode("DoubleThreshold"),"Double");Pump();Check(low.InputElement is TextBox,"无真实数值输入");var text=(TextBox)low.InputElement!;Check(text.Text=="raw-seed","隐藏seed经Resume丢失");text.Text="user-invalid";Pump();Check(view.HasUiChanges&&view.HasPendingEdits,"raw未观测");
                view.Visibility=Visibility.Collapsed;Pump();view.Visibility=Visibility.Visible;Pump();Check(text.Text=="user-invalid","重显覆盖raw");view.RetireInteractions();Check(text.Text=="user-invalid","退役覆盖raw");text.Text="raw-seed";Pump();Check(view.HasPendingEdits&&view.HasInvalidInput,"用户修改后回同raw被seed豁免");
            });
            Case("正式引用只读seed无新增invalid",()=>{
                using var view=new NativeThresholdUiDraftControls(true);
                var reference=new ReferenceSetting(new OutputReference("actual-producer","threshold",DataType.Integer));
                view.ConfigureSingleProjection(()=> (new ConstantSetting("bright"),reference,null));Show(view);
                Check(!view.HasUiChanges&&!view.HasPendingEdits&&!view.HasInvalidInput,"合法引用seed误脏");
                Check(view.TrySelectMode("AutoThreshold"),"Auto");Pump();Check(view.TrySelectMode("SingleThreshold"),"Single");Pump();
                Check(!view.HasUiChanges&&!view.HasInvalidInput,"回Single误封实际引用");
            });
            Case("15组原判定及导航无修改",()=>{
                using var view=new NativeJudgmentUiDraftControls();Show(view);Check(!view.HasUiChanges,"新判定误脏");
                var ranges=((System.Collections.IEnumerable)Field(view,"_ranges")).Cast<NativeUiDraftRange>().ToArray();Check(ranges.Length==15,"非15组");CheckOriginalRanges(ranges);
                foreach(var r in ranges){Check(!r.HasChanges&&!r.HasPendingEdits,"原范围误脏");var low=Field(r,"_lower");var high=Field(r,"_upper");Check(!Prop<bool>(low,"HasInvalidInput")&&!Prop<bool>(high,"HasInvalidInput"),"原默认非法");}
                var advanced=(NativeAdvancedPresenter)Field(view,"_advanced");advanced.IsExpanded=true;Pump();
                view.RetireInteractions();Check(!view.HasUiChanges,"导航/退役误脏");
                var toggle=(DeclaredFieldEditor)Field(ranges[0],"_toggle");Check(toggle.TrySetConstant(true),"开关失败");Pump();Check(view.HasUiChanges,"开关变化漏报");Check(toggle.TrySetConstant(false),"恢复失败");Pump();Check(!view.HasUiChanges,"恢复误脏");
            });
            Case("原范围逐端约束与pending退役",()=>{
                var toggle=new DeclaredFieldEditor(new Port("test.enable",DataType.Boolean,EditModes:FieldEditModes.Constant),"范围",new ConstantSetting(true),_=>{});
                using var view=new NativeUiDraftRange(toggle,"test","数量",0,99999,1,99999,true,1,0,1);Show(view);
                var range=Field(view,"_range");var native=Prop<object>(range,"NativeView");Check(Prop<double>(native,"MinimumLeft")==1&&Prop<double>(native,"MinimumRight")==0,"逐端min错误");
                var text=(TextBox)Call(range,"Input",0)!;text.Text="invalid-range";Pump();Check(view.HasPendingEdits&&view.HasChanges,"范围raw漏报");view.RetireInteractions();Pump();Check(text.Text=="invalid-range"&&view.HasPendingEdits,"范围raw丢失");
            });
            Case("字段退役关闭异常仍恢复且不复活Disposed",()=>{
                using var field=new DeclaredFieldEditor(new Port("choice",DataType.Text,EditModes:FieldEditModes.Constant,Choices:new TextChoiceConstraint(new[]{"a","b"})),"选择",new ConstantSetting("a"),_=>{});Show(field);
                var combo=(ComboBox)field.InputElement!;combo.IsDropDownOpen=true;Pump();Check(combo.IsDropDownOpen,"未打开真实下拉");
                EventHandler fail=(_,_)=>throw new InvalidOperationException("test-close-failure");combo.DropDownClosed+=fail;
                var observed=false;try{field.RetireInteractions();}catch(Exception e){observed=e.ToString().Contains("test-close-failure");}finally{combo.DropDownClosed-=fail;}
                Check(observed,"清理异常未被记录/报告");Check((bool)Field(field,"_presentationVisible"),"关闭异常后未恢复");Check(field.TrySetConstant("b"),"关闭异常后不可用");
                combo=(ComboBox)field.InputElement!;combo.IsDropDownOpen=true;Pump();EventHandler dispose=(_,_)=>field.Dispose();combo.DropDownClosed+=dispose;
                try{field.RetireInteractions();}finally{combo.DropDownClosed-=dispose;}
                Check((bool)Field(field,"_disposed"),"关闭期间未Dispose");
            });
            Case("条件组合基线与变化",()=>{
                var toggle=new DeclaredFieldEditor(new Port("conditional.enable",DataType.Boolean,EditModes:FieldEditModes.Constant),"开关",new ConstantSetting(false),_=>{});
                var field=new DeclaredFieldEditor(new Port("conditional.value",DataType.Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant),"数值",new ConstantSetting(0L),_=>{});
                using var view=new NativeUiConditionalFields(toggle,field);Show(view);Check(!view.HasUiChanges&&field.Visibility==Visibility.Collapsed,"条件基线");
                Check(toggle.TrySetConstant(true),"开启");Pump();Check(view.HasUiChanges&&field.Visibility==Visibility.Visible,"条件开关");field.RetireInteractions();Check(toggle.TrySetConstant(false),"恢复");Pump();Check(!view.HasUiChanges,"条件恢复");
            });
            Case("四缺值显示原眼baseline与退役",()=>{
                using var view=new NativeDisplayUiDraftRows("图形显示",Enumerable.Range(0,4).Select(i=>new NativeDisplayUiDraftEntry("缺值"+i,true)));Show(view);Check(!view.HasUiChanges&&!view.HasPendingEdits,"默认眼true误脏");
                var states=((System.Collections.IEnumerable)Field(view,"_states")).Cast<object>().ToArray();Check(states.Length==20,"非四完整样式");var eye=states[0];Check((bool)Call(eye,"TrySet",new ConstantSetting(false))!,"眼修改失败");Check(view.HasUiChanges,"眼修改漏报");
                Check((bool)Call(eye,"TrySet",new ConstantSetting(true))!,"眼恢复失败");view.RetireInteractions();Check(!view.HasUiChanges,"眼恢复误脏");
            });
            Console.WriteLine($"公共组合{passed}组通过；仅候选UI组件，不替代Blob三页/实际VS验收。");return 0;
        }
        finally{owner.Close();Application.Current.Shutdown();}
    }
    [STAThread]public static int Main(){try{return Run();}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
