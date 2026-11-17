using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.If.Runtime;

/// <summary>原厂「条件分支」的分支契约实现：按声明顺序报出命中槽位序号，节点身份与出边解析仍归宿主。</summary>
public sealed class IfTool:IToolDefinition<IfSettings,IfOutput>,IToolSettingsFields,IToolCatalogProvider,
    IToolResultPresentationProvider,IToolExecutionFilter,IToolManualReferenceSources,IToolParameterBindingNames,
    IToolDormantParameterDefaults
{
    public const string Id="If";
    /// <summary>控制输出按槽位顺序声明，声明顺序即 <see cref="ToolOutcome{T}.SelectedBranches"/> 的序号。</summary>
    public static IReadOnlyList<ControlOutput> Branches{get;}=Array.AsReadOnly(Enumerable.Range(0,IfFields.ConditionCount)
        .Select(index=>new ControlOutput($"branch.{index}",IfFields.ConditionName(index))).ToArray());
    /// <summary>调用声明：原厂条件分支没有数据输入端口，只有输出与控制输出。
    /// 条件字段属于 SettingsSchema，不能在这里重复声明。</summary>
    public static ToolSchema Schema{get;}=new(Array.Empty<Port>(),IfOutput.Ports,
        outputDisplay:IfOutput.Display,controlOutputs:Branches);
    public string ToolId=>Id;
    public ToolCatalogDescriptor Catalog{get;}=new("条件分支","按取值是否落在有效区间选择分支走向","branch","逻辑工具",10,"IFBRANCH");
    public ToolSchema SettingsSchema=>IfFields.Schema;
    public ToolSchema ResultSchema{get;}=new(Array.Empty<Port>(),IfOutput.Ports,outputDisplay:IfOutput.Display);
    public ResultProjectionDescriptor ResultProjection{get;}=new(ResultProjectionKind.PropertyList,ResultCardinality.One,new[]
    {
        new ResultColumnDescriptor("branch-match","命中分支",new(IfOutput.BranchMatch)),
        new ResultColumnDescriptor("module-status","模块状态",new(IfOutput.ModuleStatus)),
        // 单精度展示要求单元格为浮点且不固定小数位；模块耗时固定 2 位小数展示，故关闭单精度。
        new ResultColumnDescriptor("module-time","模块耗时",new(IfOutput.ModuleRunTime),Unit:"ms",DecimalPlaces:2,UseSinglePrecision:false)
    });
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources{get;}=Array.AsReadOnly(new[]
    {
        new ManualReferenceSource(IfOutput.BranchMatch),
        new ManualReferenceSource(IfOutput.ModuleStatus),
        new ManualReferenceSource(IfOutput.ModuleRunTime)
    });
    public IReadOnlyDictionary<string,string> ParameterBindingNames{get;}=
        new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(
            Enumerable.Range(0,IfFields.ConditionCount)
                .ToDictionary(index=>IfFields.SourceField(index),index=>$"Condition{index+1}Input",StringComparer.Ordinal));

    public ToolSchema Describe()=>Schema;
    public IReadOnlyList<Diagnostic> Validate(IfSettings settings)=>settings.Validate();
    public ToolCapabilities Capabilities()=>ToolCapabilities.None;
    public IToolSession<IfSettings,IfOutput> CreateSession()=>new IfSession();

    /// <summary>没有启用任何条件时本轮没有可判定的分支，按「未执行」抑制而不是静默成功。</summary>
    public bool ShouldExecute(object settings)=>RequireSettings(settings).HasEnabledCondition;

    /// <summary>未启用槽位的取值来源仍保留在方案里，但本轮不作为执行依赖去解析。</summary>
    public IReadOnlyDictionary<string,ConstantSetting> SelectDormantParameterDefaults(FieldSettings configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var dormant=new Dictionary<string,ConstantSetting>(StringComparer.Ordinal);
        for(var index=0;index<IfFields.ConditionCount;index++)
        {
            if(configuration.Read(IfFields.EnabledField(index)) is ConstantSetting{Value:true})continue;
            if(configuration.Read(IfFields.SourceField(index)) is ReferenceSetting or VariableSetting)
                dormant[IfFields.SourceField(index)]=new ConstantSetting(null);
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string,ConstantSetting>(dormant);
    }

    public InputValues ReadSettings(object settings)=>IfFields.Read(RequireSettings(settings));
    public object MaterializeSettings(InputValues values)=>IfFields.Materialize(values);
    private static IfSettings RequireSettings(object settings)=>settings as IfSettings
        ??throw new ArgumentException("条件分支设置类型不正确。");
}
