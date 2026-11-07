using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.Data;
using HikPcUI.ImageView;
using VM.Controls;

namespace IMVSOcrModuControl;

public partial class GetTrainCharacter : BaseWindow, IComponentConnector, IStyleConnector
{
	private Byte2ImageSource b2SConvertor = new Byte2ImageSource();

	private ImageData _Image;

	private int curSelectedIndex = -1;

	public ImageData CurImage
	{
		get
		{
			return _Image;
		}
		set
		{
			_Image = value;
			if (_Image != null)
			{
				imageView.ImageDataSource = CurImage;
			}
		}
	}

	public GetTrainCharacter()
	{
		InitializeComponent();
		imageView.HideSizePanel();
		HikImageView hikImageView = imageView;
		hikImageView.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Combine(hikImageView.OnRoiParamChange, new HikImageView.RoiParamChange(imageView_OnRoiParamChange));
	}

	private void Button_Click_AddToLib(object sender, RoutedEventArgs e)
	{
		DialogResult = true;
	}

	private void CheckBox_Click(object sender, RoutedEventArgs e)
	{
		OCRTrainUIData oCRTrainUIData = DataContext as OCRTrainUIData;
		if (oCRTrainUIData.ItemsData == null)
		{
			return;
		}
		foreach (object itemsDatum in oCRTrainUIData.ItemsData)
		{
			if (itemsDatum is HikPasterParameterExtern)
			{
				(itemsDatum as HikPasterParameterExtern).IsChosen = (sender as CheckBox).IsChecked.Value;
			}
		}
	}

	private void imageView_OnRoiParamChange(object Date)
	{
		if (Date is HikPasterParameterExtern && DataContext != null)
		{
			HikPasterParameterExtern hikPasterParameterExtern = Date as HikPasterParameterExtern;
			int num = (DataContext as OCRTrainUIData).ItemsData.IndexOf(hikPasterParameterExtern);
			if (hikPasterParameterExtern.IsSelected)
			{
				curSelectedIndex = num;
			}
			if (hikPasterParameterExtern.SubContent.Length < 1)
			{
				hikPasterParameterExtern.State = ROIState.Error;
			}
			else if (hikPasterParameterExtern.SubContent[0] == '？')
			{
				hikPasterParameterExtern.State = ROIState.Error;
			}
			else
			{
				hikPasterParameterExtern.State = ROIState.Normal;
			}
		}
	}

	internal void PasterPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (curSelectedIndex + 1 < (DataContext as OCRTrainUIData).ItemsData.Count)
		{
			((DataContext as OCRTrainUIData).ItemsData[curSelectedIndex + 1] as HikPasterParameter).IsSelected = true;
		}
	}

	private void tb_GotFocus(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement).DataContext is HikPasterParameter hikPasterParameter)
		{
			hikPasterParameter.IsSelected = true;
		}
		(sender as TextBox).SelectAll();
		(sender as TextBox).PreviewMouseDown -= tb_PreviewMouseDown;
	}

	private void tb_LostFocus(object sender, RoutedEventArgs e)
	{
		(sender as TextBox).PreviewMouseDown += tb_PreviewMouseDown;
	}

	private void tb_PreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		(sender as TextBox).Focus();
		e.Handled = true;
	}
}
