Set-StrictMode -Version Latest

function Get-ProductToolComponents {
    $root=Split-Path -Parent $PSScriptRoot
    [xml]$manifest=Get-Content -LiteralPath (Join-Path $root 'product/ToolComponents.props') -Raw
    $components=@(foreach($item in $manifest.SelectNodes('/Project/ItemGroup/ProductToolComponent')) {
        $relative=[string]$item.Include
        if([string]::IsNullOrWhiteSpace($relative) -or $relative.Contains('\') -or [IO.Path]::IsPathRooted($relative) -or @($relative.Split('/')|Where-Object {$_ -in '', '.', '..'}).Count){throw '产品组件工程路径无效'}
        $project=[IO.Path]::GetFullPath((Join-Path $root $relative))
        if(-not $project.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw '产品组件工程路径越界'}
        [xml]$source=Get-Content -LiteralPath $project -Raw
        $values=@{}
        foreach($field in 'ToolComponentId','ToolComponentToolId','ToolComponentRole','ToolComponentEntryType','ToolComponentCompanionToolId','AssemblyName','IsToolPlugin') {
            $nodes=@($source.SelectNodes('/Project/PropertyGroup/'+$field))
            if($nodes.Count -gt 1){throw "组件工程属性不唯一：$relative/$field"}
            $values[$field]=if($nodes.Count){$nodes[0].InnerText}else{''}
        }
        $companion=if($item.SelectSingleNode('CompanionToolId')){[string]$item.CompanionToolId}else{''}
        foreach($pair in @(@('ToolComponentId',[string]$item.ComponentId),@('ToolComponentToolId',[string]$item.ToolId),@('ToolComponentRole',[string]$item.Role),@('ToolComponentEntryType',[string]$item.EntryType),@('ToolComponentCompanionToolId',$companion))) {
            if([string]::IsNullOrWhiteSpace($pair[1]) -and $pair[0] -ne 'ToolComponentCompanionToolId'){throw '产品组件字段为空'}
            if($values[$pair[0]] -cne $pair[1]){throw "产品清单与工程声明不一致：$relative/$($pair[0])"}
        }
        if($values.IsToolPlugin -eq 'true' -or $values.AssemblyName -cne [string]$item.ComponentId){throw '产品组件不能使用旧SDK入口或不同程序集名'}
        [pscustomobject]@{project=$relative;componentId=[string]$item.ComponentId;toolId=[string]$item.ToolId;role=[string]$item.Role;entryType=[string]$item.EntryType;companionToolId=$companion;path=$values.AssemblyName+'.dll'}
    })
    if($components.Count -eq 0 -or @($components.componentId|Sort-Object -Unique).Count -ne $components.Count){throw '产品组件为空或ID重复'}
    foreach($group in @($components|Group-Object {$_.toolId+'|'+$_.role})){if($group.Count -ne 1){throw '产品组件角色重复'}}
    foreach($component in $components) {
        if($component.role -notin 'Runtime','Editor'){throw '产品组件角色无效'}
        if($component.role -eq 'Editor') {
            if($component.companionToolId -cne $component.toolId -or @($components|Where-Object {$_.toolId -ceq $component.toolId -and $_.role -eq 'Runtime'}).Count -ne 1){throw '编辑器缺少同工具Runtime'}
        } elseif($component.companionToolId){throw 'Runtime不能声明伴随工具'}
    }
    return $components
}

function Remove-RetiredProductToolOutputs {
    param([Parameter(Mandatory)][string]$AppPath, [string]$EntryAssemblyName = "SunEyeVision.UI")
    $root=Split-Path -Parent $PSScriptRoot
    $appRoot=[IO.Path]::TrimEndingDirectorySeparator((Resolve-Path -LiteralPath $AppPath).Path)
    # 只处理明确的构建输出根；绝不按输入拼接路径删除源码或任意目录。
    $allowed=@((Join-Path $root 'Release/SunEyeVision_v1.0.0/SunEyeVision'),(Join-Path $root 'dev/Debug'))
    $packRoot=[IO.Path]::GetFullPath((Join-Path $root 'artifacts/staging'))+[IO.Path]::DirectorySeparatorChar
    if(-not @($allowed|Where-Object {[IO.Path]::GetFullPath($_) -ieq $appRoot}).Count -and -not ($appRoot.StartsWith($packRoot,[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($appRoot) -eq 'app')){throw '旧工具清理仅限正式构建输出根'}
    # 源码已退出的框架及 tests 验证宿主不能被旧输出残留重新装入新封包。
    # 只删除精确、可审核的程序集文件；不处理数据目录或任意通配目标。
    if((Get-Item -LiteralPath $appRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw '产品输出根为重解析点'}
    $retired=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [void]$retired.Add('SunEyeVision.Workflow')
    foreach($directory in Get-ChildItem -LiteralPath (Join-Path $root 'tests') -Directory) {
        foreach($project in Get-ChildItem -LiteralPath $directory.FullName -Filter '*.csproj' -File) {
            if($project.BaseName.EndsWith('.Tests') -or $project.BaseName -like '*_wpftmp'){continue}
            [xml]$definition=Get-Content -LiteralPath $project.FullName -Raw
            # A declared fixture runtime component is not a disposable test entry host.
            if(@($definition.SelectNodes('/Project/PropertyGroup/ToolComponentRole')).Count){continue}
            $assembly=@($definition.SelectNodes('/Project/PropertyGroup/AssemblyName'))
            $name=if($assembly.Count){$assembly[-1].InnerText}else{$project.BaseName}
            if($name -match '^[A-Za-z0-9_.-]+$'){[void]$retired.Add($name)}
        }
    }
    if($EntryAssemblyName -notmatch '^[A-Za-z0-9_.-]+$'){throw '入口程序集名称无效'}
    [void]$retired.Remove($EntryAssemblyName)
    foreach($name in $retired) {
        foreach($suffix in '.dll','.exe','.pdb','.xml','.deps.json','.runtimeconfig.json') {
            $file=[IO.Path]::GetFullPath((Join-Path $appRoot ($name+$suffix)))
            if(-not $file.StartsWith($appRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw '退役产物路径越界'}
            if(Test-Path -LiteralPath $file -PathType Leaf) {
                if((Get-Item -LiteralPath $file -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw '退役产物为重解析点'}
                Remove-Item -LiteralPath $file -Force
                Write-Output ('已清理退役框架/验证宿主：'+[IO.Path]::GetFileName($file))
            }
        }
    }
    $pluginRoot=[IO.Path]::GetFullPath((Join-Path $appRoot 'plugins'))
    if(-not $pluginRoot.StartsWith($appRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw '旧工具输出目录越界'}
    if(Test-Path -LiteralPath $pluginRoot) {
        foreach($file in Get-ChildItem -LiteralPath $pluginRoot -Recurse -Force) {if($file.Attributes -band [IO.FileAttributes]::ReparsePoint){throw '旧工具输出包含重解析点，拒绝递归清理'}}
        if((Get-Item -LiteralPath $pluginRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw '旧工具输出根为重解析点'}
        Remove-Item -LiteralPath $pluginRoot -Recurse -Force
        Write-Output '已清理正式输出中的旧SDK工具目录；源码保持。'
    }
}
