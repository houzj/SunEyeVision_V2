using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using SunEyeVision.Plugin.Wpf.Fields;

internal static class S2DocumentEditingCases
{
    private static int _passed;
    [STAThread] private static int Main()
    {
        Thread.CurrentThread.CurrentUICulture=CultureInfo.GetCultureInfo("zh-CN");
        _=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var owner=new Window{Width=680,Height=300,Left=-10000,Top=0,ShowInTaskbar=false};owner.Show();Pump();
        var module=new DeclaredReferenceCandidate("模块整数","producer","flow",new Port("value",DataType.Integer),Array.Empty<string>());
        var index=new DeclaredReferenceCandidate("索引整数","index-source","flow",new Port("index",DataType.Integer),Array.Empty<string>());
        var scalar=new VariableSelectionItem("阈值","参数",1,"变量","77",new VariableReference("solution","scalar",DataType.Integer));
        var sequence=new VariableSelectionItem("数组","参数",2,"数组仅展示首项","10",new VariableReference("solution","sequence",TypeRef.SequenceOf(DataType.Integer,CountConstraint.Exactly(2))));
        var text=new VariableSelectionItem("文本","参数",3,"不适配整数","abc",new VariableReference("solution","text",DataType.Text));
        IReadOnlyList<VariableSelectionItem> rows=new[]{scalar,sequence,text};
        ParameterSetting accepted=new ReferenceSetting(module.Reference(BindingSelection.All));var writes=0;VariableSelectionRequest? request=null;
        using var field=new DeclaredFieldEditor(new Port("value",DataType.Integer,EditModes:FieldEditModes.Reference),"来源",accepted,
            value=>{accepted=value;writes++;},new ReferenceFieldOptions(_=>new(new[]{module},new[]{index},rows),EditableIndex:true,SelectVariable:selection=>request=selection));
        owner.Content=field;Pump();owner.UpdateLayout();var session=Session(field);
        void Open(){Command(session,"DropDownCommand");Command(session,"ClickCommand",Nodes(Property(session,"SourceNodes")).Single(node=>(bool)Property(node,"VariableManager")));}
        Case("匹配变量入口、正式身份选择与原字段单次接纳",()=>
        {
            Open();Require(request is {IsValid:true}&&request.Items.Count==2,"类型筛选或变量入口未建立");
            Require(request.TrySelect(scalar)&&accepted is VariableSetting {Reference.VariableId:"scalar"}&&writes==1,"变量选择写入显示值或未进正式字段会话");
            Require(!field.HasInvalidText&&((string)Property(session,"CurValueA")).Contains("阈值",StringComparison.Ordinal),"变量引用模式或显示名称错误");
        });
        Case("取消与迟到确认不改原来源",()=>
        {
            Open();var old=request!;old.Dispose();Require(!old.TrySelect(sequence)&&writes==1&&accepted is VariableSetting {Reference.VariableId:"scalar"},"取消后迟到确认写入字段");
            Open();var pending=request!;Command(session,"DropDownCommand");Require(!pending.IsValid&&!pending.TrySelect(scalar)&&writes==1,"目录刷新没有退役旧选择请求");
        });
        Case("序列选择只暂存，显式索引修复后再单次写入",()=>
        {
            Open();Require(!request!.TrySelect(sequence)&&writes==1&&field.HasInvalidText&&accepted is VariableSetting {Reference.VariableId:"scalar"},"序列首项被隐式接纳");
            Set(session,"CurValueA","全局变量.数组 [1]");Pump();
            Require(accepted is VariableSetting {Reference.VariableId:"sequence",Reference.Selection:BindingSelection.AtConstant {Index:1}}&&writes==2&&!field.HasInvalidText,"变量索引未通过同一准备/接纳链");
            Command(session,"IndexClickCommand",Nodes(Property(session,"IndexNodes")).Single(node=>Property(node,"Candidate") is DeclaredReferenceCandidate));
            Require(accepted is VariableSetting {Reference.Selection:BindingSelection.AtField {Reference.NodeId:"index-source"}}&&writes==3,"变量动态索引丢失来源身份");
        });
        Case("更名仅更新显示，删除标记失效并保留原身份",()=>
        {
            rows=new[]{scalar,sequence with{Name="数组新名"},text};Command(session,"DropDownCommand");
            Require(((string)Property(session,"CurValueA")).Contains("数组新名",StringComparison.Ordinal)&&writes==3,"更名改写来源或未更新显示");
            rows=Array.Empty<VariableSelectionItem>();Command(session,"DropDownCommand");
            Require(field.HasInvalidText&&accepted is VariableSetting {Reference.VariableId:"sequence"}&&writes==3,"删除来源悄悄转常量或丢失正式身份");
            rows=new[]{scalar,sequence,text};Command(session,"DropDownCommand");Require(!field.HasInvalidText,"当前有效目录恢复后字段仍失效");
        });
        Case("禁用和字段销毁先退役选择请求",()=>
        {
            Open();var disabled=request!;field.IsEnabled=false;Pump();Require(!disabled.IsValid&&!disabled.TrySelect(scalar)&&writes==3,"禁用未隔离选择回调");
            field.IsEnabled=true;Pump();Open();var disposed=request!;field.Dispose();Require(!disposed.IsValid&&!disposed.TrySelect(scalar)&&writes==3,"销毁未退役变量选择");
        });
        ParameterSetting modalAccepted=new ReferenceSetting(module.Reference(BindingSelection.All));var modalWrites=0;
        using var modalField=new DeclaredFieldEditor(new Port("numeric",DataType.Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),"数值",modalAccepted,
            value=>{modalAccepted=value;modalWrites++;},new ReferenceFieldOptions(_=>new(new[]{module},new[]{index},new[]{scalar}),EditableIndex:true));
        owner.Content=modalField;Pump();owner.UpdateLayout();var modalSession=Session(modalField);
        Case("原数值组合经实际Owner模态选择框接纳变量",()=>
        {
            WithDialog(dialog=>
            {
                Require(ReferenceEquals(dialog.Owner,owner)&&modalWrites==0,"选择框归属错误或选择前已写入");
                ((ListView)dialog.FindName("globalVarList")).SelectedIndex=0;
                Visual(dialog).OfType<Button>().Single(button=>Equals(button.Content,dialog.TryFindResource("Confirm"))).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            },()=>{Command(modalSession,"DropDownCommand");Command(modalSession,"ClickCommand",Nodes(Property(modalSession,"SourceNodes")).Single(node=>(bool)Property(node,"VariableManager")));});
            Require(modalWrites==1&&modalAccepted is VariableSetting {Reference.VariableId:"scalar"}&&!modalField.HasInvalidText,"原数值组合未完成变量模态接纳");
            Require(!Application.Current.Windows.OfType<VariableSelectionDialog>().Any(dialog=>dialog.IsVisible),"模态框关闭后仍存活");
        });
        Case("展开订阅窗的变量入口转交同一Owner模态，关闭旧借用不误退役新选择",()=>
        {
            Command(modalSession,"PopSubscribeWindowCommand");Pump();
            var expanded=Application.Current.Windows.OfType<Window>().Single(window=>window.IsVisible&&window.GetType().Name=="ParamSubscribeWindow");
            var expansion=((ReferenceWindowOwner)expanded.DataContext).CurrentRequest!;
            Require(expansion.IsValid&&ReferenceEquals(expanded.Owner,owner),"展开窗没有有效Owner租约");
            var manager=Nodes(expansion.SourceNodes).Single(node=>(bool)Property(node,"VariableManager"));
            WithDialog(dialog=>
            {
                Require(ReferenceEquals(dialog.Owner,owner)&&!expanded.IsVisible&&!expansion.IsValid,"旧展开窗未退役或新变量框归属错误");
                ((ListView)dialog.FindName("globalVarList")).SelectedIndex=0;
                Visual(dialog).OfType<Button>().Single(button=>Equals(button.Content,dialog.TryFindResource("Confirm"))).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            },()=>expansion.ClickCommand.Execute(manager));
            Require(modalWrites==1&&!modalField.HasInvalidText&&modalAccepted is VariableSetting {Reference.VariableId:"scalar"},"展开转交未接纳或重复写入相同来源");
            Require(!expansion.ClickCommand.CanExecute(manager),"退役展开窗仍能触发变量来源选择");
        });
        Case("Owner关闭取消真实模态，旧来源保持",()=>
        {
            var before=modalAccepted;
            WithDialog(_=>owner.Close(),()=>{Command(modalSession,"DropDownCommand");Command(modalSession,"ClickCommand",Nodes(Property(modalSession,"SourceNodes")).Single(node=>(bool)Property(node,"VariableManager")));});
            Require(modalWrites==1&&ReferenceEquals(before,modalAccepted),"Owner关闭时接纳了未确认选择");
        });
        Case("删除旧来源后可直接换有效变量，接纳后清除旧诊断",()=>
        {
            IReadOnlyList<VariableSelectionItem> recoveryRows=new[]{scalar};
            VariableSelectionRequest? recovery=null;var acceptedRecovery=(ParameterSetting)new VariableSetting(sequence.Reference);var recoveryWrites=0;
            var recoveryOwner=new Window{Width=680,Height=300,Left=-10000,ShowInTaskbar=false};
            using var recoveryField=new DeclaredFieldEditor(new Port("recovery",DataType.Integer,EditModes:FieldEditModes.Reference),"失效恢复",acceptedRecovery,
                value=>{acceptedRecovery=value;recoveryWrites++;},new ReferenceFieldOptions(_=>new(Array.Empty<DeclaredReferenceCandidate>(),Array.Empty<DeclaredReferenceCandidate>(),recoveryRows),EditableIndex:true,SelectVariable:value=>recovery=value));
            recoveryOwner.Content=recoveryField;recoveryOwner.Show();Pump();var recoverySession=Session(recoveryField);
            Command(recoverySession,"DropDownCommand");Require(recoveryField.HasInvalidText&&recoveryWrites==0,"删除前提没有标记来源失效");
            Command(recoverySession,"ClickCommand",Nodes(Property(recoverySession,"SourceNodes")).Single(node=>(bool)Property(node,"VariableManager")));
            Require(recovery!.TrySelect(scalar)&&acceptedRecovery is VariableSetting {Reference.VariableId:"scalar"}&&recoveryWrites==1&&!recoveryField.HasInvalidText,
                "有效新变量已接纳但旧来源诊断未清理，或旧诊断阻断重新选择");
            recoveryRows=new[]{sequence};Command(recoverySession,"DropDownCommand");Require(recoveryField.HasInvalidText,"旧标量删除没有产生来源失效");
            Command(recoverySession,"ClickCommand",Nodes(Property(recoverySession,"SourceNodes")).Single(node=>(bool)Property(node,"VariableManager")));
            Require(!recovery!.TrySelect(sequence)&&recoveryWrites==1,"序列来源未经索引修复已接纳");
            Set(recoverySession,"CurValueA","全局变量.数组 [1]");Pump();
            Require(acceptedRecovery is VariableSetting {Reference.VariableId:"sequence",Reference.Selection:BindingSelection.AtConstant {Index:1}}
                &&recoveryWrites==2&&!recoveryField.HasInvalidText,"旧来源诊断阻断新序列索引修复或未原子清理");
            recoveryOwner.Close();
        });
        Case("范围端点失效后换变量走完整宿主预检，拒绝不半写",()=>
        {
            VariableSelectionRequest? replacement=null;var reject=true;var rangeWrites=0;var low=new VariableSetting(sequence.Reference);
            var rangeOwner=new Window{Width=680,Height=300,Left=-10000,ShowInTaskbar=false};
            var ports=new[]{new Port("low",DataType.Integer,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),new Port("high",DataType.Integer,EditModes:FieldEditModes.Constant|FieldEditModes.Reference)};
            using var range=new DeclaredFieldsView(ports.Select((port,i)=>(port,new FieldPresentation(port.Id,"范围","范围",i,FieldEditorKind.Scalar)
                {Layout=new FieldLayout("参数","范围",PairWith:i==0?"high":null,Minimum:0,Maximum:1000,DecimalPlaces:0)},(ParameterSetting)(i==0?low:new ConstantSetting(100L)))),
                _=>{if(reject)throw new InvalidOperationException("宿主拒绝新来源");rangeWrites++;},
                new ReferenceFieldOptions(_=>new(Array.Empty<DeclaredReferenceCandidate>(),Array.Empty<DeclaredReferenceCandidate>(),new[]{scalar}),EditableIndex:true,SelectVariable:value=>replacement=value));
            rangeOwner.Content=range;rangeOwner.Show();Pump();rangeOwner.UpdateLayout();
            var presenter=((IEnumerable)typeof(DeclaredFieldsView).GetField("_ranges",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(range)!).Cast<object>().Single();
            var reference=((Array)presenter.GetType().GetField("_references",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(presenter)!).GetValue(0)!;
            void Choose(){Command(reference,"DropDownCommand");Command(reference,"ClickCommand",Nodes(Property(reference,"SourceNodes")).Single(node=>(bool)Property(node,"VariableManager")));}
            Choose();Require(range.Fields.Single(item=>item.FieldId=="low").HasInvalidText,"范围缺少旧来源失效前提");
            Require(!replacement!.TrySelect(scalar)&&rangeWrites==0&&range.Fields.Single(item=>item.FieldId=="low").Setting is VariableSetting {Reference.VariableId:"sequence"},"拒绝来源仍写入范围端点");
            reject=false;Choose();Require(replacement!.TrySelect(scalar)&&rangeWrites==1&&!range.Fields.Single(item=>item.FieldId=="low").HasInvalidText
                &&range.Fields.Single(item=>item.FieldId=="high").Setting is ConstantSetting {Value:100L},"范围旧诊断阻止新来源或污染另一端");
            rangeOwner.Close();
        });
        Case("变量索引用途固定，仅筛整数标量，取消不替换模块或变量主来源",()=>
        {
            foreach(var variableSource in new[]{false,true})
            {
                var source=new DeclaredReferenceCandidate("模块数组","array-producer","flow",new Port("values",TypeRef.SequenceOf(DataType.Integer,CountConstraint.Exactly(2))),Array.Empty<string>());
                ParameterSetting current=variableSource?new VariableSetting(new VariableReference("solution","sequence",sequence.Reference.Type,new BindingSelection.AtConstant(0)))
                    :new ReferenceSetting(source.Reference(new BindingSelection.AtConstant(0)));
                var changes=0;VariableSelectionRequest? indexRequest=null;var indexOwner=new Window{Width=680,Height=300,Left=-10000,ShowInTaskbar=false};
                using var indexField=new DeclaredFieldEditor(new Port("indexed",DataType.Integer,EditModes:FieldEditModes.Reference),"索引",current,
                    value=>{current=value;changes++;},new ReferenceFieldOptions(_=>new(new[]{source},Array.Empty<DeclaredReferenceCandidate>(),new[]{scalar,sequence,text}),EditableIndex:true,SelectVariable:value=>indexRequest=value));
                indexOwner.Content=indexField;indexOwner.Show();Pump();var indexSession=Session(indexField);Command(indexSession,"DropDownCommand");
                var manager=Nodes(Property(indexSession,"IndexNodes")).Single(node=>(bool)Property(node,"VariableManager"));
                Command(indexSession,"IndexClickCommand",manager);Require(indexRequest is {IsIndex:true}&&indexRequest.Items.Count==1,"索引复用了主来源候选或混入非整数/序列变量");
                var cancelled=indexRequest!;cancelled.Dispose();Require(!cancelled.TrySelect(scalar)&&changes==0,"取消索引请求改变主来源");
                Command(indexSession,"IndexClickCommand",manager);Require(indexRequest!.TrySelect(scalar)&&changes==1&&!indexField.HasInvalidText,"变量索引没有进入正式字段接纳");
                Require(variableSource?current is VariableSetting {Reference.VariableId:"sequence",Reference.Selection:BindingSelection.AtVariable {Reference.VariableId:"scalar"}}
                    :current is ReferenceSetting {Reference.NodeId:"array-producer",Reference.Selection:BindingSelection.AtVariable {Reference.VariableId:"scalar"}},"变量索引替换了主来源或丢失正式身份");
                Require(((string)Property(indexSession,"CurValueA")).Contains("阈值",StringComparison.Ordinal),"变量索引未使用声明显示名");indexOwner.Close();
            }
        });
        Case("真实索引变量模态确认保留主来源，Owner隐藏取消且不重写",()=>
        {
            var source=new DeclaredReferenceCandidate("模块数组","array-owner","flow",new Port("values",TypeRef.SequenceOf(DataType.Integer,CountConstraint.Exactly(2))),Array.Empty<string>());
            ParameterSetting selected=new ReferenceSetting(source.Reference(new BindingSelection.AtConstant(0)));var writesIndex=0;
            var indexOwner=new Window{Width=680,Height=300,Left=-10000,ShowInTaskbar=false};
            using var actual=new DeclaredFieldEditor(new Port("actual-index",DataType.Integer,EditModes:FieldEditModes.Reference),"实际索引",selected,
                value=>{selected=value;writesIndex++;},new ReferenceFieldOptions(_=>new(new[]{source},Array.Empty<DeclaredReferenceCandidate>(),new[]{scalar,sequence,text}),EditableIndex:true));
            indexOwner.Content=actual;indexOwner.Show();Pump();var sessionIndex=Session(actual);
            void OpenIndex(){Command(sessionIndex,"DropDownCommand");Command(sessionIndex,"IndexClickCommand",Nodes(Property(sessionIndex,"IndexNodes")).Single(node=>(bool)Property(node,"VariableManager")));}
            WithDialog(dialog=>
            {
                Require(ReferenceEquals(dialog.Owner,indexOwner),"实际索引模态Owner不正确");var list=(ListView)dialog.FindName("globalVarList");
                Require(list.Items.Count==1,"索引模态混入非标量或非整数变量");list.SelectedIndex=0;
                Visual(dialog).OfType<Button>().Single(button=>Equals(button.Content,dialog.TryFindResource("Confirm"))).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            },OpenIndex);
            Require(writesIndex==1&&!actual.HasInvalidText&&selected is ReferenceSetting {Reference.NodeId:"array-owner",Reference.Selection:BindingSelection.AtVariable {Reference.VariableId:"scalar"}},"实际模态未完成索引接纳或替换主来源");
            WithDialog(_=>indexOwner.Hide(),OpenIndex);Require(writesIndex==1,"Owner隐藏时确认了未接纳索引");indexOwner.Close();
        });
        Case("变量索引更名删除和目录退役不改主来源，显式重选恢复",()=>
        {
            foreach(var variableSource in new[]{false,true})
            {
                var source=new DeclaredReferenceCandidate("模块数组","refresh-array","flow",new Port("values",sequence.Reference.Type),Array.Empty<string>());
                ParameterSetting selected=variableSource
                    ?new VariableSetting(new VariableReference("solution","sequence",sequence.Reference.Type,new BindingSelection.AtVariable(scalar.Reference)))
                    :new ReferenceSetting(source.Reference(new BindingSelection.AtVariable(scalar.Reference)));
                var changes=0;VariableSelectionRequest? pending=null;
                IReadOnlyList<VariableSelectionItem> available=new[]{scalar,sequence};
                using var refreshField=new DeclaredFieldEditor(new Port("refresh-index",DataType.Integer,EditModes:FieldEditModes.Reference),"索引刷新",selected,
                    value=>{selected=value;changes++;},new ReferenceFieldOptions(_=>new(new[]{source},Array.Empty<DeclaredReferenceCandidate>(),available),EditableIndex:true,SelectVariable:value=>pending=value));
                var refreshOwner=new Window{Width=680,Height=300,Left=-10000,ShowInTaskbar=false,Content=refreshField};
                refreshOwner.Show();Pump();var refreshSession=Session(refreshField);
                try
                {
                    available=new[]{scalar with{Name="索引新名"},sequence};Command(refreshSession,"DropDownCommand");
                    Require(((string)Property(refreshSession,"CurValueA")).Contains("索引新名",StringComparison.Ordinal)&&changes==0&&!refreshField.HasInvalidText,"索引更名写配置或未更新标签");
                    Command(refreshSession,"IndexClickCommand",Nodes(Property(refreshSession,"IndexNodes")).Single(node=>(bool)Property(node,"VariableManager")));
                    var stale=pending!;available=new[]{sequence};Command(refreshSession,"DropDownCommand");
                    Require(!stale.IsValid&&!stale.TrySelect(scalar)&&changes==0&&refreshField.HasInvalidText,"索引删除未标失效或旧目录仍可写入");
                    var replacement=scalar with{Name="新索引",Reference=new VariableReference("solution","replacement-index",DataType.Integer)};
                    available=new[]{replacement,sequence};Command(refreshSession,"DropDownCommand");
                    Command(refreshSession,"IndexClickCommand",Nodes(Property(refreshSession,"IndexNodes")).Single(node=>(bool)Property(node,"VariableManager")));
                    Require(pending!.TrySelect(replacement)&&changes==1&&!refreshField.HasInvalidText,"有效索引重选没有清除旧诊断");
                    if(variableSource)Require(selected is VariableSetting {Reference.VariableId:"sequence",Reference.Selection:BindingSelection.AtVariable {Reference.VariableId:"replacement-index"}},"索引修复替换变量主来源");
                    else Require(selected is ReferenceSetting {Reference.NodeId:"refresh-array",Reference.Selection:BindingSelection.AtVariable {Reference.VariableId:"replacement-index"}},"索引修复替换模块主来源");
                }
                finally{refreshOwner.Close();}
            }
        });
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"variable-fields-report.json"),JsonSerializer.Serialize(new{passed=_passed,fullB08Acceptance=false,
            scope="真实公共字段与原数值组合的变量选择/索引/失效/Owner生命周期；不代完整Blob确认执行、VS与人工"}));
        Console.WriteLine($"变量来源字段视觉链：{_passed}组通过；完整B08和Blob验收未完成。");return 0;
    }
    private static void WithDialog(Action<VariableSelectionDialog> action,Action open)
    {
        Exception? failure=null;var called=false;var until=DateTime.UtcNow.AddSeconds(10);
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
        timer.Tick+=(_,_)=>
        {
            var dialog=Application.Current.Windows.OfType<VariableSelectionDialog>().SingleOrDefault(window=>window.IsVisible);
            if(dialog is null){if(DateTime.UtcNow>until){timer.Stop();foreach(var window in Application.Current.Windows.OfType<VariableSelectionDialog>().ToArray())window.Retire();failure=new TimeoutException("变量模态未打开");}return;}
            timer.Stop();called=true;
            try{action(dialog);}catch(Exception error){failure=error;dialog.Retire();}
        };
        timer.Start();try{open();Pump();}finally{timer.Stop();}
        if(failure is not null)throw failure;Require(called,"未进入实际变量选择模态");
    }
    private static object Session(DependencyObject field)=>Visual(field).OfType<FrameworkElement>().Select(element=>element.DataContext)
        .FirstOrDefault(value=>value?.GetType().Name=="ReferenceFieldSession")??throw new Exception("未组装真实引用会话");
    private static object Property(object item,string name)=>item.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(item)!;
    private static void Set(object item,string name,object value)=>item.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.SetValue(item,value);
    private static void Command(object session,string name,object? argument=null)=>((ICommand)Property(session,name)).Execute(argument);
    private static IEnumerable<object> Nodes(object roots)
    {foreach(var node in (IEnumerable)roots){yield return node;foreach(var child in Nodes(Property(node,"EnumEntrysA")))yield return child;}}
    private static IEnumerable<DependencyObject> Visual(DependencyObject root)
    {
        var seen=new HashSet<DependencyObject>();var pending=new Stack<DependencyObject>();pending.Push(root);
        while(pending.Count>0)
        {
            var node=pending.Pop();if(!seen.Add(node))continue;yield return node;
            foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())pending.Push(child);
            if(node is System.Windows.Media.Visual)for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)pending.Push(VisualTreeHelper.GetChild(node,i));
        }
    }
    private static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Case(string name,Action check){check();_passed++;Console.WriteLine("通过："+name);}
}
