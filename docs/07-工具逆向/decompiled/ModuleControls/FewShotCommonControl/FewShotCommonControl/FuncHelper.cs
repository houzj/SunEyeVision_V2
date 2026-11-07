using System.IO;
using HikPcUI.ImageView;
using VMControls.RenderInterface;

namespace FewShotCommonControl;

public static class FuncHelper
{
	public const byte VERSION = 0;

	public static byte[] GetRoiBytes(this HikCoverRoiParameter roi)
	{
		using MemoryStream memoryStream = new MemoryStream();
		BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write((byte)2);
		binaryWriter.Write((byte)0);
		binaryWriter.Write((float)roi.CenterPoint.X);
		binaryWriter.Write((float)roi.CenterPoint.Y);
		binaryWriter.Write((float)roi.Width);
		binaryWriter.Write((float)roi.Height);
		binaryWriter.Write((float)roi.Angle);
		if (string.IsNullOrEmpty(roi.Lable))
		{
			roi.Lable = "";
		}
		binaryWriter.Write(roi.Lable);
		binaryWriter.Write(roi.RoiColor);
		return memoryStream.ToArray();
	}

	public static byte[] GetRoiBytes(this HikPolygonRoiParameter roi)
	{
		using MemoryStream memoryStream = new MemoryStream();
		BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write((byte)4);
		binaryWriter.Write((byte)0);
		binaryWriter.Write((uint)((IPolygonROI)roi).PolygonPoints.Count);
		for (int i = 0; i < ((IPolygonROI)roi).PolygonPoints.Count; i++)
		{
			binaryWriter.Write((float)((IPolygonROI)roi).PolygonPoints[i].X);
			binaryWriter.Write((float)((IPolygonROI)roi).PolygonPoints[i].Y);
		}
		if (string.IsNullOrEmpty(roi.Lable))
		{
			roi.Lable = "";
		}
		binaryWriter.Write(roi.Lable);
		binaryWriter.Write(roi.RoiColor);
		return memoryStream.ToArray();
	}
}
