using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.FrameMean.Runtime;

/// <summary>
/// 帧均值的声明唯一真源（原厂 <c>IMVSFrameMeanModuAlgorithm.xml</c> +
/// <c>IMVSFrameMeanModuAlgorithmTab.xml</c> + <c>IMVSFrameMeanModuDisplay.xml</c>）。
/// 参数页、输入行、结果树、属性列投影与显示配置全部由这里生成。
/// </summary>
internal static class FrameMeanManifest
{
    /// <summary>结果端口身份（与 <see cref="FrameMeanOutput"/> 逐字一致）。</summary>
    public const string Image = "output.image";
    public const string ImageCount = "output.image-count";
    public const string ModuleTime = "output.module-time-ms";
    public const string ModuleStatus = "output.module-status";

    public static ToolManifest Value { get; } = Build();

    private static TypeRef ImageType => TypeRef.RecordOf("frame-mean.image", new[]
    {
        new Port("data", TypeRef.ResourceOf(ResourceKind.Image)),
        new Port("width", DataType.Integer, Storage: NumericStorage.Int32),
        new Port("height", DataType.Integer, Storage: NumericStorage.Int32),
        new Port("pixel-format", DataType.Integer, Nullable: true, Storage: NumericStorage.Int32)
    });

    private static ToolManifest Build() => new(
        "FrameMean", "帧均值", "多帧图像统计平均，输出累计均值图像", "frame-mean", "图像处理", 10,
        new[]
        {
            // 原厂 ProcStep：文本枚举，取值 1/2/3，默认统计当前图像。
            new ToolParameterDeclaration(FrameMeanFields.ProcStep, "处理步骤", "运行参数", DataType.Text, "1",
                propertyName: nameof(FrameMeanSettings.ProcStep),
                displayType: DeclaredFieldTemplateKey.Enumeration,
                // 取值与文案复用工具内共享常量，避免 manifest 与设置校验各写一份。
                choices: FrameMeanFields.ProcStepKeys
                    .Zip(FrameMeanFields.ProcStepLabels)
                    .Select(pair => new ToolChoice(pair.First, pair.Second))
                    .ToArray())
        },
        new ToolResultDeclaration(null,
            new[]
            {
                new ToolResultField(Image, "均值图像", ImageType, nullable: true,
                    children: new[]
                    {
                        new ToolResultField("data", "图像数据", TypeRef.ResourceOf(ResourceKind.Image)),
                        new ToolResultField("width", "图像宽度", DataType.Integer),
                        new ToolResultField("height", "图像高度", DataType.Integer),
                        new ToolResultField("pixel-format", "图像像素格式", DataType.Integer)
                    }),
                new ToolResultField(ImageCount, "已统计图像数量", DataType.Integer, storage: NumericStorage.Int32),
                new ToolResultField(ModuleTime, "模块耗时 (ms)", DataType.Number, storage: NumericStorage.Float32),
                new ToolResultField(ModuleStatus, "模块状态", DataType.Integer,
                    storage: NumericStorage.Int32, showInResultPanel: false)
            },
            slots: null,
            // 列标识沿用原投影键（DEFAULT/count/time），保证已保存方案的列选择不变。
            columns: new[]
            {
                new ToolResultColumn(Image, "均值图像", key: "DEFAULT"),
                new ToolResultColumn(ImageCount, "已统计图像数量", key: "count"),
                new ToolResultColumn(ModuleTime, "模块耗时", key: "time")
            },
            kind: ResultProjectionKind.PropertyList,
            shape: ToolResultShape.PropertyList),
        new ToolDisplayDeclaration(new[]
        {
            // 原厂 Display.xml 只有一个 CheckZone 对象：ok=#0000ff、ng=#ff0000、linewidth=2。
            new ToolDisplayItem(ToolDisplayRole.Inspection, "检测区域", groupKey: "check-zone",
                ok: new ResultDisplayColor(0, 0, 255), ng: new ResultDisplayColor(255, 0, 0), width: 2)
        }),
        ToolInputTemplate.Image,
        inputPresentations: new[]
        {
            new ToolInputPresentation(FrameMeanTool.ImageInput, "图像输入", "基本参数", "图像输入")
        });
}
