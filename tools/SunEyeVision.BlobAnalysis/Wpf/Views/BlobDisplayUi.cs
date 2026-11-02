using System.Windows;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

internal static class BlobDisplayUi
{
    internal static ToolSettingsViewOptions AddUnavailableRows(ToolSettingsViewOptions options,ToolSchema schema,bool supportedWindow=false,Action<NativeDisplayUiDraftRows>? observe=null)
    {
        var display=new NativeDisplayUiDraftRows("图像显示",new[]{
            new NativeDisplayUiDraftEntry("最大内接矩形",true),
            new NativeDisplayUiDraftEntry("轮廓点",false),new NativeDisplayUiDraftEntry("基准点",true,InitiallyVisible:false),
            new NativeDisplayUiDraftEntry("运行点",true,InitiallyVisible:false)});
        if(options.AdditionalAfterContent is {} assist){display.BindEntryVisibility(2,assist);display.BindEntryVisibility(3,assist);}
        var views=display.ExtractEntryViews();
        foreach(var view in views)
        {
            if(supportedWindow)view.IsEnabled=false;
            view.ToolTip="此显示对象尚未接入。";
            System.Windows.Controls.ToolTipService.SetShowOnDisabled(view,true);
        }
        observe?.Invoke(display);
        var rows=options.AdditionalAnchoredContents?.ToList()??new List<AnchoredSettingsContent>();
        var textEye=SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Field(SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Text,"visible");
        rows.Add(new(SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Field(SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Inspection,"visible"),views[0],FollowAnchorVisibility:false,InsertBefore:true));
        foreach(var view in views.Skip(1))rows.Add(new(textEye,view,FollowAnchorVisibility:false,InsertBefore:true));
        var fields=schema.Presentation!.Fields.Where(field=>field.Group=="结果判定").ToArray();
        var suppressed=options.SuppressedFieldRows?.ToHashSet(StringComparer.Ordinal)??new HashSet<string>(StringComparer.Ordinal);
        foreach(var field in fields)suppressed.Add(field.FieldId);
        if(fields.Length!=0)
        {
            var judgment=new NativeJudgmentUiDraftControls {IsEnabled=false,ToolTip="原厂判定聚合、空结果及比较语义尚未接通，当前不能编辑。"};
            System.Windows.Controls.ToolTipService.SetShowOnDisabled(judgment,true);
            rows.Add(new(fields[0].FieldId,judgment,FollowAnchorVisibility:false,InsertBefore:true));
        }
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
        return options with {AdditionalAnchoredContents=rows,SuppressedFieldRows=suppressed,TextExpressions=expressions,
            GraphicDisplays=new[]{graphic,SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Inspection,SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Mask,SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Annulus}.Select(group=>new SunEyeVision.Plugin.Wpf.Fields.NativeGraphicDisplayOptions(group,Id(group,"visible"),Id(group,"ok"),Id(group,"ng"),Id(group,"opacity"),Id(group,"width"))).ToArray(),
            VisibilityDisplays=new[]{new SunEyeVision.Plugin.Wpf.Fields.NativeVisibilityDisplayOptions(Id(text,"visible"))},
            TextDisplays=new[]{new SunEyeVision.Plugin.Wpf.Fields.NativeTextDisplayOptions(Id(text,"content"),Id(text,"ok"),Id(text,"ng"),Id(text,"size"),Id(text,"opacity"),Id(text,"x"),Id(text,"y"))}};
    }
}
