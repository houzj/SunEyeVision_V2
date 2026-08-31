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
                        // 用真实两端点（对齐 Halcon add_metrology_object_line_measure 的 Start/End 语义）
                        var startP = parameters.LineStartPoint;
                        var endP = parameters.LineEndPoint;
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

                // ★ 修复：rms 不是 get_metrology_object_result 的合法 result_type（#1305），
                //   也不是 get_metrology_object_param 的合法 GenParamName（#1303），必须自算。
                //   num_measures 是 get_metrology_object_param 的合法 GenParamName。
                var rms = ComputeRmsFromUsedEdges(metrologyModel, objectIndex.I, allParam, parameters.GeometryType);

                // 获取测量带数量（num_measures 是 get_metrology_object_param 的合法 GenParamName）
                var numMeasures = metrologyModel.GetMetrologyObjectParam(objectIndex.I, "num_measures");

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
                    var lineLen = (parameters.LineEndPoint - parameters.LineStartPoint).Length;
                    if (lineLen < 1e-6)
                    {
                        errorMsg = "引导直线未设置或长度无效，请先绘制引导直线";
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
        /// 构建 Halcon 通用参数名（产品决策：只支持数量主导；圆/圆弧带 start_phi/end_phi）
        /// </summary>
        private HTuple BuildGenParamName(EdgeDetectParameters parameters)
        {
            var names = new[]
            {
                "num_measures",
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
        /// 构建 Halcon 通用参数值（数量主导：num_measures 必须传 int，避免 HALCON #1210 类型错误）
        /// </summary>
        private HTuple BuildGenParamValue(EdgeDetectParameters parameters)
        {
            var values = new object[]
            {
                parameters.NumMeasures,             // int → num_measures
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
        /// 自算拟合残差 RMSE（按几何类型分型）。
        /// HALCON 中 rms 既不是 get_metrology_object_result 的合法 result_type（#1305），
        /// 也不是 get_metrology_object_param 的合法 GenParamName（#1303），必须自行计算。
        /// 参考官方 measure_stamping_part 示例：用 used_edges 取实际参与拟合的边缘点，
        /// 直线：点到直线距离（法向量点积）；圆/弧：点到圆心距离 − 半径。
        /// </summary>
        /// <param name="metrologyModel">计量模型</param>
        /// <param name="objectIndex">测量对象索引</param>
        /// <param name="allParam">拟合参数（直线 [Row1,Col1,Row2,Col2,Nr,Nc,Dist]；圆 [Row,Col,Radius]）</param>
        /// <param name="geometryType">几何类型（Line / Circle / Arc）</param>
        /// <returns>RMSE 值（无拟合点时为 0）</returns>
        private static HTuple ComputeRmsFromUsedEdges(HMetrologyModel metrologyModel, int objectIndex,
            HTuple allParam, GeometryType geometryType)
        {
            try
            {
                var idx = new HTuple(objectIndex);
                var usedRow = metrologyModel.GetMetrologyObjectResult(
                    idx, new HTuple("all"), new HTuple("used_edges"), new HTuple("row"));
                var usedCol = metrologyModel.GetMetrologyObjectResult(
                    idx, new HTuple("all"), new HTuple("used_edges"), new HTuple("column"));
                if (usedRow.Length == 0)
                    return new HTuple(0.0);

                double sumSq = 0;
                if (geometryType == GeometryType.Line)
                {
                    if (allParam.Length < 7) return new HTuple(0.0);
                    var r1 = allParam[0].D;   // Row1（线上一点）
                    var c1 = allParam[1].D;   // Col1
                    var nr = allParam[4].D;   // 单位法向量分量 Nr
                    var nc = allParam[5].D;   // 单位法向量分量 Nc
                    for (int i = 0; i < usedRow.Length; i++)
                    {
                        // 点到直线距离 = |Nr·(r−Row1) + Nc·(c−Col1)|（(Row1,Col1) 在直线上）
                        var dist = nr * (usedRow[i].D - r1) + nc * (usedCol[i].D - c1);
                        sumSq += dist * dist;
                    }
                }
                else
                {
                    if (allParam.Length < 3) return new HTuple(0.0);
                    var cRow = allParam[0].D;
                    var cCol = allParam[1].D;
                    var radius = allParam[2].D;
                    for (int i = 0; i < usedRow.Length; i++)
                    {
                        var dist = Math.Sqrt(Math.Pow(usedRow[i].D - cRow, 2) + Math.Pow(usedCol[i].D - cCol, 2)) - radius;
                        sumSq += dist * dist;
                    }
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
