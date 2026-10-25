using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Tool.BlobAnalysis;

/// <summary>Formal data projection of existing Blob results; no statistics or ordering are added.</summary>
public static class BlobOutputContract
{
    private static readonly Lazy<ToolSchema> Declaration=new(()=>new ToolSchema(Array.Empty<Port>(),Describe().Select(BindingClrAdapter.DescribeOutput)));
    public static Guid BlobsFieldId {get;}=Guid.Parse("8ca10000-0000-4000-8000-000000000100");
    public static string BlobsPortId=>"field:"+BlobsFieldId.ToString("N");
    public static Guid RegionFieldId {get;}=Guid.Parse("8ca10000-0000-4000-8000-000000000101");
    public static TypeRef BoundsType => Runtime.BlobSchema.BoundsType;
    public static TypeRef BlobType => Runtime.BlobSchema.BlobType;
    public static TypeRef BlobsType => Runtime.BlobSchema.BlobsType;
    public static IReadOnlyList<OutputVariable> Describe()
    {
        var prototype=new BlobAnalysisResults();
        try
        {
            var variables=prototype.GetOutputVariables();
            foreach(var variable in variables)variable.RuntimeValue=null;
            return variables;
        }
        finally{prototype.Resources!.Dispose();}
    }
    internal static void Apply(List<OutputVariable> variables,BlobAnalysisResults result)
    {
        foreach(var variable in variables)
        {
            variable.FieldId=FieldId(variable.Name);
            if(variable.VariableType=="Image")
            {variable.ValueDeclaration=TypeRef.ResourceOf(ResourceKind.Image);variable.AllowsNull=true;}
            if(variable.DeclaredType==typeof(RoiShape[]))variable.ValueDeclaration=RoiShapeValueAdapter.SequenceDeclaration;
        }
        variables.Add(new(){FieldId=BlobsFieldId,Name=nameof(result.FilteredBlobs),DisplayName="Blob记录",Group="Blob详细",
            DeclaredType=typeof(SequenceValue),ValueDeclaration=BlobsType,VariableType="Record",IsCollection=true,
            RuntimeValue=result.RuntimeOutput is { } runtime?runtime.ToValues()[Runtime.BlobSchema.BlobsPort]
                :new SequenceValue(BlobsType,Array.Empty<object?>())});
        variables.Add(new(){FieldId=RegionFieldId,Name="Region",DisplayName="Blob完整区域",Group="Blob详细",
            DeclaredType=typeof(IReadOnlyRegion),ValueDeclaration=TypeRef.ResourceOf(ResourceKind.Region),VariableType="Region",AllowsNull=true,
            RuntimeValue=result.RuntimeOutput?.Region});
    }

    internal static IEnumerable<OutputValueRequest> ToRuntimeRequests(IEnumerable<OutputValueRequest> requests)
    {
        var known=Runtime.BlobSchema.Outputs.Select(p=>p.Id).ToHashSet(StringComparer.Ordinal);
        foreach(var request in requests)
        {
            request.Validate(Declaration.Value);
            var port=request.PortId;
            // SDK metadata fields (execution time/status/overlay/ROI display geometry) are produced
            // outside the algorithm. They must not be passed as nonexistent kernel output ports.
            if(known.Contains(port))yield return new(port,request.FieldPath);
        }
    }
    private static Guid FieldId(string name)
    {
        var number=name switch{
            "ExecutionTimeMs"=>1,"ExecutionStatusText"=>2,"JudgmentStatus"=>3,"IsPass"=>4,
            "GrayImage"=>5,"BinaryMask"=>6,"OverlayImage"=>7,"ThresholdMinUsed"=>8,"ThresholdMaxUsed"=>9,"TypeUsed"=>10,
            "FilteredBlobCount"=>11,"FilteredTotalArea"=>12,"AverageArea"=>13,"AverageCircularity"=>14,"AverageRectangularity"=>15,
            "WhitePixelRatio"=>16,"HistogramData"=>17,"ChannelMeans"=>18,"BlobAreas"=>19,"BlobPerimeters"=>20,
            "BlobCentroidXs"=>21,"BlobCentroidYs"=>22,"BlobCircularities"=>23,"BlobRectangularities"=>24,
            "BlobBoundingWidths"=>25,"BlobBoundingHeights"=>26,"BlobBoundingRects"=>27,"InspectionGeometries"=>28,"MaskGeometries"=>29,
            "MinimumArea"=>30,"MaximumArea"=>31,
            _=>throw new InvalidOperationException("Blob output requires an explicit stable field identity: "+name)};
        return Guid.Parse("8ca10000-0000-4000-8000-"+number.ToString("D12",System.Globalization.CultureInfo.InvariantCulture));
    }
}
