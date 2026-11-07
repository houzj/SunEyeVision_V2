using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Xml;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Log;
using Hik.Xml;
using HikPcUI.ImageView;
using VM.Controls;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class PathDrawingGuide : NameValueItem
{
	public MyAlgorithmTabRoot SubAlgorithmTabRoot;

	public MyAlgorithmTabRoot SubAlgorithmTabRootA => SubAlgorithmTabRoot;

	public override BaseNode Init(XmlNode node)
	{
		return base.Init(node);
	}

	public override void InniterBusiness()
	{
		base.InniterBusiness();
		if (SubAlgorithmTabRoot != null)
		{
			if (SubAlgorithmTabRoot.Owner == null && GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
			{
				SubAlgorithmTabRoot.Owner = algorithmTabRoot.Owner;
				SubAlgorithmTabRoot.UserStringData = algorithmTabRoot.UserStringData;
			}
			SubAlgorithmTabRoot.DoIniter();
			SubAlgorithmTabRoot.GetItemsValueUnit();
		}
	}

	public static PathDrawAlgorithmDef.EDGEINSP_TRAJ_LIST ConvertToAlgoStruct(ObservableCollection<object> roiList)
	{
		List<object> list = roiList.Where((object p) => p is HikPolylineRoiParameter || p is HikCircleArcRoiParameter).ToList();
		PathDrawAlgorithmDef.EDGEINSP_TRAJ_LIST result = default;
		if (list == null || list.Count == 0)
		{
			return result;
		}
		result.tran_num = list.Count;
		result.tranj = new PathDrawAlgorithmDef.EDGEINSP_TRAJ[10];
		for (int num = 0; num < list.Count; num++)
		{
			result.tranj[num].traj_elem = new PathDrawAlgorithmDef.MVBEDGEINSP_TRAJ_GEO_ELEM[2048];
			if (list[num] is HikPolylineRoiParameter hikPolylineRoiParameter)
			{
				result.tranj[num].elem_num = hikPolylineRoiParameter.PathPoints.Count - 1;
				for (int num2 = 0; num2 < result.tranj[num].elem_num; num2++)
				{
					PolygonPoint polygonPoint = hikPolylineRoiParameter.PathPoints[num2];
					PolygonPoint polygonPoint2 = hikPolylineRoiParameter.PathPoints[num2 + 1];
					result.tranj[num].traj_elem[num2].elem_type = 1;
					result.tranj[num].traj_elem[num2].traj_union.traj_line.angle = (float)(Math.Atan2(polygonPoint2.Y - polygonPoint.Y, polygonPoint2.X - polygonPoint.X) * 180.0 / Math.PI);
					result.tranj[num].traj_elem[num2].traj_union.traj_line.point_start.x = polygonPoint.x;
					result.tranj[num].traj_elem[num2].traj_union.traj_line.point_start.y = polygonPoint.y;
					result.tranj[num].traj_elem[num2].traj_union.traj_line.point_end.x = polygonPoint2.x;
					result.tranj[num].traj_elem[num2].traj_union.traj_line.point_end.y = polygonPoint2.y;
				}
			}
			else if (list[num] is HikCircleArcRoiParameter hikCircleArcRoiParameter)
			{
				result.tranj[num].elem_num = 1;
				result.tranj[num].traj_elem[0].elem_type = 2;
				result.tranj[num].traj_elem[0].traj_union.traj_arc.center.x = (float)hikCircleArcRoiParameter.CenterX;
				result.tranj[num].traj_elem[0].traj_union.traj_arc.center.y = (float)hikCircleArcRoiParameter.CenterY;
				result.tranj[num].traj_elem[0].traj_union.traj_arc.radius = (float)hikCircleArcRoiParameter.Radius;
				result.tranj[num].traj_elem[0].traj_union.traj_arc.start_angle = (float)hikCircleArcRoiParameter.StartAngle;
				result.tranj[num].traj_elem[0].traj_union.traj_arc.angle_range = (float)hikCircleArcRoiParameter.AngleRange;
			}
		}
		return result;
	}

	public static bool CreateModel(IParamsConfig paramsConfig, ref PathGuidData model, bool popError = false)
	{
		try
		{
			if (model == null || model.CurImageData == null || model.ItemsData == null || model.ItemsData.Count == 0 || Enumerable.Count(model.ItemsData, (object p) => p is HikPolylineRoiParameter || p is HikCircleArcRoiParameter) == 0)
			{
				throw new Exception("Lang_ModelDataNotEnough");
			}
			SetModelParam(paramsConfig, model);
			SetModelTranj(paramsConfig, model);
			byte[] value = new byte[1];
			paramsConfig.TimeOut = 2147483647u;
			((IUserBytesData)paramsConfig)["CreateModel"] = value;
			paramsConfig.TimeOut = 3000u;
			GetModelParamsConfig(paramsConfig, model, model.ParamTab);
			GetModelOutline(paramsConfig, model);
			CreateModelImageData(model);
			return true;
		}
		catch (Exception ex)
		{
			if (popError)
			{
				int result = 0;
				string strMsg = (int.TryParse(ex.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex.Message);
				if (ex is VmException ex2)
				{
					strMsg = ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
				}
				HikMessageBox.Show(strMsg, MessageIcons.Error, isTranslate: true);
			}
			LogHelper.Error(ex.StackTrace);
			return false;
		}
		finally
		{
		}
	}

	public static void SetModelParam(IParamsConfig paramsConfig, PathGuidData model)
	{
		((IUserBytesData)paramsConfig)["ModelImageData"] = model.CurImageData?.ImageBuffer ?? new byte[1];
		((IUserStringData)paramsConfig)["ModelImageWidth"] = model.CurImageData?.Width.ToString() ?? "0";
		((IUserStringData)paramsConfig)["ModelImageHeight"] = model.CurImageData?.Height.ToString() ?? "0";
		((IUserBytesData)paramsConfig)["FixInit"] = model.FixInitBuffer ?? new byte[1];
	}

	public static void GetModelOutline(IParamsConfig paramsConfig, PathGuidData data)
	{
		if (paramsConfig == null || data == null)
		{
			return;
		}
		int num = Marshal.SizeOf(typeof(PathDrawAlgorithmDef.PATH_EDGE_POINT_RESULT));
		IntPtr intPtr = Marshal.AllocHGlobal(num);
		try
		{
			Marshal.Copy(((IUserBytesData)paramsConfig)["ModelProcessResult"], 0, intPtr, num);
			PathDrawAlgorithmDef.PATH_EDGE_POINT_RESULT pATH_EDGE_POINT_RESULT = (PathDrawAlgorithmDef.PATH_EDGE_POINT_RESULT)Marshal.PtrToStructure(intPtr, typeof(PathDrawAlgorithmDef.PATH_EDGE_POINT_RESULT));
			ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT> observableCollection = new ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>();
			for (int i = 0; i < pATH_EDGE_POINT_RESULT.edge_pts_num; i++)
			{
				observableCollection.Add(new MVBPATMATCH_EDGEPOINT_OUT
				{
					x = pATH_EDGE_POINT_RESULT.edge_pts[i].point.x,
					y = pATH_EDGE_POINT_RESULT.edge_pts[i].point.y,
					score_flag = pATH_EDGE_POINT_RESULT.edge_pts[i].status
				});
			}
			data.PointsData = observableCollection;
		}
		catch (Exception)
		{
		}
		finally
		{
			Marshal.FreeHGlobal(intPtr);
		}
	}

	public static void CreateModelImageData(PathGuidData model)
	{
		if (model == null)
		{
			return;
		}
		int num = 300;
		int num2 = 216;
		if (model.PointsData == null || model.CurImageData == null || model.PointsData.Count == 0)
		{
			return;
		}
		int minX = model.CurImageData.Width;
		int minY = model.CurImageData.Height;
		int maxX = 0;
		int maxY = 0;
		GetOutLineBorder(model, ref minX, ref minY, ref maxX, ref maxY);
		maxX = Math.Min(maxX, model.CurImageData.Width);
		maxY = Math.Min(maxY, model.CurImageData.Height);
		minX = Math.Max(minX, 0);
		minY = Math.Max(minY, 0);
		int num3 = maxX - minX;
		int num4 = maxY - minY;
		int num5 = 0;
		int num6 = 0;
		double num7 = (double)num4 / (double)num3;
		if (num7 <= 1.0 && (double)num * num7 < (double)num2)
		{
			num5 = num;
			num6 = (int)((double)num5 * num7);
		}
		else
		{
			num6 = num2;
			num5 = (int)((double)num6 / num7);
		}
		if (model.ModelImageData == null)
		{
			model.ModelImageData = new ImageData();
		}
		model.ModelImageData.ImageBuffer = new byte[num5 * num6 * 3];
		model.ModelImageData.Height = num6;
		model.ModelImageData.Width = num5;
		model.ModelImageData.PixelFormat = PixelFormats.Rgb24;
		for (int i = 0; i < num6; i++)
		{
			for (int j = 0; j < num5; j++)
			{
				int num8 = (int)((double)minX + (double)j / (double)num5 * (double)num3);
				int num9 = (int)((double)minY + (double)i / (double)num6 * (double)num4);
				model.ModelImageData.ImageBuffer[3 * (i * num5 + j)] = model.CurImageData.ImageBuffer[num9 * model.CurImageData.Width + num8];
				model.ModelImageData.ImageBuffer[3 * (i * num5 + j) + 1] = model.CurImageData.ImageBuffer[num9 * model.CurImageData.Width + num8];
				model.ModelImageData.ImageBuffer[3 * (i * num5 + j) + 2] = model.CurImageData.ImageBuffer[num9 * model.CurImageData.Width + num8];
			}
		}
		foreach (MVBPATMATCH_EDGEPOINT_OUT pointsDatum in model.PointsData)
		{
			if (pointsDatum.score_flag == 1)
			{
				continue;
			}
			int num10 = (int)((double)((pointsDatum.x - (float)minX) / (float)num3 * (float)num5) + 0.5);
			int num11 = (int)((double)((pointsDatum.y - (float)minY) / (float)num4 * (float)num6) + 0.5);
			try
			{
				int index = 3 * (num11 * num5 + num10);
				SetImagePointGreen(model.ModelImageData.ImageBuffer, index);
				index = 3 * ((num11 + 1) * num5 + num10);
				SetImagePointGreen(model.ModelImageData.ImageBuffer, index);
				if (num10 < num5 - 1)
				{
					index = 3 * (num11 * num5 + num10 + 1);
					SetImagePointGreen(model.ModelImageData.ImageBuffer, index);
					index = 3 * ((num11 + 1) * num5 + num10 + 1);
					SetImagePointGreen(model.ModelImageData.ImageBuffer, index);
				}
			}
			catch
			{
			}
		}
	}

	private static void SetImagePointGreen(byte[] imageBuffer, int index)
	{
		if (index + 2 < imageBuffer.Length)
		{
			imageBuffer[index] = 0;
			imageBuffer[index + 1] = byte.MaxValue;
			imageBuffer[index + 2] = 0;
		}
	}

	private static void GetOutLineBorder(PathGuidData model, ref int minX, ref int minY, ref int maxX, ref int maxY)
	{
		if (model == null)
		{
			return;
		}
		foreach (MVBPATMATCH_EDGEPOINT_OUT pointsDatum in model.PointsData)
		{
			if (pointsDatum.score_flag != 1)
			{
				if (pointsDatum.x < (float)minX)
				{
					minX = (int)pointsDatum.x;
				}
				if (pointsDatum.y < (float)minY)
				{
					minY = (int)pointsDatum.y;
				}
				if (pointsDatum.x > (float)maxX)
				{
					maxX = (int)Math.Min(pointsDatum.x + 1f, model.CurImageData.Width);
				}
				if (pointsDatum.y > (float)maxY)
				{
					maxY = (int)Math.Min(pointsDatum.y + 1f, model.CurImageData.Height);
				}
			}
		}
		foreach (object itemsDatum in model.ItemsData)
		{
			if (itemsDatum is HikPolylineRoiParameter hikPolylineRoiParameter)
			{
				foreach (PolygonPoint pathPoint in hikPolylineRoiParameter.PathPoints)
				{
					if (pathPoint.x < (float)minX)
					{
						minX = (int)pathPoint.x;
					}
					if (pathPoint.y < (float)minY)
					{
						minY = (int)pathPoint.y;
					}
					if (pathPoint.x > (float)maxX)
					{
						maxX = (int)Math.Min(pathPoint.x + 1f, model.CurImageData.Width);
					}
					if (pathPoint.y > (float)maxY)
					{
						maxY = (int)Math.Min(pathPoint.y + 1f, model.CurImageData.Height);
					}
				}
			}
			else if (itemsDatum is HikCircleArcRoiParameter { CenterPoint: var centerPoint } hikCircleArcRoiParameter)
			{
				if (centerPoint.X - hikCircleArcRoiParameter.Radius < (double)minX)
				{
					minX = (int)Math.Max(hikCircleArcRoiParameter.CenterPoint.X - hikCircleArcRoiParameter.Radius, 0.0);
				}
				if (hikCircleArcRoiParameter.CenterPoint.Y - hikCircleArcRoiParameter.Radius < (double)minY)
				{
					minY = (int)Math.Max(hikCircleArcRoiParameter.CenterPoint.Y - hikCircleArcRoiParameter.Radius, 0.0);
				}
				if (hikCircleArcRoiParameter.CenterPoint.X + hikCircleArcRoiParameter.Radius > (double)maxX)
				{
					maxX = (int)Math.Min(hikCircleArcRoiParameter.CenterPoint.X + hikCircleArcRoiParameter.Radius + 1.0, model.CurImageData.Width);
				}
				if (hikCircleArcRoiParameter.CenterPoint.Y + hikCircleArcRoiParameter.Radius > (double)maxY)
				{
					maxY = (int)Math.Min(hikCircleArcRoiParameter.CenterPoint.Y + hikCircleArcRoiParameter.Radius + 1.0, model.CurImageData.Height);
				}
			}
		}
	}

	public static void GetModel(IParamsConfig paramsConfig, PathGuidData model)
	{
		if (paramsConfig != null && model != null)
		{
			model.CurImageData = new ImageData();
			try
			{
				model.CurImageData.Width = Convert.ToInt32(((IUserStringData)paramsConfig)["ModelImageWidth"]);
				model.CurImageData.Height = Convert.ToInt32(((IUserStringData)paramsConfig)["ModelImageHeight"]);
				model.CurImageData.ImageBuffer = ((IUserBytesData)paramsConfig)["ModelImageData"];
				model.FixInitBuffer = ((IUserBytesData)paramsConfig)["FixInit"];
			}
			catch (Exception)
			{
				return;
			}
			model.CurImageData.PixelFormat = PixelFormats.Gray8;
			GetModelParamsConfig(paramsConfig, model, model.ParamTab);
			GetModelTranj(paramsConfig, model);
			GetModelOutline(paramsConfig, model);
			CreateModelImageData(model);
		}
	}

	public static void GetModelParamsConfig(IParamsConfig paramsConfig, PathGuidData model, Tab tab, bool isBack = false)
	{
		if (paramsConfig == null || tab == null)
		{
			return;
		}
		StringBuilder stringBuilder = new StringBuilder();
		int num = Enumerable.Count(model.ItemsData, (object p) => p is HikCircleArcRoiParameter || p is HikPolylineRoiParameter);
		for (int num2 = 0; num2 < num; num2++)
		{
			((IUserStringData)paramsConfig)["SelectedPathIndex"] = num2.ToString();
			if (!isBack)
			{
				GetModuleParam(tab);
			}
			foreach (object item in tab.Collection)
			{
				if (item is NameValueItem nameValueItem)
				{
					stringBuilder.Append(nameValueItem.Value).Append('-');
				}
			}
			stringBuilder.Remove(stringBuilder.Length - 1, 1);
			stringBuilder.Append(';');
		}
		model.ParamValues = stringBuilder.ToString();
	}

	public static void SetModelParams(IParamsConfig ownerModuleParam, PathGuidData modelData)
	{
		if (modelData.ParamTab == null)
		{
			return;
		}
		if (!string.IsNullOrEmpty(modelData.ParamValues))
		{
			string[] array = modelData.ParamValues.Split(';');
			for (int i = 0; i < array.Length; i++)
			{
				((IUserStringData)ownerModuleParam)["SelectedPathIndex"] = i.ToString();
				string[] array2 = null;
				if (array[i].Contains("--"))
				{
					List<int> list = new List<int>();
					array2 = array[i].Split('-');
					for (int j = 0; j < array2.Length; j++)
					{
						if (array2[j] == "")
						{
							if (j + 1 < array2.Length)
							{
								array2[j + 1] = "-" + array2[j + 1];
							}
							list.Add(j);
						}
					}
					ArrayList arrayList = new ArrayList(array2);
					foreach (int item in list)
					{
						arrayList.RemoveAt(item);
					}
					array2 = (string[])arrayList.ToArray(typeof(string));
				}
				else
				{
					array2 = array[i].Split('-');
				}
				if (array2.Length != modelData.ParamTab.Collection.Count)
				{
					continue;
				}
				foreach (BaseNode item2 in modelData.ParamTab.Collection)
				{
					if (item2 is NameValueItem { Name: not null } nameValueItem && ownerModuleParam != null)
					{
						((IUserStringData)ownerModuleParam)[nameValueItem.Name] = array2[modelData.ParamTab.Collection.IndexOf(nameValueItem)];
					}
				}
			}
			return;
		}
		((IUserStringData)ownerModuleParam)["SelectedPathIndex"] = "0";
		foreach (BaseNode item3 in modelData.ParamTab.Collection)
		{
			if (item3 is NameValueItem { Name: not null } nameValueItem2 && ownerModuleParam != null)
			{
				((IUserStringData)ownerModuleParam)[nameValueItem2.Name] = nameValueItem2.Value;
			}
		}
	}

	public static void GetModelTranj(IParamsConfig paramsConfig, PathGuidData model)
	{
		if (paramsConfig == null || model == null)
		{
			return;
		}
		int num = Marshal.SizeOf(typeof(PathDrawAlgorithmDef.EDGEINSP_TRAJ_LIST));
		IntPtr intPtr = Marshal.AllocHGlobal(num);
		try
		{
			Marshal.Copy(((IUserBytesData)paramsConfig)["ModelTranj"], 0, intPtr, num);
			PathDrawAlgorithmDef.EDGEINSP_TRAJ_LIST eDGEINSP_TRAJ_LIST = (PathDrawAlgorithmDef.EDGEINSP_TRAJ_LIST)Marshal.PtrToStructure(intPtr, typeof(PathDrawAlgorithmDef.EDGEINSP_TRAJ_LIST));
			model.ItemsData.Clear();
			for (int i = 0; i < eDGEINSP_TRAJ_LIST.tran_num; i++)
			{
				switch (eDGEINSP_TRAJ_LIST.tranj[i].traj_elem[0].elem_type)
				{
				case 1:
				{
					HikPolylineRoiParameter hikPolylineRoiParameter = new HikPolylineRoiParameter();
					for (int j = 0; j < eDGEINSP_TRAJ_LIST.tranj[i].elem_num; j++)
					{
						PolygonPoint polygonPoint = new PolygonPoint();
						polygonPoint.x = eDGEINSP_TRAJ_LIST.tranj[i].traj_elem[j].traj_union.traj_line.point_start.x;
						polygonPoint.y = eDGEINSP_TRAJ_LIST.tranj[i].traj_elem[j].traj_union.traj_line.point_start.y;
						hikPolylineRoiParameter.PathPoints.Add(polygonPoint);
						if (j == eDGEINSP_TRAJ_LIST.tranj[i].elem_num - 1)
						{
							PolygonPoint polygonPoint2 = new PolygonPoint();
							polygonPoint2.x = eDGEINSP_TRAJ_LIST.tranj[i].traj_elem[j].traj_union.traj_line.point_end.x;
							polygonPoint2.y = eDGEINSP_TRAJ_LIST.tranj[i].traj_elem[j].traj_union.traj_line.point_end.y;
							hikPolylineRoiParameter.PathPoints.Add(polygonPoint2);
						}
					}
					model.ItemsData.Add(hikPolylineRoiParameter);
					break;
				}
				case 2:
				{
					HikCircleArcRoiParameter item = new HikCircleArcRoiParameter
					{
						CenterX = eDGEINSP_TRAJ_LIST.tranj[i].traj_elem[0].traj_union.traj_arc.center.x,
						CenterY = eDGEINSP_TRAJ_LIST.tranj[i].traj_elem[0].traj_union.traj_arc.center.y,
						Radius = eDGEINSP_TRAJ_LIST.tranj[i].traj_elem[0].traj_union.traj_arc.radius,
						StartAngle = eDGEINSP_TRAJ_LIST.tranj[i].traj_elem[0].traj_union.traj_arc.start_angle,
						AngleRange = eDGEINSP_TRAJ_LIST.tranj[i].traj_elem[0].traj_union.traj_arc.angle_range
					};
					model.ItemsData.Add(item);
					break;
				}
				}
			}
		}
		catch (Exception)
		{
		}
		finally
		{
			Marshal.FreeHGlobal(intPtr);
		}
	}

	public static void SetModelTranj(IParamsConfig paramsConfig, PathGuidData model)
	{
		try
		{
			PathDrawAlgorithmDef.EDGEINSP_TRAJ_LIST structure = ConvertToAlgoStruct(model.ItemsData);
			int num = Marshal.SizeOf(structure);
			byte[] array = new byte[num];
			IntPtr intPtr = IntPtr.Zero;
			try
			{
				intPtr = Marshal.AllocHGlobal(num);
				Marshal.StructureToPtr(structure, intPtr, fDeleteOld: true);
				Marshal.Copy(intPtr, array, 0, num);
			}
			catch (Exception ex)
			{
				throw ex;
			}
			finally
			{
				Marshal.FreeHGlobal(intPtr);
			}
			((IUserBytesData)paramsConfig)["ModelTranj"] = array;
		}
		catch (Exception ex2)
		{
			throw ex2;
		}
	}

	public static void SetModelEdgePoint(IParamsConfig paramsConfig, PathGuidData model)
	{
		try
		{
			PathDrawAlgorithmDef.PATH_EDGE_POINT_RESULT structure = new PathDrawAlgorithmDef.PATH_EDGE_POINT_RESULT
			{
				edge_pts = new PathDrawAlgorithmDef.HKA_POINT_F_STATUS[10000],
				caliper_box = new PathDrawAlgorithmDef.MVBI_BOX_F[10000]
			};
			if (model.PointsData.Count > 0)
			{
				structure.edge_pts_num = model.PointsData.Count;
				structure.center_box_num = model.PointsData.Count;
				for (int i = 0; i < structure.edge_pts_num; i++)
				{
					structure.edge_pts[i].point.x = model.PointsData[i].x;
					structure.edge_pts[i].point.y = model.PointsData[i].y;
					structure.edge_pts[i].status = model.PointsData[i].score_flag;
				}
			}
			int num = Marshal.SizeOf(structure);
			byte[] array = new byte[num];
			IntPtr intPtr = IntPtr.Zero;
			try
			{
				intPtr = Marshal.AllocHGlobal(num);
				Marshal.StructureToPtr(structure, intPtr, fDeleteOld: true);
				Marshal.Copy(intPtr, array, 0, num);
			}
			catch (Exception ex)
			{
				throw ex;
			}
			finally
			{
				Marshal.FreeHGlobal(intPtr);
			}
			((IUserBytesData)paramsConfig)["ModelProcessResult"] = array;
		}
		catch (Exception ex2)
		{
			int result = 0;
			string strMsg = (int.TryParse(ex2.Message, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result) ? ErrorCodeInfo.GetErrorInfo(result) : ex2.Message);
			if (ex2 is VmException ex3)
			{
				strMsg = ErrorCodeInfo.GetErrorInfo(ex3.errorCode);
			}
			HikMessageBox.Show(strMsg, MessageIcons.Error, isTranslate: true);
		}
	}

	public static void GetModuleParam(Tab tab)
	{
		tab.GetItemsValueUnit();
	}
}
