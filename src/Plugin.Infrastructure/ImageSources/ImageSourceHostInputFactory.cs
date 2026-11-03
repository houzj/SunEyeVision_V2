using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Application;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.Plugin.Infrastructure.ImageSources;

/// <summary>Composes a real source input from this run's admitted settings and document resource base.</summary>
public sealed class ImageSourceHostInputFactory : IHostInputFactory
{
    private readonly ICameraCaptureService? _cameraService;
    public ImageSourceHostInputFactory(ICameraCaptureService? cameraService = null) => _cameraService = cameraService;

    public IHostInputPreparer? Create(HostInputFactoryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Declaration.CapabilityId != "image-source" || context.Declaration.Version != 1) return null;
        var selected = context.Fields.Select(context.SettingsSchema.Inputs);
        var resolved = selected.Capture().Where(pair => pair.Value is ReferenceSetting or VariableSetting)
            .ToDictionary(pair => pair.Key, pair => context.CurrentValues.TryGetValue(pair.Key, out var value)
                ? value.Value : throw new InvalidOperationException("图像源设置引用尚无已接纳的当前值：" + pair.Key), StringComparer.Ordinal);
        var policy = ImageSourcePolicy.ReadFields(context.Node.NodeId, selected.Materialize(resolved));
        var sequence = new ImageSourceRunSequence(context.Node.WorkflowId, new[] { policy }, context.ResourcePaths);
        try { return new OwnedPreparer(sequence.CreatePreparer(context.Node.NodeId, _cameraService), sequence); }
        catch { sequence.Dispose(); throw; }
    }

    private sealed class OwnedPreparer(IHostInputPreparer inner, ImageSourceRunSequence sequence) : IHostInputPreparer
    {
        private int _disposed;
        public string CapabilityId => inner.CapabilityId;
        public int CapabilityVersion => inner.CapabilityVersion;
        public IReadOnlyList<string> Services => inner.Services;
        public System.Threading.Tasks.ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context) => inner.PrepareAsync(context);
        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try { inner.Dispose(); }
            finally { sequence.FinishRound(); }
        }
    }
}
