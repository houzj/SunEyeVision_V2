namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.ParameterSchema.ParamTab;

public enum RoiType
{
	Default,
	Box,
	DoubleBox,
	QuadraBox,
	SectorRing,
	SectorRing2,
	Sector,
	Polygon,
	FullScreen,
	Point,
	Line,
	CircleMes,
	LineCaliper,
	CircleCaliper,
	SectorCaliper,
	LineBox,
	SeqBox,
	UroBox,
	OverSector,
	MatrixCircle,
	Coordinate,
	Box3D,
	PolygonCaliper
}
public enum ROIType : byte
{
	Avoid = 0,
	FullScreen = 1,
	Box = 2,
	SectorRing = 3,
	Polygon = 4,
	LineCaliper = 5,
	CircleCaliper = 6,
	Point = 7,
	Line = 8,
	CircleMes = 9,
	SectorCaliper = 10,
	MatrixCircle = 11,
	Coordinate = 12,
	PolygonCaliper = 13,
	FullBox = 14,
	Default = byte.MaxValue
}
