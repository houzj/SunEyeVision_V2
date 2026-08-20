namespace SunEyeVision.Tool.Script.Completion
{
    /// <summary>
    /// 补全项类型图标枚举（对齐 RoslynPad，从 Roslyn Microsoft.CodeAnalysis.Glyph 复刻）。
    /// 完整含访问修饰符变体（Public/Protected/Private/Internal），配合 Glyphs.xaml 的 VS 官方图标资源渲染。
    /// </summary>
    public enum Glyph
    {
        None,

        Assembly,

        BasicFile,
        BasicProject,

        ClassPublic,
        ClassProtected,
        ClassPrivate,
        ClassInternal,

        CSharpFile,
        CSharpProject,

        ConstantPublic,
        ConstantProtected,
        ConstantPrivate,
        ConstantInternal,

        DelegatePublic,
        DelegateProtected,
        DelegatePrivate,
        DelegateInternal,

        EnumPublic,
        EnumProtected,
        EnumPrivate,
        EnumInternal,

        EnumMemberPublic,
        EnumMemberProtected,
        EnumMemberPrivate,
        EnumMemberInternal,

        Error,
        StatusInformation,

        EventPublic,
        EventProtected,
        EventPrivate,
        EventInternal,

        ExtensionMethodPublic,
        ExtensionMethodProtected,
        ExtensionMethodPrivate,
        ExtensionMethodInternal,

        FieldPublic,
        FieldProtected,
        FieldPrivate,
        FieldInternal,

        InterfacePublic,
        InterfaceProtected,
        InterfacePrivate,
        InterfaceInternal,

        Intrinsic,

        Keyword,

        Label,

        Local,

        Namespace,

        MethodPublic,
        MethodProtected,
        MethodPrivate,
        MethodInternal,

        ModulePublic,
        ModuleProtected,
        ModulePrivate,
        ModuleInternal,

        OpenFolder,

        Operator,

        Parameter,

        PropertyPublic,
        PropertyProtected,
        PropertyPrivate,
        PropertyInternal,

        RangeVariable,

        Reference,

        StructurePublic,
        StructureProtected,
        StructurePrivate,
        StructureInternal,

        TypeParameter,

        Snippet,

        CompletionWarning,

        AddReference,
        NuGet
    }
}
