using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests.Manifest;

/// <summary>
/// 以 FrameMean 现有声明为基准，验证 manifest 能等价表达：
/// ① 枚举参数（取值+显示名）；② 输入行呈现；③ 属性列结果（标量端口而非行表）；
/// ④ 结果树节点（含子节点与隐藏项）；⑤ 显示项（检查区域）。
/// 这是“清单够不够用”的小步验证，不迁移工具本体。
/// </summary>
public sealed class FrameMeanManifestEquivalenceTests
{
    private const string Image = "output.image";
    private const string ImageCount = "output.image-count";
    private const string ModuleTime = "output.module-time-ms";
    private const string ModuleStatus = "output.module-status";

    private static TypeRef ImageType => TypeRef.RecordOf("frame-mean.image", new[]
    {
        new Port("data", TypeRef.ResourceOf(ResourceKind.Image)),
        new Port("width", DataType.Integer, Storage: NumericStorage.Int32),
        new Port("height", DataType.Integer, Storage: NumericStorage.Int32),
        new Port("pixel-format", DataType.Integer, Nullable: true, Storage: NumericStorage.Int32)
    });

    private static ToolManifest FrameMean() => new(
        "FrameMean", "帧均值", "多帧图像统计平均，输出累计均值图像", "frame-mean", "图像处理", 10,
        new[]
        {
            new ToolParameterDeclaration("proc.step", "处理步骤", "运行参数", DataType.Text, "1",
                propertyName: "ProcStep",
                choices: new[]
                {
                    new ToolChoice("1", "统计当前图像"),
                    new ToolChoice("2", "不统计当前图像"),
                    new ToolChoice("3", "清空统计图像")
                })
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
            columns: new[]
            {
                new ToolResultColumn(Image, "均值图像"),
                new ToolResultColumn(ImageCount, "已统计图像数量"),
                new ToolResultColumn(ModuleTime, "模块耗时")
            },
            kind: ResultProjectionKind.PropertyList,
            shape: ToolResultShape.PropertyList),
        new ToolDisplayDeclaration(new[]
        {
            new ToolDisplayItem(ToolDisplayRole.Inspection, "检测区域", groupKey: "check-zone",
                ok: new ResultDisplayColor(0, 0, 255), ng: new ResultDisplayColor(255, 0, 0), width: 2)
        }),
        ToolInputTemplate.Image,
        inputPresentations: new[]
        {
            new ToolInputPresentation("input.image", "图像输入", "基本参数", "图像输入")
        });

    [Fact]
    public void Setting_choice_matches_the_original_enum_declaration()
    {
        var schema = ManifestCompiler.SettingsSchema(FrameMean());
        var port = Assert.Single(schema.Inputs);
        Assert.Equal("proc.step", port.Id);
        Assert.Equal(DataType.Text, port.Type);
        Assert.Equal(new[] { "1", "2", "3" }, port.Choices!.Keys);
        Assert.Equal(FieldDefaultPresence.UseDeclaredDefault, port.DefaultPresence);
        var field = Assert.Single(schema.Presentation!.Fields);
        Assert.Equal(FieldEditorKind.Choice, field.Editor);
        Assert.Equal("统计当前图像", field.ChoiceLabels["1"]);
        Assert.False(field.Layout!.AllowReference);
        Assert.Equal("运行参数", field.Layout.Page);
    }

    [Fact]
    public void Execution_schema_declares_the_input_row_before_settings()
    {
        var schema = ManifestCompiler.ExecutionSchema(FrameMean());
        Assert.Equal(new[] { "input.image", "proc.step" }, schema.Inputs.Select(port => port.Id));
        // 原厂 Algorithm.xml（IO 声明）与 AlgorithmTab.xml（界面）是两份：
        // 执行 Schema 只声明输入行，参数行的呈现归 SettingsSchema。
        Assert.Single(schema.Presentation!.Fields);
        var input = schema.Presentation.Fields[0];
        Assert.Equal("input.image", input.FieldId);
        Assert.Equal(FieldEditorKind.Image, input.Editor);
        Assert.Equal("图像输入", input.DisplayName);
        Assert.Equal("基本参数", input.Layout!.Page);
        Assert.Equal(0, input.Order);
        Assert.Equal("input.image", schema.WorkflowImageInputId);
        var settings = ManifestCompiler.SettingsSchema(FrameMean());
        Assert.Equal(new[] { "proc.step" }, settings.Presentation!.Fields.Select(field => field.FieldId));
    }

    [Fact]
    public void Property_list_result_declares_scalar_ports_and_tree_nodes()
    {
        var schema = ManifestCompiler.ResultSchema(FrameMean());
        Assert.Equal(new[] { Image, ImageCount, ModuleTime, ModuleStatus },
            schema.Outputs.Select(port => port.Id));
        Assert.True(schema.Outputs[0].Nullable);
        Assert.Equal(NumericStorage.Int32, schema.Outputs[1].Storage);
        Assert.Equal(NumericStorage.Float32, schema.Outputs[2].Storage);
        Assert.False(schema.Outputs[3].Nullable);
        Assert.NotNull(schema.OutputDisplay);
        Assert.Equal(new[] { Image, ImageCount, ModuleTime, ModuleStatus },
            schema.OutputDisplay!.Select(node => node.FieldId));
        Assert.Equal(4, schema.OutputDisplay.First().Children.Count);
        Assert.False(schema.OutputDisplay.Single(node => node.FieldId == ModuleStatus).DisplayInResultPanel);
    }

    [Fact]
    public void Property_list_projection_is_one_row_with_port_columns()
    {
        var projection = new FrameMeanProjectionProbe().Build(FrameMean());
        Assert.Equal(ResultProjectionKind.PropertyList, projection.Kind);
        Assert.Equal(ResultCardinality.One, projection.Cardinality);
        Assert.Equal(new[] { Image, ImageCount, ModuleTime },
            projection.Columns.Select(column => column.Key));
        Assert.Null(projection.Rows);
        Assert.Null(projection.ElementIdentityFieldId);
        Assert.Equal(Image, projection.Columns[0].Value!.PortId);
        Assert.Empty(projection.Columns[0].Value!.MemberFieldPath);
    }

    [Fact]
    public void Display_item_generates_the_check_zone_group()
    {
        var schema = ManifestCompiler.DisplaySchema(FrameMean());
        Assert.Equal(new[]
        {
            "display.check-zone.visible", "display.check-zone.ok", "display.check-zone.ng",
            "display.check-zone.opacity", "display.check-zone.width"
        }, schema.Inputs.Select(port => port.Id));
        var defaults = ManifestCompiler.DisplayDefaults(FrameMean());
        Assert.True(defaults.Get<bool>("display.check-zone.visible"));
        var color = defaults.Get<RecordValue>("display.check-zone.ok");
        Assert.Equal(0L, color.Get<long>("r"));
        Assert.Equal(0L, color.Get<long>("g"));
        Assert.Equal(255L, color.Get<long>("b"));
        Assert.Equal(2d, defaults.Get<double>("display.check-zone.width"));
    }

    /// <summary>借用基类的投影构建逻辑，避免在测试里复制一遍。</summary>
    private sealed class FrameMeanProjectionProbe : ToolDefinition<ProbeSettings, ProbeOutput>
    {
        private ToolManifest _manifest = null!;
        internal ResultProjectionDescriptor Build(ToolManifest manifest)
        {
            _manifest = manifest;
            return ResultProjection;
        }
        protected override ToolManifest Manifest => _manifest;
        public override IToolSession<ProbeSettings, ProbeOutput> CreateSession()
            => throw new NotSupportedException();
    }

    private sealed record ProbeSettings : IToolSettings<ProbeSettings>
    {
        public string ProcStep { get; init; } = "1";
        public ProbeSettings Freeze() => this;
    }

    private sealed class ProbeOutput : IToolOutput
    {
        public IReadOnlyDictionary<string, object?> ToValues() => new Dictionary<string, object?>();
    }
}
