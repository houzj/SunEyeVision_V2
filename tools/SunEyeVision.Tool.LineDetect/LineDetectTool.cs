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
    ///   4. 提取边缘点 → 屏蔽过滤 → 共同直线方程拟合与真实RMSE
    /// </remarks>
[Tool(
        id: "LineDetect",
        displayName: "直线查找",
        Description = "直线边缘检测工具（引导式 2D Metrology）",
        Icon = "line", Order = 20,
        Category = SunEyeVision.Plugin.SDK.Core.ToolCategories.Location,
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    [JsonDerivedType(typeof(LineDetectParameters), "LineDetect")]
    public class LineDetectTool : IToolPlugin<LineDetectParameters, LineDetectResults>, IToolEditorFactory
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
                result.ImageWidth = (int)width.L;
                result.ImageHeight = (int)height.L;
                metrologyModel.SetMetrologyModelImageSize((int)width.L, (int)height.L);
                parameters.LogInfo($"[Diag] 图像尺寸: {width.L} x {height.L}");

                // 位置修正：引导直线按矩阵变换到当前图像坐标（卡尺作用到修正后位置）
                var startP = parameters.LineStartPoint;
                var endP = parameters.LineEndPoint;
                if (parameters.IsPositionCorrectionEnabled &&
                    PositionCorrectionResolver.Resolve(parameters) is { } matrix)
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

                // HALCON取点 → 可选屏蔽 → HALCON轮廓拟合；不读取Metrology模型作为成功门槛。
                bool hasEnabledMask = parameters.GetRoisByType(RoiParameterMode.MaskRoi).Any();
                FillResultWithMask(result, parameters, objectIndex, metrologyModel,
                    TransitionToHalcon(parameters.Transition), hasEnabledMask, startP, endP);

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
                    parameters.NodeResults, PositionCorrectionResolver.Resolve(parameters));

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
                    parameters.LogWarning("[Diag] 结果判断为 NG，请检查已启用的匹配点数、角度和拟合误差范围。");
                }

                result.SetSuccess();
                parameters.LogInfo(
                    $"直线查找完成: 边缘点数={result.EdgePointCount}, RMSE={result.RMSE:F3}px, 判定={isOk}");
            }
            catch (Exception ex)
            {
                parameters.LogError($"直线查找处理异常: {ex.Message}", ex);
                result.SetError($"处理失败: {ex.Message}", ex);
                parameters.LogWarning(result.FitDiagnostics1.Summary);
                try { result.FillFailureDisplay(parameters); }
                catch (Exception displayEx) { parameters.LogWarning($"失败诊断绘制异常：{displayEx.Message}"); }
            }

            return result;
        }

        /// <summary>
        /// 结果判断（匹配点数/角度/拟合误差，任一不满足判 NG）
        /// </summary>
        private static bool EvaluateResult(LineDetectResults result, LineDetectParameters parameters)
        {
            var outcomes = ResultJudgmentEvaluator.EvaluateRanges(parameters.ResultPresentation.Judgments, key => key switch
            {
                "edge-point-count" => result.EdgePointCount,
                "angle" => result.Angle,
                "rmse" => result.RMSE,
                _ => null,
            });
            return ResultJudgmentEvaluator.IsAllPass(outcomes);
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


        private void FillResultWithMask(LineDetectResults result, LineDetectParameters parameters,
            HTuple objectIndex, HMetrologyModel model, string transition, bool hasEnabledMask,
            Point guideStart, Point guideEnd)
        {
            var diagnostic = result.FitDiagnostics1;
            diagnostic.AlgorithmPath = "HALCON取点＋轮廓拟合（不设卡尺支持率门槛）";
            var points = MeasuredLineFitting.Capture(model, objectIndex, transition, diagnostic, "直线");
            result.DiagnosticPoints = points;
            result.HalconMeasureBands = diagnostic.Bands;
            try
            {
                diagnostic.Stage = hasEnabledMask ? "直线屏蔽过滤" : "直线取点校验";
                var kept = points;
                if (hasEnabledMask)
                {
                    var filtered = ToolRegionHelper.FilterPointsOutOfMaskRois(points,
                        parameters.GetRoisByType(RoiParameterMode.MaskRoi), PositionCorrectionResolver.Resolve(parameters), parameters.NodeResults);
                    kept = filtered.Kept;
                    diagnostic.ExcludedPoints = filtered.Excluded;
                    result.ExcludedPoints = filtered.Excluded;
                }
                diagnostic.CandidatePoints = kept;
                var (start, end, inliers, rmse) = MeasuredLineFitting.Fit(
                    kept, parameters.DistanceThreshold, guideStart, guideEnd, diagnostic, "直线");
                result.LineStartPoint = start;
                result.LineEndPoint = end;
                result.CenterPoint = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
                result.Line = new[] { Line2d.FromTwoPoints(start, end) };
                result.Angle = ComputeLineAngle(start, end);
                result.RMSE = rmse;
                result.EdgePointCount = inliers.Length;
                result.MeasurementPoints = inliers;
                parameters.LogInfo(diagnostic.Summary);
            }
            catch (Exception ex)
            {
                diagnostic.Error = ex.Message;
                throw;
            }
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
