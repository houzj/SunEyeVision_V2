using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.Text;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

public sealed record AnchoredSettingsContent(string FieldId,FrameworkElement Content,FieldCondition? Condition=null,bool FollowAnchorVisibility=true,bool InsertBefore=false);

/// <summary>InputContent ownership transfers to the settings view on successful attachment.
/// Rebinding the same instance retains it; replacement and final disposal release IDisposable content.
/// If creation fails before attachment, the creating caller releases its unaccepted content.
/// AdditionalPageContents follows the same ownership rule; the same instances are retained on rebind.</summary>
public sealed record ToolSettingsViewOptions(
    ReferenceFieldOptions? ReferenceOptions=null,
    Func<OutputReference,string>? FormatReference=null,
    string? InputSection=null,FrameworkElement? InputContent=null,
    Action<FieldSettings>? AcceptCandidate=null,
    IReadOnlyDictionary<string,NativeIntegerRadioOptions>? IntegerRadios=null,
    Func<FieldGroupCandidate,string?>? ValidateComposition=null,
    IReadOnlyDictionary<string,Func<IReadOnlyList<DeclaredReferenceCandidate>>>? ImageSources=null,
    IReadOnlyDictionary<string,IReadOnlyList<FieldCondition>>? ParentConditions=null,
    IReadOnlySet<string>? InlineConstants=null,
    IReadOnlyDictionary<string,TextExpressionOptions>? TextExpressions=null,
    IReadOnlyList<NativeGraphicDisplayOptions>? GraphicDisplays=null,
    IReadOnlyList<NativeTextDisplayOptions>? TextDisplays=null,
    IReadOnlyList<NativeAssistOptions>? AssistCompositions=null,
    string? InputAfterField=null,FieldCondition? InputCondition=null,
    IReadOnlyDictionary<string,FrameworkElement>? AdditionalPageContents=null,
    string? AdditionalAfterField=null,FrameworkElement? AdditionalAfterContent=null,
    FieldCondition? AdditionalAfterCondition=null,IReadOnlySet<string>? HeaderlessSectionAnchors=null,
    IReadOnlyList<AnchoredSettingsContent>? AdditionalAnchoredContents=null,
    IReadOnlyDictionary<string,double>? PageLabelWidths=null,
    Func<string?>? ValidateUiState=null,
    IReadOnlySet<string>? SuppressedFieldRows=null,
    IReadOnlyDictionary<string,FrameworkElement>? FieldVisibilitySources=null,
    IReadOnlyDictionary<string,DeclaredChoiceDisplayOptions>? ChoiceDisplays=null,
    SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeRoiFrameSource? RoiFrames=null)
{
    private IReadOnlyDictionary<string,DeclaredChoiceDisplayOptions>? _choiceDisplays=FreezeChoiceDisplays(ChoiceDisplays);
    public IReadOnlyDictionary<string,DeclaredChoiceDisplayOptions>? ChoiceDisplays
    {
        get=>_choiceDisplays;
        init=>_choiceDisplays=FreezeChoiceDisplays(value);
    }
    private static IReadOnlyDictionary<string,DeclaredChoiceDisplayOptions>? FreezeChoiceDisplays(IReadOnlyDictionary<string,DeclaredChoiceDisplayOptions>? values)
        =>values is null?null:new System.Collections.ObjectModel.ReadOnlyDictionary<string,DeclaredChoiceDisplayOptions>(
            values.ToDictionary(entry=>entry.Key,entry=>entry.Value??throw new ArgumentException("枚举展示配置不能为空。"),StringComparer.Ordinal));
}

public interface IToolSettingsView : IDisposable
{
    FrameworkElement View { get; }
    bool HasInvalidInput { get; }
    bool HasPendingEdits { get; }
    long AcceptedRevision { get; }
    IReadOnlyList<string> AcceptedFieldOrder { get; }
    FieldSettings CaptureAcceptedFields();
    event Action? Changed;
    bool TryCapture(out FieldSettings fields,out string error);
}
