# ScriptRefs 部署脚本
# 严格模式：从主程序目录 + .NET ref pack 复制 7 个关键 DLL 到 ScriptRefs
# 任何缺失即 exit 1（rule-008 报错优先）
# 仅复制，不删除已存在的额外 DLL（用户自定义 DLL 安全）

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$baseDir = Join-Path $scriptDir "dev\Debug"
$targetDir = Join-Path $baseDir "data\ScriptRefs"
$refDir = "C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\10.0.8\ref\net10.0"
$winFormsRefDir = "C:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref\10.0.8\ref\net10.0"

if (-not (Test-Path $baseDir)) {
    Write-Host "主程序目录不存在: $baseDir（请先构建）" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

# 7 个关键 DLL（严格模式：任意缺失 exit 1）
$deploy = @(
    @{Name="halcondotnetxl.dll";        Src=Join-Path $baseDir "halcondotnetxl.dll"}
    @{Name="OpenCvSharp.dll";           Src=Join-Path $baseDir "OpenCvSharp.dll"}
    @{Name="mscorlib.dll";              Src=Join-Path $refDir "mscorlib.dll"}
    @{Name="System.dll";                Src=Join-Path $refDir "System.dll"}
    @{Name="System.Core.dll";           Src=Join-Path $refDir "System.Core.dll"}
    @{Name="System.Windows.Forms.dll";  Src=Join-Path $winFormsRefDir "System.Windows.Forms.dll"}
    @{Name="netstandard.dll";           Src=Join-Path $refDir "netstandard.dll"}
)

$copied = 0
$errors = 0
foreach ($item in $deploy) {
    $src = $item.Src
    $dst = Join-Path $targetDir $item.Name
    if (Test-Path $src) {
        Copy-Item $src $dst -Force
        Write-Host "  [OK] $($item.Name)" -ForegroundColor Green
        $copied++
    } else {
        Write-Host "  [缺失] $($item.Name) (预期路径: $src)" -ForegroundColor Red
        $errors++
    }
}

if ($errors -gt 0) {
    Write-Host "ScriptRefs 部署失败: $errors 个 DLL 缺失（严格模式，报错优先）" -ForegroundColor Red
    exit 1
}

Write-Host "ScriptRefs 部署完成: $copied 个 DLL" -ForegroundColor Cyan
