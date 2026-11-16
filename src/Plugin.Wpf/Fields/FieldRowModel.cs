using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>
/// 单条声明字段的原厂命名投影（行模板 DataContext）。
///
/// 只投影静态声明（标签、数值边界、枚举项）与动态状态（当前值、订阅开关、未设置占位），
/// 不保存第二份参数值；值与校验全部落在 <see cref="DeclaredFieldState"/>，
/// 命令转回宿主 <see cref="DeclaredFieldRow"/> 执行。属性命名与原厂
/// <c>paramusercontroldatatemplate.xaml</c> 绑定名保持一致（CurValueA/EnumEntrysA/…）。
/// </summary>
public sealed class FieldRowModel : INotifyPropertyChanged
{
    /// <summary>枚举/单选项投影；命名对齐原厂模板（DisplayNameALang/ValueA）。</summary>
    public sealed record Entry(string? DisplayNameALang, object? ValueA, bool IsAvailable = true, string UnavailableReason = "");

    private readonly DeclaredFieldRow _owner;
    private string _displayNameA;
    private object? _curValueA;
    private bool _isSubA;
    private bool _subscribeVisibleA;
    private bool _clearVisibleA;
    private bool _isUnsetA;
    private bool _setAvailableA;
    private object? _referenceDataA;
    private object? _textDataA;
    private readonly BooleanStateTips? _booleanTips;

    internal FieldRowModel(
        DeclaredFieldRow owner,
        string templateKey,
        string displayName,
        double minValue,
        double maxValue,
        double increment,
        int? decimalDigits,
        double radioWidth,
        IReadOnlyList<Entry> entries,
        bool hasReferenceBranch,BooleanStateTips? booleanTips=null)
    {
        _owner = owner;
        _booleanTips=booleanTips;
        TemplateKey = templateKey;
        _displayNameA = displayName;
        MinValueA = minValue;
        MaxValueA = maxValue;
        IncValueA = increment;
        DecimalDigitsA = decimalDigits;
        LangRadioSelecterWidth = radioWidth;
        EnumEntrysA = entries;
        HasReferenceBranch = hasReferenceBranch;
        ValueChangedCommand = new DelegateCommand(parameter => _owner.HandleTemplateValueProposal(parameter));
        SelectedChangedCommand = new DelegateCommand(_ => _owner.HandleTemplateValueProposal(_curValueA));
        TypeChangeCommand = new DelegateCommand(_ => _owner.HandleTypeChange());
        SetCommand = new DelegateCommand(_ => _owner.HandleUnsetExit());
        ClearCommand = new DelegateCommand(_ => _owner.HandleClear());
        BrowseFolderCommand = new DelegateCommand(_ => _owner.BrowseFolder());
    }

    // ---- 静态投影 ----
    /// <summary>原厂模板键（DeclaredFieldTemplateRegistry 常量）。</summary>
    public string TemplateKey { get; internal set; }
    /// <summary>字段身份（声明稳定 ID）。</summary>
    public string FieldId => _owner.FieldId;
    public string DisplayNameA => _displayNameA;
    private string? _description;
    public string DescriptionA => _description ?? _displayNameA;
    public string MessageTips => (_curValueA is true?_booleanTips?.Selected:_booleanTips?.Unselected)??string.Empty;
    public double MinValueA { get; }
    public double MaxValueA { get; }
    public double IncValueA { get; }
    public int? DecimalDigitsA { get; }
    public double LangRadioSelecterWidth { get; }
    public IReadOnlyList<Entry> EnumEntrysA { get; }
    /// <summary>本行是否有常量⇄订阅切换分支（决定模板中切换按钮可见性）。</summary>
    public bool HasReferenceBranch { get; }

    // ---- 动态投影（模板 TwoWay/触发器绑定） ----
    /// <summary>当前值投影；模板 TwoWay 绑定写入时转回宿主校验与提交，失败由宿主投影回退。</summary>
    public object? CurValueA
    {
        get => _curValueA;
        set => _owner.HandleTemplateValueProposal(value);
    }

    public bool IsSubA => _isSubA;
    public bool SubscribeVisibleA => _subscribeVisibleA;
    public bool ClearVisibleA => _clearVisibleA;
    public bool IsUnsetA => _isUnsetA;
    public bool SetAvailableA => _setAvailableA;
    /// <summary>正常输入区显隐（未设置时隐藏）。</summary>
    public Visibility NormalVisibilityA => _isUnsetA ? Visibility.Collapsed : Visibility.Visible;
    /// <summary>未设置占位区显隐。</summary>
    public Visibility UnsetVisibilityA => _isUnsetA ? Visibility.Visible : Visibility.Collapsed;
    /// <summary>常量⇄订阅切换按钮提示。</summary>
    public string ModeToolTipA => _isSubA ? "切换为数值编辑" : "切换为订阅";
    /// <summary>引用模板会话；与常量文本会话分别投影，共享同一字段状态。</summary>
    public object? ReferenceDataA => _referenceDataA;
    public object? ImageSourceSelector { get; private set; }
    public object? TextDataA => _textDataA;

    // ---- 命令 ----
    public ICommand ValueChangedCommand { get; }
    public ICommand SelectedChangedCommand { get; }
    public ICommand TypeChangeCommand { get; }
    public ICommand SetCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand BrowseFolderCommand { get; }

    // ---- 宿主回写入口（内部） ----
    internal void SetCurValue(object? value)
    {
        if (Equals(_curValueA, value))
            return;
        _curValueA = value;
        Notify(nameof(CurValueA));
        Notify(nameof(MessageTips));
    }

    // A TwoWay target may have changed while the owner rejected its proposal.
    // The accepted source can be unchanged, but the target still needs its receipt.
    internal void ReapplyCurrentValue()
    {
        Notify(nameof(CurValueA));
        Notify(nameof(MessageTips));
    }

    internal void SetIsSub(bool value)
    {
        if (_isSubA == value)
            return;
        _isSubA = value;
        Notify(nameof(IsSubA));
        Notify(nameof(ModeToolTipA));
    }

    internal void SetSubscribeVisible(bool value)
    {
        if (_subscribeVisibleA == value)
            return;
        _subscribeVisibleA = value;
        Notify(nameof(SubscribeVisibleA));
    }

    internal void SetClearVisible(bool value)
    {
        if (_clearVisibleA == value)
            return;
        _clearVisibleA = value;
        Notify(nameof(ClearVisibleA));
    }

    internal void SetUnset(bool isUnset, bool setAvailable)
    {
        var changed = false;
        if (_isUnsetA != isUnset)
        {
            _isUnsetA = isUnset;
            changed = true;
        }

        if (_setAvailableA != setAvailable)
        {
            _setAvailableA = setAvailable;
            changed = true;
        }

        if (changed)
        {
            Notify(nameof(IsUnsetA));
            Notify(nameof(SetAvailableA));
            Notify(nameof(NormalVisibilityA));
            Notify(nameof(UnsetVisibilityA));
        }
    }

    internal void SetReferenceData(object? value)
    {
        if (ReferenceEquals(_referenceDataA, value))
            return;
        _referenceDataA = value;
        Notify(nameof(ReferenceDataA));
    }

    internal void SetImageSourceSelector(object value)
    {
        ImageSourceSelector = value;
        Notify(nameof(ImageSourceSelector));
    }

    internal void SetTextData(object? value)
    {
        if (ReferenceEquals(_textDataA, value))
            return;
        _textDataA = value;
        Notify(nameof(TextDataA));
    }

    internal void SetDisplayName(string value)
    {
        if (string.Equals(_displayNameA, value, StringComparison.Ordinal))
            return;
        _displayNameA = value;
        Notify(nameof(DisplayNameA));
        if (_description is null) Notify(nameof(DescriptionA));
    }

    internal void SetDescription(string value)
    {
        if (_description == value) return;
        _description = value;
        Notify(nameof(DescriptionA));
    }

    /// <summary>行释放后清空投影引用，避免模板内容悬挂会话对象。</summary>
    internal void DisposeProjection()
    {
        _referenceDataA = null;
        _textDataA = null;
        ImageSourceSelector = null;
        ParameterCleanup.Run(new Action[] { () => Notify(nameof(ReferenceDataA)), () => Notify(nameof(TextDataA)),
            () => Notify(nameof(ImageSourceSelector)) });
    }

    private void Notify(string property)
    {
        var handlers = PropertyChanged?.GetInvocationList().Cast<PropertyChangedEventHandler>() ?? Enumerable.Empty<PropertyChangedEventHandler>();
        ParameterCleanup.Run(handlers.Select(handler => (Action)(() => handler(this, new PropertyChangedEventArgs(property)))));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private sealed class DelegateCommand(Action<object?> execute) : ICommand
    {
        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }
    }
}
