using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;

namespace SunEyeVision.CvEngine
{
    // ============================================================
    // 私有辅助 — Labels/CC 数据重建
    // 供 Connection.cs / SelectShape 内部使用
    // ============================================================
    public static partial class CvOp
    {
        #region 私有辅助 — Labels/CC 数据重建

        /// <summary>
        /// 从过滤后的索引列表重建 labels 图像（过滤 + 重编号 1..M）
        /// labels 第 0 行/0 列始终为背景(0)
        /// </summary>
        private static Mat BuildFilteredLabels(Mat sourceLabels, List<int> keepIndices, Size imageSize)
        {
            var result = new Mat(imageSize, MatType.CV_32SC1, Scalar.All(0));
            int safeHeight = Math.Min(imageSize.Height, sourceLabels.Rows);
            int safeWidth = Math.Min(imageSize.Width, sourceLabels.Cols);

            unsafe
            {
                int* srcPtr = (int*)sourceLabels.Data.ToPointer();
                int* dstPtr = (int*)result.Data.ToPointer();
                int srcStep = sourceLabels.Cols;
                int dstStep = result.Cols;

                // 构建旧标签→新标签的映射（1-based: keepIndices[i] → (i+1)）
                var labelMap = new Dictionary<int, int>(keepIndices.Count);
                for (int i = 0; i < keepIndices.Count; i++)
                    labelMap[keepIndices[i]] = i + 1;

                for (int y = 0; y < safeHeight; y++)
                {
                    int srcRow = y * srcStep;
                    int dstRow = y * dstStep;
                    for (int x = 0; x < safeWidth; x++)
                    {
                        int oldLabel = srcPtr[srcRow + x];
                        if (oldLabel > 0 && labelMap.TryGetValue(oldLabel, out int newLabel))
                            dstPtr[dstRow + x] = newLabel;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 从 CC 统计矩阵中提取指定行（用于 SelectShape 过滤后重建 stats/centroids）
        /// </summary>
        private static Mat FilterCCRows(Mat source, List<int> keepIndices, int cols, MatType dtype)
        {
            int M = keepIndices.Count;
            var result = new Mat(M + 1, cols, dtype); // +1 行给背景占位 (row 0)
            for (int i = 0; i < M; i++)
                source.Row(keepIndices[i]).CopyTo(result.Row(i + 1));
            return result;
        }

        #endregion
    }
}
