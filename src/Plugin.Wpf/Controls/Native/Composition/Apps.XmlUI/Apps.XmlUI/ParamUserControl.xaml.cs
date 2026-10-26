using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public partial class ParamUserControl : ContentControl, IComponentConnector
{
	public static readonly DependencyProperty ExternDisplayTypeProperty = DependencyProperty.Register("ExternDisplayType", typeof(string), typeof(ParamUserControl), new PropertyMetadata(null, ExternDisplayTypeChanged));

	private static UserTempConverter userTempConverter = new UserTempConverter();

	public string ExternDisplayType
	{
		get
		{
			return (string)GetValue(ExternDisplayTypeProperty);
		}
		set
		{
			SetValue(ExternDisplayTypeProperty, value);
		}
	}

	private static void ExternDisplayTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ParamUserControl paramUserControl = d as ParamUserControl;
		if (e.NewValue is string)
		{
			paramUserControl.SetBinding(ContentControl.ContentTemplateProperty, new Binding("ExternDisplayType")
			{
				Converter = userTempConverter,
				Source = paramUserControl
			});
		}
		else
		{
			paramUserControl.SetDefaultBinding();
		}
	}

	private void SetDefaultBinding()
	{
		SetBinding(ContentControl.ContentTemplateProperty, new Binding("DisplayType")
		{
			Converter = userTempConverter
		});
	}

	public ParamUserControl()
	{
		InitializeComponent();
		SetDefaultBinding();
	}

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		if (e.Property == ContentControl.ContentProperty && Content != null)
		{
			Assembly assembly = Content.GetType().Assembly;
			if (assembly.GetName().Name != "Apps.XmlParser" && assembly.GetName().Name != "WindowsBase" && !UserTempConverter.ExternAssembly.Contains(assembly))
			{
				UserTempConverter.ExternAssembly.Add(assembly);
				string uriString = $"/{assembly.GetName().Name};component/DataTemplate/ParamUserControlDataTemplate.xaml";
				(Resources["userTempConverter"] as UserTempConverter).ResourceDictionary.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri(uriString, UriKind.Relative)));
			}
		}
	}
}
