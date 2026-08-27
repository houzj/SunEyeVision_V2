using System;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Tool.EdgeDetect.Views;

namespace SunEyeVision.Tool.EdgeDetect
{
    /// <summary>
    /// 边缘检测工具
    /// </summary>
    /// <remarks>
    /// 算法流程（Halcon 2D Metrology）：
    ///   1. create_metrology_model → 创建计量模型
    ///   2. add_metrology_object_line/circle_measure → 添加测量对象（自动生成测量带阵列）
    ///   3. apply_metrology_model → 执行测量（逐测量带取点 + 拟合）
    ///   4. get_metrology_object_result → 获取拟合几何参数 + RMSE
    /// </remarks>
    [Tool(
        id: "EdgeDetect",
        displayName: "边缘检测",
        Description = "直线/圆/圆弧边缘检测工具（引导式 2D Metrology）",
        Icon = "📐",
        Category = "定位工具",
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class EdgeDetectTool : IToolPlugin<EdgeDetectParameters, EdgeDetectResults>
    {
        /// <summary>
        /// 执行工具
        /// </summary>
        public EdgeDetectResults Run(EdgeDetectParameters parameters)
        {
            var result = new EdgeDetectResults
            {
                ToolName = "边缘检测",
                ToolId = "EdgeDetect",
                Timestamp = DateTime.Now,
            };

            // ===== 参数校验（Fail-Fast）=====
            var guideValid = ValidateGuide(parameters, out var errorMsg);
            if (!guideValid)
            {
                parameters.LogError($"参数校验失败: {errorMsg}");
                result.SetError($"参数校验失败: {errorMsg}");
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

            // ===== Halcon 2D Metrology 执行 =====
            // 使用 HMetrologyModel 封装类（using 自动释放句柄，对应 close_metrology_model）
            using var metrologyModel = new HMetrologyModel();
            metrologyModel.CreateMetrologyModel();

            try
            {
                HOperatorSet.GetImageSize(hImage, out var width, out var height);
                metrologyModel.SetMetrologyModelImageSize((int)width.L, (int)height.L);

                HTuple objectIndex;
                switch (parameters.GeometryType)
                {
                    case GeometryType.Line:
                        var startP = parameters.GuideLine!.Value.Point;
                        var endP = parameters.GuideLine.Value.GetPointAt(1.0);
                        objectIndex = metrologyModel.AddMetrologyObjectLineMeasure(
                            startP.Y, startP.X, endP.Y, endP.X,
                            parameters.MeasureLength1, parameters.MeasureLength2,
                            parameters.Sigma, parameters.Threshold,
                            BuildGenParamName(parameters), BuildGenParamValue(parameters));
                        break;

                    default: // Circle / Arc
                        objectIndex = metrologyModel.AddMetrologyObjectCircleMeasure(
                            parameters.GuideCircle!.Value.Center.Y,
                            parameters.GuideCircle.Value.Center.X,
                            parameters.GuideCircle.Value.Radius,
                            parameters.MeasureLength1, parameters.MeasureLength2,
                            parameters.Sigma, parameters.Threshold,
                            BuildGenParamName(parameters), BuildGenParamValue(parameters));
                        break;
                }

                // 执行测量（逐测量带取点 + 拟合）
                metrologyModel.ApplyMetrologyModel(hImage);

                // 获取拟合几何参数
                var allParam = metrologyModel.GetMetrologyObjectResult(
                    objectIndex.I, "all", "result_type", "all_param");

                // 获取拟合残差 RMSE
                var rms = metrologyModel.GetMetrologyObjectResult(
                    objectIndex.I, "all", "result_type", "rms");

                // 获取测量带数量
                var numMeasures = metrologyModel.GetMetrologyObjectResult(
                    objectIndex.I, "all", "result_type", "num_measures");

                // ===== 结果填充 =====
                FillResult(result, parameters, allParam, rms, numMeasures);

                result.SetSuccess();
                parameters.LogInfo(
                    $"边缘检测完成: 类型={parameters.GeometryType}, " +
                    $"边缘点数={result.EdgePointCount}, RMSE={result.RMSE:F3}px");
            }
            catch (Exception ex)
            {
                parameters.LogError($"边缘检测处理异常: {ex.Message}", ex);
                result.SetError($"处理失败: {ex.Message}", ex);
            }

            return result;
        }

        /// <summary>
        /// 校验引导几何有效性
        /// </summary>
        private bool ValidateGuide(EdgeDetectParameters parameters, out string errorMsg)
        {
            switch (parameters.GeometryType)
            {
                case GeometryType.Line:
                    if (parameters.GuideLine == null)
                    {
                        errorMsg = "引导直线未设置，请先绘制引导直线";
                        return false;
                    }
                    break;

                case GeometryType.Circle:
                case GeometryType.Arc:
                    if (parameters.GuideCircle == null)
                    {
                        errorMsg = $"引导圆未设置，请先绘制引导{(parameters.GeometryType == GeometryType.Circle ? "圆" : "圆弧")}";
                        return false;
                    }
                    if (parameters.GuideCircle.Value.Radius <= 0)
                    {
                        errorMsg = "引导圆半径必须大于 0";
                        return false;
                    }
                    break;
            }

            errorMsg = string.Empty;
            return true;
        }

        /// <summary>
        /// 构建 Halcon 通用参数名（measure_distance 与 num_measures 互斥，间距优先）
        /// </summary>
        private HTuple BuildGenParamName(EdgeDetectParameters parameters)
        {
            var names = new[]
            {
                parameters.MeasureDistance > 0 ? "measure_distance" : "num_measures",
                "measure_transition",
                "measure_select",
            };
            if (parameters.GeometryType != GeometryType.Line)
            {
                Array.Resize(ref names, names.Length + 2);
                names[^2] = "start_phi";
                names[^1] = "end_phi";
            }
            return new HTuple(names);
        }

        /// <summary>
        /// 构建 Halcon 通用参数值
        /// </summary>
        private HTuple BuildGenParamValue(EdgeDetectParameters parameters)
        {
            var values = new object[]
            {
                parameters.MeasureDistance > 0 ? parameters.MeasureDistance : parameters.NumMeasures,
                TransitionToHalcon(parameters.Transition),
                SelectToHalcon(parameters.Select),
            };
            if (parameters.GeometryType != GeometryType.Line)
            {
                Array.Resize(ref values, values.Length + 2);
                values[^2] = parameters.StartAngle;
                values[^1] = parameters.EndAngle;
            }
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
        /// 填充结果
        /// </summary>
        private void FillResult(EdgeDetectResults result, EdgeDetectParameters parameters,
            HTuple allParam, HTuple rms, HTuple numMeasures)
        {
            // RMSE 和边缘点数（HTuple 长度检查，避免空结果访问）
            result.RMSE = rms.Length > 0 ? rms[0].D : 0;
            result.EdgePointCount = numMeasures.Length > 0 ? numMeasures[0].I : 0;

            try
            {
                switch (parameters.GeometryType)
                {
                    case GeometryType.Line:
                        // all_param = [Row1, Col1, Row2, Col2, Nr, Nc, Dist]
                        if (allParam.Length >= 4)
                        {
                            var start = new Point(allParam[1].D, allParam[0].D);
                            var end = new Point(allParam[3].D, allParam[2].D);
                            result.Line = new[] { Line2d.FromTwoPoints(start, end) };
                        }
                        break;

                    case GeometryType.Circle:
                        // all_param = [Row, Col, Radius]
                        if (allParam.Length >= 3)
                        {
                            result.Circle = new[] { new Circle2d(allParam[1].D, allParam[0].D, allParam[2].D) };
                        }
                        break;

                    case GeometryType.Arc:
                        // all_param = [Row, Col, Radius]，起止角来自参数
                        if (allParam.Length >= 3)
                        {
                            result.Arc = new[] { new Arc2d(
                                allParam[1].D, allParam[0].D, allParam[2].D,
                                parameters.StartAngle, parameters.EndAngle) };
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"解析 Halcon 几何结果失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 创建调试控件
        /// </summary>
        public System.Windows.FrameworkElement? CreateDebugControl(IToolPlugin tool)
        {
            return new EdgeDetectToolDebugControl();
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
