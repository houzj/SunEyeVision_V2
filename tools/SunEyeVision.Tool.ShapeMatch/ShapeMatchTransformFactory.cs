using System;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Tool.ShapeMatch
{
    /// <summary>
    /// 轮廓匹配"匹配姿态 → 仿射矩阵"的唯一构造出口。
    /// </summary>
    /// <remarks>
    /// ★ 为什么需要这一层（对齐 Geometry 层 HalconArcAdapter 范式，rule-004 复用优先）：
    ///   find_scaled_shape_model 只输出 5 个姿态标量 (row, col, angle, scale, score)，
    ///   不直接输出矩阵。此前 Tool 内部手拼 WPF Matrix，导致"矩阵数值来源散落在
    ///   算法层之外 + 坐标系方向无统一注释"两类问题。
    ///   本工厂把所有"姿态 → AffineMatrix"的数学收敛到唯一出口，方向约定在注释锁定，
    ///   便于方向契约测试（+30° 样本）一次性定案。
    ///
    /// 坐标系约定（与 SDK AffineMatrix 一致）：
    ///   AffineMatrix 定义于 WPF 屏幕坐标系（X 向右、Y 向下），行向量乘法：
    ///     P'x = Px*M11 + Py*M21 + OffsetX
    ///     P'y = Px*M12 + Py*M22 + OffsetY
    ///   θ 正方向 = 视觉顺时针（WPF Matrix 惯例）。Halcon 侧如需消费请经
    ///   AffineMatrix.ToHalconArray() 适配，禁止在类型外自行补符号。
    /// </remarks>
    public static class ShapeMatchTransformFactory
    {
        /// <summary>
        /// 由 Halcon 匹配姿态构造"模型坐标 → 图像坐标"正向矩阵（渲染 / 包围盒用）。
        /// 平移量 = 模型参考点在图像中的位置（find 返回 row/col）。
        /// </summary>
        /// <param name="row">Halcon 返回的参考点行坐标（Y）</param>
        /// <param name="col">Halcon 返回的参考点列坐标（X）</param>
        /// <param name="angleRad">Halcon 返回的旋转角（弧度）</param>
        /// <param name="scale">Halcon 返回的缩放系数</param>
        /// <returns>AffineMatrix：M = scale·R(θ) + (col, row)</returns>
        public static AffineMatrix FromMatchPose(double row, double col, double angleRad, double scale)
        {
            // 等价于此前手拼 Matrix(s·cosA, s·sinA, -s·sinA, s·cosA, col, row)，
            // 收敛到 SDK 既有工厂，行为保持不变、方向决策集中在注释与测试。
            return AffineMatrix.FromCenterAngleScale(col, row, angleRad, scale);
        }

        /// <summary>
        /// 由匹配矩阵与模板参考点构造"参考图 → 测试图"位置修正矩阵。
        /// 平移量扣除模板参考点：下游把参考图上 ROI 坐标直接 TransformPoint 即得测试图位置。
        /// </summary>
        /// <param name="matchMatrix">FromMatchPose 产出的匹配矩阵</param>
        /// <param name="refX">模板参考点 X（模板训练时记录）</param>
        /// <param name="refY">模板参考点 Y</param>
        public static AffineMatrix ToPositionCorrection(AffineMatrix matchMatrix, double refX, double refY)
        {
            return new AffineMatrix(
                matchMatrix.M11, matchMatrix.M12,
                matchMatrix.M21, matchMatrix.M22,
                matchMatrix.OffsetX - refX,
                matchMatrix.OffsetY - refY);
        }
    }
}
