using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using HalconDotNet;

namespace SunEyeVision.Tool.GrayMatch.Runtime;

/// <summary>模板在模型坐标系里的外接矩形（HALCON row/column 语义，phi 为弧度）。</summary>
public sealed record GrayMatchTemplateBox(double Row, double Column, double Phi, double Length1, double Length2);

/// <summary>
/// 灰度匹配模板。原件按 <c>GrayMatchVADialogData.CreatePatE → CreateGrayPatEVA</c> 建立灰度模板，
/// 经 <c>FeaturepatMatchConfigPanalDataBase.AddModelToModule</c> 以 <c>AddSpareModel</c> 存入模块，
/// 导出为 <c>*.gmvaxml</c>（<c>GrayMatchVAConfigPanalData.cs:20</c>）。
/// 本工具按架构边界改为随工具设置持久化的中立文本（base64 承载 HALCON NCC 模板序列化项 + 原点 + 外接矩形 +
/// 角度覆盖范围），不兼容原件文件格式。模板创建、读入与释放全部在 Runtime，UI 只传图像/区域。
/// </summary>
public sealed class GrayMatchTemplate
{
    /// <summary>载荷格式版本；格式变化必须提升本串，旧载荷直接报错而不是猜测。</summary>
    public const string Format = "SunEyeVision.GrayMatch.Model/1";
    public const string FileExtension = ".gmmodel";

    private const string ModelKey = "ncc-model";
    private const string FormatKey = "format";

    private readonly byte[] _serialized;

    private GrayMatchTemplate(byte[] serialized, GrayMatchTemplateBox box, double originRow, double originColumn,
        int levelCount, int width, int height, double angleStart, double angleExtent)
    {
        _serialized = serialized;
        Box = box;
        OriginRow = originRow;
        OriginColumn = originColumn;
        LevelCount = levelCount;
        Width = width;
        Height = height;
        AngleStart = angleStart;
        AngleExtent = angleExtent;
    }

    public GrayMatchTemplateBox Box { get; }
    /// <summary>模板原点（HALCON 语义：<c>GetNccModelOrigin</c>），匹配结果的位置即该原点的位置。</summary>
    public double OriginRow { get; }
    public double OriginColumn { get; }
    public int LevelCount { get; }
    public int Width { get; }
    public int Height { get; }
    /// <summary>模板覆盖的角度起点（弧度）。</summary>
    public double AngleStart { get; }
    /// <summary>模板覆盖的角度跨度（弧度）。</summary>
    public double AngleExtent { get; }

    // ── 创建 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 用模板参数在给定图像（可带 ROI 域）上建立 NCC 模板。图像为空、区域为空或原生调用失败一律抛出
    /// <see cref="GrayMatchCapabilityException"/>，不做默认模型兜底。
    /// </summary>
    public static GrayMatchTemplate Create(HImage gray, HRegion? roi, GrayMatchSettings settings, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(gray);
        ArgumentNullException.ThrowIfNull(settings);
        cancellation.ThrowIfCancellationRequested();
        using var count = gray.CountChannels();
        if (count.I != 1) throw new GrayMatchCapabilityException("GRAYMATCH_TEMPLATE_IMAGE_INVALID", "模板图像必须是单通道灰度图。");
        using var reduced = roi is null ? gray.Clone() : gray.ReduceDomain(roi);
        HOperatorSet.AreaCenter(reduced, out var area, out var ignoredRow, out var ignoredColumn);
        using (area) using (ignoredRow) using (ignoredColumn)
            if (area.D <= 0)
                throw new GrayMatchCapabilityException("GRAYMATCH_TEMPLATE_REGION_EMPTY", "模板区域为空：请先连接有效的搜索区域。");

        // 金字塔层数标记：自动交给 HALCON 自适应，手动用「金字塔层数」（1..8）。
        var manual = settings.PyramidMode == GrayMatchPyramidMode.Manual;
        using var numLevels = manual ? new HTuple(Math.Clamp(settings.PyramidLevel, 1, 8)) : new HTuple("auto");
        using var angleStart = new HTuple(settings.ModelAngleStart * Math.PI / 180d);
        using var angleExtent = new HTuple(Math.Max(1e-4, (settings.ModelAngleEnd - settings.ModelAngleStart) * Math.PI / 180d));
        using var angleStep = new HTuple(Math.Clamp(settings.ModelAngleStep, 1, 50) * Math.PI / 180d);
        // 极性是 NCC 模板创建期度量（metric）：运行期无法切换，因此这里固定为「考虑极性」（Polarity 默认 1）。
        using var metric = new HTuple("use_polarity");

        HTuple modelId;
        try
        {
            HOperatorSet.CreateNccModel(reduced, numLevels, angleStart, angleExtent, angleStep, metric, out modelId);
        }
        catch (HOperatorException error)
        {
            throw new GrayMatchCapabilityException("GRAYMATCH_TEMPLATE_CREATE_FAILED", "灰度模板创建失败：" + error.GetErrorMessage());
        }

        using (modelId)
        {
            cancellation.ThrowIfCancellationRequested();
            HOperatorSet.GetNccModelParams(modelId, out var modelLevels, out var modelAngleStart, out var modelAngleExtent,
                out var modelAngleStep, out var modelMetric);
            using (modelLevels) using (modelAngleStart) using (modelAngleExtent) using (modelAngleStep) using (modelMetric)
            {
                HOperatorSet.GetNccModelOrigin(modelId, out var originRow, out var originColumn);
                using (originRow) using (originColumn)
                {
                    var box = ReadBox(modelId, out var width, out var height);
                    var serialized = Serialize(modelId);
                    return new GrayMatchTemplate(serialized, box, originRow.D, originColumn.D,
                        modelLevels.I, width, height, modelAngleStart.D, modelAngleExtent.D);
                }
            }
        }
    }

    private static GrayMatchTemplateBox ReadBox(HTuple modelId, out int width, out int height)
    {
        HOperatorSet.GetNccModelRegion(out var region, modelId);
        using (region)
        {
            HOperatorSet.SmallestRectangle1(region, out var row1, out var column1, out var row2, out var column2);
            using (row1) using (column1) using (row2) using (column2)
            {
                width = (int)Math.Round(column2.D - column1.D + 1);
                height = (int)Math.Round(row2.D - row1.D + 1);
            }
            HOperatorSet.GenContourRegionXld(region, out var contours, "border");
            using (contours)
            {
                HOperatorSet.SmallestRectangle2Xld(contours, out var row, out var column, out var phi, out var length1, out var length2);
                using (row) using (column) using (phi) using (length1) using (length2)
                {
                    if (length1.D <= 0 || length2.D <= 0)
                        throw new GrayMatchCapabilityException("GRAYMATCH_TEMPLATE_EMPTY", "模板区域退化：区域内没有可用的灰度结构。");
                    return new GrayMatchTemplateBox(row.D, column.D, phi.D, length1.D, length2.D);
                }
            }
        }
    }

    private static byte[] Serialize(HTuple modelId)
    {
        HOperatorSet.SerializeNccModel(modelId, out var item);
        try
        {
            HOperatorSet.GetSerializedItemPtr(item, out var pointer, out var size);
            using (pointer) using (size)
            {
                if (size.I <= 0) throw new GrayMatchCapabilityException("GRAYMATCH_TEMPLATE_SERIALIZE", "模板序列化结果为空。");
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
            writer.WriteString(ModelKey, Convert.ToBase64String(_serialized));
            writer.WriteNumber("row", Box.Row);
            writer.WriteNumber("column", Box.Column);
            writer.WriteNumber("phi", Box.Phi);
            writer.WriteNumber("length1", Box.Length1);
            writer.WriteNumber("length2", Box.Length2);
            writer.WriteNumber("origin-row", OriginRow);
            writer.WriteNumber("origin-column", OriginColumn);
            writer.WriteNumber("levels", LevelCount);
            writer.WriteNumber("width", Width);
            writer.WriteNumber("height", Height);
            writer.WriteNumber("angle-start", AngleStart);
            writer.WriteNumber("angle-extent", AngleExtent);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>解析模板载荷；空、格式不符或缺项一律抛出明确异常（不猜测、不返回空模板）。</summary>
    public static GrayMatchTemplate Decode(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new GrayMatchCapabilityException("GRAYMATCH_TEMPLATE_REQUIRED", "尚未创建模板：请先在“模板参数”中新建模板。");
        JsonDocument document;
        try { document = JsonDocument.Parse(payload); }
        catch (JsonException error)
        {
            throw new GrayMatchCapabilityException("GRAYMATCH_TEMPLATE_INVALID", "模板数据不是有效载荷：" + error.Message);
        }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new GrayMatchCapabilityException("GRAYMATCH_TEMPLATE_INVALID", "模板数据必须是一个对象。");
            if (!root.TryGetProperty(FormatKey, out var format) || format.GetString() != Format)
                throw new GrayMatchCapabilityException("GRAYMATCH_TEMPLATE_VERSION",
                    "模板数据版本不受支持（需要 " + Format + "）。");
            if (!root.TryGetProperty(ModelKey, out var model) || model.GetString() is not { Length: > 0 } encoded)
                throw new GrayMatchCapabilityException("GRAYMATCH_TEMPLATE_INVALID", "模板数据缺少灰度模板内容。");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(encoded); }
            catch (FormatException error)
            {
                throw new GrayMatchCapabilityException("GRAYMATCH_TEMPLATE_INVALID", "灰度模板内容不是有效 base64：" + error.Message);
            }
            var box = new GrayMatchTemplateBox(
                ReadNumber(root, "row"), ReadNumber(root, "column"), ReadNumber(root, "phi"),
                ReadNumber(root, "length1"), ReadNumber(root, "length2"));
            var originRow = ReadNumber(root, "origin-row");
            var originColumn = ReadNumber(root, "origin-column");
            var levels = (int)ReadNumber(root, "levels");
            var width = (int)ReadNumber(root, "width");
            var height = (int)ReadNumber(root, "height");
            var angleStart = ReadNumber(root, "angle-start");
            var angleExtent = ReadNumber(root, "angle-extent");
            if (box.Length1 <= 0 || box.Length2 <= 0)
                throw new GrayMatchCapabilityException("GRAYMATCH_TEMPLATE_INVALID", "模板外接矩形无效。");
            if (angleExtent <= 0)
                throw new GrayMatchCapabilityException("GRAYMATCH_TEMPLATE_INVALID", "模板角度覆盖范围无效。");
            return new GrayMatchTemplate(bytes, box, originRow, originColumn, levels, width, height, angleStart, angleExtent);
        }
    }

    private static double ReadNumber(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number))
            throw new GrayMatchCapabilityException("GRAYMATCH_TEMPLATE_INVALID", "模板数据缺少有效数值：" + name);
        return number;
    }

    // ── 执行期装载 ───────────────────────────────────────────────────────

    /// <summary>把载荷反序列化为 HALCON NCC 模板句柄；调用方负责 <see cref="Clear"/>。</summary>
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
                HOperatorSet.DeserializeNccModel(item, out var modelId);
                return modelId;
            }
        }
        catch (HOperatorException error)
        {
            throw new GrayMatchCapabilityException("GRAYMATCH_TEMPLATE_LOAD", "模板装载失败：" + error.GetErrorMessage());
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    public static void Clear(HTuple modelId)
    {
        HOperatorSet.ClearNccModel(modelId);
        modelId.Dispose();
    }

    /// <summary>运行期角度范围必须落在模板覆盖范围内，否则明确报错而不是给出空结果。</summary>
    public void RequireAngleCoverage(GrayMatchSettings settings)
    {
        var requestedStart = settings.AngleStart * Math.PI / 180d;
        var requestedEnd = settings.AngleEnd * Math.PI / 180d;
        var modelEnd = AngleStart + AngleExtent;
        if (requestedStart < AngleStart - 1e-9 || requestedEnd > modelEnd + 1e-9)
            throw new GrayMatchCapabilityException("GRAYMATCH_TEMPLATE_ANGLE_RANGE",
                "运行角度范围超出模板覆盖范围，请重建模板后再执行。");
    }

    /// <summary>「匹配结束层」不得超过模板层数（0 表示使用全部层）。</summary>
    public void RequireEndLevelCoverage(GrayMatchSettings settings)
    {
        if (settings.EndPyramidLevel != 0 && settings.EndPyramidLevel > LevelCount)
            throw new GrayMatchCapabilityException("GRAYMATCH_END_LEVEL_INVALID",
                $"匹配结束层 {settings.EndPyramidLevel} 超过模板层数 {LevelCount}，请调整或重建模板。");
    }

    /// <summary>面板用的一行摘要；模板无效时由调用方显示错误，不伪造数值。</summary>
    public string Describe()
        => $"模板 {Width}×{Height} 像素，金字塔层数 {LevelCount}，外接矩形 {Box.Length1 * 2:0.#}×{Box.Length2 * 2:0.#} 像素";

    public static bool TryDecode(string? payload, out GrayMatchTemplate? template, out string error)
    {
        try
        {
            template = Decode(payload);
            error = "";
            return true;
        }
        catch (GrayMatchCapabilityException failure)
        {
            template = null;
            error = failure.Message;
            return false;
        }
    }
}
