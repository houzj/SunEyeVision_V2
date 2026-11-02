using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.Workflow;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

internal static partial class S2DocumentEditingCases
{
    static object C05State(DeclaredFieldAccess field)=>Get(field.View,"_state");
    static object C05Model(DeclaredFieldAccess field)=>Get(C05State(field),"NumericModel");
    static object? C05Value(DeclaredFieldAccess field)
    {var value=Get(C05Model(field),"Value");return value is ConstantSetting constant?constant.Value:value;}
    static string C05Stamp(RuntimeToolConfiguration configuration)=>(string)Get(configuration,"ConfigurationIdentity")+"|"+(string)Get(configuration,"CurrentValuesIdentity");
    static void VerifyC05NumberModels(Func<NativeRuntimeEditorWindow> open,Func<NativeRuntimeEditorWindow,DeclaredFieldAccess> low,
        Func<RuntimeToolConfiguration> formal,Action<Action<NodeConfigurationEditDraft>> formalEdit,
        Action<NativeRuntimeEditorWindow,string,string,string> variable,Action<DeclaredFieldAccess> useConstant,
        Action<NativeRuntimeEditorWindow,long> debug,Action<NativeRuntimeEditorWindow> confirm,
        Func<int> history,Action undo,Action redo,Action<Func<ParameterSetting,bool>,long> saveCheck,
        string threshold,string variableId,string path)
    {
        ProductionEditorContext Draft(NativeRuntimeEditorWindow window)=>(ProductionEditorContext)ToolEditorCommit.GetDraftContext(window.EditorControl)!;
        DeclaredSettingsView Editor(NativeRuntimeEditorWindow window)=>(DeclaredSettingsView)Get(window.EditorControl.Content,"Editor");
        void Seed(long value)=>formalEdit(edit=>edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{threshold,value}}));
        void ReadOnly(NativeRuntimeEditorWindow window,Action action)
        {
            var before=C05Stamp(formal());var candidate=C05Stamp(Draft(window).DraftNode.RuntimeConfiguration!);var position=history();var bytes=File.ReadAllBytes(path);var pending=window.EditorControl.HasPendingEdits;
            action();Pump();
            Require(C05Stamp(formal())==before&&C05Stamp(Draft(window).DraftNode.RuntimeConfiguration!)==candidate&&position==history()&&bytes.SequenceEqual(File.ReadAllBytes(path))&&pending==window.EditorControl.HasPendingEdits,"普通数值回读写入配置/CurrentValues/历史/文件或改变pending");
        }
        NumericModelReadResult ReadField(NativeRuntimeEditorWindow window)=>window.EditorControl.ReadNumericModels(NumericModelReadScope.Field,threshold).Single();
        Case("C05真实新根初始化All与显式Field页全量回读均不提交业务",()=>{
            Seed(137);var setup=open();variable(setup,threshold,variableId,"0");confirm(setup);
            var window=open();var field=low(window);var editor=Editor(window);var model=C05Model(field);
            Require(field.Setting is VariableSetting&&(bool)Get(model,"HasValue")&&Convert.ToInt64(C05Value(field))==137,"持久引用新根初始化未回读真实普通参数，或投了默认100");
            var state=C05State(field);var reader=state.GetType().GetField("_readCurrentValue",BindingFlags.Instance|BindingFlags.NonPublic)!;var original=(Func<ConstantSetting?>)reader.GetValue(state)!;var calls=0;
            reader.SetValue(state,(Func<ConstantSetting?>)(()=>{calls++;return original();}));
            try
            {
                ReadOnly(window,()=>Require(ReadField(window).Status is NumericModelReadStatus.Unchanged or NumericModelReadStatus.Updated,"单项回读失败"));Require(calls==1,"单项回读重复调用");
                var inherited=BlobRoiInheritance.RectangleMembers.Select(member=>BlobRoiInheritance.Leaf(BlobRoiInheritance.RectangleParameters,member)).Concat(BlobRoiInheritance.AnnulusMembers.Select(member=>BlobRoiInheritance.Leaf(BlobRoiInheritance.AnnulusParameters,member))).ToArray();Require(inherited.Length==11,"继承字段夹具错误");
                editor.SelectedPageIndex=0;window.UpdateLayout();Pump();ReadOnly(window,()=>{var page=window.EditorControl.ReadNumericModels(NumericModelReadScope.CurrentPage);Require(page.All(result=>result.FieldId!=threshold)&&inherited.All(id=>page.Any(result=>result.FieldId==id&&result.Status==NumericModelReadStatus.Unavailable)),"CurrentPage漏同owner的11个真实继承字段，或按可见性/section映射错误");});Require(calls==1,"基本页读取了运行页阈值");
                editor.SelectedPageIndex=1;window.UpdateLayout();Pump();ReadOnly(window,()=>Require(window.EditorControl.ReadNumericModels(NumericModelReadScope.CurrentPage).Any(result=>result.FieldId==threshold),"运行页漏掉C05字段"));Require(calls==2,"运行页未精确读取本字段一次");
                editor.SelectedPageIndex=0;window.UpdateLayout();Pump();ReadOnly(window,()=>{var all=window.EditorControl.ReadNumericModels(NumericModelReadScope.All);Require(all.Any(result=>result.FieldId==threshold)&&inherited.All(id=>all.Any(result=>result.FieldId==id&&result.Status==NumericModelReadStatus.Unavailable)),"All因页面未显示漏字段或为未具普通值的继承字段造值");var graphicIds=new[]{BlobResultDisplay.Graphic,BlobResultDisplay.Inspection,BlobResultDisplay.Mask,BlobResultDisplay.Annulus}.SelectMany(group=>new[]{BlobResultDisplay.Field(group,"opacity"),BlobResultDisplay.Field(group,"width")});Require(graphicIds.All(id=>all.Any(result=>result.FieldId==id)),"样式popup未开时漏注册opacity/width模型");});Require(calls==3,"All未精确读取本字段一次");
                var repeated=window.EditorControl.ReadNumericModels(NumericModelReadScope.Field,threshold);Require(repeated.Single().Status==NumericModelReadStatus.Unchanged,"重复同值回读不应伪称Updated");
                ReadOnly(window,()=>Require(window.EditorControl.ReadNumericModels(NumericModelReadScope.Field,SettingId(nameof(SunEyeVision.Tool.BlobAnalysis.Runtime.BlobSettings.FilterAreaMin))).Single().Status==NumericModelReadStatus.NotApplicable,"C06范围端点被隐式注册成C05"));
                ReadOnly(window,()=>Require(window.EditorControl.ReadNumericModels(NumericModelReadScope.Field,BlobResultDisplay.Field(BlobResultDisplay.Text,"x")).Single().Status==NumericModelReadStatus.NotApplicable,"文本XY的原ButtonSelecter被误注册C05"));
                var count=calls;window.Hide();Pump();window.Show();window.UpdateLayout();Pump();Require(calls==count,"普通Loaded/重显擅自再做新根All");
                var options=editor.Options!;var references=options.ReferenceOptions!;var newReads=0;
                editor.Bind(editor.Draft!,options with{ReferenceOptions=references with{ReadCurrentValue=port=>{if(port.Id==threshold)newReads++;return references.ReadCurrentValue!(port);}}});window.UpdateLayout();Pump();
                var fresh=low(window);Require(!ReferenceEquals(state,C05State(fresh))&&newReads==1&&(bool)Get(C05Model(fresh),"HasValue")&&Convert.ToInt64(C05Value(fresh))==137,"内部Editor.Bind未对成功新根初始化一次，或初始化旧state");
                window.Hide();Pump();window.Show();window.UpdateLayout();Pump();Require(newReads==1&&calls==count,"内部根重显重复读取或触及旧state");
            }
            finally{if(!(bool)Get(state,"IsDisposed"))reader.SetValue(state,original);window.Close();Pump();}
        });
        Case("C05引用身份本地模型普通读值与实际执行值严格分离",()=>{
            Seed(137);var window=open();var field=low(window);variable(window,threshold,variableId,"0");field=low(window);
            Require(field.Setting is VariableSetting&&Convert.ToInt64(C05Value(field))==137,"选择来源把173伪当本地模型或丢失137");var relationship=field.Setting;
            ReadOnly(window,()=>Require(ReadField(window).Status is NumericModelReadStatus.Unchanged or NumericModelReadStatus.Updated,"引用模式跳过普通回读"));Require(Convert.ToInt64(C05Value(field))==137&&Equals(field.Setting,relationship),"普通回读改变引用身份或消费源结果");
            debug(window,173);Require(Convert.ToInt64(C05Value(field))==137,"运行完成后无原证自动回读了UI模型");
            ReadOnly(window,()=>Require(ReadField(window).Status==NumericModelReadStatus.Updated,"显式普通回读未投影私有已接纳173"));Require(Convert.ToInt64(C05Value(field))==173&&field.Setting is VariableSetting,"普通回读未保持引用与模型独立");
            useConstant(field);Require(low(window).Setting is ConstantSetting{Value:173L},"解绑未显式读取本端普通参数173");window.Close();Pump();Require(formal().Fields.Read(threshold) is ConstantSetting{Value:137L},"取消私有运行与解绑污染正式");
        });
        Case("C05普通回读保留真实未提交raw，不重放字段Changed",()=>{
            Seed(137);var window=open();var field=low(window);var input=(TextBox)field.InputElement!;var original=input.Text;input.Text="bad-c05";Pump();var changes=0;Action changed=()=>changes++;field.Changed+=changed;
            try{ReadOnly(window,()=>Require(ReadField(window).Status is NumericModelReadStatus.Unchanged or NumericModelReadStatus.Updated,"raw期间回读失败"));Require(((TextBox)low(window).InputElement!).Text=="bad-c05"&&changes==0&&!window.EditorControl.TryCapture(out _),"回读覆盖raw、发字段接纳或绕过非法保护");}
            finally{field.Changed-=changed;((TextBox)low(window).InputElement!).Text=original;Require(low(window).TryCommitPending(),"原raw恢复失败");window.Close();Pump();}
        });
        Case("C05无值与读异常不补旧模型，解绑进入受保护新常量输入可取消",()=>{
            foreach(var unavailable in new[]{true,false})
            {
                Seed(137);var window=open();variable(window,threshold,variableId,"0");var field=low(window);var state=C05State(field);var reader=state.GetType().GetField("_readCurrentValue",BindingFlags.Instance|BindingFlags.NonPublic)!;var original=reader.GetValue(state);var before=C05Stamp(formal());var position=history();
                reader.SetValue(state,(Func<ConstantSetting?>)(()=>unavailable?null:throw new InvalidOperationException("C05自有普通读取失败")));
                try
                {
                    ReadOnly(window,()=>Require(ReadField(window).Status==(unavailable?NumericModelReadStatus.Unavailable:NumericModelReadStatus.Failed),"无值/异常回读状态混同"));Require(Convert.ToInt64(C05Value(field))==137,"失败应保已知模型，但不得伪称本次读取成功");
                    useConstant(field);field=low(window);Require(field.Setting is VariableSetting&&field.HasPendingEdits&&!window.EditorControl.TryCapture(out _),"缺普通值时用旧137补常量或放过未接纳输入");Require(field.InputElement is TextBox text&&string.IsNullOrEmpty(text.Text),"无值解绑未进入明确的新常量输入态");
                }
                finally{reader.SetValue(state,original);window.Close();Pump();}
                Require(C05Stamp(formal())==before&&history()==position,"无值/异常解绑取消污染正式或历史");
            }
        });
        Case("C05回读期间同节点换根与旧根退出拒绝迟到投影",()=>{
            Seed(137);var window=open();var control=window.EditorControl;var field=low(window);var state=C05State(field);var reader=state.GetType().GetField("_readCurrentValue",BindingFlags.Instance|BindingFlags.NonPublic)!;var original=(Func<ConstantSetting?>)reader.GetValue(state)!;var called=false;
            reader.SetValue(state,(Func<ConstantSetting?>)(()=>{var value=original();called=true;control.BindDraft(Draft(window).DraftNode);return value;}));
            var result=control.ReadNumericModels(NumericModelReadScope.Field,threshold);Require(called&&result.All(item=>item.Status==NumericModelReadStatus.Retired),"回调中换根仍接纳旧回读");var replacement=low(window);Require(!ReferenceEquals(state,C05State(replacement))&&Convert.ToInt64(C05Value(replacement))==137,"换根新模型被旧值覆盖");window.Close();Pump();Require(control.ReadNumericModels(NumericModelReadScope.Field,threshold).All(item=>item.Status==NumericModelReadStatus.Retired),"关闭后宿主回读未拒绝");
        });
        Case("C05真实确认取消历史Copy及保存重开执行沿原参数链",()=>{
            Seed(137);var before=C05Stamp(formal());var window=open();variable(window,threshold,variableId,"0");debug(window,173);ReadField(window);confirm(window);var accepted=C05Stamp(formal());Require(accepted!=before&&formal().Fields.Read(threshold) is VariableSetting&&formal().ReadCurrentParameter(threshold) is {Value:173L},"确认丢引用或真实已接纳参数值");
            Require(C05Stamp(formal().Copy())==accepted,"Copy丢正式身份或当前值");var configuration=(string)Get(formal(),"ConfigurationIdentity");undo();Pump();Require(C05Stamp(formal())==before,"Undo重放执行或丢137");redo();Pump();Require((string)Get(formal(),"ConfigurationIdentity")==configuration&&formal().ReadCurrentParameter(threshold) is {Value:137L},"Redo未恢复引用配置或错误重放旧执行173");
            var rerun=open();Require(Convert.ToInt64(C05Value(low(rerun)))==137,"Redo后初始化伪造旧执行值");debug(rerun,173);ReadField(rerun);confirm(rerun);accepted=C05Stamp(formal());saveCheck(setting=>setting is VariableSetting{Reference.VariableId:var id}&&id==variableId,173);
            var cancel=open();var field=low(cancel);useConstant(field);Require(low(cancel).Setting is ConstantSetting{Value:173L},"重开后解绑未读持久普通值");cancel.Close();Pump();Require(C05Stamp(formal())==accepted,"保存重开后取消写正式");
        });
    }
}
