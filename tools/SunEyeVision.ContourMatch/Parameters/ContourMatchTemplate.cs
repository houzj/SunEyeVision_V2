using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using HalconDotNet;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ContourMatch.Runtime;

/// <summary>模板在模型坐标系里的外接矩形（HALCON row/column 语义，phi 为弧度）。</summary>
public sealed record ContourMatchTemplateBox(double Row, double Column, double Phi, double Length1, double Length2);

/// <summary>创建模板后在训练图像上 Find 到的参考姿态（HALCON row/column，角度为弧度）。</summary>
public sealed record ContourMatchTemplatePose(double Row, double Column, double Angle,
    double Scale, double ScaleX, double ScaleY);

/// <summary>
/// 轮廓匹配模板。原件把模板存成模块字节数据（<c>HPPyramidFeaturepatMatchDialogData.CreatePatE</c>
/// 调 <c>CreateDipolePyramid</c>，模型经 <c>FeaturepatMatchConfigPanalDataBase.AddModelToModule</c>
/// 以 <c>AddSpareModel</c> 写入模块），导出为加密 <c>.contourmxml</c>。
/// 本工具按架构边界改为随工具设置持久化的中立文本（base64 承载 HALCON 形状模板序列化项 + 外接矩形 +
/// 模型角度覆盖范围），不兼容原件加密文件格式。模板创建、读入与释放全部在 Runtime，UI 只传图像/区域。
/// </summary>
public sealed class ContourMatchTemplate
{
    /// <summary>载荷格式版本；格式变化必须提升本串，旧载荷直接报错而不是猜测。</summary>
    public const string Format = "SunEyeVision.ContourMatch.Model/1";
    public const string FileExtension = ".cmatchmodel";

    /// <summary>模型创建期覆盖完整一圈角度，运行期角度范围因此总能落在模型范围内。</summary>
    private const double ModelAngleStartRadians = -Math.PI;
    private const double ModelAngleExtentRadians = 2 * Math.PI;
    private const double ModelAngleStepRadians = Math.PI / 180d;

    private const string ShapeModelKey = "shape-model";
    private const string FormatKey = "format";
    /// <summary>可选训练原图段：原厂把建模板时的图像存在模型里（ModelItem 的 SolModelFile_{n}_CurImageData 槽，
    /// 受"模板存图"开关控制，见 decompiled HPFeaturepatMatchDialogUI.cs:1019-1040 与 :1650-1656）。
    /// 缺该段=旧模型/未存图，Decode 返回无图而不是报错。</summary>
    private const string ImageKey = "image";
    private const string ImageWidthKey = "image-width";
    private const string ImageHeightKey = "image-height";
    /// <summary>
    /// 擦除掩膜：原厂橡皮擦/画笔把笔刷路径像素置 1（otherMask/otherMask2，见 decompiled
    /// HPFeaturepatMatchDialogUI.rubber_OnWipe :1416-1490），训练时这些像素不参与建模。
    /// 以 RLE 压缩存储（掩膜稀疏，直接 base64 会让方案体积暴涨）。
    /// </summary>
    private const string MaskKey = "mask-rle";
    private const string MaskWidthKey = "mask-width";
    private const string MaskHeightKey = "mask-height";
    private const string Mask2Key = "mask2-rle";
    private const string Mask2WidthKey = "mask2-width";
    private const string Mask2HeightKey = "mask2-height";
    private const string AnisoKey = "aniso";
    private const string PreviewPoseKey = "preview-pose";
    // 原厂把 PointsData/PointsNums 作为模型用户数据独立保存；本方以模型坐标点数组持久化。
    private const string OutlinePointsKey = "outline-points";
    private const string FixPointKey = "fix-point";
    private const string FixPointXKey = "x";
    private const string FixPointYKey = "y";
    /// <summary>
    /// 预览姿态（FindPreviewPose）所需的最小模型外接框短边（像素）。低于该值时
    /// HALCON FindScaledShapeModel 在小/弱模型上会原生访问冲突（0xC0000005），必须跳过。
    /// </summary>
    private const int PreviewPoseMinimumExtent = 24;

    private readonly byte[] _serialized;

    private ContourMatchTemplate(byte[] serialized, ContourMatchTemplateBox box, int levelCount, int pointCount,
        double angleStart, double angleExtent, byte[]? image = null, int imageWidth = 0, int imageHeight = 0,
        bool anisotropic = false, byte[]? mask = null, int maskWidth = 0, int maskHeight = 0,
        byte[]? mask2 = null, int mask2Width = 0, int mask2Height = 0,
        ContourMatchTemplatePose? previewPose = null, IReadOnlyList<Point2>? outlinePoints = null,
        double? fixPointX = null, double? fixPointY = null)
    {
        _serialized = serialized;
        Box = box;
        LevelCount = levelCount;
        PointCount = pointCount;
        AngleStart = angleStart;
        AngleExtent = angleExtent;
        Image = image;
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
        Anisotropic = anisotropic;
        Mask = mask;
        MaskWidth = maskWidth;
        MaskHeight = maskHeight;
        Mask2 = mask2;
        Mask2Width = mask2Width;
        Mask2Height = mask2Height;
        PreviewPose = previewPose;
        OutlinePoints = outlinePoints is { Count: > 0 } ? outlinePoints.ToArray() : Array.Empty<Point2>();
        FixPointX = fixPointX;
        FixPointY = fixPointY;
    }

    /// <summary>
    /// 模型是否为各向异性（X/Y 尺度独立）。HALCON 提供 CreateAnisoShapeModel / FindAnisoShapeModel，
    /// 对应原厂「尺度范围XY」与 X/Y 独立尺度搜索。反序列化后模型类型不体现在字节里，故随载荷记录。
    /// </summary>
    public bool Anisotropic { get; }

    public ContourMatchTemplateBox Box { get; }
    public int LevelCount { get; }
    public int PointCount { get; }
    /// <summary>模型覆盖的角度起点（弧度，HALCON 语义）。</summary>
    public double AngleStart { get; }
    /// <summary>模型覆盖的角度跨度（弧度）。</summary>
    public double AngleExtent { get; }
    /// <summary>训练原图（Gray8 紧密像素），无存档图为 null。</summary>
    public byte[]? Image { get; }
    public int ImageWidth { get; }
    public int ImageHeight { get; }

    /// <summary>擦除掩膜像素（长度 = MaskWidth*MaskHeight，1=被擦除），无擦除时为 null。</summary>
    public byte[]? Mask { get; }
    public int MaskWidth { get; }
    public int MaskHeight { get; }

    /// <summary>原厂 otherMask2（画笔/感兴趣区域）像素。Mask 与 Mask2 必须独立持久化。</summary>
    public byte[]? Mask2 { get; }
    public int Mask2Width { get; }
    public int Mask2Height { get; }
    /// <summary>创建后在训练图像上 Find 到的姿态；旧载荷没有该段时为空。</summary>
    public ContourMatchTemplatePose? PreviewPose { get; }
    /// <summary>创建成功时保存的模型坐标轮廓点；旧载荷为空时回退到 HALCON 读取。</summary>
    public IReadOnlyList<Point2> OutlinePoints { get; }

    /// <summary>
    /// 匹配中心（图像像素坐标）。原厂 _FixPointX/_FixPointY 随模板 BaseData 持久化，
    /// 再次编辑同一模板时恢复上次中心；旧载荷没有该段时为 null，由样本中心重算。
    /// </summary>
    public double? FixPointX { get; }
    public double? FixPointY { get; }

    /// <summary>写入匹配中心并返回新实例（原厂保存模板时一并保存 FixPoint）。</summary>
    public ContourMatchTemplate WithFixPoint(double x, double y)
        => new(_serialized, Box, LevelCount, PointCount, AngleStart, AngleExtent, Image, ImageWidth, ImageHeight,
            Anisotropic, Mask, MaskWidth, MaskHeight, Mask2, Mask2Width, Mask2Height, PreviewPose, OutlinePoints, x, y);

    /// <summary>写入/替换训练原图并返回新实例（原厂"模板存图"开时的行为）。</summary>
    public ContourMatchTemplate WithImage(byte[] grayPixels, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(grayPixels);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "训练原图尺寸无效。");
        if (grayPixels.Length < checked(width * height))
            throw new ArgumentException("训练原图像素长度与尺寸不符。", nameof(grayPixels));
        return new ContourMatchTemplate(_serialized, Box, LevelCount, PointCount, AngleStart, AngleExtent,
            grayPixels, width, height, Anisotropic, Mask, MaskWidth, MaskHeight, Mask2, Mask2Width, Mask2Height, PreviewPose, OutlinePoints, FixPointX, FixPointY);
    }

    /// <summary>写入/替换擦除掩膜并返回新实例（原厂 otherMask 语义）。</summary>
    public ContourMatchTemplate WithMask(byte[]? mask, int width, int height)
        => WithMasks(mask, Mask2, width, height);

    /// <summary>写入原厂 otherMask/otherMask2 两个独立掩膜；两者在建模时才合并为有效训练域。</summary>
    public ContourMatchTemplate WithMasks(byte[]? mask, byte[]? mask2, int width, int height)
    {
        var hasMask = mask is { Length: > 0 };
        var hasMask2 = mask2 is { Length: > 0 };
        if (!hasMask && !hasMask2)
            return new ContourMatchTemplate(_serialized, Box, LevelCount, PointCount, AngleStart, AngleExtent,
                Image, ImageWidth, ImageHeight, Anisotropic, null, 0, 0, null, 0, 0, PreviewPose, OutlinePoints, FixPointX, FixPointY);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "掩膜尺寸无效。");
        var expected = checked(width * height);
        if (hasMask && mask!.Length < expected) throw new ArgumentException("掩膜像素长度与尺寸不符。", nameof(mask));
        if (hasMask2 && mask2!.Length < expected) throw new ArgumentException("第二掩膜像素长度与尺寸不符。", nameof(mask2));
        return new ContourMatchTemplate(_serialized, Box, LevelCount, PointCount, AngleStart, AngleExtent,
            Image, ImageWidth, ImageHeight, Anisotropic,
            hasMask ? mask : null, hasMask ? width : 0, hasMask ? height : 0,
            hasMask2 ? mask2 : null, hasMask2 ? width : 0, hasMask2 ? height : 0, PreviewPose, OutlinePoints, FixPointX, FixPointY);
    }

    /// <summary>丢弃训练原图并返回新实例（原厂"模板存图"关时的行为）。</summary>
    public ContourMatchTemplate WithoutImage()
        => Image is null ? this
            : new ContourMatchTemplate(_serialized, Box, LevelCount, PointCount, AngleStart, AngleExtent,
                image: null, imageWidth: 0, imageHeight: 0, anisotropic: Anisotropic,
                mask: Mask, maskWidth: MaskWidth, maskHeight: MaskHeight,
                mask2: Mask2, mask2Width: Mask2Width, mask2Height: Mask2Height, previewPose: PreviewPose,
                outlinePoints: OutlinePoints, fixPointX: FixPointX, fixPointY: FixPointY);

    // ── 创建 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 用模板参数在给定图像（可带 ROI 域）上建立形状模板。图像为空、区域为空或原生调用失败一律抛出
    /// <see cref="ContourMatchCapabilityException"/>，不做默认模型兜底。
    /// </summary>
    public static ContourMatchTemplate Create(HImage gray, HRegion? roi, ContourMatchSettings settings,
        byte[]? mask = null, int maskWidth = 0, int maskHeight = 0,
        byte[]? paintMask = null, int paintMaskWidth = 0, int paintMaskHeight = 0,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(gray);
        ArgumentNullException.ThrowIfNull(settings);
        cancellation.ThrowIfCancellationRequested();
        using var count = gray.CountChannels();
        if (count.I != 1) throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_IMAGE_INVALID", "模板图像必须是单通道灰度图。");
        // 训练域优先取模板窗口的正/背景样本区域；无样本时回退到搜索区域（兼容只连 ROI 的旧方案）。
        // 样本区域身份与正负语义见 ContourMatchTrainingRois 与原厂 SignList 证据。
        HOperatorSet.GetImageSize(gray, out var widthTuple, out var heightTuple);
        int imageWidth, imageHeight;
        using (widthTuple) using (heightTuple) { imageWidth = widthTuple.I; imageHeight = heightTuple.I; }
        using var trainingDomain = ContourMatchTrainingRois.BuildDomain(settings.TrainingRois, imageWidth, imageHeight);
        if (trainingDomain is null && roi is null)
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_REGION_EMPTY",
                "模板区域为空：请在模板窗口绘制正样本，或连接有效的搜索区域。");
        using var effectiveRoi = trainingDomain ?? roi;
        // 两层掩膜方向相反，必须分开应用（原厂 otherMask / otherMask2）：
        //   擦除层（otherMask，橡皮擦 fun2）→ 从训练域扣除，不参与边缘提取；
        //   屏蔽/画笔层（otherMask2，fun）→ 并入训练域。
        // 之前用 OR 合成一个掩膜再统一 Difference，会把屏蔽区域也扣掉。
        using var eraseRegion = MaskToRegion(mask, maskWidth, maskHeight);
        using var paintRegion = MaskToRegion(paintMask, paintMaskWidth, paintMaskHeight);
        using var adjustedRoi = ComposeTrainingDomain(effectiveRoi!, eraseRegion, paintRegion);
        using var reduced = gray.ReduceDomain(adjustedRoi ?? effectiveRoi!);
        HOperatorSet.AreaCenter(reduced, out var area, out var ignoredRow, out var ignoredColumn);
        using (area) using (ignoredRow) using (ignoredColumn)
            if (area.D <= 0)
                throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_REGION_EMPTY", "模板区域为空：请先连接有效的搜索区域或在模板窗口绘制正样本。");

        // 压缩比（PyramidScaleLevel）→ 建模板金字塔层数；自动模式交给 HALCON 自适应。
        var manual = settings.PyramidMode == ContourMatchPyramidMode.Manual;
        using var numLevels = manual ? new HTuple(Math.Clamp(settings.PyramidScaleLevel, 1, 10)) : new HTuple("auto");
        // 边缘阈值（EdgeThreshold）→ 建模板对比度；自动模式交给 HALCON 自适应。
        using var contrast = settings.EdgeThresholdMode == ContourMatchThresholdMode.Manual
            ? new HTuple(Math.Clamp(settings.EdgeThreshold, 1, 255))
            : new HTuple("auto");
        using var angleStart = new HTuple(ModelAngleStartRadians);
        using var angleExtent = new HTuple(ModelAngleExtentRadians);
        using var angleStep = new HTuple(ModelAngleStepRadians);
        // HALCON optimization：轮廓点约简方式（用户可选），替代原厂"建模类型"。
        using var optimization = new HTuple(ContourMatchSettings.HalconOptimization(settings.Optimization));
        // 原厂 0x0303 Polarity：0=忽略极性、1=考虑极性 → HALCON CreateShapeModel metric。
        // 模板每次运行按当前 settings 重建，故运行参数变更自然生效（逻辑对照；精度由测试人员验证）。
        using var metric = new HTuple(settings.Polarity == 1 ? "use_polarity" : "ignore_polarity");
        // HALCON min_contrast：忽略低于此对比度的边缘；值越小保留越多弱边（更容忍斑驳表面）。
        using var minContrast = new HTuple(Math.Clamp(settings.MinContrast, 1, 255));

        // 原厂 0x0307/0308 尺度范围：创建期建宽范围多尺度模型，运行期由 ScaleStart/End 筛选。
        // 模型侧范围必须覆盖运行期设置（HALCON 只在模型尺度范围内查找，范围不足会搜不到目标），
        // 故按运行期上下限外扩并夹到声明范围 0.1~10。
        var scaleLower = Math.Clamp(Math.Min(settings.ScaleStart, 0.5) * 0.9, 0.1, 10);
        var scaleUpper = Math.Clamp(Math.Max(settings.ScaleEnd, 2.0) * 1.1, 0.1, 10);
        if (scaleUpper <= scaleLower) scaleUpper = Math.Clamp(scaleLower * 1.2, 0.1, 10);
        using var scaleMin = new HTuple(scaleLower);
        using var scaleMax = new HTuple(scaleUpper);
        using var scaleStep = new HTuple("auto");
        // 原厂「尺度范围XY」：X/Y 尺度独立 → HALCON 各向异性形状模板（行列方向各自尺度范围）。
        var anisotropic = settings.RangeScaleType == ContourMatchRangeScaleType.XY;
        HTuple modelId;
        if (anisotropic)
        {
            var rowLower = Math.Clamp(Math.Min(settings.ScaleYStart, 0.5) * 0.9, 0.1, 10);
            var rowUpper = Math.Clamp(Math.Max(settings.ScaleYEnd, 2.0) * 1.1, 0.1, 10);
            var columnLower = Math.Clamp(Math.Min(settings.ScaleXStart, 0.5) * 0.9, 0.1, 10);
            var columnUpper = Math.Clamp(Math.Max(settings.ScaleXEnd, 2.0) * 1.1, 0.1, 10);
            if (rowUpper <= rowLower) rowUpper = Math.Clamp(rowLower * 1.2, 0.1, 10);
            if (columnUpper <= columnLower) columnUpper = Math.Clamp(columnLower * 1.2, 0.1, 10);
            using var scaleRowMin = new HTuple(rowLower);
            using var scaleRowMax = new HTuple(rowUpper);
            using var scaleRowStep = new HTuple("auto");
            using var scaleColumnMin = new HTuple(columnLower);
            using var scaleColumnMax = new HTuple(columnUpper);
            using var scaleColumnStep = new HTuple("auto");
            HOperatorSet.CreateAnisoShapeModel(reduced, numLevels, angleStart, angleExtent, angleStep,
                scaleRowMin, scaleRowMax, scaleRowStep, scaleColumnMin, scaleColumnMax, scaleColumnStep,
                optimization, metric, contrast, minContrast, out modelId);
        }
        else
        {
            HOperatorSet.CreateScaledShapeModel(reduced, numLevels, angleStart, angleExtent, angleStep,
                scaleMin, scaleMax, scaleStep, optimization, metric, contrast, minContrast, out modelId);
        }
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
                // 真实轮廓点数：GetShapeModelParams 的 NumPoints 在本环境返回的是金字塔层数
                // （实测 5），不能当点数用。这里直接展开一级轮廓数点，并把点随载荷保存
                // （原厂 PointsData），编辑与缩略图命中缓存后不再反序列化 HALCON 模型。
                var outlinePoints = ReadContourPoints(modelId);
                if (outlinePoints.Count <= 0)
                    throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_EMPTY", "模板区域没有可用边缘：请调整区域或边缘阈值后重建模板。");
                var box = ReadBox(modelId);
                // 原厂模板窗口在创建后立即得到当前图像中的模型姿态；不能把
                // GetShapeModelContours 的 (0,0,0) 模型坐标直接当图像坐标显示。
                // 极小/弱模型（例如 20×20 标记 + 自动对比度）上 HALCON FindScaledShapeModel
                // 会原生访问冲突（0xC0000005）直接杀进程，所以过小的模型不做预览匹配。
                // 用模型外接框短边判断，避免误用 NumPoints。
                var shortSide = Math.Min(box.Length1, box.Length2);
                var previewPose = shortSide >= PreviewPoseMinimumExtent
                    ? FindPreviewPose(reduced, modelId, settings, cancellation)
                    : null;
                var serialized = Serialize(modelId);
                return new ContourMatchTemplate(serialized, box, levels, outlinePoints.Count, modelAngleStart.D, modelAngleExtent.D,
                    anisotropic: anisotropic, previewPose: previewPose, outlinePoints: outlinePoints);
            }
        }
    }

    private static ContourMatchTemplatePose? FindPreviewPose(HImage image, HTuple modelId,
        ContourMatchSettings settings, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        using var angleStart = new HTuple(ModelAngleStartRadians);
        using var angleExtent = new HTuple(ModelAngleExtentRadians);
        using var minScore = new HTuple(0.0);
        using var numMatches = new HTuple(1);
        using var maxOverlap = new HTuple(0.0);
        using var subPixel = new HTuple("least_squares");
        using var numLevels = new HTuple(0);
        using var greediness = new HTuple(0.9);
        HTuple rows, columns, angles, scores, scaleRows, scaleColumns;
        if (settings.RangeScaleType == ContourMatchRangeScaleType.XY)
        {
            using var rowMin = new HTuple(Math.Clamp(Math.Min(settings.ScaleYStart, 0.5) * 0.9, 0.1, 10));
            using var rowMax = new HTuple(Math.Clamp(Math.Max(settings.ScaleYEnd, 2.0) * 1.1, 0.1, 10));
            using var columnMin = new HTuple(Math.Clamp(Math.Min(settings.ScaleXStart, 0.5) * 0.9, 0.1, 10));
            using var columnMax = new HTuple(Math.Clamp(Math.Max(settings.ScaleXEnd, 2.0) * 1.1, 0.1, 10));
            HOperatorSet.FindAnisoShapeModel(image, modelId, angleStart, angleExtent,
                rowMin, rowMax, columnMin, columnMax, minScore, numMatches, maxOverlap,
                subPixel, numLevels, greediness, out rows, out columns, out angles,
                out scaleRows, out scaleColumns, out scores);
        }
        else
        {
            using var scaleMin = new HTuple(Math.Clamp(Math.Min(settings.ScaleStart, 0.5) * 0.9, 0.1, 10));
            using var scaleMax = new HTuple(Math.Clamp(Math.Max(settings.ScaleEnd, 2.0) * 1.1, 0.1, 10));
            using var scaleStep = new HTuple("auto");
            HOperatorSet.FindScaledShapeModel(image, modelId, angleStart, angleExtent,
                scaleMin, scaleMax, minScore, numMatches, maxOverlap, subPixel,
                numLevels, greediness, out rows, out columns, out angles, out var scales, out scores);
            scaleRows = scales;
            scaleColumns = scales;
        }
        using (rows) using (columns) using (angles) using (scores) using (scaleRows) using (scaleColumns)
        {
            cancellation.ThrowIfCancellationRequested();
            if (rows.Length == 0) return null;
            var row = rows[0].D;
            var column = columns[0].D;
            var angle = angles[0].D;
            var scaleX = scaleColumns[0].D;
            var scaleY = scaleRows[0].D;
            var scale = (scaleX + scaleY) / 2.0;
            return new ContourMatchTemplatePose(row, column, angle, scale, scaleX, scaleY);
        }
    }

    private static ContourMatchTemplateBox ReadBox(HTuple modelId)
    {
        using var level = new HTuple(1);
        HOperatorSet.GetShapeModelContours(out var contours, modelId, level);
        using (contours)
        {
            HOperatorSet.SmallestRectangle2Xld(contours, out var row, out var column, out var phi, out var length1, out var length2);
            using (row) using (column) using (phi) using (length1) using (length2)
            {
                if (length1.D <= 0 || length2.D <= 0)
                    throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_EMPTY", "模板轮廓退化：区域内没有可用的边缘特征。");
                return new ContourMatchTemplateBox(row.D, column.D, phi.D, length1.D, length2.D);
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
                if (size.I <= 0) throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_SERIALIZE", "模板序列化结果为空。");
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
            writer.WriteNumber("angle-start", AngleStart);
            writer.WriteNumber("angle-extent", AngleExtent);
            if (Image is { Length: > 0 })
            {
                writer.WriteString(ImageKey, Convert.ToBase64String(Image));
                writer.WriteNumber(ImageWidthKey, ImageWidth);
                writer.WriteNumber(ImageHeightKey, ImageHeight);
            }
            if (Anisotropic) writer.WriteBoolean(AnisoKey, true);
            if (PreviewPose is { } pose)
            {
                writer.WriteStartObject(PreviewPoseKey);
                writer.WriteNumber("row", pose.Row);
                writer.WriteNumber("column", pose.Column);
                writer.WriteNumber("angle", pose.Angle);
                writer.WriteNumber("scale", pose.Scale);
                writer.WriteNumber("scale-x", pose.ScaleX);
                writer.WriteNumber("scale-y", pose.ScaleY);
                writer.WriteEndObject();
            }
            if (Mask is { Length: > 0 })
            {
                WriteMask(writer, MaskKey, Mask, MaskWidth, MaskHeight, MaskWidthKey, MaskHeightKey);
            }
            if (Mask2 is { Length: > 0 })
            {
                WriteMask(writer, Mask2Key, Mask2, Mask2Width, Mask2Height, Mask2WidthKey, Mask2HeightKey);
            }
            if (OutlinePoints.Count > 0)
            {
                // 模型轮廓点（原厂 PointsData）：扁平 [x,y,x,y,...]，避免每点一个对象撑大方案。
                writer.WriteStartArray(OutlinePointsKey);
                foreach (var point in OutlinePoints)
                {
                    writer.WriteNumberValue(point.X);
                    writer.WriteNumberValue(point.Y);
                }
                writer.WriteEndArray();
            }
            if (FixPointX is { } fixX && FixPointY is { } fixY)
            {
                // 匹配中心（原厂 _FixPointX/_FixPointY 随模板 BaseData 保存）。
                writer.WriteStartObject(FixPointKey);
                writer.WriteNumber(FixPointXKey, fixX);
                writer.WriteNumber(FixPointYKey, fixY);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteMask(Utf8JsonWriter writer, string key, byte[] mask, int width, int height,
        string widthKey, string heightKey)
    {
        // RLE：[值, 连续长度, 值, 连续长度, ...]，掩膜稀疏时体积远小于 base64。
        writer.WriteStartArray(key);
        var index = 0;
        while (index < mask.Length)
        {
            var value = mask[index];
            var run = 1;
            while (index + run < mask.Length && mask[index + run] == value) run++;
            writer.WriteNumberValue(value);
            writer.WriteNumberValue(run);
            index += run;
        }
        writer.WriteEndArray();
        writer.WriteNumber(widthKey, width);
        writer.WriteNumber(heightKey, height);
    }

    /// <summary>解析模板载荷；空、格式不符或缺项一律抛出明确异常（不猜测、不返回空模板）。</summary>
    public static ContourMatchTemplate Decode(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_REQUIRED", "尚未创建模板：请先在“模板参数”中新建模板。");
        JsonDocument document;
        try { document = JsonDocument.Parse(payload); }
        catch (JsonException error)
        {
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "模板数据不是有效载荷：" + error.Message);
        }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "模板数据必须是一个对象。");
            if (!root.TryGetProperty(FormatKey, out var format) || format.GetString() != Format)
                throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_VERSION",
                    "模板数据版本不受支持（需要 " + Format + "）。");
            if (!root.TryGetProperty(ShapeModelKey, out var model) || model.GetString() is not { Length: > 0 } encoded)
                throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "模板数据缺少形状模板内容。");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(encoded); }
            catch (FormatException error)
            {
                throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "形状模板内容不是有效 base64：" + error.Message);
            }
            var box = new ContourMatchTemplateBox(
                ReadNumber(root, "row"), ReadNumber(root, "column"), ReadNumber(root, "phi"),
                ReadNumber(root, "length1"), ReadNumber(root, "length2"));
            var levels = (int)ReadNumber(root, "levels");
            var points = (int)ReadNumber(root, "points");
            var angleStart = ReadNumber(root, "angle-start");
            var angleExtent = ReadNumber(root, "angle-extent");
            if (box.Length1 <= 0 || box.Length2 <= 0)
                throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "模板外接矩形无效。");
            if (angleExtent <= 0)
                throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "模板角度覆盖范围无效。");
            // 训练原图为可选段：旧模型没有该段时返回 null（不报错），由界面按"无原图"处理。
            byte[]? image = null;
            var imageWidth = 0;
            var imageHeight = 0;
            if (root.TryGetProperty(ImageKey, out var imageNode) && imageNode.GetString() is { Length: > 0 } imageText)
            {
                try { image = Convert.FromBase64String(imageText); }
                catch (FormatException error)
                {
                    throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "训练原图内容不是有效 base64：" + error.Message);
                }
                imageWidth = (int)ReadNumber(root, ImageWidthKey);
                imageHeight = (int)ReadNumber(root, ImageHeightKey);
                if (imageWidth <= 0 || imageHeight <= 0 || image.Length < imageWidth * imageHeight)
                    throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "训练原图尺寸无效。");
            }
            var anisotropic = root.TryGetProperty(AnisoKey, out var anisoNode) && anisoNode.ValueKind == JsonValueKind.True;
            ContourMatchTemplatePose? previewPose = null;
            if (root.TryGetProperty(PreviewPoseKey, out var poseNode) && poseNode.ValueKind == JsonValueKind.Object)
            {
                previewPose = new ContourMatchTemplatePose(
                    ReadNumber(poseNode, "row"), ReadNumber(poseNode, "column"),
                    ReadNumber(poseNode, "angle"), ReadNumber(poseNode, "scale"),
                    ReadNumber(poseNode, "scale-x"), ReadNumber(poseNode, "scale-y"));
            }
            // 擦除掩膜为可选段：旧模型无该段时返回 null（按未擦除处理）。
            var mask = ReadMask(root, MaskKey, MaskWidthKey, MaskHeightKey, "擦除掩膜");
            var mask2 = ReadMask(root, Mask2Key, Mask2WidthKey, Mask2HeightKey, "第二擦除掩膜");
            // 模型轮廓点与匹配中心都是可选段：旧载荷没有时保持 null，由 HALCON / 样本中心回退。
            IReadOnlyList<Point2>? outlinePoints = null;
            if (root.TryGetProperty(OutlinePointsKey, out var outlineNode) && outlineNode.ValueKind == JsonValueKind.Array)
            {
                var values = outlineNode.EnumerateArray().Select(node => node.GetDouble()).ToArray();
                if (values.Length % 2 != 0)
                    throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "模型轮廓点数组不成对。");
                var list = new List<Point2>(values.Length / 2);
                for (var index = 0; index < values.Length; index += 2)
                    list.Add(new Point2(values[index], values[index + 1]));
                if (list.Count > 0) outlinePoints = list;
            }
            double? fixPointX = null;
            double? fixPointY = null;
            if (root.TryGetProperty(FixPointKey, out var fixNode) && fixNode.ValueKind == JsonValueKind.Object)
            {
                fixPointX = ReadNumber(fixNode, FixPointXKey);
                fixPointY = ReadNumber(fixNode, FixPointYKey);
            }
            return new ContourMatchTemplate(bytes, box, levels, points, angleStart, angleExtent, image, imageWidth, imageHeight,
                anisotropic, mask.Mask, mask.Width, mask.Height, mask2.Mask, mask2.Width, mask2.Height, previewPose,
                outlinePoints, fixPointX, fixPointY);
        }
    }

    private static (byte[]? Mask, int Width, int Height) ReadMask(JsonElement root, string key,
        string widthKey, string heightKey, string label)
    {
        if (!root.TryGetProperty(key, out var maskNode) || maskNode.ValueKind != JsonValueKind.Array)
            return (null, 0, 0);
        var width = (int)ReadNumber(root, widthKey);
        var height = (int)ReadNumber(root, heightKey);
        if (width <= 0 || height <= 0)
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", label + "尺寸无效。");
        var mask = new byte[checked(width * height)];
        var offset = 0;
        var values = maskNode.EnumerateArray().Select(node => node.GetInt32()).ToArray();
        if (values.Length % 2 != 0)
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", label + " RLE 数据不成对。");
        for (var index = 0; index < values.Length; index += 2)
        {
            var value = values[index];
            var run = values[index + 1];
            if (value is < 0 or > byte.MaxValue || run <= 0 || offset + run > mask.Length)
                throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", label + " RLE 长度越界。");
            if (value != 0) Array.Fill(mask, (byte)value, offset, run);
            offset += run;
        }
        if (offset != mask.Length)
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", label + " RLE 长度与尺寸不符。");
        return (mask, width, height);
    }

    /// <summary>
    /// 训练域 = (样本/搜索区域 − 擦除层) ∪ 屏蔽层。
    /// 原厂 otherMask（橡皮擦）与 otherMask2（画笔/屏蔽）方向相反，不能合成一个掩膜统一处理；
    /// 返回 null 表示两层都为空，调用方直接使用原域。
    /// </summary>
    private static HRegion? ComposeTrainingDomain(HRegion domain, HRegion? erase, HRegion? paint)
    {
        if (erase is null && paint is null) return null;
        HRegion? result = null;
        var current = domain;
        try
        {
            if (erase is not null)
            {
                result = current.Difference(erase);
                current = result;
            }
            if (paint is not null)
            {
                var painted = current.Union2(paint);
                result?.Dispose();
                result = painted;
            }
            return result;
        }
        catch
        {
            result?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 擦除掩膜像素 → HALCON 区域。掩膜为 W*H 字节（1=擦除），转灰度图后阈值取非零像素。
    /// 无掩膜或无擦除像素时返回 null（正常建模路径不受影响）。
    /// </summary>
    private static HRegion? MaskToRegion(byte[]? mask, int width, int height)
    {
        if (mask is not { Length: > 0 } || width <= 0 || height <= 0) return null;
        var size = width * height;
        if (mask.Length < size) return null;
        var pixels = new byte[size];
        var any = false;
        for (var index = 0; index < size; index++)
        {
            var value = mask[index];
            pixels[index] = value;
            if (value != 0) any = true;
        }
        if (!any) return null;

        // 原厂是自研算法、看不到实现；本方走 HALCON，按其标准做法把掩膜外挂成 HImage：
        // gen_image1_extern 只接受「单个指针」作为 PixelPointer（早先传 new HTuple(int[])
        // 会被展开成 width*height 个值，触发 #1404）。
        // 用 GCHandle 固定字节数组，再 Threshold(1..255) 取非零像素成为擦除域，
        // 语义与原厂 otherMask != 0 判定一致。
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            using var pointerTuple = new HTuple(handle.AddrOfPinnedObject().ToInt64());
            using var widthTuple = new HTuple(width);
            using var heightTuple = new HTuple(height);
            using var clearTuple = new HTuple(0);
            HOperatorSet.GenImage1Extern(out var imageHandle, "byte", widthTuple, heightTuple, pointerTuple, clearTuple);
            using (imageHandle)
            {
                using var image = new HImage(imageHandle);
                HOperatorSet.Threshold(image, out var regionHandle, 1, 255);
                return new HRegion(regionHandle);
            }
        }
        finally
        {
            handle.Free();
        }
    }

    private static double ReadNumber(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number))
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "模板数据缺少有效数值：" + name);
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
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_LOAD", "模板装载失败：" + error.GetErrorMessage());
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    /// <summary>
    /// 读取原生形状模板的一级轮廓，坐标保持 HALCON 模型坐标（X=column，Y=row）。
    /// 原厂生成模板后会把 PointsData 交给 RefreshPoints；编辑器只负责把这组
    /// 已确认的模型轮廓投影到训练图像，不重新推导或伪造轮廓。
    /// </summary>
    public IReadOnlyList<Point2> ReadOutlinePoints()
    {
        // 创建时已把模型轮廓随载荷保存（原厂 PointsData）；命中就直接用，
        // 不必每次编辑/缩略图都反序列化 HALCON 形状模型。
        if (OutlinePoints.Count > 0) return OutlinePoints;
        var modelId = LoadModel();
        try { return ReadContourPoints(modelId); }
        finally { Clear(modelId); }
    }

    /// <summary>展开模型一级轮廓的全部点（模型坐标，X=column、Y=row）。</summary>
    private static IReadOnlyList<Point2> ReadContourPoints(HTuple modelId)
    {
        using var level = new HTuple(1);
        HOperatorSet.GetShapeModelContours(out var contours, modelId, level);
        using (contours)
        {
            HOperatorSet.CountObj(contours, out var count);
            using (count)
            {
                var points = new List<Point2>(Math.Max(0, count.I * 256));
                for (var index = 1; index <= count.I; index++)
                {
                    HOperatorSet.SelectObj(contours, out var selected, index);
                    using (selected)
                    {
                        HOperatorSet.GetContourXld(selected, out var rows, out var columns);
                        using (rows) using (columns)
                        {
                            for (var point = 0; point < rows.Length; point++)
                                points.Add(new Point2(columns[point].D, rows[point].D));
                        }
                    }
                }
                return points.AsReadOnly();
            }
        }
    }

    public static void Clear(HTuple modelId)
    {
        HOperatorSet.ClearShapeModel(modelId);
        modelId.Dispose();
    }

    /// <summary>运行期角度范围必须落在模型覆盖范围内，否则明确报错而不是给出空结果。</summary>
    public void RequireAngleCoverage(ContourMatchSettings settings)
    {
        var requestedStart = settings.AngleStart * Math.PI / 180d;
        var requestedEnd = settings.AngleEnd * Math.PI / 180d;
        var modelEnd = AngleStart + AngleExtent;
        if (requestedStart < AngleStart - 1e-9 || requestedEnd > modelEnd + 1e-9)
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_ANGLE_RANGE",
                "运行角度范围超出模板覆盖范围，请重建模板后再执行。");
    }

    /// <summary>面板用的一行摘要；模板无效时由调用方显示错误，不伪造数值。</summary>
    public string Describe()
        => $"边点数 {PointCount}，金字塔层数 {LevelCount}，外接矩形 {Box.Length1 * 2:0.#}×{Box.Length2 * 2:0.#} 像素";

    public static bool TryDecode(string? payload, out ContourMatchTemplate? template, out string error)
    {
        try
        {
            template = Decode(payload);
            error = "";
            return true;
        }
        catch (ContourMatchCapabilityException failure)
        {
            template = null;
            error = failure.Message;
            return false;
        }
    }
}
