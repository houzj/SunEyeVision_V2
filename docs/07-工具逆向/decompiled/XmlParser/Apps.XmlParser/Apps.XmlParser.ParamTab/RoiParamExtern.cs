using System.IO;
using Apps.Data;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public static class RoiParamExtern
{
	public const byte VERSION = 0;

	public static byte[] GetNormalizeParseBytes(this IRectROI roi)
	{
		byte value = 0;
		if (roi is IUroRectROI)
		{
			value = 1;
		}
		using MemoryStream memoryStream = new MemoryStream();
		BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write((byte)2);
		binaryWriter.Write(value);
		binaryWriter.Write((float)roi.CenterPoint.X);
		binaryWriter.Write((float)roi.CenterPoint.Y);
		binaryWriter.Write((float)roi.Width);
		binaryWriter.Write((float)roi.Height);
		binaryWriter.Write((float)roi.Angle);
		return memoryStream.ToArray();
	}

	public static byte[] GetNormalizeParseBytes(this IPolygonROI roi)
	{
		using MemoryStream memoryStream = new MemoryStream();
		BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write((byte)4);
		binaryWriter.Write((byte)0);
		binaryWriter.Write((uint)roi.PolygonPoints.Count);
		for (int i = 0; i < roi.PolygonPoints.Count; i++)
		{
			binaryWriter.Write((float)roi.PolygonPoints[i].X);
			binaryWriter.Write((float)roi.PolygonPoints[i].Y);
		}
		return memoryStream.ToArray();
	}

	public static byte[] GetNormalizeParseBytes(this ICircleROI roi)
	{
		using MemoryStream memoryStream = new MemoryStream();
		BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write((byte)3);
		binaryWriter.Write((byte)0);
		binaryWriter.Write((float)roi.CenterPoint.X);
		binaryWriter.Write((float)roi.CenterPoint.Y);
		binaryWriter.Write((float)roi.MinRadius);
		binaryWriter.Write((float)roi.Radius);
		binaryWriter.Write(0f);
		binaryWriter.Write(0f);
		return memoryStream.ToArray();
	}

	public static byte[] GetNormalizeParseBytes(this OverSectorROIModel roi)
	{
		using MemoryStream memoryStream = new MemoryStream();
		BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write((byte)3);
		binaryWriter.Write((byte)1);
		binaryWriter.Write((float)roi.CenterPoint.X);
		binaryWriter.Write((float)roi.CenterPoint.Y);
		binaryWriter.Write((float)roi.MinRadius);
		binaryWriter.Write((float)roi.Radius);
		binaryWriter.Write((float)roi.StartAngle * (float)(roi.IsClockwise ? 1 : (-1)));
		binaryWriter.Write((float)roi._AngleRange);
		return memoryStream.ToArray();
	}

	public static byte[] GetNormalizeParseBytes(this ISectorROI roi)
	{
		using MemoryStream memoryStream = new MemoryStream();
		BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write((byte)3);
		binaryWriter.Write((byte)0);
		binaryWriter.Write((float)roi.CenterPoint.X);
		binaryWriter.Write((float)roi.CenterPoint.Y);
		binaryWriter.Write((float)roi.MinRadius);
		binaryWriter.Write((float)roi.Radius);
		binaryWriter.Write((float)roi.StartAngle);
		binaryWriter.Write((float)roi.EndAngle);
		return memoryStream.ToArray();
	}

	public static byte[] GetNormalizeParseBytes(this ILineCaliperROI roi)
	{
		using MemoryStream memoryStream = new MemoryStream();
		BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write((byte)5);
		binaryWriter.Write((byte)0);
		binaryWriter.Write((float)roi.StartPoint.X);
		binaryWriter.Write((float)roi.StartPoint.Y);
		binaryWriter.Write((float)roi.EndPoint.X);
		binaryWriter.Write((float)roi.EndPoint.Y);
		binaryWriter.Write((float)roi.CaliperWidth);
		binaryWriter.Write((float)roi.CaliperHeight);
		binaryWriter.Write((float)roi.CaliperRotateAngle);
		binaryWriter.Write(roi.CaliperNum);
		return memoryStream.ToArray();
	}

	public static byte[] GetNormalizeParseBytes(this ICircleCaliperROI roi)
	{
		using MemoryStream memoryStream = new MemoryStream();
		BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write((byte)6);
		binaryWriter.Write((byte)0);
		binaryWriter.Write((float)roi.CenterPoint.X);
		binaryWriter.Write((float)roi.CenterPoint.Y);
		binaryWriter.Write((float)roi.Radius);
		binaryWriter.Write(0f);
		binaryWriter.Write(0f);
		binaryWriter.Write((float)roi.CaliperHeight);
		binaryWriter.Write((float)roi.CaliperWidth);
		binaryWriter.Write(roi.CaliperNum);
		return memoryStream.ToArray();
	}

	public static byte[] GetNormalizeParseBytes(this ISectorCaliperROI roi)
	{
		using MemoryStream memoryStream = new MemoryStream();
		BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write((byte)10);
		binaryWriter.Write((byte)0);
		binaryWriter.Write((float)roi.StartPoint.X);
		binaryWriter.Write((float)roi.StartPoint.Y);
		binaryWriter.Write((float)roi.EndPoint.X);
		binaryWriter.Write((float)roi.EndPoint.Y);
		binaryWriter.Write((float)roi.Radius);
		binaryWriter.Write((float)roi.CaliperHeight);
		binaryWriter.Write((float)roi.CaliperWidth);
		binaryWriter.Write(roi.CaliperNum);
		binaryWriter.Write(roi.IsLargeArc ? 1u : 0u);
		binaryWriter.Write(roi.IsClockwise ? 1u : 0u);
		return memoryStream.ToArray();
	}

	public static byte[] GetNormalizeParseBytes(this IMatrixCircleROI roi)
	{
		using MemoryStream memoryStream = new MemoryStream();
		BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write((byte)11);
		binaryWriter.Write((byte)0);
		binaryWriter.Write((float)roi.CenterPoint.X);
		binaryWriter.Write((float)roi.CenterPoint.Y);
		binaryWriter.Write((float)roi.Width);
		binaryWriter.Write((float)roi.Height);
		binaryWriter.Write((float)roi.Angle);
		binaryWriter.Write(roi.M);
		binaryWriter.Write(roi.N);
		binaryWriter.Write((float)roi.Radius);
		binaryWriter.Write((float)roi.MinRadius);
		return memoryStream.ToArray();
	}

	public static byte[] GetNormalizeParseBytes(this IPointROI roi)
	{
		using MemoryStream memoryStream = new MemoryStream();
		BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write((byte)7);
		binaryWriter.Write((byte)0);
		binaryWriter.Write((float)roi.CenterPoint.X);
		binaryWriter.Write((float)roi.CenterPoint.Y);
		return memoryStream.ToArray();
	}

	public static byte[] GetNormalizeParseBytes(this ILineROI roi)
	{
		using MemoryStream memoryStream = new MemoryStream();
		BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write((byte)8);
		binaryWriter.Write((byte)0);
		binaryWriter.Write((float)roi.StartPoint.X);
		binaryWriter.Write((float)roi.StartPoint.Y);
		binaryWriter.Write((float)roi.EndPoint.X);
		binaryWriter.Write((float)roi.EndPoint.Y);
		return memoryStream.ToArray();
	}

	public static byte[] GetNormalizeParseBytes(this ICircleMesROI roi)
	{
		using MemoryStream memoryStream = new MemoryStream();
		BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write((byte)9);
		binaryWriter.Write((byte)0);
		binaryWriter.Write((float)roi.CenterPoint.X);
		binaryWriter.Write((float)roi.CenterPoint.Y);
		binaryWriter.Write((float)roi.Radius);
		return memoryStream.ToArray();
	}

	public static byte[] GetParseBytes(this ICoordinateROI roi)
	{
		using MemoryStream memoryStream = new MemoryStream();
		BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write((byte)12);
		binaryWriter.Write((byte)0);
		binaryWriter.Write((float)roi.OrgPoint.X);
		binaryWriter.Write((float)roi.OrgPoint.Y);
		binaryWriter.Write((float)roi.Angle);
		return memoryStream.ToArray();
	}

	public static byte[] GetNormalizeParseBytes(this I3DBoxROI roi)
	{
		using MemoryStream memoryStream = new MemoryStream();
		BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write((byte)2);
		binaryWriter.Write((float)roi.CenterPointX);
		binaryWriter.Write((float)roi.CenterPointY);
		binaryWriter.Write((float)roi.CenterPointZ);
		binaryWriter.Write(roi.Width);
		binaryWriter.Write(roi.Length);
		binaryWriter.Write(roi.Height);
		binaryWriter.Write((float)roi.AngleX);
		binaryWriter.Write((float)roi.AngleY);
		binaryWriter.Write((float)roi.AngleZ);
		return memoryStream.ToArray();
	}
}
