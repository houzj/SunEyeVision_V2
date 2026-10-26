"""Apply reviewed image-view host boundaries, without vendor module/IO services.

Requires the integrated source. Original source hashes and protected host output
hashes are checked before changing files. This is not runtime acceptance.
"""
from pathlib import Path
import hashlib,json,re

ROOT=Path(__file__).resolve().parents[2]
MANIFEST=ROOT/'src/Plugin.Wpf/Controls/Native/Display/display-migration-manifest.json'
RECEIPT=ROOT/'src/Plugin.Wpf/Controls/Native/Display/visual-host-adaptations.json'

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def adapt(name,text):
    assert 'using VMControls.Interface;' in text
    text=text.replace('using VMControls.Interface;','using SunEyeVision.Plugin.Wpf.Native.Common;')
    if name=='ImageViewBox.xaml.cs':
        assert text.count('using Apps.Data;')==1 and text.count('using Apps.Data.Common;')==1
        text=text.replace('using Apps.Data;\n','').replace('using Apps.Data.Common;\n','')
        assert text.count(', IImageViewBox,')==1
        text=text.replace(', IImageViewBox,',',').replace('IImageViewAddition','IImageViewPresentation')
        assignment='ImageBoxAddition = Ioc.HotGet<IAdditionService>()?.GetImageViewAdditionInstance(this);'
        assert text.count(assignment)==1
        text=text.replace(assignment,'AttachPresentationHost();')
        text=text.replace('public IImageViewPresentation ImageBoxAddition { get; set; }',
                          'public IImageViewPresentation? ImageBoxAddition { get; private set; }')
        assert 'ImageDataSource is ImageData imageData' in text
        text=text.replace('ImageDataSource is ImageData imageData','ImageDataSource is IRangeImageData imageData')
        raw='ImageSaveOpenCvHelper.SaveRawImageSyn(saveFileDialog.FileName, imageDataSource);'
        rendered='ImageSaveOpenCvHelper.SaveRoiImageSyn(saveFileDialog.FileName, (ImageData)imageDataSource, shapesSource, null);'
        assert raw in text and rendered in text
        text=text.replace(raw,'NativeImageExport.SaveRaw(saveFileDialog.FileName, imageDataSource);')
        text=text.replace(rendered,'NativeImageExport.SaveRendered(saveFileDialog.FileName, imageDataSource, shapesSource, this);')
        assert not re.search(r'\bImageData\b|ImageSaveOpenCvHelper',text)
    else:
        old='\t\tif (Ioc.HotGet<IAdditionService>() != null)\n\t\t{\n\t\t\tIoc.Get<IAdditionService>()?.AdditionResource(container.GetParent<IImageViewBox>(), ResourceType.imagebox);'
        assert old in text
        profile='true' if name=='AdditionWTemplateSelector.cs' else 'false'
        new='\t\tvar view = container.GetParent<ImageViewBox>();\n\t\tif (view?.ExtensionProvider is {} provider)\n\t\t{\n\t\t\tprovider.PrepareResources(view, profile: '+profile+');'
        text=text.replace(old,new)
    assert 'Ioc.' not in text and 'IAdditionService' not in text
    return text

def main():
    manifest=json.loads(MANIFEST.read_text(encoding='utf-8'));receipt=json.loads(RECEIPT.read_text(encoding='utf-8'))
    protected={e['target']:e for e in receipt['files']};changes=[]
    for name in ('ImageViewBox.xaml.cs','AdditionTemplateSelector.cs','AdditionWTemplateSelector.cs'):
        path=ROOT/'src/Plugin.Wpf/Controls/Native/Display/VMBaseControls/VMBaseControls'/name
        key=path.relative_to(ROOT).as_posix();entry=next(e for e in manifest['files'] if e['target']==key)
        source=Path(entry['source']);assert sha(source)==entry['sourceSha256'].lower()
        if key in protected:
            assert sha(path)==protected[key]['adapted_sha256'], 'Review edited host output before reapplying: '+key
            continue
        changes.append((path,adapt(name,path.read_text(encoding='utf-8')),entry,source))
    for path,text,entry,source in changes:
        path.write_text(text,encoding='utf-8',newline='\r\n')
        entry['targetSha256']=sha(path);entry['gitNormalizedTargetSha256']=hashlib.sha256(text.encode()).hexdigest()
        entry['adaptations'].append('Explicit optional visual provider replaces vendor Ioc/module/IO; synchronous local WPF export; see visual-host-adaptations.json.')
        receipt['files'].append({'target':entry['target'],'original_source':str(source),'original_source_sha256':sha(source),'adapted_sha256':sha(path)})
    if changes:
        receipt['scope']='原颜色、ROI诊断与图像视图宿主适配；不是运行或视觉验收'
        receipt['changes'].extend([
            'ImageViewBox移除空标记接口及厂商Ioc；可选ExtensionProvider创建有限视觉会话，卸载/更换provider先清引用再Dispose，原缩放/绘图回调保留。未导入模块、IO或旧变量模型。',
            '完整原IImageViewBoxWAddition纯绘图接口按原件迁入；IImageViewPresentation承接视图消费成员，不宣称原3D模块/IO服务整体迁移完成。',
            'IRangeImageData承接位深读取；原图/带图形保存用本地WPF编码和原ShapeItemsPage。先复制借入缓冲，16位范围原图保留Gray16；WPF栅格与JPEG质量95为本方适配，不宣称等同VM/OpenCV像素结果。'])
        MANIFEST.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
        RECEIPT.write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print(json.dumps({'host_files_changed':len(changes),'runtime_verified':False}))

if __name__=='__main__':main()
