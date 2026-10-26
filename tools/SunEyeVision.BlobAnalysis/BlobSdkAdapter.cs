using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        IToolDefinition definition=new BlobRuntime.BlobTool();
        var values=new Dictionary<string,object?> { [BlobRuntime.BlobTool.ImageInput]=image };
        if(inspection is not null)values[BlobRuntime.BlobTool.InspectionInput]=inspection;
        if(mask is not null)values[BlobRuntime.BlobTool.MaskInput]=mask;
        var context=new SdkContext(parameters,resources);
        // The current caller still supplies SDK inputs/settings; actual invocation
        // and session ownership now use the same neutral host entry as other consumers.
        var outcome=SunEyeVision.Plugin.Runtime.ToolRuntimeExecutor.ExecuteAsync(definition,settings,
            new InputValues(definition.Describe(settings).Inputs,values),context,
            BlobOutputContract.ToRuntimeRequests(parameters.Context?.OutputRequests??Array.Empty<OutputValueRequest>())).GetAwaiter().GetResult();
        if(!outcome.Success||outcome.Output is not BlobRuntime.BlobOutput output)
            throw new InvalidOperationException(string.Join("; ",outcome.Diagnostics.Select(d=>d.Message)));
        foreach(var diagnostic in outcome.Diagnostics)parameters.LogWarning(diagnostic.Code+": "+diagnostic.Message);
        return output;
    }

    private sealed class SdkContext : IToolExecutionContext
    {
        private readonly BlobAnalysisParameters _parameters;
        internal SdkContext(BlobAnalysisParameters parameters,IOutputResources resources)
        {
            _parameters=parameters;Resources=resources;
            var context=parameters.Context;
            Identity=context?.ExecutionIdentity?.ToInvocationIdentity(context.NodeId??BlobRuntime.BlobTool.Id)
                ?? new RunIdentity(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),BlobRuntime.BlobTool.Id,Guid.NewGuid());
            Cancellation=context?.CancellationToken??default;
        }
        public RunIdentity Identity { get; }
        public CancellationToken Cancellation { get; }
        public IOutputResources Resources { get; }
        public void Log(string message)=>_parameters.LogInfo(message);
    }

    internal static BlobRuntime.BlobSettings Settings(BlobAnalysisParameters p) => new() {
        Threshold=p.Threshold, UpperThreshold=p.UpperThreshold, Type=p.Type,
        SegmentationMode=p.SegmentationMode,
        IsAreaFilterEnabled=p.IsAreaFilterEnabled, FilterAreaMin=p.FilterAreaMin, FilterAreaMax=p.FilterAreaMax,
        IsCircularityFilterEnabled=p.IsCircularityFilterEnabled, FilterCircularityMin=p.FilterCircularityMin, FilterCircularityMax=p.FilterCircularityMax,
        IsRectangularityFilterEnabled=p.IsRectangularityFilterEnabled, FilterRectangularityMin=p.FilterRectangularityMin, FilterRectangularityMax=p.FilterRectangularityMax,
        IsBlobCountFilterEnabled=p.IsBlobCountFilterEnabled, BlobCountLimit=p.BlobCountLimit, SortMode=p.SortMode,
        OutputGrayImage=p.OutputBlobImage, OutputBinaryImage=p.OutputBinaryImage, OutputBoundary=p.OutputContours,
        Judgments=Array.AsReadOnly(p.ResultPresentation.Judgments.Select(j=>new BlobRuntime.BlobJudgment(j.Key,j.Scope==JudgmentScope.PerItem,j.Enabled,j.Min,j.Max,(BlobRuntime.BlobJudgmentKind)j.RuleKind)).ToArray())
    };

    internal static void PopulateHostResources(BlobRuntime.BlobOutput source, BlobAnalysisResults target, int width, int height)
    {
        if(source.GrayImage is { } gray)target.GrayImage=new HalImage(HalconResourceBridge.CopyImage(gray));
        if(source.BinaryImage is { } binary)target.BinaryMask=new HalImage(HalconResourceBridge.CopyImage(binary));
        using var reader=source.Region.AcquireReader();
        target.BlobRegions=new HalRegion(HalconResourceBridge.CopyRegion(source.Region,reader.SourceFrame,width,height));
        target.OkBlobRegions=new HalRegion(HalconResourceBridge.CopyRegion(source.OkRegion,reader.SourceFrame,width,height));
        target.NgBlobRegions=new HalRegion(HalconResourceBridge.CopyRegion(source.NgRegion,reader.SourceFrame,width,height));
        target.IsPass=source.IsPass;target.ProcessedAt=DateTime.Now;
    }
}
