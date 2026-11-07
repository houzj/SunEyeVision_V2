using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using Apps.Data;
using Hik.Xml;
using HikPcUI.ImageView;

namespace IMVSColorRecognitionModuControl;

public class MaskData : CompressMaskData, INotifyPropertyChanged, IItemsData
{
	public ObservableCollection<object> _ItemsData = new ObservableCollection<object>();

	public byte[] otherMask;

	private byte[] MaskTemp;

	public ObservableCollection<object> ItemsData
	{
		get
		{
			return _ItemsData;
		}
		set
		{
			if (_ItemsData != value)
			{
				_ItemsData = value;
				NotifyPropertyChanged("ItemsData");
			}
		}
	}

	public string ImagePath { get; set; }

	public double ImageWidth { get; set; }

	public double ImageHeight { get; set; }

	public int MinRoiX { get; set; }

	public int MinRoiY { get; set; }

	public int MaxRoiX { get; set; }

	public int MaxRoiY { get; set; }

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		if (compressMask != null)
		{
			otherMask = CompressToNormalMask(compressMask, 3);
		}
		return this;
	}

	protected override void BeforSaveBaseNode()
	{
		if (otherMask != null)
		{
			compressMask = NormalToCompressMask(otherMask, 3);
			MaskTemp = otherMask;
			otherMask = null;
		}
		else
		{
			compressMask = null;
		}
	}

	protected override void AfterSaveBaseNode()
	{
		otherMask = MaskTemp;
		MaskTemp = null;
	}

	internal void SaveAlgorithmData(string saveName)
	{
		ImageData imageDataByImage = GetImageDataByImage();
		imageDataByImage.SaveGrayImage(saveName);
	}

	private byte LevelNumToByte(int Level)
	{
		return Level switch
		{
			0 => byte.MaxValue, 
			1 => (byte)128, 
			2 => (byte)0, 
			_ => byte.MaxValue, 
		};
	}

	private ImageData GetImageDataByImage()
	{
		ImageData imageData = new ImageData
		{
			Width = (int)ImageWidth,
			Height = (int)ImageHeight,
			PixelFormat = PixelFormats.Gray8
		};
		imageData.ImageBuffer = Enumerable.Repeat((byte)0, imageData.Width * imageData.Height).ToArray();
		foreach (object itemsDatum in ItemsData)
		{
			if (itemsDatum is HikCoverRoiParameter)
			{
				HikCoverRoiParameter hikCoverRoiParameter = itemsDatum as HikCoverRoiParameter;
				double x = hikCoverRoiParameter.OriginPoint.X;
				double y = hikCoverRoiParameter.OriginPoint.Y;
				double width = hikCoverRoiParameter.Width;
				double height = hikCoverRoiParameter.Height;
				RotateTransform rotateTransform = new RotateTransform();
				rotateTransform.CenterX = x + width / 2.0;
				rotateTransform.CenterY = y + height / 2.0;
				rotateTransform.Angle = hikCoverRoiParameter.Angle;
				Point point = new Point(x, y);
				point = rotateTransform.Transform(point);
				Point point2 = new Point(x + width, y);
				point2 = rotateTransform.Transform(point2);
				Point point3 = new Point(x + width, y + height);
				point3 = rotateTransform.Transform(point3);
				Point point4 = new Point(x, y + height);
				point4 = rotateTransform.Transform(point4);
				PolygonPoint polygonPoint = new PolygonPoint();
				PolygonPoint polygonPoint2 = new PolygonPoint();
				PolygonPoint polygonPoint3 = new PolygonPoint();
				PolygonPoint polygonPoint4 = new PolygonPoint();
				polygonPoint.x = (float)point.X;
				polygonPoint.y = (float)point.Y;
				polygonPoint2.x = (float)point2.X;
				polygonPoint2.y = (float)point2.Y;
				polygonPoint3.x = (float)point3.X;
				polygonPoint3.y = (float)point3.Y;
				polygonPoint4.x = (float)point4.X;
				polygonPoint4.y = (float)point4.Y;
				ObservableCollection<PolygonPoint> observableCollection = new ObservableCollection<PolygonPoint>();
				observableCollection.Add(polygonPoint);
				observableCollection.Add(polygonPoint2);
				observableCollection.Add(polygonPoint3);
				observableCollection.Add(polygonPoint4);
				HikPolygonRoiParameter hikPolygonRoiParameter = new HikPolygonRoiParameter();
				hikPolygonRoiParameter.Sign = hikCoverRoiParameter.Sign;
				hikPolygonRoiParameter.PolygonPoints = observableCollection;
				SetMaskDataByPolygen(imageData, hikPolygonRoiParameter);
			}
			else if (itemsDatum is HikPolygonRoiParameter)
			{
				HikPolygonRoiParameter polyObj = itemsDatum as HikPolygonRoiParameter;
				SetMaskDataByPolygen(imageData, polyObj);
			}
			else
			{
				if (!(itemsDatum is HikCoverCircleRoiParameter))
				{
					continue;
				}
				HikCoverCircleRoiParameter hikCoverCircleRoiParameter = itemsDatum as HikCoverCircleRoiParameter;
				int width2 = imageData.Width;
				int height2 = imageData.Height;
				double radius = hikCoverCircleRoiParameter._Radius;
				double minRadius = hikCoverCircleRoiParameter._MinRadius;
				byte b = byte.MaxValue;
				int num = (int)(hikCoverCircleRoiParameter._CenterPoint.X - radius);
				int num2 = (int)(hikCoverCircleRoiParameter._CenterPoint.X + radius);
				int num3 = (int)(hikCoverCircleRoiParameter._CenterPoint.Y - radius);
				int num4 = (int)(hikCoverCircleRoiParameter._CenterPoint.Y + radius);
				if (num < MinRoiX)
				{
					MinRoiX = num;
					MinRoiX = ((MinRoiX >= 0) ? MinRoiX : 0);
				}
				if (num3 < MinRoiY)
				{
					MinRoiY = num3;
					MinRoiY = ((MinRoiY >= 0) ? MinRoiY : 0);
				}
				if (num2 > MaxRoiX)
				{
					MaxRoiX = num2;
					MaxRoiX = ((MaxRoiX > width2) ? width2 : MaxRoiX);
				}
				if (num4 > MaxRoiY)
				{
					MaxRoiY = num4;
					MaxRoiY = ((MaxRoiY > height2) ? height2 : MaxRoiY);
				}
				double num5 = hikCoverCircleRoiParameter.EndAngle;
				double num6 = hikCoverCircleRoiParameter.StartAngle;
				if (num5 < 0.0)
				{
					num5 = 360.0 + num5;
				}
				if (num6 < 0.0)
				{
					num6 = 360.0 + num6;
				}
				for (int i = 0; (double)i < 2.0 * radius; i++)
				{
					for (int j = 0; (double)j < 2.0 * radius; j++)
					{
						double num7 = Math.Sqrt(Math.Pow((double)j - radius, 2.0) + Math.Pow((double)i - radius, 2.0));
						double num8 = Math.Atan(Math.Abs((double)i - radius) / Math.Abs((double)j - radius)) / Math.PI * 180.0;
						if ((double)j - radius <= 0.0 && (double)i - radius <= 0.0)
						{
							num8 = 180.0 + num8;
						}
						if ((double)j - radius <= 0.0 && (double)i - radius >= 0.0)
						{
							num8 = 180.0 - num8;
						}
						if ((double)j - radius >= 0.0 && (double)i - radius <= 0.0)
						{
							num8 = 360.0 - num8;
						}
						if (num7 <= radius && num7 >= minRadius && ((num8 >= num6 && num8 <= num5) || (num6 >= num5 && (num8 >= num6 || num8 <= num5))) && j + (int)(hikCoverCircleRoiParameter._CenterPoint.X - radius) >= 0 && j + (int)(hikCoverCircleRoiParameter._CenterPoint.X - radius) <= width2)
						{
							int num9 = (i + (int)(hikCoverCircleRoiParameter._CenterPoint.Y - radius)) * width2 + j + (int)(hikCoverCircleRoiParameter._CenterPoint.X - radius);
							if (num9 < imageData.ImageBuffer.Length && num9 >= 0)
							{
								imageData.ImageBuffer[num9] = b;
							}
						}
					}
				}
			}
		}
		if (otherMask != null && (double)otherMask.Length == ImageHeight * ImageWidth)
		{
			for (int k = 0; k < otherMask.Length; k++)
			{
				if (otherMask[k] > 0)
				{
					imageData.ImageBuffer[k] = LevelNumToByte(MaskToLevelNum(otherMask[k]));
				}
			}
		}
		return imageData;
	}

	public static byte LevelNumToMask(int level)
	{
		return (byte)(level + 1);
	}

	public static int MaskToLevelNum(byte data)
	{
		return data - 1;
	}

	private void SetMaskDataByPolygen(ImageData imageData, HikPolygonRoiParameter polyObj)
	{
		byte[] imageBuffer = imageData.ImageBuffer;
		int width = imageData.Width;
		int height = imageData.Height;
		int num = (int)polyObj.PolygonPoints.Min((PolygonPoint r) => r.x);
		int num2 = (int)polyObj.PolygonPoints.Max((PolygonPoint r) => r.x);
		int num3 = (int)polyObj.PolygonPoints.Min((PolygonPoint r) => r.y);
		int num4 = (int)polyObj.PolygonPoints.Max((PolygonPoint r) => r.y);
		byte b = byte.MaxValue;
		if (num < MinRoiX)
		{
			MinRoiX = num;
			MinRoiX = ((MinRoiX >= 0) ? MinRoiX : 0);
			num = MinRoiX;
		}
		if (num3 < MinRoiY)
		{
			MinRoiY = num3;
			MinRoiY = ((MinRoiY >= 0) ? MinRoiY : 0);
			num3 = MinRoiY;
		}
		if (num2 > MaxRoiX)
		{
			MaxRoiX = num2;
			MaxRoiX = ((MaxRoiX > width) ? width : MaxRoiX);
			num2 = MaxRoiX;
		}
		if (num4 > MaxRoiY)
		{
			MaxRoiY = num4;
			MaxRoiY = ((MaxRoiY > height) ? height : MaxRoiY);
			num4 = MaxRoiY;
		}
		for (int num5 = num3; num5 < num4; num5++)
		{
			int num6 = 0;
			int index = polyObj.PolygonPoints.Count - 1;
			int[] array = new int[polyObj.PolygonPoints.Count];
			int num7;
			for (num7 = 0; num7 < polyObj.PolygonPoints.Count; num7++)
			{
				if ((polyObj.PolygonPoints[num7].y < (float)num5 && polyObj.PolygonPoints[index].y >= (float)num5) || (polyObj.PolygonPoints[index].y < (float)num5 && polyObj.PolygonPoints[num7].y >= (float)num5))
				{
					array[num6++] = (int)(polyObj.PolygonPoints[num7].x + ((float)num5 - polyObj.PolygonPoints[num7].y) / (polyObj.PolygonPoints[index].y - polyObj.PolygonPoints[num7].y) * (polyObj.PolygonPoints[index].x - polyObj.PolygonPoints[num7].x));
				}
				index = num7;
			}
			int num8 = 0;
			num7 = 0;
			while (num7 < num6 - 1)
			{
				if (array[num7] > array[num7 + 1])
				{
					num8 = array[num7];
					array[num7] = array[num7 + 1];
					array[num7 + 1] = num8;
					if (num7 > 0)
					{
						num7--;
					}
				}
				else
				{
					num7++;
				}
			}
			for (num7 = 0; num7 < num6 && array[num7] < num2; num7 += 2)
			{
				if (array[num7 + 1] <= num)
				{
					continue;
				}
				if (array[num7] < num)
				{
					array[num7] = num;
				}
				if (array[num7 + 1] > num2)
				{
					array[num7 + 1] = num2;
				}
				for (index = array[num7]; index < array[num7 + 1]; index++)
				{
					int num9 = num5 * width + index;
					if (num9 < imageBuffer.Length && num9 >= 0)
					{
						imageBuffer[num9] = b;
					}
				}
			}
		}
	}
}
