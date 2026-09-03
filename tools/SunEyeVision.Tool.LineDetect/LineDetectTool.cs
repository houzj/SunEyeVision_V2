using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
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

            // ===== 诊断日志：执行参数快照（定位拟合失败第一步：确认实际传给 Halcon 的参数） =====
            parameters.LogInfo(
                $"[Diag] 直线查找参数: 引导线=({parameters.LineStartPoint.X:F1},{parameters.LineStartPoint.Y:F1})→" +
                $"({parameters.LineEndPoint.X:F1},{parameters.LineEndPoint.Y:F1}) len={lineLen:F1}px | " +
                $"带L1(宽)={parameters.MeasureLength1:F2} L2(长)={parameters.MeasureLength2:F2} N={parameters.NumMeasures} | " +
                $"Sigma={parameters.Sigma:F2} Threshold={parameters.Threshold:F1} | " +
                $"极性={TransitionToHalcon(parameters.Transition)}({parameters.Transition}) 选取={SelectToHalcon(parameters.Select)} | " +
                $"min_score={parameters.MinScore:F2} distance_threshold={parameters.DistanceThreshold:F2} | " +
                $"位置修正={(parameters.IsPositionCorrectionEnabled ? "启用" : "未启用")}");

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
                parameters.LogInfo($"[Diag] 图像尺寸: {width.L} x {height.L}");

                // 位置修正：引导直线按矩阵变换到当前图像坐标（卡尺作用到修正后位置）
                var startP = parameters.LineStartPoint;
                var endP = parameters.LineEndPoint;
                if (parameters.IsPositionCorrectionEnabled &&
                    parameters.PositionCorrectionBindingSource?.CurrentValue is AffineMatrix matrix)
                {
                    startP = matrix.TransformWpfPoint(startP);
                    endP = matrix.TransformWpfPoint(endP);
                    parameters.LogInfo(
                        $"[Diag] 位置修正已应用: 引导线 ({parameters.LineStartPoint.X:F1},{parameters.LineStartPoint.Y:F1})→" +
                        $"({parameters.LineEndPoint.X:F1},{parameters.LineEndPoint.Y:F1}) " +
                        $"→ 修正后 ({startP.X:F1},{startP.Y:F1})→({endP.X:F1},{endP.Y:F1})");
                }
                HTuple objectIndex = metrologyModel.AddMetrologyObjectLineMeasure(
                    startP.Y, startP.X, endP.Y, endP.X,
                    parameters.MeasureLength1, parameters.MeasureLength2,
                    parameters.Sigma, parameters.Threshold,
                    BuildGenParamName(parameters), BuildGenParamValue(parameters));
                parameters.LogInfo($"[Diag] 已添加测量对象 objectIndex={objectIndex.I}");

                // 执行测量（逐测量带取点 + 拟合）
                metrologyModel.ApplyMetrologyModel(hImage);
                parameters.LogInfo("[Diag] ApplyMetrologyModel 执行完成（无异常）");

                // 诊断日志：按极性检测到的全部边缘点数（= DiagnosticPoints 来源，不等同于拟合点数）
                try
                {
                    var diagContour = metrologyModel.GetMetrologyObjectMeasures(
                        objectIndex.I, TransitionToHalcon(parameters.Transition), out var diagRow, out var diagCol);
                    diagContour?.Dispose();
                    parameters.LogInfo(
                        $"[Diag] 检测点(GetMetrologyObjectMeasures)={diagRow.Length} 个 " +
                        $"| 极性={TransitionToHalcon(parameters.Transition)} | 带内0点→说明极性反/阈值过高/带未覆盖边缘");
                }
                catch (Exception diagEx)
                {
                    parameters.LogWarning($"[Diag] 查询检测点失败: {diagEx.Message}");
                }

                // ===== 结果填充 =====
                // 有启用的屏蔽区 → 屏蔽区点集过滤 + 全点鲁棒拟合（屏蔽区内的点不参与拟合，VisionMaster 语义）
                // 无屏蔽区 → 沿用 HALCON 引擎拟合（快路径，行为与旧版完全一致）
                bool hasEnabledMask = parameters.GetRoisByType(RoiType.Mask).Any();
                if (hasEnabledMask)
                {
                    FillResultWithMask(result, parameters, objectIndex, metrologyModel,
                        TransitionToHalcon(parameters.Transition));
                }
                else
                {
                    // 获取拟合直线参数（all_param = [Row1, Col1, Row2, Col2, Nr, Nc, Dist]）
                    var allParam = metrologyModel.GetMetrologyObjectResult(
                        objectIndex.I, "all", "result_type", "all_param");
                    if (allParam.Length < 4)
                    {
                        parameters.LogWarning(
                            $"[Diag] 拟合无有效输出: all_param 长度={allParam.Length}（<4，无拟合直线）。" +
                            "可能原因: 检测点数不足 / min_score 过高 / distance_threshold 过小 / 点不共线");
                    }
                    else
                    {
                        parameters.LogInfo(
                            $"[Diag] 拟合直线 all_param 长度={allParam.Length}: " +
                            $"Row1={allParam[0].D:F2} Col1={allParam[1].D:F2} Row2={allParam[2].D:F2} Col2={allParam[3].D:F2}");
                    }

                    // ★ 修复：rms 不是 get_metrology_object_param 的合法 GenParamName（#1303），
                    //   也不是 get_metrology_object_result 的合法 result_type（#1305）。
                    //   官方 measure_stamping_part 示例：用 used_edges 取实际参与拟合的点，自算点到直线距离的均方根。
                    var rms = ComputeRmsFromUsedEdges(metrologyModel, objectIndex.I, allParam);

                    // 获取测量带数量（num_measures 是 get_metrology_object_param 的合法 GenParamName）
                    var numMeasures = metrologyModel.GetMetrologyObjectParam(objectIndex.I, "num_measures");
                    parameters.LogInfo(
                        $"[Diag] Halcon 实际测量带数 num_measures={(numMeasures.Length > 0 ? numMeasures[0].I : -1)} " +
                        $"(用户配置 N={parameters.NumMeasures})");

                    FillResult(result, parameters, allParam, rms, numMeasures, objectIndex, metrologyModel,
                        TransitionToHalcon(parameters.Transition));
                }

                // ===== 诊断日志：结果概要（拟合线有无/参与拟合点/诊断点/角度） =====
                parameters.LogInfo(
                    $"[Diag] 结果概要: 拟合直线={(result.Line is { Length: > 0 } ? $"({result.LineStartPoint.X:F1},{result.LineStartPoint.Y:F1})→({result.LineEndPoint.X:F1},{result.LineEndPoint.Y:F1})" : "无")} | " +
                    $"参与拟合点 used_edges={result.MeasurementPoints.Length} | " +
                    $"诊断点={result.DiagnosticPoints.Length} | 中点=({result.CenterPoint.X:F1},{result.CenterPoint.Y:F1}) | Angle={result.Angle:G3}° | RMSE={result.RMSE:F3}px");

                // ===== 结果判断（对齐 VisionMaster：匹配点数/角度/拟合误差） =====
                var isOk = EvaluateResult(result, parameters);
                result.IsPass = isOk;
                result.FillDisplay(isOk, parameters);
                if (!isOk)
                {
                    var cfg = parameters.ResultConfig;
                    parameters.LogWarning(
                        $"[Diag] 判定 NG 明细: 匹配点数判断={(cfg.IsEdgePointCountEnabled ? $"{result.EdgePointCount} 不在 [{cfg.EdgePointCountMin},{cfg.EdgePointCountMax}]" : "未启用")} | " +
                        $"角度判断={(cfg.IsAngleEnabled ? $"{result.Angle:G3}° 不在 [{cfg.AngleMin:G3},{cfg.AngleMax:G3}]" : "未启用")} | " +
                        $"拟合误差判断={(cfg.IsRMSEEnabled ? $"{result.RMSE:F3}px 不在 [{cfg.RMSEMin:F3},{cfg.RMSEMax:F3}]" : "未启用")}");
                }

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
        /// 构建 Halcon 通用参数名（数量主导 + RANSAC 拟合高级参数 min_score/distance_threshold）
        /// </summary>
        private HTuple BuildGenParamName(LineDetectParameters parameters)
        {
            return new HTuple(new[]
            {
                "num_measures",
                "measure_transition",
                "measure_select",
                "min_score",
                "distance_threshold",
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
                parameters.MinScore,                 // double → min_score（RANSAC 分数门槛）
                parameters.DistanceThreshold,        // double → distance_threshold（RANSAC 距离阈值）
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
        /// 屏蔽路径结果填充：取全部测量点 → 屏蔽区空间过滤 → 全点鲁棒拟合。
        /// 屏蔽区内的点不参与拟合，也不作为"拟合点"显示（VisionMaster 屏蔽语义）。
        /// </summary>
        private void FillResultWithMask(LineDetectResults result, LineDetectParameters parameters,
            HTuple objectIndex, HMetrologyModel metrologyModel, string transition)
        {
            // 1. 取采样点（transition 过滤后的全部测量点）
            var measures = ExtractDiagnosticPoints(objectIndex, metrologyModel, transition);

            // 2. 屏蔽区空间过滤：剔除落在屏蔽区内的点
            var correction = parameters.PositionCorrectionBindingSource?.CurrentValue as AffineMatrix;
            var (kept, excluded) = ToolRegionHelper.FilterPointsOutOfMaskRegions(
                measures, parameters.GetRoisByType(RoiType.Mask), correction);
            parameters.LogInfo(
                $"[Diag] 屏蔽过滤: 采样点={measures.Length}, 保留={kept.Length}, 屏蔽剔除={excluded.Length}");

            // 3. 保底：有效点数不足以拟合直线 → 明确报错，不硬拟合
            if (kept.Length < 2)
                throw new InvalidOperationException($"屏蔽后有效测量点不足({kept.Length} < 2)，无法拟合直线");

            // 4. 全点鲁棒拟合（tukey 预拟合 → 按距离阈值划内点 → min_score 比例校验 → 内点精化）
            var (start, end, inliers) = FitLineRobust(kept, parameters);

            var center = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            result.LineStartPoint = start;
            result.LineEndPoint = end;
            result.CenterPoint = center;
            result.Line = new[] { Line2d.FromTwoPoints(start, end) };
            result.Angle = ComputeLineAngle(start, end);
            result.RMSE = ComputeLineRmse(inliers, start, end);
            result.EdgePointCount = inliers.Length;
            result.MeasurementPoints = inliers;
            result.ExcludedPoints = excluded;
            result.DiagnosticPoints = measures;
            result.HalconMeasureBands = ExtractHalconMeasureBands(objectIndex, metrologyModel, transition);
        }

        /// <summary>
        /// 全点鲁棒拟合直线：tukey 预拟合剔除离群 → 按 distance_threshold 划内点
        /// → 内点比例低于 min_score 判定结果不可靠 → 内点 geometric 精化提高精度。
        /// </summary>
        private static (Point Start, Point End, Point2d[] Inliers) FitLineRobust(
            IReadOnlyList<Point2d> points, LineDetectParameters parameters)
        {
            var (s0, e0) = FitLineCore(points, "tukey");

            var inliers = points
                .Where(p => DistancePointToLine(p, s0, e0) <= parameters.DistanceThreshold)
                .ToArray();

            if (inliers.Length < 2)
                throw new InvalidOperationException(
                    $"拟合内点不足({inliers.Length} < 2)：distance_threshold={parameters.DistanceThreshold:F1} 过小或引导偏差过大");

            double ratio = (double)inliers.Length / points.Count;
            if (ratio < parameters.MinScore)
                throw new InvalidOperationException(
                    $"拟合内点比例不足({ratio:P0} < min_score {parameters.MinScore:P0})，拟合结果不可靠");

            var (start, end) = inliers.Length == points.Count
                ? (s0, e0)
                : FitLineCore(inliers, "geometric");

            return (start, end, inliers);
        }

        /// <summary>
        /// 用指定算法对点集拟合直线（点集包成多边形 XLD 后调用 fit_line_contour_xld）。
        /// </summary>
        private static (Point Start, Point End) FitLineCore(
            IReadOnlyList<Point2d> points, string algorithm)
        {
            var rows = new HTuple(points.Select(p => (double)p.Y).ToArray());
            var cols = new HTuple(points.Select(p => (double)p.X).ToArray());
            HOperatorSet.GenContourPolygonXld(out var contour, rows, cols);
            try
            {
                HOperatorSet.FitLineContourXld(contour, algorithm, -1, 0, 3, 2.0,
                    out var rowBegin, out var colBegin, out var rowEnd, out var colEnd, out _, out _, out _);
                if (rowBegin.Length < 1 || colBegin.Length < 1 || rowEnd.Length < 1 || colEnd.Length < 1)
                    throw new InvalidOperationException($"直线拟合失败(algorithm={algorithm})：未返回有效结果");
                return (new Point(colBegin[0].D, rowBegin[0].D), new Point(colEnd[0].D, rowEnd[0].D));
            }
            finally
            {
                contour.Dispose();
            }
        }

        /// <summary>
        /// 点 (p) 到过 (a)-(b) 无限直线的垂直距离。
        /// </summary>
        private static double DistancePointToLine(Point2d p, Point a, Point b)
        {
            var abx = b.X - a.X;
            var aby = b.Y - a.Y;
            var len = Math.Sqrt(abx * abx + aby * aby);
            if (len < 1e-9)
                return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
            return Math.Abs(abx * (p.Y - a.Y) - aby * (p.X - a.X)) / len;
        }

        /// <summary>
        /// 计算点到拟合直线的均方根残差（RMSE）。
        /// </summary>
        private static double ComputeLineRmse(IReadOnlyList<Point2d> points, Point start, Point end)
        {
            if (points.Count == 0) return 0.0;
            double sumSq = 0;
            foreach (var p in points)
            {
                var dist = DistancePointToLine(p, start, end);
                sumSq += dist * dist;
            }
            return Math.Sqrt(sumSq / points.Count);
        }

        /// <summary>
        /// 计算线段方向角度（度，-180~180，与结果口径一致）。
        /// </summary>
        private static double ComputeLineAngle(Point start, Point end)
        {
            var dir = end - start;
            return dir.LengthSquared > 1e-12
                ? Math.Atan2(dir.Y, dir.X) * 180.0 / Math.PI
                : 0.0;
        }

        /// <summary>
        /// 填充结果（拟合直线/线段/测量点/中心/角度）
        /// </summary>
        private void FillResult(LineDetectResults result, LineDetectParameters parameters,
            HTuple allParam, HTuple rms, HTuple numMeasures,
            HTuple objectIndex, HMetrologyModel metrologyModel, string transition)
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

                    // ★ 修复：补算直线角度（度）。此前 result.Angle 从未赋值，恒为 0。
                    //   口径：拟合直线方向(start→end)与 X 轴夹角，ArcTan 弧度转度，范围 [-180,180]，
                    //   与 UI 角度判断范围 [AngleMin, AngleMax]=[-180,180] 及结果显示口径一致。
                    var dir = end - start;
                    result.Angle = dir.LengthSquared > 1e-12
                        ? Math.Atan2(dir.Y, dir.X) * 180.0 / Math.PI
                        : 0.0;
                }

                // 轮廓点（used_edges → 实际参与拟合的边缘点，主显示）
                result.MeasurementPoints = ExtractMeasurementPoints(objectIndex, metrologyModel);

                // 诊断检测点（GetMetrologyObjectMeasures → 检测到的全部边缘点，与拟合结果无关）
                result.DiagnosticPoints = ExtractDiagnosticPoints(objectIndex, metrologyModel, transition);

                // HALCON 实际测量带（MeasureContour XLD 轮廓 → 四边形点集，诊断卡尺对齐用，默认不显示）
                result.HalconMeasureBands = ExtractHalconMeasureBands(objectIndex, metrologyModel, transition);

                // ★ 诊断埋点：HALCON 测量带中心 vs 本地计算中心 vs 提取点坐标对比（定位卡尺对齐）
                LogMeasureBandAlignmentDiagnostics(result, parameters, transition);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"解析 Halcon 直线结果失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 输出测量带对齐诊断日志：HALCON 实际测量带中心 vs 本地 BuildLineBands 计算中心 vs 提取点坐标。
        /// 通过首条/末条中心对比，判断是相位（step）还是坐标变换问题。
        /// </summary>
        private static void LogMeasureBandAlignmentDiagnostics(
            LineDetectResults result, LineDetectParameters parameters, string transition)
        {
            try
            {
                // 本地计算中心（与 BuildLineBands 同一公式：N 条带分 N-1 段，首条在起点）
                var localStart = parameters.LineStartPoint;
                var localEnd = parameters.LineEndPoint;
                if (parameters.IsPositionCorrectionEnabled &&
                    parameters.PositionCorrectionBindingSource?.CurrentValue is AffineMatrix m)
                {
                    localStart = m.TransformWpfPoint(localStart);
                    localEnd = m.TransformWpfPoint(localEnd);
                }
                var seg = localEnd - localStart;
                var totalLen = seg.Length;
                var dir = seg / totalLen;
                double step = parameters.NumMeasures > 1 ? totalLen / (parameters.NumMeasures - 1) : totalLen;

                string localCenters = "";
                for (int i = 0; i < Math.Min(parameters.NumMeasures, 3); i++)
                {
                    var c = localStart + dir * (i * step);
                    localCenters += $"[{i}]({c.X:F1},{c.Y:F1}) ";
                }

                // HALCON 实际测量带中心（轮廓点均值）
                string halconCenters = "";
                if (result.HalconMeasureBands.Length > 0)
                {
                    int show = Math.Min(result.HalconMeasureBands.Length, 3);
                    for (int i = 0; i < show; i++)
                    {
                        var pts = result.HalconMeasureBands[i];
                        if (pts == null || pts.Length == 0) { halconCenters += $"[{i}](null) "; continue; }
                        double sx = 0, sy = 0;
                        foreach (var p in pts) { sx += p.X; sy += p.Y; }
                        halconCenters += $"[{i}]({sx / pts.Length:F1},{sy / pts.Length:F1}) ";
                    }
                }

                // 提取点前 3 个
                string edgePts = "";
                for (int i = 0; i < Math.Min(result.MeasurementPoints.Length, 3); i++)
                    edgePts += $"[{i}]({result.MeasurementPoints[i].X:F1},{result.MeasurementPoints[i].Y:F1}) ";

                parameters.LogInfo(
                    $"[Diag] 卡尺对齐: N={parameters.NumMeasures} transition={transition}\n" +
                    $"  引导线起终点=({parameters.LineStartPoint.X:F1},{parameters.LineStartPoint.Y:F1})→({parameters.LineEndPoint.X:F1},{parameters.LineEndPoint.Y:F1})\n" +
                    $"  本地计算中心: {localCenters}\n" +
                    $"  HALCON测量带中心: {halconCenters}\n" +
                    $"  提取点(used_edges): {edgePts}");
            }
            catch (Exception ex)
            {
                parameters.LogInfo($"[Diag] 卡尺对齐诊断异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 提取拟合点（used_edges → 实际参与拟合的边缘点，每测量带一个）。
        /// 与官方 measure_stamping_part 示例一致。
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
        /// 提取诊断检测点（GetMetrologyObjectMeasures → 指定 transition 下检测到的全部边缘点）。
        /// 与拟合结果解耦：拟合失败时也有数据，便于定位原因。
        /// </summary>
        private static Point2d[] ExtractDiagnosticPoints(
            HTuple objectIndex, HMetrologyModel metrologyModel, string transition)
        {
            try
            {
                var contour = metrologyModel.GetMetrologyObjectMeasures(
                    objectIndex, transition, out var row, out var col);
                contour?.Dispose();
                if (row.Length == 0) return Array.Empty<Point2d>();
                var pts = new Point2d[row.Length];
                for (int i = 0; i < row.Length; i++)
                    pts[i] = new Point2d(col[i].D, row[i].D);
                return pts;
            }
            catch
            {
                return Array.Empty<Point2d>();
            }
        }

        /// <summary>
        /// 提取 HALCON 实际测量带轮廓（MeasureContour XLD → 每段轮廓的点集）。
        /// 用于诊断卡尺对齐：与本地计算的卡尺（BuildLineBands）叠加对比，定位相位/步长偏差。
        /// </summary>
        private static System.Windows.Point[][] ExtractHalconMeasureBands(
            HTuple objectIndex, HMetrologyModel metrologyModel, string transition)
        {
            try
            {
                var contour = metrologyModel.GetMetrologyObjectMeasures(
                    objectIndex, transition, out var _, out var _);
                if (contour == null) return Array.Empty<System.Windows.Point[]>();

                try
                {
                    int count = contour.CountObj();
                    if (count <= 0) return Array.Empty<System.Windows.Point[]>();

                    var bands = new System.Windows.Point[count][];
                    for (int i = 1; i <= count; i++)
                    {
                        var single = contour.SelectObj(i);
                        single.GetContourXld(out var row, out var col);
                        if (row.Length == 0) { single?.Dispose(); continue; }
                        var pts = new System.Windows.Point[row.Length];
                        for (int j = 0; j < row.Length; j++)
                            pts[j] = new System.Windows.Point(col[j].D, row[j].D);
                        bands[i - 1] = pts;
                        single?.Dispose();
                    }
                    return bands;
                }
                finally
                {
                    contour.Dispose();
                }
            }
            catch
            {
                return Array.Empty<System.Windows.Point[]>();
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
