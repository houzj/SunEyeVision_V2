using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

public partial class CommunicationManagerViewModel
{
    public ObservableCollection<CommunicationTriggerRowViewModel> EventTriggers { get; } = new();
    public ObservableCollection<CommunicationTriggerRowViewModel> StringTriggers { get; } = new();
    public ObservableCollection<CommunicationResponseRowViewModel> Responses { get; } = new();
    public ICommand AddEventTriggerCommand { get; private set; } = null!;
    public ICommand AddStringTriggerCommand { get; private set; } = null!;
    private bool _automationInitialized;
    private void InitializeAutomation()
    {
        var solution = _solutionManager.CurrentSolution;
        foreach (var definition in solution?.CommunicationTriggers ?? new())
            (definition.Source == "String" ? StringTriggers : EventTriggers).Add(new(this, definition));
        var configs = solution?.CommunicationResponses ?? new();
        Responses.Add(new(this, configs.SolutionLoaded, "方案加载", "触发字符", "", true));
        Responses.Add(new(this, configs.WorkflowState, "流程控制", "空闲时触发", "忙碌时触发"));
        Responses.Add(new(this, configs.CameraState, "相机响应配置", "连接时触发", "掉线时触发"));
        AddEventTriggerCommand = new RelayCommand(() => AddAutomationTrigger("Event"));
        AddStringTriggerCommand = new RelayCommand(() => AddAutomationTrigger("String"));
        _automationInitialized = true;
    }
    private void AddAutomationTrigger(string source)
    {
        var definition = new CommunicationTriggerDefinition { Source = source };
        (source == "String" ? StringTriggers : EventTriggers).Add(new(this, definition));
        SyncAutomationConfiguration();
    }
    public void RemoveAutomationTrigger(CommunicationTriggerRowViewModel row)
    {
        row.Dispose(); EventTriggers.Remove(row); StringTriggers.Remove(row); SyncAutomationConfiguration();
    }
    public void SyncAutomationConfiguration()
    {
        if (!_automationInitialized || _solutionManager.CurrentSolution is not { } solution) return;
        solution.CommunicationTriggers = EventTriggers.Concat(StringTriggers).Select(r => r.Definition).ToList();
        solution.CommunicationResponses = new()
        {
            WorkflowIdentifiers = solution.CommunicationResponses.WorkflowIdentifiers,
            CameraIdentifiers = solution.CommunicationResponses.CameraIdentifiers,
            SolutionLoaded = Responses[0].Definition, WorkflowState = Responses[1].Definition, CameraState = Responses[2].Definition
        };
        ServiceInitializer.CommunicationService.RefreshAutomationConfiguration(solution);
    }
    public void RefreshAutomationChoices()
    {
        if (!_automationInitialized) return;
        foreach (var row in EventTriggers.Concat(StringTriggers)) row.RefreshChoices();
        foreach (var row in Responses) row.RefreshTargets();
    }
    public IReadOnlyList<AutomationChoice> GetAutomationTargets(string command)
    {
        var solution = _solutionManager.CurrentSolution;
        IEnumerable<AutomationChoice> choices = command switch
        {
            "切换方案" => _solutionManager.GetAllMetadata().Select(m => new AutomationChoice(m.Id, m.Name)),
            "执行流程" => new[] { new AutomationChoice("*", "全流程") }.Concat((solution?.Workflows ?? new())
                .Where(w => !w.IsSubroutine).Select(w => new AutomationChoice(w.Id,
                    w.Name + (solution!.CommunicationResponses.WorkflowIdentifiers.TryGetValue(w.Id, out var number) ? $"（编号 {number}）" : "")))),
            "执行模块" => (solution?.Workflows ?? new()).Where(w => !w.IsSubroutine).SelectMany(w =>
                w.Nodes.Select(n => new AutomationChoice(n.Id, w.Name + "/" + n.Name))),
            _ => Array.Empty<AutomationChoice>()
        };
        return new[] { new AutomationChoice("", "请选择") }.Concat(choices).ToArray();
    }
    private void DisposeAutomationEditor()
    {
        _automationInitialized = false;
        foreach (var row in EventTriggers.Concat(StringTriggers)) row.Dispose();
        foreach (var row in Responses) row.Dispose();
    }
}
