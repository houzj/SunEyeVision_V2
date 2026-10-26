using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.BaseData;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

[ShapeDisplayForInterfaceType(typeof(IMultiPoint))]
public class MultiPointShape : ShapeContainer
{
	private struct IMVS_PATMATCH_POINT_INFO
	{
		public float MatchOutlineX;

		public float MatchOutlineY;

		public float MatchOutlineScore;

		public int MatchOutlineIndex;
	}

	private static readonly int _step;

	private Byte2ImageSource b2SConvertor = new Byte2ImageSource();

	private RotateTransform hu = new RotateTransform();

	private List<byte[]> ExternPoint_Buffer = new List<byte[]>();

	private List<int> ExternPoint_Buffer_OkColor = new List<int>();

	private List<int> ExternPoint_Buffer_NgColor = new List<int>();

	private List<float> ExternPoint_Buffer_Opacity = new List<float>();

	private List<object> ShapeList = new List<object>();

	private double ExternOpacity = 1.0;

	private int PointLenth = 0;

	private float[] PointX_Buffer;

	private float[] PointY_Buffer;

	private int[] PointColor_Buffer;

	private bool needCreatePoint = false;

	public static readonly DependencyProperty OutlinePointsProperty;

	public static readonly DependencyProperty MicroPointsProperty;

	public static readonly DependencyProperty RectanglesProperty;

	public static readonly DependencyProperty NeedRefreshProperty;

	private static int DefaultOkColor;

	private static int DefaultNgColor;

	private bool NeedLargePoints
	{
		get
		{
			if (PointX_Buffer != null)
			{
				return PointX_Buffer.Length >= 100000;
			}
			return false;
		}
		set
		{
			if (value)
			{
				PointX_Buffer = new float[100000];
				PointY_Buffer = new float[100000];
				PointColor_Buffer = new int[100000];
			}
		}
	}

	public ObservableCollection<IOutlinePoint> OutlinePoints
	{
		get
		{
			return (ObservableCollection<IOutlinePoint>)GetValue(OutlinePointsProperty);
		}
		set
		{
			SetValue(OutlinePointsProperty, value);
		}
	}

	public ObservableCollection<IMicroPoint> MicroPoints
	{
		get
		{
			return (ObservableCollection<IMicroPoint>)GetValue(MicroPointsProperty);
		}
		set
		{
			SetValue(MicroPointsProperty, value);
		}
	}

	public ObservableCollection<IRectangle> Rectangles
	{
		get
		{
			return (ObservableCollection<IRectangle>)GetValue(RectanglesProperty);
		}
		set
		{
			SetValue(RectanglesProperty, value);
		}
	}

	public bool NeedRefresh
	{
		get
		{
			return (bool)GetValue(NeedRefreshProperty);
		}
		set
		{
			SetValue(NeedRefreshProperty, value);
		}
	}

	private static void OnOutlinePointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		MultiPointShape multiPointShape = d as MultiPointShape;
		ObservableCollection<IOutlinePoint> observableCollection = e.OldValue as ObservableCollection<IOutlinePoint>;
		ObservableCollection<IOutlinePoint> observableCollection2 = e.NewValue as ObservableCollection<IOutlinePoint>;
		if (observableCollection != null)
		{
			observableCollection.CollectionChanged -= multiPointShape.OutlinePoints_CollectionChanged;
		}
		if (observableCollection2 != null)
		{
			multiPointShape.UpdateOutlinePointsData();
			observableCollection2.CollectionChanged -= multiPointShape.OutlinePoints_CollectionChanged;
			observableCollection2.CollectionChanged += multiPointShape.OutlinePoints_CollectionChanged;
		}
	}

	private void OutlinePoints_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action != NotifyCollectionChangedAction.Add)
		{
			return;
		}
		foreach (IOutlinePoint item in e.NewItems.OfType<IOutlinePoint>())
		{
			ExternPoint_Buffer_OkColor.Add(DrawShapeHelper.XmlColorToInt(item.Stroke));
			ExternPoint_Buffer_NgColor.Add(DrawShapeHelper.XmlColorToInt(item.NgColor));
			ExternOpacity = item.Opacity;
			ExternPoint_Buffer.Add(item.OutlineData);
		}
		needCreatePoint = true;
	}

	private static void OnMicroPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		MultiPointShape multiPointShape = d as MultiPointShape;
		ObservableCollection<IMicroPoint> observableCollection = e.OldValue as ObservableCollection<IMicroPoint>;
		ObservableCollection<IMicroPoint> observableCollection2 = e.NewValue as ObservableCollection<IMicroPoint>;
		if (observableCollection != null)
		{
			observableCollection.CollectionChanged -= multiPointShape.MicroPoints_CollectionChanged;
		}
		if (observableCollection2 != null)
		{
			multiPointShape.UpdateMicroPointsData();
			observableCollection2.CollectionChanged -= multiPointShape.MicroPoints_CollectionChanged;
			observableCollection2.CollectionChanged += multiPointShape.MicroPoints_CollectionChanged;
		}
	}

	private void MicroPoints_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action != NotifyCollectionChangedAction.Add)
		{
			return;
		}
		foreach (IMicroPoint item in e.NewItems.OfType<IMicroPoint>())
		{
			AddNewPointK((float)item.Point.X, (float)item.Point.Y, (DrawShapeHelper.XmlColorToInt(item.Stroke) & 0xFFFFFF) | ((int)(item.Opacity * 255.0) << 24));
		}
		needCreatePoint = true;
	}

	private static void OnRectanglesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		MultiPointShape multiPointShape = d as MultiPointShape;
		ObservableCollection<IRectangle> observableCollection = e.OldValue as ObservableCollection<IRectangle>;
		ObservableCollection<IRectangle> observableCollection2 = e.NewValue as ObservableCollection<IRectangle>;
		if (observableCollection != null)
		{
			observableCollection.CollectionChanged -= multiPointShape.Rectangles_CollectionChanged;
		}
		if (observableCollection2 != null)
		{
			multiPointShape.UpdateRectanglesData();
			observableCollection2.CollectionChanged -= multiPointShape.Rectangles_CollectionChanged;
			observableCollection2.CollectionChanged += multiPointShape.Rectangles_CollectionChanged;
		}
	}

	private void Rectangles_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action != NotifyCollectionChangedAction.Add)
		{
			return;
		}
		foreach (IRectangle item in e.NewItems.OfType<IRectangle>())
		{
			ShapeList.Add(item);
		}
		needCreatePoint = true;
	}

	private static void OnNeedRefreshChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		MultiPointShape multiPointShape = d as MultiPointShape;
		if ((bool)e.NewValue)
		{
			multiPointShape.CreatPointK();
		}
	}

	public unsafe void CreatPointK()
	{
		ImageViewBox imageViewBox = Tag as ImageViewBox;
		if (DataContext == null || imageViewBox == null)
		{
			return;
		}
		IList shapeList = imageViewBox.ShapeList;
		if (shapeList == null || !shapeList.Contains(DataContext) || imageViewBox.SourceImage?.Source == null)
		{
			return;
		}
		if (PointLenth <= 0 && (ExternPoint_Buffer == null || ExternPoint_Buffer.Count == 0) && (ShapeList == null || ShapeList.Count == 0))
		{
			b2SConvertor.Clear();
			return;
		}
		Point point = new Point(((imageViewBox.MapCanvas as Canvas).ActualWidth - (imageViewBox.Viewbox as Viewbox).ActualWidth) / 2.0 + Canvas.GetLeft(imageViewBox.Viewbox as Viewbox) + imageViewBox.ImageOffSetX, ((imageViewBox.MapCanvas as Canvas).ActualHeight - (imageViewBox.Viewbox as Viewbox).ActualHeight) / 2.0 + Canvas.GetTop(imageViewBox.Viewbox as Viewbox) + imageViewBox.ImageOffSetY);
		double num = (imageViewBox.Viewbox as Viewbox).ActualWidth * imageViewBox.ImageScale;
		if (num == 0.0)
		{
			return;
		}
		double temp = num / (double)ImageSourceWidth;
		int num2 = 0;
		int num3 = 0;
		int num4 = (int)(imageViewBox.MapCanvas as Canvas).ActualWidth;
		int num5 = (int)(imageViewBox.MapCanvas as Canvas).ActualHeight;
		if (num4 == 0 || num5 == 0)
		{
			return;
		}
		WriteableBitmap writeableBitmap = b2SConvertor.Init(num4, num5, PixelFormats.Bgra32);
		if (writeableBitmap == null)
		{
			return;
		}
		writeableBitmap.Lock();
		byte* ptr = (byte*)writeableBitmap.BackBuffer.ToPointer();
		int wwe_Stride = writeableBitmap.BackBufferStride / 4;
		int* ztemp = (int*)ptr;
		writeableBitmap.Clear();
		double fx = 0.5 + point.X / temp;
		double fy = 0.5 + point.Y / temp;
		if (ShapeList != null && ShapeList.Count > 0)
		{
			int[] array = new int[10];
			foreach (IRectangle item in ShapeList.OfType<IRectangle>())
			{
				hu.CenterX = (item.CenterPoint.X + fx) * temp;
				hu.CenterY = (item.CenterPoint.Y + fy) * temp;
				hu.Angle = item.Angle;
				double num6 = item.Width * temp;
				double num7 = item.Height * temp;
				int strokeThickness = (int)item.StrokeThickness;
				int color = DrawShapeHelper.XmlColorToInt(item.Stroke);
				Point point2 = new Point(hu.CenterX - num6 / 2.0, hu.CenterY - num7 / 2.0);
				Point point3 = new Point(point2.X + num6, point2.Y);
				Point point4 = new Point(point3.X, point2.Y + num7);
				Point point5 = new Point(point2.X, point4.Y);
				if (hu.Angle != 0.0)
				{
					point2 = hu.Transform(point2);
					point3 = hu.Transform(point3);
					point4 = hu.Transform(point4);
					point5 = hu.Transform(point5);
				}
				array[0] = (int)point2.X;
				array[1] = (int)point2.Y;
				array[2] = (int)point3.X;
				array[3] = (int)point3.Y;
				array[4] = (int)point4.X;
				array[5] = (int)point4.Y;
				array[6] = (int)point5.X;
				array[7] = (int)point5.Y;
				array[8] = array[0];
				array[9] = array[1];
				writeableBitmap.DrawLineAa(array[0], array[1], array[2], array[3], color, strokeThickness);
				writeableBitmap.DrawLineAa(array[2], array[3], array[4], array[5], color, strokeThickness);
				writeableBitmap.DrawLineAa(array[4], array[5], array[6], array[7], color, strokeThickness);
				writeableBitmap.DrawLineAa(array[6], array[7], array[8], array[9], color, strokeThickness);
			}
		}
		fixed (float* pointX_Buffer = PointX_Buffer)
		{
			fixed (float* pointY_Buffer = PointY_Buffer)
			{
				fixed (int* pointColor_Buffer = PointColor_Buffer)
				{
					int Weeh = num4 - 4;
					int Heeh = num5 - 4;
					if (ExternPoint_Buffer != null)
					{
						int num8 = Marshal.SizeOf<IMVS_PATMATCH_POINT_INFO>();
						for (int i = 0; i < ExternPoint_Buffer.Count; i++)
						{
							int num9 = ((ExternPoint_Buffer_OkColor.Count > i) ? (ExternPoint_Buffer_OkColor[i] & 0xFFFFFF) : DefaultOkColor);
							int num10 = ((ExternPoint_Buffer_NgColor.Count > i) ? (ExternPoint_Buffer_NgColor[i] & 0xFFFFFF) : DefaultNgColor);
							double num11 = ((ExternPoint_Buffer_Opacity.Count > i) ? ((double)ExternPoint_Buffer_Opacity[i]) : ExternOpacity);
							int num12 = (int)(num11 * 255.0) << 24;
							int greenColor = num12 | num9;
							int yellowColor = num12 | 0xFFFF00;
							int redColor = num12 | num10;
							byte[] array2 = ExternPoint_Buffer[i];
							if (array2 == null)
							{
								continue;
							}
							int num13 = array2.Length;
							fixed (byte* ptr2 = array2)
							{
								IMVS_PATMATCH_POINT_INFO* tempExternPoint_Bufferptr = (IMVS_PATMATCH_POINT_INFO*)ptr2;
								int lsLenth = num13 / num8;
								int toExclusive = (int)Math.Ceiling((double)lsLenth / ((double)_step * 1.0));
								Parallel.For(0, toExclusive, (int g) =>
								{
									int num15 = g * _step;
									for (int j = 0; j < _step; j++)
									{
										int num16 = num15 + j;
										if (num16 == lsLenth)
										{
											break;
										}
										IMVS_PATMATCH_POINT_INFO iMVS_PATMATCH_POINT_INFO = tempExternPoint_Bufferptr[num16];
										int num17 = (int)(((double)iMVS_PATMATCH_POINT_INFO.MatchOutlineX + fx) * temp);
										if (num17 >= 0 && num17 <= Weeh)
										{
											int num18 = (int)(((double)iMVS_PATMATCH_POINT_INFO.MatchOutlineY + fy) * temp);
											if (num18 >= 0 && num18 <= Heeh)
											{
												float matchOutlineScore = iMVS_PATMATCH_POINT_INFO.MatchOutlineScore;
												float obj = matchOutlineScore;
												int num19 = (0f.Equals(obj) ? greenColor : ((!1f.Equals(obj)) ? redColor : yellowColor));
												int* ptr5 = ztemp + num18 * wwe_Stride + num17;
												if (*ptr5 > num19)
												{
													*ptr5 = num19;
												}
												ptr5++;
												if (*ptr5 > num19)
												{
													*ptr5 = num19;
												}
												ptr5 += wwe_Stride;
												if (*ptr5 > num19)
												{
													*ptr5 = num19;
												}
												ptr5--;
												if (*ptr5 > num19)
												{
													*ptr5 = num19;
												}
											}
										}
									}
								});
							}
						}
					}
					for (int num14 = 0; num14 < PointLenth; num14++)
					{
						if (pointX_Buffer[num14] < 0f)
						{
							continue;
						}
						num2 = (int)(((double)pointX_Buffer[num14] + fx) * temp);
						if (num2 >= 0 && num2 <= Weeh)
						{
							num3 = (int)(((double)pointY_Buffer[num14] + fy) * temp);
							if (num3 >= 0 && num3 <= Heeh)
							{
								int* ptr3 = pointColor_Buffer + num14;
								int* ptr4 = ztemp + num3 * wwe_Stride + num2;
								*ptr4 = *ptr3;
								ptr4++;
								*ptr4 = *ptr3;
								ptr4 += wwe_Stride;
								*ptr4 = *ptr3;
								ptr4--;
								*ptr4 = *ptr3;
							}
						}
					}
				}
			}
		}
		writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
		writeableBitmap.Unlock();
		imageViewBox.SetPointsImage(writeableBitmap);
		needCreatePoint = false;
	}

	private void UpdateOutlinePointsData()
	{
		if (OutlinePoints == null)
		{
			return;
		}
		foreach (IOutlinePoint outlinePoint in OutlinePoints)
		{
			if (outlinePoint.OutlineData != null)
			{
				ExternPoint_Buffer_OkColor.Add(DrawShapeHelper.XmlColorToInt(outlinePoint.Stroke));
				ExternPoint_Buffer_NgColor.Add(DrawShapeHelper.XmlColorToInt(outlinePoint.NgColor));
				ExternOpacity = outlinePoint.Opacity;
				ExternPoint_Buffer.Add(outlinePoint.OutlineData);
			}
		}
		needCreatePoint = true;
	}

	private void UpdateMicroPointsData()
	{
		if (MicroPoints == null)
		{
			return;
		}
		foreach (IMicroPoint microPoint in MicroPoints)
		{
			AddNewPointK((float)microPoint.Point.X, (float)microPoint.Point.Y, (DrawShapeHelper.XmlColorToInt(microPoint.Stroke) & 0xFFFFFF) | ((int)(microPoint.Opacity * 255.0) << 24));
		}
		needCreatePoint = true;
	}

	private void UpdateRectanglesData()
	{
		if (Rectangles == null)
		{
			return;
		}
		foreach (IRectangle rectangle in Rectangles)
		{
			ShapeList.Add(rectangle);
		}
		needCreatePoint = true;
	}

	public void AddNewPointK(float x, float y, int color)
	{
		if (PointX_Buffer == null)
		{
			NeedLargePoints = true;
		}
		if (PointLenth >= PointX_Buffer.Length)
		{
			int num = PointX_Buffer.Length;
			float[] pointX_Buffer = PointX_Buffer;
			float[] pointY_Buffer = PointY_Buffer;
			int[] pointColor_Buffer = PointColor_Buffer;
			PointX_Buffer = new float[num + 100000];
			PointY_Buffer = new float[num + 100000];
			PointColor_Buffer = new int[num + 100000];
			Buffer.BlockCopy(pointX_Buffer, 0, PointX_Buffer, 0, pointX_Buffer.Length * 4);
			Buffer.BlockCopy(pointY_Buffer, 0, PointY_Buffer, 0, pointY_Buffer.Length * 4);
			Buffer.BlockCopy(pointColor_Buffer, 0, PointColor_Buffer, 0, pointColor_Buffer.Length * 4);
		}
		PointX_Buffer[PointLenth] = x;
		PointY_Buffer[PointLenth] = y;
		PointColor_Buffer[PointLenth] = color;
		PointLenth++;
	}

	static MultiPointShape()
	{
		_step = 10000;
		OutlinePointsProperty = DependencyProperty.Register("OutlinePoints", typeof(ObservableCollection<IOutlinePoint>), typeof(MultiPointShape), new PropertyMetadata(null, OnOutlinePointsChanged));
		MicroPointsProperty = DependencyProperty.Register("MicroPoints", typeof(ObservableCollection<IMicroPoint>), typeof(MultiPointShape), new PropertyMetadata(null, OnMicroPointsChanged));
		RectanglesProperty = DependencyProperty.Register("Rectangles", typeof(ObservableCollection<IRectangle>), typeof(MultiPointShape), new PropertyMetadata(null, OnRectanglesChanged));
		NeedRefreshProperty = DependencyProperty.Register("NeedRefresh", typeof(bool), typeof(MultiPointShape), new PropertyMetadata(false, OnNeedRefreshChanged));
		DefaultOkColor = 65280;
		DefaultNgColor = 16776960;
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(MultiPointShape), new FrameworkPropertyMetadata(typeof(MultiPointShape)));
	}

	public MultiPointShape()
	{
		Loaded += MultiPointShape_Loaded;
	}

	private void MultiPointShape_Loaded(object sender, RoutedEventArgs e)
	{
		if (needCreatePoint)
		{
			CreatPointK();
		}
	}

	~MultiPointShape()
	{
	}
}
