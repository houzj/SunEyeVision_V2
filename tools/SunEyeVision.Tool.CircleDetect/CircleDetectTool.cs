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
                HTuple objectIndex = metrologyModel.AddMetrologyObjectCircleMeasure(
                    guideCenter.Y,
                    guideCenter.X,
                    guideRadius,
                    parameters.MeasureLength1, parameters.MeasureLength2,
                    parameters.Sigma, parameters.Threshold,
                    BuildGenParamName(parameters), BuildGenParamValue(parameters));

                // 执行测量（逐测量带取点 + 拟合）
                metrologyModel.ApplyMetrologyModel(hImage);

                // 获取拟合圆参数（all_param = [Row, Col, Radius]）
                var allParam = metrologyModel.GetMetrologyObjectResult(
                    objectIndex.I, "all", "result_type", "all_param");

                // ★ 修复：rms / num_measures 不是 get_metrology_object_result 的
                //   result_type 合法值（会导致 HALCON #1305），须用 get_metrology_object_param 查询。
                var rms = metrologyModel.GetMetrologyObjectParam(objectIndex.I, "rms");

                // 获取测量带数量
                var numMeasures = metrologyModel.GetMetrologyObjectParam(objectIndex.I, "num_measures");

                // ===== 结果填充 =====
                FillResult(result, parameters, allParam, rms, numMeasures, objectIndex, metrologyModel);

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
        /// 构建 Halcon 通用参数名（圆/圆弧带 start_phi/end_phi）
        /// </summary>
        private HTuple BuildGenParamName(CircleDetectParameters parameters)
        {
            return new HTuple(new[]
            {
                parameters.MeasureDistance > 0 ? "measure_distance" : "num_measures",
                "measure_transition",
                "measure_select",
                "start_phi",
                "end_phi",
            });
        }

        /// <summary>
        /// 构建 Halcon 通用参数值
        /// </summary>
        private HTuple BuildGenParamValue(CircleDetectParameters parameters)
        {
            // ★ 修复：间距模式传 double（measure_distance），数量模式必须传 int（num_measures）。
            //   原实现用三元表达式，数量模式下 int 被提升为 double，导致 HALCON #1210 类型错误。
            var paramValues = new object[]
            {
                TransitionToHalcon(parameters.Transition),
                SelectToHalcon(parameters.Select),
                parameters.StartAngle,
                parameters.EndAngle,
            };

            var values = new object[paramValues.Length + 1];
            values[0] = parameters.MeasureDistance > 0
                ? (object)parameters.MeasureDistance   // double → measure_distance
                : (object)parameters.NumMeasures;     // int    → num_measures
            Array.Copy(paramValues, 0, values, 1, paramValues.Length);

            return new HTuple(values);
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
                        result.Arc = new[] { new Arc2d(
                            center.X, center.Y, allParam[2].D,
                            parameters.StartAngle, parameters.EndAngle) };
                    }
                    else
                    {
                        result.Circle = new[] { new Circle2d(center.X, center.Y, allParam[2].D) };
                    }
                }

                // 轮廓点（每个卡尺提取的拟合点，用于拟合）
                result.MeasurementPoints = ExtractMeasurementPoints(objectIndex, metrologyModel);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"解析 Halcon 圆结果失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 提取测量点（get_metrology_object_measures → 每个卡尺的拟合点）
        /// </summary>
        private Point2d[] ExtractMeasurementPoints(HTuple objectIndex, HMetrologyModel metrologyModel)
        {
            try
            {
                metrologyModel.GetMetrologyObjectMeasures(objectIndex, "all", out var row, out var col);
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
