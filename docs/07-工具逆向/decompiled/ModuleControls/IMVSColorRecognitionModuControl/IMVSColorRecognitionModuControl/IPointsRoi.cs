using System.Collections.Generic;
using System.Windows;

namespace IMVSColorRecognitionModuControl;

internal interface IPointsRoi
{
	List<Point> GetPoints();

	void ResetByPoints(List<Point> points);
}
