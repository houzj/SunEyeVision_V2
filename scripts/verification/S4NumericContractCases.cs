using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;

internal static class S2DocumentEditingCases
{
    private static readonly List<object> Rows=new();
    [STAThread] private static int Main()
    {
        try
        {
            if(Application.Current is null)new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Themes/Dark/DarkTheme.xaml"})
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/SunEyeVision.UI;component/"+resource)});
            foreach(var source in new[]{.125,.135,0d,1d}){Scalar(source);Range(source,0);Range(source,1);}
            ScalarRejected();ScalarProposalRejected();ScalarInvalid();ScalarRetired();RangeRetired(0);RangeRetired(1);
            foreach(var raw in new[]{"1.004","-0.004"}){RawBounds(raw,false);RawBounds(raw,true);RawBounds(raw,true,1);}
            RawEmpty(false);RawEmpty(true);RawEmpty(true,1);AtomicBounds();
            if(Environment.GetEnvironmentVariable("S4_NUMERIC_LOADED")=="1"){Loaded(false,false);Loaded(true,false);Loaded(false,true);Loaded(true,true);Loaded(true,true,1);DesignBounds();}
            var file=System.IO.Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"numeric-contract-report.json");
            System.IO.File.WriteAllText(file,JsonSerializer.Serialize(Rows,new JsonSerializerOptions{WriteIndented=true}));
            if(Rows.Any(row=>!(bool)row.GetType().GetProperty("Passed")!.GetValue(row)!))return 1;
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static Port Port(string id)=>new(id,DataType.Number,EditModes:FieldEditModes.Constant|FieldEditModes.Reference);
    private static FieldPresentation Presentation(string id,string? pair=null)=>new(id,id,"数值",0,FieldEditorKind.Scalar)
        {Layout=new FieldLayout("参数","数值",PairWith:pair,Minimum:0,Maximum:1,DecimalPlaces:2,Increment:.01)};
    private static ReferenceFieldOptions Options(double value)=>new(_=>ReferenceCandidates.Empty,ReadCurrentValue:_=>new ConstantSetting(value));
    private static ParameterSetting Initial()=>new ReferenceSetting(new OutputReference("producer","out",DataType.Number));
    private static void Scalar(double source)
    {
        var writes=0;
        using var field=new DeclaredFieldEditor(Port("scalar"),"标量",Initial(),_=>writes++,Options(source),presentation:Presentation("scalar"));
        Prepare(field);Switch(field,0);
        Record("scalar",source,field.Setting,writes,field.HasInvalidText,field.Error,field);
    }
    private static void Range(double source,int endpoint)
    {
        var writes=0;var ports=new[]{Port("low"),Port("high")};
        using var fields=new DeclaredFieldsView(ports.Select((p,i)=>(p,Presentation(p.Id,i==0?"high":null),i==endpoint?Initial():(ParameterSetting)new ConstantSetting(i==0?0d:1d))),_=>writes++,Options(source));
        Prepare(fields);
        var ranges=(IEnumerable)typeof(DeclaredFieldsView).GetField("_ranges",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(fields)!;
        var presenter=(FrameworkElement)ranges.Cast<object>().Single();
        Prepare(presenter);Switch(presenter,endpoint);
        var field=fields.Fields.Single(f=>f.FieldId==ports[endpoint].Id);
        Record(endpoint==0?"range-left":"range-right",source,field.Setting,writes,field.HasInvalidText,field.Error,presenter);
    }
    private static void Check(string name,bool passed,object detail)
    {Rows.Add(new{Name=name,Passed=passed,Detail=detail});Console.WriteLine((passed?"通过":"失败")+"："+name+"，"+JsonSerializer.Serialize(detail));}
    private static void ScalarRejected()
    {
        var reject=true;var writes=0;
        using var field=new DeclaredFieldEditor(Port("reject"),"宿主拒绝",Initial(),_=>{if(reject)throw new InvalidOperationException("宿主拒绝");writes++;},Options(.125),presentation:Presentation("reject"));
        Prepare(field);Switch(field,0);
        Check("舍入提案宿主拒绝不半写",field.Setting is ReferenceSetting&&writes==0&&field.HasInvalidText,new{writes,field.Error});
        reject=false;Switch(field,0);
        Check("拒绝后再次解绑可单次接纳",field.Setting is ConstantSetting {Value:.12d}&&writes==1&&!field.HasInvalidText,new{writes,field.Error});
    }
    private static void ScalarProposalRejected()
    {
        var writes=0;
        var options=Options(.125) with {ValidateProposal=(_,value)=>value is ConstantSetting?"提案资格拒绝":null};
        using var field=new DeclaredFieldEditor(Port("proposal"),"提案拒绝",Initial(),_=>writes++,options,presentation:Presentation("proposal"));
        Prepare(field);Switch(field,0);
        Check("提案拒绝保留引用且不调用写入",field.Setting is ReferenceSetting&&writes==0&&field.HasInvalidText,new{writes,field.Error});
    }
    private static void ScalarInvalid()
    {
        var writes=0;
        using var field=new DeclaredFieldEditor(Port("invalid"),"越界",Initial(),_=>writes++,Options(1.02),presentation:Presentation("invalid"));
        Prepare(field);Switch(field,0);
        Check("解绑越界当前值保留引用",field.Setting is ReferenceSetting&&writes==0&&field.HasInvalidText,new{writes,field.Error});
    }
    private static void ScalarRetired()
    {
        DeclaredFieldEditor? field=null;var writes=0;
        field=new DeclaredFieldEditor(Port("retired"),"退役",Initial(),_=>{writes++;field!.Dispose();},Options(.125),presentation:Presentation("retired"));
        Prepare(field);var notifications=0;field.Changed+=()=>notifications++;
        Switch(field,0);
        Check("宿主接纳同步关闭后不继续通知",writes==1&&notifications==0,new{writes,notifications});
        field.Dispose();
    }
    private static void RangeRetired(int endpoint)
    {
        var writes=0;var notifications=0;DeclaredFieldsView? fields=null;
        fields=new DeclaredFieldsView(new[]{Port("low"),Port("high")}.Select((port,i)=>(port,Presentation(port.Id,i==0?"high":null),i==endpoint?Initial():(ParameterSetting)new ConstantSetting(i==0?0d:1d))),_=>{writes++;fields!.Dispose();},Options(.125));
        Prepare(fields);
        var presenter=(FrameworkElement)((IEnumerable)typeof(DeclaredFieldsView).GetField("_ranges",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(fields)!).Cast<object>().Single();
        fields.Fields[endpoint].Changed+=()=>notifications++;
        Switch(presenter,endpoint);
        Check("范围"+endpoint+"宿主接纳同步关闭后不继续通知",writes==1&&notifications==0,new{writes,notifications});
        fields.Dispose();
    }
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Loaded(bool range,bool queued,int endpoint=0)
    {
        var writes=0;var accepted=.2d;var reject=false;var id=endpoint==0?"low":"high";
        using var fields=new DeclaredFieldsView(new[]{Port("low"),Port("high")}.Take(range?2:1).Select((port,i)=>(port,Presentation(port.Id,range&&i==0?"high":null),(ParameterSetting)new ConstantSetting(i==endpoint?.2d:i==0?0d:1d))),values=>{if(reject)throw new InvalidOperationException("Loaded宿主拒绝");accepted=(double)((ConstantSetting)values[id]).Value!;writes++;},Options(.125));
        var stack=new StackPanel();stack.Children.Add(fields);var next=new Button{Content="焦点目标"};stack.Children.Add(next);
        var window=new Window{Width=650,Height=300,Content=stack,Title="数值禁用迟到验证"};
        try
        {
            window.Show();Pump();window.UpdateLayout();
            var field=fields.Fields.Single(f=>f.FieldId==id);
            var text=(TextBox)field.InputElement!;
            text.Focus();Keyboard.Focus(text);Pump();
            var focused=text.IsKeyboardFocused;
            text.Text="0.45";var beforeTransfer=writes;
            if(queued)
                text.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,Environment.TickCount,text,next){RoutedEvent=Keyboard.LostKeyboardFocusEvent});
            else next.Focus();
            var beforeDisable=writes;var valueBeforeDisable=accepted;
            fields.IsEnabled=false;var afterDisable=writes;var valueAfterDisable=accepted;
            if(queued)
            {
                var native=Desc(fields).OfType<SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.NumericControl>().ElementAt(endpoint);
                ((RepeatButton)native.FindName("UpButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                fields.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});
            }
            Pump();var afterPump=writes;
            var passed=focused&&fields.IsLoaded&&afterPump==beforeDisable&&(!queued||beforeDisable==0);
            Check((range?"range":"scalar")+(queued?"真实Loaded路由失焦排队后禁用":"真实焦点转移后禁用时序"),passed,
                new{focused,fields.IsLoaded,Endpoint=endpoint,InjectedLostKeyboardFocus=queued,beforeTransfer,beforeDisable,valueBeforeDisable,afterDisable,valueAfterDisable,afterPump,accepted,Raw=text.Text,Dpi=VisualTreeHelper.GetDpi(window).PixelsPerInchX});
            fields.IsEnabled=true;Pump();
            Check((range?"range":"scalar")+(queued?"排队禁用恢复原文":"实际失焦恢复不重复写入"),text.Text=="0.45"&&writes==beforeDisable,new{Raw=text.Text,writes,beforeDisable});
            if(queued)
            {
                Check((range?"range":"scalar")+id+"恢复后显式提交单次接纳",field.TryCommitPending()&&writes==1&&accepted==.45d,new{writes,accepted});
                text.Focus();Keyboard.Focus(text);Pump();text.Text="0.55";
                text.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,Environment.TickCount,text,next){RoutedEvent=Keyboard.LostKeyboardFocusEvent});
                stack.Children.Remove(fields);Pump();
                Check((range?"range":"scalar")+id+"卸载拒绝排队写入",!fields.IsLoaded&&writes==1&&accepted==.45d,new{writes,accepted,fields.IsLoaded});
                stack.Children.Insert(0,fields);Pump();
                reject=true;var refused=field.TryCommitPending();
                Check((range?"range":"scalar")+id+"重载原文及宿主拒绝不半写",text.Text=="0.55"&&!refused&&writes==1&&accepted==.45d,new{writes,accepted,field.Error,Raw=text.Text});
                reject=false;var committed=field.TryCommitPending();
                Check((range?"range":"scalar")+id+"拒绝修复后显式提交",committed&&writes==2&&accepted==.55d,new{writes,accepted,field.Error});
            }
        }
        finally{window.Close();Pump();}
    }
    private static void RawBounds(string raw,bool range,int endpoint=0)
    {
        var writes=0;var accepted=.25d;
        using var fields=new DeclaredFieldsView(new[]{Port("low"),Port("high")}.Take(range?2:1).Select((port,i)=>(port,Presentation(port.Id,range&&i==0?"high":null),(ParameterSetting)new ConstantSetting(i==endpoint?.25d:i==0?0d:1d))),values=>{writes++;accepted=(double)((ConstantSetting)values[endpoint==0?"low":"high"]).Value!;},Options(.125));
        Prepare(fields);var field=fields.Fields[endpoint];var text=(TextBox)field.InputElement!;
        var leaf=Desc(fields).OfType<SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.NumericControl>().ElementAt(endpoint);
        var original=new SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.RangeRule{Container=leaf}.Validate(raw,System.Globalization.CultureInfo.InvariantCulture);
        text.Text=raw;var result=field.TryCommitPending();
        Check((range?"range":"scalar")+"原始越界不可借舍入进入范围"+raw,!original.IsValid&&!result&&writes==0&&accepted==.25d&&text.Text==raw,
            new{Endpoint=endpoint,Raw=raw,OriginalRangeRuleValid=original.IsValid,result,writes,accepted,RetainedRaw=text.Text,field.Error,fields.IsLoaded,HasPresentationSource=PresentationSource.FromVisual(fields)!=null});
        text.Text="0.376";var repaired=field.TryCommitPending();
        Check((range?"range":"scalar")+endpoint+"越界原文修复后正常舍入",repaired&&writes==1&&accepted==.38d&&!field.HasInvalidText,new{writes,accepted,field.Error});
        text.Text="1";var max=field.TryCommitPending()&&accepted==1d;text.Text="0";var min=field.TryCommitPending()&&accepted==0d;
        Check((range?"range":"scalar")+endpoint+"绝对上下端点可接纳",max&&min&&writes==3,new{writes,accepted});
    }
    private static void RawEmpty(bool range,int endpoint=0)
    {
        var writes=0;
        using var fields=new DeclaredFieldsView(new[]{Port("low"),Port("high")}.Take(range?2:1).Select((port,i)=>(port,Presentation(port.Id,range&&i==0?"high":null),(ParameterSetting)new ConstantSetting(i==endpoint?.25d:i==0?0d:1d))),_=>writes++,Options(.125));
        Prepare(fields);var field=fields.Fields[endpoint];var text=(TextBox)field.InputElement!;text.Text="";
        var committed=field.TryCommitPending();
        Check((range?"range":"scalar")+endpoint+"必填空原文不隐式变零",!committed&&writes==0&&field.Setting is ConstantSetting {Value:.25d}&&text.Text==""&&field.HasInvalidText,new{writes,field.Error,Raw=text.Text});
    }
    private static void AtomicBounds()
    {
        var writes=0;var low=.2d;var high=.4d;
        using var fields=new DeclaredFieldsView(new[]{Port("low"),Port("high")}.Select((port,i)=>(port,Presentation(port.Id,i==0?"high":null),(ParameterSetting)new ConstantSetting(i==0?low:high))),values=>{writes++;low=(double)((ConstantSetting)values["low"]).Value!;high=(double)((ConstantSetting)values["high"]).Value!;},Options(.125));
        Prepare(fields);var left=(TextBox)fields.Fields[0].InputElement!;var right=(TextBox)fields.Fields[1].InputElement!;
        left.Text="0.604";right.Text="0.804";var accepted=fields.TryValidate(out var error);
        Check("双端舍入提案不被另一端旧动态值拒绝",accepted&&writes==1&&low==.6d&&high==.8d,new{writes,low,high,error});
        left.Text="0.404";right.Text="1.004";var rejected=!fields.TryValidate(out error);
        Check("整组绝对越界拒绝且两端原文保持",rejected&&writes==1&&low==.6d&&high==.8d&&left.Text=="0.404"&&right.Text=="1.004",new{writes,low,high,error,Left=left.Text,Right=right.Text});
    }
    private static void DesignBounds()
    {
        var sample=SunEyeVision.Tool.BlobAnalysis.Wpf.Design.BlobDesignData.CreateParameterContext();
        using var presenter=new SunEyeVision.Plugin.Wpf.Presenters.Parameters.DeclaredSettingsView();presenter.Bind(sample.Draft,sample.Options);presenter.SelectedPageIndex=1;
        var fields=(DeclaredFieldsView)presenter.Content;
        var tool=new SunEyeVision.Tool.BlobAnalysis.Runtime.BlobTool();var names=((IToolParameterBindingNames)tool).ParameterBindingNames;
        foreach(var label in new[]{"圆度使能","矩形度使能"})
        {
            var enable=fields.Fields.Single(f=>f.FieldId==sample.Draft.Schema.Presentation!.Fields.Single(p=>p.DisplayName==label).FieldId);
            if(!enable.TrySetConstant(true)||enable.Setting is not ConstantSetting {Value:true})throw new InvalidOperationException("设计上下文筛选开启前置失败："+enable.Error);
        }
        var window=new Window{Width=650,Height=650,Content=presenter,Title="真实设计上下文数值界限验证"};
        try
        {
        window.Show();Pump();window.UpdateLayout();fields.SelectedPageIndex=1;Pump();
        var advanced=Desc(presenter).OfType<SunEyeVision.Plugin.Wpf.Presenters.Parameters.NativeAdvancedPresenter>().ToArray();
        if(advanced.Length==0)throw new InvalidOperationException("设计上下文正式高级展开控件前置缺失。");
        foreach(var section in advanced)section.IsExpanded=true;Pump();
        foreach(var name in new[]{"MinCircularity","MaxCircularity","MinRectangularity","MaxRectangularity"})
        {
            var id=names.Single(pair=>pair.Value==name).Key;var field=fields.Fields.Single(f=>f.FieldId==id);var original=field.Setting;
            field.View.BringIntoView();Pump();
            var text=(TextBox)field.InputElement!;
            if(!text.IsLoaded||!text.IsVisible)throw new InvalidOperationException("设计字段Loaded/可见/筛选开启前置失败："+name);
            var raw=name.StartsWith("Min")?"-0.004":"1.004";text.Text=raw;
            var result=field.TryCommitPending();var current=sample.Draft.CaptureFields().Read(id);
            Check("真实设计上下文原文界限"+name,!result&&current is ConstantSetting c&&original is ConstantSetting previous&&Equals(c.Value,previous.Value)&&text.Text==raw&&field.HasInvalidText,
                new{result,Raw=text.Text,field.Error,text.IsLoaded,text.IsVisible,ViewVisibility=field.View.Visibility.ToString(),Windows=Application.Current.Windows.Count,HasPresentationSource=PresentationSource.FromVisual(fields)!=null});
            text.Text=Convert.ToString(((ConstantSetting)original).Value,System.Globalization.CultureInfo.InvariantCulture)!;field.TryCommitPending();
        }
        }
        finally{window.Close();Pump();}
    }
    private static void Prepare(FrameworkElement element)
    {element.Measure(new Size(650,500));element.Arrange(new Rect(0,0,650,500));element.UpdateLayout();foreach(var item in Desc(element).OfType<Control>().ToArray())item.ApplyTemplate();element.UpdateLayout();}
    private static void Switch(DependencyObject root,int endpoint)
    {
        var path=endpoint==0?"IsSubscribeLeft":"IsSubscribeRight";
        var button=Desc(root).OfType<ToggleButton>().Single(b=>BindingOperations.GetBinding(b,ToggleButton.IsCheckedProperty)?.Path?.Path==path);
        button.SetCurrentValue(ToggleButton.IsCheckedProperty,false);
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    }
    private static IEnumerable<DependencyObject> Desc(DependencyObject root)
    {yield return root;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Desc(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private static void Record(string kind,double source,ParameterSetting accepted,int writes,bool invalid,string error,FrameworkElement root)
    {
        var expected=Math.Round(source,2,MidpointRounding.ToEven);
        var passed=accepted is ConstantSetting {Value:double actual}&&actual==expected&&writes==1&&!invalid;
        Rows.Add(new{Kind=kind,Source=source,Expected=expected,Accepted=accepted.ToString(),AcceptedValue=(accepted as ConstantSetting)?.Value,Writes=writes,Invalid=invalid,Error=error,root.IsLoaded,root.IsVisible,HasPresentationSource=PresentationSource.FromVisual(root)!=null,Windows=Application.Current.Windows.Count,Passed=passed});
        Console.WriteLine((passed?"通过":"失败")+"：同源解绑 "+kind+"，写入="+writes+"，错误="+error);
    }
}

