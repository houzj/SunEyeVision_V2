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
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Tool.CircleDetect.Models;
using SunEyeVision.Tool.CircleDetect.Views;

namespace SunEyeVision.Tool.CircleDetect
{
    /// <summary>
    /// 圆查找工具 - 引导式 2D Metrology 圆/圆弧边缘检测
    /// </summary>
    /// <remarks>
    /// 算法流程（Halcon 2D Metrology）：
    ///   1. create_metrology_model → 创建计量模型
    ///   2. add_metrology_object_circle_measure → 添加圆测量对象（自动生成测量带阵列）
    ///   3. apply_metrology_model → 执行测量（逐测量带取点 + 拟合）
    ///   4. get_metrology_object_result → 获取拟合圆参数 + RMSE
    /// 支持整圆（默认 0~2π）与圆弧（StartAngle/EndAngle 限定角度范围）。
    /// </remarks>
    [Tool(
        id: "CircleDetect",
        displayName: "圆查找",
        Description = "圆/圆弧边缘检测工具（引导式 2D Metrology）",
        Icon = "⭕",
        Category = "定位工具",
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    [JsonDerivedType(typeof(CircleDetectParameters), "CircleDetect")]
    public class CircleDetectTool : IToolPlugin<CircleDetectParameters, CircleDetectResults>
    {
        private readonly ToolAttribute? _attribute;
        public CircleDetectTool() => _attribute = GetType().GetCustomAttribute<ToolAttribute>();

        /// <summary>
        /// 执行工具
        /// </summary>
        public CircleDetectResults Run(CircleDetectParameters parameters)
        {
            var result = new CircleDetectResults
            {
                ToolName = _attribute?.DisplayName ?? "圆查找",
                ToolId = _attribute?.Id ?? "CircleDetect",
                Timestamp = DateTime.Now,
            };

            // ===== 参数校验（Fail-Fast）=====
            if (parameters.GuideCircle == null)
            {
                parameters.LogError("参数校验失败: 引导圆未设置，请先绘制引导圆");
                result.SetError("参数校验失败: 引导圆未设置，请先绘制引导圆");
                return result;
            }
            if (parameters.GuideCircle.Value.Radius <= 0)
            {
                parameters.LogError("参数校验失败: 引导圆半径必须大于 0");
                result.SetError("参数校验失败: 引导圆半径必须大于 0");
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

            // ===== Halcon 2D Metrology 执行（圆/圆弧）=====
            using var metrologyModel = new HMetrologyModel();
            metrologyModel.CreateMetrologyModel();

            try
            {
                HOperatorSet.GetImageSize(hImage, out var width, out var height);
                metrologyModel.SetMetrologyModelImageSize((int)width.L, (int)height.L);

                // 位置修正：引导圆按矩阵变换到当前图像坐标（圆心变换 + 半径缩放；
                // 弧为有向形状，起止角随矩阵旋转，与交互层 RenderArc 同口径，保证交互/执行/结果三层一致）
                var guideCenter = parameters.GuideCircle.Value.Center;
                var guideRadius = parameters.GuideCircle.Value.Radius;
                AffineMatrix? guideMatrix = null;
                if (parameters.IsPositionCorrectionEnabled &&
                    PositionCorrectionResolver.ResolveFirstMatrix(parameters.PositionCorrectionBindingSource) is { } matrix)
                {
                    guideMatrix = matrix;
                    guideCenter = matrix.TransformWpfPoint(guideCenter);
                    guideRadius *= matrix.ScaleFactor();
                }
                // 弧模式：显示系弧（角度随矩阵旋转），作为 genParam 与结果回显的角度源
                Arc2d? guideDisplayArc = null;
                if (parameters.IsArc)
                {
                    guideDisplayArc = new Arc2d(guideCenter, guideRadius,
                        RotateWpfAngle(guideMatrix, parameters.StartAngle),
                        RotateWpfAngle(guideMatrix, parameters.EndAngle),
                        parameters.IsMajorArc);
                }
                // 整圆卡尺阵列起始相位（参考系首条在 0°，位置修正含旋转时随矩阵转到旋转角；无修正 = null）
                double? circleStartRadWpf = guideMatrix != null ? RotateWpfAngle(guideMatrix, 0.0) : (double?)null;

                // ★ 日志埋点（诊断边缘极性/选取模式组合问题）：
                //   记录参数枚举 → HALCON 字符串映射 + 完整 genParam 名称/值 + 引导圆/图像信息
                var genParamName = BuildGenParamName(parameters);
                var genParamValue = BuildGenParamValue(parameters, guideDisplayArc, circleStartRadWpf);
                parameters.LogInfo(
                    $"[Diag] 参数埋点: 图像={width.L}x{height.L}, " +
                    $"引导圆=中心({guideCenter.X:F2},{guideCenter.Y:F2}) 半径={guideRadius:F2}, " +
                    $"MeasureLength1={parameters.MeasureLength1:F2} MeasureLength2={parameters.MeasureLength2:F2}, " +
                    $"Sigma={parameters.Sigma} Threshold={parameters.Threshold}");
                parameters.LogInfo(
                    $"[Diag] 极性/选取映射: Transition={parameters.Transition}→\"{TransitionToHalcon(parameters.Transition)}\", " +
                    $"Select={parameters.Select}→\"{SelectToHalcon(parameters.Select)}\"");
                parameters.LogInfo(
                    $"[Diag] genParamName={genParamName} genParamValue={genParamValue}");

                HTuple objectIndex = metrologyModel.AddMetrologyObjectCircleMeasure(
                    guideCenter.Y,
                    guideCenter.X,
                    guideRadius,
                    parameters.MeasureLength1, parameters.MeasureLength2,
                    parameters.Sigma, parameters.Threshold,
                    genParamName, genParamValue);
                parameters.LogInfo($"[Diag] AddMetrologyObjectCircleMeasure 成功, objectIndex={objectIndex.I}");

                // 执行测量（逐测量带取点 + 拟合）
                metrologyModel.ApplyMetrologyModel(hImage);
                parameters.LogInfo("[Diag] ApplyMetrologyModel 完成");

                // ★ 诊断埋点：分别按"当前 select"与"all"查询测量点，
                //   区分 first/last 失败是"检测到了但被过滤"还是"压根没检测到"
                try
                {
                    // 实例方法 GetMetrologyObjectMeasures(index, transition, out row, out col)
                    // 封装版固定 select="all"，返回该 transition 下检测到的全部测量点
                    var selContour = metrologyModel.GetMetrologyObjectMeasures(
                        objectIndex, TransitionToHalcon(parameters.Transition),
                        out var selRow, out var selCol);
                    selContour?.Dispose();
                    parameters.LogInfo(
                        $"[Diag] 测量点统计: transition={TransitionToHalcon(parameters.Transition)} " +
                        $"all点数={selRow.Length}");
                }
                catch (Exception diagEx)
                {
                    parameters.LogInfo($"[Diag] GetMetrologyObjectMeasures 诊断异常: {diagEx.Message}");
                }

                // ===== 结果填充（统一管线：取全部检测点 → 屏蔽区空间过滤(可选) → 全点鲁棒拟合）=====
                // ★ 架构统一：有/无屏蔽区走同一条拟合路径。屏蔽只影响输入点集（剔除屏蔽区内点），
                //   不改变核心拟合算法。Halcon 2D Metrology 原生拟合不支持屏蔽区概念，
                //   屏蔽过滤必须在拟合前完成，故统一走自研鲁棒拟合（fit_circle_contour_xld）。
                bool hasEnabledMask = parameters.GetRoisByType(RoiType.Mask).Any();
                FillResultWithMask(result, parameters, objectIndex, metrologyModel, hasEnabledMask, guideDisplayArc);

                // ===== 结果判断（对齐 VisionMaster：半径/中心X/中心Y/拟合误差/匹配点数） =====
                var isOk = EvaluateResult(result, parameters);
                result.IsPass = isOk;
                result.FillDisplay(isOk, parameters);

                // 屏蔽区几何输出（RoI 契约：结果树"输入区域/屏蔽区图形"填充，对齐图像工具）
                ToolRegionHelper.ExtractMaskGeometries(result, parameters.MaskRegions?.Regions);

                // 卡尺输入回显（模块结果"输入区域/卡尺"）：引导线 + 测量带参数
                if (parameters.GuideCircle is { } guideCircle)
                {
                    result.GuideGeometry = new[]
                    {
                        new ShapeParameters { Geometry = guideCircle },
                    };
                }
                result.CaliberCount = parameters.NumMeasures;
                result.CaliberWidth = parameters.MeasureLength1 * 2;
                result.CaliberLength = parameters.MeasureLength2 * 2;
                result.CaliberTransition = parameters.Transition.ToString();

                result.SetSuccess();
                parameters.LogInfo(
                    $"圆查找完成: 边缘点数={result.EdgePointCount}, RMSE={result.RMSE:F3}px, 判定={isOk}");
            }
            catch (Exception ex)
            {
                parameters.LogError($"圆查找处理异常: {ex.Message}", ex);
                result.SetError($"处理失败: {ex.Message}", ex);
            }

            return result;
        }

        /// <summary>
        /// 结果判断（半径/中心X/中心Y/拟合误差/匹配点数，任一不满足判 NG）
        /// </summary>
        private static bool EvaluateResult(CircleDetectResults result, CircleDetectParameters parameters)
        {
            var cfg = parameters.ResultConfig;

            if (cfg.IsRadiusEnabled && CircleRadius(result) is double r &&
                (r < cfg.RadiusMin || r > cfg.RadiusMax))
                return false;

            if (cfg.IsCenterXEnabled &&
                (result.CenterPoint.X < cfg.CenterXMin || result.CenterPoint.X > cfg.CenterXMax))
                return false;

            if (cfg.IsCenterYEnabled &&
                (result.CenterPoint.Y < cfg.CenterYMin || result.CenterPoint.Y > cfg.CenterYMax))
                return false;

            if (cfg.IsRMSEEnabled &&
                (result.RMSE < cfg.RMSEMin || result.RMSE > cfg.RMSEMax))
                return false;

            if (cfg.IsEdgePointCountEnabled &&
                (result.EdgePointCount < cfg.EdgePointCountMin || result.EdgePointCount > cfg.EdgePointCountMax))
                return false;

            return true;
        }

        /// <summary>
        /// 获取拟合半径（圆或圆弧）
        /// </summary>
        private static double? CircleRadius(CircleDetectResults result)
        {
            if (result.Circle is { Length: > 0 }) return result.Circle[0].Radius;
            if (result.Arc is { Length: > 0 }) return result.Arc[0].Radius;
            return null;
        }

        /// <summary>
        /// 构建 Halcon 通用参数名（产品决策：只支持数量主导；圆/圆弧带 start_phi/end_phi；
        /// 拟合距离阈值 distance_threshold）
        /// </summary>
        private HTuple BuildGenParamName(CircleDetectParameters parameters)
        {
            return new HTuple(new[]
            {
                "num_measures",
                "measure_transition",
                "measure_select",
                "start_phi",
                "end_phi",
                "point_order",                        // ★ Halcon 圆弧方向（'positive'=数学逆时针/'negative'=数学顺时针）
                "distance_threshold",
            });
        }

        /// <summary>
        /// 构建 Halcon 通用参数值（数量主导：num_measures 必须传 int，避免 HALCON #1210 类型错误）
        /// </summary>
        /// <remarks>
        /// ★ 角度参数一律经 <see cref="HalconArcAdapter"/> 转换（WPF 连续扫掠坐标 → HALCON [0, 2π] 区间），
        ///   不在本方法内自行推导，避免"整圆退化成零长弧""跨 0° 边界方向错"等跨层语义问题。
        ///   整圆与圆弧规则不同，必须分派（HALCON 文档：闭合圆 = start_phi 0 + end_phi 2π + positive）。
        /// </remarks>
        private HTuple BuildGenParamValue(CircleDetectParameters parameters, Arc2d? displayArc = null, double? circleStartRadWpf = null)
        {
            double startPhi;
            double endPhi;
            string pointOrder;

            if (parameters.IsArc)
            {
                var arc = displayArc ?? new Arc2d(
                    parameters.GuideCircle.Value.Center,
                    parameters.GuideCircle.Value.Radius,
                    parameters.StartAngle,
                    parameters.EndAngle,
                    parameters.IsMajorArc);

                (startPhi, endPhi, pointOrder) = HalconArcAdapter.FromArc(arc);

                // 两端点物理坐标（与图上测量带端点对比，定标用）
                var p1 = new Point(
                    arc.Center.X + arc.Radius * Math.Cos(arc.StartAngle),
                    arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngle));
                var p2 = new Point(
                    arc.Center.X + arc.Radius * Math.Cos(arc.EndAngle),
                    arc.Center.Y + arc.Radius * Math.Sin(arc.EndAngle));
                parameters.LogInfo(
                    $"[Diag] 圆弧适配: Arc2d(start={arc.StartAngle:F3}, end={arc.EndAngle:F3}, " +
                    $"span={arc.AngularSpan:F3}, major={arc.IsMajorArc}, cw={arc.IsClockwise}) " +
                    $"P1=({p1.X:F1},{p1.Y:F1}) P2=({p2.X:F1},{p2.Y:F1}) " +
                    $"→ Halcon(start_phi={startPhi:F3}, end_phi={endPhi:F3}, point_order={pointOrder})");
            }
            else
            {
                // 整圆无极性但卡尺阵列有起始相位：起始相位随位置修正旋转（与屏蔽区/内容同口径，
                // 参考系首条在 0°，修正后首条在矩阵旋转角处；均分间距不变，无修正时等效旧 (0, 2π)）。
                double startWpfRad = circleStartRadWpf ?? 0.0;
                const double TwoPi = 2.0 * Math.PI;
                double phi = (-startWpfRad) % TwoPi;
                if (phi < 0) phi += TwoPi;
                startPhi = phi;
                endPhi = phi + TwoPi;      // 闭合圆：end = start + 2π（HALCON 官方语义）
                pointOrder = "positive";
                parameters.LogInfo(
                    $"[Diag] 整圆适配: 中心({parameters.GuideCircle.Value.Center.X:F1}," +
                    $"{parameters.GuideCircle.Value.Center.Y:F1}) 半径={parameters.GuideCircle.Value.Radius:F1} " +
                    $"→ Halcon(start_phi={startPhi:F3}, end_phi={endPhi:F3}, point_order={pointOrder})");
            }

            return new HTuple(new object[]
            {
                parameters.NumMeasures,             // int → num_measures
                TransitionToHalcon(parameters.Transition),
                SelectToHalcon(parameters.Select),
                startPhi,                            // ★ 弧起点（HALCON 区间 [0, 2π]）
                endPhi,                              // ★ 弧终点（HALCON 区间 [0, 2π]）
                pointOrder,                          // ★ 方向（positive=逆时针 / negative=顺时针）
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
        /// WPF 参考系弧度角 → 显示系弧度角（有向形状完整跟随位置修正旋转；无矩阵时原样返回）。
        /// </summary>
        private static double RotateWpfAngle(AffineMatrix? matrix, double angleRad)
            => matrix != null
                ? PositionCorrectionResolver.DisplayAngleDeg(matrix, angleRad * 180.0 / Math.PI) * Math.PI / 180.0
                : angleRad;

        /// <summary>
        /// 统一结果填充（有/无屏蔽共用）：取全部测量点 → 屏蔽区空间过滤(可选) → 全点鲁棒拟合。
        /// 屏蔽区内的点不参与拟合，也不作为"拟合点"显示（VisionMaster 屏蔽语义）；
        /// 无屏蔽区时全量点参与拟合（与屏蔽路径完全同一套核心拟合逻辑）。
        /// </summary>
        private void FillResultWithMask(CircleDetectResults result, CircleDetectParameters parameters,
            HTuple objectIndex, HMetrologyModel metrologyModel, bool hasEnabledMask, Arc2d? displayArc)
        {
            // 1. 取采样点（transition 过滤后的全部测量点）
            var measures = ExtractDiagnosticPoints(
                objectIndex, metrologyModel, TransitionToHalcon(parameters.Transition));

            // 2. 屏蔽区空间过滤：仅在有屏蔽区时剔除落在屏蔽区内的点；无屏蔽区 = 全量点保留
            Point2d[] kept;
            Point2d[] excluded;
            if (hasEnabledMask)
            {
                var correction = PositionCorrectionResolver.ResolveFirstMatrix(parameters.PositionCorrectionBindingSource);
                (kept, excluded) = ToolRegionHelper.FilterPointsOutOfMaskRegions(
                    measures, parameters.GetRoisByType(RoiType.Mask), correction);
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

            // 3. 保底：有效点数不足以拟合圆 → 明确报错，不硬拟合
            if (kept.Length < 3)
                throw new InvalidOperationException($"有效测量点不足({kept.Length} < 3)，无法拟合圆");

            // 4. 全点鲁棒拟合（geotukey 预拟合 → 按距离阈值划内点 → 内点精化）
            var (center, radius, inliers) = FitCircleRobust(kept, parameters);
            result.CenterPoint = center;
            result.RMSE = ComputeCircleRmse(inliers, center, radius);
            result.EdgePointCount = inliers.Length;
            result.MeasurementPoints = inliers;
            result.ExcludedPoints = excluded;
            result.DiagnosticPoints = measures;
            result.HalconMeasureBands = ExtractHalconMeasureBands(
                objectIndex, metrologyModel, TransitionToHalcon(parameters.Transition));

            if (parameters.IsArc)
            {
                // 角度源 = 显示系弧（位置修正旋转后）；无修正时与参考角一致
                double sA = displayArc?.StartAngle ?? parameters.StartAngle;
                double eA = displayArc?.EndAngle ?? parameters.EndAngle;
                result.Arc = new[] { new Arc2d(
                    new Point(center.X, center.Y), radius,
                    sA, eA, parameters.IsMajorArc) };
            }
            else
            {
                result.Circle = new[] { new Circle2d(center.X, center.Y, radius) };
            }
        }

        /// <summary>
        /// 全点鲁棒拟合圆：geotukey 预拟合剔除离群 → 按 distance_threshold 划内点（测量点）
        /// → 内点 geometric 精化提高精度。
        /// ★ fit_circle_contour_xld 无 'tukey'（官方 List: ahuber/algebraic/atukey/geohuber/geometric/geotukey），
        ///   取几何 Tukey 'geotukey'（与直线 tukey 同族的几何加权鲁棒算法）。
        /// ★ 无分数门槛：Halcon min_score 依赖"每测量带一点"归一化，自研取点是带内全候选点
        ///   （无法按带归属），套用分数门槛会误杀可用拟合。鲁棒拟合本身已剔除离群，
        ///   质量由 RMSE / EdgePointCount 判定窗口把关。
        /// </summary>
        private static (Point Center, double Radius, Point2d[] Inliers) FitCircleRobust(
            IReadOnlyList<Point2d> points, CircleDetectParameters parameters)
        {
            var (c0, r0) = FitCircleCore(points, "geotukey");

            var inliers = points
                .Where(p => Math.Abs(
                    Math.Sqrt((p.X - c0.X) * (p.X - c0.X) + (p.Y - c0.Y) * (p.Y - c0.Y)) - r0)
                    <= parameters.DistanceThreshold)
                .ToArray();

            if (inliers.Length < 3)
                throw new InvalidOperationException(
                    $"拟合内点不足({inliers.Length} < 3)：distance_threshold={parameters.DistanceThreshold:F1} 过小或引导偏差过大");

            var (center, radius) = inliers.Length == points.Count
                ? (c0, r0)
                : FitCircleCore(inliers, "geometric");

            return (center, radius, inliers);
        }

        /// <summary>
        /// 用指定算法对点集拟合圆（点集包成多边形 XLD 后调用 fit_circle_contour_xld）。
        /// </summary>
        private static (Point Center, double Radius) FitCircleCore(
            IReadOnlyList<Point2d> points, string algorithm)
        {
            var rows = new HTuple(points.Select(p => (double)p.Y).ToArray());
            var cols = new HTuple(points.Select(p => (double)p.X).ToArray());
            HOperatorSet.GenContourPolygonXld(out var contour, rows, cols);
            try
            {
                HOperatorSet.FitCircleContourXld(contour, algorithm, -1, 0.0, 0, 3, 2.0,
                    out var row, out var col, out var radius, out _, out _, out _);
                if (row.Length < 1 || col.Length < 1 || radius.Length < 1)
                    throw new InvalidOperationException($"圆拟合失败(algorithm={algorithm})：未返回有效结果");
                return (new Point(col[0].D, row[0].D), radius[0].D);
            }
            finally
            {
                contour.Dispose();
            }
        }

        /// <summary>
        /// 计算点到拟合圆的均方根残差（RMSE）。
        /// </summary>
        private static double ComputeCircleRmse(IReadOnlyList<Point2d> points, Point center, double radius)
        {
            if (points.Count == 0) return 0.0;
            double sumSq = 0;
            foreach (var p in points)
            {
                var dist = Math.Sqrt((p.X - center.X) * (p.X - center.X) + (p.Y - center.Y) * (p.Y - center.Y));
                var diff = dist - radius;
                sumSq += diff * diff;
            }
            return Math.Sqrt(sumSq / points.Count);
        }

        /// <summary>
        /// 提取诊断检测点（GetMetrologyObjectMeasures → 指定 transition 下检测到的全部边缘点）。
        /// 与拟合结果解耦：拟合失败（all_param 空）时也有数据，便于定位"点在哪、为什么不共圆"。
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
        /// 用于诊断卡尺对齐：与本地计算的卡尺（BuildCircleBands）叠加对比，定位相位/步长偏差。
        /// 每个 XLD 轮廓可能为矩形（4 点）或采样多段线，统一取全部点作为轮廓显示。
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
            return new CircleDetectToolDebugControl();
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
