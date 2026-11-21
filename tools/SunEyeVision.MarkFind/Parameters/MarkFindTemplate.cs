using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using HalconDotNet;

namespace SunEyeVision.Tool.MarkFind.Runtime;

/// <summary>模板在模型坐标系里的外接矩形（HALCON row/column 语义，phi 为弧度）。</summary>
public sealed record MarkFindTemplateBox(double Row, double Column, double Phi, double Length1, double Length2);

/// <summary>
/// 标记查找模板。原件把模板存成模块字节数据（<c>MarkFindConfigPanal.xaml.cs:539–563</c>：
/// <c>AddSpareMatchModel</c> + <c>AddSpareMarkModel</c>），本工具按架构边界改为随工具设置持久化的
/// 中立文本（base64 承载 HALCON 形状模板序列化项 + 模板外接矩形），不再兼容原件 .mfxml 加密文件。
/// 模板创建、读入与释放全部在 Runtime，UI 只传图像/区域。
/// </summary>
public sealed class MarkFindTemplate
{
    /// <summary>载荷格式版本；格式变化必须提升本串，旧载荷直接报错而不是猜测。</summary>
    public const string Format = "SunEyeVision.MarkFind.Model/1";
    public const string FileExtension = ".mfindmodel";

    private const string ShapeModelKey = "shape-model";
    private const string FormatKey = "format";

    private readonly byte[] _serialized;

    private MarkFindTemplate(byte[] serialized, MarkFindTemplateBox box, int levelCount, int pointCount)
    {
        _serialized = serialized;
        Box = box;
        LevelCount = levelCount;
        PointCount = pointCount;
    }

    public MarkFindTemplateBox Box { get; }
    public int LevelCount { get; }
    public int PointCount { get; }
    public int SerializedLength => _serialized.Length;

    // ── 创建 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 用模板参数在给定图像（可带 ROI 域）上建立形状模板。图像为空、区域为空或原生调用失败一律抛出
    /// <see cref="MarkFindCapabilityException"/>，不做默认模型兜底。
    /// </summary>
    public static MarkFindTemplate Create(HImage gray, HRegion? roi, MarkFindSettings settings, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(gray);
        ArgumentNullException.ThrowIfNull(settings);
        cancellation.ThrowIfCancellationRequested();
        using var count = gray.CountChannels();
        if (count.I != 1) throw new MarkFindCapabilityException("MARKFIND_TEMPLATE_IMAGE_INVALID", "模板图像必须是单通道灰度图。");
        using var reduced = roi is null ? gray.Clone() : gray.ReduceDomain(roi);
        HOperatorSet.AreaCenter(reduced, out var area, out var ignoredRow, out var ignoredColumn);
        using (area) using (ignoredRow) using (ignoredColumn)
            if (area.D <= 0)
                throw new MarkFindCapabilityException("MARKFIND_TEMPLATE_REGION_EMPTY", "模板区域为空：请先框选有效的搜索区域。");

        var manual = settings.PyramidScaleMode == MarkFindPyramidMode.Manual;
        var optimization = settings.FeatureMatchType == MarkFindMatchMode.Fast ? "point_reduction_high" : "none";
        using var numLevels = manual ? new HTuple(Math.Clamp(settings.RoughPyramidScale, 1, 20)) : new HTuple("auto");
        using var angleExtent = new HTuple(settings.RotationAngle * 2 * Math.PI / 180d);
        using var angleStart = new HTuple(-settings.RotationAngle * Math.PI / 180d);
        using var angleStep = new HTuple(Math.Clamp(settings.RotationStep, 0.1, 1) * Math.PI / 180d);
        using var contrast = settings.ThresholdMode == MarkFindThresholdMode.Manual
            ? new HTuple(Math.Clamp(settings.HighThreshold, 1, 255))
            : new HTuple("auto");
        using var minContrast = new HTuple(5);

        HOperatorSet.CreateShapeModel(reduced, numLevels, angleStart, angleExtent, angleStep, optimization, "use_polarity",
            contrast, minContrast, out var modelId);
        using (modelId)
        {
            cancellation.ThrowIfCancellationRequested();
            HOperatorSet.GetShapeModelParams(modelId, out var modelLevels, out var modelAngleStart, out var modelAngleExtent,
                out var modelAngleStep, out var modelOptimization, out var modelMetric, out var modelContrast,
                out var modelMinContrast, out var modelPoints);
            using (modelAngleStart) using (modelAngleExtent) using (modelAngleStep) using (modelOptimization)
            using (modelMetric) using (modelContrast) using (modelMinContrast) using (modelLevels) using (modelPoints)
            {
                var levels = modelLevels.I;
                var points = modelPoints.I;
                var box = ReadBox(modelId);
                var serialized = Serialize(modelId);
                return new MarkFindTemplate(serialized, box, levels, points);
            }
        }
    }

    private static MarkFindTemplateBox ReadBox(HTuple modelId)
    {
        using var level = new HTuple(1);
        HOperatorSet.GetShapeModelContours(out var contours, modelId, level);
        using (contours)
        {
            HOperatorSet.SmallestRectangle2Xld(contours, out var row, out var column, out var phi, out var length1, out var length2);
            using (row) using (column) using (phi) using (length1) using (length2)
            {
                if (length1.D <= 0 || length2.D <= 0)
                    throw new MarkFindCapabilityException("MARKFIND_TEMPLATE_EMPTY", "模板轮廓退化：区域内没有可用的边缘特征。");
                return new MarkFindTemplateBox(row.D, column.D, phi.D, length1.D, length2.D);
            }
        }
    }

    private static byte[] Serialize(HTuple modelId)
    {
        HOperatorSet.SerializeShapeModel(modelId, out var item);
        try
        {
            HOperatorSet.GetSerializedItemPtr(item, out var pointer, out var size);
            using (pointer) using (size)
            {
                if (size.I <= 0) throw new MarkFindCapabilityException("MARKFIND_TEMPLATE_SERIALIZE", "模板序列化结果为空。");
                var bytes = new byte[size.I];
                Marshal.Copy(pointer.IP, bytes, 0, bytes.Length);
                return bytes;
            }
        }
        finally
        {
            HOperatorSet.ClearSerializedItem(item);
            item.Dispose();
        }
    }

    // ── 载荷编解码 ───────────────────────────────────────────────────────

    public string Encode()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString(FormatKey, Format);
            writer.WriteString(ShapeModelKey, Convert.ToBase64String(_serialized));
            writer.WriteNumber("row", Box.Row);
            writer.WriteNumber("column", Box.Column);
            writer.WriteNumber("phi", Box.Phi);
            writer.WriteNumber("length1", Box.Length1);
            writer.WriteNumber("length2", Box.Length2);
            writer.WriteNumber("levels", LevelCount);
            writer.WriteNumber("points", PointCount);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>解析模板载荷；空、格式不符或缺项一律抛出明确异常（不猜测、不返回空模板）。</summary>
    public static MarkFindTemplate Decode(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new MarkFindCapabilityException("MARKFIND_TEMPLATE_REQUIRED", "尚未创建模板：请先在“模板参数”中新建模板。");
        JsonDocument document;
        try { document = JsonDocument.Parse(payload); }
        catch (JsonException error)
        {
            throw new MarkFindCapabilityException("MARKFIND_TEMPLATE_INVALID", "模板数据不是有效载荷：" + error.Message);
        }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new MarkFindCapabilityException("MARKFIND_TEMPLATE_INVALID", "模板数据必须是一个对象。");
            if (!root.TryGetProperty(FormatKey, out var format) || format.GetString() != Format)
                throw new MarkFindCapabilityException("MARKFIND_TEMPLATE_VERSION",
                    "模板数据版本不受支持（需要 " + Format + "）。");
            if (!root.TryGetProperty(ShapeModelKey, out var model) || model.GetString() is not { Length: > 0 } encoded)
                throw new MarkFindCapabilityException("MARKFIND_TEMPLATE_INVALID", "模板数据缺少形状模板内容。");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(encoded); }
            catch (FormatException error)
            {
                throw new MarkFindCapabilityException("MARKFIND_TEMPLATE_INVALID", "形状模板内容不是有效 base64：" + error.Message);
            }
            var box = new MarkFindTemplateBox(
                ReadNumber(root, "row"), ReadNumber(root, "column"), ReadNumber(root, "phi"),
                ReadNumber(root, "length1"), ReadNumber(root, "length2"));
            var levels = (int)ReadNumber(root, "levels");
            var points = (int)ReadNumber(root, "points");
            if (box.Length1 <= 0 || box.Length2 <= 0)
                throw new MarkFindCapabilityException("MARKFIND_TEMPLATE_INVALID", "模板外接矩形无效。");
            return new MarkFindTemplate(bytes, box, levels, points);
        }
    }

    private static double ReadNumber(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number))
            throw new MarkFindCapabilityException("MARKFIND_TEMPLATE_INVALID", "模板数据缺少有效数值：" + name);
        return number;
    }

    // ── 执行期装载 ───────────────────────────────────────────────────────

    /// <summary>把载荷反序列化为 HALCON 形状模板句柄；调用方负责 <see cref="Clear"/>。</summary>
    public HTuple LoadModel()
    {
        var pointer = Marshal.AllocHGlobal(_serialized.Length);
        try
        {
            Marshal.Copy(_serialized, 0, pointer, _serialized.Length);
            using var pointerTuple = new HTuple(pointer);
            using var sizeTuple = new HTuple(_serialized.Length);
            HOperatorSet.CreateSerializedItemPtr(pointerTuple, sizeTuple, "true", out var item);
            using (item)
            {
                HOperatorSet.DeserializeShapeModel(item, out var modelId);
                return modelId;
            }
        }
        catch (HOperatorException error)
        {
            throw new MarkFindCapabilityException("MARKFIND_TEMPLATE_LOAD", "模板装载失败：" + error.GetErrorMessage());
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    public static void Clear(HTuple modelId)
    {
        HOperatorSet.ClearShapeModel(modelId);
        modelId.Dispose();
    }

    /// <summary>面板用的一行摘要；模板无效时返回 <see cref="UnavailableReason"/> 而不伪造数值。</summary>
    public string Describe()
        => $"边点数 {PointCount}，金字塔层数 {LevelCount}，外接矩形 {Box.Length1 * 2:0.#}×{Box.Length2 * 2:0.#} 像素";

    public static bool TryDecode(string? payload, out MarkFindTemplate? template, out string error)
    {
        try
        {
            template = Decode(payload);
            error = "";
            return true;
        }
        catch (MarkFindCapabilityException failure)
        {
            template = null;
            error = failure.Message;
            return false;
        }
    }
}
