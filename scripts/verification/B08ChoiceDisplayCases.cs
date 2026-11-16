using System.Collections;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

internal static class ChoiceDisplayCases
{
    private static Window owner=null!;private static int passed;
    static void Require(bool value,string reason){if(!value)throw new Exception(reason);}
    static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    static void Show(UIElement view){owner.Content=view;owner.UpdateLayout();Pump();}
    static void Case(string name,Action run){run();passed++;Console.WriteLine("PASS "+name);}
    static void Reject(Action run){try{run();}catch(ArgumentException){return;}throw new Exception("无效展示声明未拒绝");}
    static readonly Port Mode=new("mode",DataType.Text,EditModes:FieldEditModes.Constant,Choices:new TextChoiceConstraint(new[]{"single","double","none"}));
    static readonly Port Number=new("value",DataType.Integer);
    static readonly FieldPresentation ModeUi=new("mode","阈值方式","运行参数",0,FieldEditorKind.Choice,new[]{"single","double","none"}){Layout=new("运行参数","运行参数")};
    static readonly FieldPresentation NumberUi=new("value","数值","运行参数",1,FieldEditorKind.Scalar){Layout=new("运行参数","运行参数",DecimalPlaces:0)};
    static DeclaredChoiceDisplayOptions Options()=>new(new[]{
        new DeclaredChoiceDisplayItem("BinaryImage","不进行二值化","none",false,"语义尚未对齐"),
        new DeclaredChoiceDisplayItem("SingleThreshold","单阈值","single",true),
        new DeclaredChoiceDisplayItem("DoubleThreshold","双阈值","double",true),
        new DeclaredChoiceDisplayItem("AutoThreshold","自动阈值（未实现）",null,false,"自动阈值尚未实现"),
        new DeclaredChoiceDisplayItem("FixedSoftThreshold","固定软阈值（未实现）",null,false,"固定软阈值尚未实现"),
        new DeclaredChoiceDisplayItem("RelativeSoftThreshold","相对软阈值（未实现）",null,false,"相对软阈值尚未实现")});
    static FieldSettingsDraft Draft(string mode="single")
    {
        var schema=new ToolSchema(new[]{Mode,Number},Array.Empty<Port>(),new ToolPresentation(new[]{ModeUi,NumberUi}));
        return new(schema,new FieldSettings(schema.Inputs,new Dictionary<string,ParameterSetting>{{"mode",new ConstantSetting(mode)},{"value",new ConstantSetting(7L)}}));
    }
    static DeclaredFieldsView Fields(DeclaredSettingsView view)=>(DeclaredFieldsView)view.Content;
    static ComboBox Combo(DeclaredSettingsView view)=>(ComboBox)Fields(view).Fields.Single(field=>field.FieldId=="mode").InputElement!;
    static string Value(FieldSettingsDraft draft)=>(string)((ConstantSetting)draft.CaptureFields().Read("mode")).Value!;
    static void Open(ComboBox combo){owner.Activate();combo.Focus();combo.IsDropDownOpen=true;Pump();combo.ApplyTemplate();}
    static ComboBoxItem Container(ComboBox combo,int index)
    {combo.UpdateLayout();return (ComboBoxItem)(combo.ItemContainerGenerator.ContainerFromIndex(index)??throw new Exception("原弹层未生成项容器"));}
    [STAThread] static int Main()
    {
        new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};owner=new Window{Width=700,Height=420,Left=-10000,ShowInTaskbar=false};
        owner.Resources.MergedDictionaries.Add(EditorResources.Create());owner.Show();
        try
        {
            Case("冻结快照、展示身份/正式键拓扑完整校验",()=>
            {
                var rows=Options().Items.ToArray();var frozen=new DeclaredChoiceDisplayOptions(rows);rows[0]=new("other","其他","none",true);
                Require(!frozen.Items[0].IsAvailable,"调用者数组改变展示快照");
                var mutable=new Dictionary<string,DeclaredChoiceDisplayOptions>{{"mode",frozen}};
                var immutable=new ToolSettingsViewOptions(ChoiceDisplays:mutable);var cloned=immutable with{ChoiceDisplays=mutable};mutable.Clear();
                Require(immutable.ChoiceDisplays!.Count==1&&cloned.ChoiceDisplays!.Count==1,"Options构造或with没有冻结字段展示映射");
                Reject(()=>new DeclaredChoiceDisplayItem("a","标签",null,true));
                Reject(()=>new DeclaredChoiceDisplayItem("a","标签","single",false));
                Reject(()=>new DeclaredChoiceDisplayOptions(new[]{frozen.Items[0],frozen.Items[0]}));
                Reject(()=>new DeclaredChoiceDisplayOptions(new[]{frozen.Items[1],new DeclaredChoiceDisplayItem("duplicate-key","重复","single",true)}));
                using var valid=new DeclaredFieldRow(Mode,"模式",new ConstantSetting("single"),_=>{},choiceDisplay:frozen);
                Reject(()=>new DeclaredFieldRow(Mode,"模式",new ConstantSetting("single"),_=>{},choiceDisplay:new(new[]{frozen.Items[1]})));
                Reject(()=>new DeclaredFieldsView(new[]{(Mode,ModeUi,(ParameterSetting)new ConstantSetting("single"))},_=>{},choiceDisplays:new Dictionary<string,DeclaredChoiceDisplayOptions>{{"missing",frozen}}));
            });
            Case("原六项容器禁用及Tooltip，鼠标/键盘/程序事件不接纳占位项",()=>
            {
                var draft=Draft();var writes=0;using var view=new DeclaredSettingsView(draft,new(AcceptCandidate:_=>writes++,ChoiceDisplays:new Dictionary<string,DeclaredChoiceDisplayOptions>{{"mode",Options()}}));
                Show(view);var combo=Combo(view);Open(combo);
                var original=(Style)combo.FindResource("ListViewComboBoxStyle");
                var originalItems=(Style)original.Setters.OfType<Setter>().Single(setter=>setter.Property==ItemsControl.ItemContainerStyleProperty).Value;
                Require(combo.Items.Count==6&&ReferenceEquals(combo.Style,original)&&ReferenceEquals(combo.ItemContainerStyle.BasedOn,originalItems)
                    &&Container(combo,1).Template is not null,"原模板或六项列表缺失");
                foreach(var index in new[]{0,3,4,5})Require(!Container(combo,index).IsEnabled&&Container(combo,index).ToolTip is string {Length:>0}
                    &&ToolTipService.GetShowOnDisabled(Container(combo,index)),"禁用项容器/原因丢失");
                var placeholder=Container(combo,3);Require(!placeholder.Focus(),"键盘可聚焦不可用项");
                placeholder.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=Mouse.MouseDownEvent});
                placeholder.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(owner)!,Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.KeyDownEvent});
                Require(Value(draft)=="single"&&writes==0,"禁用项鼠标/键盘写入");
                Require(!Fields(view).Fields.Single(field=>field.FieldId=="mode").TrySetConstant("AutoThreshold")&&writes==0,"展示ID作为参数被接受");
                combo.SelectedIndex=3;Pump();Require(Value(draft)=="single"&&writes==0&&Equals(combo.SelectedValue,"single"),"程序选择占位未恢复正式键");
                combo.SelectedValue="none";Pump();Require(Value(draft)=="single"&&writes==0&&Equals(combo.SelectedValue,"single"),"程序选择不可用正式键写入");
                combo.SelectedValue="double";Pump();Require(Value(draft)=="double"&&writes==1,"可用项没有沿正式草稿单次接受");
                combo.SelectedValue="double";Pump();Require(writes==1,"同值重复写入");
                Require(view.TryCapture(out var capture,out _)&&capture.Read("mode") is ConstantSetting {Value:"double"},"合法选项无法捕获");
                var json=JsonSerializer.Serialize(capture.Capture());Require(!json.Contains("AutoThreshold",StringComparison.Ordinal)&&!json.Contains("未实现",StringComparison.Ordinal),"展示身份进入字段序列化快照");
            });
            Case("正式不可用初值保留显示，字段/整组提案和最终捕获均拒绝",()=>
            {
                var draft=Draft("none");var writes=0;using var view=new DeclaredSettingsView(draft,new(AcceptCandidate:_=>writes++,ChoiceDisplays:new Dictionary<string,DeclaredChoiceDisplayOptions>{{"mode",Options()}}));
                Show(view);Require(Equals(Combo(view).SelectedValue,"none")&&Value(draft)=="none"&&writes==0,"不可用初值被自动替换");
                Require(view.CaptureAcceptedFields().Read("mode") is ConstantSetting {Value:"none"},"只读捕获改变合法正式键");
                Require(!view.TryCapture(out _,out var reason)&&reason.Contains("语义",StringComparison.Ordinal),"不可用正式初值可最终确认");
                Require(!Fields(view).Fields.Single(field=>field.FieldId=="mode").TrySetConstant("none")&&writes==0,"直接字段接纳禁用项");
                Require(!view.TrySetFields(new Dictionary<string,ParameterSetting>{{"value",new ConstantSetting(19L)},{"mode",new ConstantSetting("none")}})
                    &&writes==0&&draft.CaptureFields().Read("value") is ConstantSetting {Value:7L},"组合提案绕过或半写");
                Require(view.TrySetFields(new Dictionary<string,ParameterSetting>{{"mode",new ConstantSetting("single")},{"value",new ConstantSetting(7L)}})&&writes==1&&view.TryCapture(out _,out _),"完整重提未修复拒绝整组的诊断");
            });
            Case("条件隐藏不会使当前不可用正式键绕过整窗捕获",()=>
            {
                var hiddenMode=new FieldPresentation("mode","模式","运行参数",0,FieldEditorKind.Choice,new[]{"single","double","none"})
                    {Layout=new("运行参数","运行参数",VisibleWhen:new FieldCondition("value",8L))};
                var schema=new ToolSchema(new[]{Mode,Number},Array.Empty<Port>(),new ToolPresentation(new[]{hiddenMode,NumberUi}));
                var draft=new FieldSettingsDraft(schema,new FieldSettings(schema.Inputs,new Dictionary<string,ParameterSetting>{{"mode",new ConstantSetting("none")},{"value",new ConstantSetting(7L)}}));
                using var view=new DeclaredSettingsView(draft,new(ChoiceDisplays:new Dictionary<string,DeclaredChoiceDisplayOptions>{{"mode",Options()}}));Show(view);
                Require(view.HasInvalidInput&&!view.TryCapture(out _,out var reason)&&reason.Contains("语义",StringComparison.Ordinal)
                    &&Value(draft)=="none","条件隐藏绕过不可用正式键准入");
            });
            Case("宿主拒绝、回调重入与重绑旧控件不会改新代",()=>
            {
                var draft=Draft();var reject=true;var writes=0;var reenter=false;ComboBox? live=null;
                using var view=new DeclaredSettingsView(draft,new(AcceptCandidate:_=>
                {if(reject)throw new InvalidOperationException("宿主拒绝");writes++;if(reenter)live!.SelectedValue="none";},ChoiceDisplays:new Dictionary<string,DeclaredChoiceDisplayOptions>{{"mode",Options()}}));
                Show(view);live=Combo(view);live.SelectedValue="double";Pump();Require(Value(draft)=="single"&&writes==0&&Equals(live.SelectedValue,"single"),"宿主拒绝未恢复");
                reject=false;reenter=true;live.SelectedValue="double";Pump();Require(Value(draft)=="double"&&writes==1&&Equals(live.SelectedValue,"double"),"回调重入分离展示与接受值");
                var old=live;Open(old);var replacement=Draft();view.Bind(replacement,new(ChoiceDisplays:new Dictionary<string,DeclaredChoiceDisplayOptions>{{"mode",Options()}}));Pump();
                Require(!old.IsDropDownOpen,"重绑未关闭原Popup");old.SelectedValue="single";old.RaiseEvent(new SelectionChangedEventArgs(Selector.SelectionChangedEvent,new object[0],new object[]{old.Items[0]}));Pump();
                Require(Value(replacement)=="single"&&Value(draft)=="double"&&writes==1,"旧事件写入重绑新代");
                var current=Combo(view);current.IsEnabled=false;current.SelectedValue="double";Pump();Require(Value(replacement)=="single","禁用字段的UI事件写入");
                view.Dispose();current.SelectedValue="none";Pump();Require(Value(replacement)=="single","Dispose旧事件写入");
            });
            Case("显式owner历史保留旧合法正式键，仍不可确认或新接纳",()=>
            {
                var declarations=new[]{(Mode,ModeUi,(ParameterSetting)new ConstantSetting("single")),(Number,NumberUi,(ParameterSetting)new ConstantSetting(7L))};
                var ownerFields=new FieldSettings(new[]{Mode,Number},declarations.ToDictionary(entry=>entry.Item1.Id,entry=>entry.Item3));var writes=0;
                using var fields=new DeclaredFieldsView(declarations,changes=>{ownerFields=ownerFields.Apply(changes);writes++;},
                    choiceDisplays:new Dictionary<string,DeclaredChoiceDisplayOptions>{{"mode",Options()}});
                Show(fields);var replay=fields.CaptureAccepted().ToDictionary(entry=>entry.Key,entry=>entry.Value);
                replay["mode"]=new AcceptedFieldValue(new ConstantSetting("none"),false,new ConstantSetting("none"));
                Require(fields.TryReplayAccepted(replay,()=>{ownerFields=ownerFields.Apply(new Dictionary<string,ParameterSetting>{{"mode",new ConstantSetting("none")}});return true;},()=>true,out var publish),"正式owner历史不能回放旧合法键");
                publish();publish();
                var field=fields.Fields.Single(item=>item.FieldId=="mode");
                Require(field.Setting is ConstantSetting {Value:"none"}&&ownerFields.Read("mode") is ConstantSetting {Value:"none"}
                    &&Equals(((ComboBox)field.InputElement!).SelectedValue,"none")&&writes==0,"历史回放替换值或调用普通writer");
                Require(!fields.TryValidate(out var reason)&&reason.Contains("语义",StringComparison.Ordinal)&&!field.TrySetConstant("none")&&writes==0,"历史回放绕过不可用状态最终准入");
                Require(field.TrySetConstant("single")&&writes==1&&fields.TryValidate(out _),"历史只读值不能被合法新提案修复");
            });
            Case("设置选项字典冻结、设计时独立同链及重新绑定可用性",()=>
            {
                var map=new Dictionary<string,DeclaredChoiceDisplayOptions>{{"mode",Options()}};var draft=Draft();using var view=new DeclaredSettingsView(draft,new(ChoiceDisplays:map));Show(view);
                map["mode"]=new(new[]{new DeclaredChoiceDisplayItem("s","单","single",false,"新代禁用"),new("d","双","double",true),new("n","无","none",true)});
                Require(Fields(view).Fields.Single(field=>field.FieldId=="mode").TrySetConstant("double"),"调用者字典突变影响既有快照");
                view.Bind(draft,new(ChoiceDisplays:map));Require(Combo(view).Items.Count==3&&!view.TrySetFields(new Dictionary<string,ParameterSetting>{{"mode",new ConstantSetting("single")}}),"新代可用性未生效");
                using var design=new DeclaredSettingsView(Draft(),new(ChoiceDisplays:new Dictionary<string,DeclaredChoiceDisplayOptions>{{"mode",Options()}}));Show(design);
                Require(Combo(design).Items.Count==6&&design.TryCapture(out _,out _),"同组装链独立预览依赖宿主");
            });
            Console.WriteLine($"Choice展示完整链：{passed}组通过；不代Blob模式接入、主仓正式或VS/人工验收。");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally{owner.Close();Application.Current.Shutdown();}
    }
}