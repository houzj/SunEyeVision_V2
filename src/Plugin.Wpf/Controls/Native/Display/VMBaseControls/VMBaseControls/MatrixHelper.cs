using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;

public class MatrixHelper
{
	public static double CalculateDeterminant(double[,] matrix)
	{
		int length = matrix.GetLength(0);
		double num = 0.0;
		if (length == 2)
		{
			num = matrix[0, 0] * matrix[1, 1] - matrix[0, 1] * matrix[1, 0];
		}
		else
		{
			for (int i = 0; i < length; i++)
			{
				double[,] array = new double[length - 1, length - 1];
				for (int j = 1; j < length; j++)
				{
					int k = 0;
					int num2 = 0;
					for (; k < length; k++)
					{
						if (k != i)
						{
							array[j - 1, num2] = matrix[j, k];
							num2++;
						}
					}
				}
				num += Math.Pow(-1.0, i) * matrix[0, i] * CalculateDeterminant(array);
			}
		}
		return num;
	}

	public static double[,] CalculateCofactors(double[,] matrix)
	{
		int length = matrix.GetLength(0);
		int length2 = matrix.GetLength(1);
		double[,] array = new double[length, length2];
		for (int i = 0; i < length; i++)
		{
			for (int j = 0; j < length2; j++)
			{
				double[,] subMatrix = GetSubMatrix(matrix, i, j);
				double num = Math.Pow(-1.0, i + j) * CalculateDeterminant(subMatrix);
				array[i, j] = num;
			}
		}
		return Transposs(array);
	}

	public static double[,] GetSubMatrix(double[,] matrix, int excludeRow, int excludeCol)
	{
		int length = matrix.GetLength(0);
		int length2 = matrix.GetLength(1);
		double[,] array = new double[length - 1, length2 - 1];
		int num = 0;
		for (int i = 0; i < length; i++)
		{
			if (i == excludeRow)
			{
				continue;
			}
			int num2 = 0;
			for (int j = 0; j < length2; j++)
			{
				if (j != excludeCol)
				{
					array[num, num2] = matrix[i, j];
					num2++;
				}
			}
			num++;
		}
		return array;
	}

	public static double[,] Transposs(double[,] matrix)
	{
		double[,] array = new double[matrix.GetLength(0), matrix.GetLength(1)];
		for (int i = 0; i < matrix.GetLength(0); i++)
		{
			for (int j = 0; j < matrix.GetLength(1); j++)
			{
				array[i, j] = matrix[j, i];
			}
		}
		return array;
	}

	public static double[,] Divide(double[,] matrix, double num)
	{
		int length = matrix.GetLength(0);
		int length2 = matrix.GetLength(1);
		for (int i = 0; i < length; i++)
		{
			for (int j = 0; j < length2; j++)
			{
				matrix[i, j] /= num;
			}
		}
		return matrix;
	}

	public static double[,] Inverse(double[,] matrix)
	{
		double num = CalculateDeterminant(matrix);
		double[,] matrix2 = CalculateCofactors(matrix);
		return Divide(matrix2, num);
	}
}
