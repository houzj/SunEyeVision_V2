# Build Runtime from main project and copy to PluginDevKit
# Usage: .\build-runtime.ps1 [-Configuration Release]

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Path configuration
$RootDir = Split-Path -Parent $PSScriptRoot
$MainProjectDir = Split-Path -Parent $RootDir
$RuntimeOutputDir = Join-Path $RootDir "runtime"

Write-Host "=== Build SunEyeVision Runtime ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration"
Write-Host "Main project dir: $MainProjectDir"
Write-Host "Runtime output dir: $RuntimeOutputDir"

# Create runtime output directory
if (-not (Test-Path $RuntimeOutputDir)) {
    New-Item -ItemType Directory -Path $RuntimeOutputDir -Force | Out-Null
}

# Build main application
$MainProject = Join-Path $MainProjectDir "src\UI\SunEyeVision.UI.csproj"
if (-not (Test-Path $MainProject)) {
    $PossibleProjects = @(
        "src\SunEyeVision\SunEyeVision.csproj",
        "src\UI\SunEyeVision.UI.csproj",
        "src\App\SunEyeVision.csproj"
    )
    
    foreach ($path in $PossibleProjects) {
        $fullPath = Join-Path $MainProjectDir $path
        if (Test-Path $fullPath) {
            $MainProject = $fullPath
            break
        }
    }
}

Write-Host "Building main project: $MainProject" -ForegroundColor Yellow
dotnet build $MainProject -c $Configuration --no-incremental

if ($LASTEXITCODE -ne 0) {
    throw "Main project build failed"
}

# Copy runtime files
$MainBinDir = if ($Configuration -eq 'Debug') {
    Join-Path $MainProjectDir 'dev\Debug'
} else {
    Join-Path $MainProjectDir 'Release\SunEyeVision_v1.0.0\SunEyeVision'
}

Write-Host "Copying runtime files..." -ForegroundColor Yellow

# Clean old runtime files (keep plugins directory)
Get-ChildItem $RuntimeOutputDir -Exclude "plugins" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# Copy all files (exclude plugins source directory)
$ExcludeDirs = @("plugins", "ref", "Help")
Get-ChildItem $MainBinDir | Where-Object { 
    $_.Name -notin $ExcludeDirs 
} | ForEach-Object {
    if ($_.PSIsContainer) {
        $dest = Join-Path $RuntimeOutputDir $_.Name
        Copy-Item $_.FullName $dest -Recurse -Force
    } else {
        Copy-Item $_.FullName $RuntimeOutputDir -Force
    }
    Write-Host "Copied: $($_.Name)" -ForegroundColor Green
}

if ($Configuration -eq 'Debug') {
    Remove-Item (Join-Path $RuntimeOutputDir 'release-payload.json') -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $RuntimeOutputDir 'ToolGroups.xml') -Force -ErrorAction SilentlyContinue
}

# Ensure plugins directory exists
$PluginsDir = Join-Path $RuntimeOutputDir "plugins"
if (-not (Test-Path $PluginsDir)) {
    New-Item -ItemType Directory -Path $PluginsDir -Force | Out-Null
}

Write-Host "`n=== Runtime Build Complete ===" -ForegroundColor Cyan
Write-Host "Runtime files at: $RuntimeOutputDir"

# List main files
Write-Host "`nMain files:" -ForegroundColor Yellow
Get-ChildItem $RuntimeOutputDir -Filter "*.exe" | Format-Table Name, Length, LastWriteTime
Get-ChildItem $RuntimeOutputDir -Filter "SunEyeVision*.dll" | Format-Table Name, Length, LastWriteTime
