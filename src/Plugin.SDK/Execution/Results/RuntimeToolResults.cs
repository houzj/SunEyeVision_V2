using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Vision.Hosting;
using SunEyeVision.Plugin.SDK.Execution.Helpers;

namespace SunEyeVision.Plugin.SDK.Execution.Results;

/// <summary>Common host publication envelope. It owns the original Runtime output,
/// never a tool-specific copy, and exposes only formally declared canonical values.</summary>
public sealed class RuntimeToolResults : ToolResults
{
    private readonly IReadOnlyDictionary<string,object?> _values;
    private readonly object _imageGate=new();
    private readonly Dictionary<string,IVisionImage> _imageViews=new(StringComparer.Ordinal);
    private bool _imagesClosed;
    public IToolOutput? Output { get; }

    public RuntimeToolResults(string toolId, ToolSchema schema, ToolExecutionIdentity identity,
        ToolOutcome outcome, IDisposable resources, double elapsed)
    {
        Output=outcome.Output;
        Resources=new ResultResourceOwner(()=>ReleaseViewsAndOutput(Output as IDisposable,resources));
        try
        {
            ToolId=toolId;ToolName=toolId;ExecutionIdentity=identity;
            BindingOutputSchema=new ToolSchema(Array.Empty<Port>(),schema.Outputs);
            ExecutionTimeMs=elapsed;
            Status=outcome.Success?ExecutionStatus.Success:
                identity.CancellationToken.IsCancellationRequested?ExecutionStatus.Cancelled:ExecutionStatus.Failed;
            IsPass=outcome.Success&&outcome.Judgment!=BusinessJudgment.Fail;
            ErrorMessage=outcome.Success?null:string.Join("; ",outcome.Diagnostics.Select(d=>d.Message));
            foreach(var diagnostic in outcome.Diagnostics)AddWarning(diagnostic.Code+": "+diagnostic.Message);
            _values=outcome.Success?new InputValues(schema.Outputs,
                outcome.Output?.ToValues()??throw new ArgumentException("Successful Runtime has no output.")).CopyValues()
                :new Dictionary<string,object?>();
        }
        catch(Exception failure)
        {
            try{Resources.Dispose();}
            catch(Exception cleanup){throw new AggregateException("Runtime result capture and cleanup failed.",failure,cleanup);}
            throw;
        }
    }

    private static void Release(IDisposable? output,IDisposable resources)
    {
        Exception? failure=null;
        try{output?.Dispose();}catch(Exception error){failure=error;}
        try{resources.Dispose();}
        catch(Exception cleanup)
        {if(failure is not null)throw new AggregateException("Runtime output and resource cleanup failed.",failure,cleanup);throw;}
        if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private void ReleaseViewsAndOutput(IDisposable? output,IDisposable resources)
    {
        IVisionImage[] images;
        lock(_imageGate){_imagesClosed=true;images=_imageViews.Values.ToArray();_imageViews.Clear();}
        var errors=new List<Exception>();
        foreach(var image in images)try{image.Dispose();}catch(Exception error){errors.Add(error);}
        try{Release(output,resources);}catch(Exception error){errors.Add(error);}
        if(errors.Count>0)throw new AggregateException("Runtime image views and output cleanup failed.",errors);
    }

    /// <summary>Borrowed SDK/backend view, owned by the same stable result resource
    /// owner as the canonical output. Keep a result reader/lease throughout its use.</summary>
    public IVisionImage? GetImageView(string portId)
    {
        using var retained=Resources!.Retain();
        lock(_imageGate)
        {
            ObjectDisposedException.ThrowIf(_imagesClosed,this);
            var port=BindingOutputSchema!.Outputs.SingleOrDefault(p=>p.Id==portId)
                ??throw new ArgumentException("Image output is not declared: "+portId);
            if(port.Type.Shape!=ValueShape.Resource||port.Type.Resource!=ResourceKind.Image)throw new ArgumentException("Output is not an image.");
            if(!_values.TryGetValue(portId,out var value)||value is null)return null;
            if(_imageViews.TryGetValue(portId,out var cached))return cached;
            if(VisionImageFactoryLocator.Current is not IReadOnlyImageViewFactory factory)
                throw new InvalidOperationException("宿主未注册保留像素和实际Domain的图像副本工厂。");
            var source=(IReadOnlyImage)value;var image=factory.CopyImage(source)??throw new InvalidOperationException("Image copy factory returned null.");
            try
            {
                if(image.IsEmpty||image.Width!=source.Width||image.Height!=source.Height)throw new InvalidOperationException("Image copy changed the pixel grid.");
                ImageFrameProvenance.RecordExactCopy(image,source.Frame);_imageViews.Add(portId,image);return image;
            }
            catch(Exception failure)
            {try{image.Dispose();}catch(Exception cleanup){throw new AggregateException("Image copy validation and cleanup failed.",failure,cleanup);}throw;}
        }
    }
    public override Dictionary<string,IVisionImage?> GetDisplayImages()
    {
        var images=new Dictionary<string,IVisionImage?>(StringComparer.Ordinal);
        if(VisionImageFactoryLocator.Current is not IReadOnlyImageViewFactory)return images;
        foreach(var port in BindingOutputSchema!.Outputs.Where(p=>p.Type.Shape==ValueShape.Resource&&p.Type.Resource==ResourceKind.Image))
            if(GetImageView(port.Id) is { } image)images.Add(port.Id,image);
        return images;
    }

    public IReadOnlyDictionary<string,object?> Values=>_values;
    public override bool TryGetDeclaredOutputValue(string portId,out object? value)=>_values.TryGetValue(portId,out value);
    public static IReadOnlyList<OutputVariable> Describe(ToolSchema schema)=>schema.Outputs.Select(port=>new OutputVariable
    {
        PortId=port.Id,Name=port.Id,DisplayName=port.Id,ValueDeclaration=port.Type,AllowsNull=port.Nullable,
        DeclaredType=port.Type.Shape==ValueShape.Resource&&port.Type.Resource==ResourceKind.Image?typeof(IReadOnlyImage):null,
        VariableType=port.Type.Shape==ValueShape.Resource&&port.Type.Resource==ResourceKind.Image?"Image":"",
        IsCollection=port.Type.Shape==ValueShape.Sequence,Kind=OutputVariableKind.Data
    }).ToArray();
    public override IReadOnlyList<OutputVariable> GetOutputVariables()=>Describe(BindingOutputSchema!).Select(declaration=>
    {
        declaration.RuntimeValue=_values.GetValueOrDefault(declaration.PortId!);
        return declaration;
    }).ToArray();
}
