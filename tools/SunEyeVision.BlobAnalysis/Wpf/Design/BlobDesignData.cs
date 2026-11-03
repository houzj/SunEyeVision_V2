using SunEyeVision.Plugin.Wpf.Adapters.References;
using System.Windows;
using System.ComponentModel;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using System.Windows.Controls;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Design;

/// <summary>Private design-only values. No discovery, documents, algorithm sessions or native resources.</summary>
public static class BlobDesignData
{
    public static readonly DependencyProperty ParameterSampleProperty=DependencyProperty.RegisterAttached(
        "ParameterSample",typeof(bool),typeof(BlobDesignData),new PropertyMetadata(false,AttachParameterSample));
    public static bool GetParameterSample(DependencyObject view)=>(bool)view.GetValue(ParameterSampleProperty);
    public static void SetParameterSample(DependencyObject view,bool value)=>view.SetValue(ParameterSampleProperty,value);
    private static void AttachParameterSample(DependencyObject view,DependencyPropertyChangedEventArgs args)
    {
        // Attach to the real child, so displaying the root XAML does not depend on its code-behind constructor.
        if(args.NewValue is true&&DesignerProperties.GetIsInDesignMode(view)&&view is DeclaredSettingsView presenter)
        {var sample=CreateParameterContext();BindParameterContext(presenter,sample);}
    }
    public static (IToolSettingsDraft Draft,ToolSettingsViewOptions Options) CreateParameterContext()
    {
        var draft=new DesignDraft();var schema=draft.Tool.Describe();
        var inputs=new DeclaredInputFieldsView(schema,_=>new UnsetSetting(),(_,_)=>{},ReferenceFieldOptions.Empty);
        return(draft,new(ReferenceOptions:ReferenceFieldOptions.Empty,InputSection:schema.Presentation!.Fields.First().Layout!.Page,InputContent:inputs));
    }
    private static readonly DependencyProperty CompositionProperty=DependencyProperty.RegisterAttached("Composition",typeof(BlobUiComposition),typeof(BlobDesignData));
    internal static BlobUiComposition? GetComposition(DeclaredSettingsView presenter)=>presenter.GetValue(CompositionProperty) as BlobUiComposition;
    public static void BindParameterContext(DeclaredSettingsView presenter,(IToolSettingsDraft Draft,ToolSettingsViewOptions Options) context)
    {
        if(GetComposition(presenter) is {} previous)
        {
            if(!previous.IsDisposed&&ReferenceEquals(previous.Draft,context.Draft)&&ReferenceEquals(previous.BaseOptions,context.Options))
            {previous.RebindSameContext();return;}
            previous.Dispose();presenter.ClearValue(CompositionProperty);
        }
        var composition=new BlobUiComposition(presenter,context.Draft,context.Options);presenter.SetValue(CompositionProperty,composition);
    }
    private sealed class DesignDraft : IToolSettingsDraft
    {
        internal BlobTool Tool {get;}=new();
        private readonly FieldSettingsDraft _draft;
        public DesignDraft()
        {
            var declaration=(IToolSettingsFields)Tool;
            var schema=SunEyeVision.Plugin.Contracts.Results.Presentation.ResultDisplayFields.EditorSchema(Tool);
            var values=declaration.ReadSettings(new BlobSettings()).CopyValues().Concat(Tool.CreateDisplaySettings().CopyValues())
                .ToDictionary(pair=>pair.Key,pair=>(ParameterSetting)new ConstantSetting(pair.Value),StringComparer.Ordinal);
            _draft=new(schema,new FieldSettings(schema.Inputs,values));
        }
        public ToolSchema Schema=>_draft.Schema;
        public long Revision=>_draft.Revision;
        public FieldSettings CaptureFields()=>_draft.CaptureFields();
        public FieldSettings CaptureValidatedFields()
        {
            var fields=_draft.CaptureValidatedFields();
            if(fields.Capture().Values.Any(value=>value is ReferenceSetting))return fields;
            var settings=((IToolSettingsFields)Tool).MaterializeSettings(fields.Materialize(new Dictionary<string,object?>()));
            var errors=((IToolDefinition)Tool).Validate(settings);
            if(errors.Count!=0)throw new ArgumentException(string.Join("; ",errors.Select(error=>error.Message)));
            return fields;
        }
        public void ApplyFields(IReadOnlyDictionary<string,ParameterSetting> changes,Action<FieldSettings> acceptCandidate)=>_draft.ApplyFields(changes,acceptCandidate);
    }
}

/// <summary>Designer uses the same declared table as runtime, without a workflow or algorithm session.</summary>
public static class BlobResultDesignData
{
    public static readonly DependencyProperty DeclarationPreviewProperty=DependencyProperty.RegisterAttached(
        "DeclarationPreview",typeof(bool),typeof(BlobResultDesignData),new PropertyMetadata(false,Attach));
    public static bool GetDeclarationPreview(DependencyObject target)=>(bool)target.GetValue(DeclarationPreviewProperty);
    public static void SetDeclarationPreview(DependencyObject target,bool value)=>target.SetValue(DeclarationPreviewProperty,value);
    private static void Attach(DependencyObject target,DependencyPropertyChangedEventArgs args)
    {
        if(target is not ContentControl host)throw new ArgumentException("结果预览需要内容宿主。");
        if(args.NewValue is true&&DesignerProperties.GetIsInDesignMode(host))
        {
            var tool=new BlobTool();
            var schema=tool.ResultSchema;
            var table=new DeclaredResultTable(schema,tool.ResultProjection);
            var region=new SampleRegion();
            var points=new[]{new Point2(20,20),new Point2(40,20),new Point2(40,32),new Point2(20,32)};
            var secondPoints=new[]{new Point2(49,33),new Point2(61,33),new Point2(61,41),new Point2(49,41)};
            var records=new[] {
                new BlobMeasurement(1,1,240,64,30,26,0.74,0.92,new BlobBounds(20,20,20,12),new BlobOrientedBounds(30,26,20,12,0),points,Array.Empty<BlobBoundaryPath>(),region,true),
                new BlobMeasurement(2,2,96,40,55,37,0.81,0.88,new BlobBounds(49,33,12,8),new BlobOrientedBounds(55,37,12,8,0),secondPoints,Array.Empty<BlobBoundaryPath>(),region,true)
            };
            object? Sample(Port port)
            {
                if(port.Id==BlobSchema.BlobsPort)
                    return new SequenceValue(BlobSchema.BlobsType,records.Select(record=>(object?)BlobSchema.Freeze(record)));
                if(port.Nullable)return null;
                return port.Type.Shape switch {
                    ValueShape.Resource when port.Type.Resource==ResourceKind.Region=>region,
                    ValueShape.Scalar=>port.Type.Scalar switch {
                        ScalarKind.Boolean=>true,ScalarKind.Int64=>0L,ScalarKind.Float64=>0d,ScalarKind.Text=>string.Empty,
                        _=>throw new NotSupportedException("未知结果示例标量类型。")},
                    ValueShape.Sequence when port.Type.Element==(TypeRef)DataType.Number=>Array.Empty<double>(),
                    ValueShape.Sequence=>new SequenceValue(port.Type,Array.Empty<object?>()),
                    _=>throw new NotSupportedException("未知结果示例声明类型。")};
            }
            var values=new InputValues(schema.Outputs,schema.Outputs.ToDictionary(port=>port.Id,Sample,StringComparer.Ordinal));
            var run=new RunIdentity(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"design",Guid.NewGuid());
            var publication=new ResultPublication(schema,new(Guid.NewGuid(),run,0,schema.Fingerprint),
                ResultViewState.Success,values,null,()=>true);
            table.Present(publication);
            host.Content=table;
        }
    }

    // Managed-only sample: no image acquisition, native handle, workflow or algorithm execution.
    private sealed class SampleRegion : IReadOnlyRegion
    {
        public CoordinateFrame Frame=>BlobSchema.ImageFrame;
        public ImageFrameAddress SourceFrame {get;}=new(Guid.NewGuid(),"design-result");
        public IRegionReadLease AcquireReader()=>new Reader(this);
        private sealed class Reader(SampleRegion region) : IRegionReadLease
        {
            public CoordinateFrame Frame=>region.Frame;
            public ImageFrameAddress SourceFrame=>region.SourceFrame;
            public bool IsEmpty=>true;
            public int RunCount=>0;
            public ReadOnlyMemory<RegionRun> ReadRuns(int offset,int maximumCount)
            {
                ResourceContractRules.ValidateBatch(offset,maximumCount,0);
                return ReadOnlyMemory<RegionRun>.Empty;
            }
            public void Dispose() { }
        }
    }
}
