using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Apps.PcUI.Controls;
using HikPcUI.Controls;
using VM.Controls;
using VMControls.RenderInterface;

namespace FewShotCommonControl;

public partial class FewShotModelDialog : BaseWindow, IComponentConnector
{
	public static readonly DependencyProperty ROIHitTestVisibleProperty = DependencyProperty.Register("ROIHitTestVisible", typeof(bool), typeof(FewShotModelDialog), new PropertyMetadata(true));

	public static readonly DependencyProperty IsNeedCloseProperty = DependencyProperty.Register("IsNeedClose", typeof(bool), typeof(FewShotModelDialog), new PropertyMetadata(false, OnIsNeedCloseChanged));

	public static readonly DependencyProperty ShowMaskTipProperty = DependencyProperty.Register("ShowMaskTip", typeof(string), typeof(FewShotModelDialog), new PropertyMetadata(null));

	public static readonly DependencyProperty IsShowMaskProperty = DependencyProperty.Register("IsShowMask", typeof(bool), typeof(FewShotModelDialog), new PropertyMetadata(false, OnIsShowMaskChanged));

	private ProgressMask progressMask;

	public bool ROIHitTestVisible
	{
		get
		{
			return (bool)GetValue(ROIHitTestVisibleProperty);
		}
		set
		{
			SetValue(ROIHitTestVisibleProperty, value);
		}
	}

	public bool IsNeedClose
	{
		get
		{
			return (bool)GetValue(IsNeedCloseProperty);
		}
		set
		{
			SetValue(IsNeedCloseProperty, value);
		}
	}

	public string ShowMaskTip
	{
		get
		{
			return (string)GetValue(ShowMaskTipProperty);
		}
		set
		{
			SetValue(ShowMaskTipProperty, value);
		}
	}

	public bool IsShowMask
	{
		get
		{
			return (bool)GetValue(IsShowMaskProperty);
		}
		set
		{
			SetValue(IsShowMaskProperty, value);
		}
	}

	public FewShotModelDialog()
	{
		InitializeComponent();
	}

	private static void OnIsNeedCloseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if ((bool)e.NewValue)
		{
			(d as FewShotModelDialog)?.Close();
		}
	}

	private static void OnIsShowMaskChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is FewShotModelDialog fewShotModelDialog)
		{
			if (fewShotModelDialog.IsShowMask)
			{
				fewShotModelDialog.AddMask(fewShotModelDialog.ShowMaskTip);
			}
			else
			{
				fewShotModelDialog.RemoveMask();
			}
		}
	}

	private void RemoveMask()
	{
		Dispatcher.Invoke(() =>
		{
			mainGrid.Children.Remove(progressMask);
			progressMask = null;
		});
	}

	private void AddMask(string tips)
	{
		Dispatcher.Invoke(() =>
		{
			progressMask = new ProgressMask();
			progressMask.Message = tips;
			progressMask.IsShowProgressBar = false;
			progressMask.SetValue(Grid.RowSpanProperty, 2);
			mainGrid.Children.Add(progressMask);
		});
	}

	private void RegionScale_Click(object sender, RoutedEventArgs e)
	{
		imageView.Init();
	}

	private void FullScreen_Click(object sender, RoutedEventArgs e)
	{
		FullScreen instance = FullScreen.Instance;
		instance.DataContext = DataContext;
		viewGrid.Children.Remove(imageView);
		viewGrid.Children.Remove(btnLeft);
		viewGrid.Children.Remove(btnRight);
		instance.SetChild(imageView);
		instance.SetChild(btnLeft);
		instance.SetChild(btnRight);
		instance.ShowDialog();
		instance.RemoveChild();
		instance.DataContext = null;
		viewGrid.Children.Add(imageView);
		viewGrid.Children.Add(btnLeft);
		viewGrid.Children.Add(btnRight);
	}

	private void imageView_RoiAddEvent(object sender, RoiEventArgs e)
	{
		bool? isChecked = PolygonEx.IsChecked;
		bool flag = true;
		if (isChecked == true == flag && isChecked.HasValue && e.Roi is IROISign iROISign)
		{
			iROISign.Sign = false;
		}
		Arrow.IsChecked = true;
	}
}
