namespace SunEyeVision.Tool.ShapeMatch.Models;

/// <summary>亚像素模式 — 对齐 Halcon SubPixel</summary>
public enum SubPixelMode
{
    None = 0,                   // 'none'
    Interpolation = 1,          // 'interpolation'
    LeastSquares = 2,           // 'least_squares'
    LeastSquaresHigh = 3,       // 'least_squares_high'
    LeastSquaresVeryHigh = 4    // 'least_squares_very_high'
}
