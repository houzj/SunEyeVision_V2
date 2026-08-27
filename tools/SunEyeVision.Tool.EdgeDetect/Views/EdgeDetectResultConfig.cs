namespace SunEyeVision.Tool.EdgeDetect.Views
{
    /// <summary>
    /// 边缘检测结果判断配置（对齐 BlobAnalysis 结果判断模式）
    /// </summary>
    public class EdgeDetectResultConfig
    {
        /// <summary>启用拟合残差判断</summary>
        public bool IsRMSEEnabled { get; set; }

        /// <summary>拟合残差范围</summary>
        public double RMSEMin { get; set; } = 0;

        /// <summary>拟合残差范围</summary>
        public double RMSEMax { get; set; } = 1.0;

        /// <summary>启用边缘点数判断</summary>
        public bool IsEdgePointCountEnabled { get; set; }

        /// <summary>边缘点数范围</summary>
        public int EdgePointCountMin { get; set; } = 5;

        /// <summary>边缘点数范围</summary>
        public int EdgePointCountMax { get; set; } = 200;
    }
}
