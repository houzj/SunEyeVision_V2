using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.FrameMean.Runtime;

/// <summary>Stable setting identities shared through declarations, never CLR reflection.</summary>
public static class FrameMeanFields
{
    public const string ProcStep="proc.step";
    /// <summary>原厂 AlgorithmTab.xml 三项命令 DisplayTitle：统计当前图像 / 不统计当前图像 / 清空统计图像。</summary>
    public static readonly string[] ProcStepKeys=["1","2","3"];
    public static readonly string[] ProcStepLabels=["统计当前图像","不统计当前图像","清空统计图像"];
    private static readonly TypeRef Text=TypeRef.ScalarOf(ScalarKind.Text);
    public static ToolSchema Schema {get;}=new(new[] {
        new Port(ProcStep,Text,EditModes:FieldEditModes.Constant,Choices:new(ProcStepKeys),
            DefaultPresence:FieldDefaultPresence.UseDeclaredDefault)
    },Array.Empty<Port>(),new ToolPresentation(new[] {
        // 原厂证据：ProcStep 位于 AlgorithmTab.xml 的 Tab_Run Params（运行参数）页。
        new FieldPresentation(ProcStep,"处理步骤","运行参数",0,FieldEditorKind.Choice,ProcStepKeys,
            ProcStepKeys.Zip(ProcStepLabels).ToDictionary(pair=>pair.First,pair=>pair.Second,StringComparer.Ordinal)) {
            Layout=new("运行参数","运行参数",AllowReference:false)}
    }));

    public static InputValues Read(FrameMeanSettings settings)=>new(Schema.Inputs,
        new Dictionary<string,object?> {[ProcStep]=settings.ProcStep});

    public static FrameMeanSettings Apply(FrameMeanSettings original,IReadOnlyDictionary<string,object?> changes)
    {
        var values=Read(original).CopyValues().ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        foreach(var pair in changes)
        {
            if(!Schema.Inputs.Any(port=>port.Id==pair.Key))throw new ArgumentException("未声明的帧均值字段："+pair.Key);
            values[pair.Key]=pair.Value;
        }
        return Materialize(new InputValues(Schema.Inputs,values));
    }

    public static FrameMeanSettings Materialize(InputValues values)
    {
        var candidate=new InputValues(Schema.Inputs,values.CopyValues());
        var result=new FrameMeanSettings { ProcStep=candidate.Get<string>(ProcStep) };
        var errors=result.Validate();
        if(errors.Count!=0)throw new ArgumentException(string.Join("；",errors.Select(error=>error.Message)));
        return result.Freeze();
    }
}
