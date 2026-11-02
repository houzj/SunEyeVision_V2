[CmdletBinding()]
param([Parameter(Mandatory)][string]$ProductRoot,[Parameter(Mandatory)][string]$ProbeDll,[Parameter(Mandatory)][string]$CaseRoot,[Parameter(Mandatory)][string]$LogPath)
$ErrorActionPreference='Stop'
$product=(Resolve-Path -LiteralPath $ProductRoot).Path
$probe=(Resolve-Path -LiteralPath $ProbeDll).Path
New-Item -ItemType Directory -Force -Path $CaseRoot|Out-Null
$launch=[Diagnostics.ProcessStartInfo]::new('dotnet')
$launch.UseShellExecute=$false;$launch.CreateNoWindow=$true;$launch.RedirectStandardOutput=$true;$launch.RedirectStandardError=$true
$launch.WorkingDirectory=$product;$launch.ArgumentList.Add('exec');$launch.ArgumentList.Add($probe)
$launch.Environment['SUNEYE_ROOT_TEST_PAYLOAD']=$product;$launch.Environment['S2_CASE_ROOT']=[IO.Path]::GetFullPath($CaseRoot)
$process=[Diagnostics.Process]::Start($launch)
$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
if(-not $process.WaitForExit(75000)){$process.Kill($true);$process.WaitForExit();throw '根宿主探针超时'}
[IO.File]::WriteAllText([IO.Path]::GetFullPath($LogPath),$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult(),[Text.UTF8Encoding]::new($false))
exit $process.ExitCode
