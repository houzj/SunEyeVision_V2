using System.Windows;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

internal static class BlobDisplayUi
{
    internal static ToolSettingsViewOptions AddUnavailableRows(ToolSettingsViewOptions options,ToolSchema schema)
    {
        var pages=options.AdditionalPageContents?.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal)
            ??new Dictionary<string,FrameworkElement>(StringComparer.Ordinal);
        if(pages.ContainsKey("结果显示"))throw new ArgumentException("结果显示页面已有附加内容。");
        var display=new NativeDisplayUiDraftRows("图像显示",new[]{
            new NativeDisplayUiDraftEntry("Blob",true),
            new NativeDisplayUiDraftEntry("最大内接矩形",true),
            new NativeDisplayUiDraftEntry("ROI",true),
            new NativeDisplayUiDraftEntry("轮廓点",false),
            new NativeDisplayUiDraftEntry("基准点",true,InitiallyVisible:false),
            new NativeDisplayUiDraftEntry("运行点",true,InitiallyVisible:false),
            new NativeDisplayUiDraftEntry("文本",false),
            new NativeDisplayUiDraftEntry("屏蔽区域",true),
            new NativeDisplayUiDraftEntry("扇环检测区域",true)},includeTextSettings:true);
        if(options.AdditionalAfterContent is {} assist)
        {
            display.BindEntryVisibility(4,assist);display.BindEntryVisibility(5,assist);
        }
        pages.Add("结果显示",display);
        var fields=schema.Presentation!.Fields.Where(field=>field.Group=="结果判定").ToArray();
        var suppressed=options.SuppressedFieldRows?.ToHashSet(StringComparer.Ordinal)??new HashSet<string>(StringComparer.Ordinal);
        foreach(var field in fields)suppressed.Add(field.FieldId);
        var rows=options.AdditionalAnchoredContents?.ToList()??new List<AnchoredSettingsContent>();
        if(fields.Length!=0)
        {
            var judgment=new NativeJudgmentUiDraftControls {IsEnabled=false,ToolTip="原厂判定聚合、空结果及比较语义尚未接通，当前不能编辑。"};
            System.Windows.Controls.ToolTipService.SetShowOnDisabled(judgment,true);
            rows.Add(new(fields[0].FieldId,judgment,FollowAnchorVisibility:false,InsertBefore:true));
        }
        return options with {AdditionalPageContents=pages,AdditionalAnchoredContents=rows,SuppressedFieldRows=suppressed};
    }
}
