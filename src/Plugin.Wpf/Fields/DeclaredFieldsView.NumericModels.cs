using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Plugin.Wpf.Fields;

public sealed partial class DeclaredFieldsView
{
    internal IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope,string? fieldId,Func<bool> current)
    {
        if(!Enum.IsDefined(scope))throw new ArgumentOutOfRangeException(nameof(scope));
        if(scope==NumericModelReadScope.Field&&string.IsNullOrWhiteSpace(fieldId))throw new ArgumentException("单项回读必须指定字段。",nameof(fieldId));
        var candidates=scope==NumericModelReadScope.Field?_fieldStates.Where(state=>state.Port.Id==fieldId).ToArray():
            _fieldStates.Where(state=>state.NumericModel is not null&&(scope==NumericModelReadScope.All
                ||_tabs is not null&&_tabs.SelectedIndex>=0&&_tabs.SelectedIndex<_numericPageKeys.Length
                &&_numericModelPages[state.Port.Id]==_numericPageKeys[_tabs.SelectedIndex])).ToArray();
        if(candidates.Length==0&&scope==NumericModelReadScope.Field)
            return new[]{new NumericModelReadResult(fieldId!,_disposed||!current()?NumericModelReadStatus.Retired:NumericModelReadStatus.NotApplicable)};
        return Array.AsReadOnly(candidates.Select(state=>state.ReadNumericModel(()=>!_disposed&&current())).ToArray());
    }
}
