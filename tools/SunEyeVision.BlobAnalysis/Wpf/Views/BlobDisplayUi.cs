using System.Windows;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

internal static class BlobDisplayUi
{
    internal static ToolSettingsViewOptions Configure(ToolSettingsViewOptions options,FrameworkElement pointVisibility)
    {
        var graphic=SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Graphic;
        var text=SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Text;
        string Id(string group,string role)=>SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Field(group,role);
        var expressions=options.TextExpressions?.ToDictionary(item=>item.Key,item=>item.Value,StringComparer.Ordinal)??new();
        var contentId=Id(text,"content");var sourceOptions=expressions.GetValueOrDefault(contentId);
        expressions[contentId]=new(sourceOptions?.ReadCandidates??(()=>new(Array.Empty<SunEyeVision.Plugin.Wpf.Adapters.References.DeclaredReferenceCandidate>(),
            _=>throw new InvalidOperationException("文本来源尚未就绪。"),()=>true)),CreateSetting:proposal=>
        {
            var slots=proposal.Accepted is DisplayExpressionSetting existing?existing.Slots.ToList():new List<DisplayExpressionSlot>();
            if(proposal.Selected is {} selected)
            {
                if(proposal.Token is not {Length:>0} token)throw new ArgumentException("文本来源标记无效。");
                var reference=selected.Reference(BindingSelection.All);
                if(proposal.Text.Split("{"+token+"}",StringSplitOptions.None).Length>2&&slots.Any(slot=>slot.Token==token&&slot.Reference is {} old&&SunEyeVision.Plugin.Contracts.Results.Presentation.ResultDisplayFields.SameReferenceIdentity(old,reference)==false))
                    throw new ArgumentException("文本来源名称不唯一，请使用不同的来源名称。");
                slots.RemoveAll(slot=>slot.Token==token);slots.Add(new(token,reference));
            }
            return SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.ParseContent(proposal.Text,slots);
        });
        var visibilitySources = options.FieldVisibilitySources?.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal)
            ?? new Dictionary<string,FrameworkElement>(StringComparer.Ordinal);
        visibilitySources.Add(Id(BlobResultDisplay.ReferencePoint,"visible"),pointVisibility);
        visibilitySources.Add(Id(BlobResultDisplay.RunPoint,"visible"),pointVisibility);
        return options with {TextExpressions=expressions, FieldVisibilitySources=visibilitySources,
            GraphicDisplays=new[]{graphic,BlobResultDisplay.InnerRectangle,BlobResultDisplay.Inspection,BlobResultDisplay.ReferencePoint,
                BlobResultDisplay.RunPoint,BlobResultDisplay.Mask,BlobResultDisplay.Annulus}
                .Select(group=>new SunEyeVision.Plugin.Wpf.Fields.NativeGraphicDisplayOptions(group,Id(group,"visible"),Id(group,"ok"),Id(group,"ng"),Id(group,"opacity"),Id(group,"width"))).ToArray(),
            VisibilityDisplays=new[]{new SunEyeVision.Plugin.Wpf.Fields.NativeVisibilityDisplayOptions(Id(text,"visible")),new SunEyeVision.Plugin.Wpf.Fields.NativeVisibilityDisplayOptions(Id(SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Outline,"visible"))},
            TextDisplays=new[]{new SunEyeVision.Plugin.Wpf.Fields.NativeTextDisplayOptions(Id(text,"content"),Id(text,"ok"),Id(text,"ng"),Id(text,"size"),Id(text,"opacity"),Id(text,"x"),Id(text,"y"))}};
    }

}
