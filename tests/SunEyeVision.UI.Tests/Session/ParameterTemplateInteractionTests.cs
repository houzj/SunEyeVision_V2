using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("Editor commit UI")]
public sealed class ParameterTemplateInteractionTests
{
    [Fact]
    public void Enumeration_popup_uses_native_labels_under_host_implicit_item_style()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            var writes = new List<ParameterSetting>();
            using var row = new DeclaredFieldRow(new Port("polarity", DataType.Text,
                EditModes:FieldEditModes.Constant, Choices:new TextChoiceConstraint(new[] { "Binary", "BinaryInv" })),
                "极性", new ConstantSetting("Binary"), writes.Add,
                choiceLabels:new Dictionary<string,string> { ["Binary"]="亮于背景", ["BinaryInv"]="暗于背景" });
            var window = new Window { Content=row, Width=420, Height=240, ShowInTaskbar=false };
            window.Resources.MergedDictionaries.Add(EditorResources.Shared);
            // Model the actual host override: its presenter prints raw Content,
            // whereas the original item template lets WPF select the display path.
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding());
            var hostStyle = new Style(typeof(ComboBoxItem));
            hostStyle.Setters.Add(new Setter(Control.TemplateProperty,
                new ControlTemplate(typeof(ComboBoxItem)) { VisualTree=presenter }));
            window.Resources[typeof(ComboBoxItem)] = hostStyle;
            try
            {
                window.Show(); window.UpdateLayout(); UiRuntime.Pump();
                var combo = Assert.Single(UiRuntime.Visuals<ComboBox>(row));
                combo.IsDropDownOpen=true; window.UpdateLayout(); UiRuntime.Pump();
                var expected = new[] { "亮于背景", "暗于背景" };
                for (var index=0; index<expected.Length; index++)
                {
                    var item = Assert.IsType<ComboBoxItem>(combo.ItemContainerGenerator.ContainerFromIndex(index));
                    var labels = UiRuntime.Visuals<TextBlock>(item).Select(text=>text.Text).ToArray();
                    Assert.Contains(expected[index], labels);
                    Assert.DoesNotContain(labels, label=>label.StartsWith("Entry {", StringComparison.Ordinal));
                    Assert.NotSame(hostStyle, item.Style.BasedOn);
                }
                Assert.Empty(writes);
                combo.SelectedValue="BinaryInv"; UiRuntime.Pump();
                Assert.Equal("BinaryInv", Assert.IsType<ConstantSetting>(Assert.Single(writes)).Value);
                combo.IsDropDownOpen=false;
            }
            finally { window.Close(); UiRuntime.Pump(); }
        });
    }

    [Fact]
    public void Editor_footer_reset_template_does_not_reparent_its_ancestor_frame()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            using var frame = new SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.NativeToolEditorFrame
                { ResetVisibility=Visibility.Visible, EditorContent=new TextBlock { Text="正式编辑器" } };
            var model = new FieldRowModelTestCommand();
            frame.ResetCommand = model;
            var window = new Window { Content=frame, Width=420, Height=500, ShowInTaskbar=false };
            window.Resources.MergedDictionaries.Add(EditorResources.Shared);
            try
            {
                window.Show(); window.UpdateLayout(); UiRuntime.Pump();
                Assert.Same(window, frame.Parent);
                var reset = Assert.Single(UiRuntime.Visuals<Button>(frame), button=>ReferenceEquals(button.Command,model));
                Assert.True(reset.IsEnabled);
                frame.ResetCommand=null; UiRuntime.Pump();
                Assert.Null(reset.Command);
                Assert.False(reset.IsEnabled);
            }
            finally { window.Close(); UiRuntime.Pump(); }
        });
    }

    private sealed class FieldRowModelTestCommand : System.Windows.Input.ICommand
    {
        public bool CanExecute(object? parameter)=>true;
        public void Execute(object? parameter) { }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    [Fact]
    public void Integer_row_default_above_hundred_keeps_no_validation_error_after_range_binding()
    {
        // 复现：拼接高度默认 2000 超出 NumericControl 默认 Maximum(100)，范围绑定晚到后
        // 无人重跑 RangeRule，WPF 默认 ErrorTemplate 红框残留（用户实机 2026-11-12）。
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            var port = new Port("stitch.height", DataType.Integer, Storage:NumericStorage.Int32,
                EditModes:FieldEditModes.Constant);
            var presentation = new FieldPresentation("stitch.height","拼接高度","基本参数",82,FieldEditorKind.Scalar)
            {
                Layout=new("基本参数","基本参数",Minimum:0,Maximum:20000,DecimalPlaces:0,AllowReference:false)
            };
            var writes = new List<ParameterSetting>();
            using var row = new DeclaredFieldRow(port,"拼接高度",new ConstantSetting(2000L),writes.Add,
                presentation:presentation);
            var window = new Window { Content=row, Width=420, Height=240, ShowInTaskbar=false };
            window.Resources.MergedDictionaries.Add(EditorResources.Shared);
            window.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/ParameterPresentation/resourcedictionaries/ParamRowDataTemplates.xaml",UriKind.Relative)
            });
            try
            {
                window.Show(); window.UpdateLayout(); UiRuntime.Pump();
                var numeric = Assert.Single(UiRuntime.Visuals<
                    SunEyeVision.Plugin.Wpf.Native.Numeric.Interaction.ListView.NumericControl>(row),
                    item=>item.IsVisible);
                var text = UiRuntime.Visuals<TextBox>(numeric).Single(item=>item.Name=="ValueText");
                Assert.Equal("2000", text.Text);
                if (System.Windows.Controls.Validation.GetHasError(text))
                {
                    var errors=System.Windows.Controls.Validation.GetErrors(text);
                    Assert.Fail($"数值行文本残留校验错误：{string.Join("；",errors.Select(item=>item.ErrorContent))}");
                }
            }
            finally { window.Close(); UiRuntime.Pump(); }
        });
    }

    [Fact]
    public void Optional_roi_mode_with_default_uses_original_radio_without_changing_formal_input()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            var port = new Port("area", DataType.Integer, Required:false, EditModes:FieldEditModes.Constant);
            var schema = new ToolSchema(new[] { port }, Array.Empty<Port>(), new ToolPresentation(Array.Empty<FieldPresentation>()));
            ParameterSetting accepted = new UnsetSetting();
            var writes = 0;
            using var inputs = new DeclaredInputFieldsView(schema, _ => accepted, (formal, setting) =>
            {
                Assert.Same(port, formal);
                accepted = setting;
                writes++;
            }, ReferenceFieldOptions.Empty);
            var row = inputs.ComposeInputEditor("area", "ROI区域类型", defaultWhenUnset:new ConstantSetting(0L),
                integerRadioOptions:new NativeIntegerRadioOptions(new[] { new NativeIntegerRadioChoice(0,"图形类型"), new NativeIntegerRadioChoice(1,"图像类型") }));
            Assert.Equal(0L, Assert.IsType<ConstantSetting>(row.Setting).Value);
            Assert.IsType<UnsetSetting>(accepted);
            Assert.Equal(0, writes);
            Assert.False(port.Required);
            Assert.IsType<FieldRowModel>(row.Content).CurValueA = 1;
            Assert.Equal(1L, Assert.IsType<ConstantSetting>(accepted).Value);
            Assert.Equal(1, writes);
            accepted = new UnsetSetting();
            inputs.RefreshInputProjection("area", new ConstantSetting(0L));
            Assert.Equal(0L, Assert.IsType<ConstantSetting>(row.Setting).Value);
            Assert.Equal(1, writes);
        });
    }

    [Fact]
    public void Invalid_radio_declaration_preserves_original_error_when_constructor_is_unwound()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            var failure = Assert.Throws<ArgumentException>(() => new DeclaredFieldRow(
                new Port("invalid", DataType.Text), "无效单选", new ConstantSetting("text"), _ => { },
                integerRadioOptions:new NativeIntegerRadioOptions(new[] { new NativeIntegerRadioChoice(0,"图形类型") })));
            Assert.Contains("Int64", failure.Message);
        });
    }

    [Theory]
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    [InlineData(true,true)]
    public void Boolean_templates_follow_acceptance_rejection_and_retirement_with_original_state_tips(bool initial,bool withTips)
    {
        if(System.Windows.Application.Current is null)SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(()=>
        {
            var writes=new List<bool>();var reject=false;
            var port=new Port("save",DataType.Boolean,EditModes:FieldEditModes.Constant);
            var presentation=new FieldPresentation("save","保存图像","基本参数",0,FieldEditorKind.Scalar)
                {Layout=new("基本参数","基本参数",BooleanTips:withTips?new("最大保存120张图片","只保存路径"):null)};
            using var row=new DeclaredFieldRow(port,"保存图像",new ConstantSetting(initial),setting=>
            {if(reject)throw new InvalidOperationException("拒绝本次修改");writes.Add((bool)((ConstantSetting)setting).Value!);},presentation:presentation);
            var window=new Window{Content=row,Width=420,Height=160,ShowInTaskbar=false};
            window.Resources.MergedDictionaries.Add(EditorResources.Shared);
            try
            {
                window.Show();window.UpdateLayout();UiRuntime.Pump();
                var model=Assert.IsType<FieldRowModel>(row.Content);Assert.Equal(withTips?"BooleanTips":"Boolean",model.TemplateKey);
                var control=Assert.Single(UiRuntime.Visuals<SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation.ParamTabControl.BooleanControl>(row));
                var text=withTips?Assert.Single(UiRuntime.Visuals<TextBlock>(row),item=>ReferenceEquals(item.DataContext,model)&&item.MaxWidth==150):null;
                void Check(bool value)
                {
                    UiRuntime.Pump();Assert.Equal(value,Assert.IsType<ConstantSetting>(row.Setting).Value);
                    Assert.Equal(value,control.IsChecked);Assert.Equal(withTips?(value?"最大保存120张图片":"只保存路径"):string.Empty,model.MessageTips);
                    if(text is not null){Assert.Equal(model.MessageTips,text.Text);Assert.Equal(text.Text,text.ToolTip);}
                }
                Check(initial);
                if(text is not null){Assert.Equal(12d,text.FontSize);Assert.Equal(TextTrimming.CharacterEllipsis,text.TextTrimming);Assert.Equal(new Thickness(5,0,0,0),text.Margin);}
                control.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,!initial);Check(!initial);
                reject=true;control.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,initial);Check(!initial);
                reject=false;control.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,initial);Check(initial);
                var accepted=writes.Count;row.Dispose();model.CurValueA=!initial;Assert.Equal(accepted,writes.Count);
            }
            finally{window.Close();UiRuntime.Pump();}
        });
    }

    [Fact]
    public void State_tips_reject_conflicting_declarations_before_a_visual_is_built()
    {
        var presentation=new FieldPresentation("value","值","基本参数",0,FieldEditorKind.Scalar)
            {Layout=new("基本参数","基本参数",BooleanTips:new("开"))};
        foreach(var port in new[]{new Port("value",DataType.Integer,EditModes:FieldEditModes.Constant),
            new Port("value",DataType.Boolean,Nullable:true,EditModes:FieldEditModes.Constant),
            new Port("value",DataType.Boolean,EditModes:FieldEditModes.Reference)})
        {
            Assert.Throws<ArgumentException>(()=>new ToolSchema(new[]{port},Array.Empty<Port>(),new ToolPresentation(new[]{presentation})));
            Assert.Throws<ArgumentException>(()=>DeclaredFieldTemplateRegistry.ResolveTemplateKey(port,presentation));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Assist_source_creation_observes_a_reentrant_mode_change_or_close(bool close)
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            using var mode = new DeclaredFieldRow(new Port("mode", DataType.Integer,
                EditModes: FieldEditModes.Constant), "选择方式", new ConstantSetting(0L), _ => { },
                integerRadioOptions: new NativeIntegerRadioOptions(new[]
                {
                    new NativeIntegerRadioChoice(0, "按信息"),
                    new NativeIntegerRadioChoice(1, "按点"),
                    new NativeIntegerRadioChoice(2, "按坐标")
                }, useFiveSlotTemplate: true));
            using var information = Row("information");
            using var point = Row("point");
            using var coordinate = Row("coordinate");
            SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeAssistUiDraftControls? assist = null;
            var coordinateCreates = 0;
            assist = new(mode, information, new (int Mode, Func<DeclaredFieldRow> Create)[]
            {
                (1, () => { if (close) assist!.Dispose(); else Assert.True(assist!.TrySetMode(2)); return point; }),
                (2, () => { coordinateCreates++; return coordinate; })
            });
            using var lifetime = assist;
            var window = new Window { Content = assist, Width = 420, Height = 500, ShowInTaskbar = false };
            window.Resources.MergedDictionaries.Add(EditorResources.Shared);
            try
            {
                window.Show(); window.UpdateLayout(); UiRuntime.Pump();
                assist.TrySetMode(1);
                window.UpdateLayout(); UiRuntime.Pump();
                if (close)
                {
                    Assert.Empty(assist.Children);
                    Assert.Equal(0, coordinateCreates);
                    Assert.True(UiRuntime.Field<bool>(point, "_disposed"));
                    Assert.False(assist.TrySetMode(2));
                    Assert.NotNull(assist.CommitActiveSources());
                }
                else
                {
                    Assert.Equal(1, coordinateCreates);
                    Assert.Equal(Visibility.Collapsed, point.Visibility);
                    Assert.Equal(Visibility.Visible, coordinate.Visibility);
                    Assert.Equal(Visibility.Collapsed, information.Visibility);
                    Assert.Same(assist, coordinate.Parent);
                }
            }
            finally { window.Close(); }
            static DeclaredFieldRow Row(string id) => new(new Port(id, DataType.Text, false, EditModes:FieldEditModes.Reference),
                id, new UnsetSetting(), _ => { }, ReferenceFieldOptions.Empty);
        });
    }

    [Fact]
    public void Input_source_read_cannot_install_an_editor_after_closing_its_owner()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            var port = new Port("point", FixtureDeclarations.Point, false, EditModes:FieldEditModes.Reference);
            var schema = new ToolSchema(new[] { port }, Array.Empty<Port>(), new ToolPresentation(Array.Empty<FieldPresentation>()));
            DeclaredInputFieldsView? inputs = null;
            using var owner = inputs = new(schema, _ => { inputs!.Dispose(); return new UnsetSetting(); },
                (_, _) => { }, ReferenceFieldOptions.Empty);
            Assert.Throws<ObjectDisposedException>(() => inputs.ComposeInputEditor("point", "点", referenceOnly:true));
            Assert.Empty(inputs.Fields);
        });
    }

    [Fact]
    public void Native_parameter_list_uses_the_same_group_projection_when_a_slot_is_inserted()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            var list = new SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation.ParamListView();
            var hiddenGroup = new SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterGroup("隐藏组");
            var visibleGroup = new SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterGroup("参数组");
            using var hidden = new SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow(hiddenGroup,
                new ContentControl { Visibility = Visibility.Collapsed });
            using var visible = new SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow(visibleGroup, new ContentControl());
            var rows = new System.Collections.ObjectModel.ObservableCollection<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow> { visible };
            list.SetRows(rows);
            try
            {
                rows.Insert(0, hidden);
                Assert.False(hiddenGroup.CustomVisibleA);
                Assert.False(hiddenGroup.IsFirst);
                Assert.True(visibleGroup.IsFirst);

                hidden.View!.Visibility = Visibility.Visible;
                rows.Move(0, 1);
                Assert.True(hiddenGroup.CustomVisibleA);
                Assert.True(visibleGroup.IsFirst);
                rows.Move(1, 0);
                Assert.True(hiddenGroup.IsFirst);
                Assert.False(visibleGroup.IsFirst);
            }
            finally { list.SetRows(null); }
        });
    }

    [Theory]
    [InlineData(1, Visibility.Hidden)]
    [InlineData(3, Visibility.Visible)]
    public void Declared_page_count_controls_the_native_tab_header(int pageCount, Visibility expected)
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            var ports = Enumerable.Range(0, pageCount).Select(index => new Port($"field{index}", DataType.Boolean)).ToArray();
            var presentation = new ToolPresentation(ports.Select((port, index) =>
                new FieldPresentation(port.Id, port.Id, $"页{index}", index, FieldEditorKind.Scalar)).ToArray());
            var schema = new ToolSchema(ports, Array.Empty<Port>(), presentation);
            var values = ports.ToDictionary(port => port.Id, _ => (ParameterSetting)new ConstantSetting(false));
            using var view = new DeclaredSettingsView(new FieldSettingsDraft(schema, new FieldSettings(ports, values)));
            var tree = Assert.IsType<DeclaredFieldsView>(view.Content);
            var tabs = Assert.IsAssignableFrom<TabControl>(tree.Content);
            var window = new Window { Content = view, Width = 420, Height = 500, ShowInTaskbar = false };
            window.Resources.MergedDictionaries.Add(EditorResources.Shared);
            try
            {
                window.Show();
                window.UpdateLayout();
                UiRuntime.Pump();
                var header = Assert.IsType<Border>(tabs.Template.FindName("head", tabs));
                Assert.Equal(expected, header.Visibility);
                Assert.Equal(pageCount, tabs.Items.Count);
                Assert.Equal("页0", view.SelectedPageKey);
                if (pageCount > 1)
                {
                    view.SelectedPageIndex = pageCount - 1;
                    Assert.Equal($"页{pageCount - 1}", view.SelectedPageKey);
                    Assert.Equal(Visibility.Visible, header.Visibility);
                }
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void A_disabled_reference_row_cannot_open_or_revive_an_expanded_request()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            var root = new StackPanel();
            using var owner = new ReferenceWindowOwner(root, () => true, new(), false);
            using var row = new DeclaredFieldRow(new Port("pose", FixtureDeclarations.Info, false, EditModes:FieldEditModes.Reference),
                "修正信息", new UnsetSetting(), _ => throw new InvalidOperationException("本用例不提交订阅。"),
                ReferenceFieldOptions.Empty with { WindowOwner = owner }) { IsEnabled = false };
            root.Children.Add(row);
            var window = new Window { Content = root, Width = 600, Height = 180, ShowInTaskbar = false };
            window.Resources.MergedDictionaries.Add(EditorResources.Shared);
            try
            {
                window.Show();
                window.UpdateLayout();
                UiRuntime.Pump();
                var model = Assert.IsType<FieldRowModel>(row.Content);
                var session = Assert.IsAssignableFrom<object>(model.ReferenceDataA);
                var open = Assert.IsAssignableFrom<System.Windows.Input.ICommand>(
                    session.GetType().GetProperty("PopSubscribeWindowCommand")!.GetValue(session));
                open.Execute(null);
                Assert.False(owner.IsExpanded);
                Assert.Null(owner.CurrentRequest);

                row.IsEnabled = true;
                open.Execute(null);
                var accepted = Assert.IsType<ReferenceExpansionRequest>(owner.CurrentRequest);
                row.IsEnabled = false;
                Assert.Null(owner.CurrentRequest);
                row.IsEnabled = true;
                Assert.False(accepted.RefreshCommand.CanExecute(null));
                open.Execute(null);
                Assert.NotSame(accepted, Assert.IsType<ReferenceExpansionRequest>(owner.CurrentRequest));
            }
            finally { window.Close(); UiRuntime.Pump(); }
        });
    }

    [Fact]
    public void Mixed_fixture_summary_is_read_only_but_source_picker_can_clear_or_replace_it()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            var type = FixtureDeclarations.Info;
            var candidate = new DeclaredReferenceCandidate("修正源", "source", "workflow",
                new Port("fixture", type), Array.Empty<string>());
            var writes = new List<ParameterSetting>();
            using var row = new DeclaredFieldRow(new Port("input.pose", type, false, EditModes: FieldEditModes.Reference),
                "修正信息", new UnsetSetting(), writes.Add,
                new ReferenceFieldOptions(_ => new ReferenceCandidates(new[] { candidate }, Array.Empty<DeclaredReferenceCandidate>()),
                    EditableIndex: true));
            var window = new Window { Content = row, Width = 600, Height = 180, ShowInTaskbar = false };
            window.Resources.MergedDictionaries.Add(EditorResources.Shared);
            try
            {
                window.Show();
                window.UpdateLayout();
                UiRuntime.Pump();
                row.SetReferenceSummary("(左侧来源, 右侧来源)");
                var model = Assert.IsType<FieldRowModel>(row.Content);
                var session = Assert.IsAssignableFrom<object>(model.ReferenceDataA);
                object? Read(object owner, string property) => owner.GetType().GetProperty(property)!.GetValue(owner);
                void Command(string property, object? argument = null) =>
                    Assert.IsAssignableFrom<System.Windows.Input.ICommand>(Read(session, property)).Execute(argument);
                object[] Children(object owner, string property) =>
                    Assert.IsAssignableFrom<System.Collections.IEnumerable>(Read(owner, property)).Cast<object>().ToArray();
                Assert.True(row.IsEnabled);
                Assert.Equal(true, Read(session, "ReadOnlyA"));
                session.GetType().GetProperty("CurValueA")!.SetValue(session, "");
                Assert.Empty(writes);

                Command("DropDownCommand");
                var clear = Children(session, "SourceNodes").Single(node => Read(node, "Clear") is true);
                Command("ClickCommand", clear);
                Assert.IsType<UnsetSetting>(Assert.Single(writes));

                Command("DropDownCommand");
                var source = Children(session, "SourceNodes").Single(node => Read(node, "Clear") is false);
                Command("ClickCommand", Assert.Single(Children(source, "EnumEntrysA")));
                Assert.Equal(2, writes.Count);
                var selected = Assert.IsType<ReferenceSetting>(writes[1]).Reference;
                Assert.Equal("source", selected.NodeId);
                Assert.Equal("fixture", selected.PortId);
                Assert.IsType<BindingSelection.AllItems>(selected.Selection);
            }
            finally { window.Close(); UiRuntime.Pump(); }
        });
    }

    [Fact]
    public void Editing_an_unrelated_field_does_not_suspend_a_hidden_range_again()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            var ports = new[] { new Port("enabled", DataType.Boolean), new Port("low", DataType.Integer), new Port("high", DataType.Integer) };
            var presentation = new ToolPresentation(new[]
            {
                new FieldPresentation("enabled", "启用", "运行参数", 0, FieldEditorKind.Scalar),
                new FieldPresentation("low", "下限", "高级", 1, FieldEditorKind.Scalar)
                    { Layout = new("运行参数", "高级", Advanced: true, PairWith: "high", Minimum: 1, Maximum: 100) },
                new FieldPresentation("high", "上限", "高级", 2, FieldEditorKind.Scalar)
                    { Layout = new("运行参数", "高级", Advanced: true, Minimum: 1, Maximum: 100) }
            });
            var schema = new ToolSchema(ports, Array.Empty<Port>(), presentation);
            var draft = new FieldSettingsDraft(schema, new FieldSettings(ports, new Dictionary<string, ParameterSetting>
            {
                ["enabled"] = new ConstantSetting(false), ["low"] = new ConstantSetting(10L), ["high"] = new ConstantSetting(20L)
            }));
            using var view = new DeclaredSettingsView(draft, new());
            var tree = Assert.IsType<DeclaredFieldsView>(view.Content);
            var range = Assert.IsAssignableFrom<FrameworkElement>(tree.Fields.Single(field => field.FieldId == "low").View);
            Assert.Same(range, tree.Fields.Single(field => field.FieldId == "high").View);
            Assert.Equal(Visibility.Collapsed, range.Visibility);
            var generation = UiRuntime.Field<long>(range, "_generation");
            Assert.True(view.TrySetFields(new Dictionary<string, ParameterSetting> { ["enabled"] = new ConstantSetting(true) }));
            Assert.True(draft.CaptureFields().Get<bool>("enabled"));
            Assert.Equal(generation, UiRuntime.Field<long>(range, "_generation"));
            Assert.Equal(Visibility.Collapsed, range.Visibility);
        });
    }

    [Fact]
    public void Rebinding_and_disposal_abort_the_queued_numeric_read()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            var port = new Port("threshold", DataType.Integer);
            var schema = new ToolSchema(new[] { port }, Array.Empty<Port>(),
                new ToolPresentation(new[] { new FieldPresentation(port.Id, "阈值", "运行参数", 0, FieldEditorKind.Scalar) }));
            var draft = new FieldSettingsDraft(schema,
                new FieldSettings(schema.Inputs, new Dictionary<string, ParameterSetting> { [port.Id] = new ConstantSetting(10L) }));
            var calls = 0;
            var references = ReferenceFieldOptions.Empty with
            {
                ReadCurrentValues = () =>
                {
                    calls++;
                    return new Dictionary<string, ConstantSetting?> { [port.Id] = new(12L) };
                }
            };
            var options = new ToolSettingsViewOptions(ReferenceOptions: references);
            using var view = new DeclaredSettingsView(draft, options);
            var original = UiRuntime.Field<System.Windows.Threading.DispatcherOperation>(view, "_pendingNumericRead")!;
            view.Bind(draft, options);
            Assert.Equal(System.Windows.Threading.DispatcherOperationStatus.Aborted, original.Status);
            var next = UiRuntime.Field<System.Windows.Threading.DispatcherOperation>(view, "_pendingNumericRead")!;
            view.Dispose();
            Assert.Equal(System.Windows.Threading.DispatcherOperationStatus.Aborted, next.Status);
            UiRuntime.Pump(System.Windows.Threading.DispatcherPriority.SystemIdle);
            Assert.Equal(0, calls);
        });
    }

    [Fact]
    public void Bulk_numeric_read_compares_both_range_ends_from_the_same_receipt()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            var ports = new[] { new Port("low", DataType.Integer), new Port("high", DataType.Integer) };
            var presentation = new ToolPresentation(new[]
            {
                new FieldPresentation("low", "下限", "范围", 0, FieldEditorKind.Scalar) { Layout = new("运行参数", "范围", PairWith: "high", Minimum: 1, Maximum: 100) },
                new FieldPresentation("high", "上限", "范围", 1, FieldEditorKind.Scalar) { Layout = new("运行参数", "范围", Minimum: 1, Maximum: 100) }
            });
            var schema = new ToolSchema(ports, Array.Empty<Port>(), presentation);
            var draft = new FieldSettingsDraft(schema, new FieldSettings(ports, new Dictionary<string, ParameterSetting>
            {
                ["low"] = new ConstantSetting(10L), ["high"] = new ConstantSetting(20L)
            }));
            long low = 30, high = 40;
            var options = ReferenceFieldOptions.Empty with
            {
                ReadCurrentValues = () => new Dictionary<string, ConstantSetting?>
                {
                    ["low"] = new(low), ["high"] = new(high)
                }
            };
            using var view = new DeclaredSettingsView(draft, new(ReferenceOptions: options));
            void Check(NumericModelReadStatus expected)
            {
                var result = view.ReadNumericModels(NumericModelReadScope.All);
                Assert.Equal(new[] { "low", "high" }, result.Select(item => item.FieldId));
                Assert.All(result, item => Assert.Equal(expected, item.Status));
            }
            Check(NumericModelReadStatus.Updated);
            low = 60; high = 50;
            Check(NumericModelReadStatus.Failed);
            low = 30; high = 40;
            Check(NumericModelReadStatus.Unchanged);
            low = 5; high = 8;
            Check(NumericModelReadStatus.Updated);
            Assert.Equal(10L, draft.CaptureFields().Get<long>("low"));
            Assert.Equal(20L, draft.CaptureFields().Get<long>("high"));
        });
    }

    [Fact]
    public void Text_and_reference_modes_keep_their_own_native_session_and_restore_the_constant()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            using var row = new DeclaredFieldRow(
                new Port("caption", DataType.Text, EditModes: FieldEditModes.Constant | FieldEditModes.Reference),
                "文字", new ConstantSetting("原文字"), _ => { }, ReferenceFieldOptions.Empty);
            var window = new Window { Content = row, Width = 600, Height = 180, ShowInTaskbar = false };
            window.Resources.MergedDictionaries.Add(EditorResources.Shared);
            try
            {
                window.Show(); window.UpdateLayout(); UiRuntime.Pump();
                var model = Assert.IsType<FieldRowModel>(row.Content);
                Assert.NotNull(model.TextDataA);
                Assert.NotNull(model.ReferenceDataA);
                Assert.NotSame(model.TextDataA, model.ReferenceDataA);
                SunEyeVision.Plugin.Wpf.Controls.References.Primitives.DeleteTextBox VisibleInput() =>
                    Assert.Single(UiRuntime.Visuals<SunEyeVision.Plugin.Wpf.Controls.References.Primitives.DeleteTextBox>(row)
                        .Where(input => input.IsVisible));
                Assert.Same(model.TextDataA, VisibleInput().DataContext);
                Assert.Equal("原文字", VisibleInput().Text);
                for (var iteration = 0; iteration < 3; iteration++)
                {
                    model.TypeChangeCommand.Execute(null); window.UpdateLayout(); UiRuntime.Pump();
                    Assert.True(model.IsSubA);
                    Assert.Same(model.ReferenceDataA, VisibleInput().DataContext);
                    model.TypeChangeCommand.Execute(null); window.UpdateLayout(); UiRuntime.Pump();
                    Assert.False(model.IsSubA);
                    Assert.Same(model.TextDataA, VisibleInput().DataContext);
                    Assert.Equal("原文字", VisibleInput().Text);
                    Assert.Equal("原文字", Assert.IsType<ConstantSetting>(row.Setting).Value);
                }
                row.Dispose();
                Assert.Null(model.TextDataA);
                Assert.Null(model.ReferenceDataA);
            }
            finally { window.Close(); UiRuntime.Pump(); }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_radio_selection_writes_once_and_retired_controls_cannot_write(bool expanded)
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            var writes = new List<ParameterSetting>();
            var choices = Enumerable.Range(0, expanded ? 3 : 2)
                .Select(index => new NativeIntegerRadioChoice(index, "选项" + index));
            using var row = new DeclaredFieldRow(new Port("mode", DataType.Integer), "选择方式",
                new ConstantSetting(0L), writes.Add,
                integerRadioOptions: new NativeIntegerRadioOptions(choices, expanded));
            var window = new Window { Content = row, Width = 600, Height = 180, ShowInTaskbar = false };
            window.Resources.MergedDictionaries.Add(EditorResources.Shared);
            try
            {
                window.Show(); window.UpdateLayout(); UiRuntime.Pump();
                var radios = UiRuntime.Visuals<RadioButton>(row)
                    .Where(button => button.IsVisible).ToArray();
                Assert.Equal(expanded ? 3 : 2, radios.Length);
                if (!expanded)
                {
                    var expectedWidth = SunEyeVision.Plugin.Wpf.Native.Common.Localization.AppLocalizationService.GetLang("Language") == "English" ? 165d : 89d;
                    Assert.Equal(expectedWidth, Assert.IsType<FieldRowModel>(row.Content).LangRadioSelecterWidth);
                    Assert.Equal(expectedWidth, Assert.Single(UiRuntime.Visuals<SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation.ParamTab.LeftRightControl>(row)).ChildLeftWidth);
                }
                Assert.True(radios[0].IsChecked);
                Assert.Empty(writes);
                for (var index = 1; index < radios.Length; index++)
                {
                    radios[index].IsChecked = true; UiRuntime.Pump();
                    Assert.Equal((long)index, Assert.IsType<ConstantSetting>(row.Setting).Value);
                    Assert.Equal(index, writes.Count);
                    Assert.Single(radios.Where(button => button.IsChecked == true));
                }
                var count = writes.Count;
                row.Dispose();
                radios[0].IsChecked = true; UiRuntime.Pump();
                Assert.Equal(count, writes.Count);
            }
            finally { window.Close(); UiRuntime.Pump(); }
        });
    }

    [Fact]
    public void Image_source_capture_rechecks_removed_and_restored_candidates_without_writing()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            var type = TypeRef.ResourceOf(ResourceKind.Image);
            var candidate = new DeclaredReferenceCandidate("源图像", "source", "workflow", new Port("output", type), Array.Empty<string>());
            var binding = new ReferenceSetting(candidate.Reference(BindingSelection.All));
            var writes = 0;
            using var row = new DeclaredFieldRow(new Port("image", type, EditModes: FieldEditModes.Reference),
                "图像来源", binding, _ => writes++, ReferenceFieldOptions.Empty);
            IReadOnlyList<DeclaredReferenceCandidate> candidates = new[] { candidate };
            row.UseImageSourceSelector(() => candidates);
            var combo = Assert.IsType<ComboBox>(row.InputElement);
            var initial = Assert.IsType<DeclaredFieldRow.ImageSourceChoice>(combo.SelectedItem);
            Assert.True(row.TryCommitPending(), row.Error);
            Assert.Same(initial, combo.SelectedItem);

            candidates = Array.Empty<DeclaredReferenceCandidate>();
            Assert.False(row.TryCommitPending());
            Assert.False(Assert.IsType<DeclaredFieldRow.ImageSourceChoice>(combo.SelectedItem).Enabled);
            candidates = new[] { candidate };
            Assert.True(row.TryCommitPending(), row.Error);
            var restored = Assert.IsType<DeclaredFieldRow.ImageSourceChoice>(combo.SelectedItem);
            Assert.True(restored.Enabled);
            Assert.True(row.TryCommitPending(), row.Error);
            Assert.Same(restored, combo.SelectedItem);
            Assert.Equal(0, writes);
            Assert.Equal("source", Assert.IsType<ReferenceSetting>(row.Setting).Reference.NodeId);
        });
    }

    [Fact]
    public void Image_source_unset_choice_uses_the_native_none_label()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            var type = TypeRef.ResourceOf(ResourceKind.Image);
            var candidate = new DeclaredReferenceCandidate("源图像", "source", "workflow", new Port("output", type), Array.Empty<string>());
            var writes = 0;
            using var row = new DeclaredFieldRow(new Port("image", type, Required: false, EditModes: FieldEditModes.Reference),
                "图像来源", new UnsetSetting(), _ => writes++, ReferenceFieldOptions.Empty);
            row.UseImageSourceSelector(() => new[] { candidate });
            var combo = Assert.IsType<ComboBox>(row.InputElement);
            var unset = Assert.IsType<DeclaredFieldRow.ImageSourceChoice>(combo.Items
                .OfType<DeclaredFieldRow.ImageSourceChoice>().Single(item => item.Setting is UnsetSetting));
            var none = SunEyeVision.Plugin.Wpf.Native.Common.Localization.AppLocalizationService.GetLang("RunParam_None", "无");
            Assert.Equal(none, unset.Label);
            Assert.Equal("清除图像来源", unset.ToolTip);
            Assert.Equal(0, writes);
        });
    }

    [Fact]
    public void Image_source_provider_reentry_cannot_restore_the_retired_selector()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            using var row = new DeclaredFieldRow(
                new Port("image", TypeRef.ResourceOf(ResourceKind.Image), EditModes: FieldEditModes.Reference),
                "图像来源", new UnsetSetting(), _ => throw new InvalidOperationException("候选刷新不得写参数。"),
                ReferenceFieldOptions.Empty, allowUnboundReference: true);
            object? installed = null;
            row.UseImageSourceSelector(() =>
            {
                row.UseImageSourceSelector(() => Array.Empty<DeclaredReferenceCandidate>());
                installed = Assert.IsType<FieldRowModel>(row.Content).ImageSourceSelector;
                return Array.Empty<DeclaredReferenceCandidate>();
            });
            Assert.IsType<ComboBox>(installed);
            var model = Assert.IsType<FieldRowModel>(row.Content);
            Assert.Equal(DeclaredFieldTemplateRegistry.ImageSource, model.TemplateKey);
            Assert.Same(installed, model.ImageSourceSelector);
            Assert.Same(installed, row.InputElement);
            Assert.IsType<UnsetSetting>(row.Setting);
        });
    }
}
