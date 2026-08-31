using System;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Tool.LineDetect.Models;
using SunEyeVision.Tool.LineDetect.Views;

namespace SunEyeVision.Tool.LineDetect
{
    /// <summary>
    /// 直线查找工具 - 引导式 2D Metrology 直线边缘检测
    /// </summary>
    /// <remarks>
    /// 算法流程（Halcon 2D Metrology）：
    ///   1. create_metrology_model → 创建计量模型
    ///   2. add_metrology_object_line_measure → 添加直线测量对象（自动生成测量带阵列）
    ///   3. apply_metrology_model → 执行测量（逐测量带取点 + 拟合）
    ///   4. get_metrology_object_result → 获取拟合直线参数 + RMSE
    /// </remarks>
    [Tool(
        id: "LineDetect",
        displayName: "直线查找",
        Description = "直线边缘检测工具（引导式 2D Metrology）",
        Icon = "📏",
        Category = "定位工具",
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    [JsonDerivedType(typeof(LineDetectParameters), "LineDetect")]
    public class LineDetectTool : IToolPlugin<LineDetectParameters, LineDetectResults>
    {
        private readonly ToolAttribute? _attribute;
        public LineDetectTool() => _attribute = GetType().GetCustomAttribute<ToolAttribute>();

        /// <summary>
        /// 执行工具
        /// </summary>
        public LineDetectResults Run(LineDetectParameters parameters)
        {
            var result = new LineDetectResults
            {
                ToolName = _attribute?.DisplayName ?? "直线查找",
                ToolId = _attribute?.Id ?? "LineDetect",
                Timestamp = DateTime.Now,
            };

            // ===== 参数校验（Fail-Fast）=====
            var lineLen = (parameters.LineEndPoint - parameters.LineStartPoint).Length;
            if (lineLen < 1e-6)
            {
                parameters.LogError("参数校验失败: 引导直线未设置或长度无效，请先绘制引导直线");
                result.SetError("参数校验失败: 引导直线未设置或长度无效，请先绘制引导直线");
                return result;
            }

            // ===== 获取输入图像 =====
            HImage? hImage;
            try
            {
                hImage = parameters.GetHalconImage();
            }
            catch (Exception ex)
            {
                parameters.LogError($"获取输入图像失败: {ex.Message}");
                result.SetError($"获取输入图像失败: {ex.Message}");
                return result;
            }

            if (hImage == null || !hImage.IsInitialized())
            {
                parameters.LogWarning("输入图像为空");
                result.SetError("输入图像为空");
                return result;
            }

            // ===== Halcon 2D Metrology 执行（直线）=====
            using var metrologyModel = new HMetrologyModel();
            metrologyModel.CreateMetrologyModel();

            try
            {
                HOperatorSet.GetImageSize(hImage, out var width, out var height);
                metrologyModel.SetMetrologyModelImageSize((int)width.L, (int)height.L);

                // 位置修正：引导直线按矩阵变换到当前图像坐标（卡尺作用到修正后位置）
                var startP = parameters.LineStartPoint;
                var endP = parameters.LineEndPoint;
                if (parameters.IsPositionCorrectionEnabled &&
                    parameters.PositionCorrectionBindingSource?.CurrentValue is AffineMatrix matrix)
                {
                    startP = matrix.TransformWpfPoint(startP);
                    endP = matrix.TransformWpfPoint(endP);
                }
                HTuple objectIndex = metrologyModel.AddMetrologyObjectLineMeasure(
                    startP.Y, startP.X, endP.Y, endP.X,
                    parameters.MeasureLength1, parameters.MeasureLength2,
                    parameters.Sigma, parameters.Threshold,
                    BuildGenParamName(parameters), BuildGenParamValue(parameters));

                // 执行测量（逐测量带取点 + 拟合）
                metrologyModel.ApplyMetrologyModel(hImage);

                // 获取拟合直线参数（all_param = [Row1, Col1, Row2, Col2, Nr, Nc, Dist]）
                var allParam = metrologyModel.GetMetrologyObjectResult(
                    objectIndex.I, "all", "result_type", "all_param");

                // ★ 修复：rms 不是 get_metrology_object_param 的合法 GenParamName（#1303），
                //   也不是 get_metrology_object_result 的合法 result_type（#1305）。
                //   官方 measure_stamping_part 示例：用 used_edges 取实际参与拟合的点，自算点到直线距离的均方根。
                var rms = ComputeRmsFromUsedEdges(metrologyModel, objectIndex.I, allParam);

                // 获取测量带数量（num_measures 是 get_metrology_object_param 的合法 GenParamName）
                var numMeasures = metrologyModel.GetMetrologyObjectParam(objectIndex.I, "num_measures");

                // ===== 结果填充 =====
                FillResult(result, allParam, rms, numMeasures, objectIndex, metrologyModel);

                // ===== 结果判断（对齐 VisionMaster：匹配点数/角度/拟合误差） =====
                var isOk = EvaluateResult(result, parameters);
                result.IsPass = isOk;
                result.FillDisplay(isOk, parameters);

                result.SetSuccess();
                parameters.LogInfo(
                    $"直线查找完成: 边缘点数={result.EdgePointCount}, RMSE={result.RMSE:F3}px, 判定={isOk}");
            }
            catch (Exception ex)
            {
                parameters.LogError($"直线查找处理异常: {ex.Message}", ex);
                result.SetError($"处理失败: {ex.Message}", ex);
            }

            return result;
        }

        /// <summary>
        /// 结果判断（匹配点数/角度/拟合误差，任一不满足判 NG）
        /// </summary>
        private static bool EvaluateResult(LineDetectResults result, LineDetectParameters parameters)
        {
            var cfg = parameters.ResultConfig;
            if (cfg.IsEdgePointCountEnabled &&
                (result.EdgePointCount < cfg.EdgePointCountMin || result.EdgePointCount > cfg.EdgePointCountMax))
                return false;
            if (cfg.IsAngleEnabled &&
                (result.Angle < cfg.AngleMin || result.Angle > cfg.AngleMax))
                return false;
            if (cfg.IsRMSEEnabled &&
                (result.RMSE < cfg.RMSEMin || result.RMSE > cfg.RMSEMax))
                return false;
            return true;
        }

        /// <summary>
        /// 构建 Halcon 通用参数名（产品决策：只支持数量主导）
        /// </summary>
        private HTuple BuildGenParamName(LineDetectParameters parameters)
        {
            return new HTuple(new[]
            {
                "num_measures",
                "measure_transition",
                "measure_select",
            });
        }

        /// <summary>
        /// 构建 Halcon 通用参数值（数量主导：num_measures 必须传 int，避免 HALCON #1210 类型错误）
        /// </summary>
        private HTuple BuildGenParamValue(LineDetectParameters parameters)
        {
            return new HTuple(new object[]
            {
                parameters.NumMeasures,             // int → num_measures
                TransitionToHalcon(parameters.Transition),
                SelectToHalcon(parameters.Select),
            });
        }

        /// <summary>
        /// 边缘极性转 Halcon 字符串
        /// </summary>
        private static string TransitionToHalcon(EdgeTransition transition)
        {
            return transition switch
            {
                EdgeTransition.All => "all",
                EdgeTransition.Positive => "positive",
                EdgeTransition.Negative => "negative",
                _ => "all",
            };
        }

        /// <summary>
        /// 选取模式转 Halcon 字符串
        /// </summary>
        private static string SelectToHalcon(EdgeSelect select)
        {
            return select switch
            {
                EdgeSelect.All => "all",
                EdgeSelect.First => "first",
                EdgeSelect.Last => "last",
                _ => "all",
            };
        }

        /// <summary>
        /// 填充结果（拟合直线/线段/测量点/中心/角度）
        /// </summary>
        private void FillResult(LineDetectResults result, HTuple allParam, HTuple rms, HTuple numMeasures,
            HTuple objectIndex, HMetrologyModel metrologyModel)
        {
            // RMSE 和边缘点数（HTuple 长度检查，避免空结果访问）
            result.RMSE = rms.Length > 0 ? rms[0].D : 0;
            result.EdgePointCount = numMeasures.Length > 0 ? numMeasures[0].I : 0;

            try
            {
                // all_param = [Row1, Col1, Row2, Col2, Nr, Nc, Dist]
                if (allParam.Length >= 4)
                {
                    var start = new Point(allParam[1].D, allParam[0].D);
                    var end = new Point(allParam[3].D, allParam[2].D);
                    result.LineStartPoint = start;
                    result.LineEndPoint = end;
                    result.CenterPoint = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
                    result.Line = new[] { Line2d.FromTwoPoints(start, end) };
                }

                // 轮廓点（每个卡尺提取的拟合点，用于拟合）
                result.MeasurementPoints = ExtractMeasurementPoints(objectIndex, metrologyModel);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"解析 Halcon 直线结果失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 提取拟合点（used_edges → 实际参与拟合的边缘点，每测量带一个）。
        /// 与官方 measure_stamping_part 示例一致；get_metrology_object_measures 的
        /// Row/Column 顺序未定义且无法归属到具体测量带，不适合作为拟合点显示。
        /// </summary>
        private Point2d[] ExtractMeasurementPoints(HTuple objectIndex, HMetrologyModel metrologyModel)
        {
            try
            {
                var usedRow = metrologyModel.GetMetrologyObjectResult(
                    objectIndex, new HTuple("all"), new HTuple("used_edges"), new HTuple("row"));
                var usedCol = metrologyModel.GetMetrologyObjectResult(
                    objectIndex, new HTuple("all"), new HTuple("used_edges"), new HTuple("column"));
                if (usedRow.Length == 0) return Array.Empty<Point2d>();
                var pts = new Point2d[usedRow.Length];
                for (int i = 0; i < usedRow.Length; i++)
                    pts[i] = new Point2d(usedCol[i].D, usedRow[i].D);
                return pts;
            }
            catch
            {
                return Array.Empty<Point2d>();
            }
        }

        /// <summary>
        /// 自算拟合残差 RMSE。
        /// HALCON 中 rms 既不是 get_metrology_object_param 的合法 GenParamName（#1303），
        /// 也不是 get_metrology_object_result 的合法 result_type（#1305），必须自行计算。
        /// 参考官方 measure_stamping_part 示例：用 used_edges 取实际参与拟合的边缘点，
        /// 计算每个点到拟合直线的距离（直线法向量点积），取均方根。
        /// </summary>
        /// <param name="metrologyModel">计量模型</param>
        /// <param name="objectIndex">测量对象索引</param>
        /// <param name="allParam">拟合参数 [Row1, Col1, Row2, Col2, Nr, Nc, Dist]</param>
        /// <returns>RMSE 值（无拟合点时为 0）</returns>
        private static HTuple ComputeRmsFromUsedEdges(HMetrologyModel metrologyModel, int objectIndex, HTuple allParam)
        {
            try
            {
                if (allParam.Length < 7)
                    return new HTuple(0.0);

                var idx = new HTuple(objectIndex);
                var usedRow = metrologyModel.GetMetrologyObjectResult(
                    idx, new HTuple("all"), new HTuple("used_edges"), new HTuple("row"));
                var usedCol = metrologyModel.GetMetrologyObjectResult(
                    idx, new HTuple("all"), new HTuple("used_edges"), new HTuple("column"));
                if (usedRow.Length == 0)
                    return new HTuple(0.0);

                var r1 = allParam[0].D;   // Row1（线上一点）
                var c1 = allParam[1].D;   // Col1
                var nr = allParam[4].D;   // 单位法向量分量 Nr
                var nc = allParam[5].D;   // 单位法向量分量 Nc

                double sumSq = 0;
                for (int i = 0; i < usedRow.Length; i++)
                {
                    // 点到直线距离 = |Nr·(r−Row1) + Nc·(c−Col1)|（(Row1,Col1) 在直线上）
                    var dist = nr * (usedRow[i].D - r1) + nc * (usedCol[i].D - c1);
                    sumSq += dist * dist;
                }
                return new HTuple(Math.Sqrt(sumSq / usedRow.Length));
            }
            catch
            {
                return new HTuple(0.0);
            }
        }

        /// <summary>
        /// 创建调试控件
        /// </summary>
        public System.Windows.FrameworkElement? CreateDebugControl(IToolPlugin tool)
        {
            return new LineDetectToolDebugControl();
        }

        /// <summary>
        /// 创建数据分析控件（暂无）
        /// </summary>
        public System.Windows.FrameworkElement? CreateDataAnalysisControl()
        {
            return null;
        }
    }
}
