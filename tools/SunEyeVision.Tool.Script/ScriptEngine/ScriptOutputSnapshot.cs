using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Tool.Script.ScriptEngine;

/// <summary>只持有捕获时复制出的资源；绝不释放脚本原值或借用输入。</summary>
internal sealed class ScriptOutputSnapshot : IDisposable
{
    internal Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal);
    private readonly Dictionary<object, object> _copies = new(ReferenceEqualityComparer.Instance);
    private readonly List<IDisposable> _owned = new();
    private bool _disposed;
    internal void Own(IDisposable resource)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _owned.Add(resource ?? throw new ArgumentNullException(nameof(resource)));
    }

    internal object? Copy(object? value, Type declaredType)
    {
        if (value == null) return null;
        if (!declaredType.IsInstanceOfType(value)) throw new InvalidOperationException($"输出类型 {value.GetType().Name} 不符合声明 {declaredType.Name}。");
        if (value is string || value.GetType().IsValueType || value is BinaryPayload) return value;
        if (_copies.TryGetValue(value, out var existing)) return existing;
        object copy;
        if (value is IVisionImage image)
        {
            var cloned = image.Clone();
            if (cloned == null || ReferenceEquals(cloned, image)) throw new InvalidOperationException("图像Clone必须返回独立资源。");
            copy = cloned;
            _owned.Add(cloned);
            if (cloned.GetType().Assembly.IsCollectible)
                throw new InvalidOperationException("图像输出的副本仍属于可卸载用户程序集，不能外传用户对象。");
        }
        else if (value is Array array && array.Rank == 1)
        {
            var elementType = declaredType.GetElementType()!;
            var cloned = Array.CreateInstance(elementType, array.Length);
            _copies.Add(value, cloned);
            for (var i = 0; i < array.Length; i++) cloned.SetValue(Copy(array.GetValue(i), elementType), i);
            return cloned;
        }
        else if (value is Polygon2d polygon) copy = new Polygon2d(polygon.Vertices.ToArray(), polygon.IsClosed);
        else if (value is RoiShape roi)
        {
            var geometry = roi.Geometry;
            if (geometry != null && geometry is not Polygon2d && geometry is not Point2d && geometry is not Circle2d &&
                geometry is not RotatedRect2d && geometry is not Line2d && geometry is not Annulus2d && geometry is not Arc2d)
                throw new InvalidOperationException("ROI包含未声明的用户几何对象，不能外传。");
            copy = roi.Clone();
        }
        else if (value is AffineMatrix matrix) copy = new AffineMatrix(matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.OffsetX, matrix.OffsetY);
        else throw new InvalidOperationException($"输出对象类型 {value.GetType().Name} 不允许逃逸脚本运行域。");
        _copies.Add(value, copy);
        return copy;
    }

    internal ScriptOutputSnapshot TransferResources()
    {
        var transferred = new ScriptOutputSnapshot();
        transferred._owned.AddRange(_owned);
        _owned.Clear();
        return transferred;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        List<Exception>? errors = null;
        foreach (var resource in _owned)
            try { resource.Dispose(); } catch (Exception ex) { (errors ??= new()).Add(ex); }
        _owned.Clear(); _copies.Clear(); Values.Clear();
        if (errors != null) throw new AggregateException("脚本输出回收失败", errors);
    }
}
