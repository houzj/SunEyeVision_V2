using System;
using System.Windows;

namespace Apps.XmlParser.ParamTab;

internal class BindingObj : FrameworkElement
{
	public delegate void ObjChanged(object sender, EventArgs e);

	public static readonly DependencyProperty MyObjProperty = DependencyProperty.Register("MyObj", typeof(object), typeof(BindingObj), new PropertyMetadata(null, MyObjChanged));

	public object MyObj
	{
		get
		{
			return GetValue(MyObjProperty);
		}
		set
		{
			SetValue(MyObjProperty, value);
		}
	}

	public event ObjChanged OnObjChanged;

	private static void MyObjChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if ((d as BindingObj).OnObjChanged != null)
		{
			(d as BindingObj).OnObjChanged(d, null);
		}
	}
}
