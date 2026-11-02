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
            new NativeDisplayUiDraftEntry("最大内接矩形",true),new NativeDisplayUiDraftEntry("ROI",true),
            new NativeDisplayUiDraftEntry("轮廓点",false),new NativeDisplayUiDraftEntry("基准点",true,InitiallyVisible:false),
            new NativeDisplayUiDraftEntry("运行点",true,InitiallyVisible:false),new NativeDisplayUiDraftEntry("屏蔽区域",true),
            new NativeDisplayUiDraftEntry("扇环检测区域",true)});
        if(options.AdditionalAfterContent is {} assist){display.BindEntryVisibility(3,assist);display.BindEntryVisibility(4,assist);}
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
        foreach(var view in views.Take(5))rows.Add(new(textEye,view,FollowAnchorVisibility:false,InsertBefore:true));
        foreach(var view in views.Skip(5))rows.Add(new(textEye,view,FollowAnchorVisibility:false));
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
        expressions.Add(Id(text,"content"),new(()=>new(Array.Empty<SunEyeVision.Plugin.Wpf.Adapters.References.DeclaredReferenceCandidate>(),
            _=>throw new InvalidOperationException("动态文本来源尚未接入。"),()=>true),value=>
            {try{SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.ValidateContent(value);return null;}catch(ArgumentException error){return error.Message;}}));
        return options with {AdditionalAnchoredContents=rows,SuppressedFieldRows=suppressed,TextExpressions=expressions,
            GraphicDisplays=new[]{new SunEyeVision.Plugin.Wpf.Fields.NativeGraphicDisplayOptions(graphic,Id(graphic,"visible"),Id(graphic,"ok"),Id(graphic,"ng"),Id(graphic,"opacity"),Id(graphic,"width"))},
            VisibilityDisplays=new[]{new SunEyeVision.Plugin.Wpf.Fields.NativeVisibilityDisplayOptions(Id(text,"visible"))},
            TextDisplays=new[]{new SunEyeVision.Plugin.Wpf.Fields.NativeTextDisplayOptions(Id(text,"content"),Id(text,"ok"),Id(text,"ng"),Id(text,"size"),Id(text,"opacity"),Id(text,"x"),Id(text,"y"))}};
    }
}
