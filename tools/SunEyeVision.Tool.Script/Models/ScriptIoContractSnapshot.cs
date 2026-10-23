using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SunEyeVision.Tool.Script.ScriptEngine;

namespace SunEyeVision.Tool.Script.Models;

internal sealed record ScriptIoField(Guid FieldId, string Name, ScriptIoTypeDescriptor Type, bool IsInput);

/// <summary>只有合法草稿可原子发布。成员ID与生成代码身份分开，绑定值不参与Emit键。</summary>
internal sealed class ScriptIoContractSnapshot
{
    public ImmutableArray<ScriptIoField> Fields { get; }
    public string GeneratedCode { get; }
    public string Identity { get; }
    private ScriptIoContractSnapshot(IEnumerable<ScriptIoField> fields)
    {
        Fields = fields.OrderBy(f => f.Name, StringComparer.Ordinal).ToImmutableArray();
        Identity = ScriptReferenceSnapshot.Hash(Fields.SelectMany(f => new[] {
            f.FieldId.ToString("N"), f.Name, f.Type.ClrType.AssemblyQualifiedName!, f.IsInput.ToString() }));
        var code = new StringBuilder("using System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Text;\nusing System.IO;\nusing SunEyeVision.Plugin.SDK.Scripting;\nusing SunEyeVision.Plugin.SDK.Vision.Contracts;\nusing SunEyeVision.Plugin.SDK.Execution;\npublic partial class UserScript : Script\n{\n");
        foreach (var field in Fields)
            code.AppendLine($"public {field.Type.CsTypeName} {field.Name} {(field.IsInput ? field.Type.DefaultExpression : "")};");
        GeneratedCode = code.AppendLine("}").ToString();
    }
    public static ScriptIoContractSnapshot Capture(IEnumerable<ScriptInputConfig> inputs, IEnumerable<ScriptOutputConfig> outputs)
    {
        var fields = inputs.Select(f => new ScriptIoField(f.FieldId, f.Name, ScriptIoTypeDescriptor.From(f.TypeName), true))
            .Concat(outputs.Select(f => new ScriptIoField(f.FieldId, f.Name, ScriptIoTypeDescriptor.From(f.TypeName), false))).ToArray();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<Guid>();
        var reserved = new HashSet<string>(StringComparer.Ordinal) { "UserScript", "Init", "Process", "Release", "Context", "LogInfo", "LogWarning", "LogError", "GetGlobal", "SetGlobal", "CancellationToken", "ThrowIfCancellationRequested" };
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Name) || field.Name.StartsWith("@", StringComparison.Ordinal)
                || SyntaxFactory.ParseToken(field.Name).ValueText != field.Name || !SyntaxFacts.IsValidIdentifier(field.Name)
                || SyntaxFacts.GetKeywordKind(field.Name) != SyntaxKind.None || reserved.Contains(field.Name))
                throw new InvalidOperationException($"变量名称 '{field.Name}' 无效或与脚本成员冲突，请使用有效C#字段名。");
            if (!names.Add(field.Name)) throw new InvalidOperationException($"输入输出变量名称重复: {field.Name}");
            if (field.FieldId == Guid.Empty || !ids.Add(field.FieldId)) throw new InvalidOperationException($"变量 '{field.Name}' 的FieldId为空或重复。");
        }
        return new(fields);
    }
}
