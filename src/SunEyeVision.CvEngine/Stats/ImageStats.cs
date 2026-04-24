using System;

namespace SunEyeVision.CvEngine;

/// <summary>
/// 图像统计结果 - 对应Halcon的intensity算子
/// </summary>
public readonly struct ImageStats
{
    /// <summary>
    /// 灰度直方图（256 bins）
    /// </summary>
    public double[] Histogram { get; init; }

    /// <summary>
    /// 各通道均值（BGR顺序）
    /// </summary>
    public double[] ChannelMeans { get; init; }

    /// <summary>
    /// 灰度均值（单通道时与ChannelMeans[0]相同）
    /// </summary>
    public double Mean { get; init; }

    /// <summary>
    /// 灰度标准差
    /// </summary>
    public double StdDev { get; init; }

    public ImageStats(double[] histogram, double[] channelMeans, double mean, double stdDev)
    {
        Histogram = histogram ?? Array.Empty<double>();
        ChannelMeans = channelMeans ?? Array.Empty<double>();
        Mean = mean;
        StdDev = stdDev;
    }

    public static ImageStats Empty => new(Array.Empty<double>(), Array.Empty<double>(), 0, 0);
}

/// <summary>
/// 面积和质心结果 - 对应Halcon的area_center算子
/// </summary>
public readonly struct AreaCenterResult
{
    /// <summary>
    /// 区域面积（白色像素数）
    /// </summary>
    public double Area { get; }

    /// <summary>
    /// 质心X坐标
    /// </summary>
    public double CenterX { get; }

    /// <summary>
    /// 质心Y坐标
    /// </summary>
    public double CenterY { get; }

    public AreaCenterResult(double area, double centerX, double centerY)
    {
        Area = area;
        CenterX = centerX;
        CenterY = centerY;
    }
}
