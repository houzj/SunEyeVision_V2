# 构建面向外部应用的 Platform SDK 发布包。
# 外部工程只引用公开合同和宿主适配器；Application、Execution、Persistence 与 UI 类型仍留在适配器内部。
param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$kitRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $kitRoot
$contractProject = Join-Path $repoRoot "src\Platform.Sdk.Contracts\SunEyeVision.Platform.Sdk.Contracts.csproj"
$adapterProject = Join-Path $repoRoot "src\Platform.Sdk\SunEyeVision.Platform.Sdk.csproj"
$output = Join-Path $kitRoot "sdk\platform\net9.0"
New-Item -ItemType Directory -Path $output -Force | Out-Null
dotnet build $contractProject -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "Platform SDK 合同构建失败。" }
dotnet build $adapterProject -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "Platform SDK 宿主适配器构建失败。" }
$bin = Join-Path $repoRoot "Release\\SunEyeVision_v1.0.0\\SunEyeVision"
foreach ($file in @(
    "SunEyeVision.Platform.Sdk.dll", "SunEyeVision.Platform.Sdk.xml", "SunEyeVision.Platform.Sdk.deps.json",
    "SunEyeVision.Platform.Sdk.Contracts.dll", "SunEyeVision.Platform.Sdk.Contracts.xml",
    "SunEyeVision.Application.dll", "SunEyeVision.Domain.dll", "SunEyeVision.Execution.dll",
    "SunEyeVision.Foundation.dll", "SunEyeVision.Infrastructure.Persistence.dll", "SunEyeVision.Persistence.dll",
    "SunEyeVision.Plugin.Contracts.dll", "SunEyeVision.Plugin.Runtime.dll",
    "OpenCvSharp.dll", "OpenCvSharpExtern.dll", "opencv_videoio_ffmpeg4130_64.dll",
    "Microsoft.Extensions.DependencyInjection.dll", "Microsoft.Extensions.DependencyInjection.Abstractions.dll",
    "Microsoft.Extensions.Options.dll", "Microsoft.Extensions.Primitives.dll", "log4net.dll",
    "System.Configuration.ConfigurationManager.dll", "System.Security.Permissions.dll",
    "System.Security.Cryptography.ProtectedData.dll")) {
    $source = Join-Path $bin $file
    if (-not (Test-Path $source)) { throw "Platform SDK 输出不存在：$source" }
    Copy-Item $source $output -Force
}
@{
    sdk = "SunEyeVision.Platform.Sdk"
    contract = "1.0"
    targetFramework = "net9.0"
    implementationStatus = "host-adapter-and-contract"
    vmReference = "VM.PlatformSDKCS / VM.Core"
} | ConvertTo-Json | Set-Content (Join-Path $output "platform-sdk-manifest.json") -Encoding utf8
Write-Host "Platform SDK 合同与宿主适配器已发布到 $output"



