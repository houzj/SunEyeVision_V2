using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.Contracts;
using BlobRuntime = SunEyeVision.Tool.BlobAnalysis.Runtime;

namespace SunEyeVision.Tool.BlobAnalysis;

/// <summary>现行SDK设置/显示结果的边界适配，不含Blob算法、统计或判定的第二实现。</summary>
internal static class BlobSdkAdapter
{
    internal static BlobRuntime.BlobOutput Execute(BlobAnalysisParameters parameters, BlobRuntime.BlobSettings settings,
        IReadOnlyImage image, IOutputResources resources, IReadOnlyRegion? inspection, IReadOnlyRegion? mask)
    {
        var definition=new BlobRuntime.BlobDefinition();
        var values=new Dictionary<string,object?> { [BlobRuntime.BlobDefinition.ImageInput]=image };
        if(inspection is not null)values[BlobRuntime.BlobDefinition.InspectionInput]=inspection;
        if(mask is not null)values[BlobRuntime.BlobDefinition.MaskInput]=mask;
        var context=new SdkContext(parameters,resources);
        var invocation=new ToolInvocation<BlobRuntime.BlobSettings>(settings,new InputValues(definition.Describe(settings).Inputs,values),context,
            BlobOutputContract.ToRuntimeRequests(parameters.Context?.OutputRequests??Array.Empty<OutputValueRequest>()));
        var session=definition.CreateSession();
        try
        {
            // Runtime is synchronous so thread-local HALCON settings cannot move across an await.
            var outcome=session.ExecuteAsync(invocation).GetAwaiter().GetResult();
            if(!outcome.Success||outcome.Output is null)throw new InvalidOperationException(string.Join("; ",outcome.Diagnostics.Select(d=>d.Message)));
            foreach(var diagnostic in outcome.Diagnostics)parameters.LogWarning(diagnostic.Code+": "+diagnostic.Message);
            return outcome.Output;
        }
        finally { session.DisposeAsync().GetAwaiter().GetResult(); }
    }

    private sealed class SdkContext : IToolExecutionContext
    {
        private readonly BlobAnalysisParameters _parameters;
        internal SdkContext(BlobAnalysisParameters parameters,IOutputResources resources)
        {
            _parameters=parameters;Resources=resources;
            var context=parameters.Context;
            Identity=context?.ExecutionIdentity?.ToInvocationIdentity(context.NodeId??BlobRuntime.BlobDefinition.Id)
                ?? new RunIdentity(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),BlobRuntime.BlobDefinition.Id,Guid.NewGuid());
            Cancellation=context?.CancellationToken??default;
        }
        public RunIdentity Identity { get; }
        public CancellationToken Cancellation { get; }
        public IOutputResources Resources { get; }
        public void Log(string message)=>_parameters.LogInfo(message);
    }

    internal static BlobRuntime.BlobSettings Settings(BlobAnalysisParameters p) => new() {
        Threshold=p.Threshold, MaxValue=p.MaxValue, Type=(BlobRuntime.BlobThresholdMode)p.Type,
        IsPreprocessingEnabled=p.IsPreprocessingEnabled, PreprocessMethod=(BlobRuntime.BlobPreprocess)p.PreprocessMethodValue,
        PreprocessKernelSize=p.PreprocessKernelSize, IsMorphologyEnabled=p.IsMorphologyEnabled,
        MorphOperation=(BlobRuntime.BlobMorphology)p.MorphOperationValue, MorphElementShape=(BlobRuntime.BlobElementShape)p.MorphElementShapeValue,
        MorphElementWidth=p.MorphElementWidth, MorphElementHeight=p.MorphElementHeight, MorphIterations=p.MorphIterations,
        IsAreaFilterEnabled=p.IsAreaFilterEnabled, FilterAreaMin=p.FilterAreaMin, FilterAreaMax=p.FilterAreaMax,
        IsCircularityFilterEnabled=p.IsCircularityFilterEnabled, FilterCircularityMin=p.FilterCircularityMin, FilterCircularityMax=p.FilterCircularityMax,
        IsRectangularityFilterEnabled=p.IsRectangularityFilterEnabled, FilterRectangularityMin=p.FilterRectangularityMin, FilterRectangularityMax=p.FilterRectangularityMax,
        IsBlobCountFilterEnabled=p.IsBlobCountFilterEnabled, BlobCountLimit=p.BlobCountLimit, SortMode=(BlobRuntime.BlobSort)p.SortMode,
        OutputGrayImage=p.OutputBlobImage, OutputBinaryImage=p.OutputBinaryImage, OutputBoundary=p.OutputContours,
        Judgments=Array.AsReadOnly(p.ResultPresentation.Judgments.Select(j=>new BlobRuntime.BlobJudgment(j.Key,j.Scope==JudgmentScope.PerItem,j.Enabled,j.Min,j.Max,(BlobRuntime.BlobJudgmentKind)j.RuleKind)).ToArray())
    };

    internal static void CopyResult(BlobRuntime.BlobOutput source, BlobAnalysisResults target, BlobRuntime.BlobSettings settings, int width, int height)
    {
        if(source.GrayImage is { } gray)target.GrayImage=new HalImage(HalconResourceBridge.CopyImage(gray));
        if(source.BinaryImage is { } binary)target.BinaryMask=new HalImage(HalconResourceBridge.CopyImage(binary));
        using var reader=source.Region.AcquireReader();
        target.BlobRegions=new HalRegion(HalconResourceBridge.CopyRegion(source.Region,reader.SourceFrame,width,height));
        target.OkBlobRegions=new HalRegion(HalconResourceBridge.CopyRegion(source.OkRegion,reader.SourceFrame,width,height));
        target.NgBlobRegions=new HalRegion(HalconResourceBridge.CopyRegion(source.NgRegion,reader.SourceFrame,width,height));
        target.FilteredBlobs=source.Measurements.Select(b=>new BlobResult {
            ElementId=b.ElementId,Label=b.Label,Area=b.Area,Perimeter=b.Perimeter,CentroidX=b.CentroidX,CentroidY=b.CentroidY,
            Circularity=b.Circularity,Rectangularity=b.Rectangularity,JudgmentPassed=b.JudgmentPassed,
            BoundingBox=new Rect(b.Bounds.X,b.Bounds.Y,b.Bounds.Width,b.Bounds.Height),
            RotatedBoundingBox=b.RotatedBounds.Select(p=>new Point(p.X,p.Y)).ToArray(),
            // Current single-contour display projection is explicitly lossy: one outer path, no smoothing. Full topology stays in RuntimeOutput.
            Contour=b.Boundary.FirstOrDefault(p=>p.Role=="outer"&&p.ParentId is null)?.Points.Select(p=>new Point(p.X,p.Y)).ToArray()
        }).ToList();
        target.ThresholdMinUsed=settings.Threshold;target.ThresholdMaxUsed=settings.MaxValue;target.TypeUsed=settings.Type.ToString();
        target.HistogramData=source.Histogram.ToArray();target.ChannelMeans=source.ChannelMeans.ToArray();
        target.WhitePixelRatio=source.WhitePixelRatio;target.IsPass=source.IsPass;target.JudgmentMessages=source.JudgmentMessages.ToList();target.ProcessedAt=DateTime.Now;
    }
}
