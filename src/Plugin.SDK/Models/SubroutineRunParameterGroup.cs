using System;
using System.Collections.ObjectModel;

namespace SunEyeVision.Plugin.SDK.Models;

/// <summary>Ordered Group parameter presentation. Values remain in the related module configuration.</summary>
public sealed class SubroutineRunParameterGroup : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    private string _name = "分组0";
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public ObservableCollection<SubroutineRunParameter> Parameters { get; set; } = new();
}
public sealed class SubroutineRunParameter : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    private string _name = string.Empty, _description = string.Empty;
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }
    public ObservableCollection<SubroutineParameterTarget> Targets { get; set; } = new();
}
/// <summary>Stable typed relation, never a display-name path. Nested Group relations use ExposedParameterId.</summary>
public sealed class SubroutineParameterTarget
{
    public string WorkflowId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
    public Guid? ExposedParameterId { get; set; }
}
