using System.Windows;
using System.ComponentModel;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Tool.BlobAnalysis.Presentation;

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
        {var sample=CreateParameterContext();presenter.Bind(sample.Draft,sample.Options);}
    }
    public static (IToolSettingsDraft Draft,ToolSettingsViewOptions Options) CreateParameterContext()
    {
        var draft=new DesignDraft();var schema=draft.Tool.Describe((BlobSettings)draft.CaptureSettings());
        var inputs=new DeclaredInputFieldsView(schema,_=>new UnsetSetting(),(_,_)=>{},_=>null);
        return(draft,new(SelectReference:_=>null,InputSection:schema.Presentation!.Fields.First().Layout!.Page,InputContent:inputs));
    }
    // Existing presentation row type, never a second settings/output model or a native execution result.
    public static IReadOnlyList<BlobAnalysisRow> ResultRows {get;}=Array.AsReadOnly(new[] {
        new BlobAnalysisRow(1,1,240,64,80,60,0.74,0.91,new Rect(70,52,20,16)),
        new BlobAnalysisRow(2,2,96,40,140,100,0.67,0.83,new Rect(134,96,12,10))
    });
    private sealed class DesignDraft : IToolSettingsDraft
    {
        internal BlobTool Tool {get;}=new();
        private BlobSettings _settings=new(); // All defaults come from the actual embedded BlobParameters.xml.
        public ToolSchema Schema=>Tool.SettingsSchema;
        public object CaptureSettings()=>_settings.Freeze();
        public InputValues ReadFields()=>Tool.ReadSettings(_settings);
        public void ApplyFields(IReadOnlyDictionary<string,object?> changes)
        {
            var candidate=(BlobSettings)Tool.ApplySettings(_settings,changes);
            var errors=Tool.Validate(candidate);
            if(errors.Count!=0)throw new ArgumentException(string.Join("; ",errors.Select(error=>error.Message)));
            _settings=candidate.Freeze();
        }
    }
}

/// <summary>Designer-only values for bindings in the actual result XAML; not a Runtime output.</summary>
public sealed class BlobResultDesignData
{
    private readonly BlobSettings _defaults=new();
    public IReadOnlyList<BlobAnalysisRow> FilteredBlobs=>BlobDesignData.ResultRows;
    public int FilteredBlobCount=>2;
    public double FilteredTotalArea=>336;
    public double AverageArea=>168;
    public double MaxArea=>240;
    public string ThresholdRangeText=>$"{_defaults.Threshold}–{_defaults.UpperThreshold}";
    public string ThresholdTypeText=>_defaults.Type==BlobThresholdMode.Binary?"正向阈值":"反向阈值";
}
