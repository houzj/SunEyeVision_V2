using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.SDK.Validation;
using WpfPoint = System.Windows.Point;
using BlobRuntime = SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Tool.BlobAnalysis
{
    /// <summary>现行SDK入口和显示适配；算法仅由独立Runtime执行。</summary>
[Tool(
        id: "BlobAnalysis",
        displayName: "Blob分析",
        Description = "连通域分析与特征提取",
        Icon = "blob", Order = 10,
        Category = SunEyeVision.Plugin.SDK.Core.ToolCategories.Location,
        Version = "3.1.0",
        RuntimeComponentId = "SunEyeVision.Tool.BlobAnalysis.Runtime",
        HasDebugWindow = true
    )]
    public class BlobAnalysisTool : IToolPlugin<BlobAnalysisParameters, BlobAnalysisResults>, IImageFramePolicyProvider
    {
        ImageFramePolicy IImageFramePolicyProvider.ImageFramePolicy => ImageFramePolicy.PreserveCoordinates;
        private readonly ToolAttribute? _attribute;
        public BlobAnalysisTool() => _attribute = GetType().GetCustomAttribute<ToolAttribute>();

        /// <summary>Compatibility entry only. All Blob algorithm work is delegated to the neutral runtime.</summary>
        public BlobAnalysisResults Run(BlobAnalysisParameters parameters)
        {
            var result = new BlobAnalysisResults {
                ToolName = _attribute?.DisplayName ?? "BlobAnalysis", ToolId = _attribute?.Id ?? "BlobAnalysis", Timestamp = DateTime.Now
            };
            var elapsed = Stopwatch.StartNew();
            try
            {
                var validation = ValidateParameters(parameters);
                if (!validation.IsValid) { result.SetError("参数验证失败: " + string.Join(", ", validation.Errors)); return result; }
                using var resources = new FrozenOutputResources();
                var input = BlobInputAdapter.CaptureImage(parameters,resources);
                // Current SDK geometry bindings are resolved explicitly at the input boundary.
                // The neutral kernel receives the resulting same-frame regions and must not transform them twice.
                var correction = BlobInputAdapter.ResolveCorrection(parameters);
                var inspection = BlobInputAdapter.ResolveRegion(parameters,false,input,resources,correction);
                var mask = BlobInputAdapter.ResolveRegion(parameters,true,input,resources,correction);
                var settings = BlobSdkAdapter.Settings(parameters);
                result.RuntimeOutput = BlobSdkAdapter.Execute(parameters, settings, input, resources, inspection, mask);
                BlobSdkAdapter.CopyResult(result.RuntimeOutput, result, settings, input.Width, input.Height);
                BuildDisplay(parameters, result);
                if(parameters.InspectionMode!=RegionFieldMode.InheritedRegion)
                    ToolRegionHelper.ExtractInspectionGeometries(result, parameters.InspectionRois?.Rois, parameters.NodeResults, correction);
                if(parameters.MaskMode!=RegionFieldMode.InheritedRegion)
                    ToolRegionHelper.ExtractMaskGeometries(result, parameters.MaskRois?.Rois, parameters.NodeResults, correction);
                result.SetSuccess(elapsed.Elapsed.TotalMilliseconds);
                parameters.LogInfo($"Blob运行层执行完成：{result.FilteredBlobCount}个，面积{result.FilteredTotalArea:G}，{elapsed.Elapsed.TotalMilliseconds:F1}ms");
            }
            catch (Exception failure)
            {
                try { result.ClearPartialOutputs(); } catch (Exception cleanup) { failure = new AggregateException(failure, cleanup); }
                parameters?.LogError("Blob分析处理异常: " + failure.Message, failure);
                result.SetError("处理失败: " + failure.Message, failure);
                result.ExecutionTimeMs = elapsed.Elapsed.TotalMilliseconds;
            }
            return result;
        }

        public ValidationResult ValidateParameters(BlobAnalysisParameters parameters)
            => parameters is null ? ValidationResult.Failure("参数不能为空") : parameters.Validate();

        private static void BuildDisplay(BlobAnalysisParameters parameters, BlobAnalysisResults result)
        {
            var display = result.Display;
            display.Clear();

            // 轮廓：仅对有轮廓数据的 blob（通过 Tab4 判断的 blob 有轮廓）
            var displayBlobs = result.FilteredBlobs;
            var contourBlobs = displayBlobs
                .Where(b => b.HasContour)
                .ToList();
            var contourStyle = parameters.ResultPresentation.GetGraphicsStyle("contours");
            // 可见性由渲染层按样式控制，执行期不过滤 —— 否则"隐藏后执行、再显示"时几何根本不存在
            if (parameters.OutputContours && contourBlobs.Count > 0)
            {
                var okContours = contourBlobs
                    .Where(b => b.JudgmentPassed)
                    .ToList();
                var ngContours = contourBlobs
                    .Where(b => !b.JudgmentPassed)
                    .ToList();
                display.AddContourBatch(contourStyle, okContours.Select(b => b.Contour!).ToList(), true,
                    sourceElements: okContours.Select(b => new DeclaredElementAddress(BlobOutputContract.BlobsPortId, b.ElementId, new[] { "contour" })).ToList());
                display.AddContourBatch(contourStyle, ngContours.Select(b => b.Contour!).ToList(), false,
                    sourceElements: ngContours.Select(b => new DeclaredElementAddress(BlobOutputContract.BlobsPortId, b.ElementId, new[] { "contour" })).ToList());
            }

            // 外接矩形（根据 JudgmentPassed 显示 OK/NG 颜色）
            // 可见性由渲染层按样式控制，执行期不过滤 —— 否则"隐藏后执行、再显示"时几何根本不存在
            var blobStyle = parameters.ResultPresentation.GetGraphicsStyle("blobs");
            var okBoxes = displayBlobs.Where(b => b.JudgmentPassed && b.RotatedBoundingBox is { Length: >= 4 })
                .ToList();
            var ngBoxes = displayBlobs.Where(b => !b.JudgmentPassed && b.RotatedBoundingBox is { Length: >= 4 })
                .ToList();
            display.AddContourBatch(blobStyle, okBoxes.Select(b => b.RotatedBoundingBox!).ToList(), true,
                sourceElements: okBoxes.Select(b => new DeclaredElementAddress(BlobOutputContract.BlobsPortId, b.ElementId, new[] { "rotated-bounds" })).ToList());
            display.AddContourBatch(blobStyle, ngBoxes.Select(b => b.RotatedBoundingBox!).ToList(), false,
                sourceElements: ngBoxes.Select(b => new DeclaredElementAddress(BlobOutputContract.BlobsPortId, b.ElementId, new[] { "rotated-bounds" })).ToList());

            // 质心十字（根据 JudgmentPassed 显示 OK/NG 颜色）
            var centroidStyle = parameters.ResultPresentation.GetGraphicsStyle("centroids");
            foreach (var blob in displayBlobs)
            {
                if (blob.CentroidX > 0 || blob.CentroidY > 0)
                    display.AddCross(centroidStyle,
                        new WpfPoint(blob.CentroidX, blob.CentroidY), blob.JudgmentPassed);
            }

            // 面积文本（根据 JudgmentPassed 显示 OK/NG 颜色）
            var areaStyle = parameters.ResultPresentation.GetTextStyle("area");
            foreach (var blob in displayBlobs)
            {
                if (blob.CentroidX > 0 || blob.CentroidY > 0)
                    display.AddText(areaStyle, areaStyle.PositionX, areaStyle.PositionY,
                        $"{blob.Label}: {blob.Area:F0}px", blob.JudgmentPassed);
            }
        }
    }
}
