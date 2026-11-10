using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Input;
using SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation.ParamTabControl;
using NativeRow=SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation.ParamTab.LeftRightControl;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.ParameterSchema.ParamTab;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>Temporary UI-only ROI state; no geometry, execution or persistence is implied.</summary>
public sealed class NativeRoiUiDraftControls : StackPanel, IDisposable
{
    private readonly List<DeclaredFieldState> _states=[];
    private readonly List<DeclaredFieldRow> _radios=[];
    private readonly List<DeclaredFieldRow> _inheritEditors=[];
    private readonly StackPanel _rectangleParameters=new(),_circleParameters=new();
    private readonly DeclaredFieldRow _circleSource;
    private IReadOnlyList<DeclaredFieldRow>? _rectangleEditors,_annulusEditors;
    private bool _rectangleParametersRealized,_circleParametersRealized;
    private readonly DeclaredFieldState _area,_detection,_inherit,_mask;
    private readonly FrameworkElement _imageInput,_detectionRadio,_inheritRadio,_maskRadio;
    private FrameworkElement _detectionShapes,_maskShapes;
    private FrameworkElement? _detectionSource,_maskSource;
    private bool _disposed;
    private readonly NativeRoiInheritanceBinding? _inheritance;
    private readonly Dictionary<DeclaredFieldRow,ParameterSetting> _baseline=[];
    public event Action? Changed;
    public bool HasUiChanges=>!_disposed&&(_states.Any(state=>(_inheritance is null||state!=_detection&&state!=_inherit)&&state.Accepted is not ConstantSetting {Value:0L})
        ||_inheritEditors.Any(editor=>editor.HasPendingEdits||!DeclaredFieldState.SameSetting(editor.Setting,_baseline[editor]))
        ||(_imageInput is NativeRow {RightContent:ComboBox image}&&(!string.IsNullOrEmpty(image.Text)||image.SelectedIndex>=0))
        ||(_detectionShapes.DataContext is ShapeContext detection&&detection.HasChanges)||(_maskShapes.DataContext is ShapeContext mask&&mask.HasChanges));
    private void OnUiChanged(){if(!_disposed)Changed?.Invoke();}
    private void OnShapeChanged(object? sender,PropertyChangedEventArgs args)=>OnUiChanged();
    private void OnImageSelection(object sender,SelectionChangedEventArgs args)=>OnUiChanged();
    private void OnImageText(object sender,TextChangedEventArgs args)=>OnUiChanged();
    public FrameworkElement GraphicVisibilitySource=>_detectionRadio;
    public NativeRoiUiDraftControls():this(false){}
    public NativeRoiUiDraftControls(bool geometryOnlyPresentation):this(geometryOnlyPresentation,null){}
    public NativeRoiUiDraftControls(bool geometryOnlyPresentation,NativeRoiInheritanceBinding? inheritance)
    {
        _inheritance=inheritance;
        if(inheritance is not null&&(inheritance.InitialMode is <0 or >3))
            throw new ArgumentException("继承组合需要四种模式。");
        var outputMask=new DeclaredFieldRow(new Port("ui-only-output-mask",DataType.Boolean,EditModes:FieldEditModes.Constant),"输出掩膜",
            new ConstantSetting(false),_=>{});
        _inheritEditors.Add(outputMask);Children.Add(outputMask);
        (_area,_)=AddRadio("ROI区域类型",new[]{"图形类型","图像类型"});
        _imageInput=new NativeRow{LeftContent=new TextBlock{Text="屏蔽图像",VerticalAlignment=VerticalAlignment.Center},
            RightContent=NativeInputFactory.Enumeration()};Children.Add(_imageInput);
        (_detection,_detectionRadio)=AddRadio("ROI创建",new[]{"绘制","继承"},inheritance?.InitialMode is null?0:1,
            value=>inheritance?.AcceptMode(value==0?null:(int)Value(_inherit)));
        _detectionShapes=Shape("形状",true);Children.Add(_detectionShapes);
        (_inherit, _inheritRadio)=AddRadio("继承方式",new[]{"按矩形区域","按矩形参数","按圆形区域","按圆形参数"},inheritance?.InitialMode??0,
            value=>{if(Value(_detection)==1)inheritance?.AcceptMode((int)value);});
        _circleSource=inheritance?.AnnulusSource??new DeclaredFieldRow(new Port("ui-only-circle-region",RoiDeclarations.Shape,Required:false,EditModes:FieldEditModes.Reference),
            "区域",new UnsetSetting(),_=>{},ReferenceFieldOptions.Empty,allowUnboundReference:true)
            {ToolTip="界面试用，来源功能尚未接入。"};
        if(inheritance is null)_inheritEditors.Add(_circleSource);Children.Add(_circleSource);
        if(inheritance is null)
        {
            AddParameterRows(_rectangleParameters,new[]{"ROI中心点X","ROI中心点Y","ROI宽度","ROI高度","ROI角度"});
            AddParameterRows(_circleParameters,new[]{"圆心点X","圆心点Y","内径","外径","起始角度","角度范围"});
        }
        else EnsureParameterEditors(inheritance.InitialMode);
        Children.Add(_rectangleParameters);Children.Add(_circleParameters);
        (_mask,_maskRadio)=AddRadio("屏蔽区创建",new[]{"绘制","继承"});
        _maskShapes=Shape("屏蔽区",true,initialFullScreen:false);Children.Add(_maskShapes);
        if(geometryOnlyPresentation)
        {
            DeclaredFieldRow Placeholder(string id,string label,TypeRef type)
            {
                var editor=new DeclaredFieldRow(new Port(id,type,Required:false,EditModes:FieldEditModes.Reference),label,new UnsetSetting(),_=>{},ReferenceFieldOptions.Empty,allowUnboundReference:true)
                    {ToolTip="仅预览，几何继承尚未接入执行。"};
                _inheritEditors.Add(editor);return editor;
            }
            BindSourceRows(inheritance?.RectangleSource??Placeholder("ui-only-rectangle-source","区域",RoiDeclarations.Shape),Placeholder("ui-only-mask-source","屏蔽区域",DataType.Text));
        }
        _area.Changed+=ProjectVisibility;_detection.Changed+=ProjectVisibility;_inherit.Changed+=ProjectVisibility;_mask.Changed+=ProjectVisibility;
        foreach(var editor in _inheritEditors){_baseline.Add(editor,editor.Setting);editor.Changed+=OnUiChanged;}
        foreach(var state in _states)state.Changed+=OnUiChanged;
        if(inheritance is not null)foreach(var editor in InheritanceEditors())editor.Changed+=OnUiChanged;
        ((ShapeContext)_detectionShapes.DataContext).PropertyChanged+=OnShapeChanged;
        ((ShapeContext)_maskShapes.DataContext).PropertyChanged+=OnShapeChanged;
        if(_imageInput is NativeRow {RightContent:ComboBox image})
        {image.SelectionChanged+=OnImageSelection;image.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,new TextChangedEventHandler(OnImageText));}
        ProjectVisibility();
    }
    public bool IsLocalDrawing(string group)=>!_disposed&&Value(_area)==0&&Value(group=="inspection"?_detection:_mask)==0;
    public void BindLocalEditors(NativeRoiEditor detection,NativeRoiEditor mask)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(_detectionShapes is NativeRoiEditor||detection.Parent is not null||mask.Parent is not null)throw new InvalidOperationException("区域编辑器已经归属视图。");
        if(_detectionShapes.DataContext is ShapeContext first)first.PropertyChanged-=OnShapeChanged;
        if(_maskShapes.DataContext is ShapeContext second)second.PropertyChanged-=OnShapeChanged;
        var firstIndex=Children.IndexOf(_detectionShapes);Children.RemoveAt(firstIndex);Children.Insert(firstIndex,detection);_detectionShapes=detection;
        var secondIndex=Children.IndexOf(_maskShapes);Children.RemoveAt(secondIndex);Children.Insert(secondIndex,mask);_maskShapes=mask;
        ProjectVisibility();
    }
    private IEnumerable<DeclaredFieldRow> InheritanceEditors()=>_inheritance is {} binding
        ?(_rectangleEditors??Array.Empty<DeclaredFieldRow>()).Concat(_annulusEditors??Array.Empty<DeclaredFieldRow>()).Append(binding.RectangleSource).Append(binding.AnnulusSource):Array.Empty<DeclaredFieldRow>();
    private void EnsureParameterEditors(int? mode)
    {
        if(_inheritance is not {} binding)return;
        if(mode==1&&!_rectangleParametersRealized)
        {
            _rectangleEditors=binding.RectangleParametersFactory();
            foreach(var editor in _rectangleEditors)_rectangleParameters.Children.Add(editor);
            _rectangleParametersRealized=true;
            foreach(var editor in _rectangleEditors){_baseline[editor]=editor.Setting;editor.Changed+=OnUiChanged;}
        }
        if(mode==3&&!_circleParametersRealized)
        {
            _annulusEditors=binding.AnnulusParametersFactory();
            foreach(var editor in _annulusEditors)_circleParameters.Children.Add(editor);
            _circleParametersRealized=true;
            foreach(var editor in _annulusEditors){_baseline[editor]=editor.Setting;editor.Changed+=OnUiChanged;}
        }
    }
    private (DeclaredFieldState,FrameworkElement) AddRadio(string label,string[] choices,long initial=0,Action<long>? accept=null)
    {
        var state=new DeclaredFieldState(new Port("ui-only-roi-"+_states.Count,DataType.Integer),new ConstantSetting(initial),proposal=>accept?.Invoke((long)((ConstantSetting)proposal).Value!));
        _states.Add(state);
        var radio=new DeclaredFieldRow(state,label,
            integerRadioOptions:new NativeIntegerRadioOptions(choices.Select((value,index)=>new NativeIntegerRadioChoice(index,value)),useFiveSlotTemplate:choices.Length>2));
        _radios.Add(radio);Children.Add(radio);return(state,radio);
    }
    private void AddParameterRows(Panel panel,IReadOnlyList<string> labels)
    {
        foreach(var label in labels)
        {
            var port=new Port("ui-only-inherit-"+_inheritEditors.Count,DataType.Number,Required:false,EditModes:FieldEditModes.Reference);
            var editor=new DeclaredFieldRow(port,label,new UnsetSetting(),_=>{},ReferenceFieldOptions.Empty,allowUnboundReference:true)
                {ToolTip="界面试用，来源功能尚未接入。"};
            _inheritEditors.Add(editor);panel.Children.Add(editor);
        }
    }
    private static long Value(DeclaredFieldState state)=>(long)((ConstantSetting)state.Accepted).Value!;
    public void BindSourceRows(FrameworkElement detectionSource,FrameworkElement maskSource)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        _detectionSource=detectionSource??throw new ArgumentNullException(nameof(detectionSource));
        _maskSource=maskSource??throw new ArgumentNullException(nameof(maskSource));
        if(detectionSource.Parent is not null||maskSource.Parent is not null)throw new ArgumentException("区域来源已有父元素。");
        Children.Insert(Children.IndexOf(_inheritRadio)+1,detectionSource);
        Children.Insert(Children.IndexOf(_maskShapes)+1,maskSource);
        ProjectVisibility();
    }
    private void ProjectVisibility()
    {
        if(_disposed)return;
        var graphic=Value(_area)==0;var draw=Value(_detection)==0;var maskDraw=Value(_mask)==0;
        if(graphic&&!draw)EnsureParameterEditors((int)Value(_inherit));
        _imageInput.Visibility=graphic?Visibility.Collapsed:Visibility.Visible;
        if(_detectionSource is not null)_detectionSource.Visibility=graphic&&!draw&&Value(_inherit)==0?Visibility.Visible:Visibility.Collapsed;
        _circleSource.Visibility=graphic&&!draw&&Value(_inherit)==2?Visibility.Visible:Visibility.Collapsed;
        _rectangleParameters.Visibility=graphic&&!draw&&Value(_inherit)==1?Visibility.Visible:Visibility.Collapsed;
        _circleParameters.Visibility=graphic&&!draw&&Value(_inherit)==3?Visibility.Visible:Visibility.Collapsed;
        if(_maskSource is not null)_maskSource.Visibility=graphic&&!maskDraw?Visibility.Visible:Visibility.Collapsed;
        _detectionRadio.Visibility=graphic?Visibility.Visible:Visibility.Collapsed;
        _detectionShapes.Visibility=graphic&&draw?Visibility.Visible:Visibility.Collapsed;
        _inheritRadio.Visibility=graphic&&!draw?Visibility.Visible:Visibility.Collapsed;
        _maskRadio.Visibility=graphic?Visibility.Visible:Visibility.Collapsed;
        _maskShapes.Visibility=graphic&&maskDraw?Visibility.Visible:Visibility.Collapsed;

    }
    private static RoiSelectControl Shape(string title,bool fullScreen,bool initialFullScreen=true)
        =>new(){DataContext=new ShapeContext(title,fullScreen,initialFullScreen)};
    private sealed class ShapeContext : INotifyPropertyChanged
    {
        private RoiType _selected;
        private readonly RoiType _initial;
        public bool HasChanges=>_selected!=_initial;
        public ShapeContext(string name,bool fullScreen,bool initialFullScreen)
        {DisplayNameA=name;FullScreenEnableA=fullScreen;_selected=fullScreen&&initialFullScreen?RoiType.FullScreen:RoiType.Default;_initial=_selected;ValueChangedCommand=new ShapeCommand(Select);}
        public string DisplayNameA {get;}
        public string DescriptionA=>DisplayNameA;
        public bool FullScreenEnableA {get;}
        public string ROISelectionA=>"Box SectorRing Polygon Sector";
        public bool IsHaveData=>false;
        public bool Is3DROI=>false;
        public object? CurROIData=>null;
        public bool FullScreenA {get=>_selected==RoiType.FullScreen;set{}}
        public bool BoxA {get=>_selected==RoiType.Box;set{}}
        public bool SectorRingA {get=>_selected==RoiType.SectorRing;set{}}
        public bool PolygonA {get=>_selected==RoiType.Polygon;set{}}
        public bool SectorA {get=>_selected==RoiType.Sector;set{}}
        public bool FullScreen_ModuleEnableA=>FullScreenA;
        public bool BoxEnable_ModuleEnableA=>BoxA;
        public bool SectorRingEnable_ModuleEnableA=>SectorRingA;
        public bool PolygonEnable_ModuleEnableA=>PolygonA;
        public bool SectorEnable_ModuleEnableA=>SectorA;
        public ICommand ValueChangedCommand {get;}
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Select(object? value)
        {
            if(value is not RoiType type||type is not (RoiType.FullScreen or RoiType.Box or RoiType.SectorRing or RoiType.Polygon or RoiType.Sector))return;
            if(type==RoiType.FullScreen&&!FullScreenEnableA)return;
            _selected=type;
            foreach(var name in new[]{nameof(FullScreenA),nameof(BoxA),nameof(SectorRingA),nameof(PolygonA),nameof(SectorA),
                nameof(FullScreen_ModuleEnableA),nameof(BoxEnable_ModuleEnableA),nameof(SectorRingEnable_ModuleEnableA),nameof(PolygonEnable_ModuleEnableA),nameof(SectorEnable_ModuleEnableA)})
                PropertyChanged?.Invoke(this,new(name));
        }
        private sealed class ShapeCommand(Action<object?> select):ICommand
        {public bool CanExecute(object? value)=>true;public void Execute(object? value)=>select(value);public event EventHandler? CanExecuteChanged {add{}remove{}}}
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        foreach(var editor in InheritanceEditors())editor.Changed-=OnUiChanged;
        if(_imageInput is NativeRow {RightContent:ComboBox image})
        {image.SelectionChanged-=OnImageSelection;image.RemoveHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,new TextChangedEventHandler(OnImageText));}
        foreach(var state in _states)state.Changed-=OnUiChanged;
        if(_detectionShapes?.DataContext is ShapeContext detection)detection.PropertyChanged-=OnShapeChanged;
        if(_maskShapes?.DataContext is ShapeContext mask)mask.PropertyChanged-=OnShapeChanged;Changed=null;
        _area.Changed-=ProjectVisibility;_detection.Changed-=ProjectVisibility;_inherit.Changed-=ProjectVisibility;_mask.Changed-=ProjectVisibility;
        _detectionSource=null;_maskSource=null;
        Children.Clear();_rectangleParameters.Children.Clear();_circleParameters.Children.Clear();
        foreach(var editor in _inheritEditors.Concat(InheritanceEditors()).Distinct())editor.Dispose();foreach(var radio in _radios)radio.Dispose();foreach(var state in _states)state.Dispose();
    }
}
