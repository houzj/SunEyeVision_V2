using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

namespace SunEyeVision.Tool.BlobAnalysis.Presentation;

/// <summary>The actual result table definition, applied by XAML in both designer and runtime.</summary>
public static class BlobResultColumns
{
    public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached(
        "Enabled",typeof(bool),typeof(BlobResultColumns),new PropertyMetadata(false,Apply));
    public static bool GetEnabled(DependencyObject view)=>(bool)view.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject view,bool value)=>view.SetValue(EnabledProperty,value);
    private static void Apply(DependencyObject view,DependencyPropertyChangedEventArgs args)
    {
        if(view is not DataGrid table)throw new ArgumentException("Blob result columns require a DataGrid.");
        table.Columns.Clear();
        if(args.NewValue is not true)return;
        foreach(var column in BlobDisplayDefinition.Instance.Table.Columns)
        {
            var member=column.Value.MemberFieldPath.Single();
            table.Columns.Add(new DataGridTextColumn{Header=column.DisplayName,
                Binding=new Binding("["+member+"]"){Mode=BindingMode.OneWay,
                    StringFormat=member=="bounds"?null:"{0:F"+column.DecimalPlaces+"}"},
                Width=DataGridLength.SizeToHeader});
        }
    }
}
