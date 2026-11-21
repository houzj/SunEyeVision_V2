using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.If.Runtime;

/// <summary>条件槽的稳定字段身份与布局声明。
/// 原厂条件分支按行配置「取值来源 + 有效区间」，本工具把行固定成 4 个槽位：分支序号即槽位序号。</summary>
public static class IfFields
{
    /// <summary>已声明的条件槽数量；它同时是控制输出（分支）数量，序号 0..ConditionCount-1。</summary>
    public const int ConditionCount=4;
    /// <summary>原厂 IfBranchModuleControl 写入模块参数区的动态行文本，不是算法数据输入端口。</summary>
    public const string BranchText="branch.text";
    /// <summary>原厂 int 行区间限值 ±999999999；统一 Float64 后沿用该边界。</summary>
    public const double RangeLimit=999_999_999d;
    public const double DefaultMinimum=1d, DefaultMaximum=1000d;
    private const string Page="基本参数";
    public static string EnabledField(int index)=>Slot(index,$"condition.{index}.enabled");
    public static string SourceField(int index)=>Slot(index,$"condition.{index}.source");
    public static string MinimumField(int index)=>Slot(index,$"condition.{index}.min");
    public static string MaximumField(int index)=>Slot(index,$"condition.{index}.max");
    /// <summary>槽位显示名，同时用作参数页分段名与分支（控制输出）显示名。</summary>
    public static string ConditionName(int index)=>Slot(index,$"条件{index+1}");
    private static string Slot(int index,string text)
        =>index>=0&&index<ConditionCount?text:throw new ArgumentOutOfRangeException(nameof(index),"条件槽序号必须在已声明范围内。");

    private static readonly TypeRef Boolean=TypeRef.ScalarOf(ScalarKind.Boolean);
    private static readonly TypeRef Number=DataType.Number;
    private static readonly TypeRef Text=DataType.Text;

    public static ToolSchema Schema{get;}=CreateSchema();

    private static ToolSchema CreateSchema()
    {
        var inputs=new List<Port>(ConditionCount*4+1)
        {
            new Port(BranchText,Text,Required:false,Nullable:true,EditModes:FieldEditModes.Constant,DefaultPresence:FieldDefaultPresence.UseDeclaredDefault)
        };
        var fields=new List<FieldPresentation>(ConditionCount*4+1)
        {
            // The original value is owned by IfBranchModuleDynamicIO. It is persisted through
            // the tool settings contract here, while the editor row is supplied by the native
            // IfBranch panel and suppressed from the generic field list.
            new FieldPresentation(BranchText,"条件分支文本","内部",int.MaxValue,FieldEditorKind.Scalar)
            {
                Layout=new(Page,"内部")
            }
        };
        for(var index=0;index<ConditionCount;index++)
        {
            var section=ConditionName(index);
            var visible=new FieldCondition(EnabledField(index),true);
            inputs.Add(new Port(EnabledField(index),Boolean,EditModes:FieldEditModes.Constant));
            // 取值来源只有引用一种写入口：原厂行内选择器同样只能选前序变量，不允许手填常量。
            inputs.Add(new Port(SourceField(index),Number,Required:false,Nullable:true,EditModes:FieldEditModes.Reference));
            inputs.Add(new Port(MinimumField(index),Number,EditModes:FieldEditModes.Constant));
            inputs.Add(new Port(MaximumField(index),Number,EditModes:FieldEditModes.Constant));
            fields.Add(new FieldPresentation(EnabledField(index),"启用",section,index*10,FieldEditorKind.Scalar)
            {
                Layout=new(Page,section,AllowReference:false,BooleanTips:new("已启用","未启用"))
            });
            fields.Add(new FieldPresentation(SourceField(index),"取值来源",section,index*10+1,FieldEditorKind.Scalar)
            {
                Layout=new(Page,section,VisibleWhen:visible)
            });
            fields.Add(new FieldPresentation(MinimumField(index),"有效区间",section,index*10+2,FieldEditorKind.Scalar)
            {
                Layout=new(Page,section,VisibleWhen:visible,PairWith:MaximumField(index),AllowReference:false,
                    Minimum:-RangeLimit,Maximum:RangeLimit,PairDisplayName:"有效区间")
            });
            fields.Add(new FieldPresentation(MaximumField(index),"有效区间上限",section,index*10+3,FieldEditorKind.Scalar)
            {
                Layout=new(Page,section,VisibleWhen:visible,AllowReference:false,Minimum:-RangeLimit,Maximum:RangeLimit)
            });
        }
        return new(inputs,Array.Empty<Port>(),new ToolPresentation(fields));
    }

    public static IReadOnlyList<IfConditionSettings> CreateDefaultConditions()
        =>Array.AsReadOnly(Enumerable.Range(0,ConditionCount)
            .Select(_=>new IfConditionSettings()).ToArray());

    public static InputValues Read(IfSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var conditions=settings.Conditions??throw new ArgumentException("条件列表不能为空。");
        if(conditions.Count!=ConditionCount)
            throw new ArgumentException($"条件数量必须是已声明的 {ConditionCount} 个槽位。");
        var values=new Dictionary<string,object?>(ConditionCount*4,StringComparer.Ordinal);
        for(var index=0;index<ConditionCount;index++)
        {
            var condition=conditions[index]??throw new ArgumentException($"条件{index+1}不能为空。");
            values[EnabledField(index)]=condition.Enabled;
            values[MinimumField(index)]=condition.Minimum;
            values[MaximumField(index)]=condition.Maximum;
            if(condition.Source is {} source)values[SourceField(index)]=source;
        }
        values[BranchText]=settings.BranchText;
        return new InputValues(Schema.Inputs,values);
    }

    public static IfSettings Apply(IfSettings original,IReadOnlyDictionary<string,object?> changes)
    {
        ArgumentNullException.ThrowIfNull(original);ArgumentNullException.ThrowIfNull(changes);
        var values=Read(original).CopyValues().ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        foreach(var pair in changes)
        {
            if(!Schema.Inputs.Any(port=>port.Id==pair.Key))throw new ArgumentException("未声明的条件分支字段："+pair.Key);
            if(pair.Value is null)values.Remove(pair.Key);else values[pair.Key]=pair.Value;
        }
        return Materialize(new InputValues(Schema.Inputs,values));
    }

    public static IfSettings Materialize(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var candidate=new InputValues(Schema.Inputs,values.CopyValues());
        var conditions=new IfConditionSettings[ConditionCount];
        for(var index=0;index<ConditionCount;index++)
            conditions[index]=new IfConditionSettings
            {
                Enabled=candidate.Get<bool>(EnabledField(index)),
                Source=candidate.Contains(SourceField(index))?ToNumber(candidate.Get<object?>(SourceField(index)),SourceField(index)):null,
                Minimum=ToNumber(candidate.Get<object?>(MinimumField(index)),MinimumField(index))??0d,
                Maximum=ToNumber(candidate.Get<object?>(MaximumField(index)),MaximumField(index))??0d
            };
        var branchText=candidate.Contains(BranchText)?candidate.Get<string>(BranchText):IfBranchTextCodec.DefaultSerialized;
        _=IfBranchTextCodec.Parse(branchText);
        var result=new IfSettings{Conditions=Array.AsReadOnly(conditions),BranchText=branchText};
        var errors=result.Validate();
        if(errors.Count!=0)throw new ArgumentException(string.Join("；",errors.Select(error=>error.Message)));
        return result.Freeze();
    }

    /// <summary>数值来源按声明类型归一；其它类型直接报错，不做兜底。</summary>
    internal static double? ToNumber(object? value,string field)
        =>value switch
        {
            null=>null,
            double number=>number,
            float single=>(double)single,
            long integer=>integer,
            int integer=>integer,
            _=>throw new ArgumentException($"字段 {field} 必须是数值，实际为 {value.GetType().Name}。")
        };
}
