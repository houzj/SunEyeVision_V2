using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;
using System.Xml;
using Apps.BaseData;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ItemInterface;
using Hik.Xml;
using HikExternalCall.Common;
using Mvvm.Commands;
using VM.Controls;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;
using VMControls.Interface;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public class ROISelecter : NameValueItem, IGetROI, IGroupRunParam, IROISelecter, IGroupRunParamWithMultiParams
{
	public delegate void EventHandler();

	public static int MaxPolygonPointNum = 32;

	public string SelectType = "Mul";

	public int RoiMaxNum = 8;

	private byte[] CurValue;

	public bool FullScreenEnable = true;

	public string DefaultValue;

	public bool IsCurROI = true;

	public bool IsDisplaySeq = false;

	public bool IsListenResultView = false;

	protected ROIUseType UseType = ROIUseType.Other;

	private byte[] PreValue;

	private Dictionary<string, string> _roiTypeWithSelectMap = new Dictionary<string, string>();

	public bool IsShowCaliperParam = true;

	public bool IsDisplayLabel = false;

	public string DisplayLabel;

	public string ROISelection;

	private IROI _CurROIData;

	private AlgorithmTabRoot _Root;

	protected Dictionary<ROIType, List<byte[]>> RoiByteData = new Dictionary<ROIType, List<byte[]>>();

	private ICommand _DragingChangedCommand = null;

	protected bool FullScreen;

	protected bool Box;

	protected bool DoubleBox;

	protected bool QuadraBox;

	protected bool SectorRing;

	protected bool Sector;

	protected bool OverSector;

	protected bool SectorRing2;

	protected bool Polygon;

	protected bool LineCaliper;

	protected bool CircleCaliper;

	protected bool Point;

	protected bool Line;

	protected bool CircleMes;

	protected bool SectorCaliper;

	protected bool MatrixCircle;

	protected bool Coordinate;

	protected bool PolygonCaliper;

	private bool FullScreen_ModuleEnable;

	private bool Box_ModuleEnable;

	private bool UroBox_ModuleEnable;

	private bool DoubleBox_ModuleEnable;

	private bool QuadraBox_ModuleEnable;

	private bool SectorRing_ModuleEnable;

	private bool SectorRing2_ModuleEnable;

	private bool Polygon_ModuleEnable;

	private bool LineCaliper_ModuleEnable;

	private bool CircleCaliper_ModuleEnable;

	private bool Point_ModuleEnable;

	private bool Line_ModuleEnable;

	private bool CircleMes_ModuleEnable;

	private bool Sector_ModuleEnable;

	private bool OverSector_ModuleEnable;

	private bool SectorCaliper_ModuleEnable;

	private bool MatrixCircle_ModuleEnable;

	private bool Coordinate_ModuleEnable;

	private bool PolygonCaliper_ModuleEnable;

	private Type _DrawROIType;

	private bool _IsStateForDrawROI;

	private DelegateCommand<object> _ValueChangedCommand = null;

	protected bool isCustom;

	private DelegateCommand _ROIValueChangedCommand = null;

	private string[] _RoiSeq;

	private const string ImageSize = "DrawImageSize";

	private int curImageWidth;

	private int curImageHeight;

	private ICommand _AddROICommand;

	public static IROI OldROI = new RectROIModel();

	protected bool _CanUpdateROI = true;

	public byte[] CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			CurValue = value;
			OnPropertyChanged("CurValueA");
		}
	}

	public bool FullScreenEnableA
	{
		get
		{
			return FullScreenEnable;
		}
		set
		{
			FullScreenEnable = value;
			OnPropertyChanged("FullScreenEnableA");
		}
	}

	public bool IsShowCaliperParamA
	{
		get
		{
			return IsShowCaliperParam;
		}
		set
		{
			if (IsShowCaliperParam != value)
			{
				IsShowCaliperParam = value;
				OnPropertyChanged("IsShowCaliperParamA");
			}
		}
	}

	public bool IsHaveData => Box_ModuleEnable || UroBox_ModuleEnable || SectorRing_ModuleEnable || Sector_ModuleEnable || SectorCaliper_ModuleEnable || Polygon_ModuleEnable || LineCaliper_ModuleEnable || CircleCaliper_ModuleEnable || Point_ModuleEnable || Line_ModuleEnable || CircleMes_ModuleEnable || OverSector_ModuleEnable || MatrixCircle_ModuleEnable || Coordinate_ModuleEnable;

	public override bool CustomVisibleA
	{
		get
		{
			return base.CustomVisibleA;
		}
		set
		{
			base.CustomVisibleA = value;
			if (!value)
			{
				StopDrawROI();
			}
		}
	}

	public bool IsAvoid => SelectType == "Avoid";

	public string ROISelectionA
	{
		get
		{
			return ROISelection;
		}
		set
		{
			if (ROISelection != value)
			{
				ROISelection = value;
				OnPropertyChanged("ROISelectionA");
			}
		}
	}

	public IROI CurROIData
	{
		get
		{
			return _CurROIData;
		}
		set
		{
			if (_CurROIData != value)
			{
				_CurROIData = value;
				OnPropertyChanged("CurROIData");
			}
		}
	}

	public ObservableRangeCollection<IROI> ROIList => NormalizeROIList;

	private AlgorithmTabRoot Root
	{
		get
		{
			if (_Root == null)
			{
				_Root = GetParentNode<AlgorithmTabRoot>();
			}
			return _Root;
		}
	}

	public ICommand DragingChangedCommand => _DragingChangedCommand ?? (_DragingChangedCommand = (ICommand)new DelegateCommand<object>((Action<object>)DragingChangedCommandExecute));

	internal bool needSetImageSize { get; set; } = true;

	public bool FullScreenA
	{
		get
		{
			return FullScreen;
		}
		set
		{
			if (FullScreen != value)
			{
				FullScreen = value;
				OnPropertyChanged("FullScreenA");
			}
		}
	}

	public bool BoxA
	{
		get
		{
			return Box;
		}
		set
		{
			if (Box != value)
			{
				Box = value;
				OnPropertyChanged("BoxA");
			}
		}
	}

	public bool UroBoxA
	{
		get
		{
			return Box;
		}
		set
		{
			if (Box != value)
			{
				Box = value;
				OnPropertyChanged("BoxA");
			}
		}
	}

	public bool DoubleBoxA
	{
		get
		{
			return DoubleBox;
		}
		set
		{
			if (DoubleBox != value)
			{
				DoubleBox = value;
				OnPropertyChanged("DoubleBoxA");
			}
		}
	}

	public bool QuadraBoxA
	{
		get
		{
			return QuadraBox;
		}
		set
		{
			if (QuadraBox != value)
			{
				QuadraBox = value;
				OnPropertyChanged("QuadraBoxA");
			}
		}
	}

	public bool SectorRingA
	{
		get
		{
			return SectorRing;
		}
		set
		{
			if (SectorRing != value)
			{
				SectorRing = value;
				OnPropertyChanged("SectorRingA");
			}
		}
	}

	public bool SectorRing2A
	{
		get
		{
			return SectorRing2;
		}
		set
		{
			if (SectorRing2 != value)
			{
				SectorRing2 = value;
				OnPropertyChanged("SectorRing2A");
			}
		}
	}

	public bool PolygonA
	{
		get
		{
			return Polygon;
		}
		set
		{
			if (Polygon != value)
			{
				Polygon = value;
				OnPropertyChanged("PolygonA");
			}
		}
	}

	public bool LineCaliperA
	{
		get
		{
			return LineCaliper;
		}
		set
		{
			if (LineCaliper != value)
			{
				LineCaliper = value;
				OnPropertyChanged("LineCaliperA");
			}
		}
	}

	public bool CircleCaliperA
	{
		get
		{
			return CircleCaliper;
		}
		set
		{
			if (CircleCaliper != value)
			{
				CircleCaliper = value;
				OnPropertyChanged("CircleCaliperA");
			}
		}
	}

	public bool PointA
	{
		get
		{
			return Point;
		}
		set
		{
			if (Point != value)
			{
				Point = value;
				OnPropertyChanged("PointA");
			}
		}
	}

	public bool LineA
	{
		get
		{
			return Line;
		}
		set
		{
			if (Line != value)
			{
				Line = value;
				OnPropertyChanged("LineA");
			}
		}
	}

	public bool CircleMesA
	{
		get
		{
			return CircleMes;
		}
		set
		{
			if (CircleMes != value)
			{
				CircleMes = value;
				OnPropertyChanged("CircleMesA");
			}
		}
	}

	public bool SectorA
	{
		get
		{
			return Sector;
		}
		set
		{
			if (Sector != value)
			{
				Sector = value;
				OnPropertyChanged("SectorA");
			}
		}
	}

	public bool OverSectorA
	{
		get
		{
			return OverSector;
		}
		set
		{
			if (OverSector != value)
			{
				OverSector = value;
				OnPropertyChanged("OverSectorA");
			}
		}
	}

	public bool SectorCaliperA
	{
		get
		{
			return SectorCaliper;
		}
		set
		{
			if (SectorCaliper != value)
			{
				SectorCaliper = value;
				OnPropertyChanged("SectorCaliperA");
			}
		}
	}

	public bool MatrixCircleA
	{
		get
		{
			return MatrixCircle;
		}
		set
		{
			if (MatrixCircle != value)
			{
				MatrixCircle = value;
				OnPropertyChanged("MatrixCircleA");
			}
		}
	}

	public bool CoordinateA
	{
		get
		{
			return Coordinate;
		}
		set
		{
			if (Coordinate != value)
			{
				Coordinate = value;
				OnPropertyChanged("CoordinateA");
			}
		}
	}

	public bool PolygonCaliperA
	{
		get
		{
			return PolygonCaliper;
		}
		set
		{
			if (PolygonCaliper != value)
			{
				PolygonCaliper = value;
				OnPropertyChanged("PolygonCaliperA");
			}
		}
	}

	public bool FullScreen_ModuleEnableA
	{
		get
		{
			return FullScreen_ModuleEnable;
		}
		set
		{
			if (FullScreen_ModuleEnable != value)
			{
				FullScreen_ModuleEnable = value;
				OnPropertyChanged("FullScreen_ModuleEnableA");
			}
		}
	}

	public virtual bool UroBoxEnable_ModuleEnableA
	{
		get
		{
			return UroBox_ModuleEnable;
		}
		set
		{
			if (UroBox_ModuleEnable != value)
			{
				UroBox_ModuleEnable = value;
				OnPropertyChanged("UroBoxEnable_ModuleEnableA");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	public virtual bool BoxEnable_ModuleEnableA
	{
		get
		{
			return Box_ModuleEnable;
		}
		set
		{
			if (Box_ModuleEnable != value)
			{
				Box_ModuleEnable = value;
				OnPropertyChanged("BoxEnable_ModuleEnableA");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	public bool DoubleBoxEnable_ModuleEnableA
	{
		get
		{
			return DoubleBox_ModuleEnable;
		}
		set
		{
			if (DoubleBox_ModuleEnable != value)
			{
				DoubleBox_ModuleEnable = value;
				OnPropertyChanged("DoubleBoxEnable_ModuleEnableA");
			}
		}
	}

	public bool QuadraBoxEnable_ModuleEnableA
	{
		get
		{
			return QuadraBox_ModuleEnable;
		}
		set
		{
			if (QuadraBox_ModuleEnable != value)
			{
				QuadraBox_ModuleEnable = value;
				OnPropertyChanged("QuadraBoxEnable_ModuleEnableA");
			}
		}
	}

	public virtual bool SectorRingEnable_ModuleEnableA
	{
		get
		{
			return SectorRing_ModuleEnable;
		}
		set
		{
			if (SectorRing_ModuleEnable != value)
			{
				SectorRing_ModuleEnable = value;
				OnPropertyChanged("SectorRingEnable_ModuleEnableA");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	public bool SectorRing2Enable_ModuleEnableA
	{
		get
		{
			return SectorRing2_ModuleEnable;
		}
		set
		{
			if (SectorRing2_ModuleEnable != value)
			{
				SectorRing2_ModuleEnable = value;
				OnPropertyChanged("SectorRing2Enable_ModuleEnableA");
			}
		}
	}

	public virtual bool PolygonEnable_ModuleEnableA
	{
		get
		{
			return Polygon_ModuleEnable;
		}
		set
		{
			if (Polygon_ModuleEnable != value)
			{
				Polygon_ModuleEnable = value;
				OnPropertyChanged("PolygonEnable_ModuleEnableA");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	public virtual bool LineCaliper_ModuleEnableA
	{
		get
		{
			return LineCaliper_ModuleEnable;
		}
		set
		{
			if (LineCaliper_ModuleEnable != value)
			{
				LineCaliper_ModuleEnable = value;
				OnPropertyChanged("LineCaliper_ModuleEnableA");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	public virtual bool CircleCaliper_ModuleEnableA
	{
		get
		{
			return CircleCaliper_ModuleEnable;
		}
		set
		{
			if (CircleCaliper_ModuleEnable != value)
			{
				CircleCaliper_ModuleEnable = value;
				OnPropertyChanged("CircleCaliper_ModuleEnableA");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	public virtual bool Point_ModuleEnableA
	{
		get
		{
			return Point_ModuleEnable;
		}
		set
		{
			if (Point_ModuleEnable != value)
			{
				Point_ModuleEnable = value;
				OnPropertyChanged("Point_ModuleEnableA");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	public virtual bool Line_ModuleEnableA
	{
		get
		{
			return Line_ModuleEnable;
		}
		set
		{
			if (Line_ModuleEnable != value)
			{
				Line_ModuleEnable = value;
				OnPropertyChanged("Line_ModuleEnableA");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	public virtual bool CircleMes_ModuleEnableA
	{
		get
		{
			return CircleMes_ModuleEnable;
		}
		set
		{
			if (CircleMes_ModuleEnable != value)
			{
				CircleMes_ModuleEnable = value;
				OnPropertyChanged("CircleMes_ModuleEnableA");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	public virtual bool SectorEnable_ModuleEnableA
	{
		get
		{
			return Sector_ModuleEnable;
		}
		set
		{
			if (Sector_ModuleEnable != value)
			{
				Sector_ModuleEnable = value;
				OnPropertyChanged("SectorEnable_ModuleEnableA");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	public virtual bool OverSector_ModuleEnableA
	{
		get
		{
			return OverSector_ModuleEnable;
		}
		set
		{
			if (OverSector_ModuleEnable != value)
			{
				OverSector_ModuleEnable = value;
				OnPropertyChanged("OverSector_ModuleEnableA");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	public virtual bool SectorCaliper_ModuleEnableA
	{
		get
		{
			return SectorCaliper_ModuleEnable;
		}
		set
		{
			if (SectorCaliper_ModuleEnable != value)
			{
				SectorCaliper_ModuleEnable = value;
				OnPropertyChanged("SectorCaliper_ModuleEnableA");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	public virtual bool MatrixCircle_ModuleEnableA
	{
		get
		{
			return MatrixCircle_ModuleEnable;
		}
		set
		{
			if (MatrixCircle_ModuleEnable != value)
			{
				MatrixCircle_ModuleEnable = value;
				OnPropertyChanged("MatrixCircle_ModuleEnableA");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	public virtual bool Coordinate_ModuleEnableA
	{
		get
		{
			return Coordinate_ModuleEnable;
		}
		set
		{
			if (Coordinate_ModuleEnable != value)
			{
				Coordinate_ModuleEnable = value;
				OnPropertyChanged("Coordinate_ModuleEnableA");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	public virtual bool PolygonCaliper_ModuleEnableA
	{
		get
		{
			return PolygonCaliper_ModuleEnable;
		}
		set
		{
			if (PolygonCaliper_ModuleEnable != value)
			{
				PolygonCaliper_ModuleEnable = value;
				OnPropertyChanged("PolygonCaliper_ModuleEnableA");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	private bool IsDoubleBox => ROISelection.Contains("DoubleBox");

	public Type DrawROIType
	{
		get
		{
			return _DrawROIType;
		}
		set
		{
			if (_DrawROIType != value)
			{
				_DrawROIType = value;
				OnPropertyChanged("DrawROIType");
			}
		}
	}

	public bool IsStateForDrawROI
	{
		get
		{
			return _IsStateForDrawROI;
		}
		set
		{
			if (_IsStateForDrawROI == value)
			{
				return;
			}
			_IsStateForDrawROI = value;
			OnPropertyChanged("IsStateForDrawROI");
			if (!(GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot))
			{
				return;
			}
			if (value)
			{
				algorithmTabRoot.ROISelecters.ForEach((IROISelecter item) =>
				{
					if (item != this)
					{
						item.IsStateForDrawROI = false;
					}
				});
				algorithmTabRoot.SelectedROI = this;
			}
			else if (algorithmTabRoot.SelectedROI == this)
			{
				algorithmTabRoot.SelectedROI = null;
			}
		}
	}

	public ICommand ValueChangedCommand
	{
		get
		{
			if (_ValueChangedCommand == null)
			{
				_ValueChangedCommand = new DelegateCommand<object>((Action<object>)ValueChangedCmdExecute);
			}
			return (ICommand)_ValueChangedCommand;
		}
	}

	public ICommand ROIValueChangedCommand
	{
		get
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected Obj, but got Unknown
			if (_ROIValueChangedCommand == null)
			{
				_ROIValueChangedCommand = new DelegateCommand((Action)ROIValueChangedCmdExecute);
			}
			return (ICommand)_ROIValueChangedCommand;
		}
	}

	public string ComNameSeq
	{
		get
		{
			if (Name != null && Name.StartsWith("%") && !string.IsNullOrEmpty(StayName))
			{
				return StayName + "Seq";
			}
			return ComName + "Seq";
		}
	}

	public string[] RoiSeq
	{
		get
		{
			if (_RoiSeq == null)
			{
				_RoiSeq = GetRoiSeq();
			}
			return _RoiSeq;
		}
	}

	public int CurImageWidth
	{
		get
		{
			return curImageWidth;
		}
		set
		{
			if (curImageWidth != value)
			{
				curImageWidth = value;
				OnPropertyChanged("CurImageWidth");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	public int CurImageHeight
	{
		get
		{
			return curImageHeight;
		}
		set
		{
			if (curImageHeight != value)
			{
				curImageHeight = value;
				OnPropertyChanged("CurImageHeight");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	string IGroupRunParam.DisplayForGroupType => "RoiConfigControl";

	public ROIObservableRangeCollection<IROI> NormalizeROIList { get; set; } = new ROIObservableRangeCollection<IROI>();

	public ICommand AddROICommand => _AddROICommand ?? (_AddROICommand = (ICommand)new DelegateCommand<object>((Action<object>)AddROICommandExecute));

	bool IROISelecter.CanUpdateROI
	{
		get
		{
			return _CanUpdateROI;
		}
		set
		{
			if (_CanUpdateROI != value)
			{
				_CanUpdateROI = value;
				OnPropertyChanged("CanUpdateROI");
			}
		}
	}

	public event EventHandler NoticeSelectEvent;

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		InitMap();
		SetCollectionVisible();
		PropertyChanged += ROISelecter_PropertyChanged;
		return this;
	}

	protected void InitMap()
	{
		string[] array = SelectType?.Split(' ');
		if (array != null && array.Length <= 1)
		{
			return;
		}
		string[] array2 = ROISelection?.Split(' ');
		if (array2 == null)
		{
			return;
		}
		if (array.Length == array2.Length)
		{
			for (int i = 0; i < array.Length; i++)
			{
				_roiTypeWithSelectMap.Add(array2[i], array[i]);
			}
		}
		else
		{
			HikMessageBox.Show("roi选择模式和roi类型个数不一致，请保持一致配置");
		}
	}

	private void ROISelecter_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		string propertyName = e.PropertyName;
		if (propertyName == "CustomVisibleA")
		{
			SetCollectionVisible();
		}
	}

	private void SetCollectionVisible()
	{
		((IROIVisible)NormalizeROIList).IsVisible = CustomVisible;
	}

	private void ROIVisibleChanged()
	{
		OnPropertyChanged("IsCurROIA");
	}

	public void UpdateRoiBySelf()
	{
	}

	protected void GetStructBytes()
	{
		IUserBytesData paramsSource = ParamsSource;
		if (paramsSource == null)
		{
			return;
		}
		try
		{
			CurValueA = paramsSource[ComName];
		}
		catch (VmException ex)
		{
			IVmModule vmModule = Root.Owner as IVmModule;
			string message = $"error:{ErrorCodeInfo.GetErrorInfo(ex.errorCode)},name:{ComName},moduleId;{vmModule.ID},moduleName:{vmModule.ModuleTypeName}";
			LogHelper.Error(message);
			return;
		}
		catch (Exception ex2)
		{
			IVmModule vmModule2 = Root.Owner as IVmModule;
			int result = 0;
			string text = (int.TryParse(ex2.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex2.Message);
			string message2 = $"error:{text},name:{ComName},moduleId;{vmModule2.ID},moduleName:{vmModule2.ModuleTypeName}";
			LogHelper.Error(message2);
			return;
		}
		RoiByteData.Clear();
		if (CurValueA == null)
		{
			return;
		}
		using MemoryStream memoryStream = new MemoryStream(CurValueA);
		BinaryReader binaryReader = new BinaryReader(memoryStream);
		while (memoryStream.Position < CurValueA.Length - 1)
		{
			ROIType rOIType = (ROIType)binaryReader.ReadByte();
			byte b = binaryReader.ReadByte();
			List<byte[]> list = new List<byte[]>();
			if (!RoiByteData.ContainsKey(rOIType))
			{
				RoiByteData.Add(rOIType, list);
			}
			else
			{
				list = RoiByteData[rOIType];
			}
			switch (rOIType)
			{
			case ROIType.Box:
			{
				int count10 = Marshal.SizeOf(typeof(ROIBox));
				byte[] item11 = binaryReader.ReadBytes(count10);
				list.Add(item11);
				break;
			}
			case ROIType.SectorRing:
			{
				int count9 = Marshal.SizeOf(typeof(ROISectorRing));
				byte[] item10 = binaryReader.ReadBytes(count9);
				list.Add(item10);
				break;
			}
			case ROIType.Polygon:
			{
				long position = memoryStream.Position;
				uint num = binaryReader.ReadUInt32();
				memoryStream.Position = position;
				byte[] item9 = binaryReader.ReadBytes((int)(num * 8 + 4));
				list.Add(item9);
				break;
			}
			case ROIType.LineCaliper:
			{
				int count8 = Marshal.SizeOf(typeof(ROILineCaliper));
				byte[] item8 = binaryReader.ReadBytes(count8);
				list.Add(item8);
				break;
			}
			case ROIType.CircleCaliper:
			{
				int count7 = Marshal.SizeOf(typeof(ROICircleCaliper));
				byte[] item7 = binaryReader.ReadBytes(count7);
				list.Add(item7);
				break;
			}
			case ROIType.Point:
			{
				int count6 = Marshal.SizeOf(typeof(ROIPoint));
				byte[] item6 = binaryReader.ReadBytes(count6);
				list.Add(item6);
				break;
			}
			case ROIType.Line:
			{
				int count5 = Marshal.SizeOf(typeof(ROILine));
				byte[] item5 = binaryReader.ReadBytes(count5);
				list.Add(item5);
				break;
			}
			case ROIType.CircleMes:
			{
				int count4 = Marshal.SizeOf(typeof(ROICicleMes));
				byte[] item4 = binaryReader.ReadBytes(count4);
				list.Add(item4);
				break;
			}
			case ROIType.SectorCaliper:
			{
				int count3 = Marshal.SizeOf(typeof(ROISectorCaliper));
				byte[] item3 = binaryReader.ReadBytes(count3);
				list.Add(item3);
				break;
			}
			case ROIType.MatrixCircle:
			{
				int count2 = Marshal.SizeOf(typeof(ROIMatrixCircle));
				byte[] item2 = binaryReader.ReadBytes(count2);
				list.Add(item2);
				break;
			}
			case ROIType.Coordinate:
			{
				int count = Marshal.SizeOf(typeof(ROICoordinate));
				byte[] item = binaryReader.ReadBytes(count);
				list.Add(item);
				break;
			}
			}
		}
	}

	private void RoiParamChange(IROI roi)
	{
		SetValue();
	}

	protected void ClearRoi()
	{
		NormalizeROIList.Clear();
	}

	private void DragingChangedCommandExecute(object data)
	{
		bool flag = (bool)data;
		_CanUpdateROI = !flag;
	}

	protected virtual void AddROI(IROI roiParameter)
	{
		try
		{
			InitRoi(roiParameter);
			NormalizeROIList.Add(roiParameter);
		}
		catch (Exception ex)
		{
			LogHelper.Error("Message:" + ex.Message + "StackTrace:" + ex.StackTrace);
		}
	}

	internal void SelectROI(IROISelectable roi)
	{
		if (roi != null)
		{
			roi.IsSelected = true;
			ClearOtherROIGroup();
		}
	}

	internal void ClearOtherROIGroup()
	{
		Root.ROISelecters.ForEach((IROISelecter p) =>
		{
			if (p == this)
			{
				return;
			}
			p.CurROIData = null;
			foreach (IROISelectable item in p.ROIList.OfType<IROISelectable>())
			{
				item.IsSelected = false;
			}
		});
	}

	public int BaseSetParamUnit()
	{
		return base.SetParamUnit();
	}

	public override int SetParamUnit()
	{
		if (string.IsNullOrEmpty(AccessMode) || !AccessMode.ToLower().Contains("w"))
		{
			OnSetValueCompleted(success: false);
			return -2;
		}
		return SetBinaryData(CurValueA);
	}

	private void InnerSetValue(byte[] value)
	{
		((IUserBytesData)ParamsSource)[ComName] = value;
	}

	public int SetBinaryData(byte[] value)
	{
		try
		{
			if (userStringData == null)
			{
				userStringData = (GetTopBaseNode() as AlgorithmTabRoot).UserStringData;
			}
			LogHelper.objLog.Info("ComName：" + ComName);
			MyItemState = ItemState.Normal;
			InnerSetValue(value);
			OnSetValueCompleted(success: true);
			return 0;
		}
		catch (Exception ex)
		{
			LogHelper.Error("设置参数：" + ComName + "失败：" + ex.Message);
			MyItemState = ItemState.SetParamErro;
			OnSetValueCompleted(success: false);
			return -1;
		}
	}

	protected void SetValue(bool needRefresh = true)
	{
		if (string.IsNullOrEmpty(AccessMode) || !AccessMode.ToLower().Contains("w"))
		{
			return;
		}
		if (needRefresh)
		{
			SelectRoiIcon();
		}
		byte[] array = NormalizeReversParse();
		if (PreValue == null || !PreValue.SequenceEqual(array))
		{
			PreValue = array;
			CurValueA = array;
			if (needSetImageSize)
			{
				SetImageSize(curImageWidth, curImageHeight);
			}
			SetParamUnit();
			SetRoiSeq();
			PreExecuteRun();
		}
	}

	protected int BaseGetParamUnit()
	{
		return base.GetParamUnit();
	}

	public override int GetParamUnit()
	{
		if (string.IsNullOrEmpty(AccessMode) || !AccessMode.ToLower().Contains("r"))
		{
			OnGetValueCompleted(success: false);
			return -2;
		}
		return GetBinaryData();
	}

	public int GetBinaryData()
	{
		try
		{
			if (userStringData == null)
			{
				userStringData = (GetTopBaseNode() as AlgorithmTabRoot).UserStringData;
			}
			MyItemState = ItemState.Normal;
			CurValueA = ((IUserBytesData)ParamsSource)[ComName];
			LogHelper.objLog.Info("ComName：" + ComName);
			OnGetValueCompleted(success: true);
			return 0;
		}
		catch (Exception ex)
		{
			LogHelper.Error("获取参数：" + ComName + "失败：" + ex.Message);
			MyItemState = ItemState.GetParamErro;
			OnGetValueCompleted(success: false);
			return -1;
		}
	}

	private void ClearSelected()
	{
		FullScreen_ModuleEnableA = false;
		BoxEnable_ModuleEnableA = false;
		UroBoxEnable_ModuleEnableA = false;
		DoubleBoxEnable_ModuleEnableA = false;
		QuadraBoxEnable_ModuleEnableA = false;
		SectorRingEnable_ModuleEnableA = false;
		SectorRing2Enable_ModuleEnableA = false;
		PolygonEnable_ModuleEnableA = false;
		LineCaliper_ModuleEnableA = false;
		CircleCaliper_ModuleEnableA = false;
		Point_ModuleEnableA = false;
		Line_ModuleEnableA = false;
		CircleMes_ModuleEnableA = false;
		SectorEnable_ModuleEnableA = false;
		OverSector_ModuleEnableA = false;
		SectorCaliper_ModuleEnableA = false;
		MatrixCircle_ModuleEnableA = false;
		Coordinate_ModuleEnableA = false;
		PolygonCaliper_ModuleEnableA = false;
	}

	public virtual void SelectNone()
	{
		FullScreenA = false;
		BoxA = false;
		UroBoxA = false;
		DoubleBoxA = false;
		QuadraBoxA = false;
		SectorRingA = false;
		SectorA = false;
		OverSectorA = false;
		SectorRing2A = false;
		PolygonA = false;
		LineCaliperA = false;
		CircleCaliperA = false;
		PointA = false;
		LineA = false;
		CircleMesA = false;
		SectorCaliperA = false;
		MatrixCircleA = false;
		CoordinateA = false;
		PolygonCaliperA = false;
	}

	void IGetROI.SetRoiList(int imageWidth, int imageHeight, List<IROI> roiInputs)
	{
		try
		{
			IUserBytesData moduleTagInfo = GetModuleTagInfo();
			byte[] value = NormalizeReversParse(roiInputs);
			SetImageSize(imageWidth, imageHeight);
			moduleTagInfo[ComName] = value;
			SetRoiSeq(roiInputs);
		}
		catch (Exception)
		{
			throw;
		}
	}

	void IGetROI.GetRoiList(int imageWidth, int imageHeight, List<IROI> roiResults)
	{
		try
		{
			IUserBytesData moduleTagInfo = GetModuleTagInfo();
			byte[] roiBuffer = moduleTagInfo[ComName];
			ParseRoiProtocol(roiBuffer, imageWidth, imageHeight, roiResults);
		}
		catch (Exception)
		{
			throw;
		}
	}

	protected virtual void ParseRoiProtocol(byte[] roiBuffer = null, int imageWidth = 0, int imageHeight = 0, List<IROI> roiResults = null)
	{
		if (roiBuffer == null)
		{
			roiBuffer = CurValueA;
		}
		if (roiBuffer == null)
		{
			return;
		}
		bool flag = true;
		if (roiBuffer.Length > 22)
		{
			flag = false;
		}
		using MemoryStream memoryStream = new MemoryStream(roiBuffer);
		BinaryReader binaryReader = new BinaryReader(memoryStream);
		while (memoryStream.Position < roiBuffer.Length - 1)
		{
			byte b = binaryReader.ReadByte();
			byte version = binaryReader.ReadByte();
			switch ((ROIType)b)
			{
			case ROIType.Avoid:
			case ROIType.FullScreen:
				if (roiResults == null && ROIList.Count > 0)
				{
					ClearRoi();
				}
				break;
			case ROIType.Box:
			{
				int count9 = Marshal.SizeOf(typeof(ROIBox));
				byte[] array9 = binaryReader.ReadBytes(count9);
				IROI iROI10 = ExternalCallHelper.BytesToStruct<ROIBox>(array9, 0, array9.Length).GetIROI(version, imageWidth, imageHeight, ROISelectionA.Contains(RoiType.LineBox.ToString()), flag && ROISelectionA.Contains(RoiType.SeqBox.ToString()));
				if (roiResults == null)
				{
					AddROI(iROI10);
				}
				else
				{
					roiResults.Add(iROI10);
				}
				break;
			}
			case ROIType.SectorRing:
			{
				int count5 = Marshal.SizeOf(typeof(ROISectorRing));
				byte[] array5 = binaryReader.ReadBytes(count5);
				IROI iROI5 = ExternalCallHelper.BytesToStruct<ROISectorRing>(array5, 0, array5.Length).GetIROI(imageWidth, imageHeight, version);
				if (roiResults == null)
				{
					AddROI(iROI5);
				}
				else
				{
					roiResults.Add(iROI5);
				}
				break;
			}
			case ROIType.Polygon:
			{
				long position = memoryStream.Position;
				uint num = binaryReader.ReadUInt32();
				memoryStream.Position = position;
				byte[] data = binaryReader.ReadBytes((int)(num * 8 + 4));
				IROI iROI8 = GetPolygonStruct(data).GetIROI(this, imageWidth, imageHeight);
				if (roiResults == null)
				{
					AddROI(iROI8);
				}
				else
				{
					roiResults.Add(iROI8);
				}
				break;
			}
			case ROIType.LineCaliper:
			{
				int count2 = Marshal.SizeOf(typeof(ROILineCaliper));
				byte[] array2 = binaryReader.ReadBytes(count2);
				IROI iROI2 = ExternalCallHelper.BytesToStruct<ROILineCaliper>(array2, 0, array2.Length).GetIROI(imageWidth, imageHeight);
				if (roiResults == null)
				{
					AddROI(iROI2);
				}
				else
				{
					roiResults.Add(iROI2);
				}
				break;
			}
			case ROIType.CircleCaliper:
			{
				int count7 = Marshal.SizeOf(typeof(ROICircleCaliper));
				byte[] array7 = binaryReader.ReadBytes(count7);
				IROI iROI7 = ExternalCallHelper.BytesToStruct<ROICircleCaliper>(array7, 0, array7.Length).GetIROI(imageWidth, imageHeight);
				if (roiResults == null)
				{
					AddROI(iROI7);
				}
				else
				{
					roiResults.Add(iROI7);
				}
				break;
			}
			case ROIType.SectorCaliper:
			{
				int count3 = Marshal.SizeOf(typeof(ROISectorCaliper));
				byte[] array3 = binaryReader.ReadBytes(count3);
				IROI iROI3 = ExternalCallHelper.BytesToStruct<ROISectorCaliper>(array3, 0, array3.Length).GetIROI(imageWidth, imageHeight);
				if (roiResults == null)
				{
					AddROI(iROI3);
				}
				else
				{
					roiResults.Add(iROI3);
				}
				break;
			}
			case ROIType.Point:
			{
				int count8 = Marshal.SizeOf(typeof(ROIPoint));
				byte[] array8 = binaryReader.ReadBytes(count8);
				IROI iROI9 = ExternalCallHelper.BytesToStruct<ROIPoint>(array8, 0, array8.Length).GetIROI(imageWidth, imageHeight);
				if (roiResults == null)
				{
					AddROI(iROI9);
				}
				else
				{
					roiResults.Add(iROI9);
				}
				break;
			}
			case ROIType.Line:
			{
				int count6 = Marshal.SizeOf(typeof(ROILine));
				byte[] array6 = binaryReader.ReadBytes(count6);
				IROI iROI6 = ExternalCallHelper.BytesToStruct<ROILine>(array6, 0, array6.Length).GetIROI(imageWidth, imageHeight);
				if (roiResults == null)
				{
					AddROI(iROI6);
				}
				else
				{
					roiResults.Add(iROI6);
				}
				break;
			}
			case ROIType.CircleMes:
			{
				int count4 = Marshal.SizeOf(typeof(ROICicleMes));
				byte[] array4 = binaryReader.ReadBytes(count4);
				IROI iROI4 = ExternalCallHelper.BytesToStruct<ROICicleMes>(array4, 0, array4.Length).GetIROI(imageWidth, imageHeight);
				if (roiResults == null)
				{
					AddROI(iROI4);
				}
				else
				{
					roiResults.Add(iROI4);
				}
				break;
			}
			case ROIType.MatrixCircle:
			{
				int count = Marshal.SizeOf(typeof(ROIMatrixCircle));
				byte[] array = binaryReader.ReadBytes(count);
				IROI iROI = ExternalCallHelper.BytesToStruct<ROIMatrixCircle>(array, 0, array.Length).GetIROI(imageWidth, imageHeight);
				if (roiResults == null)
				{
					AddROI(iROI);
				}
				else
				{
					roiResults.Add(iROI);
				}
				break;
			}
			}
		}
	}

	protected ROIPolygon GetPolygonStruct(byte[] data)
	{
		using MemoryStream input = new MemoryStream(data);
		using BinaryReader binaryReader = new BinaryReader(input);
		uint num = binaryReader.ReadUInt32();
		ROIPolygon result = new ROIPolygon(num);
		for (int i = 0; i < num; i++)
		{
			float x = binaryReader.ReadSingle();
			float y = binaryReader.ReadSingle();
			result.Points[i] = new FloatPoint
			{
				X = x,
				Y = y
			};
		}
		return result;
	}

	private void NormalizeSelectRoiIcon()
	{
		ClearSelected();
		if (NormalizeROIList.Count < 1)
		{
			FullScreen_ModuleEnableA = true;
			return;
		}
		IROI iROI = NormalizeROIList.Last();
		if (iROI != null)
		{
			SelectRoiIcon(iROI);
		}
	}

	private void SelectRoiIcon()
	{
		ClearSelected();
		if (ROIList.Count < 1)
		{
			FullScreen_ModuleEnableA = true;
			return;
		}
		IROI iROI = ROIList.OfType<IROISelectable>().LastOrDefault((IROISelectable p) => p.IsSelected) as IROI;
		if (iROI == null && UseType != ROIUseType.Mul)
		{
			iROI = ROIList.LastOrDefault();
		}
		if (iROI != null)
		{
			SelectRoiIcon(iROI);
		}
	}

	private void SelectRoiIcon(IROI roi)
	{
		Type type = roi.GetType();
		if (roi is IUroRectROI)
		{
			UroBoxEnable_ModuleEnableA = true;
		}
		else if (roi is IRectROI || roi is ILineRectROI || roi is ISeqRectROI)
		{
			if (IsDoubleBox && ROIList.Count == 2)
			{
				DoubleBoxEnable_ModuleEnableA = true;
			}
			else if (roi is IMatrixCircleROI)
			{
				MatrixCircle_ModuleEnableA = true;
			}
			else
			{
				BoxEnable_ModuleEnableA = true;
			}
		}
		else if (roi is ICircleROI)
		{
			SectorRingEnable_ModuleEnableA = true;
		}
		else if (roi is ISectorROI)
		{
			if (roi is IOverSectorROI)
			{
				OverSector_ModuleEnableA = true;
			}
			else
			{
				SectorEnable_ModuleEnableA = true;
			}
		}
		else if (roi is IPolygonROI)
		{
			PolygonEnable_ModuleEnableA = true;
		}
		else if (roi is ICircleCaliperROI)
		{
			CircleCaliper_ModuleEnableA = true;
		}
		else if (roi is ILineCaliperROI)
		{
			LineCaliper_ModuleEnableA = true;
		}
		else if (roi is ISectorCaliperROI)
		{
			SectorCaliper_ModuleEnableA = true;
		}
		else if (roi is IPointROI)
		{
			Point_ModuleEnableA = true;
		}
		else if (roi is ILineROI)
		{
			Line_ModuleEnableA = true;
		}
		else if (roi is ICircleMesROI)
		{
			CircleMes_ModuleEnableA = true;
		}
		else if (roi is ICoordinateROI)
		{
			Coordinate_ModuleEnableA = true;
		}
		else if (roi is IPolygonCaliperROI)
		{
			PolygonCaliper_ModuleEnableA = true;
		}
	}

	protected virtual void ValueChangedCmdExecute(object type)
	{
		if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
		{
			foreach (IROISelecter rOISelecter in algorithmTabRoot.ROISelecters)
			{
				if (rOISelecter != this)
				{
					rOISelecter.SelectNone();
				}
			}
		}
		if (type is RoiType roiType)
		{
			if (!isCustom)
			{
				DoRadioButton(ref roiType);
			}
			if (roiType == RoiType.FullScreen)
			{
				SelectFullScreen();
			}
			else if (ROIList.Count >= RoiMaxNum)
			{
				SelectNone();
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ParamSet"), AppLocalizationService.GetLang("Lang_RoiMaxNumLimit"), MessageIcons.Warning);
			}
			else if (roiType == RoiType.DoubleBox)
			{
				SetDefault();
			}
			else
			{
				DrawROIType = GetRoiModelType(roiType);
				IsStateForDrawROI = DrawROIType != null;
			}
		}
	}

	public void DrawROI(RoiType rType)
	{
		DrawROIType = GetRoiModelType(rType);
		IsStateForDrawROI = DrawROIType != null;
	}

	public void DrawCustomROI(RoiType type)
	{
		isCustom = true;
		ValueChangedCmdExecute(type);
	}

	protected Type GetRoiModelType(RoiType roiType)
	{
		switch (roiType)
		{
		case RoiType.Box:
			return typeof(NormalizedRectROIModel);
		case RoiType.SectorRing:
			return typeof(NormalizedCircleROIModel);
		case RoiType.Polygon:
			if (SelectType == "Avoid")
			{
				return typeof(NormalizedPolygonMaskROIModel);
			}
			return typeof(NormalizedPolygonROIModel);
		case RoiType.Sector:
			return typeof(NormalizedSectorROIModel);
		case RoiType.CircleCaliper:
			return typeof(NormalizedCircleCaliperROIModel);
		case RoiType.LineCaliper:
			return typeof(NormalizedLineCaliperROIModel);
		case RoiType.SectorCaliper:
			return typeof(NormalizedSectorCaliperROIModel);
		case RoiType.Point:
			return typeof(NormalizedPointROIModel);
		case RoiType.Line:
			return typeof(NormalizedLineROIModel);
		case RoiType.CircleMes:
			return typeof(NormalizedCircleMesROIModel);
		case RoiType.LineBox:
			return typeof(NormalizedLineRectROIModel);
		case RoiType.SeqBox:
			return typeof(NormalizedSeqRectROIModel);
		case RoiType.UroBox:
			return typeof(NormalizedUroRectROIModel);
		case RoiType.OverSector:
			return typeof(NormalizedOverSectorROIModel);
		case RoiType.MatrixCircle:
			return typeof(NormalizedMatrixCircleROIModel);
		case RoiType.Coordinate:
			return typeof(CoordinateModel);
		default:
			return null;
		}
	}

	protected void DoRadioButton(ref RoiType roiType)
	{
		switch (roiType)
		{
		case RoiType.FullScreen:
			SelectNone();
			FullScreenA = true;
			break;
		case RoiType.CircleCaliper:
			if (CircleCaliperA)
			{
				SelectNone();
				CircleCaliperA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.LineCaliper:
			if (LineCaliperA)
			{
				SelectNone();
				LineCaliperA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.DoubleBox:
			if (DoubleBoxA)
			{
				SelectNone();
				DoubleBoxA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.QuadraBox:
			if (QuadraBoxA)
			{
				SelectNone();
				QuadraBoxA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.Box:
		case RoiType.LineBox:
		case RoiType.SeqBox:
			if (BoxA)
			{
				SelectNone();
				BoxA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.SectorRing:
			if (SectorRingA)
			{
				SelectNone();
				SectorRingA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.Sector:
			if (SectorA)
			{
				SelectNone();
				SectorA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.Polygon:
			if (PolygonA)
			{
				SelectNone();
				PolygonA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.Point:
			if (PointA)
			{
				SelectNone();
				PointA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.Line:
			if (LineA)
			{
				SelectNone();
				LineA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.CircleMes:
			if (CircleMesA)
			{
				SelectNone();
				CircleMesA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.SectorCaliper:
			if (SectorCaliperA)
			{
				SelectNone();
				SectorCaliperA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.UroBox:
			if (UroBoxA)
			{
				SelectNone();
				UroBoxA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.OverSector:
			if (OverSectorA)
			{
				SelectNone();
				OverSectorA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.MatrixCircle:
			if (MatrixCircleA)
			{
				SelectNone();
				MatrixCircleA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.Coordinate:
			if (CoordinateA)
			{
				SelectNone();
				CoordinateA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.PolygonCaliper:
			if (PolygonCaliperA)
			{
				SelectNone();
				PolygonCaliperA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.SectorRing2:
		case RoiType.Box3D:
			break;
		}
	}

	private void SelectFullScreen()
	{
		ClearRoi();
		StopDrawROI();
		SetValue();
	}

	protected ROIType GetRoiType(string roiType)
	{
		ROIType result = ROIType.Default;
		switch (roiType)
		{
		case "CircleCaliper":
			result = ROIType.CircleCaliper;
			break;
		case "LineCaliper":
			result = ROIType.LineCaliper;
			break;
		case "SeqRect":
		case "LineRect":
		case "Rect":
			result = ROIType.Box;
			break;
		case "OverSector":
		case "Sector":
		case "Circle":
			result = ROIType.SectorRing;
			break;
		case "PolygonMask":
		case "Polygon":
			result = ROIType.Polygon;
			break;
		case "Point":
			result = ROIType.Point;
			break;
		case "Line":
			result = ROIType.Line;
			break;
		case "CircleMes":
			result = ROIType.CircleMes;
			break;
		case "SectorCaliper":
			result = ROIType.SectorCaliper;
			break;
		case "MatrixCircle":
			result = ROIType.MatrixCircle;
			break;
		case "Coordinate":
			result = ROIType.Coordinate;
			break;
		}
		return result;
	}

	protected void SetDefault()
	{
		byte[] curValueA = new byte[2] { 255, 0 };
		CurValueA = curValueA;
		if (SetParamUnit() == 0)
		{
			GetParamUnit();
		}
	}

	public void NormalizeParseParamVal()
	{
		NormalizeROIList.Clear();
		SelectNone();
		NormalizeParseRoiProtocol();
		SelectROI(NormalizeROIList.LastOrDefault() as IROISelectable);
		NormalizeSelectRoiIcon();
		NoticeSelectEvent?.Invoke();
	}

	protected override void OnGetValueCompleted(bool success)
	{
		base.OnGetValueCompleted(success);
		_RoiSeq = GetRoiSeq();
		NormalizeParseParamVal();
	}

	protected override void OnSetValueCompleted(bool success)
	{
		base.OnSetValueCompleted(success);
	}

	public override void OnShownFinish()
	{
		base.OnShownFinish();
		StopDrawROI();
	}

	private void StopDrawROI()
	{
		SelectNone();
		IsStateForDrawROI = false;
	}

	protected virtual void ROIValueChangedCmdExecute()
	{
		SetValue(needRefresh: false);
	}

	private string[] GetRoiSeq()
	{
		if (IsDisplaySeq)
		{
			try
			{
				string text = InnerGetStringParamValue(ComNameSeq);
				LogHelper.objLog.Info(text);
				if (!string.IsNullOrEmpty(text))
				{
					return text.Split(' ');
				}
			}
			catch (Exception ex)
			{
				LogHelper.objLog.Error(ex.Message);
			}
		}
		return new string[0];
	}

	private string GetRoiSeqStr(IList<IROI> roiInputs = null)
	{
		StringBuilder stringBuilder = new StringBuilder("");
		int num = 0;
		foreach (IROILabel item in roiInputs.OfType<IROILabel>())
		{
			num++;
			stringBuilder.Append(item.Label);
			if (num < roiInputs.Count)
			{
				stringBuilder.Append(" ");
			}
		}
		return stringBuilder.ToString();
	}

	private void InnerSetRoiSeq(string value)
	{
		InnerSetStringParamValue(ComNameSeq, value);
	}

	private void SetRoiSeq(IList<IROI> roiInputs = null)
	{
		if (!IsDisplaySeq)
		{
			return;
		}
		try
		{
			if (roiInputs == null)
			{
				roiInputs = ROIList;
			}
			string roiSeqStr = GetRoiSeqStr(roiInputs);
			_RoiSeq = roiSeqStr.Split(' ');
			LogHelper.objLog.Info(roiSeqStr);
			InnerSetRoiSeq(roiSeqStr);
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error(ex.Message);
		}
	}

	private void AddLabel(IROILabel roi)
	{
		if (!IsDisplaySeq)
		{
			return;
		}
		HashSet<int> hashSet = new HashSet<int>();
		foreach (IROILabel item in ROIList.OfType<IROILabel>())
		{
			if (int.TryParse(item.Label, out var result))
			{
				hashSet.Add(result);
			}
		}
		for (int i = 1; i <= RoiMaxNum; i++)
		{
			if (!hashSet.Contains(i))
			{
				roi.Label = i.ToString();
				break;
			}
		}
	}

	private void SetImageSize(int imageWidth = 0, int imageHeight = 0)
	{
		try
		{
			if (imageWidth == 0 || imageHeight == 0)
			{
				imageWidth = CurImageWidth;
				imageHeight = CurImageHeight;
			}
			InnerSetImageSize(imageWidth, imageHeight);
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error(ex.Message);
		}
	}

	private void InnerSetImageSize(int imageWidth, int imageHeight)
	{
		InnerSetStringParamValue("DrawImageSize", $"{imageWidth},{imageHeight}");
	}

	private void InnerGetImageSize(out int imageWidth, out int imageHeight)
	{
		string text = InnerGetStringParamValue("DrawImageSize");
		if (text != null)
		{
			string[] array = text.Split(',');
			if (array.Length == 2)
			{
				int.TryParse(array[0], out imageWidth);
				int.TryParse(array[1], out imageHeight);
				return;
			}
		}
		imageWidth = 0;
		imageHeight = 0;
	}

	IDecorate IGroupRunParam.SupportDisplayForGroupData()
	{
		return null;
	}

	protected T GetStruct<T>(BinaryReader binaryReader) where T : struct
	{
		int count = Marshal.SizeOf(typeof(T));
		byte[] array = binaryReader.ReadBytes(count);
		return ExternalCallHelper.BytesToStruct<T>(array, 0, array.Length);
	}

	protected virtual void NormalizeParseRoiProtocol(byte[] roiBuffer = null)
	{
		if (roiBuffer == null)
		{
			roiBuffer = CurValueA;
		}
		if (roiBuffer == null)
		{
			return;
		}
		bool isSingleRec = true;
		if (roiBuffer.Length > 22)
		{
			isSingleRec = false;
		}
		using MemoryStream memoryStream = new MemoryStream(roiBuffer);
		BinaryReader binaryReader = new BinaryReader(memoryStream);
		while (memoryStream.Position < roiBuffer.Length - 1)
		{
			byte b = binaryReader.ReadByte();
			byte version = binaryReader.ReadByte();
			IROI iROI = null;
			ROIStructInterface rOIStructInterface = null;
			switch ((ROIType)b)
			{
			case ROIType.Avoid:
			case ROIType.FullScreen:
				return;
			case ROIType.Box:
				rOIStructInterface = GetStruct<ROIBox>(binaryReader);
				break;
			case ROIType.SectorRing:
				rOIStructInterface = GetStruct<ROISectorRing>(binaryReader);
				break;
			case ROIType.Polygon:
			{
				long position = memoryStream.Position;
				uint num = binaryReader.ReadUInt32();
				memoryStream.Position = position;
				byte[] data = binaryReader.ReadBytes((int)(num * 8 + 4));
				rOIStructInterface = GetPolygonStruct(data);
				break;
			}
			case ROIType.LineCaliper:
				rOIStructInterface = GetStruct<ROILineCaliper>(binaryReader);
				break;
			case ROIType.CircleCaliper:
				rOIStructInterface = GetStruct<ROICircleCaliper>(binaryReader);
				break;
			case ROIType.SectorCaliper:
				rOIStructInterface = GetStruct<ROISectorCaliper>(binaryReader);
				break;
			case ROIType.Point:
				rOIStructInterface = GetStruct<ROIPoint>(binaryReader);
				break;
			case ROIType.Line:
				rOIStructInterface = GetStruct<ROILine>(binaryReader);
				break;
			case ROIType.CircleMes:
				rOIStructInterface = GetStruct<ROICicleMes>(binaryReader);
				break;
			case ROIType.MatrixCircle:
				rOIStructInterface = GetStruct<ROIMatrixCircle>(binaryReader);
				break;
			case ROIType.Coordinate:
				rOIStructInterface = GetStruct<ROICoordinate>(binaryReader);
				break;
			}
			if (rOIStructInterface == null)
			{
				break;
			}
			iROI = rOIStructInterface.GetNormalizeIROI(this, version, isSingleRec);
			InitRoi(iROI);
			NormalizeROIList.Add(iROI);
		}
	}

	private void NormalizeRemoveROI(IROI roi)
	{
		NormalizeROIList.Remove(roi);
	}

	private void NormalizeEndDrawRoi()
	{
		StopDrawROI();
	}

	protected virtual void InitRoi(IROI roiParameter)
	{
		if (IsDisplaySeq && roiParameter is IROILabel iROILabel)
		{
			if (ROIList.Count < RoiSeq.Length)
			{
				iROILabel.Label = RoiSeq[ROIList.Count];
			}
			else
			{
				iROILabel.Label = (ROIList.Count + 1).ToString();
			}
		}
		if (roiParameter is IROIExternParam iROIExternParam)
		{
			iROIExternParam.IsShowCaliperParam = IsShowCaliperParam;
			if (!IsShowCaliperParam)
			{
				(roiParameter as NormalizedCircleCaliperROIModel).CaliperNum = 3u;
				(roiParameter as NormalizedCircleCaliperROIModel).CaliperWidth = 0.0001;
				(roiParameter as NormalizedCircleCaliperROIModel).CaliperHeight = 0.0001;
			}
		}
		if (!string.IsNullOrEmpty(AccessMode) && AccessMode.ToLower().Contains("w"))
		{
			if (!string.IsNullOrEmpty(DisplayLabel) && roiParameter is IROILabel iROILabel2)
			{
				iROILabel2.Label = AppLocalizationService.GetLang(DisplayLabel);
			}
			roiParameter.DataChangedCommand = (ICommand)new DelegateCommand<IROI>((Action<IROI>)RoiParamChange);
		}
	}

	public void NormalizeRoiAddNumInit(IROI roi, string newroilabel)
	{
		try
		{
			if (NormalizeROIList.Count > RoiMaxNum)
			{
				NormalizeRemoveROI(roi);
				NormalizeEndDrawRoi();
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ParamSet"), AppLocalizationService.GetLang("Lang_RoiMaxNumLimit"), MessageIcons.Warning);
			}
			else
			{
				roi.DataChangedCommand = (ICommand)new DelegateCommand<IROI>((Action<IROI>)RoiParamChange);
				ReplaceOldROI(roi, newroilabel);
				SetValue();
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error("Message:" + ex.Message + "StackTrace:" + ex.StackTrace);
		}
	}

	protected virtual void ReplaceOldROI(IROI newROI, string newroilabel)
	{
		string text = SelectType;
		if (_roiTypeWithSelectMap.ContainsKey(GetRoiType((newROI as IROIType).RoiType).ToString()))
		{
			text = _roiTypeWithSelectMap[GetRoiType((newROI as IROIType).RoiType).ToString()];
		}
		if (!(text == "Mul"))
		{
			IROI iROI = NormalizeROIList.LastOrDefault((IROI p) => (p as IROILabel).Label == newroilabel && p.GetType() == newROI.GetType());
			int index = NormalizeROIList.IndexOf(iROI);
			(newROI as IROILabel).Label = newroilabel;
			if (newROI is ICaliper caliper && iROI is ICaliper caliper2)
			{
				caliper.CaliperNum = caliper2.CaliperNum;
			}
			NormalizeROIList.RemoveAt(index);
			NormalizeROIList.RemoveAt(NormalizeROIList.IndexOf(NormalizeROIList.Last()));
			int.TryParse(newroilabel, out var result);
			int.TryParse((NormalizeROIList[0] as IROILabel).Label, out var result2);
			if (result > result2)
			{
				NormalizeROIList.Add(newROI);
			}
			else
			{
				NormalizeROIList.Insert(0, newROI);
			}
		}
	}

	private void NormalizeRoiAdd(IROI roi)
	{
		try
		{
			if (NormalizeROIList.Count > RoiMaxNum)
			{
				NormalizeRemoveROI(roi);
				NormalizeEndDrawRoi();
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ParamSet"), AppLocalizationService.GetLang("Lang_RoiMaxNumLimit"), MessageIcons.Warning);
			}
			else
			{
				InitRoi(roi);
				ClearOldROI(roi);
				AddLabel(roi as IROILabel);
				SetValue();
				NormalizeEndDrawRoi();
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error("Message:" + ex.Message + "StackTrace:" + ex.StackTrace);
		}
	}

	protected virtual void ClearOldROI(IROI newROI)
	{
		string text = SelectType;
		if (_roiTypeWithSelectMap.ContainsKey(GetRoiType((newROI as IROIType).RoiType).ToString()))
		{
			text = _roiTypeWithSelectMap[GetRoiType((newROI as IROIType).RoiType).ToString()];
		}
		if (text == "Mul")
		{
			return;
		}
		IROI iROI = NormalizeROIList.LastOrDefault((IROI p) => p != newROI && p.GetType() == newROI.GetType());
		if (_roiTypeWithSelectMap.Count > 0)
		{
			NormalizeROIList.Remove(iROI);
		}
		else
		{
			int num = NormalizeROIList.IndexOf(newROI);
			if (num > 0)
			{
				NormalizeROIList.RemoveRange(0, NormalizeROIList.IndexOf(newROI));
			}
		}
		if (newROI is ICaliper caliper && iROI is ICaliper caliper2)
		{
			caliper.CaliperNum = caliper2.CaliperNum;
		}
	}

	protected virtual void AddROICommandExecute(object roi)
	{
		NormalizeRoiAdd(roi as IROI);
		SelectROI(roi as IROISelectable);
		NormalizeSelectRoiIcon();
	}

	protected virtual byte[] NormalizeReversParse(IList<IROI> roiInputs = null)
	{
		if (roiInputs == null)
		{
			roiInputs = NormalizeROIList;
		}
		using MemoryStream memoryStream = new MemoryStream();
		BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		foreach (IROI item in roiInputs.OfType<IROI>())
		{
			byte[] array;
			if (item is RectROIModel roi)
			{
				array = ((!(item is MatrixCircleROIModel roi2)) ? roi.GetNormalizeParseBytes() : roi2.GetNormalizeParseBytes());
			}
			else if (item is CircleROIModel roi3)
			{
				array = roi3.GetNormalizeParseBytes();
			}
			else if (item is OverSectorROIModel roi4)
			{
				array = roi4.GetNormalizeParseBytes();
			}
			else if (item is SectorROIModel roi5)
			{
				array = roi5.GetNormalizeParseBytes();
			}
			else if (item is PolygonROIModel roi6)
			{
				array = roi6.GetNormalizeParseBytes();
			}
			else if (item is CircleCaliperROIModel roi7)
			{
				array = roi7.GetNormalizeParseBytes();
			}
			else if (item is LineCaliperROIModel roi8)
			{
				array = roi8.GetNormalizeParseBytes();
			}
			else if (item is SectorCaliperROIModel roi9)
			{
				array = roi9.GetNormalizeParseBytes();
			}
			else if (item is PointROIModel roi10)
			{
				array = roi10.GetNormalizeParseBytes();
			}
			else if (item is LineROIModel roi11)
			{
				array = roi11.GetNormalizeParseBytes();
			}
			else if (item is CircleMesROIModel roi12)
			{
				array = roi12.GetNormalizeParseBytes();
			}
			else
			{
				array = ((!(item is CoordinateModel roi13)) ? new byte[0] : roi13.GetParseBytes());
			}
			if (array.Length != 0)
			{
				binaryWriter.Write(array);
			}
		}
		if (roiInputs.Count < 1)
		{
			if (SelectType == "Avoid")
			{
				binaryWriter.Write((byte)0);
				binaryWriter.Write((byte)0);
			}
			else
			{
				binaryWriter.Write((byte)1);
				binaryWriter.Write((byte)0);
			}
		}
		return memoryStream.ToArray();
	}

	void IROISelecter.DoRoiSelect(IROI roi)
	{
		StopDrawROI();
		if (roi == OldROI)
		{
			roi = NormalizeROIList.SingleOrDefault((IROI p) => p is IROISelectable iROISelectable && iROISelectable.IsSelected);
		}
		if (roi != null)
		{
			ClearOtherROIGroup();
		}
		CurROIData = roi;
		ClearSelected();
		if (roi != null)
		{
			SelectRoiIcon(roi);
		}
		else if (NormalizeROIList.Count < 1 && FullScreenEnable)
		{
			FullScreen_ModuleEnableA = true;
		}
	}

	void IROISelecter.DoRoiDeleted(IROI roi)
	{
		if (_CurROIData != null && _CurROIData == roi)
		{
			CurROIData = null;
		}
		SetValue();
		NormalizeSelectRoiIcon();
	}

	void IROISelecter.DoRoiPaste(IROI roi)
	{
		AddROICommandExecute(roi);
	}

	void IROISelecter.UpdateROI()
	{
		UpdateRoi();
	}

	protected virtual void UpdateRoi()
	{
		if (!CustomVisible || !_CanUpdateROI)
		{
			return;
		}
		GetStructBytes();
		foreach (RoiBaseParameter rOI in ROIList)
		{
			ROIType roiType = GetRoiType(rOI.RoiType);
			if (!RoiByteData.ContainsKey(roiType))
			{
				continue;
			}
			List<byte[]> list = RoiByteData[roiType];
			byte[] array = null;
			IPolygonROI pRoi;
			array = (((pRoi = rOI as IPolygonROI) == null) ? list.FirstOrDefault() : list.FirstOrDefault((byte[] p) => p.Length == pRoi.PolygonPoints.Count * 8 + 4));
			if (array != null)
			{
				list.Remove(array);
				switch (roiType)
				{
				case ROIType.SectorRing:
					ExternalCallHelper.BytesToStruct<ROISectorRing>(array, 0, array.Length).UpdateIROI(rOI);
					break;
				case ROIType.Box:
					ExternalCallHelper.BytesToStruct<ROIBox>(array, 0, array.Length).UpdateIROI(rOI as IRectROI);
					break;
				case ROIType.Polygon:
					GetPolygonStruct(array).UpdateIROI(rOI as IPolygonROI);
					break;
				case ROIType.Point:
					ExternalCallHelper.BytesToStruct<ROIPoint>(array, 0, array.Length).UpdateIROI(rOI as IPointROI);
					break;
				case ROIType.Line:
					ExternalCallHelper.BytesToStruct<ROILine>(array, 0, array.Length).UpdateIROI(rOI as ILineROI);
					break;
				case ROIType.CircleMes:
					ExternalCallHelper.BytesToStruct<ROICicleMes>(array, 0, array.Length).UpdateIROI(rOI as ICircleMesROI);
					break;
				case ROIType.LineCaliper:
					ExternalCallHelper.BytesToStruct<ROILineCaliper>(array, 0, array.Length).UpdateIROI(rOI as ILineCaliperROI);
					break;
				case ROIType.CircleCaliper:
					ExternalCallHelper.BytesToStruct<ROICircleCaliper>(array, 0, array.Length).UpdateIROI(rOI as ICircleCaliperROI);
					break;
				case ROIType.SectorCaliper:
					ExternalCallHelper.BytesToStruct<ROISectorCaliper>(array, 0, array.Length).UpdateIROI(rOI as ISectorCaliperROI);
					break;
				case ROIType.MatrixCircle:
					ExternalCallHelper.BytesToStruct<ROIMatrixCircle>(array, 0, array.Length).UpdateIROI(rOI as IMatrixCircleROI);
					break;
				}
			}
		}
	}

	bool IROISelecter.CanDeleted(IROI roi)
	{
		string text = SelectType;
		if (_roiTypeWithSelectMap.ContainsKey(GetRoiType((roi as IROIType).RoiType).ToString()))
		{
			text = _roiTypeWithSelectMap[GetRoiType((roi as IROIType).RoiType).ToString()];
		}
		return FullScreenEnable || !(text == "Single");
	}

	bool IROISelecter.CanCopy(IROI roi)
	{
		return CanCopy(roi);
	}

	protected virtual bool CanCopy(IROI roi)
	{
		string text = SelectType;
		if (_roiTypeWithSelectMap.ContainsKey(GetRoiType((roi as IROIType).RoiType).ToString()))
		{
			text = _roiTypeWithSelectMap[GetRoiType((roi as IROIType).RoiType).ToString()];
		}
		return !(text == "Single");
	}

	object IGroupRunParamWithMultiParams.GetAllData()
	{
		ROIDataStore rOIDataStore = new ROIDataStore();
		rOIDataStore.Data = ((IUserBytesData)ParamsSource)[ComName];
		InnerGetImageSize(out var imageWidth, out var imageHeight);
		rOIDataStore.ImageWidth = imageWidth;
		rOIDataStore.ImageHeight = imageHeight;
		if (IsDisplaySeq)
		{
			rOIDataStore.SeqStr = GetRoiSeqStr(ROIList);
		}
		return rOIDataStore;
	}

	void IGroupRunParamWithMultiParams.SetAllData(object data)
	{
		if (data is ROIDataStore rOIDataStore)
		{
			if (needSetImageSize)
			{
				InnerSetImageSize(rOIDataStore.ImageWidth, rOIDataStore.ImageHeight);
			}
			InnerSetValue(rOIDataStore.Data);
			if (IsDisplaySeq)
			{
				InnerSetRoiSeq(rOIDataStore.SeqStr);
			}
		}
	}

	void IGroupRunParamWithMultiParams.Clear()
	{
	}
}
