using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Plugin.Wpf.Fields;

public sealed partial class DeclaredFieldsView
{
    private string? NumericSelectedPage=>_tabs?.SelectedItem is System.Windows.Controls.TabItem tab&&_numericTabPages.TryGetValue(tab,out var key)?key:null;
    private string? NumericInputPageKey(string? anchor,string? section)
        =>anchor is not null?_numericModelPages.GetValueOrDefault(anchor):section is null?null:
            _numericPageKeys.Contains(section,StringComparer.Ordinal)?section:
            _groupPresentations.Values.FirstOrDefault(p=>p.Group==section) is {} presentation?presentation.Layout?.Page??presentation.Group:null;
    internal bool NumericInputPage(string? anchor,string? section)=>NumericInputPageKey(anchor,section) is {} key&&key==NumericSelectedPage;
    internal bool HasNumericInputPage(string? anchor,string? section)=>NumericInputPageKey(anchor,section) is not null;
    internal IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope,string? fieldId,Func<bool> current)
    {
        if(!Enum.IsDefined(scope))throw new ArgumentOutOfRangeException(nameof(scope));
        if(scope==NumericModelReadScope.Field&&string.IsNullOrWhiteSpace(fieldId))throw new ArgumentException("单项回读必须指定字段。",nameof(fieldId));
        var candidates=scope==NumericModelReadScope.Field?_fieldStates.Where(state=>state.Port.Id==fieldId).ToArray():
            _fieldStates.Where(state=>state.NumericModel is not null&&(scope==NumericModelReadScope.All
                ||NumericSelectedPage is {} page&&_numericModelPages[state.Port.Id]==page)).ToArray();
        if(candidates.Length==0&&scope==NumericModelReadScope.Field)
            return new[]{new NumericModelReadResult(fieldId!,_disposed||!current()?NumericModelReadStatus.Retired:NumericModelReadStatus.NotApplicable)};
        var ordered=new List<SunEyeVision.Plugin.Wpf.Adapters.Fields.DeclaredFieldState>();
        foreach(var state in candidates)
        {
            if(state.NumericEndpoint==1&&state.NumericPeer is {} left&&candidates.Contains(left)&&!ordered.Contains(left))ordered.Add(left);
            if(!ordered.Contains(state))ordered.Add(state);
        }
        return Array.AsReadOnly(ordered.Select(state=>state.ReadNumericModel(()=>!_disposed&&current())).ToArray());
    }
}
