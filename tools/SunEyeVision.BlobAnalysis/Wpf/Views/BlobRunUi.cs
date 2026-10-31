using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>Unavailable original-page roles; real settings continue to use the single host draft.</summary>
internal static class BlobRunUi
{
    internal static ToolSettingsViewOptions AddUnavailableRows(ToolSettingsViewOptions options)
    {
        var rows=options.AdditionalAnchoredContents?.ToList()??new List<AnchoredSettingsContent>();
        var created=new List<DeclaredFieldEditor>();
        var initialValues=new Dictionary<DeclaredFieldEditor,ParameterSetting>();
        var ranges=new List<NativeUiDraftRange>();
        var conditionalRows=new List<NativeUiConditionalFields>();
        NativeThresholdUiDraftControls? thresholdUi=null;
        try
        {
            DeclaredFieldEditor Create(Port port,string label,ParameterSetting initial,FieldLayout layout,
                FieldEditorKind kind=FieldEditorKind.Scalar,IReadOnlyDictionary<string,string>? choices=null)
            {
                var presentation=new FieldPresentation(port.Id,label,"运行参数",0,kind,choices?.Keys,choices){Layout=layout};
                var editor=new DeclaredFieldEditor(port,label,initial,_=>{},ReferenceFieldOptions.Empty,
                    choiceLabels:choices,presentation:presentation)
                    {ToolTip="界面试用状态，功能链尚未接入。"};
                created.Add(editor);initialValues.Add(editor,initial);return editor;
            }
            var layout=new FieldLayout("运行参数","运行参数");
            var fill=Create(new Port("ui-only-run.hole-area",DataType.Integer,Storage:NumericStorage.Int32,
                    EditModes:FieldEditModes.Constant|FieldEditModes.Reference),"填充面积阈值",new ConstantSetting(0L),
                layout with{Minimum=0,Maximum=999999999,DecimalPlaces=0,Increment=1});
            var labels=new Dictionary<string,string>{{"NoneContour","不输出轮廓"},
                {"NoneSequentialContour","输出无序轮廓"},{"SequentialContour","输出有序轮廓"}};
            var contour=Create(new Port("ui-only-run.contour-type",DataType.Text,EditModes:FieldEditModes.Constant,
                    Choices:new TextChoiceConstraint(labels.Keys)),"输出轮廓类型",new ConstantSetting("NoneContour"),
                layout,FieldEditorKind.Choice,labels);
            var blob=Create(new Port("ui-only-run.blob-image",DataType.Boolean,EditModes:FieldEditModes.Constant),
                "Blob图像输出",new ConstantSetting(true),layout);
            rows.Add(new("field:e748553b7cb45d1a8003c2b345b1d98f",fill,FollowAnchorVisibility:false));
            rows.Add(new("field:fcdb01b095c25c6ea22384f993450aac",contour,FollowAnchorVisibility:false));
            rows.Add(new("field:fcdb01b095c25c6ea22384f993450aac",blob,FollowAnchorVisibility:false));
            const string circleAnchor="field:7d59f57f71c65911b88bae5f90d994d9";
            const string sortAnchor="field:cb01714e6b0b503a812fe7a99f1c815e";
            var advanced=layout with{Section="高级",Advanced=true};
            void Toggle(string id,string label,string anchor,bool before)
            {
                var editor=Create(new Port("ui-only-run."+id,DataType.Boolean,EditModes:FieldEditModes.Constant),
                    label,new ConstantSetting(false),advanced);
                var specification=id switch
                {
                    "angle"=>("角度范围",-9000d,9000d,-9000d,9000d,true),
                    "rect-width"=>("RECT宽范围",1d,1000000d,100d,200d,true),
                    "rect-height"=>("RECT高范围",1d,1000000d,100d,200d,true),
                    "perimeter"=>("周长范围",1d,999999999d,10d,999999999d,true),
                    "short-axis"=>("短轴范围",1d,999999999d,1d,999999999d,true),
                    "long-axis"=>("长轴范围",1d,999999999d,10d,999999999d,true),
                    "centroid-bias"=>("质心偏移范围",0d,9999999.99d,0d,9999999.99d,false),
                    "axis-ratio"=>("轴比范围",.01d,1d,.01d,1d,false),
                    "circularity"=>("圆度范围",0d,1d,0d,1d,false),
                    "rectangularity"=>("矩形度范围",0d,1d,0d,1d,false),
                    _=>default
                };
                FrameworkElement content=editor;
                if(id=="outside-pixels")
                {
                    var value=Create(new Port("ui-only-run.outside-count",DataType.Integer,Storage:NumericStorage.Int32,
                        EditModes:FieldEditModes.Constant|FieldEditModes.Reference),"最大超界像素",new ConstantSetting(0L),
                        advanced with{Minimum=0,Maximum=9000000,DecimalPlaces=0,Increment=1});
                    var conditional=new NativeUiConditionalFields(editor,value);conditionalRows.Add(conditional);content=conditional;
                }
                if(specification.Item1 is not null)
                {
                    var range=new NativeUiDraftRange(editor,"ui-only-run."+id,specification.Item1,specification.Item2,
                        specification.Item3,specification.Item4,specification.Item5,specification.Item6);
                    ranges.Add(range);content=range;
                }
                rows.Add(new(anchor,content,FollowAnchorVisibility:false,InsertBefore:before));
            }
            foreach(var (id,label) in new[]{("histogram","直方图信息输出"),("angle","角度使能"),
                ("rect-width","RECT宽使能"),("rect-height","RECT高使能"),("perimeter","周长使能"),
                ("short-axis","短轴使能"),("long-axis","长轴使能")})
                Toggle(id,label,circleAnchor,true);
            Toggle("circularity","圆度使能",circleAnchor,false);
            Toggle("rectangularity","矩形度使能","field:34886e8b5b765d49b8662bb061b02bf7",false);
            Toggle("centroid-bias","质心偏移使能",sortAnchor,true);
            Toggle("axis-ratio","轴比使能",sortAnchor,true);
            var sortFeatures=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var (key,resource) in new[]{("SortFeatureArea","Area"),("SortFeaturePerimeter","Perimeter"),
                ("SortFeatureCircularity","CircleScore"),("SortFeatureRect","RectScore"),
                ("SortFeatureCentroidX","Connectivity center X"),("SortFeatureCentroidY","Connectivity center Y"),
                ("SortFeatureBoxAngle","box angle"),("SortFeatureBoxWidth","box width"),("SortFeatureBoxHeight","box height"),
                ("SortFeatureRectX","Rectangle left upper x"),("SortFeatureRectY","Rectangle left upper y"),
                ("SortFeatureAxisAngle","Two step spindle angle"),("SortFeatureAxisRatio","MinAxis / LongAxis")})
                sortFeatures.Add(key,AppLocalizationService.GetLang(resource));
            var feature=Create(new Port("ui-only-run.sort-feature",DataType.Text,EditModes:FieldEditModes.Constant,
                Choices:new TextChoiceConstraint(sortFeatures.Keys)),"排序特征",new ConstantSetting("SortFeatureArea"),advanced,
                FieldEditorKind.Choice,sortFeatures);
            rows.Add(new(sortAnchor,feature,FollowAnchorVisibility:false,InsertBefore:true));
            var connectivity=new Dictionary<string,string>{{"Connected_8","8连通"},{"Connected_4","4连通"}};
            var connected=Create(new Port("ui-only-run.connectivity",DataType.Text,EditModes:FieldEditModes.Constant,
                Choices:new TextChoiceConstraint(connectivity.Keys)),"连通性",new ConstantSetting("Connected_8"),advanced,
                FieldEditorKind.Choice,connectivity);
            rows.Add(new(sortAnchor,connected,FollowAnchorVisibility:false));
            var inner=Create(new Port("ui-only-run.inner-count",DataType.Integer,Storage:NumericStorage.Int32,
                EditModes:FieldEditModes.Constant|FieldEditModes.Reference),"最大内接矩形输出数量",new ConstantSetting(0L),
                advanced with{Minimum=0,Maximum=10,DecimalPlaces=0,Increment=1});
            rows.Add(new(sortAnchor,inner,FollowAnchorVisibility:false));
            Toggle("outside-pixels","像素超界使能",sortAnchor,false);
            var widths=options.PageLabelWidths?.ToDictionary(entry=>entry.Key,entry=>entry.Value,StringComparer.Ordinal)
                ??new Dictionary<string,double>(StringComparer.Ordinal);
            widths["运行参数"]=120;
            var suppressed=options.SuppressedFieldRows?.ToHashSet(StringComparer.Ordinal)??new HashSet<string>(StringComparer.Ordinal);
            suppressed.Add("field:2fff619e51a359a6a9af286c5e8fef65");
            suppressed.Add("field:3799fbc2f80e58bf9abc7d2c0ad70067");
            suppressed.Add("field:96e3b794dc875d55b37e5b10cff52047");
            suppressed.Add("field:e748553b7cb45d1a8003c2b345b1d98f");
            suppressed.Add(sortAnchor);
            foreach(var id in new[]{circleAnchor,"field:da4d70261e23551e9a5bd832b2a686d1","field:1a8be75176b350f09f3cd64506c136da",
                "field:34886e8b5b765d49b8662bb061b02bf7","field:13d8c5e0c7445f1087497a21f4103a5e","field:c38989cfdf9e51799d27e0eeee34e726"})suppressed.Add(id);
            foreach(var id in new[]{"field:e9845ab7a46e5ee9b42310bef5a11c19","field:fcdb01b095c25c6ea22384f993450aac",
                "field:cdce3e942b555fa58489e08eeb39236f","field:73f2c01e7b385e0ab2d35f739a88e718"})suppressed.Add(id);
            var areaToggle=Create(new Port("ui-only-run.area",DataType.Boolean,EditModes:FieldEditModes.Constant),
                "面积使能",new ConstantSetting(true),layout);
            var areaRange=new NativeUiDraftRange(areaToggle,"ui-only-run.area","面积范围",1,999999999,10,999999999,true);
            ranges.Add(areaRange);
            rows.Add(new("field:e9845ab7a46e5ee9b42310bef5a11c19",areaRange,FollowAnchorVisibility:false,InsertBefore:true));
            var binary=Create(new Port("ui-only-run.binary-output",DataType.Boolean,EditModes:FieldEditModes.Constant),
                "二值化图像输出",new ConstantSetting(true),layout);
            rows.Add(new("field:73f2c01e7b385e0ab2d35f739a88e718",binary,FollowAnchorVisibility:false,InsertBefore:true));
            var count=Create(new Port("ui-only-run.find-count",DataType.Integer,Storage:NumericStorage.Int32,
                EditModes:FieldEditModes.Constant|FieldEditModes.Reference),"查找个数",new ConstantSetting(100L),
                layout with{Minimum=1,Maximum=20000,DecimalPlaces=0,Increment=1});
            rows.Add(new("field:96e3b794dc875d55b37e5b10cff52047",count,FollowAnchorVisibility:false,InsertBefore:true));
            var sortModes=new Dictionary<string,string>{{"ascending","升序"},{"descending","降序"},{"none","不排序"}};
            var sort=Create(new Port("ui-only-run.sort-mode",DataType.Text,EditModes:FieldEditModes.Constant,
                Choices:new TextChoiceConstraint(sortModes.Keys)),"排序方式",new ConstantSetting("descending"),advanced,
                FieldEditorKind.Choice,sortModes);
            rows.Add(new(sortAnchor,sort,FollowAnchorVisibility:false,InsertBefore:true));
            foreach(var id in new[]{"field:8ca20000000040008000000000000001","field:88b45ab61b965761b901670fd587903f",
                "field:43bda03261df55b88f963bd18f2ba4d9","field:8ca20000000040008000000000000002"})suppressed.Add(id);
            thresholdUi=new NativeThresholdUiDraftControls();
            rows.Add(new("field:8ca20000000040008000000000000001",thresholdUi,FollowAnchorVisibility:false,InsertBefore:true));
            return options with {AdditionalAnchoredContents=rows,PageLabelWidths=widths,SuppressedFieldRows=suppressed,
                ValidateUiState=()=>
                {
                    var previous=options.ValidateUiState?.Invoke();
                    if(!string.IsNullOrEmpty(previous))return previous;
                    if(thresholdUi is not null)return "当前阈值为纯界面试用，功能接入前不能执行或保存。";
                    return ranges.Any(range=>range.HasChanges)||initialValues.Any(entry=>entry.Key.HasPendingEdits||!Equals(entry.Key.Setting,entry.Value))
                        ?"当前修改包含尚未接入功能的界面试用项，请恢复这些项后执行或保存。":null;
                }};
        }
        catch(Exception failure)
        {
            try{thresholdUi?.Dispose();foreach(var row in conditionalRows)row.Dispose();foreach(var range in ranges)range.Dispose();foreach(var editor in created)editor.Dispose();}
            catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
    }
}
