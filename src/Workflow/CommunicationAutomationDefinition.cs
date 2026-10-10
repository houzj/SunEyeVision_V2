using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Workflow;

public sealed class CommunicationTriggerDefinition : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    private string _Source = "Event";
    public string Source { get => _Source; set => SetProperty(ref _Source, value); }
    private Guid? _EventId = null;
    public Guid? EventId { get => _EventId; set => SetProperty(ref _EventId, value); }
    private string _Text = "";
    public string Text { get => _Text; set => SetProperty(ref _Text, value); }
private string _MatchMode = "等于";
    public string MatchMode { get => _MatchMode; set => SetProperty(ref _MatchMode, value); }
    private string _Command = "执行流程";
    public string Command { get => _Command; set => SetProperty(ref _Command, value); }
    private string _TargetId = "";
    public string TargetId { get => _TargetId; set => SetProperty(ref _TargetId, value); }
    private Guid? _FieldId = null;
    public Guid? FieldId { get => _FieldId; set => SetProperty(ref _FieldId, value); }
    private string? _triggerParameterSource;
    /// <summary>Current event output path (eventId.fieldId), or a literal trigger value. Null preserves legacy FieldId.</summary>
    public string? TriggerParameterSource { get => _triggerParameterSource; set => SetProperty(ref _triggerParameterSource, value); }
    private bool _parameterComparisonEnabled;
    /// <summary>Legacy persisted flag; equality now depends only on a non-empty trigger parameter source.</summary>
    public bool ParameterComparisonEnabled { get => _parameterComparisonEnabled; set => SetProperty(ref _parameterComparisonEnabled, value); }
    private Guid? _comparisonOutputId;
    public Guid? ComparisonOutputId { get => _comparisonOutputId; set => SetProperty(ref _comparisonOutputId, value); }
    // New global rules use Source=GlobalData; old String rules are not converted or executed.
    private string _Action = "";
    public string Action { get => _Action; set => SetProperty(ref _Action, value); }
    private Guid? _conditionFieldId;
    public Guid? ConditionFieldId { get => _conditionFieldId; set => SetProperty(ref _conditionFieldId, value); }
    private string _conditionRule = "无附加条件";
    public string ConditionRule { get => _conditionRule; set => SetProperty(ref _conditionRule, value); }
    private string _conditionValue = "";
    public string ConditionValue { get => _conditionValue; set => SetProperty(ref _conditionValue, value); }
    public List<CommunicationParameterMapping> ParameterMappings { get; set; } = new();
}

/// <summary>Stable source/target identities; only applied to a per-execution parameter copy.</summary>
public sealed class CommunicationParameterMapping
{
    public Guid SourceFieldId { get; set; }
    public string TargetNodeId { get; set; } = "";
    public string TargetProperty { get; set; } = "";
    public string TargetKind { get; set; } = "运行参数";
}

public sealed class CommunicationResponseDefinition : ObservableObject
{
    private bool _Enabled = false;
    public bool Enabled { get => _Enabled; set => SetProperty(ref _Enabled, value); }
    private string _DeviceId = "";
    public string DeviceId { get => _DeviceId; set => SetProperty(ref _DeviceId, value); }
    private string _TargetId = "";
    public string TargetId { get => _TargetId; set => SetProperty(ref _TargetId, value); }
    private string _DataType = "string";
    public string DataType { get => _DataType; set => SetProperty(ref _DataType, value); }
    private string _Separator = ",";
    public string Separator { get => _Separator; set => SetProperty(ref _Separator, value); }
    private string _Terminator = "";
    public string Terminator { get => _Terminator; set => SetProperty(ref _Terminator, value); }
    private string _Content = "";
    public string Content { get => _Content; set => SetProperty(ref _Content, value); }
    private string _AlternateContent = "";
    public string AlternateContent { get => _AlternateContent; set => SetProperty(ref _AlternateContent, value); }
    private double _DelaySeconds = 0;
    public double DelaySeconds { get => _DelaySeconds; set => SetProperty(ref _DelaySeconds, value); }
}

public sealed class CommunicationResponseConfiguration
{
    public Dictionary<string, int> WorkflowIdentifiers { get; set; } = new();
    public Dictionary<string, int> CameraIdentifiers { get; set; } = new();
    public CommunicationResponseDefinition SolutionLoaded { get; set; } = new();
    public CommunicationResponseDefinition WorkflowState { get; set; } = new();
    public CommunicationResponseDefinition CameraState { get; set; } = new();
}
