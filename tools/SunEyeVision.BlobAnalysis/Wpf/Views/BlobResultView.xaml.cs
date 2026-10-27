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
    public FrameworkElement View => this;
    public IReadOnlyList<Port> Sources { get; } = Array.AsReadOnly(BlobSchema.Outputs
        .Where(port=>new[]{8,9,10,11,12,13,30,31,100}.Select(BlobSchema.Field).Contains(port.Id)).ToArray());
    public ObservableCollection<BlobAnalysisRow> FilteredBlobs { get; } = new();
    public int FilteredBlobCount { get; private set; }
    public double TotalArea { get; private set; }
    public double FilteredTotalArea => TotalArea;
    public double AverageArea { get; private set; }
    public double MaxArea { get; private set; }
    public double MinArea { get; private set; }
    public string ThresholdRangeText { get; private set; } = "-";
    public string ThresholdTypeText { get; private set; } = "-";
    public BlobAnalysisRow? SelectedBlob { get; private set; }
    public event EventHandler<IReadOnlyList<GeometryValue>?>? SelectionHighlightChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public BlobResultView()
    {
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
        SelectedBlob=BlobDataGrid.SelectedItem as BlobAnalysisRow;
        PropertyChanged?.Invoke(this,new(nameof(SelectedBlob)));
        SelectionHighlightChanged?.Invoke(this,SelectedBlob?.HighlightGeometry is {} geometry
            ?Array.AsReadOnly<GeometryValue>([geometry]):null);
    }

    public void Update(InputValues? values)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        FilteredBlobs.Clear();FilteredBlobCount=0;TotalArea=AverageArea=MaxArea=MinArea=0;
        ThresholdRangeText=ThresholdTypeText="-";NotifySummary();
        if(values is null)return;
        var records=values.Get<SequenceValue>(BlobSchema.BlobsPort);
        if(records.Declaration!=BlobSchema.BlobsType)throw new ArgumentException("Blob records differ from the declared schema.");
        var rows=records.Cast<RecordValue>().Select(BlobAnalysisRow.From).ToArray();
        if(rows.Select(row=>row.ElementId).Distinct().Count()!=rows.Length)throw new ArgumentException("Duplicate Blob element identity.");
        var count=checked((int)values.Get<long>(BlobSchema.Field(11)));
        var total=values.Get<double>(BlobSchema.Field(12));var average=values.Get<double>(BlobSchema.Field(13));
        var minimum=values.Get<object?>(BlobSchema.Field(8));var maximum=values.Get<object?>(BlobSchema.Field(9));
        var minArea=values.Get<double>(BlobSchema.Field(30));var maxArea=values.Get<double>(BlobSchema.Field(31));
        var thresholdType=values.Get<string>(BlobSchema.Field(10));
        PresentRows(rows,count,total,average,minArea,maxArea,minimum,maximum,thresholdType);
    }

    private void PresentRows(IReadOnlyList<BlobAnalysisRow> rows,int count,double total,double average,
        double minArea,double maxArea,object? minimum,object? maximum,string thresholdType)
    {
        FilteredBlobs.Clear();
        foreach(var row in rows)FilteredBlobs.Add(row);
        FilteredBlobCount=count;TotalArea=total;AverageArea=average;MinArea=minArea;MaxArea=maxArea;
        ThresholdRangeText=string.Create(CultureInfo.InvariantCulture,$"{minimum}–{maximum}");
        ThresholdTypeText=thresholdType=="Binary"?"正向阈值":thresholdType=="BinaryInv"?"反向阈值":thresholdType;
        NotifySummary();
    }

    private void NotifySummary()
    {
        foreach(var name in new[]{nameof(FilteredBlobCount),nameof(TotalArea),nameof(FilteredTotalArea),nameof(AverageArea),
            nameof(MaxArea),nameof(MinArea),nameof(ThresholdRangeText),nameof(ThresholdTypeText)})PropertyChanged?.Invoke(this,new(name));
    }
    public void Dispose()
    {
        if(_disposed)return;
        Update(null);BlobDataGrid.SelectionChanged-=BlobDataGrid_SelectionChanged;
        SelectionHighlightChanged=null;DataContext=null;_disposed=true;
    }
}
