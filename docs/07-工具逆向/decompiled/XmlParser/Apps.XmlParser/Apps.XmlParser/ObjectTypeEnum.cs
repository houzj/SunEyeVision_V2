using System.ComponentModel.DataAnnotations;

namespace Apps.XmlParser;

public enum ObjectTypeEnum
{
	[Display(Name = "OutputImage")]
	image,
	[Display(Name = "Circle")]
	circle,
	[Display(Name = "Point")]
	point,
	[Display(Name = "Lang_PointSet")]
	micropoint,
	[Display(Name = "Rect")]
	rect,
	[Display(Name = "直线")]
	line,
	[Display(Name = "轮廓点")]
	outlinepoint,
	[Display(Name = "文本")]
	text,
	[Display(Name = "Angle")]
	LineAngle,
	[Display(Name = "coordinate")]
	coordinate,
	[Display(Name = "Seg_image")]
	pixelinfoimage,
	[Display(Name = "polygon")]
	polygon,
	[Display(Name = "annularsector")]
	annularsector,
	[Display(Name = "CalibPath")]
	path,
	[Display(Name = "Ellipse")]
	ellipse
}
