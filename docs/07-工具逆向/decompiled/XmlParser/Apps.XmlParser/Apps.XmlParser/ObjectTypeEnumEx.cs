using System.ComponentModel.DataAnnotations;

namespace Apps.XmlParser;

public enum ObjectTypeEnumEx
{
	[Display(Name = "Lang_Point3D")]
	point3d,
	[Display(Name = "Lang_Line3D")]
	line3d,
	[Display(Name = "PointCloud")]
	PointCloud3DImage,
	[Display(Name = "Lang_LineChart")]
	linechart,
	[Display(Name = "Polyline")]
	polyline
}
