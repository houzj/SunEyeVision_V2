using System.Windows;
using Point=System.Windows.Point;
using Vector=System.Windows.Vector;
namespace SunEyeVision.UI.Services.PathCalculators;
// Recovered Connection.xaml.cs MouseMove / LineMove / ChangSite logic.
// Host adaptation: explicit pointer and endpoint rectangles; no event or document ownership.
internal sealed class RecoveredConnectionDrag
{
 readonly Rect sourceBounds,targetBounds;
 int moveStartPointIndex;
 public IReadOnlyList<Point> PathPoints {get;private set;}
 public int SegmentIndex=>moveStartPointIndex;
 public RecoveredConnectionDrag(IReadOnlyList<Point> points,int index,Rect source,Rect target){PathPoints=points.ToArray();moveStartPointIndex=index;source.Inflate(5,5);target.Inflate(5,5);sourceBounds=source;targetBounds=target;}
public void Move(Point position)
	{
		if (PathPoints.Count < 2)
		{
			return;
		}
		List<Point> list = new List<Point>();
		list.AddRange(PathPoints);
		
		Point point = list[moveStartPointIndex];
		Point point2 = list[moveStartPointIndex + 1];
		bool flag = point.Y == point2.Y;
		double num = 0.0;
		double num2 = 0.0;
		double num3 = 0.0;
		double num4 = 0.0;
		int num5 = 1;
		if (flag)
		{
			if (Math.Abs(position.Y - point.Y) <= 3.0)
			{
				return;
			}
			num2 = Math.Round(position.Y / 5.0) * 5.0;
			num5 = Math.Sign(num2 - point.Y);
			num3 = point.Y + (double)(num5 * 5);
			num4 = num2;
			if (num4 > 5000.0)
			{
				num4 = 5000.0;
			}
		}
		else
		{
			if (Math.Abs(position.X - point.X) <= 3.0)
			{
				return;
			}
			num = Math.Round(position.X / 5.0) * 5.0;
			num5 = Math.Sign(num - point.X);
			num3 = point.X + (double)(num5 * 5);
			num4 = num;
			if (num4 > 5000.0)
			{
				num4 = 5000.0;
			}
		}
		if (num4 < 0.0)
		{
			num4 = 0.0;
		}
		Rect rectWithMargin = sourceBounds;
		Rect rectWithMargin2 = targetBounds;
		List<Point> list2 = new List<Point>(list);
		int num6 = moveStartPointIndex;
		for (double num7 = num3; (num5 > 0) ? (num7 <= num4) : (num7 >= num4); num7 += (double)(num5 * 5))
		{
			point = list[moveStartPointIndex];
			point2 = list[moveStartPointIndex + 1];
			if (flag)
			{
				if (num7 > rectWithMargin.Y && num7 < rectWithMargin.Y + rectWithMargin.Height && !LineMove(list, rectWithMargin.X, rectWithMargin.Width, point.X, point2.X, point.Y, position.X, isY: false))
				{
					list = list2;
					moveStartPointIndex = num6;
					return;
				}
				if (num7 > rectWithMargin2.Y && num7 < rectWithMargin2.Y + rectWithMargin2.Height && !LineMove(list, rectWithMargin2.X, rectWithMargin2.Width, point.X, point2.X, point.Y, position.X, isY: false))
				{
					list = list2;
					moveStartPointIndex = num6;
					return;
				}
				list[moveStartPointIndex] = new Point(list[moveStartPointIndex].X, num7);
				list[moveStartPointIndex + 1] = new Point(list[moveStartPointIndex + 1].X, num7);
			}
			else
			{
				if (num7 > rectWithMargin.X && num7 < rectWithMargin.X + rectWithMargin.Width && !LineMove(list, rectWithMargin.Y, rectWithMargin.Height, point.Y, point2.Y, point.X, position.Y, isY: true))
				{
					list = list2;
					moveStartPointIndex = num6;
					return;
				}
				if (num7 > rectWithMargin2.X && num7 < rectWithMargin2.X + rectWithMargin2.Width && !LineMove(list, rectWithMargin2.Y, rectWithMargin2.Height, point.Y, point2.Y, point.X, position.Y, isY: true))
				{
					list = list2;
					moveStartPointIndex = num6;
					return;
				}
				list[moveStartPointIndex] = new Point(num7, list[moveStartPointIndex].Y);
				list[moveStartPointIndex + 1] = new Point(num7, list[moveStartPointIndex + 1].Y);
			}
			for (int i = 0; i < list.Count - 1; i++)
			{
				Point point3 = list[i];
				Point point4 = list[i + 1];
				if (point4 == point3)
				{
					if (i + 1 <= moveStartPointIndex)
					{
						moveStartPointIndex--;
					}
					list.RemoveAt(i + 1);
				}
			}
			for (int j = 1; j < list.Count - 1; j++)
			{
				Point point5 = list[j - 1];
				Point point6 = list[j];
				Point point7 = list[j + 1];
				Vector vector = new Vector(point5.X - point6.X, point5.Y - point6.Y);
				Vector vector2 = new Vector(point7.X - point6.X, point7.Y - point6.Y);
				vector.Normalize();
				vector2.Normalize();
				if (vector == vector2)
				{
					list = list2;
					moveStartPointIndex = num6;
					return;
				}
			}
			bool flag2;
			do
			{
				flag2 = false;
				for (int k = 0; k < list.Count - 2; k++)
				{
					Point point8 = list[k];
					Point point9 = list[k + 1];
					Point point10 = list[k + 2];
					if ((point8.X == point9.X && point8.X == point10.X) || (point8.Y == point9.Y && point8.Y == point10.Y))
					{
						if (k + 1 <= moveStartPointIndex)
						{
							moveStartPointIndex--;
						}
						list.RemoveAt(k + 1);
						flag2 = true;
						break;
					}
				}
			}
			while (flag2);
		}
		PathPoints = list;
	}

	private bool LineMove(List<Point> linePoints, double rectSourceStart, double rectSourceSpread, double sfird, double efird, double X, double Y, bool isY)
	{
		double[,] array = new double[4, 2]
		{
			{ rectSourceStart, 0.0 },
			{
				rectSourceStart + rectSourceSpread,
				0.0
			},
			{ sfird, 1.0 },
			{ efird, 1.0 }
		};
		int startIndexOfLi = 2;
		int endIndexOfLi = 3;
		for (int i = 0; i < array.GetLength(0) - 1; i++)
		{
			for (int j = 0; j < array.GetLength(0) - i - 1; j++)
			{
				if (array[j, 0] > array[j + 1, 0])
				{
					ChangSite(array, j, ref startIndexOfLi, ref endIndexOfLi);
				}
			}
		}
		if (array[1, 0] == array[2, 0] && array[1, 1] != array[0, 1])
		{
			ChangSite(array, 1, ref startIndexOfLi, ref endIndexOfLi);
		}
		if (array[1, 1] == array[2, 1])
		{
			if (array[0, 0] == array[1, 0])
			{
				ChangSite(array, 0, ref startIndexOfLi, ref endIndexOfLi);
			}
			if (array[2, 0] == array[3, 0])
			{
				ChangSite(array, 2, ref startIndexOfLi, ref endIndexOfLi);
			}
		}
		Dictionary<Point, bool> dictionary = new Dictionary<Point, bool>();
		double num = array[0, 1];
		bool flag = num == 0.0;
		bool flag2 = num == 1.0;
		for (int k = 1; k < 3; k++)
		{
			if (num == array[k, 1])
			{
				if (num == 0.0)
				{
					if (!flag)
					{
						flag = true;
						throw new Exception();
					}
					flag = false;
					if (flag2)
					{
						if (isY)
						{
							dictionary.Add(new Point(X, array[k, 0]), flag);
						}
						else
						{
							dictionary.Add(new Point(array[k, 0], X), flag);
						}
					}
				}
				else
				{
					if (!flag2)
					{
						flag2 = true;
						throw new Exception();
					}
					flag2 = false;
				}
			}
			else if (num == 0.0)
			{
				flag2 = !flag2;
			}
			else
			{
				flag = !flag;
				if (flag2)
				{
					if (isY)
					{
						dictionary.Add(new Point(X, array[k, 0]), flag);
					}
					else
					{
						dictionary.Add(new Point(array[k, 0], X), flag);
					}
				}
			}
			num = array[k, 1];
		}
		if (array[0, 1] == 0.0 && array[3, 1] == 0.0)
		{
			return false;
		}
		if (dictionary.Count > 0)
		{
			bool flag3 = false;
			Point key;
			if (dictionary.Count == 1)
			{
				if (array[0, 1] == 0.0)
				{
					if (Y <= array[2, 0])
					{
						return false;
					}
					if (array[2, 0] == array[3, 0])
					{
						return false;
					}
					if (array[1, 0] == sfird)
					{
						flag3 = true;
					}
				}
				else
				{
					if (Y >= array[1, 0])
					{
						return false;
					}
					if (array[1, 0] == array[0, 0])
					{
						return false;
					}
					if (array[2, 0] == sfird)
					{
						flag3 = true;
					}
				}
				key = dictionary.First().Key;
			}
			else
			{
				if (Y >= array[1, 0] && Y <= array[2, 0])
				{
					return false;
				}
				if (Y < array[1, 0])
				{
					if (array[3, 0] == sfird)
					{
						flag3 = true;
					}
					key = dictionary.First().Key;
				}
				else
				{
					if (array[0, 0] == sfird)
					{
						flag3 = true;
					}
					key = dictionary.Last().Key;
				}
			}
			foreach (KeyValuePair<Point, bool> item in dictionary)
			{
				linePoints.Insert(moveStartPointIndex + 1, key);
				linePoints.Insert(moveStartPointIndex + 2, key);
				if (flag3)
				{
					moveStartPointIndex += 2;
				}
			}
		}
		return true;
	}

	private void ChangSite(double[,] li, int j, ref int startIndexOfLi, ref int endIndexOfLi)
	{
		double num = li[j, 0];
		li[j, 0] = li[j + 1, 0];
		li[j + 1, 0] = num;
		double num2 = li[j, 1];
		li[j, 1] = li[j + 1, 1];
		li[j + 1, 1] = num2;
		if (j == startIndexOfLi)
		{
			startIndexOfLi = j + 1;
		}
		if (j + 1 == startIndexOfLi)
		{
			startIndexOfLi = j;
		}
		if (j == endIndexOfLi)
		{
			endIndexOfLi = j + 1;
		}
		if (j + 1 == endIndexOfLi)
		{
			endIndexOfLi = j;
		}
	}

	
}
