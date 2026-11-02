using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>Original run-page UI; supported fields stay on the host draft and previews stay in this window.</summary>
internal static class BlobRunUi
{
    internal static ToolSettingsViewOptions AddRows(ToolSettingsViewOptions options,IToolSettingsDraft draft,DeclaredSettingsView settingsView,Func<string?> validateSupportedState,Action<BlobRunUiState>? observe=null)
    {
        var rows=options.AdditionalAnchoredContents?.ToList()??new List<AnchoredSettingsContent>();
        var created=new List<DeclaredFieldEditor>();
        var ranges=new List<NativeUiDraftRange>();
        var conditionalRows=new List<NativeUiConditionalFields>();

        NativeThresholdUiDraftControls? threshold=null;BlobRunUiState? state=null;
        try
        {
            DeclaredFieldEditor Create(Port port,string label,ParameterSetting initial,FieldLayout layout,
                FieldEditorKind kind=FieldEditorKind.Scalar,IReadOnlyDictionary<string,string>? choices=null)
            {
                var presentation=new FieldPresentation(port.Id,label,"运行参数",0,kind,choices?.Keys,choices){Layout=layout};
                var editor=new DeclaredFieldEditor(port,label,initial,_=>{},ReferenceFieldOptions.Empty,
                    choiceLabels:choices,presentation:presentation)
                    ;
                created.Add(editor);return editor;
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
                        EditModes:FieldEditModes.Constant|FieldEditModes.Reference),AppLocalizationService.GetLang("MaxOutPixelNum"),new ConstantSetting(0L),
                        advanced with{Minimum=0,Maximum=9000000,DecimalPlaces=0,Increment=1});
                    var conditional=new NativeUiConditionalFields(editor,value);conditionalRows.Add(conditional);content=conditional;
                }
                if(specification.Item1 is not null)
                {
                    var range=new NativeUiDraftRange(editor,"ui-only-run."+id,specification.Item1,specification.Item2,
                        specification.Item3,specification.Item4,specification.Item5,specification.Item6,upperMinimum:id=="axis-ratio"?0:null);
                    ranges.Add(range);content=range;
                }
                rows.Add(new(anchor,content,FollowAnchorVisibility:false,InsertBefore:before));
            }
            foreach(var (id,label) in new[]{("histogram","直方图信息输出"),("angle","角度使能"),
                ("rect-width","RECT宽使能"),("rect-height","RECT高使能"),("perimeter","周长使能"),
                ("short-axis","短轴使能"),("long-axis","长轴使能")})
                Toggle(id,label,circleAnchor,true);

            Toggle("centroid-bias","质心偏移使能","field:8ca20000000040008000000000000004",true);
            Toggle("axis-ratio","轴比使能","field:8ca20000000040008000000000000004",true);
            var sortFeatures=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var (key,resource) in new[]{("SortFeatureArea","Area"),("SortFeaturePerimeter","Perimeter"),
                ("SortFeatureCircularity","CircleScore"),("SortFeatureRect","RectScore"),
                ("SortFeatureCentroidX","Connectivity center X"),("SortFeatureCentroidY","Connectivity center Y"),
                ("SortFeatureBoxAngle","box angle"),("SortFeatureBoxWidth","box width"),("SortFeatureBoxHeight","box height"),
                ("SortFeatureRectX","Rectangle left upper x"),("SortFeatureRectY","Rectangle left upper y"),
                ("SortFeatureAxisAngle","Two step spindle angle"),("SortFeatureAxisRatio","MinAxis / LongAxis")})
                sortFeatures.Add(key,AppLocalizationService.GetLang(resource));
            var sortFeatureId="field:8ca20000000040008000000000000004";
            var sortInitial=draft.CaptureFields().Read(sortFeatureId);
            var sortPreview=Create(new Port("ui-only-run.sort-feature",DataType.Text,EditModes:FieldEditModes.Constant,Choices:new TextChoiceConstraint(sortFeatures.Keys)),
                "排序特征",sortInitial,advanced,FieldEditorKind.Choice,sortFeatures);
            rows.Add(new(sortFeatureId,sortPreview,FollowAnchorVisibility:false,InsertBefore:true));
            var inner=Create(new Port("ui-only-run.inner-count",DataType.Integer,Storage:NumericStorage.Int32,
                EditModes:FieldEditModes.Constant|FieldEditModes.Reference),AppLocalizationService.GetLang("RunParam_MaxAreaInnerRectNum"),new ConstantSetting(0L),
                advanced with{Minimum=0,Maximum=10,DecimalPlaces=0,Increment=1});
            rows.Add(new("field:8ca20000000040008000000000000003",inner,FollowAnchorVisibility:false));
            Toggle("outside-pixels",AppLocalizationService.GetLang("RunParam_MaxOutPixelNumEnable"),"field:8ca20000000040008000000000000003",false);
            var suppressed=options.SuppressedFieldRows?.ToHashSet(StringComparer.Ordinal)??new HashSet<string>(StringComparer.Ordinal);
            suppressed.Add("field:2fff619e51a359a6a9af286c5e8fef65");
            suppressed.Add("field:3799fbc2f80e58bf9abc7d2c0ad70067");



            const string mode="field:8ca20000000040008000000000000001";
            threshold=new NativeThresholdUiDraftControls(externalSingleFields:true);
            rows.Add(new(mode,threshold,FollowAnchorVisibility:false,InsertBefore:true));
            suppressed.Add(mode);suppressed.Add(sortFeatureId);suppressed.Add("field:8ca20000000040008000000000000002");
            var gates=options.FieldVisibilitySources?.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal)??new Dictionary<string,FrameworkElement>(StringComparer.Ordinal);
            foreach(var field in new[]{"field:88b45ab61b965761b901670fd587903f","field:43bda03261df55b88f963bd18f2ba4d9"})gates[field]=threshold.SingleVisibilitySource;
            var displays=options.ChoiceDisplays?.ToDictionary(entry=>entry.Key,entry=>entry.Value,StringComparer.Ordinal)
                ??new Dictionary<string,DeclaredChoiceDisplayOptions>(StringComparer.Ordinal);
            var supportedFeatures=new HashSet<string>(new[]{"SortFeatureArea","SortFeaturePerimeter","SortFeatureCircularity","SortFeatureRect","SortFeatureCentroidX","SortFeatureCentroidY"},StringComparer.Ordinal);
            displays.Add(sortFeatureId,new(sortFeatures.Select(pair=>new DeclaredChoiceDisplayItem(pair.Key,pair.Value,
                supportedFeatures.Contains(pair.Key)?pair.Key:null,supportedFeatures.Contains(pair.Key),
                supportedFeatures.Contains(pair.Key)?null:"该排序特征的正式数值语义尚未实现。"))));
            displays.Add(mode,new(new[]{
                new DeclaredChoiceDisplayItem("BinaryImage","不进行二值化","None",false,"当前算法语义尚未统一，不能确认或执行此模式。"),
                new DeclaredChoiceDisplayItem("SingleThreshold","单阈值","SingleThreshold",true),
                new DeclaredChoiceDisplayItem("DoubleThreshold","双阈值","DoubleThreshold",false,"当前双阈值算法极性与原厂显隐不一致，不能确认或执行。"),
                new DeclaredChoiceDisplayItem("AutoThreshold","自动阈值",null,false,"自动阈值尚未实现。"),
                new DeclaredChoiceDisplayItem("FixedSoftThreshold","软阈值（固定）",null,false,"固定软阈值尚未实现。"),
                new DeclaredChoiceDisplayItem("RelativeSoftThreshold","软阈值（相对）",null,false,"相对软阈值尚未实现。")
            }));
            state=new BlobRunUiState(draft,settingsView,threshold,sortPreview,supportedFeatures,created,ranges);
            observe?.Invoke(state);
            return options with {AdditionalAnchoredContents=rows,SuppressedFieldRows=suppressed,ChoiceDisplays=displays,FieldVisibilitySources=gates,
                ReferenceOptions=options.ReferenceOptions is {} reference?reference with{EditableIndex=true}:null,
                ValidateUiState=()=>
                {
                    var previous=options.ValidateUiState?.Invoke();
                    if(!string.IsNullOrEmpty(previous))return previous;
                    return validateSupportedState();
                }};
        }
        catch(Exception failure)
        {
            var errors=new List<Exception>{failure};
            var cleanup=new Action[]{()=>state?.Dispose(),()=>threshold?.Dispose()}
                .Concat(conditionalRows.Select(row=>(Action)row.Dispose))
                .Concat(ranges.Select(range=>(Action)range.Dispose))
                .Concat(created.Select(editor=>(Action)editor.Dispose));
            foreach(var action in cleanup)try{action();}catch(Exception error){errors.Add(error);}
            if(errors.Count>1)throw new AggregateException(errors);
            throw;
        }
    }
}
