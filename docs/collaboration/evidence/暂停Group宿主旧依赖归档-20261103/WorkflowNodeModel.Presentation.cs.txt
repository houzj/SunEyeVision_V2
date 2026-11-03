using System.Text.Json.Serialization;
namespace SunEyeVision.UI.Shared;
public partial class WorkflowNode
{
    // 展示专用临时模型，不进入方案、执行或撤销历史。
    [JsonIgnore] public bool IsToolboxPreview { get; init; }
#if SUNEYE_GROUP_LEGACY
    private SubroutineParameters? _vmGroupParameters;
    private SubroutineLoopConfig? _vmGroupLoop;
    [JsonIgnore] public bool IsGroupLoopEnabled => IsSubroutineNode && Parameters is SubroutineParameters p && p.LoopConfig.EnableLoop;
    private void BindVmGroupParameters()
    {
        if (_vmGroupParameters != null) _vmGroupParameters.PropertyChanged -= VmGroupParameterChanged;
        _vmGroupParameters = Parameters as SubroutineParameters;
        if (_vmGroupParameters != null) _vmGroupParameters.PropertyChanged += VmGroupParameterChanged;
        BindVmGroupLoop();
    }
    private void VmGroupParameterChanged(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(SubroutineParameters.LoopConfig)) BindVmGroupLoop(); }
    private void BindVmGroupLoop()
    {
        if (_vmGroupLoop != null) _vmGroupLoop.PropertyChanged -= VmGroupLoopChanged;
        _vmGroupLoop = _vmGroupParameters?.LoopConfig;
        if (_vmGroupLoop != null) _vmGroupLoop.PropertyChanged += VmGroupLoopChanged;
        OnPropertyChanged(nameof(IsGroupLoopEnabled));
    }
    private void VmGroupLoopChanged(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(SubroutineLoopConfig.EnableLoop)) OnPropertyChanged(nameof(IsGroupLoopEnabled)); }
#else
    // Group 和循环入口当前不可用，未迁移状态不能表现为已启用。
    [JsonIgnore] public bool IsGroupLoopEnabled => false;
#endif
    private void InitializeVmPresentation()
    {
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Position) or nameof(ToolType) or nameof(SubroutineId) or nameof(StyleConfig))
            {
                foreach (var property in new[] { nameof(IsSubroutineNode), nameof(VisualWidth), nameof(VisualHeight), nameof(HitAreaWidth), nameof(HitAreaHeight),
                    nameof(HitAreaLeft), nameof(HitAreaTop), nameof(NodeRect), nameof(TopPortPosition), nameof(BottomPortPosition), nameof(LeftPortPosition), nameof(RightPortPosition), nameof(IsGroupLoopEnabled) })
                    OnPropertyChanged(property);
            }
        };
    }
}
