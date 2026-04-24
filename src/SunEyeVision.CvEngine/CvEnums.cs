namespace SunEyeVision.CvEngine
{
    /// <summary>
    /// 阈值类型枚举 - 对应Halcon阈值算子
    /// </summary>
    public enum CvThresholdType
    {
        /// <summary>二值化: 大于阈值为maxValue，其余为0</summary>
        Binary = 0,

        /// <summary>反向二值化: 大于阈值为0，其余为maxValue</summary>
        BinaryInv = 1,

        /// <summary>截断: 大于阈值为阈值，其余保持原值</summary>
        Trunc = 2,

        /// <summary>零值化: 大于阈值保持原值，其余为0</summary>
        ToZero = 3,

        /// <summary>反向零值化: 大于阈值为0，其余保持原值</summary>
        ToZeroInv = 4
    }

    /// <summary>
    /// 自适应阈值方法枚举
    /// </summary>
    public enum CvAdaptiveMethod
    {
        /// <summary>均值法</summary>
        Mean = 0,

        /// <summary>高斯法</summary>
        Gaussian = 1
    }

    /// <summary>
    /// 连通性类型枚举 - 对应Halcon connection算子
    /// </summary>
    public enum CvConnectivity
    {
        /// <summary>4连通（十字形）</summary>
        Connect4 = 0,

        /// <summary>8连通（方形）</summary>
        Connect8 = 1
    }

    /// <summary>
    /// 形状特征类型枚举 - 对应Halcon select_shape算子
    /// </summary>
    public enum CvShapeFeature
    {
        /// <summary>面积</summary>
        Area,

        /// <summary>圆度</summary>
        Circularity,

        /// <summary>矩形度</summary>
        Rectangularity,

        /// <summary>外接矩形宽度</summary>
        BoundingBoxWidth,

        /// <summary>外接矩形高度</summary>
        BoundingBoxHeight
    }
}
