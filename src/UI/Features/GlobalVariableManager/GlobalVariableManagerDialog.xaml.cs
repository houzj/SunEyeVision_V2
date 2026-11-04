using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Controls.Foundation;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.Domain.Solutions;
namespace SunEyeVision.UI.Features.GlobalVariableManager
{
    public partial class GlobalVariableManagerDialog : BaseWindow
    {
        private readonly System.Collections.Generic.HashSet<string> _valueEdits = new(StringComparer.Ordinal);
        private readonly System.Collections.Generic.Dictionary<string, string> _pendingNames = new(StringComparer.Ordinal);
        private readonly System.Collections.Generic.Dictionary<string, string> _pendingValues = new(StringComparer.Ordinal);
        private readonly System.Collections.Generic.Dictionary<string, string> _pendingGroups = new(StringComparer.Ordinal);
        private readonly System.Collections.Generic.List<Window> _children = new();
        private bool _refreshing;
        public GlobalVariableManagerDialog(GlobalVariableManagerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            SetResourceReference(StyleProperty, typeof(BaseWindow));
            viewModel.PrepareView = () => TryCommitPendingEdits(out var error) ? null : error;
            viewModel.HasPendingViewEdits = () => HasRetainedPendingEdits;
            viewModel.AcceptView = AcceptPublication;
            viewModel.PendingViewStamp = () => PendingEditStamp;
            viewModel.AcknowledgeViewEdits = AcknowledgeRetainedEdits;
            ManagerView.EditorAction += OnEditorAction;
            Closed += (_, _) =>
            {
                ManagerView.EditorAction -= OnEditorAction;
                foreach (var child in _children.ToArray()) child.Close();
                _children.Clear();
                viewModel.PrepareView = null; viewModel.HasPendingViewEdits = null;
                viewModel.AcceptView = null; viewModel.PendingViewStamp = null; viewModel.AcknowledgeViewEdits = null;
                viewModel.Discard();
            };
        }
        private void OnEditorAction(string action, object sender, RoutedEventArgs args)
        {
            if (DataContext is not GlobalVariableManagerViewModel vm || !vm.IsValid) return;
            switch (action)
            {
                case "Loaded": VariableEditor_Loaded(sender, args); break;
                case "NameChanged": VariableNameTextBox_TextChanged(sender, (TextChangedEventArgs)args); break;
                case "ValueChanged": VariableValueTextBox_TextChanged(sender, (TextChangedEventArgs)args); break;
                case "LostFocus":
                    if (sender is TextBox {Name:"VariableNameEditor"}) VariableNameTextBox_LostFocus(sender,args);
                    else if (sender is TextBox {Name:"VariableValueEditor"}) VariableValueTextBox_LostFocus(sender,args);
                    break;
                case "KeyDown":
                    if (args is KeyEventArgs key && key.Key == Key.Enter) { if (TryCommitPendingEdits(out _)) Keyboard.ClearFocus(); key.Handled=true; }
                    break;
                case "RenameGroup": if (vm.CanEdit && sender is FrameworkElement { DataContext: VariableGroupViewModel group }) group.BeginEdit(); break;
                case "GroupLostFocus":
                    if (vm.CanEdit && sender is FrameworkElement { DataContext: VariableGroupViewModel renamed }) ConfirmGroup(vm,renamed);
                    break;
                case "GroupChanged":
                    if(!_refreshing && sender is GroupRenameTextBox {DataContext:VariableGroupViewModel changed} groupBox && changed.IsEditing)
                        _pendingGroups[changed.Name]=groupBox.Text;
                    break;
                case "GroupKeyDown":
                    if (sender is FrameworkElement {DataContext:VariableGroupViewModel editing} && args is KeyEventArgs groupKey)
                    {
                        if(groupKey.Key==Key.Escape){_pendingGroups.Remove(editing.Name);editing.CancelEdit();groupKey.Handled=true;}
                        else if(groupKey.Key==Key.Enter){ConfirmGroup(vm,editing);groupKey.Handled=true;}
                    }
                    break;
                case "DeleteGroup":
                    if (vm.CanEdit && sender is FrameworkElement {DataContext:VariableGroupViewModel deleted})
                    {
                        if (deleted.Variables.Count!=0 && ThemeMessageDialog.Show($"删除分组“{deleted.Name}”及其中{deleted.Variables.Count}个变量？","删除分组",MessageBoxButton.YesNo,ThemeMessageDialog.MessageIcon.Warning)!=MessageBoxResult.Yes) return;
                        vm.SelectedGroup=deleted;vm.DeleteGroupWithVariables();
                    }
                    break;
                case "TypeChanged":
                    if(vm.CanEdit&&sender is ComboBox {DataContext:GlobalVariable typed,SelectedValue:string type}&&vm.DraftVariables.Any(item=>ReferenceEquals(item,typed)))
                    {
                        object? initial=type switch {"Integer"=>new long[]{0},"Double"=>new double[]{0},"String"=>"","Boolean"=>new bool[]{false},"Integer[]"=>Array.Empty<long>(),"Double[]"=>Array.Empty<double>(),"String[]"=>Array.Empty<string>(),"Boolean[]"=>Array.Empty<bool>(),_=>null};
                        if(initial is not null){typed.Type=type;_pendingValues.Remove(typed.Id);_valueEdits.Remove(typed.Id);vm.SetDraftValue(typed,initial);}
                    }
                    break;
                case "Delete": if (sender is FrameworkElement {DataContext:GlobalVariable variable} && vm.DeleteVariableCommand.CanExecute(variable)) vm.DeleteVariableCommand.Execute(variable);break;
                case "Array": if (sender is FrameworkElement {DataContext:GlobalVariable array}) ShowArray(array);break;
                case "Search": Keyboard.ClearFocus();break;
            }
        }
        private void ShowArray(GlobalVariable variable)
        {
            if (DataContext is not GlobalVariableManagerViewModel vm || !vm.IsValid || !vm.DraftVariables.Any(item=>ReferenceEquals(item,variable))) return;
            if (variable.CaptureValue().Value is not Array values) return;
            var dialog=new VariableArrayDialog(variable.Name,values.Cast<object?>().Select(VariableManagerValueTextProxy.Format)){Owner=this,ShowInTaskbar=false};
            _children.Add(dialog);dialog.Closed+=(_,_)=>_children.Remove(dialog);dialog.ShowDialog();
        }
        private void ConfirmGroup(GlobalVariableManagerViewModel vm,VariableGroupViewModel group)
        {
            var openingName=group.Name;vm.ConfirmGroupRename(group);
            if(!group.IsEditing)_pendingGroups.Remove(openingName);
        }
        private void RestorePendingGroups()
        {
            if(DataContext is not GlobalVariableManagerViewModel vm)return;
            _refreshing=true;
            try{foreach(var group in vm.Groups)if(_pendingGroups.TryGetValue(group.Name,out var raw)){group.EditingText=raw;group.IsEditing=true;}}
            finally{_refreshing=false;}
        }
        private bool TryValidateNameEditor(TextBox box, out string error)
        {
            error = "";
            if (box.DataContext is not GlobalVariable variable || DataContext is not GlobalVariableManagerViewModel vm) return true;
            var name = box.Text.Trim();
            var variables = vm.DraftVariables;
            if (name.Length == 0) error = "变量名称不能为空，请修改当前输入后再保存。";
            else if (variables != null && GlobalVariable.IsNameDuplicate(variables, name, variable.Id)) error = $"变量名称“{name}”已存在。";
            var binding = box.GetBindingExpression(TextBox.TextProperty);
            if (binding != null)
            {
                if (error.Length > 0) System.Windows.Controls.Validation.MarkInvalid(binding, new ValidationError(new ExceptionValidationRule(), binding, error, null));
                else System.Windows.Controls.Validation.ClearInvalid(binding);
            }
            return error.Length == 0;
        }
        private void VariableNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_refreshing) return;
            if (sender is TextBox box && box.DataContext is GlobalVariable variable &&
                DataContext is GlobalVariableManagerViewModel vm && TryValidateNameEditor(box, out _) && box.Text.Trim() != variable.Name)
                vm.ConfirmVariableRename(variable, box.Text.Trim(), variable.Name);
        }

        /// <summary>
        /// 当前值 TextBox 失焦时按变量类型解析文本并写回
        /// 仅文本类（整型/双精度/字符串/布尔）可编辑，图像/图形/矩阵等只读占位不解析。
        /// </summary>
        private void VariableValueTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_refreshing) return;
            if (sender is not TextBox textBox || textBox.DataContext is not GlobalVariable variable)
                return;
            if (textBox.IsReadOnly)
                return;

            if (!TryParseValueEditor(textBox, out var value, out _, _valueEdits.Contains(variable.Id))) return;
            if (value != null && DataContext is GlobalVariableManagerViewModel vm) vm.SetDraftValue(variable, value);

        }
        private void VariableValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_refreshing || sender is not TextBox { IsLoaded: true, IsReadOnly: false } box ||
                box.DataContext is not GlobalVariable variable) return;
            var current = new GlobalVariableValueTextConverter().Convert(variable.Value!, typeof(string), null!, CultureInfo.InvariantCulture)?.ToString() ?? "";
            if (box.Text != current || _valueEdits.Contains(variable.Id))
            {
                _valueEdits.Add(variable.Id);
                _pendingValues[variable.Id] = box.Text;
            }
        }
        private void VariableNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_refreshing && sender is TextBox { IsLoaded: true } box && box.DataContext is GlobalVariable variable && box.Text != variable.Name)
                _pendingNames[variable.Id] = box.Text;
        }
        private void VariableEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox box || box.DataContext is not GlobalVariable variable) return;
            var pending = box.Name == "VariableNameEditor" ? _pendingNames : _pendingValues;
            if (!pending.TryGetValue(variable.Id, out var raw)) return;
            _refreshing = true;
            try { box.SetCurrentValue(TextBox.TextProperty, raw); }
            finally { _refreshing = false; }
        }
        internal bool HasRetainedPendingEdits => _pendingNames.Count != 0 || _pendingValues.Count != 0 || _pendingGroups.Count!=0 ||
            SunEyeVision.Plugin.Editing.Wpf.ToolEditorCommit.HasPendingEdits(this);
        internal string PendingEditStamp => System.Text.Json.JsonSerializer.Serialize(new { Names = _pendingNames, Values = _pendingValues, Groups=_pendingGroups });
        internal void AcknowledgeRetainedEdits(string stamp)
        {
            using var saved = System.Text.Json.JsonDocument.Parse(stamp);
            foreach (var item in saved.RootElement.GetProperty("Names").EnumerateObject())
                if (_pendingNames.TryGetValue(item.Name, out var current) && current == item.Value.GetString()) _pendingNames.Remove(item.Name);
            foreach (var item in saved.RootElement.GetProperty("Values").EnumerateObject())
                if (_pendingValues.TryGetValue(item.Name, out var current) && current == item.Value.GetString())
                { _pendingValues.Remove(item.Name); _valueEdits.Remove(item.Name); }
            foreach(var item in saved.RootElement.GetProperty("Groups").EnumerateObject())
                if(_pendingGroups.TryGetValue(item.Name,out var current)&&current==item.Value.GetString())_pendingGroups.Remove(item.Name);
            RestorePendingGroups();
        }
        internal void AcceptPublication()
        {
            _refreshing = true;
            try
            {
                _valueEdits.Clear();
                _pendingNames.Clear();
                _pendingValues.Clear();
                _pendingGroups.Clear();

            }
            finally { _refreshing = false; }
        }
        private static System.Collections.Generic.IEnumerable<DependencyObject> Visuals(DependencyObject root)
        {
            yield return root;
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in Visuals(System.Windows.Media.VisualTreeHelper.GetChild(root, i))) yield return child;
        }
        private static bool TryParseValueEditor(TextBox box, out object? parsed, out string error, bool explicitEdit = false)
        {
            parsed = null; error = "";
            if (box.IsReadOnly || box.DataContext is not GlobalVariable variable) return true;
            var binding = box.GetBindingExpression(TextBox.TextProperty);
            var currentText = new GlobalVariableValueTextConverter().Convert(variable.Value!, typeof(string), null!, CultureInfo.InvariantCulture)?.ToString() ?? "";
            if (!explicitEdit && box.Text == currentText)
            { if (binding != null) System.Windows.Controls.Validation.ClearInvalid(binding); return true; }
            parsed = GlobalVariableValueTextConverter.ParseText(box.Text, variable.Type);
            if (parsed == null)
            {
                error = $"变量“{variable.Name}”的{variable.Type}值无效，请修改当前输入后再保存。";
                if (binding != null) System.Windows.Controls.Validation.MarkInvalid(binding,
                    new ValidationError(new ExceptionValidationRule(), binding, error, null));
                return false; // Keep the typed text and existing model value; never UpdateTarget here.
            }
            if (binding != null) System.Windows.Controls.Validation.ClearInvalid(binding);
            return true;
        }
        public bool TryValidatePendingEdits(out string error)
        {
            if (DataContext is GlobalVariableManagerViewModel vm && !vm.TryPrepare(out error)) return false;
            if (!TryValidateRetainedEdits(out error)) return false;
            foreach (var box in Visuals(this).OfType<TextBox>().Where(b => b.Name == "VariableNameEditor"))
                if (!TryValidateNameEditor(box, out error)) return false;
            foreach (var box in Visuals(this).OfType<TextBox>().Where(b => b.Name == "VariableValueEditor"))
                if (!TryParseValueEditor(box, out _, out error,
                    box.DataContext is GlobalVariable v && _valueEdits.Contains(v.Id))) return false;
            return SunEyeVision.Plugin.Editing.Wpf.ToolEditorCommit.TryValidate(this, out error);
        }
        private bool TryValidateRetainedEdits(out string error)
        {
            error = "";
            if (DataContext is not GlobalVariableManagerViewModel vm) return true;
            var variables = vm.DraftVariables;
            foreach(var group in vm.Groups)
            {
                var name=_pendingGroups.TryGetValue(group.Name,out var rawGroup)?rawGroup.Trim():group.Name;
                if(string.IsNullOrWhiteSpace(name)){error="分组名称不能为空，当前输入已保留。";return false;}
                if(vm.Groups.Any(other=>!ReferenceEquals(other,group)&&string.Equals(_pendingGroups.TryGetValue(other.Name,out var rawOther)?rawOther.Trim():other.Name,name,StringComparison.Ordinal)))
                {error=$"分组名称“{name}”已存在，当前输入已保留。";return false;}
            }
            foreach (var variable in variables)
            {
                var name = _pendingNames.TryGetValue(variable.Id, out var rawName) ? rawName.Trim() : variable.Name;
                if (string.IsNullOrWhiteSpace(name)) { error = "变量名称不能为空，当前输入已保留。"; return false; }
                if (variables.Any(other => other.Id != variable.Id && string.Equals(
                    _pendingNames.TryGetValue(other.Id, out var otherName) ? otherName.Trim() : other.Name,
                    name, StringComparison.OrdinalIgnoreCase)))
                { error = $"变量名称“{name}”已存在，当前输入已保留。"; return false; }
                if (_pendingValues.TryGetValue(variable.Id, out var rawValue) &&
                    GlobalVariableValueTextConverter.ParseText(rawValue, variable.Type) is null)
                { error = $"变量“{name}”的{variable.Type}值无效，当前输入已保留。"; return false; }
            }
            return true;
        }
        public bool TryCommitPendingEdits(out string error)
        {
            if (!TryValidatePendingEdits(out error)) return false;
            if(DataContext is GlobalVariableManagerViewModel groupsModel)
                foreach(var group in groupsModel.Groups.Where(group=>group.IsEditing).ToArray())ConfirmGroup(groupsModel,group);
            var nameEditors = Visuals(this).OfType<TextBox>().Where(b => b.Name == "VariableNameEditor").Select(b => (Box: b, Text: b.Text)).ToArray();
            var valueEditors = Visuals(this).OfType<TextBox>()
                .Where(b => b.Name == "VariableValueEditor" && !b.IsReadOnly)
                .Select(b => (Box: b, Text: b.Text)).ToArray();
            // Commit normal bindings and DataGrid rows first; invalid edits are not cancelled.
            if (!SunEyeVision.Plugin.Editing.Wpf.ToolEditorCommit.TryPrepare(this, out error)) return false;
            foreach (var entry in nameEditors)
            {
                entry.Box.SetCurrentValue(TextBox.TextProperty, entry.Text);
                if (!TryValidateNameEditor(entry.Box, out error)) return false;
                if (entry.Box.DataContext is GlobalVariable variable && DataContext is GlobalVariableManagerViewModel vm &&
                    entry.Text.Trim() != variable.Name && !vm.ConfirmVariableRename(variable, entry.Text.Trim(), variable.Name))
                { error = "变量重命名未完成，当前输入保留。"; return false; }
            }
            var parsed = new System.Collections.Generic.List<(GlobalVariable Variable, object Value)>();
            foreach (var entry in valueEditors)
            {
                // A DataGrid commit can refresh OneWay displays. Restore the pending text before parsing.
                entry.Box.SetCurrentValue(TextBox.TextProperty, entry.Text);
                if (!TryParseValueEditor(entry.Box, out var value, out error,
                    entry.Box.DataContext is GlobalVariable v && _valueEdits.Contains(v.Id))) return false;
                if (value != null && entry.Box.DataContext is GlobalVariable variable) parsed.Add((variable, value));
            }
            if (DataContext is GlobalVariableManagerViewModel model)
            {
                foreach (var item in parsed) model.SetDraftValue(item.Variable, item.Value);
                foreach (var variable in model.DraftVariables)
                {
                    if (_pendingNames.TryGetValue(variable.Id, out var rawName)) variable.Name = rawName.Trim();
                    if (_pendingValues.TryGetValue(variable.Id, out var rawValue))
                        model.SetDraftValue(variable, GlobalVariableValueTextConverter.ParseText(rawValue, variable.Type));
                }
            }
            error = ""; return true;
        }
    }

    /// <summary>
    /// 全局变量当前值显示转换器：把 object 值（基础类型数组/图像/图形/矩阵）格式化为可读文本。
    /// 只负责显示（Convert），回写由 VariableValueTextBox_LostFocus 按 Type 解析。
    /// </summary>
    public class GlobalVariableValueTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            return value switch
            {
                long[] arr   => string.Join(", ", arr),
                double[] arr => string.Join(", ", arr.Select(v => v.ToString(CultureInfo.InvariantCulture))),
                string[] arr => string.Join(", ", arr),
                bool[] arr   => string.Join(", ", arr),
                long l       => l.ToString(CultureInfo.InvariantCulture),
                double d     => d.ToString(CultureInfo.InvariantCulture),
                string s     => s,
                bool b       => b.ToString(),
                _ => FormatComplex(value)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;

        private static string FormatComplex(object value) => VariableManagerValueTextProxy.Format(value);

        /// <summary>
        /// 按变量类型把文本解析回值（数组形态，对齐反序列化契约）。解析失败返回 null。
        /// </summary>
        public static object? ParseText(string? text, string typeName)
        {
            switch (typeName)
            {
                case "Integer":
                    if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                        return new[] { l };
                    break;
                case "Double":
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d))
                        return new[] { d };
                    break;
                case "Boolean":
                    if (bool.TryParse(text, out var b))
                        return new[] { b };
                    break;
                case "String":
                    return new[] { text ?? string.Empty };
            }
            return null;
        }
    }

    /// <summary>全局变量类型词汇本地化转换器。</summary>
    public sealed class GlobalVariableTypeTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is string type && SunEyeVision.Plugin.Contracts.VariableValueDeclarations.DisplayNames.TryGetValue(type, out var name)
                ? name
                : value?.ToString() ?? string.Empty;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}


