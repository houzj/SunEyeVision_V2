using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.MachineLearningClassifier.Runtime;

/// <summary>
/// ML 分类参数身份与布局，字段与默认值来自原厂证据：
/// - 25 项参数名/默认值：<c>tools/imvsmachinelearningclassifiermodu/parameters.json</c>；
/// - 四页签 <c>resources.json.uiTabs</c>：Tab_Basic Params / ClassifierTrain / Tab_Run Params / ResultShow；
/// - 训练页开关联动 <c>MLImagePage.xaml:841-930</c>：TextureFeature 控制 Hog/Lbp/Glcm/Edge/Polar 显隐，
///   GrayFeature 控制 GrayRange/Mean/Deviation/Entropy/Anisotropy/ProjHor/ProjVert/GrayHis 显隐。
/// 算法/模型格式未取证（sourceEvidence=[]，algorithm-semantics 全 unknown），本阶段只固定 UI 面。
/// </summary>
public static class MachineLearningClassifierFields
{
    public const string LoadModelPath = "train.load-model-path";
    public const string SaveModelDataEnable = "train.save-model-enable";
    public const string ClassifyType = "train.classify-type";
    public const string SvmMaxIter = "train.svm-max-iter";
    public const string SvmEsp = "train.svm-esp";

    public const string TextureFeature = "feature.texture";
    public const string HogFeature = "feature.hog";
    public const string LbpFeature = "feature.lbp";
    public const string GlcmFeature = "feature.glcm";
    public const string EdgeFeature = "feature.edge";
    public const string PolarFeature = "feature.polar";
    public const string GrayFeature = "feature.gray";
    public const string GrayRangeFeature = "feature.gray-range";
    public const string GrayMeanFeature = "feature.gray-mean";
    public const string GrayDeviationFeature = "feature.gray-deviation";
    public const string GrayEntropyFeature = "feature.gray-entropy";
    public const string GrayAnisotropyFeature = "feature.gray-anisotropy";
    public const string GrayProjHorFeature = "feature.gray-proj-hor";
    public const string GrayProjVertFeature = "feature.gray-proj-vert";
    public const string GrayHis = "feature.gray-his";

    public const string NumLimitEnable = "limit.num.enable";
    public const string NumLimitLow = "limit.num.low";
    public const string NumLimitHigh = "limit.num.high";
    public const string LabelNameLimitEnable = "limit.label.enable";
    public const string LabelNameLimit = "limit.label.name";

    private const string TrainPage = "分类器训练";
    private const string RunPage = "运行参数";
    private const string ModelSection = "模型";
    private const string TextureSection = "纹理特征";
    private const string GraySection = "灰度特征";
    private const string SvmSection = "训练器";
    private const string LimitSection = "结果判断";

    private static readonly TypeRef Boolean = TypeRef.ScalarOf(ScalarKind.Boolean);
    private static readonly TypeRef Integer = TypeRef.ScalarOf(ScalarKind.Int64);
    private static readonly TypeRef Number = TypeRef.ScalarOf(ScalarKind.Float64);
    private static readonly TypeRef Text = TypeRef.ScalarOf(ScalarKind.Text);
    private static readonly TypeRef ClassifyTypeText = TypeRef.ScalarOf(ScalarKind.Text);

    /// <summary>原厂 ClassifyType 默认 0x1；UI 为 SVM/RF 两项下拉（MLImagePage.xaml:823-826）。</summary>
    public static readonly string[] ClassifyTypeKeys = ["1", "2"];
    public static readonly string[] ClassifyTypeLabels = ["SVM", "RF"];

    public static ToolSchema Schema { get; } = new(new[]
    {
        new Port(LoadModelPath, Text, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(SaveModelDataEnable, Boolean, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(ClassifyType, ClassifyTypeText, EditModes: FieldEditModes.Constant, Choices: new(ClassifyTypeKeys), DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(SvmMaxIter, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(SvmEsp, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(TextureFeature, Boolean, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(HogFeature, Boolean, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(LbpFeature, Boolean, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(GlcmFeature, Boolean, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(EdgeFeature, Boolean, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(PolarFeature, Boolean, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(GrayFeature, Boolean, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(GrayRangeFeature, Boolean, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(GrayMeanFeature, Boolean, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(GrayDeviationFeature, Boolean, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(GrayEntropyFeature, Boolean, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(GrayAnisotropyFeature, Boolean, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(GrayProjHorFeature, Boolean, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(GrayProjVertFeature, Boolean, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(GrayHis, Boolean, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(NumLimitEnable, Boolean, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(NumLimitLow, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(NumLimitHigh, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(LabelNameLimitEnable, Boolean, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(LabelNameLimit, Text, EditModes: FieldEditModes.Constant, DefaultPresence: FieldDefaultPresence.UseDeclaredDefault)
    }, Array.Empty<Port>(), new ToolPresentation(new[]
    {
        new FieldPresentation(LoadModelPath, "加载模型", ModelSection, 0, FieldEditorKind.FolderPath, null, null, DeclaredFieldTemplateKey.FolderPath) { Layout = new(TrainPage, ModelSection) },
        new FieldPresentation(SaveModelDataEnable, "保存模型数据", ModelSection, 1, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.BooleanTips) { Layout = new(TrainPage, ModelSection, AllowReference: false, BooleanTips: new("已启用", "未启用")) },
        new FieldPresentation(ClassifyType, "分类方式", SvmSection, 2, FieldEditorKind.Choice, ClassifyTypeKeys, ClassifyTypeKeys.Zip(ClassifyTypeLabels).ToDictionary(p => p.First, p => p.Second, StringComparer.Ordinal), DeclaredFieldTemplateKey.Enumeration) { Layout = new(TrainPage, SvmSection, AllowReference: false) },
        new FieldPresentation(SvmMaxIter, "最大迭代次数", SvmSection, 3, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Integer) { Layout = new(TrainPage, SvmSection, Minimum: 10, Maximum: 100000, DecimalPlaces: 0, Increment: 1, AllowReference: false) },
        new FieldPresentation(SvmEsp, "精度阈值", SvmSection, 4, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Integer) { Layout = new(TrainPage, SvmSection, Minimum: 1, Maximum: 100000, DecimalPlaces: 0, Increment: 1, AllowReference: false) },
        new FieldPresentation(TextureFeature, "纹理特征", TextureSection, 10, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.BooleanTips) { Layout = new(TrainPage, TextureSection, AllowReference: false, BooleanTips: new("已启用", "未启用")) },
        new FieldPresentation(HogFeature, "HOG 特征", TextureSection, 11, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Boolean) { Layout = new(TrainPage, TextureSection, new(TextureFeature, true), AllowReference: false) },
        new FieldPresentation(LbpFeature, "LBP 特征", TextureSection, 12, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Boolean) { Layout = new(TrainPage, TextureSection, new(TextureFeature, true), AllowReference: false) },
        new FieldPresentation(GlcmFeature, "GLCM 特征", TextureSection, 13, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Boolean) { Layout = new(TrainPage, TextureSection, new(TextureFeature, true), AllowReference: false) },
        new FieldPresentation(EdgeFeature, "边缘特征", TextureSection, 14, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Boolean) { Layout = new(TrainPage, TextureSection, new(TextureFeature, true), AllowReference: false) },
        new FieldPresentation(PolarFeature, "极性特征", TextureSection, 15, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Boolean) { Layout = new(TrainPage, TextureSection, new(TextureFeature, true), AllowReference: false) },
        new FieldPresentation(GrayFeature, "灰度特征", GraySection, 20, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Boolean) { Layout = new(TrainPage, GraySection, AllowReference: false, BooleanTips: new("已启用", "未启用")) },
        new FieldPresentation(GrayRangeFeature, "灰度范围", GraySection, 21, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Boolean) { Layout = new(TrainPage, GraySection, new(GrayFeature, true), AllowReference: false) },
        new FieldPresentation(GrayMeanFeature, "灰度均值", GraySection, 22, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Boolean) { Layout = new(TrainPage, GraySection, new(GrayFeature, true), AllowReference: false) },
        new FieldPresentation(GrayDeviationFeature, "灰度方差", GraySection, 23, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Boolean) { Layout = new(TrainPage, GraySection, new(GrayFeature, true), AllowReference: false) },
        new FieldPresentation(GrayEntropyFeature, "灰度熵", GraySection, 24, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Boolean) { Layout = new(TrainPage, GraySection, new(GrayFeature, true), AllowReference: false) },
        new FieldPresentation(GrayAnisotropyFeature, "灰度各向异性", GraySection, 25, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Boolean) { Layout = new(TrainPage, GraySection, new(GrayFeature, true), AllowReference: false) },
        new FieldPresentation(GrayProjHorFeature, "水平投影", GraySection, 26, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Boolean) { Layout = new(TrainPage, GraySection, new(GrayFeature, true), AllowReference: false) },
        new FieldPresentation(GrayProjVertFeature, "垂直投影", GraySection, 27, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Boolean) { Layout = new(TrainPage, GraySection, new(GrayFeature, true), AllowReference: false) },
        new FieldPresentation(GrayHis, "灰度直方图", GraySection, 28, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Boolean) { Layout = new(TrainPage, GraySection, new(GrayFeature, true), AllowReference: false) },
        new FieldPresentation(NumLimitEnable, "数量判断", LimitSection, 30, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.BooleanTips) { Layout = new(RunPage, LimitSection, AllowReference: false, BooleanTips: new("已启用", "未启用")) },
        new FieldPresentation(NumLimitLow, "数量下限", LimitSection, 31, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.IntegerBettween) { Layout = new(RunPage, LimitSection, new(NumLimitEnable, true), PairWith: NumLimitHigh, Minimum: 0, Maximum: 99999, DecimalPlaces: 0, Increment: 1, PairMinimum: 0, PairMaximum: 99999, PairDisplayName: "数量范围", AllowReference: false) },
        new FieldPresentation(NumLimitHigh, "数量上限", LimitSection, 32, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Integer) { Layout = new(RunPage, LimitSection, new(NumLimitEnable, true), Minimum: 0, Maximum: 99999, DecimalPlaces: 0, Increment: 1, AllowReference: false) },
        new FieldPresentation(LabelNameLimitEnable, "标签名判断", LimitSection, 33, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.BooleanTips) { Layout = new(RunPage, LimitSection, AllowReference: false, BooleanTips: new("已启用", "未启用")) },
        new FieldPresentation(LabelNameLimit, "标签名", LimitSection, 34, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.String) { Layout = new(RunPage, LimitSection, new(LabelNameLimitEnable, true), AllowReference: false) }
    }));

    public static InputValues Read(MachineLearningClassifierSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(Schema.Inputs, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [LoadModelPath] = settings.LoadModelPath,
            [SaveModelDataEnable] = settings.SaveModelDataEnable,
            [ClassifyType] = settings.ClassifyType,
            [SvmMaxIter] = settings.SvmMaxIter,
            [SvmEsp] = settings.SvmEsp,
            [TextureFeature] = settings.TextureFeature,
            [HogFeature] = settings.HogFeature,
            [LbpFeature] = settings.LbpFeature,
            [GlcmFeature] = settings.GlcmFeature,
            [EdgeFeature] = settings.EdgeFeature,
            [PolarFeature] = settings.PolarFeature,
            [GrayFeature] = settings.GrayFeature,
            [GrayRangeFeature] = settings.GrayRangeFeature,
            [GrayMeanFeature] = settings.GrayMeanFeature,
            [GrayDeviationFeature] = settings.GrayDeviationFeature,
            [GrayEntropyFeature] = settings.GrayEntropyFeature,
            [GrayAnisotropyFeature] = settings.GrayAnisotropyFeature,
            [GrayProjHorFeature] = settings.GrayProjHorFeature,
            [GrayProjVertFeature] = settings.GrayProjVertFeature,
            [GrayHis] = settings.GrayHis,
            [NumLimitEnable] = settings.NumLimitEnable,
            [NumLimitLow] = settings.NumLimitLow,
            [NumLimitHigh] = settings.NumLimitHigh,
            [LabelNameLimitEnable] = settings.LabelNameLimitEnable,
            [LabelNameLimit] = settings.LabelNameLimit
        });
    }

    public static MachineLearningClassifierSettings Materialize(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var candidate = new InputValues(Schema.Inputs, values.CopyValues());
        var result = new MachineLearningClassifierSettings
        {
            LoadModelPath = candidate.Get<string>(LoadModelPath),
            SaveModelDataEnable = candidate.Get<bool>(SaveModelDataEnable),
            ClassifyType = candidate.Get<string>(ClassifyType),
            SvmMaxIter = candidate.Get<long>(SvmMaxIter),
            SvmEsp = candidate.Get<long>(SvmEsp),
            TextureFeature = candidate.Get<bool>(TextureFeature),
            HogFeature = candidate.Get<bool>(HogFeature),
            LbpFeature = candidate.Get<bool>(LbpFeature),
            GlcmFeature = candidate.Get<bool>(GlcmFeature),
            EdgeFeature = candidate.Get<bool>(EdgeFeature),
            PolarFeature = candidate.Get<bool>(PolarFeature),
            GrayFeature = candidate.Get<bool>(GrayFeature),
            GrayRangeFeature = candidate.Get<bool>(GrayRangeFeature),
            GrayMeanFeature = candidate.Get<bool>(GrayMeanFeature),
            GrayDeviationFeature = candidate.Get<bool>(GrayDeviationFeature),
            GrayEntropyFeature = candidate.Get<bool>(GrayEntropyFeature),
            GrayAnisotropyFeature = candidate.Get<bool>(GrayAnisotropyFeature),
            GrayProjHorFeature = candidate.Get<bool>(GrayProjHorFeature),
            GrayProjVertFeature = candidate.Get<bool>(GrayProjVertFeature),
            GrayHis = candidate.Get<bool>(GrayHis),
            NumLimitEnable = candidate.Get<bool>(NumLimitEnable),
            NumLimitLow = candidate.Get<long>(NumLimitLow),
            NumLimitHigh = candidate.Get<long>(NumLimitHigh),
            LabelNameLimitEnable = candidate.Get<bool>(LabelNameLimitEnable),
            LabelNameLimit = candidate.Get<string>(LabelNameLimit)
        };
        var errors = result.Validate();
        if (errors.Count != 0)
            throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return result.Freeze();
    }
}
