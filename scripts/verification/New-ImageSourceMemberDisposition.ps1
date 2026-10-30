param()
$ErrorActionPreference='Stop'
$workspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$inventoryPath=Join-Path $workspace 'docs/collaboration/evidence/S1_SOURCE_COMPONENT_INVENTORY_20261030.json'
$inventory=Get-Content -LiteralPath $inventoryPath -Raw | ConvertFrom-Json
$propertyReview=Get-Content -LiteralPath (Join-Path $workspace 'docs/collaboration/evidence/S1_PROPERTY_IMPLEMENTATION_REVIEW_20261030.json') -Raw | ConvertFrom-Json
$strip='src/Plugin.Wpf/Controls/Native/Composition/ImageSources/ImageSourceStrip.xaml.cs'
$session='src/UI/Services/Images/ImageSourceStripSession.cs'
$import='src/UI/Services/Images/ImageSourceStripSession.FileImport.cs'
$model='src/UI/Shared/ImageInputSource.cs'
$viewHost='src/UI/Shell/Controls/Shell/ImageResultControl.xaml.cs'
$execution='src/UI/Services/Workflow/WorkflowExecutionManager.LocalImages.cs'
$groups=@{}
function AddMemberGroup([string]$owner,[string]$names,[string]$decision,[string[]]$files,[string]$note){
    foreach($name in $names.Split('|')){
        $id=$owner+'.'+$name
        if($groups.ContainsKey($id)){throw "重复成员分组：$id"}
        $groups[$id]=[ordered]@{decision=$decision;productFiles=$files;note=$note}
    }
}
AddMemberGroup 'LocalImageListView.TempClass' 'displayName|val|PropertyChanged|OnPropertyChanged' 'immutable-mode-presentation-adaptation' @($strip) '原setter本身不通知；当前RunModeItem表达模式值和显示名，原通知方法在已核五文件无消费。语言查询/重建另核，不为私有字段新造类型。'
AddMemberGroup 'LocalImageListView' 'list|cbx|IsExpandedProperty|IsSelectModeProperty|RootProperty|imageModes|_AutoPlay|_CurrentImagePath|PropertyChanged|OnPropertyChanged|LocalImageListView|OnApplyTemplate|OnRootChanged|cbxMode_SelectionChanged' 'existing-host-composition-adaptation' @($strip,$session,$viewHost) '原模板查找、根参数代理与事件合入实际UserControl/Session；Attach/Detach、版本及当前root/source门禁承载。控件不持有SDK全局服务，模式/开关正式接纳复用已有编辑链。'
AddMemberGroup 'LocalImageListView' '_Interval' 'no-shared-visual-consumer' @($strip) '原Interval代理没有共享XAML显示入口；不复制旧参数代理API，不增加不可见间隔设置。已记录属性及外部消费者边界。'
AddMemberGroup 'LocalImageListView' 'OnSelectedItemChanged' 'bounded-unconsumed-declaration' @($strip) '原本文件仅字段声明；不凭名称新增用户交互或保留旧API兼容。外部动态调用未证不作全域断言。'
AddMemberGroup 'LocalImageListView' 'OnLanguageChanged' 'existing-language-query-and-refresh-adaptation' @($strip,'src/Plugin.Wpf/Controls/Native/Common/Localization/AppLocalizationService.cs') '原件重建模式名称；复用现有语言查询及实际模板/根刷新边界，不新增全局动态语言服务。'
AddMemberGroup 'LocalImageListView' 'ClearParamListView|listViewUserDefinedKeyParam_SelectionChanged|ScrollViewer_PreviewMouseWheel' 'original-empty-body-no-behavior-to-port' @($strip) '原件方法体为空；不把空方法变成三个开发任务，也不复制旧签名作为兼容入口。'
AddMemberGroup 'ImageListPanel' 'ImageListPanel|ChangeShadowDataSource' 'original-shadow-host-binding-adaptation' @($strip,$viewHost) '实际组合保留最终子遮罩；根IsRunning绑定和释放在RefreshSourceSession/ReleaseSourceSession，单次状态已纳入真实生产回归。'
AddMemberGroup 'ImageListPanel' '_CreateDelegate' 'generated-xaml-infrastructure' @($strip) '原生成代理由当前XAML编译及InitializeComponent承载，不手工复制生成兼容方法。'
AddMemberGroup 'ImageListPanelBase' 'TargetValueProperty|CurSelectedModuleProperty|CurSelectedIndexProperty|DoFreshImageListProperty|IsExpandedProperty|IsSelectModeProperty|OwnerModule|root|TargetValueChanged|CurSelectedModuleChanged|DoFreshImageListChanged|GetVMModule|GetRoot|ImagePanel_DataContextChanged|ChangeShadowDataSource' 'formal-identity-and-projection-adaptation' @($strip,$session,$model,$viewHost) '按功能复用SessionChanged、源投影通知、当前root/source实例与EditVersion；生命周期先退旧后接新并防重入，不复制VM全局业务模型。'
AddMemberGroup 'ImageListPanelBase' 'ImageListPanelBase|ImageListPanel_Loaded|ImageListPanel_Unloaded|refreshImageThread|refreshImageThreadAlive|mutexrefreshImageThread|dataNumForReceiveStore|dataNumForSendStore|maxStoreLen|forReceiveStore|forSendStore|UpdateImageListSource|LoadThumbnailImage' 'owned-cache-and-cancellation-adaptation' @($strip,$session,'src/UI/Services/Thumbnail/LocalImageAssetCache.cs') '原可见范围队列/线程改由现有独立缓存等待者与CTS承载；可见范围退出取消本视图请求，冻结位图，不释放别的视图资源。实际缓存/退出/重入回归与缩略刷新焦点诊断分别记录。'
AddMemberGroup 'ImageListPanelBase' 'ImageItemWidth|curScrollOffset|curActualWidth|scrollViewer|lst_ScrollChanged' 'original-horizontal-viewport-adaptation' @($strip,'src/Plugin.Wpf/Controls/Native/Composition/ImageSources/ImageSourceStrip.xaml') '原76单元、72x52内容和横向回收模板保留；QueueViewport按实际可见容器求交，既有1051图真实宿主回归覆盖滚动及释放，不为字段再造状态。'
AddMemberGroup 'ImageListPanelBase' 'EXECUTE_TYPE|OnUpdateExcuteType|UpdateExcuteType|OnExcuteTypeParamChanged|CheckBox_Click|Grid_MouseDown|ChangedExecutePic|GetModuleSelectMode|needSetSelectImage' 'formal-run-selection-adaptation' @($strip,$session,$model) '复用正式运行勾选集合、原Ctrl切换/Shift闭区间、全部/选中回读及拒绝恢复；原PropertyManager广播由同一正式来源通知替代，不把UI变成调度器。'
AddMemberGroup 'ImageListPanelBase' 'CurImageIndex|needSelectionTrigger|needSelectImagePath|curTime|IsTimeRefreshView|GetModuleSel|lst_SelectionChanged|OnPreduceResultCallBackArrived|DoRefreshLocalImage' 'navigation-readback-and-version-adaptation' @($strip,$session,$model,$execution,'src/UI/Shell/Windows/MainWindowImageSourceEditing.cs') '程序回填不执行、用户选择先接纳再过忙门；原50/100ms单槽回填不是latest-only调度证据。现有执行快照和版本门禁防旧结果覆盖新选择；订阅目录的RefreshImage后端分支另列未实现，不冒称全域等价。'
AddMemberGroup 'ImageListPanelBase' 'isReturnFirstForce|FirstImage_Click' 'original-first-reset-transient-state' @($strip,$model,$execution) '全部/选中和AutoPlay四组合保留；首图不直接Run；真实同索引回写复原高亮，缩略刷新不误清。'
AddMemberGroup 'ImageListPanelBase' 'RefreshImageList|InterfaceUpdateImageList|NotifyPropertyChanged|PropertyChanged' 'observable-list-and-source-policy-adaptation' @($strip,$session,$model) '已有正式路径/勾选/游标投影与通知承载；不再由VM参数字符串重新造真相源。普通图像导入/删除/重开链已验证；订阅/远程/3D分支不计完成。'
AddMemberGroup 'ImageListPanelBase' 'BtnDelete_Click|BtnImageClear_Click' 'original-local-structural-edit-adaptation' @($strip,$session,$model,$execution) '普通唯一路径按原追加/删除原生证据：删前减一、删后不变、删当前无选择；原子编辑、历史、资源失效及下一次实际读取复用已验证链。'
AddMemberGroup 'ImageListPanelBase' 'Grid_MouseEnter|Grid_MouseLeave' 'original-item-delete-affordance-retained' @($strip) '悬停显示条目删除按钮，离开隐藏；只读/导入门禁不误暴露可用编辑。实际WPF外观仍待人工。'
AddMemberGroup 'ImageListPanelBase' 'BtnImageAdd_Click|LocalImage_Click|DoAddImages|AddImage|AddImageLocal|AddImageToServer|AddImageByDir|AddImageToList|BtnImageAddFolder_Click|LocalImageFolder_Click' 'original-local-import-with-explicit-platform-boundary' @($strip,$import,$model,'src/UI/Services/Thumbnail/ImageFileImport.cs') '仅本地平台，原手动目录一层展开和排序、去重、追加不激活、不执行；复用枚举冻结/取消/一次接纳/历史与异常链，不复制SDK字符串命令。远程上传支路另列未实现。'
AddMemberGroup 'ImageListPanelBase' 'JudgeDrage|lst_DragEnter|lst_DragOver|lst_Drop' 'original-file-drop-acceptance-retained' @($strip,$import) '原六扩展名、文件存在性逐项核验；非法混合整批拒绝。DragEnter/Over/Drop同一规则，正式导入一次接纳、零执行。路由事件测试不是物理OS拖放。'
AddMemberGroup 'ImageListPanelBase' 'MenuItem_Click|ILFree|ILCreateFromPathW|SHOpenFolderAndSelectItems' 'original-windows-folder-action-retained' @($strip,$session) '沿原PIDL定位文件并在finally释放；当前会话/路径校验沿现有入口；目录进程参数安全传入。Explorer实机体验留最终人工，不把静态代码当物理操作。'
AddMemberGroup 'ImageListPanelBase' 'ptr|isWindowsPicture|LinuxImage_Click|AddImageToLinux|LinuxImageFolder_Click' 'unimplemented-remote-deployment-branch' @($import) '原ptr/本地远程标志服务SDK远程上传；当前无正式远程会话，未实现/不暴露远程菜单，不移植业务全局句柄或伪造成功。'
AddMemberGroup 'ImageListPanelBase' '_IsSubscribeFolderMode|needReflashImages|OnSubscribeFolderModeChanged|VmSolution_OnReportModuleData' 'unimplemented-folder-subscription-branch' @('tools/SunEyeVision.ImageSource/Runtime/ImageSourceFields.cs','src/Workflow/ImageSourcePolicy.cs') '原特定模块报告类型驱动订阅目录刷新和模式变化；当前正式工具配置无订阅目录能力。保留明确依赖，不把手动添加目录冒充自动订阅。'
AddMemberGroup 'ImageListPanelBase' 'BtnImageAddRpFolder_Click' 'disabled-rp-3d-placeholder' @($strip,'src/Plugin.Wpf/Controls/Native/Composition/ImageSources/ImageSourceStrip.xaml') '无正式RP/3D导入基础，入口禁用折叠；不计功能完成，不展开算法或插件合同重写。'
AddMemberGroup 'ImageListPanelBase' 'GetImageEntitybyPath|GetFiles|DoEvents|IsChildVisibleInParent|PATH_MAX' 'bounded-helper-not-a-separate-feature' @($strip,$import,$model) '原件有界搜索未见这些辅助函数调用或常量消费；相关实际路径/目录/视口由现有正式链承载。保留来源与外部未知，不复制无用途方法或增加兼容入口。'
$propertyById=@{};foreach($row in $propertyReview.properties){$propertyById[$row.originalId]=$row}
$unmapped=[Collections.Generic.List[string]]::new()
$rows=@(foreach($member in $inventory.originalDeclaredMembers){
    $id=$member.type+'.'+$member.member
    if($member.kind -eq 'PropertyDeclarationSyntax'){
        $p=$propertyById[$id];if(!$p){throw "属性未核收：$id"}
        $decision=$p.decision;$files=@();if($p.productFile){$files=@($p.productFile)};$note=$p.implementation
    }elseif($groups.ContainsKey($id)){
        $group=$groups[$id];$decision=$group.decision;$files=$group.productFiles;$note=$group.note
    }else{$unmapped.Add($id);$decision='unreviewed';$files=@();$note='不得以统计补齐此条；需要真实行为判断。'}
    $products=@(foreach($file in $files){$path=Join-Path $workspace $file;if(!(Test-Path -LiteralPath $path)){throw "实现路径不存在：$file"};[ordered]@{path=$file;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}})
    [ordered]@{id=$id;originalFile=$member.file;originalLine=$member.line;kind=$member.kind;originalSyntaxSha256=$member.syntaxSha256;decision=$decision;product=$products;note=$note;acceptance='classification-is-not-runtime-equivalence-or-stage-closure'}
})
[ordered]@{scope='五文件143声明完整职责/适配/缺口处置表，内部声明不是独立开发任务；不把声明分类冒充原件运行等价或S1封版';sourceInventorySha256=(Get-FileHash -LiteralPath $inventoryPath -Algorithm SHA256).Hash;members=$rows;unmapped=@($unmapped);additionalDependency='ImageSourceThumbnail另核原ImageView全部模板与方法；两个鼠标处理体为空，像素转换沿现有冻结ImageSource必要适配。正式ContentToggleSwitch另沿总监e83b066d完整闭包接入。'} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $workspace 'docs/collaboration/evidence/S1_MEMBER_DISPOSITION_20261030.json') -Encoding utf8
'声明总数：'+$rows.Count
'未分配数：'+$unmapped.Count
$unmapped