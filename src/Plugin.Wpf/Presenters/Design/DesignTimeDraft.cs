using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Plugin.Wpf.Presenters.Design;

/// <summary>
/// 设计时草稿：用工具自身的默认设置构造一份可渲染的参数页上下文。
/// 不触达发现、文档、算法会话或原生资源，只在设计器与验收探针里使用。
/// </summary>
public sealed class DesignTimeDraft : IToolSettingsDraft
{
    private readonly FieldSettingsDraft _draft;
    private readonly IToolDefinition _tool;
    private readonly IToolSettingsFields _declaration;

    public DesignTimeDraft(IToolDefinition tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        _tool = tool;
        _declaration = tool as IToolSettingsFields
            ?? throw new ArgumentException("Settings are not declared on the tool.", nameof(tool));

        var schema = ResultDisplayFields.EditorSchema(tool);
        var defaults = _declaration.ReadSettings(tool.CreateSettings()).CopyValues();
        if (tool is IToolDisplaySettingsProvider display)
            defaults = defaults.Concat(display.CreateDisplaySettings().CopyValues())
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        _draft = new FieldSettingsDraft(schema, new FieldSettings(schema.Inputs,
            defaults.ToDictionary(pair => pair.Key, pair => (ParameterSetting)new ConstantSetting(pair.Value),
                StringComparer.Ordinal)));
    }

    public ToolSchema Schema => _draft.Schema;
    public long Revision => _draft.Revision;
    public FieldSettings CaptureFields() => _draft.CaptureFields();

    public FieldSettings CaptureValidatedFields()
    {
        var fields = _draft.CaptureValidatedFields();
        // 含引用的草稿无法物化成设置，设计时直接接受当前快照。
        if (fields.Capture().Values.Any(value => value is ReferenceSetting)) return fields;
        var settings = _declaration.MaterializeSettings(
            fields.Select(_declaration.SettingsSchema.Inputs).Materialize(new Dictionary<string, object?>()));
        var errors = _tool.Validate(settings);
        if (errors.Count != 0)
            throw new ArgumentException(string.Join("; ", errors.Select(error => error.Message)));
        return fields;
    }

    public void ApplyFields(IReadOnlyDictionary<string, ParameterSetting> changes, Action<FieldSettings> acceptCandidate)
        => _draft.ApplyFields(changes, acceptCandidate);
}
