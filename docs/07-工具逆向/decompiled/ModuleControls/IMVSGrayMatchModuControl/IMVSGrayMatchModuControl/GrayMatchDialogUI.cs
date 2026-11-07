using System;
using System.Collections.Specialized;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using HikPcUI.ImageView;
using IMVSHPFeatureMatchModuControl;
using VM.Controls;
using VM.ModuleControl;

namespace IMVSGrayMatchModuControl;

public class GrayMatchDialogUI : HPFeaturepatMatchDialogUI
{
	protected override string FilterName => "xml files(*.gmxml)|*.gmxml";

	static GrayMatchDialogUI()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(GrayMatchDialogUI), new FrameworkPropertyMetadata(typeof(GrayMatchDialogUI)));
	}

	protected override void ItemsData_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		base.featurepatMatchDialogData.AutoSetFixPoint();
		if (e.Action != NotifyCollectionChangedAction.Add || ((FeaturepatMatchDialogDataBase)base.featurepatMatchDialogData)._ItemsData.Count <= 1)
		{
			((HPFeaturepatMatchDialogUI)this).RecordHistory();
			((HPFeaturepatMatchDialogUI)this).MatchSelfExecute();
		}
	}

	protected override HPFeaturepatMatchDialogData GetFeaturepatMatchDialogData()
	{
		return (HPFeaturepatMatchDialogData)(object)new GrayMatchDialogData();
	}

	protected override void InitSign()
	{
	}

	protected override void imageView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (base.Arrow.IsChecked != true)
		{
			return;
		}
		System.Windows.Point point = new System.Windows.Point(base.imageViewBox.PixelX, base.imageViewBox.PixelY);
		if (((HPFeaturepatMatchDialogUI)this).CurImage == null || point.X < 0.0 || point.X >= (double)((HPFeaturepatMatchDialogUI)this).CurImage.Width || point.Y < 0.0 || point.Y >= (double)((HPFeaturepatMatchDialogUI)this).CurImage.Height)
		{
			return;
		}
		int num = (int)(point.Y * (double)((HPFeaturepatMatchDialogUI)this).CurImage.Width + point.X);
		byte[] otherMask = ((FeaturepatMatchDialogDataBase)base.featurepatMatchDialogData).otherMask;
		if (otherMask != null && otherMask.Count() > num)
		{
			byte b = otherMask[num];
			if (b > 0 && b < 10)
			{
				((HPFeaturepatMatchDialogUI)this).ReFreshMask(otherMask);
				uint num2 = 2583632410u;
				((HPFeaturepatMatchDialogUI)this).setUnicomColor(num, otherMask, num2, b);
				((HPFeaturepatMatchDialogUI)this).DrawMask();
				base.tempImage.Focus();
			}
			else if (b == 0)
			{
				((HPFeaturepatMatchDialogUI)this).ReFreshMask(otherMask);
			}
		}
	}

	public override void RefreshByModelData()
	{
		((FeaturepatMatchDialogDataBase)base.featurepatMatchDialogData).NotifyPropertyChanged("ItemsData");
		((FeaturepatMatchDialogDataBase)base.featurepatMatchDialogData).NotifyPropertyChanged("FixPointX");
		((FeaturepatMatchDialogDataBase)base.featurepatMatchDialogData).NotifyPropertyChanged("FixPointY");
		((HPFeaturepatMatchDialogUI)this).CurImage = ((FeaturepatMatchDialogDataBase)base.featurepatMatchDialogData).CurImageDataA;
		((HPFeaturepatMatchDialogUI)this).ShowImage();
		((HPFeaturepatMatchDialogUI)this).FreshOtherMask();
		((HPFeaturepatMatchDialogUI)this).RefreshPoints();
	}

	protected unsafe override void DrawMask(System.Drawing.Point point, int sWidth, int sHeight, bool isNormal = true)
	{
		if (base.imageViewBox.Source == null || ((FeaturepatMatchDialogDataBase)base.featurepatMatchDialogData).otherMask == null)
		{
			return;
		}
		System.Windows.Point point2 = new System.Windows.Point(((base.imageViewBox.MapCanvas as Canvas).ActualWidth - (base.imageViewBox.Viewbox as Viewbox).ActualWidth) / 2.0 + Canvas.GetLeft(base.imageViewBox.Viewbox as Viewbox) + base.imageViewBox.ImageOffSetX, ((base.imageViewBox.MapCanvas as Canvas).ActualHeight - (base.imageViewBox.Viewbox as Viewbox).ActualHeight) / 2.0 + Canvas.GetTop(base.imageViewBox.Viewbox as Viewbox) + base.imageViewBox.ImageOffSetY);
		double onePixelWidth = base.imageViewBox.OnePixelWidth;
		int num = 0;
		int num2 = 0;
		int num3 = (int)((base.imageViewBox.MapCanvas as Canvas)?.ActualWidth).Value;
		int num4 = (int)((base.imageViewBox.MapCanvas as Canvas)?.ActualHeight).Value;
		if (num3 == 0 || num4 == 0)
		{
			return;
		}
		WriteableBitmap writeableBitmap = base.MaskSourceConvertor.Init(num3, num4, PixelFormats.Bgra32);
		if (writeableBitmap == null)
		{
			return;
		}
		base.tempImage.Source = writeableBitmap;
		writeableBitmap.Lock();
		byte* ptr = (byte*)writeableBitmap.BackBuffer.ToPointer();
		int num5 = writeableBitmap.BackBufferStride / 4;
		uint* ptr2 = (uint*)ptr;
		if (isNormal)
		{
			for (int i = 0; i < num4; i++)
			{
				uint* ptr3 = ptr2 + i * num5;
				for (int j = 0; j < num3; j++)
				{
					ptr3[j] = 0u;
				}
			}
		}
		fixed (byte* otherMask = ((FeaturepatMatchDialogDataBase)base.featurepatMatchDialogData).otherMask)
		{
			uint num6 = 0u;
			int num7 = (int)Math.Ceiling(onePixelWidth);
			int width = ((HPFeaturepatMatchDialogUI)this).CurImage.Width;
			int height = ((HPFeaturepatMatchDialogUI)this).CurImage.Height;
			int num8 = 0;
			int num9 = Math.Max((int)((double)point.X * onePixelWidth + point2.X), 0);
			int num10 = Math.Max((int)((double)point.Y * onePixelWidth + point2.Y), 0);
			int num11 = Math.Min((int)((double)(point.X + sWidth) * onePixelWidth + point2.X) + 1, num3);
			int num12 = Math.Min((int)((double)(point.Y + sHeight) * onePixelWidth + point2.Y) + 1, num4);
			for (int k = num10; k < num12; k++)
			{
				for (int l = num9; l < num11; l++)
				{
					num = (int)(((double)l - point2.X) / onePixelWidth);
					if (num < 0 || num >= width)
					{
						continue;
					}
					num2 = (int)(((double)k - point2.Y) / onePixelWidth);
					if (num2 >= 0 && num2 < height)
					{
						num8 = num2 * width + num;
						if (otherMask[num8] != 0)
						{
							num6 = ((otherMask[num8] <= 10) ? 1291786778u : 2583632410u);
							uint* ptr4 = ptr2 + k * num5 + l;
							*ptr4 = num6;
						}
					}
				}
			}
			if (isNormal)
			{
				writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
			}
			else
			{
				try
				{
					writeableBitmap.AddDirtyRect(new Int32Rect(num9, num10, num11 - num9, num12 - num10));
				}
				catch
				{
					writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
				}
			}
			writeableBitmap.Unlock();
		}
	}

	protected override void CustomFeaturePatCreateByEraser(int ret, bool? needpopTip)
	{
		if (ret == 0)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("创建模版成功"), MessageIcons.Ok);
		}
		else
		{
			LogHelper.objLog.Error($"Show points fail, using eraser,errorcode={ret}");
		}
	}

	protected override void CustomHikFrameChecked()
	{
	}

	protected override void CustomShowPoints(HPFeaturepatMatchDialogData featurepatMatchData, ref int ret)
	{
		if ((((FeaturepatMatchDialogDataBase)featurepatMatchData).ItemsData == null || ((FeaturepatMatchDialogDataBase)featurepatMatchData).ItemsData.Count == 0) && ((FeaturepatMatchDialogDataBase)featurepatMatchData).otherMask == null)
		{
			ret = -2;
			throw new Exception(AppLocalizationService.GetLang("Lang_NoROIRegion"));
		}
		if (((FeaturepatMatchDialogDataBase)featurepatMatchData).ItemsData.Count > 1)
		{
			ret = -2;
			throw new Exception(AppLocalizationService.GetLang("Lang_MaskNumErr"));
		}
		if (!(((FeaturepatMatchDialogDataBase)featurepatMatchData).ItemsData[0] is HikCoverRoiParameter) || ((((FeaturepatMatchDialogDataBase)featurepatMatchData).ItemsData[0] as HikCoverRoiParameter).Sign.HasValue && !(((FeaturepatMatchDialogDataBase)featurepatMatchData).ItemsData[0] as HikCoverRoiParameter).Sign.Value) || (((FeaturepatMatchDialogDataBase)featurepatMatchData).ItemsData[0] as HikCoverRoiParameter).Angle != 0.0)
		{
			ret = -2;
			throw new Exception(AppLocalizationService.GetLang("Lang_MaskInfoErr"));
		}
	}

	protected override int CreatePatE(HPFeaturepatMatchDialogData featurepatMatchData, dynamic dynamicObject)
	{
		return dynamicObject.CreateGrayPatE(featurepatMatchData);
	}
}
