[CmdletBinding()]
param(
    [string]$PackageRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$RuntimeAssembly = "runtime/plugins/RuntimePlugin.Template/net9.0-windows/RuntimePlugin.Template.dll",
    [string]$EditorAssembly = "runtime/plugins/WpfTool.Template/net9.0-windows/WpfTool.Template.dll"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$root = (Resolve-Path $PackageRoot).Path
$sdk = Join-Path $root "sdk/net9.0"
if (-not (Test-Path (Join-Path $sdk "SunEyeVision.Plugin.Contracts.dll"))) { throw "SDK 合同 DLL 不存在：$sdk" }
foreach ($source in Get-ChildItem (Join-Path $root "templates") -Recurse -File |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' -and $_.Extension -in '.cs','.csproj','.props','.targets','.md' }) {
    $text = [System.IO.File]::ReadAllText($source.FullName, [System.Text.Encoding]::UTF8)
    if ($text -match 'SunEyeVision\.Plugin\.SDK|src[\\/]Plugin\.SDK') {
        throw "模板包含已退役 Plugin.SDK 引用：$($source.FullName)"
    }
}
[Reflection.Assembly]::LoadFrom((Join-Path $sdk "SunEyeVision.Plugin.Contracts.dll")) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $sdk "SunEyeVision.Foundation.dll")) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $sdk "SunEyeVision.Plugin.Runtime.dll")) | Out-Null

function Read-Component([string]$relativePath, [string]$role) {
    $path = Join-Path $root $relativePath
    if (-not (Test-Path $path)) { throw "组件程序集不存在：$path" }
    $assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $path).Path)
    $attribute = @($assembly.GetCustomAttributes($false) | Where-Object {
        $_.GetType().FullName -eq "SunEyeVision.Plugin.Contracts.ToolComponentAttribute"
    })
    if ($attribute.Count -ne 1) { throw "组件必须且只能声明一个 ToolComponentAttribute：$relativePath" }
    $item = $attribute[0]
    if ($item.Metadata.Role.ToString() -ne $role) { throw "组件角色不匹配：$relativePath" }
    if ($item.Metadata.ContractMajor -ne 6 -or $item.Metadata.ContractMinor -ne 0) { throw "组件合同版本不匹配：$relativePath" }
    $entry = [Type]::GetType($item.EntryType, $false)
    if ($null -eq $entry) {
        $entry = $assembly.GetType($item.EntryType, $false)
    }
    if ($null -eq $entry -and $role -ne "Editor") { throw "组件入口类型不存在：$($item.EntryType)" }
    [pscustomobject]@{ Path=$relativePath; ComponentId=$item.Metadata.ComponentId; ToolId=$item.Metadata.ToolId; Role=$item.Metadata.Role.ToString(); EntryType=$item.EntryType }
}

$runtime = Read-Component $RuntimeAssembly "Runtime"
$editor = Read-Component $EditorAssembly "Editor"
if ($runtime.ToolId -ne $editor.ToolId) { throw "Runtime 与 Editor 工具身份不一致。" }
$runtimeMeta = [SunEyeVision.Plugin.Contracts.ToolComponentMetadata]::new(
    $runtime.ComponentId, $runtime.ToolId,
    [SunEyeVision.Plugin.Contracts.ToolComponentRole]::Runtime,
    6, 0)
$editorMeta = [SunEyeVision.Plugin.Contracts.ToolComponentMetadata]::new(
    $editor.ComponentId, $editor.ToolId,
    [SunEyeVision.Plugin.Contracts.ToolComponentRole]::Editor,
    6, 0, $runtime.ToolId)
$metadata = [System.Collections.Generic.List[SunEyeVision.Plugin.Contracts.ToolComponentMetadata]]::new()
$metadata.Add($runtimeMeta)
$metadata.Add($editorMeta)
[SunEyeVision.Plugin.Runtime.ToolComponentCatalog]::Prepare($metadata) | Out-Null
Write-Output ("PASS 外部组件身份：Runtime={0} Editor={1} ToolId={2}" -f $runtime.ComponentId,$editor.ComponentId,$runtime.ToolId)
