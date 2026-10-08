using System;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.UI.Features.CommunicationManager;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class NumericUpDownBindingTests
{
    private sealed class NumericValueHolder
    {
        public double Value { get; set; } = 1;
    }

    [Fact]
    public void Changing_displayed_byte_end_keeps_two_way_binding_and_updates_rule()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var rule = new CommunicationCompareInputViewModel { ByteEnd = 1 };
                var control = new NumericUpDown { Minimum = 0, Maximum = 65535 };
                BindingOperations.SetBinding(control, NumericUpDown.IntValueProperty,
                    new Binding(nameof(rule.ByteEnd))
                    {
                        Source = rule, Mode = BindingMode.TwoWay
                    });

                control.SetCurrentValue(NumericUpDown.ValueProperty, 3d);

                Assert.Equal(3, control.IntValue);
                Assert.NotNull(BindingOperations.GetBindingExpression(control, NumericUpDown.IntValueProperty));
                Assert.Equal(3, rule.ByteEnd);
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "数字控件绑定测试超时");
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }

    [Fact]
    public void Changing_displayed_range_maximum_updates_modbus_comparison_status()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var rule = new CommunicationCompareInputViewModel
                {
                    DataType = "int", IsByteRule = true, CompareRule = "范围内",
                    Minimum = 0, Maximum = 1, Value = 5, Result = "5"
                };
                var control = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue };
                BindingOperations.SetBinding(control, NumericUpDown.IntValueProperty,
                    new Binding(nameof(rule.Maximum))
                    {
                        Source = rule, Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    });

                Assert.Equal("NG", CommunicationReceiveProcessor.GetCompareStatus(rule));
                control.SetCurrentValue(NumericUpDown.ValueProperty, 6d);

                Assert.Equal(6, control.IntValue);
                Assert.Equal(6, rule.Maximum);
                Assert.Equal("OK", CommunicationReceiveProcessor.GetCompareStatus(rule));
                rule.CompareRule = "范围外";
                Assert.Equal("NG", CommunicationReceiveProcessor.GetCompareStatus(rule));
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "数字控件区间绑定测试超时");
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }

    [Fact]
    public void Integer_synchronization_does_not_replace_double_value_binding()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var source = new NumericValueHolder();
                var control = new NumericUpDown { Minimum = 0, Maximum = 100 };
                BindingOperations.SetBinding(control, NumericUpDown.ValueProperty,
                    new Binding(nameof(source.Value))
                    {
                        Source = source, Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    });

                control.SetCurrentValue(NumericUpDown.IntValueProperty, 3);

                Assert.NotNull(BindingOperations.GetBindingExpression(control, NumericUpDown.ValueProperty));
                Assert.Equal(3, source.Value);
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "数字控件绑定测试超时");
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
