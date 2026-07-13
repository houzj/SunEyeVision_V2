# ScriptRefs 初始化脚本
# 预置 3 个必备 DLL 到 plugins/SunEyeVision.Tool.Script/ScriptRefs/
# 严格模式：任意缺失 exit 1（rule-008 报错优先）
# 仅复制，不删除已存在的额外 DLL（用户自定义 DLL 安全）

$scriptDir = $PSScriptRoot
$repoRoot = Split-Path -Parent $scriptDir
$baseDir = Join-Path (Join-Path $repoRoot "dev") "Debug"
$pluginDir = Join-Path $baseDir "plugins\SunEyeVision.Tool.Script"
$targetDir = Join-Path $pluginDir "ScriptRefs"
$winFormsRefDir = "C:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref\10.0.8\ref\net10.0"

if (-not (Test-Path $baseDir)) {
    Write-Host "Main program directory not found: $baseDir (please build first)" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $pluginDir)) {
    Write-Host "Plugin directory not found: $pluginDir (please build first)" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

# 3 个必备程序集（非 .NET facade，facade 由 Roslyn 自动解析）
$deploy = @(
    @{Name="halcondotnetxl.dll";        Src=Join-Path $baseDir "halcondotnetxl.dll"}
    @{Name="OpenCvSharp.dll";           Src=Join-Path $baseDir "OpenCvSharp.dll"}
    @{Name="System.Windows.Forms.dll";  Src=Join-Path $winFormsRefDir "System.Windows.Forms.dll"}
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
        Write-Host "  [MISS] $($item.Name) (expected: $src)" -ForegroundColor Red
        $errors++
    }
}

if ($errors -gt 0) {
    Write-Host "ScriptRefs init failed: $errors DLL(s) missing (strict mode, fail-fast)" -ForegroundColor Red
    exit 1
}

Write-Host "ScriptRefs init completed: $copied DLL(s) -> $targetDir" -ForegroundColor Cyan
