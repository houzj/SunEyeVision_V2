using System.Windows;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.UI.Services.Images;

internal sealed partial class ResultWorkspacePresenter
{
    private static readonly byte[] OverlayAlpha=[191,127,63];
    private readonly string?[] _overlayKeys=new string?[OverlayAlpha.Length];
    private readonly NativeResultViewport.RegionLayer?[] _overlayLayers=new NativeResultViewport.RegionLayer?[OverlayAlpha.Length];
    private string? _backgroundKey;
    private long _overlayRequest;
    private (int Width,int Height) _overlaySamplingSize;
    private DispatcherOperation? _overlaySamplingRefresh;

    private void OnOverlayViewportChanged(object sender,object tag)
    {
        if(_disposed||_backgroundKey is null||_outputSource?.Key!=_backgroundKey||
            _overlaySamplingSize==_output.Image.Viewport.OverlaySamplingSize||_overlaySamplingRefresh is not null)return;
        _overlaySamplingRefresh=_shell.Dispatcher.BeginInvoke(DispatcherPriority.Render,new Action(()=>
        {
            _overlaySamplingRefresh=null;
            if(!_disposed&&_backgroundKey is not null&&_outputSource?.Key==_backgroundKey&&
                _overlaySamplingSize!=_output.Image.Viewport.OverlaySamplingSize)
                RefreshOverlayLayers();
        }));
    }

    private bool IsCheckedOutput(ResultWorkspaceImageSource item,ResultWorkspaceImageSource? selected)
        => _backgroundKey is null?ReferenceEquals(item,selected):
            item.Key==_backgroundKey||Array.IndexOf(_overlayKeys,item.Key)>=0;

    private bool CanToggleOverlay(object? parameter)
        =>parameter is ResultWorkspaceImageSource {Reference:not null} source&&OwnsChoice(source,false)
            &&_outputSource?.Reference is not null
            &&(_backgroundKey is not null||source.Key!=_outputSource?.Key);

    private void WarnOverlay(string message)
    {
        if(Window.GetWindow(_shell) is {IsVisible:true} owner)
            NativeMessageBox.ShowOwned(owner,AppLocalizationService.GetLang("Warning","警告"),message,MessageButtons.Normal,MessageIcons.Warning);
    }

    private void ToggleOverlay(object? parameter)
    {
        if(!CanToggleOverlay(parameter)||parameter is not ResultWorkspaceImageSource source)return;
        void Reject(string message){source.RefreshCheck();WarnOverlay(message);}
        if(_main.IsResultImageChoiceLocked)
        {Reject(AppLocalizationService.GetLang("Lang_PlzLoopStop","请等待流程执行停止后再进行操作"));return;}
        var slots=_overlayKeys.ToArray();
        if(source.Key==_backgroundKey)
        {
            if(_outputSource?.Key!=source.Key&&slots.Count(key=>key is not null)==1)
            {
                SaveOverlayChoice(_outputSource?.Key,null,slots);
                return;
            }
            var next=_plan!.Sources.FirstOrDefault(item=>item.HasImageValue&&Array.IndexOf(slots,item.Key)>=0);
            if(next is null){SaveOverlayChoice(_outputSource?.Key,null,slots);return;}
            slots[Array.IndexOf(slots,next.Key)]=null;
            SaveOverlayChoice(next.Key,next.Key,slots);
            return;
        }
        var slot=Array.IndexOf(slots,source.Key);
        if(slot>=0)
            slots[slot]=null;
        else
        {
            var free=Array.IndexOf(slots,null);
            if(free<0)
            {Reject(AppLocalizationService.GetLang("Lang_OverlyingImageLimitTip","图像叠加张数超限")+" : "+(slots.Length+1));return;}
            var background=_backgroundKey??_outputSource?.Key;
            if(_output.Image.CurrentFrame is null||!_plan!.Sources.Any(item=>item.Key==background&&item.HasImageValue))
            {Reject(AppLocalizationService.GetLang("Lang_BackgroundImageEmptyTip","背景图为空，不能进行叠加显示。"));return;}
            if(!source.HasImageValue)
            {Reject(AppLocalizationService.GetLang("Lang_CheckImageEmptyTip","图像数据为空，不支持叠加显示。"));return;}
            slots[free]=source.Key;
        }
        SaveOverlayChoice(_outputSource?.Key,_backgroundKey??_outputSource?.Key,slots);
    }

    private bool CanSwapBackground(object? parameter)
        => _backgroundKey is not null&&parameter is ResultWorkspaceImageSource {HasImageValue:true,Reference:not null} source
           &&_outputSource?.Reference is not null
           &&OwnsChoice(source,false)&&Array.IndexOf(_overlayKeys,source.Key)>=0
           &&_plan!.Sources.Any(item=>item.Key==_backgroundKey&&item.HasImageValue);

    private void SwapBackground(object? parameter)
    {
        if(!CanSwapBackground(parameter)||parameter is not ResultWorkspaceImageSource source||Window.GetWindow(_shell) is not {} owner)return;
        if(_main.IsResultImageChoiceLocked)
        {WarnOverlay(AppLocalizationService.GetLang("Lang_PlzLoopStop","请等待流程执行停止后再进行操作"));return;}
        if(NativeMessageBox.ShowOwned(owner,AppLocalizationService.GetLang("Lang_Sure","确认"),
            AppLocalizationService.GetLang("Lang_ChangeBackgroundImageTip","是否切换背景图像？"),
            MessageButtons.YesOrNo,MessageIcons.Question)!=SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.DialogResult.Yes
            ||_main.IsResultImageChoiceLocked||!CanSwapBackground(source))return;
        var slots=_overlayKeys.ToArray();
        slots[Array.IndexOf(slots,source.Key)]=null;
        slots[Array.IndexOf(slots,null)]=_backgroundKey;
        SaveOverlayChoice(source.Key,source.Key,slots);
    }

    private void SaveOverlayChoice(string? selected,string? background,string?[] slots)
    {
        if(_main.SelectedNode is not {} node||!ReferenceEquals(node,_selectionOwner))return;
        _main.SetFormalDisplayOverlays(node,selected,
            background is not null&&slots.Any(key=>key is not null)?new ImageOverlaySelection(background,slots):null);
    }
    private void ReadOverlayChoice(ImageOverlaySelection? selection)
    {
        _backgroundKey=selection?.Background;
        for(var slot=0;slot<_overlayKeys.Length;slot++)_overlayKeys[slot]=selection?.Slots[slot];
    }
    /// <summary>当前应用的叠加勾选计划是否与节点声明一致：不一致说明选择计划已更替，必须照常重建。</summary>
    private bool OverlayChoiceMatches(ImageOverlaySelection? selection)
    {
        if(!string.Equals(_backgroundKey,selection?.Background,StringComparison.Ordinal))return false;
        for(var slot=0;slot<_overlayKeys.Length;slot++)
            if(!string.Equals(_overlayKeys[slot],slot<(selection?.Slots.Count??0)?selection!.Slots[slot]:null,StringComparison.Ordinal))return false;
        return true;
    }

    private void RefreshOverlayLayers()
    {
        var request=++_overlayRequest;
        _overlaySamplingSize=_output.Image.Viewport.OverlaySamplingSize;
        var plan=_plan;
        var selected=_outputSource;
        bool Current()=>!_disposed&&request==_overlayRequest&&ReferenceEquals(plan,_plan)&&ReferenceEquals(selected,_outputSource);
        for(var slot=0;slot<_overlayLayers.Length;slot++)
        {
            if(!Current())return;
            var layer=_overlayLayers[slot];
            _overlayLayers[slot]=null;
            layer?.Dispose();
            if(!Current())return;
            var key=_overlayKeys[slot];
            if(plan is null||selected is null||selected.Key!=_backgroundKey||key is null)continue;
            var source=plan.Sources.FirstOrDefault(item=>item.Key==key&&item.HasImageValue);
            if(source is null)continue;
            layer=_output.Image.Viewport.CreateRegionLayer(100+slot);
            _overlayLayers[slot]=layer;
            try
            {
                if(_output.Image.TryPresentOverlay(source,layer,OverlayAlpha[slot],request,Current))continue;
            }
            catch(Exception error) when(error is ArgumentException or InvalidOperationException)
            {
                if(Current())_main.AddLog("叠加图像未显示："+source.DisplayName+"，"+error.Message);
            }
            if(!Current())return;
            _overlayLayers[slot]=null;
            layer.Dispose();
        }
    }

    private void ResetOverlayChoice()
    {
        ++_overlayRequest;
        _overlaySamplingRefresh?.Abort();
        _overlaySamplingRefresh=null;
        _backgroundKey=null;
        Array.Clear(_overlayKeys);
        var retired=_overlayLayers.ToArray();
        Array.Clear(_overlayLayers);
        Run(retired.OfType<NativeResultViewport.RegionLayer>().Select(layer=>(Action)layer.Dispose).ToArray());
    }
}
