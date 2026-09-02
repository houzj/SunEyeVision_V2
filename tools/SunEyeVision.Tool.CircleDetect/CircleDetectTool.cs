using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Rendering;
using SunEyeVision.Plugin.SDK.VisionAdapters;
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

                // 位置修正：引导圆按矩阵变换到当前图像坐标（圆心变换 + 半径缩放）
                var guideCenter = parameters.GuideCircle.Value.Center;
                var guideRadius = parameters.GuideCircle.Value.Radius;
                if (parameters.IsPositionCorrectionEnabled &&
                    parameters.PositionCorrectionBindingSource?.CurrentValue is AffineMatrix matrix)
                {
                    guideCenter = matrix.TransformWpfPoint(guideCenter);
                    guideRadius *= matrix.ScaleFactor();
                }

                // ★ 日志埋点（诊断边缘极性/选取模式组合问题）：
                //   记录参数枚举 → HALCON 字符串映射 + 完整 genParam 名称/值 + 引导圆/图像信息
                var genParamName = BuildGenParamName(parameters);
                var genParamValue = BuildGenParamValue(parameters);
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

                // 获取拟合圆参数（all_param = [Row, Col, Radius]）
                var allParam = metrologyModel.GetMetrologyObjectResult(
                    objectIndex.I, "all", "result_type", "all_param");
                parameters.LogInfo($"[Diag] all_param 长度={allParam.Length}, 内容={allParam}");

                // ★ 修复：rms 不是 get_metrology_object_param 的合法 GenParamName（#1303），
                //   也不是 get_metrology_object_result 的合法 result_type（#1305）。
                //   官方 measure_stamping_part 示例：用 used_edges 取实际参与拟合的点，自算点到圆距离的均方根。
                var rms = ComputeRmsFromUsedEdges(metrologyModel, objectIndex.I, allParam);
                parameters.LogInfo($"[Diag] rms={rms.D:F3}");

                // 获取测量带数量（num_measures 是 get_metrology_object_param 的合法 GenParamName）
                var numMeasures = metrologyModel.GetMetrologyObjectParam(objectIndex.I, "num_measures");
                parameters.LogInfo($"[Diag] num_measures={numMeasures.I}");

                // ===== 结果填充 =====
                FillResult(result, parameters, allParam, rms, numMeasures, objectIndex, metrologyModel);

                // ★ 卡尺定标诊断（临时，A7 阶段删除）
                LogCalibrationDiagnostics(result, parameters);

                // ===== 结果判断（对齐 VisionMaster：半径/中心X/中心Y/拟合误差/匹配点数） =====
                var isOk = EvaluateResult(result, parameters);
                result.IsPass = isOk;
                result.FillDisplay(isOk, parameters);

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
        /// RANSAC 拟合高级参数 min_score/distance_threshold 开放给用户）
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
                "min_score",
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
        private HTuple BuildGenParamValue(CircleDetectParameters parameters)
        {
            double startPhi;
            double endPhi;
            string pointOrder;

            if (parameters.IsArc)
            {
                var arc = new Arc2d(
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
                (startPhi, endPhi, pointOrder) = HalconArcAdapter.FromFullCircle();
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
        /// ★ 卡尺定标诊断（临时，A7 阶段删除）。
        /// 目的：让"WPF 画出的卡尺"与"HALCON 实际采样的 measure regions"逐条重合（所见即所得）。
        /// 做法：用 get_metrology_object_measures 取 HALCON 真实测量带，与本地 BuildCircleBands/BuildArcBands 比对，输出：
        ///   ① 逐条中心偏差（max/avg，目标 &lt; 0.5px）
        ///   ② 首条相位差 delta（0 = 格点 / step/2 = 半格 / 其它 = 固定偏移）
        ///   ③ 首带中心到弧起点距离 d0（0 = 中心贴端点 / halfBand = 边贴端点）
        ///   ④ 首带两条边长在径向/切向的投影（验证 MeasureLength1/2 的方向映射是否搞反）
        /// </summary>
        private static void LogCalibrationDiagnostics(CircleDetectResults result, CircleDetectParameters parameters)
        {
            var halconBands = result.HalconMeasureBands;
            if (halconBands == null || halconBands.Length == 0) return;
            if (!parameters.GuideCircle.HasValue) return;

            var circle = parameters.GuideCircle.Value;
            double twoPi = 2.0 * Math.PI;

            // 本地卡尺（与结果显示同源）
            System.Windows.Point[][] localBands = parameters.IsArc
                ? MeasureBandOverlay.BuildArcBands(
                    new Arc2d(circle.Center, circle.Radius, parameters.StartAngle, parameters.EndAngle, parameters.IsMajorArc),
                    parameters.MeasureLength1 * 2, parameters.MeasureLength2 * 2, parameters.NumMeasures).ToArray()
                : MeasureBandOverlay.BuildCircleBands(
                    circle, parameters.MeasureLength1 * 2, parameters.MeasureLength2 * 2, parameters.NumMeasures).ToArray();

            if (localBands.Length == 0) return;

            // ① 配对：对每条 HALCON 带找"最近的本地带"（避免按索引配对受编号顺序/方向影响而误判）
            double maxNearest = 0, sumNearest = 0;
            for (int i = 0; i < halconBands.Length; i++)
            {
                var h = BandCenter(halconBands[i]);
                double best = double.MaxValue;
                foreach (var lb in localBands)
                {
                    double d = (h - BandCenter(lb)).Length;
                    if (d < best) best = d;
                }
                maxNearest = Math.Max(maxNearest, best);
                sumNearest += best;
            }

            // ② 首带半径：验证 HALCON measure region 中心是否落在引导圆边界上
            var h0 = BandCenter(halconBands[0]);
            var w0 = BandCenter(localBands[0]);
            double rHalcon = (h0 - circle.Center).Length;
            double rLocal = (w0 - circle.Center).Length;

            // ③ 首条相位差（0 = 格点 / step/2 = 半格 / 其它 = 固定偏移）
            double phiH = Math.Atan2(h0.Y - circle.Center.Y, h0.X - circle.Center.X);
            double phiW = Math.Atan2(w0.Y - circle.Center.Y, w0.X - circle.Center.X);
            double step = twoPi / parameters.NumMeasures;
            double delta = ((phiH - phiW) % twoPi + twoPi) % twoPi;

            // ④ HALCON 相邻带中心角差：判断编号方向与步长（排除"顺序相反"导致的巨大偏差）
            string stepInfo = "n/a";
            if (halconBands.Length >= 2)
            {
                var h1 = BandCenter(halconBands[1]);
                double phiH1 = Math.Atan2(h1.Y - circle.Center.Y, h1.X - circle.Center.X);
                double d01 = ((phiH1 - phiH) % twoPi + twoPi) % twoPi;
                stepInfo = $"相邻带角差={d01:F4}(step={step:F4}) 编号方向={(d01 <= Math.PI ? "屏角增" : "屏角减")}";
            }

            // ⑤ 首带两条边长在径向/切向的投影（验证 MeasureLength1/2 方向映射）
            var radial = new Vector(h0.X - circle.Center.X, h0.Y - circle.Center.Y);
            if (radial.Length > 1e-9) radial.Normalize();
            var tangential = new Vector(-radial.Y, radial.X);
            string edgeInfo = "n/a";
            if (halconBands[0].Length >= 3)
            {
                var e1 = halconBands[0][1] - halconBands[0][0];
                var e2 = halconBands[0][2] - halconBands[0][1];
                edgeInfo = $"边1={e1.Length:F1}(径向{Math.Abs(e1 * radial):F1}/切向{Math.Abs(e1 * tangential):F1}) " +
                           $"边2={e2.Length:F1}(径向{Math.Abs(e2 * radial):F1}/切向{Math.Abs(e2 * tangential):F1}) " +
                           $"[L1*2={parameters.MeasureLength1 * 2:F1} L2*2={parameters.MeasureLength2 * 2:F1}]";
            }

            string endpoints = "整圆";
            if (parameters.IsArc)
            {
                var arc = new Arc2d(circle.Center, circle.Radius, parameters.StartAngle, parameters.EndAngle, parameters.IsMajorArc);
                var startPt = arc.GetPointOnArc(arc.StartAngle);
                var endPt = arc.GetPointOnArc(arc.StartAngle + arc.AngularSpan);
                var lastH = BandCenter(halconBands[halconBands.Length - 1]);
                endpoints = $"弧首带距起点={(h0 - startPt).Length:F2} 末带距终点={(lastH - endPt).Length:F2} " +
                            $"(半带长={parameters.MeasureLength2:F2}, 期望: 0=中心贴端点 / 半带长=边贴端点)";
            }

            parameters.LogInfo(
                $"[Calib] 卡尺定标: 模式={(parameters.IsArc ? "弧" : "整圆")} 数量=HALCON {halconBands.Length}/本地 {localBands.Length} | " +
                $"最近邻偏差 max={maxNearest:F3} avg={(halconBands.Length > 0 ? sumNearest / halconBands.Length : 0):F3} px | " +
                $"首带半径 HALCON={rHalcon:F2} 本地={rLocal:F2} 引导R={circle.Radius:F2} | " +
                $"首条相位 delta={delta:F4} (step={step:F4}, step/2={step / 2:F4}) | {stepInfo} | {endpoints} | {edgeInfo}");
        }

        /// <summary>
        /// 测量带轮廓的中心（包围盒中心）。
        /// ★ 不能用"顶点均值"：HALCON 返回的是闭合轮廓（首点会重复一次），
        ///   均值会被重复的角点带偏 √(L1²+L2²)/5 —— 实测正好 7.62px，会伪装成"卡尺对不齐"。
        /// </summary>
        private static Point BandCenter(System.Windows.Point[] pts)
        {
            if (pts == null || pts.Length == 0) return new Point(0, 0);

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var p in pts)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            return new Point((minX + maxX) / 2, (minY + maxY) / 2);
        }

        /// <summary>
        /// 填充结果（拟合圆/圆心/测量点）
        /// </summary>
        private void FillResult(CircleDetectResults result, CircleDetectParameters parameters,
            HTuple allParam, HTuple rms, HTuple numMeasures,
            HTuple objectIndex, HMetrologyModel metrologyModel)
        {
            // RMSE 和边缘点数（HTuple 长度检查，避免空结果访问）
            result.RMSE = rms.Length > 0 ? rms[0].D : 0;
            result.EdgePointCount = numMeasures.Length > 0 ? numMeasures[0].I : 0;

            try
            {
                // all_param = [Row, Col, Radius]
                if (allParam.Length >= 3)
                {
                    var center = new Point(allParam[1].D, allParam[0].D);
                    result.CenterPoint = center;

                    if (parameters.IsArc)
                    {
                        // ★ 补 isMajorArc：4 参构造恒为小弧，与检测弧的大/小弧不一致导致结果显示失真
                        result.Arc = new[] { new Arc2d(
                            new Point(center.X, center.Y), allParam[2].D,
                            parameters.StartAngle, parameters.EndAngle,
                            parameters.IsMajorArc) };
                    }
                    else
                    {
                        result.Circle = new[] { new Circle2d(center.X, center.Y, allParam[2].D) };
                    }
                }

                // 轮廓点（used_edges → 实际参与拟合的边缘点，主显示）
                result.MeasurementPoints = ExtractMeasurementPoints(objectIndex, metrologyModel);

                // 诊断检测点（GetMetrologyObjectMeasures → 检测到的全部边缘点，与拟合结果无关，
                // 默认不显示，Tab3 勾选后用于定位拟合失败原因）
                result.DiagnosticPoints = ExtractDiagnosticPoints(
                    objectIndex, metrologyModel, TransitionToHalcon(parameters.Transition));

                // HALCON 实际测量带（MeasureContour XLD 轮廓 → 四边形点集，诊断卡尺对齐用，默认不显示）
                result.HalconMeasureBands = ExtractHalconMeasureBands(
                    objectIndex, metrologyModel, TransitionToHalcon(parameters.Transition));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"解析 Halcon 圆结果失败: {ex.Message}", ex);
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
        /// 自算拟合残差 RMSE。
        /// HALCON 中 rms 既不是 get_metrology_object_param 的合法 GenParamName（#1303），
        /// 也不是 get_metrology_object_result 的合法 result_type（#1305），必须自行计算。
        /// 参考官方 measure_stamping_part 示例：用 used_edges 取实际参与拟合的边缘点，
        /// 计算每个点到拟合圆（|点到圆心| − 半径）的距离，取均方根。
        /// </summary>
        /// <param name="metrologyModel">计量模型</param>
        /// <param name="objectIndex">测量对象索引</param>
        /// <param name="allParam">拟合参数 [Row, Col, Radius]</param>
        /// <returns>RMSE 值（无拟合点时为 0）</returns>
        private static HTuple ComputeRmsFromUsedEdges(HMetrologyModel metrologyModel, int objectIndex, HTuple allParam)
        {
            try
            {
                if (allParam.Length < 3)
                    return new HTuple(0.0);

                var idx = new HTuple(objectIndex);
                var usedRow = metrologyModel.GetMetrologyObjectResult(
                    idx, new HTuple("all"), new HTuple("used_edges"), new HTuple("row"));
                var usedCol = metrologyModel.GetMetrologyObjectResult(
                    idx, new HTuple("all"), new HTuple("used_edges"), new HTuple("column"));
                if (usedRow.Length == 0)
                    return new HTuple(0.0);

                var cRow = allParam[0].D;
                var cCol = allParam[1].D;
                var radius = allParam[2].D;

                double sumSq = 0;
                for (int i = 0; i < usedRow.Length; i++)
                {
                    var dist = Math.Sqrt(Math.Pow(usedRow[i].D - cRow, 2) + Math.Pow(usedCol[i].D - cCol, 2)) - radius;
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
