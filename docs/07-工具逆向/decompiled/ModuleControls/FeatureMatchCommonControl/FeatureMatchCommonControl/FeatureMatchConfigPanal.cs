using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Apps.BaseData;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace FeatureMatchCommonControl;

public class FeatureMatchConfigPanal : Control, INotifyPropertyChanged
{
	public static readonly DependencyProperty ParentNodeProperty;

	public static readonly DependencyProperty DecorateProperty;

	public static readonly DependencyProperty SourceProperty;

	protected bool needSaveModelData = false;

	public static readonly DependencyProperty ItemsDataProperty;

	public static readonly DependencyProperty SelectedItemProperty;

	protected bool hasSavedUIData = false;

	public object ParentNode
	{
		get
		{
			return GetValue(ParentNodeProperty);
		}
		set
		{
			SetValue(ParentNodeProperty, value);
		}
	}

	public object Decorate
	{
		get
		{
			return GetValue(DecorateProperty);
		}
		set
		{
			SetValue(DecorateProperty, value);
		}
	}

	public IParamsConfig Source
	{
		get
		{
			return (IParamsConfig)GetValue(SourceProperty);
		}
		set
		{
			SetValue(SourceProperty, value);
		}
	}

	public virtual IParamsConfig OwnerModuleParam { get; protected set; }

	public virtual IVmModule OwnerModule { get; protected set; }

	public IList ItemsData
	{
		get
		{
			return (IList)GetValue(ItemsDataProperty);
		}
		set
		{
			SetValue(ItemsDataProperty, value);
		}
	}

	public object SelectedItem
	{
		get
		{
			return GetValue(SelectedItemProperty);
		}
		set
		{
			SetValue(SelectedItemProperty, value);
		}
	}

	public NonOfParamHandler EditModelMethod
	{
		get
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Expected Obj, but got Unknown
			return EditModel;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	static FeatureMatchConfigPanal()
	{
		ParentNodeProperty = DependencyProperty.Register("ParentNode", typeof(object), typeof(FeatureMatchConfigPanal), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure));
		DecorateProperty = DependencyProperty.Register("Decorate", typeof(object), typeof(FeatureMatchConfigPanal), new PropertyMetadata(null));
		SourceProperty = DependencyProperty.Register("Source", typeof(IParamsConfig), typeof(FeatureMatchConfigPanal), new PropertyMetadata(null, OnSourceChanged));
		ItemsDataProperty = DependencyProperty.Register("ItemsData", typeof(IList), typeof(FeatureMatchConfigPanal), new PropertyMetadata(null));
		SelectedItemProperty = DependencyProperty.Register("SelectedItem", typeof(object), typeof(FeatureMatchConfigPanal), new PropertyMetadata(null));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(FeatureMatchConfigPanal), new FrameworkPropertyMetadata(typeof(FeatureMatchConfigPanal)));
	}

	private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		FeatureMatchConfigPanal featureMatchConfigPanal = d as FeatureMatchConfigPanal;
		IParamsConfig paramsConfig = e.OldValue as IParamsConfig;
		IParamsConfig paramsConfig2 = (featureMatchConfigPanal.OwnerModuleParam = e.NewValue as IParamsConfig);
		if (paramsConfig2 is IVmModule ownerModule)
		{
			featureMatchConfigPanal.OwnerModule = ownerModule;
		}
	}

	public FeatureMatchConfigPanal()
	{
		needSaveModelData = false;
	}

	private void EditModel()
	{
	}
}
