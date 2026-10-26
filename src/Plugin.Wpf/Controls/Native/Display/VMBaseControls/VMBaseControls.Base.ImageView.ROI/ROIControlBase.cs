using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Apps.UIHelper.Extern;
using Mvvm.Commands;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public class ROIControlBase : ROIControlFrame
{
	public static readonly DependencyProperty DataChangedCommandProperty = DependencyProperty.Register("DataChangedCommand", typeof(ICommand), typeof(ROIControlBase), new PropertyMetadata(null));

	public static readonly DependencyProperty ContainerPositionProperty = DependencyProperty.Register("ContainerPosition", typeof(Point), typeof(ROIControlBase), new PropertyMetadata(default(Point)));

	protected bool _isMove = false;

	protected static Point mouseOffset;

	protected static Type drawRoiType;

	protected static bool IsMoved;

	private static ResourceDictionary resource;

	public static readonly DependencyProperty DeleteRoiCommandProperty = DependencyProperty.Register("DeleteRoiCommand", typeof(ICommand), typeof(ROIControlBase), new PropertyMetadata(null));

	public static readonly DependencyProperty CopyRoiCommandProperty = DependencyProperty.Register("CopyRoiCommand", typeof(ICommand), typeof(ROIControlBase), new PropertyMetadata(null));

	public static readonly DependencyProperty MoveCommandProperty = DependencyProperty.Register("MoveCommand", typeof(ICommand), typeof(ROIControlBase), new PropertyMetadata(null));

	public ICommand DataChangedCommand
	{
		get
		{
			return (ICommand)GetValue(DataChangedCommandProperty);
		}
		set
		{
			SetValue(DataChangedCommandProperty, value);
		}
	}

	public Point ContainerPosition
	{
		get
		{
			return (Point)GetValue(ContainerPositionProperty);
		}
		set
		{
			SetValue(ContainerPositionProperty, value);
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

	public SubROIItemsPage ParenPage => this.GetParent<SubROIItemsPage>();

	public ICommand MoveCommand
	{
		get
		{
			return (ICommand)GetValue(MoveCommandProperty);
		}
		set
		{
			SetValue(MoveCommandProperty, value);
		}
	}

	protected void AfterMouseUp()
	{
		ImageViewBox parent;
		if ((parent = this.GetParent<ImageViewBox>()) != null)
		{
			parent.IsDragROI = false;
		}
	}

	protected static void BaseBeforeDraw(FrameworkElement parent, Type roiDataType)
	{
		mouseOffset = Mouse.GetPosition(parent);
		drawRoiType = roiDataType;
		IsMoved = false;
		ROIItemsPage rOIItemsPage = parent as ROIItemsPage;
	}

	protected static void BaseAfterDraw(FrameworkElement frameworkElement, FrameworkElement parent)
	{
		frameworkElement.SetBinding(ROIControlFrame.ThicknessScaleProperty, new Binding("ThicknessScale")
		{
			Source = parent
		});
		frameworkElement.SetBinding(ROIControlFrame.ControlToImageRadioProperty, new Binding("ControlToImageRadio")
		{
			Source = parent
		});
		parent.CaptureMouse();
		ROIItemsPage rOIItemsPage = parent as ROIItemsPage;
		rOIItemsPage.SubstituteHost.Children.Add(frameworkElement);
	}

	protected static void BaseFinishDraw(FrameworkElement frameworkElement, FrameworkElement parent)
	{
		ROIItemsPage rOIItemsPage = parent as ROIItemsPage;
		rOIItemsPage.SubstituteHost.Children.Remove(frameworkElement);
		parent.ReleaseMouseCapture();
		IsMoved = false;
		frameworkElement = null;
	}

	protected static void CompleteDraw(object data, FrameworkElement parent)
	{
		ROIItemsPage rOIItemsPage = parent as ROIItemsPage;
		rOIItemsPage.CompleteDrawSubstitute(data);
	}

	public object GetResource(string key)
	{
		try
		{
			if (resource == null)
			{
				resource = new ResourceDictionary
				{
					Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/VMBaseControls/base/imageview/roi/roicontrolbasestyle.xaml", UriKind.Relative)
				};
			}
			return resource[key];
		}
		catch (Exception)
		{
			return null;
		}
	}

	public Point GetImagePoint(Point p)
	{
		return new Point(p.X / ControlToImageRadio - 0.0, p.Y / ControlToImageRadio - 0.0);
	}

	public Point GetControlPoint(Point p)
	{
		return new Point((p.X + 0.0) * ControlToImageRadio, (p.Y + 0.0) * ControlToImageRadio);
	}

	protected void NotifyROIParamChanged()
	{
		DataChangedCommand?.Execute(DataContext);
		this.GetParent<ImageViewBox>()?.NotifyROIParamChanged(DataContext);
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		SetValue(MoveCommandProperty, new DelegateCommand<object>((Action<object>)MoveCommandExcute));
	}

	private void MoveCommandExcute(object obj)
	{
		if (Move((ROIMoveDirection)obj))
		{
			NotifyROIParamChanged();
		}
	}

	protected virtual bool Move(ROIMoveDirection rOIMoveDirection)
	{
		return false;
	}
}
