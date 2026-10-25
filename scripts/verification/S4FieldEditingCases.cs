using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Workflow;

internal static class S2DocumentEditingCases
{
    [STAThread] private static int Main()
    {
        try
        {
            if(Application.Current is null)new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            ParameterSetting current=new ConstantSetting(100L);var writes=0;
            var number=new DeclaredFieldEditor(new("threshold",DataType.Integer,Storage:NumericStorage.Int32),"阈值",current,
                next=>{current=next;writes++;});
            var input=(TextBox)number.InputElement!;
            input.Text="2147483648";Require(number.HasInvalidText&&Equals(((ConstantSetting)current).Value,100L)&&writes==0,"溢出数字污染草稿");
            input.Text="1.2";Require(number.HasInvalidText&&input.Text=="1.2"&&writes==0,"非法原文未保留");
            input.Text="210";Require(!number.HasInvalidText&&Equals(((ConstantSetting)current).Value,210L)&&writes==1,"数字修正未恢复");
            Console.WriteLine("通过：声明Int32范围、整数格式、非法原文保留和修正恢复");

            var exact=1d;var floating=new DeclaredFieldEditor(new("ratio",DataType.Number),"比例",new ConstantSetting(exact),next=>exact=(double)((ConstantSetting)next).Value!);
            ((TextBox)floating.InputElement!).Text="NaN";Require(floating.HasInvalidText&&exact==1d,"NaN通过声明校验");
            ((TextBox)floating.InputElement!).Text="0.5";Require(!floating.HasInvalidText&&exact==.5d,"有限数值不能恢复");
            Console.WriteLine("通过：非有限数值拒绝，独立字段错误状态互不污染");

            var choiceValue="Binary";var choice=new DeclaredFieldEditor(new("type",DataType.Text,Choices:new TextChoiceConstraint(new[]{"Binary","BinaryInv"})),"极性",
                new ConstantSetting(choiceValue),next=>choiceValue=(string)((ConstantSetting)next).Value!);
            ((ComboBox)choice.InputElement!).SelectedItem="BinaryInv";Require(choiceValue=="BinaryInv","已声明选项未提交到草稿");
            Require(((ComboBox)choice.InputElement!).Items.Count==2,"选项不是来自正式闭合声明");
            Console.WriteLine("通过：下拉选项直接消费正式声明");

            var color=PresentationDeclarations.Color(10,20,30);var colorWrites=0;
            var record=new DeclaredFieldEditor(new("color",PresentationDeclarations.Rgba),"颜色",new ConstantSetting(color),next=>{color=(RecordValue)((ConstantSetting)next).Value!;colorWrites++;});
            var red=Descendants(record).OfType<DeclaredFieldEditor>().Single(field=>field.FieldId=="r");
            ((TextBox)red.InputElement!).Text="256";Require(record.HasInvalidText&&color.Get<long>("r")==10&&colorWrites==0,"颜色成员溢出仍更新了记录");
            ((TextBox)red.InputElement!).Text="255";Require(!record.HasInvalidText&&color.Get<long>("r")==255&&colorWrites==1,"记录成员纠正未原子更新");
            Console.WriteLine("通过：颜色记录成员按UInt8声明校验，整条记录原子更新");

            ParameterSetting bound=new ConstantSetting(7L);
            var reference=new OutputReference("producer","port",TypeRef.SequenceOf(DataType.Integer),selection:new BindingSelection.AtConstant(0));
            var binding=new DeclaredFieldEditor(new("optional",DataType.Integer,Required:false,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),"可选数值",bound,
                next=>bound=next,_=>new ReferenceSetting(reference));
            Click(binding,"绑定");Require(bound is ReferenceSetting source&&source.Reference==reference,"绑定意图未交给宿主");
            Click(binding,"常量");Require(bound is ConstantSetting restored&&Equals(restored.Value,7L),"常量备份没有恢复");
            Click(binding,"清除");Require(bound is UnsetSetting,"清除可选项误变为空值或空集合");
            Console.WriteLine("通过：宿主选择绑定、恢复常量及明确Unset；控件不自行执行或发布");

            var rejected=3L;var reject=new DeclaredFieldEditor(new("host-reject",DataType.Integer),"宿主拒绝",new ConstantSetting(rejected),_=>throw new InvalidOperationException("编辑范围已失效"));
            ((TextBox)reject.InputElement!).Text="4";Require(reject.HasInvalidText&&reject.Error.Contains("编辑范围")&&Equals(((ConstantSetting)reject.Setting).Value,3L),"宿主拒绝后控件冒称写入成功");
            var names=typeof(DeclaredFieldEditor).Assembly.GetReferencedAssemblies().Select(assembly=>assembly.Name!).ToArray();
            Require(!names.Any(name=>name.Contains("Plugin.SDK")||name.Contains("Halcon",StringComparison.OrdinalIgnoreCase)||name=="SunEyeVision.UI"),"公共字段层倒置依赖");
            Console.WriteLine("通过：失效范围拒绝且保留原值；公共字段程序集不依赖SDK、HALCON或UI宿主");
            var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;
            var manager=new PluginManager();manager.LoadPlugins(Path.Combine(product,"plugins"));
            manager.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(tool=>tool.Id));
            var parameters=ToolRegistry.CreateParameters("BlobAnalysis");
            var access=((IConfigurationFieldProvider)parameters).GetConfigurationFields();
            Require(access.Count>100&&access.Select(field=>field.Port.Id).Distinct().Count()==access.Count,"正式Blob字段没有按唯一声明展开");
            var area=access.Single(field=>field.StoragePath=="ResultPresentation.Judgments[Key=total-area].Min");
            var other=access.Single(field=>field.StoragePath=="ResultPresentation.Judgments[Key=max-area].Min");var before=other.Read();
            area.Write(12d);Require(Equals(area.Read(),12d)&&Equals(before,other.Read()),"同名Min字段写入了其他判定项");
            var mode=access.Single(field=>field.StoragePath=="InspectionMode");mode.Write("InheritedRegion");Require(Equals(mode.Read(),"InheritedRegion"),"模式字段错误覆盖ROI集合");mode.Write("LocalGeometry");
            var colorSlot=access.Single(field=>field.StoragePath=="ResultPresentation.Graphics[Key=blobs].Style.OkColor");
            colorSlot.Write(PresentationDeclarations.Color(32,64,128,200));var roundTrip=(RecordValue)colorSlot.Read()!;
            Require(roundTrip.Get<long>("a")==200&&roundTrip.Get<long>("b")==128,"声明颜色与SDK模型往返丢失");
            Console.WriteLine($"通过：正式Blob的{access.Count}项声明常量字段、嵌套判定、区域模式和RGBA准确读写");

            var node=new WorkflowNodeBase("blob","Blob","Blob","BlobAnalysis"){Parameters=parameters};
            var flow=new SunEyeVision.Workflow.Workflow("flow","字段草稿");flow.Nodes.Add(node);var solution=new Solution();solution.Workflows.Add(flow);
            var edits=(DocumentEditSession)Activator.CreateInstance(typeof(DocumentEditSession),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{solution},null)!;
            var tool=ToolRegistry.CreateToolInstance("BlobAnalysis")!;
            var fallback=new SunEyeVision.UI.Services.Editing.DeclaredToolConfigurationControl(tool);
            using var context=new SunEyeVision.UI.Services.Editing.ProductionEditorContext(edits.Host,flow.Id,node,()=>true,draft=>fallback.SetParameters(draft.Parameters),documentEdits:edits);
            fallback.SetParameters(context.DraftNode.Parameters);
            var fields=(DeclaredFieldsView)fallback.Content;
            var thresholdId=((IConfigurationFieldProvider)context.DraftNode.Parameters).GetConfigurationFields().Single(field=>field.StoragePath=="Threshold").Port.Id;
            ((TextBox)fields.Fields.Single(field=>field.FieldId==thresholdId).InputElement!).Text="201";
            Require((int)parameters.GetType().GetProperty("Threshold")!.GetValue(parameters)! ==128,"共享控件直接写了正式参数");
            Require(fallback.TryValidateEditor(out _)&&context.TryCommitEditor(out var error),"声明字段草稿不能按S2范围确认");
            Require((int)node.Parameters.GetType().GetProperty("Threshold")!.GetValue(node.Parameters)! ==201,"确认未发布字段草稿");
            ((TextBox)((DeclaredFieldsView)fallback.Content).Fields.Single(field=>field.FieldId==thresholdId).InputElement!).Text="215";
            context.Discard();Require((int)node.Parameters.GetType().GetProperty("Threshold")!.GetValue(node.Parameters)! ==201,"取消撤回了已确认内容或提交了新草稿");
            edits.Close();
            Console.WriteLine("通过：真实宿主默认页写私有草稿，确认仅当前范围，随后取消不改变已确认值");
            Console.WriteLine("S4字段编辑：8组通过；完整默认配置、ROI和保存重开链尚未核收。");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static void Click(DependencyObject control,string text)=>Descendants(control).OfType<Button>().Single(button=>Equals(button.Content,text))
        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach(var item in LogicalTreeHelper.GetChildren(parent))if(item is DependencyObject child)
        {yield return child;foreach(var descendant in Descendants(child))yield return descendant;}
    }
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
}
