using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using Apps.BaseData;
using Apps.Data;
using Apps.Localization;
using HikExternalCall.Common;
using Microsoft.Win32;
using VM.Controls;
using VM.Utility;
using VMControls.RenderInterface;

namespace IMVSImgStitchCalibModuControl;

public partial class StitchImageView : UserControl, IComponentConnector
{
	public ImgStitchDialog Owner;

	private Byte2ImageSource _byte2Image = new Byte2ImageSource();

	public static readonly DependencyProperty IndexProperty = DependencyProperty.Register("Index", typeof(int), typeof(StitchImageView), new PropertyMetadata(0));

	public static readonly DependencyProperty IsActualImageProperty = DependencyProperty.Register("IsActualImage", typeof(bool), typeof(StitchImageView), new PropertyMetadata(false, OnImageModeChanged));

	public static readonly DependencyProperty CalibImageProperty = DependencyProperty.Register("CalibImage", typeof(ImageData), typeof(StitchImageView), new PropertyMetadata(null));

	public static readonly DependencyProperty ActualImageProperty = DependencyProperty.Register("ActualImage", typeof(ImageData), typeof(StitchImageView), new PropertyMetadata(null));

	public static readonly DependencyProperty DisplayImageWidthProperty = DependencyProperty.Register("DisplayImageWidth", typeof(int), typeof(StitchImageView), new PropertyMetadata(200));

	public static readonly DependencyProperty DisplayImageHeightProperty = DependencyProperty.Register("DisplayImageHeight", typeof(int), typeof(StitchImageView), new PropertyMetadata(160));

	public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(ImageSource), typeof(StitchImageView), new PropertyMetadata(null, SourceChanged));

	public static readonly DependencyProperty ImageDataSourceProperty = DependencyProperty.Register("ImageDataSource", typeof(ImageData), typeof(StitchImageView), new PropertyMetadata(null, ImageSourceChanged));

	private double ratio = 1.0;

	private const int ImageControlWidth = 500;

	private const int PointWidth = 4;

	private const float GeoThickness = 1.5f;

	public int Index
	{
		get
		{
			return (int)GetValue(IndexProperty);
		}
		set
		{
			SetValue(IndexProperty, value);
		}
	}

	public bool IsActualImage
	{
		get
		{
			return (bool)GetValue(IsActualImageProperty);
		}
		set
		{
			SetValue(IsActualImageProperty, value);
		}
	}

	public ImageData CalibImage
	{
		get
		{
			return (ImageData)GetValue(CalibImageProperty);
		}
		set
		{
			SetValue(CalibImageProperty, value);
		}
	}

	public ImageData ActualImage
	{
		get
		{
			return (ImageData)GetValue(ActualImageProperty);
		}
		set
		{
			SetValue(ActualImageProperty, value);
		}
	}

	public int DisplayImageWidth
	{
		get
		{
			return (int)GetValue(DisplayImageWidthProperty);
		}
		set
		{
			SetValue(DisplayImageWidthProperty, value);
		}
	}

	public int DisplayImageHeight
	{
		get
		{
			return (int)GetValue(DisplayImageHeightProperty);
		}
		set
		{
			SetValue(DisplayImageHeightProperty, value);
		}
	}

	public ImageSource Source
	{
		get
		{
			return (ImageSource)GetValue(SourceProperty);
		}
		set
		{
			SetValue(SourceProperty, value);
		}
	}

	public ImageData ImageDataSource
	{
		get
		{
			return (ImageData)GetValue(ImageDataSourceProperty);
		}
		set
		{
			SetValue(ImageDataSourceProperty, value);
		}
	}

	public bool IsOnlyShowImage { get; set; }

	private static void OnImageModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is StitchImageView stitchImageView)
		{
			stitchImageView.SwitchDisplayImage();
		}
	}

	public StitchImageView()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected Obj, but got Unknown
		InitializeComponent();
	}

	public StitchImageView(ImgStitchDialog owner)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected Obj, but got Unknown
		InitializeComponent();
		Owner = owner;
	}

	private static void SourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		StitchImageView stitchImageView = d as StitchImageView;
		ImageSource source = e.NewValue as ImageSource;
		stitchImageView.image.Source = source;
	}

	private static void ImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		StitchImageView stitchImageView = d as StitchImageView;
		stitchImageView.DoImageSourceChanged();
	}

	private void DoImageSourceChanged()
	{
		try
		{
			if (ImageDataSource == null)
			{
				Source = null;
				return;
			}
			Size size = default;
			double num = (double)ImageDataSource.Width / (double)ImageDataSource.Height;
			int num2 = DisplayImageWidth / DisplayImageHeight;
			if (num > (double)num2)
			{
				int displayImageWidth = DisplayImageWidth;
				double height = (double)displayImageWidth / num;
				size = new Size(displayImageWidth, height);
			}
			else
			{
				int displayImageHeight = DisplayImageHeight;
				double width = num * (double)displayImageHeight;
				size = new Size(width, displayImageHeight);
			}
			bool flag = true;
			Source = _byte2Image.BuiltImageSourceA((IImageData)ImageDataSource, (int)size.Width, (int)size.Height, ref flag, 0, 0, 0, 0, false);
		}
		catch (Exception)
		{
		}
	}

	private void EmptyGrid_Click(object sender, MouseButtonEventArgs e)
	{
		if (!Owner.OwnerModulePanel.imageStitchData.IsGetingImage)
		{
			SelectNewImage();
		}
	}

	private void SelectNewImage()
	{
		string text = ConfigurationHelper.INIGetDefaultOpenPath(Owner.OwnerModulePanel.OwnerModule?.ModuleDir);
		try
		{
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Filter = HikMessageBox.MsgFindResoure("图像文件") + "|*.bmp;*.jpg;*.jpeg;*.tif;*.png;*.BMP;*.JPG;*.JPEG;*.TIF;*.PNG";
			openFileDialog.FilterIndex = 2;
			openFileDialog.RestoreDirectory = false;
			if (text != null && Directory.Exists(text))
			{
				openFileDialog.InitialDirectory = text;
			}
			if (openFileDialog.ShowDialog() != true)
			{
				return;
			}
			text = openFileDialog.FileName;
			ImageData imageDataFromFile = ImageData.GetImageDataFromFile(text);
			if (imageDataFromFile.ImageBuffer != null)
			{
				if (imageDataFromFile.Width < 64 || imageDataFromFile.Height < 64)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SmallSizeImage"), MessageIcons.Error);
					return;
				}
				if (IsActualImage)
				{
					(Owner.OwnerModulePanel.imageStitchData.ItemsData[Index - 1] as StitchImageDataItem).ActualImageDataA = imageDataFromFile;
				}
				else
				{
					(Owner.OwnerModulePanel.imageStitchData.ItemsData[Index - 1] as StitchImageDataItem).ImageDataA = imageDataFromFile;
				}
				(Owner.OwnerModulePanel.imageStitchData.ItemsData[Index - 1] as StitchImageDataItem).IsReadyA = false;
				Owner.OwnerModulePanel.imageStitchData.SelectedItemA = Owner.OwnerModulePanel.imageStitchData.ItemsData[Index - 1] as StitchImageDataItem;
				Owner.OwnerModulePanel.imageStitchData.OnPropertyChanged("SuccessRate");
				ShowImage();
			}
			ConfigurationHelper.INISetDefaultOpenPath(System.IO.Path.GetDirectoryName(text), Owner.OwnerModulePanel.OwnerModule?.ModuleDir);
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(ex.Message, MessageIcons.Error, isTranslate: true);
		}
	}

	public void ShowImage(bool ignorePop = false)
	{
		ImageData imageData = (IsActualImage ? ActualImage : CalibImage);
		if (imageData == null || imageData.Width == 0 || imageData.Height == 0 || imageData.ImageBuffer == null)
		{
			if (!ignorePop)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ImageEmpty"), MessageIcons.Error);
			}
		}
		else
		{
			ImageDataSource = imageData;
			EmptyGrid.Visibility = Visibility.Collapsed;
			ImageGrid.Visibility = Visibility.Visible;
		}
	}

	public void DrawPoints(StitchImageDataItem data)
	{
		try
		{
			ratio = 500.0 / (double)data.ImageData.Width;
			int result = 0;
			if (!int.TryParse(data.CornersNum, out result))
			{
				return;
			}
			List<HKA_POINT_F> list = ExternalCallHelper.BytesToStructs<HKA_POINT_F>(data.CornersImg);
			if (list == null || list.Count <= 0)
			{
				return;
			}
			foreach (HKA_POINT_F item in list)
			{
				Point pt = new Point(item.x, item.y);
				string lang = AppLocalizationService.GetLang("coordinate");
				float x = item.x;
				string arg = x.ToString("N2");
				x = item.y;
				CrearPoint(pt, string.Format("{0}:({1},{2})", lang, arg, x.ToString("N2")));
			}
		}
		catch (Exception)
		{
		}
	}

	private void CrearPoint(Point pt, string tips)
	{
		if (image.Source != null)
		{
			GeometryGroup geometryGroup = new GeometryGroup();
			double num = (pt.X + 0.5) * ratio;
			double num2 = (pt.Y + 0.5) * ratio;
			LineGeometry value = new LineGeometry(new Point(num, num2 - 4.0), new Point(num, num2 + 4.0));
			LineGeometry value2 = new LineGeometry(new Point(num - 4.0, num2), new Point(num + 4.0, num2));
			geometryGroup.Children.Add(value);
			geometryGroup.Children.Add(value2);
			System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
			path.Stroke = new SolidColorBrush(Color.FromRgb(0, byte.MaxValue, 0));
			path.StrokeThickness = 1.5;
			if (tips != "")
			{
				path.ToolTip = tips;
				path.IsHitTestVisible = true;
			}
			else
			{
				path.IsHitTestVisible = false;
			}
			path.Data = geometryGroup;
			mView.Children.Add(path);
		}
	}

	public void DeleteImage()
	{
		mView.Children.Clear();
		ImageDataSource = null;
		EmptyGrid.Visibility = Visibility.Visible;
		ImageGrid.Visibility = Visibility.Collapsed;
	}

	private void SwitchDisplayImage()
	{
		DeleteImage();
		ShowImage(ignorePop: true);
	}

	private void BtnNewImage_Click(object sender, RoutedEventArgs e)
	{
		SelectNewImage();
	}

	private void ImageGrid_MouseEnter(object sender, MouseEventArgs e)
	{
		SetControlsStates();
	}

	private void ImageGrid_MouseLeave(object sender, MouseEventArgs e)
	{
		SetControlsStates();
	}

	private void ImageGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (Owner != null)
		{
			Owner.OwnerModulePanel.imageStitchData.SelectedItemA = Owner.OwnerModulePanel.imageStitchData.ItemsData[Index - 1] as StitchImageDataItem;
			if (e.ClickCount == 2)
			{
				Owner.ChangeLargeImageStatus(Index);
			}
		}
	}

	private void us_Loaded(object sender, RoutedEventArgs e)
	{
		SetControlsStates();
		SizeChanged += CheckSize;
	}

	private void CheckSize(object sender, SizeChangedEventArgs e)
	{
		SetControlsStates();
	}

	public void SetControlsStates()
	{
		if (IsOnlyShowImage || ActualHeight < 60.0 || ActualWidth < 60.0)
		{
			CvsIndex.Visibility = Visibility.Collapsed;
			PlgStatus.Visibility = Visibility.Collapsed;
			BtnNewImage.Visibility = Visibility.Collapsed;
		}
		else
		{
			CvsIndex.Visibility = Visibility.Visible;
			PlgStatus.Visibility = Visibility.Visible;
			if (IsMouseOver)
			{
				BtnNewImage.Visibility = Visibility.Visible;
			}
			else
			{
				BtnNewImage.Visibility = Visibility.Collapsed;
			}
		}
		if (IsOnlyShowImage)
		{
			EmptyGrid.Visibility = Visibility.Collapsed;
			ImageGrid.Visibility = Visibility.Visible;
		}
	}

	public void Dispose()
	{
		ImageDataSource = null;
		image = null;
		_byte2Image.Clear();
		_byte2Image = null;
		Owner = null;
		DataContext = null;
	}
}
