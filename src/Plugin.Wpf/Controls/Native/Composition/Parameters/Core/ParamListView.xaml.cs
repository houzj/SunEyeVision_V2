using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Data;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Core;

public partial class ParamListView : ItemsControl, IComponentConnector
{
	private double CustomParamCHWidth = 89.0;

	public static readonly DependencyProperty CustomParamENWidthProperty = DependencyProperty.Register("CustomParamENWidth", typeof(double), typeof(ParamListView), new PropertyMetadata(170.0, CustomParamENWidthChanged));

	public static readonly DependencyProperty ChildLeftWidthProperty = DependencyProperty.Register("ChildLeftWidth", typeof(double), typeof(ParamListView), new PropertyMetadata(89.0));

	public static readonly DependencyProperty ChildMarginProperty = DependencyProperty.Register("ChildMargin", typeof(Thickness), typeof(ParamListView), new PropertyMetadata(new Thickness(32.0, 1.0, 32.0, 0.0)));

	public static readonly DependencyProperty ChildHeaderMarginProperty = DependencyProperty.Register("ChildHeaderMargin", typeof(Thickness), typeof(ParamListView), new PropertyMetadata(new Thickness(32.0, 0.0, 0.0, 8.0)));

	public object Instance => this;

	public double CustomParamENWidth
	{
		get
		{
			return (double)GetValue(CustomParamENWidthProperty);
		}
		set
		{
			SetCurrentValue(CustomParamENWidthProperty, value);
		}
	}

	public double ChildLeftWidth
	{
		get
		{
			return (double)GetValue(ChildLeftWidthProperty);
		}
		set
		{
			SetCurrentValue(ChildLeftWidthProperty, value);
		}
	}

	public Thickness ChildMargin
	{
		get
		{
			return (Thickness)GetValue(ChildMarginProperty);
		}
		set
		{
			SetCurrentValue(ChildMarginProperty, value);
		}
	}

	public Thickness ChildHeaderMargin
	{
		get
		{
			return (Thickness)GetValue(ChildHeaderMarginProperty);
		}
		set
		{
			SetCurrentValue(ChildHeaderMarginProperty, value);
		}
	}

	private static void CustomParamENWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is ParamListView paramListView)
		{
			double num = ((AppLocalizationService.GetLang("Language") == "English") ? paramListView.CustomParamENWidth : paramListView.CustomParamCHWidth);
			if (paramListView.ChildLeftWidth != num)
			{
				paramListView.ChildLeftWidth = num;
			}
		}
	}

    private CollectionViewSource _view;
    private IEnumerable? _rows;
    private long _sourceGeneration,_orderGeneration;
    public void SetRows(IEnumerable? rows)
    {
        var generation=++_sourceGeneration;
        if(rows is not null)_=rows.Cast<NativeParameterRow>().ToArray();
        if(generation!=_sourceGeneration)return;
        if(_rows is INotifyCollectionChanged old)CollectionChangedEventManager.RemoveHandler(old,OnRows);
        _rows=rows;_view.Source=rows;
        if(generation!=_sourceGeneration)return;
        if(rows is INotifyCollectionChanged next)CollectionChangedEventManager.AddHandler(next,OnRows);
        UpdateGroupOrder();
    }
    private void OnRows(object? sender,NotifyCollectionChangedEventArgs args){if(ReferenceEquals(sender,_rows))UpdateGroupOrder();}
    private void UpdateGroupOrder()
    {
        var generation=_sourceGeneration;var order=++_orderGeneration;
        var groups=_rows?.Cast<NativeParameterRow>().Select(row=>row.GroupName).Distinct().ToArray()??Array.Empty<NativeParameterGroup>();
        ParameterCleanup.Run(groups.Select((group,index)=>(Action)(()=>{if(generation==_sourceGeneration&&order==_orderGeneration)group.IsFirst=index==0;})));
    }
    public ParamListView()
	{
		InitializeComponent();
        _view=(CollectionViewSource)FindResource("src");
        SetBinding(ItemsSourceProperty,new Binding{Source=_view});
		ChildLeftWidth = ((AppLocalizationService.GetLang("Language") == "English") ? CustomParamENWidth : CustomParamCHWidth);
	}
}
