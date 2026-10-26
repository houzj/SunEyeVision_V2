using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.Wpf.Fields;
using SettingValue=SunEyeVision.Plugin.Contracts.ParameterSetting;

namespace SunEyeVision.UI.Services.Editing;

public sealed partial class DeclaredToolConfigurationControl
{
    private readonly List<(DeclaredFieldEditor Editor,Func<bool> Active)> _inheritedRegionEditors=new();
    private readonly List<Func<string?>> _inheritedRegionChecks=new();

    // Each presentation branch writes the existing persisted ROI model. No second coordinate store.
    private void BuildInheritancePanels(RegionFieldGroup group,RoiCollection collection,string title)
    {
        Add(RegionFieldMode.InheritedRectangle,ShapeType.RotatedRectangle,false);
        Add(RegionFieldMode.InheritedCircle,ShapeType.Circle,false);
        Add(RegionFieldMode.InheritedRectangleParameters,ShapeType.RotatedRectangle,true);
        Add(RegionFieldMode.InheritedCircleParameters,ShapeType.Circle,true);
        bool Active(RegionFieldMode mode)=>_accessors.Single(field=>field.Port.Id==group.ModeFieldId).Read()?.ToString()==mode.ToString();
        void Add(RegionFieldMode mode,ShapeType shape,bool composed)
        {
            var panel=new StackPanel{Margin=new Thickness(0,4,0,8),Tag=group.Role+":"+mode};
            _fields!.AddAfterField(group.ModeFieldId,panel,new(group.ModeFieldId,mode.ToString()));
            bool Matches(Roi roi)=>composed?roi.IsComposed&&roi.DeclaredShapeType==shape:roi.IsSubscribed&&roi.InheritedShapeType==shape;
            var existing=collection.Rois.Where(Matches).ToArray();
            _inheritedRegionChecks.Add(()=>Active(mode)&&!collection.Rois.Any(roi=>roi.IsEnabled&&Matches(roi))?title+"：请设置当前继承方式的来源。":null);
            if(existing.Length==0)AddSlot(null);else foreach(var roi in existing)AddSlot(roi);
            void AddSlot(Roi? item)
            {
            var content=new StackPanel();panel.Children.Add(content);
            if(item is not null)
            {
                var enable=new CheckBox{Content=item.Name,IsChecked=item.IsEnabled,Margin=new Thickness(0,4,0,4)};
                enable.Click+=(_,_)=>{if(RegionScopeCurrent()){item.IsEnabled=enable.IsChecked==true;RefreshRegionScene();}};content.Children.Add(enable);
            }
            Roi Ensure()
            {
                if(!RegionScopeCurrent())throw new InvalidOperationException("当前编辑范围已失效。");
                if(item is not null)return item;
                item=composed?Roi.CreateComposed(title+"参数",shape):Roi.CreateSubscribed(title+"继承","","");
                if(!composed)item.InheritedShapeType=shape;
                collection.Rois.Add(item);return item;
            }
            _inheritedRegionChecks.Add(()=>
            {
                if(!Active(mode))return null;
                if(item is not {IsEnabled:true})return null;
                if(!composed)return StorageError(item.Binding)??(item.Binding?.BindingType==BindingType.Binding?null:title+"：请选择区域来源。");
                var missing=RoiComposeResolver.RequiredParameters(shape).Where(key=>item.ParameterBindings?.ContainsKey(key)!=true).ToArray();
                return missing.Length==0?RoiComposeResolver.RequiredParameters(shape).Select(key=>StorageError(item.ParameterBindings![key])).FirstOrDefault(error=>error is not null):title+"：请设置"+string.Join("、",missing.Select(ParameterLabel))+"。";
            });
            if(!composed)
            {
                var port=new Port(group.Role+":"+mode,TypeRef.SequenceOf(RoiShapeValueAdapter.Declaration),EditModes:FieldEditModes.Reference);
                var initial=Read(item?.Binding);
                var editor=new DeclaredFieldEditor(port,"区域",initial,setting=>
                {
                    if(setting is not ReferenceSetting reference)throw new ArgumentException("整区域必须选择声明来源。");
                    var roi=Ensure();roi.Binding=ToSetting(roi.Name,reference.Reference);RefreshRegionScene();
                },_=>SelectReference(port,reference=>
                {
                    var target=new RoiBindingInputs.Slot(port.Id,item??new Roi(),null,null).Target(reference);
                    var element=target.Type.Element!;
                    var expected=shape==ShapeType.Circle?GeometryKind.Circle:GeometryKind.OrientedRectangle;
                    if(element.Shape==ValueShape.Geometry&&element.Geometry!=expected)throw new ArgumentException("来源几何与所选继承类型不一致。");
                    return target;
                }),formatReference:FormatReference);
                content.Children.Add(editor);_inheritedRegionEditors.Add((editor,()=>Active(mode)&&item?.IsEnabled!=false));return;
            }
            foreach(var key in RoiComposeResolver.RequiredParameters(shape))
            {
                var port=new Port(group.Role+":"+mode+":"+key,TypeRef.ScalarOf(ScalarKind.Float64),EditModes:FieldEditModes.Constant|FieldEditModes.Reference);
                var initial=Read(item?.ParameterBindings?.GetValueOrDefault(key));
                var editor=new DeclaredFieldEditor(port,ParameterLabel(key),initial,setting=>
                {
                    ParamSetting value;
                    if(setting is ConstantSetting constant)
                    {
                        var number=Convert.ToDouble(constant.Value,System.Globalization.CultureInfo.InvariantCulture);
                        if(!double.IsFinite(number)||key is "Width" or "Height" or "Radius"&&number<=0)throw new ArgumentException("区域参数须为有限数字，尺寸须大于0。");
                        value=ParamSetting.CreateConstant(key,number);
                    }
                    else if(setting is ReferenceSetting reference)value=ToSetting(key,reference.Reference);
                    else throw new ArgumentException("请设置必需的区域参数。");
                    var roi=Ensure();roi.ParameterBindings??=new();roi.ParameterBindings[key]=value;RefreshRegionScene();
                },_=>SelectReference(port,_=>new Port(port.Id,TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Float64)))),
                new ConstantSetting(key is "Width" or "Height" or "Radius"?1d:0d),FormatReference);
                content.Children.Add(editor);_inheritedRegionEditors.Add((editor,()=>Active(mode)&&item?.IsEnabled!=false));
            }
            }
        }
        static SettingValue Read(ParamSetting? stored)
        {
            try{return ReadChecked(stored);}
            catch(Exception error) when(error is JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException or InvalidCastException){return new UnsetSetting();}
        }
        static string? StorageError(ParamSetting? stored)
        {
            try{_=ReadChecked(stored);return null;}
            catch(Exception error) when(error is JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException or InvalidCastException)
            {return "区域来源或参数存储无效，请重新设置："+error.Message;}
        }
        static SettingValue ReadChecked(ParamSetting? stored)
        {
            if(stored is null)return new UnsetSetting();
            if(stored.ReferenceExpression is { } expression)return new ReferenceSetting(BindingReferenceSerializer.Deserialize(expression));
            if(stored.BindingType==BindingType.Binding)throw new ArgumentException("旧名称来源需要重新选择正式声明。");
            var value=stored.ConstantValue is JsonElement number&&number.ValueKind==JsonValueKind.Number?number.GetDouble():stored.ConstantValue;
            if(value is not (double or float or int or long))throw new ArgumentException("数值参数必须为明确的数字常量。");
            var scalar=Convert.ToDouble(value,System.Globalization.CultureInfo.InvariantCulture);
            if(!double.IsFinite(scalar))throw new ArgumentException("数值参数必须有限。");
            return new ConstantSetting(scalar);
        }
    }
    private static string ParameterLabel(string key)=>key switch
    {"CenterX"=>"ROI中心点X","CenterY"=>"ROI中心点Y","Width"=>"ROI宽度","Height"=>"ROI高度","Angle"=>"ROI角度","Radius"=>"ROI半径",_=>key};
    private bool HasPendingInheritedRegions=>_inheritedRegionEditors.Any(part=>part.Active()&&part.Editor.HasInvalidText);
    private bool ValidateInheritedRegionEditors(out string error)
    {
        error=_inheritedRegionEditors.FirstOrDefault(part=>part.Active()&&part.Editor.HasInvalidText).Editor?.Error??"";
        if(error.Length>0)return false;
        error=_inheritedRegionChecks.Select(check=>check()).FirstOrDefault(message=>message is not null)??"";
        return error.Length==0;
    }
}
