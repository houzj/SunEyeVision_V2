# Read CLI metadata only. No Assembly.Load, target execution or native initialization.
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$root='D:/Program Files/VisionMaster4.4.0/Applications'
$specs=@(
    @{path='PublicFile/x64/VM.PlatformSDKCS.dll';types=@('ModuleManager','ServerSDKManager','IMVS_MODULE_IO_INFO','IMVS_MODULE_IO_INFO_LIST','IMVS_MODULE_ID_LIST','IMVS_MODULE_CONNECT_INFO','IMVS_MODULE_CONNECT_INFO_LIST')},
    @{path='myLibs/VM.Core.dll';types=@('VmModule','VmIO')},
    @{path='myLibs/Apps.XmlParser.dll';types=@('VmModuleExtern','Holder')})
$records=@()
foreach($spec in $specs){
    $path=Join-Path $root $spec.path
    $stream=[IO.File]::OpenRead($path)
    $pe=[System.Reflection.PortableExecutable.PEReader]::new($stream)
    try{
        $reader=[System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        $module=$reader.GetModuleDefinition();$types=@()
        foreach($handle in $reader.TypeDefinitions){
            $definition=$reader.GetTypeDefinition($handle);$name=$reader.GetString($definition.Name)
            if($name -notin $spec.types){continue}
            $layout=$definition.GetLayout();$fields=@();$methods=@()
            foreach($fieldHandle in $definition.GetFields()){
                $field=$reader.GetFieldDefinition($fieldHandle)
                $fields+=@{name=$reader.GetString($field.Name);attributes=$field.Attributes.ToString();
                    signature=[Convert]::ToHexString($reader.GetBlobBytes($field.Signature));
                    marshal=[Convert]::ToHexString($reader.GetBlobBytes($field.GetMarshallingDescriptor()))}
            }
            foreach($methodHandle in $definition.GetMethods()){
                $method=$reader.GetMethodDefinition($methodHandle);$blob=$reader.GetBlobReader($method.Signature)
                $header=$blob.ReadSignatureHeader();if($header.IsGeneric){$null=$blob.ReadCompressedInteger()}
                $count=$blob.ReadCompressedInteger()
                $methods+=@{name=$reader.GetString($method.Name);attributes=$method.Attributes.ToString();parameterCount=$count;
                    signature=[Convert]::ToHexString($reader.GetBlobBytes($method.Signature));
                    parameterNames=@($method.GetParameters()|ForEach-Object {$parameter=$reader.GetParameter($_);@{sequence=$parameter.SequenceNumber;name=$reader.GetString($parameter.Name)}})}
            }
            $types+=@{name=$name;namespace=$reader.GetString($definition.Namespace);attributes=$definition.Attributes.ToString();
                packing=$layout.PackingSize;size=$layout.Size;fields=$fields;methods=$methods}
        }
        if($types.Count -ne $spec.types.Count){throw ('目标类型缺失或重名：'+$path)}
        $records+=@{path=$path;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash;mvid=$reader.GetGuid($module.Mvid).ToString();types=$types}
    }finally{$pe.Dispose();$stream.Dispose()}
}
$checks=@(
    @('ModuleManager','GetModuOutputIOInfo',1),@('ModuleManager','GetMultiModuleConnect',1),
    @('ServerSDKManager','get_serverSDKManager',0),@('VmModule','GetPreModules',0),@('VmModule','get_Outputs',0),
    @('VmIO','get_Children',0),@('VmIO','get_TypeName',0),@('VmIO','get_CanSubscribeA',0),@('VmIO','get_Visible',0),@('VmIO','get_IsPrivate',0),
    @('VmIO','get_Name',0),@('VmIO','get_Root',0),@('VmIO','get_IsLeafNode',0),@('VmIO','get_CustomVisibleA',0),@('VmIO','get_VisibleInResultTree',0),
    @('Holder','get_EnumEntrysA',0),@('Holder','get_DisplayNameA',0),@('Holder','get_HolderTarget',0),@('VmModuleExtern','GetFrontFilterTree',11))
foreach($check in $checks){
    $type=@($records.types|Where-Object {$_.name -eq $check[0]})
    $matches=@($type.methods|Where-Object {$_.name -eq $check[1] -and $_.parameterCount -eq $check[2] -and $_.attributes -match 'Public'})
    if($matches.Count -ne 1){throw ('反射入口声明不唯一或缺失：'+($check -join '/'))}
}
$requiredFields=@(
    @('ServerSDKManager','mModuleManager'),@('IMVS_MODULE_IO_INFO_LIST','nIoClass'),@('IMVS_MODULE_IO_INFO_LIST','nNum'),@('IMVS_MODULE_IO_INFO_LIST','pstModuIoInfo'),
    @('IMVS_MODULE_IO_INFO','nDataType'),@('IMVS_MODULE_IO_INFO','nNum'),@('IMVS_MODULE_IO_INFO','pstCombInfo'),
    @('IMVS_MODULE_IO_INFO','szParamName','061D05','1E8080'),@('IMVS_MODULE_IO_INFO','szParamType','061D05','1E8080'),@('IMVS_MODULE_IO_INFO','szExtraData','061D05','1E8200'),
    @('IMVS_MODULE_ID_LIST','nNum'),@('IMVS_MODULE_ID_LIST','anModuleId','061D09','1E881008'),@('IMVS_MODULE_ID_LIST','nReserved','061D09','1E0408'),
    @('IMVS_MODULE_CONNECT_INFO_LIST','nNum'),@('IMVS_MODULE_CONNECT_INFO_LIST','pstModuleConnectInfo'),
    @('IMVS_MODULE_CONNECT_INFO','nModuleId'),@('IMVS_MODULE_CONNECT_INFO','nNextModuNum'),@('IMVS_MODULE_CONNECT_INFO','anNextModuleId','061D09','1E8810'),
    @('IMVS_MODULE_CONNECT_INFO','nPreModuNum'),@('IMVS_MODULE_CONNECT_INFO','anPreModuleId','061D09','1E8810'))
foreach($check in $requiredFields){
    $type=@($records.types|Where-Object {$_.name -eq $check[0]})
    $field=@($type.fields|Where-Object {$_.name -eq $check[1] -and $_.attributes -match 'Public'})
    if($field.Count -ne 1){throw ('原SDK字段声明缺失：'+($check -join '/'))}
    if($check.Count -eq 4 -and ($field[0].signature -ne $check[2] -or $field[0].marshal -ne $check[3])){throw ('原SDK字段类型/固定数组Marshal形状变化：'+($check -join '/'))}
}
$audit=@{status='静态CLI元数据核对，不加载或执行目标';artifactReader=[System.Reflection.Metadata.MetadataReader].Assembly.Location;
    artifacts=$records;requiredPublicMethods=$checks;passedMethods=$checks.Count;requiredPublicFields=$requiredFields;passedFields=$requiredFields.Count;
    limits=@('签名保留原始blob，不冒称已解码全部类型或验证native ABI','静态存在不代表运行成功；运行窗口仍待确认')}
$output=Join-Path $workspace 'docs/collaboration/evidence/B08_PROBE_STATIC_METADATA_20261031.json'
$audit|ConvertTo-Json -Depth 15|Set-Content -LiteralPath $output -Encoding utf8
Write-Output ('通过：'+$checks.Count+'个必需公开方法及'+$requiredFields.Count+'个字段/Marshal形状；仅元数据，无SDK启动')
