# ScriptRefs 清理与迁移
# 新方案：框架程序集和项目程序集由默认 ALC 自动提供，不再需要复制到 ScriptRefs。
# ScriptRefs 目录仅保留用户通过程序集管理器手动添加的第三方 DLL。

$targetDir = "d:\MyWork\SunEyeVision_V2-script\dev\Debug\data\ScriptRefs"
if (-not (Test-Path $targetDir)) {
    Write-Host "ScriptRefs 目录不存在，无需清理。" -ForegroundColor Cyan
    exit 0
}

# === 清理旧框架程序集（由 ALC 自动提供） ===
$systemPrefixes = @(
    "System.", "Microsoft.", "netstandard", "mscorlib",
    "SunEyeVision.",  # 项目程序集也由 ALC 自动提供
    "Presentation", "WindowsBase", "DirectWriteForwarder",
    "Accessibility", "ReachFramework", "SMDiagnostics",
    "UIAutomation", "System.Core", "System.Data", "System.Drawing",
    "System.Numerics", "System.Xml", "System.Configuration",
    "System.ComponentModel", "System.Collections", "System.IO",
    "System.Linq", "System.Text", "System.Threading", "System.Runtime",
    "System.Console", "System.Diagnostics", "System.Globalization",
    "System.Net", "System.Security", "System.Reflection"
)

$removedCount = 0
$fileList = Get-ChildItem $targetDir -Filter "*.dll"

foreach ($file in $fileList) {
    $name = $file.Name
    $shouldRemove = $false
    foreach ($prefix in $systemPrefixes) {
        if ($name.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            $shouldRemove = $true
            break
        }
    }
    if ($shouldRemove) {
        Remove-Item $file.FullName -Force
        Write-Host "  已清理: $name" -ForegroundColor Yellow
        $removedCount++
    }
}

if ($removedCount -eq 0) {
    Write-Host "未发现需要清理的程序集。ScriptRefs 已干净。" -ForegroundColor Green
} else {
    Write-Host "`n共清理 $removedCount 个旧程序集" -ForegroundColor Cyan
}

# === 显示剩余（用户自定义）程序集 ===
$remaining = Get-ChildItem $targetDir -Filter "*.dll"
if ($remaining.Count -gt 0) {
    Write-Host "`n剩余（用户自定义）程序集:" -ForegroundColor Green
    $remaining | Select-Object Name | Format-Table -AutoSize
} else {
    Write-Host "`nScriptRefs 目录为空（所有程序集由 ALC 自动提供）" -ForegroundColor Cyan
}

Write-Host "`n============================================" -ForegroundColor DarkGray
Write-Host "新方案说明" -ForegroundColor DarkGray
Write-Host "============================================" -ForegroundColor DarkGray
Write-Host "框架程序集和项目程序集已由运行时默认 ALC 自动提供。" -ForegroundColor DarkGray
Write-Host "ScriptRefs 目录仅需保留用户通过程序集管理器添加的第三方 DLL。" -ForegroundColor DarkGray
Write-Host "如需添加自定义 DLL，请在脚本编辑器中点击『程序集引用管理』。" -ForegroundColor DarkGray
