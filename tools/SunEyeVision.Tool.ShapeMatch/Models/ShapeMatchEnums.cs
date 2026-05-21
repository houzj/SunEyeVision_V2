namespace SunEyeVision.Tool.ShapeMatch.Models;

/// <summary>极性匹配 — 对齐 Halcon Metric</summary>
public enum MetricMode
{
    UsePolarity = 0,           // 'use_polarity'
    IgnoreGlobalPolarity = 1,  // 'ignore_global_polarity'
    IgnoreLocalPolarity = 2    // 'ignore_local_polarity'
}

/// <summary>优化模式 — 对齐 Halcon Optimization</summary>
public enum OptimizationMode
{
    Auto = 0,                  // 'auto'
    None = 1,                  // 'none'
    PointReductionLow = 2,     // 'point_reduction_low'
    PointReductionMedium = 3,  // 'point_reduction_medium'
    PointReductionHigh = 4     // 'point_reduction_high'
}

/// <summary>亚像素模式 — 对齐 Halcon SubPixel</summary>
public enum SubPixelMode
{
    None = 0,                   // 'none'
    Interpolation = 1,          // 'interpolation'
    LeastSquares = 2,           // 'least_squares'
    LeastSquaresHigh = 3,       // 'least_squares_high'
    LeastSquaresVeryHigh = 4    // 'least_squares_very_high'
}
