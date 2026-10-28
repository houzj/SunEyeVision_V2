using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Tool.BlobAnalysis.Presentation;

namespace SunEyeVision.Tool.BlobAnalysis.Views;

/// <summary>Read-only projection of published neutral values. No algorithm, SDK model or resource ownership.</summary>
public partial class BlobResultView : UserControl, IToolResultView, INotifyPropertyChanged
{
    private bool _disposed;
    private long _generation;
    private IResultPublication? _publication;
    private int _publishing;
    private readonly DisplayRows _rows=new();
    public FrameworkElement View => this;
    public IReadOnlyList<Port> Sources { get; } = Array.AsReadOnly(BlobSchema.Outputs
        .Where(port=>new[]{8,9,10,11,12,13,30,31,100}.Select(BlobSchema.Field).Contains(port.Id)).ToArray());
    public ReadOnlyObservableCollection<BlobAnalysisRow> FilteredBlobs {get;}
    public int FilteredBlobCount { get; private set; }
    public double TotalArea { get; private set; }
    public double FilteredTotalArea => TotalArea;
    public double AverageArea { get; private set; }
    public double MaxArea { get; private set; }
    public double MinArea { get; private set; }
    public string ThresholdRangeText { get; private set; } = "-";
    public string ThresholdTypeText { get; private set; } = "-";
    public BlobAnalysisRow? SelectedBlob { get; private set; }
    public event EventHandler<ResultSelection>? SelectionHighlightChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public BlobResultView()
    {
        FilteredBlobs=new(_rows);
        InitializeComponent();
        DataContext=this;
        if(DesignerProperties.GetIsInDesignMode(this))
        {
            var settings=new BlobSettings();
            PresentRows(Wpf.Design.BlobDesignData.ResultRows,2,336,168,96,240,
                settings.Threshold,settings.UpperThreshold,settings.Type.ToString());
        }
    }

    private void BlobDataGrid_SelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(_disposed||_publishing!=0)return;
        var publication=_publication;var generation=_generation;
        if(publication is null||!publication.IsCurrent()||_disposed||generation!=_generation||!ReferenceEquals(publication,_publication))return;
        SelectedBlob=BlobDataGrid.SelectedItem as BlobAnalysisRow;
        PropertyChanged?.Invoke(this,new(nameof(SelectedBlob)));
        if(_disposed||generation!=_generation||!ReferenceEquals(publication,_publication)||!publication.IsCurrent()||_disposed||generation!=_generation)return;
        var row=BlobDataGrid.SelectedItem as BlobAnalysisRow;
        if(row is not null&&!_rows.Any(item=>ReferenceEquals(item,row)))return;
        SelectionHighlightChanged?.Invoke(this,new(publication.Stamp,row?.ElementId,
            row is {HighlightGeometry:{} geometry,SourceFrame:{} frame}
                ?new[]{new ResultHighlightTarget(frame,geometry.Frame,new[]{geometry})}:Array.Empty<ResultHighlightTarget>()));
    }

    public void Present(IResultPublication publication)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        ArgumentNullException.ThrowIfNull(publication);
        var expected=_generation;
        bool Current()=>!_disposed&&expected==_generation&&publication.IsCurrent()&&!_disposed&&expected==_generation;
        if(publication.Schema.Fingerprint!=new ToolSchema(Array.Empty<Port>(),BlobSchema.Outputs).Fingerprint
            ||publication.Stamp.SchemaFingerprint!=publication.Schema.Fingerprint)
            throw new ArgumentException("Blob result publication differs from the frozen output schema.");
        if(!Current())return;
        using var lease=publication.Borrow();
        if(lease.Stamp!=publication.Stamp)throw new ArgumentException("Result values belong to another publication.");
        var values=lease.Values;
        if(values is null){if(Current())InstallEmpty(publication);return;}
        var records=values.Get<SequenceValue>(BlobSchema.BlobsPort);
        if(records.Declaration!=BlobSchema.BlobsType)throw new ArgumentException("Blob records differ from the declared schema.");
        var rows=records.Cast<RecordValue>().Select(BlobAnalysisRow.From).ToArray();
        if(rows.Select(row=>row.ElementId).Distinct().Count()!=rows.Length)throw new ArgumentException("Duplicate Blob element identity.");
        var count=checked((int)values.Get<long>(BlobSchema.Field(11)));
        var total=values.Get<double>(BlobSchema.Field(12));var average=values.Get<double>(BlobSchema.Field(13));
        var minimum=values.Get<object?>(BlobSchema.Field(8));var maximum=values.Get<object?>(BlobSchema.Field(9));
        var minArea=values.Get<double>(BlobSchema.Field(30));var maxArea=values.Get<double>(BlobSchema.Field(31));
        var thresholdType=values.Get<string>(BlobSchema.Field(10));
        if(!Current())return;
        _publication=publication;
        PresentRows(rows,count,total,average,minArea,maxArea,minimum,maximum,thresholdType);
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        InstallEmpty(null);
    }
    private void InstallEmpty(IResultPublication? publication)
    {
        var previous=_publication;
        _publication=publication;
        PresentRows(Array.Empty<BlobAnalysisRow>(),0,0,0,0,0,null,null,"-",(publication??previous)?.Stamp);
    }

    private void PresentRows(IReadOnlyList<BlobAnalysisRow> rows,int count,double total,double average,
        double minArea,double maxArea,object? minimum,object? maximum,string thresholdType,ResultPublicationStamp? clearStamp=null)
    {
        var generation=++_generation;
        var publication=_publication;
        FilteredBlobCount=count;TotalArea=total;AverageArea=average;MinArea=minArea;MaxArea=maxArea;
        ThresholdRangeText=minimum is null&&maximum is null?"-":string.Create(CultureInfo.InvariantCulture,$"{minimum}–{maximum}");
        ThresholdTypeText=thresholdType=="Binary"?"正向阈值":thresholdType=="BinaryInv"?"反向阈值":thresholdType;
        SelectedBlob=null;
        _rows.Install(rows);
        var errors=new List<Exception>();
        bool Current()=>!_disposed&&generation==_generation&&ReferenceEquals(publication,_publication)
            &&(publication is null||publication.IsCurrent())&&!_disposed&&generation==_generation&&ReferenceEquals(publication,_publication);
        void Notify(Action action){try{if(Current())action();}catch(Exception error){errors.Add(error);}}
        _publishing++;
        try
        {
            Notify(()=>BlobDataGrid.SelectedItem=null);
            Notify(()=>_rows.Publish(Current));
            foreach(var name in new[]{nameof(FilteredBlobCount),nameof(TotalArea),nameof(FilteredTotalArea),nameof(AverageArea),
                nameof(MaxArea),nameof(MinArea),nameof(ThresholdRangeText),nameof(ThresholdTypeText),nameof(SelectedBlob)})
                Notify(()=>PropertyChanged?.Invoke(this,new(name)));
            Notify(()=>{if((_publication?.Stamp??clearStamp) is {} stamp)SelectionHighlightChanged?.Invoke(this,new(stamp,null,Array.Empty<ResultHighlightTarget>()));});
        }
        finally{_publishing--;}
        if(errors.Count!=0)throw new AggregateException("Blob projection installed; result observers failed.",errors);
    }

    public void Dispose()
    {
        if(_disposed)return;
        _disposed=true;++_generation;_publication=null;
        var errors=new List<Exception>();
        void Clean(Action action){try{action();}catch(Exception error){errors.Add(error);}}
        Clean(()=>BlobDataGrid.SelectionChanged-=BlobDataGrid_SelectionChanged);
        _rows.Install(Array.Empty<BlobAnalysisRow>());
        Clean(()=>_rows.Publish(()=>true));Clean(()=>BlobDataGrid.SelectedItem=null);
        SelectionHighlightChanged=null;PropertyChanged=null;Clean(()=>DataContext=null);
        if(errors.Count!=0)throw new AggregateException("Blob view retired; cleanup observers failed.",errors);
    }
    private sealed class DisplayRows:ObservableCollection<BlobAnalysisRow>
    {
        internal void Install(IReadOnlyList<BlobAnalysisRow> rows){Items.Clear();foreach(var row in rows)Items.Add(row);}
        internal void Publish(Func<bool> current)
        {
            var errors=new List<Exception>();
            void Notify(Action action){if(!current())return;try{action();}catch(Exception error){errors.Add(error);}}
            Notify(()=>OnPropertyChanged(new(nameof(Count))));
            Notify(()=>OnPropertyChanged(new("Item[]")));
            Notify(()=>OnCollectionChanged(new(System.Collections.Specialized.NotifyCollectionChangedAction.Reset)));
            if(errors.Count!=0)throw new AggregateException("Result rows installed; collection observers failed.",errors);
        }
    }
}
