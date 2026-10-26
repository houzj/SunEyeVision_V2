using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab;

public partial class CheckBoxConfigTreeView : Control, INotifyPropertyChanged, IComponentConnector
{
	public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register("ItemsSource", typeof(IList), typeof(CheckBoxConfigTreeView), new PropertyMetadata(null));

	private bool _IsDisplayCheckBox = true;

	private bool _IsExpanded;

	public IList ItemsSource
	{
		get
		{
			return (IList)GetValue(ItemsSourceProperty);
		}
		set
		{
			SetValue(ItemsSourceProperty, value);
		}
	}

	public bool IsDisplayCheckBox
	{
		get
		{
			return _IsDisplayCheckBox;
		}
		set
		{
			if (_IsDisplayCheckBox != value)
			{
				_IsDisplayCheckBox = value;
				NotifyPropertyChanged("IsDisplayCheckBox");
			}
		}
	}

	public bool IsExpanded
	{
		get
		{
			return _IsExpanded;
		}
		set
		{
			if (_IsExpanded != value)
			{
				_IsExpanded = value;
				NotifyPropertyChanged("IsExpanded");
			}
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

	public CheckBoxConfigTreeView()
	{
		InitializeComponent();
	}
}
