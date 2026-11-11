using System.Collections.ObjectModel;
using System.Collections.Frozen;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

/// <summary>A view over immutable document state and a definition admitted by the session directory.</summary>
internal sealed class AdmittedToolConfiguration
{
    private readonly IToolDefinition _definition;
    internal IToolDefinition Definition => _definition;
    private readonly Func<ToolConfiguration, AdmittedToolConfiguration> _factory;
    internal RuntimeDirectorySnapshot Directory { get; }
    internal ToolConfiguration Configuration { get; }
    internal ToolSchema Schema { get; }
    internal ToolSchema SettingsSchema { get; }
    internal ToolSchema DisplaySchema { get; }
    internal ToolHostDeclarationSnapshot HostDeclarations { get; }
    internal IReadOnlySet<string> ActiveInputs { get; }
    internal FieldSettings DormantParameterDefaults { get; }
    internal IReadOnlyDictionary<string, ParameterReferenceBehavior> ReferenceBehaviors { get; }
    internal IReadOnlyDictionary<string, string> BindingNames { get; }
    internal IReadOnlyList<ManualReferenceSource> ManualReferenceSources { get; }

    internal bool IsDisplayField(string id) => DisplaySchema.Inputs.Any(port => port.Id == id);
    internal bool IsExecutionFieldActive(string id) => !IsDisplayField(id) && Configuration.Fields.Declarations.ContainsKey(id) &&
        !DormantParameterDefaults.Declarations.ContainsKey(id) &&
        (!Schema.Inputs.Any(port => port.Id == id) || ActiveInputs.Contains(id));
    internal IReadOnlyList<Diagnostic> ValidateForExecution()
    {
        var inputs = (_definition as IToolExecutionInputs)?.ValidateExecutionInputs(Configuration.Fields) ?? Array.Empty<Diagnostic>();
        if (inputs.Count != 0) return inputs;
        return ValidateForEditing();
    }
    internal IReadOnlyList<Diagnostic> ValidateForEditing()
    {
        _ = ResultDisplayFields.Capture(_definition, Configuration.Fields);
        var fields = Configuration.Fields.Select(SettingsSchema.Inputs);
        var known = (_definition as IToolKnownSettingsValidation)?.ValidateKnownSettings(fields) ?? Array.Empty<Diagnostic>();
        if (known.Count != 0) return known;
        if (fields.Capture().Values.Any(value => value is ReferenceSetting or VariableSetting)) return Array.Empty<Diagnostic>();
        var settings = (IToolSettingsFields)_definition;
        return _definition.Validate(_definition.FreezeSettings(settings.MaterializeSettings(fields.Materialize(new Dictionary<string, object?>()))));
    }
    internal string ConfigurationIdentity => CaptureConfigurationIdentity(false);
    internal string ExecutionConfigurationIdentity => CaptureConfigurationIdentity(true);

    private string CaptureConfigurationIdentity(bool executionOnly)
    {
        using var buffer = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("ToolId", Configuration.ToolId);
            writer.WriteString("InputSchema", Schema.Fingerprint);
            writer.WriteString("SettingsSchema", SettingsSchema.Fingerprint);
            writer.WriteString("DisplaySchema", DisplaySchema.Fingerprint);
            writer.WritePropertyName("ReferenceBehaviors"); writer.WriteStartObject();
            foreach (var pair in ReferenceBehaviors.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                writer.WriteString(pair.Key, pair.Value.ToString());
            writer.WriteEndObject();
            writer.WritePropertyName("Fields");
            ParameterSettingCanonical.Write(writer, Configuration.Fields.Capture()
                .Where(pair => !executionOnly || !DisplaySchema.Inputs.Any(port => port.Id == pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
            writer.WritePropertyName("ParameterBindingOrder"); writer.WriteStartArray();
            foreach (var id in Configuration.ParameterBindingOrder) writer.WriteStringValue(id);
            writer.WriteEndArray();
            writer.WritePropertyName("ParameterBindingNames"); writer.WriteStartObject();
            foreach (var pair in BindingNames.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                writer.WriteString(pair.Key, pair.Value);
            writer.WriteEndObject(); writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    internal static Func<ToolConfiguration, AdmittedToolConfiguration> CreateFactory(RuntimeDirectorySnapshot directory, string toolId)
    {
        ArgumentNullException.ThrowIfNull(directory);
        using var creationLease = directory.AcquireInvocation("工具准入创建");
        var definition = directory.CreateDefinition(toolId);
        var input = definition.Describe().Fingerprint;
        var settings = (definition as IToolSettingsFields)?.SettingsSchema.Fingerprint;
        var display = ResultDisplayFields.Schema(definition).Fingerprint;
        var host = ToolHostDeclarationRules.ValidateAndFingerprint(definition);
        var referenceBehaviors = ParameterReferenceRules.Freeze(definition)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var bindingNames = ((definition as IToolParameterBindingNames)?.ParameterBindingNames ?? new Dictionary<string, string>())
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var manualSources = CaptureManualSources(definition);
        var settingsType = definition.SettingsType;
        var outputType = definition.OutputType;
        void EnsureDeclaration()
        {
            if (!directory.IsActive || definition.ToolId != toolId || definition.SettingsType != settingsType ||
                definition.OutputType != outputType || definition.Describe().Fingerprint != input ||
                (definition as IToolSettingsFields)?.SettingsSchema.Fingerprint != settings ||
                ResultDisplayFields.Schema(definition).Fingerprint != display ||
                ToolHostDeclarationRules.ValidateAndFingerprint(definition) != host ||
                !SameDeclaration(referenceBehaviors, ParameterReferenceRules.Freeze(definition)) ||
                !SameDeclaration(bindingNames, (definition as IToolParameterBindingNames)?.ParameterBindingNames ?? new Dictionary<string, string>()) ||
                !SameManualSources(manualSources, CaptureManualSources(definition)))
                throw new InvalidOperationException("缓存的工具声明或目录代次已变化。");
        }
        Func<ToolConfiguration, AdmittedToolConfiguration>? factory = null;
        factory = configuration =>
        {
            using var validationLease = directory.AcquireInvocation("工具准入校验");
            EnsureDeclaration();
            var admitted = new AdmittedToolConfiguration(directory, definition, configuration, factory!);
            EnsureDeclaration();
            return admitted;
        };
        return factory;
    }

    private static bool SameDeclaration<T>(IReadOnlyDictionary<string, T> left, IReadOnlyDictionary<string, T> right) =>
        left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var value) && EqualityComparer<T>.Default.Equals(pair.Value, value));
    private static IReadOnlyList<ManualReferenceSource> CaptureManualSources(IToolDefinition definition)
    {
        if (definition is not IToolManualReferenceSources declared) return Array.Empty<ManualReferenceSource>();
        return Array.AsReadOnly((declared.ManualReferenceSources ?? throw new InvalidOperationException("手动引用来源声明缺失。"))
            .Select(source => source is null ? throw new InvalidOperationException("手动引用来源声明为空。") :
                new ManualReferenceSource(source.PortId, source.MemberPath)).ToArray());
    }
    private static bool SameManualSources(IReadOnlyList<ManualReferenceSource> first, IReadOnlyList<ManualReferenceSource> second) =>
        first.Count == second.Count && first.Zip(second).All(pair => pair.First.PortId == pair.Second.PortId &&
            pair.First.MemberPath.SequenceEqual(pair.Second.MemberPath, StringComparer.Ordinal));
    internal AdmittedToolConfiguration WithConfiguration(ToolConfiguration configuration)
        => _factory(configuration);

    internal AdmittedToolConfiguration WithAcceptedParameterValue(string fieldId, object? value)
    {
        if (!SettingsSchema.Inputs.Any(port => port.Id == fieldId))
            throw new ArgumentException("当前值必须属于已准入的参数字段。", nameof(fieldId));
        return WithConfiguration(Configuration.WithAcceptedParameterValue(fieldId, value));
    }
    internal AdmittedToolConfiguration WithFieldSettings(IReadOnlyDictionary<string, ParameterSetting> changes, IReadOnlyList<string>? acceptedFieldOrder = null)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (_definition is IToolFieldTransitions transitions)
            changes = transitions.ExpandChanges(Configuration.Fields, changes);
        var fields = Configuration.Fields.Apply(changes);
        var parameters = SettingsSchema.Inputs.Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
        bool Referenced(string id) => parameters.Contains(id) && fields.Read(id) is ReferenceSetting or VariableSetting;
        static string Identity(string id, ParameterSetting setting)
        {
            using var stream = new MemoryStream();
            using (var writer = new System.Text.Json.Utf8JsonWriter(stream))
                ParameterSettingCanonical.Write(writer, new Dictionary<string, ParameterSetting> { [id] = setting });
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
        var order = Configuration.ParameterBindingOrder.Where(id => Referenced(id) &&
            Identity(id, Configuration.Fields.Read(id)) == Identity(id, fields.Read(id))).ToList();
        foreach (var id in changes.Keys.Concat(fields.Capture().Keys).Distinct(StringComparer.Ordinal))
            if (Referenced(id) && !order.Contains(id, StringComparer.Ordinal)) order.Add(id);
        if (acceptedFieldOrder is not null)
        {
            var requested = acceptedFieldOrder.ToArray();
            if (requested.Length != order.Count || requested.Distinct(StringComparer.Ordinal).Count() != requested.Length ||
                requested.Any(id => id is null || !order.Contains(id, StringComparer.Ordinal)))
                throw new ArgumentException("引用接受顺序必须完整且唯一地列出已绑定的参数字段。", nameof(acceptedFieldOrder));
            order = requested.ToList();
        }
        var values = new Dictionary<string, ConstantSetting>(StringComparer.Ordinal);
        var revisions = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var port in SettingsSchema.Inputs)
            if (fields.Read(port.Id) is ReferenceSetting or VariableSetting && Configuration.ReadCurrentParameter(port.Id) is { } current)
            {
                values.Add(port.Id, current);
                revisions.Add(port.Id, Configuration.CurrentRevision(port.Id));
            }
        return WithConfiguration(new ToolConfiguration(Configuration.ToolId, fields, values, revisions, order));
    }
    internal AdmittedToolConfiguration PreserveCurrentValuesFrom(AdmittedToolConfiguration live)
    {
        ArgumentNullException.ThrowIfNull(live);
        if (!ReferenceEquals(Directory, live.Directory) || !ReferenceEquals(_definition, live._definition))
            throw new InvalidOperationException("历史值与正式声明或目录代次不一致。");
        var parameters = SettingsSchema.Inputs.Select(port => port.Id).ToFrozenSet(StringComparer.Ordinal);
        return WithConfiguration(Configuration.PreserveCurrentValuesFrom(live.Configuration, parameters));
    }
    internal AdmittedToolConfiguration MergeCurrentValues(AdmittedToolConfiguration live, AdmittedToolConfiguration? opening)
    {
        ArgumentNullException.ThrowIfNull(live);
        if (!ReferenceEquals(Directory, live.Directory) || !ReferenceEquals(_definition, live._definition) ||
            opening is not null && (!ReferenceEquals(Directory, opening.Directory) || !ReferenceEquals(_definition, opening._definition)))
            throw new InvalidOperationException("参数草稿、正式状态或目录代次不一致。");
        var parameters = SettingsSchema.Inputs.Select(port => port.Id).ToFrozenSet(StringComparer.Ordinal);
        return WithConfiguration(Configuration.MergeCurrentValues(live.Configuration, opening?.Configuration, parameters));
    }
    private AdmittedToolConfiguration(RuntimeDirectorySnapshot directory, IToolDefinition definition, ToolConfiguration configuration, Func<ToolConfiguration, AdmittedToolConfiguration> factory)
    {
        ArgumentNullException.ThrowIfNull(directory); ArgumentNullException.ThrowIfNull(definition); ArgumentNullException.ThrowIfNull(configuration);
        if (!directory.IsActive || definition.ToolId != configuration.ToolId || !directory.Contains(configuration.ToolId))
            throw new InvalidOperationException("配置不属于当前有效准入目录。");
        _definition = definition; _factory = factory;
        Directory = directory; Configuration = configuration;
        Schema = definition.Describe();
        var settings = definition as IToolSettingsFields ?? throw new ArgumentException("工具缺少参数声明。");
        SettingsSchema = settings.SettingsSchema; DisplaySchema = ResultDisplayFields.Schema(definition);
        HostDeclarations = ToolHostDeclarationRules.Capture(definition);
        ManualReferenceSources = CaptureManualSources(definition);
        var targets = Schema.Inputs.Concat(SettingsSchema.Inputs).Concat(DisplaySchema.Inputs).ToArray();
        if (targets.Select(port => port.Id).Distinct(StringComparer.Ordinal).Count() != targets.Length)
            throw new ArgumentException("输入、参数及显示字段身份必须互不重复。");
        var declared = targets.ToDictionary(port => port.Id, StringComparer.Ordinal);
        if (!declared.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(configuration.Fields.Declarations.Keys) ||
            configuration.Fields.Declarations.Any(pair => !Equals(pair.Value, declared[pair.Key])))
            throw new ArgumentException("字段声明与准入工具不同。");
        ReferenceBehaviors = ParameterReferenceRules.Freeze(definition);
        foreach (var pair in configuration.Fields.Capture())
        {
            var selection = pair.Value switch { ReferenceSetting r => r.Reference.Selection, VariableSetting v => v.Reference.Selection, _ => null };
            if (selection is not null)
                ParameterReferenceRules.ValidateIndexSelections(selection, ReferenceBehaviors.GetValueOrDefault(pair.Key, ParameterReferenceBehavior.StrictExactlyOne));
        }
        _ = ResultDisplayFields.Capture(definition, configuration.Fields);
        var executionInputs = (definition as IToolExecutionInputs)?.SelectExecutionInputs(configuration.Fields)?.ToArray()
            ?? (definition is IToolExecutionInputs ? throw new ArgumentException("活动输入声明为空。") : Schema.Inputs.Select(p => p.Id).ToArray());
        var inputIds = Schema.Inputs.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var active = executionInputs.ToHashSet(StringComparer.Ordinal);
        if (active.Count != executionInputs.Length || executionInputs.Any(id => id is null || !inputIds.Contains(id)) ||
            Schema.Inputs.Any(port => port.Required && !active.Contains(port.Id)))
            throw new ArgumentException("活动输入必须唯一、已声明并包含必需输入。");
        ActiveInputs = active.ToFrozenSet(StringComparer.Ordinal);
        var dormant = definition is IToolDormantParameterDefaults defaults
            ? defaults.SelectDormantParameterDefaults(configuration.Fields)
                ?? throw new ArgumentException("休眠参数默认值声明为空。")
            : new Dictionary<string, ConstantSetting>();
        var settingsPorts = SettingsSchema.Inputs.ToDictionary(port => port.Id, StringComparer.Ordinal);
        if (dormant.Any(pair => !settingsPorts.ContainsKey(pair.Key) || pair.Value is null
            || configuration.Fields.Read(pair.Key) is not (ReferenceSetting or VariableSetting)))
            throw new ArgumentException("休眠默认值只能替代已声明的参数绑定。");
        DormantParameterDefaults = new FieldSettings(dormant.Keys.Select(id => settingsPorts[id] with
            { EditModes = settingsPorts[id].EditModes | FieldEditModes.Constant }),
            dormant.ToDictionary(pair => pair.Key, pair => (ParameterSetting)pair.Value, StringComparer.Ordinal));
        var parameterIds = SettingsSchema.Inputs.Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
        var names = (definition as IToolParameterBindingNames)?.ParameterBindingNames ?? new Dictionary<string, string>();
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in names)
        {
            if (!parameterIds.Contains(pair.Key) || string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Contains('\0') || !unique.Add(pair.Value))
                throw new ArgumentException("绑定名称必须属于参数声明且互不重复。");
            _ = new System.Text.UTF8Encoding(false, true).GetByteCount(pair.Value);
            copy.Add(pair.Key, pair.Value);
        }
        BindingNames = new ReadOnlyDictionary<string, string>(copy);
        var referenced = configuration.Fields.Capture().Where(pair => parameterIds.Contains(pair.Key) && pair.Value is ReferenceSetting or VariableSetting)
            .Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal);
        if (referenced.Any(id => !copy.ContainsKey(id)) || !configuration.ParameterBindingOrder.ToHashSet(StringComparer.Ordinal).SetEquals(referenced) ||
            configuration.ParameterBindingOrder.Count != referenced.Count)
            throw new ArgumentException("绑定顺序必须恰好包含所有具名引用参数。");
        if (configuration.CaptureCurrentValues().Keys.Any(id => !parameterIds.Contains(id)))
            throw new ArgumentException("当前参数值不属于已准入的参数字段。");
        foreach (var port in SettingsSchema.Inputs.Where(port => port.Required))
            if (configuration.Fields.Read(port.Id) is UnsetSetting) throw new ArgumentException("缺少必需参数：" + port.Id);
        if (!configuration.Fields.Select(SettingsSchema.Inputs).Capture().Values.Any(value => value is ReferenceSetting or VariableSetting))
        {
            var materialized = configuration.Fields.Select(SettingsSchema.Inputs).Materialize(new Dictionary<string, object?>());
            var frozen = definition.FreezeSettings(settings.MaterializeSettings(materialized));
            var diagnostics = definition.Validate(frozen);
            if (diagnostics.Count != 0) throw new ArgumentException(string.Join("; ", diagnostics.Select(item => item.Message)));
        }
    }
}















