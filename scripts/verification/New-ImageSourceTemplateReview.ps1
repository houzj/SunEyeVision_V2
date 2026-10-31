param([string]$EvidenceRoot='D:/MyWork/REA逆向测试')
$ErrorActionPreference='Stop'
$workspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$product=Join-Path $workspace 'src/Plugin.Wpf/Controls/Native/Composition/ImageSources/ImageSourceStrip.xaml'
[xml]$target=Get-Content -LiteralPath $product -Raw
$pairs=@(
    @{file='VMControls.WPF/VMControls.WPF/ImageListPanel.xaml';keys=@('VerticalScrollBarPageButton','MyButtonStyle','ScrollBarThumb','for_scrollbar','ListBoxStyle1','ScrollViewerControlTemplate1')},
    @{file='Apps.Style/checkboxstyle/checkboxstyle.xaml';keys=@('SmallCheckBoxStyle','EmptyCheckBoxFocusVisual','CheckRadioFocusVisual')},
    @{file='Apps.Style/buttonstyle/togglebuttonstyle.xaml';keys=@('DisplayHideBtnStyle')}
)
$reviews=@(foreach($pair in $pairs){
    $path=Join-Path $EvidenceRoot ('analysis/decompiled/'+$pair.file)
    [xml]$source=Get-Content -LiteralPath $path -Raw
    foreach($key in $pair.keys){
        $selector='//*[@*[local-name()="Key"]="'+$key+'"]'
                $productKey=if($key -in @('EmptyCheckBoxFocusVisual','CheckRadioFocusVisual')){'ImageSources.'+$key}else{$key}
        $left=$source.SelectSingleNode($selector)
        $right=$target.SelectSingleNode('//*[@*[local-name()="Key"]="'+$productKey+'"]')
        if(!$left -or !$right){throw "模板未定位：$key"}
        $aa=@($left.SelectNodes('descendant-or-self::*'));$bb=@($right.SelectNodes('descendant-or-self::*'))
        $differences=@(if($aa.Count -ne $bb.Count){
            [ordered]@{kind='node-count';original=$aa.Count;product=$bb.Count}
        }else{
            for($i=0;$i -lt $aa.Count;$i++){
                if($aa[$i].LocalName -ne $bb[$i].LocalName){[ordered]@{kind='element-type';node=$i;original=$aa[$i].LocalName;product=$bb[$i].LocalName}}
                foreach($attr in $aa[$i].Attributes){
                    $other=$bb[$i].GetAttribute($attr.LocalName,$attr.NamespaceURI)
                    if($attr.Value -ne $other){[ordered]@{kind='attribute';node=$i;element=$aa[$i].LocalName;attribute=$attr.Name;original=$attr.Value;product=$other}}
                }
                foreach($attr in $bb[$i].Attributes){
                    if(!$aa[$i].HasAttribute($attr.LocalName,$attr.NamespaceURI)){[ordered]@{kind='added-attribute';node=$i;element=$bb[$i].LocalName;attribute=$attr.Name;original=$null;product=$attr.Value}}
                }
            }
        })
        [ordered]@{key=$key;productKey=$productKey;originalFile=$pair.file;originalFileSha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash;originalNodeCount=$aa.Count;productNodeCount=$bb.Count;differences=$differences;review='raw-differences-not-runtime-equivalence';note='所有属性和元素类型差异保留。结构变化导致后续对齐不可靠，不能按节点数相同判通过。资源/语言/宿主适配须结合原主方案逐项审核。'}
    }
})
[ordered]@{scope='S1局部六套列表/滚动模板、运行勾选及两套焦点依赖、折叠模板静态逐属性核收；不包含所有传递字典或运行解析，不证明完整组件完成';productFileSha256=(Get-FileHash -LiteralPath $product -Algorithm SHA256).Hash;templates=$reviews} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $workspace 'docs/collaboration/evidence/S1_LOCAL_TEMPLATE_REVIEW_20261030.json') -Encoding utf8
$reviews | ForEach-Object {'{0}: {1}/{2} nodes; {3} raw differences' -f $_.key,$_.originalNodeCount,$_.productNodeCount,$_.differences.Count}