using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Windows;
using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
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
                $"distance_threshold={parameters.DistanceThreshold:F2} | " +
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
                    PositionCorrectionResolver.ResolveFirstMatrix(parameters.PositionCorrectionBindingSource) is { } matrix)
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

                // ===== 结果填充（统一管线：取全部检测点 → 屏蔽区空间过滤(可选) → 全点鲁棒拟合）=====
                // ★ 架构统一：有/无屏蔽区走同一条拟合路径。屏蔽只影响输入点集（剔除屏蔽区内点），
                //   不改变核心拟合算法。Halcon 2D Metrology 原生拟合不支持屏蔽区概念，
                //   屏蔽过滤必须在拟合前完成，故统一走自研鲁棒拟合（fit_line_contour_xld）。
                bool hasEnabledMask = parameters.GetRoisByType(RoiParameterMode.MaskRoi).Any();
                FillResultWithMask(result, parameters, objectIndex, metrologyModel,
                    TransitionToHalcon(parameters.Transition), hasEnabledMask);

                // ===== 诊断日志：结果概要（拟合线有无/参与拟合点/诊断点/角度） =====
                parameters.LogInfo(
                    $"[Diag] 结果概要: 拟合直线={(result.Line is { Length: > 0 } ? $"({result.LineStartPoint.X:F1},{result.LineStartPoint.Y:F1})→({result.LineEndPoint.X:F1},{result.LineEndPoint.Y:F1})" : "无")} | " +
                    $"参与拟合点(内点)={result.MeasurementPoints.Length} | " +
                    $"诊断点={result.DiagnosticPoints.Length} | 中点=({result.CenterPoint.X:F1},{result.CenterPoint.Y:F1}) | Angle={result.Angle:G3}° | RMSE={result.RMSE:F3}px");

                // ===== 结果判断（对齐 VisionMaster：匹配点数/角度/拟合误差） =====
                var isOk = EvaluateResult(result, parameters);
                result.IsPass = isOk;
                result.FillDisplay(isOk, parameters);

                // 屏蔽区几何输出（RoI 契约：结果树"输入/屏蔽区图形"填充，对齐图像工具）
                ToolRegionHelper.ExtractMaskGeometries(result, parameters.MaskRois?.Rois,
                    parameters.NodeResults);

                // 卡尺输入回显（模块结果"输入/卡尺"）：引导线 + 测量带参数
                var guideStart = parameters.LineStartPoint;
                var guideEnd = parameters.LineEndPoint;
                var dx = guideEnd.X - guideStart.X;
                var dy = guideEnd.Y - guideStart.Y;
                var length = Math.Sqrt(dx * dx + dy * dy);
                if (length >= 1e-6)
                {
                    result.GuideGeometry = new[]
                    {
                        new RoiShape
                        {
                            Geometry = new Line2d(
                                new Point(guideStart.X, guideStart.Y),
                                new Vector(dx / length, dy / length)),
                        },
                    };
                }
                result.CaliberCount = parameters.NumMeasures;
                result.CaliberWidth = parameters.MeasureLength1 * 2;
                result.CaliberLength = parameters.MeasureLength2 * 2;
                result.CaliberTransition = parameters.Transition.ToString();

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
        /// 构建 Halcon 通用参数名（数量主导 + 拟合距离阈值 distance_threshold）
        /// </summary>
        private HTuple BuildGenParamName(LineDetectParameters parameters)
        {
            return new HTuple(new[]
            {
                "num_measures",
                "measure_transition",
                "measure_select",
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
                parameters.DistanceThreshold,        // double → distance_threshold（拟合距离阈值）
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
        /// 统一结果填充（有/无屏蔽共用）：取全部测量点 → 屏蔽区空间过滤(可选) → 全点鲁棒拟合。
        /// 屏蔽区内的点不参与拟合，也不作为"拟合点"显示（VisionMaster 屏蔽语义）；
        /// 无屏蔽区时全量点参与拟合（与屏蔽路径完全同一套核心拟合逻辑）。
        /// </summary>
        private void FillResultWithMask(LineDetectResults result, LineDetectParameters parameters,
            HTuple objectIndex, HMetrologyModel metrologyModel, string transition, bool hasEnabledMask)
        {
            // 1. 取采样点（transition 过滤后的全部测量点）
            var measures = ExtractDiagnosticPoints(objectIndex, metrologyModel, transition);

            // 2. 屏蔽区空间过滤：仅在有屏蔽区时剔除落在屏蔽区内的点；无屏蔽区 = 全量点保留
            Point2d[] kept;
            Point2d[] excluded;
            if (hasEnabledMask)
            {
                var correction = PositionCorrectionResolver.ResolveFirstMatrix(parameters.PositionCorrectionBindingSource);
                (kept, excluded) = ToolRegionHelper.FilterPointsOutOfMaskRois(
                    measures, parameters.GetRoisByType(RoiParameterMode.MaskRoi), correction);
                parameters.LogInfo(
                    $"[Diag] 屏蔽过滤: 采样点={measures.Length}, 保留={kept.Length}, 屏蔽剔除={excluded.Length}");
            }
            else
            {
                kept = measures;
                excluded = Array.Empty<Point2d>();
                parameters.LogInfo(
                    $"[Diag] 无屏蔽区: 全量测量点={measures.Length} 全部参与拟合（与屏蔽路径同一套鲁棒拟合）");
            }

            // 3. 保底：有效点数不足以拟合直线 → 明确报错，不硬拟合
            if (kept.Length < 2)
                throw new InvalidOperationException($"有效测量点不足({kept.Length} < 2)，无法拟合直线");

            // 4. 全点鲁棒拟合（tukey 预拟合 → 按距离阈值划内点 → 内点精化）
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
        /// → 内点 regression 最小二乘精化。
        /// ★ 无分数门槛：Halcon min_score 依赖"每测量带一点"归一化，自研取点是带内全候选点
        ///   （无法按带归属），套用分数门槛会误杀可用拟合。鲁棒拟合本身已剔除离群，
        ///   质量由 RMSE / EdgePointCount 判定窗口把关。
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

            var (start, end) = inliers.Length == points.Count
                ? (s0, e0)
                : FitLineCore(inliers, "regression");

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
