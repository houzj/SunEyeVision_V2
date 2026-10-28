using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Input;
using Numbers = SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;

namespace SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Display;

public partial class CrosshairSettingsView : UserControl
{
    private bool _released;
    private bool _preparingNumbers;
    public CrosshairSettingsView() { InitializeComponent(); PreviewKeyDown += CommitNumber; }
    protected override Size MeasureOverride(Size constraint)
    {
        var desired = base.MeasureOverride(constraint);
        if (_released || _preparingNumbers) return desired;
        _preparingNumbers = true;
        try
        {
            // A designer may measure this real view without ever raising Loaded.
            // Materialize the same original numeric templates used by the runtime.
            foreach (var number in NumbersIn(this).ToArray()) number.EnsureTemplateInputs();
            return base.MeasureOverride(constraint);
        }
        finally { _preparingNumbers = false; }
    }
    private static IEnumerable<Numbers.SliderArrange> NumbersIn(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); ++i)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Numbers.SliderArrange number) yield return number;
            foreach (var descendant in NumbersIn(child)) yield return descendant;
        }
    }
    private void CommitNumber(object sender, KeyEventArgs args)
    {
        if (_released || args.Key != Key.Enter || args.OriginalSource is not TextBox text) return;
        DependencyObject? node = text;
        while (node is not null && node is not Numbers.SliderArrange) node = VisualTreeHelper.GetParent(node);
        if (node is not Numbers.SliderArrange arrange) return;
        args.Handled = true;
        var leaf = arrange.EnsureTemplateInputs().Left;
        var combo = (ComboBox)arrange.FindName("CBox");
        var arrangeTemplate = arrange.Template; var comboTemplate = combo.Template; var leafTemplate = leaf.Template;
        var context = DataContext; var model = arrange.DataContext; var raw = text.Text;
        var inner = text.GetBindingExpression(TextBox.TextProperty);
        var middle = leaf.GetBindingExpression(Numbers.NumericControl.TextProperty);
        var outer = arrange.GetBindingExpression(Numbers.SliderArrangeBase.StartValueProperty);
        bool Current() => !_released && ReferenceEquals(context, DataContext) && ReferenceEquals(model, arrange.DataContext)
            && ReferenceEquals(arrangeTemplate, arrange.Template) && ReferenceEquals(combo, arrange.FindName("CBox"))
            && ReferenceEquals(comboTemplate, combo.Template) && ReferenceEquals(leaf, combo.Template.FindName("T1", combo))
            && ReferenceEquals(leafTemplate, leaf.Template) && ReferenceEquals(text, leaf.FindName("ValueText"))
            && ReferenceEquals(inner, text.GetBindingExpression(TextBox.TextProperty))
            && ReferenceEquals(middle, leaf.GetBindingExpression(Numbers.NumericControl.TextProperty))
            && ReferenceEquals(outer, arrange.GetBindingExpression(Numbers.SliderArrangeBase.StartValueProperty));
        try
        {
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                || !double.IsFinite(value) || value < arrange.Minimum || value > arrange.Maximum) throw new ArgumentException("请输入原范围内的有限数值。");
            if (inner?.Status != BindingStatus.Active || middle?.Status != BindingStatus.Active || outer?.Status != BindingStatus.Active)
                throw new ArgumentException("原十字数值绑定未就绪。");
            Validation.ClearInvalid(inner); if (!Current()) return;
            inner.UpdateSource(); if (!Current()) return;
            middle.UpdateSource(); if (!Current()) return;
            arrange.SetCurrentValue(Numbers.SliderArrangeBase.StartValueProperty, (double)value); if (!Current()) return;
            outer.UpdateSource(); if (!Current()) return;
            if (inner.HasValidationError || middle.HasValidationError || outer.HasValidationError) throw new ArgumentException("原十字数值绑定拒绝输入。");
            combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
            if (!Current()) return;
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(text), null); Keyboard.ClearFocus();
        }
        catch (Exception error) when (error is ArgumentException or FormatException or OverflowException)
        {
            if (!Current()) return;
            text.SetCurrentValue(TextBox.TextProperty, raw);
            if (inner is not null) Validation.MarkInvalid(inner, new ValidationError(new ExceptionValidationRule(), inner, error.Message, error));
            text.Focus();
        }
    }
    internal void ReleaseBindings()
    {
        if (_released) return; _released = true; PreviewKeyDown -= CommitNumber;
        var nodes = new List<DependencyObject>();
        void Read(DependencyObject value)
        {
            nodes.Add(value);
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(value); ++i) Read(VisualTreeHelper.GetChild(value, i));
        }
        Read(this);
        ParameterCleanup.Run(nodes.Select<DependencyObject, Action>(node => () =>
        {
            BindingOperations.ClearAllBindings(node);
            node.ClearValue(DataContextProperty);
        }));
    }
}
