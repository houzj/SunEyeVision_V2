using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.UIHelper.Extern;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView;

public class ROIItemsPage : ItemsControl, INotifyPropertyChanged
{
	public static readonly DependencyProperty IsHitTestVisibleOfHostProperty;

	public static readonly DependencyProperty ThicknessScaleProperty;

	public static readonly DependencyProperty ControlToImageRadioProperty;

	public static readonly DependencyProperty ImageWidthProperty;

	public static readonly DependencyProperty ImageHeightProperty;

	public static readonly DependencyProperty ROIListProperty;

	private Canvas Child;

	public static readonly DependencyProperty CopyRoiCommandProperty;

	public static readonly DependencyProperty DeleteRoiCommandProperty;

	public static readonly DependencyProperty ControlToAxisXRadioProperty;

	public static readonly DependencyProperty ControlToAxisYRadioProperty;

	public bool IsHitTestVisibleOfHost
	{
		get
		{
			return (bool)GetValue(IsHitTestVisibleOfHostProperty);
		}
		set
		{
			SetValue(IsHitTestVisibleOfHostProperty, value);
		}
	}

	public double ThicknessScale
	{
		get
		{
			return (double)GetValue(ThicknessScaleProperty);
		}
		set
		{
			SetValue(ThicknessScaleProperty, value);
		}
	}

	public double ControlToImageRadio
	{
		get
		{
			return (double)GetValue(ControlToImageRadioProperty);
		}
		set
		{
			SetValue(ControlToImageRadioProperty, value);
		}
	}

	public int ImageWidth
	{
		get
		{
			return (int)GetValue(ImageWidthProperty);
		}
		set
		{
			SetValue(ImageWidthProperty, value);
		}
	}

	public int ImageHeight
	{
		get
		{
			return (int)GetValue(ImageHeightProperty);
		}
		set
		{
			SetValue(ImageHeightProperty, value);
		}
	}

	public IList ROIList
	{
		get
		{
			return (IList)GetValue(ROIListProperty);
		}
		set
		{
			SetValue(ROIListProperty, value);
		}
	}

	public object InnerROIList
	{
		get
		{
			IEnumerable rOIList;
			if ((rOIList = ROIList) != null)
			{
				Type type = rOIList.GetType();
				if (type.IsGenericType)
				{
					if (typeof(IEnumerable).IsAssignableFrom(type.GetGenericArguments().First()))
					{
						return ROIList;
					}
					return new List<object> { ROIList };
				}
			}
			return null;
		}
	}

	public Canvas SubstituteHost => Child;

	public ICommand CopyRoiCommand
	{
		get
		{
			return (ICommand)GetValue(CopyRoiCommandProperty);
		}
		set
		{
			SetValue(CopyRoiCommandProperty, value);
		}
	}

	public ICommand DeleteRoiCommand
	{
		get
		{
			return (ICommand)GetValue(DeleteRoiCommandProperty);
		}
		set
		{
			SetValue(DeleteRoiCommandProperty, value);
		}
	}

	public double ControlToAxisXRadio
	{
		get
		{
			return (double)GetValue(ControlToAxisXRadioProperty);
		}
		set
		{
			SetValue(ControlToAxisXRadioProperty, value);
		}
	}

	public double ControlToAxisYRadio
	{
		get
		{
			return (double)GetValue(ControlToAxisYRadioProperty);
		}
		set
		{
			SetValue(ControlToAxisYRadioProperty, value);
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public event ObjectEventHandler DrawSubstituteComplete;

	public void OnPropertyChanged(string strPropertyInfo)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(strPropertyInfo));
		}
	}

	private static void OnRoiListChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ROIItemsPage rOIItemsPage = d as ROIItemsPage;
		rOIItemsPage.OnPropertyChanged("InnerROIList");
	}

	static ROIItemsPage()
	{
		IsHitTestVisibleOfHostProperty = DependencyProperty.Register("IsHitTestVisibleOfHost", typeof(bool), typeof(ROIItemsPage), new PropertyMetadata(true));
		ThicknessScaleProperty = DependencyProperty.Register("ThicknessScale", typeof(double), typeof(ROIItemsPage), new PropertyMetadata(1.0));
		ControlToImageRadioProperty = DependencyProperty.Register("ControlToImageRadio", typeof(double), typeof(ROIItemsPage), new PropertyMetadata(1.0));
		ImageWidthProperty = DependencyProperty.Register("ImageWidth", typeof(int), typeof(ROIItemsPage), new PropertyMetadata(0));
		ImageHeightProperty = DependencyProperty.Register("ImageHeight", typeof(int), typeof(ROIItemsPage), new PropertyMetadata(0));
		ROIListProperty = DependencyProperty.Register("ROIList", typeof(IList), typeof(ROIItemsPage), new PropertyMetadata(null, OnRoiListChanged));
		CopyRoiCommandProperty = DependencyProperty.Register("CopyRoiCommand", typeof(ICommand), typeof(ROIItemsPage), new PropertyMetadata(null));
		DeleteRoiCommandProperty = DependencyProperty.Register("DeleteRoiCommand", typeof(ICommand), typeof(ROIItemsPage), new PropertyMetadata(null));
		ControlToAxisXRadioProperty = DependencyProperty.Register("ControlToAxisXRadio", typeof(double), typeof(ROIItemsPage), new PropertyMetadata(0.0));
		ControlToAxisYRadioProperty = DependencyProperty.Register("ControlToAxisYRadio", typeof(double), typeof(ROIItemsPage), new PropertyMetadata(1.0));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(ROIItemsPage), new FrameworkPropertyMetadata(typeof(ROIItemsPage)));
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		Child = Template.FindName("Bd", this) as Canvas;
	}

	internal void CompleteDrawSubstitute(object data)
	{
		DrawSubstituteComplete?.Invoke(this, new ObjectEventArgs
		{
			Param = data
		});
	}
}
