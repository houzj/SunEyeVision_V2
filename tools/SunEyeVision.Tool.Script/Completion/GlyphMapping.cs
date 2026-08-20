using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Tags;
// WPF 项目里 Accessibility 与 System.Windows 的 Accessibility 命名空间冲突，用短别名 Acc 避开全局命名冲突。
using Acc = Microsoft.CodeAnalysis.Accessibility;

namespace SunEyeVision.Tool.Script.Completion
{
    /// <summary>
    /// 把 CompletionItem.Tags（WellKnownTags）精确映射到 Glyph（含访问修饰符变体）。
    /// 对齐 RoslynPad CodeActionExtensions.GetGlyph，逐 tag 匹配。
    /// </summary>
    internal static class GlyphMapping
    {
        public static Glyph GetGlyph(this CompletionItem completionItem)
        {
            return GetGlyph(completionItem.Tags);
        }

        public static Glyph GetGlyph(ImmutableArray<string> tags)
        {
            foreach (var tag in tags)
            {
                switch (tag)
                {
                    case WellKnownTags.Assembly:
                        return Glyph.Assembly;

                    case WellKnownTags.File:
                        return tags.Contains(LanguageNames.VisualBasic) ? Glyph.BasicFile : Glyph.CSharpFile;

                    case WellKnownTags.Project:
                        return tags.Contains(LanguageNames.VisualBasic) ? Glyph.BasicProject : Glyph.CSharpProject;

                    case WellKnownTags.Class:
                        switch (GetAccessibility(tags))
                        {
                            case Acc.Protected: return Glyph.ClassProtected;
                            case Acc.Private: return Glyph.ClassPrivate;
                            case Acc.Internal: return Glyph.ClassInternal;
                            default: return Glyph.ClassPublic;
                        }

                    case WellKnownTags.Constant:
                        switch (GetAccessibility(tags))
                        {
                            case Acc.Protected: return Glyph.ConstantProtected;
                            case Acc.Private: return Glyph.ConstantPrivate;
                            case Acc.Internal: return Glyph.ConstantInternal;
                            default: return Glyph.ConstantPublic;
                        }

                    case WellKnownTags.Delegate:
                        switch (GetAccessibility(tags))
                        {
                            case Acc.Protected: return Glyph.DelegateProtected;
                            case Acc.Private: return Glyph.DelegatePrivate;
                            case Acc.Internal: return Glyph.DelegateInternal;
                            default: return Glyph.DelegatePublic;
                        }

                    case WellKnownTags.Enum:
                        switch (GetAccessibility(tags))
                        {
                            case Acc.Protected: return Glyph.EnumProtected;
                            case Acc.Private: return Glyph.EnumPrivate;
                            case Acc.Internal: return Glyph.EnumInternal;
                            default: return Glyph.EnumPublic;
                        }

                    case WellKnownTags.EnumMember:
                        switch (GetAccessibility(tags))
                        {
                            case Acc.Protected: return Glyph.EnumMemberProtected;
                            case Acc.Private: return Glyph.EnumMemberPrivate;
                            case Acc.Internal: return Glyph.EnumMemberInternal;
                            default: return Glyph.EnumMemberPublic;
                        }

                    case WellKnownTags.Error:
                        return Glyph.Error;

                    case WellKnownTags.Event:
                        switch (GetAccessibility(tags))
                        {
                            case Acc.Protected: return Glyph.EventProtected;
                            case Acc.Private: return Glyph.EventPrivate;
                            case Acc.Internal: return Glyph.EventInternal;
                            default: return Glyph.EventPublic;
                        }

                    case WellKnownTags.ExtensionMethod:
                        switch (GetAccessibility(tags))
                        {
                            case Acc.Protected: return Glyph.ExtensionMethodProtected;
                            case Acc.Private: return Glyph.ExtensionMethodPrivate;
                            case Acc.Internal: return Glyph.ExtensionMethodInternal;
                            default: return Glyph.ExtensionMethodPublic;
                        }

                    case WellKnownTags.Field:
                        switch (GetAccessibility(tags))
                        {
                            case Acc.Protected: return Glyph.FieldProtected;
                            case Acc.Private: return Glyph.FieldPrivate;
                            case Acc.Internal: return Glyph.FieldInternal;
                            default: return Glyph.FieldPublic;
                        }

                    case WellKnownTags.Interface:
                        switch (GetAccessibility(tags))
                        {
                            case Acc.Protected: return Glyph.InterfaceProtected;
                            case Acc.Private: return Glyph.InterfacePrivate;
                            case Acc.Internal: return Glyph.InterfaceInternal;
                            default: return Glyph.InterfacePublic;
                        }

                    case WellKnownTags.Intrinsic:
                        return Glyph.Intrinsic;

                    case WellKnownTags.Keyword:
                        return Glyph.Keyword;

                    case WellKnownTags.Label:
                        return Glyph.Label;

                    case WellKnownTags.Local:
                        return Glyph.Local;

                    case WellKnownTags.Namespace:
                        return Glyph.Namespace;

                    case WellKnownTags.Method:
                        switch (GetAccessibility(tags))
                        {
                            case Acc.Protected: return Glyph.MethodProtected;
                            case Acc.Private: return Glyph.MethodPrivate;
                            case Acc.Internal: return Glyph.MethodInternal;
                            default: return Glyph.MethodPublic;
                        }

                    case WellKnownTags.Module:
                        switch (GetAccessibility(tags))
                        {
                            case Acc.Protected: return Glyph.ModulePublic;
                            case Acc.Private: return Glyph.ModulePrivate;
                            case Acc.Internal: return Glyph.ModuleInternal;
                            default: return Glyph.ModulePublic;
                        }

                    case WellKnownTags.Folder:
                        return Glyph.OpenFolder;

                    case WellKnownTags.Operator:
                        return Glyph.Operator;

                    case WellKnownTags.Parameter:
                        return Glyph.Parameter;

                    case WellKnownTags.Property:
                        switch (GetAccessibility(tags))
                        {
                            case Acc.Protected: return Glyph.PropertyProtected;
                            case Acc.Private: return Glyph.PropertyPrivate;
                            case Acc.Internal: return Glyph.PropertyInternal;
                            default: return Glyph.PropertyPublic;
                        }

                    case WellKnownTags.RangeVariable:
                        return Glyph.RangeVariable;

                    case WellKnownTags.Reference:
                        return Glyph.Reference;

                    case WellKnownTags.NuGet:
                        return Glyph.NuGet;

                    case WellKnownTags.Structure:
                        switch (GetAccessibility(tags))
                        {
                            case Acc.Protected: return Glyph.StructureProtected;
                            case Acc.Private: return Glyph.StructurePrivate;
                            case Acc.Internal: return Glyph.StructureInternal;
                            default: return Glyph.StructurePublic;
                        }

                    case WellKnownTags.TypeParameter:
                        return Glyph.TypeParameter;

                    case WellKnownTags.Snippet:
                        return Glyph.Snippet;

                    case WellKnownTags.Warning:
                        return Glyph.CompletionWarning;

                    case WellKnownTags.StatusInformation:
                        return Glyph.StatusInformation;
                }
            }

            return Glyph.None;
        }

        private static Acc GetAccessibility(ImmutableArray<string> tags)
        {
            if (tags.Contains(WellKnownTags.Public)) return Acc.Public;
            if (tags.Contains(WellKnownTags.Protected)) return Acc.Protected;
            if (tags.Contains(WellKnownTags.Internal)) return Acc.Internal;
            if (tags.Contains(WellKnownTags.Private)) return Acc.Private;
            return Acc.NotApplicable;
        }
    }
}
