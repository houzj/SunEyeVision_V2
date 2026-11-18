using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.MachineLearningClassifier.Runtime;

/// <summary>
/// ML 分类唯一持久化设置。25 项默认值来自 <c>tools/imvsmachinelearningclassifiermodu/parameters.json</c>。
/// 算法/模型格式未取证，只校验设置自身（数量范围、训练器参数边界），不伪造分类数值逻辑。
/// </summary>
public sealed record MachineLearningClassifierSettings : IToolSettings<MachineLearningClassifierSettings>
{
    public string LoadModelPath { get; init; } = string.Empty;
    public bool SaveModelDataEnable { get; init; }
    /// <summary>原厂 ClassifyType 默认 0x1（SVM）。</summary>
    public string ClassifyType { get; init; } = "1";
    public long SvmMaxIter { get; init; } = 1000;
    public long SvmEsp { get; init; } = 1000;
    public bool TextureFeature { get; init; } = true;
    public bool HogFeature { get; init; } = true;
    public bool LbpFeature { get; init; }
    public bool GlcmFeature { get; init; }
    public bool EdgeFeature { get; init; }
    public bool PolarFeature { get; init; }
    public bool GrayFeature { get; init; } = true;
    public bool GrayRangeFeature { get; init; } = true;
    public bool GrayMeanFeature { get; init; }
    public bool GrayDeviationFeature { get; init; }
    public bool GrayEntropyFeature { get; init; }
    public bool GrayAnisotropyFeature { get; init; }
    public bool GrayProjHorFeature { get; init; }
    public bool GrayProjVertFeature { get; init; }
    public bool GrayHis { get; init; } = true;
    public bool NumLimitEnable { get; init; }
    public long NumLimitLow { get; init; } = 1;
    public long NumLimitHigh { get; init; } = 99999;
    public bool LabelNameLimitEnable { get; init; }
    public string LabelNameLimit { get; init; } = string.Empty;

    public MachineLearningClassifierSettings Freeze() => this;

    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors = new List<Diagnostic>();
        if (!MachineLearningClassifierFields.ClassifyTypeKeys.Contains(ClassifyType, StringComparer.Ordinal))
            errors.Add(new("MLCLASSIFIER_SETTINGS", "分类方式无效。", MachineLearningClassifierFields.ClassifyType));
        if (SvmMaxIter < 10 || SvmMaxIter > 100000)
            errors.Add(new("MLCLASSIFIER_SETTINGS", "最大迭代次数超出 10 到 100000。", MachineLearningClassifierFields.SvmMaxIter));
        if (SvmEsp < 1 || SvmEsp > 100000)
            errors.Add(new("MLCLASSIFIER_SETTINGS", "精度阈值超出 1 到 100000。", MachineLearningClassifierFields.SvmEsp));
        if (NumLimitLow < 0 || NumLimitHigh > 99999 || NumLimitLow > NumLimitHigh)
            errors.Add(new("MLCLASSIFIER_SETTINGS", "数量范围无效。", MachineLearningClassifierFields.NumLimitLow));
        return errors.AsReadOnly();
    }
}
