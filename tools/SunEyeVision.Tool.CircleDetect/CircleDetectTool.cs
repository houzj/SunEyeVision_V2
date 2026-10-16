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
        Icon = "circle", Order = 30,
        Category = SunEyeVision.Plugin.SDK.Core.ToolCategories.Location,
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    [JsonDerivedType(typeof(CircleDetectParameters), "CircleDetect")]
    public class CircleDetectTool : IToolPlugin<CircleDetectParameters, CircleDetectResults>, IToolEditorFactory
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
                var guideMatrix = PositionCorrectionResolver.Resolve(parameters);
                HOperatorSet.GetImageSize(hImage, out var width, out var height);
                metrologyModel.SetMetrologyModelImageSize((int)width.L, (int)height.L);

                // 位置修正：引导圆按矩阵变换到当前图像坐标（圆心变换 + 半径缩放；
                // 弧为有向形状，起止角随矩阵旋转，与交互层 RenderArc 同口径，保证交互/执行/结果三层一致）
                var guideCenter = parameters.GuideCircle.Value.Center;
                var guideRadius = parameters.GuideCircle.Value.Radius;
                if (guideMatrix is { } matrix)
                {
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

                // ★ 日志埋点（诊断边缘极性/选取模式组合问题）：
                //   记录参数枚举 → HALCON 字符串映射 + 完整 genParam 名称/值 + 引导圆/图像信息
                var genParamName = BuildGenParamName(parameters);
                var genParamValue = BuildGenParamValue(parameters, guideDisplayArc);
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

                // HALCON取点 → 可选屏蔽 → HALCON轮廓拟合；不读取Metrology模型作为成功门槛。
                bool hasEnabledMask = parameters.GetRoisByType(RoiParameterMode.MaskRoi).Any();
                FillResultWithMask(result, parameters, objectIndex, metrologyModel, hasEnabledMask, guideDisplayArc, guideMatrix);

                // ===== 结果判断（对齐 VisionMaster：半径/中心X/中心Y/拟合误差/匹配点数） =====
                var isOk = EvaluateResult(result, parameters);
                result.IsPass = isOk;
                result.FillDisplay(isOk, parameters);

                // 屏蔽区几何输出（RoI 契约：结果树"输入/屏蔽区图形"填充，对齐图像工具）
                ToolRegionHelper.ExtractMaskGeometries(result, parameters.MaskRois?.Rois,
                    parameters.NodeResults, guideMatrix);

                // 卡尺输入回显（模块结果"输入/卡尺"）：引导线 + 测量带参数
                if (parameters.GuideCircle is { } guideCircle)
                {
                    result.GuideGeometry = new[]
                    {
                        new RoiShape { Geometry = guideCircle },
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
                parameters.LogWarning(result.FitDiagnostics1.Summary);
                try { result.FillFailureDisplay(parameters); }
                catch (Exception displayEx) { parameters.LogWarning($"失败诊断绘制异常：{displayEx.Message}"); }
            }

            return result;
        }

        /// <summary>
        /// 结果判断（半径/中心X/中心Y/拟合误差/匹配点数，任一不满足判 NG）
        /// </summary>
        private static bool EvaluateResult(CircleDetectResults result, CircleDetectParameters parameters)
        {
            var outcomes = ResultJudgmentEvaluator.EvaluateRanges(parameters.ResultPresentation.Judgments, key => key switch
            {
                "radius" => CircleRadius(result),
                "center-x" => result.CenterPoint.X,
                "center-y" => result.CenterPoint.Y,
                "rmse" => result.RMSE,
                "edge-point-count" => result.EdgePointCount,
                _ => null,
            });
            return ResultJudgmentEvaluator.IsAllPass(outcomes);
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
        private HTuple BuildGenParamValue(CircleDetectParameters parameters, Arc2d? displayArc = null)
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
                // HALCON normalizes arc endpoints; phase + 2π is not a safe closed-circle encoding.
                // A full circle has no orientation. Keep center/radius correction and canonical sampling.
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


        private void FillResultWithMask(CircleDetectResults result, CircleDetectParameters parameters,
            HTuple objectIndex, HMetrologyModel model, bool hasEnabledMask, Arc2d? displayArc, AffineMatrix? correction)
        {
            var diagnostic = result.FitDiagnostics1;
            diagnostic.AlgorithmPath = "HALCON取点＋轮廓拟合（不设卡尺支持率门槛）";
            var points = MeasuredLineFitting.Capture(model, objectIndex,
                TransitionToHalcon(parameters.Transition), diagnostic, "圆");
            result.DiagnosticPoints = points;
            result.HalconMeasureBands = diagnostic.Bands;
            try
            {
                diagnostic.Stage = hasEnabledMask ? "圆屏蔽过滤" : "圆有效点拟合准备";
                var kept = points;
                if (hasEnabledMask)
                {
                    var filtered = ToolRegionHelper.FilterPointsOutOfMaskRois(points,
                        parameters.GetRoisByType(RoiParameterMode.MaskRoi), correction, parameters.NodeResults);
                    kept = filtered.Kept;
                    diagnostic.ExcludedPoints = filtered.Excluded;
                    result.ExcludedPoints = filtered.Excluded;
                }
                diagnostic.CandidatePoints = kept;
                var (center, radius, inliers, rmse) = MeasuredCircleFitting.Fit(
                    kept, parameters.DistanceThreshold, diagnostic, "圆");
                result.CenterPoint = center;
                result.Center = new[] { new Point2d(center.X, center.Y) };
                result.RMSE = rmse;
                result.EdgePointCount = inliers.Length;
                result.MeasurementPoints = inliers;
                if (parameters.IsArc)
                {
                    result.Arc = new[] { new Arc2d(center, radius,
                        displayArc?.StartAngle ?? parameters.StartAngle,
                        displayArc?.EndAngle ?? parameters.EndAngle, parameters.IsMajorArc) };
                }
                else result.Circle = new[] { new Circle2d(center.X, center.Y, radius) };
                parameters.LogInfo(diagnostic.Summary);
            }
            catch (Exception ex) { diagnostic.Error = ex.Message; throw; }
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
