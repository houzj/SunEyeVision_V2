using System.Windows;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

internal static class BlobDisplayUi
{
    internal static ToolSettingsViewOptions Configure(ToolSettingsViewOptions options,ToolSchema schema)
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
        return options with {TextExpressions=expressions,
            GraphicDisplays=new[]{graphic,SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Inspection,SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Mask,SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Annulus}.Select(group=>new SunEyeVision.Plugin.Wpf.Fields.NativeGraphicDisplayOptions(group,Id(group,"visible"),Id(group,"ok"),Id(group,"ng"),Id(group,"opacity"),Id(group,"width"))).ToArray(),
            VisibilityDisplays=new[]{new SunEyeVision.Plugin.Wpf.Fields.NativeVisibilityDisplayOptions(Id(text,"visible")),new SunEyeVision.Plugin.Wpf.Fields.NativeVisibilityDisplayOptions(Id(SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Outline,"visible"))},
            TextDisplays=new[]{new SunEyeVision.Plugin.Wpf.Fields.NativeTextDisplayOptions(Id(text,"content"),Id(text,"ok"),Id(text,"ng"),Id(text,"size"),Id(text,"opacity"),Id(text,"x"),Id(text,"y"))}};
    }

    internal static ToolSettingsViewOptions AddRows(ToolSettingsViewOptions options,ToolSchema schema,FrameworkElement assistPointVisibility,Action<NativeDisplayUiDraftRows>? observe=null)
    {
        options=Configure(options,schema);
        var display=new NativeDisplayUiDraftRows("图像显示",new[]{
            new NativeDisplayUiDraftEntry("最大内接矩形",true),
            new NativeDisplayUiDraftEntry("基准点",true,InitiallyVisible:false),
            new NativeDisplayUiDraftEntry("运行点",true,InitiallyVisible:false)});
        display.BindEntryVisibility(1,assistPointVisibility);display.BindEntryVisibility(2,assistPointVisibility);
        var views=display.ExtractEntryViews();
        observe?.Invoke(display);
        var rows=options.AdditionalAnchoredContents?.ToList()??new List<AnchoredSettingsContent>();
        var textEye=SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Field(SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Text,"visible");
        rows.Add(new(SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Field(SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Inspection,"visible"),views[0],FollowAnchorVisibility:false,InsertBefore:true));
        foreach(var view in views.Skip(1))rows.Add(new(textEye,new PointVisibilityHost(view),FollowAnchorVisibility:false,InsertBefore:true));
        return options with {AdditionalAnchoredContents=rows};
    }
    // The generic row owns only this shell. Retiring it detaches the borrowed native view's logical binding.
    private sealed class PointVisibilityHost : System.Windows.Controls.Decorator,IDisposable
    {
        internal PointVisibilityHost(FrameworkElement view)=>Child=view;
        public void Dispose()
        {
            if(Child is not FrameworkElement view)return;
            System.Windows.Data.BindingOperations.ClearBinding(view,FrameworkElement.VisibilityProperty);Child=null;
        }
    }}
