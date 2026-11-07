using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Log;
using HikPcUI.ImageView;
using VM.Controls;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class Panel_EdgepatMatch : NameValueItem
{
	public AlgorithmTabRoot_Edgepat SubAlgorithmTabRoot;

	public AlgorithmTabRoot_Edgepat SubAlgorithmTabRootA => SubAlgorithmTabRoot;

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

	public static void GetModel(IParamsConfig paramsConfig, EdgepatMatchData model)
	{
		if (paramsConfig == null || model == null)
		{
			return;
		}
		model.CurImageData = new ImageData();
		try
		{
			string text = ((IUserStringData)paramsConfig)["ModelExist"];
			if (!text.Equals("True"))
			{
				return;
			}
			model.ModelData = ((IUserBytesData)paramsConfig)["ModelData"];
			model.CurImageData.Width = Convert.ToInt32(((IUserStringData)paramsConfig)["ModelImageWidth"]);
			model.CurImageData.Height = Convert.ToInt32(((IUserStringData)paramsConfig)["ModelImageHeight"]);
			model.CurImageData.ImageBuffer = ((IUserBytesData)paramsConfig)["ModelImageData"];
		}
		catch (Exception)
		{
			return;
		}
		model.CurImageData.PixelFormat = PixelFormats.Gray8;
		try
		{
			model.otherMask = ((IUserBytesData)paramsConfig)["ModelImageMask"];
		}
		catch (Exception)
		{
		}
		GetModelParam(paramsConfig, model);
		GetModelOutine(paramsConfig, model);
		CreateModelImageData(model);
	}

	public static string GetSdkImportModelPath(IParamsConfig paramsConfig)
	{
		string result = string.Empty;
		if (paramsConfig != null)
		{
			try
			{
				result = ((IUserStringData)paramsConfig)["SdkImportModelPath"];
			}
			catch (Exception)
			{
				result = string.Empty;
			}
		}
		return result;
	}

	public static void GetModelTranj(IParamsConfig paramsConfig, EdgepatMatchData model)
	{
		if (paramsConfig == null || model == null)
		{
			return;
		}
		int num = Marshal.SizeOf(typeof(EdgeflawAlgorithmDef.EDGEINSP_TRAJ_LIST));
		IntPtr intPtr = Marshal.AllocHGlobal(num);
		try
		{
			Marshal.Copy(((IUserBytesData)paramsConfig)["ModelTranj"], 0, intPtr, num);
			EdgeflawAlgorithmDef.EDGEINSP_TRAJ_LIST eDGEINSP_TRAJ_LIST = (EdgeflawAlgorithmDef.EDGEINSP_TRAJ_LIST)Marshal.PtrToStructure(intPtr, typeof(EdgeflawAlgorithmDef.EDGEINSP_TRAJ_LIST));
			object item;
			while ((item = model.ItemsData.FirstOrDefault((object p) => p is HikPolylineRoiParameter)) != null)
			{
				model.ItemsData.Remove(item);
			}
			for (int num2 = 0; num2 < eDGEINSP_TRAJ_LIST.tran_num; num2++)
			{
				HikPolylineRoiParameter hikPolylineRoiParameter = new HikPolylineRoiParameter();
				model.ItemsData.Add(hikPolylineRoiParameter);
				for (int num3 = 0; num3 < eDGEINSP_TRAJ_LIST.tranj[num2].elem_num; num3++)
				{
					PolygonPoint polygonPoint = new PolygonPoint();
					polygonPoint.x = eDGEINSP_TRAJ_LIST.tranj[num2].traj_elem[num3].traj_union.traj_line.point_start.x;
					polygonPoint.y = eDGEINSP_TRAJ_LIST.tranj[num2].traj_elem[num3].traj_union.traj_line.point_start.y;
					hikPolylineRoiParameter.PathPoints.Add(polygonPoint);
					if (num3 == eDGEINSP_TRAJ_LIST.tranj[num2].elem_num - 1)
					{
						PolygonPoint polygonPoint2 = new PolygonPoint();
						polygonPoint2.x = eDGEINSP_TRAJ_LIST.tranj[num2].traj_elem[num3].traj_union.traj_line.point_end.x;
						polygonPoint2.y = eDGEINSP_TRAJ_LIST.tranj[num2].traj_elem[num3].traj_union.traj_line.point_end.y;
						hikPolylineRoiParameter.PathPoints.Add(polygonPoint2);
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

	public static void GetModelParam(IParamsConfig paramsConfig, EdgepatMatchData model)
	{
		GetModelTranj(paramsConfig, model);
	}

	public static void CreateModelImageData(EdgepatMatchData model)
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
		if (index >= 0 && index + 2 < imageBuffer.Length)
		{
			imageBuffer[index] = 0;
			imageBuffer[index + 1] = byte.MaxValue;
			imageBuffer[index + 2] = 0;
		}
	}

	private static void GetOutLineBorder(EdgepatMatchData model, ref int minX, ref int minY, ref int maxX, ref int maxY)
	{
		if (model == null)
		{
			return;
		}
		foreach (MVBPATMATCH_EDGEPOINT_OUT pointsDatum in model.PointsData)
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
		foreach (object itemsDatum in model.ItemsData)
		{
			if (!(itemsDatum is HikPolylineRoiParameter hikPolylineRoiParameter))
			{
				continue;
			}
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
		foreach (object shapesDatum in model.ShapesData)
		{
			if (shapesDatum is HikRectangleShapeParameter { CenterPoint: var centerPoint } hikRectangleShapeParameter)
			{
				if (centerPoint.X - (double)hikRectangleShapeParameter.Width / 2.0 < (double)minX)
				{
					minX = (int)Math.Max(0.0, hikRectangleShapeParameter.CenterPoint.X - (double)hikRectangleShapeParameter.Width / 2.0);
				}
				if (hikRectangleShapeParameter.CenterPoint.Y - (double)hikRectangleShapeParameter.Height / 2.0 < (double)minY)
				{
					minY = (int)Math.Max(0.0, hikRectangleShapeParameter.CenterPoint.Y - (double)hikRectangleShapeParameter.Height / 2.0);
				}
				if (hikRectangleShapeParameter.CenterPoint.X + (double)hikRectangleShapeParameter.Width / 2.0 > (double)maxX)
				{
					maxX = (int)Math.Min(hikRectangleShapeParameter.CenterPoint.X + (double)hikRectangleShapeParameter.Width / 2.0, model.CurImageData.Width);
				}
				if (hikRectangleShapeParameter.CenterPoint.Y + (double)hikRectangleShapeParameter.Height / 2.0 > (double)maxY)
				{
					maxY = (int)Math.Min(hikRectangleShapeParameter.CenterPoint.Y + (double)hikRectangleShapeParameter.Height / 2.0, model.CurImageData.Height);
				}
			}
		}
	}

	public static bool CreateModel(IParamsConfig paramsConfig, ref EdgepatMatchData model, bool popError = false, bool isLoad = false)
	{
		try
		{
			SetModelParam(paramsConfig, model);
			EdgeflawAlgorithmDef.EDGEINSP_TRAJ_LIST structure = ConvertToAlgoStruct(model.ItemsData);
			int num = Marshal.SizeOf(structure);
			byte[] array = new byte[num];
			IntPtr intPtr = IntPtr.Zero;
			try
			{
				intPtr = Marshal.AllocHGlobal(num);
				Marshal.StructureToPtr(structure, intPtr, fDeleteOld: true);
				Marshal.Copy(intPtr, array, 0, num);
			}
			finally
			{
				Marshal.FreeHGlobal(intPtr);
			}
			((IUserBytesData)paramsConfig)["ModelTranj"] = array;
			byte[] value = new byte[1];
			paramsConfig.TimeOut = 2147483647u;
			((IUserBytesData)paramsConfig)["CreateModel"] = value;
			paramsConfig.TimeOut = 3000u;
			model.ModelData = ((IUserBytesData)paramsConfig)["ModelData"];
			if (isLoad)
			{
				((IUserBytesData)paramsConfig)["FixInit"] = model.FixInitBuffer ?? new byte[1];
			}
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
			model.ModelData = null;
			LogHelper.Error(ex.StackTrace);
			return false;
		}
		finally
		{
			GetModelOutine(paramsConfig, model);
			CreateModelImageData(model);
		}
	}

	private static void SetModelParam(IParamsConfig paramsConfig, EdgepatMatchData model)
	{
		SetImageAndMask(paramsConfig, model);
	}

	private static void SetImageAndMask(IParamsConfig paramsConfig, EdgepatMatchData model)
	{
		((IUserBytesData)paramsConfig)["ModelImageData"] = model.CurImageData?.ImageBuffer;
		((IUserStringData)paramsConfig)["ModelImageWidth"] = model.CurImageData?.Width.ToString();
		((IUserStringData)paramsConfig)["ModelImageHeight"] = model.CurImageData?.Height.ToString();
		if (model.otherMask == null)
		{
			((IUserStringData)paramsConfig)["ClearModelImageMask"] = "";
		}
		else
		{
			((IUserBytesData)paramsConfig)["ModelImageMask"] = model.otherMask;
		}
	}

	private static void ClearPoint(EdgepatMatchData model)
	{
		if (model != null)
		{
			model.ShapesData.Clear();
			model.PointsData.Clear();
		}
	}

	private static void GetModelOutine(IParamsConfig paramsConfig, EdgepatMatchData data)
	{
		if (paramsConfig == null || data == null)
		{
			return;
		}
		int num = Marshal.SizeOf(typeof(EdgeflawAlgorithmDef.EDGEINSP_EDGE_POINT_PRC_RESULT));
		IntPtr intPtr = Marshal.AllocHGlobal(num);
		try
		{
			Marshal.Copy(((IUserBytesData)paramsConfig)["ModelProcessResult"], 0, intPtr, num);
			EdgeflawAlgorithmDef.EDGEINSP_EDGE_POINT_PRC_RESULT eDGEINSP_EDGE_POINT_PRC_RESULT = (EdgeflawAlgorithmDef.EDGEINSP_EDGE_POINT_PRC_RESULT)Marshal.PtrToStructure(intPtr, typeof(EdgeflawAlgorithmDef.EDGEINSP_EDGE_POINT_PRC_RESULT));
			ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT> observableCollection = new ObservableCollection<MVBPATMATCH_EDGEPOINT_OUT>();
			ObservableCollection<object> observableCollection2 = new ObservableCollection<object>();
			for (int i = 0; i < eDGEINSP_EDGE_POINT_PRC_RESULT.edge_pts_num; i++)
			{
				observableCollection.Add(new MVBPATMATCH_EDGEPOINT_OUT
				{
					x = eDGEINSP_EDGE_POINT_PRC_RESULT.edge_pts[i].x,
					y = eDGEINSP_EDGE_POINT_PRC_RESULT.edge_pts[i].y,
					weight_flag = eDGEINSP_EDGE_POINT_PRC_RESULT.edge_pts_status[i],
					score_flag = eDGEINSP_EDGE_POINT_PRC_RESULT.edge_pts_status[i]
				});
			}
			for (int j = 0; j < eDGEINSP_EDGE_POINT_PRC_RESULT.edge1_pts_num; j++)
			{
				observableCollection.Add(new MVBPATMATCH_EDGEPOINT_OUT
				{
					x = eDGEINSP_EDGE_POINT_PRC_RESULT.edge1_pts[j].x,
					y = eDGEINSP_EDGE_POINT_PRC_RESULT.edge1_pts[j].y,
					weight_flag = eDGEINSP_EDGE_POINT_PRC_RESULT.edge1_pts_status[j],
					score_flag = eDGEINSP_EDGE_POINT_PRC_RESULT.edge1_pts_status[j]
				});
			}
			data.PointsData = observableCollection;
			for (int k = 0; k < eDGEINSP_EDGE_POINT_PRC_RESULT.center_box_num; k++)
			{
				HikRectangleShapeParameter hikRectangleShapeParameter = new HikRectangleShapeParameter();
				hikRectangleShapeParameter._CenterPoint.X = eDGEINSP_EDGE_POINT_PRC_RESULT.caliper_box[k].center_x;
				hikRectangleShapeParameter._CenterPoint.Y = eDGEINSP_EDGE_POINT_PRC_RESULT.caliper_box[k].center_y;
				hikRectangleShapeParameter.Width = eDGEINSP_EDGE_POINT_PRC_RESULT.caliper_box[k].width;
				hikRectangleShapeParameter.Height = eDGEINSP_EDGE_POINT_PRC_RESULT.caliper_box[k].height;
				hikRectangleShapeParameter.Angle = eDGEINSP_EDGE_POINT_PRC_RESULT.caliper_box[k].angle;
				observableCollection2.Add(hikRectangleShapeParameter);
			}
			data.ShapesData = observableCollection2;
		}
		catch (Exception)
		{
		}
		finally
		{
			Marshal.FreeHGlobal(intPtr);
		}
	}

	public static EdgeflawAlgorithmDef.EDGEINSP_TRAJ_LIST ConvertToAlgoStruct(ObservableCollection<object> shapes)
	{
		List<object> list = shapes.Where((object p) => p is HikPolylineRoiParameter).ToList();
		EdgeflawAlgorithmDef.EDGEINSP_TRAJ_LIST result = default;
		if (list == null)
		{
			return result;
		}
		result.tranj = new EdgeflawAlgorithmDef.EDGEINSP_TRAJ[10];
		result.tran_num = Enumerable.Count(list);
		for (int num = 0; num < result.tran_num; num++)
		{
			result.tranj[num].traj_elem = new EdgeflawAlgorithmDef.MVBEDGEINSP_TRAJ_GEO_ELEM[2048];
			if (list[num] is HikPolylineRoiParameter hikPolylineRoiParameter)
			{
				result.tranj[num].elem_num = hikPolylineRoiParameter.PathPoints.Count - 1;
				for (int num2 = 0; num2 < result.tranj[num].elem_num; num2++)
				{
					PolygonPoint polygonPoint = hikPolylineRoiParameter.PathPoints[num2];
					PolygonPoint polygonPoint2 = hikPolylineRoiParameter.PathPoints[num2 + 1];
					result.tranj[num].traj_elem[num2].elem_type = 1;
					result.tranj[num].traj_elem[num2].traj_union.traj_line.point_start.x = polygonPoint.x;
					result.tranj[num].traj_elem[num2].traj_union.traj_line.point_start.y = polygonPoint.y;
					result.tranj[num].traj_elem[num2].traj_union.traj_line.point_end.x = polygonPoint2.x;
					result.tranj[num].traj_elem[num2].traj_union.traj_line.point_end.y = polygonPoint2.y;
				}
			}
		}
		return result;
	}
}
