using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.If.Runtime;

namespace SunEyeVision.Tool.If.Wpf.Design;

/// <summary>设计期上下文：用真实 Runtime 声明与默认值组装参数页，样例来源只用于展示引用控件。</summary>
public static class IfDesignData
{
    public static (IToolSettingsDraft Draft,ToolSettingsViewOptions Options) CreateParameterContext()
    {
        var tool=new IfTool();
        return(CreateDraft(tool),new ToolSettingsViewOptions(ReferenceOptions:CreateReferenceOptions()));
    }

    private static ReferenceFieldOptions CreateReferenceOptions()
    {
        var sample=new DeclaredReferenceCandidate("样例面积","design-source",null,
            new Port("area",DataType.Number),Array.Empty<string>());
        return new(_=>new ReferenceCandidates(new[]{sample},Array.Empty<DeclaredReferenceCandidate>()));
    }

    private static IToolSettingsDraft CreateDraft(IfTool tool)
    {
        var schema=ResultDisplayFields.EditorSchema(tool);
        var defaults=((IToolSettingsFields)tool).ReadSettings(new IfSettings()).CopyValues()
            .ToDictionary(pair=>pair.Key,pair=>(ParameterSetting)new ConstantSetting(pair.Value),StringComparer.Ordinal);
        return new FieldSettingsDraft(schema,new FieldSettings(schema.Inputs,defaults));
    }
}
