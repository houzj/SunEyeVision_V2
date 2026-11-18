using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.DataClassification.Runtime;

/// <summary>
/// 数据分类的参数身份与布局。字段与页签来自原厂 UI 反编译证据：
/// - <c>DataClassificationParam.xaml</c>：基本参数页 = 输入源下拉 + 动态 IO 订阅列表；
/// - <c>DataClassificationConfig.xaml</c>：分类设置页 = 三列（分类列表 / 条件组 / 条件项）。
/// 算法语义未取证（sourceEvidence=[]，algorithm-semantics 全 9 步 unknown），本阶段只固定 UI 面，
/// 动态集合按原厂五段字节序列化思路以文本字段承载，条数与顺序由文本本身给出。
/// </summary>
public static class DataClassificationFields
{
    /// <summary>基本参数页「输入源」：来自 DataClassificationParam.xaml 的 SelectedModule/FrontModules。</summary>
    public const string InputSource = "input.source";
    /// <summary>基本参数页动态 IO 订阅列表（参数名/类型/订阅关系），原厂序列化于 InputText。</summary>
    public const string Subscriptions = "param.subscriptions";
    /// <summary>分类设置页分类定义（分类名/互斥/条件组/条件项），原厂序列化于 ClassText/GroupText/ConditionText。</summary>
    public const string ClassificationSetting = "param.classification-setting";
    /// <summary>原厂 IsIncompatible（互斥开关），DataClassificationParam.cs:131 证据。</summary>
    public const string OpenMutex = "param.open-mutex";

    private const string BasicPage = "基本参数";
    private const string SettingPage = "分类设置";
    private const string InputSection = "输入源";
    private const string SubscriptionSection = "订阅数据";
    private const string ClassificationSection = "分类配置";

    private static readonly TypeRef Text = TypeRef.ScalarOf(ScalarKind.Text);
    private static readonly TypeRef Boolean = TypeRef.ScalarOf(ScalarKind.Boolean);

    public static ToolSchema Schema { get; } = new(new[]
    {
        new Port(InputSource, Text, EditModes: FieldEditModes.Constant,
            DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(Subscriptions, Text, EditModes: FieldEditModes.Constant,
            DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(OpenMutex, Boolean, EditModes: FieldEditModes.Constant,
            DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(ClassificationSetting, Text, EditModes: FieldEditModes.Constant,
            DefaultPresence: FieldDefaultPresence.UseDeclaredDefault)
    }, Array.Empty<Port>(), new ToolPresentation(new[]
    {
        new FieldPresentation(InputSource, "输入源", InputSection, 0, FieldEditorKind.Scalar)
        {
            Layout = new(BasicPage, InputSection, AllowReference: false)
        },
        new FieldPresentation(Subscriptions, "订阅数据", SubscriptionSection, 1, FieldEditorKind.Scalar)
        {
            Layout = new(BasicPage, SubscriptionSection, AllowReference: false)
        },
        new FieldPresentation(OpenMutex, "互斥分类", ClassificationSection, 2, FieldEditorKind.Scalar)
        {
            Layout = new(SettingPage, ClassificationSection, AllowReference: false,
                BooleanTips: new("已启用", "未启用"))
        },
        new FieldPresentation(ClassificationSetting, "分类条件", ClassificationSection, 3, FieldEditorKind.Scalar)
        {
            Layout = new(SettingPage, ClassificationSection,
                new(OpenMutex, true), AllowReference: false)
        }
    }));

    public static InputValues Read(DataClassificationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(Schema.Inputs, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [InputSource] = settings.InputSource,
            [Subscriptions] = settings.Subscriptions,
            [OpenMutex] = settings.OpenMutex,
            [ClassificationSetting] = settings.ClassificationSetting
        });
    }

    public static DataClassificationSettings Materialize(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var candidate = new InputValues(Schema.Inputs, values.CopyValues());
        var result = new DataClassificationSettings
        {
            InputSource = candidate.Get<string>(InputSource),
            Subscriptions = candidate.Get<string>(Subscriptions),
            OpenMutex = candidate.Get<bool>(OpenMutex),
            ClassificationSetting = candidate.Get<string>(ClassificationSetting)
        };
        var errors = result.Validate();
        if (errors.Count != 0)
            throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return result.Freeze();
    }
}
