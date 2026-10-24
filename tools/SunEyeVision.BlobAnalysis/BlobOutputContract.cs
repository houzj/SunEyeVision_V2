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
    public static Guid BlobsFieldId {get;}=Guid.Parse("8ca10000-0000-4000-8000-000000000100");
    public static string BlobsPortId=>"field:"+BlobsFieldId.ToString("N");
    private static TypeRef PointType=>TypeRef.GeometryOf(GeometryKind.Point,RoiShapeValueAdapter.ImageFrame);
    public static TypeRef BoundsType {get;}=TypeRef.RecordOf("blob.bounds",new[]{new Port("x",DataType.Number),new Port("y",DataType.Number),new Port("width",DataType.Number),new Port("height",DataType.Number)});
    public static TypeRef BlobType {get;}=TypeRef.RecordOf("blob.result",new[]{
        new Port("element-id",DataType.Integer),new Port("label",DataType.Integer),new Port("area",DataType.Number),new Port("perimeter",DataType.Number),
        new Port("centroid-x",DataType.Number),new Port("centroid-y",DataType.Number),new Port("centroid",PointType),
        new Port("circularity",DataType.Number),new Port("rectangularity",DataType.Number),new Port("bounds",BoundsType),
        new Port("rotated-bounds",TypeRef.SequenceOf(PointType,CountConstraint.Exactly(4)),false),
        new Port("contour",TypeRef.SequenceOf(PointType),false),new Port("judgment-passed",DataType.Boolean)});
    public static TypeRef BlobsType {get;}=TypeRef.SequenceOf(BlobType);
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
            RuntimeValue=new SequenceValue(BlobsType,result.FilteredBlobs.Select(blob=>(object?)Freeze(blob)))});
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
            _=>throw new InvalidOperationException("Blob output requires an explicit stable field identity: "+name)};
        return Guid.Parse("8ca10000-0000-4000-8000-"+number.ToString("D12",System.Globalization.CultureInfo.InvariantCulture));
    }
    private static RecordValue Freeze(BlobResult blob)
    {
        object? Point(System.Windows.Point point)=>new PointGeometry(RoiShapeValueAdapter.ImageFrame,new(point.X,point.Y));
        var values=new Dictionary<string,object?>{
            {"element-id",blob.ElementId},{"label",(long)blob.Label},{"area",blob.Area},{"perimeter",blob.Perimeter},
            {"centroid-x",blob.CentroidX},{"centroid-y",blob.CentroidY},{"centroid",new PointGeometry(RoiShapeValueAdapter.ImageFrame,new(blob.CentroidX,blob.CentroidY))},
            {"circularity",blob.Circularity},{"rectangularity",blob.Rectangularity},
            {"bounds",new RecordValue(BoundsType,new Dictionary<string,object?>{{"x",blob.BoundingBox.X},{"y",blob.BoundingBox.Y},{"width",blob.BoundingBox.Width},{"height",blob.BoundingBox.Height}})},
            {"judgment-passed",blob.JudgmentPassed}};
        if(blob.RotatedBoundingBox is {Length:4} corners)values["rotated-bounds"]=new SequenceValue(BlobType.Fields.Single(p=>p.Id=="rotated-bounds").Type,corners.Select(Point));
        if(blob.Contour is { } contour)values["contour"]=new SequenceValue(BlobType.Fields.Single(p=>p.Id=="contour").Type,contour.Select(Point));
        return new(BlobType,values);
    }
}
